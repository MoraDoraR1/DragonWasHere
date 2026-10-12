#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Drops;
using DWH.InGame.Stage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 한 판 전체 흐름 (M3): 아웃게임 → 출발 → 전투(자동) → 드랍 획득 → 결과 정산·저장 → 아웃게임 복귀.
    /// </summary>
    public class GameFlowTests
    {
        const int FrameRate = 60;
        const int MaxFrames = FrameRate * 240;

        GameDatabase db;
        GameManager manager;
        MemorySaveStorage storage;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            var art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;

            var go = new GameObject("GameManager");
            go.SetActive(false);   // Start에서 기본 저장소로 초기화되지 않게 먼저 끈다
            manager = go.AddComponent<GameManager>();
            manager.database = db;
            manager.art = art;
            manager.enableStarter = false;   // 스타터(클릭 입력)는 StageStarterTests에서 따로 검증한다
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>("Assets/_Project/Data/UiTheme.asset");
            go.SetActive(true);
            storage = new MemorySaveStorage();
            manager.Initialize(storage, _ => new AutoOnlyInput());
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

        static readonly string ScreenFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Screens");

        /// <summary>
        /// UI까지 포함한 화면 캡처. 배치모드에서는 WaitForEndOfFrame과 ScreenCapture가 동작하지 않으므로
        /// 캡처하는 동안만 캔버스를 카메라 렌더 모드로 바꿔 카메라로 그린다.
        /// </summary>
        const int CaptureSortingOffset = 10000;

        static IEnumerator CaptureScreen(string name, int width = 1280, int height = 720, System.Action check = null)
        {
            yield return null;
            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.activeInHierarchy).ToArray();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder += CaptureSortingOffset;   // 카메라 모드에서는 스프라이트와 같이 정렬되므로 UI를 위로 올린다
            }

            // 캔버스 배율(CanvasScaler)이 렌더 텍스처 크기에 맞춰 바뀌도록 한 프레임 기다린다
            yield return null;
            Canvas.ForceUpdateCanvases();
            check?.Invoke();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(ScreenFolder);
            File.WriteAllBytes(Path.Combine(ScreenFolder, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();

            foreach (var c in canvases)
            {
                c.sortingOrder -= CaptureSortingOffset;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        IEnumerator PlayUntilResult(string capturePrefix = null)
        {
            bool dropShot = false;
            for (int i = 0; i < MaxFrames && manager.State != GameState.Result; i++)
            {
                yield return null;
                if (capturePrefix != null && !dropShot && manager.Battle != null && manager.Battle.Drops.ActiveCount > 8)
                {
                    dropShot = true;
                    yield return CaptureScreen($"{capturePrefix}_drops");
                }
            }
            Assert.AreEqual(GameState.Result, manager.State, "시간 안에 결과 화면에 도달해야 한다");
        }

        /// <summary>파괴한 건물들의 드랍 구성 표 합계 = 받아야 할 골드</summary>
        double ExpectedGold(string stageId) =>
            manager.Battle.Buildings.Where(b => !b.IsAlive)
                .Sum(b => DropPlanner.TotalGold(DropPlanner.Expand(db.Composition(b.Type, stageId)), r => db.Resource(r).goldValue));

        [UnityTest]
        public IEnumerator Stage11_FullLoop_WithBonus_PaysDropGoldAndAdvances()
        {
            Assert.AreEqual(GameState.OutGame, manager.State);
            Assert.AreEqual("1-1", manager.Save.currentStage);

            yield return CaptureScreen("flow_outgame");
            manager.StartStage();
            Assert.AreEqual(GameState.Stage, manager.State);

            // 일반 스테이지 → 클리어 시 기력 10 이상이면 보너스 (8.1: 1-1은 업그레이드 없이 보너스까지 경험)
            for (int i = 0; i < MaxFrames && manager.State == GameState.Stage; i++) yield return null;
            Assert.AreEqual(GameState.Bonus, manager.State, "1-1 클리어 후 보너스 스테이지로 가야 한다");
            yield return null;
            var bonus = manager.Battle;
            Assert.AreEqual(BattleMode.Bonus, bonus.Mode);
            Assert.AreEqual(db.Stage("1-1").goldenCount, bonus.Buildings.Count);
            Assert.IsTrue(bonus.Buildings.All(b => b.Type == BuildingType.Golden));
            Assert.Greater(bonus.Dragon.Vitals.Elapsed, 20, "피로(경과 시간)는 일반 스테이지에서 이어진다");
            for (int i = 0; i < 90; i++) yield return null;
            yield return CaptureScreen("flow_bonus");

            yield return PlayUntilResult();
            yield return CaptureScreen("flow_result");
            var goldLabel = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).First(t => t.name == "Gold");
            Assert.AreEqual(NumberFormat.ToKorean(manager.Save.goldCoin), goldLabel.text, "정산 후 HUD 골드를 두 번 더하지 않는다");

            var result = manager.LastResult;
            Assert.IsTrue(result.cleared);
            Assert.IsTrue(result.bonusEntered);
            Assert.AreEqual(10, result.destroyedBuildings);
            double bonusExpected = ExpectedGold("1-1");
            Assert.AreEqual(bonusExpected, result.bonusGold, "보너스 골드 = 부순 황금 건물의 드랍 합계");
            Assert.Greater(result.bonusDestroyed, 0, "보너스에서 황금 건물을 하나 이상 부순다");
            double expected = 106 + bonusExpected;   // 1-1 일반 건물 전체 드랍 합계 106 (엑셀 「스테이지별 골드」)
            Assert.AreEqual(expected, result.earnedGold, "일반 + 보너스 골드를 모두 받아야 한다");
            Assert.AreEqual(0, manager.Battle.Drops.ActiveCount, "남은 드랍 자원은 정산 때 모두 획득 처리");
            Debug.Log($"[DWH] 1-1 정산: 일반 106 + 보너스 {result.bonusGold} (황금 {result.bonusDestroyed}/{result.bonusTotal}) = {result.earnedGold}");

            // 정산 결과가 저장되었다
            var saved = SaveSystem.Load(storage, "1-1");
            Assert.AreEqual(expected, saved.goldCoin);
            Assert.AreEqual("1-2", saved.currentStage);
            Assert.AreEqual("1-1", saved.highestStage);

            Assert.AreEqual(SessionEnd.Cleared, result.end);
            Assert.AreEqual(1, manager.Save.playCount);

            manager.ConfirmResult();
            yield return null;
            Assert.AreEqual(GameState.OutGame, manager.State);
            Assert.IsNull(manager.Battle);

            // 받은 골드로 각성을 산 뒤의 허브 화면
            Assert.Greater(manager.BuyUpgrade(db.upgradeNodes.rows.First(n => n.node == "DragonAwakening"), OutGame.UpgradeTree.MaxQuantity), 0);
            yield return CaptureScreen("flow_outgame_after");

            // 16:9가 아닌 화면에서도 허브 아래쪽이 잘리지 않는다 (Game 창 1919×895, 4:3)
            yield return CaptureScreen("flow_outgame_wide", 1919, 895, AssertHubInsideCanvas);
            yield return CaptureScreen("flow_outgame_4x3", 1440, 1080, AssertHubInsideCanvas);
        }

        static void AssertHubInsideCanvas()
        {
            var hub = Object.FindAnyObjectByType<DWH.UI.OutGameScreen>();
            var canvas = (RectTransform)hub.transform;
            Assert.GreaterOrEqual(canvas.rect.height, 1079.9f, "캔버스 높이는 기준 1080 이상");
            Assert.GreaterOrEqual(canvas.rect.width, 1919.9f, "캔버스 폭은 기준 1920 이상");
            var corners = new Vector3[4];
            foreach (var name in new[] { "Buy", "StatsLeft", "TreeView", "Detail", "Depart" })
            {
                var target = hub.GetComponentsInChildren<RectTransform>(true).First(r => r.name == name);
                target.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = canvas.InverseTransformPoint(corner);
                    Assert.IsTrue(canvas.rect.Contains(new Vector2(local.x, local.y) + new Vector2(0.01f, 0.01f) * Mathf.Sign(-local.y)) || canvas.rect.Contains((Vector2)local * 0.999f),
                        $"{name}이(가) 화면 밖으로 나간다 ({local}, 캔버스 {canvas.rect})");
                }
            }
        }

        /// <summary>M9: 1-4를 클리어한 기록이 있으면 환생할 수 있고, 진행을 초기화하고 포인트를 준다. 특성·액티브 스킬은 포인트로 산다</summary>
        [UnityTest]
        public IEnumerator Rebirth_ResetsRunAndSpendsPoints()
        {
            var save = manager.Save;
            Assert.AreEqual(0, manager.PerformRebirth(), "1-4 클리어 전에는 환생할 수 없다");
            save.highestStage = "1-4";
            save.currentStage = "2-1";
            save.runEarnedGold = 10000;
            save.goldCoin = 777;
            save.SetNodeLevel("DragonAwakening", 10);

            Assert.AreEqual(10, manager.PerformRebirth(), "floor(sqrt(10000) / 10)");
            Assert.AreEqual("1-1", save.currentStage);
            Assert.AreEqual(0, save.goldCoin);
            Assert.AreEqual(0, manager.Upgrades.Level(manager.Upgrades.Node("DragonAwakening")));
            Assert.AreEqual(1, save.rebirthCount);

            Assert.IsTrue(manager.BuyActive(ActiveSkillId.Meteor));
            Assert.IsTrue(manager.BuyTrait(RebirthTraitId.PrimalFury));
            Assert.AreEqual(10 - 5 - 1, save.rebirthPoint);
            Assert.AreEqual(1, SaveSystem.Load(storage, "1-1").GetActiveLevel("Meteor"), "구매 직후 저장");

            var hub = Object.FindAnyObjectByType<DWH.UI.OutGameScreen>();
            hub.SetTab(1);
            yield return CaptureScreen("flow_rebirth");
            hub.SetTab(0);

            // HUD 스킬 칸 캡처용으로 패시브 몇 개를 열어 둔다
            save.SetNodeLevel("Skill_FireBreath", 1);
            save.SetNodeLevel("Skill_Whirlwind", 1);
            save.SetNodeLevel("Sub_Dash", 1);
            // 출격하면 액티브 스킬 횟수가 충전되어 있다
            manager.StartStage();
            yield return null;
            Assert.AreEqual(1, manager.Battle.Actives.UsesLeft(ActiveSkillId.Meteor));
            Assert.AreEqual(0, manager.Battle.Actives.UsesLeft(ActiveSkillId.Hurricane));
            Assert.AreEqual(20 * 1.05, manager.Battle.Stats.AttackPower, 1e-9, "태고의 분노 1레벨");
            yield return CaptureScreen("flow_actives_hud");
        }

        [UnityTest]
        public IEnumerator Stage12_Failure_PaysPartialGoldAndStays()
        {
            manager.Save.currentStage = "1-2";
            manager.Save.goldCoin = 100;
            manager.StartStage();
            yield return PlayUntilResult("flow_12");

            var result = manager.LastResult;
            Assert.IsFalse(result.cleared);
            Assert.Greater(result.earnedGold, 0, "실패해도 부순 건물의 골드는 받는다");
            Assert.AreEqual(ExpectedGold("1-2"), result.earnedGold);
            Assert.AreEqual(100 + result.earnedGold, manager.Save.goldCoin);
            Assert.AreEqual("1-2", manager.Save.currentStage);
            Assert.AreNotEqual(SessionEnd.Cleared, result.end);
            yield return CaptureScreen("flow_result_fail");
            Debug.Log($"[DWH] 1-2 실패 정산({result.end}): 획득 골드 {result.earnedGold} (파괴 {result.destroyedBuildings}/{result.totalBuildings})");

            // 바로 다시 출격: 아웃게임을 거치지 않고 같은 도전 스테이지로 간다
            manager.RetryFromResult();
            Assert.AreEqual(GameState.Stage, manager.State);
            Assert.AreEqual("1-2", manager.Battle.Stage.stageId);

            // 일시정지: 시간을 멈췄다가 되돌린다
            yield return null;
            manager.SetPaused(true);
            Assert.IsTrue(manager.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            yield return CaptureScreen("flow_pause");
            manager.SetPaused(false);
            Assert.IsFalse(manager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
#endif
