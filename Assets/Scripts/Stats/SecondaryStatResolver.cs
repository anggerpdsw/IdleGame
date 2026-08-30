using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Core;
using UnityEngine;

namespace IdleDefenseSurvival.Stats
{
    public static class SecondaryStatResolver
    {
        private static HashSet<SkillType> _mainAttributeAffectedStats;
        private static SecondaryStat[] _validSecondaryStats;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                var data = DatabaseJSONCache.DatabaseSecondaryStatAttribute;
                _mainAttributeAffectedStats = new HashSet<SkillType>();
                foreach (var kvp in data)
                {
                    foreach (var mapping in kvp.Value)
                    {
                        if (Enum.TryParse<SkillType>(mapping.stat, true, out var skillType))
                        {
                            _mainAttributeAffectedStats.Add(skillType);
                        }
                    }
                }

                // All SkillType that are not affected by any MainAttribute
                // become valid candidates for SecondaryStat.
                var allSkillTypes = (SkillType[])Enum.GetValues(typeof(SkillType));
                _validSecondaryStats = allSkillTypes
                    .Where(s => s != SkillType.None && !_mainAttributeAffectedStats.Contains(s))
                    .Select(s => SecondaryStatExtensions.SkillTypeToSecondaryStat(s))
                    .Where(s => s != SecondaryStat.None)
                    .Distinct()
                    .ToArray();

                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SecondaryStatResolver] Failed to parse dataAttribute.json: {e.Message}");
            }
        }

        public static IReadOnlyList<SecondaryStat> GetValidSecondaryStats()
        {
            Initialize();
            return _validSecondaryStats ?? Array.Empty<SecondaryStat>();
        }

        public static bool IsMainAttributeAffected(SkillType skillType)
        {
            Initialize();
            return _mainAttributeAffectedStats?.Contains(skillType) ?? false;
        }

        public static bool IsValidSecondaryStat(SecondaryStat stat)
        {
            Initialize();
            return _validSecondaryStats?.Contains(stat) ?? false;
        }

    }
}
