using System;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Secondary stat modes - defines how a stat modifier is applied.
    /// Allows complex stat calculations beyond simple Flat/Percent.
    /// </summary>
    public enum SecondaryStatMode
    {
        None = 0,
        Flat = 1,              // Simple addition: base + value
        Percent = 2,           // Percentage multiplier: base * (1 + value/100)
        Multiplier = 3,        // Direct multiplier: base * value
        Additive = 4,          // Additive with other sources before multiplication
        Multiplicative = 5,    // Multiplicative with other sources
        Override = 6,          // Overrides base value entirely
        Conditional = 7,       // Applied only when conditions are met
    }

    /// <summary>
    /// Extension methods for SecondaryStatMode.
    /// </summary>
    public static class SecondaryStatModeExtensions
    {
        /// <summary>
        /// Gets the display name for the mode.
        /// </summary>
        public static string GetDisplayName(this SecondaryStatMode mode) => mode switch
        {
            SecondaryStatMode.Flat => "Flat",
            SecondaryStatMode.Percent => "Percent",
            SecondaryStatMode.Multiplier => "Multiplier",
            SecondaryStatMode.Additive => "Additive",
            SecondaryStatMode.Multiplicative => "Multiplicative",
            SecondaryStatMode.Override => "Override",
            SecondaryStatMode.Conditional => "Conditional",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short symbol for UI display.
        /// </summary>
        public static string GetSymbol(this SecondaryStatMode mode) => mode switch
        {
            SecondaryStatMode.Flat => "+",
            SecondaryStatMode.Percent => "+%",
            SecondaryStatMode.Multiplier => "×",
            SecondaryStatMode.Additive => "+Σ",
            SecondaryStatMode.Multiplicative => "×Π",
            SecondaryStatMode.Override => "=",
            SecondaryStatMode.Conditional => "?",
            _ => ""
        };

        /// <summary>
        /// Calculates the final value based on base value and this mode.
        /// </summary>
        public static float Calculate(this SecondaryStatMode mode, float baseValue, float modifierValue) => mode switch
        {
            SecondaryStatMode.Flat => baseValue + modifierValue,
            SecondaryStatMode.Percent => baseValue * (1f + modifierValue * 0.01f),
            SecondaryStatMode.Multiplier => baseValue * modifierValue,
            SecondaryStatMode.Additive => baseValue + modifierValue, // Combined additively with other additives first
            SecondaryStatMode.Multiplicative => baseValue * modifierValue, // Combined multiplicatively with other multiplicatives first
            SecondaryStatMode.Override => modifierValue,
            SecondaryStatMode.Conditional => baseValue, // Requires external condition check
            _ => baseValue
        };

        /// <summary>
        /// Checks if the mode is valid (not None).
        /// </summary>
        public static bool IsValid(this SecondaryStatMode mode) => mode != SecondaryStatMode.None;

        /// <summary>
        /// Gets all valid modes (excludes None).
        /// </summary>
        public static SecondaryStatMode[] GetAllModes() =>
            (SecondaryStatMode[])Enum.GetValues(typeof(SecondaryStatMode));
    }
}