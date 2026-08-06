using System.Collections.Generic;

namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Contract for the upgrade service. Handles skill‑level queries, unlocks and upgrades.
    /// </summary>
    public interface IUpgradeService
    {
        int GetSkillLevel(string skillId);
        int GetSkillMaxLevel(string skillId);
        bool CanUpgrade(string skillId);
        bool IsSkillLocked(string skillId);
        bool UnlockSkill(string skillId);
        bool UpgradeSkill(string skillId, string reason = "");
        void SetSkillLevel(string skillId, int level);
        Dictionary<string, int> GetAllSkillLevels();
        void SetAllSkillLevels(Dictionary<string, int> levels);
        event System.Action<string, int, int> OnSkillUpgraded; // skillId, oldLevel, newLevel
    }
}