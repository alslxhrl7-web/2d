using System.Collections.Generic;
using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// [해설] 스테이지(웨이브)마다 적이 걸어오는 길 모양을 바꾸기 위한 "길 도안" 모음집이다.
    /// 여기 담긴 각 Template 하나가 길 하나를 통째로 정의한다 (진입로 A/B의 꺾이는 지점들).
    ///
    /// 실제로 길이 바뀌는 방식:
    /// 1) WaveManager.BeginNextWave()가 새 웨이브를 시작할 때마다 GetForWave(웨이브번호)를 호출해서
    ///    "이번 웨이브에 쓸 길 도안"을 하나 받아온다.
    /// 2) 받아온 도안의 좌표 리스트를, 게임이 실제로 참조하는 PathData 객체의
    ///    WaypointsA / WaypointsB "필드 자체"에 통째로 새로 갈아끼운다 (리스트 안의 값만 고치는 게
    ///    아니라 리스트 자체를 새 걸로 교체한다는 뜻).
    /// 3) 이미 화면에 나와서 걷고 있던 적들은 스폰될 때 "그 순간의 리스트"를 직접 손에 쥐고 있으므로
    ///    (참조를 저장해둔 상태) 나중에 필드가 새 리스트로 바뀌어도 옛 리스트를 계속 들고 이동한다.
    ///    → 그래서 길이 바뀌는 순간 이미 걷고 있던 적이 갑자기 다른 방향으로 순간이동하듯 튀지 않는다.
    ///    새로 스폰되는 적부터만 새 길을 걷게 된다.
    /// 4) 화면에 그려지는 길(도로 타일/스폰 마커)도 GameBootstrapper.RedrawPathVisuals()로 다시 그려서
    ///    눈으로 보이는 길도 실제 이동 경로와 항상 일치하게 맞춘다.
    ///
    /// 모든 도안은 공통적으로 (7.8, 4.2) 지점에서 끝나도록 만들었다. 거점(기지) 시각 마커가
    /// 항상 그 자리에 있으므로, 길이 스테이지마다 바뀌어도 "거점 위치"만큼은 흔들리지 않게 하기 위함이다.
    /// </summary>
    public static class PathLibrary
    {
        /// <summary>길 도안 하나. WaypointsA(1번 진입로)는 항상 존재하고,
        /// WaypointsB(2번 진입로, 11웨이브부터 사용)는 도안마다 다른 모양으로 준비돼 있다.</summary>
        public struct Template
        {
            public List<Vector3> WaypointsA;
            public List<Vector3> WaypointsB;
        }

        // 도안은 늘리기 쉽도록 배열로 관리한다. 웨이브 번호를 이 배열 길이로 나눈 나머지로
        // 순환시키므로(아래 GetForWave), 새 도안을 추가하면 그만큼 더 다양하게 순환된다.
        private static readonly Template[] Templates =
        {
            // ---- 0번 도안: 기본 계단식 길 (원래 사용하던 경로) ----
            // 좌하단 / 우하단에서 각각 출발해 계단처럼 꺾이며 우상단 거점으로 모인다.
            new Template
            {
                WaypointsA = new List<Vector3>
                {
                    new Vector3(-8.5f, -4.2f, 0), new Vector3(-6.5f, -4.2f, 0), new Vector3(-6.5f, -2.2f, 0),
                    new Vector3(-4.3f, -2.2f, 0), new Vector3(-4.3f, -0.4f, 0), new Vector3(-2.0f, -0.4f, 0),
                    new Vector3(-2.0f, 1.4f, 0), new Vector3(0.6f, 1.4f, 0), new Vector3(0.6f, 3.0f, 0),
                    new Vector3(3.2f, 3.0f, 0), new Vector3(3.2f, 4.2f, 0), new Vector3(7.8f, 4.2f, 0),
                },
                WaypointsB = new List<Vector3>
                {
                    new Vector3(8.5f, -4.2f, 0), new Vector3(8.5f, -1.5f, 0), new Vector3(5.0f, -1.5f, 0),
                    new Vector3(5.0f, 0.5f, 0), new Vector3(2.0f, 0.5f, 0), new Vector3(2.0f, 2.2f, 0),
                    new Vector3(7.8f, 2.2f, 0), new Vector3(7.8f, 4.2f, 0),
                },
            },

            // ---- 1번 도안: 화면을 크게 가로지르는 길 ----
            // 좌상단 / 우하단에서 출발해 중앙을 크게 지그재그로 가로지른다. 0번과는 꺾이는 위치가
            // 전혀 달라서, 같은 자리에 지어둔 타워의 사거리가 이번엔 안 닿을 수도 있다(전략 변화 유도).
            new Template
            {
                WaypointsA = new List<Vector3>
                {
                    new Vector3(-8.5f, 4.5f, 0), new Vector3(-5.0f, 4.5f, 0), new Vector3(-5.0f, 1.0f, 0),
                    new Vector3(-1.5f, 1.0f, 0), new Vector3(-1.5f, -2.5f, 0), new Vector3(2.5f, -2.5f, 0),
                    new Vector3(2.5f, 0.5f, 0), new Vector3(7.8f, 0.5f, 0), new Vector3(7.8f, 4.2f, 0),
                },
                WaypointsB = new List<Vector3>
                {
                    new Vector3(8.5f, -4.5f, 0), new Vector3(5.5f, -4.5f, 0), new Vector3(5.5f, -1.0f, 0),
                    new Vector3(2.0f, -1.0f, 0), new Vector3(2.0f, 2.5f, 0), new Vector3(7.8f, 2.5f, 0),
                    new Vector3(7.8f, 4.2f, 0),
                },
            },

            // ---- 2번 도안: 좌측을 크게 휘도는 길 ----
            // 왼쪽에서 아래위로 크게 휘돌아 들어오는 A와, 오른쪽에서 짧게 우회하는 B로 구성해
            // 앞의 두 도안과는 또 다른 방어 동선을 만들어야 한다.
            new Template
            {
                WaypointsA = new List<Vector3>
                {
                    new Vector3(-8.5f, 3.5f, 0), new Vector3(-4.5f, 3.5f, 0), new Vector3(-4.5f, -3.0f, 0),
                    new Vector3(-1.0f, -3.0f, 0), new Vector3(-1.0f, 0.5f, 0), new Vector3(3.0f, 0.5f, 0),
                    new Vector3(3.0f, -2.5f, 0), new Vector3(7.8f, -2.5f, 0), new Vector3(7.8f, 4.2f, 0),
                },
                WaypointsB = new List<Vector3>
                {
                    new Vector3(8.5f, -1.5f, 0), new Vector3(5.5f, -1.5f, 0), new Vector3(5.5f, 2.0f, 0),
                    new Vector3(2.0f, 2.0f, 0), new Vector3(2.0f, -1.0f, 0), new Vector3(7.8f, -1.0f, 0),
                    new Vector3(7.8f, 4.2f, 0),
                },
            },
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
