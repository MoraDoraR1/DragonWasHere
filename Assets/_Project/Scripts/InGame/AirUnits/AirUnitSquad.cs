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
        /// <summary>등장 순서별 일렁임 위상과 고도 비율(0 = 고도 하한, 1 = 상한) [임시]</summary>
        static readonly (float phase, float altitude)[] Formation = { (0.62f, 0.4f), (0.8f, 0.9f), (0.9f, 0f) };

        GameDatabase db;
        ArtCatalog art;
        DragonController dragon;
        Camera view;
        float stageLeft, stageRight;
        List<UnitRow> schedule;
        int next;
        bool enabledFlag = true;
        readonly List<AirUnit> units = new();

        public IReadOnlyList<AirUnit> Units => units;
        public event Action<AirUnit> Spawned;

        /// <param name="left">맵 왼쪽 끝(월드 x). 공중 유닛이 이 사이를 왕복한다</param>
        /// <param name="right">맵 오른쪽 끝(월드 x)</param>
        public void Init(GameDatabase database, ArtCatalog catalog, StageRow stage, DragonController target, Camera camera, float left, float right)
        {
            stageLeft = left;
            stageRight = right;
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
            var (phase, altitudeRatio) = Formation[index % Formation.Length];
            var go = new GameObject($"Air{index}_{row.unitId}");
            go.transform.SetParent(transform, false);
            var unit = go.AddComponent<AirUnit>();
            unit.Init(row, db.Text(row.nameStr), art.Sprite(row.resId), dragon, view,
                phase, Mathf.Lerp(Tuning.AirUnitAltitudeMin, Tuning.AirUnitAltitudeMax, altitudeRatio), Tuning.GroundHeight,
                stageLeft, stageRight, GrayboxSprites.Square, SortingOrders.AirUnit);
            unit.Database = db;
            unit.Art = art;
            units.Add(unit);
            Spawned?.Invoke(unit);
        }
    }
}
