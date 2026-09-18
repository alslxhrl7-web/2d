namespace Defense2D
{
    // 건설 메뉴에서 등장하는 타워 종류.
    // [해설] 여기에 값을 추가하면 BuildManager.RandomTowerType()이 자동으로 새 값까지 포함해서
    // 굴린다(Enum 길이 기반). 다만 아래 네 곳은 수동으로 같이 채워 줘야 한다:
    //   1) GameConstants.CostFor()        — 건설 비용
    //   2) BuildManager.TowerRangeFor()   — 배치 미리보기 사거리 링 (실제 Setup()의 Range와 일치시킬 것)
    //   3) BuildManager.TowerLabel()      — 화면 표기 이름
    //   4) BuildManager.ApplyTowerVisual()/SpawnTower() — 대체 도형 색상과 컴포넌트 부착
    public enum TowerType { Arrow, Ice, Cannon, Lightning }
}
