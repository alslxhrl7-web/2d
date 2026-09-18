using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 적 공통 로직: 경로(무한 루프)를 따라 이동, 체력/피격, 사망 보상.
    /// 잡몹(원)/돌진형(삼각)/방패병(사각)은 실루엣과 스탯만 다르다.
    /// 방패병은 "타워 공격을 버팀" 설정에 따라 타워 피해만 경감받는다.
    /// [해설] 거점(기지) 개념이 제거되면서 경로는 도착점 없이 영원히 순회하는 폐곡선이 되었다.
    /// 적은 오직 타워에게 처치될 때만 사라진다.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        public static readonly List<EnemyController> Active = new List<EnemyController>();

        public EnemyType Type;
        public float MaxHP;
        public float HP;
        public float Speed;
        /// <summary>[해설] 처치 보상은 이제 1골드보다 작을 수 있어서 float이다. 한 웨이브에 적이
        /// 50마리씩 나오는데 정수로만 주면 "최소 1골드 × 450마리 = 450골드"라는 바닥에 걸려서
        /// 경제를 원하는 만큼 조일 수가 없었다. 소수점 보상은 GameManager.AddGold가 모아뒀다가
        /// 1을 넘길 때마다 실제 골드로 바꿔준다.</summary>
        public float GoldReward;
        public float ShieldTowerDamageReduction = 0f;

        [NonSerialized] public List<Vector3> Waypoints;
        private int _waypointIndex;
        protected SpriteRenderer _sr;

        // ---------- 머리 위 체력바 ----------
        private Transform _hpBarRoot;     // 부모(적)의 스케일을 상쇄해 주는 홀더
        private Transform _hpBarBgTf;
        private Transform _hpFillTf;
        private SpriteRenderer _hpFillSr;
        private float _visualWorldHeight = EnemyArtWorldHeight; // 현재 보이는 몸 높이(월드 단위)
        private float _barWidth, _barHeight;

        /// <summary>체력바 조각들이 공유하는 흰색 사각 스프라이트. 색은 SpriteRenderer.color로 입힌다.
        /// [해설] 적이 웨이브마다 50마리씩 생겼다 사라지는데 매번 8x8 텍스처를 새로 만들면 낭비라
        /// 하나만 만들어 돌려 쓴다. 씬을 다시 불러와 텍스처가 파괴되면 null 검사에 걸려 다시 만든다.</summary>
        private static Sprite _barSprite;
        private static Sprite BarSprite =>
            _barSprite != null ? _barSprite : (_barSprite = SpriteFactory.SolidSquare(Color.white));

        private const float EnemyArtWorldHeight = 1.1f;
        protected const float BossArtWorldHeight = 2.0f;

        private float _slowTimer;
        private float _slowFactor = 1f;
        private float _stunTimer;

        private float _totalDistanceTraveled;

        public bool IsDead { get; private set; }

        public event Action<EnemyController> OnDied;

        /// <summary>타워가 "가장 위협적인(=오래 살아남아 계속 루프를 도는)" 적을 우선 타겟팅하는 데 쓰는 값.
        /// 거점 없는 무한 루프 경로에서는 "얼마나 남았는가"가 의미 없으므로, 대신 지금까지 누적으로
        /// 이동한 거리를 반환한다(죽을 때까지 계속 커지며 절대 리셋되지 않는다). 오래 살아남은 적일수록
        /// 값이 커지므로, TowerBase.FindTarget()이 여전히 "값이 가장 큰 적"을 조준하기만 하면
        /// 자연스럽게 가장 오래 살아남은(=처리 못하고 방치된) 적부터 우선 처리된다.</summary>
        public float PathProgress => _totalDistanceTraveled;

        public void Init(EnemyType type, float maxHp, float speed, float goldReward,
            List<Vector3> waypoints, Color fill, Color outline)
        {
            Type = type;
            MaxHP = maxHp;
            HP = maxHp;
            Speed = speed;
            GoldReward = goldReward;
            Waypoints = waypoints;
            transform.position = waypoints[0];

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 5;

            // 실제 아트(Assets/Resources/Sprites/Enemy_Mob.png 등)가 있으면 그것을 쓰고,
            // 없으면 기존 도형(SpriteFactory)으로 대체한다. 보스는 이 시점엔 아직 BossIndex를
            // 모르므로 일단 도형으로 그려두고, InitBoss()에서 보스 전용 아트로 덮어씌운다.
            Sprite art = type != EnemyType.Boss ? Resources.Load<Sprite>($"Sprites/Enemy_{type}") : null;
            if (art != null)
            {
                ApplyArt(art, EnemyArtWorldHeight);
            }
            else
            {
                _sr.sprite = type switch
                {
                    EnemyType.Mob => SpriteFactory.Circle(fill, outline),
                    EnemyType.Charger => SpriteFactory.Triangle(fill, outline),
                    EnemyType.Shield => SpriteFactory.Square(fill, outline),
                    _ => SpriteFactory.Diamond(fill, outline)
                };
                float scale = type == EnemyType.Shield ? 0.62f : type == EnemyType.Charger ? 0.5f : 0.55f;
                transform.localScale = Vector3.one * scale;
                _visualWorldHeight = _sr.sprite.bounds.size.y * scale;
            }

            if (type == EnemyType.Shield) ShieldTowerDamageReduction = 0.5f;

            BuildHpBar();
        }

        private void BuildHpBar()
        {
            _hpBarRoot = new GameObject("HPBar").transform;
            _hpBarRoot.SetParent(transform, false);

            _hpBarBgTf = NewBarPiece("HPBarBG", new Color(0.04f, 0.05f, 0.08f, 0.92f), 6).transform;
            _hpFillSr = NewBarPiece("HPBarFill", Color.white, 7);
            _hpFillTf = _hpFillSr.transform;

            LayoutHpBar();
        }

        private SpriteRenderer NewBarPiece(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_hpBarRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BarSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>
        /// 체력바를 "머리 위"에, 화면에서 확실히 보이는 크기로 배치한다.
        ///
        /// [해설] ★ 예전에는 체력바가 사실상 보이지 않았다. 원인은 <b>부모(적)의 스케일</b>이다.
        /// 적 스프라이트는 원본이 크기 때문에(예: 682px @PPU 100 = 6.82유닛) 원하는 몸 크기
        /// 1.1유닛에 맞추려고 transform.localScale이 0.16배로 줄어 있는데, 체력바가 그 자식이라
        /// 같이 0.16배로 쪼그라들었다. 실제 계산해 보면 화면에서 약 3 x 0.4 픽셀이고, 위치도
        /// 로컬 y=0.55 * 0.16 = 0.09유닛이라 머리 위가 아니라 몸통 한복판이었다.
        ///
        /// 그래서 체력바를 담는 홀더(_hpBarRoot)의 스케일에 <b>부모 스케일의 역수</b>를 넣어
        /// 상쇄한다. 이렇게 하면 그 아래 조각들은 부모가 아무리 작아도 항상 월드 단위 크기로
        /// 그려지고, 위치도 몸 높이를 기준으로 정확히 머리 위에 오게 된다.
        /// 보스는 Init 이후 InitBoss()에서 아트가 한 번 더 바뀌며 스케일이 달라지므로,
        /// ApplyArt()에서도 이 메서드를 다시 불러 준다.
        /// </summary>
        private void LayoutHpBar()
        {
            if (_hpBarRoot == null) return;

            _barWidth = _visualWorldHeight * 0.95f;
            _barHeight = Mathf.Max(0.13f, _visualWorldHeight * 0.15f);
            float gap = _visualWorldHeight * 0.07f;                        // 머리와 바 사이 여백(작을수록 머리에 붙는다)
            float centerY = _visualWorldHeight * 0.5f + gap + _barHeight * 0.5f;

            float parent = Mathf.Abs(transform.localScale.x) < 1e-5f ? 1f : transform.localScale.x;
            float inv = 1f / parent;
            _hpBarRoot.localScale = new Vector3(inv, inv, 1f);
            _hpBarRoot.localPosition = new Vector3(0f, centerY * inv, 0f);

            // 배경은 바보다 살짝 크게 만들어 테두리처럼 보이게 한다(밝은 배경에서도 구분되도록).
            float unit = BarSprite.bounds.size.x;
            float border = _barHeight * 0.30f;
            _hpBarBgTf.localScale = new Vector3((_barWidth + border) / unit, (_barHeight + border) / unit, 1f);
            _hpBarBgTf.localPosition = Vector3.zero;

            RefreshHpBar();
        }

        /// <summary>남은 체력에 맞춰 채움 막대의 길이와 색을 갱신한다.
        /// [해설] 예전에는 localScale.x만 줄여서 막대가 <b>가운데로 모이며</b> 작아졌다(양쪽이 같이
        /// 줄어듦). 이제는 왼쪽 끝을 고정하고 오른쪽부터 줄어들도록 위치도 함께 옮긴다.
        /// 색도 남은 비율에 따라 초록 → 노랑 → 빨강으로 바뀌어서 위험한 적이 한눈에 보인다.</summary>
        private void RefreshHpBar()
        {
            if (_hpFillTf == null) return;

            float ratio = MaxHP > 0f ? Mathf.Clamp01(HP / MaxHP) : 0f;
            float unit = BarSprite.bounds.size.x;
            float w = _barWidth * ratio;

            _hpFillTf.localScale = new Vector3(w / unit, _barHeight / unit, 1f);
            _hpFillTf.localPosition = new Vector3(-_barWidth * 0.5f + w * 0.5f, 0f, -0.01f);
            _hpFillSr.color = ratio > 0.5f ? new Color(0.36f, 0.86f, 0.40f)
                            : ratio > 0.25f ? new Color(0.96f, 0.78f, 0.26f)
                                            : new Color(0.93f, 0.28f, 0.24f);
        }

        /// <summary>실제 아트 스프라이트를 적용하고, 지정한 월드 높이에 맞춰 스케일을 보정한다.
        /// (기존 도형은 흰색 틴트를 쓰지 않으므로, 실제 아트 적용 시 색을 흰색으로 리셋한다.)</summary>
        protected void ApplyArt(Sprite art, float desiredWorldHeight)
        {
            _sr.sprite = art;
            _sr.color = Color.white;
            float scale = desiredWorldHeight / art.bounds.size.y;
            transform.localScale = new Vector3(scale, scale, 1f);

            // 몸 크기가 바뀌었으므로 체력바 위치/크기도 다시 맞춘다(보스가 이 경로를 탄다).
            _visualWorldHeight = desiredWorldHeight;
            LayoutHpBar();
        }

        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        public void ApplyStun(float duration)
        {
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>
        /// 피해를 받는다. [해설] 타워 피해 계산의 마지막 단계다 — 앞 단계는 Projectile.Hit()에
        /// 정리해 뒀다(타워별 기본 피해 → 전역 공격력 배율 → 여기서 방패병 경감).
        /// 여기서 깎이는 건 <b>타워가 준 피해뿐</b>이다: 방패병의 설정이 "타워 공격을 버팀"이라
        /// 보스 능력 등 타워가 아닌 피해원(DamageSource.Tower가 아닌 경우)에는 경감이 걸리지 않는다.
        /// </summary>
        public virtual void TakeDamage(float amount, DamageSource source)
        {
            if (IsDead) return;
            // 방패병(ShieldTowerDamageReduction = 0.5)이면 타워 피해만 절반으로 줄인다.
            if (source == DamageSource.Tower && ShieldTowerDamageReduction > 0f)
                amount *= (1f - ShieldTowerDamageReduction);

            HP -= amount;
            RefreshHpBar();

            if (HP <= 0f) Die();
        }

        protected virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        protected virtual void Update()
        {
            if (IsDead) return;

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f) _slowFactor = 1f;
            }

            // [해설] 거점이 사라지고 경로가 "무한 순회" 루프가 되면서, 마지막 웨이포인트에 닿아도
            // 멈추지 않고 다음 인덱스를 (인덱스+1) % Count로 계산해 첫 지점으로 돌아간다.
            // 그래서 적은 처치되기 전까지 폐곡선을 영원히 맴돈다.
            int nextIndex = (_waypointIndex + 1) % Waypoints.Count;
            Vector3 target = Waypoints[nextIndex];
            Vector3 dir = target - transform.position;
            float dist = dir.magnitude;
            float step = Speed * _slowFactor * Time.deltaTime;
            _totalDistanceTraveled += Mathf.Min(step, dist);
            if (step >= dist)
            {
                transform.position = target;
                _waypointIndex = nextIndex;
            }
            else
            {
                transform.position += dir.normalized * step;
            }
        }
    }
}
