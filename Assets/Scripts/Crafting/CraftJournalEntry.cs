using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// One transaction record in the craft journal (§11.3 schema).
    ///</summary>
    [Serializable]
    public class CraftJournalEntry
    {
        public Guid TransactionId;
        public string JobId;
        public CraftJournalPhase Phase = CraftJournalPhase.Prepared;
        public CraftExecutionSnapshot ExecutionSnapshot;
        public List<CraftJournalOperation> Operations = new();
        public long CreatedAt;
    }
}
