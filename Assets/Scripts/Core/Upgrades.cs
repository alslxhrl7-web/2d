namespace Defense2D
{
    /// <summary>
    /// 웨이브 클리어 보상으로 선택하는 업그레이드 종류.
    /// (플레이어 캐릭터가 제거되어, 플레이어 전용 업그레이드는 함께 제외했다.)
    /// </summary>
    public enum UpgradeKind
    {
        AliveCapacity,
        GoldGain,

        /// <summary>
        /// 타워 공격력 강화. [해설] ★ 예전에는 이 하나가 <b>모든 타워</b>에 한꺼번에 곱해졌다.
        /// 그러면 고를지 말지만 있고 "무엇을 키울까"라는 선택이 없어서, 사실상 매번 같은 답이었다.
        /// 이제는 타워 종류별로 따로 쌓인다(UpgradeOption.Target이 어느 타워인지 가리킨다).
        /// 보상마다 네 타워 중 일부만 후보로 뜨므로, 지금 깔아 둔 타워 구성에 맞춰 무엇을
        /// 키울지 고르게 된다.
        /// </summary>
        TowerDamage,
    }

    public class UpgradeOption
    {
        public UpgradeKind Kind;
        public string Label;
        public string Description;

        /// <summary>Kind가 TowerDamage일 때, 강화 대상 타워 종류. 다른 Kind에서는 쓰이지 않는다.</summary>
        public TowerType Target;
    }
}
