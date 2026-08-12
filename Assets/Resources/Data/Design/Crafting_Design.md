# Crafting Design — Equipment Crafting & Recipe System

> **Status:** Production Specification (v2.4 — Runtime Contract Correction)
>
> **v2.3 was superseded by v2.4** after implementation-readiness review surfaced eight structural contradictions and terminology collisions that would produce bugs if coded against v2.3 as-is.
>
> **v2.4 scope:** P0 blockers only — transaction architecture split (CURRENT vs TARGET), idempotency wording downgrade, Primary Tier rule fix, quantity glossary collapse, Water single source of truth, decomposed snapshot rule, transaction ID ownership cleanup, reward commit failure semantics. P1/P2 deferred until P0 contracts are locked and code-side verification confirms.
>
> **v2.4 changes (8 P0 blockers):**
> 1. §24 split into CURRENT IMPLEMENTATION (best-effort, in production today) vs TARGET ARCHITECTURE (requires `CraftTransactionJournal`).
> 2. §27 downgraded from "Crash-Safe Idempotency" to **Best-Effort Crash-Recoverable Completion**. Hard rule reframed: persisted `Results` are authoritative; RNG forbidden when `Results != null`.
> 3. §6.2 Primary Tier rule fixed: `Primary.CraftingTier MUST NOT exceed Recipe.Rarity` (was incorrectly `Primary tier = Recipe.Rarity`, which excluded Leather from R6 leather-slot recipes and contradicted §9).
> 4. §16/§17 glossary collapsed: dropped `ActualQuantity` from runtime vocabulary. Added `TotalOutputCount = Count + BonusResultCount` with `Results.Length == TotalOutputCount` invariant.
> 5. §13 Water table demoted to **validation reference only**. JSON `Ingredients[].Quantity` for water is sole runtime source.
> 6. §14 + §26: decomposed requirements explicitly snapshotted into `CraftJob.DecomposedRequirementsSnapshot[]` at `StartCraft`; formula recomputation forbidden in all subsequent paths.
> 7. §29.3 + §32: all `CraftRewardTransactionId[]` stored references removed. `IInventoryService.HasProcessedTransaction` / `MarkTransactionProcessed` removed. Replaced by `CraftRewardCommitter` + `CraftJobSaveData.ProcessedRewardIndexes[]`.
> 8. §25 + §28: reward commit failure transitions defined. `RewardPendingCommit → Failed` after N retries. `RewardPendingCommit → Cancelled` forbidden.
>
> §36 audit checklist reclassifies items whose §32 confirms runtime absence as `[!]` unresolved. "No contradictory rules" reclassified to `[!]` until §24/§31/§25/§6.2 are aligned and code-verified.
---

## 1. Purpose

Define the complete equipment crafting system for the **66-recipe matrix** (11 equipment slots × 6 rarities). This document is the contract that binds data, runtime, persistence, validation, and balancing into a single coherent system. It must be sufficient to implement crafting without further design clarification.

---

## 2. Scope

**In scope:**

- 11 equipment slots (Hat, Gloves, Cape, Armor, Belt, Pants, Pendant, Ring, Earring, Bracelet, Shoes).
- 6 rarities (R1 Common → R6 Divine).
- 66 recipes sourced from `dataRecipeEquipment.json`.
- 39 regular crafting materials + Water (catalyst) + 5 decomposed (progression gate).
- Atomic craft transaction covering all three resource classes (materials, water, decomposed).
- Craft queue lifecycle with persistence and crash-safe completion.
- Save/load compatibility for active craft jobs.
- Validation pipeline (structural, item, design, economy).

**Out of scope:** See §34 Non-Goals.

---

## 3. Design Principles

### Single Responsibility

```text
dataItems on each JSON file Resources/Data/Items
    → Item identity + CraftingTier + CraftingFamily + Role

dataRecipeEquipment.json
    → Recipe definition + ingredients + balance

Crafting Services (runtime)
    → Execution / Validation / State

SaveData
    → Player Progress / Active Craft Jobs
```

### Core Quality Attributes

```text
Production Ready
Data Driven
Deterministic
Save Safe
Version Safe
Balance Friendly
QA Testable
Extensible
Maintainable
```

### Single Quantity Model

```text
Ingredients[].Quantity (JSON) = Final per-unit quantity = Runtime source of truth
```

There is **one quantity per ingredient**, the one in JSON. No base/multiplier runtime formula. Designers rebalance by editing JSON.

---

## 4. Material Taxonomy (clarified)

| Class | Count | Examples | Role |
|---|---|---|---|
| **Regular crafting material** | 39 | leather, granite, thick_thread, refined_steel | Primary / secondary ingredients |
| **Catalyst** | 1 | water | Mandatory in every recipe; excluded from material identity rules |
| **Progression resource** | 5 | decomposed_common, decomposed_rare, decomposed_epic, decomposed_legendary, decomposed_mythic | Rarity gate; excluded from sink coverage; included in transaction |

The number **39** refers to regular materials only. Decomposed items are progression resources (gated by dismantling), not crafting materials. Water is a catalyst, not a material.

**TODO — Requires Code Verification:** audit `dataItems.json` to confirm the 39 regular materials. The count in this document is the design target; the file is authoritative.

---

## 5. Material System

### 5.1 Regular Material Families (39)

```text
Stone      — Rock, Stone Dust, Granite, Sandstone, Corundum Powder, Rubstone Powder
Wood       — Disposed Logs, Logs, Rough Lumber, Fine Lumber, High-grade Lumber, Lumber Essence
Thread     — Cotton Thread, Thick Thread, Silk Thread, Compound Thread, Azureworm Silk, Vega String
Leather    — Leather
Coal       — Coal, Coal Dust, Charcoal, Anthracite, Extruded Charcoal
Metal      — Pig Iron, High-carbon Steel, Refined Steel, Steel Alloy, High Alloy Steel, Iron Dust
Adhesive   — Organic Glue, Concentrated Glue, Strong Glue, Colored Glue, Super Glue, Compound Glue
Special    — Elemental Essence, Dream of Reminiscence, Essence of Hope
```

### 5.2 Water (Catalyst)

```text
CraftingTier   = 0
CraftingFamily = Water
Role           = Catalyst
```

Water is **excluded** from material identity rules (§7). Validator must skip Water when checking primary/secondary composition.

### 5.3 Iron Dust

```text
Role: Auxiliary Metal-family material (regular material, family = Metal).
Tier: CraftingTier = 5.
Usage: Allowed as Metal-family supporting ingredient in selected high-tier recipes.
```

**Provenance:** Iron Dust is an auxiliary material, **not** a by-product of any refining pipeline. If a future Refining System produces it, this section must be updated.

### 5.4 Material Tier Resolution (data-driven)

Material tier and family come from `dataItems.json`. The validator never hard-codes a dictionary.

```json
{
  "Id": "granite",
  "Name": "Granite",
  "Category": 3,
  "CraftingTier": 3,
  "CraftingFamily": "Stone",
  "StackSize": 999
}
```

| Field | Values | Purpose |
|---|---|---|
| `CraftingTier` | Meaningful **only** for `Role = Material`. For `Role = Catalyst` (Water) the value is `0`. For `Role = Progression` the value is `0` and ignored — progression tier is encoded in the itemId. | Drives `MaximumAllowedMaterialTier` rule (§6.2). **Validator MUST skip `CraftingTier` check when `Role ≠ Material`.** |
| `CraftingFamily` | `Stone` / `Wood` / `Thread` / `Leather` / `Coal` / `Metal` / `Adhesive` / `Special` / `Water` | Drives material identity rules (§7) |
| `Role` | `Material` / `Catalyst` / `Progression` | Drives (a) transaction grouping (§24), (b) eligibility for material identity rules (§7), (c) eligibility for primary/secondary ordering (§11.4). Sink coverage (§15) is a downstream consequence of `Role = Material` — not a direct driver of `Role` itself. |

**Unified terminology note:** the family is named `Stone`, not `Mineral`. Designers and code refer to the same enum value. Earlier documentation drift between `Stone` (in §5) and `Mineral` (in §7) is corrected here.

