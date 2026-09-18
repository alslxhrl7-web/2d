using System.Collections.Generic;
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
        /// <summary>
        /// [해설] ★ 성능 수정. 이 클래스의 모든 메서드는 호출될 때마다 Texture2D를 새로 만들고
        /// 픽셀을 하나씩 찍은 뒤 Apply()로 GPU에 올린다. 그런데 호출부 중에는 <b>발사할 때마다</b>
        /// 부르는 곳이 있었다 — Projectile.Init의 화살/포탄/원 스프라이트, ImpactEffect의 착탄
        /// 연출 2장, 배치 고스트가 0.35초마다 다시 굴리는 삼각형. 타워 10개가 깔린 중반이면
        /// 초당 20장 넘는 텍스처가 새로 생기고, 아무도 파괴하지 않으니 씬을 다시 불러올 때까지
        /// 계속 쌓였다(메모리 증가 + GC 끊김).
        ///
        /// 실제로는 같은 인자로 부르면 항상 같은 그림이 나오므로, 인자를 키로 한 번 만든 것을
        /// 재사용한다. 색/크기가 다르면 키가 달라져 각각 따로 캐시된다.
        ///
        /// 주의: 씬을 다시 불러오면 Unity가 참조되지 않는 에셋을 정리하면서 캐시해 둔 Sprite를
        /// 파괴할 수 있다. 그래서 꺼낼 때 반드시 null 검사를 한다 — 파괴된 UnityEngine.Object는
        /// == null이 true가 되므로, 이 검사만으로 "파괴됐으면 다시 만든다"가 성립한다.
        /// (그래서 GameBootstrapper.ResetStaticState에서 이 캐시를 따로 비울 필요가 없다.)
        /// </summary>
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Sprite Cached(string key, System.Func<Sprite> build)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var made = build();
            Cache[key] = made;
            return made;
        }

        private static string K(Color c) => $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}";

        private static Sprite MakeSprite(Texture2D tex, float pixelsPerUnit = 32f)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static Sprite BuildCircle(Color fill, Color outline, int size)
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

        private static Sprite BuildTriangle(Color fill, Color outline, int size)
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

        private static Sprite BuildSquare(Color fill, Color outline, int size)
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

        private static Sprite BuildCapsule(Color fill, Color outline, int w, int h)
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

        private static Sprite BuildDiamond(Color fill, Color outline, int size)
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

        /// <summary>
        /// [해설] 화살탑 투사체용 화살 모양. 텍스처 기준 +Y(위쪽)를 향해 뾰족한 화살촉이 있고,
        /// 아래쪽엔 화살대와 양옆으로 퍼지는 깃(fletching)이 달려 있다. Projectile이 매 프레임
        /// transform.up을 진행 방향으로 맞춰 회전시키므로, 이 스프라이트는 "위로 날아가는" 형태로
        /// 만들어 두면 어느 방향으로 날아가든 항상 촉이 진행 방향을 향하게 된다.
        /// </summary>
        private static Sprite BuildArrow(Color fill, Color outline, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

            float cx = w / 2f;
            int shaftW = Mathf.Max(2, w / 6);
            int shaftX0 = Mathf.RoundToInt(cx - shaftW / 2f);
            int shaftTop = Mathf.RoundToInt(h * 0.6f);
            int shaftBottom = Mathf.RoundToInt(h * 0.16f);

            // 화살대(shaft): 얇고 긴 세로 막대
            for (int y = shaftBottom; y < shaftTop; y++)
            for (int x = shaftX0; x < shaftX0 + shaftW; x++)
                tex.SetPixel(x, y, fill);

            // 화살촉(head): 진행 방향(+Y, 텍스처 위쪽)을 향한 뾰족한 삼각형
            Vector2 tip = new Vector2(cx, h - 2);
            Vector2 headL = new Vector2(cx - w * 0.42f, shaftTop - 1);
            Vector2 headR = new Vector2(cx + w * 0.42f, shaftTop - 1);
            for (int y = shaftTop - 2; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInTriangle(p, tip, headL, headR)) tex.SetPixel(x, y, fill);
            }

            // 깃(fletching): 화살대 맨 아래쪽 끝에서 양옆으로 퍼지는 작은 삼각형 두 개
            Vector2 fletchTip = new Vector2(cx, shaftBottom + h * 0.06f);
            Vector2 fletchMid = new Vector2(cx, 2);
            Vector2 fletchL = new Vector2(cx - w * 0.46f, 2);
            Vector2 fletchR = new Vector2(cx + w * 0.46f, 2);
            int fletchTopY = shaftBottom + Mathf.RoundToInt(h * 0.08f);
            for (int y = 0; y < fletchTopY; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInTriangle(p, fletchTip, fletchL, fletchMid) || PointInTriangle(p, fletchTip, fletchMid, fletchR))
                    tex.SetPixel(x, y, fill);
            }

            DrawOutlineFromAlpha(tex, outline);
            return MakeSprite(tex);
        }

        /// <summary>
        /// [해설] 포격탑 투사체용 검은색 포탄 모양. 기존엔 다른 타워와 마찬가지로 그냥 색칠한
        /// 원(주황색)을 썼는데, "주황색 도형 대신 검은색 포탄형태로" 요청에 따라 전용 스프라이트로
        /// 바꿨다. 그냥 단색 원과 구분되도록 좌상단에 옅은 하이라이트를 넣어 둥근 쇠구슬 같은
        /// 입체감을 주고, 배경(어두운 도로/필드)과 구분되도록 옅은 회색 테두리를 둘렀다.
        /// </summary>
        private static Sprite BuildCannonball(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color fill = new Color(0.07f, 0.07f, 0.08f);
            Color outline = new Color(0.38f, 0.38f, 0.42f);
            Color highlight = new Color(0.6f, 0.6f, 0.65f);

            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 2f;
            Vector2 hl = new Vector2(c.x - r * 0.32f, c.y + r * 0.32f);
            float hlR = r * 0.38f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, c);
                Color col = Color.clear;
                if (d <= r) col = fill;
                if (d > r - 2.2f && d <= r) col = outline;

                float hd = Vector2.Distance(p, hl);
                if (d <= r && hd <= hlR)
                {
                    float t = 1f - Mathf.Clamp01(hd / hlR);
                    col = Color.Lerp(col, highlight, t * 0.55f);
                }
                tex.SetPixel(x, y, col);
            }
            return MakeSprite(tex);
        }

        /// <summary>
        /// [해설] 이펙트 전용 "부드러운 광채" 원. Circle()은 경계가 또렷한 단색 원이라 이펙트에
        /// 그대로 쓰면 스티커를 붙인 것처럼 밋밋하고 촌스러워 보인다. 이 스프라이트는 중심에서
        /// 바깥으로 갈수록 알파(smoothstep 감쇠)가 자연스럽게 옅어지므로, 서리 장판/폭발 같은
        /// 이펙트의 "빛 번짐" 표현에 훨씬 깔끔하게 어울린다. size=64 기준 scale 1일 때 반지름이
        /// 1유닛이 되도록 맞춰서, 기존 Circle 기반 이펙트와 동일한 방식(반지름=scale)으로 쓸 수 있다.
        /// </summary>
        private static Sprite BuildSoftGlow(Color color, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float t = Mathf.Clamp01(d / r);
                float falloff = 1f - t * t * (3f - 2f * t); // smoothstep 역방향: 중심 1 → 가장자리 0
                Color col = color;
                col.a *= falloff;
                tex.SetPixel(x, y, col);
            }
            return MakeSprite(tex);
        }

        /// <summary>
        /// [해설] 경계가 부드럽게 페더링된 얇은 링. 기존 Ring()은 경계가 딱 잘려서 확산되는
        /// 충격파 같은 연출에 쓰면 계단처럼 딱딱해 보인다. 이건 링의 중심선을 기준으로
        /// smoothstep 감쇠를 줘서 안팎 경계가 부드럽게 사라지므로 폭발 충격파에 더 잘 어울린다.
        /// size=64 기준 scale 1일 때 반지름이 1유닛이 되도록 맞췄다(SoftGlow와 동일 규칙).
        /// </summary>
        private static Sprite BuildSoftRing(Color color, int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 2f;
            float half = thickness * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float distFromBand = Mathf.Abs(d - r);
                float t = Mathf.Clamp01(1f - distFromBand / half);
                float alpha = t * t * (3f - 2f * t); // smoothstep으로 부드럽게 페더링
                Color col = color;
                col.a *= alpha;
                tex.SetPixel(x, y, col);
            }
            return MakeSprite(tex);
        }

        private static Sprite BuildRing(Color color, int size, float thickness)
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

        private static Sprite BuildSolidSquare(Color color, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, color);
            return MakeSprite(tex, 8f);
        }

        // ---------- 캐시를 타는 공개 진입점 (실제 생성은 위의 Build* 가 한다) ----------
        public static Sprite Circle(Color fill, Color outline, int size = 64) =>
            Cached($"Circle|{K(fill)}|{K(outline)}|{size}", () => BuildCircle(fill, outline, size));

        public static Sprite Triangle(Color fill, Color outline, int size = 64) =>
            Cached($"Triangle|{K(fill)}|{K(outline)}|{size}", () => BuildTriangle(fill, outline, size));

        public static Sprite Square(Color fill, Color outline, int size = 64) =>
            Cached($"Square|{K(fill)}|{K(outline)}|{size}", () => BuildSquare(fill, outline, size));

        public static Sprite Capsule(Color fill, Color outline, int w = 48, int h = 64) =>
            Cached($"Capsule|{K(fill)}|{K(outline)}|{w}x{h}", () => BuildCapsule(fill, outline, w, h));

        public static Sprite Diamond(Color fill, Color outline, int size = 64) =>
            Cached($"Diamond|{K(fill)}|{K(outline)}|{size}", () => BuildDiamond(fill, outline, size));

        public static Sprite Arrow(Color fill, Color outline, int w = 28, int h = 64) =>
            Cached($"Arrow|{K(fill)}|{K(outline)}|{w}x{h}", () => BuildArrow(fill, outline, w, h));

        public static Sprite Cannonball(int size = 48) =>
            Cached($"Cannonball|{size}", () => BuildCannonball(size));

        public static Sprite SoftGlow(Color color, int size = 64) =>
            Cached($"SoftGlow|{K(color)}|{size}", () => BuildSoftGlow(color, size));

        public static Sprite SoftRing(Color color, int size = 64, float thickness = 6f) =>
            Cached($"SoftRing|{K(color)}|{size}|{thickness}", () => BuildSoftRing(color, size, thickness));

        public static Sprite Ring(Color color, int size = 96, float thickness = 3f) =>
            Cached($"Ring|{K(color)}|{size}|{thickness}", () => BuildRing(color, size, thickness));

        public static Sprite SolidSquare(Color color, int size = 8) =>
            Cached($"SolidSquare|{K(color)}|{size}", () => BuildSolidSquare(color, size));

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
