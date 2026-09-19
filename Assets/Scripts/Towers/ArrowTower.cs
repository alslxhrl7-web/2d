using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 화살탑: 빠른 연사, 단일 대상, 표준 피해.
    /// [해설] 화살에 "약한 빙결(슬로우)" 효과가 붙어 있다 — 맞을 때마다 대상의 이동속도가
    /// 잠깐 느려진다. 빙결탑(IceTower)만큼 강하게 늦추지는 않지만, 연사 속도가 빨라서
    /// (0.6초마다 한 발) 사거리 안에 계속 있으면 슬로우가 거의 끊기지 않고 유지되는 효과를 낸다.
    /// </summary>
    public class ArrowTower : TowerBase
    {
        // [해설] Range: 공격 사거리(유닛), FireInterval: 한 발 쏘고 다음 발까지 걸리는 시간(초),
        // Damage: 한 발당 피해량. 화살탑은 세 타워 중 가장 빨리 쏘는 대신 한 방 피해는 가장 낮다.
        // 이 Damage가 실제로 적의 체력에서 깎이기까지 거치는 과정(전역 공격력 배율, 방패병 경감,
        // 단일 대상 vs 범위 판정)은 Projectile.Hit()의 주석에 세 타워를 묶어서 정리해 뒀다.
        public void Setup()
        {
            Type = TowerType.Arrow;
            // [해설] ★ 2.5D 전환에 맞춘 사거리 재조정(3.2 → 2.6). 바닥을 세로로 누르면서
            // 루프 중앙에서 위/아래 도로까지의 거리가 4.8에서 2.976으로 줄어, 사거리를 그대로
            // 두면 한가운데 타워 하나가 위아래 도로를 동시에 때렸다. 그래서 네 타워를 일괄
            // 하향했다. ※ 이후 길을 상하좌우 3칸씩 넓히면서 그 거리는 4.13~4.44로 다시 늘어
            //   났으므로, 지금은 원래 사거리(3.2 등)로 되돌려도 문제가 없다 — 되돌릴 때는
            //   BuildManager.TowerRangeFor의 값도 반드시 함께 고칠 것.
            Range = 2.6f;
            FireInterval = 0.6f;
            Damage = 9f;
        }

        // [해설] 화살에 붙는 슬로우 세기/지속시간을 이름 붙은 상수로 분리해 나중에 밸런스를
        // 조정할 때 이 두 값만 보면 되게 했다.
        private const float ArrowSlowFactor = 0.8f;    // 이동속도를 80%로 낮춘다 (20% 감속)
        private const float ArrowSlowDuration = 0.5f;  // 감속 지속 시간(초). 연사 간격(0.6초)보다
                                                         // 살짝 짧게 잡아 맞을 때마다 자연스럽게 갱신되게 한다.

        protected override void Fire(EnemyController target)
        {
            var go = new GameObject("ArrowShot");
            go.transform.position = MuzzlePoint; // 2.5D: 바닥이 아니라 탑 윗부분에서 발사
            var p = go.AddComponent<Projectile>();
            // splashRadius는 0f로 그대로 둬서(단일 대상 유지) 화살탑은 여전히 한 명만 노리되,
            // 그 한 명에게 약한 슬로우(ArrowSlowFactor/ArrowSlowDuration)를 함께 건다.
            // arrowVisual: true → 원 대신 실제 화살 모양이 대상을 향해 회전하며 날아간다.
            p.Init(target, 12f, EffectiveDamage, DamageSource.Tower, new Color(0.35f, 0.75f, 1f),
                0f, ArrowSlowFactor, ArrowSlowDuration, arrowVisual: true);
        }
    }
}
