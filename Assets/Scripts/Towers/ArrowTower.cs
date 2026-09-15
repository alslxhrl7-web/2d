using UnityEngine;

namespace Defense2D
{
    /// <summary>화살탑: 빠른 연사, 단일 대상, 표준 피해.</summary>
    public class ArrowTower : TowerBase
    {
        public void Setup()
        {
            Type = TowerType.Arrow;
            Range = 3.2f;
            FireInterval = 0.6f;
            Damage = 9f;
        }

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("ArrowShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            p.Init(target, 9f, Damage, DamageSource.Tower, new Color(0.35f, 0.75f, 1f));
        }
    }
}
