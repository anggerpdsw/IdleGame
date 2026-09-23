using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Multi-effect pool system.
    /// Auto-returns effects after animation completes.
    /// </summary>
    public class EffectPool : MonoBehaviour
    {
        [SerializeField] private string[] _preloadEffects = { "SwordEffect" };
        [SerializeField] private int _initialSize = 8;
        [SerializeField] private bool _expandable = true;
        [SerializeField] private float _autoReturnDelay = 3f;

        private Dictionary<string, Queue<GameObject>> _pools;
        private Dictionary<string, List<GameObject>> _allByType;

        private static EffectPool _instance;
        public static EffectPool Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _pools = new Dictionary<string, Queue<GameObject>>();
            _allByType = new Dictionary<string, List<GameObject>>();

            foreach (string effectName in _preloadEffects)
                InitializePool(effectName);
        }

        private void InitializePool(string effectName)
        {
            if (_pools.ContainsKey(effectName)) return;

            GameObject prefab = EffectResources.GetEffect(effectName);
            if (prefab == null)
            {
                Debug.LogWarning($"Effect prefab '{effectName}' not found in Resources/Effects/");
                return;
            }

            _pools[effectName] = new Queue<GameObject>(_initialSize);
            _allByType[effectName] = new List<GameObject>(_initialSize);

            for (int i = 0; i < _initialSize; i++)
                CreateNew(effectName, prefab);
        }

        private GameObject CreateNew(string effectName, GameObject prefab)
        {
            if (prefab == null) return null;

            GameObject obj = Instantiate(prefab, transform);
            obj.name = $"{effectName}_{_allByType[effectName].Count}";
            obj.SetActive(false);
            _allByType[effectName].Add(obj);
            _pools[effectName].Enqueue(obj);
            return obj;
        }

        public void Spawn(string effectName, Vector3 position)
        {
            if (!_pools.ContainsKey(effectName))
                InitializePool(effectName);

            if (!_pools.ContainsKey(effectName)) return;

            GameObject go = Get(effectName);
            if (go == null) return;

            go.transform.position = position;
            go.SetActive(true);
            StartCoroutine(AutoReturn(effectName, go));
        }

        private GameObject Get(string effectName)
        {
            Queue<GameObject> pool = _pools[effectName];
            if (pool.Count > 0)
                return pool.Dequeue();

            if (_expandable)
            {
                GameObject prefab = EffectResources.GetEffect(effectName);
                return CreateNew(effectName, prefab);
            }

            return null;
        }

        private void Return(string effectName, GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            _pools[effectName].Enqueue(go);
        }

        private IEnumerator AutoReturn(string effectName, GameObject go)
        {
            yield return new WaitForSeconds(_autoReturnDelay);
            Return(effectName, go);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_initialSize < 1) _initialSize = 1;
        }
#endif
    }
}
