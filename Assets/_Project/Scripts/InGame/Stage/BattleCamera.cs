using UnityEngine;

namespace DWH.InGame.Stage
{
    /// <summary>드래곤을 따라 가로로만 스크롤한다 (4.1.2). 스테이지 양 끝 밖은 보여 주지 않는다.</summary>
    public class BattleCamera : MonoBehaviour
    {
        Camera view;
        Transform target;
        float offsetX, minX, maxX;
        float shakeTimer, shakeAmplitude;

        /// <summary>스테이지 스타터가 카메라를 직접 움직이는 동안 따라가기를 멈춘다</summary>
        public bool Suspended { get; set; }

        /// <summary>화면 흔들림 (충격파 4.3). 세로로만 흔든다</summary>
        public void Shake(float seconds, float amplitude)
        {
            shakeTimer = Mathf.Max(shakeTimer, seconds);
            shakeAmplitude = amplitude;
        }

        public void Init(Camera camera, Transform follow, float dragonOffsetX, float stageLeft, float stageRight)
        {
            view = camera;
            target = follow;
            offsetX = dragonOffsetX;
            float halfWidth = view.orthographicSize * view.aspect;
            minX = stageLeft + halfWidth;
            maxX = Mathf.Max(minX, stageRight - halfWidth);
            LateUpdate();
        }

        void LateUpdate()
        {
            if (target == null || Suspended) return;
            var pos = view.transform.position;
            pos.x = Mathf.Clamp(target.position.x - offsetX, minX, maxX);
            pos.y = view.orthographicSize;
            if (shakeTimer > 0)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                pos.y += Mathf.Sin(Time.unscaledTime * 90f) * shakeAmplitude;
            }
            view.transform.position = pos;
        }
    }
}
