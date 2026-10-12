using System.Collections.Generic;
using System.Text;

namespace DWH.Editor.Data
{
    /// <summary>RFC 4180 형식의 CSV를 읽는다. 큰따옴표로 감싼 칸 안의 쉼표·줄바꿈·"" 이스케이프를 처리한다.</summary>
    public static class CsvReader
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { EndRow(rows, row, cell); }
                else cell.Append(c);
            }
            EndRow(rows, row, cell);
            return rows;
        }

        static void EndRow(List<string[]> rows, List<string> row, StringBuilder cell)
        {
            row.Add(cell.ToString());
            cell.Clear();
            bool empty = row.Count == 1 && row[0].Length == 0;
            if (!empty)
                rows.Add(row.ToArray());
            row.Clear();
        }
    }
}
