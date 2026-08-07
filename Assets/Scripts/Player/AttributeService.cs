using System;
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
            // Hardcoded per-point bonuses per MainAttribute as per redesign.
            // Scaling values correspond to flat additions for each SkillType.
            _constitutionBonuses = new AttributeBonusData[]
            {
                new(SkillType.HealthPoint, 40f, 0f),
                new(SkillType.DefenseAmount, 1f, 0f),
                new(SkillType.HealthRegen, 0.1f, 0f),
                new(SkillType.DeathDefy, 0.0025f, 0f)
            };
            _strengthBonuses = new AttributeBonusData[]
            {
                new(SkillType.AttackDamage, 5f, 0f),
                new(SkillType.KnockbackForce, 0.2f, 0f),
                new(SkillType.UltimateAttack, 2f, 0f)
            };
            _intelligenceBonuses = new AttributeBonusData[]
            {
                new(SkillType.SkillDamage, 3f, 0f),
                new(SkillType.ElementDamage, 3f, 0f),
                new(SkillType.UltimateAttack, 2f, 0f)
            };
            _dexterityBonuses = new AttributeBonusData[]
            {
                new(SkillType.AttackSpeed, 0.4f, 0f),
                new(SkillType.CriticalChance, 0.15f, 0f),
                new(SkillType.CriticalDamage, 0.3f, 0f),
                new(SkillType.Evasion, 0.15f, 0f)
            };
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
