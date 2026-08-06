using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Virtual inventory snapshot for batch roll processing.
    /// Allows CardRollService to track inventory state (levels, duplicates)
    /// during a multi-roll without mutating real CardInventory.
    /// Pure data - no Unity dependencies, easily testable.
    /// </summary>
    public sealed class VirtualCardInventorySnapshot
    {
        // cardId -> (level, duplicateCount)
        private readonly Dictionary<string, (int Level, int DuplicateCount)> _cards = new();

        // Duplicate requirements per level (copied from CardUpgradeService for pure function)
        private static readonly int[] DuplicateRequirements =
        {
            2,  // Lv 1 -> Lv 2
            4,  // Lv 2 -> Lv 3
            7,  // Lv 3 -> Lv 4
            11, // Lv 4 -> Lv 5
            19, // Lv 5 -> Lv 6
            31, // Lv 6 -> Lv 7
            47, // Lv 7 -> Lv 8
            69, // Lv 8 -> Lv 9
            99  // Lv 9 -> Lv 10
        };

        /// <summary>
        /// Creates a snapshot from real CardInventory.
        /// </summary>
        public static VirtualCardInventorySnapshot FromInventory(CardInventory inventory)
        {
            var snapshot = new VirtualCardInventorySnapshot();
            if (inventory != null)
            {
                foreach (var kvp in inventory.AllOwned)
                {
                    var owned = kvp.Value;
                    snapshot._cards[kvp.Key] = (owned.Level, owned.DuplicateCount);
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Checks if a card is owned (level > 0).
        /// </summary>
        public bool HasCard(string cardId)
        {
            return _cards.ContainsKey(cardId) && _cards[cardId].Level > 0;
        }

        /// <summary>
        /// Gets the level of a card (0 if not owned).
        /// </summary>
        public int GetLevel(string cardId)
        {
            return _cards.TryGetValue(cardId, out var data) ? data.Level : 0;
        }

        /// <summary>
        /// Gets the duplicate count of a card.
        /// </summary>
        public int GetDuplicateCount(string cardId)
        {
            return _cards.TryGetValue(cardId, out var data) ? data.DuplicateCount : 0;
        }

        /// <summary>
        /// Simulates acquiring a new card or duplicate.
        /// Immediately processes auto-upgrades (consumes duplicates).
        /// Returns (isNewCard, isDuplicate, newLevel, newDuplicateCount, excessCopiesRefunded).
        /// </summary>
        public (bool IsNewCard, bool IsDuplicate, int NewLevel, int NewDuplicateCount, int ExcessCopiesRefunded) SimulateAcquire(string cardId, int maxLevel)
        {
            if (!_cards.TryGetValue(cardId, out var data))
            {
                // Brand new card - starts at level 1, 0 duplicates
                _cards[cardId] = (1, 0);
                return (true, false, 1, 0, 0);
            }

            // Already owned - check if at max level
            if (data.Level >= maxLevel)
            {
                // At max level: excess copies are "refunded" (not accumulated)
                return (false, true, data.Level, data.DuplicateCount, 1);
            }

            // Not at max level: add duplicate, then process auto-upgrades
            int newDupCount = data.DuplicateCount + 1;
            int newLevel = data.Level;

            // Process auto-upgrades: consume duplicates while we have enough
            while (newLevel < maxLevel)
            {
                int required = GetRequiredDuplicates(newLevel);
                if (newDupCount < required) break;

                newDupCount -= required;
                newLevel++;
            }

            _cards[cardId] = (newLevel, newDupCount);
            return (false, true, newLevel, newDupCount, 0);
        }

        /// <summary>
        /// Gets required duplicates for next level (pure function, mirrors CardUpgradeService).
        /// </summary>
        private static int GetRequiredDuplicates(int currentLevel)
        {
            if (currentLevel < 1 || currentLevel >= GameConstants.CARD_MAX_LEVEL)
                return 0;
            return DuplicateRequirements[currentLevel - 1];
        }

        /// <summary>
        /// Gets all owned card IDs for iteration.
        /// </summary>
        public IEnumerable<string> OwnedCardIds => _cards.Keys;

        /// <summary>
        /// Applies this virtual state to the real CardInventory.
        /// Called by CardManager after Roll() completes.
        /// </summary>
        public void ApplyToInventory(CardInventory inventory)
        {
            if (inventory == null) return;

            foreach (var kvp in _cards)
            {
                var cardId = kvp.Key;
                var (level, dupCount) = kvp.Value;

                if (inventory.HasCard(cardId))
                {
                    var owned = inventory.GetOwnedCard(cardId);
                    if (owned != null)
                    {
                        owned.Level = level;
                        owned.DuplicateCount = dupCount;
                    }
                }
                else if (level > 0)
                {
                    // New card - add to inventory
                    inventory.AddNewCard(cardId);
                    var owned = inventory.GetOwnedCard(cardId);
                    if (owned != null)
                    {
                        owned.Level = level;
                        owned.DuplicateCount = dupCount;
                    }
                }
            }
        }
    }
}