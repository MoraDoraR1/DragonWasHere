using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using DWH.InGame.Stage;
using UnityEngine;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 액티브 스킬 (4.4): 버튼(또는 1·2·3 키)으로 즉시 발동한다. 쿨타임은 없고 세션당 사용 횟수만 있다.
    /// - 횟수는 세션(스테이지 진입)마다 최대치로 충전하고, 보너스 스테이지에서는 남은 횟수를 이어 쓴다.
    /// - 대상이 없어도 발동하고 횟수를 쓴다. 같은 스킬을 다시 쓰면 따로 발동하고, 다른 스킬과 동시에 쓸 수 있다.
    /// - 클리어·패배하면 진행 중인 효과를 즉시 끝낸다. 일시정지(timeScale 0)하면 같이 멈춘다.
    /// - 세 스킬 모두 공중 유닛도 때린다.
    /// </summary>
    public class ActiveSkillSystem : MonoBehaviour
    {
        const float MeteorFallSeconds = 0.35f;
        const float MeteorStagger = 0.12f;
        const int MeteorRangeBuildings = 3;
        const float HurricaneSpeed = 1f;
        const float HurricaneWidth = 1.2f;
        const float HurricaneHeight = 6f;

        GameDatabase db;
        PlayerStats stats;
        DragonController dragon;
        IReadOnlyList<Building> buildings;
        IReadOnlyList<AirUnit> airUnits;
        Camera view;
        BattleCamera shaker;
        System.Random random;
        IReadOnlyDictionary<ActiveSkillId, int> levels = new Dictionary<ActiveSkillId, int>();
        Dictionary<ActiveSkillId, int> uses = new();
        bool stopped;

        public event Action<ActiveSkillId> Used;
        public event Action<ActiveSkillId, ICombatTarget, double, bool> SkillHit;

        public void Init(GameDatabase database, PlayerStats playerStats, DragonController owner, IReadOnlyList<Building> stageBuildings,
            IReadOnlyList<AirUnit> flyingUnits, Camera camera, int randomSeed)
        {
            db = database;
            stats = playerStats;
            dragon = owner;
            buildings = stageBuildings;
            airUnits = flyingUnits ?? Array.Empty<AirUnit>();
            view = camera;
            camera.TryGetComponent(out shaker);
            random = new System.Random(randomSeed * 17 + 3);
        }

        /// <param name="skillLevels">습득 단계 (0 = 미습득)</param>
        /// <param name="usesLeft">남은 사용 횟수. 보너스 스테이지는 일반 스테이지와 같은 사전을 넘겨 이어 쓴다</param>
        public void Configure(IReadOnlyDictionary<ActiveSkillId, int> skillLevels, Dictionary<ActiveSkillId, int> usesLeft)
        {
            levels = skillLevels;
            uses = usesLeft;
        }

        /// <summary>새 세션의 사용 횟수 = 단계별 세션당 횟수</summary>
        public static Dictionary<ActiveSkillId, int> FullUses(GameDatabase db, IReadOnlyDictionary<ActiveSkillId, int> skillLevels) =>
            skillLevels.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => db.Active(kv.Key, kv.Value).usesPerSession);

        public int Level(ActiveSkillId id) => levels.TryGetValue(id, out int l) ? l : 0;
        public int UsesLeft(ActiveSkillId id) => uses.TryGetValue(id, out int u) ? u : 0;
        public bool CanUse(ActiveSkillId id) => !stopped && Level(id) > 0 && UsesLeft(id) > 0 && dragon != null && dragon.Controllable && dragon.IsAlive;

        public bool Use(ActiveSkillId id)
        {
            if (!CanUse(id)) return false;
            uses[id] = UsesLeft(id) - 1;
            var row = db.Active(id, Level(id));
            StartCoroutine(id switch
            {
                ActiveSkillId.Meteor => Meteor(row),
                ActiveSkillId.Hurricane => Hurricane(row),
                _ => Earthquake(row),
            });
            Used?.Invoke(id);
            return true;
        }

        void Update()
        {
            if (stopped || dragon == null) return;
            if (!dragon.Controllable || !dragon.IsAlive)
            {
                stopped = true;
                StopAllCoroutines();
            }
        }

        // ───── 대상 ─────

        (float left, float right) ViewRange()
        {
            float half = view.orthographicSize * view.aspect;
            float x = view.transform.position.x;
            return (x - half, x + half);
        }

        bool OnScreen(ICombatTarget t)
        {
            var (l, r) = ViewRange();
            return t.Hitbox.xMax >= l && t.Hitbox.xMin <= r;
        }

        IEnumerable<ICombatTarget> AliveTargets =>
            buildings.Where(b => b.IsAlive).Cast<ICombatTarget>().Concat(airUnits.Where(u => u.IsAlive));

        void Hit(ActiveSkillId id, ICombatTarget target, double multiplier)
        {
            if (target == null || !target.IsAlive) return;
            bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
            double amount = Damage.Compute(stats.AttackPower, multiplier, critical, stats.CritMultiplier);
            target.TakeDamage(amount, critical);
            SkillHit?.Invoke(id, target, amount, critical);
        }

        // ───── 스킬 ─────

        /// <summary>메테오: 드래곤 앞쪽 건물 3개 범위에 N개가 떨어진다. 1개당 대상 1개, 대상보다 많으면 중복 타격 (T-09)</summary>
        IEnumerator Meteor(ActiveSkillRow row)
        {
            var box = dragon.Hitbox;
            float facing = dragon.Facing;
            var ahead = buildings.Where(b => b.IsAlive && (facing > 0 ? b.Right > box.center.x : b.Left < box.center.x))
                .OrderBy(b => Mathf.Abs(b.CenterX - box.center.x)).Take(MeteorRangeBuildings).ToList();
            float near = facing > 0 ? box.xMax : box.xMin;
            float far = ahead.Count > 0 ? (facing > 0 ? ahead.Max(b => b.Right) : ahead.Min(b => b.Left)) : near + facing * 3f;
            float min = Mathf.Min(near, far), max = Mathf.Max(near, far);
            var targets = AliveTargets.Where(t => t.Hitbox.xMax >= min && t.Hitbox.xMin <= max)
                .OrderBy(t => Mathf.Abs(t.Hitbox.center.x - near)).ToList();

            float top = view.transform.position.y + view.orthographicSize + 1f;
            for (int i = 0; i < row.hitCount; i++)
            {
                var target = targets.Count > 0 ? targets[i % targets.Count] : null;
                float x = target != null ? target.Hitbox.center.x : Mathf.Lerp(min, max, (i + 0.5f) / row.hitCount);
                float y = target != null ? target.Hitbox.center.y : (float)db.balance.groundHeight;
                StartCoroutine(MeteorFall(row, target, new Vector2(x + 1.5f, top), new Vector2(x, y)));
                yield return new WaitForSeconds(MeteorStagger);
            }
            yield return new WaitForSeconds(MeteorFallSeconds);
        }

        IEnumerator MeteorFall(ActiveSkillRow row, ICombatTarget target, Vector2 from, Vector2 to)
        {
            var fx = SkillFx.Meteor(transform.parent, from, new Color(1f, 0.45f, 0.2f));
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / MeteorFallSeconds;
                if (fx != null) fx.transform.position = Vector2.Lerp(from, to, t);
                yield return null;
            }
            if (fx != null) Destroy(fx);
            SkillFx.Burst(transform.parent, to, 1.2f, new Color(1f, 0.6f, 0.25f, 0.8f));
            if (shaker != null) shaker.Shake(0.12f, 0.08f);
            Hit(ActiveSkillId.Meteor, target, row.damageMultiplier);
        }

        /// <summary>허리케인: 드래곤 앞에서 생겨 화면 안을 좌우로 왕복하며(1 unit/s) 겹친 대상을 0.375초마다 때린다</summary>
        IEnumerator Hurricane(ActiveSkillRow row)
        {
            var box = dragon.Hitbox;
            float x = dragon.Facing > 0 ? box.xMax + HurricaneWidth / 2 : box.xMin - HurricaneWidth / 2;
            float direction = dragon.Facing;
            float ground = (float)db.balance.groundHeight;
            var fx = SkillFx.Column(transform.parent, new Vector2(x, ground + HurricaneHeight / 2), new Vector2(HurricaneWidth, HurricaneHeight),
                new Color(0.7f, 0.9f, 1f, 0.35f), (float)row.duration + 0.5f);
            int ticks = Mathf.RoundToInt((float)(row.duration / row.tickInterval));
            float tickTimer = 0;
            int done = 0;
            while (done < ticks)
            {
                float dt = Time.deltaTime;
                var (left, right) = ViewRange();
                x += direction * HurricaneSpeed * dt;
                if (x > right - HurricaneWidth / 2) { x = right - HurricaneWidth / 2; direction = -1; }
                if (x < left + HurricaneWidth / 2) { x = left + HurricaneWidth / 2; direction = 1; }
                if (fx != null) fx.transform.position = new Vector3(x, ground + HurricaneHeight / 2, 0);

                tickTimer += dt;
                if (tickTimer >= row.tickInterval)
                {
                    tickTimer -= (float)row.tickInterval;
                    done++;
                    var area = new Rect(x - HurricaneWidth / 2, ground, HurricaneWidth, HurricaneHeight);
                    foreach (var t in AliveTargets.Where(t => t.Hitbox.Overlaps(area)).ToList())
                        Hit(ActiveSkillId.Hurricane, t, row.damageMultiplier);
                }
                yield return null;
            }
            if (fx != null) Destroy(fx);
        }

        /// <summary>지진: 화면 안의 모든 건물과 공중 유닛을 0.5초마다 때린다. 지속 중에 화면에 들어온 대상도 포함</summary>
        IEnumerator Earthquake(ActiveSkillRow row)
        {
            int ticks = Mathf.RoundToInt((float)(row.duration / row.tickInterval));
            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds((float)row.tickInterval);
                if (shaker != null) shaker.Shake((float)row.tickInterval, 0.12f);
                var (left, right) = ViewRange();
                SkillFx.Ground(transform.parent, (left + right) / 2, right - left, (float)db.balance.groundHeight, new Color(0.6f, 0.45f, 0.3f, 0.5f));
                foreach (var t in AliveTargets.Where(OnScreen).ToList())
                    Hit(ActiveSkillId.Earthquake, t, row.damageMultiplier);
            }
        }
    }
}
