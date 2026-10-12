using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DWH.Analytics.Editor
{
    /// <summary>
    /// itch.io용 Web(WebGL) 빌드.
    /// - 팀 테스트: DWH_DEV 심볼 → is_dev=1, GA4 DebugView에 표시
    /// - 출시: is_dev=0
    /// 결과: Builds/Web_Test(또는 Web_Release) 폴더와, itch.io에 바로 올릴 수 있는 같은 이름의 .zip
    /// 배치모드: -executeMethod DWH.Analytics.Editor.DwhWebBuild.BuildTestBatch (또는 BuildReleaseBatch)
    /// </summary>
    public static class DwhWebBuild
    {
        const string TemplateName = "PROJECT:DragonFirebase";

        [MenuItem("DWH/Analytics/Build Web (Team Test)")]
        public static void BuildTest() => Build(true);

        [MenuItem("DWH/Analytics/Build Web (Release)")]
        public static void BuildRelease() => Build(false);

        public static void BuildTestBatch() => ExitWith(Build(true));
        public static void BuildReleaseBatch() => ExitWith(Build(false));

        public static bool Build(bool dev)
        {
            ApplyWebSettings();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[DWH Build] Build Profiles의 Scene List가 비어 있습니다.");
                return false;
            }

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", dev ? "Web_Test" : "Web_Release"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
                extraScriptingDefines = dev ? new[] { "DWH_DEV" } : null,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"[DWH Build] 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");
                return false;
            }

            if (!File.Exists(Path.Combine(outDir, "firebase-config.js")))
                Debug.LogWarning("[DWH Build] firebase-config.js가 없습니다. WebGL 템플릿이 DragonFirebase인지 확인하세요.");

            // itch.io는 zip을 열었을 때 바로 index.html이 보여야 한다.
            string zipPath = outDir + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            Debug.Log($"[DWH Build] 완료 ({(dev ? "팀 테스트" : "출시")}, v{Application.version}): {zipPath}");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(zipPath);
            return true;
        }

        /// <summary>itch.io에서 문제없이 열리는 Web 설정.</summary>
        public static void ApplyWebSettings()
        {
            PlayerSettings.WebGL.template = TemplateName;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            if (!Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", "DragonFirebase")))
                Debug.LogError("[DWH Build] Assets/WebGLTemplates/DragonFirebase 폴더가 없습니다.");
        }

        static void ExitWith(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
