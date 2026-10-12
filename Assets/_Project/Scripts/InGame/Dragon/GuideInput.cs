using UnityEngine;
using UnityEngine.InputSystem;

namespace DWH.InGame.Dragon
{
    /// <summary>마우스 유도 입력 (8.5.1). 유도 목표가 없으면 드래곤은 자동 모드로 움직인다.</summary>
    public interface IGuideInput
    {
        bool TryGetGuideX(out float worldX);

        /// <summary>유도 중인 커서의 월드 위치 (조준점·궤적 표시용). 유도 중이 아니면 false</summary>
        bool TryGetCursor(out Vector2 world);
    }

    /// <summary>입력이 없는 자동 모드 전용. 테스트와 방치 플레이 검증에 쓴다.</summary>
    public class AutoOnlyInput : IGuideInput
    {
        public bool TryGetGuideX(out float worldX)
        {
            worldX = 0;
            return false;
        }

        public bool TryGetCursor(out Vector2 world)
        {
            world = default;
            return false;
        }
    }

    /// <summary>
    /// 커서의 x좌표가 목표 지점이다. 커서가 게임 화면 밖에 있거나 일정 시간 움직이지 않으면 자동 모드로 바뀐다.
    /// </summary>
    public class MouseGuideInput : IGuideInput
    {
        readonly Camera camera;
        readonly float idleSeconds;
        Vector2 lastPosition;
        float lastMoveTime = float.NegativeInfinity;

        public MouseGuideInput(Camera camera, float idleSeconds)
        {
            this.camera = camera;
            this.idleSeconds = idleSeconds;
        }

        int cachedFrame = -1;
        bool cachedValid;
        Vector2 cachedWorld;

        public bool TryGetCursor(out Vector2 world)
        {
            if (cachedFrame != Time.frameCount) TryGetGuideX(out _);
            world = cachedWorld;
            return cachedValid;
        }

        public bool TryGetGuideX(out float worldX)
        {
            cachedFrame = Time.frameCount;
            cachedValid = false;
            worldX = 0;
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused)
                return false;

            Vector2 pos = mouse.position.ReadValue();
            if ((pos - lastPosition).sqrMagnitude > 0.01f)
            {
                lastPosition = pos;
                lastMoveTime = Time.unscaledTime;
            }

            bool onScreen = pos.x >= 0 && pos.y >= 0 && pos.x <= Screen.width && pos.y <= Screen.height;
            if (!onScreen || Time.unscaledTime - lastMoveTime >= idleSeconds)
                return false;

            cachedWorld = camera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, -camera.transform.position.z));
            cachedValid = true;
            worldX = cachedWorld.x;
            return true;
        }
    }
}
