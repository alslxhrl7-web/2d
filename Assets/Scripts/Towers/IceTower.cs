using UnityEngine;

namespace Defense2D
{
    /// <summary>빙결탑: 낮은 피해, 대신 슬로우로 경로 통과 속도를 늦춘다.</summary>
    public class IceTower : TowerBase
    {
        public void Setup()
        {
            Type = TowerType.Ice;
            Range = 2.6f;
            FireInterval = 1.1f;
            Damage = 3f;
        }

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("IceShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            p.Init(target, 7f, Damage, DamageSource.Tower, new Color(0.6f, 0.95f, 1f), 0f, 0.5f, 1.8f);
        }
    }
}
