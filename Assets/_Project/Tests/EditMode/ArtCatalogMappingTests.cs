using System.Linq;
using DWH.InGame;
using NUnit.Framework;
using UnityEditor;

namespace DWH.Tests
{
    /// <summary>아트 카탈로그의 테마별 건물이 9.2 스테이지별 리소스 매핑표와 일치하는지 확인한다.</summary>
    public class ArtCatalogMappingTests
    {
        // 9.2 표: 테마 번호(레벨디자인 순서) → 일반 건물 3종 / 공격 건물 2종
        static readonly (int theme, string[] normal, string[] attack)[] Expected =
        {
            (1, new[] { "basalt-cave", "fossil-den", "hollow-log" }, new[] { "lava-vent", "spine-nest" }),
            (2, new[] { "hide-tent", "thatch-hut", "wood-watchtower" }, new[] { "bow-watchtower", "stone-sling" }),
            (3, new[] { "mastaba-tomb", "mudbrick-granary", "pylon-temple" }, new[] { "archer-pylon", "sun-obelisk" }),
            (4, new[] { "arch-gate", "column-temple", "roman-villa" }, new[] { "ballista-bastion", "catapult-court" }),
            (5, new[] { "castle-gate", "guard-tower", "stone-chapel" }, new[] { "longbow-turret", "siege-battery" }),
            (6, new[] { "grain-mill", "hay-barn", "timber-house" }, new[] { "crossbow-tower", "trebuchet-barn" }),
            (7, new[] { "rail-station", "steam-workshop", "textile-mill" }, new[] { "mortar-works", "steam-cannon" }),
            (8, new[] { "assembly-factory", "steel-foundry", "water-tower-depot" }, new[] { "dynamite-launcher", "gatling-bunker" }),
            (9, new[] { "art-deco-office", "city-diner", "times-square-theater" }, new[] { "autocannon-bunker", "missile-base" }),
            (10, new[] { "apartment-block", "neighborhood-shop", "subway-entrance" }, new[] { "rocket-battery", "sentry-post" }),
            (11, new[] { "capsule-hotel", "neon-shop", "robot-workshop" }, new[] { "em-mortar", "laser-watchtower" }),
            (12, new[] { "drone-logistics", "lantern-market", "vertical-housing" }, new[] { "drone-hangar", "plasma-tower" }),
            (13, new[] { "lunar-habitat", "lunar-mine", "lunar-observatory" }, new[] { "ion-defense", "lunar-railgun" }),
            (14, new[] { "mars-greenhouse", "mars-habitat", "mars-refinery" }, new[] { "gravity-mortar", "thermal-turret" }),
        };

        ArtCatalog art;

        [OneTimeSetUp]
        public void Load()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            Assert.IsNotNull(art, "DWH/Build All을 먼저 실행해야 한다");
        }

        [Test]
        public void Themes_MatchGuideTable()
        {
            Assert.AreEqual(14, art.themes.Length);
            foreach (var (theme, normal, attack) in Expected)
            {
                var t = art.Theme(theme);
                CollectionAssert.AreEquivalent(normal, t.normal.Select(s => s.name), $"테마 {theme} 일반 건물");
                CollectionAssert.AreEquivalent(attack, t.attack.Select(s => s.name), $"테마 {theme} 공격 건물");
                CollectionAssert.AreEquivalent(normal, t.golden.Select(s => s.name), $"테마 {theme} 황금 건물(같은 이름의 -gold 버전)");
                CollectionAssert.AreEquivalent(attack, t.goldenAttack.Select(s => s.name), $"테마 {theme} 황금 공격 건물");
                Assert.AreEqual($"theme{theme:00}", t.landmark.name);
                CollectionAssert.AreEqual(new[] { $"theme{theme:00}_unit1", $"theme{theme:00}_unit2" }, t.airUnits.Select(s => s.name));
            }
        }

        [Test]
        public void Eras_HaveThreeLayers()
        {
            Assert.AreEqual(7, art.eras.Length);
            for (int e = 1; e <= 7; e++)
            {
                var era = art.Era(e);
                Assert.IsNotNull(era.far, $"시대 {e} far");
                Assert.IsNotNull(era.mid, $"시대 {e} mid");
                Assert.IsNotNull(era.ground, $"시대 {e} ground");
            }
        }

        [Test]
        public void SameStageAlwaysPicksSameBuildings()
        {
            // 9.2: 시드 = 스테이지 번호 × 1000 + 건물 순번 → 같은 스테이지는 항상 같은 배치
            var first = Enumerable.Range(0, 20).Select(i => art.BuildingSprite(5, Data.BuildingType.Normal, 9, i, 0).name).ToArray();
            var again = Enumerable.Range(0, 20).Select(i => art.BuildingSprite(5, Data.BuildingType.Normal, 9, i, 0).name).ToArray();
            CollectionAssert.AreEqual(first, again);
            Assert.Greater(first.Distinct().Count(), 1, "3종이 섞여 나와야 한다");
        }
    }
}
