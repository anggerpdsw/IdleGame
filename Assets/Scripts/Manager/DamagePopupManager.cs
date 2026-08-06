using System.Collections.Generic;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.UI;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Singleton manager for all damage popup operations.
    /// Handles object pooling, display, and lifecycle of damage popups.
    /// Menggunakan pola yang sama dengan EnemyHealthBarManager (Screen Space).
    /// </summary>
    public class DamagePopupManager : MonoBehaviour
    {
        private static DamagePopupManager _instance;
        public static DamagePopupManager Instance => _instance;

        [SerializeField] private bool debug;
        [SerializeField]
        [Tooltip("Reference to the DamagePopupPool component for managing popup instances.")]
        private DamagePopupPool _popupPool;

        [Header("Position Settings")]
        [SerializeField]
        [Tooltip("Offset vertikal popup dari posisi enemy di layar (dalam piksel)")]
        private float _screenOffsetY = 30f;

        // Melacak counter popup per target untuk menghindari penumpukan
        private Dictionary<Transform, int> _targetPopupCounters = new();

        // Cache settings untuk menghindari akses terus-menerus ke SettingsController
        private bool _showDamagePopup = true;  // Master switch untuk damage (normal + critical)
        private bool _showCriticalText = true; // Filter khusus untuk critical damage
        private bool _showHealPopup = true;    // Filter khusus untuk heal popup

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            InitializePoolIfNeeded();

            // Subscribe ke SettingsController untuk mendapatkan perubahan setting
            if (SettingsController.Instance != null)
            {
                // Load initial values
                _showDamagePopup = SettingsController.Instance.ShowDamagePopup;
                _showCriticalText = SettingsController.Instance.ShowCriticalText;
                _showHealPopup = SettingsController.Instance.ShowHealPopup;

                // Subscribe to changes
                SettingsController.Instance.DamagePopupChanged += OnDamagePopupChanged;
                SettingsController.Instance.CriticalTextChanged += OnCriticalTextChanged;
                SettingsController.Instance.HealPopupChanged += OnHealPopupChanged;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe untuk mencegah memory leak
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.DamagePopupChanged -= OnDamagePopupChanged;
                SettingsController.Instance.CriticalTextChanged -= OnCriticalTextChanged;
                SettingsController.Instance.HealPopupChanged -= OnHealPopupChanged;
            }
        }

        private void OnDamagePopupChanged(bool enabled) => _showDamagePopup = enabled;
        private void OnCriticalTextChanged(bool enabled) => _showCriticalText = enabled;
        private void OnHealPopupChanged(bool enabled) => _showHealPopup = enabled;

        /// <summary>
        /// Show a damage popup at the specified world position.
        /// </summary>
        public void ShowDamage(Vector3 worldPos, DamagePopupData data, Transform target = null)
        {
            // ============================================================
            // FILTER: Cek settings sebelum menampilkan popup
            // ============================================================
            if (data.Damage < 1f) return;

            // 1. Filter Heal or Miss Popup
            bool isHealOrMiss = data.Type == DamageType.Heal || data.Type == DamageType.Miss;
            if (isHealOrMiss && !_showHealPopup) return;

            // 2. Filter Damage Popup (Normal damage, bukan Heal, bukan Miss)
            bool isDamage = data.Type != DamageType.Heal && data.Type != DamageType.Miss;
            if (isDamage && !_showDamagePopup) return;

            // 3. Filter Critical Text (sub-filter dari damage popup)
            bool isCritical = data.Critical != CriticalType.None;
            if (isCritical && !_showCriticalText) return;

            // ============================================================
            InitializePoolIfNeeded();

            if (_popupPool == null || _popupPool.Prefab == null)
            {
                if (debug) Debug.LogError("[DamagePopupManager] Pool or prefab not configured!");
                return;
            }

            // Get popup from pool
            DamagePopup popup = _popupPool.Get();
            if (popup == null)
            {
                if (debug) Debug.LogWarning($"[DamagePopupManager] No popup available in pool.");
                return;
            }

            // --- SAMA SEPERTI EnemyHealthBarManager.LateUpdate() ---
            // Konversi world position ke screen position
            Vector3 screenPos = Utilityku.WorldToScreen(worldPos);

            // Hitung sequence berdasarkan target (0-3)
            int sequence = GetTargetSequence(target);

            // Set posisi popup per slot dengan offset (x, y) yang berbeda
            // Slot 0: center       Slot 1: right
            // Slot 2: left         Slot 3: far right
            Vector3 slotOffset = sequence switch
            {
                0 => new Vector3(0f, 0f, 0f),   // Center, baseline
                1 => new Vector3(10f, 3f, 0f),  // Right, slightly up
                2 => new Vector3(-10f, 6f, 0f), // Left, more up
                _ => new Vector3(20f, 9f, 0f),  // Far right, most up
            };

            // Set posisi popup ke screen position + slot offset
            popup.transform.position = screenPos + new Vector3(slotOffset.x, _screenOffsetY + slotOffset.y, 0f);
            // ------------------------------------------------------

            // Initialize the popup (dengan sequence untuk animasi)
            // Gunakan referensi kamera utama untuk menjaga teks damage tetap terlihat baik
            popup.Initialize(popup.transform.position, data, _popupPool);
        }

        /// <summary>
        /// Get sequence (0-3) untuk target tertentu.
        /// </summary>
        private int GetTargetSequence(Transform target)
        {
            if (target == null) return 0;

            if (!_targetPopupCounters.ContainsKey(target))
                _targetPopupCounters[target] = 0;

            int sequence = _targetPopupCounters[target];

            // Update counter: 0→1→2→3→0→1→2→3...
            _targetPopupCounters[target] = (sequence + 1) % 4;

            return sequence;
        }

        /// <summary>
        /// Initialize pool if it hasn't been set up yet.
        /// </summary>
        private void InitializePoolIfNeeded()
        {
            if (_popupPool == null)
            {
                _popupPool = GetComponent<DamagePopupPool>();
                if (_popupPool == null)
                {
                    if (debug) Debug.LogError("[DamagePopupManager] No DamagePopupPool component found!");
                    enabled = false;
                }
            }
        }

        /// <summary>
        /// Display pool statistics in the console for debugging.
        /// </summary>
        [ContextMenu("Show Pool Stats")]
        public void DebugShowPoolStats()
        {
            if (_popupPool == null)
            {
                if (debug) Debug.LogError("[DamagePopupManager] Pool not available for stats display!");
                return;
            }

            var (total, available, active) = _popupPool.GetStats();
            if (debug) Debug.Log($"[DamagePopupManager] Pool Stats - Total: {total}, Available: {available}, Active: {active}");
        }

        /// <summary>
        /// Clear all active popups (for debugging purposes).
        /// </summary>
        [ContextMenu("Clear All Popups")]
        public void DebugClearAllPopups()
        {
            if (_popupPool == null) return;

            for (int i = _popupPool.AllPopups.Count - 1; i >= 0; i--)
            {
                _popupPool.AllPopups[i]?.Initialize(_popupPool.transform.position, new DamagePopupData(0, DamageType.Miss), _popupPool);
            }

            if (debug) Debug.LogWarning("[DamagePopupManager] Cleared all active popups (debug action).");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure we have a pool component
            if (_popupPool == null) _popupPool = GetComponent<DamagePopupPool>();
        }
#endif
    }
}
