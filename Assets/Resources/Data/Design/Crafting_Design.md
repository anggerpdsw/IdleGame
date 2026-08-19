# Crafting Design — Equipment Crafting & Recipe System

> **Status:** v3.7 (P0-D Completion Idempotency/Recovery — IMPLEMENTED & VERIFIED: Two-Phase Completion, Idempotent Rewards, Startup Recovery Executor)
>
> **v3.3 corrections from v3.2 — 15 mandatory fixes:**
> 1. **Durable persistence:** `MarkDirty()` deprecated for craft. `PersistDurably()` contract added. I-17 added.
> 2. **I-15 weakened:** journal reflects durable transitions; inventory is authoritative; recovery reconciles.
> 3. **Reconciliation protocol:** journal ↔ inventory reconciliation defined for recovery.
> 4. **Operation ID nomenclatura:** `CraftTransactionOperationId` (Guid) vs `RewardOperationId` (string) named explicitly.
> 5. **Duplicate RecipeVersion:** `CraftJob.RecipeVersion` removed; derived from `RecipeSnapshot.RecipeVersion`.
> 6. **I-11 simplified:** input list reduced to `CraftExecutionSnapshot + CompletionSeed`.
> 7. **Primary/Secondary removed entirely:** §7, §7.1, §7.2 rewritten without the concept.
> 8. **Tier rule data-driven:** rule per `Role = Material` ingredient, no positional semantics.
> 9. **Special material semantics:** `Role = Material`. Adhesive-family slot → adhesive tier rule.
> 10. **Decomposed table explicit:** no `...` placeholders.
> 11. **Migration corrected:** legacy Complete jobs trusted, no replay, no window reconstruction.
> 12. **I-20 added:** `RewardPendingCommit` implies durable seed + Results.
> 13. **Failed semantics:** defined — post-commit only, no refund by default, no rewards, no replay.
> 14. **I-19 added:** `CraftContextSnapshot` value-only contract.
> 15. **Pruning rule tightened:** I-18 + complete conditions.
>
> **v3.4 changes:** seed-at-snapshot (I-21), RewardPendingCommit strict invariant (I-20), reconstruction protocol (§11.5/I-22), snapshot ownership (§15.5 — journal=durable, CraftJobSaveData=cache), test legend integrity.
>
> **v3.5 changes:** P0-A shipped (5 DTOs + CraftJob + CraftJobSaveData + persistence symmetry); DecompositionTests [VERIFIED] 16/16 green; dual-source migration documented (IngredientsSnapshot[] legacy + ExecutionSnapshot.Cost new, mitigation in P0-B).
>
> **v3.6 changes (P0-C Transaction Durability — IMPLEMENTED):**
> 1. **Commit per-operation checkpointing** (`CraftTransactionService.Commit()`): each Pending op mutates (Material/Catalyst/Progression via `RemoveItemById` exact `!=`-throw; Currency via `Enum.TryParse`+`TrySpendCurrency`, NO `HasEnoughCurrency` precheck) → `OperationState.Applied` → `PersistCurrentStateDurably()`. Sets `_committed=true` at end; Phase NOT set to Committed inside `Commit()` (CraftService owns that transition).
> 2. **Journal-aware `Rollback()`**: marks `CraftJournalPhase.RolledBack` + persists (try/caught, logs but proceeds), clears RAM reservations; does NOT compensate already-Applied ops (P0-D LOCKED).
> 3. **`CraftTransactionJournal` state machine**: `AppendEntry` (Prepared, ops Pending) → `UpdateEntryPhase`/`UpdateOperationState` (idempotent + legal-transition guards) → `ClassifyReconciliation()` (pure decision emitter, does NOT mutate inventory). Forward path Prepared→Reserved→Committed→JobPersisted→Completed; Committed→RolledBack allowed; Applied→RolledBack (compensation) allowed. Persistence via SaveManager.
> 4. **SaveManager durability**: `PersistDurably()` atomic temp+`File.Replace`; `PersistCurrentStateDurably()` thin wrapper; load-fail catch calls `NotifySaveLoaded()` (no save-brick); `GatherAllData` serializes journal, `ApplyAllData` restores, `UpgradeSave` defaults `craftJournal ??= new CraftJournalSaveData()`.
> 5. **CraftService canonical start lifecycle**: Validate → Build snapshot (`CraftSnapshotBuilder.Build` + `_rollService.RngProvider`) → `CraftJob.Create` → `BeginTransaction` (4-param: reserve + Prepared + persist) → `EnqueueJob` (strict) → Reserved + persist → `Commit` (per-op, try/catch) → Committed + persist → `TryStartNextJob`. Legacy 2-param `BeginTransaction` retained.
> 6. **`StartBatchCraft`**: loops `StartCraft(recipeId,1)`; each JobId independent; `EnqueueJob` strict per-job (one failure does not illegally enqueue others).
>
> **v3.7 changes (P0-D Completion Idempotency/Recovery — IMPLEMENTED & VERIFIED):**
> 1. **InventoryService.ApplyReward + ActiveTransactionWindow**: `ApplyReward(InventoryItem item, string rewardOperationId)` returns `ApplyResult` (Success/AlreadyApplied/Failure). Idempotency key = `"{JobId}#{rewardIndex}"`. Applied operation IDs persisted in `InventorySaveData.AppliedRewardOperationIds[]`. `HasAppliedOperation(rewardOperationId)` guard prevents duplicate mutations across crashes/restarts.
> 2. **Two-phase CraftCompletionService**: Phase A — `RewardPendingCommit` + durably persist `Results` + `CompletionSeed` (I-12, I-17, I-20). Phase B — iterate rewards, call `ApplyReward` per reward with idempotency key, persist after each. On partial failure: job stays `RewardPendingCommit` for recovery. On all success: `Complete` + persist.
> 3. **Recovery executor at startup** (`CraftService.RunTransactionRecovery`): consumes `CraftTransactionJournal.ClassifyReconciliation()` pure decisions. For `Commit` (phase Committed/JobPersisted + Pending ops): execute resource consumption (items via `RemoveItemById`, currency via `TrySpendCurrency`) → mark op `Applied` → if all ops terminal → advance phase to `JobPersisted`. For `Rollback` (phase Prepared/Reserved + Applied ops): refund resources (items via `AddItemInstance`, currency via `AddCurrency`) → mark op `RolledBack` → if all ops `RolledBack` → advance phase to `RolledBack`. Persists after each decision.
> 4. **Journal phase progression now complete**: `StartCraft` ends at `Committed`; `RunTransactionRecovery` advances `Committed`→`JobPersisted` for pending ops; `CraftCompletionService.Complete` advances `JobPersisted`→`Completed` on full success. I-18 pruning ready (entries reach `Completed`).
> 5. **Compile + regression tests VERIFIED**: Unity 0 CS errors confirmed. Phase 9 EditMode tests 23/23 green (successful multi-op commit, partial failure, Applied state, pending handling, currency failure, exact removal failure, rollback state, journal persistence, crash/reload recovery, canonical CraftService flow, enqueue-only behavior, legacy overload compat).
>
> **v3.8 changes (P0-E Equipment Attribute Generation — DESIGNED):**
> 1. **Rarity source of truth enforced:** crafted equipment rarity = `CraftRecipeData.Rarity` (1..6). KNOWN BUG: `CraftRewardService.GenerateEquipmentFromBase` reads `recipe.RequiredTier` (CraftRewardService.cs:94) — must be corrected to `recipe.Rarity`.
> 2. **Attribute enum:** reuse `MainAttribute` (Enumku.cs:20). NO new `EquipmentAttribute`.
> 3. **Tier config:** central `AttributeRolls` table (MaxRolls/MinValue/MaxValue per rarity 1..6) in `dataConfigCrafting.json`.
> 4. **Roll service:** `AttributeRollService` — MaxRolls rolls, random MainAttribute + value, aggregate via `Dictionary<MainAttribute,int>`, store `CustomData["AttributeStats"]`.
> 5. **Rebuild chain:** 3 lossy rebuild points (ToInventoryItem / FromInventoryItems / Complete Phase B) must carry `CustomData` or attributes (and existing secondaries/affixes) die before ApplyReward.
> 6. **Determinism:** `CompletionSeed` must resolve BEFORE roll (currently after, CraftCompletionService.cs:83-88) to honor I-11.

