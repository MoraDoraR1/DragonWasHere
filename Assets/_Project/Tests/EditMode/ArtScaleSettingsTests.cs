using DWH.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DWH.Tests
{
    public class ArtScaleSettingsTests
    {
        ArtScaleSettings settings;

        [SetUp]
        public void SetUp() => settings = ScriptableObject.CreateInstance<ArtScaleSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [TestCase("Buildings/Normal/theme01/basalt-cave.png", 600f, SpriteAlignment.BottomCenter)]
        [TestCase("Buildings/GoldenAttack/theme14/thermal-turret.png", 600f, SpriteAlignment.BottomCenter)]
        [TestCase("Buildings/Landmark/theme10.png", 350f, SpriteAlignment.BottomCenter)]
        [TestCase("Backgrounds/era7/ground.png", 100f, SpriteAlignment.BottomCenter)]
        [TestCase("Dragon/Parts/body.png", 700f, SpriteAlignment.Center)]
        [TestCase("AirUnits/theme01_unit1.png", 200f, SpriteAlignment.Center)]
        [TestCase("Drops/crown.png", 500f, SpriteAlignment.Center)]
        public void FindRule_MatchesCategory(string path, float ppu, SpriteAlignment pivot)
        {
            var rule = settings.FindRule(path);
            Assert.IsNotNull(rule);
            Assert.AreEqual(ppu, rule.pixelsPerUnit);
            Assert.AreEqual(pivot, rule.pivot);
        }

        [Test]
        public void FindRule_UnknownPath_ReturnsNull()
        {
            Assert.IsNull(settings.FindRule("Unknown/foo.png"));
        }
    }
}
