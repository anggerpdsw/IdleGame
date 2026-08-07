using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Durability fraction (0..1) → color. Thresholds reuse GameColors once, at load.
    /// </summary>
    public static class DurabilityColorTable
    {
        private static readonly (float min, Color color)[] Tiers =
        {
            (0.75f, GameColors.green),
            (0.5f,  GameColors.yellow),
            (0.25f, GameColors.orange),
            (0.0f,  GameColors.red)
        };

        /// <summary>First tier with percent &gt;= min wins; falls back to red.</summary>
        public static Color GetColor(float percent)
        {
            foreach (var (min, color) in Tiers)
            {
                if (percent >= min) return color;
            }
            return GameColors.red;
        }
    }
}