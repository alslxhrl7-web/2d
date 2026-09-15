using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 씬을 수동으로 편집하지 않고도 재생(Play) 버튼만 누르면 게임 전체(카메라, 경로,
    /// 거점, 플레이어, 타워/웨이브/UI 매니저)가 코드로 조립되도록 하는 진입점.
    /// 어떤 씬을 열어도 동작한다 (RuntimeInitializeOnLoadMethod).
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SetupCamera();

            var path = BuildPathData();
            DrawPathVisuals(path);

            var gameGO = new GameObject("GameManager");
            var game = gameGO.AddComponent<GameManager>();

            var uiGO = new GameObject("UIManager");
            var ui = uiGO.AddComponent<UIManager>();

            var playerGO = new GameObject("Player");
            var player = playerGO.AddComponent<PlayerController>();
            playerGO.transform.position = path.WaypointsA[0] + new Vector3(0.4f, 0.4f, 0f);

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
            game.Player = player;
            game.Build = build;

            ui.Game = game;
            ui.Waves = waves;
            ui.Build = build;
            ui.Player = player;

            waves.OnWaveStarted += ui.RefreshWave;
            waves.OnWaveCleared += game.OnWaveClearedHandler;
            waves.OnBossIncoming += ui.ShowBossBanner;
            player.OnActionPointsChanged += ui.RefreshActionPoints;

            ui.RefreshGold();
            ui.RefreshBaseHP();
            ui.RefreshActionPoints();
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

        private static PathData BuildPathData()
        {
            var path = new PathData
            {
                WaypointsA = new List<Vector3>
                {
                    new Vector3(-8.5f, -4.2f, 0),
                    new Vector3(-6.5f, -4.2f, 0),
                    new Vector3(-6.5f, -2.2f, 0),
                    new Vector3(-4.3f, -2.2f, 0),
                    new Vector3(-4.3f, -0.4f, 0),
                    new Vector3(-2.0f, -0.4f, 0),
                    new Vector3(-2.0f, 1.4f, 0),
                    new Vector3(0.6f, 1.4f, 0),
                    new Vector3(0.6f, 3.0f, 0),
                    new Vector3(3.2f, 3.0f, 0),
                    new Vector3(3.2f, 4.2f, 0),
                    new Vector3(7.8f, 4.2f, 0),
                },
                WaypointsB = new List<Vector3>
                {
                    new Vector3(8.5f, -4.2f, 0),
                    new Vector3(8.5f, -1.5f, 0),
                    new Vector3(5.0f, -1.5f, 0),
                    new Vector3(5.0f, 0.5f, 0),
                    new Vector3(2.0f, 0.5f, 0),
                    new Vector3(2.0f, 2.2f, 0),
                    new Vector3(7.8f, 2.2f, 0),
                    new Vector3(7.8f, 4.2f, 0),
                }
            };
            return path;
        }

        private static void DrawPathVisuals(PathData path)
        {
            var root = new GameObject("PathVisual");
            DrawPolyline(path.WaypointsA, root.transform, new Color(0.28f, 0.34f, 0.46f));
            DrawPolyline(path.WaypointsB, root.transform, new Color(0.24f, 0.3f, 0.4f));

            SpawnMarker(path.WaypointsA[0], root.transform, new Color(0.3f, 0.85f, 0.5f));
            SpawnMarker(path.WaypointsB[0], root.transform, new Color(0.3f, 0.65f, 0.85f));
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
