using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// One resource-mutation row in a journal entry.
    ///</summary>
    [Serializable]
    public class CraftJournalOperation
    {
        public Guid CraftTransactionOperationId;
        public string ResourceId;
        public ResourceKind ResourceType = ResourceKind.Material;
        public int Quantity;
        public OperationState State = OperationState.Pending;
    }

    public enum ResourceKind
    {
        Material = 0,
        Catalyst = 1,
        Progression = 2,
        Currency = 3,
    }

    public enum OperationState
    {
        Pending = 0,
        Applied = 1,
        RolledBack = 2,
    }

    public enum CraftJournalPhase
    {
        Prepared = 0,
        Reserved = 1,
        Committed = 2,
        JobPersisted = 3,
        Completed = 4,
        RolledBack = 5,
    }
}
