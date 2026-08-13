using UnityEngine;
using IdleDefenseSurvival.Core.Interfaces;
using System;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Advertising manager (previously AdsManager) – now renamed for clarity.
    /// Handles rewarded video, banner ads, and ad-based events.
    /// Can be replaced with a real SDK later.
    /// </summary>
    public class AdvertisingManager : MonoBehaviour, IAdsService
    {
        private static AdvertisingManager _instance;
        public static AdvertisingManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
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

        #region Public API

        /// <summary>
        /// Check if a rewarded video is available for the current level.
        /// </summary>
        public bool IsRewardedAdAvailable()
        {
            return false;
        }

        /// <summary>
        /// Show a rewarded video. Invoke onComplete with true if the user
        /// watched fully (in a real implementation) or false on any error.
        /// </summary>
        public void ShowRewardedAd(System.Action<bool> onComplete)
        {
            // Simulate an immediate completion (no real ad).
            onComplete?.Invoke(false);
        }

        /// <summary>
        /// Request a banner ad to be displayed. This is a stub.
        /// </summary>
        public void ShowBanner()
        {
            // No-op – implement when real ad SDK is integrated.
        }

        /// <summary>
        /// Hide any currently visible banner. Stub implementation.
        /// </summary>
        public void HideBanner()
        {
            // No-op.
        }

        /// <summary>
        /// Called during unit tests or clean‑up at shutdown.
        /// </summary>
        public void Cleanup()
        {
            // No-op.
        }

        public bool IsRewardedAvailable()
        {
            throw new NotImplementedException();
        }

        public void ShowRewardedVideo(Action<bool> onComplete)
        {
            throw new NotImplementedException();
        }

        public void RegisterWaveCompletion(int conqueredCount)
        {
            throw new NotImplementedException();
        }

        public void OnSceneEnter(string sceneName)
        {
            throw new NotImplementedException();
        }

        #endregion

        // Additional public methods to bridge into existing AdsManager behavior –
        // in case other code references AdsManager.Instance. This provides a backward‑compatible layer.
    }
}