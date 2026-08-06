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
                    CriticalType.SuperCritical => new Color32(255, 69, 0, 255),  // Orange-Red untuk Super
                    CriticalType.UltraCritical => new Color32(255, 20, 147, 255), // Deep Pink untuk Ultra
                    _ => new Color32(255, 215, 0, 255) // Gold untuk Critical biasa
                };
            }

            return Type switch
            {
                DamageType.Normal     => new Color32(220, 38, 38, 255),   // Darker Red
                DamageType.Heal       => new Color32(22, 163, 74, 255),   // Dark Green
                DamageType.Poison     => new Color32(147, 51, 234, 255),  // Purple
                DamageType.Burn       => new Color32(234, 88, 12, 255),   // Orange
                DamageType.Ice        => new Color32(6, 182, 212, 255),   // Cyan
                DamageType.TrueDamage => new Color32(30, 30, 30, 255),    // Dark Gray
                DamageType.Miss       => new Color32(107, 114, 128, 255), // Gray
                _                     => new Color32(255, 255, 255, 255)
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
