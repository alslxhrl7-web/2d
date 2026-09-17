using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 기획서 03 PROGRESSION의 보스 5종 규칙을 구현한다.
    /// 1(5R): 직선 돌진 + 돌진 직후 약점 노출
    /// 2(10R): 주기적으로 타워 하나를 무력화 (맵 일부 봉쇄/타워 재배치 대응)
    /// 3(15R): 주기적으로 잡몹 소환
    /// 4(20R): 골드 약탈 + 골드 수급 방해 (경제 압박 — 거점이 없는 대신 자원을 직접 노린다)
    /// 5(25R): 체력 구간별 3페이즈, 이전 보스 패턴을 혼합
    /// </summary>
    public class BossController : EnemyController
    {
        public int BossIndex; // 1..5
        public string BossName;

        private WaveManager _waveManager;
        private GameManager _gameManager;
        private BuildManager _buildManager;

        private float _abilityTimer;
        private float _abilityInterval = 5f;
        private bool _charging;
        private float _bonusDamageWindow;
        private int _phase = 1;

        public void InitBoss(int bossIndex, string name, WaveManager wm, GameManager gm, BuildManager bm)
        {
            BossIndex = bossIndex;
            BossName = name;
            _waveManager = wm;
            _gameManager = gm;
            _buildManager = bm;
            _abilityTimer = 2f;

            // Init()에서 임시로 그려둔 도형(Diamond)을, 있으면 보스 번호별 전용 아트
            // (Sprites/Boss_1 ~ Boss_5)로 덮어씌운다. 없으면 도형을 그대로 사용한다.
            Sprite art = Resources.Load<Sprite>($"Sprites/Boss_{bossIndex}");
            if (art != null) ApplyArt(art, BossArtWorldHeight);
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) return;

            _abilityTimer -= Time.deltaTime;
            if (_abilityTimer <= 0f)
            {
                _abilityTimer = _abilityInterval;
                RunAbility();
            }

            if (BossIndex == 5)
            {
                int newPhase = HP > MaxHP * 0.66f ? 1 : HP > MaxHP * 0.33f ? 2 : 3;
                if (newPhase != _phase)
                {
                    _phase = newPhase;
                    Speed *= 1.25f;
                    _abilityInterval = Mathf.Max(1.5f, _abilityInterval * 0.7f);
                    _gameManager?.ShowBanner($"최종 보스 {_phase}페이즈 돌입!");
                }
            }
        }

        private void RunAbility()
        {
            switch (BossIndex)
            {
                case 1:
                    StartCoroutine(ChargeRoutine());
                    break;
                case 2:
                    _buildManager?.DisableRandomTowerBriefly(3.5f);
                    _gameManager?.ShowBanner("보스가 타워 하나를 무력화했습니다!");
                    break;
                case 3:
                    _waveManager?.SpawnBossAdds(3);
                    _gameManager?.ShowBanner("보스가 잡몹을 소환했습니다!");
                    break;
                case 4:
                    _gameManager?.StealGold(25);
                    _gameManager?.SuppressIncomeBriefly(5f);
                    _gameManager?.ShowBanner("보스가 골드를 약탈했습니다! (골드 수급 일시 정지)");
                    break;
                case 5:
                    _waveManager?.SpawnBossAdds(1);
                    if (_phase >= 2) _buildManager?.DisableRandomTowerBriefly(2.5f);
                    if (_phase >= 3) StartCoroutine(ChargeRoutine());
                    break;
            }
        }

        private IEnumerator ChargeRoutine()
        {
            if (_charging) yield break;
            _charging = true;
            float original = Speed;
            Speed *= 2.4f;
            SetVulnerable(true);
            yield return new WaitForSeconds(1.4f);
            Speed = original;
            SetVulnerable(false);
            _charging = false;
        }

        private void SetVulnerable(bool on)
        {
            _bonusDamageWindow = on ? 1f : 0f;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = on ? new Color(1f, 0.9f, 0.4f) : Color.white;
        }

        public override void TakeDamage(float amount, DamageSource source)
        {
            if (_bonusDamageWindow > 0f) amount *= 1.6f; // 돌진 직후 약점 노출: 추가 피해
            base.TakeDamage(amount, source);
        }
    }
}
