using System;

namespace IdleDefenseSurvival.Items.Random
{
    /// <summary>
    /// Deterministic random provider using System.Random with a seed.
    /// Used for replay, save/load consistency, and testing.
    /// </summary>
    public sealed class SeedRandomProvider : IRandomProvider
    {
        private readonly System.Random _rng;

        public SeedRandomProvider(int seed)
        {
            _rng = new System.Random(seed);
        }

        public SeedRandomProvider(uint seed)
        {
            _rng = new System.Random(unchecked((int)seed));
        }

        public SeedRandomProvider(string seedString)
        {
            unchecked
            {
                int hash = 0;
                foreach (char c in seedString)
                {
                    hash = (hash * 31) + c;
                }
                _rng = new System.Random(hash);
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _rng.Next(minInclusive, maxExclusive);
        }

        public int NextInt(int maxExclusive)
        {
            return _rng.Next(maxExclusive);
        }

        public float NextFloat()
        {
            return (float)_rng.NextDouble();
        }

        public double NextDouble()
        {
            return _rng.NextDouble();
        }

        public bool Chance(float probability)
        {
            return _rng.NextDouble() < probability;
        }

        public bool ChancePercent(float percent)
        {
            return _rng.NextDouble() * 100.0 < percent;
        }

        public float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return _rng.Next(minInclusive, maxExclusive);
        }

        public T Choice<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[_rng.Next(array.Length)];
        }

        public T Choice<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[_rng.Next(list.Count)];
        }

        public void Shuffle<T>(T[] array)
        {
            if (array == null || array.Length <= 1) return;
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Creates a derived provider with a new seed based on current state.
        /// Useful for creating independent sub-generators.
        /// </summary>
        public SeedRandomProvider Derive()
        {
            return new SeedRandomProvider(_rng.Next());
        }
    }
}