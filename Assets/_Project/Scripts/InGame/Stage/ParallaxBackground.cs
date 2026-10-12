using UnityEngine;

namespace DWH.InGame.Stage
{
    /// <summary>
    /// 배경 패럴랙스 (4.1.2, 9.3).
    /// far(원경)는 화면에 고정하고 반복하지 않는다. mid(중경)는 근경의 일정 비율로, ground(근경)는 카메라와 1:1로 스크롤하며 3장을 이어 붙여 순환한다.
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        Camera view;
        Transform far;
        Transform[] mid, ground;
        float midWidth, groundWidth, midFactor;
        Sprite midSprite, groundSprite;

        public Sprite FarSprite { get; private set; }

        public void Init(Camera camera, Sprite farSprite, Sprite midSprite, Sprite groundSprite, float midScrollFactor)
        {
            view = camera;
            midFactor = midScrollFactor;
            FarSprite = farSprite;
            far = Create("Far", farSprite, SortingOrders.BackgroundFar).transform;
            this.midSprite = midSprite;
            this.groundSprite = groundSprite;
            mid = CreateTiles("Mid", midSprite, SortingOrders.BackgroundMid, out midWidth);
            ground = CreateTiles("Ground", groundSprite, SortingOrders.BackgroundGround, out groundWidth);
            LateUpdate();
        }

        void LateUpdate()
        {
            if (view == null) return;
            float camX = view.transform.position.x;
            float bottom = view.transform.position.y - view.orthographicSize;

            far.position = new Vector3(camX, bottom, 0);
            far.localScale = Vector3.one * CoverScale(FarSprite, view);
            // 스타터 줌아웃처럼 화면이 넓어지면 타일을 늘린다
            float viewWidth = view.orthographicSize * 2f * view.aspect;
            mid = EnsureTiles(mid, "Mid", midSprite, SortingOrders.BackgroundMid, midWidth, viewWidth);
            ground = EnsureTiles(ground, "Ground", groundSprite, SortingOrders.BackgroundGround, groundWidth, viewWidth);
            // 중경은 화면 기준으로 근경 속도의 midFactor만큼 움직인다 → 월드에서는 카메라를 (1 - midFactor)만큼 따라간다
            Place(mid, camX, camX * (1f - midFactor), midWidth, bottom);
            Place(ground, camX, 0f, groundWidth, bottom);
        }

        /// <summary>
        /// 원경은 반복하지 않으므로 화면을 항상 덮도록 키운다. 16:9보다 넓은 화면이나 픽셀 경계에서 가장자리가 비지 않게 한다.
        /// 바닥 가운데 피벗이라 위쪽과 양옆으로만 커진다.
        /// </summary>
        static float CoverScale(Sprite sprite, Camera camera)
        {
            const float pixelBleed = 1.01f;
            float viewHeight = camera.orthographicSize * 2f;
            float viewWidth = viewHeight * camera.aspect;
            var size = sprite.bounds.size;
            return Mathf.Max(1f, viewWidth / size.x, viewHeight / size.y) * pixelBleed;
        }

        /// <summary>타일은 홀수 개로 가운데 타일을 중심에 두고 양옆으로 이어 붙인다</summary>
        static void Place(Transform[] tiles, float camX, float layerOffset, float width, float bottom)
        {
            float baseX = layerOffset + Mathf.Floor((camX - layerOffset) / width + 0.5f) * width;
            int half = tiles.Length / 2;
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].position = new Vector3(baseX + (i - half) * width, bottom, 0);
        }

        Transform[] EnsureTiles(Transform[] tiles, string name, Sprite sprite, int order, float width, float viewWidth)
        {
            int needed = Mathf.CeilToInt(viewWidth / width) + 2;
            if (needed % 2 == 0) needed++;
            if (tiles.Length >= needed) return tiles;
            var list = new System.Collections.Generic.List<Transform>(tiles);
            while (list.Count < needed) list.Add(Create($"{name}{list.Count}", sprite, order).transform);
            return list.ToArray();
        }

        Transform[] CreateTiles(string name, Sprite sprite, int order, out float width)
        {
            width = sprite.bounds.size.x;
            var tiles = new Transform[3];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = Create($"{name}{i}", sprite, order).transform;
            return tiles;
        }

        GameObject Create(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go;
        }
    }
}
