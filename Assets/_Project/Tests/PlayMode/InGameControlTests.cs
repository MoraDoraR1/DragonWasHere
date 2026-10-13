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
    /// 10-12 피드백: 일시정지 중 인게임 정지(커서를 움직여도 드래곤 방향이 바뀌지 않음),
    /// 공중 유닛 왕복 비행·범위 안에서만 공격, 진행 방향 앞보기 카메라.
    /// </summary>
    public class InGameControlTests
    {
        const int FrameRate = 60;

        /// <summary>테스트가 정하는 마우스 유도 위치. null이면 자동 이동</summary>
        class ScriptedInput : IGuideInput
        {
            public float? GuideX;

            public bool TryGetGuideX(out float worldX)
            {
                worldX = GuideX ?? 0;
                return GuideX.HasValue;
            }

            public bool TryGetCursor(out Vector2 world)
            {
                world = new Vector2(GuideX ?? 0, 3f);
                return GuideX.HasValue;
            }
        }

        GameManager manager;
        Camera camera;
        readonly ScriptedInput input = new();

        [SetUp]
        public void SetUp()
        {
            Random.InitState(20261012);
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            var go = new GameObject("GameManager");
            go.SetActive(false);
            manager = go.AddComponent<GameManager>();
            manager.database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            manager.art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>("Assets/_Project/Data/UiTheme.asset");
            manager.enableStarter = false;
            go.SetActive(true);
            input.GuideX = null;
            manager.Initialize(new MemorySaveStorage(), _ => input);
            Time.captureFramerate = FrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(manager.gameObject);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Pause_FreezesDragonAndCamera_ThenCameraLeadsFacing()
        {
            manager.StartStage();
            var dragon = manager.Battle.Dragon;
            yield return Frames(30);

            // 오른쪽으로 유도: 드래곤은 화면 왼쪽(카메라 중심보다 왼쪽)에 있다
            input.GuideX = dragon.transform.position.x + 50f;
            yield return Frames(90);
            Assert.AreEqual(1f, dragon.Facing);
            Assert.AreEqual(1f, camera.GetComponent<DWH.InGame.Stage.BattleCamera>().LeadDirection);
            Assert.Less(dragon.transform.position.x, camera.transform.position.x, "오른쪽을 보면 오른쪽이 넓게 보인다");

            // 일시정지 중 커서를 왼쪽으로 옮겨도 아무것도 바뀌지 않는다
            manager.SetPaused(true);
            float x = dragon.transform.position.x, camX = camera.transform.position.x;
            input.GuideX = x - 50f;
            yield return Frames(60);
            Assert.AreEqual(1f, dragon.Facing, "일시정지 중에는 방향이 바뀌지 않는다");
            Assert.AreEqual(x, dragon.transform.position.x, 1e-4f);
            Assert.AreEqual(camX, camera.transform.position.x, 1e-4f);

            // 풀면 왼쪽으로 돌아서고, 0.3초 뒤 카메라가 왼쪽을 넓게 보여 준다
            manager.SetPaused(false);
            yield return Frames(90);
            Assert.AreEqual(-1f, dragon.Facing);
            // 맵 왼쪽 끝에서는 카메라가 맵 밖을 보여 주지 않게 멈추므로 앞보기 방향으로 확인한다
            Assert.AreEqual(-1f, camera.GetComponent<DWH.InGame.Stage.BattleCamera>().LeadDirection, "왼쪽을 보면 왼쪽을 넓게 보여 준다");
        }

        /// <summary>스타터 화면 비율별 캡처 (DWH-56: 좁은 화면에서 용·배경 잘림). 조준 단계에서 찍는다</summary>
        [UnityTest]
        public IEnumerator Starter_CapturesAtAspects([Values(16f / 9f, 4f / 3f, 1f)] float aspect)
        {
            manager.enableStarter = true;
            camera.aspect = aspect;
            manager.StartStage();
            yield return Frames(FrameRate * 4);
            int h = 720, w = Mathf.RoundToInt(h * aspect);
            yield return TestScreens.Capture(camera, $"starter_{w}x{h}", w, h);
            camera.ResetAspect();
        }

        /// <summary>전투 화면 비율별 캡처 (QA: 게임 진행 중 화면 잘림). 좁은 화면에서도 16:9 가로 폭이 보여야 한다</summary>
        [UnityTest]
        public IEnumerator Battle_KeepsWidthAtNarrowAspects([Values(4f / 3f, 1f)] float aspect)
        {
            camera.aspect = aspect;
            manager.StartStage();
            yield return Frames(FrameRate * 2);
            Assert.GreaterOrEqual(camera.orthographicSize * camera.aspect, Tuning.CameraOrthoSize * Tuning.CameraMinAspect - 0.01f, "가로 폭이 16:9 기준보다 좁다");
            int h = 720, w = Mathf.RoundToInt(h * aspect);
            yield return TestScreens.Capture(camera, $"battle_{w}x{h}", w, h);
            camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator AirUnits_HoverOverDragon_AndAttackOnlyInRange()
        {
            manager.StartStage();
            var battle = manager.Battle;
            for (int i = 0; i < FrameRate * 6 && battle.AirUnits.Units.Count == 0; i++) yield return null;
            Assert.Greater(battle.AirUnits.Units.Count, 0, "spawn_sec에 공중 유닛이 나온다");
            var unit = battle.AirUnits.Units[0];

            // 드래곤 머리 위를 배회한다: 몇 초 동안 드래곤 근처에 머물고, 한자리에 고정되지 않는다
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < FrameRate * 9; i++)
            {
                yield return null;
                if (!unit.IsAlive) break;
                float x = unit.transform.position.x;
                if (i >= FrameRate * 6)   // 화면 오른쪽 밖에서 날아와 자리를 잡은 뒤
                    Assert.LessOrEqual(Mathf.Abs(x - battle.Dragon.Hitbox.center.x), Tuning.AirUnitHoverRange + 2f, "드래곤 근처를 떠나지 않는다");
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                Assert.GreaterOrEqual(x, battle.StageLeft, "맵 밖으로 나가지 않는다");
                Assert.Greater(unit.Hitbox.yMin, Tuning.GroundHeight + battle.Dragon.Hitbox.height, "드래곤 머리 위 상공에서만 난다");
            }
            Assert.Greater(maxX - minX, 0.5f, "한자리에 고정되지 않는다");
            yield return TestScreens.Capture(Camera.main, "air_hover");

            // 공격 범위: 드래곤과 가로 거리 안 + 화면 안
            var dragonX = battle.Dragon.Hitbox.center.x;
            Assert.IsTrue(unit.InAttackRange(new Vector2(dragonX + 1f, 4f)));
            Assert.IsFalse(unit.InAttackRange(new Vector2(dragonX + Tuning.AirUnitAttackRange + 1f, 4f)));
        }
    }
}
#endif
