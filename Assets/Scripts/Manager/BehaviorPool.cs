using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Generic pool for enemy behavior objects (graves, totems, summons).
    /// Mirrors ProjectilePool pattern. Single world-space canvas for all pooled UI-based objects.
    /// </summary>
    public class BehaviorPool : MonoBehaviour
    {
        [SerializeField] private bool _debug;

        [Header("Pool Configuration")]
        [SerializeField] private bool _expandable = true;
        [SerializeField] private int _maxPoolSize = 256;

        private readonly Dictionary<string, Queue<GameObject>> _pools = new();
        private readonly Dictionary<string, List<GameObject>> _allObjects = new();

        private static BehaviorPool _instance;
        public static BehaviorPool Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// Get object from pool. Creates pool on first request for prefab.
        /// </summary>
        public T Get<T>(GameObject prefab) where T : Component
        {
            if (prefab == null)
            {
                if (_debug) Debug.LogError("[BehaviorPool] Null prefab");
                return null;
            }

            string key = prefab.name;

            if (!_pools.ContainsKey(key))
            {
                _pools[key] = new Queue<GameObject>();
                _allObjects[key] = new List<GameObject>();
            }

            GameObject obj;
            if (_pools[key].Count > 0)
            {
                obj = _pools[key].Dequeue();
            }
            else if (_expandable)
            {
                if (_maxPoolSize > 0 && _allObjects[key].Count >= _maxPoolSize)
                {
                    if (_debug) Debug.LogWarning($"[BehaviorPool] Max size reached for {key}");
                    return null;
                }
                obj = CreateNew(prefab, key);
            }
            else
            {
                if (_debug) Debug.LogWarning($"[BehaviorPool] Pool exhausted for {key}");
                return null;
            }

            if (obj == null) return null;

            obj.SetActive(true);
            return obj.GetComponent<T>();
        }

        private GameObject CreateNew(GameObject prefab, string key)
        {
            var obj = Instantiate(prefab, this.transform);
            obj.name = $"{key}_{_allObjects[key].Count}";
            obj.SetActive(false);
            _allObjects[key].Add(obj);
            return obj;
        }

        /// <summary>
        /// Return object to pool.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null)
            {
                if (_debug) Debug.LogWarning("[BehaviorPool] Attempted to return null object");
                return;
            }

            string key = obj.name.Split('_')[0];
            if (!_pools.ContainsKey(key))
            {
                if (_debug) Debug.LogWarning($"[BehaviorPool] Unknown pool key: {key}");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            _pools[key].Enqueue(obj);
        }

        [ContextMenu("Show Pool Stats")]
        private void DebugShowPoolStats()
        {
            if (!_debug) return;
            foreach (var kvp in _allObjects)
            {
                int total = kvp.Value.Count;
                int available = _pools[kvp.Key].Count;
                int active = total - available;
                Debug.Log($"[BehaviorPool] {kvp.Key} - Total: {total}, Available: {available}, Active: {active}");
            }
        }
    }
}
