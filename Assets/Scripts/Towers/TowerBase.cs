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

        /// <summary>이 타워의 "평상시" 색. 설치 직후 BuildManager가 채워 준다.
        /// 철거 호버 강조(빨강)와 보스의 무력화 연출(회색)이 끝나면 둘 다 이 색으로 되돌린다 —
        /// 원래 색의 출처를 한 곳으로 모아 두 연출이 서로의 색을 덮어쓰는 사고를 막는다.</summary>
        public Color BaseColor { get; set; } = Color.white;

        private float _cooldown;
        private float _disableTimer;
        private Coroutine _flashRoutine;

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        /// <summary>
        /// 보스 능력으로 이 타워를 seconds초 동안 멈춘다.
        /// [해설] ★ 버그 수정. 예전에는 호출할 때마다 WaitForSeconds(seconds) 코루틴을 새로 띄웠다.
        /// 그런데 보스 5의 3페이즈는 능력 간격이 2.45초까지 줄어드는데 무력화 시간은 2.5초라,
        /// 같은 타워가 연달아 찍히면 코루틴 두 개가 겹친다. 두 번째 코루틴은 "시작할 때의 색"으로
        /// 이미 회색을 집어 든 상태이므로, 첫 번째가 흰색을 돌려놓은 뒤 두 번째가 다시 회색을
        /// 덮어써서 <b>멀쩡히 작동하는 타워가 영영 회색으로 굳는</b> 일이 벌어졌다.
        /// 이제 코루틴은 항상 하나만 돌고, 복원 시점도 WaitForSeconds가 아니라 실제 _disableTimer가
        /// 0이 되는 순간에 맞춘다(무력화가 연장되면 연출도 같이 연장된다).
        /// </summary>
        public void SetDisabledFor(float seconds)
        {
            _disableTimer = Mathf.Max(_disableTimer, seconds);

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (_flashRoutine != null) return; // 이미 회색 연출 중이면 타이머만 늘리고 끝낸다
            _flashRoutine = StartCoroutine(FlashDisabled(sr));
        }

        private IEnumerator FlashDisabled(SpriteRenderer sr)
        {
            sr.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
            while (_disableTimer > 0f) yield return null; // Update()가 매 프레임 줄여 준다
            if (sr != null) sr.color = BaseColor;
            _flashRoutine = null;
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
