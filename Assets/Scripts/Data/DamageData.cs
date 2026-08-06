using IdleDefenseSurvival;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Carries all damage-related information from source to target.
    /// Scalable struct designed to easily accommodate new properties like armor penetration,
    /// life steal, status effects, etc., without changing method signatures.
    /// </summary>
    public struct DamageData
    {
        /// <summary>The source of the damage (e.g., "Player", "Enemy_Boss").</summary>
        public string Source;

        /// <summary>The raw damage value before any calculations.</summary>
        public float Damage;
        public Element Element;

        /// <summary>Type of damage (Normal, Critical, Poison, etc.).</summary>
        public DamageType Type;

        /// <summary>Whether this hit is a critical strike.</summary>
        public CriticalType Critical;

        /// <summary>Optional: multiplier applied to the damage value (for crits, status effects, etc.).</summary>
        public float DamageMultiplier;

        public bool HasKnockback;
        public float KnockbackForce;
        public bool HasStunt;
        public bool HasBounce;
        public float SlowPercent;
        public float StuntMultiplier;
        public float DefenseBreak;

        public DamageData(float damage, DamageType type = DamageType.Normal, CriticalType crit = CriticalType.None, string source = "Unknown")
        {
            Source  = source;
            Damage  = damage;
            Element = Element.None;
            Type = type;
            Critical = crit;
            DamageMultiplier = 1f;
            HasKnockback = false;
            KnockbackForce = 0f;
            HasStunt = false;
            HasBounce = false;
            SlowPercent = 0f;
            StuntMultiplier = 1f;
            DefenseBreak = 0f;
        }

        /// <summary>Calculate final damage after applying multipliers.</summary>
        public float GetFinalDamage(float elementMultiplier = 1f)
        {
            return Damage * DamageMultiplier * elementMultiplier;
        }

        /// <summary>
        /// True when the damage source is the player's basic auto-attack.
        /// (Ultimates and tanks use UltimateDMG.* names — they are skills.)
        /// </summary>
        public static bool IsBasicAttack(string source)
            => source == UltimateDMG.Player.ToString();
    }
}
