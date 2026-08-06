using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using Newtonsoft.Json;
using IdleDefenseSurvival.Upgrade;
using static IdleDefenseSurvival.Manager.SaveManager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Singleton manager for player skill upgrades.
    /// Handles level tracking, cost verification, currency deduction,
    /// and reloading player stats after upgrades.
    /// </summary>
    public class UpgradeManager : MonoBehaviour, IUpgradeService
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static UpgradeManager _instance;
        public static UpgradeManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        [SerializeField] private bool debug;
        // -------------------------------------------------------------------
        // Events for UI/Systems
        // -------------------------------------------------------------------
        [Serializable]
        public class SkillUpgradedEvent : UnityEngine.Events.UnityEvent<string, int, int>
        {
            // skillId, oldLevel, newLevel
        }

        [Header("Events")]
        public SkillUpgradedEvent OnSkillUpgraded { get; } = new();

        // -------------------------------------------------------------------
        // Upgrade Data
        // -------------------------------------------------------------------
        private Dictionary<string, int> _skillLevels = new(); // level per flat skillId (e.g., "attackDamage")
        private Dictionary<string, SkillData> _skillData = new(); // meta data per flat skillId
        private PlayerData _playerData;

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------
        private void Initialize()
        {
            UpgradeCostCalculator.Initialize();

            // Load initial skill levels from dataPlayer.json
            // SaveManager will override with saved data if it exists
            LoadInitialSkillLevels();
        }

        /// <summary>
        /// Load initial skill levels from dataPlayer.json when player hasn't played before.
        /// </summary>
        private void LoadInitialSkillLevels()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null)
            {
                if (debug) Debug.LogError("[UpgradeManager] dataPlayer.json not found!");
                return;
            }

            _playerData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
            if (_playerData?.skills == null) return;

            // Initialize all skills at their starting level with the new grouped structure
            PopulateSkillDictionaries(_playerData.skills);
        }

        /// <summary>
        /// Populate _skillLevels and _skillData using reflection on the grouped structure.
        /// Uses flat skill IDs (e.g., "attackDamage") for compatibility with UpgradeCostCalculator
        /// and existing saves.
        /// </summary>
        private void PopulateSkillDictionaries(PlayerSkills groups)
        {
            _skillLevels.Clear();
            _skillData.Clear();

            void ProcessGroup(object group)
            {
                if (group == null) return;
                var fields = group.GetType().GetFields();
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(SkillData))
                    {
                        var skill = (SkillData)field.GetValue(group);
                        string flatId = field.Name; // flat identifier (e.g., "attackDamage")
                        _skillLevels[flatId] = skill.level;
                        _skillData[flatId] = skill;
                    }
                    else if (field.FieldType.IsClass && !field.FieldType.IsAbstract)
                    {
                        ProcessGroup(field.GetValue(group));
                    }
                }
            }

            ProcessGroup(groups.attack);
            ProcessGroup(groups.defense);
            ProcessGroup(groups.currency);
        }

        // -------------------------------------------------------------------
        // Skill Level Operations (Adapted to support grouped skill structure)
        // -------------------------------------------------------------------

        /// <summary>
        /// Get the current level of a skill using flattened skill ID.
        /// Supports the new grouped structure (e.g., "attack/attackDamage").
        /// Falls back to original skill names for backward compatibility.
        /// </summary>
        public int GetSkillLevel(string skillId)
        {
            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        /// <summary>
        /// Get the max level of a skill using the flattened skill structure.
        /// Returns 0 for skills that have been migrated to dataUltimate.json (bomb/tank).
        /// </summary>
        public int GetSkillMaxLevel(string skillId)
        {
            // Handle skills migrated to dataUltimate.json - they have no max level in dataPlayer.json
            if (IsMigratedSkill(skillId))
            {
                return 0; // Signals this skill is no longer upgradable
            }

            if (_skillData.TryGetValue(skillId, out var data)) return data.maxLevel; // Hint: remove unnecessary assignment of 'data'
            return 100; // default for unknown skills
        }

        /// <summary>
        /// Check if a skill has been migrated to dataUltimate.json.
        /// These skills are no longer upgradable via the upgrade system.
        /// </summary>
        private bool IsMigratedSkill(string skillId)
        {
            return skillId is "bomb" or "tank" or "toxicDeathCloud" or "shockwave";
        }

        /// <summary>
        /// Check if a skill can be upgraded.
        /// Returns false for skills migrated to dataUltimate.json.
        /// </summary>
        public bool CanUpgrade(string skillId)
        {
            if (IsMigratedSkill(skillId)) return false;

            int currentLevel = GetSkillLevel(skillId);
            int maxLevel = GetSkillMaxLevel(skillId);

            if (currentLevel >= maxLevel) return false;

            return UpgradeCostCalculator.CanAfford(skillId, currentLevel);
        }

        /// <summary>
        /// Check if a skill requires unlocking before it can be upgraded.
        /// A skill is locked if its current level is 0 AND the config defines an unlock cost.
        /// Once unlocked (level >= 1), RequiresUnlock no longer applies.
        /// </summary>
        public bool IsSkillLocked(string skillId)
        {
            // Unknown skill — treat as unlocked
            if (!_skillData.ContainsKey(skillId))
                return false;

            int currentLevel = GetSkillLevel(skillId);

            // Only locked when level == 0 AND config requires unlock
            return currentLevel <= 0 && UpgradeCostCalculator.RequiresUnlock(skillId);
        }

        /// <summary>
        /// Unlock a locked skill by spending the unlock cost (Gem by default).
        /// Returns true if unlock succeeded, false otherwise.
        /// </summary>
        public bool UnlockSkill(string skillId)
        {
            // Early exit: cannot unlock a skill that is not locked
            if (!IsSkillLocked(skillId))
            {
                // This should not typically be called for unlocked skills.
                if (debug) Debug.LogWarning($"[UpgradeManager] UnlockSkill: {skillId} is not locked (no unlock needed).");
                return false;
            }

            // Verify player can afford unlock cost
            if (!UpgradeCostCalculator.CanAffordUnlock(skillId))
            {
                if (debug) Debug.LogWarning($"[UpgradeManager] Not enough currency to unlock {skillId}.");
                return false;
            }

            // Deduct unlock cost
            var unlockCosts = UpgradeCostCalculator.CalculateUnlockCost(skillId);
            var economy = Economy.EconomyManager.Instance;
            if (economy == null)
            {
                if (debug) Debug.LogError("[UpgradeManager] EconomyManager not found!");
                return false;
            }

            foreach (var kvp in unlockCosts)
            {
                if (!economy.TrySpendCurrency(kvp.Key, kvp.Value, $"Unlock {skillId}"))
                    return false;
            }

            // Mark unlocked – we simply set the skill level to its starting level (probably 1)
            // The starting level is stored in _skillData (original data from dataPlayer.json).
            if (_skillData.TryGetValue(skillId, out var skillData))
            {
                // Unlock moves the skill level to its starting point (usually 1)
                _skillLevels[skillId] = Mathf.Max(1, skillData.level);
                OnSkillUpgraded?.Invoke(skillId, 0, _skillLevels[skillId]);
                Player.Player.Instance?.ReloadStats();
                if (debug) Debug.Log($"[UpgradeManager] Skill {skillId} unlocked.");
                return true;
            }

            return false;
        }

        public bool UpgradeSkill(string skillId, string reason = "")
        {
            // Ensure the skill is unlocked first
            if (IsSkillLocked(skillId))
            {
                if (debug) Debug.LogWarning($"[UpgradeManager] Skill {skillId} is locked. Unlock it before upgrading.");
                return false;
            }

            int currentLevel = GetSkillLevel(skillId);
            int maxLevel = GetSkillMaxLevel(skillId);

            // Validation
            if (currentLevel >= maxLevel)
            {
                if (debug) Debug.LogWarning($"[UpgradeManager] {skillId} is already at max level ({maxLevel}).");
                return false;
            }

            // Check costs
            var costs = UpgradeCostCalculator.CalculateUpgradeCost(skillId, currentLevel);
            if (costs == null)
            {
                if (debug) Debug.LogError($"[UpgradeManager] No upgrade config found for {skillId}.");
                return false;
            }

            // Deduct currency
            var economy = Economy.EconomyManager.Instance;
            if (economy == null)
            {
                if (debug) Debug.LogError("[UpgradeManager] EconomyManager not found!");
                return false;
            }

            foreach (var kvp in costs)
            {
                if (!economy.TrySpendCurrency(kvp.Key, kvp.Value, $"Upgrade {skillId} to level {currentLevel + 1}"))
                    return false;
            }

            // Apply upgrade - use the same format for _skillLevels
            _skillLevels[skillId] = currentLevel + 1;

            // Fire event
            OnSkillUpgraded?.Invoke(skillId, currentLevel, currentLevel + 1);

            // Update Player stats
            Player.Player.Instance?.ReloadStats();

            // Log
            if (!string.IsNullOrEmpty(reason))
                if (debug) Debug.Log($"[UpgradeManager] Upgraded {skillId} ({reason})");
            else
                if (debug) Debug.Log($"[UpgradeManager] Upgraded {skillId}: {currentLevel} -> {currentLevel + 1}");

            return true;
        }

        /// <summary>
        /// Set skill level directly (use for save/load or debugging).
        /// </summary>
        public void SetSkillLevel(string skillId, int level)
        {
            int oldLevel = GetSkillLevel(skillId);
            _skillLevels[skillId] = level;
            OnSkillUpgraded?.Invoke(skillId, oldLevel, level);
            Player.Player.Instance?.ReloadStats();
        }

        /// <summary>
        /// Get all skill levels as a dictionary (for SaveManager).
        /// </summary>
        public Dictionary<string, int> GetAllSkillLevels()
        {
            return new Dictionary<string, int>(_skillLevels);
        }

        /// <summary>
        /// Set all skill levels at once (for SaveManager load).
        /// Automatically merges missing skills from dataPlayer.json defaults
        /// to handle save files created before new skills were added.
        /// </summary>
        public void SetAllSkillLevels(Dictionary<string, int> skillLevels)
        {
            if (skillLevels == null) return;

            _skillLevels = new Dictionary<string, int>(skillLevels);

            // Merge: ensure any skills defined in PlayerSkills but missing from save file
            // are initialized with their default level from dataPlayer.json.
            // This handles save files created before new skills (e.g. bombChance) were added.
            MergeMissingSkillsFromDefaults();

            // Reload player stats to apply all changes at once
            Player.Player.Instance?.ReloadStats();
        }

        /// <summary>
        /// Check if any skills from dataPlayer.json are missing in _skillLevels
        /// and add them with their starting level.
        /// Called after loading save data to handle schema migrations.
        /// Now handles the grouped skill structure.
        /// </summary>
        private void MergeMissingSkillsFromDefaults()
        {
            if (_playerData?.skills == null)
            {
                // Reload dataPlayer.json if not loaded yet
                TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
                if (jsonAsset == null) return;
                _playerData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
                if (_playerData?.skills == null) return;
            }

            // Populate _skillData (meta) without clearing _skillLevels (which has saved data)
            _skillData.Clear();
            void ProcessGroupForData(object group)
            {
                if (group == null) return;
                var fields = group.GetType().GetFields();
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(SkillData))
                    {
                        var skill = (SkillData)field.GetValue(group);
                        _skillData[field.Name] = skill;
                    }
                    else if (field.FieldType.IsClass && !field.FieldType.IsAbstract)
                    {
                        ProcessGroupForData(field.GetValue(group));
                    }
                }
            }
            ProcessGroupForData(_playerData.skills.attack);
            ProcessGroupForData(_playerData.skills.defense);
            ProcessGroupForData(_playerData.skills.currency);

            // Add any skills missing from the save file with their default level
            foreach (var kv in _skillData)
            {
                if (!_skillLevels.ContainsKey(kv.Key))
                {
                    _skillLevels[kv.Key] = kv.Value.level;
                    if (debug) Debug.Log($"[UpgradeManager] Added missing skill '{kv.Key}' with level {kv.Value.level} (schema migration)");
                }
            }
        }

        public UpgradeData GatherUpgradeData()
        {
            var existingLevels = GetAllSkillLevels();
            if (existingLevels == null || existingLevels.Count == 0)
            {
                Debug.LogWarning("[SaveManager] ⚠️ Skill levels empty, triggering reload from dataPlayer.json...");
                ForceLoadInitialSkills();
                existingLevels = GetAllSkillLevels();
            }

            if (existingLevels == null || existingLevels.Count == 0)
            {
                Debug.LogError("[SaveManager] ❌ Still empty after reload! dataPlayer.json might be missing or has wrong format.");
                return new UpgradeData();
            }

            return new UpgradeData { skillLevels = existingLevels };
        }

        public void ForceLoadInitialSkills() => LoadInitialSkillLevels();

        public void ResetProgress()
        {
            _skillLevels.Clear();
            Player.Player.Instance?.ReloadStats();
            // Note: SaveManager handles persistence, no need to save here
            if (debug) Debug.Log("[UpgradeManager] Progress reset.");
        }

    }
}
