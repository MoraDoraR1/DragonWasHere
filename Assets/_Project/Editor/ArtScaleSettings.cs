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

            public Rule(string pathPrefix, float pixelsPerUnit, SpriteAlignment pivot)
            {
                this.pathPrefix = pathPrefix;
                this.pixelsPerUnit = pixelsPerUnit;
                this.pivot = pivot;
            }
        }

        public int maxTextureSize = 2048;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public FilterMode filterMode = FilterMode.Bilinear;

        public List<Rule> rules = new()
        {
            new Rule("Backgrounds/", 100f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Normal/", 600f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Golden/", 600f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Attack/", 600f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/GoldenAttack/", 600f, SpriteAlignment.BottomCenter),
            new Rule("Buildings/Landmark/", 350f, SpriteAlignment.BottomCenter),
            // 드래곤 파츠 피벗(회전점)은 파츠마다 달라서 M2의 조립 코드에서 지정한다
            new Rule("Dragon/", 700f, SpriteAlignment.Center),
            new Rule("AirUnits/", 200f, SpriteAlignment.Center),
            new Rule("Drops/", 500f, SpriteAlignment.Center),
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
    }
}
