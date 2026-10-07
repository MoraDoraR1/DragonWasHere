using DWH.Core;
using NUnit.Framework;

namespace DWH.Tests
{
    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(9999, "9,999")]
        [TestCase(10000, "1만")]
        [TestCase(12345, "1.2만")]
        [TestCase(19999, "1.9만")]
        [TestCase(7810000, "781만")]
        [TestCase(99999999, "9999만")]
        [TestCase(120000000, "1.2억")]
        [TestCase(1500000000000, "1.5조")]
        [TestCase(-7810000, "-781만")]
        [TestCase(12.7, "12")]
        public void ToKorean_FormatsWithKoreanUnits(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.ToKorean(value));
        }

        [Test]
        public void ToKorean_InvalidNumbers_ReturnZero()
        {
            Assert.AreEqual("0", NumberFormat.ToKorean(double.NaN));
            Assert.AreEqual("0", NumberFormat.ToKorean(double.PositiveInfinity));
        }
    }
}
