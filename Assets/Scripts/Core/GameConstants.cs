namespace Defense2D
{
    /// <summary>
    /// 기획서(25라운드 2D 디펜스) 기준 밸런스/규칙 상수.
    /// 아트 리소스는 사용하지 않고 시스템(로직) 구현에 집중한다.
    /// </summary>
    public static class GameConstants
    {
        // [해설] "길은 고정하고 스테이지 형식으로" 요청에 따라 25웨이브 단일 진행에서
        // "25웨이브 = 1스테이지, 총 3스테이지"로 개편했다. 스테이지 안에서는 길이 고정되고
        // (스테이지가 바뀔 때만 새 길로 교체), 웨이브 번호도 스테이지마다 1로 리셋해서 표시한다.
        public const int WavesPerStage = 25;
        public const int TotalStages = 3;

        // 스테이지 중간 보스 주기(예: 10, 20웨이브). 스테이지의 마지막 웨이브(WavesPerStage,
        // 예: 25)는 이 주기와 별개로 항상 "피날레" 보스전이 된다(아래 StageFinaleBossTimeLimit 참고).
        public const int BossIntervalWaves = 10;

        /// <summary>[해설] 스테이지 마지막(피날레) 웨이브는 보스와 일반 유닛 무리가 함께 몰아친다.
        /// 이 시간(초) 안에 보스를 처치하지 못하면, 화면에 남은 적 수와 상관없이 즉시 게임오버
        /// 처리된다(WaveManager.Update 참고) — "25웨이브 보스 못 잡으면 게임오버" 요청에 따른 것.</summary>
        public const float StageFinaleBossTimeLimit = 60f;

        /// <summary>[해설] 건설 비용 변천: 25 → (절반으로) 12 → 15. 12는 너무 헐거워서 다시 올렸다.
        /// 15로 잡으면 시작 골드 30과 정확히 맞아떨어져서(15*2 = 30) "처음에 2개"가 딱 떨어진다.</summary>
        public const int TowerCost = 15; // 화살탑/빙결탑 공통 비용

        /// <summary>[해설] 포격탑은 광역 폭발 데미지(26)에 사거리도 가장 길어서 가장 비싸야 한다.
        /// 50 → 25로 내렸더니 값싼 타워 2개(30)보다 오히려 싸져서 "일단 포격탑부터"가 거의 항상
        /// 정답이 되어버렸다. 그래서 30으로 되돌렸다 — 이제 포격탑 1개와 값싼 타워 2개가 정확히
        /// 같은 값(30)이라, 시작 골드로 어느 쪽을 열든 잔돈 없이 딱 떨어지고 진짜 선택이 된다.
        /// (30보다 더 올리면 시작 골드 30으로 포격탑을 아예 못 세워서 그 선택지가 사라진다.)</summary>
        public const int CannonTowerCost = 30;

        /// <summary>[해설] 시작 골드는 "포격탑이면 1개, 화살탑/빙결탑이면 2개를 세울 수 있는 돈"으로
        /// 맞췄다. 값싼 타워는 15*2 = 30으로 정확히 2개, 포격탑은 25라서 1개를 세우고 5골드가 남는다.
        /// → 첫 수를 "단일 2개로 넓게 깔기"와 "광역 1개 + 잔돈 비축" 중 무엇으로 열지 고르게 하는 것이
        /// 이 값의 목적이다. 비용(TowerCost)을 바꿀 때는 이 값도 같이 봐야 규칙이 유지된다.</summary>
        public const int StartingGold = 30;

        /// <summary>[해설] 타워 종류별 건설 비용을 한 곳에서 관리한다. BuildManager(구매 처리)와
        /// UIManager(건설 메뉴 안내 문구)가 둘 다 이 메서드를 통해 비용을 물어보므로,
        /// 나중에 타워별 가격이 또 바뀌어도 여기 한 곳만 고치면 된다.</summary>
        public static int CostFor(TowerType type) => type == TowerType.Cannon ? CannonTowerCost : TowerCost;

        /// <summary>[해설] 타워를 철거할 때 건설 비용 중 돌려받는 비율(%). 전액을 돌려주면
        /// "아무 데나 짓고 마음에 안 들면 무료로 옮기기"가 최적 전략이 되어 배치 선택의 긴장감이
        /// 사라지므로, 절반만 돌려줘서 재배치에 약간의 대가가 따르게 했다.</summary>
        public const int TowerRefundPercent = 50;

        /// <summary>타워 철거 시 돌려받는 골드(정수 내림). 화살탑/빙결탑 25 → 12, 포격탑 50 → 25.</summary>
        public static int RefundFor(TowerType type) => CostFor(type) * TowerRefundPercent / 100;

        public const float PrepPhaseSeconds = 8f; // 준비 단계(건설/배치) 기본 시간

        /// <summary>[해설] 직전 웨이브를 "스킵"으로 끝냈을 때만 적용되는 짧은 준비 시간(초).
        /// 스킵은 곧 "빨리 넘어가고 싶다"는 의사표시인데, 그래놓고 다음 웨이브까지 기본 8초를
        /// 그대로 기다리게 하면 스킵한 보람이 없다. 그래서 이 경우에만 1초로 줄인다.
        /// (준비 단계 자체를 건너뛰지는 않는다 — 보상 선택 후 상황을 볼 최소한의 틈은 남겨둔다.)</summary>
        public const float PrepPhaseSecondsAfterSkip = 1f;

        // 플레이 필드 경계 (월드 유닛)
        // [해설] 맵이 화면 대비 작아 보인다는 요청에 따라 확장했다(9→10.5, 5.2→5.8).
        // 카메라 orthographicSize(6.2)는 그대로 두었으므로, 필드가 커진 만큼 화면을 더 채운다.
        public const float WorldHalfWidth = 10.5f;
        public const float WorldHalfHeight = 5.8f;

        // 경로에서 최소 이 거리 이상 떨어져야 타워 설치 가능
        public const float MinDistanceFromPath = 0.85f;
        public const float MinDistanceBetweenTowers = 0.9f;

        /// <summary>[해설] 거점(기지) 개념을 제거하면서, 유일한 패배 조건은 "동시 생존 적이
        /// 이 한도를 넘어서는 것"이 되었다. 웨이브 보상으로 이 한도를 늘릴 수 있다
        /// (Upgrades.cs의 AliveCapacity, WaveManager.IncreaseAliveCapacity 참고).</summary>
        public const int StartingMaxAliveEnemies = 50;
    }
}
