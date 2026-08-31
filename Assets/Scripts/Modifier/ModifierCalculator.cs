namespace IdleDefenseSurvival.Modifier
{
    /// <summary>
    /// Pure stat calculation utility.
    /// Contains every formula used to calculate player stats.
    /// Stateless and allocation free.
    /// </summary>
    public static class ModifierCalculator
    {
        /// <summary>
        /// Final Formula
        ///
        /// (Base + Flat) * (1 + Percent / 100)
        /// </summary>
        public static float Calculate(float baseValue, float flat, float percent)
            => (baseValue + flat) * (1f + percent * 0.01f);

        /// <summary>
        /// Clamp helper.
        /// Useful for stats like Crit Chance.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Never below zero.
        /// Useful for Attack Speed, Damage, etc.
        /// </summary>
        public static float ClampMinZero(float value) => value < 0f ? 0f : value;
        
    }
}