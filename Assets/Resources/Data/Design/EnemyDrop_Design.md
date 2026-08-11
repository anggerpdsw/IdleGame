# Enemy Drop System — Design Doc

Status: **Implemented v3 (rebalanced 2026-08-11)**. Runtime: enemy death → material drop → inventory.

## 1. Schema — `dataEnemy.json` → `EnemyData.dropItems`

```json
{
  "id": "Rock Golem",
  "element": "Earth",
  "exp": 14,
  "...existing fields...": null,
  "dropItems": [
    { "ItemId": "rock",      "Weight": 16, "MinCount": 1, "MaxCount": 2, "MinTier": 1 },
    { "ItemId": "granite",   "Weight": 8,  "MinCount": 1, "MaxCount": 1, "MinTier": 2 },
    { "ItemId": "corundum_powder", "Weight": 4, "MinCount": 1, "MaxCount": 1, "MinTier": 3 }
  ]
}
```

- Type: `DropEntry[]` (exists in `ItemData.cs`, `IdleDefenseSurvival.Items`).
- **`Weight` = chance percent, scale 0–100.** Consistent with `Utilityku.Chance(percent)` (`Random.Range(0f,100f) < percent`).
- `MinCount`/`MaxCount`: inclusive; quantity = `Random.Range(min, max+1)`, clamped ≥1.
- **`MinTier` = game tier gate = material `ItemRarity`** (1=Common…4=Legendary, see §3).
- Each entry rolls **independently** — an enemy may drop 0, 1, or all of its entries. No weighted single-pick.

## 2. Runtime flow

```
Enemy eliminated (EnemyAi.Die)
  └→ DropRewards()            — existing: gold/exp/gem/meat, UNCHANGED (physical pickups)
       └→ DropItemDrops()     — NEW, private in EnemyAi (additive)
            per entry:
              Utilityku.Chance(Weight) → quantity → InventoryService.Instance.AddItem(itemId, qty)
```

- `InventoryService.AddItem` handles stacking, empty-slot fill, capacity (full → warning + abort, **no item loss**), auto-expand, `OnItemAdded`/`OnInventoryChanged` events, `MarkSaveDirty`.
- Zero exceptions; null-safe (`InventoryService.Instance` guard).
- Unknown `ItemId` → `Debug.LogWarning` + skip (defensive; validator guarantees none).
- `EnemySpawner.SpawnEnemy()` copies `dropItems = rawData.dropItems` onto the wave-scaled clone — required, otherwise drops are empty.

## 3. Progression tiers (material gating)

Tiered by `ItemRarity` + `SellPrice` in `dataItems.json` (crafting recipes do not exist yet — see §6):

| Tier | Rarity | Sell price | Materials |
|---|---|---|---|
| **T1 Common** | 1 | 1–8 | rock, stone_dust, sandstone, iron_dust, logs, disposed_logs, rough_lumber, cotton_thread, coal, coal_dust, charcoal, pig_iron, organic_glue |
| **T2 Uncommon** | 2 | 10–20 | granite, fine_lumber, thick_thread, silk_thread, anthracite, high_carbon_steel, concentrated_glue, strong_glue, colored_glue |
| **T3 Rare** | 3 | 35–100 | corundum_powder, rubstone_powder, compound_thread, azureworm_silk, extruded_charcoal, refined_steel, steel_alloy, super_glue, compound_glue, high_grade_lumber |
| **T4 Ephemeral** | 4 | 150–1500 | lumber_essence, vega_string, high_alloy_steel, elemental_essence, dream_of_reminiscence, essence_of_hope |

**Gates (enforced by validator + runtime):**

- **`MinTier` = `ItemRarity`** of the material (T1=Common, T2=Uncommon, T3=Rare, T4=Legendary).
- Runtime skip in `EnemyAi.DropItemDrops()`: `if (entry.MinTier > WaveManager.Instance.CurrentTier) continue;`
  → Tier 1 (waves 1–350 of tier 1) **only drops T1 materials**. T2 becomes visible in Tier 2, T3 in Tier 3, T4 in Tier 4.
