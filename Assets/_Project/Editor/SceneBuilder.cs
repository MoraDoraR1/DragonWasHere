using System.IO;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// 게임 씬을 만든다 (0.2: 씬 YAML을 손으로 고치지 않고 메뉴로 재현). 씬에는 카메라와 GameManager만 두고
    /// 화면·레벨·드래곤·HUD는 런타임에 데이터로 생성한다.
    /// 배치모드: -executeMethod DWH.Editor.SceneBuilder.BuildAll
    /// </summary>
    public static class SceneBuilder
    {
        public const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        const string OldBattleScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>데이터 임포트 → 아트 카탈로그 → TMP 필수 리소스 → 전투 씬 순서로 모두 만든다</summary>
        [MenuItem("DWH/Build All (Data, Art, Scenes)")]
        public static void BuildAll()
        {
            ReimportChangedArt();
            CsvImporter.ImportAll();
            ArtCatalogBuilder.Build();
            EnsureTmpEssentials();
            UiThemeBuilder.Build();
            BuildGameScene();
        }

        /// <summary>ArtScaleSettings의 PPU가 바뀐 이미지(예: 건물 크기 조정)를 다시 임포트한다</summary>
        static void ReimportChangedArt()
        {
            var settings = ArtScaleSettings.LoadOrDefault();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtScaleSettings.ArtRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rule = settings.FindRule(path.Substring(ArtScaleSettings.ArtRoot.Length));
                if (rule == null || AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (Mathf.Approximately(importer.spritePixelsPerUnit, rule.pixelsPerUnit)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
            if (count > 0) Debug.Log($"[DWH] PPU가 바뀐 이미지 {count}개를 다시 임포트했습니다.");
        }

        [MenuItem("DWH/Build Scenes")]
        public static void BuildGameScene()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{CsvImporter.AssetFolder}/GameDatabase.asset");
            var art = AssetDatabase.LoadAssetAtPath<InGame.ArtCatalog>(ArtCatalogBuilder.CatalogPath);
            if (database == null || art == null)
            {
                Debug.LogError("[DWH] GameDatabase 또는 ArtCatalog가 없습니다. DWH/Build All을 먼저 실행하세요.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = (float)database.balance.cameraOrthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0, camera.orthographicSize, -10);

            var manager = new GameObject("GameManager").AddComponent<GameManager>();
            manager.database = database;
            manager.art = art;
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>(UiThemeBuilder.ThemePath);

            Directory.CreateDirectory(Path.GetDirectoryName(GameScenePath));
            EditorSceneManager.SaveScene(scene, GameScenePath);
            if (File.Exists(OldBattleScenePath)) AssetDatabase.DeleteAsset(OldBattleScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScenePath, true) };
            Debug.Log($"[DWH] 게임 씬 생성: {GameScenePath}");
        }

        /// <summary>
        /// TextMeshPro 기본 셰이더와 설정. 없으면 TMP 텍스트가 보이지 않는다.
        /// 에디터에서는 바로 임포트된다. 배치모드에서는 ImportPackage가 종료 전에 처리되지 않으므로
        /// 명령줄 옵션 -importPackage "&lt;ugui 패키지&gt;/Package Resources/TMP Essential Resources.unitypackage"를 쓴다.
        /// </summary>
        public static void EnsureTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath)) return;
            AssetDatabase.ImportPackage(TmpEssentialsPackage, false);
            if (Application.isBatchMode)
                Debug.LogWarning("[DWH] 배치모드에서는 TMP Essential Resources가 임포트되지 않을 수 있습니다. -importPackage 옵션을 쓰세요.");
            else
                Debug.Log("[DWH] TMP Essential Resources 임포트");
        }
    }
}
