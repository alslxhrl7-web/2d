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

        /// <summary>
        /// [해설] ★ 타워 타입별 피해가 실제로 "들어가는" 지점이다. 세 타워는 각자 전용 공격 코드를
        /// 갖고 있는 게 아니라, 전부 이 메서드 하나를 거쳐 간다. 무엇이 타입을 가르냐면 —
        /// 각 타워의 Fire()가 Init(...)에 넘긴 <b>_splashRadius 값 하나</b>다.
        ///
        ///   화살탑(ArrowTower)   _splashRadius = 0    → 아래 else 분기. 맞은 적 <b>한 명만</b>
        ///                        9 피해. 연사가 빨라(0.6초) 약한 슬로우가 거의 끊기지 않는다.
        ///   빙결탑(IceTower)     _splashRadius = 1.3  → 아래 if 분기. 착탄 지점 반경 1.3 안의
        ///                        <b>모든 적</b>에게 각각 3 피해 + 강한 슬로우(50%, 1.8초).
        ///   포격탑(CannonTower)  _splashRadius = 1.4  → 같은 if 분기. 반경 1.4 안의 <b>모든 적</b>
        ///                        에게 각각 26 피해(슬로우 없음).
        ///
        /// 범위 공격은 거리에 따른 감쇠가 없다 — 반경 안에 있기만 하면 전원이 동일하게 Damage를
        /// 전부 받는다. 그래서 포격탑은 적이 몰린 곳에 쏠수록 총 피해가 배로 늘어난다.
        ///
        /// 한 대 맞을 때 적이 실제로 잃는 체력은 다음 세 단계를 모두 거친 값이다:
        ///   ① 타워가 정한 기본 피해        — 각 타워 Setup()의 Damage (9 / 3 / 26)
        ///   ② × 전역 공격력 배율            — 아래 mult. 보상 "타워 강화"를 고를 때마다 ×1.2
        ///                                     (TowerBase.GlobalDamageMultiplier, 타워 공격에만 적용)
        ///   ③ × 방패병 경감                 — 대상이 방패병이면 타워 피해만 50% 경감
        ///                                     (EnemyController.TakeDamage에서 처리)
        /// 예) 방패병이 "타워 강화"를 한 번 고른 뒤 포격탑에 맞으면 26 × 1.2 × 0.5 = 15.6 피해.
        /// </summary>
        private void Hit()
        {
            // ②단계: 타워가 쏜 것만 전역 공격력 배율을 받는다(보스 소환물 등 타워가 아닌 피해원은 제외).
            float mult = (_source == DamageSource.Tower) ? TowerBase.GlobalDamageMultiplier : 1f;

            if (_splashRadius > 0f)
            {
                // 범위형(빙결탑·포격탑): 착탄 지점 주변의 적을 전부 훑어서 각각에게 같은 피해를 준다.
                // 원본 리스트를 그대로 순회하면 피해로 적이 죽으면서 Active 목록이 바뀌어 예외가 나므로,
                // 복사본을 만들어 순회한다.
                foreach (var e in new List<EnemyController>(EnemyController.Active))
                {
                    if (e == null || e.IsDead) continue;
                    if (Vector2.Distance(e.transform.position, _target.transform.position) <= _splashRadius)
                    {
                        e.TakeDamage(_damage * mult, _source);
                        // 슬로우는 넘어온 지속시간이 0보다 클 때만 건다 → 빙결탑은 걸고, 포격탑은 안 건다.
                        if (_slowDuration > 0f) e.ApplySlow(_slowFactor, _slowDuration);
                    }
                }
            }
            else
            {
                // 단일 대상형(화살탑): 조준했던 그 적에게만 피해와 약한 슬로우를 준다.
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
