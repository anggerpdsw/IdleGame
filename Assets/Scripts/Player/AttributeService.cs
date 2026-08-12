using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Per-point bonus of one attribute, already parsed to SkillType.
    /// </summary>
    public readonly struct AttributeBonusData
    {
        public readonly SkillType Stat;
        public readonly float Flat;
        public readonly float Percent;

        public AttributeBonusData(SkillType stat, float flat, float percent)
        {
            Stat = stat;
            Flat = flat;
            Percent = percent;
        }
    }

    /// <summary>
    /// Static config for the four main attributes' per-point bonuses.
    /// Loaded and parsed ONCE from dataAttribute.json (cached) — Apply() calls
    /// never re-read JSON or re-parse stat strings.
    /// </summary>
    public static class AttributeService
    {
        private static AttributeBonusData[] _constitutionBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _strengthBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _intelligenceBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _dexterityBonuses = Array.Empty<AttributeBonusData>();

        private static bool _loaded;

        /// <summary>Load config once (reloads if <paramref name="force"/> is true).</summary>
        public static void Initialize(bool force = false)
        {
            if (_loaded && !force) return;

            LoadBonuses();
            _loaded = true;
        }

        private static void LoadBonuses()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/Player/dataAttribute");
            if (jsonAsset == null)
            {
                Debug.LogWarning("[AttributeService] dataAttribute.json not found.");
                return;
            }

            try
            {
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<AttributeConfig>(jsonAsset.text);
                if (data == null) return;

                _constitutionBonuses = Parse(data.constitution);
                _strengthBonuses = Parse(data.strength);
                _intelligenceBonuses = Parse(data.intelligence);
                _dexterityBonuses = Parse(data.dexterity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributeService] Failed to parse dataAttribute.json: {e.Message}");
            }
        }

        private static AttributeBonusData[] Parse(AttributeBonusEntry[] entries)
        {
            if (entries == null) return Array.Empty<AttributeBonusData>();

            var list = new List<AttributeBonusData>(entries.Length);
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (!Enum.TryParse(entry.stat, out SkillType stat)) continue; // Unknown stat — skip.
                list.Add(new AttributeBonusData(stat, entry.flat, entry.percent));
            }
            return list.ToArray();
        }

        /// <summary>Cached per-point bonuses for the attribute (already parsed).</summary>
        public static AttributeBonusData[] GetBonuses(MainAttribute attribute) => attribute switch
        {
            MainAttribute.Constitution => _constitutionBonuses,
            MainAttribute.Strength => _strengthBonuses,
            MainAttribute.Intelligence => _intelligenceBonuses,
            MainAttribute.Dexterity => _dexterityBonuses,
            _ => Array.Empty<AttributeBonusData>()
        };
    }
}
