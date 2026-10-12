#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Stage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// M4: 28스테이지 전부를 생성해 데이터·허들·리소스 매핑이 맞는지 확인하고, 테마별 화면을 Logs/Screens/stages에 캡처한다.
    /// </summary>
    public class AllStagesTests
    {
        static readonly string ScreenFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Screens", "stages");

        GameDatabase db;
        ArtCatalog art;
        Camera camera;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            camera = new GameObject("Test Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Time.captureFramerate = 60;
        }

        [OneTimeSetUp]
        public void ClearScreens()
        {
            if (Directory.Exists(ScreenFolder))
                foreach (var f in Directory.GetFiles(ScreenFolder, "*.png")) File.Delete(f);
            Directory.CreateDirectory(ScreenFolder);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(camera.gameObject);
        }

        [UnityTest]
        public IEnumerator AllStages_BuildWithCorrectDataAndArt()
        {
            foreach (var stage in db.stages.rows)
            {
                var battle = BattleController.Create(db, art, stage.stageId, camera, new AutoOnlyInput());
                var theme = art.Theme(stage.theme);
                string where = stage.stageId;

                // 건물 수와 배치
                Assert.AreEqual(stage.normalCount, battle.Buildings.Count(b => b.Type == BuildingType.Normal), where);
                Assert.AreEqual(stage.attackCount, battle.Buildings.Count(b => b.Type == BuildingType.Attack), where);
                Assert.AreEqual(stage.landmarkCount, battle.Buildings.Count(b => b.Type == BuildingType.Landmark), where);
                if (stage.landmarkCount == 1)
                    Assert.AreEqual(BuildingType.Landmark, battle.Buildings[battle.Buildings.Count / 2].Type, $"{where} 랜드마크는 가운데");

                // 건물이 왼쪽부터 겹치지 않고 이어진다
                for (int i = 1; i < battle.Buildings.Count; i++)
                    Assert.Greater(battle.Buildings[i].Left, battle.Buildings[i - 1].Right, $"{where} {i}번 건물 겹침");

                foreach (var b in battle.Buildings)
                {
                    // 허들 배율이 적용된 HP와 원본 HP 기준 골드 (3.2, 3.3)
                    Assert.AreEqual(db.ActualHp(stage, b.Type), b.MaxHp, $"{where} {b.Order}번 HP");
                    Assert.AreEqual(db.BuildingGold(stage, b.Type), b.Gold, $"{where} {b.Order}번 골드");

                    // 테마별 리소스 (9.2)
                    var pool = b.Type switch
                    {
                        BuildingType.Normal => theme.normal,
                        BuildingType.Attack => theme.attack,
                        _ => new[] { theme.landmark },
                    };
                    CollectionAssert.Contains(pool, b.Sprite, $"{where} {b.Order}번 {b.Type} 스프라이트가 테마 {stage.theme} 것이 아니다");
                }
                // 공격 건물 2종은 번갈아 나온다
                var attackSprites = battle.Buildings.Where(b => b.Type == BuildingType.Attack).Select(b => b.Sprite).ToArray();
                for (int i = 1; i < attackSprites.Length; i++)
                    Assert.AreNotEqual(attackSprites[i - 1], attackSprites[i], $"{where} 공격 건물이 번갈아 나오지 않는다");

                Assert.AreEqual(art.Era(stage.era).far, battle.Background.FarSprite, $"{where} 시대 배경");

                // 몇 프레임 실제로 돌려 오류가 없는지 본다
                for (int f = 0; f < 30; f++) yield return null;

                // 테마의 첫 스테이지는 시작 화면, 랜드마크 스테이지는 랜드마크를 캡처
                if (stage.landmarkCount == 0)
                {
                    Capture($"{stage.stageId}_{stage.themeName}");
                }
                else
                {
                    camera.GetComponent<BattleCamera>().enabled = false;
                    var landmark = battle.Buildings.First(b => b.Type == BuildingType.Landmark);
                    camera.transform.position = new Vector3(landmark.CenterX, camera.transform.position.y, -10);
                    yield return null;
                    Capture($"{stage.stageId}_{stage.themeName}_landmark");
                    camera.GetComponent<BattleCamera>().enabled = true;
                }

                Object.Destroy(battle.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator UltraWideScreen_BackgroundCoversEdges()
        {
            // 21:9 화면에서도 하늘(원경) 양 끝이 비면 안 된다
            var battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            yield return null;
            var rt = new RenderTexture(840, 360, 24);
            camera.targetTexture = rt;
            camera.aspect = 840f / 360f;
            yield return null;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            foreach (int x in new[] { 0, rt.width - 1 })
                for (int y = rt.height / 2; y < rt.height; y += 10)   // 위쪽 절반 = 하늘
                    Assert.Greater(tex.GetPixel(x, y).grayscale, 0.05f, $"x={x}, y={y}이 검다");
            camera.targetTexture = null;
            camera.ResetAspect();
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(battle.gameObject);
        }

        void Capture(string name)
        {
            var rt = new RenderTexture(640, 360, 24);
            camera.targetTexture = rt;
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
        }
    }
}
#endif
