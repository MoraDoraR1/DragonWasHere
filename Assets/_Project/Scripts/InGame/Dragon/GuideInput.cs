using DWH.Data;
using DWH.InGame.Fx;
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

    /// <summary>마우스 유도 / 자동 유도를 바꿀 수 있는 입력 (Space·HUD 모드 버튼, 10-14)</summary>
    public interface IGuideModeInput
    {
        /// <summary>true = 마우스 유도, false = 자동 유도</summary>
        bool MouseMode { get; set; }
    }

    /// <summary>
    /// 마우스 유도 (10-14 변경): 커서의 x좌표가 목표 지점이다.
    /// - 마우스 유도 / 자동 유도는 Space 또는 HUD 왼쪽 아래 모드 버튼으로 바꾼다 (예전: 커서가 일정 시간 멈추면 자동으로 바뀜 → 삭제).
    /// - 커서가 근경(건물 HP바 아래 지면, 보조 스킬 버튼 줄)이나 화면 밖에 있으면 커서를 무시하고 마지막 유도 지점으로 계속 간다.
    ///   보조 스킬 버튼을 누르러 커서를 내려도 드래곤이 엉뚱한 쪽으로 가지 않게 하려는 것.
    /// - 아직 유도 지점이 없으면(판 시작 직후 커서가 지면에 있으면) 자동으로 움직인다.
    /// </summary>
    public class MouseGuideInput : IGuideInput, IGuideModeInput
    {
        readonly Camera camera;
        bool hasLast;
        float lastGuideX;

        public MouseGuideInput(Camera camera) => this.camera = camera;

        public bool MouseMode { get; set; } = true;

        /// <summary>이보다 아래(월드 y)의 커서는 무시한다: 건물 HP바 아래 끝</summary>
        public static float FloorY => Tuning.StandHeight - WorldHealthBar.BottomBelowStand;

        int cachedFrame = -1;
        bool cachedValid, cachedGuided;
        float cachedX;
        Vector2 cachedWorld;

        /// <summary>조준점·궤적을 그릴 커서 위치. 커서를 무시하는 중(근경·화면 밖)이면 false</summary>
        public bool TryGetCursor(out Vector2 world)
        {
            Refresh();
            world = cachedWorld;
            return cachedValid;
        }

        public bool TryGetGuideX(out float worldX)
        {
            Refresh();
            worldX = cachedX;
            return cachedGuided;
        }

        void Refresh()
        {
            if (cachedFrame == Time.frameCount) return;
            cachedFrame = Time.frameCount;
            cachedValid = false;
            cachedGuided = false;
            if (!MouseMode) return;

            var mouse = Mouse.current;
            if (mouse != null && Application.isFocused)
            {
                Vector2 pos = mouse.position.ReadValue();
                // 레터박스(검은 띠) 위의 커서는 화면 밖으로 본다
                if (camera.pixelRect.Contains(pos))
                {
                    var world = (Vector2)camera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, -camera.transform.position.z));
                    if (world.y >= FloorY)
                    {
                        cachedWorld = world;
                        cachedValid = true;
                        hasLast = true;
                        lastGuideX = world.x;
                    }
                }
            }
            cachedGuided = hasLast;
            cachedX = lastGuideX;
        }
    }
}
