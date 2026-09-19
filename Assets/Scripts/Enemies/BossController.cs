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

        /// <summary>보스는 남은 체력이 곧 전투의 핵심 정보이므로, 가득 차 있어도 체력바를 계속 보여 준다.</summary>
        protected override bool AlwaysShowHpBar => true;

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
            // ★ 버그 수정: 2.5D 전환으로 스프라이트가 본체에서 자식("Visual")으로 옮겨가면서
            // GetComponent<SpriteRenderer>()가 늘 null을 돌려줬고, 그 결과 "지금 약점이 열렸다"는
            // 유일한 신호(노란빛)가 아예 안 나왔다. 부모 클래스가 이미 들고 있는 _sr을 쓴다.
            if (_sr != null) _sr.color = on ? new Color(1f, 0.9f, 0.4f) : Color.white;
        }

        /// <summary>
        /// ★ 약점 노출(1.6배)을 TakeDamage가 아니라 <b>ExpectedDamage</b>에서 곱한다.
        ///
        /// [해설] 예전에는 TakeDamage에서 곱했는데, 그러면 딜 누수 방지용 예약
        /// (Projectile이 발사 시점에 EnemyController.ExpectedDamage로 계산해 잡아 두는 값)이
        /// 실제 피해보다 37.5% 적게 잡힌다. 그 결과 취약 창 동안 보스의 EffectiveHP가 실제보다
        /// 높게 보여, 이미 죽을 보스에게 화살을 계속 쏘고 그 화살들이 허공에 사라진다 —
        /// 바로 이 시스템이 없애려던 누수다. ExpectedDamage 하나만 덮어쓰면 TakeDamage는
        /// 부모 구현이 이 함수를 거치므로 실제 피해와 예약이 자동으로 같아진다.
        /// </summary>
        public override float ExpectedDamage(float amount, DamageSource source)
        {
            if (_bonusDamageWindow > 0f) amount *= 1.6f; // 돌진 직후 약점 노출: 추가 피해
            return base.ExpectedDamage(amount, source);
        }
    }
}
