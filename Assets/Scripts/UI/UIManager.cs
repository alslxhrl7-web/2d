using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Defense2D
{
    /// <summary>
    /// HUD 전체를 코드로 생성/관리한다 (아트 리소스 없이 uGUI만 사용).
    /// 상단 바(거점 HP/웨이브/남은 적/골드), 행동력 표시, 건설 메뉴(TAB),
    /// 준비 단계 패널(다음 보스 힌트 포함), 배너, 보상 선택, 게임 종료 화면.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public GameManager Game;
        public WaveManager Waves;
        public BuildManager Build;
        public PlayerController Player;

        private Canvas _canvas;
        private Font _font;
        private Image _fillImage;

        private Text _hpText;
        private Text _waveText;
        private Text _enemiesText;
        private Text _goldText;
        private readonly List<Image> _apPips = new List<Image>();

        private GameObject _buildPanel;
        private GameObject _prepPanel;
        private Text _prepText;
        private Text _prepCountdownText;
        private Text _bossHintText;

        private GameObject _bannerRoot;
        private Text _bannerText;
        private Coroutine _bannerRoutine;

        private GameObject _bossBanner;
        private Text _bossBannerText;

        private GameObject _rewardPanel;
        private readonly List<GameObject> _rewardButtons = new List<GameObject>();

        private GameObject _endPanel;
        private Text _endText;

        private static readonly Dictionary<int, string> BossHints = new Dictionary<int, string>
        {
            { 1, "패턴: 직선으로 돌진하고, 돌진 직후 잠시 약점이 노출됩니다." },
            { 2, "패턴: 주기적으로 타워 하나를 잠시 무력화합니다. 여러 타워로 분산 대응하세요." },
            { 3, "패턴: 주기적으로 잡몹을 소환합니다. 소환된 잡몹부터 정리하세요." },
            { 4, "패턴: 거점을 직접 공격하고 골드 수급을 방해합니다. 미리 거점 체력을 확보하세요." },
            { 5, "패턴: 체력에 따라 3페이즈로 변하며 이전 보스들의 패턴을 섞어 사용합니다." },
        };

        private static readonly List<UpgradeOption> AllUpgrades = new List<UpgradeOption>
        {
            new UpgradeOption{ Kind = UpgradeKind.PlayerDamage, Label = "공격력 강화", Description = "플레이어 공격력 +25%" },
            new UpgradeOption{ Kind = UpgradeKind.PlayerAttackSpeed, Label = "공격속도 강화", Description = "플레이어 공격 속도 증가" },
            new UpgradeOption{ Kind = UpgradeKind.PlayerRange, Label = "사거리 강화", Description = "플레이어 공격 사거리 +20%" },
            new UpgradeOption{ Kind = UpgradeKind.PlayerMoveSpeed, Label = "이동속도 강화", Description = "플레이어 이동속도 +15%" },
            new UpgradeOption{ Kind = UpgradeKind.BaseMaxHp, Label = "거점 보강", Description = "거점 최대 체력 +15% 및 즉시 일부 회복" },
            new UpgradeOption{ Kind = UpgradeKind.SkillCooldown, Label = "스킬 숙련", Description = "행동력 회복 속도 증가" },
            new UpgradeOption{ Kind = UpgradeKind.GoldGain, Label = "재화 감각", Description = "골드 획득량 +20%" },
            new UpgradeOption{ Kind = UpgradeKind.TowerDamage, Label = "타워 강화", Description = "모든 타워 공격력 +20%" },
        };

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildTopBar();
            BuildActionPoints();
            BuildBuildMenu();
            BuildPrepPanel();
            BuildBanner();
            BuildBossBanner();
            BuildRewardPanel();
            BuildEndPanel();
            BuildHintText();
        }

        // ---------- 뼈대 ----------

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // 런타임 생성 시 기본 바인딩이 비어있어 직접 할당 필요
        }

        private void BuildCanvas()
        {
            EnsureEventSystem();
            var canvasGO = new GameObject("Canvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.text = content;
            txt.alignment = anchor;
            return txt;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 sizeDelta,
            Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax, Action onClick, Color? bg = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = bg ?? new Color(0.15f, 0.2f, 0.3f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = 18;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;

            return btn;
        }

        // ---------- 상단 바 ----------

        private void BuildTopBar()
        {
            var bar = CreatePanel("TopBar", _canvas.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, 74), Vector2.zero, new Color(0.05f, 0.08f, 0.15f, 0.85f));

            var hpBg = CreatePanel("HPBarBG", bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, 22), new Vector2(20, -14), new Color(1, 1, 1, 0.15f));

            var hpFillGO = new GameObject("HPFill");
            hpFillGO.transform.SetParent(hpBg, false);
            var fillRt = hpFillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = hpFillGO.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.85f, 0.4f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            _fillImage = fillImg;

            _hpText = CreateText("HPText", bar, "거점 HP 100/100", 14, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, 20), new Vector2(150, -14));

            _waveText = CreateText("WaveText", bar, "WAVE 00 / 25", 22, new Color(0.4f, 0.75f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(260, 30), new Vector2(0, -20));

            _enemiesText = CreateText("EnemiesText", bar, "남은 적 0", 16, Color.white, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(160, 24), new Vector2(-190, -16));

            _goldText = CreateText("GoldText", bar, "골드 0", 18, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(160, 24), new Vector2(-20, -16));
        }

        private void BuildActionPoints()
        {
            var row = CreatePanel("ActionPoints", _canvas.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(260, 40), new Vector2(20, 20), new Color(0, 0, 0, 0));

            for (int i = 0; i < GameConstants.MaxActionPoints; i++)
            {
                var pipGo = new GameObject($"Pip{i}");
                pipGo.transform.SetParent(row, false);
                var rt = pipGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(28, 28);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(16 + i * 34, 16);
                var img = pipGo.AddComponent<Image>();
                img.sprite = SpriteFactory.Circle(new Color(1f, 0.65f, 0.2f), Color.white);
                img.color = Color.white;
                _apPips.Add(img);
            }
        }

        public void RefreshActionPoints()
        {
            for (int i = 0; i < _apPips.Count; i++)
                _apPips[i].color = i < Player.ActionPoints ? Color.white : new Color(1, 1, 1, 0.2f);
        }

        // ---------- 건설 메뉴 ----------

        private void BuildBuildMenu()
        {
            _buildPanel = CreatePanel("BuildMenu", _canvas.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(220, 220), new Vector2(-20, 20), new Color(0.05f, 0.08f, 0.15f, 0.92f)).gameObject;

            CreateText("BuildTitle", _buildPanel.transform, "건설 메뉴 (TAB)", 16, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 24), new Vector2(0, -16));

            string[] names = { "화살탑", "빙결탑", "포격탑" };
            TowerType[] types = { TowerType.Arrow, TowerType.Ice, TowerType.Cannon };
            Color[] colors =
            {
                new Color(0.24f, 0.4f, 0.78f), new Color(0.35f, 0.85f, 0.92f), new Color(0.95f, 0.58f, 0.28f)
            };

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                CreateButton($"TowerBtn{i}", _buildPanel.transform, $"{names[i]}\n비용 {GameConstants.TowerCost}",
                    new Vector2(190, 46), new Vector2(0, -50 - i * 54), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    () => Build.SelectTower(types[idx]), colors[i] * 0.55f + new Color(0, 0, 0, 0.4f));
            }

            _buildPanel.SetActive(false);
        }

        public void SetBuildMenuOpen(bool open) => _buildPanel.SetActive(open);

        private void BuildHintText()
        {
            CreateText("Hint", _canvas.transform, "WASD 이동 · 마우스 클릭 배치 · SPACE 스킬 · TAB 건설",
                14, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(700, 24), new Vector2(0, 8));
        }

        // ---------- 준비 단계 ----------

        private void BuildPrepPanel()
        {
            _prepPanel = CreatePanel("PrepPanel", _canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(560, 90), new Vector2(0, -90), new Color(0.05f, 0.08f, 0.15f, 0.88f)).gameObject;

            _prepText = CreateText("PrepText", _prepPanel.transform, "다음 웨이브 준비 중...", 18, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, 26), new Vector2(-60, -14));

            _prepCountdownText = CreateText("PrepCountdown", _prepPanel.transform, "", 20, new Color(1f, 0.85f, 0.3f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, 26), new Vector2(220, -14));

            _bossHintText = CreateText("BossHint", _prepPanel.transform, "", 14, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 22), new Vector2(0, -40));

            CreateButton("SkipPrepBtn", _prepPanel.transform, "지금 시작", new Vector2(120, 30), new Vector2(0, -68),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), () => Game.SkipPrep());
        }

        public void ShowPrepPanel(int nextWave, bool nextIsBoss)
        {
            _prepPanel.SetActive(true);
            _prepText.text = nextIsBoss ? $"WAVE {nextWave} - 보스 웨이브 준비!" : $"WAVE {nextWave} 준비 중...";
            int bossIdx = nextWave / GameConstants.BossWaveInterval;
            _bossHintText.text = (nextIsBoss && BossHints.ContainsKey(bossIdx)) ? BossHints[bossIdx] : "";
        }

        public void SetPrepCountdown(float t)
        {
            if (_prepCountdownText != null) _prepCountdownText.text = $"{Mathf.CeilToInt(t)}s";
        }

        public void HidePrepPanel() => _prepPanel.SetActive(false);

        // ---------- 배너 ----------

        private void BuildBanner()
        {
            _bannerRoot = CreatePanel("Banner", _canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(600, 40), new Vector2(0, -140), new Color(0, 0, 0, 0)).gameObject;
            _bannerText = CreateText("BannerText", _bannerRoot.transform, "", 20, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600, 36), Vector2.zero);
            _bannerRoot.SetActive(false);
        }

        public void ShowBanner(string text)
        {
            _bannerText.text = text;
            _bannerRoot.SetActive(true);
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(HideBannerAfter(2.2f));
        }

        private IEnumerator HideBannerAfter(float t)
        {
            yield return new WaitForSeconds(t);
            _bannerRoot.SetActive(false);
        }

        private void BuildBossBanner()
        {
            _bossBanner = CreatePanel("BossBanner", _canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(800, 100), Vector2.zero, new Color(0.5f, 0.05f, 0.1f, 0.9f)).gameObject;
            _bossBannerText = CreateText("BossBannerText", _bossBanner.transform, "", 30, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760, 80), Vector2.zero);
            _bossBanner.SetActive(false);
        }

        public void ShowBossBanner(int bossIndex, string bossName)
        {
            _bossBannerText.text = $"BOSS INCOMING\n{bossName}";
            _bossBanner.SetActive(true);
            StartCoroutine(HideBossBannerAfter(2.4f));
        }

        private IEnumerator HideBossBannerAfter(float t)
        {
            yield return new WaitForSeconds(t);
            _bossBanner.SetActive(false);
        }

        // ---------- 보상 선택 ----------

        private void BuildRewardPanel()
        {
            _rewardPanel = CreatePanel("RewardPanel", _canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(760, 260), Vector2.zero, new Color(0.05f, 0.08f, 0.15f, 0.95f)).gameObject;

            CreateText("RewardTitle", _rewardPanel.transform, "웨이브 클리어! 업그레이드를 선택하세요", 20, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(720, 30), new Vector2(0, -20));

            _rewardPanel.SetActive(false);
        }

        public void ShowRewardPanel(Action<UpgradeOption> onChosen)
        {
            foreach (var b in _rewardButtons) Destroy(b);
            _rewardButtons.Clear();

            var pool = new List<UpgradeOption>(AllUpgrades);
            var picks = new List<UpgradeOption>();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                picks.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            for (int i = 0; i < picks.Count; i++)
            {
                var opt = picks[i];
                var btn = CreateButton($"RewardBtn{i}", _rewardPanel.transform, $"{opt.Label}\n{opt.Description}",
                    new Vector2(220, 140), new Vector2(-260 + i * 260, -30), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    () => onChosen(opt), new Color(0.15f, 0.25f, 0.4f, 0.95f));
                _rewardButtons.Add(btn.gameObject);
            }

            _rewardPanel.SetActive(true);
        }

        public void HideRewardPanel() => _rewardPanel.SetActive(false);

        // ---------- 종료 화면 ----------

        private void BuildEndPanel()
        {
            _endPanel = CreatePanel("EndPanel", _canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.04f, 0.92f)).gameObject;

            _endText = CreateText("EndText", _endPanel.transform, "", 30, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800, 140), new Vector2(0, 30));

            CreateButton("RestartBtn", _endPanel.transform, "다시 시작", new Vector2(160, 40), new Vector2(0, -60),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));

            _endPanel.SetActive(false);
        }

        public void ShowGameOver(int reachedWave)
        {
            _endText.text = $"거점이 함락되었습니다\n도달 웨이브: {reachedWave} / {GameConstants.TotalWaves}";
            _endPanel.SetActive(true);
        }

        public void ShowVictory()
        {
            _endText.text = "25 웨이브 클리어!\n모든 보스를 물리쳤습니다.";
            _endPanel.SetActive(true);
        }

        // ---------- 갱신 ----------

        public void RefreshGold() => _goldText.text = $"골드 {Game.Gold}";

        public void RefreshBaseHP()
        {
            _hpText.text = $"거점 HP {Mathf.CeilToInt(Game.BaseHP)}/{Mathf.CeilToInt(Game.BaseMaxHP)}";
            _fillImage.fillAmount = Mathf.Clamp01(Game.BaseHP / Game.BaseMaxHP);
        }

        public void RefreshWave(int wave) => _waveText.text = $"WAVE {wave:00} / {GameConstants.TotalWaves}";

        public void RefreshAliveCount(int count) => _enemiesText.text = $"남은 적 {count}";
    }
}
