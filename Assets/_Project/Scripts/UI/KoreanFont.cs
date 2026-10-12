using TMPro;
using UnityEngine;

namespace DWH.UI
{
    /// <summary>
    /// 한글 표시용 TMP 폰트. UiTheme의 폰트(Jua, OFL)로 동적 폰트 에셋을 만들고, Jua에 없는 기호(×, →, · 등)는
    /// 대체 글꼴(Noto Sans KR, OFL)로 채운다.
    /// 테마가 없으면(테스트 등) Windows 기본 폰트(맑은 고딕)로 대신한다. 맑은 고딕은 배포할 수 없으므로 개발 중 대체용이다.
    /// </summary>
    public static class KoreanFont
    {
        static readonly (string family, string style)[] OsFallbacks =
        {
            ("Malgun Gothic", "Regular"),
            ("맑은 고딕", "Regular"),
        };

        static TMP_FontAsset font;
        static Font source;

        public static TMP_FontAsset Get()
        {
            var themeFont = UiTheme.Active != null ? UiTheme.Active.font : null;
            if (font != null && source == themeFont) return font;

            source = themeFont;
            if (themeFont != null)
            {
                font = TMP_FontAsset.CreateFontAsset(themeFont);
                if (font != null)
                {
                    var fallback = UiTheme.Active.fallbackFont != null ? TMP_FontAsset.CreateFontAsset(UiTheme.Active.fallbackFont) : null;
                    if (fallback != null)
                    {
                        font.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                        font.fallbackFontAssetTable.Add(fallback);
                    }
                    return font;
                }
            }
            foreach (var (family, style) in OsFallbacks)
            {
                font = TMP_FontAsset.CreateFontAsset(family, style);
                if (font != null) return font;
            }
            Debug.LogWarning("[DWH] 한글 폰트를 찾지 못해 TMP 기본 폰트를 씁니다. 한글이 네모로 보일 수 있습니다.");
            return font = TMP_Settings.defaultFontAsset;
        }
    }
}