---

## 1. Purpose

Define production contract for equipment crafting. Lock atomicity, idempotency, snapshot semantics, and durable persistence so crafting cannot lose resources, duplicate rewards, or fail to recover from crash.

---

## 2. Scope

In scope: 11 slots × 6 rarities = 66 recipes. 39 regular + Water + 5 decomposed (validation output, I-13). Queue lifecycle + persistence. 6-layer validation.

Out of scope: recipe discovery, recipe modification, cross-slot substitution, stack discount, procedural generation.

---

## 3. Terminology

| Term | Definition |
|---|---|
| **Category** | Broad inventory classification. |
| **Role** | Crafting participation: `Material` / `Catalyst` / `Progression`. |
| **CraftingFamily** | Material identity: Stone / Wood / Thread / Leather / Coal / Metal / Adhesive / Special / Water. |
| **CraftingTier** | Progression tier for `Role = Material`. Ignored for Catalyst/Progression. |
| **EquipmentType** | One of 11 slot identifiers. Stable. |
| **Rarity** | 1..6. Drives decomposed gate + water quantity. |
| **RecipeSnapshot** | Immutable recipe data captured at `StartCraft`. Detached from live repository. |
| **CraftExecutionSnapshot** | Immutable per-job execution state. See §10. |
| **CraftContextSnapshot** | Frozen value-only subset of player state read by roll/reward. |
| **RewardOperationId** | Deterministic `"{JobId}#{rewardIndex}"`. Inventory-side idempotency. |
| **CraftTransactionOperationId** | Guid identifying one resource operation within a craft transaction. |
| **ActiveTransactionWindow** | Inventory-side bounded set of applied reward operation IDs. |
| **PersistDurably** | Synchronous write that returns only after data is on disk. See §11.0. |

---

## 4. Invariants

```
I-1   A CraftJob MUST have exactly one RecipeId.
I-2   A CraftJob MUST use an immutable CraftExecutionSnapshot.
I-3   Recipe data changes MUST NOT alter an active CraftJob.
I-4   RNG MUST execute at most once per CraftJob.
I-5   Once durably persisted, CraftResultData IS the authoritative completion state.
I-6   A reward operation MUST be idempotent under RewardOperationId.
I-7   A completed CraftJob MUST NOT consume resources again.
I-8   A cancelled CraftJob MUST NOT produce rewards.
I-9   A CraftJob MUST NOT reach Complete without durably persisted Results.
I-10  Recovery MUST NOT depend on current player crafting state.
      Snapshot fallback to live context IS a recovery failure.
I-11  Given identical CraftExecutionSnapshot and CompletionSeed,
      RollCraft MUST produce identical results.
I-12  CraftResultData MUST be durably persisted before any reward inventory mutation.
I-13  Every recipe ingredient MUST resolve to exactly one crafting Role.
I-14  Progression items MUST NOT participate in material-family composition validation.
I-15  A journal Operation MUST transition to Applied only after the
      corresponding inventory mutation has been durably persisted.
      A journal Operation in RolledBack MUST NOT be treated as Applied during recovery.
      Recovery MUST reconcile journal state against idempotent inventory operation state.
      Inventory is authoritative; journal is recovery checkpoint.
I-16  CraftExecutionSnapshot.RecipeSnapshot MUST contain every recipe field
      read by execution. Roll MUST NOT query the live repository.
I-17  A reward inventory mutation MUST NOT occur until CraftJob Results
      and CompletionSeed have been durably persisted.
I-18  A reward operation record MUST NOT be pruned while its CraftJob
      or transaction journal can still trigger reward replay.
      Pruning requires: Job.Status == Complete AND no journal entry for JobId
      AND last persist checkpoint succeeded.
I-19  CraftContextSnapshot MUST contain only serialized value data.
      It MUST NOT contain runtime object references, service references,
      or live repository references.
I-20  A CraftJob in RewardPendingCommit MUST have a non-null CompletionSeed
      and durably persisted CraftResultData.
```

---

## 5. Equipment & Rarity Matrix

11 × 6 = 66 recipes.

[VERIFIED] Loaded from 11 per-slot JSON files via `CraftRecipeRepository.LoadRecipesFromJson()`.

