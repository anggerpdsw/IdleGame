using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Validates craft results after pipeline execution.
    /// Ensures quality bounds, count bounds, valid item IDs, etc.
    /// </summary>
    public sealed class CraftResultValidator
    {
        private readonly IItemDatabase _itemDatabase;
        private readonly CraftFormulasConfig _config;

        public CraftResultValidator(IItemDatabase itemDatabase = null, CraftFormulasConfig config = null)
        {
            _itemDatabase = itemDatabase;
            _config = config ?? new CraftFormulasConfig();
        }

        /// <summary>
        /// Validates all entries and returns only valid ones.
        /// </summary>
        public List<CraftResultEntry> ValidateAndFilter(List<CraftResultEntry> entries, out List<string> errors)
        {
            errors = new List<string>();
            var validEntries = new List<CraftResultEntry>();

            foreach (var entry in entries)
            {
                var entryErrors = ValidateEntry(entry);
                if (entryErrors.Count == 0)
                {
                    // Auto-clamp valid values
                    ClampEntry(entry);
                    validEntries.Add(entry);
                }
                else
                {
                    errors.AddRange(entryErrors);
                    Debug.LogWarning($"[CraftResultValidator] Filtered invalid entry: {entry.ItemId} - {string.Join(", ", entryErrors)}");
                }
            }

            return validEntries;
        }

        /// <summary>
        /// Validates a single entry, returns list of error messages.
        /// </summary>
        public List<string> ValidateEntry(CraftResultEntry entry)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(entry.ItemId))
            {
                errors.Add("ItemId is empty");
            }

            if (entry.Count <= 0)
            {
                errors.Add($"Count must be positive, got {entry.Count}");
            }

            if (entry.Quality < 0)
            {
                errors.Add($"Quality cannot be negative, got {entry.Quality}");
            }

            if (entry.Quality > _config.MaxQualityTier)
            {
                errors.Add($"Quality {entry.Quality} exceeds max tier {_config.MaxQualityTier}");
            }

            if (_itemDatabase != null && !_itemDatabase.IsValidItemId(entry.ItemId))
            {
                errors.Add($"ItemId '{entry.ItemId}' not found in database");
            }

            if (entry.FixedLevel < 0)
            {
                errors.Add($"FixedLevel cannot be negative");
            }

            if (entry.SocketCount < 0)
            {
                errors.Add($"SocketCount cannot be negative");
            }

            return errors;
        }

        /// <summary>
        /// Clamps entry values to valid ranges.
        /// </summary>
        public void ClampEntry(CraftResultEntry entry)
        {
            entry.Quality = Mathf.Clamp(entry.Quality, 0, _config.MaxQualityTier);
            entry.Count = Mathf.Max(1, entry.Count);
            entry.FixedLevel = Mathf.Max(0, entry.FixedLevel);
            entry.SocketCount = Mathf.Max(0, entry.SocketCount);
        }

        /// <summary>
        /// Checks if there are any valid results.
        /// </summary>
        public bool HasValidResults(List<CraftResultEntry> entries)
        {
            return entries != null && entries.Count > 0;
        }
    }

}