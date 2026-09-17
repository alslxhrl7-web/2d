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
        TowerDamage,
    }

    public class UpgradeOption
    {
        public UpgradeKind Kind;
        public string Label;
        public string Description;
    }
}
