using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Manager;
using TMPro;
using System.Collections.Generic;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays wave information using a single slider.
    /// - Green color during ActiveWave
    /// - Yellow color during InterWave
    /// </summary>
    public class DamageStatsUI : MonoBehaviour
    {
        [Header("Damage Stats UI")]
        [Tooltip("Panel that contains the whole damage stats UI (can be toggled on/off)")]
        [SerializeField] private GameObject _damageStatsPanel;
        [Tooltip("Parent transform that holds damage stat items (Vertical Layout Group)")]
        [SerializeField] private Transform _damageStatsContent;
        [Tooltip("Button to toggle damage stats panel visibility")]
        [SerializeField] private Button _damageStatToggleButton;
        [Tooltip("Prefab of a single damage stat item (label + slider)")]
        [SerializeField] private GameObject _damageStatItemPrefab;

        // Runtime cache of instantiated stat items per source name
        private readonly Dictionary<string, GameObject> _statItems = new();
        private bool _damageStatsVisible = false;

        private void Awake()
        {
            // Setup toggle button listener
            if (_damageStatToggleButton != null)
                _damageStatToggleButton.onClick.AddListener(ToggleDamageStatsPanel);

            // Initialize panel as hidden
            if (_damageStatsPanel != null)
            {
                _damageStatsPanel.SetActive(false);
                _damageStatsVisible = false;
            }
        }

        private void Start()
        {
        }

        private void Update()
        {
            // Update damage stats only if panel is visible
            if (_damageStatsVisible) UpdateDamageStatsDisplay();
        }

        /// <summary>
        /// Toggle visibility of damage stats panel.
        /// </summary>
        private void ToggleDamageStatsPanel()
        {
            _damageStatsVisible = !_damageStatsVisible;
            if (_damageStatsPanel != null) _damageStatsPanel.SetActive(_damageStatsVisible);
        }

        private void UpdateDamageStatsDisplay()
        {
            if (_damageStatsPanel == null || _damageStatItemPrefab == null || _damageStatsContent == null) return;

            var currentWaveDamage = WaveManager.Instance.CurrentWaveDamage;

            // Aggregate total damage per source across all enemies
            var damagePerSource = new Dictionary<string, long>();

            foreach (var enemyData in currentWaveDamage.Values)
            {
                foreach (var kvp in enemyData)
                {
                    if (!damagePerSource.ContainsKey(kvp.Key))
                        damagePerSource[kvp.Key] = 0;
                    damagePerSource[kvp.Key] += kvp.Value;
                }
            }

            long totalDamage = 0;
            foreach (var damage in damagePerSource.Values) totalDamage += damage;

            // Create missing prefab instances for new sources
            foreach (var source in damagePerSource.Keys)
            {
                if (!_statItems.ContainsKey(source))
                {
                    GameObject item = Instantiate(_damageStatItemPrefab, _damageStatsContent);
                    _statItems[source] = item;
                }
            }

            // Remove obsolete prefab instances for sources that no longer exist
            var sourcesToRemove = new List<string>();
            foreach (var source in _statItems.Keys)
            {
                if (!damagePerSource.ContainsKey(source)) sourcesToRemove.Add(source);
            }
            foreach (var source in sourcesToRemove)
            {
                Destroy(_statItems[source]);
                _statItems.Remove(source);
            }

            // Update UI for each source
            foreach (var kvp in damagePerSource)
            {
                string source = kvp.Key;
                long damage = kvp.Value;

                GameObject item = _statItems[source];

                // Find icon, label, and slider in prefab
                Transform iconTransform = item.transform.Find("Image");
                Transform labelTransform = item.transform.Find("Label");
                Slider slider = item.GetComponentInChildren<Slider>();

                // Update icon sprite if exists
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                if (icon != null) icon.sprite = PlayerResources.GetUltimateSource(source);

                // Update label text
                TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
                if (label != null) label.text = $"{source}: {damage:N0}";

                // Update slider value
                if (slider != null) slider.value = totalDamage > 0 ? (float)damage / totalDamage : 0;
            }

            // Show panel only if there's damage to display
            _damageStatsPanel.SetActive(totalDamage > 0);
        }

    }
}
