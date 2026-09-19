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
        /// 맞췄다. 값싼 타워는 15*2 = 30으로 정확히 2개, 포격탑도 30이라 정확히 1개 — 어느 쪽을
        /// 골라도 잔돈이 남지 않는다. (번개탑 25를 고르면 5골드가 남는다.)
        /// → 첫 수를 "단일 2개로 넓게 깔기"와 "광역 1개 + 잔돈 비축" 중 무엇으로 열지 고르게 하는 것이
        /// 이 값의 목적이다. 비용(TowerCost)을 바꿀 때는 이 값도 같이 봐야 규칙이 유지된다.</summary>
        public const int StartingGold = 30;

        /// <summary>[해설] 번개탑은 맞은 적에서 주변 적으로 연쇄하는 광역 타워다. 한 방 피해는
        /// 포격탑보다 낮지만 길을 따라 늘어선 적을 줄줄이 훑기 때문에, 값싼 타워(15)와
        /// 포격탑(30) 사이인 25로 잡았다.</summary>
        public const int LightningTowerCost = 25;

        /// <summary>[해설] 타워 종류별 건설 비용을 한 곳에서 관리한다. BuildManager(구매 처리)와
        /// UIManager(건설 메뉴 안내 문구)가 둘 다 이 메서드를 통해 비용을 물어보므로,
        /// 나중에 타워별 가격이 또 바뀌어도 여기 한 곳만 고치면 된다.
        /// 새 타워를 추가하면 여기에 case를 꼭 넣어야 한다 — 빠뜨리면 조용히 TowerCost(15)로
        /// 취급되어 "왜 이 타워만 싸지?" 같은 원인 모를 밸런스 버그가 된다.</summary>
        public static int CostFor(TowerType type) => type switch
        {
            TowerType.Cannon => CannonTowerCost,
            TowerType.Lightning => LightningTowerCost,
            _ => TowerCost,
        };

        /// <summary>[해설] 타워를 철거할 때 건설 비용 중 돌려받는 비율(%). 전액을 돌려주면
        /// "아무 데나 짓고 마음에 안 들면 무료로 옮기기"가 최적 전략이 되어 배치 선택의 긴장감이
        /// 사라지므로, 절반만 돌려줘서 재배치에 약간의 대가가 따르게 했다.</summary>
        public const int TowerRefundPercent = 50;

        /// <summary>타워 철거 시 돌려받는 골드(정수 내림). 현재 비용 기준으로
        /// 화살탑·빙결탑 15 → 7, 번개탑 25 → 12, 포격탑 30 → 15.</summary>
        public static int RefundFor(TowerType type) => CostFor(type) * TowerRefundPercent / 100;

        public const float PrepPhaseSeconds = 8f; // 준비 단계(건설/배치) 기본 시간

        /// <summary>[해설] 직전 웨이브를 "스킵"으로 끝냈을 때만 적용되는 짧은 준비 시간(초).
        /// 스킵은 곧 "빨리 넘어가고 싶다"는 의사표시인데, 그래놓고 다음 웨이브까지 기본 8초를
        /// 그대로 기다리게 하면 스킵한 보람이 없다. 그래서 이 경우에만 1초로 줄인다.
        /// (준비 단계 자체를 건너뛰지는 않는다 — 보상 선택 후 상황을 볼 최소한의 틈은 남겨둔다.)</summary>
        public const float PrepPhaseSecondsAfterSkip = 1f;

        // 플레이 필드 경계 (월드 유닛)
        // [해설] ★ 2.5D 전환 후 이 두 값은 사실상 <b>죽은 제약</b>이다. 배치 가능 여부는
        // PathData.ContainsPoint(길 안쪽인가)가 먼저 가르는데, 가장 넓은 도안도 |x| 6.0 /
        // |y| 3.3을 넘지 않아서 아래 경계(10.2 / 5.5)에는 절대 닿지 않는다.
        // 실제 화면 범위도 더 이상 이 값과 무관하다 — 카메라가 (0, 1.05)에 크기 6.0(좁은 화면비에서는
        // 자동 확대)이라 보이는 범위가 y -4.95~7.05로 위아래가 <b>비대칭</b>이다
        // (GameBootstrapper.SetupCamera 참고). 나중에 필드를 다시 손볼 때 이 숫자를 기준으로
        // 삼으면 틀리니, 도안 크기는 PathLibrary에서 카메라 범위를 직접 보고 정할 것.
        // 값 자체는 혹시 ContainsPoint가 빠지는 변경이 생겼을 때의 최후 방어선으로 남겨 둔다.
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
