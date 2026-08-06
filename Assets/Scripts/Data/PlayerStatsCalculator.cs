using UnityEngine;

namespace IdleDefenseSurvival.Data
{
    public static class PlayerStatsCalculator
    {
        public static float CalculateSkillFloatValue(SkillData skill)
        {
            if (skill.level <= 0) return 0f;

            float t = Mathf.Clamp01((float)skill.level / skill.maxLevel);
            return Mathf.Lerp(skill.min, skill.max, t);
        }

        public static int CalculateSkillIntValue(SkillData skill)
        {
            if (skill.level <= 0) return 0;
            if (skill.maxLevel <= 1) return Mathf.RoundToInt(skill.max);

            float t = (float)(skill.level - 1) / (skill.maxLevel - 1);
            return Mathf.RoundToInt(Mathf.Lerp(skill.min, skill.max, t));
        }

    }
}
