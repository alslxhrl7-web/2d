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

        /// <summary>그림만 담는 자식. 본체는 바닥 지점에 두고, 이 자식을 위로 올려 발밑을 맞춘다.
        /// 크기 조절(스케일)도 본체가 아니라 전부 여기에 건다.</summary>
        protected Transform _visual;

        /// <summary>정렬 순서가 같을 때 앞뒤를 일정하게 가르기 위한 스폰 일련번호(위 Init 해설 참고).</summary>
        private static int _spawnCounter;

        // ---------- 머리 위 체력바 ----------
        private Transform _hpBarRoot;     // 부모(적)의 스케일을 상쇄해 주는 홀더
        private Transform _hpBarBgTf;
        private SpriteRenderer _hpBarBgSr;

        /// <summary>true면 체력이 가득 차 있어도 체력바를 계속 보여 준다. 보스가 켠다.</summary>
        protected virtual bool AlwaysShowHpBar => false;
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

        // [해설] ★ 2.5D 전환 후 화면을 재 보고 낮춘 값(1.1 → 0.95). 바닥을 세로로 누르면서
        // 가로 도로의 두께가 39px까지 얇아졌는데 적은 113px 그대로라, 60마리가 도로를 완전히
        // 뒤덮고 서로 겹쳤다. 적을 조금 줄여 도로 위에 "올라서 있는" 비율을 되찾는다.
        private const float EnemyArtWorldHeight = 0.95f;
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

            // [해설] ★ 2.5D 전환. 예전에는 SpriteRenderer가 적 오브젝트 본체에 붙어 있어서
            // 스프라이트의 <b>중심</b>이 경로 위에 놓였다 — 탑뷰에서는 맞지만, 바닥이 누운
            // 2.5D에서는 몸이 바닥에 반쯤 파묻힌 것처럼 보인다. 그래서 그림만 자식(_visual)으로
            // 떼어 내고 몸 높이의 절반만큼 위로 올려, <b>발밑</b>이 경로 위에 오게 했다.
            // 본체(transform)는 여전히 바닥 지점 그대로라 이동·조준·사거리 판정은 하나도 안 바뀐다.
            // 크기 조절도 이제 본체가 아니라 _visual에만 걸리므로, 본체 스케일은 항상 1이다.
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            // [해설] ★ 가로 도로 위에서는 모든 적의 y가 <b>비트 단위로 똑같다</b>(이동 방향의
            // y성분이 정확히 0이라 y가 한 번도 안 변한다). 그러면 정렬 순서도 전부 같아져서
            // 겹칠 때 누가 앞인지가 다시 제멋대로가 된다 — y 정렬을 넣은 이유가 무색해진다.
            // 그래서 스폰 순서에 따라 아주 작은 z를 부여한다. 직교 카메라에서는 정렬 순서가
            // 같을 때 z가 앞뒤를 가르므로, 최소한 <b>깜빡이지 않고 일정한</b> 순서가 된다.
            _visual.localPosition = new Vector3(0f, 0f, (_spawnCounter++ % 512) * -0.0005f);
            _sr = _visual.gameObject.AddComponent<SpriteRenderer>();

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
                _visual.localScale = Vector3.one * scale;
                _visualWorldHeight = _sr.sprite.bounds.size.y * scale;
                AnchorVisualToFeet();
            }

            if (type == EnemyType.Shield) ShieldTowerDamageReduction = 0.5f;

            BuildHpBar();
        }

        /// <summary>그림을 몸 높이의 절반만큼 위로 올려, 스프라이트 아래쪽 끝이 바닥 지점에 닿게 한다.</summary>
        private void AnchorVisualToFeet()
        {
            if (_visual == null) return;
            _visual.localPosition = new Vector3(0f, _visualWorldHeight * 0.5f, _visual.localPosition.z); // z(정렬 타이브레이커)는 보존
            RefreshDepth();
        }

        /// <summary>
        /// 화면 아래쪽(= y가 작은 쪽)일수록 앞에 그린다.
        /// [해설] 예전에는 모든 적이 sortingOrder 5로 고정이라, 적끼리 겹칠 때 누가 앞인지가
        /// 스프라이트 생성 순서에 따라 제멋대로였다. 이제 타워와 적이 같은 밴드(View.BandActor)에서
        /// y로 함께 정렬되므로, 적이 타워 앞을 지나가면 앞으로, 뒤로 지나가면 가려진다.
        /// 적은 계속 움직이므로 매 프레임 갱신해야 한다(Update에서 호출).
        /// </summary>
        protected void RefreshDepth()
        {
            if (_sr == null) return;
            int order = View.Order(View.BandActor, transform.position.y);
            _sr.sortingOrder = order;
            if (_hpBarBgSr != null) _hpBarBgSr.sortingOrder = View.Order(View.BandOverhead, transform.position.y);
            if (_hpFillSr != null) _hpFillSr.sortingOrder = View.Order(View.BandOverhead, transform.position.y) + 1;
        }

        private void BuildHpBar()
        {
            _hpBarRoot = new GameObject("HPBar").transform;
            _hpBarRoot.SetParent(transform, false);

            _hpBarBgSr = NewBarPiece("HPBarBG", new Color(0.04f, 0.05f, 0.08f, 0.92f), 0);
            _hpBarBgTf = _hpBarBgSr.transform;
            _hpFillSr = NewBarPiece("HPBarFill", Color.white, 0);
            _hpFillTf = _hpFillSr.transform;

            LayoutHpBar();
            RefreshDepth(); // 방금 만든 체력바에도 올바른 정렬 순서를 바로 넣어 준다
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

            // [해설] ★ 실제 화면을 재 보고 줄인 값. 이전 계수(0.95)는 "몸 높이"에 곱하는 값이라,
            // 세로로 긴 적 그림에서는 바가 <b>몸보다 넓어졌다</b> — 화면에서 재 보니 고블린 몸통이
            // 약 60px인데 바가 107px이었다. 게다가 웨이브당 적이 60마리라 그 바들이 화면을 초록으로
            // 도배했다. 몸통 폭에 맞도록 0.55로 낮추고, 두께와 머리와의 간격도 같이 줄였다.
            _barWidth = _visualWorldHeight * 0.55f;
            _barHeight = Mathf.Max(0.08f, _visualWorldHeight * 0.09f);
            float gap = _visualWorldHeight * 0.05f;                        // 머리와 바 사이 여백(작을수록 머리에 붙는다)
            // [해설] 2.5D로 바꾸면서 그림이 발밑 기준이 되었으므로, 머리 끝은 y = 몸 높이다
            // (예전 중심 기준일 때의 몸높이/2가 아니다).
            float centerY = _visualWorldHeight + gap + _barHeight * 0.5f;

            // 2.5D 전환 후 본체 스케일은 항상 1이라 아래 보정은 사실상 1을 곱한다. 혹시 본체에
            // 스케일이 걸리는 변경이 생겨도 체력바 크기가 흔들리지 않도록 방어적으로 남겨 둔다.
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

            // [해설] ★ 화면이 초록 막대로 뒤덮이는 문제의 핵심 해결책. 한 웨이브에 적이 60마리인데
            // 아직 한 대도 안 맞은 적까지 전부 가득 찬 바를 달고 있으면, 정작 <b>중요한 정보인
            // "누가 다쳤나"</b>가 묻힌다. 멀쩡한 적은 바를 숨기고 한 대라도 맞은 순간부터 보여 준다
            // (디펜스 장르에서 흔한 방식이다). 보스는 체력 상태가 곧 전투의 핵심 정보이므로
            // BossController가 AlwaysShowHpBar를 true로 덮어써서 항상 보이게 한다.
            bool show = AlwaysShowHpBar || ratio < 0.999f;
            if (_hpBarRoot != null) _hpBarRoot.gameObject.SetActive(show);
            if (!show) return;

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
            _visual.localScale = new Vector3(scale, scale, 1f);

            // 몸 크기가 바뀌었으므로 체력바 위치/크기도 다시 맞춘다(보스가 이 경로를 탄다).
            _visualWorldHeight = desiredWorldHeight;
            AnchorVisualToFeet();
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

        // ---------- 딜 누수 방지용 예약 피해 ----------

        /// <summary>
        /// 이미 발사되어 <b>날아오고 있지만 아직 명중하지 않은</b> 피해의 합계.
        ///
        /// [해설] ★ 왜 필요한가 — 딜 누수 문제. 예전에는 타워가 사거리 안에서 "가장 앞선 적"만
        /// 보고 쐈기 때문에, 체력 5만 남은 적에게 여러 타워가 동시에 조준해 화살을 퍼부었다.
        /// 첫 발이 적을 죽이면 나머지 화살은 Projectile.Update의 "대상이 죽었으면 자폭" 분기에
        /// 걸려 <b>피해를 한 번도 주지 못하고 사라졌다</b>. 쏘는 순간 이미 낭비가 확정된 셈이다.
        ///
        /// 그래서 발사 시점에 "이 적에게 몇의 피해가 예약됐는지"를 적 쪽에 적어 둔다. 타워는
        /// 조준할 때 남은 체력이 아니라 <b>EffectiveHP(체력 - 예약)</b>를 보고, 그 값이 0 이하인
        /// 적(= 날아오는 것만으로 이미 죽는 적)은 건너뛴다. 예약은 명중하거나 투사체가 사라질 때
        /// 반드시 해제되므로(Projectile.ReleaseReservation), 투사체가 중간에 소멸해도 그 적은
        /// 곧바로 다시 유효한 표적이 된다.
        /// </summary>
        public float IncomingDamage { get; private set; }

        /// <summary>
        /// 투사체가 날아가 꽂힐 지점 — 발밑이 아니라 <b>몸통 한가운데</b>다.
        ///
        /// [해설] ★ 2.5D 전환의 부작용 수정. transform.position은 이제 적의 발밑(바닥 지점)이라
        /// 거리·사거리·범위 판정에는 이 값이 맞지만, 화살이 그리로 날아가면 몸이 아니라
        /// <b>발밑 땅에 꽂히는</b> 것처럼 보인다. 그래서 날아가는 경로만 이 조준점을 쓴다.
        /// 착탄 이펙트(서리 장판·폭발)는 바닥에 깔려야 하므로 그대로 transform.position을 쓴다.
        /// </summary>
        public Vector3 AimPoint => transform.position + new Vector3(0f, _visualWorldHeight * 0.5f, 0f);

        /// <summary>날아오는 피해까지 반영한 "실질 남은 체력". 타워의 조준 판단 기준이다.</summary>
        public float EffectiveHP => HP - IncomingDamage;

        /// <summary>기본 피해량이 이 적에게 실제로 몇으로 들어가는지 계산한다(방패병 경감 반영).
        /// TakeDamage와 예약 계산이 반드시 같은 식을 쓰도록 여기 한 곳에 모아 둔다.</summary>
        public virtual float ExpectedDamage(float amount, DamageSource source)
        {
            // 방패병(ShieldTowerDamageReduction = 0.5)이면 타워 피해만 절반으로 줄인다.
            if (source == DamageSource.Tower && ShieldTowerDamageReduction > 0f)
                amount *= (1f - ShieldTowerDamageReduction);
            return amount;
        }

        public void ReserveIncoming(float amount)
        {
            if (amount > 0f) IncomingDamage += amount;
        }

        /// <summary>예약 해제. 부동소수점 오차가 쌓여 음수로 내려가지 않도록 0에서 자른다.</summary>
        public void ReleaseIncoming(float amount)
        {
            if (amount <= 0f) return;
            IncomingDamage = Mathf.Max(0f, IncomingDamage - amount);
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
            // 방패병 경감 등, "기본 피해 → 실제로 깎이는 양" 변환은 ExpectedDamage 한 곳에 모아 뒀다.
            // 예약(IncomingDamage) 계산도 같은 함수를 쓰므로 두 식이 어긋날 일이 없다.
            amount = ExpectedDamage(amount, source);

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

            RefreshDepth(); // 움직였으니 앞뒤 순서를 다시 계산한다
        }
    }
}
