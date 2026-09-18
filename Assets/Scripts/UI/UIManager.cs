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
    /// 상단 바(생존 한도/웨이브/남은 적), 건설 메뉴(TAB, 보유 골드 표시 포함),
    /// 준비 단계 패널(다음 보스 힌트 포함), 배너, 보상 선택, 게임 종료 화면.
    /// [해설] 상단 바의 골드 표기는 제거되었다(보유 골드는 건설 메뉴 안에서 확인).
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

        private GameObject _pausePanel;
        private Button _pauseButton;
        private Button _speedButton;

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
            new UpgradeOption{ Kind = UpgradeKind.AliveCapacity, Label = "수용력 강화", Description = $"동시 생존 허용 한도 +{GameManager.AliveCapacityBonus}" },
            new UpgradeOption{ Kind = UpgradeKind.GoldGain, Label = "재화 감각", Description = "골드 획득량 +10%" },
            new UpgradeOption{ Kind = UpgradeKind.TowerDamage, Label = "타워 강화", Description = "모든 타워 공격력 +20%" },
        };

        /// <summary>프로젝트에 내장한 한글 폰트의 Resources 경로(확장자 제외).</summary>
        private const string KoreanFontResourcePath = "Fonts/NanumGothic-Subset";

        private void Awake()
        {
            _font = LoadUiFont();
            BuildCanvas();
            BuildTopBar();
            BuildActionButton();
            BuildBuildMenu();
            BuildPrepPanel();
            BuildBanner();
            BuildBossBanner();
            BuildRewardPanel();
            // 일시정지 패널은 종료 화면보다 먼저 만든다 — uGUI는 나중에 만든 것이 위에 그려지므로,
            // 게임오버 화면이 일시정지 화면을 덮도록 하려면 이 순서여야 한다.
            BuildPausePanel();
            BuildEndPanel();
            BuildHintText();
        }

        // ---------- 뼈대 ----------

        /// <summary>
        /// HUD 전체가 사용할 폰트를 고른다.
        /// [해설] ★ 한글이 안 보이던 원인이 여기였다. 예전에는 유니티 내장 폰트
        /// <c>LegacyRuntime.ttf</c>(Liberation Sans)를 썼는데, 이 폰트에는 <b>한글 글리프가
        /// 아예 없다</b>. 에디터에서는 운영체제 폰트로 대충 대체되어 보이던 것이, 빌드한
        /// 실행 파일에서는 대체가 되지 않아 한글이 전부 빈칸으로 나왔다. 특히 WebGL(브라우저)
        /// 빌드에는 기댈 OS 폰트 자체가 없으므로, 한글을 쓰려면 폰트를 프로젝트에 직접
        /// 넣는 것 말고는 방법이 없다.
        /// 그래서 나눔고딕 서브셋(현대 한글 음절 11,172자 전체 포함, 약 1.8MB)을
        /// Resources/Fonts에 넣고 그것을 우선 사용한다. 어떤 이유로든 못 찾으면 예전 내장
        /// 폰트로 물러나서, 한글은 안 보여도 게임 자체는 돌아가게 한다.
        /// </summary>
        private static Font LoadUiFont()
        {
            var korean = Resources.Load<Font>(KoreanFontResourcePath);
            if (korean != null) return korean;

            Debug.LogWarning(
                $"[UI] 한글 폰트를 찾지 못했습니다 (Assets/Resources/{KoreanFontResourcePath}.ttf). " +
                "내장 폰트로 대체하며, 이 경우 한글은 표시되지 않습니다.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

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

            _waveText = CreateText("WaveText", bar, "STAGE 1 · WAVE 00", 22, new Color(0.4f, 0.75f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(260, 30), new Vector2(0, -20));

            _enemiesText = CreateText("EnemiesText", bar, "남은 적 0", 16, Color.white, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(160, 24), new Vector2(-190, -16));

            // [해설] "위에 골드표기창은 지우고" 요청에 따라 상단 바의 골드 표시(GoldText)를
            // 제거했다. 건설 메뉴(TAB) 안의 "보유 골드" 표시(_buildGoldText)는 그대로 남아있어
            // 타워를 설치하려 할 때는 여전히 골드를 확인할 수 있다.
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

            // [해설] 일시정지 버튼. 키(P/ESC)만 두면 브라우저(WebGL)에서 캔버스에 키보드 포커스가
            // 없을 때 눌러도 반응이 없어서, 마우스로도 언제든 멈출 수 있게 버튼을 함께 둔다.
            // 상단 바에서 골드 표기를 걷어내며 비어 있던 오른쪽 끝 자리를 쓴다.
            _pauseButton = CreateButton("PauseBtn", _canvas.transform, "일시정지 (P)", new Vector2(104, 26),
                new Vector2(-58, -16), new Vector2(1, 1), new Vector2(1, 1),
                () => Game.TogglePause(), new Color(0.18f, 0.22f, 0.34f, 0.92f));
            var pauseLabel = _pauseButton.GetComponentInChildren<Text>();
            pauseLabel.fontSize = 13;
            pauseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            pauseLabel.verticalOverflow = VerticalWrapMode.Overflow;

            // [해설] 배속 버튼. 일시정지 버튼 왼쪽에 붙인다. 키(F)만으로도 되지만, WebGL에서
            // 캔버스에 키보드 포커스가 없을 때를 대비해 일시정지와 마찬가지로 버튼을 함께 둔다.
            _speedButton = CreateButton("SpeedBtn", _canvas.transform, "배속 x1", new Vector2(74, 26),
                new Vector2(-150, -16), new Vector2(1, 1), new Vector2(1, 1),
                () => Game.CycleSpeed(), new Color(0.18f, 0.3f, 0.26f, 0.92f));
            var speedLabel = _speedButton.GetComponentInChildren<Text>();
            speedLabel.fontSize = 13;
            speedLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            speedLabel.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>배속 버튼의 표기를 현재 배속에 맞춘다. GameManager.CycleSpeed가 호출한다.</summary>
        public void RefreshSpeedButton()
        {
            if (_speedButton == null) return;
            _speedButton.GetComponentInChildren<Text>().text = $"배속 x{Game.GameSpeed:0.#}";
        }

        // ---------- 일시정지 ----------

        private void BuildPausePanel()
        {
            // 화면 전체를 덮는 패널이라, 뒤쪽 버튼(건설 메뉴 등)이 실수로 눌리는 것도 함께 막아준다.
            _pausePanel = CreatePanel("PausePanel", _canvas.transform, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.06f, 0.82f)).gameObject;

            CreateText("PauseTitle", _pausePanel.transform, "일시정지", 38, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600, 56), new Vector2(0, 86));

            CreateText("PauseHint", _pausePanel.transform, "P 또는 ESC로 계속할 수 있습니다", 16,
                new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600, 26), new Vector2(0, 42));

            CreateButton("ResumeBtn", _pausePanel.transform, "계속하기", new Vector2(190, 46), new Vector2(0, -10),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), () => Game.SetPaused(false),
                new Color(0.2f, 0.5f, 0.28f, 0.95f));

            CreateButton("PauseRestartBtn", _pausePanel.transform, "처음부터 다시", new Vector2(190, 40),
                new Vector2(0, -66), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), RestartGame,
                new Color(0.2f, 0.26f, 0.4f, 0.95f));

            // 브라우저에서는 Application.Quit()이 아무것도 못 하므로 종료 버튼을 만들지 않는다
            // (종료 화면의 처리와 같은 이유 — BuildEndPanel 주석 참고).
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                CreateButton("PauseQuitBtn", _pausePanel.transform, "게임 종료", new Vector2(190, 40),
                    new Vector2(0, -114), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Application.Quit,
                    new Color(0.34f, 0.22f, 0.24f, 0.95f));
            }

            _pausePanel.SetActive(false);
        }

        /// <summary>GameManager.SetPaused()가 호출한다. 패널을 띄우고, 상단 일시정지 버튼의
        /// 문구를 현재 상태에 맞게 바꾼다.</summary>
        public void ShowPausePanel(bool paused)
        {
            if (_pausePanel != null) _pausePanel.SetActive(paused);
            if (_pauseButton != null)
                _pauseButton.GetComponentInChildren<Text>().text = paused ? "계속하기 (P)" : "일시정지 (P)";
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

            // 게임이 끝난 뒤에는 멈출 것이 없으므로 일시정지 버튼을 숨긴다.
            bool over = Game.State == GameState.GameOver || Game.State == GameState.Victory;
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(!over);
            if (_speedButton != null) _speedButton.gameObject.SetActive(!over);

            // 일시정지 중에는 "웨이브 시작/스킵" 버튼을 눌러 진행시킬 수 없어야 한다.
            if (Game.IsPaused)
            {
                _actionButton.gameObject.SetActive(false);
                _actionMode = ActionMode.None;
                return;
            }

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
            // 타입별 버튼 3개 대신 "타워 설치" 버튼 하나로 단순화했다. 이후 "타워 철거" 버튼이
            // 하나 더 추가되면서 패널 높이를 210 → 268로 늘렸다.
            _buildPanel = CreatePanel("BuildMenu", _canvas.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(220, 268), new Vector2(-20, 20), new Color(0.05f, 0.08f, 0.15f, 0.92f)).gameObject;

            CreateText("BuildTitle", _buildPanel.transform, "건설 메뉴 (TAB)", 16, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 24), new Vector2(0, -16));

            CreateText("BuildInfo", _buildPanel.transform,
                $"화살탑/빙결탑 {GameConstants.TowerCost} · 번개탑 {GameConstants.LightningTowerCost} · 포격탑 {GameConstants.CannonTowerCost}\n설치 시 타입이 무작위로 결정됩니다\n철거하면 건설비의 {GameConstants.TowerRefundPercent}%를 돌려받습니다",
                12, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(204, 54), new Vector2(0, -62));

            CreateButton("PlaceBtn", _buildPanel.transform, "타워 설치", new Vector2(190, 44), new Vector2(0, -122),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), () => Build.BeginPlacement(),
                new Color(0.3f, 0.4f, 0.6f, 0.9f));

            // [해설] 철거는 되돌리기 어려운 동작이므로, 설치 버튼(푸른 계열)과 확실히 구분되도록
            // 붉은 계열 색을 줬다.
            CreateButton("RemoveBtn", _buildPanel.transform, "타워 철거", new Vector2(190, 44), new Vector2(0, -172),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), () => Build.BeginRemoval(),
                new Color(0.52f, 0.24f, 0.26f, 0.92f));

            // 건설 메뉴 안에서도 현재 보유 골드가 바로 보이도록 표시 (실제 값은 RefreshGold에서 갱신)
            _buildGoldText = CreateText("BuildGoldText", _buildPanel.transform, "보유 골드 0", 15,
                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(200, 22), new Vector2(0, -222));

            _buildPanel.SetActive(false);
        }

        public void SetBuildMenuOpen(bool open) => _buildPanel.SetActive(open);

        private void BuildHintText()
        {
            CreateText("Hint", _canvas.transform, "TAB 건설 메뉴 · 클릭으로 타워 배치/철거 · 우클릭·ESC 취소 · P 일시정지 · F 배속",
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

        /// <summary>
        /// [해설] 스테이지 구조 개편에 따라 "다음 웨이브"를 스테이지 번호 + 스테이지 내 로컬 웨이브로
        /// 함께 표시한다. 피날레(스테이지 마지막 웨이브)는 보스+유닛 대량 스폰 및 제한시간 패배
        /// 조건이 있는 특별한 웨이브이므로, 중간 보스 웨이브와 문구를 다르게 보여준다.
        /// </summary>
        public void ShowPrepPanel(int stageNumber, int localWave, bool nextIsBoss, bool nextIsFinale, int bossPatternIndex)
        {
            _prepPanel.SetActive(true);
            if (nextIsFinale)
                _prepText.text = $"STAGE {stageNumber} - 피날레! 보스 + 대규모 유닛 (제한시간 {Mathf.RoundToInt(GameConstants.StageFinaleBossTimeLimit)}초)";
            else if (nextIsBoss)
                _prepText.text = $"STAGE {stageNumber} · WAVE {localWave} - 보스 웨이브 준비!";
            else
                _prepText.text = $"STAGE {stageNumber} · WAVE {localWave} 준비 중...";

            _bossHintText.text = (nextIsBoss && BossHints.ContainsKey(bossPatternIndex)) ? BossHints[bossPatternIndex] : "";
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

            var restart = CreateButton("RestartBtn", _endPanel.transform, "다시 시작", new Vector2(160, 40),
                new Vector2(-90, -60), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), RestartGame);

            // [해설] 독립 실행 파일(빌드)로 돌릴 때는 에디터의 정지 버튼이 없어서, 이 버튼이 없으면
            // Alt+F4 말고는 게임을 끝낼 방법이 없다. 에디터에서는 Application.Quit()이 아무 일도
            // 하지 않으므로(정상 동작) 버튼을 눌러도 무해하다.
            // 다만 브라우저(WebGL)에서는 Application.Quit()이 탭을 닫을 수 없어 아무 반응이 없는
            // "죽은 버튼"이 되므로, 그때는 아예 만들지 않고 "다시 시작"을 가운데로 옮긴다.
            bool canQuit = Application.platform != RuntimePlatform.WebGLPlayer;
            if (canQuit)
            {
                CreateButton("QuitBtn", _endPanel.transform, "게임 종료", new Vector2(160, 40), new Vector2(90, -60),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Application.Quit,
                    new Color(0.28f, 0.2f, 0.22f, 0.92f));
            }
            else
            {
                restart.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);
            }

            _endPanel.SetActive(false);
        }

        /// <summary>
        /// "다시 시작" — 현재 씬을 통째로 다시 불러와 완전히 새 게임으로 시작한다.
        /// [해설] 씬을 다시 여는 것만으로는 부족하다. 게임 조립 코드(GameBootstrapper)는 원래
        /// 게임 시작 시 한 번만 도는 구조였고 static 값(예: 타워 공격력 배율)은 씬을 넘어 살아남기
        /// 때문에, GameBootstrapper 쪽에서 sceneLoaded를 받아 다시 조립 + static 초기화를 하도록
        /// 함께 고쳤다. 자세한 내용은 GameBootstrapper.Bootstrap()의 주석 참고.
        /// </summary>
        private static void RestartGame()
        {
            var scene = SceneManager.GetActiveScene();
            // buildIndex는 씬이 Build Settings에 등록돼 있지 않으면 -1이고, LoadScene(-1)은 실패한다.
            // 지금은 등록돼 있지만, 나중에 씬이 바뀌어도 죽지 않도록 이름으로 한 번 더 시도한다.
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        public void ShowGameOver(int stageNumber, int localWave, string reason = "게임 오버")
        {
            _endText.text = $"{reason}\n도달 지점: STAGE {stageNumber} · WAVE {localWave} / {GameConstants.WavesPerStage}";
            _endPanel.SetActive(true);
        }

        public void ShowVictory()
        {
            _endText.text = $"STAGE {GameConstants.TotalStages} 클리어!\n모든 보스를 물리치고 게임을 완료했습니다.";
            _endPanel.SetActive(true);
        }

        // ---------- 갱신 ----------

        public void RefreshGold()
        {
            if (_buildGoldText != null) _buildGoldText.text = $"보유 골드 {Game.Gold}";
        }

        public void RefreshWave(int stageNumber, int localWave) =>
            _waveText.text = $"STAGE {stageNumber} · WAVE {localWave:00} / {GameConstants.WavesPerStage}";

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
