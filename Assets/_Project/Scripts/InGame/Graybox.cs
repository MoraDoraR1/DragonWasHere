using UnityEngine;

namespace DWH.InGame
{
    /// <summary>화면 정렬 순서. 배경 → 건물 → 드래곤(20 - 레이어) → 공중 유닛 → 드랍 → 탄</summary>
    public static class SortingOrders
    {
        public const int BackgroundFar = -100;
        public const int BackgroundMid = -90;
        public const int BackgroundGround = -80;
        public const int Building = 0;
        public const int AirUnit = 30;
        public const int Drop = 40;
        public const int Projectile = 50;
    }

    /// <summary>아트가 없는 요소의 그레이박스 색 (0.2.3, 9.6)</summary>
    public static class GrayboxColors
    {
        public static readonly Color Projectile = new(1f, 0.85f, 0.3f);
        public static readonly Color AirProjectile = new(0.75f, 0.9f, 1f);
    }

    /// <summary>그레이박스용 1×1 unit 흰 사각형 스프라이트</summary>
    public static class GrayboxSprites
    {
        static Sprite square;

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = Texture2D.whiteTexture;
                    square = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                    square.name = "GrayboxSquare";
                }
                return square;
            }
        }
    }
}
