using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Mengelola semua health bar enemy menggunakan 1 Canvas global dan pooling.
    /// Health bar ditampilkan di atas enemy menggunakan Screen Space - Overlay.
    /// Termasuk indikator status effect pada enemy.
    /// </summary>
    public class EnemyHealthBarManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _healthBarPrefab;
        [Tooltip("Offset vertikal health bar dari posisi enemy di layar (dalam piksel)")]
        [SerializeField] private float _healthBarOffsetY = 0.15f;
        public float HealthBarOffsetY => _healthBarOffsetY;

        private static EnemyHealthBarManager _instance;

        // Pool entry yang menyimpan Slider + StatusIcon dictionary references
        private class HealthBarEntry
        {
            public Slider Slider;
            public Dictionary<StatusEffectType, Image> StatusIcons;
            public GameObject RootObject; // Parent GameObject (PanelHealth)
            public float LastHealth;    // last displayed value – avoids redundant slider updates

            // Menghindari assignment Transform.position jika posisi belum berubah.
            public Vector3 LastScreenPosition;
            public bool HasLastScreenPosition;
        }

        private const int POOL_SIZE = 256;
        private const float SCREEN_MARGIN = 100f;
        private const float HEALTH_EPSILON = 0.001f;

        private readonly Dictionary<EnemyAi, HealthBarEntry> _activeHealthBars = new(POOL_SIZE);
        private readonly Queue<HealthBarEntry> _healthBarPool = new(POOL_SIZE);
        // Hanya digunakan ketika enemy destroyed/null harus dikeluarkan dari dictionary.
        private readonly List<EnemyAi> _invalidEnemies = new(16);

        private bool _enemyHealthBarToggle = true;

        public static EnemyHealthBarManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            PrePoolHealthBars();
        }

        private void Start()
        {
            SettingsController settings = SettingsController.Instance;
            if (settings == null) return;
            _enemyHealthBarToggle = settings.ShowEnemyHealthBar;
            SettingsController.Instance.EnemyHealthBarChanged += OnEnemyHealthBarChanged;
        }

        private void OnDestroy()
        {
            SettingsController settings = SettingsController.Instance;
            if (settings == null) return;
            settings.EnemyHealthBarChanged -= OnEnemyHealthBarChanged;
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            // Tidak ada pekerjaan CPU ketika fitur dimatikan atau tidak ada enemy aktif.
            if (!_enemyHealthBarToggle || _activeHealthBars.Count == 0) return;
            _invalidEnemies.Clear();

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Vector3 offset = Vector3.up * _healthBarOffsetY;

            foreach (var kvp in _activeHealthBars)
            {
                EnemyAi enemy = kvp.Key;
                HealthBarEntry entry = kvp.Value;

                if (enemy == null)
                {
                    _invalidEnemies.Add(enemy);
                    continue;
                }

                // Convert world position to screen once.
                Vector3 screenPos = Utilityku.WorldToScreen(enemy.HealthBarWorldPosition);

                bool outsideScreen =
                    screenPos.z < 0f ||
                    screenPos.x < -SCREEN_MARGIN ||
                    screenPos.x > screenWidth + SCREEN_MARGIN ||
                    screenPos.y < -SCREEN_MARGIN ||
                    screenPos.y > screenHeight + SCREEN_MARGIN;

                if (outsideScreen)
                {
                    SetActiveIfChanged(entry.RootObject, false);
                    continue;
                }

                SetActiveIfChanged(entry.RootObject, true);

                Vector3 targetPosition = screenPos + offset;

                if (!entry.HasLastScreenPosition || entry.LastScreenPosition != targetPosition)
                {
                    entry.RootObject.transform.position = targetPosition;
                    entry.LastScreenPosition = targetPosition;
                    entry.HasLastScreenPosition = true;
                }
            }

            // Dictionary tidak dimodifikasi selama foreach.
            for (int i = 0; i < _invalidEnemies.Count; i++)
                UnregisterEnemy(_invalidEnemies[i]);
        }

        /// <summary>
        /// Update single status icon based on effect type (event-driven).
        /// </summary>
        private void UpdateStatusIcon(EnemyAi enemy, StatusEffectType type)
        {
            if (enemy == null || !_activeHealthBars.TryGetValue(enemy, out var entry)) return;
            if (entry.StatusIcons == null || !entry.StatusIcons.TryGetValue(type, out var image)) return;

            bool active = type switch
            {
                StatusEffectType.DamageReduction => enemy.HasActiveDamageReduction(),
                StatusEffectType.HeartBreak => enemy.HasReducedMaxHealth(),
                StatusEffectType.DefenseBreak => enemy.HasActiveDefenseBreak(),
                StatusEffectType.Volatile => enemy.HasActiveStatus(StatusEffectType.Volatile),
                _ => false
            };
            SetImageState(image, active);
        }

        /// <summary>
        /// Refresh all status icons (fallback for batch changes).
        /// </summary>
        public void UpdateEnemyStatus(EnemyAi enemy)
        {
            if (enemy == null || !_activeHealthBars.TryGetValue(enemy, out var entry)) return;
            if (entry.StatusIcons == null) return;

            foreach (var kvp in entry.StatusIcons)
            {
                UpdateStatusIcon(enemy, kvp.Key);
            }
        }

        public void UpdateEnemyHealth(EnemyAi enemy, float currentHealth)
        {
            if (enemy == null || !_activeHealthBars.TryGetValue(enemy, out var entry)) return;
            Slider slider = entry.Slider;
            if (slider == null) return;
            if (Mathf.Abs(slider.value - currentHealth) <= HEALTH_EPSILON) return;
            slider.value = currentHealth;
            entry.LastHealth = currentHealth;
        }

        private void OnEnemyHealthBarChanged(bool enabled)
        {
            if (_enemyHealthBarToggle == enabled) return;
            _enemyHealthBarToggle = enabled;
            // Show/hide all active bars instantly.
            foreach (var kvp in _activeHealthBars)
            {
                GameObject root = kvp.Value?.RootObject;
                if (root != null)
                    SetActiveIfChanged(root, enabled && kvp.Key != null);
            }
        }

        private void PrePoolHealthBars()
        {
            for (int i = 0; i < POOL_SIZE; i++)
                _healthBarPool.Enqueue(CreateHealthBarEntry());
        }

        public void RegisterEnemy(EnemyAi enemy, float maxHealth)
        {
            if (enemy == null || _activeHealthBars.ContainsKey(enemy)) return;
            HealthBarEntry entry = GetHealthBarFromPool();
            Slider slider = entry.Slider;
            if (slider != null)
            {
                slider.maxValue = maxHealth;
                slider.value = maxHealth;
            }
            entry.LastHealth = maxHealth;
            entry.HasLastScreenPosition = false;

            ResetIndicatorImages(entry);
            SetActiveIfChanged(entry.RootObject, _enemyHealthBarToggle);

            _activeHealthBars.Add(enemy, entry);

            // Subscribe to status effect events for targeted icon updates
            var statusController = enemy.EnemyStatusEffect;
            if (statusController != null)
            {
                statusController.OnEffectApplied += effect => UpdateStatusIcon(enemy, effect.Type);
                statusController.OnEffectRemoved += effect => UpdateStatusIcon(enemy, effect.Type);
                statusController.OnEffectsChanged += () => UpdateEnemyStatus(enemy);
            }

            // Initial status refresh
            UpdateEnemyStatus(enemy);
        }

        public void UnregisterEnemy(EnemyAi enemy)
        {
            if (!_activeHealthBars.TryGetValue(enemy, out var entry)) return;

            // Unsubscribe from status effect events
            var statusController = enemy.EnemyStatusEffect;
            if (statusController != null)
            {
                statusController.OnEffectApplied -= effect => UpdateStatusIcon(enemy, effect.Type);
                statusController.OnEffectRemoved -= effect => UpdateStatusIcon(enemy, effect.Type);
                statusController.OnEffectsChanged -= () => UpdateEnemyStatus(enemy);
            }

            ReturnHealthBar(entry);
            _activeHealthBars.Remove(enemy);
        }

        private HealthBarEntry GetHealthBarFromPool()
        {
            return _healthBarPool.Count > 0
                ? _healthBarPool.Dequeue()
                : CreateHealthBarEntry();
        }
        
        private HealthBarEntry CreateHealthBarEntry()
        {
            GameObject bar = Instantiate(_healthBarPrefab, transform);
            Transform barTransform = bar.transform;

            Transform healthBarTf = barTransform.Find("HealthBar");

            var statusIcons = new Dictionary<StatusEffectType, Image>(4);
            AddStatusIcon(barTransform, StatusEffectType.DamageReduction, statusIcons);
            AddStatusIcon(barTransform, StatusEffectType.HeartBreak, statusIcons);
            AddStatusIcon(barTransform, StatusEffectType.DefenseBreak, statusIcons);
            AddStatusIcon(barTransform, StatusEffectType.Volatile, statusIcons);

            HealthBarEntry entry = new()
            {
                Slider = healthBarTf?.GetComponent<Slider>(),
                StatusIcons = statusIcons,
                RootObject = bar,
                LastHealth = -1f,
                HasLastScreenPosition = false
            };

            ResetIndicatorImages(entry);
            bar.SetActive(false);

            return entry;
        }

        private static void AddStatusIcon(
            Transform parent, StatusEffectType type, Dictionary<StatusEffectType, Image> dict)
        {
            Transform child = parent.Find(type.ToString());
            if (child == null)
            {
                Debug.LogWarning($"[EnemyHealthBar] Child '{type}' not found in prefab");
                return;
            }
            if (!child.TryGetComponent<Image>(out var img)) return;
            dict[type] = img;
        }

        private void ReturnHealthBar(HealthBarEntry entry)
        {
            if (entry == null || entry.RootObject == null) return;
            SetActiveIfChanged(entry.RootObject, false);
            // Selalu reset posisi-cache ketika masuk pool.
            entry.HasLastScreenPosition = false;
            entry.RootObject.transform.SetParent(transform);
            ResetIndicatorImages(entry);
            _healthBarPool.Enqueue(entry);
        }

        private static void SetImageState(Image image, bool enabled)
        {
            if (image == null) return;
            GameObject go = image.gameObject;
            if (go.activeSelf != enabled) go.SetActive(enabled);
        }

        private static void SetActiveIfChanged(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private static void ResetIndicatorImages(HealthBarEntry entry)
        {
            if (entry.StatusIcons == null) return;
            foreach (var image in entry.StatusIcons.Values)
                SetImageState(image, false);
        }
    }
}