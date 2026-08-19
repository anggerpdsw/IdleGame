namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Facade for craft queue persistence.
    /// SaveManager (composition root) gathers/saves via CraftService's public methods.
    /// </summary>
    public sealed class CraftPersistenceService
    {
        private readonly CraftQueueService _queueService;

        public CraftPersistenceService(CraftQueueService queueService)
        {
            _queueService = queueService;
        }

        /// <summary>
        /// Serializes the current queue state for saving.
        /// </summary>
        public CraftQueueSaveData CreateSaveData()
        {
            return _queueService.GetSaveData();
        }

        /// <summary>
        /// Restores the queue from saved state.
        /// Offline progress is automatic via EndTimeUtc comparison (no manual calculation needed).
        /// </summary>
        public void RestoreSaveData(CraftQueueSaveData data)
        {
            _queueService.LoadFromSaveData(data);
        }
    }
}