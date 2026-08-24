using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Single data structure for all ultimate abilities.
    /// All ultimates share these fields; ultimates that don't use a field simply ignore it.
    /// </summary>
    [Serializable]
    public class UltimateData
    {
        /// <summary>Unique identifier (e.g. "bomb", "tank", "shockwave").</summary>
        public string id;
        /// <summary>Need to be active before can use.</summary>
        public bool active;
        /// <summary>Need mana to cast an ultimate.</summary>
        public float manaCost;
        /// <summary>Probability (0-100) to trigger the ultimate per attack / per check.</summary>
        public float chance;
        public int triggerKillCount;
        /// <summary>Maximum number of active instances / stacks of this ultimate.</summary>
        public int count;
        /// <summary>Time between activations (for ultimates that use a cooldown like Shockwave).</summary>
        public float cooldown;
        /// <summary>Lifetime of the ultimate in seconds.</summary>
        public float duration;
        /// <summary>Damage multiplier applied on top of player damage.</summary>
        public float damageMultiplier;
        public float knockbackMultiplier;
        public float slowPercent;
        public float stuntMultiplier;
        public float defenseBreak;
        public float healthBreak;
        public Element element;

        // ---- Safe accessors (with defaults) ----
        // Centralises default values so callers don't have to pick their own
        // fallback when an ultimate entry is missing or has 0 for a field it
        // doesn't use.

        public bool GetActive() => active;
        public float GetChance() => chance;
        public int GetTriggerKillCount(int fallback = 20) => triggerKillCount > 0 ? triggerKillCount : fallback;
        public int GetCount(int fallback = 1) => count > 0 ? count : fallback;
        public float GetCooldown(float fallback = 1f) => cooldown > 0f ? cooldown : fallback;
        public float GetDuration(float fallback = 0f) => duration > 0f ? duration : fallback;
        public float GetDamageMultiplier(float fallback = 1f) => damageMultiplier > 0f ? damageMultiplier : fallback;
        public float GetKnockbackMultiplier(float fallback = 2f) => knockbackMultiplier > 0f ? knockbackMultiplier : fallback;
        public float GetSlowPercent(float fallback = 0f) => slowPercent > 0f ? slowPercent * 0.01f : fallback;
        public float GetStuntMultiplier(float fallback = 1f) => stuntMultiplier > 1f ? stuntMultiplier : fallback;
        public float GetDefenseBreak(float fallback = 0f) => defenseBreak > 0f ? defenseBreak : fallback;
        public float GetHealthBreak(float fallback = 0f) => healthBreak > 0f ? healthBreak : fallback;
        public Element GetElement(Element fallback = Element.None) => element == Element.None ? fallback : element;

        /// <summary>
        /// Whether this ultimate uses a stack system (chance-based or kill-count based).
        /// </summary>
        public bool UsesStackSystem => chance > 0f || triggerKillCount > 0;
    }

    [Serializable]
    public class UltimateWrapper
    {
        public List<UltimateData> ultimate;
    }
}
