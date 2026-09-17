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
    /// 상단 바(생존 한도/웨이브/남은 적/골드), 행동력 표시, 건설 메뉴(TAB),
    /// 준비 단계 패널(다음 보스 힌트 포함), 배너, 보상 선택, 게임 종료 화면.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public GameManager Game;
        public WaveManager Waves;
        public BuildManager Build;

        private Canvas _canvas;
        private Font _font;
        private Image _fillImage;

        private Text _crowdText;
        private Text _waveText;
        private Text _enemiesText;
        private Text _goldText;

        // 준비 단계의 "웨이브 시작" 버튼과 방어 단계의 "웨이브 스킵" 버튼을 하나로 통합한
        // 단일 액션 버튼. 항상 같은 자리에 있고, 현재 상태에 맞는 동작/문구로 바뀐다.
        private enum ActionMode { None, StartWave, SkipWave }
        private Button _actionButton;
        private Text _actionLabel;
        private ActionMode _actionMode = ActionMode.None;

        private GameObject _buildPanel;
        private Text _buildGoldText;
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
            { 4, "패턴: 골드를 약탈하고 수급을 방해합니다. 미리 골드를 소비해 두는 것이 안전합니다." },
            { 5, "패턴: 체력에 따라 3페이즈로 변하며 이전 보스들의 패턴을 섞어 사용합니다." },
        };

        private static readonly List<UpgradeOption> AllUpgrades = new List<UpgradeOption>
        {
            new UpgradeOption{ Kind = UpgradeKind.AliveCapacity, Label = "수용력 강화", Description = "동시 생존 허용 한도 +15%" },
            new UpgradeOption{ Kind = UpgradeKind.GoldGain, Label = "재화 감각", Description = "골드 획득량 +20%" },
            new UpgradeOption{ Kind = UpgradeKind.TowerDamage, Label = "타워 강화", Description = "모든 타워 공격력 +20%" },
        };

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildTopBar();
            BuildActionButton();
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

            var hpBg = CreatePanel("CrowdBarBG", bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, 22), new Vector2(20, -14), new Color(1, 1, 1, 0.15f));

            var hpFillGO = new GameObject("CrowdFill");
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
            fillImg.fillAmount = 0f;
            _fillImage = fillImg;

            // [해설] 거점 체력 대신, "동시 생존 허용 한도" 대비 현재 생존 적 수를 보여주는
            // 위험도 게이지로 재활용한다 (가득 찰수록 게임 오버(적에게 압도당함)에 가까워짐).
            _crowdText = CreateText("CrowdText", bar, "생존 한도 0/50", 14, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, 20), new Vector2(150, -14));

            _waveText = CreateText("WaveText", bar, "WAVE 00 / 25", 22, new Color(0.4f, 0.75f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(260, 30), new Vector2(0, -20));

            _enemiesText = CreateText("EnemiesText", bar, "남은 적 0", 16, Color.white, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(160, 24), new Vector2(-190, -16));

            _goldText = CreateText("GoldText", bar, "골드 0", 18, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(160, 24), new Vector2(-20, -16));
        }

        // ---------- 웨이브 시작/스킵 통합 버튼 ----------

        private void BuildActionButton()
        {
            // [해설] "남은 적" 숫자(x=-190) 바로 아래에 붙도록 x를 맞췄다. 또한 이전에 버튼을
            // 줄였을 때(150x34→128x28) Text 기본 Wrap+Truncate 설정 때문에 "웨이브 시작"의
            // 마지막 글자가 잘려 보이는 문제가 있었다 — 폭을 140으로 살짝 늘리고, 라벨의
            // overflow 모드를 Overflow로 바꿔 어떤 문구든(스킵까지 30초 등) 절대 잘리지 않게 했다.
            _actionButton = CreateButton("ActionBtn", _canvas.transform, "웨이브 시작", new Vector2(140, 30),
                new Vector2(-190, -90), new Vector2(1, 1), new Vector2(1, 1),
                OnActionButtonClicked, new Color(0.2f, 0.55f, 0.25f, 0.92f));
            _actionLabel = _actionButton.GetComponentInChildren<Text>();
            _actionLabel.fontSize = 15;
            _actionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _actionLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _actionButton.gameObject.SetActive(false);
        }

        private void OnActionButtonClicked()
        {
            switch (_actionMode)
            {
                case ActionMode.StartWave:
                    Game.SkipPrep();
                    break;
                case ActionMode.SkipWave:
                    Waves.SkipWave();
                    break;
            }
        }

        /// <summary>
        /// 준비 단계면 "웨이브 시작", 방어 단계(보스 제외)면 "웨이브 스킵"으로 같은 버튼이 동작한다.
        /// 매 프레임 현재 게임 상태를 보고 버튼의 표시/문구/클릭 동작을 갱신한다.
        /// </summary>
        public void RefreshActionButton()
        {
            if (_actionButton == null || Game == null) return;

            if (Game.State == GameState.Prep)
            {
                _actionMode = ActionMode.StartWave;
                _actionButton.gameObject.SetActive(true);
                _actionButton.interactable = true;
                _actionLabel.text = "웨이브 시작";
                return;
            }

            if (Game.State == GameState.Defense && Waves != null && Waves.WaveInProgress && !Waves.IsBossWave)
            {
                _actionMode = ActionMode.SkipWave;
                _actionButton.gameObject.SetActive(true);
                bool canSkip = Waves.CanSkipWave;
                _actionButton.interactable = canSkip;
                _actionLabel.text = canSkip ? "웨이브 스킵!" : $"스킵까지 {Mathf.CeilToInt(Waves.SkipUnlockRemaining)}초";
                return;
            }

            _actionMode = ActionMode.None;
            _actionButton.gameObject.SetActive(false);
        }

        // ---------- 건설 메뉴 ----------

        private void BuildBuildMenu()
        {
            // [해설] 타워 종류를 직접 고르지 않고, 설치 시점에 타입이 무작위로 결정되도록 바뀌면서
            // 타입별 버튼 3개 대신 "타워 설치" 버튼 하나로 단순화했다.
            _buildPanel = CreatePanel("BuildMenu", _canvas.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(220, 210), new Vector2(-20, 20), new Color(0.05f, 0.08f, 0.15f, 0.92f)).gameObject;

            CreateText("BuildTitle", _buildPanel.transform, "건설 메뉴 (TAB)", 16, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 24), new Vector2(0, -16));

            CreateText("BuildInfo", _buildPanel.transform,
                $"화살탑/빙결탑 {GameConstants.TowerCost} · 포격탑 {GameConstants.CannonTowerCost}\n설치 시 타입이 무작위로 결정됩니다",
                13, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(200, 40), new Vector2(0, -56));

            CreateButton("PlaceBtn", _buildPanel.transform, "타워 설치", new Vector2(190, 46), new Vector2(0, -116),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), () => Build.BeginPlacement(),
                new Color(0.3f, 0.4f, 0.6f, 0.9f));

            // 건설 메뉴 안에서도 현재 보유 골드가 바로 보이도록 표시 (실제 값은 RefreshGold에서 갱신)
            _buildGoldText = CreateText("BuildGoldText", _buildPanel.transform, "보유 골드 0", 15,
                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(200, 22), new Vector2(0, -176));

            _buildPanel.SetActive(false);
        }

        public void SetBuildMenuOpen(bool open) => _buildPanel.SetActive(open);

        private void BuildHintText()
        {
            CreateText("Hint", _canvas.transform, "마우스 클릭으로 타워 배치 · TAB 건설 메뉴",
                14, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(700, 24), new Vector2(0, 8));
        }

        // ---------- 준비 단계 ----------

        private void BuildPrepPanel()
        {
            // "지금 시작" 버튼은 우측 상단의 통합 액션 버튼(웨이브 시작/스킵)으로 옮겨서 그만큼 패널을 낮췄다.
            _prepPanel = CreatePanel("PrepPanel", _canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(560, 64), new Vector2(0, -90), new Color(0.05f, 0.08f, 0.15f, 0.88f)).gameObject;

            _prepText = CreateText("PrepText", _prepPanel.transform, "다음 웨이브 준비 중...", 18, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, 26), new Vector2(-60, -14));

            _prepCountdownText = CreateText("PrepCountdown", _prepPanel.transform, "", 20, new Color(1f, 0.85f, 0.3f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, 26), new Vector2(220, -14));

            _bossHintText = CreateText("BossHint", _prepPanel.transform, "", 14, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 22), new Vector2(0, -40));
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

        public void ShowGameOver(int reachedWave, string reason = "게임 오버")
        {
            _endText.text = $"{reason}\n도달 웨이브: {reachedWave} / {GameConstants.TotalWaves}";
            _endPanel.SetActive(true);
        }

        public void ShowVictory()
        {
            _endText.text = "25 웨이브 클리어!\n모든 보스를 물리쳤습니다.";
            _endPanel.SetActive(true);
        }

        // ---------- 갱신 ----------

        public void RefreshGold()
        {
            _goldText.text = $"골드 {Game.Gold}";
            if (_buildGoldText != null) _buildGoldText.text = $"보유 골드 {Game.Gold}";
        }

        public void RefreshWave(int wave) => _waveText.text = $"WAVE {wave:00} / {GameConstants.TotalWaves}";

        /// <summary>
        /// [해설] 거점 체력 대신 "동시 생존 허용 한도" 대비 현재 생존 적 수를 함께 갱신한다.
        /// 한도에 가까워질수록(=적에게 압도당해 게임 오버되는 조건에 가까워질수록) 게이지가
        /// 붉게 물든다.
        /// </summary>
        public void RefreshAliveCount(int count)
        {
            _enemiesText.text = $"남은 적 {count}";

            int cap = Waves != null ? Waves.MaxAliveEnemies : count;
            _crowdText.text = $"생존 한도 {count}/{cap}";
            float ratio = cap > 0 ? Mathf.Clamp01((float)count / cap) : 0f;
            _fillImage.fillAmount = ratio;
            _fillImage.color = Color.Lerp(new Color(0.3f, 0.85f, 0.4f), new Color(0.9f, 0.25f, 0.2f), ratio);
        }
    }
}
