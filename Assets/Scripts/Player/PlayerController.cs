using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Defense2D
{
    /// <summary>
    /// 플레이어 "수호자": WASD/방향키 이동, 사거리 내 자동 원거리 공격,
    /// SPACE로 행동력 1을 소모해 광역 기절 스킬 사용. (기획서 04 CONTENT)
    /// 이 프로젝트는 새 Input System 전용(activeInputHandler=1)이라
    /// 레거시 Input 클래스 대신 Keyboard/Mouse.current를 사용한다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float MoveSpeed = 4.2f;
        public float AttackRange = 2.4f;
        public float AttackInterval = 0.5f;
        public float AttackDamage = 6f;
        public float SkillRadius = 2.6f;
        public float SkillStunDuration = 1.6f;
        public float ApRegenSeconds = GameConstants.ActionPointRegenSeconds;

        public int ActionPoints { get; private set; } = GameConstants.MaxActionPoints;
        public int MaxActionPoints = GameConstants.MaxActionPoints;

        public event Action OnActionPointsChanged;

        private float _attackCooldown;
        private float _apRegenTimer;

        private void Awake()
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Capsule(new Color(0.95f, 0.95f, 0.85f), new Color(0.15f, 0.55f, 0.85f));
            sr.sortingOrder = 6;
            transform.localScale = Vector3.one * 0.6f;
        }

        private void Update()
        {
            HandleMove();
            HandleAttack();
            HandleSkill();
            HandleApRegen();
        }

        private void HandleMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;

            if (dir.sqrMagnitude > 0f)
            {
                dir.Normalize();
                Vector3 next = transform.position + (Vector3)(dir * MoveSpeed * Time.deltaTime);
                next.x = Mathf.Clamp(next.x, -GameConstants.WorldHalfWidth, GameConstants.WorldHalfWidth);
                next.y = Mathf.Clamp(next.y, -GameConstants.WorldHalfHeight, GameConstants.WorldHalfHeight);
                transform.position = next;
            }
        }

        private void HandleAttack()
        {
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown > 0f) return;

            EnemyController target = FindNearestEnemy();
            if (target == null) return;

            _attackCooldown = AttackInterval;
            var go = new GameObject("PlayerShot");
            go.transform.position = transform.position;
            var p = go.AddComponent<Projectile>();
            p.Init(target, 11f, AttackDamage, DamageSource.Player, new Color(1f, 0.9f, 0.3f));
        }

        private EnemyController FindNearestEnemy()
        {
            EnemyController best = null;
            float bestDist = AttackRange;
            foreach (var e in EnemyController.Active)
            {
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        private void HandleSkill()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame && ActionPoints > 0)
            {
                ActionPoints--;
                OnActionPointsChanged?.Invoke();

                foreach (var e in new List<EnemyController>(EnemyController.Active))
                {
                    if (e == null || e.IsDead) continue;
                    if (Vector2.Distance(transform.position, e.transform.position) <= SkillRadius)
                        e.ApplyStun(SkillStunDuration);
                }
            }
        }

        private void HandleApRegen()
        {
            if (ActionPoints >= MaxActionPoints) return;
            _apRegenTimer += Time.deltaTime;
            if (_apRegenTimer >= ApRegenSeconds)
            {
                _apRegenTimer = 0f;
                ActionPoints = Mathf.Min(MaxActionPoints, ActionPoints + 1);
                OnActionPointsChanged?.Invoke();
            }
        }

        public void RefillActionPointsPartial(int amount)
        {
            ActionPoints = Mathf.Min(MaxActionPoints, ActionPoints + amount);
            OnActionPointsChanged?.Invoke();
        }
    }
}
