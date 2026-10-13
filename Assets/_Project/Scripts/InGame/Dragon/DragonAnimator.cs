using DWH.InGame.Fx;
using UnityEngine;

namespace DWH.InGame.Dragon
{
    /// <summary>
    /// 드래곤 파츠 애니메이션 (웹 프로토타입 v0.3 방식: 파츠를 회전점 기준으로 돌려 움직인다. 동작 그림은 없다).
    /// - 기본: 걸을 때 다리를 번갈아 흔들고, 꼬리·날개가 살랑인다. 공중에서는 다리를 접고 날개를 편다.
    /// - 몸통 박치기: 앞다리로 차며 몸을 앞으로 들이민다(Kick, Lunge).
    /// - 브레스: 아래턱(아랫머리)이 벌어진다(SetBreathing). 파이어 볼은 짧게 벌린다(PulseJaw). 낙뢰는 고개를 들고 포효(Roar).
    /// - 회전 회오리: 붕 떠서 날개를 펴고 제자리에서 빙글빙글 돈다(Spin). 이동은 그대로다.
    /// - 충격파: 몸을 들었다가 발로 땅을 쿵 찍는다(Stomp). 찍는 순간까지의 시간을 돌려준다.
    /// 각도는 원본 그림 기준(그림은 왼쪽을 본다: 앞 = -x)이라 방향을 뒤집어도 의미가 같다. 충돌 박스는 바뀌지 않는다.
    /// </summary>
    public class DragonAnimator : MonoBehaviour
    {
        const float WalkStride = 2.4f;     // 이동 1 unit당 걸음 위상(라디안, 크기 1 기준)
        const float SpinSpeed = 780f;      // 회오리 회전 속도(도/초)
        const float StompWindup = 0.22f;
        const float StompSlam = 0.07f;
        const float StompRecover = 0.25f;

        DragonRig rig;
        float groundY, size;
        Vector2 pivot;
        Transform body, neck, upHead, downHead, frontLeg, frontLegB, backLeg, backLegB, wing, wingB, tail;

        float walkPhase, lastX, speedRatio, flap, time;
        float claw, lunge, jaw, jawPulse, roar, spinTimer, spinAngle, spinDir = 1, hover, wingOpen;
        float stompTime = -1;
        bool breathing;

        public FxParticles Fx { get; set; }
        /// <summary>마우스 유도 조준 지점 (AimGuide가 넣는다). 있으면 고개를 그쪽으로 돌린다</summary>
        public Vector2? Aim { get; set; }
        /// <summary>마우스 조준이 없을 때(자동 이동) 브레스가 겨누는 곳. 브레스 중 하늘의 공중 유닛을 올려다볼 때 쓴다</summary>
        public Vector2? AutoAim { get; set; }
        float aimNeck, aimHead;
        /// <summary>커서 조준 각도 한계(도). 원본 그림 기준 음수 = 듦</summary>
        const float AimNeckUp = 40f, AimNeckDown = 18f, AimHeadUp = 20f, AimHeadDown = 8f;
        public bool IsSpinning => spinTimer > 0;
        public bool IsBreathing => breathing;
        /// <summary>아래턱이 벌어진 정도 0~1 (테스트용)</summary>
        public float Jaw => jaw;
        public float SpinAngle => spinAngle;
        /// <summary>바라보는 방향 (1 = 오른쪽)</summary>
        public float Facing => rig.Visual.localScale.x < 0 ? 1f : -1f;

        public void Init(DragonRig dragonRig, float groundHeight, float sizeScale)
        {
            rig = dragonRig;
            groundY = groundHeight;
            size = sizeScale;
            // 회전 중심: 쉬는 자세의 그림 가운데 (리그 로컬 좌표)
            var b = rig.LocalBounds;
            pivot = b.center / Mathf.Max(0.01f, rig.transform.localScale.x);
            Transform N(string part) => rig.HasNode(part) ? rig.Node(part) : null;
            body = N("Body"); neck = N("Neck"); upHead = N("UpHead"); downHead = N("DownHead");
            frontLeg = N("FrontLeg"); frontLegB = N("FrontLeg_B"); backLeg = N("BackLeg"); backLegB = N("BackLeg_B");
            wing = N("Wing"); wingB = N("Wing_B"); tail = N("Tail");
            lastX = transform.position.x;
        }

        // ───── 동작 요청 ─────

        /// <summary>몸통 박치기 한 번: 앞다리로 차고 몸을 들이민다</summary>
        public void Kick()
        {
            claw = 1f;
            lunge = Mathf.Max(lunge, 0.7f);
        }

        /// <summary>돌진처럼 크게 들이민다</summary>
        public void Lunge(float power = 1f) => lunge = Mathf.Max(lunge, power);

