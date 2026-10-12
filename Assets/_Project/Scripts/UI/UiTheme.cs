using UnityEngine;

namespace DWH.UI
{
    /// <summary>
    /// UI 디자인 토큰: 폰트, 9-slice 프레임 스프라이트, 아이콘, 색.
    /// UI 에셋 V15(Fix_Resource/UI&amp;폰트/01_UI에셋)를 쓴다: 밝은 크림 패널, 먹색 테두리, 골드·보라 버튼. 폰트는 Noto Sans KR(OFL).
    /// 스프라이트는 kit 배열에 이름(파일 이름) 그대로 담아 Kit("panel-normal")처럼 찾는다.
    /// DWH/Build All이 Assets/_Project/Data/UiTheme.asset으로 만든다. 없으면 UiFactory가 단색 그레이박스로 그린다.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        /// <summary>현재 쓰는 테마. GameManager가 설정한다</summary>
        public static UiTheme Active { get; set; }

        [Header("폰트 (OFL)")]
        [Tooltip("기본 글꼴 (Noto Sans KR)")]
        public Font font;
        [Tooltip("기본 글꼴에 없는 글자를 채우는 글꼴")]
        public Font fallbackFont;

        [Header("UI 에셋 V15 스프라이트")]
        public Sprite[] kit;

        System.Collections.Generic.Dictionary<string, Sprite> kitByName;

        /// <summary>패키지 스프라이트를 파일 이름으로 찾는다. 없으면 null</summary>
        public Sprite Kit(string name)
        {
            if (kit == null || string.IsNullOrEmpty(name)) return null;
            if (kitByName == null || kitByName.Count != kit.Length)
            {
                kitByName = new System.Collections.Generic.Dictionary<string, Sprite>();
                foreach (var s in kit) if (s != null) kitByName[s.name] = s;
            }
            return kitByName.TryGetValue(name, out var sprite) ? sprite : null;
        }

        /// <summary>현재 테마의 패키지 스프라이트 (테마가 없으면 null)</summary>
        public static Sprite KitSprite(string name) => Active != null ? Active.Kit(name) : null;

        // 색 (UI 에셋 V15). 패널이 밝아서 본문 글자(InkText)는 먹색이다. 화면 위에 바로 쓰는 글자는 TextLight + 테두리
        public static readonly Color Cream = Hex(0xf5e6cc);
        public static readonly Color Chocolate = Hex(0x493638);
        public static readonly Color Ink = Hex(0x20191b);
        public static readonly Color InkText = Hex(0x3a2b27);
        public static readonly Color InkDim = Hex(0x7d6a60);
        public static readonly Color Gold = Hex(0xefbc52);
        public static readonly Color GoldDeep = Hex(0xbd8d72);
        public static readonly Color Ember = Hex(0xdd8064);
        public static readonly Color Jade = Hex(0x83b594);
        public static readonly Color JadeDeep = Hex(0x557b5c);
        public static readonly Color Soul = Hex(0xb09bce);
        public static readonly Color Hp = Hex(0xd96662);
        public static readonly Color Energy = Hex(0xefbc52);
        public static readonly Color Rock = Hex(0x8b6b4a);
        public static readonly Color LineIdle = Hex(0x8c7d78);
        public static readonly Color LineLit = Hex(0xbd8d72);
        public static readonly Color LockedPurple = Hex(0x72676b);
        public static readonly Color TextLight = Hex(0xf5e6cc);
        public static readonly Color Panel = Hex(0x493638);
        public static readonly Color Well = Hex(0x30282c);
        public static readonly Color Background = Hex(0x30272b);
        public static readonly Color Disabled = Hex(0x72676b);
        public static readonly Color Dim = new(0.5f, 0.5f, 0.5f);

        public static Color Hex(int rgb) =>
            new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        public static string HexString(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