### 5.1 Rarity ↔ Decomposed Mapping

| Rarity | Requirement |
|---|---|
| R1 Common | None |
| R2 Rare | decomposed_common = 1 |
| R3 Epic | decomposed_common = 2, decomposed_rare = 1 |
| R4 Legendary | decomposed_common = 3, decomposed_rare = 2, decomposed_epic = 1 |
| R5 Mythic | decomposed_common = 4, decomposed_rare = 3, decomposed_epic = 2, decomposed_legendary = 1 |
| R6 Divine | decomposed_common = 5, decomposed_rare = 4, decomposed_epic = 3, decomposed_legendary = 2, decomposed_mythic = 1 |

Enforced by `DecomposedRequirementResolver.Compute(rarity)`.

---

## 6. Material Taxonomy

| Class | Count | Role |
|---|---|---|
| Regular | 39 (validation output, I-13) | Material |
| Catalyst | 1 (water) | Catalyst |
| Progression | 5 | Progression |

[VERIFIED] Water: `Role = Catalyst`, `CraftingTier = 0`, `CraftingFamily = Water`.

[VERIFIED] 5 decomposed: `Role = Progression`, `CraftingTier = 0`.

### 6.1 Special

```
Role            = Material
CraftingFamily  = Special
CraftingTier    = 6
Allowed only for R6 recipes
Excluded from per-ingredient tier cap (its tier IS the cap)
If used in an Adhesive-family slot, follows adhesive tier rule
```

### 6.2 Water

Mandatory per recipe. Runtime reads `Ingredients[].Quantity` from JSON. Baseline table is validator reference (Warning if |JSON − baseline| / baseline > 0.20).

---

## 7. Equipment Material Composition Rules

```json
{
  "EquipmentType": "Earring",
  "RequiredGroups": [
    ["Stone"],
    ["Metal", "Thread"]
  ],
  "OptionalFamilies": [],
  "ForbiddenFamilies": []
}
```

`RequiredGroups` = AND-of-ORs. All groups must match; each group matches if any listed family appears (excluding Catalyst/Progression per I-14).

| Slot | RequiredGroups | Optional | Forbidden |
|---|---|---|---|
| Hat | `[Thread]` | — | Metal |
| Gloves | `[Leather], [Thread]` | — | — |
| Cape | `[Thread]` | Adhesive | — |
| Armor | `[Leather], [Metal]` | Coal | Wood |
| Belt | `[Leather], [Metal]` | — | — |
| Pants | `[Leather], [Thread]` | — | — |
| Pendant | `[Stone], [Metal]` | — | — |
| Ring | `[Metal], [Stone]` | — | — |
| **Earring** | `[Stone], [Metal, Thread]` | — | — |
| Bracelet | `[Metal], [Stone]` | — | — |
| Shoes | `[Leather], [Wood]` | — | — |

### 7.1 Group Semantics

There is no Primary/Secondary concept. `RequiredGroups` are ordered only for deterministic presentation. Validation is exclusively AND-of-ORs. Group order MUST NOT affect crafting validity or output.

### 7.2 MaximumAllowedMaterialTier (per ingredient)

```
Every Role = Material ingredient:
    CraftingTier ≤ Recipe.Rarity

Adhesive-family ingredient:
    CraftingTier ≤ Recipe.Rarity
    OR CraftingTier ≤ Recipe.Rarity - 1 (adhesive exception)

Catalyst:
    ignored (CraftingTier = 0)

Progression:
    validated exclusively by DecomposedRequirementResolver (§5.1)

Special:
    R6 only (§6.1)
```

---

## 8. Recipe Data Contract

### 8.1 Schema (runtime)

```json
{
  "SchemaVersion": 1,
  "Recipes": [
    {
      "Id": "craft_leather_armor_r1",
      "Name": "Leather Armor",
      "EquipmentType": "Armor",
      "Rarity": 1,
      "RecipeVersion": 1,
      "Ingredients": [
        { "ItemId": "leather",       "Quantity": 8 },
        { "ItemId": "cotton_thread", "Quantity": 3 },
        { "ItemId": "coal",          "Quantity": 3 },
        { "ItemId": "organic_glue",  "Quantity": 2 },
        { "ItemId": "water",         "Quantity": 10 }
      ]
    }
  ]
}
```

Removed: `CraftingProfile`, `Category`.

### 8.2 RollConfiguration

[DESIGNED] Optional per-recipe roll config (quality weights, affix table id, bonus cap). Lives on `RecipeSnapshot`, never read live (I-16).

---

## 9. Validation Contract

[VERIFIED] 6-layer CLI:

```
Items → resolve with CraftingTier/Family/Role
Design → RequiredGroups + MaxAllowedMaterialTier
Economy → sink coverage
R6 Special → ≥1 Special-family ingredient
Water Catalyst → water present
Monotonic Cost → weighted_cost(R1) < ... < R6 per slot
```

### 9.1 Validator Architecture

```
CraftRecipeValidator (orchestrator)
├── ItemReferenceValidator
├── EquipmentCompositionValidator
├── EconomyValidator
├── R6SpecialValidator
├── WaterCatalystValidator
└── MonotonicCostValidator
```

---

## 10. Craft Execution Snapshot

### 10.1 Structure

```
CraftExecutionSnapshot
├── RecipeSnapshot                (immutable, I-16)
│   ├── RecipeId
│   ├── RecipeVersion
│   ├── EquipmentType
│   ├── Rarity
│   ├── Ingredients[]             (per-unit quantities)
│   └── RollConfiguration         (optional)
├── CostSnapshot                  (scaled by CraftCount)
│   ├── Materials[]
│   ├── Catalysts[]
│   ├── Progression[]             (BLOCKED on resolver)
│   └── Currency
│       ├── GoldSnapshot
│       ├── GemSnapshot
│       └── AdditionalCostsSnapshot[]
├── CraftContextSnapshot          (value-only, I-19)
├── CompletionSeed                (long?, §10.4)
└── CraftCount
```

### 10.2 Field Status

| Field | Status |
|---|---|
| `IngredientsSnapshot[]` | [VERIFIED] |
| `RecipeVersion` | [DESIGNED — P0] (on RecipeSnapshot) |
| `RecipeSnapshot` (root, immutable) | [DESIGNED — P0] |
| `CraftContextSnapshot` | [DESIGNED] |
| `CompletionSeed` (`long?`) | [DESIGNED] |
| `CurrencySnapshot` | [DESIGNED] |
| `DecomposedRequirementsSnapshot[]` | [BLOCKED] |

