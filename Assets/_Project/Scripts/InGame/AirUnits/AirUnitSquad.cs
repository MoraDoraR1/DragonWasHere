using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.AirUnits
{
    /// <summary>
    /// 공중 유닛 등장 관리 (4.5). 스테이지 시작 후 3·10·25초에 1마리씩 나온다(최대 3마리).
    /// 홀수 스테이지는 세 슬롯 모두 1번 유닛, 짝수 스테이지는 3·10초에 1번, 25초에 2번 유닛이다.
    /// 그 시점 전에 클리어하면 나오지 않는다. 보너스 스테이지에서는 만들지 않는다.
    /// </summary>
    public class AirUnitSquad : MonoBehaviour
    {
        /// <summary>등장 순서별 화면 가로 위치와 고도 비율(0 = 고도 하한, 1 = 상한) `[임시]`</summary>
        static readonly (float fraction, float altitude)[] Formation = { (0.62f, 0.4f), (0.8f, 0.9f), (0.9f, 0f) };

        GameDatabase db;
        ArtCatalog art;
        StageRow stage;
        DragonController dragon;
        Camera view;
        List<(float time, AirUnitRow row)> schedule;
        int next;
        bool enabledFlag = true;
        readonly List<AirUnit> units = new();

        public IReadOnlyList<AirUnit> Units => units;
        public event Action<AirUnit> Spawned;

        /// <summary>등장 시각과 유닛 행. 1번 유닛을 수만큼 먼저, 그다음 2번 유닛 (엑셀 「공중 유닛 배치」의 수와 같다)</summary>
        public static List<(float time, AirUnitRow row)> Schedule(IEnumerable<AirUnitRow> rows, IReadOnlyList<double> spawnTimes)
        {
            var order = rows.OrderBy(r => r.slot).SelectMany(r => Enumerable.Repeat(r, r.count)).ToList();
            return order.Take(spawnTimes.Count).Select((row, i) => ((float)spawnTimes[i], row)).ToList();
        }

        public void Init(GameDatabase database, ArtCatalog catalog, StageRow stageRow, DragonController target, Camera camera)
        {
            db = database;
            art = catalog;
            stage = stageRow;
            dragon = target;
            view = camera;
            schedule = Schedule(db.AirUnits(stage.stageId), db.balance.airUnitSpawnTimes);
        }

        /// <summary>전투가 끝나면 끈다. 남은 유닛은 공격을 멈춘다</summary>
        public bool Enabled
        {
            get => enabledFlag;
            set
            {
                enabledFlag = value;
                foreach (var u in units) u.Enabled = value;
            }
        }

        void Update()
        {
            if (!enabledFlag || dragon == null || schedule == null) return;
            double elapsed = dragon.Vitals.Elapsed;
            while (next < schedule.Count && elapsed >= schedule[next].time)
                Spawn(next++);
        }

        void Spawn(int index)
        {
            var row = schedule[index].row;
            var b = db.balance;
            var (fraction, altitudeRatio) = Formation[index % Formation.Length];
            var go = new GameObject($"Air{index}_{row.name}");
            go.transform.SetParent(transform, false);
            var unit = go.AddComponent<AirUnit>();
            var sprites = art.Theme(stage.theme).airUnits;
            unit.Init(row.slot, row.name, row.hp, row.damage, sprites[Mathf.Clamp(row.slot - 1, 0, sprites.Length - 1)], dragon, view,
                fraction, Mathf.Lerp((float)b.airUnitAltitudeMin, (float)b.airUnitAltitudeMax, altitudeRatio), (float)b.groundHeight,
                (float)b.airUnitFlySpeed, (float)b.airUnitAps, (float)b.airUnitProjectileSpeed, (float)b.projectileSize,
                GrayboxSprites.Square, SortingOrders.AirUnit);
            units.Add(unit);
            Spawned?.Invoke(unit);
        }
    }
}
