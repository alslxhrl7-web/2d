using System.Collections.Generic;

namespace Defense2D
{
    public struct SpawnEntry
    {
        public EnemyType Type;
        public int PathIndex;
        public float Delay; // 직전 스폰으로부터의 상대 지연 시간(초)
    }

    public class WaveDefinition
    {
        public int WaveNumber;
        public bool IsBoss;
        /// <summary>스테이지 마지막(피날레) 웨이브인지. true면 보스와 일반 유닛이 함께 나오고,
        /// 제한시간 안에 보스를 못 잡으면 즉시 게임오버된다(WaveManager.Update 참고).</summary>
        public bool IsStageFinale;
        /// <summary>보스 능력/이름 패턴 선택용(1..5, BossController.RunAbility 참고). 보스 인카운터가
        /// 5번을 넘어가면 다시 1번부터 순환한다.</summary>
        public int BossIndex;
        /// <summary>보스 체력/처치 보상 스케일링용(1, 2, 3, ...). 패턴은 순환하지만 이 값은 게임
        /// 전체에서 계속 누적 증가해서, 뒤로 갈수록 보스가 꾸준히 강해지도록 한다.</summary>
        public int BossEncounterNumber;
        public List<SpawnEntry> Entries = new List<SpawnEntry>();
    }
}
