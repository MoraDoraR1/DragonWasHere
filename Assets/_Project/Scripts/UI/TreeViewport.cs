using UnityEngine;
using UnityEngine.EventSystems;

namespace DWH.UI
{
    /// <summary>
    /// 업그레이드 트리 보기 창: 드래그로 이동, 휠·버튼으로 확대·축소, 전체 보기.
    /// view(마스크가 걸린 창)에 붙이고, content(노드를 담은 판)의 위치와 배율만 바꾼다.
    /// </summary>
    public class TreeViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        const float MinZoom = 0.3f;
        const float MaxZoom = 1.6f;
        const float WheelStep = 1.12f;
        const float FitPadding = 60f;

        RectTransform view;
        RectTransform content;
        Rect bounds;

        public float Zoom => content != null ? content.localScale.x : 1f;

        /// <param name="contentBounds">content 좌표계에서 노드가 차지하는 영역 (전체 보기 기준)</param>
        public void Init(RectTransform viewRect, RectTransform contentRect, Rect contentBounds)
        {
            view = viewRect;
            content = contentRect;
            bounds = contentBounds;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            // 화면 픽셀 이동량을 캔버스 단위로 바꾼다
            float canvasScale = view.lossyScale.x > 0 ? view.lossyScale.x : 1f;
            content.anchoredPosition += eventData.delta / canvasScale;
            Clamp();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Approximately(eventData.scrollDelta.y, 0)) return;
            ZoomBy(eventData.scrollDelta.y > 0 ? WheelStep : 1f / WheelStep);
        }

        /// <summary>보기 창 중심을 기준으로 확대·축소한다</summary>
        public void ZoomBy(float factor)
        {
            float before = Zoom;
            float after = Mathf.Clamp(before * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(before, after)) return;
            content.localScale = new Vector3(after, after, 1);
            content.anchoredPosition *= after / before;
            Clamp();
        }

        /// <summary>모든 노드가 보이게 배율과 위치를 맞춘다</summary>
        public void Fit()
        {
            var size = view.rect.size - new Vector2(FitPadding, FitPadding) * 2;
            float zoom = Mathf.Clamp(Mathf.Min(size.x / bounds.width, size.y / bounds.height), MinZoom, MaxZoom);
            content.localScale = new Vector3(zoom, zoom, 1);
            content.anchoredPosition = -bounds.center * zoom;
        }

        /// <summary>content 좌표의 한 점을 보기 창 가운데에 두고 배율을 맞춘다</summary>
        public void Focus(Vector2 point, float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            content.localScale = new Vector3(zoom, zoom, 1);
            content.anchoredPosition = -point * zoom;
            Clamp();
        }

        /// <summary>노드 영역이 보기 창 밖으로 완전히 나가지 않게 막는다</summary>
        void Clamp()
        {
            float zoom = Zoom;
            var half = view.rect.size / 2;
            var p = content.anchoredPosition;
            float minX = -half.x - bounds.xMax * zoom + FitPadding, maxX = half.x - bounds.xMin * zoom - FitPadding;
            float minY = -half.y - bounds.yMax * zoom + FitPadding, maxY = half.y - bounds.yMin * zoom - FitPadding;
            content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minY, maxY));
        }
    }
}
