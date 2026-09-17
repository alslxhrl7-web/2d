using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 타워/플레이어 공격의 투사체. 단일 대상 또는 스플래시(포격탑) 피해와
    /// 슬로우(빙결탑) 효과를 처리한다.
    /// [해설] arrowVisual/cannonballVisual/impactEffect는 순수 연출용 옵션이다. arrowVisual이
    /// 켜지면 원 대신 화살 모양 스프라이트를 쓰고 진행 방향으로 계속 회전시킨다(화살탑).
    /// cannonballVisual이 켜지면 색칠한 원 대신 검은 포탄 스프라이트를 쓴다(포격탑).
    /// impactEffect는 명중 순간 ImpactEffect에 서리 장판(빙결탑)이나 폭발(포격탑) 연출을 맡긴다.
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
        private bool _arrowVisual;
        private bool _cannonballVisual;
        private ImpactEffectKind _impactEffect;

        public void Init(EnemyController target, float speed, float damage, DamageSource source, Color color,
            float splashRadius = 0f, float slowFactor = 1f, float slowDuration = 0f,
            bool arrowVisual = false, ImpactEffectKind impactEffect = ImpactEffectKind.None,
            bool cannonballVisual = false)
        {
            _target = target;
            _speed = speed;
            _damage = damage;
            _source = source;
            _splashRadius = splashRadius;
            _slowFactor = slowFactor;
            _slowDuration = slowDuration;
            _arrowVisual = arrowVisual;
            _impactEffect = impactEffect;
            _cannonballVisual = cannonballVisual;

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;
            if (_arrowVisual)
            {
                // 화살 스프라이트는 세로로 긴 텍스처라, 균등 스케일을 줘야 비율이 안 찌그러진다.
                sr.sprite = SpriteFactory.Arrow(color, Color.white);
                transform.localScale = Vector3.one * 0.2f;
            }
            else if (_cannonballVisual)
            {
                // 검은 포탄은 색상 인자(color)를 쓰지 않고 전용 스프라이트를 그대로 쓴다.
                sr.sprite = SpriteFactory.Cannonball();
                transform.localScale = Vector3.one * 0.26f;
            }
            else
            {
                sr.sprite = SpriteFactory.Circle(color, color, 20);
                transform.localScale = Vector3.one * 0.22f;
            }
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

            if (_arrowVisual && dir.sqrMagnitude > 0.0001f)
                transform.up = dir.normalized; // 화살촉이 항상 진행 방향(대상 쪽)을 향하도록 회전

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

            switch (_impactEffect)
            {
                case ImpactEffectKind.FrostZone:
                    ImpactEffect.SpawnFrostZone(_target.transform.position,
                        _splashRadius > 0f ? _splashRadius : 0.9f, _slowDuration > 0f ? _slowDuration : 1.2f);
                    break;
                case ImpactEffectKind.Explosion:
                    ImpactEffect.SpawnExplosion(_target.transform.position, _splashRadius > 0f ? _splashRadius : 0.9f);
                    break;
            }

            Destroy(gameObject);
        }
    }
}
