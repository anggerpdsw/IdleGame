namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Contract for the global save/load service.
    /// Allows the rest of the game to request persistence without knowing the file format.
    /// </summary>
    public interface ISaveService
    {
        void SaveAll();
        void LoadAll();
        void DeleteAll();

        // P0-C: gather fresh runtime state (including journal) and write durably.
        // Used by CraftTransactionService at each checkpoint — prevents stale SaveData propagation.
        // Throws IOException on filesystem failure so the caller can react.
        void PersistCurrentStateDurably();

        // Wave progress API
        // Current tier concept removed – tier selection handled by MainMenu and WaveManager.

        int GetHighestWave(int tier);
        void UpdateHighestWave(int tier, int wave);
        bool IsTierUnlocked(int tier);
        // Removed GetAllTierProgress per design – tiers are managed internally.


        void RecordEnemyKill(string enemyId, string damageSource, string role);

        bool HasReachedDailyGemLimit();
        int GetRemainingDailyGems();
        int RecordGemDrop(int gemCount);
        int GetTodaysGemEarnings();
        void ResetDailyGemCounter();

        void SetAutoCollect(bool enabled);
        bool IsAutoCollectEnabled();
        void SetMaxSpeed(bool enabled);
        bool IsMaxSpeedEnabled();

        void AddSpending(CurrencyType type, long amount);
        void AddEarn(CurrencyType type, long amount);
    }
}