### 10.3 CraftContextSnapshot (value-only, I-19)

```
CraftContextSnapshot
├── PlayerLevel              (int)
├── ModifierVersion          (int)
├── CraftingModifierValues[] (float, with source-tag string)
├── RelevantStatValues[]     (float, with stat-id string)
└── RelevantCardModifierValues[] (float, with card-id string)
```

No runtime references. No service references. No `UnityEngine.Object`. No live repository refs.

### 10.4 CompletionSeed (Nullable)

```
long? CompletionSeed
  null  → not generated
  value → immutable, used by RollCraft
```

Sentinel `0` BANNED. Persisted before any reward mutation (I-12, I-17).

### 10.5 Snapshot Capture Point

After `Validate`, before any resource mutation. Immutable thereafter.

---

## 11. Craft Start Transaction

### 11.0 PersistDurably Contract

```
SaveManager.PersistDurably():
  - Synchronous
  - Returns ONLY after data is on disk
  - Throws on IO failure
  - Caller treats post-return state as durable
```

`MarkDirty()` is deprecated for craft. All craft state transitions use `PersistDurably()`.

### 11.1 CURRENT — Best-Effort `[!] Transitional`

```
1. Validate
2. Reserve (in-memory)
3. Consume
4. Create CraftJob
5. Persist (SaveManager — independent call)
```

Crash between 3 and 5 = orphan consumption. Reservation is coordination, not durability.

### 11.2 TARGET — Atomic Craft Start [P0-C]

```
1. Validate
2. Build CraftExecutionSnapshot
3. SaveManager.PersistDurably(snapshot)            ← durable BEFORE any mutation
4. Journal.Append(entry, phase=Prepared) + PersistDurably
5. For each cost item: inventory.Reserve(operationId)
6. Journal.Update(phase=Reserved) + PersistDurably
7. For each operation: inventory.Commit(operationId)
8. Journal.Update(phase=Committed) + PersistDurably
9. Create CraftJob + snapshot
10. SaveManager.PersistDurably(craftJob)
11. Journal.Update(phase=JobPersisted) + PersistDurably
```

**v3.6 IMPLEMENTED deviation note:** current `CraftService.StartCraft` orders the steps as Validate → BuildSnapshot → CraftJob.Create → BeginTransaction(reserve + Prepared+persist) → EnqueueJob → Reserved+persist → Commit(per-op+Applied+persist) → Committed+persist → TryStartNextJob. Two deltas vs target above: (a) `Reserved`/`Committed` checkpoints are emitted AFTER enqueue/commit respectively (not strictly in the §11.2 step order); (b) phase `JobPersisted` + `Completed` + I-18 pruning are NOT yet implemented — journal entries stay at `Committed`. Rollback-on-commit-failure uses `CancelJob(RefundPolicy.None)` (recovery handles Applied ops; P0-D owns compensation). Reservation is RAM coordination only (no durable `inventory.Reserve(operationId)` API exists), matching §11.1's "reservation is coordination, not durability." Atomic crash-safety rests on per-operation `Applied`+persist checkpoints, not the §11.2 step order.

### 11.3 Journal Entry Schema

```
CraftTransactionJournalEntry
├── TransactionId                    (Guid)
├── JobId                            (string)
├── Phase                            (Prepared|Reserved|Committed|JobPersisted|Completed)
├── ExecutionSnapshot                (full snapshot for recovery)
├── Operations[]
│   ├── CraftTransactionOperationId  (Guid)
│   ├── ResourceType                 (Material|Catalyst|Progression|Currency)
│   ├── ResourceId
│   ├── Quantity
│   └── State                        (Pending|Applied|RolledBack)
└── CreatedAt
```

### 11.4 Operation State Machine

```
Pending → Applied        (inventory.Commit durably persisted)
Pending → RolledBack     (inventory.Rollback durably persisted)
Applied → RolledBack     (compensation during recovery)
```

### 11.5 Reconciliation Protocol

Inventory is authoritative. Journal is recovery checkpoint.

Recovery reads journal entries:

```
For each entry with phase ∈ {Committed, JobPersisted}:
  For each operation:
    inventory.HasAppliedOperation(operation.CraftTransactionOperationId)?
      true  → state = Applied (skip)
      false → state = Pending → execute inventory.Commit (idempotent)
```

Rollback reconciliation:

```
For each entry with phase ∈ {Prepared, Reserved}:
  For each operation where state == Applied:
    inventory.HasAppliedOperation(operation.CraftTransactionOperationId)?
      true  → inventory.Rollback (idempotent) → state = RolledBack
      false → state = RolledBack (no-op)
```

I-15: inventory never lies. Journal may be stale; recovery corrects.

---

## 12. Craft Job Lifecycle

### 12.1 States

```
Queued = 0
Crafting = 1
RewardPendingCommit = 5   (added in v3.3 TARGET)
Complete = 2
Cancelled = 3
Failed = 4
```

Transitions:

```
Queued                → Crafting | Cancelled
Crafting              → RewardPendingCommit | Cancelled | Failed
RewardPendingCommit   → Complete
```

### 12.2 State Semantics

```
Queued
  Resources committed, Craft not started, no Results
Crafting
  Resources committed, Timer active, Results == null
RewardPendingCommit
  CompletionSeed != null (durable, I-20)
  Results != null (durable, I-20)
  Inventory reward mutation may be partial
Complete
  All reward operations applied + durable
Cancelled
  No rewards produced; refund governed by journal
Failed
  Post-commit failure (validator/roll). Resources retained (committed).
  No rewards produced. No reward transaction executed. No replay on recovery.
```

I-20 invariant makes `RewardPendingCommit` a fully-determined state: if seed or Results missing, recovery treats it as `Crafting` (re-run Phase A).

### 12.3 Failed Semantics (NEW v3.3)

`Failed` is reached ONLY when:
- A post-commit roll/validation fails (RNG produced impossible state), OR
- An unrecoverable invariant violation is detected.

