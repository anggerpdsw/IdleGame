using System;
using System.Collections.Generic;
using IdleDefenseSurvival;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Per-build attribute weights for auto-equip scoring. Crit 1: "all attributes
    /// equal value" — CON=STR=INT=DEX=1 was flat. Builds map each main attribute to a
    /// derived power scalar, so a build that speaks DEX weights DEX gear highest.
    /// </summary>
    [Serializable]
    public class AttributeWeightsConfig
    {
        public BuildProfile BuildProfile = BuildProfile.All;
        public Dictionary<MainAttribute, float> Weights = new()
        {
            { MainAttribute.Strength, 1f },
            { MainAttribute.Constitution, 1f },
            { MainAttribute.Intelligence, 1f },
            { MainAttribute.Dexterity, 1f }
        };

        /// <summary>
        /// Build profile -> per-attribute weight. Focus profile: primary ×3, others ×0.5.
        /// BuildProfile.All = flat equivalence (all ×1) — the historical default.
        /// </summary>
        public static AttributeWeightsConfig ForBuild(BuildProfile profile)
        {
            var config = new AttributeWeightsConfig { BuildProfile = profile };
            if (profile == BuildProfile.All) return config;

            config.Weights[PrimaryAttribute(profile)] = 3f;
            foreach (var other in new[] { MainAttribute.Constitution, MainAttribute.Strength, MainAttribute.Intelligence, MainAttribute.Dexterity })
                if (other != PrimaryAttribute(profile)) config.Weights[other] = 0.5f;
            return config;
        }

        /// <summary>The attribute a build focuses on. All = 1f each, fallback STR.</summary>
        private static MainAttribute PrimaryAttribute(BuildProfile profile) => profile switch
        {
            BuildProfile.Tank => MainAttribute.Constitution,
            BuildProfile.Warrior => MainAttribute.Strength,
            BuildProfile.Mage => MainAttribute.Intelligence,
            BuildProfile.Assassin => MainAttribute.Dexterity,
            _ => MainAttribute.Strength,
        };

        /// <summary>Weight for one main attribute; defaults 1 if unset.</summary>
        public float WeightFor(MainAttribute attr) =>
            Weights != null && Weights.TryGetValue(attr, out var w) ? w : 1f;
    }
}
