using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Defense2D.EditorTools
{
    /// <summary>
    /// 유니티 에디터 없이도 게임을 돌릴 수 있도록, Windows 독립 실행 파일(.exe)을 한 번에
    /// 뽑아주는 에디터 전용 스크립트.
    ///
    /// [해설] 이 파일이 <b>Assets/Editor 폴더 안에</b> 있는 것이 중요하다. 유니티는 이 폴더의
    /// 스크립트를 에디터에서만 컴파일하고 실제 게임 빌드에는 넣지 않는다. UnityEditor를 참조하는
    /// 코드가 플레이어 빌드에 섞여 들어가면 컴파일 에러가 나기 때문에, 빌드 관련 코드는 반드시
    /// 여기에 둬야 한다.
    ///
    /// 쓰는 법
    ///  - 에디터 메뉴: 상단 메뉴의 <b>Defense2D > Windows 실행 파일 빌드</b>
    ///    → 프로젝트 폴더 아래 Build/Windows/ 에 exe가 만들어지고, 끝나면 탐색기가 열린다.
    ///  - 명령줄로 뽑고 싶을 때(선택):
    ///      "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -quit -batchmode
    ///        -projectPath "C:\Users\mbc\Documents\user\2d"
    ///        -executeMethod Defense2D.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        /// <summary>결과물이 만들어지는 폴더(프로젝트 루트 기준 상대 경로).</summary>
        private const string OutputDir = "Build/Windows";

        [MenuItem("Defense2D/Windows 실행 파일 빌드", false, 10)]
        public static void BuildWindows()
        {
            // Build Settings에 체크되어 있는 씬만 빌드에 포함된다. 하나도 없으면 실행 파일이
            // 열 씬이 없어서 검은 화면만 뜨므로, 아예 여기서 막고 안내한다.
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("Build Settings에 포함된 씬이 없습니다. File > Build Settings에서 씬을 추가하세요.");
                return;
            }

            // 활성 빌드 타겟이 Windows가 아니면 먼저 전환한다(전환에 시간이 걸릴 수 있다).
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.Log("[빌드] 활성 플랫폼을 Windows(x64)로 전환합니다. 잠시 걸릴 수 있습니다.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            ApplyStandaloneDisplaySettings();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, PlayerSettings.productName + ".exe");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[빌드 성공] {exePath}\n" +
                    $"  포함된 씬: {string.Join(", ", scenes)}\n" +
                    $"  크기: {summary.totalSize / (1024UL * 1024UL)} MB, " +
                    $"걸린 시간: {summary.totalTime.TotalSeconds:F1}초\n" +
                    $"  이 exe를 더블클릭하면 유니티 없이 바로 실행됩니다. " +
                    $"다른 PC로 옮길 때는 exe와 같은 폴더의 _Data 폴더까지 통째로 복사해야 합니다.");

                if (!Application.isBatchMode) EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Fail($"빌드 실패 ({summary.result}). Console 창 위쪽의 에러 로그에서 원인을 확인하세요.");
            }
        }

        /// <summary>
        /// [해설] 빌드 결과물이 "닫을 수 있는 창"으로 뜨도록 화면 설정을 맞춘다.
        /// 유니티 기본값은 네이티브 해상도 전체화면인데, 이 게임은 게임오버/승리 화면에 도달하기
        /// 전까지 종료 수단이 없어서 전체화면으로 뜨면 Alt+F4 말고는 빠져나올 방법이 없다.
        /// 창 모드로 두면 창의 X 버튼으로 언제든 닫을 수 있다.
        /// (여기서 건드리는 값들은 전부 Project Settings > Player에서 되돌릴 수 있다.)
        /// </summary>
        private static void ApplyStandaloneDisplaySettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            Debug.Log("[빌드] 창 모드 1600x900(크기 조절 가능)으로 설정했습니다. " +
                      "전체화면으로 바꾸려면 Project Settings > Player > Resolution and Presentation에서 조정하세요.");
        }

        [MenuItem("Defense2D/빌드 폴더 열기", false, 11)]
        public static void OpenBuildFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, OutputDir);
            if (!Directory.Exists(outDir))
            {
                Debug.LogWarning("[빌드] 아직 빌드 폴더가 없습니다. 먼저 'Defense2D > Windows 실행 파일 빌드'를 실행하세요.");
                return;
            }
            EditorUtility.RevealInFinder(outDir);
        }

        private static void Fail(string message)
        {
            Debug.LogError("[빌드] " + message);
            // 명령줄(-batchmode)로 돌렸을 때는 실패를 종료 코드로 알려줘야 CI 등에서 감지할 수 있다.
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
