namespace Defense2D
{
    /// <summary>
    /// 기획서(25라운드 2D 디펜스) 기준 밸런스/규칙 상수.
    /// 아트 리소스는 사용하지 않고 시스템(로직) 구현에 집중한다.
    /// </summary>
    public static class GameConstants
    {
        public const int TotalWaves = 25;
        public const int BossWaveInterval = 5;

        public const float BaseMaxHP = 100f;
        public const int StartingGold = 50; // 시작 골드: 타워 2개(25*2) 정도만 세울 수 있게 축소
        public const int TowerCost = 25; // 화살탑/빙결탑 공통 비용
        public const int CannonTowerCost = 50; // [해설] 포격탑은 광역 폭발 데미지가 있어 다른 두 타워보다 비싸게 책정

        /// <summary>[해설] 타워 종류별 건설 비용을 한 곳에서 관리한다. BuildManager(구매 처리)와
        /// UIManager(건설 메뉴 버튼 문구)가 둘 다 이 메서드를 통해 비용을 물어보므로,
        /// 나중에 타워별 가격이 또 바뀌어도 여기 한 곳만 고치면 된다.</summary>
        public static int CostFor(TowerType type) => type == TowerType.Cannon ? CannonTowerCost : TowerCost;

        public const float PrepPhaseSeconds = 8f; // 준비 단계(건설/배치) 기본 시간

        // 플레이 필드 경계 (월드 유닛)
        public const float WorldHalfWidth = 9f;
        public const float WorldHalfHeight = 5.2f;

        // 경로에서 최소 이 거리 이상 떨어져야 타워 설치 가능
        public const float MinDistanceFromPath = 0.85f;
        public const float MinDistanceBetweenTowers = 0.9f;
    }
}