- Exp gates only gate *source availability* within a tier run (all enemies spawn from tier 1; exp ≥ 7/12 enemies appear progressively). The tier gate is what blocks progression-2/3 materials in Tier 1 — exp alone cannot (exp 10–16 enemies spawn in tier 1).
- Pool size: exp 1–9 → max 2 entries | exp 10+ → max 3 | BOSS → max 4

## 4. Drop rates (per-kill expected values)

| Band | exp | Entries | Expected items/kill |
|---|---|---|---|
| Early | 1–6 | 1 × 22% | ~0.22 |
| Mid | 7–9 | 1 × 18–22% + 1 × 5–8% | ~0.27 |
| High | 10–13 | 1 × 15–18% + 1 × 8% + 1 × 3–6% | ~0.30 |
| Elite | 14+ | 1 × 15–16% + 1 × 8% + 1 × 3–4% | ~0.28 |
| BOSS | — | 1 × 25% (qty 2–3) + 1 × 15% + 1 × 6–10% + 1 × 1% | ~0.92 (boss kills are rare) |

Global weighted average: **~0.33 items/kill**.

### Expected accumulation (spawn curve: interval 1.51s → min 0.117s, decay over 200 waves)

| Horizon | Kills | Total items (expected) |
|---|---|---|
| W1–18 (Tier 1 early) | ~400 | ~132 |
| 10 waves (W10–19) | ~236 | ~78 |
| 1 full tier (350 waves) | ~57,000 | ~18,800 |

Material-level (weighted over full pool, per tier): rock ~5,553 | cotton_thread ~2,904 | iron_dust ~2,641 | coal ~2,008 | disposed_logs ~1,593 | organic_glue ~1,190 | stone_dust ~490 | silk_thread ~329 | granite ~325 | high_carbon_steel ~266 | fine_lumber ~222 | charcoal ~212 | coal_dust ~180 | pig_iron ~136 | anthracite ~128 | concentrated_glue ~108 | thick_thread ~78 | rough_lumber/logs ~69 | strong_glue ~62 | corundum_powder ~50 | sandstone ~48 | extruded_charcoal ~23 | azureworm_silk/high_grade_lumber ~21 | dream_of_reminiscence ~17 | elemental_essence ~4 | high_alloy_steel ~1.7.

**Tier 1 W1–18 (target gate):** common T1 mats flow steadily (rock ~23/10w, cotton ~12, iron ~11, coal ~8, logs ~7); D2 (granite, silk, glue, lumber) arrive at **1–2/hour rate**; T3+ materials do **not** appear at all (first T3 sources need exp ≥ 12 enemies, unavailable until late tier / min ~2–3% on the few that exist). `dream_of_reminiscence` is BOSS-adjacent only (Devourer, Dark Sage) at ≤2% — **unobtainable in Tier 1**.

## 5. Source mapping (element/role family)

