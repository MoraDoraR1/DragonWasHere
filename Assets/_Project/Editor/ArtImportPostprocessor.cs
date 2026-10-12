using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// Assets/_Project/Art 아래 이미지를 ArtScaleSettings 규칙대로 스프라이트로 임포트한다 (가이드 5장).
    /// 건물·유닛·자원은 옅은 픽셀(알파 8 이하)을 투명으로 지운다. 원본 파일은 고치지 않는다.
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        ArtScaleSettings.Rule Rule()
        {
            if (!assetPath.StartsWith(ArtScaleSettings.ArtRoot, System.StringComparison.Ordinal))
                return null;
            return ArtScaleSettings.LoadOrDefault().FindRule(assetPath.Substring(ArtScaleSettings.ArtRoot.Length));
        }

        void OnPreprocessTexture()
        {
            var rule = Rule();
            if (rule == null)
                return;

            var settings = ArtScaleSettings.LoadOrDefault();
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = rule.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = settings.filterMode;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = settings.maxTextureSize;
            importer.textureCompression = rule.uncompressed ? TextureImporterCompression.Uncompressed : settings.compression;
            importer.spriteBorder = new Vector4(rule.sliceBorder, rule.sliceBorder, rule.sliceBorder, rule.sliceBorder);

            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
            spriteSettings.spriteAlignment = (int)rule.pivot;
            importer.SetTextureSettings(spriteSettings);
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            var rule = Rule();
            if (rule == null || rule.clearAlphaAtOrBelow <= 0)
                return;
            var pixels = texture.GetPixels32();
            bool changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0 || pixels[i].a > rule.clearAlphaAtOrBelow) continue;
                pixels[i] = new Color32(0, 0, 0, 0);
                changed = true;
            }
            if (!changed) return;
            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
}
