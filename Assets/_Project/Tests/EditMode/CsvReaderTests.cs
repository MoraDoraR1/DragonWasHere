using DWH.Editor.Data;
using NUnit.Framework;

namespace DWH.Tests
{
    public class CsvReaderTests
    {
        [Test]
        public void Parse_SimpleRows()
        {
            var rows = CsvReader.Parse("a,b,c\n1,2,3\n");
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, rows[1]);
        }

        [Test]
        public void Parse_QuotedCellsWithCommaQuoteAndNewline()
        {
            var rows = CsvReader.Parse("k,v\r\nx,\"a, \"\"b\"\"\nc\"\r\n");
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("a, \"b\"\nc", rows[1][1]);
        }

        [Test]
        public void Parse_KeepsEmptyCellsAndSkipsBlankLines()
        {
            var rows = CsvReader.Parse("﻿a,b,c\n1,,3\n\n");
            Assert.AreEqual("a", rows[0][0]);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "1", "", "3" }, rows[1]);
        }

        [TestCase("stage_id", "stageId")]
        [TestCase("air_unit1_hp_multiplier", "airUnit1HpMultiplier")]
        [TestCase("hp", "hp")]
        public void ToCamelCase_ConvertsCsvHeaders(string snake, string camel)
        {
            Assert.AreEqual(camel, CsvImporter.ToCamelCase(snake));
        }
    }
}
