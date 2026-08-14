using NUnit.Framework;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Core.Interfaces;

namespace IdleDefenseSurvival.Items.Tests
{
    /// <summary>
    /// EditMode tests for craft transaction recovery and idempotency (P0-D).
    /// Tests the CraftTransactionJournal classifier and CraftTransactionService per-op checkpoint behavior.
    /// </summary>
    [TestFixture]
    public class CraftRecoveryTests
    {
        private CraftTransactionJournal _journal;
        private CraftTestMocks.MockInventoryService _inventory;
        private CraftTestMocks.MockEconomyService _economy;
        private CraftTestMocks.MockSaveService _saveService;

        [SetUp]
        public void Setup()
        {
            _journal = new CraftTransactionJournal();
            _inventory = new CraftTestMocks.MockInventoryService();
            _economy = new CraftTestMocks.MockEconomyService();
            _saveService = new CraftTestMocks.MockSaveService();
        }

        private CraftExecutionSnapshot CreateDummySnapshot()
        {
            return new CraftExecutionSnapshot(
                new RecipeSnapshot("test_recipe", 1, "Weapon", 0, Array.Empty<CraftIngredientSnapshot>()),
                new CostSnapshot(Array.Empty<IngredientCost>(), Array.Empty<IngredientCost>(), Array.Empty<IngredientCost>(), new CurrencySnapshot()),
                new CraftContextSnapshot(1, 1),
                null,
                1);
        }

        [Test]
        public void Commit_Op1Success_Op2Failure_Op1NotReexecuted()
        {
            // Setup: Journal entry at Committed phase with 2 Pending operations
            var op1Id = Guid.NewGuid();
            var op2Id = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = op1Id, State = OperationState.Pending, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 },
                new CraftJournalOperation { CraftTransactionOperationId = op2Id, State = OperationState.Pending, ResourceType = ResourceKind.Material, ResourceId = "gold_ore", Quantity = 5 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Reserved);
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Committed);

            // Mock: Op1 remove succeeds, Op2 remove fails (simulates mid-commit failure)
            _inventory.RemoveItemByIdResults["iron"] = 3;   // success
            _inventory.RemoveItemByIdResults["gold_ore"] = -1; // failure indicator (throw)

            var service = new CraftTransactionService(_inventory, _economy, _journal, _saveService);
            // Inject journal entry ID via reflection (private field)
            var field = typeof(CraftTransactionService).GetField("_journalEntryId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(service, entryId);

            // Execute commit - should throw on op2 failure
            Assert.Throws<InvalidOperationException>(() => service.Commit());

            // Verify: Op1 was Applied and persisted, Op2 remains Pending
            var ops = _journal.GetOperations(entryId);
            Assert.AreEqual(OperationState.Applied, ops[0].State, "Op1 should be Applied after successful mutation");
            Assert.AreEqual(OperationState.Pending, ops[1].State, "Op2 should remain Pending after failure");

            // Verify: Op1 mutation executed exactly once (idempotent on retry)
            Assert.AreEqual(1, _inventory.RemoveCallCount["iron"], "Op1 mutation should execute exactly once");
            // Op2 attempt is made (mock increments counter before throw), so count = 1
            Assert.AreEqual(1, _inventory.RemoveCallCount.ContainsKey("gold_ore") ? _inventory.RemoveCallCount["gold_ore"] : 0, "Op2 mutation attempted but failed");
        }

