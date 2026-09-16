using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 빙결탑: 낮은 피해, 대신 슬로우로 경로 통과 속도를 늦춘다.
    /// [해설] 기존에는 맞은 적 한 명에게만 슬로우가 걸렸지만, 이제는 "범위 슬로우"로 바뀌어
    /// 착탄 지점(IceRadius) 안에 있는 모든 적이 함께 느려지고 약한 피해도 같이 받는다.
    /// 좁은 통로에 몰려오는 적 무리를 통째로 묶어두는 용도로 쓰기 좋다.
    /// </summary>
    public class IceTower : TowerBase
    {
        // [해설] Range: 공격 사거리, FireInterval: 발사 간격(초), Damage: 범위 안 적 1명당 피해량.
        // 빙결탑은 세 타워 중 피해가 가장 낮은 대신, 아래 IceRadius만큼 범위로 슬로우를 건다.
        public void Setup()
        {
            Type = TowerType.Ice;
            Range = 2.6f;
            FireInterval = 1.1f;
            Damage = 3f;
        }

        // [해설] 착탄 지점 기준 범위 슬로우의 반경/세기/지속시간.
        // IceRadius를 0보다 크게 준 것이 "단일 대상 → 범위 슬로우" 전환의 핵심이다
        // (Projectile.cs의 Hit()에서 splashRadius가 0보다 크면 범위 판정으로 처리된다).
        private const float IceRadius = 1.3f;       // 이 반경 안의 모든 적이 슬로우+피해를 함께 받는다.
        private const float IceSlowFactor = 0.5f;   // 이동속도를 50%로 낮춘다 (절반 속도)
        private const float IceSlowDuration = 1.8f; // 슬로우 지속 시간(초)

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("IceShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            // splashRadius 자리에 IceRadius(0보다 큼)를 넘겨서, 착탄 지점 반경 안의 모든 적에게
            // 피해와 슬로우가 함께 적용되는 "범위 슬로우"로 동작한다.
            p.Init(target, 7f, Damage, DamageSource.Tower, new Color(0.6f, 0.95f, 1f),
                IceRadius, IceSlowFactor, IceSlowDuration);
        }
    }
}
