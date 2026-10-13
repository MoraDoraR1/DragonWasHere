namespace DWH.Data
{
    /// <summary>
    /// 데이터 테이블에 없는 값. 게임 규칙 수치는 모두 테이블(GameDatabase)에서 읽고, 여기에는 연출·물리와
    /// 기획서에만 있는 기본값을 모은다. 기획이 테이블에 컬럼을 추가하면 여기서 지운다.
    /// 거리 단위는 unit(1 unit = 100 px)이다.
    /// </summary>
    public static class Tuning
    {
        /// <summary>데이터의 px 값을 unit으로 바꿀 때 나누는 값 (가이드 2장)</summary>
        public const float PixelsPerUnit = 100f;

        public static float Px(double px) => (float)(px / PixelsPerUnit);

        // ───── 타이틀 (UI 기획서 2장). resource 테이블 res_id ─────
        public const string TitleLogoKoRes = "res6000138";
        public const string TitleLogoEnRes = "res6000137";
        /// <summary>타이틀 배경 영상. 빌드 때 StreamingAssets/{res_name}으로 복사해 URL로 재생한다</summary>
        public const string TitleVideoRes = "res9000001";

        // ───── 화면 (가이드 2장, 임시unity제작.md 9.3) ─────
        public const float CameraOrthoSize = 5.4f;
        /// <summary>전투 화면에서 늘 보장하는 가로:세로 비율. 이보다 좁은 화면은 카메라를 키워 가로 폭을 지킨다</summary>
        public const float CameraMinAspect = 16f / 9f;
        /// <summary>진행 방향 앞보기: 드래곤은 뒤쪽 가장자리에서 화면 폭의 이 비율 지점에 선다 (1/3)</summary>
        public const float CameraLeadScreenFraction = 1f / 3f;
        /// <summary>바라보는 방향이 이 시간 이상 유지돼야 카메라가 돌아선다(넉백·짧은 방향 전환 무시)</summary>
        public const float CameraTurnDelay = 0.15f;
        /// <summary>카메라가 반대쪽으로 넘어가는 시간(초). 10-12 피드백 1.5 → 10-13 피드백(드래곤 방향 전환보다 너무 느림) 0.6</summary>
        public const float CameraTurnSeconds = 0.6f;
        /// <summary>지면 높이 = 바닥 배경 이미지 높이(244 px)</summary>
        public const float GroundHeight = 2.44f;
        /// <summary>
        /// 건물·랜드마크·드래곤이 서는 높이. 바닥 그림 맨 위는 테두리선이고 그 아래 0.45~0.66 unit이 밝은 지표 띠라,
        /// 테두리선 위에 세우면 떠 보인다 → 지표 띠 안쪽으로 0.25 내려 세운다 (10-13 사용자 요청)
        /// </summary>
        public const float StandHeight = GroundHeight - 0.25f;
        /// <summary>중경 스크롤 = 바닥의 1/3 (가이드 6.5)</summary>
        public const float ParallaxMidFactor = 1f / 3f;
        /// <summary>드래곤 시작 위치에서 첫 건물까지</summary>
        public const float StageStartMargin = 3f;
        /// <summary>충돌 박스 = 그림 경계 × 비율</summary>
        public const float ColliderScaleX = 0.85f, ColliderScaleY = 0.9f;
        /// <summary>드래곤 파츠 sortingOrder = 이 값 - layer_order (가이드 5장)</summary>
        public const int DragonSortingBase = 20;
        /// <summary>반대편 파츠 밝기 (가이드 5장)</summary>
        public const float BackPartTint = 0.6f;
        /// <summary>건물 그림의 위·좌·우 투명 여백(px). Fix_Resource 건물은 여백이 없다 [임시]</summary>
        public const float BuildingArtMarginPx = 0f;
        public const float HitFlashSeconds = 0.05f;
        public const float EndSlowmoScale = 0.2f;

        // ───── 드래곤 (임시unity제작.md 4.1, 8.6) ─────
        /// <summary>피로 증가량. fatigue_interval_sec마다 +1</summary>
        public const double FatigueStep = 1;
        /// <summary>몸통 박치기 후 뒤로 밀리는 거리. 시간은 dragon.knockback_sec</summary>
        public const float KnockbackDistance = 0.15f;
        /// <summary>이 거리 안이면 건물과 닿은 것으로 본다</summary>
        public const float ContactTolerance = 0.05f;
        public const float JumpCooldown = 0.5f;

        // ───── 탄 (8.5.2) ─────
        public const float ProjectileSpeed = 5f;
        public const float ProjectileSize = 0.2f;
        /// <summary>투사체 그림 배율 (256px 프레임 = 1 unit, 공 지름 약 0.31 unit)</summary>
        public const float ProjectileSpriteScale = 1f;
        /// <summary>공격 건물 탄 높이(드래곤 충돌 박스 높이 대비). 1 미만은 지면 탄이라 점프로 피한다</summary>
        public const float AttackShotHeight = 0.6f;
        /// <summary>랜드마크 3발(아래·중간·위) 높이 비율</summary>
        public static readonly float[] LandmarkShotHeights = { 0.25f, 0.6f, 1.3f };

        // ───── 공중 유닛 (4.5) ─────
        public const float AirUnitAltitudeMin = 3f, AirUnitAltitudeMax = 4.5f;
        /// <summary>공중 유닛 그림 배율 (QA DWH-60: ×1.5~2.0 제안 → 1.75)</summary>
        public const float AirUnitScale = 1.75f;
        public const float AirUnitProjectileSpeed = 4f;
        /// <summary>[임시] 공중 유닛 공격 범위: 드래곤과의 가로 거리(unit). unit 테이블에 컬럼이 없다</summary>
        public const float AirUnitAttackRange = 6f;
        /// <summary>[임시] 공중 유닛이 드래곤 머리 위를 배회하는 가로 폭(드래곤 기준 ±, unit). 공격 범위 안쪽</summary>
        public const float AirUnitHoverRange = 3.5f;
        /// <summary>공중 유닛은 드래곤(서 있을 때) 머리보다 이만큼 + 자기 몸 절반 이상 높이에서만 난다. 몸통 충돌을 피한다</summary>
        public const float AirUnitHeadClearance = 0.8f;

        // ───── 드랍 연출 (4.6) ─────
        public const float DropBurstSpeedMin = 2.5f, DropBurstSpeedMax = 5.5f;
        /// <summary>튀는 방향의 수직 기준 최대 기울기(도)</summary>
        public const float DropBurstAngle = 50f;
        public const float DropGravity = 14f;
        public const float DropAbsorbSpeed = 14f;
        public const float DropCollectDistance = 0.3f;

        // ───── 진행 (가이드 1장) ─────
        /// <summary>
        /// 환생 벽. n번째 환생은 n번째 벽 스테이지에 도달하면 해금된다. 최대 횟수 = 벽 수.
        /// 테이블에 표시가 없어 가이드 1장 값을 쓴다 [임시] (const 추가 요청 대상)
        /// </summary>
        public static readonly string[] RebirthWalls = { "stg302", "stg502" };

        /// <summary>건물 파괴 시 기력 회복(효과 3)에서 랜드마크 배수 (upgrade_desc_202)</summary>
        public const double LandmarkStaminaMultiplier = 5;
        /// <summary>낮은 기력 공격력 증가(효과 7)가 켜지는 기력 비율 (upgrade_desc_206)</summary>
        public const double LowStaminaRatio = 0.25;
        /// <summary>황금 감별안으로 황금화된 건물의 드랍 골드 배수 [임시 해석] (가이드 6.7 효과 9)</summary>
        public const double GildedGoldMultiplier = 3;
        /// <summary>화상 지속 시간(초) (가이드 6.7 효과 19)</summary>
        public const float BurnSeconds = 3f;
    }

    /// <summary>
    /// 패시브 스킬 기본값 (임시unity제작.md 4.3, 패시브 스킬 기획서 Ver03). 테이블에는 업그레이드 효과만 있다(가이드 6.7).
    /// 대미지 배율은 공격력 대비, 범위는 unit.
    /// </summary>
    public static class PassiveSpecs
    {
        public class Spec
        {
            public PassiveSkillId id;
            public MotionSlot slot;
            public float cooldown;
            public double damage;
            /// <summary>발 수, 틱 수, 번개 수, 회전 수 등 기본 횟수</summary>
            public int hits;
            /// <summary>횟수 사이 간격(초)</summary>
            public float interval;
            public float range;
            /// <summary>스킬 아이콘 (resource.res_id)</summary>
            public string icon;
            /// <summary>해금·강화 효과. 이 효과가 1번 효과인 업그레이드 노드가 1레벨이면 해금된다</summary>
            public EffectType unlockEffect;
        }

        public static readonly Spec BodySlam = new() { id = PassiveSkillId.BodySlam, slot = MotionSlot.Body, damage = 1.0, icon = "res6000091", unlockEffect = EffectType.BodySlamDamage };
        /// <summary>돌진: 앞쪽 건물을 관통하며 전진한다. 쿨타임은 효과 26 값. 관통 수는 정의가 없다 [임시]</summary>
        public static readonly Spec Dash = new() { id = PassiveSkillId.Dash, slot = MotionSlot.Body, damage = 1.0, hits = 2, range = 3f, icon = "res6000103", unlockEffect = EffectType.DashCooldown };
        public static readonly Spec FireBall = new() { id = PassiveSkillId.FireBall, slot = MotionSlot.Head, cooldown = 3f, damage = 0.6, hits = 3, interval = 0.15f, icon = "res6000094", unlockEffect = EffectType.FireBallDamage };
        public static readonly Spec FireBreath = new() { id = PassiveSkillId.FireBreath, slot = MotionSlot.Head, cooldown = 7f, damage = 0.4, hits = 6, interval = 0.5f, range = 0.75f, icon = "res6000093", unlockEffect = EffectType.BreathDamage };
        public static readonly Spec Lightning = new() { id = PassiveSkillId.Lightning, slot = MotionSlot.Head, cooldown = 9f, damage = 1.5, hits = 3, interval = 0.1f, icon = "res6000096", unlockEffect = EffectType.LightningDamage };
        /// <summary>충격파: 꼬리로 땅을 내리친다. 맞닿은 건물 1개 + 충격 범위만큼 양옆 건물 (가이드 6.7)</summary>
        public static readonly Spec Shockwave = new() { id = PassiveSkillId.Shockwave, slot = MotionSlot.Body, cooldown = 10f, damage = 1.5, hits = 1, range = 0.5f, icon = "res6000098", unlockEffect = EffectType.ShockwaveDamage };
        public static readonly Spec Whirlwind = new() { id = PassiveSkillId.Whirlwind, slot = MotionSlot.Body, cooldown = 8f, damage = 1.5, hits = 2, interval = 0.3f, range = 0.5f, icon = "res6000099", unlockEffect = EffectType.WhirlwindDamage };

        public const float ShockwaveStunSeconds = 1f;
        public const float ShockwaveShakeSeconds = 0.3f;
        public const float LightningRoarDelay = 0.5f;
        /// <summary>같은 모션 슬롯 순차 발동 간격은 const.motion_queue_interval_sec</summary>
        public static readonly Spec[] All = { BodySlam, Dash, FireBreath, FireBall, Shockwave, Whirlwind, Lightning };

        public static Spec Of(PassiveSkillId id) => System.Array.Find(All, s => s.id == id);
    }

    /// <summary>액티브 스킬 기본값 (임시unity제작.md 4.4). 대미지·개수·지속은 active_skill 효과에서 읽는다</summary>
    public static class ActiveSpecs
    {
        /// <summary>단계별 세션당 사용 횟수. 테이블에 없다(가이드 6.10) → active_skill.uses_per_run 컬럼 추가 요청 대상</summary>
        public static readonly int[] UsesPerRun = { 1, 1, 2, 2, 3 };
        public const float HurricaneTick = 0.375f;
        public const float EarthquakeTick = 0.5f;
        public const int MeteorRangeBuildings = 3;

        public static int Uses(int level) => level <= 0 ? 0 : UsesPerRun[System.Math.Min(level, UsesPerRun.Length) - 1];
    }
}
