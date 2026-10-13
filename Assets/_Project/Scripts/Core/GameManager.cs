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

        ISaveStorage storage;
        Func<Camera, IGuideInput> inputFactory;
        Camera view;
        OutGameScreen outGame;
        ResultScreen resultScreen;
        BattleHud hud;
        PauseMenu pauseMenu;
        TitleScreen title;
        SettingsPopup settings;
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

        public event Action<GameState> StateChanged;

        void Awake() => Instance = this;

        void Start()
        {
            if (!IsInitialized)
                Initialize(SaveSystem.DefaultStorage(), cam => new MouseGuideInput(cam, (float)database.Const("mouse_idle_sec")), showTitle: true);
        }

        /// <summary>저장소와 입력을 바꿔 끼울 수 있게 분리했다. 테스트는 메모리 저장소와 자동 입력을 넣는다.</summary>
        /// <param name="showTitle">타이틀 화면부터 시작한다. 테스트·봇은 바로 아웃게임으로 간다</param>
        public void Initialize(ISaveStorage saveStorage, Func<Camera, IGuideInput> guideInputFactory, bool showTitle = false)
        {
            storage = saveStorage;
            inputFactory = guideInputFactory;
            UiText.Database = database;
            Save = SaveSystem.Load(storage, database.StageOrder[0]);
            if (database.OrderIndex(Save.currentStage) < 0) Save.currentStage = database.StageOrder[0];
            if (database.OrderIndex(Save.highestStage) < 0) Save.highestStage = Save.currentStage;
            Localization.Current = Localization.Parse(Save.language);
            BattleCamera.ShakeEnabled = Save.settings.screenShake;
            view = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();

            if (uiTheme != null) UiTheme.Active = uiTheme;
            Upgrades = new UpgradeTree(database, Save);
            BuildScreens();
            Analytics.SessionStart(database, Save);
            if (showTitle) EnterTitle();
            else EnterOutGame();
        }

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
            pauseMenu = PauseMenu.Create(() => SetPaused(false), OpenSettings, ReturnToTitle);
            title = TitleScreen.Create(database, art, new TitleActions
            {
                NewGame = StartNewGame,
                Continue = ContinueGame,
                OpenSettings = OpenSettings,
            });
            settings = SettingsPopup.Create(new SettingsActions
            {
                ToggleLanguage = ToggleLanguage,
                CanChangeLanguage = () => State is GameState.Title or GameState.OutGame,
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
            title.Hide();
            SetState(GameState.OutGame);
            StartStage(false, fromTitle: true);
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
            var starter = enableStarter && mode == BattleMode.Normal ? new MouseStarterInput() : null;
            Battle = BattleController.Create(database, art, stageId, view, inputFactory(view), mode, carriedVitals, sessionStats, starter);
            Battle.Ended += OnBattleEnded;
            Battle.Actives.Configure(activeLevels, sessionUses);
            // 정산 후에는 보유 골드에 세션 골드가 더해지므로, HUD는 출발 시점 보유량 + 앞 구간 골드 + 현재 구간 획득량으로 표시한다
            double shown = goldAtDeparture + goldBefore;
            hud = BattleHud.Create(Battle, () => shown, art, TogglePause, mode == BattleMode.Normal && Save.playCount < 2, goldBefore);
        }

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
                    end = cleared ? SessionEnd.Cleared : battle.Dragon.Vitals.IsDead ? SessionEnd.Defeated : SessionEnd.Exhausted,
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

        /// <summary>한/영 전환. 화면을 다시 만든다 (타이틀·아웃게임에서만)</summary>
        public void ToggleLanguage()
        {
            if (State is not (GameState.Title or GameState.OutGame)) return;
            Localization.Current = Localization.Current == Language.Ko ? Language.En : Language.Ko;
            Save.language = Localization.Code(Localization.Current);
            SaveSystem.Save(storage, Save);
            int tab = outGame.Tab;
            bool settingsOpen = settings.IsOpen;
            BuildScreens();
            if (State == GameState.Title) title.Show(HasProgress);
            else
            {
                outGame.Show(Save);
                outGame.SetTab(tab);
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
            StateChanged?.Invoke(next);
        }
    }
}
