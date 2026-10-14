#if UNITY_EDITOR
using System.Collections;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Starter;
using DWH.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 튜토리얼 (튜토리얼 기획서 Ver03): 첫 판에서 스타터 → 인게임 → 정산 → 강화 → 환생 탭 순서로 한 번씩 뜨고, 떠 있는 동안 게임을 멈추며,
    /// 끝낸 뒤에는 다시 뜨지 않는다. 각 쪽을 Logs/Screens/tutorial_*.png로 찍는다.
    /// </summary>
    public class TutorialTests
    {
        const int FrameRate = 60;

        GameDatabase db;
        GameManager manager;
        Camera camera;

        /// <summary>표적은 0.5초 뒤, 게이지는 바로 누른다</summary>
        class QuickStarter : IStarterInput
        {
            float aiming;

            public bool Pressed(StageStarter s)
            {
                if (Time.timeScale <= 0) return false;
                if (s.Phase == StarterPhase.Aiming) return (aiming += Time.deltaTime) >= 0.5f;
                return s.Phase == StarterPhase.Gauge;
            }
        }

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            Random.InitState(20261013);
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;

            var go = new GameObject("GameManager");
            go.SetActive(false);
            manager = go.AddComponent<GameManager>();
            manager.database = db;
            manager.art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/_Project/Data/UiTheme.asset");
            manager.starterInputFactory = () => new QuickStarter();
            go.SetActive(true);
            Localization.Current = Language.Ko;
            manager.Initialize(new MemorySaveStorage(), _ => new AutoOnlyInput());
            manager.TutorialsEnabled = true;
            Time.captureFramerate = FrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.Destroy(cam.gameObject);
            Object.Destroy(manager.gameObject);
        }

        TutorialOverlay Tutorial => manager.Tutorial;

        IEnumerator WaitOpen(string key, float seconds)
        {
            for (int i = 0; i < FrameRate * seconds && !Tutorial.IsOpen; i++) yield return null;
            Assert.IsTrue(Tutorial.IsOpen, $"{key} 튜토리얼이 떠야 한다");
            Assert.AreEqual(key, Tutorial.CurrentKey);
            Assert.AreEqual(0f, Time.timeScale, "튜토리얼 중에는 게임이 멈춘다");
            yield return null;
        }

        /// <summary>지금 쪽을 찍고 다음으로 (마지막이면 닫는다)</summary>
        IEnumerator ShotAndNext(string shot)
        {
            yield return null;
            yield return TestScreens.Capture(camera, shot);
            Tutorial.OnNext();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstRun_ShowsEachTutorialOnce()
        {
            manager.StartStage();

            // 스타터: 표적 고르기 시작 1쪽 → 닫으면 진행 → 위치 확정 후 2·3쪽
            yield return WaitOpen("ui_tutorial_starter_1", 10);
            var dragonX = manager.Battle.Dragon.transform.position.x;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(dragonX, manager.Battle.Dragon.transform.position.x, 1e-4, "멈춘 동안 드래곤이 움직이지 않는다");
            yield return ShotAndNext("tutorial_starter_1");
            Assert.IsFalse(Tutorial.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            yield return WaitOpen("ui_tutorial_starter_2", 10);
            yield return ShotAndNext("tutorial_starter_2");
            Assert.AreEqual("ui_tutorial_starter_3", Tutorial.CurrentKey);
            yield return ShotAndNext("tutorial_starter_3");
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.Starter));

            // 인게임: 착지 2초 뒤 4쪽
            yield return WaitOpen("ui_tutorial_ingame_1", 20);
            Assert.IsFalse(manager.Battle.InStarter);
            yield return ShotAndNext("tutorial_ingame_1");
            yield return ShotAndNext("tutorial_ingame_2");
            yield return ShotAndNext("tutorial_ingame_3");
            yield return ShotAndNext("tutorial_ingame_4");
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.InGame));
            Assert.AreEqual(1f, Time.timeScale);

            // 정산: 첫 결과 화면 1쪽 (Ver03). 닫아도 결과 화면에 그대로 있다
            for (int i = 0; i < FrameRate * 300 && manager.State != GameState.Result; i++) yield return null;
            Assert.AreEqual(GameState.Result, manager.State);
            yield return WaitOpen("ui_tutorial_settle_1", 5);
            yield return ShotAndNext("tutorial_settle_1");
            Assert.AreEqual(GameState.Result, manager.State);
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.Settle));

            // 강화: 아웃게임에 처음 들어오면 2쪽 (노드 → 상세 창)
            manager.ConfirmResult();
            yield return WaitOpen("ui_tutorial_upgrade_1", 5);
            yield return ShotAndNext("tutorial_upgrade_1");
            Assert.AreEqual("ui_tutorial_upgrade_2", Tutorial.CurrentKey);
            yield return ShotAndNext("tutorial_upgrade_2");
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.Upgrade));

            // 환생: 환생 탭을 처음 열면 3쪽
            manager.OutGame.SetTab(1);
            yield return WaitOpen("ui_tutorial_rebirth_1", 5);
            yield return ShotAndNext("tutorial_rebirth_1");
            yield return ShotAndNext("tutorial_rebirth_2");
            yield return ShotAndNext("tutorial_rebirth_3");
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.RebirthTab));

            // 끝낸 튜토리얼은 다시 뜨지 않는다
            manager.OutGame.SetTab(0);
            manager.OutGame.SetTab(1);
            manager.OutGame.SetTab(0);
            manager.StartStage();
            for (int i = 0; i < FrameRate * 12; i++)
            {
                yield return null;
                Assert.IsFalse(Tutorial.IsOpen, "최초 1회만");
            }
        }

        [UnityTest]
        public IEnumerator Skip_SkipsWholeTutorial_NewGameShowsAgain()
        {
            manager.StartStage();
            yield return WaitOpen("ui_tutorial_starter_1", 10);
            yield return TestScreens.Capture(camera, "tutorial_skip");
            // SKIP: 스타터 튜토리얼 전체(뒤의 게이지 2·3쪽 포함)를 건너뛴다
            Tutorial.OnSkip();
            yield return null;
            Assert.IsFalse(Tutorial.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(manager.Save.TutorialDone(TutorialOverlay.Starter));
            for (int i = 0; i < FrameRate * 20 && manager.Battle.InStarter; i++)
            {
                yield return null;
                Assert.IsFalse(Tutorial.IsOpen, "건너뛴 스타터 튜토리얼은 게이지에서 다시 뜨지 않는다");
            }
            Assert.IsFalse(manager.Battle.InStarter);

            // 새 게임: 튜토리얼 기록을 지워 처음부터 다시 보여 준다
            manager.ReturnToTitle();
            yield return null;
            manager.StartNewGame();
            Assert.AreEqual(0, manager.Save.tutorialsDone.Count, "새 게임은 튜토리얼을 다시 보여 준다");
            yield return WaitOpen("ui_tutorial_starter_1", 10);
        }

        [UnityTest]
        public IEnumerator Disabled_InTestsAndForReturningPlayers()
        {
            // 테스트·봇(타이틀 없이 시작)은 꺼져 있다
            manager.TutorialsEnabled = false;
            manager.StartStage();
            for (int i = 0; i < FrameRate * 5; i++)
            {
                yield return null;
                Assert.IsFalse(Tutorial.IsOpen);
            }

            // 튜토리얼 이전 버전 저장으로 이미 플레이한 사람은 시작 튜토리얼을 건너뛴다
            var storage = new MemorySaveStorage();
            storage.Write("{\"version\":2,\"playCount\":5,\"currentStage\":\"stg201\"}");
            var save = SaveSystem.Load(storage, "stg101");
            Assert.IsTrue(save.TutorialDone(TutorialOverlay.Starter));
            Assert.IsTrue(save.TutorialDone(TutorialOverlay.InGame));
            Assert.IsTrue(save.TutorialDone(TutorialOverlay.Settle));
            Assert.IsTrue(save.TutorialDone(TutorialOverlay.Upgrade));
            Assert.IsFalse(save.TutorialDone(TutorialOverlay.RebirthTab), "환생은 안 해 봤으면 처음 열 때 보여 준다");
        }
    }
}
#endif
