using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.AirUnits
{
    /// <summary>
    /// 공중 유닛 등장 관리 (unit, 가이드 6.3). 스테이지당 3행이고 spawn_sec(3/10/25초)에 1마리씩 나온다.
    /// 그 시점 전에 클리어하면 나오지 않는다. 보너스 스테이지에서는 만들지 않는다.
    /// </summary>
    public class AirUnitSquad : MonoBehaviour
    {
        /// <summary>등장 순서별 화면 가로 위치와 고도 비율(0 = 고도 하한, 1 = 상한) [임시]</summary>
        static readonly (float fraction, float altitude)[] Formation = { (0.62f, 0.4f), (0.8f, 0.9f), (0.9f, 0f) };

        GameDatabase db;
        ArtCatalog art;
        DragonController dragon;
        Camera view;
        List<UnitRow> schedule;
        int next;
        bool enabledFlag = true;
        readonly List<AirUnit> units = new();

        public IReadOnlyList<AirUnit> Units => units;
        public event Action<AirUnit> Spawned;

        public void Init(GameDatabase database, ArtCatalog catalog, StageRow stage, DragonController target, Camera camera)
        {
            db = database;
            art = catalog;
            dragon = target;
            view = camera;
            schedule = db.Units(stage.stageId).ToList();
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
            while (next < schedule.Count && elapsed >= schedule[next].spawnSec)
                Spawn(next++);
        }

        void Spawn(int index)
        {
            var row = schedule[index];
            var (fraction, altitudeRatio) = Formation[index % Formation.Length];
            var go = new GameObject($"Air{index}_{row.unitId}");
            go.transform.SetParent(transform, false);
            var unit = go.AddComponent<AirUnit>();
            unit.Init(row, db.Text(row.nameStr), art.Sprite(row.resId), dragon, view,
                fraction, Mathf.Lerp(Tuning.AirUnitAltitudeMin, Tuning.AirUnitAltitudeMax, altitudeRatio), Tuning.GroundHeight,
                GrayboxSprites.Square, SortingOrders.AirUnit);
            units.Add(unit);
            Spawned?.Invoke(unit);
        }
    }
}