        public void SetBreathing(bool on) => breathing = on;

        /// <summary>파이어 볼: 입을 잠깐 벌린다</summary>
        public void PulseJaw(float seconds = 0.22f) => jawPulse = Mathf.Max(jawPulse, seconds);

        /// <summary>낙뢰 포효: 고개를 들고 입을 크게 벌린다</summary>
        public void Roar(float seconds) => roar = Mathf.Max(roar, seconds);

        public void Spin(float seconds, float direction)
        {
            spinTimer = Mathf.Max(spinTimer, seconds);
            spinDir = direction >= 0 ? 1 : -1;
        }

        /// <summary>충격파 발 구르기를 시작하고, 발이 땅에 닿을 때까지의 시간을 돌려준다</summary>
        public float Stomp()
        {
            stompTime = 0;
            return StompWindup + StompSlam;
        }

        /// <summary>전투가 끝나거나 스킬이 끊기면 지속 동작을 멈춘다</summary>
        public void StopSkills()
        {
            breathing = false;
            spinTimer = 0;
            roar = 0;
        }

        /// <summary>
        /// 불이 나오는 입 위치 (월드). 윗머리 아래 변(아래턱이 붙는 선)의 주둥이 쪽.
        /// 10-13 피드백: 예전 값(아래에서 32%, 끝에서 6%)은 콧구멍 높이라 코에서 불이 나왔다.
        /// 머리 그림 안의 한 점(왼쪽을 보는 원본 기준 주둥이 끝에서 12%, 아래에서 8%)을 머리의 실제 회전·뒤집기로 옮긴다.
        /// 예전에는 회전된 그림의 바깥 상자 모서리를 써서, 머리가 커서를 따라 기울면 입과 어긋났다 (10-13 피드백)
        /// </summary>
        public Vector2 Mouth
        {
            get
            {
                var r = rig.PartRenderer("UpHead");
                if (r == null || r.sprite == null) return transform.position;
                var b = r.sprite.bounds;
                var local = new Vector3(b.min.x + b.size.x * 0.12f, b.min.y + b.size.y * 0.08f, 0);
                return r.transform.TransformPoint(local);
            }
        }

        /// <summary>머리가 실제로 향한 방향 (월드, 단위 벡터). 원본 그림은 왼쪽을 보므로 머리의 왼쪽 축을 회전·뒤집기와 함께 옮긴다</summary>
        public Vector2 MouthDirection
        {
            get
            {
                var r = rig.PartRenderer("UpHead");
                if (r == null) return new Vector2(Facing, 0);
                var d = (Vector2)r.transform.TransformVector(Vector3.left);
                return d.sqrMagnitude > 1e-6f ? d.normalized : new Vector2(Facing, 0);
            }
        }

        // ───── 매 프레임 자세 ─────

