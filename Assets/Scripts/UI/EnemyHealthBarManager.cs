using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Mengelola semua health bar enemy menggunakan 1 Canvas global dan pooling.
    /// Health bar ditampilkan di atas enemy menggunakan Screen Space - Overlay.
    /// Termasuk indikator DefenseBreak dan HeartBreak (ReduceMaxHealth).
    /// </summary>
    public class EnemyHealthBarManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _healthBarPrefab;
        [Tooltip("Offset vertikal health bar dari posisi enemy di layar (dalam piksel)")]
        [SerializeField] private float _healthBarOffsetY = 0.15f;
        public float HealthBarOffsetY => _healthBarOffsetY;

        private static EnemyHealthBarManager _instance;

        // Pool entry yang menyimpan Slider + Image references
        private class HealthBarEntry
        {
            public Slider Slider;
            public Image DamageReductionImage;
            public Image HeartBreakImage;
            public Image DefenseBreakImage;
            public GameObject RootObject; // Parent GameObject (PanelHealth)
            public float LastHealth;    // last displayed value – avoids redundant slider updates
        }

        private readonly Dictionary<EnemyAi, HealthBarEntry> _activeHealthBars = new();
        private readonly Queue<HealthBarEntry> _healthBarPool = new();
        private const int POOL_SIZE = 256;

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
            if (SettingsController.Instance != null)
            {
                _enemyHealthBarToggle = SettingsController.Instance.ShowEnemyHealthBar;
                SettingsController.Instance.EnemyHealthBarChanged += OnEnemyHealthBarChanged;
            }
        }

        private void OnDestroy()
        {
            if (SettingsController.Instance != null)
                SettingsController.Instance.EnemyHealthBarChanged -= OnEnemyHealthBarChanged;
        }

        private void LateUpdate()
        {
            if (!_enemyHealthBarToggle) return; // Early return jika setting dimatikan

            foreach (var kvp in _activeHealthBars)
            {
                var enemy = kvp.Key;
                var entry = kvp.Value;

                if (enemy == null)
                {
                    UnregisterEnemy(enemy);
                    continue;
                }

                // Convert world position to screen once.
                Vector3 screenPos = Utilityku.WorldToScreen(enemy.HealthBarWorldPosition);

                // Simple view‑cull: if behind camera (negative Z) or far outside screen, hide.
                const float margin = 100f; // a few pixels outside the view is fine to hide.
                if (screenPos.z < 0f ||
                    screenPos.x < -margin || screenPos.x > Screen.width + margin ||
                    screenPos.y < -margin || screenPos.y > Screen.height + margin)
                {
                    if (entry.RootObject.activeSelf)
                        entry.RootObject.SetActive(false);
                    continue;
                }

                // Show/update the health bar.
                entry.RootObject.SetActive(true);
                entry.RootObject.transform.position = screenPos + Vector3.up * _healthBarOffsetY;
            }
        }

        public void UpdateEnemyStatus(EnemyAi enemy)
        {
            if (enemy == null) return;
            if (!_activeHealthBars.TryGetValue(enemy, out var entry)) return;
            if (entry.DamageReductionImage != null)
            {
                bool showDamageReduction = enemy.HasActiveDamageReduction();
                if (entry.DamageReductionImage.enabled != showDamageReduction)
                    entry.DamageReductionImage.enabled = showDamageReduction;
            }
            if (entry.HeartBreakImage != null)
            {
                bool showHeartBreak = enemy.HasReducedMaxHealth();
                if (entry.HeartBreakImage.enabled != showHeartBreak)
                    entry.HeartBreakImage.enabled = showHeartBreak;
            }
            if (entry.DefenseBreakImage != null)
            {
                bool showDefenseBreak = enemy.HasActiveDefenseBreak();
                if (entry.DefenseBreakImage.enabled != showDefenseBreak)
                    entry.DefenseBreakImage.enabled = showDefenseBreak;
            }
        }

        public void UpdateEnemyHealth(EnemyAi enemy, float currentHealth)
        {
            if (enemy == null) return;
            if (!_activeHealthBars.TryGetValue(enemy, out var entry)) return;
            if (entry.Slider != null &&
                Mathf.Abs(entry.Slider.value - currentHealth) > 0.001f)
            {
                entry.Slider.value = currentHealth;
                entry.LastHealth = currentHealth;
            }
        }

        private void OnEnemyHealthBarChanged(bool enabled)
        {
            _enemyHealthBarToggle = enabled;

            // Show/hide all active bars instantly.
            foreach (var kvp in _activeHealthBars)
            {
                if (kvp.Value?.RootObject != null)
                    kvp.Value.RootObject.SetActive(enabled && kvp.Key != null);
            }
        }

        private void PrePoolHealthBars()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject bar = Instantiate(_healthBarPrefab, this.transform);

                // Find DefenseBreak and HeartBreak images in the prefab hierarchy
                Transform damageReductionTf = bar.transform.Find("DamageReduction");
                Transform heartBreakTf = bar.transform.Find("HeartBreak");
                Transform defenseBreakTf = bar.transform.Find("DefenseBreak");
                Transform healthBarTf = bar.transform.Find("HealthBar");
                var damageReductionImg = damageReductionTf ? damageReductionTf.GetComponent<Image>() : null;
                var heartBreakImg = heartBreakTf ? heartBreakTf.GetComponent<Image>() : null;
                var defenseBreakImg = defenseBreakTf ? defenseBreakTf.GetComponent<Image>() : null;
                var heartSlider = healthBarTf ? healthBarTf.GetComponent<Slider>() : null;

                var entry = new HealthBarEntry
                {
                    Slider = heartSlider,
                    DamageReductionImage = damageReductionImg,
                    HeartBreakImage = heartBreakImg,
                    DefenseBreakImage = defenseBreakImg,
                    RootObject = bar,
                    LastHealth = -1f // force first update
                };

                bar.SetActive(false);
                _healthBarPool.Enqueue(entry);
            }
        }

        public void RegisterEnemy(EnemyAi enemy, float maxHealth)
        {
            if (enemy == null || _activeHealthBars.ContainsKey(enemy)) return;

            HealthBarEntry entry  = GetHealthBarFromPool();
            entry.Slider.maxValue = maxHealth;
            entry.Slider.value    = maxHealth;
            entry.LastHealth      = maxHealth;

            // Sembunyikan indikator di awal
            if (entry.DamageReductionImage != null) entry.DamageReductionImage.enabled = false;
            if (entry.HeartBreakImage != null) entry.HeartBreakImage.enabled = false;
            if (entry.DefenseBreakImage != null) entry.DefenseBreakImage.enabled = false;

            // Tampilkan hanya jika setting aktif
            entry.RootObject.SetActive(_enemyHealthBarToggle);

            _activeHealthBars[enemy] = entry;
        }

        public void UnregisterEnemy(EnemyAi enemy)
        {
            if (_activeHealthBars.TryGetValue(enemy, out var entry))
            {
                ReturnHealthBar(entry);
                _activeHealthBars.Remove(enemy);
            }
        }

        private HealthBarEntry GetHealthBarFromPool()
        {
            if (_healthBarPool.Count > 0)
                return _healthBarPool.Dequeue();

            // Expand pool bila habis
            GameObject newBar = Instantiate(_healthBarPrefab, this.transform);
            newBar.SetActive(false);
            Transform damageReductionTf = newBar.transform.Find("DamageReduction");
            Transform heartBreakTf = newBar.transform.Find("HeartBreak");
            Transform defenseBreakTf = newBar.transform.Find("DefenseBreak");
            Transform healthBarTf = newBar.transform.Find("HealthBar");
            var damageReductionImg = damageReductionTf ? damageReductionTf.GetComponent<Image>() : null;
            var heartBreakImg = heartBreakTf ? heartBreakTf.GetComponent<Image>() : null;
            var defenseBreakImg = defenseBreakTf ? defenseBreakTf.GetComponent<Image>() : null;
            var heartSlider = healthBarTf ? healthBarTf.GetComponent<Slider>() : null;

            var entry = new HealthBarEntry
            {
                Slider = heartSlider,
                DamageReductionImage = damageReductionImg,
                HeartBreakImage   = heartBreakImg,
                DefenseBreakImage = defenseBreakImg,
                RootObject        = newBar,
                LastHealth        = -1f
            };

            return entry;
        }

        private void ReturnHealthBar(HealthBarEntry entry)
        {
            if (entry == null || entry.Slider == null || entry.RootObject == null) return;
            entry.RootObject.SetActive(false);
            entry.RootObject.transform.SetParent(this.transform);
            if (entry.DamageReductionImage != null) entry.DamageReductionImage.enabled = false;
            if (entry.HeartBreakImage != null) entry.HeartBreakImage.enabled = false;
            if (entry.DefenseBreakImage != null) entry.DefenseBreakImage.enabled = false;
            _healthBarPool.Enqueue(entry);
        }
    }
}