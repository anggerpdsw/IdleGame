using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Items.Random
{
    /// <summary>
    /// Test random provider with pre-defined sequence for deterministic testing.
    /// </summary>
    public sealed class TestRandomProvider : IRandomProvider
    {
        private readonly Queue<float> _floatQueue = new();
        private readonly Queue<int> _intQueue = new();
        private readonly Queue<bool> _boolQueue = new();

        public TestRandomProvider() { }

        public TestRandomProvider(params float[] floats)
        {
            foreach (var f in floats) _floatQueue.Enqueue(f);
        }

        public TestRandomProvider(IEnumerable<float> floats)
        {
            foreach (var f in floats) _floatQueue.Enqueue(f);
        }

        // Queue manipulation for test setup
        public void EnqueueFloat(float value) => _floatQueue.Enqueue(value);
        public void EnqueueInt(int value) => _intQueue.Enqueue(value);
        public void EnqueueBool(bool value) => _boolQueue.Enqueue(value);
        public void EnqueueRange(params float[] values) { foreach (var v in values) _floatQueue.Enqueue(v); }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (_intQueue.Count > 0)
            {
                int val = _intQueue.Dequeue();
                return Math.Clamp(val, minInclusive, maxExclusive - 1);
            }
            return minInclusive;
        }

        public int NextInt(int maxExclusive)
        {
            if (_intQueue.Count > 0)
            {
                int val = _intQueue.Dequeue();
                return Math.Clamp(val, 0, maxExclusive - 1);
            }
            return 0;
        }

        public float NextFloat()
        {
            return _floatQueue.Count > 0 ? _floatQueue.Dequeue() : 0f;
        }

        public double NextDouble()
        {
            return _floatQueue.Count > 0 ? _floatQueue.Dequeue() : 0.0;
        }

        public bool Chance(float probability)
        {
            if (_boolQueue.Count > 0) return _boolQueue.Dequeue();
            return _floatQueue.Count > 0 ? _floatQueue.Dequeue() < probability : false;
        }

        public bool ChancePercent(float percent)
        {
            return Chance(percent / 100f);
        }

        public float Range(float min, float max)
        {
            float t = NextFloat();
            return min + t * (max - min);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return NextInt(minInclusive, maxExclusive);
        }

        public T Choice<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            int idx = NextInt(0, array.Length);
            return array[idx];
        }

        public T Choice<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            int idx = NextInt(0, list.Count);
            return list[idx];
        }

        public void Shuffle<T>(T[] array)
        {
            // No-op for test provider - use EnqueueInt to control order
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            // No-op for test provider
        }

        /// <summary>
        /// Resets all queues.
        /// </summary>
        public void Reset()
        {
            _floatQueue.Clear();
            _intQueue.Clear();
            _boolQueue.Clear();
        }
    }
}