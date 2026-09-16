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
        private bool _isBossWave;
        private int _waveTotalCount;
        private Coroutine _spawnCoroutine;

        // 일반 웨이브당 스폰되는 적 수. 시간이 지나도(웨이브 도중) 알아서 사라지는 적은 없고,
        // 오직 처치되거나 거점에 도달했을 때만 카운트가 줄어든다. 동시 생존 적이 이 수를
        // 넘어서면(=플레이어가 감당하지 못하고 밀린 것으로 간주) 즉시 패배 처리한다.
        private const int WaveEnemyCount = 50;
        private const int MaxAliveEnemies = 50;

        // 스폰 간격(초). 예전보다 더 촘촘하게 몰아쳐서 나오도록 축소했다.
        private const float FirstSpawnDelay = 0.18f;
        private const float BaseSpawnGap = 0.55f;
        private const float MinSpawnGap = 0.22f;
        private const float SpawnGapWaveDecay = 0.01f;

        // 웨이브 스킵: 일반 웨이브에서 이 비율 이상 처치하면 남은 스폰/생존 적을 정리하고
        // 즉시 웨이브 클리어 처리한다 (보스 웨이브는 스킵 불가).
        private const float SkipKillRatio = 0.6f;

        public int KilledThisWave { get; private set; }
        public bool IsBossWave => _isBossWave;
        public int SkipKillThreshold => _isBossWave ? 0 : Mathf.CeilToInt(_waveTotalCount * SkipKillRatio);
        public bool CanSkipWave => WaveInProgress && !_isBossWave && _waveTotalCount > 0 && KilledThisWave >= SkipKillThreshold;

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
            ApplyPathForWave(CurrentWave); // [해설] 스테이지(웨이브)마다 길 모양을 바꾼다 (아래 메서드 설명 참고)
            WaveDefinition def = BuildWave(CurrentWave);
            _isBossWave = def.IsBoss;
            _waveTotalCount = def.Entries.Count;
            KilledThisWave = 0;
            OnWaveStarted?.Invoke(CurrentWave);
            if (def.IsBoss)
                OnBossIncoming?.Invoke(def.BossIndex, BossNames[def.BossIndex - 1]);
            _spawnCoroutine = StartCoroutine(SpawnRoutine(def));
        }

        /// <summary>
        /// [해설] 이번 웨이브에 쓸 길 도안을 PathLibrary에서 받아와 Path의 WaypointsA/B "필드 자체"를
        /// 새 리스트로 바꿔치기한다 (리스트 내용을 고치는 게 아니라 필드를 통째로 교체하는 방식).
        /// 이렇게 하는 이유: 이미 스폰돼서 화면을 걷고 있는 적은 스폰될 때 그 시점의 리스트 "참조"를
        /// 직접 들고 이동한다(EnemyController.Waypoints). 필드를 새 리스트로 바꿔도 옛 리스트 자체는
        /// 사라지지 않고 그 적이 계속 들고 있으므로, 걷고 있던 적이 갑자기 다른 방향으로 튀는 일 없이
        /// 원래 가던 길을 마저 걸어간다. 반대로 이 시점 "이후"에 새로 스폰되는 적(Path.GetPath 호출)은
        /// 새로 바뀐 리스트를 받아 새 길로 걷게 된다.
        /// 화면에 보이는 도로/스폰 마커도 GameBootstrapper.RedrawPathVisuals로 같이 다시 그려서
        /// 시각적으로도 실제 이동 경로와 항상 일치하게 맞춘다.
        /// </summary>
        private void ApplyPathForWave(int wave)
        {
            var template = PathLibrary.GetForWave(wave);
            Path.WaypointsA = template.WaypointsA;
            Path.WaypointsB = template.WaypointsB;
            GameBootstrapper.RedrawPathVisuals(Path);
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

            int count = WaveEnemyCount;
            bool useTwoPaths = wave >= 11;
            for (int i = 0; i < count; i++)
            {
                EnemyType type = PickTypeForWave(wave, i);
                int pathIndex = useTwoPaths ? (i % 2) : 0;
                float gap = (i == 0) ? FirstSpawnDelay : Mathf.Max(MinSpawnGap, BaseSpawnGap - wave * SpawnGapWaveDecay);
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

            // 웨이브가 지날수록 적이 전반적으로 강해지도록 체력/이동속도/거점 피해량/보상을
            // 모두 웨이브 번호에 비례해서 키운다. (1웨이브 기준 배율 1.0)
            float hpScale = 1f + (CurrentWave - 1) * 0.08f;
            float speedScale = 1f + Mathf.Min(0.6f, (CurrentWave - 1) * 0.02f);
            float dmgScale = 1f + (CurrentWave - 1) * 0.04f;
            float goldScale = 1f + (CurrentWave - 1) * 0.03f;

            if (type == EnemyType.Boss)
            {
                var boss = go.AddComponent<BossController>();
                float baseHp = (260f + bossIndex * 180f) * (1f + (bossIndex - 1) * 0.1f);
                int bossGold = Mathf.RoundToInt((60 + bossIndex * 30) * goldScale); // 처치 보상 절반으로 축소
                boss.Init(EnemyType.Boss, baseHp, 1.1f * speedScale, bossGold, 10, wp,
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
                        // [해설] 기본 체력을 27로 맞춰서, 1웨이브 기준(hpScale=1) 화살탑(공격력 9)에
                        // 정확히 3번 맞으면 죽도록(9×3=27) 1차 밸런스 기준점을 잡았다. 처치 보상은 절반으로 축소(4 → 2).
                        ec.Init(type, 27f * hpScale, 1.5f * speedScale, Mathf.RoundToInt(2f * goldScale),
                            Mathf.CeilToInt(1 * dmgScale), wp, new Color(0.85f, 0.3f, 0.3f), Color.white);
                        break;
                    case EnemyType.Charger:
                        // 처치 보상 절반으로 축소 (6 → 3)
                        ec.Init(type, 22f * hpScale, 2.6f * speedScale, Mathf.RoundToInt(3f * goldScale),
                            Mathf.CeilToInt(1 * dmgScale), wp, new Color(0.95f, 0.55f, 0.2f), Color.white);
                        break;
                    case EnemyType.Shield:
                        // 처치 보상 절반으로 축소 (9 → 4.5)
                        ec.Init(type, 55f * hpScale, 0.9f * speedScale, Mathf.RoundToInt(4.5f * goldScale),
                            Mathf.CeilToInt(2 * dmgScale), wp, new Color(0.55f, 0.35f, 0.85f), Color.white);
                        break;
                }
            }

            AliveEnemies++;
            ec.OnDied += HandleEnemyRemoved;
            ec.OnReachedBase += HandleEnemyReachedBase;

            if (AliveEnemies > MaxAliveEnemies)
            {
                Game.ShowBanner($"동시 생존 적 {MaxAliveEnemies}마리 초과! 적에게 압도당했습니다.");
                Game.TriggerOverwhelmDefeat();
            }
        }

        private void HandleEnemyRemoved(EnemyController e)
        {
            Game.AddGold(e.GoldReward);
            if (e is BossController bc) Game.ShowBanner($"{bc.BossName} 처치!");
            AliveEnemies--;
            KilledThisWave++;
            CheckWaveClear();
        }

        private void HandleEnemyReachedBase(EnemyController e)
        {
            Game.DamageBase(e.DamageToBase);
            AliveEnemies--;
            CheckWaveClear();
        }

        /// <summary>
        /// 일정 수 이상 처치하면 즉시 웨이브를 클리어 처리한다 (보상 선택 → 다음 웨이브로 바로 진행).
        /// 화면에 남아있는 적은 강제로 없애지 않고 그대로 둔다 — 계속 이동/전투를 이어가며, 처치되거나
        /// 거점에 도달하면 그때그때 골드/피해가 평소처럼 정상 반영된다(다음 웨이브와 함께 공존).
        /// 앞으로 예정돼 있던 미스폰 물량만 취소한다.
        /// </summary>
        public void SkipWave()
        {
            if (!CanSkipWave) return;

            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
            _allSpawned = true;

            Game.ShowBanner("웨이브 스킵!");
            WaveInProgress = false;
            OnWaveCleared?.Invoke(CurrentWave);
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
