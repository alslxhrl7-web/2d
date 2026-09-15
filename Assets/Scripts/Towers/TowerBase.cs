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
        public static float GlobalDamageMultiplier = 1f;
        public static readonly List<TowerBase> Active = new List<TowerBase>();

        public TowerType Type;
        public float Range = 2.6f;
        public float FireInterval = 1f;
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
