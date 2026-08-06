using UnityEngine;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Player;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays all player stats in a scrollable panel.
    /// Rows are instantiated once in Awake() and reused — no GC pressure on open/close.
    /// </summary>
    public class AccountUI : MonoBehaviour
    {
        [Header("Panel Reference")]
        [SerializeField] private TextMeshProUGUI _level;
        [SerializeField] private TextMeshProUGUI _currentExp;
        [SerializeField] private TextMeshProUGUI _totalExp;
        [SerializeField] private TextMeshProUGUI _requiredExp;
        [SerializeField] private Slider _progressExp;
        [SerializeField] private TextMeshProUGUI _progressPercent;

        private void Start()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            var account = AccountManager.Instance;
            if (account == null) return;

            _level.text         = account.Level.ToString();
            if (_currentExp != null) 
                _currentExp.text    = account.CurrentExp.ToString();
            if (_totalExp != null) 
                _totalExp.text      = account.TotalExp.ToString();
            if (_requiredExp != null) 
                _requiredExp.text   = account.RequiredExp.ToString();
            _progressExp.value      = account.Progress;

            float percent = Mathf.Min(account.Progress * 100f, 99.9f);
            _progressPercent.text = $"{percent:F1}%";
        }
        
        private void OnEnable()
        {
            // Subscribe to save loaded event
            SaveManager.OnSaveLoaded += OnSaveLoaded;

            AccountManager.Instance.OnExpChanged += RefreshUI;
            AccountManager.Instance.OnLevelUp += OnLevelUp;

            RefreshUI();
        }

        private void OnDisable()
        {
            SaveManager.OnSaveLoaded -= OnSaveLoaded;
            
            AccountManager.Instance.OnExpChanged -= RefreshUI;
            AccountManager.Instance.OnLevelUp -= OnLevelUp;
        }

        private void OnSaveLoaded()
        {
            RefreshUI();
        }

        private void OnLevelUp(int level)
        {
            RefreshUI();
        }

    }
}
