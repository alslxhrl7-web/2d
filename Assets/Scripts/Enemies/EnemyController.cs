using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 적 공통 로직: 경로를 따라 이동, 체력/피격, 거점 도달, 사망 보상.
    /// 잡몹(원)/돌진형(삼각)/방패병(사각)은 실루엣과 스탯만 다르다.
    /// 방패병은 "타워 공격을 버팀" 설정에 따라 타워 피해만 경감받는다.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        public static readonly List<EnemyController> Active = new List<EnemyController>();

        public EnemyType Type;
        public float MaxHP;
        public float HP;
        public float Speed;
        public int GoldReward;
        public int DamageToBase = 1;
        public float ShieldTowerDamageReduction = 0f;

        [NonSerialized] public List<Vector3> Waypoints;
        private int _waypointIndex;
        protected SpriteRenderer _sr;
        private Transform _hpFillTf;

        private const float EnemyArtWorldHeight = 1.1f;
        protected const float BossArtWorldHeight = 2.0f;

        private float _slowTimer;
        private float _slowFactor = 1f;
        private float _stunTimer;

        public bool IsDead { get; private set; }

        public event Action<EnemyController> OnDied;
        public event Action<EnemyController> OnReachedBase;

        public float PathProgress =>
            (Waypoints != null && Waypoints.Count > 1) ? _waypointIndex / (float)(Waypoints.Count - 1) : 0f;

        public void Init(EnemyType type, float maxHp, float speed, int goldReward, int damageToBase,
            List<Vector3> waypoints, Color fill, Color outline)
        {
            Type = type;
            MaxHP = maxHp;
            HP = maxHp;
            Speed = speed;
            GoldReward = goldReward;
            DamageToBase = damageToBase;
            Waypoints = waypoints;
            transform.position = waypoints[0];

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 5;

            // 실제 아트(Assets/Resources/Sprites/Enemy_Mob.png 등)가 있으면 그것을 쓰고,
            // 없으면 기존 도형(SpriteFactory)으로 대체한다. 보스는 이 시점엔 아직 BossIndex를
            // 모르므로 일단 도형으로 그려두고, InitBoss()에서 보스 전용 아트로 덮어씌운다.
            Sprite art = type != EnemyType.Boss ? Resources.Load<Sprite>($"Sprites/Enemy_{type}") : null;
            if (art != null)
            {
                ApplyArt(art, EnemyArtWorldHeight);
            }
            else
            {
                _sr.sprite = type switch
                {
                    EnemyType.Mob => SpriteFactory.Circle(fill, outline),
                    EnemyType.Charger => SpriteFactory.Triangle(fill, outline),
                    EnemyType.Shield => SpriteFactory.Square(fill, outline),
                    _ => SpriteFactory.Diamond(fill, outline)
                };
                float scale = type == EnemyType.Shield ? 0.62f : type == EnemyType.Charger ? 0.5f : 0.55f;
                transform.localScale = Vector3.one * scale;
            }

            if (type == EnemyType.Shield) ShieldTowerDamageReduction = 0.5f;

            BuildHpBar();
        }

        private void BuildHpBar()
        {
            var bg = new GameObject("HPBarBG");
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(0, 0.55f, 0);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = SpriteFactory.SolidSquare(new Color(0, 0, 0, 0.6f));
            bgSr.sortingOrder = 6;
            bg.transform.localScale = new Vector3(0.9f, 0.12f, 1f);

            var fg = new GameObject("HPBarFG");
            fg.transform.SetParent(transform, false);
            fg.transform.localPosition = new Vector3(0, 0.55f, -0.01f);
            var fgSr = fg.AddComponent<SpriteRenderer>();
            fgSr.sprite = SpriteFactory.SolidSquare(new Color(0.85f, 0.2f, 0.2f, 1f));
            fgSr.sortingOrder = 7;
            fg.transform.localScale = new Vector3(0.86f, 0.09f, 1f);
            _hpFillTf = fg.transform;
        }

        /// <summary>실제 아트 스프라이트를 적용하고, 지정한 월드 높이에 맞춰 스케일을 보정한다.
        /// (기존 도형은 흰색 틴트를 쓰지 않으므로, 실제 아트 적용 시 색을 흰색으로 리셋한다.)</summary>
        protected void ApplyArt(Sprite art, float desiredWorldHeight)
        {
            _sr.sprite = art;
            _sr.color = Color.white;
            float scale = desiredWorldHeight / art.bounds.size.y;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        public void ApplyStun(float duration)
        {
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        public virtual void TakeDamage(float amount, DamageSource source)
        {
            if (IsDead) return;
            if (source == DamageSource.Tower && ShieldTowerDamageReduction > 0f)
                amount *= (1f - ShieldTowerDamageReduction);

            HP -= amount;
            if (_hpFillTf != null)
                _hpFillTf.localScale = new Vector3(0.86f * Mathf.Clamp01(HP / MaxHP), 0.09f, 1f);

            if (HP <= 0f) Die();
        }

        protected virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        protected virtual void Update()
        {
            if (IsDead) return;

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f) _slowFactor = 1f;
            }

            if (_waypointIndex >= Waypoints.Count - 1)
            {
                ReachBase();
                return;
            }

            Vector3 target = Waypoints[_waypointIndex + 1];
            Vector3 dir = target - transform.position;
            float dist = dir.magnitude;
            float step = Speed * _slowFactor * Time.deltaTime;
            if (step >= dist)
            {
                transform.position = target;
                _waypointIndex++;
            }
            else
            {
                transform.position += dir.normalized * step;
            }
        }

        protected void ReachBase()
        {
            if (IsDead) return;
            IsDead = true;
            OnReachedBase?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