        void LateUpdate()
        {
            if (rig == null || Time.timeScale <= 0) return;
            float dt = Time.deltaTime;
            time += dt;

            float x = transform.position.x;
            float moved = dt > 0 ? Mathf.Abs(x - lastX) / dt : 0;
            lastX = x;
            speedRatio = Mathf.Lerp(speedRatio, Mathf.Clamp01(moved / 3f), 1f - Mathf.Exp(-10f * dt));
            bool airborne = transform.position.y > groundY + 0.05f || hover > 0.05f;
            if (!airborne) walkPhase += moved * dt * WalkStride / size;

            claw = Mathf.Max(0, claw - dt * 4f);
            lunge = Mathf.Max(0, lunge - dt * 3.5f);
            jawPulse = Mathf.Max(0, jawPulse - dt);
            roar = Mathf.Max(0, roar - dt);
            float jawTarget = breathing || roar > 0 ? 1f : jawPulse > 0 ? 0.75f : 0f;
            jaw = Mathf.Lerp(jaw, jawTarget, 1f - Mathf.Exp(-16f * dt));

            if (spinTimer > 0)
            {
                spinTimer -= dt;
                spinAngle += SpinSpeed * spinDir * dt;
                if (spinTimer <= 0) spinAngle = 0;
            }
            float hoverTarget = spinTimer > 0 ? 0.55f * size : 0f;
            hover = Mathf.Lerp(hover, hoverTarget, 1f - Mathf.Exp(-12f * dt));
            wingOpen = Mathf.Lerp(wingOpen, airborne || spinTimer > 0 ? 1f : 0f, 1f - Mathf.Exp(-7f * dt));
            flap += dt * (airborne ? 9f : 2f);

            // 발 구르기 타임라인: 들어 올림 → 쿵 → 회복
            float stompLift = 0, stompTilt = 0, stompLeg = 0;
            if (stompTime >= 0)
            {
                stompTime += dt;
                float t = stompTime;
                if (t < StompWindup)
                {
                    float k = Mathf.SmoothStep(0, 1, t / StompWindup);
                    stompLift = 0.35f * k; stompTilt = -9f * k; stompLeg = -38f * k;
                }
                else if (t < StompWindup + StompSlam)
                {
                    float k = (t - StompWindup) / StompSlam;
                    stompLift = Mathf.Lerp(0.35f, -0.04f, k); stompTilt = Mathf.Lerp(-9f, 6f, k); stompLeg = Mathf.Lerp(-38f, 12f, k);
                }
                else if (t < StompWindup + StompSlam + StompRecover)
                {
                    float k = 1f - (t - StompWindup - StompSlam) / StompRecover;
                    stompLift = -0.04f * k; stompTilt = 6f * k; stompLeg = 12f * k;
                }
                else stompTime = -1;
            }

            // 다리 (원본 그림 기준 각도: 음수 = 앞으로 차기)
            float swing = 20f * Mathf.Min(1.3f, speedRatio);
            float LegAngle(float phase, bool hind, bool near)
            {
                if (airborne) return hind ? 28f : -22f;
                float a = -Mathf.Sin(walkPhase + phase) * swing;
                if (!hind && near) a += -62f * claw + stompLeg;
                if (!hind && !near) a += stompLeg * 0.7f;
                return a;
            }
            SetRot(frontLeg, LegAngle(Mathf.PI, false, true));
            SetRot(frontLegB, LegAngle(0, false, false));
            SetRot(backLeg, LegAngle(0, true, true));
            SetRot(backLegB, LegAngle(Mathf.PI, true, false));

            // 꼬리, 날개
            SetRot(tail, Mathf.Sin(time * 2.4f) * 5f + (airborne ? -8f : 0f) - speedRatio * 3f);
            float wingRot = wingOpen * (10f + 22f * Mathf.Sin(flap)) + Mathf.Sin(time * 2.2f) * 1.5f;
            SetRot(wing, wingRot);
            SetRot(wingB, wingOpen * (10f + 22f * Mathf.Sin(flap + 0.6f)) + Mathf.Sin(time * 2.2f + 1f) * 1.5f);

            // 목·머리·턱: 포효는 고개를 들고, 브레스는 살짝 숙여 앞을 겨눈다
            float roarK = roar > 0 ? 1f : 0f;
            // 조준: 목 기준으로 조준 지점이 위면 고개를 들고 아래면 숙인다 (원본 그림 기준 음수 = 듦)
            // 10-13: 브레스로 공중 유닛을 맞힐 수 있게 하늘까지 올려다본다. 목이 최대 40도, 머리가 20도를 더 든다 (아래는 18+8도)
            float aimTarget = 0, aimHeadTarget = 0;
            if ((Aim ?? AutoAim) is Vector2 aim && neck != null)
            {
                var d = aim - (Vector2)neck.position;
                if (d.x * Facing > 0.2f)
                {
                    float up = Mathf.Atan2(d.y, Mathf.Abs(d.x)) * Mathf.Rad2Deg;   // 위 = 양수
                    aimTarget = Mathf.Clamp(-up * 0.7f, -AimNeckUp, AimNeckDown);
                    aimHeadTarget = Mathf.Clamp(-up * 0.3f, -AimHeadUp, AimHeadDown);
                }
            }
            aimNeck = Mathf.Lerp(aimNeck, aimTarget, 1f - Mathf.Exp(-10f * dt));
            aimHead = Mathf.Lerp(aimHead, aimHeadTarget, 1f - Mathf.Exp(-10f * dt));
            SetRot(neck, -12f * roarK + (breathing && Aim == null ? 4f : 0f) + aimNeck + Mathf.Sin(time * 1.6f) * 1.5f);
            SetRot(upHead, -10f * roarK + (breathing && Aim == null ? 3f : 0f) + aimHead);
            SetRot(downHead, 30f * jaw);

            // 몸 전체: 들이밀기(앞으로 기울며 전진), 발 구르기(들었다 내려찍기), 회오리(띄워서 회전), 걸을 때 출렁임
            float facing = Facing;
            float tiltForward = 9f * lunge + stompTilt;               // 양수 = 머리를 숙임
            float angle = -facing * tiltForward + spinAngle;          // 월드 기준 반시계 +
            float bob = airborne ? 0 : Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f * speedRatio;
            var offset = new Vector2(facing * 0.28f * lunge, hover / Mathf.Max(0.01f, size) + (stompLift + bob));
            var rot = Quaternion.Euler(0, 0, angle);
            rig.Visual.localRotation = rot;
            rig.Visual.localPosition = (Vector3)(pivot - (Vector2)(rot * pivot) + offset);
        }

        static void SetRot(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(0, 0, degrees);
        }
    }
}
