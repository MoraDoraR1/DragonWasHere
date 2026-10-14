#if UNITY_EDITOR
using System.Collections;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 타이틀 흐름 (UI 기획서 2장): 기록 없음 → 이어하기 비활성·새 게임은 바로 1-1, 기록 있음 → 이어하기는 둥지,
    /// 새 게임은 기록을 지우고 언어는 남김, 일시정지 "타이틀로 이동"은 이번 판을 기록하지 않음.
    /// </summary>
    public class TitleFlowTests
    {
        GameManager manager;
        MemorySaveStorage storage;
        Camera camera;

        GameManager CreateManager()
        {
            var go = new GameObject("GameManager");
            go.SetActive(false);
            var m = go.AddComponent<GameManager>();
            m.database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            m.art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            m.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>("Assets/_Project/Data/UiTheme.asset");
            m.enableStarter = false;
            go.SetActive(true);
            return m;
        }

        [SetUp]
        public void SetUp()
        {
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            storage = new MemorySaveStorage();
            Localization.Current = Language.Ko;
            manager = CreateManager();
            manager.Initialize(storage, _ => new AutoOnlyInput(), showTitle: true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Localization.Current = Language.Ko;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(manager.gameObject);
        }

        [UnityTest]
        public IEnumerator FreshSave_ContinueDisabled_NewGameStartsFirstStage()
        {
            yield return TestScreens.Capture(Camera.main, "title_first");
            Assert.AreEqual(GameState.Title, manager.State);
            Assert.IsFalse(manager.HasProgress);

            manager.ContinueGame();   // 기록이 없으면 무시
            Assert.AreEqual(GameState.Title, manager.State);

            // 배경 영상: StreamingAssets 파일을 URL로 재생한다 (SceneBuilder.CopyStreamingVideos)
            float until = Time.realtimeSinceStartup + 8f;
            while (!manager.Title.VideoPlaying && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 20; i++) yield return null;
            yield return TestScreens.Capture(Camera.main, "title_video");
            Assert.IsTrue(manager.Title.VideoPlaying, "타이틀 배경 영상이 재생되어야 한다");

            manager.OpenSettings();
            yield return TestScreens.Capture(Camera.main, "title_settings");
            manager.Settings.Hide();

            manager.StartNewGame();
            Assert.AreEqual(GameState.Stage, manager.State, "첫 실행 새 게임은 바로 1-1");
            Assert.AreEqual(manager.database.StageOrder[0], manager.Battle.Stage.stageId);
        }

        [UnityTest]
        public IEnumerator ReturnToTitle_DiscardsRun_ThenContinueAndNewGame()
        {
            manager.StartNewGame();
            yield return null;
            manager.SetPaused(true);
            Assert.IsTrue(manager.IsPaused);
            yield return TestScreens.Capture(Camera.main, "pause_menu");
            manager.ReturnToTitle();
            Assert.AreEqual(GameState.Title, manager.State);
            Assert.IsFalse(manager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(manager.Battle);
            Assert.AreEqual(0, manager.Save.playCount, "타이틀로 나간 판은 기록하지 않는다");

            // 기록이 있는 상태를 만든다: 영어로 바꾸고 한 판 한 것으로 저장
            manager.ToggleLanguage();
            manager.Save.playCount = 3;
            manager.Save.goldCoin = 500;
            SaveSystem.Save(storage, manager.Save);
            string userId = manager.Save.userId;

            // 다시 켜면 타이틀, 이어하기 → 둥지
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(manager.gameObject);
            yield return null;
            manager = CreateManager();
            manager.Initialize(storage, _ => new AutoOnlyInput(), showTitle: true);
            yield return TestScreens.Capture(Camera.main, "title_en_saved");
            Assert.AreEqual(GameState.Title, manager.State);
            Assert.IsTrue(manager.HasProgress);
            // 게임을 켤 때는 언제나 영어 (저장된 한국어가 있어도, 10-14)
            Assert.AreEqual("en", manager.Save.language);
            Assert.AreEqual(Language.En, Localization.Current);
            manager.ContinueGame();
            Assert.AreEqual(GameState.OutGame, manager.State);
            Assert.AreEqual(500, manager.Save.goldCoin);

            // 새 게임: 기록이 있으면 경고 팝업부터
            manager.ReturnToTitle();
            manager.Title.OnNewGame();
            Assert.IsTrue(manager.Title.WarningOpen);
            Assert.AreEqual(GameState.Title, manager.State);
            yield return TestScreens.Capture(Camera.main, "title_warning");
            manager.Title.HideWarning();

            // "네!" → 기록 초기화, 언어·사용자 ID는 유지 (이번 실행 중에 바꾼 언어는 새 게임에도 남는다)
            manager.ToggleLanguage();
            manager.StartNewGame();
            Assert.AreEqual(GameState.Stage, manager.State);
            Assert.AreEqual(0, manager.Save.goldCoin);
            Assert.AreEqual(0, manager.Save.playCount);
            Assert.AreEqual("ko", manager.Save.language, "실행 중에 바꾼 언어(영어 → 한국어)는 새 게임에도 남는다");
            Assert.AreEqual(userId, manager.Save.userId);
        }
    }
}
#endif
