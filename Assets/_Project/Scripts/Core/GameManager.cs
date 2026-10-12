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
    public enum GameState { OutGame, Stage, Bonus, Result }

    /// <summary>
    /// 게임 상태 머신 (5장). OutGame → Stage → (Bonus) → Result → OutGame.
    /// 일반 스테이지를 클리어하고 기력이 10 이상 남으면 보너스로 간다 (1.1). 스테이지 스타터(M10)는 Stage 앞에 들어간다.
    /// 저장 시점: 세션 정산 직후, 업그레이드·환생 특성·액티브 스킬 구매 직후, 환생 직후, 앱 종료 시 (4.11).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameDatabase database;
        public ArtCatalog art;
        public UiTheme uiTheme;
        /// <summary>일반 스테이지 앞의 스테이지 스타터(4.7). 자동 진행 테스트와 봇은 끈다</summary>
        public bool enableStarter = true;

        ISaveStorage storage;
        Func<Camera, IGuideInput> inputFactory;
        string[] stageOrder;
        Camera view;
        OutGameScreen outGame;
        ResultScreen resultScreen;
        BattleHud hud;
        PauseMenu pauseMenu;
        float timeScaleBeforePause = 1f;
        double goldAtDeparture;
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

        public event Action<GameState> StateChanged;

        void Awake() => Instance = this;

        void Start()
        {
            if (!IsInitialized)
                Initialize(SaveSystem.DefaultStorage(), cam => new MouseGuideInput(cam, (float)database.dragon.autoMoveIdleSeconds));
        }

        /// <summary>저장소와 입력을 바꿔 끼울 수 있게 분리했다. 테스트는 메모리 저장소와 자동 입력을 넣는다.</summary>
        public void Initialize(ISaveStorage saveStorage, Func<Camera, IGuideInput> guideInputFactory)
        {
            storage = saveStorage;
            inputFactory = guideInputFactory;
            stageOrder = database.stages.rows.Select(s => s.stageId).ToArray();
            Save = SaveSystem.Load(storage, stageOrder[0]);
            view = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();

            if (uiTheme != null) UiTheme.Active = uiTheme;
            Upgrades = new UpgradeTree(database, Save);
            outGame = OutGameScreen.Create(database, art, Upgrades, new OutGameActions
            {
                Depart = StartStage,
                BuyNode = (node, quantity) => BuyUpgrade(node, quantity),
                Rebirth = PerformRebirth,
                BuyTrait = BuyTrait,
                BuyActive = BuyActive,
                RebirthUnlocked = () => Rebirth.IsUnlocked(database, Save, stageOrder),
            });
            resultScreen = ResultScreen.Create(database, ConfirmResult, RetryFromResult);
            pauseMenu = PauseMenu.Create(() => SetPaused(false));
            EnterOutGame();
        }

        void EnterOutGame()
        {
            resultScreen.Hide();
            outGame.Show(Save);
            SetState(GameState.OutGame);
        }

        /// <summary>"출발": 현재 도전 스테이지로 간다 (1.1)</summary>
        public void StartStage()
        {
            if (State != GameState.OutGame) return;
            outGame.Hide();
            goldAtDeparture = Save.goldCoin;
            sessionStats = Upgrades.Stats();
            // 액티브 스킬 사용 횟수는 세션마다 최대치로 충전한다. 보너스 스테이지는 이어서 쓴다 (4.4)
            activeLevels = Enum.GetValues(typeof(ActiveSkillId)).Cast<ActiveSkillId>()
                .ToDictionary(id => id, id => Rebirth.ActiveLevel(Save, id));
            sessionUses = ActiveSkillSystem.FullUses(database, activeLevels);
            StartBattle(Save.currentStage, BattleMode.Normal, null, 0);
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
            // 첫 두 판은 조작 안내를 띄운다
            hud = BattleHud.Create(Battle, () => shown, art, TogglePause, mode == BattleMode.Normal && Save.playCount < 2, goldBefore);
        }

        void OnBattleEnded(BattleController battle)
        {
            battle.Drops.CollectAll();

            if (battle.Mode == BattleMode.Normal)
            {
                LastResult = new SessionResult
                {
                    stageId = battle.Stage.stageId,
                    cleared = battle.State == BattleState.Cleared,
                    end = battle.State == BattleState.Cleared ? SessionEnd.Cleared
                        : battle.Dragon.Vitals.IsDead ? SessionEnd.Defeated : SessionEnd.Exhausted,
                    earnedGold = battle.Drops.CollectedGold,
                    destroyedBuildings = battle.DestroyedCount,
                    totalBuildings = battle.Buildings.Count,
                };

                if (battle.BonusEligible)
                {
                    // 보너스 스테이지: 체력·기력·피로를 이어받는다 (3.5, 8.1)
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
                LastResult.earnedGold += LastResult.bonusGold;
            }

            // 보물 더미: 클리어 시 획득 골드의 일정 비율을 더 준다 (4.8)
            if (LastResult.cleared && sessionStats.ClearGoldBonus > 0)
            {
                LastResult.clearBonusGold = LastResult.earnedGold * sessionStats.ClearGoldBonus;
                LastResult.earnedGold += LastResult.clearBonusGold;
            }

            Progression.Settle(Save, LastResult, stageOrder);
            Save.playCount++;
            SaveSystem.Save(storage, Save);

            resultScreen.Show(LastResult, Save, AffordableUpgradeCount());
            SetState(GameState.Result);
        }

        int AffordableUpgradeCount() => database.upgradeNodes.rows.Count(n => Upgrades.Status(n) == NodeStatus.Affordable);

        void CloseBattle()
        {
            SetPaused(false);
            Destroy(hud.gameObject);
            Destroy(Battle.gameObject);
            Battle = null;
        }

        /// <summary>업그레이드 구매. 구매 직후 저장한다 (4.11)</summary>
        public bool BuyUpgrade(UpgradeNodeRow node) => BuyUpgrade(node, 1) > 0;

        /// <summary>구매 수량 ×1 / ×10 / 최대(UpgradeTree.MaxQuantity). 산 레벨 수를 돌려준다</summary>
        public int BuyUpgrade(UpgradeNodeRow node, int quantity)
        {
            if (State != GameState.OutGame) return 0;
            var before = Growth.Stage(database, Save.lifetimeSpentGold);
            int bought = Upgrades.BuyMany(node, quantity);
            if (bought == 0) return 0;
            SaveSystem.Save(storage, Save);
            outGame.Refresh();
            // 누적 사용 골드로 성장 단계가 오르면 알린다 (4.1.4)
            var after = Growth.Stage(database, Save.lifetimeSpentGold);
            if (after.growthStage != before.growthStage)
                outGame.Celebrate($"{after.nameKo}{Ro(after.nameKo)} 성장!", $"용이 {after.sizeScaleFromPrevious:0.##}배 커졌어요. 다음 출격부터 더 크게 날뛰어요");
            return bought;
        }

        /// <summary>조사 '로/으로': 받침이 없거나 ㄹ 받침이면 '로'</summary>
        static string Ro(string word)
        {
            char last = word[^1];
            if (last < 0xAC00 || last > 0xD7A3) return "(으)로";
            int jong = (last - 0xAC00) % 28;
            return jong == 0 || jong == 8 ? "로" : "으로";
        }

        /// <summary>환생 (4.9). 받은 포인트를 돌려준다. 아웃게임에서만, 해금된 뒤에만 된다</summary>
        public double PerformRebirth()
        {
            if (State != GameState.OutGame || !Rebirth.IsUnlocked(database, Save, stageOrder)) return 0;
            double points = Rebirth.Perform(database, Save, stageOrder);
            SaveSystem.Save(storage, Save);
            outGame.Show(Save);
            return points;
        }

        public bool BuyTrait(RebirthTraitId id)
        {
            if (State != GameState.OutGame || !Rebirth.BuyTrait(database, Save, id)) return false;
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

        /// <summary>결과 화면 "확인": 전투를 정리하고 아웃게임으로 돌아간다</summary>
        public void ConfirmResult()
        {
            if (State != GameState.Result) return;
            CloseBattle();
            EnterOutGame();
        }

        /// <summary>결과 화면 "바로 다시 출격": 아웃게임을 거치지 않고 도전 스테이지로 출발한다</summary>
        public void RetryFromResult()
        {
            if (State != GameState.Result) return;
            ConfirmResult();
            StartStage();
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
            // 키보드 단축키: Space / Enter로 출발·확인
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
                TogglePause();
            if (IsPaused) return;
            bool confirm = keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
            if (confirm)
            {
                if (State == GameState.OutGame) StartStage();
                else if (State == GameState.Result) ConfirmResult();
            }
            if (keyboard.rKey.wasPressedThisFrame && State == GameState.Result)
                RetryFromResult();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 개발용: 아웃게임에서 PageUp/PageDown으로 도전 스테이지를 바꾼다 (게임에는 스테이지 선택이 없다. 1.1)
            if (State == GameState.OutGame)
            {
                int step = keyboard.pageUpKey.wasPressedThisFrame ? 1 : keyboard.pageDownKey.wasPressedThisFrame ? -1 : 0;
                if (step != 0) DebugShiftStage(step);
                if (keyboard.gKey.wasPressedThisFrame)
                    DebugAddGold(keyboard.shiftKey.isPressed ? 100000 : 1000);
            }
#endif
        }

        /// <summary>개발용 골드 지급. 밸런스 확인용이며 회차 누적 획득 골드(환생 포인트 기준)는 늘리지 않는다.</summary>
        public void DebugAddGold(double amount)
        {
            Save.goldCoin += amount;
            SaveSystem.Save(storage, Save);
            outGame.Refresh();
        }

        /// <summary>개발용 도전 스테이지 이동. 진행 기록(클리어 목록·최고 스테이지)은 바꾸지 않는다.</summary>
        public void DebugShiftStage(int step)
        {
            int index = Array.IndexOf(stageOrder, Save.currentStage);
            Save.currentStage = stageOrder[Mathf.Clamp(index + step, 0, stageOrder.Length - 1)];
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
