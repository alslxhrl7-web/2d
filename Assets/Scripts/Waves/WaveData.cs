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
        public int BossIndex;
        public List<SpawnEntry> Entries = new List<SpawnEntry>();
    }
}
