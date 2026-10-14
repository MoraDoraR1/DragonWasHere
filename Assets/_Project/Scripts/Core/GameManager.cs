using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Skills;
using DWH.InGame.Starter;
using DWH.InGame.Stage;
using DWH.OutGame;
using DWH.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DWH.Core
{
    public enum GameState { Title, OutGame, Stage, Bonus, Result }

    /// <summary>
    /// 게임 상태 머신. Title → OutGame → Stage(스타터 포함) → (Bonus) → Result → OutGame.
    /// 타이틀: 새 게임(기록이 있으면 경고 후 초기화) → 바로 1-1 출격 / 이어하기 → OutGame. 일시정지의 "타이틀로 이동"은 이번 판을 기록하지 않는다.
    /// 일반 스테이지를 클리어한 순간 기력이 const.bonus_min_stamina 이상이면 보너스로 간다 (가이드 6.6).
    /// 저장 시점 (가이드 6.12): 정산 직후, 구매 직후, 환생 직후, "바로 다시 출격" 직전, 언어 변경.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameDatabase database;
        public ArtCatalog art;
        public UiTheme uiTheme;
        /// <summary>일반 스테이지 앞의 스테이지 스타터. 자동 진행 테스트와 봇은 끈다</summary>
        public bool enableStarter = true;
        /// <summary>스타터 입력 (없으면 마우스·스페이스). 테스트가 대본 입력을 넣는다</summary>
        public Func<IStarterInput> starterInputFactory;

        ISaveStorage storage;
        Func<Camera, IGuideInput> inputFactory;
        IGuideInput guideInput;
        Camera view;
        OutGameScreen outGame;
        ResultScreen resultScreen;
        BattleHud hud;
        PauseMenu pauseMenu;
        TitleScreen title;
        SettingsPopup settings;
        TutorialOverlay tutorial;
        float timeScaleBeforePause = 1f;
        double goldAtDeparture;
        float departTime;
        PlayerStats sessionStats;
        Dictionary<ActiveSkillId, int> activeLevels = new();
        Dictionary<ActiveSkillId, int> sessionUses = new();

        public static GameManager Instance { get; private set; }
        public SaveData Save { get; private set; }
        public GameState State { get; private set; }
        public BattleController Battle { get; private set; }
        public SessionResult LastResult { get; private set; }
        public UpgradeTree Upgrades { get; private set; }
        public bool IsInitialized => Save != null;
        public bool IsPaused { get; private set; }
        public OutGameScreen OutGame => outGame;
        public TitleScreen Title => title;
        public SettingsPopup Settings => settings;
        public TutorialOverlay Tutorial => tutorial;
        /// <summary>튜토리얼(최초 1회 안내)을 띄운다. 실제 실행(타이틀부터 시작)에서만 켜고 테스트·봇은 끈다</summary>
        public bool TutorialsEnabled { get; set; }

        public event Action<GameState> StateChanged;

        void Awake() => Instance = this;

        void Start()
        {
            if (!IsInitialized)
                Initialize(SaveSystem.DefaultStorage(), cam => new MouseGuideInput(cam), showTitle: true);
        }

        /// <summary>저장소와 입력을 바꿔 끼울 수 있게 분리했다. 테스트는 메모리 저장소와 자동 입력을 넣는다.</summary>
        /// <param name="showTitle">타이틀 화면부터 시작한다. 테스트·봇은 바로 아웃게임으로 간다</param>
        public void Initialize(ISaveStorage saveStorage, Func<Camera, IGuideInput> guideInputFactory, bool showTitle = false)
        {
            storage = saveStorage;
            inputFactory = guideInputFactory;
            UiText.Database = database;
            Save = SaveSystem.Load(storage, database.StageOrder[0]);
            // 이전 빌드 저장을 지금 테이블에 맞춘다 (없어진 스테이지·노드 정리). 빌드가 바뀌어도 진행을 잇는다
            if (SaveSystem.Migrate(Save, database)) SaveSystem.Save(storage, Save);
            // 게임을 켤 때는 언제나 영어로 시작한다 (10-14 사용자 요청). 실행 중에 바꾼 언어는 그 실행 동안(새 게임 포함) 유지된다
            Save.language = Localization.Code(Language.En);
            Localization.Current = Language.En;
            BattleCamera.ShakeEnabled = Save.settings.screenShake;
            view = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            // 화면 고정: 창 비율이 바뀌어도 16:9로 그리고 남는 곳은 검은 띠 (테스트·배치모드는 렌더 텍스처로 찍으므로 제외)
            if (!Application.isBatchMode && !view.TryGetComponent(out LetterboxCamera _)) view.gameObject.AddComponent<LetterboxCamera>();

            if (uiTheme != null) UiTheme.Active = uiTheme;
            Upgrades = new UpgradeTree(database, Save);
            BuildScreens();
            TutorialsEnabled = showTitle;
            if (tutorial == null) tutorial = TutorialOverlay.Create(this, database);
            Analytics.SessionStart(database, Save);
            if (showTitle) EnterTitle();
            else EnterOutGame();
        }

        /// <summary>HUD 모드 버튼: 지금 모드 표시와 누르면 바꾸기</summary>
        BattleHud WireHud(BattleHud h)
        {
            h.MouseModeProvider = () => guideInput is IGuideModeInput m ? m.MouseMode : Battle != null && Battle.Dragon.IsGuided;
            h.ToggleMode = ToggleGuideMode;
            return h;
        }

        /// <summary>
        /// 조작 모드 바꾸기: 마우스 유도 ↔ 자동 이동 (Space 또는 HUD 왼쪽 아래 모드 버튼, 10-14).
        /// 전투 중에만, 스타터(Space로 낙하)·일시정지·튜토리얼 중에는 바꾸지 않는다. 바꾼 모드는 저장해 다음 판에도 쓴다
        /// </summary>
        public void ToggleGuideMode()
        {
            if (State is not (GameState.Stage or GameState.Bonus) || Battle == null || Battle.InStarter || IsPaused) return;
            if (tutorial != null && tutorial.IsOpen) return;
            if (guideInput is not IGuideModeInput m) return;
            m.MouseMode = !m.MouseMode;
            Save.settings.mouseGuide = m.MouseMode;
            SaveSystem.Save(storage, Save);
        }

        /// <summary>지금 저장 (튜토리얼 완료 등)</summary>
        public void SaveNow() => SaveSystem.Save(storage, Save);

        /// <summary>이어할 기록이 있는지 (한 판이라도 했거나 환생했으면)</summary>
        public bool HasProgress => Save.playCount > 0 || Save.rebirthCount > 0;

        void BuildScreens()
        {
            if (outGame != null) Destroy(outGame.gameObject);
            if (resultScreen != null) Destroy(resultScreen.gameObject);
            if (pauseMenu != null) Destroy(pauseMenu.gameObject);
            if (title != null) Destroy(title.gameObject);
            if (settings != null) Destroy(settings.gameObject);
            outGame = OutGameScreen.Create(database, art, Upgrades, new OutGameActions
            {
                Depart = StartStage,
                BuyNode = BuyUpgrade,
                Rebirth = PerformRebirth,
                BuyTrait = BuyTrait,
                BuyActive = BuyActive,
                RebirthUnlocked = () => Rebirth.IsUnlocked(database, Save),
                ToggleLanguage = ToggleLanguage,
            });
            resultScreen = ResultScreen.Create(database, ConfirmResult, RetryFromResult);
            pauseMenu = PauseMenu.Create(() => SetPaused(false), OpenSettings, ReturnToTitle, EndSession, () => Battle != null);
            title = TitleScreen.Create(database, art, new TitleActions
            {
                NewGame = StartNewGame,
                Continue = ContinueGame,
                OpenSettings = OpenSettings,
            });
            settings = SettingsPopup.Create(new SettingsActions
            {
                ToggleLanguage = ToggleLanguage,
                CanChangeLanguage = () => true,
                SetScreenShake = on =>
                {
                    Save.settings.screenShake = on;
                    SaveSystem.Save(storage, Save);
                },
            });
        }

        void EnterTitle()
        {
            resultScreen.Hide();
            outGame.Hide();
            title.Show(HasProgress);
            SetState(GameState.Title);
        }

        public void OpenSettings() => settings.Show();

        /// <summary>타이틀 "이어하기": 저장된 기록으로 아웃게임(둥지)에 간다</summary>
        public void ContinueGame()
        {
            if (State != GameState.Title || !HasProgress) return;
            title.Hide();
            EnterOutGame();
        }

        /// <summary>
        /// 타이틀 "새 게임": 기록을 지우고(경고 팝업은 TitleScreen이 띄운다) 바로 1-1로 출격한다.
        /// 언어·환경설정·사용자 ID는 남긴다.
        /// </summary>
        public void StartNewGame()
        {
            if (State != GameState.Title) return;
            if (HasProgress)
            {
                var keep = Save;
                storage.Delete();
                Save = SaveSystem.Load(storage, database.StageOrder[0]);
                Save.language = keep.language;
                Save.userId = keep.userId;
                Save.settings = keep.settings;
                SaveSystem.Save(storage, Save);
                Upgrades = new UpgradeTree(database, Save);
                BuildScreens();
            }
            // 새 게임은 언제나 튜토리얼을 처음부터 다시 보여 준다 (기록이 없어도 지난번에 본 튜토리얼이 남아 있을 수 있다, 10-14)
            if (Save.tutorialsDone.Count > 0)
            {
                Save.tutorialsDone.Clear();
                SaveSystem.Save(storage, Save);
            }
            title.Hide();
            SetState(GameState.OutGame);
            StartStage(false, fromTitle: true);
        }

        /// <summary>일시정지 "세션 끝내기": 이번 판을 끝내고 지금까지 모은 골드로 정산해 결과창을 띄운다 (업그레이드하러 갈 수 있게, 10-14)</summary>
        public void EndSession()
        {
            if (State is not (GameState.Stage or GameState.Bonus) || Battle == null) return;
            SetPaused(false);
            Battle.Retire();
        }

        /// <summary>일시정지 "타이틀로 이동": 이번 판은 정산하지 않고 버린다</summary>
        public void ReturnToTitle()
        {
            if (Battle != null) CloseBattle();
            Time.timeScale = 1f;
            settings.Hide();
            EnterTitle();
        }

        void EnterOutGame()
        {
            resultScreen.Hide();
            outGame.Show(Save);
            Analytics.OutgameEnter();
            SetState(GameState.OutGame);
        }

        /// <summary>"출격": 현재 도전 스테이지로 간다. 스테이지를 골라서 다시 하는 기능은 없다 (가이드 1장)</summary>
        public void StartStage() => StartStage(false);

        /// <param name="retry">결과 화면 "바로 다시 출격"으로 왔는지 (지표 entry)</param>
        void StartStage(bool retry, bool fromTitle = false)
        {
            if (State != GameState.OutGame) return;
            if (!retry && !fromTitle) Analytics.OutgameExit(database, Save, Upgrades.AffordableCount());
            outGame.Hide();
            goldAtDeparture = Save.goldCoin;
            departTime = Time.time;
            sessionStats = Upgrades.Stats();
            // 액티브 스킬 사용 횟수는 출격할 때마다 채운다. 보너스 스테이지는 이어서 쓴다 (가이드 6.10)
            activeLevels = database.activeSkills.ToDictionary(a => a.Id, a => Rebirth.ActiveLevel(Save, a));
            sessionUses = ActiveSkillSystem.FullUses(activeLevels);
            StartBattle(Save.currentStage, BattleMode.Normal, null, 0);
            Analytics.RunStart(database, Save, retry);
            SetState(GameState.Stage);
        }

        void StartBattle(string stageId, BattleMode mode, DragonVitals carriedVitals, double goldBefore)
        {
            var starter = enableStarter && mode == BattleMode.Normal ? starterInputFactory?.Invoke() ?? new MouseStarterInput() : null;
            guideInput = inputFactory(view);
            // 조작 모드(마우스 유도 / 자동 이동)는 저장해 둔 것으로 시작한다 (처음은 마우스 유도)
            if (guideInput is IGuideModeInput modeInput) modeInput.MouseMode = Save.settings.mouseGuide;
            Battle = BattleController.Create(database, art, stageId, view, guideInput, mode, carriedVitals, sessionStats, starter);
            Battle.Ended += OnBattleEnded;
            Battle.Actives.Configure(activeLevels, sessionUses);
            // 정산 후에는 보유 골드에 세션 골드가 더해지므로, HUD는 출발 시점 보유량 + 앞 구간 골드 + 현재 구간 획득량으로 표시한다
            double shown = goldAtDeparture + goldBefore;
            hud = WireHud(BattleHud.Create(Battle, () => shown, art, TogglePause, mode == BattleMode.Normal && Save.playCount < 2 && !TutorialsEnabled, goldBefore));
            // 전투 중 언어를 바꾸면 HUD를 새 언어로 다시 만든다 (첫 판 안내는 다시 띄우지 않는다)
            rebuildHud = () => WireHud(BattleHud.Create(Battle, () => shown, art, TogglePause, false, goldBefore));
        }

        Func<BattleHud> rebuildHud;

        void OnBattleEnded(BattleController battle)
        {
            battle.Drops.CollectAll();

            if (battle.Mode == BattleMode.Normal)
            {
                bool cleared = battle.State == BattleState.Cleared;
                LastResult = new SessionResult
                {
                    stageId = battle.Stage.stageId,
                    cleared = cleared,
                    end = cleared ? SessionEnd.Cleared : battle.Retired ? SessionEnd.Retired : battle.Dragon.Vitals.IsDead ? SessionEnd.Defeated : SessionEnd.Exhausted,
                    earnedGold = battle.Drops.CollectedGold,
                    destroyedBuildings = battle.DestroyedCount,
                    totalBuildings = battle.Buildings.Count,
                    maxCombo = battle.Drops.MaxCombo,
                    hpLeft = battle.Dragon.Vitals.Hp,
                    staminaLeft = battle.Dragon.Vitals.Stamina,
                };

                // 보물 산더미: 클리어 시 이번 스테이지 골드 × v 추가 (효과 12)
                if (cleared && sessionStats.ClearGoldBonus > 0)
                {
                    LastResult.clearBonusGold = LastResult.earnedGold * sessionStats.ClearGoldBonus;
                    LastResult.earnedGold += LastResult.clearBonusGold;
                }

                if (battle.BonusEligible)
                {
                    // 보너스 스테이지: 체력·기력·피로를 이어받는다
                    LastResult.bonusEntered = true;
                    var vitals = battle.Dragon.Vitals;
                    CloseBattle();
                    StartBattle(LastResult.stageId, BattleMode.Bonus, vitals, LastResult.earnedGold);
                    SetState(GameState.Bonus);
                    return;
                }
            }
            else
            {
                LastResult.bonusGold = battle.Drops.CollectedGold;
                LastResult.bonusDestroyed = battle.DestroyedCount;
                LastResult.bonusTotal = battle.Buildings.Count;
                LastResult.maxCombo = Math.Max(LastResult.maxCombo, battle.Drops.MaxCombo);
                LastResult.earnedGold += LastResult.bonusGold;
            }

            LastResult.playSeconds = Time.time - departTime;
            bool rebirthBefore = Rebirth.IsUnlocked(database, Save);
            Progression.Settle(database, Save, LastResult);
            Save.playCount++;
            SaveSystem.Save(storage, Save);

            Analytics.RunEnd(database, Save, LastResult);
            if (!rebirthBefore && Rebirth.IsUnlocked(database, Save)) Analytics.RebirthUnlocked(database, Save);
            if (LastResult.gameCompleted) Analytics.GameComplete(database, Save);

            resultScreen.Show(LastResult, Save, Upgrades.AffordableCount(), Save.playCount > 1);
            SetState(GameState.Result);
        }

        void CloseBattle()
        {
            SetPaused(false);
            Destroy(hud.gameObject);
            Destroy(Battle.gameObject);
            Battle = null;
        }

        /// <summary>업그레이드 구매. 구매 수량 ×1 / ×10 / 최대. 산 레벨 수를 돌려준다. 구매 직후 저장한다</summary>
        public int BuyUpgrade(UpgradeRow node, int quantity)
        {
            if (State != GameState.OutGame) return 0;
            var before = Growth.Stage(database, Save.runSpentGold);
            double goldBefore = Save.goldCoin;
            int bought = Upgrades.BuyMany(node, quantity);
            if (bought == 0) return 0;
            SaveSystem.Save(storage, Save);
            Analytics.UpgradePurchase(database, Save, node, bought, goldBefore - Save.goldCoin);
            outGame.Refresh();
            // 누적 사용 골드로 성장 단계가 오르면 외형이 바뀐다 (가이드 6.11)
            var after = Growth.Stage(database, Save.runSpentGold);
            if (after.growthStage != before.growthStage)
                outGame.Celebrate(UiText.Format("ui_hud_growth", database.Text(after.nameStr)), "", art != null ? art.SpriteOrNull(after.resId) : null);
            return bought;
        }

        public bool BuyUpgrade(UpgradeRow node) => BuyUpgrade(node, 1) > 0;

        /// <summary>환생. 받은 포인트를 돌려준다. 아웃게임에서만, 해금된 뒤에만 된다</summary>
        public double PerformRebirth()
        {
            if (State != GameState.OutGame || !Rebirth.IsUnlocked(database, Save)) return 0;
            double points = Rebirth.Perform(database, Save);
            SaveSystem.Save(storage, Save);
            Analytics.RebirthPerformed(database, Save, points);
            outGame.Show(Save);
            return points;
        }

        public bool BuyTrait(string traitId)
        {
            if (State != GameState.OutGame || !Rebirth.BuyTrait(database, Save, traitId)) return false;
            SaveSystem.Save(storage, Save);
            outGame.Refresh();
            return true;
        }

        public bool BuyActive(ActiveSkillId id)
        {
            if (State != GameState.OutGame || !Rebirth.BuyActive(database, Save, id)) return false;
            SaveSystem.Save(storage, Save);
            outGame.Refresh();
            return true;
        }

        /// <summary>한/영 전환. 화면을 다시 만든다. 전투·일시정지·결과 화면에서도 된다 (환경설정, 10-13 사용자 요청)</summary>
        public void ToggleLanguage()
        {
            Localization.Current = Localization.Current == Language.Ko ? Language.En : Language.Ko;
            Save.language = Localization.Code(Localization.Current);
            SaveSystem.Save(storage, Save);
            int tab = outGame.Tab;
            bool settingsOpen = settings.IsOpen;
            BuildScreens();
            if (State == GameState.Title) title.Show(HasProgress);
            else if (State == GameState.OutGame)
            {
                outGame.Show(Save);
                outGame.SetTab(tab);
            }
            else
            {
                // 전투 중: HUD를 다시 만들고, 열려 있던 일시정지·결과 화면을 다시 띄운다
                outGame.Hide();
                if (hud != null && Battle != null && rebuildHud != null)
                {
                    Destroy(hud.gameObject);
                    hud = rebuildHud();
                }
                if (IsPaused) pauseMenu.Show();
                if (State == GameState.Result && LastResult != null)
                    resultScreen.Show(LastResult, Save, Upgrades.AffordableCount(), Save.playCount > 1);
            }
            if (settingsOpen) settings.Show();
        }

        /// <summary>결과 화면 "둥지로": 전투를 정리하고 아웃게임으로 돌아간다</summary>
        public void ConfirmResult()
        {
            if (State != GameState.Result) return;
            CloseBattle();
            EnterOutGame();
        }

        /// <summary>결과 화면 "바로 다시 출격": 저장한 뒤 아웃게임을 거치지 않고 도전 스테이지로 출발한다 (첫 판에서는 비활성)</summary>
        public void RetryFromResult()
        {
            if (State != GameState.Result || Save.playCount <= 1) return;
            SaveSystem.Save(storage, Save);
            CloseBattle();
            resultScreen.Hide();
            SetState(GameState.OutGame);
            StartStage(true);
        }

        /// <summary>일시정지는 전투 중에만 걸 수 있다. 종료 슬로모션 중에는 걸지 않는다</summary>
        public void SetPaused(bool paused)
        {
            bool canPause = (State is GameState.Stage or GameState.Bonus) && Battle != null && Battle.State == BattleState.Playing;
            if (paused == IsPaused || (paused && !canPause)) return;
            if (paused && tutorial != null && tutorial.IsOpen) return;   // 튜토리얼이 이미 멈춰 두었다
            IsPaused = paused;
            if (paused)
            {
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                pauseMenu.Show();
            }
            else
            {
                Time.timeScale = timeScaleBeforePause;
                pauseMenu.Hide();
            }
        }

        public void TogglePause() => SetPaused(!IsPaused);

        void OnApplicationFocus(bool focused)
        {
            // 창 포커스를 잃으면 자동으로 일시정지한다. 배치모드(테스트)에서는 하지 않는다
            if (!focused && IsInitialized && !Application.isBatchMode) SetPaused(true);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (tutorial != null && tutorial.IsOpen) return;   // 튜토리얼 중에는 단축키(일시정지·출격·확인)를 막는다
            bool escape = keyboard.escapeKey.wasPressedThisFrame;
            // 열린 팝업이 있으면 Esc는 팝업부터 닫는다
            if (escape && settings.IsOpen)
            {
                settings.Hide();
                return;
            }
            if (escape && title.WarningOpen)
            {
                title.HideWarning();
                return;
            }
            if (escape || keyboard.pKey.wasPressedThisFrame)
                TogglePause();
            if (IsPaused || settings.IsOpen) return;
            // 전투 중 Space = 조작 모드 바꾸기 (스타터 중에는 스타터가 Space를 낙하 입력으로 쓴다)
            if (keyboard.spaceKey.wasPressedThisFrame && State is GameState.Stage or GameState.Bonus && Battle != null && !Battle.InStarter)
                ToggleGuideMode();
            bool confirm = keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
            if (confirm)
            {
                if (State == GameState.Title) title.SubmitDefault();
                else if (State == GameState.OutGame) StartStage();
                else if (State == GameState.Result) ConfirmResult();
            }
            if (keyboard.rKey.wasPressedThisFrame && State == GameState.Result)
                RetryFromResult();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 개발용: 아웃게임에서 PageUp/PageDown으로 도전 스테이지를 바꾸고, G로 골드를 받는다
            if (State == GameState.OutGame)
            {
                int step = keyboard.pageUpKey.wasPressedThisFrame ? 1 : keyboard.pageDownKey.wasPressedThisFrame ? -1 : 0;
                if (step != 0) DebugShiftStage(step);
                if (keyboard.gKey.wasPressedThisFrame)
                    DebugAddGold(keyboard.ctrlKey.isPressed ? 100000000 : keyboard.shiftKey.isPressed ? 100000 : 1000);
            }
#endif
        }

        /// <summary>개발용 골드 지급. 회차 누적 획득 골드(환생 포인트 기준)는 늘리지 않는다.</summary>
        public void DebugAddGold(double amount)
        {
            Save.goldCoin += amount;
            SaveSystem.Save(storage, Save);
            outGame.Refresh();
        }

        /// <summary>개발용 도전 스테이지 이동. 최고 스테이지는 도달한 만큼 갱신한다(환생 해금 확인용).</summary>
        public void DebugShiftStage(int step)
        {
            var order = database.StageOrder;
            int index = database.OrderIndex(Save.currentStage);
            Save.currentStage = order[Mathf.Clamp(index + step, 0, order.Count - 1)];
            if (database.OrderIndex(Save.currentStage) > database.OrderIndex(Save.highestStage)) Save.highestStage = Save.currentStage;
            outGame.Show(Save);
        }

        void OnApplicationQuit()
        {
            if (IsInitialized) SaveSystem.Save(storage, Save);
        }

        void SetState(GameState next)
        {
            State = next;
            Analytics.SceneChanged(next);
            StateChanged?.Invoke(next);
        }
    }
}
