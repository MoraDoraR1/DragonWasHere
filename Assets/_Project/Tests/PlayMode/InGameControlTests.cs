#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
        class ScriptedInput : IGuideInput, IGuideModeInput
        {
            public float? GuideX;
            public bool MouseMode { get; set; } = true;

            public bool TryGetGuideX(out float worldX)
            {
                worldX = GuideX ?? 0;
                return MouseMode && GuideX.HasValue;
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

        /// <summary>인게임(일시정지 중) 환경설정으로 언어를 바꾼다. 전투·일시정지는 그대로, HUD는 새 언어로 다시 만든다</summary>
        [UnityTest]
        public IEnumerator Language_ChangesDuringBattle()
        {
            manager.StartStage();
            yield return Frames(30);
            manager.SetPaused(true);
            var before = Localization.Current;
            manager.ToggleLanguage();
            Assert.AreNotEqual(before, Localization.Current);
            Assert.IsTrue(manager.IsPaused, "일시정지 유지");
            Assert.IsNotNull(manager.Battle);
            yield return null;   // 옛 HUD는 프레임 끝에 지워진다
            Assert.AreEqual(1, Object.FindObjectsByType<DWH.UI.BattleHud>(FindObjectsSortMode.None).Length, "HUD는 하나");
            yield return TestScreens.Capture(camera, "battle_lang_paused");
            manager.SetPaused(false);
            yield return Frames(30);
            Assert.AreEqual(GameState.Stage, manager.State);
        }

        /// <summary>공격 건물·공중 유닛 투사체가 데이터(const)의 그림으로 나온다</summary>
        [UnityTest]
        public IEnumerator Projectiles_UseDataSprites()
        {
            Assert.IsTrue(manager.database.HasConst("projectile_res_building"));
            Assert.IsTrue(manager.art.Has(manager.database.ConstRow("projectile_res_air_unit").value));
            manager.StartStage();
            DWH.InGame.Fx.SpriteAnim shot = null;
            for (int i = 0; i < FrameRate * 20 && shot == null; i++)
            {
                yield return null;
                foreach (var p in Object.FindObjectsByType<DWH.InGame.Buildings.Projectile>(FindObjectsSortMode.None))
                    if (p.TryGetComponent(out shot)) break;
            }
            Assert.IsNotNull(shot, "투사체가 그림(SpriteAnim)으로 나와야 한다");
            yield return TestScreens.Capture(camera, "projectile");
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

        [UnityTest]
        public IEnumerator ControlMode_SpaceAndButtonToggle_AndPersist()
        {
            // 처음은 마우스 유도. Space(ToggleGuideMode)와 HUD 모드 버튼이 같은 일을 하고, 바꾼 모드는 저장해 다음 판에도 쓴다 (10-14)
            manager.StartStage();
            yield return Frames(10);
            Assert.IsTrue(input.MouseMode);
            input.GuideX = manager.Battle.Dragon.transform.position.x + 5f;
            yield return Frames(2);
            Assert.IsTrue(manager.Battle.Dragon.IsGuided);

            manager.ToggleGuideMode();
            yield return Frames(2);
            Assert.IsFalse(input.MouseMode);
            Assert.IsFalse(manager.Save.settings.mouseGuide);
            Assert.IsFalse(manager.Battle.Dragon.IsGuided, "자동 이동이면 커서를 따라가지 않는다");

            var modeButton = GameObject.Find("HUD").GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Mode");
            modeButton.onClick.Invoke();
            Assert.IsTrue(input.MouseMode);
            Assert.IsTrue(manager.Save.settings.mouseGuide);
            yield return TestScreens.Capture(camera, "control_mode");

            // 일시정지 중에는 바꾸지 않는다
            manager.SetPaused(true);
            manager.ToggleGuideMode();
            Assert.IsTrue(input.MouseMode);
            manager.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator EndSession_SettlesAndShowsResult()
        {
            // 일시정지 메뉴 "세션 끝내기": 확인 후 이번 판을 끝내고 지금까지 모은 골드로 정산해 결과창 (10-14)
            manager.StartStage();
            yield return Frames(FrameRate * 8);
            double goldBefore = manager.Save.goldCoin;
            manager.SetPaused(true);
            yield return null;
            var pause = GameObject.Find("PauseMenu");
            var end = pause.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "EndSession");
            Assert.IsTrue(end.interactable);
            end.onClick.Invoke();
            yield return null;
            Assert.IsTrue(GameObject.Find("Confirm") != null, "확인 상자가 먼저 뜬다");
            yield return TestScreens.Capture(camera, "end_session_confirm");
            pause.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Yes").onClick.Invoke();
            // 종료 슬로모션(실시간 0.5초)이 끝나야 결과창이 뜬다. 테스트는 프레임을 빨리 돌려 프레임 수로는 부족하다
            for (int i = 0; i < FrameRate * 120 && manager.State != GameState.Result; i++) yield return null;
            Assert.AreEqual(GameState.Result, manager.State);
            Assert.AreEqual(SessionEnd.Retired, manager.LastResult.end);
            Assert.IsFalse(manager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(goldBefore + manager.LastResult.earnedGold, manager.Save.goldCoin, 1e-6, "모은 골드는 정산된다");
            yield return TestScreens.Capture(camera, "end_session_result");
        }

        [UnityTest]
        public IEnumerator EndSession_DuringStarter_StopsStarterAndSettles()
        {
            // 스타터 중에도 세션 끝내기: 스타터를 멈추고 카메라·드래곤을 되돌린 뒤 결과창 (10-14)
            manager.enableStarter = true;
            manager.StartStage();
            yield return Frames(FrameRate * 2);
            Assert.IsTrue(manager.Battle.InStarter, "스타터 중이어야 한다");
            manager.SetPaused(true);
            yield return null;
            var end = GameObject.Find("PauseMenu").GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "EndSession");
            Assert.IsTrue(end.interactable, "스타터 중에도 누를 수 있다");
            manager.EndSession();
            Assert.IsFalse(manager.Battle.InStarter);
            Assert.AreEqual(Tuning.StandHeight, manager.Battle.Dragon.transform.position.y, 1e-3, "드래곤은 땅으로 돌아온다");
            for (int i = 0; i < FrameRate * 120 && manager.State != GameState.Result; i++) yield return null;
            Assert.AreEqual(GameState.Result, manager.State);
            Assert.AreEqual(SessionEnd.Retired, manager.LastResult.end);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(GameObject.Find("Starter"), "스타터 화면이 남지 않는다");
        }
    }
}
#endif