Failed jobs:
- Resources remain committed (already consumed at `StartCraft`).
- No rewards produced.
- No reward transaction executed.
- No replay on recovery.
- May be manually cleaned or retained for audit.

Pre-commit failures (validation, reservation) do NOT use `Failed` — they return error and leave the job in `Queued` or never create one.

---

## 13. Craft Completion

### 13.1 CURRENT — Single-Phase [VERIFIED]

```
CraftCompletionService.Complete(jobId)
  1. if job.IsComplete: return
  2. roll = RollCraft(...)
  3. rewards = GenerateRewards(...)
  4. inventory.AddItemInstance(reward)
  5. job.Status = Complete
  6. job.Results = ...
  7. SaveManager.MarkDirty()             ← NOT durable
```

Crash between 4 and 7 = duplicate reward on reload.

### 13.2 TARGET — Two-Phase [DESIGNED]

Phase A — durable Results BEFORE inventory mutation:

```
1. job.Status = RewardPendingCommit
2. SaveManager.PersistDurably()              ← durably enter RewardPendingCommit
3. if job.Results == null:
     snapshot = job.CraftExecutionSnapshot
     seed = snapshot.CompletionSeed ?? GenerateSeed()
     snapshot.CompletionSeed = seed
     context = snapshot.CraftContextSnapshot    ← I-10, I-19: snapshot only
     roll = RollCraft(snapshot.RecipeSnapshot, context, seed)
     rewards = GenerateRewards(roll, snapshot.RecipeSnapshot, context)
     job.Results = CraftResultData.FromInventoryItems(rewards)
   else:
     rewards = ConvertResultsToRewards(job.Results)
4. SaveManager.PersistDurably()              ← Results + Seed durable (I-12, I-17, I-20)
```

Phase B — atomic inventory apply:

```
5. for each reward at index i:
     operationId = RewardOperationId(jobId, i)     ← "{JobId}#{i}"
     inventory.ApplyReward(reward, operationId)
6. job.Status = Complete
7. SaveManager.PersistDurably()              ← Complete durable
```

### 13.3 InventoryService.ApplyReward Contract

```
InventoryService.ApplyReward(item, rewardOperationId) → ApplyResult:
  if ActiveTransactionWindow.Contains(rewardOperationId):
    return AlreadyApplied
  begin transaction
    _items.Add(item)
    ActiveTransactionWindow.Add(rewardOperationId)
  commit transaction                       ← atomic
  return Applied
```

### 13.4 ActiveTransactionWindow Pruning (I-18)

Pruning requires ALL of:
- `Job.Status == Complete`
- No journal entry exists for `JobId`
- Last `PersistDurably()` checkpoint succeeded

Pruning is a write that records absence of operation IDs. Crash during prune = safe replay on next load (prune is idempotent).

---

## 14. Failure & Recovery

### 14.1 CURRENT

Same as v3.2 §14.1. Inherits crash windows.

### 14.2 TARGET

| Failure | Behavior |
|---|---|
| Crash before Phase A | Status=Crafting, Results=null → re-run Phase A from scratch (RNG re-rolls) |
| Crash mid-Phase A (between Status=RewardPendingCommit and Results persist) | Status=RewardPendingCommit, Results=null → recovery treats as Crafting, re-runs Phase A |
| Crash after Phase A Results persist | Status=RewardPendingCommit, Results set → Phase B resume |
| Crash mid-Phase B | Status=RewardPendingCommit, partial window → ApplyReward skips already-applied |
| Crash after Complete | Status=Complete → no-op |
| Crash mid-journal transition | Reconciliation protocol (§11.5) corrects |
| Crash mid-inventory operation | Inventory operation itself is atomic; journal may be stale, reconciled on load |

---

## 15. Persistence & Migration

### 15.1 Persistent State

```
CraftRecipeRepositorySaveData  → UnlockedRecipeIds, KnownRecipeIds
CraftQueueSaveData             → Jobs[], MaxConcurrentJobs
CraftJobSaveData               → CraftExecutionSnapshot, Results, Status, CompletedCount
InventorySaveData              → ActiveTransactionWindow (pruned per I-18)
CraftTransactionJournal        → Entries (pruned on Completed)
```

### 15.2 Required Persistence Fields

| Field | Status |
|---|---|
| `IngredientsSnapshot[]` | [VERIFIED] |
| `RecipeSnapshot` (root) | [DESIGNED — P0] |
| `CraftExecutionSnapshot` | [DESIGNED] |
| `CraftContextSnapshot` | [DESIGNED] |
| `CompletionSeed` (`long?`) | [DESIGNED] |
| `CurrencySnapshot` | [DESIGNED] |
| `DecomposedRequirementsSnapshot[]` | [BLOCKED] |
| `ActiveTransactionWindow` | [DESIGNED] |
| ~~`CraftJob.RecipeVersion`~~ | **REMOVED** — derived from `RecipeSnapshot.RecipeVersion` |
| ~~`ProcessedRewardIndexes[]`~~ | **REMOVED** |

### 15.3 Migration: Legacy Complete Jobs (CORRECTED v3.3)

Pre-v3.3 saves may have `Status = Complete` with `Results` but no `ActiveTransactionWindow` entries.

```
Migration rule:
  for each CraftJob where Status == Complete (legacy):
    → trust legacy completion state
    → DO NOT replay rewards
    → DO NOT reconstruct ActiveTransactionWindow
    → results are authoritative as-is

  for each CraftJob where Status == RewardPendingCommit (new in v3.3):
    → enter Phase B replay immediately
    → ApplyReward per entry, idempotent
```

Replaying legacy Complete jobs risks duplicate rewards. Trust legacy state.

### 15.4 Version Independence

```
SchemaVersion       = JSON container
RecipeVersion       = per-recipe balance
GameSaveVersion     = persistent save schema
```

Adding field = backward compatible. Removing/renaming = migration.

---

## 16. Service Ownership

### 16.1 CURRENT [VERIFIED]

```
CraftRecipeRepository              → Recipe loading, lookup, unlock
CraftValidator (orchestrator)      → "can craft?"
CraftTransactionService            → Reserve / Commit / Rollback
CraftQueueService                  → Job lifecycle, progress, persistence
CraftCompletionService             → Single-phase completion
CraftRewardService                 → Generate InventoryItem[]
CraftRollService                   → RNG
CraftContextBuilder                → Build CraftContext snapshot
CraftPersistenceService            → SaveData façade
CraftRefundService                 → Refund on cancel
SaveManager                        → Persistence
InventoryService                   → Inventory mutation
```

