using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>Assets/_Project/Art 아래 이미지를 ArtScaleSettings 규칙대로 스프라이트로 임포트한다.</summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string UiKitRoot = ArtScaleSettings.ArtRoot + "UIKit/";
        public const string UiKitManifest = UiKitRoot + "sprite-manifest.csv";

        /// <summary>sprite-manifest.csv의 id → border_LBRT. 예: panel-normal → (24, 30, 24, 24)</summary>
        public static Dictionary<string, Vector4> UiKitBorders()
        {
            var result = new Dictionary<string, Vector4>();
            if (!File.Exists(UiKitManifest)) return result;
            var lines = File.ReadAllLines(UiKitManifest);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int open = line.IndexOf('['), close = line.IndexOf(']');
                if (open < 0 || close < open) continue;
                string id = line.Substring(0, line.IndexOf(',')).Trim().TrimStart('\uFEFF');
                var n = line.Substring(open + 1, close - open - 1).Split(',');
                if (n.Length != 4) continue;
                result[id] = new Vector4(float.Parse(n[0]), float.Parse(n[1]), float.Parse(n[2]), float.Parse(n[3]));
            }
            return result;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtScaleSettings.ArtRoot, System.StringComparison.Ordinal))
                return;

            var settings = ArtScaleSettings.LoadOrDefault();
            var rule = settings.FindRule(assetPath.Substring(ArtScaleSettings.ArtRoot.Length));
            if (rule == null)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = rule.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = settings.filterMode;
            importer.maxTextureSize = settings.maxTextureSize;
            importer.textureCompression = rule.uncompressed ? TextureImporterCompression.Uncompressed : settings.compression;
            importer.spriteBorder = new Vector4(rule.sliceBorder, rule.sliceBorder, rule.sliceBorder, rule.sliceBorder);
            // UI 패키지: 규격표의 경계(왼쪽, 아래, 오른쪽, 위 = Unity spriteBorder 순서)와 권장 임포트 값
            if (assetPath.StartsWith(UiKitRoot, System.StringComparison.Ordinal))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                if (UiKitBorders().TryGetValue(Path.GetFileNameWithoutExtension(assetPath), out var border))
                    importer.spriteBorder = border;
            }

            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
            spriteSettings.spriteAlignment = (int)rule.pivot;
            importer.SetTextureSettings(spriteSettings);
        }
    }
}
