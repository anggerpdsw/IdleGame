using System;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager; // for IUpgradeService

namespace IdleDefenseSurvival.Player
{
    public static class StatLoader
    {
        public static void LoadFromPlayerData(PlayerData playerData)
        {
            Load(playerData, null);
        }

        public static void LoadWithUpgrades(PlayerData playerData, UpgradeManager upgradeManager)
        {
            Load(playerData, upgradeManager);
        }

        private static void Load(PlayerData playerData, IUpgradeService upgradeService)
        {
            if (playerData?.skills == null) return;

            foreach (var group in playerData.skills.GetType().GetFields())
            {
                ProcessGroup(group.GetValue(playerData.skills), upgradeService);
            }
            
            PlayerStatsManager.Instance.RefreshStats();
        }

        private static void ProcessGroup(object group, IUpgradeService upgradeService)
        {
            if (group == null) return;

            foreach (var field in group.GetType().GetFields())
            {
                if (field.FieldType != typeof(SkillData)) continue;

                if (!TryGetStatType(field.Name, out var statType)) continue;

                SkillData skill = (SkillData)field.GetValue(group);

                if (upgradeService != null)
                {
                    skill = CloneWithLevel(skill, upgradeService.GetSkillLevel(field.Name));
                }

                float value = skill.isFloat
                    ? PlayerStatsCalculator.CalculateSkillFloatValue(skill)
                    : PlayerStatsCalculator.CalculateSkillIntValue(skill);

                PlayerStatsManager.Instance.SetBaseStat(statType, value);
            }
        }

        private static bool TryGetStatType(string skillName, out SkillType statType)
        {
            statType = default;
            if (string.IsNullOrWhiteSpace(skillName)) return false;

            string enumName = char.ToUpperInvariant(skillName[0]) + skillName[1..];
            return Enum.TryParse(enumName, out statType);
        }

        private static SkillData CloneWithLevel(SkillData skill, int level)
        {
            return new SkillData
            {
                level = level,
                maxLevel = skill.maxLevel,
                min = skill.min,
                max = skill.max,
                isFloat = skill.isFloat,
                locked = skill.locked,
                description = skill.description,
                displayName = skill.displayName
            };
        }
    }
}