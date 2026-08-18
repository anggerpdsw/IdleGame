using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Craft transaction journal service (§11.2-11.5, I-15, I-22).
    /// Owns the authoritative state of all in-flight craft transactions.
    /// P0-C: emits decisions via ClassifyReconciliation(); does NOT execute inventory mutations.
    /// P0-D: consumes decisions and calls InventoryService.ApplyReward.
    /// Thread-safe via single lock on all mutations.
    ///</summary>
    public sealed class CraftTransactionJournal
    {
        private readonly Dictionary<Guid, CraftJournalEntry> _entries = new();
        private readonly object _lock = new();

        // ============ Append / Update ============

        /// <summary>
        /// Create a new entry in Prepared phase with given operations.
        /// All operations start as Pending.
        /// Returns the entry's TransactionId for later updates.
        ///</summary>
        public Guid AppendEntry(string jobId, CraftExecutionSnapshot snapshot, CraftJournalOperation[] operations)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentException("jobId required", nameof(jobId));
            if (operations == null) operations = Array.Empty<CraftJournalOperation>();

            var entry = new CraftJournalEntry
            {
                TransactionId = Guid.NewGuid(),
                JobId = jobId,
                Phase = CraftJournalPhase.Prepared,
                ExecutionSnapshot = snapshot,
                Operations = new List<CraftJournalOperation>(operations),
                CreatedAt = DateTime.UtcNow.Ticks
            };

            lock (_lock)
            {
                _entries[entry.TransactionId] = entry;
            }
            return entry.TransactionId;
        }

        /// <summary>
        /// Transition entry phase. Same-phase update is idempotent (no-op).
        /// Throws InvalidOperationException on illegal transition (e.g. Prepared → JobPersisted without Reserved/Committed).
        /// Throws KeyNotFoundException if entryId is unknown.
        ///</summary>
        public void UpdateEntryPhase(Guid entryId, CraftJournalPhase newPhase)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(entryId, out var entry))
                    throw new KeyNotFoundException($"Journal entry {entryId} not found");

                if (entry.Phase == newPhase) return; // idempotent

                if (!IsLegalPhaseTransition(entry.Phase, newPhase))
                    throw new InvalidOperationException(
                        $"Illegal phase transition {entry.Phase} → {newPhase} for entry {entryId}");

                entry.Phase = newPhase;
            }
        }

        /// <summary>
        /// Transition a single operation state. Same-state update is idempotent.
        /// Throws on illegal transitions (e.g. RolledBack → Applied).
        ///</summary>
        public void UpdateOperationState(Guid entryId, Guid operationId, OperationState newState)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(entryId, out var entry))
                    throw new KeyNotFoundException($"Journal entry {entryId} not found");

                var op = entry.Operations.FirstOrDefault(o => o.CraftTransactionOperationId == operationId) ?? throw new KeyNotFoundException(
                        $"Operation {operationId} not found in entry {entryId}");

                if (op.State == newState) return; // idempotent

                if (!IsLegalOperationTransition(op.State, newState))
                    throw new InvalidOperationException(
                        $"Illegal operation state transition {op.State} → {newState} for op {operationId}");

                op.State = newState;
            }
        }

        /// <summary>
        /// Returns a read-only snapshot of operations for an entry.
        /// Returns a fresh List<T>.AsReadOnly() copy so callers cannot mutate journal state
        /// outside the state machine. Throws KeyNotFoundException if entryId is unknown.
        ///</summary>
        public IReadOnlyList<CraftJournalOperation> GetOperations(Guid entryId)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(entryId, out var entry))
                    throw new KeyNotFoundException($"Journal entry {entryId} not found");

                return entry.Operations.ToList().AsReadOnly();
            }
        }

        // ============ Reconciliation Classification (§11.5) ============

        /// <summary>
        /// Pure analysis. Returns a list of decisions for the recovery executor.
        /// Does NOT touch IInventoryService — that is P0-D territory.
        /// Operation state semantics (locked contract for P0-D):
        ///   Pending     = no side effect has occurred
        ///   Applied     = side effect is durable (committed to inventory/economy)
        ///   RolledBack  = side effect has been compensated
        /// Rules (§11.5):
        ///   - phase ∈ {Committed, JobPersisted} → Commit only Pending ops; Applied/RolledBack → Skip
        ///   - phase ∈ {Prepared, Reserved} → Rollback only Applied ops; Pending/RolledBack → Skip
        ///   - phase ∈ {Completed, RolledBack} → terminal, no decisions
        ///</summary>
        public List<ReconciliationDecision> ClassifyReconciliation()
        {
            var decisions = new List<ReconciliationDecision>();
            lock (_lock)
            {
                foreach (var entry in _entries.Values)
                {
                    switch (entry.Phase)
                    {
                        case CraftJournalPhase.Committed:
                        case CraftJournalPhase.JobPersisted:
                            foreach (var op in entry.Operations)
                            {
                                if (op.State == OperationState.Pending)
                                    decisions.Add(DecisionFor(entry, op, ReconciliationAction.Commit));
                                // Applied / RolledBack → Skip (already durably handled or compensated)
                            }
                            break;

                        case CraftJournalPhase.Prepared:
                        case CraftJournalPhase.Reserved:
                            foreach (var op in entry.Operations)
                            {
                                if (op.State == OperationState.Applied)
                                    decisions.Add(DecisionFor(entry, op, ReconciliationAction.Rollback));
                                // Pending / RolledBack → Skip (no side effect to compensate)
                            }
                            break;

                        case CraftJournalPhase.Completed:
                        case CraftJournalPhase.RolledBack:
                            // terminal — nothing to do
                            break;
                    }
                }
            }
            return decisions;
        }

        private static ReconciliationDecision DecisionFor(CraftJournalEntry entry, CraftJournalOperation op, ReconciliationAction action)
        {
            return new ReconciliationDecision
            {
                Action = action,
                EntryId = entry.TransactionId,
                JobId = entry.JobId,
                OperationId = op.CraftTransactionOperationId,
                ResourceType = op.ResourceType,
                ResourceId = op.ResourceId,
                Quantity = op.Quantity
            };
        }

        // ============ Persistence ============

        /// <summary>
        /// Snapshot copy for SaveManager.GatherAllData().
        /// Returns a fresh CraftJournalSaveData; caller may serialize freely.
        ///</summary>
        public CraftJournalSaveData GetSaveData()
        {
            lock (_lock)
            {
                var data = new CraftJournalSaveData();
                foreach (var e in _entries.Values)
                {
                    data.Entries.Add(new CraftJournalEntry
                    {
                        TransactionId = e.TransactionId,
                        JobId = e.JobId,
                        Phase = e.Phase,
                        ExecutionSnapshot = e.ExecutionSnapshot,
                        Operations = new List<CraftJournalOperation>(e.Operations),
                        CreatedAt = e.CreatedAt
                    });
                }
                return data;
            }
        }

        /// <summary>
        /// Load entries from save data. Replaces all in-memory state.
        /// Called by SaveManager.ApplyAllData() after deserialization.
        ///</summary>
        public void LoadFromSaveData(CraftJournalSaveData data)
        {
            lock (_lock)
            {
                _entries.Clear();
                if (data?.Entries == null) return;
                foreach (var e in data.Entries)
                {
                    if (e == null || e.TransactionId == Guid.Empty) continue;
                    _entries[e.TransactionId] = e;
                }
            }
        }

        // ============ State Transition Legality ============

        private static bool IsLegalPhaseTransition(CraftJournalPhase from, CraftJournalPhase to)
        {
            // Forward path
            if (from == CraftJournalPhase.Prepared && to == CraftJournalPhase.Reserved) return true;
            if (from == CraftJournalPhase.Reserved && to == CraftJournalPhase.Committed) return true;
            if (from == CraftJournalPhase.Committed && to == CraftJournalPhase.JobPersisted) return true;
            if (from == CraftJournalPhase.JobPersisted && to == CraftJournalPhase.Completed) return true;

            // Allow rollback from almost any non-terminal state.
            // The recovery classifier is responsible for deciding what to do based on operation states.
            if (to == CraftJournalPhase.RolledBack &&
                (from == CraftJournalPhase.Prepared ||
                 from == CraftJournalPhase.Reserved ||
                 from == CraftJournalPhase.Committed))
                return true;

            return false;
        }

        private static bool IsLegalOperationTransition(OperationState from, OperationState to)
        {
            if (from == OperationState.Pending && to == OperationState.Applied) return true;
            if (from == OperationState.Pending && to == OperationState.RolledBack) return true;
            // Compensation (I-15): Applied → RolledBack during recovery
            if (from == OperationState.Applied && to == OperationState.RolledBack) return true;
            return false;
        }
    }

    /// <summary>
    /// One unit of reconciliation work. Emitted by CraftTransactionJournal.ClassifyReconciliation(),
    /// consumed by the recovery executor (P0-D territory: InventoryService.ApplyReward).
    ///</summary>
    public struct ReconciliationDecision
    {
        public ReconciliationAction Action;
        public Guid EntryId;
        public string JobId;
        public Guid OperationId;
        public ResourceKind ResourceType;
        public string ResourceId;
        public int Quantity;
    }

    public enum ReconciliationAction
    {
        Skip = 0,
        Commit = 1,
        Rollback = 2
    }
}
