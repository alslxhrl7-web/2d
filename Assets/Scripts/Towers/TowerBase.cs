using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 타워 공통 로직: 사거리 내 가장 진행도가 높은(=가장 위협적인) 적을 자동 타겟팅,
    /// 쿨타임에 따라 발사. 보스가 "타워 하나 무력화" 능력을 쓸 때 일시적으로 정지시킬 수 있다.
    /// </summary>
    public abstract class TowerBase : MonoBehaviour
    {
        /// <summary>
        /// ★ 타워 <b>종류별</b> 공격력 배율. 웨이브 보상에서 "화살탑 강화"처럼 한 종류를 고를
        /// 때마다 그 종류에만 누적된다(GameManager.ApplyUpgrade).
        ///
        /// [해설] 예전에는 모든 타워에 함께 곱해지는 값 하나(GlobalDamageMultiplier)였다.
        /// 그러면 보상에서 "타워 강화"가 뜨면 고민 없이 고르면 되는, 선택이 아닌 항목이었다.
        /// 종류별로 나누면 "지금 화살탑이 많으니 화살탑을 키울까, 아니면 포격탑 하나에
        /// 몰아줄까" 같은 판단이 생긴다.
        ///
        /// [해설] ★★ 누적 방식을 <b>곱셈에서 덧셈으로</b> 바꿨다. 이 게임 최대의 밸런스 붕괴
        /// 원인이 바로 이 항이었다. 예전에는 고를 때마다 ×1.3이 곱해졌는데, 75웨이브 동안
        /// 이 보상을 고르다 보면 1.3의 거듭제곱이 되어 배율이 수천만 배까지 치솟는다.
        /// 반면 적은 웨이브당 체력이 +10씩 <b>더해질</b> 뿐이라 선형으로만 강해진다.
        /// 지수 대 선형이라 상수를 아무리 조정해도 교차점만 뒤로 밀릴 뿐, 어느 시점부터는
        /// 반드시 무위험 상태가 된다(실제로 5웨이브 이후 동시 생존 적이 0~1마리였다).
        ///
        /// 이제 한 번 고를 때마다 <b>기본 공격력의 25%p</b>가 더해진다. 4번 고르면 2배,
        /// 8번이면 3배 — 적의 성장과 같은 "직선" 단위가 되어 두 곡선이 나란히 간다.
        /// 값 자체가 배율(1.0에서 시작)인 것은 그대로라 EffectiveDamage 쪽은 바뀌지 않는다.
        ///
        /// static이라 이미 세워 둔 타워에도 소급 적용되고, 씬을 다시 불러와도 남으므로
        /// GameBootstrapper.ResetStaticState에서 반드시 되돌려야 한다.
        /// 배열 크기는 enum 길이를 따라가므로 타워를 추가해도 그대로 동작한다.
        /// </summary>
        private static float[] _damageMultipliers = NewMultiplierTable();

        private static float[] NewMultiplierTable()
        {
            var t = new float[System.Enum.GetValues(typeof(TowerType)).Length];
            for (int i = 0; i < t.Length; i++) t[i] = 1f;
            return t;
        }

        public static float DamageMultiplierFor(TowerType type)
        {
            int i = (int)type;
            return (i >= 0 && i < _damageMultipliers.Length) ? _damageMultipliers[i] : 1f;
        }

        /// <summary>그 종류의 공격력 배율에 <b>더한다</b>(곱하지 않는다 — 위 해설 참고).
        /// amount 0.25는 "기본 공격력의 25%p 증가"를 뜻한다.</summary>
        public static void AddDamageBonus(TowerType type, float amount)
        {
            int i = (int)type;
            if (i >= 0 && i < _damageMultipliers.Length) _damageMultipliers[i] += amount;
        }

        /// <summary>새 게임 시작 시 모든 종류의 배율을 1로 되돌린다.</summary>
        public static void ResetDamageMultipliers() => _damageMultipliers = NewMultiplierTable();
        public static readonly List<TowerBase> Active = new List<TowerBase>();

        public TowerType Type;
        public float Range = 2.6f;       // 공격 사거리(월드 유닛)
        public float FireInterval = 1f;  // 발사 간격(초)

        /// <summary>강화 배율까지 반영한 <b>실제로 나가는</b> 피해량. 각 타워의 Fire()는
        /// Damage가 아니라 이 값을 투사체에 넘긴다 — 그래서 Projectile은 배율을 몰라도 되고,
        /// 딜 누수 방지용 예약 계산도 실제 값과 자동으로 일치한다.</summary>
        protected float EffectiveDamage => Damage * DamageMultiplierFor(Type);

        /// <summary>이 타워가 한 발에 주는 기본 피해량. 각 타워의 Setup()에서 정한다
        /// (화살탑 9 / 빙결탑 4.5 / 포격탑 26 / 번개탑 14). 이 값이 단일 대상에게 들어갈지 범위 안 전원에게
        /// 들어갈지는 각 타워의 Fire()가 Projectile에 넘기는 splashRadius가 결정한다 —
        /// 타입별 피해 방식 전체 설명은 Projectile.Hit() 주석 참고.</summary>
        public float Damage = 10f;

        /// <summary>이 타워의 "평상시" 색. 설치 직후 BuildManager가 채워 준다.
        /// 철거 호버 강조(빨강)와 보스의 무력화 연출(회색)이 끝나면 둘 다 이 색으로 되돌린다 —
        /// 원래 색의 출처를 한 곳으로 모아 두 연출이 서로의 색을 덮어쓰는 사고를 막는다.</summary>
        public Color BaseColor { get; set; } = Color.white;

        /// <summary>
        /// 발사체가 나가는 지점 — 타워의 발밑이 아니라 <b>윗부분</b>이다.
        ///
        /// [해설] ★ 2.5D 전환의 부작용 수정. transform.position은 타워가 딛고 선 바닥 지점이라,
        /// 거기서 화살이 나가면 탑 꼭대기가 아니라 <b>땅바닥에서 화살이 솟는</b> 것처럼 보인다.
        /// 타워 그림 높이(BuildManager.TowerArtWorldHeight = 1.5)의 0.75 지점에서 쏘게 했다.
        /// 조준·사거리 판정은 그대로 바닥 좌표로 하므로 게임 로직은 바뀌지 않는다.
        /// </summary>
        public Vector3 MuzzlePoint => transform.position + new Vector3(0f, MuzzleHeight, 0f);

        private const float MuzzleHeight = 1.125f; // 1.5 * 0.75

        private float _cooldown;
        private float _disableTimer;
        private Coroutine _flashRoutine;

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        /// <summary>
        /// 보스 능력으로 이 타워를 seconds초 동안 멈춘다.
        /// [해설] ★ 버그 수정. 예전에는 호출할 때마다 WaitForSeconds(seconds) 코루틴을 새로 띄웠다.
        /// 그런데 보스 5의 3페이즈는 능력 간격이 2.45초까지 줄어드는데 무력화 시간은 2.5초라,
        /// 같은 타워가 연달아 찍히면 코루틴 두 개가 겹친다. 두 번째 코루틴은 "시작할 때의 색"으로
        /// 이미 회색을 집어 든 상태이므로, 첫 번째가 흰색을 돌려놓은 뒤 두 번째가 다시 회색을
        /// 덮어써서 <b>멀쩡히 작동하는 타워가 영영 회색으로 굳는</b> 일이 벌어졌다.
        /// 이제 코루틴은 항상 하나만 돌고, 복원 시점도 WaitForSeconds가 아니라 실제 _disableTimer가
        /// 0이 되는 순간에 맞춘다(무력화가 연장되면 연출도 같이 연장된다).
        /// </summary>
        public void SetDisabledFor(float seconds)
        {
            _disableTimer = Mathf.Max(_disableTimer, seconds);

            // 2.5D 전환 후 스프라이트는 본체가 아니라 자식("Visual")에 붙어 있다.
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            if (_flashRoutine != null) return; // 이미 회색 연출 중이면 타이머만 늘리고 끝낸다
            _flashRoutine = StartCoroutine(FlashDisabled(sr));
        }

        private IEnumerator FlashDisabled(SpriteRenderer sr)
        {
            sr.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
            while (_disableTimer > 0f) yield return null; // Update()가 매 프레임 줄여 준다
            if (sr != null) sr.color = BaseColor;
            _flashRoutine = null;
        }

        protected virtual void Update()
        {
            if (_disableTimer > 0f)
            {
                _disableTimer -= Time.deltaTime;
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            EnemyController target = FindTarget();
            if (target != null)
            {
                Fire(target);
                _cooldown = FireInterval;
            }
        }

        /// <summary>
        /// 이 타워가 범위 공격인가.
        /// [해설] 아래 FindTarget이 "이미 죽을 적"을 거를 때 쓴다. 단일 대상 타워는 그런 적에게
        /// 쏘면 100% 낭비지만, 범위 타워는 <b>조준 대상이 죽을 예정이어도 그 주변 적들에게는
        /// 피해가 그대로 들어가므로</b> 쏘는 게 이득이다. 그래서 범위 타워는 거르지 않는다.
        /// </summary>
        protected virtual bool IsAreaAttack => false;

        /// <summary>
        /// 사거리 안에서 가장 진행도가 높은(=가장 위협적인) 적을 고른다.
        ///
        /// [해설] ★ 딜 누수 수정. 예전에는 남은 체력을 보지 않고 진행도만 봤다. 그래서 체력이
        /// 5밖에 안 남은 적에게 타워 세 개가 동시에 조준했고, 첫 발이 죽이면 나머지 두 발은
        /// 명중하지 못하고 사라졌다(Projectile.Update의 자폭 분기). 쏜 순간 이미 낭비가
        /// 확정된 발사였다.
        ///
        /// 이제 조준 판단을 EffectiveHP(남은 체력 - 날아오는 피해)로 한다. 그 값이 0 이하인
        /// 적은 "이미 배정된 피해만으로 죽는" 적이므로 후보에서 뺀다. 사거리 안이 전부 그런
        /// 적뿐이면 단일 대상 타워는 <b>null을 돌려주고 발사를 아낀다</b> — 이때 Update()가
        /// 쿨다운을 초기화하지 않으므로, 유효한 적이 나타나는 즉시 곧바로 쏠 수 있다.
        /// (범위 타워는 IsAreaAttack이 true라 fallback으로 그냥 쏜다. 위 설명 참고.)
        /// </summary>
        protected EnemyController FindTarget()
        {
            EnemyController best = null;          // 아직 죽지 않을 적 중 가장 앞선 적
            float bestProgress = -1f;
            EnemyController fallback = null;      // 죽을 예정인 적까지 포함한 가장 앞선 적
            float fallbackProgress = -1f;

            foreach (var e in EnemyController.Active)
            {
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d > Range) continue;

                if (e.PathProgress > fallbackProgress)
                {
                    fallbackProgress = e.PathProgress;
                    fallback = e;
                }

                if (e.EffectiveHP <= 0f) continue; // 날아오는 피해만으로 이미 죽는 적
                if (e.PathProgress > bestProgress)
                {
                    bestProgress = e.PathProgress;
                    best = e;
                }
            }

            if (best != null) return best;
            return IsAreaAttack ? fallback : null;
        }

        protected abstract void Fire(EnemyController target);
    }
}
