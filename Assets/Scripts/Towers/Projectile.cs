using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 타워/플레이어 공격의 투사체. 단일 대상 또는 스플래시(포격탑) 피해와
    /// 슬로우(빙결탑) 효과를 처리한다.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private EnemyController _target;
        private float _speed;
        private float _damage;
        private DamageSource _source;
        private float _splashRadius;
        private float _slowFactor;
        private float _slowDuration;

        public void Init(EnemyController target, float speed, float damage, DamageSource source, Color color,
            float splashRadius = 0f, float slowFactor = 1f, float slowDuration = 0f)
        {
            _target = target;
            _speed = speed;
            _damage = damage;
            _source = source;
            _splashRadius = splashRadius;
            _slowFactor = slowFactor;
            _slowDuration = slowDuration;

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(color, color, 20);
            sr.sortingOrder = 8;
            transform.localScale = Vector3.one * 0.22f;
        }

        private void Update()
        {
            if (_target == null || _target.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 dir = _target.transform.position - transform.position;
            float dist = dir.magnitude;
            float step = _speed * Time.deltaTime;
            if (step >= dist) Hit();
            else transform.position += dir.normalized * step;
        }

        private void Hit()
        {
            float mult = (_source == DamageSource.Tower) ? TowerBase.GlobalDamageMultiplier : 1f;

            if (_splashRadius > 0f)
            {
                foreach (var e in new List<EnemyController>(EnemyController.Active))
                {
                    if (e == null || e.IsDead) continue;
                    if (Vector2.Distance(e.transform.position, _target.transform.position) <= _splashRadius)
                    {
                        e.TakeDamage(_damage * mult, _source);
                        if (_slowDuration > 0f) e.ApplySlow(_slowFactor, _slowDuration);
                    }
                }
            }
            else
            {
                _target.TakeDamage(_damage * mult, _source);
                if (_slowDuration > 0f) _target.ApplySlow(_slowFactor, _slowDuration);
            }

            Destroy(gameObject);
        }
    }
}
