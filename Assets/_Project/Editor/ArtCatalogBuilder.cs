using System.IO;
using System.Linq;
using DWH.Data;
using DWH.InGame;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>Tools/import_art.py가 만든 Assets/_Project/Art 구조를 읽어 ArtCatalog 에셋을 만든다.</summary>
    public static class ArtCatalogBuilder
    {
        public const string CatalogPath = "Assets/_Project/Data/ArtCatalog.asset";
        const string Art = "Assets/_Project/Art";
        const int ThemeCount = 14;
        const int EraCount = 7;

        [MenuItem("DWH/Build Art Catalog")]
        public static ArtCatalog Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.themes = Enumerable.Range(1, ThemeCount).Select(t => new ArtCatalog.ThemeArt
            {
                normal = SpritesIn($"{Art}/Buildings/Normal/theme{t:00}"),
                attack = SpritesIn($"{Art}/Buildings/Attack/theme{t:00}"),
                golden = SpritesIn($"{Art}/Buildings/Golden/theme{t:00}"),
                goldenAttack = SpritesIn($"{Art}/Buildings/GoldenAttack/theme{t:00}"),
                landmark = Load($"{Art}/Buildings/Landmark/theme{t:00}.png"),
                airUnits = new[] { Load($"{Art}/AirUnits/theme{t:00}_unit1.png"), Load($"{Art}/AirUnits/theme{t:00}_unit2.png") },
            }).ToArray();

            catalog.eras = Enumerable.Range(1, EraCount).Select(e => new ArtCatalog.EraArt
            {
                far = Load($"{Art}/Backgrounds/era{e}/far.png"),
                mid = Load($"{Art}/Backgrounds/era{e}/mid.png"),
                ground = Load($"{Art}/Backgrounds/era{e}/ground.png"),
            }).ToArray();

            catalog.dragonParts = AssetDatabase.FindAssets("t:Sprite", new[] { $"{Art}/Dragon/Parts" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(p => new ArtCatalog.NamedSprite { name = Path.GetFileNameWithoutExtension(p), sprite = Load(p) })
                .ToArray();

            var dropNames = new[] { "gold_coin", "gold_bars", "sapphire", "ruby", "diamond", "crown" };
            Debug.Assert(dropNames.Length == System.Enum.GetValues(typeof(DropResource)).Length);
            catalog.drops = dropNames.Select(n => Load($"{Art}/Drops/{n}.png")).ToArray();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DWH] 아트 카탈로그 생성: 테마 {catalog.themes.Length}, 시대 {catalog.eras.Length}, 드래곤 파츠 {catalog.dragonParts.Length}");
            return catalog;
        }

        static Sprite[] SpritesIn(string folder) =>
            AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(Load).ToArray();

        static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException($"스프라이트가 없습니다: {path}");
    }
}
