using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem Experience Service - handles gem experience and level-up logic.
    /// </summary>
    public sealed class GemExperienceService : MonoBehaviour
    {
        #region Singleton
        private static GemExperienceService _instance;
        public static GemExperienceService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        #endregion

        #region Events
        public event Action<string, int> OnGemExperienceChanged; // gemId, newExp
        #endregion

        #region Public API
        /// <summary>
        /// Adds experience to a socketed gem.
        /// </summary>
        public bool AddGemExperience(InventoryItem item, int socketIndex, int experience, GemInstanceData gemInstance)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length || experience <= 0) return false;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return false;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            if (gemData == null) return false;

            if (gemInstance == null) return false;

            int oldLevel = gemInstance.Level;
            gemInstance.Experience += experience;

            // Check for level up
            while (gemInstance.Level < gemData.MaxLevel)
            {
                int requiredExp = gemData.GetExperienceForLevel(gemInstance.Level + 1);
                if (gemInstance.Experience >= requiredExp)
                {
                    gemInstance.Experience -= requiredExp;
                    gemInstance.Level++;
                }
                else break;
            }

            if (gemInstance.Level != oldLevel)
            {
                gemInstance.Stats = GemStatService.Instance.GenerateGemStats(gemData, gemInstance.Level);
                socket.GemLevel = gemInstance.Level;

                // Re-apply modifiers (idempotent - replaces same ids)
                GemModifierService.Instance.Apply(item, socketIndex, gemInstance);

                OnGemExperienceChanged?.Invoke(socket.GemId, gemInstance.Experience);
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

                // Mark item dirty for UI update
                InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket | DirtyType.Upgrade);
            }
            else
            {
                OnGemExperienceChanged?.Invoke(socket.GemId, gemInstance.Experience);
            }

            return true;
        }

        /// <summary>
        /// Gets the experience required for next level.
        /// </summary>
        public int GetExperienceForNextLevel(InventoryItem item, int socketIndex)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return 0;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return 0;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            if (gemData == null) return 0;

            return gemData.GetExperienceForLevel(socket.GemLevel + 1);
        }

        /// <summary>
        /// Gets the current experience of a socketed gem.
        /// </summary>
        public int GetCurrentExperience(GemInstanceData gemInstance)
        {
            return gemInstance?.Experience ?? 0;
        }

        /// <summary>
        /// Gets the experience required for a specific level.
        /// </summary>
        public int GetExperienceForLevel(string gemId, int level)
        {
            var gemData = ItemDatabase.Instance?.GetGem(gemId);
            if (gemData == null) return 0;
            return gemData.GetExperienceForLevel(level);
        }
        #endregion
    }
}