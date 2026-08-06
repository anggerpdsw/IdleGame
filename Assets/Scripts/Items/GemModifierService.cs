using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem modifier service contract - apply/remove gem stat modifiers.
    /// Keeps GemSocketService free of ModifierManager/StatModifier details.
    /// </summary>
    public interface IGemModifierService
    {
        void Apply(InventoryItem item, int socketIndex, GemInstanceData gemInstance);
        void Remove(InventoryItem item, int socketIndex, GemInstanceData gemInstance);
        void RemoveAll(InventoryItem item);
    }

    /// <summary>
    /// Gem Modifier Service - handles applying/removing gem modifiers to ModifierManager.
    /// Modifier IDs are keyed by gem instance id, so they survive socket swaps.
    /// </summary>
    public sealed class GemModifierService : MonoBehaviour, IGemModifierService
    {
        #region Singleton
        private static GemModifierService _instance;
        public static GemModifierService Instance => _instance;

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

        #region Public API
        /// <summary>
        /// Applies (replaces) gem modifiers from ModifierManager.
        /// Idempotent: same id, so re-apply simply overwrites.
        /// </summary>
        public void Apply(InventoryItem item, int socketIndex, GemInstanceData gemInstance)
        {
            if (gemInstance?.Stats == null) return;

            string prefix = $"Gem:{gemInstance.InstanceId}";

            foreach (var statEntry in gemInstance.Stats)
            {
                float value = statEntry.GetValue(gemInstance.Level, 0);
                if (value == 0) continue;

                // Stat (SkillType) is the ModifierManager lookup key; missing it meant
                // gem modifiers were never applied. MainStat kept for UI/display.
                var modifier = new StatModifier
                {
                    Id = $"{prefix}_{statEntry.Stat}",
                    Source = ModifierSource.Equipment,
                    Stat = statEntry.Stat.ToSkillType(),
                    MainStat = statEntry.Stat,
                    Mode = (ModifierMode)statEntry.Mode,
                    Value = value,
                    Permanent = true
                };

                ModifierManager.Instance?.AddModifier(modifier);
            }
        }

        /// <summary>
        /// Removes gem modifiers for one socketed gem.
        /// </summary>
        public void Remove(InventoryItem item, int socketIndex, GemInstanceData gemInstance)
        {
            if (gemInstance?.Stats == null) return;

            string prefix = $"Gem:{gemInstance.InstanceId}";

            foreach (var statEntry in gemInstance.Stats)
            {
                ModifierManager.Instance?.RemoveModifier($"{prefix}_{statEntry.Stat}");
            }
        }

        /// <summary>
        /// Removes all gem modifiers for an item.
        /// </summary>
        public void RemoveAll(InventoryItem item)
        {
            if (item?.Sockets == null) return;

            for (int i = 0; i < item.Sockets.Length; i++)
            {
                var socket = item.Sockets[i];
                if (socket.IsFilled && !string.IsNullOrEmpty(socket.GemInstanceId))
                {
                    string prefix = $"Gem:{socket.GemInstanceId}";
                    var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
                    if (gemData?.BaseStats != null)
                    {
                        foreach (var statEntry in gemData.BaseStats)
                        {
                            ModifierManager.Instance?.RemoveModifier($"{prefix}_{statEntry.Stat}");
                        }
                    }
                }
            }
        }
        #endregion
    }
}
