using System.Collections.Generic;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    public class PlayerStats
    {
        private readonly Dictionary<SkillType, float> _baseStats = new();

        public void SetBaseStat(SkillType type, float value)
        {
            _baseStats[type] = value;
        }

        public float GetBaseStat(SkillType type)
        {
            return _baseStats.TryGetValue(type, out var value) ? value : 0f;
        }

        public bool HasBaseStat(SkillType type)
        {
            return _baseStats.ContainsKey(type);
        }

        public void AddBaseStat(SkillType stat, float value)
        {
            _baseStats[stat] = GetBaseStat(stat) + value;
        }

        public void Clear()
        {
            _baseStats.Clear();
        }

    }
}
