using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 적 이동 경로(웨이포인트) 데이터. WaypointsA/B는 서로 다른 길이 아니라, 같은 정사각형
    /// 루프를 서로 다른 꼭짓점(절반 바퀴 차이)에서 도는 두 진입로다. 1웨이브부터 두 진입로가
    /// 항상 함께 사용되어, 하나로 합쳐진 사각형의 양쪽에서 유닛이 나온다.
    /// [System.Serializable]: WaveManager/BuildManager의 public PathData Path 필드가
    /// 유니티 직렬화 분석기(UAC1001)에서 "직렬화 불가" 경고를 내지 않도록 표시.
    /// (실제 값은 항상 코드로 런타임에 채워지므로 인스펙터 직렬화 자체는 필요 없다.)
    /// </summary>
    [System.Serializable]
    public class PathData
    {
        public List<Vector3> WaypointsA;
        public List<Vector3> WaypointsB;

        public List<Vector3> GetPath(int index) => (index == 1 && WaypointsB != null) ? WaypointsB : WaypointsA;

        /// <summary>
        /// 점이 길(사각형 폐곡선) <b>안쪽</b>에 있는지 판정한다.
        ///
        /// [해설] 타워를 길 안쪽에만 세울 수 있게 하려고 추가했다. 예전에는 "길에서 일정 거리
        /// 떨어졌는가"만 봤기 때문에 사각형 바깥의 빈 땅 어디에나 세울 수 있었고, 실제로 화면
        /// 구석구석에 타워가 흩어져 배치되는 그림이 나왔다.
        ///
        /// 판정은 표준적인 레이 캐스팅(홀짝 규칙)이다. 점에서 오른쪽으로 무한히 반직선을 쏴서
        /// 다각형의 변과 몇 번 교차하는지 세고, 홀수면 안쪽·짝수면 바깥이다. 경로가 닫힌 루프라
        /// 마지막 꼭짓점에서 첫 꼭짓점으로 돌아가는 변까지 포함해야 하므로 (i + 1) % n을 쓴다
        /// (DistanceToPolyline, GameBootstrapper.DrawPolyline과 같은 규칙).
        ///
        /// WaypointsA만 본다 — WaypointsB는 같은 사각형을 다른 꼭짓점에서 도는 순서만 다른
        /// 목록이라(PathLibrary 참고), 두 목록이 그리는 도형은 완전히 동일하기 때문이다.
        /// </summary>
        public bool ContainsPoint(Vector2 point)
        {
            var pts = WaypointsA;
            if (pts == null || pts.Count < 3) return true; // 길이 아직 없으면 제약하지 않는다

            bool inside = false;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % n];

                // 변이 점의 y높이를 가로지르는가? (한쪽 끝만 위에 있어야 한 번 교차한다)
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (!crosses) continue;

                // 그 높이에서 변의 x좌표를 구해, 점보다 오른쪽이면 교차 1회로 센다.
                float xAtY = a.x + (point.y - a.y) / (b.y - a.y) * (b.x - a.x);
                if (xAtY > point.x) inside = !inside;
            }
            return inside;
        }

        public float DistanceToNearestPath(Vector2 point)
        {
            float best = DistanceToPolyline(point, WaypointsA);
            if (WaypointsB != null) best = Mathf.Min(best, DistanceToPolyline(point, WaypointsB));
            return best;
        }

        private float DistanceToPolyline(Vector2 p, List<Vector3> pts)
        {
            float best = float.MaxValue;
            int n = pts.Count;
            // [해설] 경로가 도착점 없이 닫힌 사각형 루프이므로, 마지막 꼭짓점에서 첫 꼭짓점으로
            // 돌아가는 변(i == n-1일 때)까지 포함해서 검사해야 사각형 네 변 전체에서 타워 설치
            // 최소 거리가 올바르게 적용된다 (그리기 쪽 GameBootstrapper.DrawPolyline과 동일한 규칙).
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % n];
                Vector2 ab = b - a;
                float t = Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f);
                t = Mathf.Clamp01(t);
                Vector2 closest = a + ab * t;
                best = Mathf.Min(best, Vector2.Distance(p, closest));
            }
            return best;
        }
    }
}
