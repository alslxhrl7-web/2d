using System.Collections;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// Projectile.Init()의 impactEffect 인자로 고르는 명중 연출 종류.
    /// FrostZone: 빙결탑이 착탄 지점에 남기는 서리 장판(애니비아 장판 모션 참고).
    /// Explosion: 포격탑이 맞으면 터지는 순간 폭발 이펙트.
    /// (번개탑의 번개 줄기는 투사체를 쓰지 않으므로 이 enum이 아니라
    ///  ImpactEffect.SpawnLightningBolt를 LightningTower가 직접 호출한다.)
    /// </summary>
    public enum ImpactEffectKind { None, FrostZone, Explosion }

    /// <summary>
    /// 타워 공격이 명중했을 때 잠깐 나타났다 사라지는 시각 이펙트를 만든다.
    /// 데미지/슬로우 판정(Projectile.Hit)과는 별개의 순수 연출용 오브젝트이며,
    /// 스스로 애니메이션을 마치면 자기 자신을 파괴한다.
    /// [해설] 이전 버전은 단색으로 꽉 채운 원(SpriteFactory.Circle)을 그냥 키웠다 줄이는
    /// 방식이라 스티커를 붙인 것처럼 밋밋하고 촌스러워 보였다("이펙트가 촌스럽다" 피드백).
    /// 지금은 중심에서 바깥으로 갈수록 알파가 부드럽게 옅어지는 광채(SoftGlow)와, 경계가
    /// 페더링된 얇은 링(SoftRing)을 겹쳐서 훨씬 자연스러운 이펙트를 만든다.
    /// </summary>
    public static class ImpactEffect
    {
        /// <summary>
        /// [해설] 빙결탑 명중 지점에 서리 장판을 깐다. 부드러운 서리 광채(Glow) 위에 살짝 더
        /// 밝고 또렷한 테두리 링(Rim)을 겹쳐서, 애니비아 장판처럼 순간적으로 확 퍼졌다가
        /// 슬로우가 지속되는 동안 바닥에 서서히 옅어지며 사라진다. sortingOrder를 도로(-5)보다는
        /// 위, 적(5)/타워(4)보다는 아래로 둬서 "바닥 장판" 느낌을 낸다.
        /// </summary>
        public static void SpawnFrostZone(Vector3 pos, float radius, float duration)
        {
            var go = new GameObject("FrostZone");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.zero;

            var glow = NewChild(go.transform, "Glow", -1);
            glow.sprite = SpriteFactory.SoftGlow(new Color(0.55f, 0.88f, 1f, 0.5f));

            var rim = NewChild(go.transform, "Rim", -1);
            rim.sprite = SpriteFactory.SoftRing(new Color(0.85f, 0.98f, 1f, 0.75f), 64, 7f);

            // 두 스프라이트 모두 size=64 기준(scale 1 = 반지름 1유닛)이라, 원하는 반경만큼
            // 부모(go)를 그대로 스케일해도 둘 다 같은 비율로 함께 커진다.
            EffectRunner.Run(go, GrowThenFade(go.transform, new[] { glow, rim }, radius, duration));
        }

        /// <summary>
        /// [해설] 포격탑 명중 지점에서 터지는 폭발 이펙트. 단일 색 원 하나 대신, 짧게 번쩍이는
        /// 밝은 섬광(Flash)과 바깥으로 퍼져나가는 얇은 충격파 링(Shockwave) 두 겹으로 나눠서
        /// 훨씬 입체적이고 깔끔하게 보이게 했다. sortingOrder를 투사체(8)/적(5)보다 위로 둬서
        /// 터지는 순간 화면에서 확실히 눈에 띄게 한다.
        /// </summary>
        public static void SpawnExplosion(Vector3 pos, float radius)
        {
            var go = new GameObject("Explosion");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var flash = NewChild(go.transform, "Flash", 9);
            flash.sprite = SpriteFactory.SoftGlow(new Color(1f, 0.9f, 0.65f, 0.95f));
            flash.transform.localScale = Vector3.zero;

            var ring = NewChild(go.transform, "Shockwave", 9);
            ring.sprite = SpriteFactory.SoftRing(new Color(1f, 0.6f, 0.28f, 0.85f), 64, 7f);
            ring.transform.localScale = Vector3.zero;

            EffectRunner.Run(go, ExplosionBurst(go.transform, flash, ring, radius));
        }

        /// <summary>번개탑의 번개 줄기를 from에서 to까지 지그재그로 그린다(순수 연출).
        ///
        /// [해설] LineRenderer를 쓰지 않았다. LineRenderer는 머티리얼이 필요하고 URP에서
        /// Shader.Find로 셰이더를 찾아야 하는데, 빌드에 그 셰이더가 포함되지 않으면 런타임에만
        /// 분홍색으로 깨지는 종류의 사고가 난다. 대신 이미 이 프로젝트 전체에서 검증된
        /// SpriteRenderer + SpriteFactory.SolidSquare 조합으로, 얇고 긴 사각형 여러 개를 이어
        /// 붙여 번개를 만든다. 셰이더를 찾을 일도, 머티리얼을 만들 일도 없다.
        ///
        /// 스프라이트는 한 번 만들어 static으로 캐시한다 — SpriteFactory는 호출할 때마다
        /// Texture2D를 새로 만들기 때문에, 발사마다 새로 만들면 GC가 계속 쌓인다.
        /// </summary>
        public static void SpawnLightningBolt(Vector3 from, Vector3 to)
        {
            Vector3 a = new Vector3(from.x, from.y, 0f);
            Vector3 b = new Vector3(to.x, to.y, 0f);
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.0001f) return; // 같은 지점이면 그릴 것이 없다(0으로 나누는 것도 막는다)

            var go = new GameObject("LightningBolt");
            go.transform.position = Vector3.zero; // 자식들을 월드 좌표로 그대로 배치하기 위해 원점에 둔다

            // 선분에 수직인 단위 벡터. 지그재그의 좌우 흔들림 방향이 된다.
            Vector3 perp = new Vector3(-delta.y, delta.x, 0f) / length;
            float jitter = Mathf.Min(0.22f, length * 0.16f); // 짧은 연쇄에서 과하게 튀지 않도록 상한

            var points = new Vector3[BoltSegments + 1];
            points[0] = a;
            points[BoltSegments] = b;
            for (int i = 1; i < BoltSegments; i++)
            {
                points[i] = Vector3.Lerp(a, b, i / (float)BoltSegments)
                            + perp * Random.Range(-jitter, jitter);
            }

            var segments = new SpriteRenderer[BoltSegments];
            for (int i = 0; i < BoltSegments; i++)
                segments[i] = MakeBoltSegment(go.transform, points[i], points[i + 1]);

            EffectRunner.Run(go, FadeOutBolt(go, segments));
        }

        private const int BoltSegments = 6;        // 지그재그 마디 수
        private const float BoltThickness = 0.07f; // 번개 굵기(월드 유닛)
        private const float BoltLifetime = 0.16f;  // 번쩍하고 사라지는 시간(초)

        private static Sprite _boltSprite;
        private static Sprite BoltSprite =>
            _boltSprite != null ? _boltSprite : (_boltSprite = SpriteFactory.SolidSquare(Color.white));

        /// <summary>두 점을 잇는 얇은 사각형 하나를 만든다. 길이만큼 늘이고 방향만큼 회전시킨다.</summary>
        private static SpriteRenderer MakeBoltSegment(Transform parent, Vector3 p0, Vector3 p1)
        {
            var sr = NewChild(parent, "BoltSegment", 9); // 투사체(8)보다 위, 폭발(9)과 같은 층
            sr.sprite = BoltSprite;
            sr.color = new Color(1f, 0.95f, 0.55f, 0.95f);

            Vector3 d = p1 - p0;
            float len = d.magnitude;
            float unit = BoltSprite.bounds.size.x; // SolidSquare는 PPU 8/크기 8이라 1유닛이지만, 바뀌어도 안전하도록 나눠 준다

            sr.transform.position = (p0 + p1) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(len / unit, BoltThickness / unit, 1f);
            return sr;
        }

        private static IEnumerator FadeOutBolt(GameObject go, SpriteRenderer[] segments)
        {
            var baseColors = new Color[segments.Length];
            for (int i = 0; i < segments.Length; i++) baseColors[i] = segments[i].color;

            float elapsed = 0f;
            while (elapsed < BoltLifetime)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / BoltLifetime);
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == null) continue; // 씬 전환 등으로 먼저 파괴됐을 수 있다
                    var c = baseColors[i];
                    segments[i].color = new Color(c.r, c.g, c.b, c.a * (1f - p));
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        private static SpriteRenderer NewChild(Transform parent, string name, int sortingOrder)
        {
            var childGo = new GameObject(name);
            childGo.transform.SetParent(parent, false);
            var sr = childGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private static IEnumerator GrowThenFade(Transform t, SpriteRenderer[] srs, float targetScale, float duration)
        {
            const float growTime = 0.15f;
            var baseColors = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++) baseColors[i] = srs[i].color;

            float elapsed = 0f;
            while (elapsed < growTime)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.one * (targetScale * EaseOutQuad(Mathf.Clamp01(elapsed / growTime)));
                yield return null;
            }
            t.localScale = Vector3.one * targetScale;

            float fadeTime = Mathf.Max(0.1f, duration - growTime);
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / fadeTime);
                for (int i = 0; i < srs.Length; i++)
                {
                    var c0 = baseColors[i];
                    srs[i].color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - p));
                }
                yield return null;
            }
            Object.Destroy(t.gameObject);
        }

        private static IEnumerator ExplosionBurst(Transform root, SpriteRenderer flash, SpriteRenderer ring, float radius)
        {
            const float flashTime = 0.12f; // 섬광은 아주 짧게 번쩍이고 사라진다
            const float ringTime = 0.32f;  // 충격파 링은 조금 더 오래, 더 넓게 퍼진다
            Color flashC0 = flash.color;
            Color ringC0 = ring.color;

            float elapsed = 0f;
            while (elapsed < ringTime)
            {
                elapsed += Time.deltaTime;

                float ft = Mathf.Clamp01(elapsed / flashTime);
                flash.transform.localScale = Vector3.one * (radius * 0.9f * EaseOutQuad(ft));
                flash.color = new Color(flashC0.r, flashC0.g, flashC0.b, flashC0.a * (1f - ft));

                float rt = Mathf.Clamp01(elapsed / ringTime);
                ring.transform.localScale = Vector3.one * (radius * 1.7f * EaseOutQuad(rt));
                ring.color = new Color(ringC0.r, ringC0.g, ringC0.b, ringC0.a * (1f - rt));

                yield return null;
            }
            Object.Destroy(root.gameObject);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>정적 클래스(ImpactEffect)는 StartCoroutine을 직접 호출할 수 없으므로,
        /// 이펙트 오브젝트 자신에게 이 작은 러너 컴포넌트를 붙여 코루틴을 돌린다.</summary>
        private class EffectRunner : MonoBehaviour
        {
            public static void Run(GameObject host, IEnumerator routine)
            {
                var runner = host.AddComponent<EffectRunner>();
                runner.StartCoroutine(routine);
            }
        }
    }
}
