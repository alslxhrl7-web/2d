using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 아트 리소스 없이 실루엣만으로 역할을 구분할 수 있는 단색 도형 스프라이트를
    /// 런타임에 생성한다. (기획서: "실루엣만 봐도 역할 구분", "2~3색 제한 팔레트")
    /// 시스템 검증이 목적이므로 실제 캐릭터 아트는 포함하지 않는다.
    /// </summary>
    public static class SpriteFactory
    {
        private static Sprite MakeSprite(Texture2D tex, float pixelsPerUnit = 32f)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        public static Sprite Circle(Color fill, Color outline, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                Color col = Color.clear;
                if (d <= r) col = fill;
                if (d > r - 3f && d <= r) col = outline;
                tex.SetPixel(x, y, col);
            }
            return MakeSprite(tex);
        }

        public static Sprite Triangle(Color fill, Color outline, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 p0 = new Vector2(size / 2f, size - 4);
            Vector2 p1 = new Vector2(4, 6);
            Vector2 p2 = new Vector2(size - 4, 6);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = PointInTriangle(p, p0, p1, p2);
                tex.SetPixel(x, y, inside ? fill : Color.clear);
            }
            DrawOutlineFromAlpha(tex, outline);
            return MakeSprite(tex);
        }

        public static Sprite Square(Color fill, Color outline, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                tex.SetPixel(x, y, edge ? outline : fill);
            }
            return MakeSprite(tex);
        }

        public static Sprite Capsule(Color fill, Color outline, int w = 48, int h = 64)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float r = w / 2f - 1f;
            Vector2 topC = new Vector2(w / 2f, h - r - 1);
            Vector2 botC = new Vector2(w / 2f, r + 1);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside;
                if (p.y > topC.y) inside = Vector2.Distance(p, topC) <= r;
                else if (p.y < botC.y) inside = Vector2.Distance(p, botC) <= r;
                else inside = Mathf.Abs(p.x - w / 2f) <= r;
                tex.SetPixel(x, y, inside ? fill : Color.clear);
            }
            DrawOutlineFromAlpha(tex, outline);
            return MakeSprite(tex);
        }

        public static Sprite Diamond(Color fill, Color outline, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y) <= r;
                tex.SetPixel(x, y, inside ? fill : Color.clear);
            }
            DrawOutlineFromAlpha(tex, outline);
            return MakeSprite(tex);
        }

        public static Sprite Ring(Color color, int size = 96, float thickness = 3f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                Color col = (d <= r && d >= r - thickness) ? color : Color.clear;
                tex.SetPixel(x, y, col);
            }
            return MakeSprite(tex);
        }

        public static Sprite SolidSquare(Color color, int size = 8)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, color);
            return MakeSprite(tex, 8f);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p - a, b - a);
            float d2 = Cross(p - b, c - b);
            float d3 = Cross(p - c, a - c);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static void DrawOutlineFromAlpha(Texture2D tex, Color outline)
        {
            int w = tex.width, h = tex.height;
            var src = tex.GetPixels();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (src[i].a <= 0f) continue;
                bool edge = false;
                for (int dy = -1; dy <= 1 && !edge; dy++)
                for (int dx = -1; dx <= 1 && !edge; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) { edge = true; break; }
                    if (src[ny * w + nx].a <= 0f) edge = true;
                }
                if (edge) tex.SetPixel(x, y, outline);
            }
        }
    }
}
