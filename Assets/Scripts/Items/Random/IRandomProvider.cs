using System;

namespace IdleDefenseSurvival.Items.Random
{
    /// <summary>
    /// Abstraction for random number generation.
    /// Allows deterministic replay, testing, and different RNG strategies.
    /// </summary>
    public interface IRandomProvider
    {
        int NextInt(int minInclusive, int maxExclusive);
        int NextInt(int maxExclusive);
        float NextFloat();
        double NextDouble();
        bool Chance(float probability); // 0-1
        bool ChancePercent(float percent); // 0-100
        float Range(float min, float max);
        int Range(int minInclusive, int maxExclusive);
        T Choice<T>(T[] array);
        T Choice<T>(System.Collections.Generic.IReadOnlyList<T> list);
        void Shuffle<T>(T[] array);
        void Shuffle<T>(System.Collections.Generic.IList<T> list);
    }

    /// <summary>
    /// Extensions for common random operations.
    /// </summary>
    public static class RandomProviderExtensions
    {
        public static float NextFloat(this IRandomProvider rng, float max) => rng.NextFloat() * max;
        public static int NextInt(this IRandomProvider rng) => rng.NextInt(int.MaxValue);
        public static T Choice<T>(this IRandomProvider rng, params T[] items) => rng.Choice(items);
        public static bool Roll(this IRandomProvider rng, float chance) => rng.Chance(chance);
        public static bool RollPercent(this IRandomProvider rng, float percent) => rng.ChancePercent(percent);
        public static float NextGaussian(this IRandomProvider rng, float mean = 0f, float stdDev = 1f)
        {
            // Box-Muller transform
            float u1 = rng.NextFloat();
            float u2 = rng.NextFloat();
            float z = (float)Math.Sqrt(-2f * Math.Log(u1)) * (float)Math.Cos(2f * Math.PI * u2);
            return mean + z * stdDev;
        }
    }
}