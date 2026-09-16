using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 씬을 수동으로 편집하지 않고도 재생(Play) 버튼만 누르면 게임 전체(카메라, 경로,
    /// 거점, 타워/웨이브/UI 매니저)가 코드로 조립되도록 하는 진입점.
    /// (플레이어 캐릭터는 제거되었고, 타워 배치만으로 방어한다.)
    /// 어떤 씬을 열어도 동작한다 (RuntimeInitializeOnLoadMethod).
    /// [해설] 길(경로) 자체는 이제 고정이 아니라 PathLibrary에서 웨이브별로 다른 도안을 받아온다.
    /// WaveManager가 매 웨이브 시작 시 RedrawPathVisuals()를 호출해서 여기서 그린 도로를 다시 그리므로,
    /// 이 클래스는 "최초 1회" 길을 그리는 역할과 "다시 그리는 방법"을 함께 들고 있다.
    /// </summary>
    public static class GameBootstrapper
    {
        // 웨이브가 바뀔 때마다 이전에 그려둔 도로/스폰 마커를 지우고 새로 그리기 위해,
        // 마지막으로 그린 길 표시용 루트 오브젝트를 기억해 둔다 (정적 클래스라 필드로 유지).
        private static GameObject _pathVisualRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SetupCamera();

            // 1웨이브용 길 도안으로 시작한다. 이후 웨이브부터는 WaveManager.BeginNextWave()가
            // PathLibrary에서 새 도안을 받아 이 PathData의 WaypointsA/B 필드를 직접 갈아끼운다.
            var initial = PathLibrary.GetForWave(1);
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

            waves.OnWaveStarted += ui.RefreshWave;
            waves.OnWaveCleared += game.OnWaveClearedHandler;
            waves.OnBossIncoming += ui.ShowBossBanner;

            ui.RefreshGold();
            ui.RefreshBaseHP();
            ui.RefreshWave(0);
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
            DrawPolyline(path.WaypointsA, root.transform, new Color(0.28f, 0.34f, 0.46f));
            DrawPolyline(path.WaypointsB, root.transform, new Color(0.24f, 0.3f, 0.4f));

            SpawnMarker(path.WaypointsA[0], root.transform, new Color(0.3f, 0.85f, 0.5f));
            SpawnMarker(path.WaypointsB[0], root.transform, new Color(0.3f, 0.65f, 0.85f));
            // 모든 길 도안이 같은 지점에서 끝나도록 만들어 뒀으므로, 길이 바뀌어도 거점 마커는
            // 항상 같은 자리에 그려진다 (PathLibrary 클래스 주석 참고).
            BaseMarker(path.WaypointsA[path.WaypointsA.Count - 1], root.transform);
        }

        private static void DrawPolyline(List<Vector3> points, Transform parent, Color color)
        {
            var tileSprite = SpriteFactory.Square(color, color * 0.8f);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
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

        private static void SpawnMarker(Vector3 pos, Transform parent, Color color)
        {
            var go = new GameObject("SpawnMarker");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Ring(color, 96, 5f);
            sr.sortingOrder = -3;
            go.transform.localScale = Vector3.one * 0.9f;
        }

        private static void BaseMarker(Vector3 pos, Transform parent)
        {
            var go = new GameObject("BaseMarker");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(new Color(0.3f, 0.8f, 0.9f), Color.white);
            sr.sortingOrder = -2;
            go.transform.localScale = Vector3.one * 1.3f;
        }
    }
}