### 16.2 TARGET [DESIGNED]

```
SaveManager.PersistDurably()                  ← synchronous durable write (I-17)
CraftTransactionJournal                      → State machine + Operations[] (§11.3)
DecomposedRequirementResolver                → Compute(rarity) → IngredientRequirement[] (pure)
DecomposedRequirementAggregator              → SumPerJob(reqs, count) → SnapshotEntry[] (pure)
InventoryService.ApplyReward                 → Atomic operation (§13.3)
InventoryService.ActiveTransactionWindow     → Reward idempotency (§13.3)
CraftRewardCommitter                         → Compute RewardOperationId, coordinate ApplyReward
CraftExecutionSnapshot DTOs                  → RecipeSnapshot, CostSnapshot, etc.
CraftContextBuilder                          → MUST emit value-only DTOs (I-19)
```

### 16.3 RecipeVersion Accessor

```csharp
public int RecipeVersion => ExecutionSnapshot.RecipeSnapshot.RecipeVersion;
```

Derived, not persistent.

---

## 17. Implementation Roadmap

### P0-A — Freeze Execution Contract

```
1. CraftExecutionSnapshot schema
2. RecipeSnapshot (immutable, I-16)
3. CostSnapshot
4. CurrencySnapshot
5. CraftContextSnapshot (value-only, I-19)
6. CompletionSeed (long?)
7. RecipeVersion removed from CraftJob (derived only)
```

### P0-B — Requirement Correctness

```
8. DecomposedRequirementResolver (pure, 6-rarity tests)
9. DecomposedRequirementAggregator (pure)
10. Transaction wiring (decomposed into Reserve/Commit/Rollback)
11. Refund wiring (snapshot-primary)
```

### P0-C — Transaction Durability

```
12. SaveManager.PersistDurably() implementation
13. CraftTransactionJournal state machine + Operations[]
14. Reconciliation protocol (§11.5)
15. Recovery semantics per phase + I-18 pruning
```

### P0-D — Completion Idempotency

```
16. RewardOperationId computation
17. InventoryService.ApplyReward + ActiveTransactionWindow
18. RewardPendingCommit = 5 enum value
19. Two-phase CraftCompletionService (I-12, I-17 enforced)
```

### P1 — Verification

```
20. EditMode test suite
21. Crash simulation (journal reconciliation, ApplyReward idempotency)
22. Save/load regression (legacy Complete trust)
23. Balance regression
```

### P2 — UI

```
24. UI integration
```

**Sequencing rule:** Do NOT start P0-C until P0-A + P0-B complete and tests green. Do NOT start P0-D until P0-C journal verified.

**v3.6 P0-C status:**
- 12 `SaveManager.PersistDurably()` — [IMPLEMENTED] atomic temp+`File.Replace`; `PersistCurrentStateDurably()`; journal Gather/Apply; `UpgradeSave` default.
- 13 `CraftTransactionJournal` state machine + `Operations[]` — [IMPLEMENTED] legal-transition guards, idempotent updates, pure `ClassifyReconciliation`, persistence.
- 14 Reconciliation protocol (§11.5) — [PARTIAL] classifier ready; recovery EXECUTOR (consumes decisions) + `InventoryService.ApplyReward` = P0-D.
- 15 Recovery semantics per phase + I-18 pruning — [PARTIAL] phase classifier done; I-18 pruning NOT implemented (entries stay Committed, never reach Completed/JobPersisted).

---

## 18. Quality Compliance

### 18.1 Production Targets

Data-driven · Deterministic · Save-safe · Version-safe · Balance-friendly · QA-testable · Extensible · Maintainable

### 18.2 Current Compliance

| Target | Status |
|---|---|
| Data-driven | [PARTIAL] |
| Deterministic | [PARTIAL] |
| Save-safe | [NOT YET] |
| Version-safe | [PARTIAL] |
| Balance-friendly | [PARTIAL] |
| QA-testable | [VERIFIED] |
| Extensible | [PARTIAL] |
| Maintainable | [PARTIAL] |

---

## 19. Files / Implementation Status

### 19.1 Implemented

```
Assets/Resources/Data/Crafting/Equipment/dataRecipe*.json (×11)
Assets/Resources/Data/Items/Material/dataHerbs.json
Assets/Resources/Data/Items/Material/dataMinerals.json
Assets/Resources/Data/Items/Material/dataOtherMaterials.json
Assets/Scripts/Items/CraftRecipeRepository.cs
Assets/Scripts/Items/CraftIngredientSnapshot.cs
Assets/Scripts/Items/RecipeSnapshot.cs                     (NEW v3.5 — P0-A)
Assets/Scripts/Items/CostSnapshot.cs                       (NEW v3.5 — P0-A)
Assets/Scripts/Items/CurrencySnapshot.cs                   (NEW v3.5 — P0-A)
Assets/Scripts/Items/CraftContextSnapshot.cs               (NEW v3.5 — P0-A)
Assets/Scripts/Items/CraftExecutionSnapshot.cs             (NEW v3.5 — P0-A)
Assets/Scripts/Items/CraftJobSaveData.cs                    (NEW v3.5 — canonical, was embedded in CraftQueueService.cs)
Assets/Scripts/Items/CraftQueueService.cs                   (UPDATED v3.5 — mapper + load persistence symmetry)
Assets/Scripts/Items/CraftJob.cs (IngredientsSnapshot)
Assets/Scripts/Items/CraftRefundService.cs
Assets/Scripts/Items/CraftValidator.cs
Assets/Scripts/Items/CraftRecipeValidationRunner.cs
Assets/Scripts/Items/Editor/CraftRecipeValidationMenu.cs
Assets/Scripts/Items/Decomposition/DecomposedRequirement.cs          (NEW v3.3)
Assets/Scripts/Items/Decomposition/DecomposedRequirementResolver.cs  (NEW v3.3)
Assets/Scripts/Items/Decomposition/DecomposedSnapshotEntry.cs        (NEW v3.3)
Assets/Scripts/Items/Decomposition/DecomposedRequirementAggregator.cs (NEW v3.3)
Assets/Scripts/Items/Decomposition/Tests/DecompositionTests.cs       (NEW v3.3)
```

