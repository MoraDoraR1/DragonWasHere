using System.Collections.Generic;
using System.IO;
using DWH.UI;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// UiTheme 에셋을 만든다: UI 에셋 V15 스프라이트 전부와 Noto Sans KR(Fix_Resource 폰트).
    /// 배치모드: -executeMethod DWH.Editor.UiThemeBuilder.Build
    /// </summary>
    public static class UiThemeBuilder
    {
        public const string ThemePath = "Assets/_Project/Data/UiTheme.asset";
        const string KitArt = ArtScaleSettings.ArtRoot + ArtScaleSettings.FixRoot + "UI&폰트/01_UI에셋/assets";
        const string FontPath = ArtScaleSettings.ArtRoot + ArtScaleSettings.FixRoot + "UI&폰트/02_폰트/원본/static/NotoSansKR-Medium.ttf";
        const string FallbackFontPath = ArtScaleSettings.ArtRoot + ArtScaleSettings.FixRoot + "UI&폰트/02_폰트/원본/static/NotoSansKR-Bold.ttf";

        [MenuItem("DWH/Build UI Theme")]
        public static UiTheme Build()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            theme.fallbackFont = AssetDatabase.LoadAssetAtPath<Font>(FallbackFontPath);
            var kit = new List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { KitArt }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) kit.Add(sprite);
            }
            kit.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            theme.kit = kit.ToArray();

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] UI 테마 생성: 폰트 {(theme.font != null ? theme.font.name : "없음")}, UI 스프라이트 {theme.kit.Length}개");
            return theme;
        }
    }
}
