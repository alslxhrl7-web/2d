using UnityEngine;

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
            if (_incomeSuppressTimer > 0f) _incomeSuppressTimer -= Time.deltaTime;

            if (State == GameState.Prep)
            {
                _prepTimer -= Time.deltaTime;
                UI.SetPrepCountdown(Mathf.Max(0f, _prepTimer));
                if (_prepTimer <= 0f) StartDefense();
            }

            if (Waves != null) UI.RefreshAliveCount(Waves.AliveEnemies);
            UI.RefreshActionButton();
        }

        public void EnterPrep()
        {
            State = GameState.Prep;
            _prepTimer = GameConstants.PrepPhaseSeconds;
            int nextWave = Waves.CurrentWave + 1;
            bool nextIsBoss = nextWave % GameConstants.BossWaveInterval == 0;
            UI.ShowPrepPanel(nextWave, nextIsBoss);
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

        public void AddGold(int amount)
        {
            if (_incomeSuppressTimer > 0f) return;
            Gold += Mathf.RoundToInt(amount * _goldMultiplier);
            UI.RefreshGold();
        }

        public void SpendGold(int amount)
        {
            Gold -= amount;
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

        public void SuppressIncomeBriefly(float seconds)
        {
            _incomeSuppressTimer = Mathf.Max(_incomeSuppressTimer, seconds);
        }

        public void ShowBanner(string text) => UI.ShowBanner(text);

        public void OnWaveClearedHandler(int waveNumber)
        {
            Gold += 15 + waveNumber;
            UI.RefreshGold();

            if (waveNumber >= GameConstants.TotalWaves)
            {
                Victory();
                return;
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

        private void ApplyUpgrade(UpgradeOption opt)
        {
            switch (opt.Kind)
            {
                case UpgradeKind.AliveCapacity:
                    Waves.IncreaseAliveCapacity(1.15f);
                    break;
                case UpgradeKind.GoldGain:
                    _goldMultiplier *= 1.2f;
                    break;
                case UpgradeKind.TowerDamage:
                    TowerBase.GlobalDamageMultiplier *= 1.2f;
                    break;
            }
            UI.ShowBanner($"업그레이드 적용: {opt.Label}");
        }

        private void GameOver(string reason)
        {
            State = GameState.GameOver;
            Waves.StopAllCoroutines();
            if (Build != null) Build.enabled = false;
            UI.ShowGameOver(Waves.CurrentWave, reason);
        }

        private void Victory()
        {
            State = GameState.Victory;
            if (Build != null) Build.enabled = false;
            UI.ShowVictory();
        }
    }
}