### 5.5 Required Additions (TODO — Requires Code Verification)

- Add `CraftingTier` + `CraftingFamily` + `Role` to every material in `dataItems.json`.
- Extend `ItemData` (or equivalent) with these fields.
- Add `ItemDatabase.GetCraftingTier(itemId)` and `GetCraftingFamily(itemId)` accessors.
- Add `ItemDatabase.GetRole(itemId)` for transaction grouping.

---

## 6. Rarity Progression

### 6.1 Material Tier → Rarity (baseline)

| Rarity | Stone | Wood | Thread | Coal | Metal | Adhesive | Special |
|---|---|---|---|---|---|---|---|
| R1 Common | Rock | Disposed Logs | Cotton Thread | Coal | Pig Iron | Organic Glue | — |
| R2 Rare | Stone Dust | Logs | Thick Thread | Coal Dust | High-carbon Steel | Concentrated Glue | — |
| R3 Epic | Granite | Rough Lumber | Silk Thread | Charcoal | Refined Steel | Strong Glue | — |
| R4 Legendary | Sandstone | Fine Lumber | Compound Thread | Anthracite | Steel Alloy | Colored Glue | — |
| R5 Mythic | — | High-grade Lumber | Azureworm Silk | Extruded Charcoal | High Alloy Steel (+ Iron Dust) | Super Glue | — |
| R6 Divine | Corundum Powder / Rubstone Powder | Lumber Essence | Vega String | — | — | Compound Glue | Elemental Essence, Dream of Reminiscence, Essence of Hope |

**Note:** Leather is intentionally excluded from this tier table. Leather is R1 only (it does not have higher tiers). Use `Thread` for high-rarity textile upgrades.

### 6.2 MaximumAllowedMaterialTier Rule

```text
Primary material tier     = recipe rarity
Secondary material tier   ≤ recipe rarity
Processing / Adhesive     ≤ recipe rarity (or one tier below)
Catalyst (Water)          = always allowed (CraftingTier = 0)
Progression (decomposed)  = determined by rarity table (§13), not by CraftingTier
```

Example R3 recipe: `Primary = R3 Granite`, `Secondary = R2 Thick Thread (allowed)`, `Adhesive = R2 or R3 Strong Glue`.

### 6.3 Rarity-driven Decomposed Gate

Decomposed equipment is **derived from rarity**, not authored per recipe. See §13.

---

## 7. Material Identity Rules

| Slot | Primary | Required Composition |
|---|---|---|
| Hat | Thread | `Thread` primary; no `Metal` as primary |
| Gloves | Leather | `Leather` + `Thread` required |
| Cape | Thread | `Thread` primary; `Adhesive` secondary |
| Armor | Leather | `Leather` + `Metal` required; `Coal` allowed |
| Belt | Leather | `Leather` + `Metal` required |
| Pants | Leather | `Leather` + `Thread` required |
| Pendant | Stone | `Stone` + `Metal` required |
| Ring | Metal | `Metal` + `Stone` required |
| Earring | Stone | `Stone` + `Metal`/`Thread` required |
| Bracelet | Metal | `Metal` + `Stone` required |
| Shoes | Leather | `Leather` + `Wood` required |

**Validator exclusions:** `Water` (Catalyst) and `decomposed_*` (Progression) are skipped when evaluating identity composition.

**Enforced by §20.3 Design Validation.**

---

## 8. Crafting Profiles

```text
Small   — Ring, Earring
Medium  — Hat, Gloves, Belt, Pendant, Bracelet
Large   — Cape, Pants, Shoes
Heavy   — Armor
```

Profile is recorded on each recipe. Used by:

1. The §16 validator to compute `ExpectedQuantity` (reference baseline).
2. Designer tooling (filtering, balance dashboards).

The runtime **never** uses profile to compute actual quantity.

---

## 9. Equipment & Rarity Matrix

### 9.1 Equipment Slots (11)

```text
Hat, Gloves, Cape, Armor, Belt, Pants, Pendant, Ring, Earring, Bracelet, Shoes
```

Stable identifiers. No aliases (Boots ↔ Shoes, Necklace ↔ Pendant, Artifact ↔ Bracelet).

### 9.2 Rarities (6)

```text
R1 — Common
R2 — Rare
R3 — Epic
R4 — Legendary
R5 — Mythic
R6 — Divine
```

### 9.3 Recipe Matrix

```text
11 equipment types × 6 rarities = 66 recipes
```

Every (slot, rarity) combination must be present. Missing recipes fail structural validation (§20.1).

### 9.4 Equipment Progression Names (display-only)

Names are display-only. Recipe identity is the `Id`.

| Slot | R1 | R2 | R3 | R4 | R5 | R6 |
|---|---|---|---|---|---|---|
| Hat | Cotton Hat | Thick Thread Hat | Silk Hat | Compound Hat | Azureworm Hat | Vega Hat |
| Gloves | Leather Gloves | Reinforced Leather Gloves | Thick Leather Gloves | Silk-Lined Gloves | Azureworm Gloves | Vega Gloves |
| Cape | Cotton Cape | Thick Thread Cape | Silk Cape | Compound Cape | Azureworm Cape | Vega Cape |
| Armor | Leather Armor | Reinforced Leather Armor | Iron-Plated Armor | Steel Armor | Steel Alloy Armor | High Alloy Armor |
| Belt | Leather Belt | Reinforced Leather Belt | Iron Buckle Belt | Steel Buckle Belt | Alloy Belt | High Alloy Belt |
| Pants | Leather Pants | Reinforced Leather Pants | Thick Thread Pants | Silk-Lined Pants | Azureworm Pants | Vega Pants |
| Pendant | Rock Pendant | Granite Pendant | Sandstone Pendant | Corundum Pendant | Rubstone Pendant | Essence Pendant |
| Ring | Iron Ring | High-carbon Steel Ring | Refined Steel Ring | Steel Alloy Ring | High Alloy Ring | Essence Ring |
| Earring | Rock Earring | Granite Earring | Sandstone Earring | Corundum Earring | Rubstone Earring | Essence Earring |
| Bracelet | Iron Bracelet | High-carbon Steel Bracelet | Refined Steel Bracelet | Steel Alloy Bracelet | High Alloy Bracelet | Essence Bracelet |
| Shoes | Leather Shoes | Reinforced Leather Shoes | Lumber Sole Shoes | Fine Lumber Shoes | High-grade Lumber Shoes | Essence Sole Shoes |

---

## 10. Recipe Data Architecture

### 10.1 File Location

```text
Assets/Resources/Data/Crafting/dataRecipeEquipment.json
```

### 10.2 Load Path

```text
dataRecipeEquipment.json
    ↓ Resources.Load<TextAsset>("Data/dataRecipeEquipment")
CraftRecipeRepository.LoadRecipesFromJson()   ← Existing — Requires Code Verification
    ↓ JsonConvert.DeserializeObject<RecipeContainer>
Dictionary<string, CraftRecipeData> _allRecipes
    ↓ ItemDatabase.GetItem(ingredient.ItemId)
Ingredient resolution
```

### 10.3 Why JSON, not inline `EquipmentData.CraftRecipe`

- 66 recipes inflate `dataItems.json` by ~1500 lines.
- Equipment definitions change rarely; recipe balance tweaks frequently.
- Matches project pattern (`dataCard.json`, `dataWave.json`).

---

## 11. Recipe Schema

### 11.1 Container

```json
{
  "SchemaVersion": 1,
  "Recipes": [ ... ]
}
```

### 11.2 Recipe Entry

```json
{
  "Id": "craft_leather_armor_r1",
  "Name": "Leather Armor",
  "Category": "Equipment",
  "EquipmentType": "Armor",
  "Rarity": 1,
  "RecipeVersion": 1,
  "CraftingProfile": "Heavy",
  "Ingredients": [
    { "ItemId": "leather",        "Quantity": 8 },
    { "ItemId": "cotton_thread",  "Quantity": 3 },
    { "ItemId": "coal",           "Quantity": 3 },
    { "ItemId": "organic_glue",   "Quantity": 2 },
    { "ItemId": "water",          "Quantity": 10 }
  ]
}
```

