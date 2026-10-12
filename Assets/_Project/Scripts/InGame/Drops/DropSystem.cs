using System;
using System.Collections.Generic;
using DWH.Core;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Dragon;
using UnityEngine;
using UnityEngine.Pool;

namespace DWH.InGame.Drops
{
    /// <summary>
    /// 건물·공중 유닛이 파괴되면 drop_group대로 자원을 쏟아내고, const.drop_absorb_delay_sec 뒤 드래곤에게 빨아들여 골드로 환산한다 (가이드 6.4).
    /// 골드 = drop_item.gold_value × 골드 배율(탐욕·황금 혈통). 건물은 콤보 보너스와 황금화(×3)도 붙는다.
    /// 콤보 (가이드 6.9): 직전 파괴 후 const.combo_keep_sec 안에 다음 건물을 부수면 이어진다.
    /// 자원 오브젝트는 풀링한다. 세션이 끝날 때 남은 자원은 모두 획득으로 처리한다.
    /// </summary>
    public class DropSystem : MonoBehaviour
    {
        GameDatabase db;
        ArtCatalog art;
        PlayerStats stats;
        DragonController dragon;
        System.Random random;
        ObjectPool<DropPickup> pool;
        readonly HashSet<DropPickup> active = new();
        float comboKeep, absorbDelay;
        float lastDestroyTime = float.NegativeInfinity;

        /// <summary>지금 이어지는 연속 파괴 수 (끊기면 0)</summary>
        public int Combo => ComboActive ? comboCount : 0;
        int comboCount;
        public int MaxCombo { get; private set; }
        public bool ComboActive => comboCount > 0 && Time.time - lastDestroyTime <= comboKeep;
        /// <summary>콤보가 끊기기까지 남은 비율 (1 → 0)</summary>
        public float ComboTimeLeft => ComboActive ? 1f - (Time.time - lastDestroyTime) / comboKeep : 0f;
        /// <summary>콤보 골드 스택 = min(연속 파괴 수 - 1, 최대 콤보) (효과 10·11)</summary>
        public int ComboStacks => Math.Min(Math.Max(0, Combo - 1), stats.MaxComboStacks);
        public double ComboGoldBonus => stats.ComboGoldPerStack * ComboStacks;

        /// <summary>이번 세션에 획득한 골드 코인</summary>
        public double CollectedGold { get; private set; }
        /// <summary>쏟아졌지만 아직 획득하지 않은 골드</summary>
        public double PendingGold { get; private set; }
        public int ActiveCount => active.Count;

        public event Action<string, double> Collected;
        /// <summary>건물이 파괴되어 자원을 쏟아냈다 (건물, 쏟아낸 골드 합계). HUD 토스트용</summary>
        public event Action<Building, double> Burst;
        /// <summary>공중 유닛이 격추되어 자원을 쏟아냈다 (유닛, 골드 합계)</summary>
        public event Action<AirUnit, double> AirBurst;
        /// <summary>콤보가 이어졌다 (연속 파괴 수)</summary>
        public event Action<int> ComboChanged;

        public void Init(GameDatabase database, ArtCatalog catalog, PlayerStats playerStats, DragonController target,
            IEnumerable<Building> buildings, int seed)
        {
            db = database;
            art = catalog;
            stats = playerStats;
            dragon = target;
            comboKeep = (float)db.Const("combo_keep_sec");
            absorbDelay = (float)db.Const("drop_absorb_delay_sec");
            random = new System.Random(seed);
            pool = new ObjectPool<DropPickup>(CreatePickup, p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false));

            foreach (var b in buildings)
                b.Destroyed += OnBuildingDestroyed;
        }

        void OnBuildingDestroyed(Building building)
        {
            float now = Time.time;
            comboCount = now - lastDestroyTime <= comboKeep ? comboCount + 1 : 1;
            lastDestroyTime = now;
            MaxCombo = Math.Max(MaxCombo, comboCount);
            ComboChanged?.Invoke(comboCount);

            var origin = new Vector2(building.CenterX, building.Hitbox.center.y);
            double before = PendingGold;
            double multiplier = stats.GoldMultiplier * (1 + ComboGoldBonus) * (building.IsGilded ? Tuning.GildedGoldMultiplier : 1);
            Spawn(DropPlanner.Expand(db, building.Row.dropGroup, random), origin, multiplier);
            Burst?.Invoke(building, PendingGold - before);
        }

        /// <summary>공중 유닛 격추 드랍. 골드 배율은 붙지만 콤보는 붙지 않고 쌓지도 않는다</summary>
        public void OnAirUnitDowned(AirUnit unit)
        {
            double before = PendingGold;
            Spawn(DropPlanner.Expand(db, unit.Row.dropGroup, random), unit.Hitbox.center, stats.GoldMultiplier);
            AirBurst?.Invoke(unit, PendingGold - before);
        }

        public void Spawn(IEnumerable<DropItemRow> drops, Vector2 origin, double multiplier)
        {
            foreach (var item in drops)
            {
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-Tuning.DropBurstAngle, Tuning.DropBurstAngle, (float)random.NextDouble());
                float speed = Mathf.Lerp(Tuning.DropBurstSpeedMin, Tuning.DropBurstSpeedMax, (float)random.NextDouble());
                var velocity = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;

                var pickup = pool.Get();
                double gold = item.goldValue * multiplier;
                pickup.Launch(item.itemId, art.SpriteOrNull(item.resId), gold, origin, velocity, Tuning.GroundHeight, absorbDelay);
                active.Add(pickup);
                PendingGold += gold;
            }
        }

        void Update()
        {
            if (dragon == null || active.Count == 0) return;
            Vector2 target = dragon.Hitbox.center;
            float dt = Time.deltaTime;
            List<DropPickup> done = null;
            foreach (var p in active)
            {
                if (p.Step(dt, target))
                    (done ??= new List<DropPickup>()).Add(p);
            }
            if (done != null)
                foreach (var p in done) Collect(p);
        }

        /// <summary>세션 종료 시 남은 자원을 모두 획득으로 처리한다</summary>
        public void CollectAll()
        {
            foreach (var p in new List<DropPickup>(active)) Collect(p);
        }

        void Collect(DropPickup pickup)
        {
            active.Remove(pickup);
            PendingGold -= pickup.Gold;
            CollectedGold += pickup.Gold;
            Collected?.Invoke(pickup.ItemId, pickup.Gold);
            pool.Release(pickup);
        }

        DropPickup CreatePickup()
        {
            var go = new GameObject("Drop");
            go.transform.SetParent(transform, false);
            return go.AddComponent<DropPickup>();
        }
    }
}
