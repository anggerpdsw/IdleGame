using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.SkillTree
{
    /// <summary>
    /// Manages the SkillTreeBonus system.
    /// 
    /// Responsibilities:
    /// - Generate random skill choices (6 unique skills)
    /// - Persist pending choices to SaveData
    /// - Handle skill selection (max 3 per batch)
    /// - Apply skill bonuses to SkillTreeBonusData
    /// - Allocate skill points from unspentSkillPoints
    /// - Integrate with level-up events
    /// - Calculate total bonus per skill type
    /// - Update ModifierManager with skill tree modifiers
    /// 
    /// Architecture:
    /// - Singleton, DontDestroyOnLoad
    /// - Works alongside SaveManager for persistence
    /// - Notifies PlayerStatsManager of changes
    /// - Registers modifiers with ModifierManager
    /// </summary>
    public class SkillTreeBonusManager : MonoBehaviour
    {
        #region Singleton
        [SerializeField] private bool _debug = false;
        private static SkillTreeBonusManager _instance;
        public static SkillTreeBonusManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Tunggu save load
            if (SaveManager.Instance != null && SaveManager.Instance.IsSaveLoaded)
            {
                InitializeFromSaveData();
            }
            else
            {
                SaveManager.OnSaveLoaded += OnSaveLoadedInitialize;
            }

            // Subscribe to level-up event
            if (AccountManager.Instance != null)
                AccountManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }

        private void OnDestroy()
        {
            if (AccountManager.Instance != null)
                AccountManager.Instance.OnLevelUp -= OnPlayerLevelUp;
            SaveManager.OnSaveLoaded -= OnSaveLoadedInitialize;
        }
                
        private void OnSaveLoadedInitialize()
        {
            SaveManager.OnSaveLoaded -= OnSaveLoadedInitialize;
            InitializeFromSaveData();
        }
        #endregion

        #region Data Access

        /// <summary>Get the current SkillTreeBonusData from SaveData.</summary>
        private SkillTreeBonusData Data 
            => SaveManager.Instance.LastLoadedSaveData?.skillTreeBonus;

        /// <summary>Get the current AccountData.</summary>
        private AccountData Account => SaveManager.Instance.GetAccountData();

        /// <summary>
        /// Get the current SkillTreeBonusData from SaveData.
        /// Used by SaveManager to persist state.
        /// </summary>
        public SkillTreeBonusData GetSaveData()
        {
            return Data ?? new SkillTreeBonusData();
        }

        #endregion

        #region Events

        /// <summary>Fired when pending choices are generated/loaded.</summary>
        public event Action OnPendingChoicesUpdated;

        /// <summary>Fired when a skill is selected/deselected.</summary>
        public event Action OnSelectionChanged;

        /// <summary>Fired after Confirm is successfully processed.</summary>
        public event Action OnConfirmed;

        #endregion

        #region Public API
        public int UnspentSkillPoints => Account?.unspentSkillPoints ?? 0;

        /// <summary>
        /// Get total number of skill points already allocated across all skills.
        /// Used to calculate remaining unspent points.
        /// </summary>
        public int GetTotalAllocatedSkillPoints()
        {
            var data = Data;
            if (data == null) return 0;
            var total = 0;
            foreach (var kvp in data.allocatedSkills)
            {
                total += kvp.Value;
            }
            return total;
        }

        /// <summary>
        /// Calculate how many unspent skill points a player SHOULD have based on their level.
        /// Formula: level - totalAllocatedSkillPoints
        /// Level 1 = 1 skill point
        /// Level 2 = 2 skill point
        /// Level 6 = 6 skill point
        /// Then subtract what's already allocated.
        /// </summary>
        public int CalculateCorrectUnspentPoints()
        {
            var account = Account;
            if (account == null) return 0;
            // Skill points equal level directly
            // Level 1 = 1 skill point
            // Level 2 = 2 skill point
            // Level N = N skill point
            var totalSkillPoints = account.level;
            var totalAllocated = GetTotalAllocatedSkillPoints();
            // Unspent = total - allocated
            var unspent = totalSkillPoints - totalAllocated;
            // Should never be negative
            return Mathf.Max(0, unspent);
        }

        /// <summary>
        /// Recover unspent skill points for old saves that don't have this field set correctly.
        /// Called during initialization to ensure backward compatibility.
        /// </summary>
        private void RecoverUnspentSkillPoints()
        {
            var account = Account;
            if (account == null) return;
            var correct = CalculateCorrectUnspentPoints();
            account.unspentSkillPoints = correct;
            if (correct > 0)
            {
                if (_debug) Debug.Log($"[SkillTreeBonus] Recovered {correct} unspent skill points for Level {account.level} player");
                SaveManager.Instance.SaveAll();
            }
        }

        /// <summary>
        /// Get the current player level.
        /// </summary>
        public int CurrentLevel => Account?.level ?? 1;

        /// <summary>
        /// Get total skill points that should have been earned.
        /// Formula: level (Level 1 = 1 skill point, Level 6 = 6 skill points)
        /// </summary>
        public int TotalEarnedSkillPoints => CurrentLevel;

        /// <summary>
        /// Check if player has unspent skill points.
        /// </summary>
        public bool HasUnspentSkillPoints()
        {
            var account = Account;
            return account != null && account.unspentSkillPoints > 0;
        }

        /// <summary>
        /// Check if there are pending choices to display.
        /// </summary>
        public bool HasPendingSkillChoices()
        {
            var data = Data;
            return data != null && data.pendingChoices.Count > 0;
        }

        /// <summary>
        /// Get the current pending choices as SkillType enum in SkillType.cs.
        /// </summary>
        public IReadOnlyList<SkillType> GetPendingChoices()
        {
            var data = Data;
            var result = new List<SkillType>();
            if (data == null) return result;
            foreach (var skillStr in data.pendingChoices)
            {
                if (Enum.TryParse<SkillType>(skillStr, out var skillType))
                    result.Add(skillType);
            }
            return result;
        }

        /// <summary>
        /// Get the current selected choices as SkillType enum.
        /// </summary>
        public IReadOnlyList<SkillType> GetSelectedChoices()
        {
            var data = Data;
            var result = new List<SkillType>();
            if (data == null) return result;
            foreach (var skillStr in data.selectedChoices)
            {
                if (Enum.TryParse<SkillType>(skillStr, out var skillType))
                    result.Add(skillType);
            }
            return result;
        }

        /// <summary>
        /// Select a skill from pending choices.
        /// Returns true if successful, false if invalid (already selected, not in pending, etc).
        /// </summary>
        public bool SelectSkill(SkillType skillType)
        {
            var data = Data;
            if (data == null) return false;
            // Validation
            if (skillType == SkillType.None) return false;
            var skillStr = skillType.ToString();
            // Check if skill is in pending choices
            if (!data.pendingChoices.Contains(skillStr)) return false;
            // Check if already selected
            if (data.selectedChoices.Contains(skillStr)) return false;
            // Check if max 3 reached
            if (data.selectedChoices.Count >= 3) return false;

            // Add to selection
            data.selectedChoices.Add(skillStr);
            SaveManager.Instance.SaveAll();
            OnSelectionChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Deselect a previously selected skill.
        /// Returns true if successful.
        /// </summary>
        public bool DeselectSkill(SkillType skillType)
        {
            var data = Data;
            if (data == null) return false;
            var skillStr = skillType.ToString();
            if (data.selectedChoices.Remove(skillStr))
            {
                SaveManager.Instance.SaveAll();
                OnSelectionChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if the player can select more skills (< 3 selected).
        /// </summary>
        public bool CanSelectMoreSkills()
        {
            var data = Data;
            return data != null && data.selectedChoices.Count < 3;
        }

        /// <summary>
        /// Get number of currently selected skills.
        /// </summary>
        public int GetSelectedCount()
        {
            var data = Data;
            return data != null ? data.selectedChoices.Count : 0;
        }

        /// <summary>
        /// Confirm the current selection and apply bonuses.
        /// Returns true if successful, false if validation fails.
        /// </summary>
        public bool ConfirmSelection()
        {
            var data = Data;
            var account = Account;
            if (data == null || account == null) return false;
            // Guard against double-click
            if (!data.isSelectionActive || data.pendingChoices.Count == 0)
                return false;
            // Validate selected count - must be EXACTLY 3
            var selectedCount = data.selectedChoices.Count;
            if (selectedCount != 3)
                return false;
            // Validate all selections are from pending
            foreach (var selectedStr in data.selectedChoices)
            {
                if (!data.pendingChoices.Contains(selectedStr))
                    return false;
            }
            // Apply bonuses
            foreach (var selectedStr in data.selectedChoices)
            {
                if (Enum.TryParse<SkillType>(selectedStr, out var skillType))
                {
                    // Increment allocated points
                    if (!data.allocatedSkills.ContainsKey(selectedStr))
                        data.allocatedSkills[selectedStr] = 0;
                    data.allocatedSkills[selectedStr]++;
                }
            }

            // Consume one unspent skill point
            account.unspentSkillPoints--;

            // Clear selection state
            data.pendingChoices.Clear();
            data.selectedChoices.Clear();
            data.isSelectionActive = false;

            // Save immediately
            SaveManager.Instance.SaveAll();

            // Refresh player stats to include new bonuses
            ApplySkillTreeBonusesToModifiers();
            PlayerStatsManager.Instance?.RefreshStats();

            OnConfirmed?.Invoke();

            return true;
        }

        /// <summary>
        /// Generate 6 random skill choices and store to SaveData.
        /// Only call when ready to start a new selection batch.
        /// </summary>
        public bool GenerateChoices()
        {
            var data = Data;
            if (data == null) return false;
            // Ensure no pending choices already
            if (data.pendingChoices.Count > 0) return false;
            // Get valid skill pool
            var validPool = GetValidSkillPool();
            if (validPool.Count < 6)
            {
                if (_debug) Debug.LogError("[SkillTreeBonus] Valid skill pool has fewer than 6 skills!");
                return false;
            }

            // Shuffle and pick 6
            var shuffled = validPool.OrderBy(_ => UnityEngine.Random.value).ToList();
            var chosen = shuffled.Take(6).ToList();

            // Store as strings
            data.pendingChoices = chosen.Select(s => s.ToString()).ToList();
            data.selectedChoices.Clear();
            data.isSelectionActive = true;

            SaveManager.Instance.SaveAll();
            OnPendingChoicesUpdated?.Invoke();

            return true;
        }

        /// <summary>
        /// Open/activate the skill tree UI.
        /// If pending choices exist, restore them.
        /// Otherwise, generate new choices.
        /// </summary>
        public bool OpenSkillTreeSelection()
        {
            if (!HasUnspentSkillPoints()) return false;
            var data = Data;
            if (data == null) return false;
            // If pending choices exist, restore them
            if (data.pendingChoices.Count == 6)
            {
                data.isSelectionActive = true;
                SaveManager.Instance.SaveAll();
                OnPendingChoicesUpdated?.Invoke();
                return true;
            }

            // Otherwise generate new batch
            return GenerateChoices();
        }

        /// <summary>
        /// Get the total bonus for a skill type.
        /// Formula: allocatedPoints × bonusPerPoint (from dataPlayer.json)
        /// </summary>
        public float GetTotalBonus(SkillType skillType)
        {
            var data = Data;
            if (data == null) return 0f;
            var skillStr = skillType.ToString();
            if (!data.allocatedSkills.TryGetValue(skillStr, out var points) || points <= 0)
                return 0f;
            // Load bonus per point from dataPlayer.json
            var loader = BaseStatLoader.Instance;
            if (loader == null) return 0f;
            var skillData = loader.GetSkillData(skillType);
            if (skillData == null) return 0f;
            return points * skillData.bonusPerPoint;
        }

        /// <summary>
        /// Get the number of allocated points for a skill type.
        /// </summary>
        public int GetAllocatedPoints(SkillType skillType)
        {
            var data = Data;
            if (data == null) return 0;
            var skillStr = skillType.ToString();
            return data.allocatedSkills.TryGetValue(skillStr, out var points) ? points : 0;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initialize the manager from SaveData on Awake.
        /// Ensures modifiers are applied if they exist.
        /// Handles backward compatibility for old saves.
        /// </summary>
        private void InitializeFromSaveData()
        {
            // Ensure SkillTreeBonusData is not null
            if (SaveManager.Instance?.LastLoadedSaveData != null)
            {
                if (SaveManager.Instance.LastLoadedSaveData.skillTreeBonus == null)
                {
                    SaveManager.Instance.LastLoadedSaveData.skillTreeBonus = new SkillTreeBonusData();
                }
            }

            // Backward compatibility: recover unspent skill points for old saves
            // This ensures that if a save was created before this system existed,
            // or if it was created with incomplete initialization,
            // the unspent points are calculated correctly based on level.
            RecoverUnspentSkillPoints();

            // Apply existing bonuses to modifier system
            ApplySkillTreeBonusesToModifiers();
        }

        /// <summary>
        /// Callback when player levels up.
        /// Increments unspentSkillPoints.
        /// </summary>
        private void OnPlayerLevelUp(int newLevel)
        {
            Account.unspentSkillPoints++;
            SaveManager.Instance.SaveAll();
        }

        /// <summary>
        /// Get the pool of valid skills (those with bonusPerPoint in dataPlayer.json).
        /// Excludes None.
        /// </summary>
        private List<SkillType> GetValidSkillPool()
        {
            var loader = BaseStatLoader.Instance;
            if (loader == null)
                return new List<SkillType>();
            var pool = new List<SkillType>();
            // Iterate all SkillType values
            foreach (SkillType skill in Enum.GetValues(typeof(SkillType)))
            {
                if (skill == SkillType.None) continue;
                var skillData = loader.GetSkillData(skill);
                if (skillData != null && skillData.bonusPerPoint > 0)
                    pool.Add(skill);
            }
            return pool;
        }

        /// <summary>
        /// Apply all allocated skill tree bonuses to the modifier system.
        /// Called on initialization and after Confirm.
        /// </summary>
        private void ApplySkillTreeBonusesToModifiers()
        {
            var modifierMgr = ModifierManager.Instance;
            if (modifierMgr == null)
            {
                // ModifierManager not ready yet, defer to next frame
                StartCoroutine(ApplySkillTreeBonusesToModifiersDeferred());
                return;
            }
            var loader = BaseStatLoader.Instance;
            if (loader == null) return;
            var data = Data;
            if (data == null) { if (_debug) Debug.Log("[SkillTree] Data null"); return; }

            // Remove all existing SkillTreeBonus modifiers first
            if (_debug) Debug.Log($"[SkillTree] allocatedSkills count: {data.allocatedSkills.Count}");
            foreach (var kvp in data.allocatedSkills)
            {
                if (!Enum.TryParse<SkillType>(kvp.Key, out var skillType))
                    continue;
                var modifierId = $"SkillTreeBonus_{kvp.Key}";
                modifierMgr.RemoveModifier(modifierId);
            }

            // Now add all current bonuses
            foreach (var kvp in data.allocatedSkills)
            {
                if (_debug) Debug.Log($"[SkillTree]   {kvp.Key} = {kvp.Value}");
                if (!Enum.TryParse<SkillType>(kvp.Key, out var skillType))
                    continue;

                var points = kvp.Value;
                if (points <= 0) continue;

                var skillData = loader.GetSkillData(skillType);
                if (skillData == null) continue;

                var totalBonus = points * skillData.bonusPerPoint;

                // Create modifier
                var modifier = new StatModifier
                {
                    Id = $"SkillTreeBonus_{kvp.Key}",
                    Source = ModifierSource.SkillTreeBonus,
                    Stat = skillType,
                    Mode = ModifierMode.Flat,
                    Value = totalBonus
                };

                modifierMgr.AddModifier(modifier);
            }
        }

        private IEnumerator ApplySkillTreeBonusesToModifiersDeferred()
        {
            yield return null; // Wait one frame for ModifierManager to initialize
            ApplySkillTreeBonusesToModifiers();
        }

        #endregion
    }
}
