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
        private BossController _finaleBoss; // 현재 보스 인스턴스(모든 보스의 제한시간 판정용)
        private bool _isTimedBossWave;
        private float _bossTimeLimit;
        private float _bossTimerStart;
        private int _waveTotalCount;
        private Coroutine _spawnCoroutine;

        // 일반 웨이브당 스폰되는 적 수. 시간이 지나도(웨이브 도중) 알아서 사라지는 적은 없고,
        // 오직 처치되었을 때만 카운트가 줄어든다. 동시 생존 적이 이 수(_maxAliveCapacity)를
        // 넘어서면(=플레이어가 감당하지 못하고 밀린 것으로 간주) 즉시 패배 처리한다.
        // [해설] 거점 체력이 없는 지금, 이것이 게임의 기본 패배 조건이다. 웨이브 보상으로
        // 이 한도를 늘릴 수 있다(IncreaseAliveCapacity 참고). 스테이지 피날레는 별도로
        // 제한시간 패배 조건도 함께 가진다(아래 Update 참고).
        /// <summary>
        /// 웨이브에 나오는 일반 적의 수. 기본 물량은 60마리에서 두 웨이브마다 한 마리씩 늘어난다.
        /// 이 기본 물량에 6라운드부터 10%를 추가한다. 중간 보스전은 이 함수를 쓰지 않는다.
        ///
        /// [해설] ★ 이 값이 50이던 시절, 게임의 <b>유일한 패배 조건이 사실상 죽어 있었다</b>.
        /// 패배 판정이 "동시 생존 적 &gt; 한도(50)"인데 한 웨이브에 나오는 적도 정확히 50마리라,
        /// 한 마리도 못 잡아야 겨우 50이 되고 50 &gt; 50은 거짓이다. 즉 평범한 웨이브에서는
        /// 아무리 못해도 절대 지지 않았다. 그런데 화면 위 게이지는 늘 50/50으로 새빨갛게 차
        /// 있어서, 곧 죽을 것처럼 보이는데 실제로는 죽지 않는 이상한 상태였다.
        ///
        /// 이제 적 수가 한도보다 많으므로, 웨이브 중에 일정 수를 처리하지 못하면 실제로 밀려서
        /// 진다. 웨이브가 갈수록 적 수도 함께 늘어나기 때문에 후반에도 긴장이 유지된다.
        /// (한도를 올리는 보상은 Upgrades의 AliveCapacity — 아래 IncreaseAliveCapacity 참고.)
        /// </summary>
        private static int EnemyCountForWave(int globalWave)
        {
            // 기존 기본 물량: 60마리부터 시작해 두 라운드마다 1마리 증가한다.
            int baseCount = 60 + (globalWave - 1) / 2;
            if (globalWave < GameConstants.EnemyCountBoostStartWave) return baseCount;

            // 6라운드부터 10% 추가. 몹은 쪼갤 수 없으므로 소수점은 올린다.
            // 예: 6라운드 기본 62 × 1.10 = 68.2 → 69마리.
            // 실제 등장과 UI 미리보기가 이 함수를 함께 사용하므로 숫자가 일치한다.
            return Mathf.CeilToInt(baseCount * (1f + GameConstants.EnemyCountBoostRate));
        }

        private static float SpawnGapForWave(int globalWave)
        {
            // '등장 간격'은 다음 몹이 나올 때까지 기다리는 초 단위 시간이다.
            float baseGap = Mathf.Max(MinSpawnGap, BaseSpawnGap - globalWave * SpawnGapWaveDecay);
            // 16라운드부터 기존 간격의 95%. 기존 최소 간격에도 같은 비율을 적용한다.
            return globalWave >= GameConstants.FasterSpawnStartWave
                ? baseGap * GameConstants.SpawnGapMultiplier : baseGap;
        }

        /// <summary>일반 적 1마리 처치 보상(골드). 0.5 = "2마리 잡을 때마다 1골드".
        /// 종류(몹/돌진/방패)와 웨이브에 상관없이 고정이다 — 위 SpawnEnemy의 해설 참고.</summary>
        private const float KillGoldReward = 0.5f;

        /// <summary>적 체력 증가가 시작되는 (전역) 웨이브 번호. 이 웨이브 전까지는 기본 체력
        /// 그대로이고, 이 웨이브부터 웨이브당 HpGrowthPerWave만큼 곱해진다 — SpawnEnemy 참고.</summary>
        private const int HpScaleStartWave = 5;

        /// <summary>HpScaleStartWave 이후 <b>웨이브 한 번마다</b> 모든 일반 적의 체력에 곱해지는
        /// 배율(복리). 1.10 = 웨이브당 +10%.
        /// 잡몹 기준 1~4웨 27 / 5웨 30 / 10웨 48 / 25웨 200 / 50웨 2,165 / 75웨 23,455가 된다.
        /// 보스는 자기 공식(baseHp)을 따로 쓰므로 이 값의 영향을 받지 않는다.
        /// 왜 가산이 아니라 복리인지는 SpawnEnemy의 hpScale 해설 참고.</summary>
        private const float HpGrowthPerWave = 1.10f;
        private int _maxAliveCapacity = GameConstants.StartingMaxAliveEnemies;

        // 스폰 간격(초). 예전보다 더 촘촘하게 몰아쳐서 나오도록 축소했다.
        private const float FirstSpawnDelay = 0.18f;
        private const float BaseSpawnGap = 0.55f;
        private const float MinSpawnGap = 0.22f;
        private const float SpawnGapWaveDecay = 0.01f;

        // Time.time은 일시정지·보상 선택 중 멈추므로 대기 시간이 카운트되지 않는다.
        private float _waveStartTime;

        public int KilledThisWave { get; private set; }
        public bool IsBossWave => _isBossWave;
        public bool IsStageFinale => _isStageFinale;
        public int MaxAliveEnemies => _maxAliveCapacity;
        public float NextWaveTimeRemaining => Mathf.Max(0f,
            (CurrentWave <= 4 ? GameConstants.EarlyWaveInterval : GameConstants.NormalWaveInterval)
            - (Time.time - _waveStartTime));

        /// <summary>모든 보스의 제한시간(초). 보스 처치 즉시 표시를 종료한다.</summary>
        public float FinaleTimeRemaining =>
            WaveInProgress && _isTimedBossWave && _finaleBoss != null && !_finaleBoss.IsDead
                ? Mathf.Max(0f, _bossTimeLimit - (Time.time - _bossTimerStart)) : 0f;

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
            // [해설] ★ 버그 수정. 예전에는 여기서 "로컬 웨이브 1이면 길을 새로 깐다"고 했는데,
            // <b>게임 첫 웨이브도 로컬 웨이브 1</b>이라 시작하자마자 스테이지 전환 처리가 돌았다.
            // 그 결과 첫 준비 시간(8초) 동안 시작 골드로 세운 타워 2개가 웨이브 1이 시작되는
            // 순간 통째로 철거됐다. 게다가 전환이 "웨이브 시작" 시점이라, 2·3스테이지에서도
            // 플레이어는 아직 바뀌지 않은 옛 길을 보며 준비 시간을 쓰고 타워를 세운 뒤,
            // 웨이브가 시작되자마자 그게 다 지워졌다.
            // 이제 길 교체는 다음 스테이지 시작 버튼에서(GameManager.ConfirmNextStage →
            // PrepareNextStage)에 하고 여기서는 아무것도 하지 않는다. 그러면 다음 스테이지 준비
            // 시간을 새 길을 보면서 쓸 수 있고, 그때 세운 타워도 지워지지 않는다.
            // 첫 스테이지의 길은 GameBootstrapper.BuildGame이 이미 깔아 둔다.

            WaveDefinition def = BuildWave(LocalWave, StageIndex);
            _isBossWave = def.IsBoss;
            _isStageFinale = def.IsStageFinale;
            _isTimedBossWave = def.IsBoss;
            _bossTimeLimit = GameConstants.BossTimeLimit;
            _finaleBoss = null;
            _waveTotalCount = def.Entries.Count;
            KilledThisWave = 0;
            _waveStartTime = Time.time; // [해설] 스킵/피날레 제한시간 카운트다운의 공통 시작점.
            OnWaveStarted?.Invoke(StageNumber, LocalWave);
            if (def.IsBoss)
                OnBossIncoming?.Invoke(def.BossIndex, LocalWave == 5 ? "보호막 보스" : BossNames[def.BossIndex - 1]);
            _spawnCoroutine = StartCoroutine(SpawnRoutine(def));
        }

        private void Update()
        {
            if (Game.State != GameState.Defense || Game.IsPaused) return;
            if (WaveInProgress && !_isBossWave)
            {
                CheckWaveClear();
                return;
            }
            // 모든 보스는 등장 후 제한시간 내 처치하지 못하면 즉시 패배한다.
            if (!WaveInProgress || !_isTimedBossWave) return;

            bool bossAlive = _finaleBoss != null && !_finaleBoss.IsDead;
            // 무적 중에도 제한시간은 흐른다. 일시정지와 배속은 기존 게임 시간 규칙을 따른다.
            if (bossAlive && (Time.time - _bossTimerStart) >= _bossTimeLimit)
            {
                _isTimedBossWave = false;
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

        /// <summary>
        /// 보상 선택 후 다음 스테이지 시작 버튼에서 호출된다. 다음 스테이지의 길로 <b>미리</b> 갈아끼우고,
        /// 기존 타워를 전부 철거해 건설비를 전액 돌려준다.
        ///
        /// [해설] 스테이지마다 길 모양이 통째로 달라지므로, 판을 비우고 새 길에 맞춰 처음부터
        /// 다시 짜게 한다("새 길에서 무효가 된 것만" 골라 지우면 남은 타워와 새 타워가 뒤섞여
        /// 배치가 누더기가 되고, 무엇이 왜 사라졌는지도 알 수 없다).
        ///
        /// 호출 시점이 중요하다. 이 뒤에 준비 시간이 오므로, 플레이어는 이미 바뀐
        /// 새 길을 보면서 타워를 배치하게 된다. 예전처럼 다음 웨이브가 "시작"될 때 교체하면
        /// 준비 시간에 옛 길을 보고 세운 타워가 웨이브 시작과 동시에 지워진다.
        /// </summary>
        public void PrepareNextStage()
        {
            int next = StageIndex + 1;
            if (next >= GameConstants.TotalStages) return; // 마지막 스테이지면 넘어갈 곳이 없다

            ApplyPathForStage(next);
            Build?.ClearAllTowersForNewStage();
        }

        /// <summary>스테이지 중간 보스(10, 20웨이브...) 여부와 피날레(스테이지 마지막 웨이브) 여부를
        /// 함께 판정한다. 피날레도 "보스 웨이브"이지만(IsBoss=true), 별도 플래그(IsStageFinale)로
        /// 구분해서 일반 유닛 동반 스폰과 제한시간 규칙을 적용한다.</summary>
        private static bool IsMidStageBossWave(int localWave) =>
            localWave != GameConstants.WavesPerStage && localWave % GameConstants.BossIntervalWaves == 0;

        private static bool IsStageFinaleWave(int localWave) => localWave == GameConstants.WavesPerStage;

        // 25라운드 / 5라운드 간격 = 스테이지당 보스 5회.
        private static int BossesPerStage =>
            (GameConstants.WavesPerStage - 1) / GameConstants.BossIntervalWaves + 1;

        /// <summary>5/10/15/20/25라운드를 보스 등장 순서 1/2/3/4/5로 변환한다.</summary>
        private static int BossSlotForLocalWave(int localWave) =>
            IsStageFinaleWave(localWave) ? BossesPerStage : localWave / GameConstants.BossIntervalWaves;

        /// <summary>게임 전체를 통틀어 몇 번째 보스 인카운터인지(1, 2, 3, ... — 스테이지 경계 넘어 계속 누적).
        /// 체력/처치 보상 스케일링에 쓴다(뒤로 갈수록 보스가 꾸준히 강해짐).</summary>
        private static int BossEncounterNumber(int stageIndex, int localWave) =>
            stageIndex * BossesPerStage + BossSlotForLocalWave(localWave);

        /// <summary>스테이지마다 보스 패턴 1~5를 순서대로 배정한다. 최종전은 항상 5번이다.</summary>
        private static int BossPatternIndexFor(int stageIndex, int localWave)
        {
            if (stageIndex == GameConstants.TotalStages - 1 && IsStageFinaleWave(localWave))
                return BossNames.Length; // 5 = "최종 보스"
            return ((BossSlotForLocalWave(localWave) - 1) % BossNames.Length) + 1;
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
            int count = EnemyCountForWave(CurrentWave);
            for (int i = 0; i < count; i++)
            {
                EnemyType type = PickTypeForWave(CurrentWave, i);
                // [해설] 진입로 A/B는 이제 같은 정사각형을 서로 다른 지점에서 도는 두 출발점이므로,
                // 웨이브에 상관없이 항상 번갈아 스폰해서 두 지점 모두에서 유닛이 나오게 한다.
                int pathIndex = i % 2;
                float gap = (i == 0) ? FirstSpawnDelay : SpawnGapForWave(CurrentWave);
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
            // 11~14라운드: 12마리 단위로 방패병 4, 돌진병 3, 잡몹 5.
            // 기존 방패병 3/12(25%)에서 4/12(약 33%)로 증가. 잡몹 한 자리를 바꾼다.
            // %는 나머지 연산이다. i % 12 == 1은 매 12마리 중 두 번째 자리를 뜻한다.
            if (wave <= 14) return (i % 4 == 0 || i % 12 == 1)
                ? EnemyType.Shield : (i % 3 == 0) ? EnemyType.Charger : EnemyType.Mob;
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
                if (_isTimedBossWave && entry.Type == EnemyType.Boss)
                {
                    _finaleBoss = ec as BossController;
                    // 스폰 대기 시간은 빼고, 실제 보스가 등장한 순간부터 60초를 센다.
                    _bossTimerStart = Time.time;
                }
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

            // [해설] 체력 증가는 HpScaleStartWave(5)웨이브부터 시작한다. 1웨이브부터 곧바로
            // 올리면 타워 한두 개로 버티는 도입부가 사라지기 때문이다. 1~4웨이브는 기본 체력
            // 그대로이고, 5웨이브부터 웨이브당 HpGrowthPerWave(+10%)씩 <b>복리로</b> 곱해진다.
            //   잡몹 기준 1~4웨 27 / 5웨 30 / 10웨 48 / 25웨 200 / 50웨 2,165 / 75웨 23,455
            // Mathf.Max(0, ...)가 4웨이브 이하에서 지수가 음수가 되는(=체력이 줄어드는) 것을 막는다.
            // 속도/보스 체력은 각자 따로 계산하므로 여기 영향을 받지 않는다.
            //
            // ★★ 증가 방식을 <b>가산(+10)에서 복리(+10%)로</b> 되돌렸다. 이유는 방향이다.
            // 플레이어의 힘은 "타워 수 × 강화"라서 뒤로 갈수록 빠르게 커지는데, 적 체력이
            // 선형이면 격차가 계속 벌어지기만 한다. 실제로 가산일 때는 화살탑 한 대가 잡몹
            // 하나를 잡는 시간이 몇 웨이브든 2.5초를 넘지 않았다 — 강화가 체력 증가를 완전히
            // 따라잡아서, 뒤로 갈수록 오히려 더 쉬워졌다.
            // 복리로 바꾸면 그 상한이 풀린다(75웨 기준 처치 시간 2.5초 → 80초, 약 32배).
            //
            // 주의 1: 10~25웨이브 구간은 오히려 지금보다 <b>쉬워진다</b>. +10은 기본 체력 27에
            //   비해 큰 폭이라 초반에 가팔랐지만 1.1^6은 1.77배뿐이기 때문이다. 역전은 30웨쯤.
            // 주의 2: 이 값만으로는 아직 패배가 성립하지 않는다(모델상 75웨 여유 2.6배).
            //   다음 단계인 경제 압박(유지비·타워 상한)과 곱해져야 실제로 질 수 있게 된다.
            //   두 변경은 곱해지므로, 경제 압박을 넣을 때 이 증가율을 반드시 다시 계산할 것.
            // 주의 3: 배율이라 기본 체력이 큰 방패병(55)이 잡몹(27)보다 더 가파르게 벌어진다.
            int hpGrowthWaves = Mathf.Max(0, CurrentWave - HpScaleStartWave + 1);
            float hpScale = Mathf.Pow(HpGrowthPerWave, hpGrowthWaves);
            // [해설] 기본 이동속도를 일괄 +50% 올렸다(일반 1.5→2.25 / 돌진 2.6→3.9 / 방패 0.9→1.35 /
            // 보스 1.1→1.65). 적이 사거리 안에 머무는 시간이 3분의 2로 줄어들기 때문에, 타워의
            // 실효 화력도 그만큼 떨어져서 난이도가 눈에 띄게 올라간다.
            // 아래 웨이브 배율(최대 +60%)은 그 위에 그대로 곱해진다.
            float speedScale = 1f + Mathf.Min(0.6f, (CurrentWave - 1) * 0.02f);
            // [해설] 골드 증가 폭도 0.03 → 0.02로 완만하게 낮췄다(아래 경제 너프의 일부).
            float goldScale = 1f + (CurrentWave - 1) * 0.02f;

            if (type == EnemyType.Boss)
            {
                var boss = go.AddComponent<BossController>();
                // [해설] 체력/보상은 "패턴 번호(1..5, 순환)"가 아니라 "인카운터 번호(1, 2, 3, ... 계속 누적)"
                // 기준으로 커지므로, 보스 패턴이 반복돼도 뒤로 갈수록 꾸준히 강해진다.
                float baseHp = (GameConstants.BossHealthBase + bossEncounterNumber * GameConstants.BossHealthPerEncounter)
                    * (1f + (bossEncounterNumber - 1) * GameConstants.BossHealthGrowthPerEncounter);
                // [해설] 보스 보상도 60+30n → 15+10n으로 크게 낮췄다. 일반 처치 보상을 대폭 줄인
                // 뒤에도 보스만 예전 값을 유지하면 보스 한 마리(약 106골드)가 그때까지 번 돈의
                // 절반을 차지해버려서, 경제 너프가 사실상 무의미해지기 때문이다.
                float bossGold = (15 + bossEncounterNumber * 10) * goldScale;
                boss.Init(EnemyType.Boss, baseHp, 1.65f * speedScale, bossGold, wp,
                    new Color(0.75f, 0.2f, 0.75f), Color.white);
                boss.InitBoss(bossPatternIndex, LocalWave == 5 ? "보호막 보스" : BossNames[bossPatternIndex - 1], this, Game, Build);
                ec = boss;
            }
            else
            {
                ec = go.AddComponent<EnemyController>();
                switch (type)
                {
                    // [해설] 처치 보상 규칙을 "몬스터 2마리 = 1골드"로 단순화했다. 예전에는 종류마다
                    // 달랐고(몹 0.22 / 돌진 0.32 / 방패 0.5) 거기에 웨이브 배율(goldScale)까지 곱해서
                    // 플레이어가 수입을 가늠하기 어려웠다. 이제는 종류·웨이브와 무관하게 전부 0.5로
                    // 고정이라, 정확히 2마리를 잡을 때마다 1골드가 들어온다.
                    //
                    // ★ goldScale(웨이브당 +2%)을 일부러 곱하지 않는다. 곱하면 50웨이브쯤엔 1마리당
                    //   0.99골드가 되어 "2마리 = 1골드" 규칙이 사실상 "1마리 = 1골드"로 깨지기 때문이다.
                    //   웨이브 스케일링은 보스 보상(bossGold) 쪽에만 그대로 남아 있다.
                    // ★ 0.5는 1골드 미만이므로 EnemyController.GoldReward가 float이고,
                    //   GameManager.AddGold가 _goldFraction에 모아뒀다가 1이 넘을 때 지급한다.
                    //   (그래서 홀수 번째 처치분도 버려지지 않고 다음 처치 때 합산된다.)
                    // ★ 체력/속도 스케일링(hpScale/speedScale)은 그대로라 난이도는 계속 오른다.
                    case EnemyType.Mob:
                        // [해설] 기본 체력을 27로 맞춰서, 1웨이브 기준(hpScale=1) 화살탑(공격력 9)에
                        // 정확히 3번 맞으면 죽도록(9×3=27) 1차 밸런스 기준점을 잡았다.
                        ec.Init(type, 27f * hpScale, 2.25f * speedScale, KillGoldReward,
                            wp, new Color(0.85f, 0.3f, 0.3f), Color.white);
                        break;
                    case EnemyType.Charger:
                        ec.Init(type, 22f * hpScale, 3.9f * speedScale, KillGoldReward,
                            wp, new Color(0.95f, 0.55f, 0.2f), Color.white);
                        break;
                    case EnemyType.Shield:
                        ec.Init(type, 55f * hpScale, 1.35f * speedScale, KillGoldReward,
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

        private void CheckWaveClear()
        {
            if (!_allSpawned || !WaveInProgress || Game.State != GameState.Defense || Game.IsPaused) return;
            if (_isBossWave)
            {
                // 보스전은 보스와 소환된 잡몹까지 정리한 뒤 다음으로 진행한다.
                if (AliveEnemies > 0) return;
            }
            else
            {
                // 적을 일찍 다 잡아도 정해진 시간까지 기다린다.
                if (NextWaveTimeRemaining > 0f) return;
                // 다음이 보스라면 남은 적을 모두 정리해야 한다.
                if (NextIsBoss() && AliveEnemies > 0) return;
            }

            // 일반→일반에서는 남은 적을 없애지 않는다. 다음 웨이브의 적과 함께 남는다.
            // 먼저 진행 플래그를 내려 같은 프레임에 보상이 두 번 지급되는 것을 막는다.
            WaveInProgress = false;
            _isStageFinale = false;
            OnWaveCleared?.Invoke(CurrentWave);
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
        /// <summary>
        /// 게임이 끝났을 때(패배/승리) 웨이브 진행 상태를 완전히 닫는다.
        /// [해설] ★ 버그 수정. 예전에는 GameOver가 StopAllCoroutines만 부르고 WaveInProgress는
        /// true로 남겨 뒀다. StopAllCoroutines를 스폰 코루틴 <b>안에서</b> 부르면 그 프레임의
        /// MoveNext는 끝까지 실행되므로, 마지막 스폰에서 패배가 나면 _allSpawned가 true가 된다.
        /// 그 뒤 살아남은 타워가 남은 적을 정리하면 CheckWaveClear의 조건
        /// (_allSpawned &amp;&amp; AliveEnemies &lt;= 0 &amp;&amp; WaveInProgress)이 그대로 성립해서,
        /// <b>게임오버 화면 뒤에서 웨이브 클리어 처리가 한 번 더 돌고</b> 보상 패널까지 떴다
        /// (마지막 스테이지 피날레였다면 패배 문구가 승리 문구로 덮이기까지 했다).
        /// WaveInProgress를 여기서 확실히 내려서 그 경로를 막는다.
        /// </summary>
        public void HaltForGameEnd()
        {
            StopAllCoroutines();
            WaveInProgress = false;
            _isStageFinale = false;
        }

        /// <summary>보상 "수용력 강화"로 동시 생존 허용 한도를 올린다.
        ///
        /// [해설] ★ 예전에는 ×1.15 배율이었는데, 배율은 고를수록 눈덩이처럼 불어난다
        /// (50 → 57 → 66 → 76 → 87 → 100). 적 수는 웨이브당 0.5마리씩 <b>직선으로</b> 느는데
        /// 한도만 기하급수로 뛰니, 이 보상을 두세 번만 골라도 패배 조건이 다시 영영 닿지 않게
        /// 된다 — 위 EnemyCountForWave에서 고친 문제가 그대로 되살아나는 셈이다.
        /// 그래서 적 수와 같은 단위인 <b>가산(+6)</b>으로 바꿨다(50 → 56 → 62 → 68 → 74 → 80).
        /// 이제 한도와 적 수가 비슷한 속도로 올라가서, 보상을 골라도 긴장이 남는다.</summary>
        public void IncreaseAliveCapacity(int amount)
        {
            _maxAliveCapacity += Mathf.Max(1, amount);
        }

        // ---------- 준비(Prep) 단계에서 "다음 웨이브"를 미리 내다보기 위한 헬퍼 ----------
        // GameManager.EnterPrep()이 BeginNextWave() 호출 전에 다음 웨이브 정보를 UI에 보여줄 때 쓴다.

        public int NextLocalWave() => ((CurrentWave + 1 - 1) % GameConstants.WavesPerStage) + 1;
        public int NextStageIndex() => (CurrentWave + 1 - 1) / GameConstants.WavesPerStage;
        public int NextStageNumber() => NextStageIndex() + 1;
        public bool NextIsStageFinale() => IsStageFinaleWave(NextLocalWave());
        public bool NextIsBoss() => IsMidStageBossWave(NextLocalWave()) || NextIsStageFinale();
        public int NextBossPatternIndex() => BossPatternIndexFor(NextStageIndex(), NextLocalWave());

        /// <summary>
        /// 다음 웨이브에 어떤 적이 몇 마리 오는지. 준비 단계 UI가 그대로 찍어 준다.
        ///
        /// [해설] ★ 준비 시간을 실제 플레이로 만들기 위해 추가했다. 예전에는 웨이브 사이 8초가
        /// 그냥 기다리는 시간이었다 — 무엇이 오는지 모르니 대비할 수가 없고, 대비할 수 없으면
        /// 타워를 고르게 해도 그 선택이 도박이 된다. 정보 없는 선택은 재미가 아니라 운이다.
        /// 이제 "차저 40 · 방패 20"을 보고 무엇을 지을지(혹은 무엇을 팔지) 판단할 수 있다.
        ///
        /// BuildWave()와 <b>완전히 같은 규칙</b>으로 세어야 한다 — 미리보기와 실제가 어긋나면
        /// 정보가 없느니만 못하다. 그래서 마리 수는 EnemyCountForWave, 종류는 PickTypeForWave로
        /// BuildWave가 쓰는 바로 그 함수를 똑같이 호출한다.
        /// </summary>
        public struct WavePreview
        {
            public int Mob;
            public int Charger;
            public int Shield;
            public bool HasBoss;
            /// <summary>보스를 포함한 총 마리 수.</summary>
            public int Total;
        }

        public WavePreview NextWaveComposition()
        {
            var p = new WavePreview();
            int nextWave = CurrentWave + 1;
            int localWave = NextLocalWave();

            // 중간 보스 웨이브는 보스 한 마리뿐이다(BuildWave의 첫 분기와 같다).
            if (IsMidStageBossWave(localWave))
            {
                p.HasBoss = true;
                p.Total = 1;
                return p;
            }

            int count = EnemyCountForWave(nextWave);
            for (int i = 0; i < count; i++)
            {
                switch (PickTypeForWave(nextWave, i))
                {
                    case EnemyType.Charger: p.Charger++; break;
                    case EnemyType.Shield: p.Shield++; break;
                    default: p.Mob++; break;
                }
            }
            p.Total = count;

            // 스테이지 피날레는 위 물량에 보스가 한 마리 얹힌다.
            if (IsStageFinaleWave(localWave))
            {
                p.HasBoss = true;
                p.Total++;
            }
            return p;
        }
    }
}
