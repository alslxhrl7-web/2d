using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 적 이동 경로(웨이포인트) 데이터. 11웨이브부터 두 번째 진입로(WaypointsB)가 함께 사용된다.
    /// (기획서: "11~14 경로 분기, 2개 진입로를 번갈아 방어")
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
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
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
