using System;
using System.Linq;
using DWH.Data;
using UnityEngine;

namespace DWH.InGame
{
    /// <summary>
    /// 스테이지 리소스 참조 모음 (9.1, 9.2). Assets/_Project/Art 폴더 구조에서 DWH/Build Art Catalog 메뉴로 생성한다.
    /// 테마 번호는 레벨디자인 순서(1 공룡 … 14 화성), 시대 번호는 1 원시 … 7 우주.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/Art Catalog", fileName = "ArtCatalog")]
    public class ArtCatalog : ScriptableObject
    {
        [Serializable]
        public class ThemeArt
        {
            public Sprite[] normal;
            public Sprite[] attack;
            public Sprite[] golden;
            public Sprite[] goldenAttack;
            public Sprite landmark;
            public Sprite[] airUnits;
        }

        [Serializable]
        public class EraArt
        {
            public Sprite far;
            public Sprite mid;
            public Sprite ground;
        }

        [Serializable]
        public class NamedSprite
        {
            public string name;
            public Sprite sprite;
        }

        public ThemeArt[] themes;
        public EraArt[] eras;
        public NamedSprite[] dragonParts;
        public Sprite[] drops;

        public ThemeArt Theme(int theme) => themes[theme - 1];
        public EraArt Era(int era) => eras[era - 1];
        public Sprite Drop(DropResource resource) => drops[(int)resource];

        public Sprite DragonPart(string spriteName) =>
            dragonParts.FirstOrDefault(p => p.name == spriteName)?.sprite
            ?? throw new ArgumentException($"드래곤 파츠 스프라이트 {spriteName}가 없습니다");

        /// <summary>
        /// 스테이지 order번째 건물의 스프라이트 (9.2 사용 규칙).
        /// 일반·황금은 테마 3종 중 시드(스테이지 번호 × 1000 + 순번)로 고르고, 공격은 2종을 번갈아 쓴다.
        /// </summary>
        public Sprite BuildingSprite(int theme, BuildingType type, int stageIndex, int order, int attackOrder)
        {
            var art = Theme(theme);
            return type switch
            {
                BuildingType.Normal => Pick(art.normal, stageIndex, order),
                BuildingType.Golden => Pick(art.golden, stageIndex, order),
                BuildingType.Attack => art.attack[attackOrder % art.attack.Length],
                BuildingType.Landmark => art.landmark,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        /// <summary>감별안으로 황금화된 건물의 스프라이트. 일반은 황금 건물, 공격은 황금 공격 건물 (3.4)</summary>
        public Sprite GildedSprite(int theme, BuildingType type, int stageIndex, int order, int gildedOrder)
        {
            var art = Theme(theme);
            return type == BuildingType.Attack
                ? art.goldenAttack[gildedOrder % art.goldenAttack.Length]
                : Pick(art.golden, stageIndex, order);
        }

        static Sprite Pick(Sprite[] sprites, int stageIndex, int order) =>
            sprites[new System.Random(stageIndex * 1000 + order).Next(sprites.Length)];
    }
}
