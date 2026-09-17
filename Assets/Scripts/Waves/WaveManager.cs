using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 웨이브 진행을 절차적으로 생성한다. (기획서 03 PROGRESSION 기반, 이후 스테이지 구조로 개편)
    /// [해설] "길은 고정하고 스테이지 형식으로" 요청에 따라 25웨이브 단일 진행에서
    /// "25웨이브 = 1스테이지, 총 3스테이지(GameConstants.TotalStages)"로 개편했다.
    /// - 길: 스테이지 안에서는 고정되고, 스테이지가 바뀔 때만 새 길(PathLibrary.GetForStage)로 교체된다.
    /// - 보스: 스테이지 중간(10, 20웨이브 — GameConstants.BossIntervalWaves 주기)은 보스 단독 웨이브,
    ///   스테이지 마지막 웨이브(25 — GameConstants.WavesPerStage)는 "피날레"로 보스와 일반 유닛 무리가
    ///   함께 몰아친다. 피날레는 제한시간(StageFinaleBossTimeLimit) 안에 보스를 못 잡으면 화면에 남은
    ///   적 수와 상관없이 즉시 게임오버 처리된다(아래 Update 참고).
    /// - 웨이브 번호는 스테이지마다 1로 리셋해서 표시하지만(UIManager), 내부적으로는 CurrentWave가
    ///   게임 시작부터 계속 증가하는 전역 카운터로 남아있어 체력/속도/골드 스케일링이 스테이지 경계와
    ///   무관하게 매끄럽게 이어진다.
    /// [해설] 거점(기지) 개념 제거 후, 적은 처치되거나 "동시 생존 허용 한도(MaxAliveEnemies)"를
    /// 넘겨 즉시 패배 처리(Game.TriggerOverwhelmDefeat)되는 것으로만 사라진다.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public PathData Path;
        public GameManager Game;
        public BuildManager Build;

        // [해설] CurrentWave는 게임 전체를 통틀어 계속 증가하는 전역 웨이브 카운터다(난이도 스케일링용).
        // LocalWave/StageIndex는 여기서 파생되는, 화면 표시와 스테이지 경계 판정에 쓰는 값이다.
        public int CurrentWave { get; private set; } = 0;
        public int LocalWave { get; private set; } = 0;      // 1..WavesPerStage, 스테이지마다 1로 리셋
        public int StageIndex { get; private set; } = 0;     // 0-based
        public int StageNumber => StageIndex + 1;             // 화면 표시용 1-based

        public int AliveEnemies { get; private set; } = 0;
        public bool WaveInProgress { get; private set; } = false;

        private bool _allSpawned;
        private bool _isBossWave;
        private bool _isStageFinale;
        private BossController _finaleBoss; // 피날레 웨이브의 보스 인스턴스(제한시간 판정용)
        private int _waveTotalCount;
        private Coroutine _spawnCoroutine;

        // 일반 웨이브당 스폰되는 적 수. 시간이 지나도(웨이브 도중) 알아서 사라지는 적은 없고,
        // 오직 처치되었을 때만 카운트가 줄어든다. 동시 생존 적이 이 수(_maxAliveCapacity)를
        // 넘어서면(=플레이어가 감당하지 못하고 밀린 것으로 간주) 즉시 패배 처리한다.
        // [해설] 거점 체력이 없는 지금, 이것이 게임의 기본 패배 조건이다. 웨이브 보상으로
        // 이 한도를 늘릴 수 있다(IncreaseAliveCapacity 참고). 스테이지 피날레는 별도로
        // 제한시간 패배 조건도 함께 가진다(아래 Update 참고).
        private const int WaveEnemyCount = 50;
        private int _maxAliveCapacity = GameConstants.StartingMaxAliveEnemies;

        // 스폰 간격(초). 예전보다 더 촘촘하게 몰아쳐서 나오도록 축소했다.
        private const float FirstSpawnDelay = 0.18f;
        private const float BaseSpawnGap = 0.55f;
        private const float MinSpawnGap = 0.22f;
        private const float SpawnGapWaveDecay = 0.01f;

        // 웨이브 스킵: 일반 웨이브는 시작 후 이 시간(초)이 지나면 처치 수와 상관없이 바로
        // 남은 스폰/생존 적을 정리하고 즉시 웨이브 클리어 처리할 수 있다 (보스 웨이브는 스킵 불가).
        private const float SkipUnlockSeconds = 10f;
        private float _waveStartTime;

        public int KilledThisWave { get; private set; }
        public bool IsBossWave => _isBossWave;
        public bool IsStageFinale => _isStageFinale;
        public int MaxAliveEnemies => _maxAliveCapacity;
        public bool CanSkipWave => WaveInProgress && !_isBossWave && (Time.time - _waveStartTime) >= SkipUnlockSeconds;

        /// <summary>스킵이 풀리기까지 남은 시간(초). UI에서 카운트다운 표시용으로 쓴다.</summary>
        public float SkipUnlockRemaining =>
            _isBossWave ? 0f : Mathf.Max(0f, SkipUnlockSeconds - (Time.time - _waveStartTime));

        /// <summary>직전 웨이브가 "스킵"으로 끝났는지. 스킵으로 끝냈다면 다음 준비 단계를
        /// 1초로 짧게 준다(GameManager.EnterPrep / GameConstants.PrepPhaseSecondsAfterSkip 참고).</summary>
        public bool LastWaveSkipped { get; private set; }

        /// <summary>스테이지 피날레의 제한시간이 다 되기까지 남은 시간(초). UI 경고 표시용.</summary>
        public float FinaleTimeRemaining =>
            _isStageFinale ? Mathf.Max(0f, GameConstants.StageFinaleBossTimeLimit - (Time.time - _waveStartTime)) : 0f;

        // [해설] 보스 "이름/능력 패턴"은 5종(BossNames)을 계속 순환해서 재사용하지만, 보스가
        // 몇 번째로 등장하는지(인카운터 번호, 1..)는 스테이지 경계와 무관하게 계속 누적 증가시켜
        // 체력/보상 스케일이 뒤로 갈수록 꾸준히 강해지게 한다 (BossPatternIndexFor/BossEncounterNumber 참고).
        private static readonly string[] BossNames =
        {
            "돌진 보스", "봉쇄 보스", "소환 보스", "압박 보스", "최종 보스"
        };

        public System.Action<int, int> OnWaveStarted; // (stageNumber, localWave)
        public System.Action<int> OnWaveCleared;
        public System.Action<int, string> OnBossIncoming;

        public void BeginNextWave()
        {
            CurrentWave++;
            LocalWave = ((CurrentWave - 1) % GameConstants.WavesPerStage) + 1;
            StageIndex = (CurrentWave - 1) / GameConstants.WavesPerStage;

            // [해설] "길은 고정" — 스테이지의 첫 웨이브에서만 새 길을 적용한다. 나머지 웨이브에서는
            // Path.WaypointsA/B를 건드리지 않으므로 그 스테이지 내내 같은 길을 그대로 쓴다.
            if (LocalWave == 1) ApplyPathForStage(StageIndex);

            WaveDefinition def = BuildWave(LocalWave, StageIndex);
            _isBossWave = def.IsBoss;
            _isStageFinale = def.IsStageFinale;
            _finaleBoss = null;
            _waveTotalCount = def.Entries.Count;
            KilledThisWave = 0;
            _waveStartTime = Time.time; // [해설] 스킵/피날레 제한시간 카운트다운의 공통 시작점.
            OnWaveStarted?.Invoke(StageNumber, LocalWave);
            if (def.IsBoss)
                OnBossIncoming?.Invoke(def.BossIndex, BossNames[def.BossIndex - 1]);
            _spawnCoroutine = StartCoroutine(SpawnRoutine(def));
        }

        private void Update()
        {
            // [해설] "25웨이브 보스 못 잡으면 게임오버" 요청에 따른 제한시간 판정. 스테이지 피날레
            // 웨이브에서만 동작하며, 보스가 아직 살아있는 상태로 제한시간을 넘기면 화면에 남은 적
            // 수(생존 한도 초과 여부)와 무관하게 즉시 게임오버 처리한다.
            if (!WaveInProgress || !_isStageFinale) return;

            bool bossAlive = _finaleBoss != null && !_finaleBoss.IsDead;
            if (bossAlive && (Time.time - _waveStartTime) >= GameConstants.StageFinaleBossTimeLimit)
            {
                _isStageFinale = false; // 중복 트리거 방지
                WaveInProgress = false;
                if (_spawnCoroutine != null)
                {
                    StopCoroutine(_spawnCoroutine);
                    _spawnCoroutine = null;
                }
                Game.TriggerBossTimeoutDefeat();
            }
        }

        /// <summary>
        /// [해설] 이번 스테이지에 쓸 길 도안을 PathLibrary에서 받아와 Path의 WaypointsA/B "필드 자체"를
        /// 새 리스트로 바꿔치기한다 (리스트 내용을 고치는 게 아니라 필드를 통째로 교체하는 방식).
        /// 이렇게 하는 이유: 이미 스폰돼서 화면을 돌고 있는 적은 스폰될 때 그 시점의 리스트 "참조"를
        /// 직접 들고 이동한다(EnemyController.Waypoints). 필드를 새 리스트로 바꿔도 옛 리스트 자체는
        /// 사라지지 않고 그 적이 계속 들고 있으므로, 스테이지가 바뀌는 순간 이미 돌고 있던 적이 갑자기
        /// 다른 방향으로 튀는 일 없이 원래 돌던 루프를 마저 돈다. 반대로 이 시점 "이후"에 새로
        /// 스폰되는 적(Path.GetPath 호출)은 새로 바뀐 리스트를 받아 새 루프를 돌게 된다.
        /// 화면에 보이는 도로/스폰 마커도 GameBootstrapper.RedrawPathVisuals로 같이 다시 그려서
        /// 시각적으로도 실제 이동 경로와 항상 일치하게 맞춘다.
        /// </summary>
        private void ApplyPathForStage(int stageIndex)
        {
            var template = PathLibrary.GetForStage(stageIndex);
            Path.WaypointsA = template.WaypointsA;
            Path.WaypointsB = template.WaypointsB;
            GameBootstrapper.RedrawPathVisuals(Path);
        }

        /// <summary>스테이지 중간 보스(10, 20웨이브...) 여부와 피날레(스테이지 마지막 웨이브) 여부를
        /// 함께 판정한다. 피날레도 "보스 웨이브"이지만(IsBoss=true), 별도 플래그(IsStageFinale)로
        /// 구분해서 일반 유닛 동반 스폰과 제한시간 규칙을 적용한다.</summary>
        private static bool IsMidStageBossWave(int localWave) =>
            localWave != GameConstants.WavesPerStage && localWave % GameConstants.BossIntervalWaves == 0;

        private static bool IsStageFinaleWave(int localWave) => localWave == GameConstants.WavesPerStage;

        /// <summary>보스가 스테이지 안에서 몇 번째 보스 슬롯인지(1, 2, 3 — 10웨이브/20웨이브/피날레).</summary>
        private static int BossSlotForLocalWave(int localWave) =>
            IsStageFinaleWave(localWave) ? 3 : localWave / GameConstants.BossIntervalWaves;

        /// <summary>게임 전체를 통틀어 몇 번째 보스 인카운터인지(1, 2, 3, ... — 스테이지 경계 넘어 계속 누적).
        /// 체력/처치 보상 스케일링에 쓴다(뒤로 갈수록 보스가 꾸준히 강해짐).</summary>
        private static int BossEncounterNumber(int stageIndex, int localWave) =>
            stageIndex * 3 + BossSlotForLocalWave(localWave);

        /// <summary>보스 능력/이름 패턴 선택(1..5, BossNames.Length만큼 계속 순환). 단, 게임의 진짜
        /// 마지막 전투(마지막 스테이지의 피날레)는 순환 주기와 상관없이 항상 5번("최종 보스",
        /// 3페이즈 보스)으로 고정해서, 엔딩이 반드시 가장 극적인 보스로 마무리되게 한다.
        /// (예: 3스테이지 × 슬롯3 = 9번째 인카운터가 순환상 4번째 패턴에 걸리는 우연 때문에
        /// "압박 보스"로 게임이 끝나버리는 것을 방지.)</summary>
        private static int BossPatternIndexFor(int stageIndex, int localWave)
        {
            if (stageIndex == GameConstants.TotalStages - 1 && IsStageFinaleWave(localWave))
                return BossNames.Length; // 5 = "최종 보스"
            return ((BossEncounterNumber(stageIndex, localWave) - 1) % BossNames.Length) + 1;
        }

        private WaveDefinition BuildWave(int localWave, int stageIndex)
        {
            var def = new WaveDefinition { WaveNumber = CurrentWave };

            if (IsMidStageBossWave(localWave))
            {
                def.IsBoss = true;
                def.BossIndex = BossPatternIndexFor(stageIndex, localWave);
                def.BossEncounterNumber = BossEncounterNumber(stageIndex, localWave);
                def.Entries.Add(new SpawnEntry { Type = EnemyType.Boss, PathIndex = 0, Delay = 0.5f });
                return def;
            }

            // 일반 웨이브(스테이지 피날레도 여기서 물량을 만든 뒤, 아래에서 보스 항목만 추가로 얹는다)
            int count = WaveEnemyCount;
            for (int i = 0; i < count; i++)
            {
                EnemyType type = PickTypeForWave(CurrentWave, i);
                // [해설] 진입로 A/B는 이제 같은 정사각형을 서로 다른 지점에서 도는 두 출발점이므로,
                // 웨이브에 상관없이 항상 번갈아 스폰해서 두 지점 모두에서 유닛이 나오게 한다.
                int pathIndex = i % 2;
                float gap = (i == 0) ? FirstSpawnDelay : Mathf.Max(MinSpawnGap, BaseSpawnGap - CurrentWave * SpawnGapWaveDecay);
                def.Entries.Add(new SpawnEntry { Type = type, PathIndex = pathIndex, Delay = gap });
            }

            if (IsStageFinaleWave(localWave))
            {
                def.IsBoss = true;
                def.IsStageFinale = true;
                def.BossIndex = BossPatternIndexFor(stageIndex, localWave);
                def.BossEncounterNumber = BossEncounterNumber(stageIndex, localWave);
                // [해설] "25웨이브에 보스 하고 유닛나오게 설정" — 위에서 이미 만든 일반 웨이브 물량
                // 맨 앞에 보스 항목을 끼워 넣어 스테이지 피날레를 구성한다. 보스와 일반 유닛이
                // 동시에 몰아치므로 동시 생존 한도를 넘기기 쉬워지고(=자연스러운 위험 증가),
                // 그와 별개로 제한시간(GameConstants.StageFinaleBossTimeLimit) 안에 보스를 못
                // 잡아도 위 Update()에서 즉시 게임오버 처리된다.
                def.Entries.Insert(0, new SpawnEntry { Type = EnemyType.Boss, PathIndex = 0, Delay = 0.5f });
            }

            return def;
        }

        private EnemyType PickTypeForWave(int wave, int i)
        {
            if (wave <= 4) return EnemyType.Mob;
            if (wave <= 9) return (i % 3 == 0) ? EnemyType.Charger : EnemyType.Mob;
            if (wave <= 14) return (i % 4 == 0) ? EnemyType.Shield : (i % 3 == 0) ? EnemyType.Charger : EnemyType.Mob;
            if (wave <= 19) return (i % 3 == 0) ? EnemyType.Shield : (i % 2 == 0) ? EnemyType.Charger : EnemyType.Mob;
            return (EnemyType)(i % 3); // 21웨이브 이후: 모든 적 타입 등장(스테이지 경계와 무관하게 계속 유지)
        }

        private IEnumerator SpawnRoutine(WaveDefinition def)
        {
            WaveInProgress = true;
            _allSpawned = false;
            foreach (var entry in def.Entries)
            {
                yield return new WaitForSeconds(entry.Delay);
                var ec = SpawnEnemy(entry.Type, entry.PathIndex,
                    def.IsBoss ? def.BossIndex : 0, def.IsBoss ? def.BossEncounterNumber : 0);
                if (def.IsStageFinale && entry.Type == EnemyType.Boss) _finaleBoss = ec as BossController;
            }
            _allSpawned = true;
            CheckWaveClear();
        }

        private EnemyController SpawnEnemy(EnemyType type, int pathIndex, int bossPatternIndex, int bossEncounterNumber = 0)
        {
            var wp = Path.GetPath(pathIndex);
            var go = new GameObject($"Enemy_{type}");
            EnemyController ec;

            // 웨이브가 지날수록 적이 전반적으로 강해지도록 체력/이동속도/보상을
            // 모두 (전역) 웨이브 번호에 비례해서 키운다. (1웨이브 기준 배율 1.0)
            float hpScale = 1f + (CurrentWave - 1) * 0.08f;
            float speedScale = 1f + Mathf.Min(0.6f, (CurrentWave - 1) * 0.02f);
            // [해설] 골드 증가 폭도 0.03 → 0.02로 완만하게 낮췄다(아래 경제 너프의 일부).
            float goldScale = 1f + (CurrentWave - 1) * 0.02f;

            if (type == EnemyType.Boss)
            {
                var boss = go.AddComponent<BossController>();
                // [해설] 체력/보상은 "패턴 번호(1..5, 순환)"가 아니라 "인카운터 번호(1, 2, 3, ... 계속 누적)"
                // 기준으로 커지므로, 보스 패턴이 반복돼도 뒤로 갈수록 꾸준히 강해진다.
                float baseHp = (260f + bossEncounterNumber * 180f) * (1f + (bossEncounterNumber - 1) * 0.1f);
                // [해설] 보스 보상도 60+30n → 15+10n으로 크게 낮췄다. 일반 처치 보상을 대폭 줄인
                // 뒤에도 보스만 예전 값을 유지하면 보스 한 마리(약 106골드)가 그때까지 번 돈의
                // 절반을 차지해버려서, 경제 너프가 사실상 무의미해지기 때문이다.
                float bossGold = (15 + bossEncounterNumber * 10) * goldScale;
                boss.Init(EnemyType.Boss, baseHp, 1.1f * speedScale, bossGold, wp,
                    new Color(0.75f, 0.2f, 0.75f), Color.white);
                boss.InitBoss(bossPatternIndex, BossNames[bossPatternIndex - 1], this, Game, Build);
                ec = boss;
            }
            else
            {
                ec = go.AddComponent<EnemyController>();
                switch (type)
                {
                    // [해설] 처치 보상 대폭 하향 (몹 2 → 0.22, 돌진 3 → 0.32, 방패 4.5 → 0.5).
                    // 이전 수치로는 10웨이브에 도달할 즈음 타워를 38개나 지을 수 있어서 배치 고민이
                    // 사실상 사라졌다. "10웨이브 보스를 만날 때 타워 6개 정도"가 되도록 역산한 값이다
                    // (계산 근거: 웨이브당 50마리 × 9웨이브 = 450마리 처치 + 웨이브 클리어 보너스).
                    // 1골드 미만이라 EnemyController.GoldReward가 float이고, GameManager.AddGold가
                    // 소수점을 모아뒀다가 1이 넘을 때 지급한다.
                    case EnemyType.Mob:
                        // [해설] 기본 체력을 27로 맞춰서, 1웨이브 기준(hpScale=1) 화살탑(공격력 9)에
                        // 정확히 3번 맞으면 죽도록(9×3=27) 1차 밸런스 기준점을 잡았다.
                        ec.Init(type, 27f * hpScale, 1.5f * speedScale, 0.22f * goldScale,
                            wp, new Color(0.85f, 0.3f, 0.3f), Color.white);
                        break;
                    case EnemyType.Charger:
                        ec.Init(type, 22f * hpScale, 2.6f * speedScale, 0.32f * goldScale,
                            wp, new Color(0.95f, 0.55f, 0.2f), Color.white);
                        break;
                    case EnemyType.Shield:
                        ec.Init(type, 55f * hpScale, 0.9f * speedScale, 0.5f * goldScale,
                            wp, new Color(0.55f, 0.35f, 0.85f), Color.white);
                        break;
                }
            }

            AliveEnemies++;
            ec.OnDied += HandleEnemyRemoved;

            if (AliveEnemies > _maxAliveCapacity)
            {
                Game.ShowBanner($"동시 생존 적 {_maxAliveCapacity}마리 초과! 적에게 압도당했습니다.");
                Game.TriggerOverwhelmDefeat();
            }

            return ec;
        }

        private void HandleEnemyRemoved(EnemyController e)
        {
            Game.AddGold(e.GoldReward);
            if (e is BossController bc) Game.ShowBanner($"{bc.BossName} 처치!");
            AliveEnemies--;
            KilledThisWave++;
            CheckWaveClear();
        }

        /// <summary>
        /// 웨이브 시작 후 SkipUnlockSeconds(10초)가 지나면 즉시 웨이브를 클리어 처리한다
        /// (보상 선택 → 다음 웨이브로 바로 진행). 화면에 남아있는 적은 강제로 없애지 않고 그대로
        /// 둔다 — 계속 이동/전투를 이어가며, 처치되면 그때그때 골드가 평소처럼 정상 반영된다
        /// (다음 웨이브와 함께 공존). 앞으로 예정돼 있던 미스폰 물량만 취소한다.
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
            LastWaveSkipped = true; // 다음 준비 단계를 1초로 줄이기 위한 표시
            OnWaveCleared?.Invoke(CurrentWave);
        }

        private void CheckWaveClear()
        {
            if (_allSpawned && AliveEnemies <= 0 && WaveInProgress)
            {
                WaveInProgress = false;
                _isStageFinale = false; // 정상 클리어됐으므로 제한시간 판정도 함께 종료
                LastWaveSkipped = false; // 전멸시켜 정상 클리어한 경우는 준비 시간을 평소대로
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

        /// <summary>웨이브 보상(AliveCapacity)으로 "동시 생존 허용 한도"를 늘린다.
        /// 거점 최대 체력 업그레이드를 대신하는 성격의 보상이라, 곱연산(배율)으로 늘어난다.</summary>
        public void IncreaseAliveCapacity(float multiplier)
        {
            _maxAliveCapacity = Mathf.Max(_maxAliveCapacity + 1, Mathf.RoundToInt(_maxAliveCapacity * multiplier));
        }

        // ---------- 준비(Prep) 단계에서 "다음 웨이브"를 미리 내다보기 위한 헬퍼 ----------
        // GameManager.EnterPrep()이 BeginNextWave() 호출 전에 다음 웨이브 정보를 UI에 보여줄 때 쓴다.

        public int NextLocalWave() => ((CurrentWave + 1 - 1) % GameConstants.WavesPerStage) + 1;
        public int NextStageIndex() => (CurrentWave + 1 - 1) / GameConstants.WavesPerStage;
        public int NextStageNumber() => NextStageIndex() + 1;
        public bool NextIsStageFinale() => IsStageFinaleWave(NextLocalWave());
        public bool NextIsBoss() => IsMidStageBossWave(NextLocalWave()) || NextIsStageFinale();
        public int NextBossPatternIndex() => BossPatternIndexFor(NextStageIndex(), NextLocalWave());
    }
}
