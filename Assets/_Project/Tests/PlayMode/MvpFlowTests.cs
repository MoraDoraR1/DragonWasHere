#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.OutGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 한 판 전체 흐름 (가이드 M3·M5·M9·M10): 아웃게임 → 출격 → 전투(자동) → (보너스) → 결과 정산·저장 → 아웃게임, 환생, 한/영 전환.
    /// </summary>
    public class MvpFlowTests
    {
        const int FrameRate = 60;
        const int MaxFrames = FrameRate * 300;

        GameDatabase db;
        GameManager manager;
        MemorySaveStorage storage;
        Camera camera;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            // 전투는 Random(치명타·드랍·이펙트)을 쓴다. 앞 테스트가 쓴 만큼 결과가 바뀌지 않게 시드를 고정한다
            Random.InitState(20261012);
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;

            var go = new GameObject("GameManager");
            go.SetActive(false);   // Start에서 기본 저장소로 초기화되지 않게 먼저 끈다
            manager = go.AddComponent<GameManager>();
            manager.database = db;
            manager.art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>("Assets/_Project/Data/UiTheme.asset");
            manager.enableStarter = false;
            go.SetActive(true);
            storage = new MemorySaveStorage();
            Localization.Current = Language.Ko;
            manager.Initialize(storage, _ => new AutoOnlyInput());
            Time.captureFramerate = FrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            Localization.Current = Language.Ko;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.Destroy(cam.gameObject);
            Object.Destroy(manager.gameObject);
        }

        IEnumerator PlayUntilResult()
        {
            for (int i = 0; i < MaxFrames && manager.State != GameState.Result; i++)
                yield return null;
            Assert.AreEqual(GameState.Result, manager.State, "시간 안에 결과 화면에 도달해야 한다");
        }

        [UnityTest]
        public IEnumerator FirstRun_ClearsWithBonus_SettlesAndAdvances()
        {
            yield return TestScreens.Capture(Camera.main, "hub_first");
            Assert.AreEqual(GameState.OutGame, manager.State);
            manager.StartStage();
            Assert.AreEqual(GameState.Stage, manager.State);
            yield return PlayUntilResult();
            var r = manager.LastResult;
            yield return TestScreens.Capture(Camera.main, "result_first");
            Assert.IsTrue(r.cleared, "가이드 9장: 업그레이드 없이 1-1 첫 판 클리어");
            Assert.IsTrue(r.bonusEntered, "가이드 9장: 첫 판 보너스 진입");
            Assert.Greater(r.earnedGold, 0);
            Assert.AreEqual(r.earnedGold, manager.Save.goldCoin, 1e-6, "정산: 골드 지급");
            Assert.AreEqual("stg102", manager.Save.currentStage);
            Assert.IsNotNull(storage.Json, "정산 직후 저장");
            Assert.Contains("run_end", Analytics.Recent);

            manager.ConfirmResult();
            Assert.AreEqual(GameState.OutGame, manager.State);
            manager.RetryFromResult();   // 결과 화면이 아니면 무시
            Assert.AreEqual(GameState.OutGame, manager.State);
        }

        [UnityTest]
        public IEnumerator Hub_BuyUpgradeGrowAndCapture()
        {
            manager.DebugAddGold(2000);
            var awaken = db.Upgrade("upg101");
            Assert.AreEqual(3, manager.BuyUpgrade(awaken, 3));
            Assert.AreEqual(3, manager.Save.GetNodeLevel("upg101"));
            // 성장 2단계: 누적 사용 골드 1,000 이상
            manager.BuyUpgrade(db.Upgrade("upg201"), 10);
            manager.BuyUpgrade(db.Upgrade("upg301"), 10);
            Assert.GreaterOrEqual(manager.Save.runSpentGold, 1000);
            Assert.AreEqual(GrowthStage.Juvenile, manager.Upgrades.Stats().Growth.growthStage);
            for (int i = 0; i < 10; i++) yield return null;
            yield return TestScreens.Capture(Camera.main, "hub_tree");
            manager.OutGame.SetTab(1);
            yield return TestScreens.Capture(Camera.main, "hub_rebirth");
        }

        [UnityTest]
        public IEnumerator Rebirth_AtWall_ResetsAndKeeps()
        {
            manager.DebugShiftStage(5);   // 3-2
            Assert.AreEqual("stg302", manager.Save.currentStage);
            Assert.IsTrue(Rebirth.IsUnlocked(db, manager.Save));
            manager.Save.runEarnedGold = 57600;
            manager.Save.SetActiveLevel("act1", 1);
            Assert.AreEqual(24, manager.PerformRebirth());
            Assert.AreEqual("stg101", manager.Save.currentStage);
            Assert.AreEqual(1, manager.Save.GetActiveLevel("act1"));
            Assert.AreEqual(GrowthStage.Hatchling, manager.Upgrades.Stats().Growth.growthStage);
            Assert.Contains("rebirth", Analytics.Recent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Language_TogglesAndPersists()
        {
            manager.ToggleLanguage();
            Assert.AreEqual(Language.En, Localization.Current);
            Assert.AreEqual("en", manager.Save.language);
            Assert.AreEqual("Fire Dragon", db.Text(db.Dragon.nameStr));
            yield return TestScreens.Capture(Camera.main, "hub_en");
            manager.ToggleLanguage();
            Assert.AreEqual(Language.Ko, Localization.Current);
        }

        [UnityTest]
        public IEnumerator Boss_Stage_ShowsHudAndLandmark()
        {
            manager.DebugShiftStage(1);   // 1-2 보스
            manager.StartStage();
            for (int i = 0; i < FrameRate * 5; i++) yield return null;
            Assert.IsTrue(db.IsBoss(manager.Battle.Stage));
            Assert.AreEqual(1, manager.Battle.Buildings.Count(b => b.Type == BuildingType.Landmark));
            yield return TestScreens.Capture(Camera.main, "hud_boss_1-2");
        }
    }
}
#endif