### 11.3 Field Semantics

| Field | Type | Purpose |
|---|---|---|
| `SchemaVersion` | int | Top-level. Increments on schema breaks. |
| `Id` | string | Stable recipe identity. Lookup key. |
| `Name` | string | Display name. Not an identifier. |
| `Category` | string | `"Equipment"` for equipment recipes. |
| `EquipmentType` | enum | One of the 11 slot names. |
| `Rarity` | int (1..6) | Drives decomposed requirement + water table. |
| `RecipeVersion` | int ≥ 1 | Balance version. Increments on quantity/ingredient change. |
| `CraftingProfile` | enum | `Small` / `Medium` / `Large` / `Heavy`. Validator reference. |
| `Ingredients` | array | Material + water + decomposed. No duplicates. |
| `Ingredients[].ItemId` | string | Must resolve in `ItemDatabase` and have `CraftingTier` + `CraftingFamily` + `Role`. |
| `Ingredients[].Quantity` | int > 0 | **Final per-unit quantity. Single runtime source of truth.** |

There is **no** `BaseProfileQuantity` field.

### 11.4 Ingredient Ordering Rule

```text
Primary Material    = first ingredient in Ingredients[] where Role = Material
Secondary Materials = all subsequent ingredients where Role = Material
Excluded            = ingredients where Role = Catalyst or Role = Progression
```

The Primary Material determines item identity (skin, base visual, family identity). Secondary Materials are auxiliary composition that shapes stats and craft feel. Catalyst (Water) and Progression (Decomposed) are never considered Primary or Secondary.

**Validator MUST** identify Primary by declaration order, not by quantity, tier, or family weight.

---

## 12. Schema / Recipe / Save Versioning

```text
SchemaVersion     = JSON structure version (top-level container)
RecipeVersion     = per-recipe balance version
Game Save Version = persistent save schema (in SaveData)
```

These three are **independent**.

- `SchemaVersion` bump → requires migration on load.
- `RecipeVersion` bump → does NOT break saves (active jobs use snapshot, §26).
- `Game Save Version` bump → requires `SaveManager` migration.

`RecipeVersion` must NOT change recipe `Id`, `EquipmentType`, or `Rarity`. Identity is stable; only quantities and ingredient composition may change.

---

## 13. Water Quantity (Validation Reference)

Water is mandatory for every recipe (R1..R6). It is a **catalyst**.

**Source of truth:** the runtime water quantity is read **solely** from `Ingredients[].Quantity` for the `water` entry in each recipe's JSON. JSON is the only runtime authority.

The table below is a **designer validation baseline target only** — it informs design but does NOT override JSON. A deviation between JSON `water` quantity and this baseline by more than **20%** triggers a validator **Warning** (§20.4).

| Rarity | Baseline Water Quantity (validation reference) |
|---|---|
| R1 Common | 10 |
| R2 Rare | 20 |
| R3 Epic | 30 |
| R4 Legendary | 50 |
| R5 Mythic | 80 |
| R6 Divine | 120 |

Water quantity alone does NOT determine difficulty. The decomposed gate is the real gate.

**Validator behavior:**
- Runtime reads `Ingredients[].Quantity` for `water` from JSON — no lookup against this table.
- Validator emits Warning if `|JSON_water_quantity − table_baseline| / table_baseline > 0.20`.
- Validator never blocks recipe load on water quantity mismatch.

---

## 14. Decomposed Equipment Requirement

Decomposed equipment is the byproduct of dismantling. Real progression gate for R≥2.

| Target Rarity | Decomposed Requirements |
|---|---|
| R1 Common | — |
| R2 Rare | 1× Common |
| R3 Epic | 2× Common + 1× Rare |
| R4 Legendary | 3× Common + 2× Rare + 1× Epic |
| R5 Mythic | 4× Common + 3× Rare + 2× Epic + 1× Legendary |
| R6 Divine | 5× Common + 4× Rare + 3× Epic + 2× Legendary + 1× Mythic |

**Recipe does NOT author this.** Engine computes from `Rarity` at validation.

ItemIds: `decomposed_common`, `decomposed_rare`, `decomposed_epic`, `decomposed_legendary`, `decomposed_mythic`. Registered in `dataItems.json` with `Role = Progression`, `CraftingTier = 0` (progression tier is determined by itemId, not CraftingTier), `StackSize = 999`.

Decomposed equipment is a **transaction resource** and MUST participate in Reserve/Commit/Rollback (§24).

---

## 15. Material Sink Coverage

All **39 regular materials** must have at least one meaningful crafting sink.

```text
Rule: every regular material appears in ≥1 recipe ingredient across the 66 recipes.
```

**Excluded from sink coverage:**

- Water (Catalyst)
- 5 decomposed items (Progression resources — generated by dismantle, not by craft)

If a regular material has zero recipes:

- It is a documentation error OR a missing recipe assignment.
- Do not silently leave materials unsunk — they become inventory clutter.

Coverage report:

```text
39 Regular Materials Covered = true
0 Regular Materials Unused  = true
```

---

## 16. Recipe Cost Formula

### 16.1 Quantity Glossary (collapsed, v2.4)

```text
RecipeQuantity       = Ingredients[].Quantity (from JSON, per unit)
SnapshotQuantity     = RecipeQuantity copied into CraftJob.IngredientsSnapshot[] at start time
TotalRequired        = SnapshotQuantity × CraftJob.Count       (transaction reserve / consumption)
ExpectedQuantity     = ceil(ProfileBaseline × RarityMultiplier) (validator reference only — NEVER runtime)
BonusResultCount     = roll-driven additive OUTPUT count (per recipe, opt-in, §17)
TotalOutputCount     = Count + BonusResultCount               (governs Results[].Length)
```

**Runtime vocabulary lock:** the runtime uses ONLY `SnapshotQuantity`, `TotalRequired`, `BonusResultCount`, `TotalOutputCount`. `ActualQuantity` is **dropped** from runtime vocab — it has been collapsed into `RecipeQuantity`/`SnapshotQuantity`. `BonusQuantity` is **dropped** — replaced by `BonusResultCount`. Do NOT reintroduce either term in code or future sections.

### 16.2 Rarity Multiplier (baseline)

```text
R1 = 1.0×
R2 = 1.2×
R3 = 1.5×
R4 = 2.0×
R5 = 3.0×
R6 = 4.5×
```

### 16.3 Profile Baseline (reference only)

```text
Small   = 2
Medium  = 4
Large   = 6
Heavy   = 10
```

### 16.4 Rounding

Use `ceil` for `ExpectedQuantity`. `RecipeQuantity` is already integer.

### 16.5 Runtime vs Validation

```text
Runtime    →  SnapshotQuantity × Count                  →  consumed at craft start
Validation →  RecipeQuantity vs ExpectedQuantity        →  Warning if |deviation| > 20%
```

Runtime **never** recomputes quantity. JSON `RecipeQuantity` is the single source of truth; `SnapshotQuantity` is the per-job copy captured at `StartCraft`.

### 16.6 ExpectedQuantity Consumer Whitelist (HARD RULE)

`ExpectedQuantity` may be consumed **only** by:

```text
- CraftValidator           (deviation check)
- Editor tooling           (balance dashboards)
- Balance reporting        (designer-facing reports)
```

`ExpectedQuantity` is **forbidden** in:

```text
- CraftTransactionService
- CraftRewardService
- CraftRollService
- CraftQueueService
- CraftCompletionService
- CraftJob
- CraftService
```

If a future contributor tries to use `ExpectedQuantity` in any runtime service, the change must be rejected at code review. JSON `Ingredients[].Quantity` is the only runtime source.

---

## 17. CraftRollService — Base vs Bonus Quantity

`CraftRollService` owns RNG-driven bonus outputs. Quantity is **not** a roll output for crafting resources.

### 17.1 Split

