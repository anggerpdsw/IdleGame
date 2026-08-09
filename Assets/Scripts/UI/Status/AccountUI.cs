using UnityEngine;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Player;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Manager;
using System.Collections;

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
                
        private Coroutine _bindRoutine; 
        
        private void OnEnable() 
        { 
            _bindRoutine = StartCoroutine(BindAccount()); 
        }
        
        private void OnDisable() 
        { 
            if (_bindRoutine != null) 
            { 
                StopCoroutine(_bindRoutine); 
                _bindRoutine = null; 
                } 
                UnbindAccount(); 
        } 

        private IEnumerator BindAccount() { 
            // Wait until AccountManager has been initialized. 
            while (AccountManager.Instance == null) yield return null; 
            var account = AccountManager.Instance; 
            account.OnDataLoaded += RefreshUI; 
            account.OnExpChanged += RefreshUI; 
            account.OnLevelUp += OnLevelUp; 
            // Important: 
            // The save may already have finished loading before 
            // this UI became enabled. 
            RefreshUI(); 
            _bindRoutine = null; 
        } 
        
        private void UnbindAccount() 
        { 
            var account = AccountManager.Instance; 
            if (account == null) return; 
            account.OnDataLoaded -= RefreshUI; 
            account.OnExpChanged -= RefreshUI; 
            account.OnLevelUp -= OnLevelUp; 
        }

        private void RefreshUI()
        {
            var account = AccountManager.Instance;
            if (account == null) return;
            
            if (_level != null)
                _level.text = account.Level.ToString();
            if (_currentExp != null) 
                _currentExp.text = account.CurrentExp.ToString();
            if (_totalExp != null) 
                _totalExp.text = account.TotalExp.ToString();
            if (_requiredExp != null) 
                _requiredExp.text = account.RequiredExp.ToString();
            if (_progressExp != null)
                _progressExp.value = account.Progress;

            if (_progressPercent != null) {
                float percent = Mathf.Min(account.Progress * 100f, 99.9f);
                _progressPercent.text = $"{percent:F1}%";
            }
        }
        
        private void OnLevelUp(int level) => RefreshUI();

    }
}
