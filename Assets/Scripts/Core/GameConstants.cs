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
        public const int StartingGold = 120;
        public const int TowerCost = 25; // 화살탑/빙결탑/포격탑 공통 비용 25 (기획서 기준)

        public const int MaxActionPoints = 5; // 행동력 5칸
        public const float ActionPointRegenSeconds = 14f;

        public const float PrepPhaseSeconds = 8f; // 준비 단계(건설/배치) 기본 시간

        // 플레이 필드 경계 (월드 유닛)
        public const float WorldHalfWidth = 9f;
        public const float WorldHalfHeight = 5.2f;

        // 경로에서 최소 이 거리 이상 떨어져야 타워 설치 가능
        public const float MinDistanceFromPath = 0.85f;
        public const float MinDistanceBetweenTowers = 0.9f;
    }
}
