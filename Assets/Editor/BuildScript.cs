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
    ///  - <b>Defense2D > Windows 실행 파일 빌드</b>
    ///    → Build/Windows/ 에 exe가 만들어진다(유니티 없이 더블클릭 실행).
    ///  - <b>Defense2D > WebGL 빌드 (itch.io 업로드용)</b>
    ///    → Build/WebGL/ 과 업로드용 Build/WebGL-itch.zip이 만들어진다(브라우저에서 실행).
    ///  - 명령줄로 뽑고 싶을 때(선택):
    ///      "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -quit -batchmode
    ///        -projectPath "C:\Users\mbc\Documents\user\2d"
    ///        -executeMethod Defense2D.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        /// <summary>결과물이 만들어지는 폴더(프로젝트 루트 기준 상대 경로).</summary>
        private const string OutputDir = "Build/Windows";
        private const string WebGlOutputDir = "Build/WebGL";

        // itch.io 임베드 창 크기(업로드 설정에서 같은 값을 넣으면 스크롤 없이 딱 맞는다).
        private const int ItchViewportWidth = 1280;
        private const int ItchViewportHeight = 720;

        /// <summary>
        /// WebGL 빌드를 압축할지 여부. <b>여기 한 줄만 바꾸면 된다.</b>
        ///
        /// false(기본): 무압축. 어떤 서버 설정에서도 확실히 실행되지만 내려받는 용량이 크다.
        ///              (실측: .wasm 37MB + .data 29MB = 약 65MB를 브라우저가 받아야 함)
        /// true:        Brotli + Decompression Fallback. 받는 용량이 1/3~1/4로 줄어 첫 로딩이
        ///              훨씬 빠르다. Fallback을 함께 켜므로 서버가 Content-Encoding 헤더를
        ///              안 붙여줘도 브라우저가 스스로 풀 수 있어 itch.io에서도 대체로 잘 된다.
        ///              다만 환경에 따라 실패 사례가 보고되므로, 바꾼 뒤에는 반드시 itch.io에
        ///              올려서 실제로 뜨는지 확인할 것.
        /// </summary>
        private const bool UseWebGlCompression = false;

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

        /// <summary>
        /// itch.io에 "브라우저에서 바로 플레이"로 올리기 위한 WebGL 빌드.
        /// 끝나면 업로드용 zip까지 만들어 준다 — itch.io는 <b>index.html이 zip 최상단</b>에 있어야
        /// 인식하는데, 폴더째로 압축해서 실패하는 경우가 흔해서 여기서 아예 맞춰서 만든다.
        ///
        /// 업로드 방법: itch.io 프로젝트에서 Kind of project를 <b>HTML</b>로 두고, 만들어진
        /// webgl-itch.zip을 올린 뒤 "This file will be played in the browser"에 체크하면 된다.
        /// </summary>
        [MenuItem("Defense2D/WebGL 빌드 (itch.io 업로드용)", false, 11)]
        public static void BuildWebGL()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("Build Settings에 포함된 씬이 없습니다. File > Build Settings에서 씬을 추가하세요.");
                return;
            }

            // WebGL 모듈이 Unity Hub에서 설치돼 있지 않으면 빌드가 불가능하다. 알아보기 힘든
            // 내부 에러 대신 먼저 안내한다.
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Fail("이 유니티에 WebGL 모듈이 설치돼 있지 않습니다. " +
                     "Unity Hub > 설치 > 해당 에디터의 톱니바퀴 > 모듈 추가에서 'WebGL Build Support'를 설치하세요.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[빌드] 활성 플랫폼을 WebGL로 전환합니다. 에셋을 다시 임포트하느라 몇 분 걸릴 수 있습니다.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            ApplyWebGlSettings();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, WebGlOutputDir);
            // WebGL은 폴더 통째로 결과물이므로, 옛 결과가 섞이지 않도록 비우고 시작한다.
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"WebGL 빌드 실패 ({summary.result}). Console 창의 에러 로그를 확인하세요.");
                return;
            }

            string zipPath = Path.Combine(projectRoot, WebGlOutputDir + "-itch.zip");
            string zipNote;
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                // CreateFromDirectory는 "폴더 안의 내용물"을 zip 최상단에 넣는다 = itch.io가 원하는 모양.
                System.IO.Compression.ZipFile.CreateFromDirectory(outDir, zipPath);
                zipNote = $"  업로드용 zip: {zipPath}";
            }
            catch (System.Exception e)
            {
                zipNote = "  (zip 자동 생성 실패: " + e.Message + ")\n" +
                          $"  직접 압축하세요 — {outDir} 폴더를 여는 게 아니라 '폴더 안의 내용물'(index.html 포함)을 선택해 압축해야 합니다.";
            }

            Debug.Log(
                $"[WebGL 빌드 성공] {outDir}\n{zipNote}\n" +
                $"  크기: {summary.totalSize / (1024UL * 1024UL)} MB, 걸린 시간: {summary.totalTime.TotalSeconds:F1}초\n" +
                $"  itch.io 업로드: 프로젝트의 Kind of project를 'HTML'로 하고 위 zip을 올린 뒤, " +
                $"그 파일에 'This file will be played in the browser' 체크. " +
                $"Embed 크기는 {ItchViewportWidth}x{ItchViewportHeight}를 권장합니다.");

            if (!Application.isBatchMode) EditorUtility.RevealInFinder(outDir);
        }

        /// <summary>
        /// [해설] itch.io에서 Unity WebGL이 안 뜨는 가장 흔한 원인이 <b>압축 형식</b>이라, 기본값은
        /// "확실히 뜨는" 무압축이다. 다만 무압축은 브라우저가 받아야 할 용량이 그대로 나가므로
        /// (실측 약 65MB) 첫 로딩이 느리다. 용량을 줄이고 싶으면 위의 UseWebGlCompression을
        /// true로 바꾸면 Brotli + Decompression Fallback 조합으로 빌드된다.
        /// </summary>
        private static void ApplyWebGlSettings()
        {
            if (UseWebGlCompression)
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                // 서버가 Content-Encoding 헤더를 안 붙여줘도 브라우저가 직접 풀 수 있게 한다.
                // 이게 꺼져 있으면 itch.io에서 "decompression fallback" 에러로 아예 안 뜬다.
                PlayerSettings.WebGL.decompressionFallback = true;
            }
            else
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
            }

            PlayerSettings.WebGL.dataCaching = true;   // 재방문 시 다시 받지 않도록
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = ItchViewportWidth;
            PlayerSettings.defaultWebScreenHeight = ItchViewportHeight;

            Debug.Log($"[빌드] WebGL 설정: 압축 {(UseWebGlCompression ? "Brotli + Fallback" : "끔")}, " +
                      $"캔버스 {ItchViewportWidth}x{ItchViewportHeight}. " +
                      "(압축 전환은 BuildScript.cs의 UseWebGlCompression 상수 한 줄)");
        }

        [MenuItem("Defense2D/빌드 폴더 열기", false, 20)]
        public static void OpenBuildFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildRoot = Path.Combine(projectRoot, "Build");
            if (!Directory.Exists(buildRoot))
            {
                Debug.LogWarning("[빌드] 아직 빌드 폴더가 없습니다. 먼저 'Defense2D' 메뉴의 빌드를 실행하세요.");
                return;
            }
            EditorUtility.RevealInFinder(buildRoot);
        }

        private static void Fail(string message)
        {
            Debug.LogError("[빌드] " + message);
            // 명령줄(-batchmode)로 돌렸을 때는 실패를 종료 코드로 알려줘야 CI 등에서 감지할 수 있다.
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
