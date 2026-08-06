using System;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Loads player skill base values from dataPlayer.json into PlayerStatsManager.
    /// No levels, no upgrades — skills are static values from JSON.
    /// </summary>
    public static class StatLoader
    {
        public static void LoadBaseStats(PlayerData playerData)
        {
            if (playerData?.skills == null) return;

            foreach (var group in playerData.skills.GetType().GetFields())
                ProcessGroup(group.GetValue(playerData.skills));

            PlayerStatsManager.Instance.RefreshStats();
        }

        private static void ProcessGroup(object group)
        {
            if (group == null) return;

            foreach (var field in group.GetType().GetFields())
            {
                if (field.FieldType != typeof(SkillData)) continue;
                if (!TryGetStatType(field.Name, out var statType)) continue;

                var skill = (SkillData)field.GetValue(group);
                PlayerStatsManager.Instance.SetBaseStat(statType, skill.baseValue);
            }
        }

        private static bool TryGetStatType(string skillName, out SkillType statType)
        {
            statType = default;
            if (string.IsNullOrWhiteSpace(skillName)) return false;

            string enumName = char.ToUpperInvariant(skillName[0]) + skillName[1..];
            return Enum.TryParse(enumName, out statType);
        }
    }
}
