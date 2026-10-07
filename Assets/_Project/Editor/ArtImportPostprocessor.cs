using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>Assets/_Project/Art 아래 이미지를 ArtScaleSettings 규칙대로 스프라이트로 임포트한다.</summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
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
            importer.textureCompression = settings.compression;

            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
            spriteSettings.spriteAlignment = (int)rule.pivot;
            importer.SetTextureSettings(spriteSettings);
        }
    }
}
