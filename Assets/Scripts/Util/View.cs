using UnityEngine;

namespace Defense2D
{
    /// <summary>
    /// 2.5D 표현을 위한 공용 상수/헬퍼.
    ///
    /// [해설] ★ 이 게임은 원래 완전한 탑뷰(위에서 수직으로 내려다보는 2D)였다. 2.5D로 바꾸면서
    /// 택한 방식은 "바닥은 눕히고, 그 위의 물체는 세운다"이다:
    ///
    ///   1) <b>바닥을 눕힌다</b> — 비스듬히 내려다본 평면 위의 정사각형은 화면에서 세로가 눌린
    ///      직사각형으로 보인다. 그래서 길(PathLibrary의 사각형 루프)과 도로 타일의 y를
    ///      GroundSquash만큼 눌러 준다. 좌표 자체를 눌러 두기 때문에 이동·조준·배치 판정이
    ///      전부 눌린 좌표계 하나로 일관되게 돌아간다 — 로직과 그림이 어긋날 여지가 없다.
    ///
    ///   2) <b>물체는 세운다</b> — 타워와 적 스프라이트는 누르지 않고 원래 비율 그대로 두되,
    ///      중심이 아니라 <b>발밑</b>이 바닥 지점에 오도록 위로 띄운다. 눌린 바닥 위에 안 눌린
    ///      물체가 서 있으니 "서 있다"로 읽힌다.
    ///
    ///   3) <b>앞뒤를 가린다</b> — 화면 아래쪽(= y가 작은 쪽)이 카메라에 가까우므로, y가 작을수록
    ///      앞에 그려야 한다. 예전에는 모든 적이 sortingOrder 5로 같은 층이라 겹칠 때 순서가
    ///      뒤죽박죽이었다. 이제 y로 정렬 순서를 계산한다(Depth 참고).
    /// </summary>
    public static class View
    {
        /// <summary>바닥 평면을 세로로 누르는 비율. 1이면 완전 탑뷰, 작을수록 더 비스듬히 본 각도다.
        /// 0.62는 대략 위에서 약 52도 기울여 내려다보는 정도에 해당한다.</summary>
        public const float GroundSquash = 0.62f;

        /// <summary>탑뷰 기준 좌표(x, y)를 2.5D 바닥 좌표로 바꾼다. y만 눌린다.</summary>
        public static Vector3 Ground(float x, float y) => new Vector3(x, y * GroundSquash, 0f);

        public static Vector3 Ground(Vector3 p) => new Vector3(p.x, p.y * GroundSquash, p.z);

        // ---------- 정렬 층(밴드) ----------
        //
        // [해설] 각 밴드는 2000칸씩 떨어져 있고, 밴드 안에서 Depth(y)가 0~1999를 차지한다.
        // 그래서 "같은 밴드 안에서는 y로 앞뒤가 갈리고, 다른 밴드끼리는 절대 섞이지 않는다".
        // 타워와 적을 <b>같은 밴드(BandActor)</b>에 둔 것이 핵심이다 — 그래야 적이 타워 앞을
        // 지나갈 때 앞으로, 뒤로 지나갈 때 뒤로 가려진다.

        public const int BandBackground = -30000; // 배경 이미지(항상 맨 뒤)
        public const int BandRoad = -10000;       // 도로 타일
        public const int BandSpawn = -8000;       // 진입로 표시
        public const int BandGroundFx = -4000;    // 바닥에 깔리는 이펙트(서리 장판)
        public const int BandActor = 0;           // 타워 + 적 (서로 y로 정렬된다)
        public const int BandOverhead = 4000;     // 머리 위 체력바
        public const int BandProjectile = 8000;   // 날아가는 투사체
        public const int BandEffect = 10000;      // 폭발·번개 등 공중 이펙트
        public const int BandGhost = 14000;       // 배치 미리보기·사거리 링

        /// <summary>
        /// 월드 y로부터 밴드 안에서의 정렬 값(0~1999)을 만든다. y가 <b>작을수록 큰 값</b>이 나와서
        /// 화면 아래쪽 물체가 앞에 그려진다. 0.01유닛 단위까지 구분되며, 필드 밖 극단값이 들어와도
        /// 밴드를 넘지 않도록 잘라 낸다.
        /// </summary>
        public static int Depth(float worldY) => Mathf.Clamp(Mathf.RoundToInt((8f - worldY) * 100f), 0, 1999);

        /// <summary>밴드와 y를 합쳐 최종 sortingOrder를 만든다.</summary>
        public static int Order(int band, float worldY) => band + Depth(worldY);
    }
}
