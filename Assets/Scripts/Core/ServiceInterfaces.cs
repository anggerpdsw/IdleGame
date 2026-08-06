using UnityEngine;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Simple interface definitions for the core services used throughout the game.
    /// Placing them under Core keeps the dependency direction from systems → interfaces.
    /// </summary>
    public interface ISaveService
    {
        void SaveAll();
        void LoadAll();
        void DeleteAll();
    }

    public interface IEconomyService
    {
        long GetCurrency(CurrencyType type);
        void AddCurrency(CurrencyType type, long amount, string reason = "");
        bool TrySpendCurrency(CurrencyType type, long amount, string reason = "");
        bool HasEnoughCurrency(CurrencyType type, long amount);
    }

    public interface IUpgradeService
    {
        int GetSkillLevel(string skillId);
        bool CanUpgrade(string skillId);
        bool UpgradeSkill(string skillId, string reason = "");
        bool UnlockSkill(string skillId);
        bool IsSkillLocked(string skillId);
    }

    public interface IAudioService
    {
        void PlayMusic(AudioClip clip, bool loop = true);
        void PlaySfx(AudioClip clip, Vector3 position, float volumeScale = 1f);
        void SetVolumes(float master, float music, float sfx);
    }

    public interface IAdsService
    {
        bool IsRewardedAdAvailable();
        void ShowRewardedAd(System.Action<bool> onComplete);
        void ShowBanner();
        void HideBanner();
    }

    public interface IAnalyticsService
    {
        void LogEvent(string name, System.Collections.Generic.IDictionary<string, object> parameters = null);
        void RecordLevelComplete(float playtime, int wavesCleared, int finalWave);
        void RecordUpgradePurchase(string skillId, int level, long cost);
        void RecordEnemyKill(string enemyId, string damageSource, string role);
        void SetUserProperty(string key, object value);
    }
}

