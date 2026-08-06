using System;
using UnityEngine;

namespace IdleDefenseSurvival.Enemy.StatusEffects
{
    /// <summary>
    /// Interface for all status effects that can be applied to enemies.
    /// Provides a unified API for applying, ticking, and expiring effects.
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>Unique identifier for this status effect type.</summary>
        StatusEffectType Type { get; }

        /// <summary>Category for grouping effects.</summary>
        StatusEffectCategory Category { get; }

        /// <summary>Total duration of the effect in seconds.</summary>
        float Duration { get; set; }

        /// <summary>Current elapsed time since application.</summary>
        float ElapsedTime { get; set; }

        /// <summary>Whether the effect has expired.</summary>
        bool IsExpired { get; }

        /// <summary>Current stack count (for stackable effects).</summary>
        int StackCount { get; }

        /// <summary>Maximum allowed stacks (0 = unlimited).</summary>
        int MaxStacks { get; }

        /// <summary>Stacking behavior for this effect.</summary>
        StackPolicy StackPolicy { get; }

        /// <summary>
        /// Called when the effect is first applied to an enemy.
        /// Use for one-time setup (storing original values, applying visual changes, etc.).
        /// </summary>
        void OnApply(EnemyAi enemy);

        /// <summary>
        /// Called every frame while the effect is active.
        /// Implement damage over time, visual updates, etc. here.
        /// </summary>
        void Tick(EnemyAi enemy, float deltaTime);

        /// <summary>
        /// Called when the effect expires or is removed.
        /// Use for cleanup (restoring original values, removing visuals, etc.).
        /// </summary>
        void OnExpire(EnemyAi enemy);

        /// <summary>
        /// Called when a new stack of the same effect is applied.
        /// Implement stack-specific logic here (refresh duration, increase damage, etc.).
        /// </summary>
        void OnStackAdded(EnemyAi enemy, int newStackCount);

        /// <summary>
        /// Gets the current effective value of the effect (e.g., DPS for burn).
        /// </summary>
        float GetCurrentValue();

        /// <summary>
        /// Creates a copy of this effect for application to another enemy.
        /// </summary>
        IStatusEffect Clone();
    }

    /// <summary>
    /// Types of status effects available in the game.
    /// </summary>
    public enum StatusEffectType
    {
        None = 0,
        Burn = 1,
        Poison = 2,
        Freeze = 3,
        Bleed = 4,
        Shock = 5,
        Slow = 6,
        Curse = 7,
        Fear = 8,
        Stun = 9,
        Silence = 10,
        Root = 11,
        Knockback = 12,
        Taunt = 13,
        Charm = 14,
        Confusion = 15,
        Weaken = 16,
        Vulnerable = 17,
        ArmorBreak = 18,
        MagicResistBreak = 19,
        HealingReduction = 20,
        DamageReduction = 21,
        CriticalVulnerability = 22,
        EvasionDown = 23,
        AttackSpeedDown = 24,
        MoveSpeedDown = 25,
        CooldownIncrease = 26,
        ManaBurn = 27,
        LifeStealReduction = 28,
        ShieldBreak = 29,
        Invulnerability = 30,
        Stealth = 31,
        TauntImmune = 32,
        ControlImmune = 33,
        DamageOverTimeImmune = 34,
    }

    /// <summary>
    /// Stacking policies for status effects.
    /// </summary>
    public enum StackPolicy
    {
        /// <summary>Effect cannot stack. New application refreshes duration.</summary>
        RefreshDuration = 0,

        /// <summary>Effect stacks additively. Each application adds a stack.</summary>
        Additive = 1,

        /// <summary>Effect takes the maximum value. New application updates if stronger.</summary>
        MaximumValue = 2,

        /// <summary>Effect replaces existing one entirely (new duration, new value).</summary>
        Replace = 3,

        /// <summary>Effect stacks multiplicatively (e.g., 0.5 slow * 0.5 slow = 0.25 speed).</summary>
        Multiplicative = 4,

        /// <summary>Custom stacking logic implemented in OnStackAdded.</summary>
        Custom = 5,
    }

    /// <summary>
    /// Categories for grouping status effects.
    /// </summary>
    public enum StatusEffectCategory
    {
        DamageOverTime = 0,     // Burn, Poison, Bleed
        CrowdControl = 1,       // Freeze, Stun, Root, Fear, Silence, Charm, Confusion
        MovementImpairment = 2, // Slow, Root
        StatDebuff = 3,         // Weaken, Vulnerable, ArmorBreak, etc.
        Utility = 4,            // Knockback, Taunt
        Immunity = 5,           // Invulnerability, ControlImmune, etc.
        Special = 6,            // Stealth, etc.
    }
}