```text
SnapshotQuantity   = RecipeQuantity copied into CraftJob.IngredientsSnapshot[] at start time
BonusResultCount   = roll-driven additive bonus for OUTPUT (additional result items), not for input consumption
```

**Terminology lock:** `SnapshotQuantity` is the runtime per-unit input. `BonusResultCount` is the only RNG-driven numeric output. `BaseQuantity` does not exist in this design — the alias is collapsed into `SnapshotQuantity`.

**OUTPUT COUNT INVARIANT:**

```text
TotalOutputCount   = Count + BonusResultCount
Results.Length     == TotalOutputCount
```

Every entry in `Results[]` corresponds to exactly one output item instance. CraftRollService MUST produce exactly `TotalOutputCount` entries — neither more nor fewer. Phase A and Phase B operate over `Results[]`; any count mismatch between RollCraft output and persisted `Results` is a **bug**, not a tolerance window.

### 17.2 What Roll Affects

- Result **quality** (Common → Rare → ... → Divine tier upgrade chance).
- Result **affix** roll.
- Result **stat range** variation.
- Optional `BonusResultCount` for recipes configured with `AllowBonusResult = true` — adds extra output items, never modifies input consumption.

### 17.3 What Roll Does NOT Affect

- Input ingredient consumption (always `SnapshotQuantity × Count`).
- Water consumption (always read from `CraftJob.IngredientsSnapshot[]` for the `water` entry — JSON authority per §13).
- Decomposed consumption (always per-rarity table).

`CraftRollService` MUST NOT alter input quantities. If a recipe needs bonus output items, the recipe author sets `AllowBonusResult = true` and `MaxBonusResultCount` in JSON; the roll decides how many bonus outputs (if any) within that cap.

---

## 18. Economy Rules

### 18.1 Progression Discipline

- R1 equipment is intentionally easy to obtain.
- R6 Divine requires multi-session resource accumulation.
- Common materials cannot shortcut into Rare+ equipment — decomposed gate enforces this.
- Armor is the largest resource consumer per craft.
- Jewelry has small quantity but high material value.

### 18.2 Forbidden

```text
Common Material →  High-Rarity Equipment   (decomposed gate prevents this)
R5/R6 mass-production from Common drops alone
R4 cheaper than R3 of the same slot
```

### 18.3 Monotonic Cost Rule

For each equipment slot, across all rarities:

```text
weighted_cost(R1) < weighted_cost(R2) < ... < weighted_cost(R6)
```

Cost is `Σ (SnapshotQuantity × MaterialWeight[family])`. Violation = **Error** (regression).

### 18.4 Material Economic Weight

```text
Stone      = 1.0
Wood       = 1.0
Thread     = 1.0
Leather    = 1.0
Coal       = 1.2
Metal      = 1.5
Adhesive   = 0.8
Special    = 5.0
Water      = 0.1   (catalyst, near-free)
Progression = 0.0   (decomposed — see §18.5)
```

**TODO — Requires Code Verification:** confirm or replace weights from designer. Weights live in `dataConfigCrafting.json`, NOT in `CraftValidator`.

### 18.5 Progression Resource Weight

Decomposed items are weighted `0.0` in economic comparisons because they are generated by dismantle (player-owned by-product). They are still required for the gate, but they do not represent economic sink value. The gate's "difficulty" is the time investment of dismantling, not material cost.

### 18.6 Armor Economic Validation (Warning)

Armor MUST have the highest weighted material cost per craft within its rarity. Comparison is over weighted cost, NOT raw count.

```text
For every recipe R at rarity X:
  if recipe.EquipmentType == Armor:
    ArmorWeightedCost(recipe) ≥ WeightedCost(otherRecipe at rarity X) + 20% margin
    Else: WARNING (not Error — designer judgment still allowed).
```

Stays **Warning** until weighted costs are reliable across all recipes.

---

## 19. Craft Validation — Overview

Validation runs in four layers: **Structural → Item → Design → Economy**. Each layer has its own severity profile (§21).

```
Structural  →  JSON shape, schema correctness
Item        →  every Ingredient.ItemId resolves to a material with CraftingTier/CraftingFamily/Role
Design      →  material identity rules (§7), MaximumAllowedMaterialTier (§6.2)
Economy     →  monotonic cost (§18.3), Armor weighted cost (§18.6), sink coverage (§15)
```

---

## 20. Craft Validation — Layers

### 20.1 Structural Validation (Error)

Reject if any of the following:

- `SchemaVersion` invalid or missing.
- `Recipe.Id` empty or duplicate.
- `Recipe.Name` empty.
- `EquipmentType` not in the 11-slot enum.
- `Rarity` < 1 or > 6.
- `RecipeVersion` < 1.
- `CraftingProfile` not in {Small, Medium, Large, Heavy}.
- `Ingredients` empty or null.
- `Ingredient.Quantity` ≤ 0.
- Duplicate `Ingredient.ItemId` within the same recipe.
- Missing matrix slot (any of the 11×6 combinations absent).

### 20.2 Item Validation (Error)

Reject if any of the following:

