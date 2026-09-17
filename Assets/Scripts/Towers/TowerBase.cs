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
        /// <summary>모든 타워에 공통으로 곱해지는 공격력 배율. 웨이브 보상 "타워 강화"를 고를
        /// 때마다 ×1.2로 누적된다(GameManager.ApplyUpgrade). 실제로 곱해지는 곳은
        /// Projectile.Hit()이며, 타워가 쏜 피해에만 적용된다.
        /// static이라 이미 세워둔 타워까지 전부 소급 적용된다.</summary>
        public static float GlobalDamageMultiplier = 1f;
        public static readonly List<TowerBase> Active = new List<TowerBase>();

        public TowerType Type;
        public float Range = 2.6f;       // 공격 사거리(월드 유닛)
        public float FireInterval = 1f;  // 발사 간격(초)

        /// <summary>이 타워가 한 발에 주는 기본 피해량. 각 타워의 Setup()에서 정한다
        /// (화살탑 9 / 빙결탑 3 / 포격탑 26). 이 값이 단일 대상에게 들어갈지 범위 안 전원에게
        /// 들어갈지는 각 타워의 Fire()가 Projectile에 넘기는 splashRadius가 결정한다 —
        /// 타입별 피해 방식 전체 설명은 Projectile.Hit() 주석 참고.</summary>
        public float Damage = 10f;

        private float _cooldown;
        private float _disableTimer;

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        public void SetDisabledFor(float seconds)
        {
            _disableTimer = Mathf.Max(_disableTimer, seconds);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) StartCoroutine(FlashDisabled(seconds, sr));
        }

        private IEnumerator FlashDisabled(float seconds, SpriteRenderer sr)
        {
            Color orig = sr.color;
            sr.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
            yield return new WaitForSeconds(seconds);
            if (sr != null) sr.color = orig;
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

        protected EnemyController FindTarget()
        {
            EnemyController best = null;
            float bestProgress = -1f;
            foreach (var e in EnemyController.Active)
            {
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d > Range) continue;
                if (e.PathProgress > bestProgress)
                {
                    bestProgress = e.PathProgress;
                    best = e;
                }
            }
            return best;
        }

        protected abstract void Fire(EnemyController target);
    }
}
