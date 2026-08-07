using UnityEngine;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Data structure for displaying damage popups.
    /// Separates presentation logic from damage calculation logic (DamageInfo).
    /// </summary>
    public struct DamagePopupData
    {
        /// <summary>The damage value to display.</summary>
        public float Damage;

        /// <summary>Type of damage affecting color and visual style.</summary>
        public DamageType Type;

        /// <summary>Whether this is a critical hit (affects size and scale).</summary>
        public CriticalType Critical;

        /// <summary>Override color for the popup text. If not set, uses type-based color.</summary>
        public Color? OverrideColor;

        /// <summary>Optional prefix text (e.g., "+", "-", "Miss").</summary>
        public string Prefix;

        /// <summary>Duration the popup stays visible (in seconds).</summary>
        public float Duration;

        public DamagePopupData(float damage, DamageType type = DamageType.Normal, CriticalType crit = CriticalType.None, string prefix = "")
        {
            Damage = damage;
            Type = type;
            Critical = crit;
            Prefix = prefix;
            OverrideColor = null;
            Duration = 1.5f; // Default duration
        }

        /// <summary>Get color based on damage type. Used if OverrideColor is not set.</summary>
        public Color GetTypeColor()
        {
            // Jika Critical, gunakan warna khusus berdasarkan tier
            if (Critical != CriticalType.None)
            {
                return Critical switch
                {
                    CriticalType.SuperCritical => GameColors.orangered,
                    CriticalType.UltraCritical => GameColors.pink,
                    _ => GameColors.gold
                };
            }

            return Type switch
            {
                DamageType.Normal     => GameColors.red,
                DamageType.Heal       => GameColors.green,
                DamageType.Mana       => GameColors.blue,
                DamageType.Poison     => GameColors.purple,
                DamageType.Burn       => GameColors.orange,
                DamageType.Ice        => GameColors.cyan,
                DamageType.TrueDamage => GameColors.darkgray,
                DamageType.Miss       => GameColors.gray,
                _                     => GameColors.white
            };
        }

        /// <summary>Get scale multiplier based on damage type and criticality.</summary>
        public float GetScale()
        {
            float baseScale = 48f;

            if (Critical != CriticalType.None)
            {
                baseScale *= Critical switch
                {
                    CriticalType.Critical      => 1.15f,
                    CriticalType.SuperCritical => 1.33f,
                    CriticalType.UltraCritical => 1.47f,
                    _ => 1.03f
                };
            }

            return Type switch
            {
                DamageType.Heal => baseScale * 1.07f,
                DamageType.Mana => baseScale * 1.07f,
                _ => baseScale
            };
        }

        /// <summary>Format damage display string.</summary>
        public string GetDisplayText()
        {
            string text = Prefix + (Damage > 1f ? Damage.ToString("F0") : "");

            text += Type switch
            {
                DamageType.Miss => Prefix != "" ? "" : "Miss",
                _   => ""
            };

            if (Critical != CriticalType.None)
            {
                text += Critical switch
                {
                    CriticalType.Critical      => "◆",
                    CriticalType.SuperCritical => "★",
                    CriticalType.UltraCritical => "⚔",
                    _ => ""
                };
            }


            return text;
        }
    }
}
