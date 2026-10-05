using UnityEngine;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Manager;
using System.Collections;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays all player stats in a scrollable panel.
    /// Rows are instantiated once in Awake() and reused — no GC pressure on open/close.
    /// </summary>
    public class PlayerUI : MonoBehaviour
    {
        [Header("Panel Reference")]
        [SerializeField] private GameObject _statusPanel;
        [SerializeField] private Transform _content;
        [SerializeField] private PlayerStatRow _rowPrefab;

        private Player.Player _player;
        private readonly Dictionary<SkillType, PlayerStatRow> _rows = new();

        private void Awake()
        {
            // Start with panel hidden
            if (_statusPanel != null) _statusPanel.SetActive(false);
            BuildRows();
        }

        private void Start()
        {
            _player = Player.Player.Instance;
            // Use a coroutine to wait until essential singletons are initialized.
            StartCoroutine(InitializeUI());
        }

        private IEnumerator InitializeUI()
        {
            yield return new WaitUntil(() => PlayerStatsManager.Instance != null);
            PlayerStatsManager.Instance.OnStatsChanged += RefreshValues;
        }

        // ================================================================
        // ROW POOLING — create once, update forever
        // ================================================================

        /// <summary>
        /// Instantiate one row per SkillType and store references.
        /// Called once in Awake() so the dictionary is ready before any ShowPanel().
        /// </summary>
        private void BuildRows()
        {
            if (_rowPrefab == null || _content == null) return;

            foreach (SkillType stat in Enum.GetValues(typeof(SkillType)))
            {
                PlayerStatRow row = Instantiate(_rowPrefab, _content);
                row.gameObject.name = $"Row_{stat}";
                _rows[stat] = row;
            }
        }

        // ================================================================
        // PUBLIC API — dipakai kode lain kalau perlu (mis. tutorial, deep link)
        // ================================================================

        /// <summary>
        /// Show the status panel and refresh all stat values.
        /// </summary>
        public void ShowPanel()
        {
            if (_statusPanel == null) return;
            _statusPanel.SetActive(true);
        }

        /// <summary>
        /// Hide the status panel.
        /// </summary>
        public void HidePanel()
        {
            if (_statusPanel != null) _statusPanel.SetActive(false);
        }

        // ================================================================
        // REFRESH — just update text, no instantiation
        // ================================================================

        /// <summary>
        /// Update every row with the current stat value from Player.Stats.
        /// Called on ShowPanel() so it always reflects the latest upgrades/buffs.
        /// </summary>
        private void RefreshValues()
        {
            if (_player == null) return;

            foreach (var kvp in _rows)
            {
                float value = PlayerStatsManager.Instance.GetStat(kvp.Key);
                kvp.Value.SetValue(kvp.Key.GetSkillDisplayName(), value);
            }
        }

        // PanelOpener akan memanggil ini via SetActive(true) → OnEnable
        private void OnEnable() => RefreshValues();
        private void OnDisable()
            => PlayerStatsManager.Instance.OnStatsChanged -= RefreshValues;
    }
}
