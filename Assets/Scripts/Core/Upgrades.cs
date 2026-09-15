namespace Defense2D
{
    /// <summary>
    /// 웨이브 클리어 보상으로 선택하는 업그레이드 종류 (기획서 "업그레이드 8개" 대응).
    /// </summary>
    public enum UpgradeKind
    {
        PlayerDamage,
        PlayerAttackSpeed,
        PlayerRange,
        PlayerMoveSpeed,
        BaseMaxHp,
        SkillCooldown,
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
