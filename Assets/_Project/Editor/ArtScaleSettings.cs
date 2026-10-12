using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// 아트 카테고리별 스프라이트 임포트 설정 (임시unity제작.md 9.3).
    /// 원본 크기가 카테고리마다 달라 Pixels Per Unit으로 표시 크기를 맞춘다. 코드에서 scale을 곱하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/Art Scale Settings", fileName = "ArtScaleSettings")]
    public class ArtScaleSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Settings/ArtScaleSettings.asset";
        public const string ArtRoot = "Assets/_Project/Art/";

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

            public Rule(string pathPrefix, float pixelsPerUnit, SpriteAlignment pivot, int sliceBorder = 0, bool uncompressed = false)
            {
                this.pathPrefix = pathPrefix;
                this.pixelsPerUnit = pixelsPerUnit;
                this.pivot = pivot;
                this.sliceBorder = sliceBorder;
                this.uncompressed = uncompressed;
            }
        }

        public int maxTextureSize = 2048;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public FilterMode filterMode = FilterMode.Bilinear;

        public List<Rule> rules = new()
        {
            new Rule("Backgrounds/", 100f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Normal/", 240f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Golden/", 240f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Attack/", 240f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/GoldenAttack/", 240f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Landmark/", 140f, SpriteAlignment.BottomCenter),
            // 드래곤 파츠 피벗(회전점)은 파츠마다 달라서 M2의 조립 코드에서 지정한다
            new Rule("Dragon/", 700f, SpriteAlignment.Center),
            new Rule("AirUnits/", 200f, SpriteAlignment.Center),
            new Rule("Drops/", 500f, SpriteAlignment.Center),
            // UI 프레임 (Tools/make_ui_frames.py). 48px 원본의 가장자리 12px를 9-slice로 고정한다
            new Rule("UI/", 100f, SpriteAlignment.Center, 12, true),
            // UI 디자인 패키지 v1 (리소스/dragon_ui_unity_v1.zip). 9-slice 경계는 UIKit/sprite-manifest.csv의 border_LBRT를 쓴다
            new Rule("UIKit/", 100f, SpriteAlignment.Center, 0, true),
        };

        /// <summary>코드의 기본 규칙 중 저장된 에셋에 없는 것을 추가한다. 규칙이 늘어났을 때 기존 에셋을 갱신한다.</summary>
        public bool AddMissingDefaultRules()
        {
            var defaults = CreateInstance<ArtScaleSettings>();
            bool changed = false;
            foreach (var rule in defaults.rules)
            {
                if (rules.Exists(r => r.pathPrefix == rule.pathPrefix)) continue;
                rules.Add(rule);
                changed = true;
            }
            DestroyImmediate(defaults);
            return changed;
        }

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
    }
}
