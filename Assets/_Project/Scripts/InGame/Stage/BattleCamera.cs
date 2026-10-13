using System;
using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Stage
{
    /// <summary>
    /// 드래곤을 따라 가로로만 스크롤한다 (4.1.2). 스테이지 양 끝 밖은 보여 주지 않는다.
    /// 진행 방향 앞보기: 드래곤이 바라보는 쪽을 넓게 보여 준다. 오른쪽을 보면 드래곤이 화면 왼쪽 1/3, 왼쪽을 보면 오른쪽 1/3.
    /// 방향이 Tuning.CameraTurnDelay(0.15초) 이상 유지돼야 바꾸고(넉백·짧은 방향 전환 무시), Tuning.CameraTurnSeconds(0.6초) 동안 부드럽게(SmoothStep) 옮겨 간다.
    /// </summary>
    public class BattleCamera : MonoBehaviour
    {
        Camera view;
        Transform target;
        Func<float> facing;
        float stageLeft, stageRight;
        float shakeTimer, shakeAmplitude;
        float leadDirection = 1f, pendingTimer, lead, turnFrom, turnTimer;

        /// <summary>스테이지 스타터가 카메라를 직접 움직이는 동안 따라가기를 멈춘다</summary>
        public bool Suspended { get; set; }

        /// <summary>지금 넓게 보여 주는 쪽 (+1 오른쪽, -1 왼쪽)</summary>
        public float LeadDirection => leadDirection;

        /// <summary>환경설정 "화면 흔들림". 끄면 Shake를 무시한다</summary>
        public static bool ShakeEnabled = true;

        /// <summary>화면 흔들림 (충격파 4.3). 세로로만 흔든다</summary>
        public void Shake(float seconds, float amplitude)
        {
            if (!ShakeEnabled) return;
            shakeTimer = Mathf.Max(shakeTimer, seconds);
            shakeAmplitude = amplitude;
        }

        /// <param name="facingOf">드래곤이 바라보는 방향(+1 오른쪽, -1 왼쪽)</param>
        public void Init(Camera camera, Transform follow, Func<float> facingOf, float stageLeft, float stageRight)
        {
            view = camera;
            target = follow;
            facing = facingOf;
            leadDirection = 1f;
            pendingTimer = 0;
            lead = LeadDistance();
            turnFrom = lead;
            turnTimer = Tuning.CameraTurnSeconds;
            this.stageLeft = stageLeft;
            this.stageRight = stageRight;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (target == null || Suspended || Time.timeScale <= 0) return;
            FitWidth();
            UpdateLead(Time.deltaTime);
            float halfWidth = view.orthographicSize * view.aspect;
            float minX = stageLeft + halfWidth, maxX = Mathf.Max(minX, stageRight - halfWidth);
            var pos = view.transform.position;
            pos.x = Mathf.Clamp(target.position.x + lead, minX, maxX);
            pos.y = view.orthographicSize;
            if (shakeTimer > 0)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                pos.y += Mathf.Sin(Time.unscaledTime * 90f) * shakeAmplitude;
            }
            view.transform.position = pos;
        }

        /// <summary>
        /// 16:9보다 좁은 화면(웹 창, 에디터 Game 뷰 등)에서도 16:9 기준 가로 폭이 다 보이게 카메라를 키운다.
        /// 위로만 넓어지고 지면은 화면 아래에 그대로 붙는다. 넓은 화면은 기본 크기 그대로 (QA: 게임 진행 중 화면 잘림)
        /// </summary>
        void FitWidth()
        {
            float minHalfWidth = Tuning.CameraOrthoSize * Tuning.CameraMinAspect;
            view.orthographicSize = Mathf.Max(Tuning.CameraOrthoSize, minHalfWidth / Mathf.Max(0.1f, view.aspect));
        }

        /// <summary>화면 가운데에서 드래곤까지 거리: 드래곤이 뒤쪽 가장자리에서 화면 폭의 Tuning.CameraLeadScreenFraction 지점에 오게</summary>
        float LeadDistance()
        {
            float halfWidth = view.orthographicSize * view.aspect;
            return halfWidth - halfWidth * 2f * Tuning.CameraLeadScreenFraction;
        }

        void UpdateLead(float dt)
        {
            float wanted = facing != null && facing() < 0 ? -1f : 1f;
            if (wanted == leadDirection) pendingTimer = 0;
            else if ((pendingTimer += dt) >= Tuning.CameraTurnDelay)
            {
                leadDirection = wanted;
                pendingTimer = 0;
                // 지금 위치에서 반대쪽까지 정해진 시간에 걸쳐 옮긴다 (중간에 다시 돌아도 그 자리에서 다시 시작)
                turnFrom = lead;
                turnTimer = 0;
            }
            turnTimer = Mathf.Min(turnTimer + dt, Tuning.CameraTurnSeconds);
            float t = Mathf.SmoothStep(0f, 1f, turnTimer / Tuning.CameraTurnSeconds);
            lead = Mathf.Lerp(turnFrom, leadDirection * LeadDistance(), t);
        }
    }
}
