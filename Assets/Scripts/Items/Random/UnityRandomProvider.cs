using UnityEngine;

namespace IdleDefenseSurvival.Items.Random
{
    /// <summary>
    /// Unity's built-in Random implementation.
    /// Non-deterministic, uses UnityEngine.Random.
    /// </summary>
    public sealed class UnityRandomProvider : IRandomProvider
    {
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        public int NextInt(int maxExclusive)
        {
            return UnityEngine.Random.Range(0, maxExclusive);
        }

        public float NextFloat()
        {
            return UnityEngine.Random.value;
        }

        public double NextDouble()
        {
            return UnityEngine.Random.value;
        }

        public bool Chance(float probability)
        {
            return UnityEngine.Random.value < probability;
        }

        public bool ChancePercent(float percent)
        {
            return UnityEngine.Random.value * 100f < percent;
        }

        public float Range(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        public T Choice<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[UnityEngine.Random.Range(0, array.Length)];
        }

        public T Choice<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public void Shuffle<T>(T[] array)
        {
            if (array == null || array.Length <= 1) return;
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}