using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DWH.Editor
{
    /// <summary>
    /// URP 3D 템플릿으로 만든 프로젝트를 2D 렌더러 기반으로 전환한다 (임시unity제작.md 0.1, M0).
    /// 여러 번 실행해도 결과가 같다. 배치모드: -executeMethod DWH.Editor.ProjectSetup.ConvertTo2D
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsFolder = "Assets/_Project/Settings";
        const string RendererPath = SettingsFolder + "/Renderer2D.asset";
        const string PipelinePath = SettingsFolder + "/URP_2D.asset";
        // 템플릿의 3D 렌더러에서 후처리 리소스를 가져온다 (기본값을 만드는 URP API가 internal이라서)
        const string TemplateRendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("DWH/Setup/Convert Project To 2D")]
        public static void ConvertTo2D()
        {
            Directory.CreateDirectory(SettingsFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                ResourceReloader.ReloadAllNullIn(rendererData, "Packages/com.unity.render-pipelines.universal");

                // Renderer2DData.postProcessData는 internal이라 직렬화 필드로 설정한다
                var template = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(TemplateRendererPath);
                if (template != null)
                {
                    var serialized = new SerializedObject(rendererData);
                    serialized.FindProperty("m_PostProcessData").objectReferenceValue = template.postProcessData;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            if (AssetDatabase.LoadAssetAtPath<ArtScaleSettings>(ArtScaleSettings.AssetPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ArtScaleSettings>(), ArtScaleSettings.AssetPath);

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] 2D 전환 완료: {PipelinePath} (품질 레벨 {QualitySettings.names.Length}개에 적용)");
        }
    }
}
