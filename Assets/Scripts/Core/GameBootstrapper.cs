using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Defense2D
{
    /// <summary>
    /// 씬을 수동으로 편집하지 않고도 재생(Play) 버튼만 누르면 게임 전체(카메라, 경로,
    /// 타워/웨이브/UI 매니저)가 코드로 조립되도록 하는 진입점.
    /// (플레이어 캐릭터는 제거되었고, 타워 배치만으로 방어한다. 거점(기지) 개념도 제거되어
    /// 길은 도착점 없이 영원히 도는 정사각형 루프다.)
    /// 어떤 씬을 열어도 동작한다 (RuntimeInitializeOnLoadMethod).
    /// [해설] 길(경로) 자체는 이제 고정이 아니라 PathLibrary에서 스테이지별로 다른 도안을 받아온다.
    /// WaveManager가 스테이지가 바뀔 때 RedrawPathVisuals()를 호출해서 여기서 그린 도로를 다시 그리므로,
    /// 이 클래스는 "최초 1회" 길을 그리는 역할과 "다시 그리는 방법"을 함께 들고 있다.
    /// </summary>
    public static class GameBootstrapper
    {
        // 웨이브가 바뀔 때마다 이전에 그려둔 도로/스폰 마커를 지우고 새로 그리기 위해,
        // 마지막으로 그린 길 표시용 루트 오브젝트를 기억해 둔다 (정적 클래스라 필드로 유지).
        private static GameObject _pathVisualRoot;

        // sceneLoaded 구독을 딱 한 번만 걸기 위한 표시 (아래 Bootstrap 설명 참고).
        private static bool _sceneHookInstalled;

        /// <summary>
        /// [해설] ★ "다시 시작"이 동작하려면 이 구조가 필요하다.
        /// RuntimeInitializeOnLoadMethod는 이름과 달리 <b>게임이 시작될 때 딱 한 번만</b> 불린다 —
        /// 씬을 다시 불러와도(SceneManager.LoadScene) 다시 불리지 않는다. 그래서 예전에는
        /// "다시 시작"을 누르면 씬만 새로 열리고 이 조립 코드가 돌지 않아, 카메라도 UI도 없는
        /// 빈 화면이 나왔다.
        /// 이를 고치려고 최초 1회 sceneLoaded 이벤트에 구독해 두고, 이후 씬이 다시 로드될 때마다
        /// BuildGame()이 다시 돌게 했다. 구독 자체는 static이라 씬을 넘나들어도 살아남으므로
        /// _sceneHookInstalled로 중복 구독을 막는다.
        /// </summary>
        /// <summary>
        /// [해설] ★ 에디터 "이중 조립" 버그 수정. 이 프로젝트는 Edit &gt; Project Settings &gt;
        /// Editor에서 <b>Reload Domain이 꺼져</b> 있다(EditorSettings.asset의
        /// EnterPlayModeOptions = 1). 그러면 Play를 멈춰도 C#의 static 값과 static 이벤트 구독이
        /// 그대로 살아남는다. 그 상태로 다시 Play를 누르면:
        ///   ① 씬이 새로 로드되면서 <b>지난 세션에 걸어둔</b> sceneLoaded 구독이 살아 있어 BuildGame()
        ///   ② Bootstrap()도 다시 불리는데 _sceneHookInstalled가 이미 true라 구독만 건너뛰고 BuildGame()
        /// 이렇게 게임이 두 벌 만들어졌다 — GameManager 둘(각자 골드 30), WaveManager 둘(각자 60마리
        /// 스폰), Canvas 둘, BuildManager 둘. 빌드에서는 실행할 때마다 도메인이 새로 뜨므로 멀쩡하고,
        /// <b>에디터에서만</b> 깨지는 종류라 더 찾기 어렵다.
        ///
        /// SubsystemRegistration은 Play가 시작될 때 AfterSceneLoad보다 <b>먼저</b>, 그리고 도메인
        /// 리로드 여부와 무관하게 매번 불린다. 여기서 구독을 확실히 떼어 두면, 아래 Bootstrap이
        /// 매 세션 정확히 한 번만 구독하게 되어 조립도 한 번만 일어난다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 구독 안 돼 있으면 아무 일도 안 일어난다
            _sceneHookInstalled = false;
            _pathVisualRoot = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!_sceneHookInstalled)
            {
                _sceneHookInstalled = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            BuildGame();
        }

        /// <summary>씬이 다시 로드될 때마다(=다시 시작) 게임을 처음부터 새로 조립한다.
        /// Additive 로드는 이 게임에서 쓰지 않으므로 Single일 때만 반응한다.</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) BuildGame();
        }

        /// <summary>
        /// [해설] 씬을 다시 불러와도 <b>static 값은 살아남는다</b>. 초기화하지 않으면 지난 판에서
        /// 모아둔 "타워 강화" 배율이 새 판 1웨이브부터 그대로 적용되는 등, 새 게임이 새 게임이
        /// 아니게 된다. 그래서 매번 조립 직전에 살아남는 값들을 공장 초기화한다.
        /// (Active 목록들은 이전 씬 오브젝트가 파괴되면서 OnDisable로 이미 비워지지만,
        ///  순서에 기대지 않도록 명시적으로 한 번 더 비운다.)
        /// </summary>
        private static void ResetStaticState()
        {
            // [해설] Time.timeScale은 씬을 다시 불러와도 되돌아오지 않는 전역 값이다. 일시정지
            // (timeScale=0) 상태에서 "다시 시작"을 누르면 새 게임이 얼어붙은 채로 시작되므로,
            // 조립할 때마다 반드시 정상 속도로 되돌린다.
            Time.timeScale = 1f;
            TowerBase.ResetDamageMultipliers(); // 타워 종류별 강화 배율을 전부 1로
            TowerBase.Active.Clear();
            EnemyController.Active.Clear();
            // [해설] 보통은 씬과 함께 이미 파괴됐지만, 도메인 리로드가 꺼진 에디터에서는 살아
            // 남을 수 있다. null로만 비우면 그 오브젝트가 고아가 되어 도로가 두 겹으로 남으므로,
            // 아직 살아 있으면 직접 파괴한 뒤에 참조를 비운다.
            if (_pathVisualRoot != null) Object.Destroy(_pathVisualRoot);
            _pathVisualRoot = null;
        }

        private static void BuildGame()
        {
            ResetStaticState();

            SetupCamera();

            // [해설] 스테이지 배경(숲 → 오염된 숲 → 보스전). 웨이브가 시작될 때마다
            // StageBackground가 로컬 웨이브 번호를 보고 배경을 교차 페이드로 바꾼다.
            // 배경 아트(Resources/Sprites/BG_*.png)가 없으면 스스로 비활성화되므로,
            // 아트가 아직 없는 상태에서도 안전하게 동작한다(카메라 단색 배경 유지).
            var backgroundGO = new GameObject("StageBackground");
            var background = backgroundGO.AddComponent<StageBackground>();

            // 1스테이지용 길 도안으로 시작한다. 스테이지가 바뀔 때마다 WaveManager.BeginNextWave()가
            // PathLibrary에서 그 스테이지의 고정 도안을 받아 이 PathData의 WaypointsA/B 필드를
            // 직접 갈아끼운다(스테이지 안에서는 길이 고정, PathLibrary.cs 참고).
            var initial = PathLibrary.GetForStage(0);
            var path = new PathData { WaypointsA = initial.WaypointsA, WaypointsB = initial.WaypointsB };
            DrawPathVisuals(path);

            var gameGO = new GameObject("GameManager");
            var game = gameGO.AddComponent<GameManager>();

            var uiGO = new GameObject("UIManager");
            var ui = uiGO.AddComponent<UIManager>();

            var buildGO = new GameObject("BuildManager");
            var build = buildGO.AddComponent<BuildManager>();

            var waveGO = new GameObject("WaveManager");
            var waves = waveGO.AddComponent<WaveManager>();

            build.Path = path;
            build.Game = game;
            build.UI = ui;
            build.Cam = Camera.main;

            waves.Path = path;
            waves.Game = game;
            waves.Build = build;

            game.UI = ui;
            game.Waves = waves;
            game.Build = build;

            ui.Game = game;
            ui.Waves = waves;
            ui.Build = build;

            waves.OnWaveStarted += ui.RefreshWave; // [해설] Action<int,int> (스테이지 번호, 스테이지 내 로컬 웨이브)
            waves.OnWaveStarted += background.OnWaveStarted; // 같은 신호로 배경도 함께 갈아끼운다
            waves.OnWaveCleared += game.OnWaveClearedHandler;
            waves.OnBossIncoming += ui.ShowBossBanner;

            ui.RefreshGold();
            ui.RefreshAliveCount(0);
            ui.RefreshWave(1, 0);
        }

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            // [해설] ★ 2.5D 프레이밍. 바닥을 세로로 누르면서(View.GroundSquash) 길이 차지하는
            // 세로 폭이 ±4.8에서 약 ±3.0으로 줄었다. 대신 타워·적이 이제 발밑 기준으로 서 있어서
            // 바닥 지점보다 위로 최대 2.7유닛(보스 몸 2.2 + 체력바)까지 삐져나온다.
            // 그래서 필요한 화면 범위가 아래로는 -3.3, 위로는 +5.9로 <b>위아래가 비대칭</b>이다.
            // ★ 길을 상하좌우 3칸씩 넓히면서(PathLibrary) 카메라도 같이 키웠다.
            // 확장 후 가장 넓은 3번 도안은 x -6.26~7.86, 눌린 y -4.44~4.32를 차지하고,
            // 그 위에 보스(몸 2.0 + 체력바)가 6.5까지 솟는다. 그래서 필요한 범위는
            // y -4.5~6.8로 여전히 위아래가 비대칭이다. 카메라를 1.15로 올리고 크기를 5.7로
            // 잡으면 보이는 범위가 y -4.55~6.85, x ±10.1(16:9)이 되어 전부 들어온다.
            // ★ 아래 두 값은 "웨이포인트 좌표"가 아니라 <b>실제로 그려지는 도로 타일의 바깥
            //   끝</b>까지 담아야 한다. 타일은 SpriteFactory.Square(64px @ PPU 32 = 2유닛)를
            //   (0.62, 0.62*0.62)로 줄인 것이라 1.24 x 0.77유닛이고, 중심에서 아래로 0.384,
            //   옆으로 0.62가 더 튀어나온다. 이걸 빼먹어서 3스테이지 아래쪽 도로가 0.27만큼
            //   잘려 있었다. 아래로 0.384를 더 확보하도록 크기를 키우고 중심을 내렸다.
            cam.orthographicSize = 6.0f;
            cam.transform.position = new Vector3(0, 1.05f, -10f);

            // ★ 가로는 화면비에 따라 달라진다. 3스테이지 도로 오른쪽 끝이 x = 8.48인데,
            //   16:9에서는 반폭이 10.7로 넉넉하지만 4:3(반폭 8.0)에서는 잘린다. 빌드 창은
            //   크기 조절이 가능하고 WebGL 임베드 비율도 제각각이므로, 화면이 좁으면
            //   크기를 키워서 가로를 확보한다(세로 여백이 늘어날 뿐 잘리지는 않는다).
            const float NeededHalfWidth = 8.6f;
            if (cam.orthographicSize * cam.aspect < NeededHalfWidth)
                cam.orthographicSize = NeededHalfWidth / Mathf.Max(0.5f, cam.aspect);
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.14f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        /// <summary>
        /// [해설] WaveManager가 웨이브(스테이지)마다 길을 바꿀 때 외부에서 호출하는 진입점.
        /// 이전에 그려둔 도로/스폰 마커(PathVisual)를 통째로 지우고, 넘겨받은 새 PathData 기준으로
        /// 다시 그린다. static 클래스라 인스턴스 없이 "GameBootstrapper.RedrawPathVisuals(...)"
        /// 형태로 어디서든 바로 호출할 수 있다.
        /// </summary>
        public static void RedrawPathVisuals(PathData path) => DrawPathVisuals(path);

        private static void DrawPathVisuals(PathData path)
        {
            // 이전 웨이브의 길 표시가 남아있다면 먼저 지운다 (안 지우면 옛 길과 새 길이 겹쳐 보인다).
            if (_pathVisualRoot != null) Object.Destroy(_pathVisualRoot);

            var root = new GameObject("PathVisual");
            _pathVisualRoot = root;
            // [해설] WaypointsA/B는 이제 서로 다른 사각형이 아니라 같은 사각형을 도는 두 출발점이므로
            // (PathLibrary 참고), 도로 타일은 한 번만 그린다. 대신 스폰 마커는 두 지점 모두 표시해서
            // 하나로 합쳐진 사각형의 양쪽에서 유닛이 나온다는 것을 눈으로 보여준다.
            DrawPolyline(path.WaypointsA, root.transform, new Color(0.28f, 0.34f, 0.46f));

            // [해설] 스폰 지점에 별도 도형(화살표/링)을 얹는 대신, 그 지점 근처 도로 자체를
            // 진입로 색으로 물들여서 표시한다 — "동그라미 대신 길에다가 색만 넣어서" 요청에 따른 것.
            SpawnMarker(path.WaypointsA[0], path.WaypointsA[1], root.transform, new Color(0.3f, 0.85f, 0.5f));
            SpawnMarker(path.WaypointsB[0], path.WaypointsB[1], root.transform, new Color(0.3f, 0.65f, 0.85f));
        }

        private static void DrawPolyline(List<Vector3> points, Transform parent, Color color)
        {
            var tileSprite = SpriteFactory.Square(color, color * 0.8f);
            int n = points.Count;
            // [해설] 거점이 없는 무한 루프 경로이므로, 마지막 점에서 다시 첫 점으로 돌아가는
            // 구간(i == n-1일 때 다음 점이 points[0])까지 그려서 사각형 모양을 완전히 닫는다.
            for (int i = 0; i < n; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % n];
                float dist = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.RoundToInt(dist / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                    var go = new GameObject("RoadTile");
                    go.transform.SetParent(parent, false);
                    go.transform.position = p;
                    // 바닥에 깔리는 타일이므로 세로를 같이 눌러야 평면이 누워 보인다.
                    go.transform.localScale = new Vector3(0.62f, 0.62f * View.GroundSquash, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = tileSprite;
                    sr.sortingOrder = View.Order(View.BandRoad, p.y);
                }
            }
        }

        /// <summary>
        /// [해설] 적이 나오는 진입로 표시. 예전에는 2폭 x 2깊이 = 4칸짜리 체크무늬 구간이었는데,
        /// "몹 나오는 길을 한 칸으로만" 요청에 따라 출발 꼭짓점 위의 도로 <b>한 칸</b>만 진입로
        /// 색으로 칠한다. 칸 크기를 도로 타일(DrawPolyline의 0.62)과 똑같이 맞췄기 때문에, 위에
        /// 새 도형을 얹은 것이 아니라 "길의 그 칸 하나가 물든" 것처럼 보인다.
        ///
        /// nextPos는 더 이상 위치 계산에 쓰이지 않지만, 두 점이 겹쳐서 진입 방향이 없는 잘못된
        /// 구간을 걸러내는 용도로 남겨 둔다 (호출부 시그니처도 그대로 유지된다).
        /// 도로 타일(-5) 바로 위(-4)에 얹어서 항상 길보다 앞에 보이게 한다.
        /// </summary>
        private static void SpawnMarker(Vector3 pos, Vector3 nextPos, Transform parent, Color color)
        {
            if ((nextPos - pos).sqrMagnitude < 0.0001f) return;

            const float tileScale = 0.62f; // 도로 타일과 동일 = 정확히 한 칸

            var go = new GameObject("SpawnRoadTint");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(tileScale, tileScale * View.GroundSquash, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square(color, color * 0.85f);
            sr.sortingOrder = View.Order(View.BandSpawn, pos.y);
        }
    }
}
