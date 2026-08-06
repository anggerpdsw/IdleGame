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

        private static EnemyHealthBarManager _instance;

        // Pool entry yang menyimpan Slider + Image references
        private class HealthBarEntry
        {
            public Slider Slider;
            public Image DefenseBreakImage;
            public Image HeartBreakImage;
            public GameObject RootObject; // Parent GameObject (PanelHealth)
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

        private void LateUpdate()
        {
            if (!_enemyHealthBarToggle) return; // Early return jika setting dimatikan

            var keys = new List<EnemyAi>(_activeHealthBars.Keys);

            foreach (var enemy in keys)
            {
                if (enemy == null)
                {
                    UnregisterEnemy(enemy);
                    continue;
                }

                if (!_activeHealthBars.TryGetValue(enemy, out var entry)) continue;

                Vector3 screenPos = Utilityku.WorldToScreen(enemy.HealthBarWorldPosition);
                entry.RootObject.transform.position = screenPos;

                // Update DefenseBreak visibility
                bool hasDefenseBreak = enemy.HasActiveDefenseBreak();
                entry.DefenseBreakImage.enabled = hasDefenseBreak && _enemyHealthBarToggle;

                // Update HeartBreak visibility (enemy max health reduced)
                bool hasHeartBreak = enemy.HasReducedMaxHealth();
                entry.HeartBreakImage.enabled = hasHeartBreak && _enemyHealthBarToggle;
            }
        }

        private void OnDestroy()
        {
            if (SettingsController.Instance != null)
                SettingsController.Instance.EnemyHealthBarChanged -= OnEnemyHealthBarChanged;
        }

        private void OnEnemyHealthBarChanged(bool enabled)
        {
            _enemyHealthBarToggle = enabled;

            // Sembunyikan/tampilkan semua health bar yang aktif
            foreach (var kvp in _activeHealthBars)
            {
                if (kvp.Value != null && kvp.Key != null && kvp.Value.RootObject != null)
                {
                    kvp.Value.RootObject.SetActive(enabled);
                    kvp.Value.DefenseBreakImage.enabled = enabled && kvp.Key.HasActiveDefenseBreak();
                    kvp.Value.HeartBreakImage.enabled = enabled && kvp.Key.HasReducedMaxHealth();
                }
            }
        }

        private void PrePoolHealthBars()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject bar = Instantiate(_healthBarPrefab, this.transform);

                // Find DefenseBreak and HeartBreak images in the prefab hierarchy
                Transform defenseBreakTf = bar.transform.Find("DefenseBreak");
                Transform heartBreakTf = bar.transform.Find("HeartBreak");
                Transform healthBarTf = bar.transform.Find("HealthBar");
                var defenseBreakImg = defenseBreakTf ? defenseBreakTf.GetComponent<Image>() : null;
                var heartBreakImg = heartBreakTf ? heartBreakTf.GetComponent<Image>() : null;
                var heartSlider = healthBarTf ? healthBarTf.GetComponent<Slider>() : null;

                var entry = new HealthBarEntry
                {
                    Slider = heartSlider,
                    DefenseBreakImage = defenseBreakImg,
                    HeartBreakImage = heartBreakImg,
                    RootObject = bar
                };

                bar.SetActive(false);
                _healthBarPool.Enqueue(entry);
            }
        }

        public void RegisterEnemy(EnemyAi enemy, float maxHealth)
        {
            if (enemy == null || _activeHealthBars.ContainsKey(enemy)) return;

            HealthBarEntry entry = GetHealthBarFromPool();
            entry.Slider.maxValue = maxHealth;
            entry.Slider.value = maxHealth;

            // Sembunyikan indikator di awal
            if (entry.DefenseBreakImage != null) entry.DefenseBreakImage.enabled = false;
            if (entry.HeartBreakImage != null) entry.HeartBreakImage.enabled = false;

            // Tampilkan hanya jika setting aktif
            entry.RootObject.SetActive(_enemyHealthBarToggle);

            _activeHealthBars[enemy] = entry;
        }

        public void UpdateEnemyHealth(EnemyAi enemy, float currentHealth)
        {
            if (_activeHealthBars.TryGetValue(enemy, out var entry))
                entry.Slider.value = currentHealth;
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
            Transform defenseBreakTf = newBar.transform.Find("DefenseBreak");
            Transform heartBreakTf = newBar.transform.Find("HeartBreak");
            Transform healthBarTf = newBar.transform.Find("HealthBar");
            var defenseBreakImg = defenseBreakTf ? defenseBreakTf.GetComponent<Image>() : null;
            var heartBreakImg = heartBreakTf ? heartBreakTf.GetComponent<Image>() : null;
            var heartSlider = healthBarTf ? healthBarTf.GetComponent<Slider>() : null;

            var entry = new HealthBarEntry
            {
                Slider = heartSlider,
                DefenseBreakImage = defenseBreakImg,
                HeartBreakImage = heartBreakImg,
                RootObject = newBar
            };

            return entry;
        }

        private void ReturnHealthBar(HealthBarEntry entry)
        {
            if (entry == null || entry.Slider == null || entry.RootObject == null) return;
            entry.RootObject.SetActive(false);
            entry.RootObject.transform.SetParent(this.transform);
            if (entry.DefenseBreakImage != null) entry.DefenseBreakImage.enabled = false;
            if (entry.HeartBreakImage != null) entry.HeartBreakImage.enabled = false;
            _healthBarPool.Enqueue(entry);
        }
    }
}