- `Ingredient.ItemId` does not resolve via `ItemDatabase.GetItem(...)`.
- Resolved item missing `CraftingTier`, `CraftingFamily`, or `Role`.
- Resolved item has `Role != Material && Role != Catalyst && Role != Progression` (i.e., it's a non-crafting item).
- Water missing from any recipe (no ingredient with `Role = Catalyst`).

### 20.3 Design Validation (Error)

Reject if any material identity rule (§7) is violated. Composition check **excludes** ingredients with `Role = Catalyst` or `Role = Progression`.

- Hat primary family ≠ `Thread`.
- Cape primary family ≠ `Thread`.
- Armor missing `Leather` + `Metal` (excluding Catalyst/Progression).
- Gloves missing `Leather` + `Thread` (excluding Catalyst/Progression).
- Belt missing `Leather` + `Metal` (excluding Catalyst/Progression).
- Pants missing `Leather` + `Thread` (excluding Catalyst/Progression).
- Shoes missing `Leather` + `Wood` (excluding Catalyst/Progression).
- **Pendant:** must contain `Stone` + `Metal` (excluding Catalyst/Progression).
- **Ring:** must contain `Metal` + `Stone` (excluding Catalyst/Progression).
- **Earring:** must contain `Stone` + (`Metal` OR `Thread`) (excluding Catalyst/Progression).
- **Bracelet:** must contain `Metal` + `Stone` (excluding Catalyst/Progression).

Generic "Jewelry" rules are forbidden — the validator MUST check per-slot. The four jewelry slots have distinct material composition requirements and cannot share a single rule.
- Material `CraftingTier` > `MaximumAllowedMaterialTier` for that role (§6.2).
- R6 missing at least one ingredient with `CraftingFamily = Special` AND `CraftingTier = 6`.

### 20.4 Economy Validation (Warning / Error)

Warn or reject on:

- R4 weighted cost ≤ R3 weighted cost for same slot (**Error** — §18.3 regression).
- R5 weighted cost ≤ R4 weighted cost for same slot (**Error**).
- R6 weighted cost ≤ R5 weighted cost for same slot (**Error**).
- Ring/Earring primary quantity > 3 (**Error** — craft profile violation).
- Armor not the highest weighted cost at its rarity (**Warning** — §18.6).
- Regular material with zero recipes (**Error** — sink coverage, §15).
- High-tier material used at too low a rarity (**Warning**).
- `RecipeQuantity` vs `ExpectedQuantity` deviation > 20% (**Warning** — §16.5).
- Material sink imbalance (**Warning**).

---

## 21. Validation Severity

```text
Error    — Recipe must not be loaded into the runtime.
Warning  — Recipe loads, but must be flagged for designer attention.
Info     — Informational; no action required.
```

---

## 22. Editor Validation

```text
Menu: Tools → Crafting → Validate All Equipment Recipes
```

Output report:

```text
Recipes Loaded     : 66
Valid              : 66
Errors             : 0
Warnings           : 3
Regular Materials Covered : 39 / 39
Regular Materials Unused  : 0
```

Returns non-zero exit code on errors so CI can fail builds.

---

## 23. QA Test Matrix

### 23.1 Loading

```text
[ ] Exactly 66 recipes load.
[ ] Every recipe has a unique Id.
[ ] Every recipe resolves in ItemDatabase.
```

### 23.2 Water Coverage

```text
[ ] 66/66 recipes contain water.
[ ] Water quantity is present in `Ingredients[]` for every recipe (JSON authority — runtime never reads §13 table).
[ ] |JSON water quantity − §13 baseline| / §13 baseline ≤ 0.20 for every recipe (validator Warning if exceeded).
```

### 23.3 Decomposed Coverage

```text
[ ] R1 has no decomposed requirement.
[ ] R2 requires 1 Common.
[ ] R3 requires 2 Common + 1 Rare.
[ ] R4 requires 3 Common + 2 Rare + 1 Epic.
[ ] R5 requires 4 Common + 3 Rare + 2 Epic + 1 Legendary.
[ ] R6 requires 5 Common + 4 Rare + 3 Epic + 2 Legendary + 1 Mythic.
```

### 23.4 Material Identity

```text
[ ] All 11 slot identity rules pass (§7).
[ ] Catalyst and Progression ingredients excluded from composition check.
```

### 23.5 Economy Monotonicity

```text
[ ] For every slot: weighted cost(R1) < ... < weighted cost(R6).
[ ] Armor has highest weighted cost at every rarity (Warning level).
[ ] Ring/Earring primary quantity ≤ 3.
```

### 23.6 Completion Idempotency (CRASH-SAFE)

```text
[ ] Complete(job) once → exactly one reward set added.
[ ] Complete(job) twice → no duplicate reward (Results guard).
[ ] Simulate crash mid-Phase B (after some rewards added, before Results persisted as fully-completed) → reload → no duplicate reward, missing rewards added.
[ ] Simulate crash mid-Phase A (status = RewardPendingCommit, Results persisted) → reload → resume Phase B without re-rolling.
[ ] Reload save mid-craft → completion still produces exactly one reward.
```

### 23.7 Snapshot Stability

```text
[ ] Start craft at RecipeVersion = 1.
[ ] Update JSON to RecipeVersion = 2.
[ ] In-flight job still completes using snapshot v1 quantities.
[ ] TotalRequired = SnapshotQuantity × Count works for batch crafts.
[ ] New craft started after update uses v2 quantities.
```

### 23.8 Transaction Atomicity

```text
[ ] Insufficient material → no resource is consumed.
[ ] Job creation fails → all reservations (materials, water, decomposed) rolled back.
[ ] Commit fails after job creation → job cancelled AND all resources restored.
[ ] Decomposed requirement rolls back atomically with materials.
```

### 23.9 Save/Load

```text
[ ] Start craft → save → reload → craft completes with correct resources consumed.
[ ] Crash during commit → reload → no orphan consumption.
[ ] Crash during Complete (after status flip, before Phase B done) → reload → resume safely.
```

---

## 24. Craft Transaction

### 24.0 Two Architectures — Do Not Mix

This section describes TWO distinct transaction architectures. Implementers MUST read both and pick the one matching the build state.

```text
§24.1–§24.5  CURRENT IMPLEMENTATION  ← in production today, best-effort
§24.6       TARGET ARCHITECTURE     ← requires CraftTransactionJournal (not built)
```

**Do not** mix CURRENT steps with TARGET steps when describing runtime behavior. §31 (Runtime Flow) MUST reference the CURRENT architecture only.

### 24.1 CURRENT Pipeline — Best-Effort

```text
1. Validate                      (CraftValidator.CanCraft)
2. Reserve resources             (CraftTransactionService.BeginTransaction — in-memory)
3. Commit resources              (CraftTransactionService.Commit — consume now)
4. Create CraftJob with snapshot (CraftQueueService.StartCraft)
5. Persist CraftJob              (SaveManager — independent save call)
```

**Atomicity gap:** step 3 (consume) and step 5 (persist CraftJob) are NOT in the same atomic boundary. A crash between step 3 and step 5 leaves resources consumed but no CraftJob record. The window is small (single-process, short) but not zero.

**This is the runtime contract today.** §31 Runtime Flow MUST match this ordering exactly.

### 24.2 Failure Path (CURRENT)

If **any** step fails:

```text
Rollback all reservations (Materials + Catalyst + Progression)
    ↓
Return error (no partial state)
```

Once step 3 commits, there is no rollback for resource consumption. Crash recovery in this window is best-effort (see §24.5).

### 24.3 Forbidden States (CURRENT)

```text
Leather consumed, Water reserved, Decomposed missing → rollback all
Materials committed, Job creation failed               → impossible (job created before commit)
Phase A persisted, Phase B inventory mutation partial   → resume via §27
```

### 24.4 Resource Classes in Transaction

All three classes participate in the same atomic boundary:

```text
Materials   — leather, granite, ...            (Role = Material)
Catalyst    — water                             (Role = Catalyst)
Progression — decomposed_common, ...            (Role = Progression)
```

Reservation must include all three. Failure on any one rolls back all.

**TODO — Requires Code Verification:** confirm `CraftTransactionService` currently treats all three resource classes. If decomposed resources are not yet wired into transaction, that is a blocking implementation gap.

### 24.5 Honest Disclosure — CURRENT Best-Effort Crash Recovery

The CURRENT pipeline in §24.1 does NOT achieve atomic persistence across resource consumption and CraftJob creation. This is an acknowledged gap.

**Concrete failure window:**

```text
Step 3 (Commit — consume resources) executes
  ↓
[CRASH]
  ↓
Step 5 (Persist CraftJob) never runs
  ↓
Result: resources consumed, no job, no record, no recovery path
```

**Until `CraftTransactionJournal` is built, this window is accepted.**

### 24.6 TARGET Architecture — Requires CraftTransactionJournal

The target eliminates the §24.5 crash window using a write-ahead journal:

```text
1. Validate                      (CraftValidator.CanCraft)
2. Build immutable CraftJob snapshot (recipe version, ingredients, count,
   decomposed requirements, completion seed, context snapshot)
3. Journal intent                 (CraftTransactionJournal.Append — persisted first)
4. Reserve resources              (CraftTransactionService.BeginTransaction — in-memory)
5. Commit resources               (CraftTransactionService.Commit — consume)
6. Persist committed CraftJob     (SaveManager — with ProcessedRewardIndexes, status)
7. Clear journal entry            (CraftTransactionJournal.Clear — post-commit)
```

**Crash recovery (TARGET):**

```text
[CRASH at any step]
  ↓
On load: replay CraftTransactionJournal
  ↓
If journal entry exists with no committed CraftJob → Rollback resources
If journal entry exists with committed CraftJob → forward-resume Phase B (§27)
```

**Prerequisite for TARGET:** `CraftTransactionJournal` (write-ahead log of reservation entries, replay on load) MUST exist. Until then, §24.1 CURRENT is the only valid runtime contract.

**TODO — Requires Code Verification:** `CraftTransactionJournal` does NOT exist in the current codebase. Building it is a P0 prerequisite for switching to TARGET.

---

## 25. Craft Job Lifecycle

### 25.1 State Machine

```text
Queued  →  Crafting  →  RewardPendingCommit  →  Complete
                                  ↘
                                Failed
                                  ↘
                              Cancelled
```

### 25.2 Terminology — Commit, Not Claim

`RewardPendingCommit` is the **inventory commit window**, not a player-facing claim.

- Player does not need to click anything during this phase.
- The system is mid-mutation: rewards have been rolled and persisted, inventory is being updated.
- If a crash happens here, reload resumes without re-rolling.

`Claimed` is **not** added by default. The decision rule is below.

### 25.3 Existing `CraftJobStatus` enum (verified)

```text
Queued    = 0   (waiting for slot)
Crafting  = 1   (active)
Complete  = 2   (rewards generated and added)
Cancelled = 3   (player cancelled)
Failed    = 4   (roll / validation failure)
```

### 25.4 Decision Rule for `Claimed` State

```text
IF UI requires explicit [ CLAIM ] button:
    → Add RewardPendingClaim (= 5) and Claimed (= 6) to enum
    → Implement two-phase with player-triggered second phase

IF UI auto-collects rewards (current plan):
    → Do NOT add Claimed
    → RewardPendingCommit transitions directly to Complete
```

**Document decision now, before implementation.** Current intended behavior is **auto-collect → no Claimed state.**

### 25.5 Valid Transitions (auto-collect mode)

```text
Queued                → Crafting
Queued                → Cancelled
Crafting              → RewardPendingCommit
Crafting              → Cancelled
Crafting              → Failed
RewardPendingCommit   → Complete       (after Phase B inventory mutation done)
```

### 25.6 Invalid Transitions

```text
Claimed                → Crafting             (rejected — does not exist in auto-collect mode)
Complete               → Reserved             (rejected)
Cancelled              → Crafting             (rejected)
Failed                 → Crafting             (rejected)
RewardPendingCommit    → Crafting             (rejected)
```

---

## 26. Craft Job Snapshot

### 26.1 Snapshot Fields

```text
RecipeId
RecipeVersion
EquipmentType
Rarity
CraftingProfile
IngredientsSnapshot[].ItemId
IngredientsSnapshot[].Quantity   ← per UNIT (one craft)
Count                            ← batch multiplier
WaterQuantity
DecomposedRequirementsSnapshot[] ← per UNIT
StartTimeUtc
DurationTicks
Status
CompletionSeed                  ← RNG seed for Phase A roll (see §27.1)
CraftContextSnapshot             ← immutable copy of CraftContext at start time
```

### 26.2 Snapshot Semantics — Per Unit vs Batch

```text
IngredientsSnapshot[].Quantity    = quantity for ONE craft (per unit)
Count                             = number of crafts (batch)
TotalRequired                     = IngredientsSnapshot[].Quantity × Count
```

Example: `Craft x10` recipe Leather=8 → reservation reserves `8 × 10 = 80` leather.

### 26.3 Why

```text
Player starts craft x5
  RecipeVersion = 1
  Leather = 8 per unit
  Count = 5
  TotalReserved = 40

Designer updates JSON
  RecipeVersion = 2
  Leather = 12 per unit

In-flight job
  → uses snapshot v1 (Leather = 8 per unit, Count = 5, TotalReserved = 40)

New craft started after update
  → uses snapshot v2 (Leather = 12 per unit)
```

### 26.4 Required Additions (TODO — Requires Code Verification)

- Extend `CraftJob` with `IngredientsSnapshot[]`, `DecomposedRequirementsSnapshot[]`.
- Confirm `Count` already serializes (verified: `CraftJob.Count` exists; persists via `CraftJobSaveData.Count`).

---

## 27. Best-Effort Crash-Recoverable Completion

> **Scope:** This section provides **best-effort crash-recoverable** completion semantics. It is **not** strictly crash-safe until `Add Reward` + `Mark Processed` become a single atomic commit (requires `CraftTransactionJournal` per §24.6 TARGET, not built).
>
> Phase B has a small window where `AddItemInstance` succeeds but `MarkProcessed` persistence fails. On reload, the reward at that index WILL be re-added. The current contract RECOVERS rather than GUARANTEES no-duplicate-inventory.

### 27.1 INVARIANT (HARD RULE — Persisted Results Authoritative)

```text
Persisted Results ARE the authoritative completion state for a CraftJob.
When job.Results != null:
  - RNG MUST NEVER be invoked (regardless of how many times Complete(jobId) is called).
  - Recovery MUST use the persisted Results verbatim.
  - Phase B MUST operate on persisted Results, not re-derived values.

RNG is invoked exactly once per job — during the first Phase A execution,
when Results == null.
```

### 27.2 Mechanism — Two-Phase with Per-Reward Transaction ID

`CraftRewardTransactionId`:

```text
CraftRewardTransactionId = "{CraftJobId}#{rewardIndex}"
```

Where `rewardIndex` is the zero-based position of the reward in the `Results` array. The ID is **deterministic** from jobId + rewardIndex, so it does not need to be stored separately — it can be computed on demand.

### 27.3 Phase A — Pre-Mutation (safe to crash here)

```text
1. job.Status = RewardPendingCommit       ← PERSISTED
2. if job.Results == null:
     seed = (job.CompletionSeed != 0) ? job.CompletionSeed : GenerateSeed()
     if job.CompletionSeed == 0: job.CompletionSeed = seed    ← persist on first roll
     context = job.CraftContextSnapshot ?? _contextBuilder.Build()
     roll = RollCraft(job.RecipeId, context, seed)            ← RNG executed once
     rewards = GenerateRewards(roll, recipe, context)
     job.Results = CraftResultData.FromInventoryItems(rewards)
     for each reward:
       index = rewardIndex in Results[]                        ← assigned by order
   else:
     rewards = ConvertResultsToRewards(job.Results)           ← use existing, do NOT re-roll
3. SaveManager.MarkDirty()                                     ← Results + seed persisted to disk
4. ↓ if crash here, reload sees Results + RewardPendingCommit → resume Phase B
```

**Note:** `CraftRewardTransactionId` is **NOT** stored on rewards. It is computed on demand as `"{JobId}#{rewardIndex}"` per §27.7. No assignment line in Phase A.

### 27.4 Phase B — Inventory Mutation (idempotent)

```text
5. for each reward at index rewardIndex in rewards:
     if CraftRewardCommitter.HasProcessed(job.JobId, rewardIndex):
       skip                                       ← already added in prior partial run
     else:
       _inventory.AddItemInstance(reward)
       CraftRewardCommitter.MarkProcessed(job.JobId, rewardIndex)   ← recorded next
6. job.Status = Complete
7. SaveManager.MarkDirty()                            ← ProcessedRewardIndexes persisted
```

**Best-effort caveat:** step 5 has a window between `AddItemInstance` and `MarkProcessed` persistence where a crash causes the reward to be re-added on reload. This is the documented recovery gap; closing it requires `CraftTransactionJournal` (§24.6 TARGET).

### 27.5 Guard on Re-entry

```text
if job.Status == Complete:
    return                                    ← fully done

if job.Status == RewardPendingCommit:
    if job.Results == null:
        run Phase A from step 1                ← should not happen but defensive
    else:
        run Phase B from step 5                ← crash recovery
```

### 27.6 Crash Scenarios

| Crash point | Reload behavior | Reward result |
|---|---|---|
| Before Phase A step 1 | Status = Crafting, Results = null | Re-run `Complete()` → Phase A from scratch |
| After Phase A step 3 (Results persisted, Status = RewardPendingCommit) | Status = RewardPendingCommit, Results populated | Phase B resumes; transaction IDs prevent duplicates |
| Mid-Phase B (some rewards added) | Status = RewardPendingCommit, partial `ProcessedRewardIndexes[]` | Phase B resumes; missing rewards added; existing rewards skipped via `CraftRewardCommitter.HasProcessed` |

### 27.7 Ownership — CraftRewardCommitter Owns Idempotency

`CraftRewardCommitter` is the **single owner** of reward-mutation idempotency. `IInventoryService` is a passive sink — it MUST NOT track transaction history.

```text
CraftRewardCommitter.HasProcessed(jobId, rewardIndex)   ← query before mutation
CraftRewardCommitter.MarkProcessed(jobId, rewardIndex)  ← record after mutation
CraftJobSaveData.ProcessedRewardIndexes[]               ← only persisted state
```

`CraftRewardTransactionId` is **computed on demand** as `"{JobId}#{rewardIndex}"` — it is NOT a stored field. This avoids storing redundant data and keeps the contract purely deterministic from jobId + rewardIndex.

### 27.8 Required Additions (TODO — Requires Code Verification)

- Rewrite `CraftCompletionService.Complete(jobId)` to two-phase flow (§27.3, §27.4).
- Add `CraftRewardCommitter` class owning `HasProcessed`/`MarkProcessed` for reward idempotency.
- Add `ProcessedRewardIndexes[]` (int array) to `CraftJobSaveData` for save/load persistence.
- Add `CompletionSeed` (long) + `CraftContextSnapshot` (serialized struct) to `CraftJobSaveData`.
- Add `RewardPendingCommit = 5` to `CraftJobStatus` enum (if enum extension is approved).
- Remove any `IInventoryService.HasProcessedTransaction` / `MarkTransactionProcessed` methods — idempotency lives on `CraftRewardCommitter` only.

---

## 28. Failure & Recovery

| Failure | Behavior |
|---|---|
| Insufficient material / water / decomposed | Validation fails, no resources consumed. |
| Invalid recipe (missing) | `ValidationResult.Fail("Recipe not found")`. |
| Missing item | Validator returns `"Not enough {ItemId}"`. |
| Duplicate completion | Skipped via §27 guard (transaction IDs). |
| Application quit mid-craft | Job persists. On reload, `CalculateOfflineProgress()` resumes. |
| Application crash mid-Phase A | Reload sees `RewardPendingCommit` + persisted Results → resume Phase B. |
| Application crash mid-Phase B | Reload sees `RewardPendingCommit` + partial processed set → resume Phase B. |
| Application crash mid-Phase A before step 3 | Reload sees `Crafting` + null Results → re-run `Complete()`. |
| Save failure during commit | **Pre-commit** (Reserve in-memory fails or job creation fails): rollback all reservations, return error. **Post-commit** (resources already consumed, CraftJob not yet persisted): orphan-consumption risk — acknowledged in §24.1 / §24.5. No rollback possible. |
| Corrupted craft job | `LoadFromSaveData` filters invalid entries. |
| Outdated RecipeVersion | Job uses snapshot — no retroactive rebalance. |

**No silent material loss.** Every state transition is observable in logs and events.

---

## 29. Save Compatibility

### 29.1 Persistent State

```text
CraftRecipeRepositorySaveData  → UnlockedRecipeIds, KnownRecipeIds
CraftQueueSaveData             → Jobs[], MaxConcurrentJobs
```

### 29.2 Recipe JSON Changes

Recipe JSON changes are **balance changes**, not save schema changes.

- Existing jobs use snapshot (RecipeVersion at start time).
- New crafts use updated JSON.
- Removed recipes leave dangling IDs filtered by `if (_allRecipes.ContainsKey(id))`.

### 29.3 Active Job Persistence Requirements

`CraftQueueService` already serializes `JobId`, `RecipeId`, `StartTimeUtc`, `EndTimeUtc`, `DurationTicks`, `Count`, `Status`, `Results`, `FailureReason`. **TODO — Requires Code Verification**: extend `CraftJobSaveData` with `RecipeVersion`, `IngredientsSnapshot`, `DecomposedRequirementsSnapshot`, `CompletionSeed`, `CraftContextSnapshot`, `ProcessedRewardIndexes[]`.

**Note:** `CraftRewardTransactionId` is NOT a stored field per §27.7 — it is computed on demand as `"{JobId}#{rewardIndex}"`. Do NOT add `CraftRewardTransactionId[]` to save data.

### 29.4 Migration Rules

- Adding new field to `CraftJobSaveData` = backward compatible (default fills).
- Removing/renaming field = breaking change; requires `Game Save Version` bump.

---

## 30. Data Ownership

```text
dataItems.json
    → Item identity + CraftingTier + CraftingFamily + Role

dataRecipeEquipment.json
    → Recipe definition + ingredients + balance

dataConfigCrafting.json
    → Material economic weights (§18.4)

CraftRecipeRepository    → Recipe loading, lookup, unlock state
CraftValidator           → "Can this recipe be crafted?" (read-only)
CraftTransactionService  → Reserve / Commit / Rollback (Materials + Catalyst + Progression)
CraftQueueService        → Job lifecycle, progress, persistence
CraftCompletionService   → Two-phase reward orchestration (§27)
CraftRewardService       → Generate final InventoryItem instances
CraftRollService         → RNG for quality / affix / bonus output count
CraftContextBuilder      → Snapshot player state for crafting context
CraftPersistenceService  → SaveData façade
CraftRefundService       → Refund on cancel per policy
SaveManager              → Persistence orchestration (centralized)
```

No god object. Each has one responsibility.

---

## 31. Runtime Flow

```text
Player taps "Craft" UI
    ↓
CraftService.StartCraft(recipeId, count)
    ↓
CraftValidator.CanCraft(recipeId, count)             (includes decomposed requirement)
    ↓ (validation passed)
CraftTransactionService.BeginTransaction(recipe, count)   (reserve all 3 classes, in-memory)
    ↓ (resources reserved, NOT yet consumed)
CraftTransactionService.Commit()                      (consume resources — irreversible)
    ↓ (resources now gone from inventory)
CraftQueueService.StartCraft(recipeId, count)        (create CraftJob with snapshot)
    ↓ (jobId returned, CraftJob persisted)
Update() loop ticks → CraftQueueService.OnJobProgress
    ↓
OnJobCompleted → CraftCompletionService.Complete(jobId)   (two-phase, §27)
    ↓
Phase A: status = RewardPendingCommit, Results persisted
    ↓
Phase B: inventory mutation via CraftRewardCommitter (ProcessedRewardIndexes[])
    ↓
status = Complete → events fire → UI updates
```

**Ordering note:** this flow matches §24.1 CURRENT (Validate → Reserve → Commit → CreateJob → Save). Resource consumption happens BEFORE CraftJob creation — accepted orphan-consumption window per §24.5. Do NOT swap CreateJob before Commit without first building `CraftTransactionJournal` (§24.6 TARGET).

---

## 32. Implementation Contract

### 32.1 Existing Classes — Verification Status

| Class | Status | Responsibility | Forbidden |
|---|---|---|---|
| `CraftRecipeRepository` | **Verified** | Recipe lookup, unlock state, persistence | Inventory mutation, RNG |
| `CraftValidator` | **Verified** (lacks tier/family lookups) | Read-only "can craft?" check | Resource mutation |
| `CraftTransactionService` | **Verified** (decomposed wiring TBD) | Reserve / commit / rollback | Recipe lookup, RNG |
| `CraftQueueService` | **Verified** | Job lifecycle, progress, persistence | Reward generation |
| `CraftCompletionService` | **Verified** (idempotency incomplete) | Two-phase completion | Transaction logic |
| `CraftRewardService` | **Verified** | Generate `InventoryItem[]` from roll | Recipe lookup, inventory mutation |
| `CraftRollService` | **Requires Code Verification** | RNG quality / affix / bonus count | Input quantity mutation |
| `CraftContextBuilder` | **Requires Code Verification** | Build `CraftContext` snapshot | Mutation |
| `CraftPersistenceService` | **Verified** | SaveData façade | Craft logic |
| `CraftRefundService` | **Requires Code Verification** | Refund on cancel per policy | Craft start logic |
| `CraftJob` | **Verified** (lacks snapshot fields) | Job state container | Validation, RNG |
| `CraftJobStatus` | **Verified** (enum lacks RewardPendingCommit) | Job status enum | — |
| `ItemDatabase` | **Requires Code Verification** (lacks tier/family/role accessors) | Item resolution | Craft logic |
| `EconomyManager` | **Requires Code Verification** | Currency mutation | Craft logic |
| `SaveManager` | **Requires Code Verification** (transactional save unclear) | Persistence | Craft logic |

**Legend:**

- **Verified** — I read the source file in this session.
- **Requires Code Verification** — name referenced from project context; specific behavior not opened in this session.

### 32.2 Required Additions

```text
[Requires Code Verification]  CraftRecipeRepository.LoadRecipesFromJson()
[Requires Code Verification]  RecipeContainer / RecipeData / RecipeIngredient DTOs
[Requires Code Verification]  CraftJob.IngredientsSnapshot[], DecomposedRequirementsSnapshot[]
[Requires Code Verification]  CraftJob.CompletionSeed (long), CraftContextSnapshot (serialized struct)
[Requires Code Verification]  CraftJobSaveData.ProcessedRewardIndexes[] (int array) for reward commit idempotency
[Requires Code Verification]  CraftJobStatus.RewardPendingCommit = 5 (if auto-collect not chosen, add Claimed = 6)
[Requires Code Verification]  CraftCompletionService rewrite to two-phase (§27) using CraftRewardCommitter
[New]                         CraftRewardCommitter class — owns HasProcessed/MarkProcessed for reward idempotency
[DO NOT ADD]                  IInventoryService.HasProcessedTransaction / MarkTransactionProcessed — idempotency lives on CraftRewardCommitter only (§27.7)
[Requires Code Verification]  dataItems.json: CraftingTier + CraftingFamily + Role on every material
[Requires Code Verification]  CraftValidator.GetMaterialTier / GetMaterialFamily / GetRole accessors
[Requires Code Verification]  CraftTransactionService: include decomposed resources in transaction
[New]                         Assets/Resources/Data/dataConfigCrafting.json (material weights)
[New]                         Assets/Editor/CraftingValidatorMenu.cs (§22)
[New]                         Assets/Scripts/Items/Tests/CraftValidationTests.cs (§23)
```

---

## 33. Files Touched

| File | Change | Status |
|---|---|---|
| `Assets/Resources/Data/Crafting/dataRecipeEquipment.json` | 66 recipes | **TODO** |
| `Assets/Resources/Data/dataItems.json` | Register `decomposed_*`, `water`, add `CraftingTier`/`CraftingFamily`/`Role` | **PARTIAL** |
| `Assets/Resources/Data/Crafting/dataConfigCrafting.json` | New — material weights | **TODO** |
| `Assets/Scripts/Items/CraftRecipeRepository.cs` | Add `LoadRecipesFromJson()` | **TODO** |
| `Assets/Scripts/Items/CraftJob.cs` | Add snapshot + transaction ID fields | **TODO** |
| `Assets/Scripts/Items/CraftCompletionService.cs` | Rewrite to two-phase | **TODO** |
| `Assets/Scripts/Items/CraftValidator.cs` | Add tier/family/role lookups + decomposed requirement | **TODO** |
| `Assets/Scripts/Items/CraftTransactionService.cs` | Wire decomposed into transaction | **TODO** |
| `Assets/Editor/CraftingValidatorMenu.cs` | New — validation menu | **TODO** |
| `Assets/Scripts/Items/Tests/CraftValidationTests.cs` | New — EditMode tests | **TODO** |
| `Assets/Resources/Data/Design/Crafting_Design.md` | This file | **REFACTORED (v2.2)** |

---

## 34. Implementation Order

Do not start with UI. Crashes and idempotency bugs surface only when transaction, snapshot, and completion are correct. UI built on broken plumbing makes bugs harder to find.

```text
 1. Finalize domain terminology              ← this document (v2.2)
 2. Verify existing code                     ← open CraftJob, CraftQueueService, CraftCompletionService, CraftTransactionService, SaveManager source
 3. Finalize DTO classes                     ← RecipeContainer, RecipeData, RecipeIngredient, snapshot fields
 4. Finalize dataItems.json                  ← add CraftingTier + CraftingFamily + Role to every material
 5. Finalize dataConfigCrafting.json         ← material economic weights (§18.4)
 6. Implement repository                     ← CraftRecipeRepository.LoadRecipesFromJson
 7. Implement validator                      ← tier/family/role lookups, decomposed requirement, weighted Armor check
 8. Implement transaction                    ← wire decomposed into Reserve/Commit/Rollback
 9. Implement CraftJob snapshot              ← IngredientsSnapshot, DecomposedRequirementsSnapshot, Count semantics
10. Implement crash-safe completion          ← two-phase + transaction IDs (§27)
11. Editor validation                        ← Tools → Crafting → Validate All Equipment Recipes
12. Automated tests                          ← §23 test matrix as EditMode tests
13. UI integration                           ← only after steps 1–12 are verified
```

---

## 35. Non-Goals

- Recipe discovery / quest system — out of scope.
- Recipe modification / upgrade — out of scope.
- Cross-slot material substitution — identity rules are strict.
- Stack-based discount — recipes consume flat quantity.
- Runtime procedural recipe generation — recipes are explicit JSON.

---

## 36. Audit Checklist

Legend:

```text
[✓]  defined AND verified against runtime code
[~]  defined in spec but runtime implementation unverified (requires §34 step)
[!]  unresolved / open issue / blocker
```

```text
[✓] 11 equipment types
[✓] 6 rarities
[~] 66 recipes — 3 sample recipes authored in dataRecipeEquipment.json; remaining 63 TODO
[~] 39 regular materials — taxonomy defined; dataItems.json registration TODO
[✓] Water separated (Catalyst, Role = Catalyst, CraftingTier = 0)
[✓] 5 decomposed separated (Progression, excluded from sink coverage)
[✓] Water required by every recipe
[✓] Decomposed progression defined (derived from rarity, not authored)
[✓] Material identity defined (§7) — family-based, data-driven, Catalyst/Progression excluded
[✓] Crafting profiles defined (§8) — validator reference only
[✓] Cost scaling defined (§16) — SnapshotQuantity from JSON, ExpectedQuantity validator-only
[✓] Single quantity source of truth enforced (§16.6 consumer whitelist)
[✓] Recipe schema defined (§11) — single Quantity field, no BaseProfileQuantity
[✓] Primary/Secondary ordering rule defined (§11.4) — declaration order, Material only
[✓] SchemaVersion / RecipeVersion / GameSaveVersion independent (§12)
[~] Craft snapshot defined (§26) — spec complete; snapshot fields implemented in CraftJob TODO
[✓] CompletionSeed + CraftContextSnapshot in snapshot (§26.1) — for RNG re-roll contract
[✓] Material Tier Resolution defined (§5.4) — data-driven via dataItems.json
[~] Material Economic Weight defined (§18.4) — spec complete; dataConfigCrafting.json TODO
[✓] Leather family added to CraftingFamily enum (§5.1)
[✓] Stone vs Mineral terminology unified (§5.4)
[~] Transaction safety defined (§24) — best-effort crash recovery until transaction journal exists
[~] Decomposed equipment wired into transaction (§24.4) — spec complete; CraftTransactionService wiring TODO
[~] Crash-safe idempotency via two-phase + CraftRewardTransactionId (§27) — spec complete; implementation TODO
[~] CraftRewardCommitter ownership (§27.7) — new class; not yet present in codebase
[✓] RewardPendingCommit = commit window, not UI claim (§25.2)
[✓] Claimed state decision rule documented (§25.4)
[✓] Results invariant: generated exactly once (§27.1)
[✓] RNG re-roll contract: persisted seed + context (§27.1)
[✓] Save compatibility defined (§29)
[✓] Validation defined (§19-§20, four layers)
[✓] Validation severity defined (§21)
[~] Editor validation defined (§22) — spec complete; menu implementation TODO
[~] QA test matrix defined (§23) — spec complete; EditMode tests TODO
[✓] Material sink coverage defined (§15) — regular materials only
[✓] Failure recovery defined (§28)
[✓] Armor validation = Warning, weighted (§18.6)
[✓] Iron Dust provenance clarified (§5.3)
[✓] R6 Special family + CraftingTier = 6 requirement (§20.3)
[✓] Jewelry per-slot rules eksplisit (§20.3) — Pendant/Ring/Earring/Bracelet individually
[✓] ExpectedQuantity consumer whitelist enforced (§16.6)
[✓] CraftRollService Base vs Bonus split (§17)
[✓] Transaction order unified (§24, §31)
[!] SaveManager atomicity gap acknowledged but not closed (§24.5) — requires CraftTransactionJournal
[✓] Implementation order defined (§34) — 13 steps, not UI-first
[~] No contradictory rules — review-pass complete; next review may surface new contradictions
[~] No undefined terminology — locked glossary; minor §11.4 alias added this pass
[✓] No invented APIs (all claims tagged Verified or Requires Code Verification)
```

---

