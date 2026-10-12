using System;
using System.Collections.Generic;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Dragon;
using UnityEngine;
using UnityEngine.Pool;

namespace DWH.InGame.Drops
{
    /// <summary>
    /// 건물이 파괴되면 드랍 구성 표대로 자원을 쏟아내고, 잠시 뒤 드래곤에게 빨아들여 골드로 환산한다 (4.6).
    /// 자원 오브젝트는 풀링한다. 세션이 끝날 때 남은 자원은 모두 획득으로 처리한다.
    /// </summary>
    public class DropSystem : MonoBehaviour
    {
        GameDatabase db;
        ArtCatalog art;
        StageRow stage;
        DragonController dragon;
        System.Random random;
        ObjectPool<DropPickup> pool;
        readonly HashSet<DropPickup> active = new();
        double goldMultiplier = 1;
        double chainGoldPerStack;
        float lastDestroyTime = float.NegativeInfinity;

        /// <summary>연쇄 파괴 스택 (4.8). 일정 시간 안에 다음 건물을 부술 때마다 1씩 오르고, 시간이 지나면 0으로 돌아간다</summary>
        public int ChainStacks { get; private set; }

        /// <summary>이번 세션에 획득한 골드 코인</summary>
        public double CollectedGold { get; private set; }
        /// <summary>쏟아졌지만 아직 획득하지 않은 골드</summary>
        public double PendingGold { get; private set; }
        public int ActiveCount => active.Count;

        public event Action<DropResource, double> Collected;
        /// <summary>건물이 파괴되어 자원을 쏟아냈다 (건물, 쏟아낸 골드 합계). HUD 토스트용</summary>
        public event Action<Building, double> Burst;
        /// <summary>공중 유닛이 격추되어 자원을 쏟아냈다 (유닛, 골드 합계)</summary>
        public event Action<AirUnit, double> AirBurst;

        /// <summary>연쇄 파괴가 이어지는 중인지 (마지막 파괴 후 제한 시간 안)</summary>
        public bool ChainActive => ChainStacks > 0 && Time.time - lastDestroyTime <= db.balance.chainDestroyWindow;
        /// <summary>연쇄가 끊기기까지 남은 비율 (1 → 0)</summary>
        public float ChainTimeLeft => ChainActive ? 1f - (Time.time - lastDestroyTime) / (float)db.balance.chainDestroyWindow : 0f;
        /// <summary>현재 연쇄 스택이 주는 골드 보너스 비율</summary>
        public double ChainGoldBonus => chainGoldPerStack * ChainStacks;

        /// <param name="goldBonusMultiplier">탐욕·환생 보너스를 합친 골드 배율 (8.3)</param>
        /// <param name="chainBonusPerStack">연쇄 파괴 스택당 보너스 골드 비율 (4.8)</param>
        public void Init(GameDatabase database, ArtCatalog catalog, StageRow stageRow, DragonController target, IEnumerable<Building> buildings,
            double goldBonusMultiplier = 1, double chainBonusPerStack = 0)
        {
            goldMultiplier = goldBonusMultiplier;
            chainGoldPerStack = chainBonusPerStack;
            db = database;
            art = catalog;
            stage = stageRow;
            dragon = target;
            random = new System.Random(stage.index);
            pool = new ObjectPool<DropPickup>(CreatePickup, p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false));

            foreach (var b in buildings)
                b.Destroyed += OnBuildingDestroyed;
        }

        void OnBuildingDestroyed(Building building)
        {
            float now = Time.time;
            ChainStacks = now - lastDestroyTime <= db.balance.chainDestroyWindow
                ? Math.Min(ChainStacks + 1, db.balance.chainDestroyMaxStacks)
                : 0;
            lastDestroyTime = now;

            // 황금화된 건물은 황금 건물의 드랍 구성을 쓴다 (4.8 감별안)
            var row = db.Composition(building.RewardType, stage.stageId);
            var origin = new Vector2(building.CenterX, building.Hitbox.center.y);
            double before = PendingGold;
            Spawn(DropPlanner.Expand(row), origin, 1 + chainGoldPerStack * ChainStacks);
            Burst?.Invoke(building, PendingGold - before);
        }

        /// <summary>
        /// 공중 유닛 격추 드랍 (4.5). 「공중 유닛 드랍」 구성을 쓰고, 탐욕·환생 배율은 붙지만 연쇄 파괴는 붙지 않으며 연쇄 스택도 쌓지 않는다.
        /// </summary>
        public void OnAirUnitDowned(AirUnit unit)
        {
            var row = db.AirDrop(unit.Slot, stage.stageId);
            double before = PendingGold;
            Spawn(DropPlanner.Expand(row), unit.Hitbox.center);
            AirBurst?.Invoke(unit, PendingGold - before);
        }

        /// <param name="extraMultiplier">이 묶음에만 붙는 추가 배율 (연쇄 파괴 등)</param>
        public void Spawn(IEnumerable<DropResource> drops, Vector2 origin, double extraMultiplier = 1)
        {
            var b = db.balance;
            foreach (var resource in drops)
            {
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-(float)b.dropBurstAngle, (float)b.dropBurstAngle, (float)random.NextDouble());
                float speed = Mathf.Lerp((float)b.dropBurstSpeedMin, (float)b.dropBurstSpeedMax, (float)random.NextDouble());
                var velocity = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;

                var pickup = pool.Get();
                double gold = db.Resource(resource).goldValue * goldMultiplier * extraMultiplier;
                pickup.Launch(resource, art.Drop(resource), gold, origin, velocity, (float)b.groundHeight,
                    (float)b.dropGravity, (float)b.dropAbsorbDelay, (float)b.dropAbsorbSpeed, (float)b.dropCollectDistance);
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
            Collected?.Invoke(pickup.Resource, pickup.Gold);
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
