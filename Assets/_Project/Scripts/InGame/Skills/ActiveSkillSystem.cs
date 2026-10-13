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
using DWH.InGame.Fx;
using DWH.InGame.Stage;
using UnityEngine;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 액티브 스킬 3종 (active_skill, 가이드 6.10): 단축키 1·2·3 또는 버튼으로 즉시 발동한다. 쿨타임은 없고 세션당 사용 횟수만 있다.
    /// - 대미지 = 공격력 × 피해 배율(효과 36). 메테오 수(효과 37), 허리케인·지진 지속 시간(효과 38).
    /// - 횟수는 출격할 때 채우고(ActiveSpecs.UsesPerRun), 보너스 스테이지에서는 남은 횟수를 이어 쓴다.
    /// - 대상이 없어도 발동하고 횟수를 쓴다. 클리어·패배하면 진행 중인 효과를 즉시 끝낸다. 세 스킬 모두 공중 유닛도 때린다.
    /// </summary>
    public class ActiveSkillSystem : MonoBehaviour
    {
        const float MeteorFallSeconds = 0.35f;
        const float MeteorStagger = 0.12f;
        /// <summary>메테오가 비스듬히 떨어지는 정도: 떨어지는 높이당 가로 이동 (0.6 ≈ 세로에서 31도)</summary>
        const float MeteorSlant = 0.6f;
        const float HurricaneSpeed = 1f;
        const float HurricaneWidth = 1.2f;
        const float HurricaneHeight = 6f;

        GameDatabase db;
        ArtCatalog art;
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

        public void Init(GameDatabase database, ArtCatalog catalog, PlayerStats playerStats, DragonController owner, IReadOnlyList<Building> stageBuildings,
            IReadOnlyList<AirUnit> flyingUnits, Camera camera, int randomSeed)
        {
            db = database;
            art = catalog;
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
        public static Dictionary<ActiveSkillId, int> FullUses(IReadOnlyDictionary<ActiveSkillId, int> skillLevels) =>
            skillLevels.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => ActiveSpecs.Uses(kv.Value));

        public int Level(ActiveSkillId id) => levels.TryGetValue(id, out int l) ? l : 0;
        public int UsesLeft(ActiveSkillId id) => uses.TryGetValue(id, out int u) ? u : 0;
        public bool CanUse(ActiveSkillId id) => enabled && !stopped && Level(id) > 0 && UsesLeft(id) > 0 && dragon != null && dragon.Controllable && dragon.IsAlive;

        /// <summary>단계 n의 효과 값 (효과 36 피해 배율, 37 메테오 수, 38 지속 시간)</summary>
        public static double Value(ActiveSkillRow row, EffectType effect, int level) =>
            row.effectType1 == effect ? EffectMath.LevelValue(row.baseValue1, row.addValue1, level)
            : row.effectType2 == effect ? EffectMath.LevelValue(row.baseValue2, row.addValue2, level)
            : 0;

        public bool Use(ActiveSkillId id)
        {
            if (!CanUse(id)) return false;
            uses[id] = UsesLeft(id) - 1;
            var row = db.Active(id);
            int level = Level(id);
            double multiplier = Value(row, EffectType.ActiveDamage, level);
            StartCoroutine(id switch
            {
                ActiveSkillId.Meteor => Meteor(multiplier, (int)Math.Floor(Value(row, EffectType.MeteorCount, level))),
                ActiveSkillId.Hurricane => Hurricane(multiplier, (float)Value(row, EffectType.ActiveDuration, level)),
                _ => Earthquake(multiplier, (float)Value(row, EffectType.ActiveDuration, level)),
            });
            Used?.Invoke(id);
            return true;
        }

        void Update()
        {
            if (stopped || dragon == null) return;
            if (!dragon.Controllable || !dragon.IsAlive)
            {
                // 스타터 중에는 이 컴포넌트가 꺼져 있어 여기 오지 않는다. 전투가 끝나면 진행 중인 효과를 멈춘다
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
            double amount = Damage.Compute(dragon.AttackPower, multiplier, critical, stats.CritMultiplier);
            target.TakeDamage(amount, critical);
            SkillHit?.Invoke(id, target, amount, critical);
        }

        // ───── 스킬 ─────

        /// <summary>메테오: 드래곤 앞쪽 건물 3개 범위에 N개가 떨어진다. 1개당 대상 1개, 대상보다 많으면 중복 타격</summary>
        IEnumerator Meteor(double multiplier, int count)
        {
            var box = dragon.Hitbox;
            float facing = dragon.Facing;
            var ahead = buildings.Where(b => b.IsAlive && (facing > 0 ? b.Right > box.center.x : b.Left < box.center.x))
                .OrderBy(b => Mathf.Abs(b.CenterX - box.center.x)).Take(ActiveSpecs.MeteorRangeBuildings).ToList();
            float near = facing > 0 ? box.xMax : box.xMin;
            float far = ahead.Count > 0 ? (facing > 0 ? ahead.Max(b => b.Right) : ahead.Min(b => b.Left)) : near + facing * 3f;
            float min = Mathf.Min(near, far), max = Mathf.Max(near, far);
            var targets = AliveTargets.Where(t => t.Hitbox.xMax >= min && t.Hitbox.xMin <= max)
                .OrderBy(t => Mathf.Abs(t.Hitbox.center.x - near)).ToList();

            float top = view.transform.position.y + view.orthographicSize + 1f;
            for (int i = 0; i < count; i++)
            {
                var target = targets.Count > 0 ? targets[i % targets.Count] : null;
                float x = target != null ? target.Hitbox.center.x : Mathf.Lerp(min, max, (i + 0.5f) / count);
                float y = target != null ? target.Hitbox.center.y : Tuning.StandHeight;
                // 드래곤 뒤쪽 하늘에서 바라보는 쪽으로 비스듬히 떨어진다 (DWH-59: 방향이 고정돼 있었다)
                float slant = (top - y) * MeteorSlant;
                StartCoroutine(MeteorFall(multiplier, target, new Vector2(x - facing * slant, top), new Vector2(x, y)));
                yield return new WaitForSeconds(MeteorStagger);
            }
            yield return new WaitForSeconds(MeteorFallSeconds);
        }

        IEnumerator MeteorFall(double multiplier, ICombatTarget target, Vector2 from, Vector2 to)
        {
            var fx = SkillFx.Meteor(transform.parent, db, art, from);
            // 메테오 그림은 오른쪽 아래로 떨어지는 모습이다. 실제로 떨어지는 방향에 맞춰 뒤집고 돌린다
            if (fx != null && fx.TryGetComponent(out SpriteAnim anim)) anim.Orient(new Vector2(1f, -1f), to - from);
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / MeteorFallSeconds;
                if (fx != null) fx.transform.position = Vector2.Lerp(from, to, t);
                yield return null;
            }
            if (fx != null) Destroy(fx);
            SkillFx.Burst(transform.parent, db, art, FxIds.MeteorExplosion, to, 2f, new Color(1f, 0.6f, 0.25f, 0.8f));
            if (shaker != null) shaker.Shake(0.12f, 0.08f);
            Hit(ActiveSkillId.Meteor, target, multiplier);
        }

        /// <summary>허리케인: 드래곤 앞에서 생겨 화면 안을 좌우로 왕복하며(1 unit/s) 겹친 대상을 0.375초마다 때린다</summary>
        IEnumerator Hurricane(double multiplier, float duration)
        {
            var box = dragon.Hitbox;
            float x = dragon.Facing > 0 ? box.xMax + HurricaneWidth / 2 : box.xMin - HurricaneWidth / 2;
            float direction = dragon.Facing;
            // 왕복 범위는 쓴 순간의 화면(월드 좌표)으로 고정한다. 드래곤이 움직여도 회오리가 화면 끝에 붙어 따라오지 않는다 (DWH-58)
            var (left, right) = ViewRange();
            x = Mathf.Clamp(x, left + HurricaneWidth / 2, right - HurricaneWidth / 2);
            float ground = Tuning.StandHeight;
            var fx = SkillFx.Column(transform.parent, db, art, new Vector2(x, ground + HurricaneHeight / 2), new Vector2(HurricaneWidth, HurricaneHeight), duration + 0.3f);
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / ActiveSpecs.HurricaneTick));
            float tickTimer = 0;
            int done = 0;
            while (done < ticks)
            {
                float dt = Time.deltaTime;
                x += direction * HurricaneSpeed * dt;
                if (x > right - HurricaneWidth / 2) { x = right - HurricaneWidth / 2; direction = -1; }
                if (x < left + HurricaneWidth / 2) { x = left + HurricaneWidth / 2; direction = 1; }
                if (fx != null) fx.transform.position = new Vector3(x, ground + HurricaneHeight / 2, 0);

                tickTimer += dt;
                if (tickTimer >= ActiveSpecs.HurricaneTick)
                {
                    tickTimer -= ActiveSpecs.HurricaneTick;
                    done++;
                    var area = new Rect(x - HurricaneWidth / 2, ground, HurricaneWidth, HurricaneHeight);
                    foreach (var t in AliveTargets.Where(t => t.Hitbox.Overlaps(area)).ToList())
                        Hit(ActiveSkillId.Hurricane, t, multiplier);
                }
                yield return null;
            }
            if (fx != null) Destroy(fx);
        }

        /// <summary>
        /// 지진: 화면 안의 모든 건물과 공중 유닛을 0.5초마다 때린다. 지속 중에 화면에 들어온 대상도 포함.
        /// 균열은 지속 시간 내내 지면 위에 끊김 없이 깔리고 화면을 따라간다 (DWH-57)
        /// </summary>
        IEnumerator Earthquake(double multiplier, float duration)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / ActiveSpecs.EarthquakeTick));
            float total = ticks * ActiveSpecs.EarthquakeTick;
            var (l0, r0) = ViewRange();
            var strip = SkillFx.QuakeStrip(transform.parent, db, art, l0, r0, Tuning.StandHeight, total + 0.3f);
            float tickTimer = 0;
            int done = 0;
            if (shaker != null) shaker.Shake(total, 0.1f);
            while (done < ticks)
            {
                var (left, _) = ViewRange();
                SkillFx.LayQuakeStrip(strip, left, Tuning.StandHeight);
                tickTimer += Time.deltaTime;
                if (tickTimer >= ActiveSpecs.EarthquakeTick)
                {
                    tickTimer -= ActiveSpecs.EarthquakeTick;
                    done++;
                    foreach (var t in AliveTargets.Where(OnScreen).ToList())
                        Hit(ActiveSkillId.Earthquake, t, multiplier);
                }
                yield return null;
            }
        }
    }
}
