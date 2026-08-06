using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Object pool for Projectile instances to avoid expensive Instantiate/Destroy calls.
    /// Manages a pool of reusable projectile GameObjects for high-performance combat.
    /// Mirrors the pattern used by DamagePopupPool for consistency.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private bool debug;
        [Header("Pool Configuration")]
        [SerializeField]
        [Tooltip("Prefab of the Projectile to pool.")]
        private GameObject _projectilePrefab;

        [SerializeField]
        [Tooltip("Initial number of projectile instances to pre-instantiate.")]
        private int _initialPoolSize = 20;

        [SerializeField]
        [Tooltip("Whether the pool can grow beyond initial size when needed.")]
        private bool _expandable = true;

        [SerializeField]
        [Tooltip("Maximum pool size (only enforced if expandable is true). 0 = unlimited.")]
        private int _maxPoolSize = 256;

        private Queue<Projectile> _availableProjectiles;
        private List<Projectile> _allProjectiles;

        private static ProjectilePool _instance;
        public static ProjectilePool Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitializePool();
        }

        /// <summary>
        /// Pre-instantiate projectile instances and prepare the pool.
        /// </summary>
        private void InitializePool()
        {
            _availableProjectiles = new Queue<Projectile>(_initialPoolSize);
            _allProjectiles = new List<Projectile>(_initialPoolSize);

            // Pre-instantiate initial pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateNewProjectile();
            }
        }

        /// <summary>
        /// Create a new projectile instance and add it to the pool.
        /// </summary>
        private Projectile CreateNewProjectile()
        {
            if (_projectilePrefab == null)
            {
                if (debug) Debug.LogError("[ProjectilePool] Projectile prefab is not assigned.");
                return null;
            }

            GameObject projectileObj = Instantiate(_projectilePrefab, this.transform);
            projectileObj.SetActive(false);
            if (!projectileObj.TryGetComponent<Projectile>(out var projectile))
            {
                if (debug) Debug.LogError("[ProjectilePool] Prefab does not have a Projectile component!");
                Destroy(projectileObj);
                return null;
            }
            _allProjectiles.Add(projectile);
            _availableProjectiles.Enqueue(projectile);
            return projectile;
        }

        /// <summary>
        /// Get a projectile from the pool. Creates a new one if pool is empty and expandable.
        /// </summary>
        public Projectile Get()
        {
            Projectile projectile = null;
            if (_availableProjectiles.Count > 0)
            {
                projectile = _availableProjectiles.Dequeue();
            }
            else if (_expandable)
            {
                if (_maxPoolSize > 0 && _allProjectiles.Count >= _maxPoolSize)
                {
                    if (debug) Debug.LogWarning($"[ProjectilePool] Max pool size ({_maxPoolSize}) reached. Reusing oldest projectile.");
                    return null;
                }
                projectile = CreateNewProjectile();
                if (projectile != null && debug) Debug.Log($"[ProjectilePool] Pool expanded to {_allProjectiles.Count} instances.");
            }
            else
            {
                if (debug) Debug.LogWarning("[ProjectilePool] Pool exhausted and not expandable. Consider increasing initial size.");
                return null;
            }

            if (projectile != null) projectile.gameObject.SetActive(true);
            return projectile;
        }

        /// <summary>
        /// Return a projectile to the pool for reuse.
        /// </summary>
        public void Return(Projectile projectile)
        {
            if (projectile == null)
            {
                if (debug) Debug.LogWarning("[ProjectilePool] Attempted to return null projectile.");
                return;
            }

            // Reset state via Projectile's ResetState method
            projectile.ResetState();

            projectile.gameObject.SetActive(false);
            _availableProjectiles.Enqueue(projectile);
        }

        /// <summary>
        /// Get pool statistics for debugging.
        /// </summary>
        public (int total, int available, int active) GetStats()
        {
            int total = _allProjectiles.Count;
            int available = _availableProjectiles.Count;
            int active = total - available;
            return (total, available, active);
        }

        [ContextMenu("Show Pool Stats")]
        private void DebugShowPoolStats()
        {
            var (total, available, active) = GetStats();
            if (debug) Debug.Log($"[ProjectilePool] Pool Stats - Total: {total}, Available: {available}, Active: {active}");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_initialPoolSize < 1) _initialPoolSize = 1;
            if (_maxPoolSize < 0) _maxPoolSize = 0;
            if (_maxPoolSize > 0 && _initialPoolSize > _maxPoolSize) _initialPoolSize = _maxPoolSize;
        }
#endif
    }
}