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
