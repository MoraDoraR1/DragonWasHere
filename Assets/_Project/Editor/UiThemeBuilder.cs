using System.IO;
using DWH.UI;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// UiTheme 에셋을 만든다. 프레임 스프라이트는 Tools/make_ui_frames.py로 먼저 만들어 둔다.
    /// 배치모드: -executeMethod DWH.Editor.UiThemeBuilder.Build
    /// </summary>
    public static class UiThemeBuilder
    {
        public const string ThemePath = "Assets/_Project/Data/UiTheme.asset";
        const string UiArt = "Assets/_Project/Art/UI";
        const string FontPath = "Assets/_Project/Fonts/Jua-Regular.ttf";
        const string FallbackFontPath = "Assets/_Project/Fonts/NotoSansKR-Regular.ttf";
        const string KitArt = "Assets/_Project/Art/UIKit";

        [MenuItem("DWH/Build UI Theme")]
        public static UiTheme Build()
        {
            // 임포트 규칙에 UI(9-slice)가 없으면 추가하고 UI 스프라이트를 다시 임포트한다
            var settings = AssetDatabase.LoadAssetAtPath<ArtScaleSettings>(ArtScaleSettings.AssetPath);
            if (settings != null && settings.AddMissingDefaultRules())
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiArt, KitArt }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            theme.fallbackFont = AssetDatabase.LoadAssetAtPath<Font>(FallbackFontPath);
            // UI 패키지 v1: 프레임·아이콘·바·오버레이 전부
            var kit = new System.Collections.Generic.List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { KitArt }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) kit.Add(sprite);
            }
            kit.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            theme.kit = kit.ToArray();
            theme.panelDark = Sprite("panel_dark");
            theme.panelCream = Sprite("panel_cream");
            theme.panelInk = Sprite("panel_ink");
            theme.buttonGold = Sprite("button_gold");
            theme.buttonDark = Sprite("button_dark");
            theme.buttonSoul = Sprite("button_soul");
            theme.chipGold = Sprite("chip_gold");
            theme.chipCream = Sprite("chip_cream");
            theme.ring = Sprite("ring_white");
            theme.barBack = Sprite("bar_back");
            theme.barFill = Sprite("bar_fill");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] UI 테마 생성: 폰트 {(theme.font != null ? theme.font.name : "없음")} + {(theme.fallbackFont != null ? theme.fallbackFont.name : "없음")}, UI 패키지 스프라이트 {theme.kit.Length}개");
            return theme;
        }

        static Sprite Sprite(string name)
        {
            string path = $"{UiArt}/{name}.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException($"UI 스프라이트가 없습니다: {path}. python Tools/make_ui_frames.py를 먼저 실행하세요.");
        }
    }
}
