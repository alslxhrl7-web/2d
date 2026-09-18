using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 포격탑(폭발탑): 느린 연사, 대신 착탄 지점 "주변 유닛"에게 함께 폭발 피해를 준다.
    /// [해설] 세 타워 중 유일한 광역 폭발형이다 — 한 발이 CannonSplashRadius 반경 안의
    /// 모든 적에게 각각 Damage만큼 피해를 준다(감소 없이 전원 동일하게 적용). 연사는 가장
    /// 느리지만, 적이 몰려있는 통로 초입 등에서 압도적인 처리력을 낸다.
    /// </summary>
    public class CannonTower : TowerBase
    {
        // [해설] Range: 공격 사거리, FireInterval: 발사 간격(초, 세 타워 중 가장 김),
        // Damage: 폭발 반경 안 적 "1명당" 피해량(반경 안에 있으면 여러 명에게 동시에 들어간다).
        // 이 Damage가 실제로 적의 체력에서 깎이기까지 거치는 과정(전역 공격력 배율, 방패병 경감,
        // 단일 대상 vs 범위 판정)은 Projectile.Hit()의 주석에 세 타워를 묶어서 정리해 뒀다.
        /// <summary>범위/연쇄 공격이라 조준 대상이 죽을 예정이어도 주변 적에게 피해가
        /// 들어간다 — TowerBase.FindTarget이 대상을 거르지 않도록 true로 둔다.</summary>
        protected override bool IsAreaAttack => true;

        public void Setup()
        {
            Type = TowerType.Cannon;
            Range = 3.3f; // 사거리 소폭 증가 (2.9 → 3.3)
            FireInterval = 1.5f;
            Damage = 26f; // 공격력 증가 (14 → 26)
        }

        // [해설] 폭발(스플래시) 반경. 값이 클수록 한 발로 더 넓게 "주변 유닛"까지 피해가 퍼진다.
        private const float CannonSplashRadius = 1.4f; // 기존 1.15 → 1.4로 확장해 폭발 범위를 더 체감되게 넓힘

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("CannonShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            // splashRadius 자리에 CannonSplashRadius(0보다 큼)를 넘기면, Projectile.Hit()에서
            // 착탄 지점 기준 그 반경 안의 모든 적을 찾아 각각 Damage만큼 피해를 준다.
            // cannonballVisual: true → 주황색 원 대신 검은 포탄 모양으로 날아간다.
            // impactEffect: Explosion → 맞는 순간 폭발 이펙트가 터진다.
            p.Init(target, 6f, Damage, DamageSource.Tower, Color.black, CannonSplashRadius,
                impactEffect: ImpactEffectKind.Explosion, cannonballVisual: true);
        }
    }
}
