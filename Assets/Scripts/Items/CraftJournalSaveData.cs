using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Root container for craft transaction journal entries.
    /// Embedded in SaveData.craftJournal.
    ///</summary>
    [Serializable]
    public class CraftJournalSaveData
    {
        public List<CraftJournalEntry> Entries = new();

        public static CraftJournalSaveData Empty => new();
    }
}
