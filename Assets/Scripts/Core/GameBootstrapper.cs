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
            TowerBase.GlobalDamageMultiplier = 1f;
            TowerBase.Active.Clear();
            EnemyController.Active.Clear();
            _pathVisualRoot = null; // 이전 씬과 함께 이미 파괴된 참조
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
            cam.orthographicSize = 6.2f;
            cam.transform.position = new Vector3(0, 0, -10f);
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
                    go.transform.localScale = Vector3.one * 0.62f;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = tileSprite;
                    sr.sortingOrder = -5;
                }
            }
        }

        /// <summary>
        /// [해설] 이전에는 진입로 방향으로 길게 이어지는 단색 타일 여러 칸(무늬 없이 한 줄)을
        /// 깔았는데, "조금 더 복잡하게 만들고 표시되는 칸은 줄여달라"는 요청에 따라 다시 다듬었다.
        /// 칸 수는 4칸(2폭 x 2깊이)으로 줄이는 대신, 진입로 색과 그보다 밝은 색을 체크무늬로
        /// 교차시켜서 단순한 단색 구간보다 조금 더 정교한 "출입구" 패턴으로 보이게 했다.
        /// 여전히 별도 아이콘이 아니라 도로와 같은 타일 모양(SpriteFactory.Square)만 쓰고, 도로
        /// 타일(-5) 바로 위(-4)에 얹어서 "길 자체가 칠해진" 느낌을 유지한다.
        /// </summary>
        private static void SpawnMarker(Vector3 pos, Vector3 nextPos, Transform parent, Color color)
        {
            Vector3 dir = nextPos - pos;
            float segLen = dir.magnitude;
            if (segLen < 0.0001f) return;
            dir /= segLen;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f); // dir과 수직인 좌우 방향(폭)

            const float tileScale = 0.58f; // 기본 도로 타일(0.62)과 비슷한 크기
            float pitch = tileScale * 2f;  // Square 텍스처(64px, PPU 32) 기준 한 칸의 실제 폭 = scale*2, 겹치지 않게 딱 맞춘 간격

            var tileLight = SpriteFactory.Square(Color.Lerp(color, Color.white, 0.4f), color * 0.85f);
            var tileDark = SpriteFactory.Square(color, color * 0.85f);

            for (int row = 0; row < 2; row++)      // 진입 방향으로 2칸 깊이
            for (int col = 0; col < 2; col++)      // 진입로 폭으로 2칸
            {
                Vector3 p = pos + dir * (row * pitch) + perp * ((col - 0.5f) * pitch);
                var go = new GameObject("SpawnRoadTint");
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                go.transform.localScale = Vector3.one * tileScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ((row + col) % 2 == 0) ? tileDark : tileLight; // 체크무늬로 교차
                sr.sortingOrder = -4;
            }
        }
    }
}
