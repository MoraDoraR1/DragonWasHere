using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame;
using UnityEditor;
using UnityEngine;

namespace DWH.Editor
{
    /// <summary>
    /// resource 테이블로 ArtCatalog 에셋을 만든다 (가이드 5장). 파일 위치 = Assets/_Project/Art/ + res_path.
    /// 이펙트(type 80)는 시트 대신 낱장을 쓴다: sheet.png면 같은 폴더의 frames/, frame_00.png면 그 폴더의 PNG를 이름순으로 frame_count장.
    /// 영상(90)·폰트(91)·사운드(70·71)는 스프라이트가 아니라서 넣지 않는다.
    /// </summary>
    public static class ArtCatalogBuilder
    {
        public const string CatalogPath = "Assets/_Project/Data/ArtCatalog.asset";
        const string SpriteFxShaderPath = "Assets/_Project/Art/Shaders/SpriteFx.shader";
        const string SpriteFxMaterialPath = "Assets/_Project/Art/Shaders/SpriteFx.mat";

        static readonly ResType[] NotSprites = { ResType.Video, ResType.Font, ResType.Bgm, ResType.Sfx };

        [MenuItem("DWH/Build Art Catalog")]
        public static ArtCatalog Build()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(CsvImporter.DatabasePath)
                     ?? throw new FileNotFoundException("GameDatabase가 없습니다. DWH/Import Data를 먼저 실행하세요");
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var missing = new List<string>();
            catalog.entries = new List<ArtCatalog.Entry>();
            foreach (var res in db.resources)
            {
                if (NotSprites.Contains(res.type)) continue;
                var frames = res.type == ResType.Vfx ? LoadFrames(res) : new[] { Load(res.resPath) };
                if (frames.Length == 0 || frames.Any(f => f == null))
                {
                    missing.Add($"{res.resId} {res.resPath}");
                    continue;
                }
                catalog.entries.Add(new ArtCatalog.Entry { resId = res.resId, frames = frames });
            }

            catalog.spriteFx = SpriteFxMaterial();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (missing.Count > 0)
                Debug.LogError($"[DWH] 리소스 {missing.Count}개를 찾지 못했습니다:\n{string.Join("\n", missing)}");
            Debug.Log($"[DWH] 아트 카탈로그 생성: {catalog.entries.Count}개");
            return catalog;
        }

        static Material SpriteFxMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteFxMaterialPath);
            if (material != null) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SpriteFxShaderPath) ?? throw new FileNotFoundException(SpriteFxShaderPath);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SpriteFxMaterialPath);
            return material;
        }

        /// <summary>Art 폴더 기준 전체 경로</summary>
        public static string AssetPathOf(string resPath) => ArtScaleSettings.ArtRoot + resPath;

        static Sprite Load(string resPath) => AssetDatabase.LoadAssetAtPath<Sprite>(AssetPathOf(resPath));

        static Sprite[] LoadFrames(ResourceRow res)
        {
            string folder = Path.GetDirectoryName(AssetPathOf(res.resPath)).Replace('\\', '/');
            if (Path.GetFileName(res.resPath) == "sheet.png") folder += "/frames";
            var paths = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, System.StringComparer.Ordinal).ToList()
                : new List<string>();
            if (res.frameCount > 0) paths = paths.Take(res.frameCount).ToList();
            return paths.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
        }
    }
}
