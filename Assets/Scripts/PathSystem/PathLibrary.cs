using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// [해설] 스테이지마다 적이 도는 길 모양을 바꾸기 위한 "길 도안" 모음집이다.
    /// 여기 담긴 각 Template은 정사각형 폐곡선(루프) "하나"를 정의한다. 진입로 A/B는 서로 다른
    /// 사각형이 아니라, 같은 사각형 둘레를 서로 다른 꼭짓점(정확히 절반 바퀴 차이)에서 출발해
    /// 도는 두 지점일 뿐이다 — 그래서 화면에는 사각형이 하나만 보이지만, 유닛은 항상 두 지점
    /// (마주보는 꼭짓점) 모두에서 나와 같은 루프를 함께 돈다.
    ///
    /// 마지막 꼭짓점에서 다시 첫 꼭짓점으로 돌아가는 구간까지 자동으로 이어진다 (그리기:
    /// GameBootstrapper.DrawPolyline, 이동: EnemyController.Update()가 각각 인덱스를
    /// (인덱스+1) % Count로 계산해 루프를 닫는다). 적은 이 사각형을 영원히 맴돌며, 오직
    /// 타워에게 처치될 때만 사라진다.
    ///
    /// [해설] "길은 고정하고 스테이지 형식으로" 요청에 따라, 길은 더 이상 웨이브마다 바뀌지
    /// 않는다. 대신 스테이지(0,1,2 — 총 3개, 위 Templates 배열과 1:1 대응)마다 도안 하나를
    /// 고정으로 배정하고, 그 스테이지의 25웨이브 내내 동일한 길을 유지한다. 실제로 길이
    /// 적용되는 방식:
    /// 1) WaveManager.BeginNextWave()가 스테이지의 첫 웨이브(로컬 웨이브 1)를 시작할 때만
    ///    GetForStage(스테이지번호)를 호출해서 "이 스테이지에 쓸 길 도안"을 받아온다.
    /// 2) 받아온 도안의 좌표 리스트를, 게임이 실제로 참조하는 PathData 객체의
    ///    WaypointsA / WaypointsB "필드 자체"에 통째로 새로 갈아끼운다 (리스트 안의 값만 고치는 게
    ///    아니라 리스트 자체를 새 걸로 교체한다는 뜻).
    /// 3) 이미 화면에 나와서 돌고 있던 적들은 스폰될 때 "그 순간의 리스트"를 직접 손에 쥐고 있으므로
    ///    (참조를 저장해둔 상태) 나중에 필드가 새 리스트로 바뀌어도 옛 리스트를 계속 들고 이동한다.
    ///    → 그래서 스테이지가 바뀌는 순간 이미 돌고 있던 적이 갑자기 다른 방향으로 순간이동하듯
    ///    튀지 않는다. 새 스테이지에서 새로 스폰되는 적부터만 새 길을 돈다.
    /// 4) 화면에 그려지는 길(도로 타일/스폰 마커)도 GameBootstrapper.RedrawPathVisuals()로 다시 그려서
    ///    눈으로 보이는 길도 실제 이동 경로와 항상 일치하게 맞춘다.
    /// </summary>
    public static class PathLibrary
    {
        /// <summary>길 도안 하나. WaypointsA/B는 항상 같은 정사각형의 꼭짓점 목록이며,
        /// B는 A보다 정확히 절반 바퀴(꼭짓점 2개) 앞서 출발하도록 순서만 돌려놓은 것이다.
        /// 그래서 두 진입로는 "서로 다른 사각형"이 아니라 "하나로 합쳐진 사각형을 도는 두 출발점"이다.</summary>
        public struct Template
        {
            public List<Vector3> WaypointsA;
            public List<Vector3> WaypointsB;
        }

        /// <summary>사각형 네 꼭짓점(순서대로 이어 돌면 폐곡선이 되는 순환 순서)으로부터
        /// A(0번 꼭짓점 출발)와 B(2번 꼭짓점, 즉 절반 바퀴 앞서 출발) 도안을 함께 만든다.</summary>
        private static Template FromSquare(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            return new Template
            {
                WaypointsA = new List<Vector3> { c0, c1, c2, c3 },
                WaypointsB = new List<Vector3> { c2, c3, c0, c1 },
            };
        }

        // 도안은 늘리기 쉽도록 배열로 관리한다. 스테이지 번호를 이 배열 길이로 나눈 나머지로
        // 골라 쓰므로(아래 GetForStage), 3개 스테이지에 정확히 하나씩 고정 배정된다.
        // [해설] "정사각형은 유지하되 사이즈를 키워달라"는 요청에 따라 한 번 더 확대했다
        // (7.6/8.2/9.0 → 9.6/9.6/10.4). 세로(WorldHalfHeight=5.8, 즉 y는 ±5.8) 여유가
        // 가로보다 훨씬 좁으므로, 각 정사각형이 세로 방향으로 world 경계(타워 배치 한계인
        // WorldHalfHeight-0.3=5.5)를 넘지 않는 선에서 최대한 키웠다.
        private static readonly Template[] Templates =
        {
            // ---- 0번 도안: 화면 중앙의 정사각형 루프 (한 변 9.6) ----
            FromSquare(
                new Vector3(-4.8f, -4.8f, 0), new Vector3(4.8f, -4.8f, 0),
                new Vector3(4.8f, 4.8f, 0), new Vector3(-4.8f, 4.8f, 0)),

            // ---- 1번 도안: 좌측으로 치우친, 조금 더 세로가 긴 정사각형 루프 (한 변 9.6) ----
            FromSquare(
                new Vector3(-5.8f, -4.5f, 0), new Vector3(3.8f, -4.5f, 0),
                new Vector3(3.8f, 5.1f, 0), new Vector3(-5.8f, 5.1f, 0)),

            // ---- 2번 도안: 우측으로 치우친, 화면을 가장 크게 쓰는 정사각형 루프 (한 변 10.4) ----
            FromSquare(
                new Vector3(-4.4f, -5.3f, 0), new Vector3(6.0f, -5.3f, 0),
                new Vector3(6.0f, 5.1f, 0), new Vector3(-4.4f, 5.1f, 0)),
        };

        /// <summary>
        /// 스테이지 번호(0부터 시작)에 맞는 길 도안을 "복제"해서 돌려준다. 이 도안은 그 스테이지의
        /// 25웨이브 내내 고정으로 쓰인다(WaveManager가 스테이지 첫 웨이브에서만 이 메서드를 호출).
        /// (원본 Templates 배열의 리스트를 그대로 넘기면 여러 스테이지가 같은 List 인스턴스를
        /// 공유하게 돼서, 나중에 실수로 하나를 고치면 다른 스테이지 도안까지 같이 바뀌는 사고가
        /// 날 수 있다. 그래서 항상 새 List로 복사한 뒤 돌려준다.)
        /// </summary>
        public static Template GetForStage(int stageIndex)
        {
            int index = stageIndex % Templates.Length;
            if (index < 0) index += Templates.Length; // 방어적 처리: 음수가 들어와도 안전하게
            var src = Templates[index];
            return new Template
            {
                WaypointsA = new List<Vector3>(src.WaypointsA),
                WaypointsB = new List<Vector3>(src.WaypointsB),
            };
        }
    }
}
