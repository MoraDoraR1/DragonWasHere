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
    /// Docs/Data/*.csv를 Assets/_Project/Data/*.asset으로 변환한다 (임시unity제작.md 5장).
    /// CSV 머리글(snake_case)은 데이터 클래스 필드(camelCase)와 이름으로 연결한다. 맞지 않는 열이나 필드가 있으면 실패한다.
    /// 배치모드: -executeMethod DWH.Editor.Data.CsvImporter.ImportAll
    /// </summary>
    public static class CsvImporter
    {
        public const string AssetFolder = "Assets/_Project/Data";
        const char ListSeparator = ';';

        public static string DefaultCsvFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "Data");

        // CSV 파일 이름 → GameDatabase 필드
        static readonly (string file, string field)[] Tables =
        {
            ("stages", nameof(GameDatabase.stages)),
            ("hurdles", nameof(GameDatabase.hurdles)),
            ("building_types", nameof(GameDatabase.buildingTypes)),
            ("drop_curves", nameof(GameDatabase.dropCurves)),
            ("drop_resources", nameof(GameDatabase.dropResources)),
            ("drop_composition", nameof(GameDatabase.dropComposition)),
            ("air_units", nameof(GameDatabase.airUnits)),
            ("air_drop", nameof(GameDatabase.airDrop)),
            ("passive_skills", nameof(GameDatabase.passiveSkills)),
            ("active_skills", nameof(GameDatabase.activeSkills)),
            ("rebirth_traits", nameof(GameDatabase.rebirthTraits)),
            ("upgrade_nodes", nameof(GameDatabase.upgradeNodes)),
            ("growth", nameof(GameDatabase.growth)),
            ("stage_starter", nameof(GameDatabase.stageStarter)),
            ("dragon_parts", nameof(GameDatabase.dragonParts)),
        };

        // key,value 형식 CSV → GameDatabase 필드 (key가 필드 이름)
        static readonly (string file, string field)[] KeyValues =
        {
            ("dragon_base", nameof(GameDatabase.dragon)),
            ("balance", nameof(GameDatabase.balance)),
        };

        [MenuItem("DWH/Import Data")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(AssetFolder);
            var database = LoadOrCreate<GameDatabase>($"{AssetFolder}/GameDatabase.asset");

            foreach (var (file, field) in Tables.Concat(KeyValues))
            {
                var fieldInfo = typeof(GameDatabase).GetField(field);
                var asset = (ScriptableObject)AssetDatabase.LoadAssetAtPath($"{AssetFolder}/{fieldInfo.FieldType.Name}.asset", fieldInfo.FieldType);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(fieldInfo.FieldType);
                    AssetDatabase.CreateAsset(asset, $"{AssetFolder}/{fieldInfo.FieldType.Name}.asset");
                }
                Fill(asset, file, DefaultCsvFolder);
                EditorUtility.SetDirty(asset);
                fieldInfo.SetValue(database, asset);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] 데이터 임포트 완료: CSV {Tables.Length + KeyValues.Length}종 → {AssetFolder}");
        }

        /// <summary>에셋 파일을 만들지 않고 메모리에 데이터베이스를 만든다. 테스트에서 쓴다.</summary>
        public static GameDatabase LoadInMemory(string csvFolder)
        {
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            foreach (var (file, field) in Tables.Concat(KeyValues))
            {
                var fieldInfo = typeof(GameDatabase).GetField(field);
                var asset = ScriptableObject.CreateInstance(fieldInfo.FieldType);
                Fill(asset, file, csvFolder);
                fieldInfo.SetValue(database, asset);
            }
            return database;
        }

        static void Fill(ScriptableObject asset, string file, string csvFolder)
        {
            string path = Path.Combine(csvFolder, file + ".csv");
            var csv = CsvReader.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (csv.Count == 0)
                throw new InvalidDataException($"{file}.csv가 비어 있습니다");

            var rowsField = asset.GetType().GetField("rows");
            if (rowsField != null)
                FillTable(asset, rowsField, csv, file);
            else
                FillKeyValue(asset, csv, file);
        }

        static void FillTable(ScriptableObject asset, FieldInfo rowsField, List<string[]> csv, string file)
        {
            var rowType = rowsField.FieldType.GetGenericArguments()[0];
            string[] header = csv[0];
            var fields = header.Select(h => FieldFor(rowType, h, file)).ToArray();

            var unmapped = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)
                .Except(fields.Select(f => f.Name)).ToArray();
            if (unmapped.Length > 0)
                throw new InvalidDataException($"{file}.csv에 {rowType.Name} 필드에 해당하는 열이 없습니다: {string.Join(", ", unmapped)}");

            var list = (IList)Activator.CreateInstance(rowsField.FieldType);
            for (int r = 1; r < csv.Count; r++)
            {
                if (csv[r].Length != header.Length)
                    throw new InvalidDataException($"{file}.csv {r + 1}행: 열 개수 {csv[r].Length}개 (머리글은 {header.Length}개)");
                object row = Activator.CreateInstance(rowType);
                for (int c = 0; c < header.Length; c++)
                    fields[c].SetValue(row, Convert(csv[r][c], fields[c].FieldType, $"{file}.csv {r + 1}행 {header[c]}"));
                list.Add(row);
            }
            rowsField.SetValue(asset, list);
        }

        static void FillKeyValue(ScriptableObject asset, List<string[]> csv, string file)
        {
            var type = asset.GetType();
            var seen = new HashSet<string>();
            for (int r = 1; r < csv.Count; r++)
            {
                var field = FieldFor(type, csv[r][0], file);
                field.SetValue(asset, Convert(csv[r][1], field.FieldType, $"{file}.csv {r + 1}행 {csv[r][0]}"));
                seen.Add(field.Name);
            }

            var missing = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(f => f.Name).Where(n => !seen.Contains(n)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"{file}.csv에 값이 없습니다: {string.Join(", ", missing)}");
        }

        static FieldInfo FieldFor(Type type, string column, string file) =>
            type.GetField(ToCamelCase(column), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidDataException($"{file}.csv의 열 '{column}'에 해당하는 {type.Name} 필드가 없습니다");

        public static string ToCamelCase(string snake)
        {
            var parts = snake.Trim().Split('_');
            return parts[0] + string.Concat(parts.Skip(1).Select(p => p.Length == 0 ? "" : char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        public static object Convert(string text, Type type, string where)
        {
            text = text.Trim();
            try
            {
                if (type == typeof(string)) return text;
                if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
                if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
                if (type == typeof(double)) return double.Parse(text, CultureInfo.InvariantCulture);
                if (type == typeof(bool)) return bool.Parse(text);
                if (type.IsEnum) return Enum.Parse(type, text);
                if (type.IsArray)
                {
                    var element = type.GetElementType();
                    var parts = text.Length == 0 ? Array.Empty<string>() : text.Split(ListSeparator);
                    var array = Array.CreateInstance(element, parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                        array.SetValue(Convert(parts[i], element, where), i);
                    return array;
                }
            }
            catch (Exception e) when (e is FormatException or ArgumentException or OverflowException)
            {
                throw new InvalidDataException($"{where}: '{text}'을(를) {type.Name}(으)로 읽을 수 없습니다", e);
            }
            throw new NotSupportedException($"{where}: {type.Name} 형식은 지원하지 않습니다");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
