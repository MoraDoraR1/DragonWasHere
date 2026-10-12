using System;
using System.Collections.Generic;
using UnityEngine;

namespace DWH.InGame
{
    /// <summary>
    /// resource 테이블의 res_id → 스프라이트 (가이드 3.2, 5장). DWH/Build Art Catalog(ArtCatalogBuilder)가
    /// Assets/_Project/Art/ + res_path에서 만든다. 이펙트(type 80)는 프레임 여러 장을 담는다.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/Art Catalog", fileName = "ArtCatalog")]
    public class ArtCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string resId;
            public Sprite[] frames;
        }

        public List<Entry> entries = new();
        /// <summary>건물·유닛 황금화와 흰 플래시용 머티리얼 (Art/Shaders/SpriteFx.shader)</summary>
        public Material spriteFx;

        [NonSerialized] Dictionary<string, Entry> index;

        void OnEnable() => index = null;

        Entry Find(string resId)
        {
            if (index == null)
            {
                index = new Dictionary<string, Entry>(entries.Count);
                foreach (var e in entries) index[e.resId] = e;
            }
            return resId != null && index.TryGetValue(resId, out var entry) ? entry : null;
        }

        public bool Has(string resId) => Find(resId) is { frames: { Length: > 0 } };

        /// <summary>res_id의 스프라이트(이펙트는 첫 프레임). 없으면 예외</summary>
        public Sprite Sprite(string resId) =>
            Find(resId) is { frames: { Length: > 0 } } e ? e.frames[0] : throw new KeyNotFoundException($"아트 카탈로그에 {resId} 스프라이트가 없습니다");

        /// <summary>없으면 null (UI 아이콘처럼 비어 있어도 되는 곳)</summary>
        public Sprite SpriteOrNull(string resId) => Find(resId) is { frames: { Length: > 0 } } e ? e.frames[0] : null;

        /// <summary>이펙트 프레임 (resource.frame_count장)</summary>
        public Sprite[] Frames(string resId) =>
            Find(resId)?.frames ?? throw new KeyNotFoundException($"아트 카탈로그에 {resId} 프레임이 없습니다");

        /// <summary>시드로 고른 그림 (가이드 6.2: 시드 = 스테이지 순번 × 1000 + 건물 순번)</summary>
        public static string Pick(IReadOnlyList<string> resIds, int stageOrder, int buildingOrder) =>
            resIds[new System.Random(stageOrder * 1000 + buildingOrder).Next(resIds.Count)];
    }
}
