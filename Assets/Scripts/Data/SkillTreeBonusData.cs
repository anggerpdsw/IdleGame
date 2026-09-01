using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Persistent data for the SkillTreeBonus system.
    /// Stores allocated skill points and pending choice state.
    /// </summary>
    [Serializable]
    public class SkillTreeBonusData
    {
        /// <summary>
        /// Allocated points per skill type.
        /// Key: SkillType enum value (as string or int serialization).
        /// Value: Number of points allocated to this skill.
        /// 
        /// Example:
        /// {
        ///   "AttackDamage": 3,
        ///   "MoveSpeed": 2,
        ///   "HealthPoint": 1
        /// }
        /// </summary>
        public Dictionary<string, int> allocatedSkills = new();

        /// <summary>
        /// Pending choices generated but not yet applied.
        /// Stores SkillType values as strings (serialization-friendly).
        /// Count should be 0 or 6 (never partial).
        /// 
        /// When empty (Count == 0):
        /// - No pending choices are active.
        /// 
        /// When Count == 6:
        /// - Player can select from these 6 options.
        /// - Data persists across game restarts.
        /// - Do not reroll/regenerate these choices on load.
        /// </summary>
        public List<string> pendingChoices = new();

        /// <summary>
        /// Currently selected skills from pendingChoices.
        /// Stores SkillType values as strings.
        /// Count should be 0-3.
        /// 
        /// Cleared when Confirm is pressed successfully.
        /// </summary>
        public List<string> selectedChoices = new();

        /// <summary>
        /// Whether a choice batch is currently active.
        /// True = player is in the middle of selecting skills.
        /// False = no active selection batch.
        /// 
        /// Used to determine whether to display pending choices UI
        /// or generate a new batch.
        /// </summary>
        public bool isSelectionActive = false;

        /// <summary>
        /// Create a copy of this data (shallow copy of collections).
        /// Used for comparison and debugging.
        /// </summary>
        public SkillTreeBonusData Clone()
        {
            return new SkillTreeBonusData
            {
                allocatedSkills = new Dictionary<string, int>(allocatedSkills),
                pendingChoices = new List<string>(pendingChoices),
                selectedChoices = new List<string>(selectedChoices),
                isSelectionActive = isSelectionActive
            };
        }
    }
}
