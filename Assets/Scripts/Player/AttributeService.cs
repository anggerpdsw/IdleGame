using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival;
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
    /// Static config for the four main attributes.
    /// Base values from dataPlayer.json ("mainAttributes"); per-point bonuses from
    /// dataAttribute.json. Both are loaded and parsed ONCE (cached) — Apply() calls
    /// never re-read JSON or re-parse stat strings.
    /// </summary>
    public static class AttributeService
    {
        private const float DefaultAttribute = 5f;
        private static float _constitution = DefaultAttribute;
        private static float _strength = DefaultAttribute;
        private static float _intelligence = DefaultAttribute;
        private static float _dexterity = DefaultAttribute;

        private static AttributeBonusData[] _constitutionBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _strengthBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _intelligenceBonuses = Array.Empty<AttributeBonusData>();
        private static AttributeBonusData[] _dexterityBonuses = Array.Empty<AttributeBonusData>();

        private static bool _loaded;

        /// <summary>Load config once (reloads if <paramref name="force"/> is true).</summary>
        public static void Initialize(bool force = false)
        {
            if (_loaded && !force) return;

            LoadBaseValues();
            LoadBonuses();
            _loaded = true;
        }

        private static void LoadBaseValues()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null) return;

            try
            {
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
                if (data?.mainAttributes == null) return;

                _constitution = data.mainAttributes.constitution;
                _strength = data.mainAttributes.strength;
                _intelligence = data.mainAttributes.intelligence;
                _dexterity = data.mainAttributes.dexterity;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributeService] Failed to load mainAttributes: {e.Message}");
            }
        }

        private static void LoadBonuses()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataAttribute");
            if (jsonAsset == null) return;

            try
            {
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<AttributeBonuses>(jsonAsset.text);
                _constitutionBonuses = Parse(data?.constitution);
                _strengthBonuses = Parse(data?.strength);
                _intelligenceBonuses = Parse(data?.intelligence);
                _dexterityBonuses = Parse(data?.dexterity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributeService] Failed to load dataAttribute.json: {e.Message}");
            }
        }

        /// <summary>Parse raw JSON bonuses to SkillType once; invalid entries are dropped entirely (no empty slots).</summary>
        private static AttributeBonusData[] Parse(AttributeBonus[] raw)
        {
            if (raw == null || raw.Length == 0) return Array.Empty<AttributeBonusData>();

            var result = new List<AttributeBonusData>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == null) continue;
                if (!Enum.TryParse(raw[i].stat, true, out SkillType stat))
                {
                    Debug.LogWarning($"[AttributeService] Unknown stat \"{raw[i].stat}\" in dataAttribute.json — skipped.");
                    continue;
                }
                result.Add(new AttributeBonusData(stat, raw[i].flat, raw[i].percent));
            }
            return result.ToArray();
        }

        /// <summary>Base (level-1) value of the attribute.</summary>
        public static float GetBaseValue(MainAttribute attribute) => attribute switch
        {
            MainAttribute.Constitution => _constitution,
            MainAttribute.Strength => _strength,
            MainAttribute.Intelligence => _intelligence,
            MainAttribute.Dexterity => _dexterity,
            _ => 5f
        };

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
