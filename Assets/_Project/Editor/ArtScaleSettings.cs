using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// 아트 카테고리별 스프라이트 임포트 설정 (MVP 제작 가이드 5장 "표시 크기"). 값은 [임시]이며 화면을 보고 여기서만 조정한다.
    /// 원본 크기가 카테고리마다 달라 Pixels Per Unit으로 표시 크기를 맞춘다. 코드에서 scale을 곱하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/Art Scale Settings", fileName = "ArtScaleSettings")]
    public class ArtScaleSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Settings/ArtScaleSettings.asset";
        public const string ArtRoot = "Assets/_Project/Art/";
        /// <summary>리소스 폴더(Fix_Resource)를 그대로 복사한 곳. resource.res_path = Art/ 기준 경로</summary>
        public const string FixRoot = "Fix_Resource/";

        [Serializable]
        public class Rule
        {
            [Tooltip("Art 폴더 기준 경로 접두어. 가장 길게 일치하는 규칙을 쓴다.")]
            public string pathPrefix;
            public float pixelsPerUnit = 100f;
            public SpriteAlignment pivot = SpriteAlignment.Center;
            [Tooltip("9-slice 경계(px). 0이면 늘리지 않는 일반 스프라이트")]
            public int sliceBorder;
            [Tooltip("UI 스프라이트는 압축하지 않는다 (테두리 번짐 방지)")]
            public bool uncompressed;
            [Tooltip("알파가 이 값 이하인 옅은 픽셀을 투명으로 지운다 (0이면 그대로)")]
            public int clearAlphaAtOrBelow;

            public Rule(string pathPrefix, float pixelsPerUnit, SpriteAlignment pivot, int sliceBorder = 0, bool uncompressed = false, int clearAlphaAtOrBelow = 0)
            {
                this.pathPrefix = pathPrefix;
                this.pixelsPerUnit = pixelsPerUnit;
                this.pivot = pivot;
                this.sliceBorder = sliceBorder;
                this.uncompressed = uncompressed;
                this.clearAlphaAtOrBelow = clearAlphaAtOrBelow;
            }
        }

        public int maxTextureSize = 2048;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public FilterMode filterMode = FilterMode.Bilinear;

        /// <summary>건물·유닛·자원 PNG의 옅은 픽셀(알파 1~8)을 지우는 기준 (가이드 5장)</summary>
        public const int FaintAlpha = 8;

        public List<Rule> rules = new()
        {
            // 배경 19.2 × 10.8 unit (바닥 19.2 × 2.44)
            new Rule(FixRoot + "배경/", 100f, SpriteAlignment.BottomCenter),
            // 일반·공격 건물 높이 2.4(180px) 또는 4.4(330px) unit, 랜드마크 약 8.7 unit
            new Rule(FixRoot + "건물/", 75f, SpriteAlignment.BottomCenter, clearAlphaAtOrBelow: FaintAlpha),
            // 공중 유닛 1 × 1 unit
            new Rule(FixRoot + "적 유닛/", 200f, SpriteAlignment.Center, clearAlphaAtOrBelow: FaintAlpha),
            // 드래곤 1단계 약 2.3 unit 폭 → 4단계 약 5 unit. 파츠는 절대 잘라 내지 않는다(좌표가 어긋남). 회전점은 조립 코드에서 맞춘다
            new Rule(FixRoot + "드래곤/", 300f, SpriteAlignment.Center),
            // 드랍 자원 0.4 × 0.4 unit
            new Rule(FixRoot + "UI&폰트/03_자원/", 1350f, SpriteAlignment.Center, clearAlphaAtOrBelow: FaintAlpha),
            // UI(V15): 둥근 사각형 프레임이라 경계 20px로 9-slice한다. 아이콘은 Simple로 그리므로 경계가 있어도 그대로다
            new Rule(FixRoot + "UI&폰트/01_UI에셋/", 100f, SpriteAlignment.Center, 20, true),
            // 스킬·피격 이펙트 540px 프레임 → 2.7 unit [임시]
            new Rule(FixRoot + "스킬 이펙트/", 200f, SpriteAlignment.Center),
            new Rule(FixRoot + "피격 이펙트/", 200f, SpriteAlignment.Center),
        };

        public Rule FindRule(string artRelativePath)
        {
            Rule best = null;
            foreach (var rule in rules)
            {
                if (artRelativePath.StartsWith(rule.pathPrefix, StringComparison.Ordinal)
                    && (best == null || rule.pathPrefix.Length > best.pathPrefix.Length))
                    best = rule;
            }
            return best;
        }

        /// <summary>저장된 에셋이 없으면 기본값을 쓴다. 첫 임포트 때는 에셋이 아직 없을 수 있다.</summary>
        public static ArtScaleSettings LoadOrDefault()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ArtScaleSettings>(AssetPath);
            return settings != null ? settings : CreateInstance<ArtScaleSettings>();
        }

        /// <summary>에셋이 없으면 기본값으로 만든다</summary>
        public static ArtScaleSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ArtScaleSettings>(AssetPath);
            if (settings != null) return settings;
            settings = CreateInstance<ArtScaleSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
