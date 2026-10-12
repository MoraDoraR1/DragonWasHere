#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Stage;
using DWH.InGame.Starter;
using DWH.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DWH.Tests
{
    /// <summary>
    /// 스테이지 스타터 (M10, 4.7). 대본 입력이 정한 시점에 클릭한다:
    /// 조준 단계는 일정 시간 뒤, 게이지 단계는 원하는 판정이 나오는 순간.
    /// </summary>
    public class StageStarterTests
    {
        const int FrameRate = 60;
        static readonly string ScreenFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Screens");

        GameDatabase db;
        ArtCatalog art;
        Camera camera;
        BattleController battle;
        System.Collections.Generic.Dictionary<InGame.Buildings.Building, double> hpAtFinish;

        class ScriptedInput : IStarterInput
        {
            readonly float aimSeconds;
            readonly StarterGrade want;
            float aiming;
            public ScriptedInput(float aimDelay, StarterGrade grade) { aimSeconds = aimDelay; want = grade; }

            public bool Pressed(StageStarter s)
            {
                if (s.Phase == StarterPhase.Aiming) return (aiming += Time.deltaTime) >= aimSeconds;
                if (s.Phase == StarterPhase.Gauge) return s.Gauge.Judge() == want;
                return false;
            }
        }

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            UiTheme.Active = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/_Project/Data/UiTheme.asset");
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            Time.captureFramerate = FrameRate;
            Directory.CreateDirectory(ScreenFolder);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (battle != null) Object.Destroy(battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(camera.gameObject);
        }

        /// <summary>UI를 포함한 캡처 (GameFlowTests와 같은 방법)</summary>
        IEnumerator Capture(string name)
        {
            yield return null;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.activeInHierarchy).ToArray();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1f; c.sortingOrder += 10000; }
            var rt = new RenderTexture(1280, 720, 24);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ScreenFolder, name + ".png"), tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
            foreach (var c in canvases) { c.sortingOrder -= 10000; c.renderMode = RenderMode.ScreenSpaceOverlay; }
        }

        IEnumerator RunStarter(string stageId, StarterGrade grade, float aimSeconds, string capturePrefix = null)
        {
            battle = BattleController.Create(db, art, stageId, camera, new AutoOnlyInput(), starterInput: new ScriptedInput(aimSeconds, grade));
            BattleHud.Create(battle);
            // 스타터가 끝난 순간의 체력 (그 뒤로는 몸통 박치기가 시작되므로)
            battle.Starter.Finished += _ => hpAtFinish = battle.Buildings.ToDictionary(b => b, b => b.Hp);
            Assert.IsTrue(battle.InStarter);
            Assert.IsFalse(battle.Dragon.Controllable, "스타터 중에는 전투가 멈춘다");
            bool aimShot = false, gaugeShot = false, introChecked = false;
            for (int i = 0; i < FrameRate * 30 && battle.InStarter; i++)
            {
                yield return null;
                // 시작(줌아웃) 중에도 드래곤은 땅이 아니라 화면 가로 정중앙 상공에 있다
                if (battle.Starter.Phase == StarterPhase.Intro && i == FrameRate / 2)
                {
                    introChecked = true;
                    Assert.AreEqual(camera.transform.position.x, battle.Dragon.transform.position.x, 0.05f, "화면 가로 정중앙");
                    Assert.Greater(battle.Dragon.transform.position.y, camera.transform.position.y, "화면 위쪽 절반(상공)");
                    if (capturePrefix != null) yield return Capture($"{capturePrefix}_intro");
                }
                if (capturePrefix == null) continue;
                if (!aimShot && battle.Starter.Phase == StarterPhase.Aiming && i > FrameRate * 2) { aimShot = true; yield return Capture($"{capturePrefix}_aim"); }
                if (!gaugeShot && battle.Starter.Phase == StarterPhase.Gauge) { gaugeShot = true; yield return Capture($"{capturePrefix}_gauge"); }
            }
            Assert.IsFalse(battle.InStarter, "스타터가 끝나야 한다");
            Assert.IsTrue(introChecked);
            Assert.AreEqual(grade, battle.Starter.Grade);
            // 클릭한 순간의 바 위치로 판정하고, 그 뒤로 바는 멈춰 있다
            Assert.AreEqual(battle.Starter.JudgedBar, battle.Starter.Gauge.Bar, "판정 후 바가 움직이지 않는다");
            Assert.AreEqual(grade, StarterGauge.Judge(battle.Starter.JudgedBar, battle.Starter.Gauge.ZoneCenter,
                battle.Starter.Gauge.GreatWidth, battle.Starter.Gauge.GoodWidth), "클릭한 순간의 위치 = 판정");
            Assert.IsTrue(battle.Dragon.Controllable, "스타터가 끝나면 전투가 시작된다");
            Assert.AreEqual((float)db.balance.groundHeight, battle.Dragon.transform.position.y, 1e-3, "낙하 후 지면에 선다");
        }

        [UnityTest]
        public IEnumerator Great_HitsTargetAndFiveEachSide()
        {
            yield return RunStarter("1-3", StarterGrade.Great, 2.5f, "starter_1-3");
            var s = battle.Starter;
            var ordered = battle.Buildings.OrderBy(b => b.CenterX).ToList();
            int i = ordered.IndexOf(s.Target);
            int expected = Math.Min(ordered.Count - 1, i + 5) - Math.Max(0, i - 5) + 1;
            Assert.AreEqual(expected, s.HitCount, "표적과 양옆 5개씩 (맵 끝이면 있는 만큼)");
            double damage = battle.Stats.AttackPower * 2.0;
            var hit = ordered.Skip(Math.Max(0, i - 5)).Take(expected).ToList();
            foreach (var b in hit)
                Assert.AreEqual(Math.Max(0, b.MaxHp - damage), hpAtFinish[b], 1e-6, $"{b.name}: 공격력 200%");
            Assert.IsTrue(ordered.Except(hit).All(b => hpAtFinish[b] == b.MaxHp), "범위 밖 건물은 그대로");
            yield return Capture("starter_1-3_after");
            Debug.Log($"[DWH] 스타터 1-3 Great: 표적 {s.Target.name}, {s.HitCount}채 타격, 파괴 {battle.DestroyedCount}");

            // 전투가 이어진다
            for (int f = 0; f < FrameRate * 2; f++) yield return null;
            Assert.AreEqual(BattleState.Playing, battle.State);
        }

        /// <summary>
        /// 4-4(건물 34채, 가장 긴 축에 속하는 맵): 스타터 줌아웃 화면에 맵 전체(스테이지 양 끝)와 모든 건물이 들어오는지,
        /// 건물이 맵 처음부터 끝까지 일정한 간격으로 놓였는지 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Stage44_ZoomOutShowsWholeMapWithAllBuildings()
        {
            battle = BattleController.Create(db, art, "4-4", camera, new AutoOnlyInput(), starterInput: new ScriptedInput(999f, StarterGrade.Fail));
            BattleHud.Create(battle);
            var stage = db.Stage("4-4");
            Assert.AreEqual(stage.normalCount + stage.attackCount + stage.landmarkCount, battle.Buildings.Count, "스테이지 표의 건물 수만큼 설치");

            for (int i = 0; i < FrameRate * 3 && battle.Starter.Phase != StarterPhase.Aiming; i++) yield return null;
            Assert.AreEqual(StarterPhase.Aiming, battle.Starter.Phase);
            yield return Capture("starter_4-4_whole");

            float half = camera.orthographicSize * camera.aspect;
            float left = camera.transform.position.x - half, right = camera.transform.position.x + half;
            float bottom = camera.transform.position.y - camera.orthographicSize, top = camera.transform.position.y + camera.orthographicSize;
            Assert.LessOrEqual(left, battle.StageLeft + 1e-3f, "맵 왼쪽 끝이 화면 안");
            Assert.GreaterOrEqual(right, battle.StageRight - 1e-3f, "맵 오른쪽 끝이 화면 안");
            foreach (var b in battle.Buildings)
            {
                Assert.That(b.Left, Is.GreaterThanOrEqualTo(left), $"{b.name} 왼쪽");
                Assert.That(b.Right, Is.LessThanOrEqualTo(right), $"{b.name} 오른쪽");
                Assert.That(b.Hitbox.yMin, Is.GreaterThanOrEqualTo(bottom), $"{b.name} 바닥");
                Assert.That(b.Hitbox.yMax, Is.LessThanOrEqualTo(top), $"{b.name} 꼭대기");
                Assert.IsTrue(b.GetComponent<SpriteRenderer>().enabled && b.GetComponent<SpriteRenderer>().sprite != null, $"{b.name} 스프라이트");
            }

            // 건물 배치: 첫 건물은 시작 여백 뒤, 마지막 건물 뒤에도 여백, 사이 간격은 일정 (그림 경계 기준 0.5 unit, 충돌 박스 기준은 그보다 넓다)
            var ordered = battle.Buildings.OrderBy(b => b.CenterX).ToList();
            var gaps = ordered.Zip(ordered.Skip(1), (a, b) => b.Left - a.Right).ToList();
            float spanCovered = (ordered.Last().Right - ordered.First().Left) / (battle.StageRight - battle.StageLeft);
            Debug.Log($"[DWH] 4-4 줌아웃: 카메라 크기 {camera.orthographicSize:0.00}, 화면 {left:0.0}~{right:0.0}, 맵 {battle.StageLeft:0.0}~{battle.StageRight:0.0}, " +
                      $"건물 {ordered.Count}채 {ordered.First().Left:0.0}~{ordered.Last().Right:0.0} (맵 폭의 {spanCovered * 100:0}%), " +
                      $"충돌 박스 사이 간격 {gaps.Min():0.00}~{gaps.Max():0.00}, 종류 " +
                      string.Join(" ", ordered.GroupBy(b => b.Type).Select(g => $"{g.Key} {g.Count()}")));
            Assert.Greater(spanCovered, 0.8f, "건물이 맵 대부분에 걸쳐 놓여 있다");
            Assert.IsTrue(gaps.All(g => g > 0), "건물끼리 겹치지 않는다");

            // 표적이 맵 오른쪽 끝까지 간다: 조준 중 드래곤이 양 끝을 왕복한다
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < FrameRate * 5; i++)
            {
                yield return null;
                minX = Mathf.Min(minX, battle.Dragon.transform.position.x);
                maxX = Mathf.Max(maxX, battle.Dragon.transform.position.x);
            }
            Debug.Log($"[DWH] 4-4 조준 왕복 범위 {minX:0.0}~{maxX:0.0}");
            Assert.Less(minX, ordered.First().CenterX, "왼쪽 끝 건물까지 조준할 수 있다");
            Assert.Greater(maxX, ordered.Last().CenterX, "오른쪽 끝 건물까지 조준할 수 있다");
        }

        [UnityTest]
        public IEnumerator Good_HitsTargetAndTwoEachSide()
        {
            yield return RunStarter("1-3", StarterGrade.Good, 1.0f);
            var s = battle.Starter;
            var ordered = battle.Buildings.OrderBy(b => b.CenterX).ToList();
            int i = ordered.IndexOf(s.Target);
            Assert.AreEqual(Math.Min(ordered.Count - 1, i + 2) - Math.Max(0, i - 2) + 1, s.HitCount, "표적과 양옆 2개씩");
            double damage = battle.Stats.AttackPower * 1.3;
            Assert.AreEqual(Math.Max(0, s.Target.MaxHp - damage), hpAtFinish[s.Target], 1e-6, "공격력 130%");
        }

        [UnityTest]
        public IEnumerator Fail_DealsNoDamage()
        {
            yield return RunStarter("1-1", StarterGrade.Fail, 0.5f);
            Assert.AreEqual(0, battle.Starter.HitCount);
            Assert.IsTrue(battle.Buildings.All(b => hpAtFinish[b] == b.MaxHp));
            Assert.AreEqual(0, battle.AirUnits.Units.Count, "스타터 시간은 공중 유닛 등장 시간에 들어가지 않는다");
        }
    }
}
#endif
