using UnityEngine;

namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Simple wrapper for an Ads SDK. Allows the game to request rewarded video, banners, etc.
    /// The stub implementation lives in `Assets/Scripts/Manager/AdsManager.cs`.
    /// </summary>
    public interface IAdsService
    {
        bool IsRewardedAvailable();
        void ShowRewardedVideo(System.Action<bool> onComplete);
        void ShowBanner();
        void HideBanner();
        void RegisterWaveCompletion(int conqueredCount);
        void OnSceneEnter(string sceneName);
    }
}