        [Test]
        public void Recovery_PendingToApplied_CommittedPhase()
        {
            // Setup: Journal at Committed phase, one Pending operation
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.Pending, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Reserved);
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Committed);

            // Classifier should emit Commit decision for Pending op in Committed phase
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(1, decisions.Count, "Should produce one Commit decision");
            Assert.AreEqual(ReconciliationAction.Commit, decisions[0].Action);
            Assert.AreEqual(entryId, decisions[0].EntryId);
            Assert.AreEqual(opId, decisions[0].OperationId);
            Assert.AreEqual(ResourceKind.Material, decisions[0].ResourceType);
            Assert.AreEqual("iron", decisions[0].ResourceId);
            Assert.AreEqual(3, decisions[0].Quantity);
        }

        [Test]
        public void Recovery_AppliedIdempotent_CommittedPhase()
        {
            // Setup: Journal at Committed phase, operation already Applied
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.Applied, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Reserved);
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Committed);

            // Classifier should SKIP already-Applied operations (idempotent)
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(0, decisions.Count, "Should skip already-Applied operations - no duplicate consumption");
        }

        [Test]
        public void Recovery_RollbackApplied_PreparedPhase()
        {
            // Setup: Journal at Prepared phase, operation marked Applied (crash after mutation but before Reserved)
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.Applied, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Prepared);

            // Classifier should emit Rollback for Applied op in Prepared phase
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(1, decisions.Count, "Should produce one Rollback decision");
            Assert.AreEqual(ReconciliationAction.Rollback, decisions[0].Action);
            Assert.AreEqual(entryId, decisions[0].EntryId);
            Assert.AreEqual(opId, decisions[0].OperationId);
        }

        [Test]
        public void Recovery_RollbackApplied_ReservedPhase()
        {
            // Setup: Journal at Reserved phase, operation marked Applied (crash after mutation, before Committed)
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.Applied, ResourceType = ResourceKind.Currency, ResourceId = "Gold", Quantity = 100 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Reserved);

            // Classifier should emit Rollback for Applied op in Reserved phase
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(1, decisions.Count, "Should produce one Rollback decision");
            Assert.AreEqual(ReconciliationAction.Rollback, decisions[0].Action);
            Assert.AreEqual(entryId, decisions[0].EntryId);
            Assert.AreEqual(opId, decisions[0].OperationId);
            Assert.AreEqual(ResourceKind.Currency, decisions[0].ResourceType);
            Assert.AreEqual("Gold", decisions[0].ResourceId);
        }

        [Test]
        public void Recovery_SkipPendingInPreparedPhase()
        {
            // Setup: Journal at Prepared phase, operation still Pending (normal state, no mutation occurred)
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.Pending, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Prepared);

            // Classifier should SKIP Pending ops in Prepared/Reserved (no side effect to compensate)
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(0, decisions.Count, "Should skip Pending operations in Prepared phase - nothing to rollback");
        }

        [Test]
        public void Recovery_SkipRolledBackInCommittedPhase()
        {
            // Setup: Journal at Committed phase, operation already RolledBack (compensated)
            var opId = Guid.NewGuid();
            var entryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = opId, State = OperationState.RolledBack, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Reserved);
            _journal.UpdateEntryPhase(entryId, CraftJournalPhase.Committed);

            // Classifier should SKIP RolledBack operations (already compensated)
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(0, decisions.Count, "Should skip RolledBack operations - already compensated");
        }

        [Test]
        public void Recovery_TerminalPhases_NoDecisions()
        {
            // Setup: Entries in Completed and RolledBack phases
            var completedEntryId = _journal.AppendEntry("job1", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = Guid.NewGuid(), State = OperationState.Applied, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(completedEntryId, CraftJournalPhase.Reserved);
            _journal.UpdateEntryPhase(completedEntryId, CraftJournalPhase.Committed);
            _journal.UpdateEntryPhase(completedEntryId, CraftJournalPhase.JobPersisted);
            _journal.UpdateEntryPhase(completedEntryId, CraftJournalPhase.Completed);

            var rolledBackEntryId = _journal.AppendEntry("job2", CreateDummySnapshot(), new[]
            {
                new CraftJournalOperation { CraftTransactionOperationId = Guid.NewGuid(), State = OperationState.Pending, ResourceType = ResourceKind.Material, ResourceId = "iron", Quantity = 3 }
            });
            _journal.UpdateEntryPhase(rolledBackEntryId, CraftJournalPhase.RolledBack);

            // Classifier should produce NO decisions for terminal phases
            var decisions = _journal.ClassifyReconciliation();

            Assert.AreEqual(0, decisions.Count, "Terminal phases (Completed, RolledBack) should produce no decisions");
        }
    }
}