| Family | Enemies | Materials |
|---|---|---|
| Earth/Stone | Rock Golem, Stone Rhino, Cyclops, Rock Mimic, Crystal Cluster, Boulderling, Sand Worm, Crystal Ogre, Stone Titan, Stone Golem, Burrower | rock, stone_dust, sandstone, granite, corundum_powder |
| Fire | Magma Golem, Fire Wisp, Fire Shaman, Fire Beetle, Embershard Golem, Flame Core, Lava Beast, Crimson Imp, Demon Pup, Infernal Lord | coal, coal_dust, charcoal, anthracite, extruded_charcoal |
| Wood | Log Mimic, Thorn Hulk, Moss Slime, Swamp Brute, Mushroom Beast, Bramble Chief, Thorn Golem, Thorn Sprite, Forest Archer, Archer Elf | disposed_logs, logs, rough_lumber, fine_lumber, high_grade_lumber |
| Metal/Skeleton | Steel Wolf, Skeleton Guard, Skeleton Raider, Skeleton Duelist, Goblin Ranger, Shield Kobold, Bone Archer, Guardian Golem, Sentinel, Watcher, Mecha Crawler | iron_dust, pig_iron, high_carbon_steel |
| Slime/Ooze | Green Slime, Aqua Slime, Purple Ooze, Elder Ooze, Moss Slime, Venom Lizard | organic_glue, concentrated_glue, strong_glue |
| Insect/Thread | Hornet, Cannon Bug, Jet Beetle, Frost Beetle, Buzz Drone | cotton_thread, thick_thread, silk_thread, azureworm_silk |
| Water/Ice | Ice Golem, Ice Mage, Ice Wisp, Ice Sprite, Frost Adept, Ice Wolf, Aqua Slime, Yeti, Water Spirit | cotton_thread, silk_thread, azureworm_silk |
| None/Dark/generic | Devourer, Void Puff, Void Mage, Dark Sage, Shadow Stalker, Ash Assassin, Night Rogue, Shadow Ninja, Rogue Imp, bats, orcs | rock, stone_dust, iron_dust; Devourer/DarkSage → dream_of_reminiscence |
| BOSS | Infernal Lord, Stone Titan, Red-Eye Tank | common T1 + T2 + T3 + 1% T4 (elemental_essence / high_alloy_steel) |

## 6. UNRESOLVED materials (banned from drops until a source is designed)

| Material | Rarity | Reason |
|---|---|---|
| rubstone_powder | T3 | no enemy family matches; craft sink unknown |
| lumber_essence | T4 | endgame essence; no wood enemy high enough yet (Thorn Hulk 4% is its only candidate — held back) |
| vega_string | T4 | endgame thread; no source enemy designed |
| compound_thread | T3 | mid thread; no source enemy designed |
| refined_steel / steel_alloy / high_alloy_steel | T3/T4 | only boss-mapped; full steel pipeline needs recipe data |
| super_glue / colored_glue / compound_glue | T3 | only boss-mapped; glue pipeline needs recipe data |
| chocolate_additive / chocolate_syrup / edible_pigment | — | culinary shop-buy items, not monster loot |
| essence_of_hope | T4 | endgame; no source designed |
| UltimateStone_* | — | reward/shop items, not monster loot |

**Do not add these to drop tables to fill loot pools.** When crafting recipes / equipment requirements exist (currently `CraftRecipeRepository` loads from `EquipmentData.CraftRecipe` — all 7 equipment have no recipe; `dataCraftRecipes.json` is a TODO), re-derive material tiering from recipe `RequiredTier` instead of rarity.

## 7. Validation & tooling

`tools/gen_enemy_drops.cjs` — regenerate v3 tables from the D map (run only when editing the map).
`tools/validate_enemy_drops.cjs` — CI-style check (run after any `dataEnemy.json`/`dataItems.json` edit):

```
node tools/validate_enemy_drops.cjs
```

Checks: every enemy has dropItems; every ItemId ∈ `dataItems.json`; `Weight ∈ [0,100]`; `Min ≥ 1`, `Min ≤ Max`; no per-enemy duplicates; pool-size cap; **progression gates** (T2 exp≥7, T3 exp≥12, T4 boss-only & ≤2%); **banned list** (§6). Current state: **84/84 enemies, 165 entries, 28 material IDs, PASS.**

`tools/analyze_drops.cjs` — expected-drop calculator (kills/wave from wave JSON curve, spawn-weighted rates). Re-run when tuning.

## 8. Explicit non-goals / preserved behavior

- NO `dataDropTables.json` per-enemy (file duplication, two-place mapping).
- NO wiring of enemy death to `LootGenerator`/`ItemGenerator` — those re-roll gold/gem/meat → double currency.
- Currency flow (gold/exp/gem/meat), wave formulas, save format, `dataItems.json`, combat: untouched.
- `DropTableId` kept (dead field, removal = risk without benefit).
- No new ItemId; drops reference existing materials only.
- Combat balance, enemy stats, wave duration, XP, gold, cards, equipment: **not modified** — this rebalance touched only drop data + the additive `DropItemDrops()` call.