### 19.2 Planned Changes

```
Assets/Scripts/Manager/SaveManager.cs
  + PersistDurably() — synchronous durable write  ← [IMPLEMENTED v3.6: atomic temp+File.Replace; PersistCurrentStateDurably(); journal Gather/Apply; UpgradeSave default]

Assets/Scripts/Items/RecipeSnapshot.cs                    (NEW — P0-A)
Assets/Scripts/Items/CraftExecutionSnapshot.cs            (NEW — P0-A)
Assets/Scripts/Items/CraftContextSnapshot.cs              (NEW — P0-A, value-only)
Assets/Scripts/Items/CurrencySnapshot.cs                  (NEW — P0-A)
Assets/Scripts/Items/CraftJob.cs
  + CraftExecutionSnapshot root
  + CompletionSeed (long?)
  + DecomposedRequirementsSnapshot[] (blocked)
  - RecipeVersion (removed; derived)

Assets/Scripts/Items/CraftJobSaveData.cs
  + RecipeSnapshot persistence
  + CurrencySnapshot persistence
  + long? CompletionSeed serialization

Assets/Scripts/Items/CraftTransactionService.cs
  + Decomposed wiring (uses Resolver+Aggregator)
  + PersistDurably checkpoints

Assets/Scripts/Items/CraftTransactionJournal.cs           (NEW — P0-C)  ← [IMPLEMENTED v3.6]
Assets/Scripts/Items/CraftJournalEntry.cs                 (NEW)         ← [IMPLEMENTED v3.6]
Assets/Scripts/Items/CraftJournalOperation.cs             (NEW)         ← [IMPLEMENTED v3.6]

Assets/Scripts/Items/InventoryService.cs
  + ApplyReward(item, rewardOperationId)
  + ActiveTransactionWindow
  + Atomicity guarantee

Assets/Scripts/Items/CraftRewardCommitter.cs              (NEW — coordinator)
Assets/Scripts/Items/CraftCompletionService.cs
  ~ Rewrite to two-phase
Assets/Scripts/Items/CraftJobStatus.cs
  + RewardPendingCommit = 5

Assets/Scripts/Items/CraftValidator.cs
  ~ Split into per-layer classes

Assets/Scripts/Items/EquipmentCraftingRuleData.cs         (NEW — RequiredGroups)
Assets/Scripts/Items/EquipmentCraftingRuleRepository.cs   (NEW)

Assets/Scripts/Items/Tests/CraftValidationTests.cs        (NEW — EditMode)
Assets/Scripts/Items/Tests/CraftJournalTests.cs           (NEW — EditMode)
Assets/Scripts/Items/Tests/CraftExecutionSnapshotTests.cs (NEW — EditMode)
```

---

## 20. Equipment Attribute Generation (v3.8)

> **Status:** [DESIGNED] — attribute rolls on crafted equipment. Rarity source of truth: `CraftRecipeData.Rarity`. No random rarity.

### 20.1 Source of Truth

Crafted equipment rarity MUST come from `CraftRecipeData.Rarity` (1..6). No random rarity in the crafting flow.

```text
Rarity = 1 → Common
Rarity = 2 → Rare
Rarity = 3 → Epic
Rarity = 4 → Legendary
Rarity = 5 → Mythic
Rarity = 6 → Divine
```

**KNOWN BUG [v3.8]:** `CraftRewardService.GenerateEquipmentFromBase` uses `recipe.RequiredTier` for the rarity level (CraftRewardService.cs:94), not `recipe.Rarity`. `RequiredTier` is a gating requirement, NOT the output rarity. Must be corrected to `int rarityLevel = recipe.Rarity;`.

### 20.2 Attribute Enum

Reuse existing `MainAttribute { Constitution, Strength, Intelligence, Dexterity }` (Enumku.cs:20). NO new `EquipmentAttribute` enum.

```text
MainAttribute.Constitution → CON
MainAttribute.Strength     → STR
MainAttribute.Intelligence → INT
MainAttribute.Dexterity    → DEX
```

### 20.3 Tier Configuration

Central config. NOT hardcoded in generation logic. Lives in `dataConfigCrafting.json` under `AttributeRolls` (per-rarity key). One source of truth.

| Rarity | MaxRolls | MinValue | MaxValue |
| -----: | -------: | -------: | -------: |
|      1 |        1 |        3 |        6 |
|      2 |        2 |        5 |       10 |
|      3 |        3 |        8 |       16 |
|      4 |        4 |       12 |       24 |
|      5 |        5 |       17 |       34 |
|      6 |        6 |       25 |       50 |

Runtime lookup: `EquipmentAttributeTierConfig` (Rarity / MaxRolls / MinValue / MaxValue), loaded with `CraftingConfig`.

### 20.4 Roll Algorithm

`AttributeRollService.RollAttributes(Rarity rarity, IRandomProvider rng)` → `AttributeStatEntry[]`.

**Roll count = MaxRolls — number of random rolls, NOT number of unique attributes.** Duplicates allowed on all rarities and aggregated:

```text
for roll in 1..MaxRolls:
    attribute = random MainAttribute
    value     = random MinValue..MaxValue

    if aggregate contains attribute:
        aggregate[attribute] += value
    else:
        aggregate[attribute]  = value
```

Output: one `AttributeStatEntry` per aggregated attribute (class already defined, ItemData.cs:224). No duplicate entries.

Example (Divine, 6 rolls): `STR+30, STR+40, CON+27, DEX+35, INT+29, STR+25` → `STR+95, CON+27, DEX+35, INT+29`.

### 20.5 Storage & Persistence

- Rolled attributes stored per instance: `InventoryItem.CustomData["AttributeStats"]` = `AttributeStatEntry[]` — same pattern as `CustomData["SecondaryStats"]` / `CustomData["Affixes"]`.
- `CustomDataConverter` (Save/CustomDataConverter.cs) MUST add case `"AttributeStats"` → `ToObject<AttributeStatEntry[]>()` for typed round-trip. Without it, post-load casts fail.

### 20.6 Integration Points

