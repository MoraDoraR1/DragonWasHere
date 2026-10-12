using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DWH.Data;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor.Data
{
    /// <summary>
    /// Docs/Data/*.csv(엑셀 Ver02에서 Docs/Tools/export_csv.py로 만든 20종)를 Assets/_Project/Data/GameDatabase.asset으로 변환한다 (가이드 3.2).
    /// CSV 머리글(snake_case)은 행 클래스 필드(camelCase)와 이름으로 연결한다. 맞지 않는 열이나 필드가 있으면 실패한다.
    /// 빈칸은 [Optional] 필드만 허용하고 "해당 없음"으로 읽는다(Rows.cs).
    /// 배치모드: -executeMethod DWH.Editor.Data.CsvImporter.ImportAll
    /// </summary>
    public static class CsvImporter
    {
        public const string AssetFolder = "Assets/_Project/Data";
        public const string DatabasePath = AssetFolder + "/GameDatabase.asset";

        public static string DefaultCsvFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "Data");

        /// <summary>CSV 파일 이름(= 엑셀 시트 이름) → GameDatabase 필드</summary>
        public static readonly (string file, string field)[] Tables =
        {
            ("const", nameof(GameDatabase.consts)),
            ("enum", nameof(GameDatabase.enums)),
            ("dragon", nameof(GameDatabase.dragons)),
            ("dragon_growth", nameof(GameDatabase.dragonGrowths)),
            ("dragon_part", nameof(GameDatabase.dragonParts)),
            ("era", nameof(GameDatabase.eras)),
            ("theme", nameof(GameDatabase.themes)),
            ("stage", nameof(GameDatabase.stages)),
            ("building", nameof(GameDatabase.buildings)),
            ("unit", nameof(GameDatabase.units)),
            ("drop_item", nameof(GameDatabase.dropItems)),
            ("drop_group", nameof(GameDatabase.dropGroups)),
            ("starter_grade", nameof(GameDatabase.starterGrades)),
            ("starter_stage", nameof(GameDatabase.starterStages)),
            ("combo", nameof(GameDatabase.combos)),
            ("upgrade", nameof(GameDatabase.upgrades)),
            ("rebirth_trait", nameof(GameDatabase.rebirthTraits)),
            ("active_skill", nameof(GameDatabase.activeSkills)),
            ("string", nameof(GameDatabase.strings)),
            ("resource", nameof(GameDatabase.resources)),
        };

        [MenuItem("DWH/Import Data")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(AssetFolder);
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            Fill(database, DefaultCsvFolder);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] 데이터 임포트 완료: CSV {Tables.Length}종 → {DatabasePath}");
        }

        /// <summary>에셋 파일을 만들지 않고 메모리에 데이터베이스를 만든다. 테스트에서 쓴다.</summary>
        public static GameDatabase LoadInMemory(string csvFolder = null)
        {
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            Fill(database, csvFolder ?? DefaultCsvFolder);
            return database;
        }

        static void Fill(GameDatabase database, string csvFolder)
        {
            foreach (var (file, field) in Tables)
            {
                var fieldInfo = typeof(GameDatabase).GetField(field);
                string path = Path.Combine(csvFolder, file + ".csv");
                if (!File.Exists(path)) throw new FileNotFoundException($"{file}.csv가 없습니다. python Docs/Tools/export_csv.py로 만드세요", path);
                var csv = CsvReader.Parse(File.ReadAllText(path, Encoding.UTF8));
                if (csv.Count == 0) throw new InvalidDataException($"{file}.csv가 비어 있습니다");
                fieldInfo.SetValue(database, ReadTable(fieldInfo.FieldType, csv, file));
            }
            database.Invalidate();
        }

        static IList ReadTable(Type listType, List<string[]> csv, string file)
        {
            var rowType = listType.GetGenericArguments()[0];
            string[] header = csv[0].Select(h => h.Trim()).ToArray();
            var fields = header.Select(h => FieldFor(rowType, h, file)).ToArray();

            var unmapped = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)
                .Except(fields.Select(f => f.Name)).ToArray();
            if (unmapped.Length > 0)
                throw new InvalidDataException($"{file}.csv에 {rowType.Name} 필드에 해당하는 열이 없습니다: {string.Join(", ", unmapped)}");

            var list = (IList)Activator.CreateInstance(listType);
            for (int r = 1; r < csv.Count; r++)
            {
                if (csv[r].Length != header.Length)
                    throw new InvalidDataException($"{file}.csv {r + 1}행: 열 개수 {csv[r].Length}개 (머리글은 {header.Length}개)");
                object row = Activator.CreateInstance(rowType);
                for (int c = 0; c < header.Length; c++)
                {
                    var f = fields[c];
                    bool optional = f.GetCustomAttribute<OptionalAttribute>() != null;
                    f.SetValue(row, Convert(csv[r][c], f.FieldType, optional, $"{file}.csv {r + 1}행 {header[c]}"));
                }
                list.Add(row);
            }
            return list;
        }

        static FieldInfo FieldFor(Type type, string column, string file) =>
            type.GetField(ToCamelCase(column), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidDataException($"{file}.csv의 열 '{column}'에 해당하는 {type.Name} 필드가 없습니다");

        /// <summary>res_id_1 → resId1, effect_type_2 → effectType2</summary>
        public static string ToCamelCase(string snake)
        {
            var parts = snake.Trim().Split('_');
            return parts[0] + string.Concat(parts.Skip(1).Select(p => p.Length == 0 ? "" : char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        /// <summary>
        /// 칸 하나를 필드 형식으로 바꾼다. 빈칸은 optional일 때만 허용한다:
        /// 문자열 "", 정수·enum 0, 실수 NaN, bool false.
        /// </summary>
        public static object Convert(string text, Type type, bool optional, string where)
        {
            text = text.Trim();
            if (text.Length == 0)
            {
                if (!optional)
                    throw new InvalidDataException($"{where}: 빈칸입니다 (빈칸을 허용하지 않는 컬럼)");
                if (type == typeof(string)) return "";
                if (type == typeof(double)) return double.NaN;
                if (type == typeof(float)) return float.NaN;
                if (type == typeof(bool)) return false;
                if (type == typeof(int)) return 0;
                if (type.IsEnum) return Enum.ToObject(type, 0);
                throw new NotSupportedException($"{where}: {type.Name} 형식은 지원하지 않습니다");
            }
            try
            {
                if (type == typeof(string)) return text;
                if (type == typeof(int)) return (int)Math.Round(double.Parse(text, CultureInfo.InvariantCulture));
                if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
                if (type == typeof(double)) return double.Parse(text, CultureInfo.InvariantCulture);
                if (type == typeof(bool)) return text is "1" or "true" or "True" or "TRUE";
                if (type.IsEnum)
                {
                    int value = (int)Math.Round(double.Parse(text, CultureInfo.InvariantCulture));
                    if (!Enum.IsDefined(type, value))
                        throw new InvalidDataException($"{where}: {value}은(는) {type.Name}에 없는 값입니다 (enum.csv와 Enums.cs를 맞추세요)");
                    return Enum.ToObject(type, value);
                }
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                throw new InvalidDataException($"{where}: '{text}'을(를) {type.Name}(으)로 읽을 수 없습니다", e);
            }
            throw new NotSupportedException($"{where}: {type.Name} 형식은 지원하지 않습니다");
        }
    }
}
