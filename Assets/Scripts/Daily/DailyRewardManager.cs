using System;
using System.Collections;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    public class DailyRewardManager : MonoBehaviour
    {
        private static DailyRewardManager _instance;
        public static DailyRewardManager Instance => _instance;

        public static event Action OnRewardClaimed;
        public static event Action OnStreakReset;
        public static event Action OnInitialized;
        public static event Action<bool> OnClaimableStateChanged;
        private bool _lastClaimable;
        private Coroutine _claimableRoutine;

        private DailyRewardService _service = new();


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
            OnRewardClaimed = null;
            OnStreakReset = null;
            OnInitialized = null;
            OnClaimableStateChanged = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeInternal()
        {
            var saveManager = SaveManager.Instance;
            var existingSave = saveManager != null ? saveManager.GetDailyRewardData() : null;
            _service.Initialize(DateTime.UtcNow, existingSave);

            OnInitialized?.Invoke();
            
            if (_claimableRoutine != null) StopCoroutine(_claimableRoutine);
            _claimableRoutine = StartCoroutine(ClaimableWatcher());
            NotifyClaimableState();
        }

        public DailyRewardService Service => _service;

        public bool TryClaimCurrentReward(DateTime utcNow)
        {
            if (!_service.ClaimCurrentReward(utcNow)) return false;
            SaveManager.Instance?.SaveDailyRewardData(_service.SaveState());
            NotifyClaimableState();
            OnRewardClaimed?.Invoke();
            return true;
        }

        public void HandleDailyReset(DateTime utcNow)
        {
            _service.EnsureReset(utcNow);
            SaveManager.Instance?.SaveDailyRewardData(_service.SaveState());
            OnStreakReset?.Invoke();
            NotifyClaimableState();
        }

        public void RefreshFromSave(DateTime utcNow)
        {
            var data = SaveManager.Instance?.GetDailyRewardData();
            _service.Initialize(utcNow, data);
            SaveManager.Instance?.SaveDailyRewardData(_service.SaveState());
            NotifyClaimableState();
        }

        private IEnumerator ClaimableWatcher()
        {
            var wait = new WaitForSeconds(1);

            while (true)
            {
                NotifyClaimableState();
                yield return wait;
            }
        }
        
        private void NotifyClaimableState()
        {
            bool current = _service.HasClaimableReward;
            if (current == _lastClaimable) return;

            _lastClaimable = current;
            OnClaimableStateChanged?.Invoke(current);
        }

        private void OnEnable() => SaveManager.OnSaveLoaded += InitializeInternal;

        private void OnDisable() => SaveManager.OnSaveLoaded -= InitializeInternal;
    }
}
