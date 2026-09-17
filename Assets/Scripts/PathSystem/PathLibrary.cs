using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// [해설] 스테이지(웨이브)마다 적이 도는 길 모양을 바꾸기 위한 "길 도안" 모음집이다.
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
    /// 실제로 길이 바뀌는 방식:
    /// 1) WaveManager.BeginNextWave()가 새 웨이브를 시작할 때마다 GetForWave(웨이브번호)를 호출해서
    ///    "이번 웨이브에 쓸 길 도안"을 하나 받아온다.
    /// 2) 받아온 도안의 좌표 리스트를, 게임이 실제로 참조하는 PathData 객체의
    ///    WaypointsA / WaypointsB "필드 자체"에 통째로 새로 갈아끼운다 (리스트 안의 값만 고치는 게
    ///    아니라 리스트 자체를 새 걸로 교체한다는 뜻).
    /// 3) 이미 화면에 나와서 돌고 있던 적들은 스폰될 때 "그 순간의 리스트"를 직접 손에 쥐고 있으므로
    ///    (참조를 저장해둔 상태) 나중에 필드가 새 리스트로 바뀌어도 옛 리스트를 계속 들고 이동한다.
    ///    → 그래서 길이 바뀌는 순간 이미 돌고 있던 적이 갑자기 다른 방향으로 순간이동하듯 튀지 않는다.
    ///    새로 스폰되는 적부터만 새 길을 돈다.
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

        // 도안은 늘리기 쉽도록 배열로 관리한다. 웨이브 번호를 이 배열 길이로 나눈 나머지로
        // 순환시키므로(아래 GetForWave), 새 도안을 추가하면 그만큼 더 다양하게 순환된다.
        // [해설] 맵(GameConstants.WorldHalfWidth/Height)이 커진 만큼, 사각형들도 함께 키워서
        // 화면을 더 채우도록 했다 (기존 6.4/7.2/8.4 한 변 길이 → 7.6/8.2/9.0).
        private static readonly Template[] Templates =
        {
            // ---- 0번 도안: 화면 중앙의 정사각형 루프 (한 변 7.6) ----
            FromSquare(
                new Vector3(-3.8f, -3.8f, 0), new Vector3(3.8f, -3.8f, 0),
                new Vector3(3.8f, 3.8f, 0), new Vector3(-3.8f, 3.8f, 0)),

            // ---- 1번 도안: 좌측으로 치우친, 조금 더 세로가 긴 정사각형 루프 (한 변 8.2) ----
            FromSquare(
                new Vector3(-5.1f, -3.8f, 0), new Vector3(3.1f, -3.8f, 0),
                new Vector3(3.1f, 4.4f, 0), new Vector3(-5.1f, 4.4f, 0)),

            // ---- 2번 도안: 우측으로 치우친, 화면을 크게 쓰는 정사각형 루프 (한 변 9.0) ----
            FromSquare(
                new Vector3(-3.7f, -4.6f, 0), new Vector3(5.3f, -4.6f, 0),
                new Vector3(5.3f, 4.4f, 0), new Vector3(-3.7f, 4.4f, 0)),
        };

        /// <summary>
        /// 웨이브 번호(1부터 시작)에 맞는 길 도안을 "복제"해서 돌려준다.
        /// (원본 Templates 배열의 리스트를 그대로 넘기면 여러 웨이브가 같은 List 인스턴스를 공유하게 돼서,
        /// 나중에 실수로 하나를 고치면 다른 웨이브 도안까지 같이 바뀌는 사고가 날 수 있다.
        /// 그래서 항상 새 List로 복사한 뒤 돌려준다.)
        /// </summary>
        public static Template GetForWave(int wave)
        {
            int index = (wave - 1) % Templates.Length;
            if (index < 0) index += Templates.Length; // 방어적 처리: wave가 1 미만으로 들어와도 안전하게
            var src = Templates[index];
            return new Template
            {
                WaypointsA = new List<Vector3>(src.WaypointsA),
                WaypointsB = new List<Vector3>(src.WaypointsB),
            };
        }
    }
}
