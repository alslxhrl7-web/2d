using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 번개탑: 한 대상을 때린 뒤 그 지점에서 가장 가까운 다른 적으로 번개가 연쇄로 튄다.
    ///
    /// [해설] 왜 이 타워가 필요했나 — 이 게임의 적은 정사각형 길 위를 <b>한 줄로 늘어서서</b> 돈다.
    /// 그런데 기존 광역기는 포격탑의 원형 폭발뿐이라, 넓게 퍼진 원으로 얇고 긴 줄을 때리는 셈이어서
    /// 실제로 닿는 적은 두세 마리가 한계였다. 번개탑은 "적에서 적으로" 튀기 때문에 원이 아니라
    /// 줄 모양을 따라간다 — 같은 광역이라도 이 맵 구조에서만 성립하는 역할을 맡는다.
    ///
    /// 피해 전달 방식이 다른 타워와 다르다. 화살탑/빙결탑/포격탑은 Projectile을 날려서
    /// Projectile.Hit()이 피해를 주지만, 번개는 날아가는 물체가 아니라 즉시 이어지는 선이므로
    /// 투사체 없이 Fire()에서 곧바로 피해를 준다. 강화 배율은 다른 타워와 마찬가지로
    /// TowerBase.EffectiveDamage가 종류별로 곱해 주고, 방패병 경감은 EnemyController.TakeDamage
    /// 안에서 처리되므로 결과적으로 피해 계산 경로는 세 타워와 완전히 같다.
    /// </summary>
    public class LightningTower : TowerBase
    {
        // [해설] Range: 공격 사거리, FireInterval: 발사 간격(초), Damage: <b>첫 대상</b>에게 주는 피해량.
        // 두 번째 이후로 튈 때마다 ChainFalloff(0.65)가 곱해져 점점 약해진다.
        // 단일 대상만 놓고 보면 초당 10.8로 세 타워 중 중간이지만, 적이 줄지어 있으면
        // 한 번에 최대 4마리를 때리므로 실효 피해는 훨씬 커진다(아래 계산 참고).
        /// <summary>범위/연쇄 공격이라 조준 대상이 죽을 예정이어도 주변 적에게 피해가
        /// 들어간다 — TowerBase.FindTarget이 대상을 거르지 않도록 true로 둔다.</summary>
        protected override bool IsAreaAttack => true;

        public void Setup()
        {
            Type = TowerType.Lightning;
            Range = 3.0f;        // BuildManager.TowerRangeFor(Lightning)과 반드시 같아야 한다  //밸런스 조절 사거리 3로 증가
            FireInterval = 1.3f;
            Damage = 14f;
        }

        /// <summary>최초 대상을 포함해 번개가 때리는 최대 적 수.</summary>
        private const int MaxChainTargets = 4;

        /// <summary>직전에 맞은 적을 기준으로, 이 반경 안에 있는 적에게만 번개가 튄다.
        /// 사거리(Range)와는 별개다 — 일단 사거리 안의 적을 때리면, 그 다음부터는 타워에서
        /// 아무리 멀어져도 적끼리 가깝기만 하면 계속 이어진다.</summary>
        private const float ChainJumpRadius = 5.0f;

        // <summary>한 번 튈 때마다 피해에 곱해지는 감쇠율. 14 → 9.1 → 5.9 → 3.8 (합계 32.8).</summary>
        private const float ChainFalloff = 0.7f;

        /// <summary>이번 발사에서 이미 맞은 적 목록. 같은 적을 두 번 때리거나 두 적 사이를
        /// 무한히 왕복하는 것을 막는다.
        /// [해설] static이 아니라 인스턴스 필드다 — static으로 두면 번개탑이 여러 개일 때
        /// 서로의 상태를 덮어쓸 여지가 생긴다. 발사 때마다 Clear()해서 재사용하므로
        /// 매 발사마다 새 List를 만드는 할당도 없다.</summary>
        private readonly List<EnemyController> _chained = new List<EnemyController>(MaxChainTargets);

        protected override void Fire(EnemyController target)
        {
            _chained.Clear();

            EnemyController current = target;
            Vector3 from = MuzzlePoint; // 2.5D: 번개도 탑 윗부분에서 뻗어 나간다
            float damage = EffectiveDamage; // 번개탑 전용 강화 배율이 이미 반영된 값

            while (current != null && _chained.Count < MaxChainTargets)
            {
                _chained.Add(current);

                // 번개가 닿는 곳은 몸통(AimPoint), 연쇄 판정 기준은 바닥(transform.position)이다.
                Vector3 hitPos = current.AimPoint;
                ImpactEffect.SpawnLightningBolt(from, hitPos);

                // [해설] TakeDamage가 적을 죽일 수 있다. 죽은 적은 IsDead가 즉시 true가 되고
                // (Destroy는 프레임 끝에 처리되므로 Active 목록에는 잠시 남아 있다), 아래
                // FindNextChainTarget이 IsDead를 걸러내므로 이미 죽은 적으로는 튀지 않는다.
                // 피해를 준 뒤에 다음 대상을 찾으므로, 목록을 순회하는 도중에 목록이 바뀌는
                // 상황(InvalidOperationException)도 생기지 않는다.
                current.TakeDamage(damage, DamageSource.Tower);

                from = hitPos;
                damage *= ChainFalloff;
                current = FindNextChainTarget(current.transform.position);
            }
        }

        /// <summary>직전 명중 지점에서 ChainJumpRadius 안에 있는, 아직 맞지 않은 적 중
        /// 가장 가까운 하나를 고른다. 없으면 null(=연쇄 종료).</summary>
        private EnemyController FindNextChainTarget(Vector3 from)
        {
            EnemyController best = null;
            float bestDist = float.MaxValue;

            foreach (var e in EnemyController.Active)
            {
                if (e == null || e.IsDead) continue;
                if (_chained.Contains(e)) continue;
                if (e.EffectiveHP <= 0f) continue; // 다른 타워의 투사체만으로 이미 죽을 적에는 튀지 않는다

                float d = Vector2.Distance(from, e.transform.position);
                if (d > ChainJumpRadius || d >= bestDist) continue;

                bestDist = d;
                best = e;
            }
            return best;
        }
    }
}
