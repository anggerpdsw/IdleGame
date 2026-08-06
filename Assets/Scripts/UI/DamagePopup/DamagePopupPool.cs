using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Object pool for DamagePopup instances to avoid expensive Instantiate/Destroy calls.
    /// Manages a pool of reusable popup GameObjects for high-performance damage display.
    /// </summary>
    public class DamagePopupPool : MonoBehaviour
    {
        [SerializeField] private bool debug;
        [Header("Pool Configuration")]
        [SerializeField]
        [Tooltip("Prefab of the DamagePopup to pool.")]
        private GameObject _popupPrefab;

        [SerializeField]
        [Tooltip("Initial number of popup instances to pre-instantiate.")]
        private int _initialPoolSize = 20;

        [SerializeField]
        [Tooltip("Whether the pool can grow beyond initial size when needed.")]
        private bool _expandable = true;

        [SerializeField]
        [Tooltip("Maximum pool size (only enforced if expandable is true). 0 = unlimited.")]
        private int _maxPoolSize = 256;

        private Queue<DamagePopup> _availablePopups;
        private List<DamagePopup> _allPopups;

        private void Awake()
        {
            InitializePool();
        }

        /// <summary>
        /// Pre-instantiate popup instances and prepare the pool.
        /// </summary>
        private void InitializePool()
        {
            _availablePopups = new Queue<DamagePopup>(_initialPoolSize);
            _allPopups = new List<DamagePopup>(_initialPoolSize);

            // Pre-instantiate initial pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateNewPopup();
            }
        }

        /// <summary>
        /// Create a new popup instance and add it to the pool.
        /// </summary>
        private DamagePopup CreateNewPopup()
        {
            GameObject popupObj = Instantiate(_popupPrefab, this.transform);
            popupObj.SetActive(false);

            if (!popupObj.TryGetComponent<DamagePopup>(out var popup))
            {
                if (debug) Debug.LogError("[DamagePopupPool] Prefab does not have a DamagePopup component!");
                Destroy(popupObj);
                return null;
            }

            _allPopups.Add(popup);
            _availablePopups.Enqueue(popup);

            return popup;
        }

        /// <summary>
        /// Get a popup from the pool. Creates a new one if pool is empty and expandable.
        /// </summary>
        public DamagePopup Get()
        {
            DamagePopup popup = null;

            // Try to get from available pool
            if (_availablePopups.Count > 0)
            {
                popup = _availablePopups.Dequeue();
            }
            // Pool is empty - try to expand
            else if (_expandable)
            {
                // Check max pool size limit
                if (_maxPoolSize > 0 && _allPopups.Count >= _maxPoolSize)
                {
                    if (debug) Debug.LogWarning($"[DamagePopupPool] Max pool size ({_maxPoolSize}) reached. Reusing oldest popup.");
                    // In production, you might want to queue this request or force-return an active popup
                    return null;
                }

                popup = CreateNewPopup();
                if (popup != null)
                    if (debug) Debug.Log($"[DamagePopupPool] Pool expanded to {_allPopups.Count} instances.");
            }
            else
            {
                if (debug) Debug.LogWarning("[DamagePopupPool] Pool exhausted and not expandable. Consider increasing initial size.");
                return null;
            }

            if (popup != null) popup.gameObject.SetActive(true);

            return popup;
        }

        /// <summary>
        /// Expose prefab for external use (e.g., DamagePopupManager).
        /// </summary>
        public GameObject Prefab => _popupPrefab;

        /// <summary>
        /// Expose list of all active popups for debugging/utilities.
        /// </summary>
        public List<DamagePopup> AllPopups => _allPopups;

        /// <summary>
        /// Return a popup to the pool for reuse.
        /// </summary>
        public void Return(DamagePopup popup)
        {
            if (popup == null)
            {
                if (debug) Debug.LogWarning("[DamagePopupPool] Attempted to return null popup.");
                return;
            }

            // Deactivate and reset
            popup.gameObject.SetActive(false);

            // Return to pool
            _availablePopups.Enqueue(popup);
        }

        /// <summary>
        /// Get pool statistics for debugging.
        /// </summary>
        public (int total, int available, int active) GetStats()
        {
            int total = _allPopups.Count;
            int available = _availablePopups.Count;
            int active = total - available;

            return (total, available, active);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_initialPoolSize < 1) _initialPoolSize = 1;

            if (_maxPoolSize < 0) _maxPoolSize = 0;

            if (_maxPoolSize > 0 && _initialPoolSize > _maxPoolSize) 
                _initialPoolSize = _maxPoolSize;
        }
#endif
    }
}
