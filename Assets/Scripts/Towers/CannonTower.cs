using UnityEngine;

namespace Defense2D
{
    /// <summary>포격탑: 느린 연사, 착탄 지점 스플래시 피해.</summary>
    public class CannonTower : TowerBase
    {
        public void Setup()
        {
            Type = TowerType.Cannon;
            Range = 2.9f;
            FireInterval = 1.5f;
            Damage = 14f;
        }

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("CannonShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            p.Init(target, 6f, Damage, DamageSource.Tower, new Color(1f, 0.55f, 0.25f), 1.15f);
        }
    }
}
