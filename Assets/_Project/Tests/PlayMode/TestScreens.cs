#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DWH.Tests
{
    /// <summary>
    /// 테스트 화면 캡처 (Logs/Screens). 배치모드에서는 ScreenCapture가 동작하지 않으므로
    /// 캡처하는 동안만 캔버스를 카메라 렌더 모드로 바꿔 카메라로 그린다.
    /// </summary>
    public static class TestScreens
    {
        public static readonly string Folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Screens");
        const int SortingOffset = 10000;

        public static void Clear()
        {
            if (Directory.Exists(Folder))
                foreach (var file in Directory.GetFiles(Folder, "*.png")) File.Delete(file);
        }

        public static IEnumerator Capture(Camera camera, string name, int width = 1280, int height = 720)
        {
            yield return null;
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.activeInHierarchy).ToArray();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = camera;
                c.planeDistance = 1f;
                c.sortingOrder += SortingOffset;
            }
            // 캔버스 배율이 렌더 텍스처 크기에 맞춰 바뀌도록 한 프레임 기다린다
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
            foreach (var c in canvases)
            {
                if (c == null) continue;
                c.sortingOrder -= SortingOffset;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
#endif
