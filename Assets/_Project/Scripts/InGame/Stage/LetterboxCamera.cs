using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Stage
{
    /// <summary>
    /// 화면 고정 (레터박스). 창 비율과 관계없이 게임 화면을 항상 16:9(Tuning.CameraMinAspect)로 그린다.
    /// 창이 더 넓으면 좌우, 더 높으면 위아래에 검은 띠가 생기고, 배경·건물의 크기와 보이는 범위는 바뀌지 않는다.
    /// 띠 부분은 뒤에 둔 검은 카메라가 칠한다(뷰포트 밖은 지워지지 않아 잔상이 남기 때문).
    /// 렌더 텍스처로 찍을 때(테스트 캡처)는 건드리지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LetterboxCamera : MonoBehaviour
    {
        Camera view;
        Camera backdrop;
        int lastWidth, lastHeight;

        void Awake()
        {
            view = GetComponent<Camera>();
            var go = new GameObject("LetterboxBackdrop");
            go.transform.SetParent(transform, false);
            backdrop = go.AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Color.black;
            backdrop.cullingMask = 0;
            backdrop.orthographic = true;
            backdrop.depth = view.depth - 1;
            Apply();
        }

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) Apply();
        }

        void Apply()
        {
            if (view.targetTexture != null) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            view.rect = ViewportFor(lastHeight > 0 ? (float)lastWidth / lastHeight : Tuning.CameraMinAspect, Tuning.CameraMinAspect);
        }

        /// <summary>창 비율(screenAspect) 안에 target 비율 화면을 가운데 맞춘 뷰포트 (0~1)</summary>
        public static Rect ViewportFor(float screenAspect, float target) => screenAspect > target
            ? new Rect((1f - target / screenAspect) / 2f, 0f, target / screenAspect, 1f)    // 넓은 창: 좌우 띠
            : new Rect(0f, (1f - screenAspect / target) / 2f, 1f, screenAspect / target);   // 높은 창: 위아래 띠

        void OnDestroy()
        {
            if (view != null) view.rect = new Rect(0, 0, 1, 1);
            if (backdrop != null) Destroy(backdrop.gameObject);
        }
    }
}
