using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Defense2D
{
    /// <summary>
    /// 핵심 플레이 루프 상태머신: 준비(Prep) → 방어(Defense) → 보상(Reward) → (다시 준비).
    /// 골드와 게임 오버/승리 판정을 총괄한다. (기획서 01 CORE IDEA 핵심 플레이 루프)
    /// [해설] 거점(기지) 개념은 제거되었다. 길이 도착점 없는 무한 루프가 되면서 "거점 도달"이라는
    /// 사건 자체가 없어졌고, 유일한 패배 조건은 동시 생존 적이 한도(WaveManager.MaxAliveEnemies)를
    /// 넘어서는 것(TriggerOverwhelmDefeat)이다.
    /// </summary>
    public enum GameState { Prep, Defense, Reward, GameOver, Victory }

    public class GameManager : MonoBehaviour
    {
        public UIManager UI;
        public WaveManager Waves;
        public BuildManager Build;

        public int Gold;
        public GameState State = GameState.Prep;

        private float _incomeSuppressTimer;
        private float _goldMultiplier = 1f;
        private float _prepTimer;
        /// <summary>아직 1골드가 되지 못하고 쌓여 있는 처치 보상의 소수점 부분 (AddGold 참고).</summary>
        private float _goldFraction;

        /// <summary>일시정지 중인지. 일시정지는 Time.timeScale을 0으로 만드는 방식이라
        /// 적 이동·타워 발사·스폰 코루틴·스킵/제한시간 카운트다운이 모두 함께 멈춘다
        /// (SetPaused 주석 참고).</summary>
        public bool IsPaused { get; private set; }

        // ---------- 배속 ----------

        /// <summary>고를 수 있는 배속 단계. 여기에 값을 추가하면 버튼 순환에 자동으로 들어간다.</summary>
        private static readonly float[] SpeedSteps = { 1f, 2f, 3f };
        private int _speedIndex;

        /// <summary>현재 배속(1 / 2 / 3). 일시정지가 아닐 때 Time.timeScale에 들어가는 값이다.</summary>
        public float GameSpeed => SpeedSteps[_speedIndex];

        /// <summary>
        /// 배속을 다음 단계로 넘긴다(1x → 2x → 3x → 1x).
        ///
        /// [해설] 배속은 Time.timeScale 하나로 구현한다. 이 게임은 적 이동·타워 쿨타임·스폰
        /// 간격·슬로우/기절 타이머·보스 능력 주기·피날레 제한시간이 전부 스케일된 시간
        /// (Time.deltaTime / Time.time / WaitForSeconds)을 쓰기 때문에, 이 값 하나만 바꾸면
        /// 게임 전체가 균일하게 빨라진다. UI 버튼은 timeScale과 무관하게 동작하므로 배속
        /// 중에도 조작에는 영향이 없다.
        ///
        /// 일시정지(timeScale = 0)와 충돌하지 않도록, 멈춰 있는 동안 배속을 바꾸면 값만
        /// 기억해 두고 실제 적용은 재개할 때 한다(SetPaused 참고).
        /// </summary>
        public void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
            if (!IsPaused) Time.timeScale = GameSpeed;
            UI.RefreshSpeedButton();
        }

        /// <summary>게임이 이미 끝난 상태(게임오버/승리)에서는 일시정지를 걸 수 없다.</summary>
        private bool CanPause => State != GameState.GameOver && State != GameState.Victory;

        private void Awake()
        {
            Gold = GameConstants.StartingGold;
        }

        private void Start()
        {
            EnterPrep();
        }

        private void Update()
        {
            // [해설] 일시정지 입력은 여기 한 곳에서만 처리한다. ESC는 "배치/철거 모드 취소"와
            // 의미가 겹치는데, BuildManager와 GameManager가 각자 같은 프레임에 ESC를 보면
            // 어느 쪽이 먼저 도느냐에 따라 동작이 달라지는 경합이 생긴다. 그래서 ESC 판정을
            // 통째로 이쪽으로 모으고, 배치/철거 중일 때만 BuildManager에 취소를 지시한다.
            HandlePauseInput();

            // Time.timeScale이 0이면 아래 deltaTime 계산은 어차피 0이라 멈추지만, 의도를
            // 분명히 하려고 일시정지 중에는 게임 로직 갱신을 아예 건너뛴다.
            // (Update 자체는 timeScale과 무관하게 계속 돌기 때문에 UI 버튼은 정상 동작한다.)
            if (!IsPaused)
            {
                if (_incomeSuppressTimer > 0f) _incomeSuppressTimer -= Time.deltaTime;

                if (State == GameState.Prep)
                {
                    _prepTimer -= Time.deltaTime;
                    UI.SetPrepCountdown(Mathf.Max(0f, _prepTimer));
                    if (_prepTimer <= 0f) StartDefense();
                }

                if (Waves != null) UI.RefreshAliveCount(Waves.AliveEnemies);
            }

            UI.RefreshActionButton();
            UI.RefreshFinaleTimer(); // 피날레 제한시간 카운트다운(해당 웨이브가 아니면 스스로 숨는다)
        }

        private void HandlePauseInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // P는 언제나 일시정지 토글.
            if (kb.pKey.wasPressedThisFrame) TogglePause();

            // F는 배속 순환. 게임이 끝난 뒤에는 바꿀 이유가 없으므로 막는다.
            if (kb.fKey.wasPressedThisFrame && CanPause) CycleSpeed();

            if (!kb.escapeKey.wasPressedThisFrame) return;

            // ESC: 타워를 놓는 중/철거하는 중이면 그 모드를 먼저 취소하고, 그게 아니면 일시정지.
            if (!IsPaused && Build != null && Build.HasActiveMode) Build.CancelMode();
            else TogglePause();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>
        /// 일시정지를 켜고 끈다.
        /// [해설] Time.timeScale = 0으로 게임 시간을 통째로 멈춘다. 이 게임은 적 이동·타워 쿨타임·
        /// 이펙트가 전부 Time.deltaTime 기반이고, 스폰 코루틴은 WaitForSeconds, 스킵/피날레
        /// 제한시간은 Time.time 기준이라 — 이 값들이 전부 스케일된 시간이므로 timeScale 하나로
        /// 한꺼번에 얼어붙는다. 별도로 멈춰줘야 하는 것이 없다.
        /// 주의: timeScale은 씬을 다시 불러와도 되돌아오지 않는 전역 값이라, 일시정지 상태에서
        /// "다시 시작"을 누르면 새 게임이 멈춘 채로 시작된다. 그래서 GameBootstrapper가 게임을
        /// 조립할 때마다 1로 되돌린다(ResetStaticState 참고).
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (paused && !CanPause) return;
            if (IsPaused == paused) return;

            IsPaused = paused;
            // 재개할 때 1이 아니라 현재 배속으로 돌아간다 — 그러지 않으면 2배속으로 놀다가
            // 한 번 멈췄다 풀면 조용히 1배속이 되어 버린다.
            Time.timeScale = paused ? 0f : GameSpeed;

            // 일시정지 중에 타워를 놓거나 철거하지 못하게 한다(무한 계획 시간 방지).
            if (paused && Build != null) Build.CancelMode();
            UI.ShowPausePanel(paused);
        }

        public void EnterPrep()
        {
            State = GameState.Prep;
            // [해설] 직전 웨이브를 스킵으로 끝냈다면 준비 시간을 1초로 줄인다 — 빨리 넘어가려고
            // 스킵한 사람을 다시 8초 기다리게 만들지 않기 위한 것이다.
            _prepTimer = Waves.LastWaveSkipped
                ? GameConstants.PrepPhaseSecondsAfterSkip
                : GameConstants.PrepPhaseSeconds;
            // [해설] 스테이지 구조 개편에 따라, "다음 웨이브"를 더 이상 단순 정수 하나로 다루지 않고
            // WaveManager의 Next*() 헬퍼로 스테이지 번호/로컬 웨이브/보스 여부/피날레 여부/보스 패턴을
            // 함께 내다본다.
            UI.ShowPrepPanel(Waves.NextStageNumber(), Waves.NextLocalWave(),
                Waves.NextIsBoss(), Waves.NextIsStageFinale(), Waves.NextBossPatternIndex());
        }

        public void SkipPrep()
        {
            if (State == GameState.Prep) StartDefense();
        }

        private void StartDefense()
        {
            State = GameState.Defense;
            UI.HidePrepPanel();
            Waves.BeginNextWave();
        }

        /// <summary>
        /// 골드 수입을 더한다(적 처치 보상 + 웨이브 클리어 보너스). [해설] 보상이 1골드보다 작을 수
        /// 있으므로(모든 일반 적 0.5 = 2마리당 1골드) 소수점을 버리지 않고 _goldFraction에 모아뒀다가, 1을 넘길 때마다
        /// 그만큼만 실제 골드로 지급한다. 이렇게 해야 "50마리 × 최소 1골드"라는 바닥에 걸리지 않고
        /// 경제를 원하는 만큼 조일 수 있다. "재화 감각" 업그레이드 배율(_goldMultiplier)도 여기서 곱한다.
        /// </summary>
        /// <param name="ignoreSuppression">
        /// [해설] 보스 4번은 SuppressIncomeBriefly(5초)로 수입을 잠시 끊는데, 그 기능은 "적을 잡아도
        /// 돈이 안 들어온다"는 압박을 주려는 것이지 웨이브 클리어 보너스까지 통째로 날리려는 게 아니다.
        /// 보스가 죽기 직전에 이 기술을 쓰면 5초 안에 웨이브가 끝나면서 보너스 전액이 조용히 증발하는데,
        /// 플레이어 입장에서는 원인을 알 수 없는 손해다. 그래서 웨이브 보너스만 true로 넘겨 예외 처리한다.
        /// </param>
        public void AddGold(float amount, bool ignoreSuppression = false)
        {
            if (!ignoreSuppression && _incomeSuppressTimer > 0f) return;

            _goldFraction += amount * _goldMultiplier;
            int whole = Mathf.FloorToInt(_goldFraction);
            if (whole <= 0) return;

            _goldFraction -= whole;
            Gold += whole;
            UI.RefreshGold();
        }

        public void SpendGold(int amount)
        {
            Gold -= amount;
            UI.RefreshGold();
        }

        /// <summary>타워 철거 시 건설비 일부를 돌려준다. 적 처치 보상(AddGold)과 달리 골드 획득량
        /// 업그레이드 배율이나 보스의 수급 방해(_incomeSuppressTimer)의 영향을 받지 않는다 —
        /// 이미 낸 돈을 되돌려주는 것이지 새로 버는 수입이 아니기 때문이다.</summary>
        public void RefundGold(int amount)
        {
            Gold += amount;
            UI.RefreshGold();
        }

        /// <summary>보스(4번)가 골드를 직접 약탈할 때 사용. 보유 골드보다 많이 뺏기지 않도록 클램프한다.</summary>
        public void StealGold(int amount)
        {
            int stolen = Mathf.Min(Gold, amount);
            Gold -= stolen;
            UI.RefreshGold();
        }

        /// <summary>동시 생존 적이 허용치를 넘어섰을 때(WaveManager) 즉시 패배 처리.
        /// 거점이 없는 지금은 이것이 게임의 유일한 패배 조건이다.</summary>
        public void TriggerOverwhelmDefeat()
        {
            if (State == GameState.GameOver || State == GameState.Victory) return;
            GameOver("적에게 압도당했습니다");
        }

        /// <summary>스테이지 피날레(로컬 웨이브 25) 보스를 제한시간(GameConstants.StageFinaleBossTimeLimit)
        /// 안에 처치하지 못했을 때 WaveManager.Update()가 호출하는 즉시 패배 처리.
        /// "25웨이브 보스 못잡으면 게임오버" 요청을 그대로 구현한다.</summary>
        public void TriggerBossTimeoutDefeat()
        {
            if (State == GameState.GameOver || State == GameState.Victory) return;
            GameOver("제한시간 안에 보스를 처치하지 못했습니다");
        }

        public void SuppressIncomeBriefly(float seconds)
        {
            _incomeSuppressTimer = Mathf.Max(_incomeSuppressTimer, seconds);
        }

        public void ShowBanner(string text) => UI.ShowBanner(text);

        public void OnWaveClearedHandler(int waveNumber)
        {
            // [해설] 웨이브 클리어 보너스. 한때 (15 + 웨이브)로 너무 후했던 것을 (2 + 웨이브/2)까지
            // 깎았는데, 이번에는 그게 조금 빡빡하다는 판단에 따라 (3 + 웨이브*2/3)으로 소폭 올렸다.
            // 웨이브 10에서 7 → 9, 25에서 14 → 19, 75에서 39 → 53골드가 된다(보너스 기준 약 +38%).
            // 전체 수입에서 이 보너스가 차지하는 비중은 1/4 정도라, 총 수입으로는 약 +10%다.
            // waveNumber는 스테이지마다 리셋되는 표시용 번호가 아니라 게임 전체를 통틀어 누적되는
            // 전역 웨이브 번호(CurrentWave, 1..75)이므로, 스테이지가 넘어가도 보너스는 계속 커진다.
            //
            // [해설] 예전에는 Gold에 직접 더해서 "재화 감각" 업그레이드가 이 보너스에는 안 붙었는데,
            // 안내 문구가 "골드 획득량 +10%"인 이상 플레이어는 당연히 웨이브 보상에도 붙는다고 읽는다.
            // 그래서 AddGold()를 경유하도록 바꿨다 — 이제 _goldMultiplier가 여기에도 곱해지고,
            // 배율 때문에 생기는 소수점도 _goldFraction에 쌓였다가 나중에 온전히 지급된다.
            // (UI 갱신도 AddGold 안에서 처리하므로 여기서 RefreshGold를 또 부르지 않는다.)
            AddGold(3 + waveNumber * 2 / 3, ignoreSuppression: true);

            // [해설] 스테이지 구조 개편: "몇 번째 전체 웨이브인가"가 아니라 "방금 끝난 웨이브가
            // 어떤 스테이지의 몇 번째 로컬 웨이브였는가"로 클리어/승리를 판정해야 한다. Waves는
            // 이미 다음 웨이브를 위한 상태로 넘어가지 않은 시점이므로(BeginNextWave가 다음 EnterPrep
            // 이후에야 호출됨) LocalWave/StageIndex는 여전히 "방금 끝난 웨이브"를 가리킨다.
            bool wasStageFinale = Waves.LocalWave == GameConstants.WavesPerStage;
            bool wasLastStage = Waves.StageIndex >= GameConstants.TotalStages - 1;

            if (wasStageFinale && wasLastStage)
            {
                Victory();
                return;
            }

            if (wasStageFinale)
            {
                // [해설] 길 교체와 타워 철거를 여기서 미리 해 둔다. 이 다음에 보상 선택과 준비
                // 시간이 오므로, 플레이어는 새 길을 보면서 타워를 새로 배치할 수 있다.
                // (예전에는 다음 웨이브가 "시작"될 때 교체해서, 준비 시간에 세운 타워가
                //  웨이브 시작과 동시에 지워졌다 — WaveManager.BeginNextWave 해설 참고.)
                //
                // ★ 배너 순서 주의: PrepareNextStage 안에서 "타워 N개 철거" 배너를 띄우기 때문에,
                //   스테이지 클리어 배너를 <b>먼저</b> 띄우면 같은 프레임에 덮여서 한 프레임도
                //   보이지 않는다. 그래서 철거를 먼저 하고, 두 소식을 한 배너로 합쳐서 띄운다.
                int stageJustCleared = Waves.StageNumber;
                Waves.PrepareNextStage();
                UI.ShowBanner($"STAGE {stageJustCleared} 클리어! 다음 스테이지 — 새 길에 맞춰 타워를 다시 배치하세요.");
            }

            State = GameState.Reward;
            UI.ShowRewardPanel(OnRewardChosen);
        }

        private void OnRewardChosen(UpgradeOption opt)
        {
            ApplyUpgrade(opt);
            UI.HideRewardPanel();
            EnterPrep();
        }

        /// <summary>"수용력 강화" 보상 1회당 올려 주는 동시 생존 허용 한도.
        /// UIManager.AllUpgrades의 안내 문구("동시 생존 허용 한도 +6")와 반드시 같은 값이어야 한다.</summary>
        public const int AliveCapacityBonus = 6;

        /// <summary>"○○탑 강화" 보상 1회당 <b>그 종류</b>의 공격력에 곱해지는 배율.
        /// [해설] 예전 전체 강화는 ×1.2였는데, 이제 네 종류 중 하나에만 붙으므로 같은 값이면
        /// 체감이 4분의 1로 줄어든다. 한 종류에 몰아주는 선택이 의미 있도록 ×1.3으로 올렸다.
        /// UIManager.AllUpgrades의 안내 문구(+30%)와 반드시 같은 값이어야 한다.</summary>
        public const float TowerDamageBonus = 1.3f;

        private void ApplyUpgrade(UpgradeOption opt)
        {
            switch (opt.Kind)
            {
                case UpgradeKind.AliveCapacity:
                    Waves.IncreaseAliveCapacity(AliveCapacityBonus);
                    break;
                case UpgradeKind.GoldGain:
                    // [해설] 이 배율이 이제 처치 보상뿐 아니라 웨이브 클리어 보너스에도 적용되므로
                    // (OnWaveClearedHandler 참고), 실질 효과가 커진 만큼 +20% → +10%로 낮춘다.
                    // 안내 문구는 UIManager.AllUpgrades의 "골드 획득량 +10%"와 짝을 맞춰야 한다.
                    _goldMultiplier *= 1.1f;
                    break;
                case UpgradeKind.TowerDamage:
                    // ★ 모든 타워가 아니라 고른 종류 하나에만 누적된다(UpgradeOption.Target).
                    TowerBase.MultiplyDamage(opt.Target, TowerDamageBonus);
                    break;
            }
            UI.ShowBanner($"업그레이드 적용: {opt.Label}");
        }

        /// <summary>
        /// 게임이 끝난 뒤에도 돌아가던 적/타워의 Update를 멈춘다.
        /// [해설] ★ 버그 수정. 예전에는 종료 화면을 띄운 뒤에도 세상이 그대로 돌아갔다.
        /// 적은 닫힌 루프를 영원히 돌고, 타워는 계속 쏘고, 보스는 능력을 계속 써서 소환·골드
        /// 약탈·배너가 종료 화면 뒤에서 무한히 반복됐다. 플레이어는 멈출 수도 없다 — 종료
        /// 상태에서는 일시정지 버튼이 숨겨지고 CanPause도 false이기 때문이다. 화면이 거의
        /// 불투명한 패널로 덮여 있어 눈에는 안 보이지만, CPU는 계속 타고 있었다.
        ///
        /// Time.timeScale을 0으로 만들지 않은 이유: 종료 패널의 연출과 배너가 모두 스케일된
        /// 시간을 쓰기 때문에 같이 얼어붙는다. 그래서 시간이 아니라 "배우"만 멈춘다.
        /// enabled = false는 OnDisable → Active.Remove를 부르므로, 순회 중에 목록이 바뀌지
        /// 않도록 반드시 복사본을 돌아야 한다.
        /// </summary>
        private static void HaltActors()
        {
            foreach (var t in new List<TowerBase>(TowerBase.Active))
                if (t != null) t.enabled = false;
            foreach (var e in new List<EnemyController>(EnemyController.Active))
                if (e != null) e.enabled = false;
        }

        private void GameOver(string reason)
        {
            // 일시정지 상태에서 패배 판정이 날 일은 없지만(시간이 멈춰 있으니), 혹시라도 멈춘 채로
            // 종료 화면에 들어가면 버튼만 살아있고 화면이 얼어붙은 이상한 상태가 되므로 풀어준다.
            SetPaused(false);
            State = GameState.GameOver;
            Waves.HaltForGameEnd();
            if (Build != null) Build.enabled = false;
            HaltActors();
            UI.ShowGameOver(Waves.StageNumber, Waves.LocalWave, reason);
        }

        private void Victory()
        {
            SetPaused(false);
            State = GameState.Victory;
            Waves.HaltForGameEnd();
            if (Build != null) Build.enabled = false;
            HaltActors();
            UI.ShowVictory();
        }
    }
}
