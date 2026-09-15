using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 25웨이브 진행을 절차적으로 생성한다. (기획서 03 PROGRESSION)
    /// 1-4 기본 잡몹 → 6-9 돌진형 합류 → 11-14 방패병+2진입로 → 16-19 강화 → 21-24 전 타입 → 25 최종.
    /// 5의 배수 웨이브는 보스. 웨이브 클리어 조건은 "스폰 완료 + 생존 적 0"이다.
    /// (기획서의 25분/40분 타이머는 제작 규모 참고용 메모로, 프로토타입은 명확한 클리어 판정을 위해
    ///  시간 제한 대신 전멸 조건을 사용한다.)
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public PathData Path;
        public GameManager Game;
        public BuildManager Build;

        public int CurrentWave { get; private set; } = 0;
        public int AliveEnemies { get; private set; } = 0;
        public bool WaveInProgress { get; private set; } = false;

        private bool _allSpawned;

        private static readonly string[] BossNames =
        {
            "돌진 보스", "봉쇄 보스", "소환 보스", "압박 보스", "최종 보스"
        };

        public System.Action<int> OnWaveStarted;
        public System.Action<int> OnWaveCleared;
        public System.Action<int, string> OnBossIncoming;

        public void BeginNextWave()
        {
            CurrentWave++;
            WaveDefinition def = BuildWave(CurrentWave);
            OnWaveStarted?.Invoke(CurrentWave);
            if (def.IsBoss)
                OnBossIncoming?.Invoke(def.BossIndex, BossNames[def.BossIndex - 1]);
            StartCoroutine(SpawnRoutine(def));
        }

        private WaveDefinition BuildWave(int wave)
        {
            var def = new WaveDefinition { WaveNumber = wave };

            if (wave % GameConstants.BossWaveInterval == 0)
            {
                def.IsBoss = true;
                def.BossIndex = wave / GameConstants.BossWaveInterval;
                def.Entries.Add(new SpawnEntry { Type = EnemyType.Boss, PathIndex = 0, Delay = 0.5f });
                return def;
            }

            int count = 6 + wave;
            bool useTwoPaths = wave >= 11;
            for (int i = 0; i < count; i++)
            {
                EnemyType type = PickTypeForWave(wave, i);
                int pathIndex = useTwoPaths ? (i % 2) : 0;
                float gap = (i == 0) ? 0.3f : Mathf.Max(0.35f, 0.85f - wave * 0.01f);
                def.Entries.Add(new SpawnEntry { Type = type, PathIndex = pathIndex, Delay = gap });
            }
            return def;
        }

        private EnemyType PickTypeForWave(int wave, int i)
        {
            if (wave <= 4) return EnemyType.Mob;
            if (wave <= 9) return (i % 3 == 0) ? EnemyType.Charger : EnemyType.Mob;
            if (wave <= 14) return (i % 4 == 0) ? EnemyType.Shield : (i % 3 == 0) ? EnemyType.Charger : EnemyType.Mob;
            if (wave <= 19) return (i % 3 == 0) ? EnemyType.Shield : (i % 2 == 0) ? EnemyType.Charger : EnemyType.Mob;
            return (EnemyType)(i % 3); // 21-24: 모든 적 타입 등장
        }

        private IEnumerator SpawnRoutine(WaveDefinition def)
        {
            WaveInProgress = true;
            _allSpawned = false;
            foreach (var entry in def.Entries)
            {
                yield return new WaitForSeconds(entry.Delay);
                SpawnEnemy(entry.Type, entry.PathIndex, def.IsBoss ? def.BossIndex : 0);
            }
            _allSpawned = true;
            CheckWaveClear();
        }

        private void SpawnEnemy(EnemyType type, int pathIndex, int bossIndex)
        {
            var wp = Path.GetPath(pathIndex);
            var go = new GameObject($"Enemy_{type}");
            EnemyController ec;

            float hpScale = 1f + (CurrentWave - 1) * 0.06f;
            float speedScale = 1f + Mathf.Min(0.4f, (CurrentWave - 1) * 0.01f);

            if (type == EnemyType.Boss)
            {
                var boss = go.AddComponent<BossController>();
                float baseHp = (260f + bossIndex * 180f) * (1f + (bossIndex - 1) * 0.1f);
                boss.Init(EnemyType.Boss, baseHp, 1.1f * speedScale, 120 + bossIndex * 60, 10, wp,
                    new Color(0.75f, 0.2f, 0.75f), Color.white);
                boss.InitBoss(bossIndex, BossNames[bossIndex - 1], this, Game, Build);
                ec = boss;
            }
            else
            {
                ec = go.AddComponent<EnemyController>();
                switch (type)
                {
                    case EnemyType.Mob:
                        ec.Init(type, 18f * hpScale, 1.5f * speedScale, 4, 1, wp,
                            new Color(0.85f, 0.3f, 0.3f), Color.white);
                        break;
                    case EnemyType.Charger:
                        ec.Init(type, 22f * hpScale, 2.6f * speedScale, 6, 1, wp,
                            new Color(0.95f, 0.55f, 0.2f), Color.white);
                        break;
                    case EnemyType.Shield:
                        ec.Init(type, 55f * hpScale, 0.9f * speedScale, 9, 2, wp,
                            new Color(0.55f, 0.35f, 0.85f), Color.white);
                        break;
                }
            }

            AliveEnemies++;
            ec.OnDied += HandleEnemyRemoved;
            ec.OnReachedBase += HandleEnemyReachedBase;
        }

        private void HandleEnemyRemoved(EnemyController e)
        {
            Game.AddGold(e.GoldReward);
            if (e is BossController bc) Game.ShowBanner($"{bc.BossName} 처치!");
            AliveEnemies--;
            CheckWaveClear();
        }

        private void HandleEnemyReachedBase(EnemyController e)
        {
            Game.DamageBase(e.DamageToBase);
            AliveEnemies--;
            CheckWaveClear();
        }

        private void CheckWaveClear()
        {
            if (_allSpawned && AliveEnemies <= 0 && WaveInProgress)
            {
                WaveInProgress = false;
                OnWaveCleared?.Invoke(CurrentWave);
            }
        }

        /// <summary>소환형 보스(3번, 5번)가 추가 잡몹을 불러올 때 사용.</summary>
        public void SpawnBossAdds(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(EnemyType.Mob, 0, 0);
            }
        }
    }
}