1. **Rarity fix:** `CraftRewardService.GenerateEquipmentFromBase` — rarity from `recipe.Rarity` (§20.1).
2. **Pipeline route:** crafted equipment must go through `ItemGenerator`/`EquipmentGenerator.Generate` (which populates `CustomData`), NOT `ItemDatabase.GenerateEquipment` directly. `EquipmentGenerator` gains `AttributeRollService` (injected with the SAME `IRandomProvider` as `StatRollService`), called after secondary stats, storing to `CustomData["AttributeStats"]`.
3. **Rebuild chain — 3 lossy points MUST carry `CustomData`:**
   - `CraftRewardService.ToInventoryItem` (CraftRewardService.cs:133)
   - `CraftResultData.FromInventoryItems` (CraftData.cs:23) — add `CustomData` field
   - `CraftCompletionService.Complete` Phase B rebuilds (lines 96-102, 137-143)
   Without this, attribute rolls (and existing secondaries/affixes/sockets) are dropped before `ApplyReward`.

### 20.7 Determinism (I-11)

`CompletionSeed` must be resolved BEFORE the roll and passed into roll + attribute generation (currently resolved after, CraftCompletionService.cs:83-88). Attribute rolls use the same `IRandomProvider`/seed as the rest of generation → identical snapshot + seed ⇒ identical attribute results.

### 20.8 Backward Compatibility

- Legacy saved equipment: `CustomData` absent → null-coalesce to empty → zero attribute bonus. Identical behavior to today. No save migration.
- Consumers (AttributeModifierManager aggregation) must guard missing `CustomData["AttributeStats"]`, mirroring missing-`Affixes` handling.

### 20.9 Testing (EditMode)

| Rarity | Assert |
| -----: | ------ |
|      1 | exactly 1 roll; each value 3..6 |
|      2 | exactly 2 rolls; each value 5..10; duplicates allowed |
|      3 | exactly 3 rolls; each value 8..16; duplicates allowed |
|      4 | exactly 4 rolls; each value 12..24; duplicates allowed |
|      5 | exactly 5 rolls; each value 17..34; duplicates allowed |
|      6 | exactly 6 rolls; each value 25..50; duplicates allowed |

Aggregation test: `STR+5, STR+8, STR+6` → single `STR+19` entry.

### 20.10 Files

```
Assets/Scripts/Items/Generation/AttributeRollService.cs    (NEW)
Assets/Scripts/Crafting/EquipmentAttributeTierConfig.cs     (NEW — or extend CraftingConfig)
Assets/Scripts/Items/Generation/EquipmentGenerator.cs       (~ inject AttributeRollService)
Assets/Scripts/Crafting/CraftRewardService.cs               (~ recipe.Rarity + pipeline route)
Assets/Scripts/Crafting/CraftData.cs                        (~ CraftResultData.CustomData)
Assets/Scripts/Crafting/CraftCompletionService.cs           (~ carry CustomData in rebuilds)
Assets/Scripts/Save/CustomDataConverter.cs                  (~ "AttributeStats" case)
Assets/Resources/Data/Crafting/dataConfigCrafting.json      (~ AttributeRolls table)
Assets/Scripts/Items/Generation/Tests/AttributeRollTests.cs (NEW — EditMode)
```

---

## Appendix A — Recipe Matrix

11 × 6 = 66 recipes.

## Appendix B — Material Tables

39 regular + water + 5 decomposed.

## Appendix C — Validation Rules

Per §9. CLI: `Assets/Scripts/Items/CraftRecipeValidationRunner.cs`.

## Appendix D — QA Matrix

### D.1 VERIFIED

```
[VERIFIED] 66 recipes load
[VERIFIED] Unique IDs
[VERIFIED] ItemDatabase resolution
[VERIFIED] 66/66 contain water
[VERIFIED] Identity rules pass (hardcoded branches)
[VERIFIED] Catalyst/Progression excluded from composition
[VERIFIED] Monotonic cost per slot
[VERIFIED] IngredientsSnapshot[] captured
[VERIFIED] Snapshot persists
[VERIFIED] Refund reads snapshot primary
[VERIFIED] 6-layer CLI runner
[VERIFIED] DecomposedRequirementResolver (16 EditMode tests, v3.5 — 16/16 green)
[VERIFIED] DecomposedRequirementAggregator (integrated with resolver tests, v3.5 — 16/16 green)
```

### D.2 DESIGNED

```
[DESIGNED] CraftExecutionSnapshot root
[DESIGNED] RecipeSnapshot immutable
[DESIGNED] CraftContextSnapshot value-only (I-19)
[DESIGNED] CompletionSeed long?
[DESIGNED] CurrencySnapshot
[DESIGNED] EquipmentCraftingRuleData + RequiredGroups
[DESIGNED] Validator split
[DESIGNED] Armor cross-slot validator
[DESIGNED] Water deviation Warning
[IMPLEMENTED v3.6 — UNVERIFIED] SaveManager.PersistDurably() (atomic temp+File.Replace; journal Gather/Apply/UpgradeSave; NotifySaveLoaded on load-fail)
[IMPLEMENTED v3.6 — UNVERIFIED] CraftTransactionJournal + Operations[] + ClassifyReconciliation (pure decision emitter)
[PARTIAL v3.6] Reconciliation protocol (§11.5) — classifier ready; recovery EXECUTOR + I-18 pruning = P0-D
[DESIGNED] InventoryService.ApplyReward + ActiveTransactionWindow
[DESIGNED] RewardPendingCommit = 5
[DESIGNED] Two-phase completion
[DESIGNED v3.8] Equipment attribute generation (§20) — recipe.Rarity source of truth, AttributeRollService, CustomData["AttributeStats"]
[DESIGNED] Failed semantics (§12.3)
```

### D.3 BLOCKED

```
[BLOCKED] DecomposedRequirementsSnapshot[] → needs resolver+aggregator (§11)
[UNBLOCKED v3.6] Atomic craft-start → journal + PersistDurably implemented (UNVERIFIED compile/test)
[BLOCKED] Two-phase completion recovery → needs journal + ApplyReward (P0-D)
```

### D.4 TODO

```
[TODO v3.6] EditMode suite for journal + snapshot + completion (P0-C regression tests — NOT authored; compile green unconfirmed)
[TODO] Crash simulation tests
[TODO] Save/load regression with legacy Complete trust
[TODO] Balance regression
[TODO] UI integration
```
