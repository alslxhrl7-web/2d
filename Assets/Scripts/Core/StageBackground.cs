using System.Collections;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 스테이지 진행(스테이지 안의 로컬 웨이브)에 따라 화면 배경 아트를 바꿔주는 컴포넌트.
    /// [해설] 사용자 요청("1스테이지 10라운드 까지 숲배경에 20~24 오염된숲 배경 25는 보스전
    /// 느낌나게")을 그대로 옮긴 구성이다.
    ///   - 로컬 웨이브  1~10 : 평범한 숲            (Sprites/BG_Forest)
    ///   - 로컬 웨이브 11~19 : 병들기 시작한 숲    (Sprites/BG_WitheringForest)
    ///                         숲과 오염된 숲의 중간 단계. 처음에는 숲 그림에 틴트만 입혀
    ///                         표현했지만, 이후 이 구간 전용 아트를 따로 만들어 교체했다
    ///                         (그 아트가 없으면 예전 틴트 방식으로 자동 폴백).
    ///   - 로컬 웨이브 20~24 : 오염된 숲            (Sprites/BG_CorruptedForest)
    ///   - 로컬 웨이브   25  : 보스전 배경          (Sprites/BG_Boss)
    /// 이 규칙은 스테이지 번호와 무관하게 3개 스테이지 모두에 동일하게 적용된다
    /// (각 스테이지가 "숲 → 오염 → 보스전"이라는 같은 호흡을 반복하는 구조).
    ///
    /// 아트 파일(Assets/Resources/Sprites/BG_*.png)이 아직 없으면 이 컴포넌트는 아무것도 그리지
    /// 않고 조용히 스스로 비활성화되며, 기존처럼 카메라의 단색 배경이 그대로 보인다
    /// (다른 아트들과 동일한 "있으면 쓰고 없으면 폴백" 방식 — BuildManager.ApplyTowerVisual 참고).
    /// </summary>
    public class StageBackground : MonoBehaviour
    {
        // 배경은 도로 타일(-5)보다 훨씬 뒤에 깔려야 하므로 아주 낮은 정렬 순서를 쓴다.
        private const int BaseSortingOrder = -20;
        private const int OverlaySortingOrder = -19;

        /// <summary>배경이 바뀔 때 교차 페이드에 걸리는 시간(초). 웨이브 시작과 함께 바뀌므로
        /// 너무 빠르면 툭 끊겨 보이고, 너무 느리면 전투 중에 계속 어른거린다.</summary>
        private const float FadeSeconds = 0.9f;

        // 오염이 시작되는 웨이브와, 완전히 오염된 숲이 되는 웨이브.
        // (마지막 보스전 웨이브는 GameConstants.WavesPerStage를 그대로 쓴다.)
        private const int CorruptionStartWave = 11;
        private const int FullyCorruptedWave = 20;

        // 배경이 너무 밝으면 적/타워/발사체가 묻히므로 전체적으로 한 단계 눌러서 깐다.
        private static readonly Color NormalTint = new Color(0.62f, 0.62f, 0.62f, 1f);
        // 11~19웨이브에서 숲이 서서히 병들어 보이도록 섞어 넣는 색(누렇게 뜬 초록).
        private static readonly Color CorruptingTint = new Color(0.50f, 0.55f, 0.40f, 1f);
        // 보스전은 배경을 살짝 붉게 띄워서 분위기를 확 바꾼다.
        private static readonly Color BossTint = new Color(0.74f, 0.58f, 0.58f, 1f);

        private SpriteRenderer _baseLayer;    // 현재 보이는 배경
        private SpriteRenderer _overlayLayer; // 교차 페이드로 위에 얹혀 들어오는 새 배경
        private Sprite _forest, _withering, _corrupted, _boss;
        private Coroutine _transition;
        private float _lastAspect;

        private void Awake()
        {
            _forest = Resources.Load<Sprite>("Sprites/BG_Forest");
            _withering = Resources.Load<Sprite>("Sprites/BG_WitheringForest");
            _corrupted = Resources.Load<Sprite>("Sprites/BG_CorruptedForest");
            _boss = Resources.Load<Sprite>("Sprites/BG_Boss");

            // 배경 아트가 하나도 없으면 할 일이 없다 (카메라 단색 배경을 그대로 둔다).
            if (_forest == null && _withering == null && _corrupted == null && _boss == null)
            {
                enabled = false;
                return;
            }

            var cam = Camera.main;
            transform.position = cam != null
                ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f)
                : Vector3.zero;

            _baseLayer = CreateLayer("BackgroundBase", BaseSortingOrder);
            _overlayLayer = CreateLayer("BackgroundOverlay", OverlaySortingOrder);
            SetAlpha(_overlayLayer, 0f);
            if (cam != null) _lastAspect = cam.aspect;

            // 첫 웨이브가 시작되기 전(준비 단계)부터 숲 배경이 보이도록 즉시 한 번 적용한다.
            ApplyImmediate(FirstAvailable(_forest, _corrupted, _boss), NormalTint);
        }

        private void Update()
        {
            // 창 크기가 바뀌어 화면 비율이 달라지면 배경이 화면을 못 덮을 수 있으므로 다시 맞춘다.
            var cam = Camera.main;
            if (cam == null || _baseLayer == null) return;
            if (Mathf.Approximately(cam.aspect, _lastAspect)) return;

            _lastAspect = cam.aspect;
            FitToCamera(_baseLayer);
            FitToCamera(_overlayLayer);
        }

        /// <summary>WaveManager.OnWaveStarted(스테이지 번호, 로컬 웨이브)에 연결되는 진입점.</summary>
        public void OnWaveStarted(int stageNumber, int localWave)
        {
            if (!enabled || _baseLayer == null) return;
            ResolveLook(localWave, out Sprite target, out Color tint);
            TransitionTo(target, tint);
        }

        /// <summary>로컬 웨이브 번호로부터 "어떤 배경을 어떤 색으로 보여줄지"를 결정한다.</summary>
        private void ResolveLook(int localWave, out Sprite sprite, out Color tint)
        {
            if (localWave >= GameConstants.WavesPerStage) // 25웨이브: 보스전
            {
                sprite = FirstAvailable(_boss, _corrupted, _forest);
                tint = BossTint;
            }
            else if (localWave >= FullyCorruptedWave)     // 20~24웨이브: 오염된 숲
            {
                sprite = FirstAvailable(_corrupted, _forest, _boss);
                tint = NormalTint;
            }
            else if (localWave >= CorruptionStartWave)    // 11~19웨이브: 병들기 시작한 숲
            {
                // [해설] 처음에는 이 구간을 "숲 그림 + 누렇게 뜬 틴트"로만 표현했지만, 이후 이 구간
                // 전용 아트(BG_WitheringForest)를 따로 뽑아 실제 그림으로 교체했다.
                // 전용 아트가 있으면 그 그림을 쓰되, 그림 자체가 이미 "절반쯤 병든" 상태이므로 틴트는
                // 절반 세기로만 겹쳐서 구간 안에서도 서서히 더 나빠지는 느낌만 남긴다.
                // 아트가 없으면 예전처럼 숲 그림에 틴트를 풀세기로 입히는 방식으로 되돌아간다.
                float t = Mathf.InverseLerp(CorruptionStartWave - 1, FullyCorruptedWave - 1, localWave);
                if (_withering != null)
                {
                    sprite = _withering;
                    tint = Color.Lerp(NormalTint, CorruptingTint, t * 0.5f);
                }
                else
                {
                    sprite = FirstAvailable(_forest, _corrupted, _boss);
                    tint = Color.Lerp(NormalTint, CorruptingTint, t);
                }
            }
            else                                          // 1~10웨이브: 평범한 숲
            {
                sprite = FirstAvailable(_forest, _corrupted, _boss);
                tint = NormalTint;
            }
        }

        private void TransitionTo(Sprite target, Color tint)
        {
            if (target == null) return;

            // 이전 전환이 아직 진행 중이면 그 결과를 먼저 확정(flatten)시키고 새 전환을 시작한다.
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
                FlattenOverlay();
            }

            // 배경 그림은 그대로이고 색(오염도)만 달라지는 경우(11~19웨이브)는 색만 서서히 바꾼다.
            _transition = _baseLayer.sprite == target
                ? StartCoroutine(FadeTint(tint))
                : StartCoroutine(CrossFade(target, tint));
        }

        private IEnumerator CrossFade(Sprite target, Color tint)
        {
            _overlayLayer.sprite = target;
            _overlayLayer.color = new Color(tint.r, tint.g, tint.b, 0f);
            FitToCamera(_overlayLayer);

            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                SetAlpha(_overlayLayer, Mathf.Clamp01(t / FadeSeconds));
                yield return null;
            }

            // 다 덮였으면 새 배경을 바닥 레이어로 확정하고 오버레이는 다시 투명하게 비워둔다.
            _baseLayer.sprite = target;
            _baseLayer.color = tint;
            FitToCamera(_baseLayer);
            SetAlpha(_overlayLayer, 0f);
            _transition = null;
        }

        private IEnumerator FadeTint(Color tint)
        {
            Color from = _baseLayer.color;
            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                _baseLayer.color = Color.Lerp(from, tint, Mathf.Clamp01(t / FadeSeconds));
                yield return null;
            }
            _baseLayer.color = tint;
            _transition = null;
        }

        /// <summary>진행 중이던 교차 페이드를 "이미 끝난 것"으로 간주하고 상태를 정리한다.
        /// 오버레이가 절반 이상 덮였으면 그 배경이 이긴 것으로 보고 바닥 레이어로 옮긴다.</summary>
        private void FlattenOverlay()
        {
            if (_overlayLayer.sprite != null && _overlayLayer.color.a > 0.5f)
            {
                _baseLayer.sprite = _overlayLayer.sprite;
                Color c = _overlayLayer.color;
                c.a = 1f;
                _baseLayer.color = c;
                FitToCamera(_baseLayer);
            }
            SetAlpha(_overlayLayer, 0f);
        }

        private void ApplyImmediate(Sprite sprite, Color tint)
        {
            _baseLayer.sprite = sprite;
            _baseLayer.color = tint;
            FitToCamera(_baseLayer);
            SetAlpha(_overlayLayer, 0f);
        }

        private SpriteRenderer CreateLayer(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>배경 스프라이트가 화면을 빈틈없이 덮도록 크기를 맞춘다(가로/세로 중 더 큰 배율을
        /// 써서 잘리더라도 여백이 생기지 않게 하는 방식 = "cover"). 스프라이트의 PPU 설정과
        /// 무관하게 항상 올바르게 동작하도록 실제 월드 크기(bounds)를 기준으로 계산한다.</summary>
        private void FitToCamera(SpriteRenderer sr)
        {
            var cam = Camera.main;
            if (cam == null || sr == null || sr.sprite == null) return;

            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;
            Vector2 size = sr.sprite.bounds.size;
            if (size.x <= 0.0001f || size.y <= 0.0001f) return;

            float scale = Mathf.Max(worldWidth / size.x, worldHeight / size.y);
            sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static Sprite FirstAvailable(Sprite a, Sprite b, Sprite c) => a != null ? a : (b != null ? b : c);

        private static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
