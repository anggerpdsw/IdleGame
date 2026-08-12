# Crafting Design — Equipment Crafting & Recipe System

## 1. Scope

Defines equipment crafting: 11 equipment slots × 6 rarities = 66 recipes. Each recipe consumes primary materials + secondary materials + core craft requirement (water + decomposed equipment).

## 2. Data Source of Truth

Recipe data lives in:

`Assets/Resources/Data/dataRecipeEquipment.json`

Format:

```json
{
  "Recipes": [
    {
      "Id": "craft_<equipment_id>",
      "Name": "<Display Name>",
      "Category": "Equipment",
      "EquipmentType": "<Hat|Gloves|Cape|Armor|Belt|Pants|Pendant|Ring|Earring|Bracelet|Shoes>",
      "Rarity": <1..6>,
      "Ingredients": [
        { "ItemId": "<id>", "Quantity": <n> }
      ]
    }
  ]
}
```

### 2.1 Why JSON (not inline `EquipmentData.CraftRecipe`)

- 66 recipes inflate `dataItems.json` by ~1500 lines.
- Equipment definitions change rarely; recipe balance tweaks frequently.
- Single responsibility: `dataItems.json` owns item/equipment definitions; `dataRecipeEquipment.json` owns crafting rules.
- Matches project pattern (`dataCard.json`, `dataWave.json`, etc. — balance data lives in dedicated files).

### 2.2 Load Path

```
dataRecipeEquipment.json
    ↓ Resources.Load<TextAsset>("Data/dataRecipeEquipment")
CraftRecipeRepository.LoadRecipesFromJson()  ← TO IMPLEMENT
    ↓ JsonConvert.DeserializeObject<RecipeContainer>
Dictionary<string, CraftRecipeData> _allRecipes
    ↓ ItemDatabase.GetItem(ingredient.ItemId)
Ingredient validation
```

## 3. Core Craft Requirements

Every recipe R≥2 requires **two ingredient classes** in addition to its primary/secondary materials.

### 3.1 Water

Water is a mandatory ingredient for **every** recipe (including R1). It is not part of the 39 regular materials — it represents the casting medium.

| Rarity | Water Quantity |
|---|---|
| 1 Common | 10 |
| 2 Rare | 20 |
| 3 Epic | 30 |
| 4 Legendary | 50 |
| 5 Mythic | 80 |
| 6 Divine | 120 |

`ItemId: "water"` — registered in `dataItems.json`.

### 3.2 Decomposed Equipment

Decomposed equipment is the byproduct of dismantling equipment at lower rarities. Mandatory for R≥2.

| Target Rarity | Decomposed Requirements |
|---|---|
| 1 Common | — |
| 2 Rare | 1× Common |
| 3 Epic | 2× Common + 1× Rare |
| 4 Legendary | 3× Common + 2× Rare + 1× Epic |
| 5 Mythic | 4× Common + 3× Rare + 2× Epic + 1× Legendary |
| 6 Divine | 5× Common + 4× Rare + 3× Epic + 2× Legendary + 1× Mythic |

ItemIds: `decomposed_common`, `decomposed_rare`, `decomposed_epic`, `decomposed_legendary`, `decomposed_mythic` — registered in `dataItems.json`.

### 3.3 Why Decomposed Equipment Exists

- Prevents R5/R6 from being mass-crafted from Common drops alone.
- Creates a long-term crafting sink: every crafted Common feeds into Rare→Epic→...→Divine.
- Reinforces rarity as meaningful progression, not just stat bloat.

## 4. Material Identity Per Slot

Each equipment slot has a fixed material identity. Recipes must respect this.

| Slot | Primary Material | Secondary Material | High-Tier Material |
|---|---|---|---|
| Hat | Thread | Leather / Adhesive | Essence |
| Gloves | Leather | Thread | High-rarity Thread / Metal |
| Cape | Thread | Adhesive | Essence |
| Armor | Leather | Metal | Steel + Coal processing |
| Belt | Leather | Metal / Thread | High-rarity Steel |
| Pants | Leather | Thread | High-rarity Thread |
| Pendant | Mineral | Metal | Essence |
| Ring | Metal | Mineral | Essence |
| Earring | Mineral | Metal / Thread | Essence |
| Bracelet | Metal | Mineral | Essence |
| Shoes | Leather | Wood | High-grade Wood |

**Material identity rules:**

- Hat must not use Metal as primary (would feel like Armor).
- Jewelry (Pendant/Ring/Earring/Bracelet) must combine Mineral + Metal.
- Shoes must use Leather + Wood (sole).
- Armor is the highest-quantity consumer of Metal + Coal.

## 5. Rarity Progression

### 5.1 Material → Rarity Mapping

| Rarity | Stone | Wood | Thread | Coal | Metal | Adhesive | Special |
|---|---|---|---|---|---|---|---|
| 1 Common | Rock | Disposed Logs | Cotton Thread / Leather | Coal | Pig Iron | Organic Glue | — |
| 2 Rare | Stone Dust | Logs | Thick Thread | Coal Dust | High-carbon Steel | Concentrated Glue | — |
| 3 Epic | Granite | Rough Lumber | Silk Thread | Charcoal | Refined Steel | Strong Glue | — |
| 4 Legendary | Sandstone | Fine Lumber | Compound Thread | Anthracite | Steel Alloy | Colored Glue | — |
| 5 Mythic | Iron Dust (alt) | High-grade Lumber | Azureworm Silk | Extruded Charcoal | High Alloy Steel | Super Glue | — |
| 6 Divine | Corundum Powder / Rubstone Powder | Lumber Essence | Vega String | — | — | Compound Glue | Elemental Essence, Dream of Reminiscence, Essence of Hope |

Note: `Iron Dust` is positioned as R5 in some identities (Ring/Earring/Bracelet primary progression aligns with Metal going from Pig Iron → High Alloy Steel — `iron_dust` is a by-product processed form).

### 5.2 Equipment Progression Names

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

## 6. Recipe Cost Design

Equipment size determines material quantity. Scales up by rarity.

### 6.1 Size Categories

| Size | Slots | Primary Qty | Secondary Qty |
|---|---|---|---|
| Small | Ring, Earring | 1-3 | 1-2 |
| Medium | Hat, Gloves, Belt, Pendant, Bracelet | 2-5 | 1-4 |
| Large | Cape, Pants, Shoes | 4-8 | 2-5 |
| Heavy | Armor | 8-15 | 4-10 |

### 6.2 Rarity Scaling (within size class)

| Rarity Tier | Multiplier vs R1 |
|---|---|
| R1 | 1.0× |
| R2 | 1.2× |
| R3 | 1.5× |
| R4 | 2.0× |
| R5 | 3.0× |
| R6 | 4.5× |

Armor adds Coal processing material at 3-8 quantity (Heavy class only).

## 7. Economy Rules

### 7.1 Long-Term Progression

Equipment crafting is a **long-term sink**, not early-game resource:

- R1 equipment is intentionally easy to obtain.
- R6 Divine equipment must require multi-session resource accumulation.
- Common materials cannot shortcut directly into Rare-or-higher equipment — decomposed equipment gate enforces this.

### 7.2 Material Value Hierarchy

```
Rare Material  →  Rare Equipment  →  High Equipment Value
Common Material  →  Common Equipment only
```

Forbidden:

```
Common Material  →  High-Rarity Equipment   (this is what decomposed gate prevents)
```

## 8. Implementation Requirements

### 8.1 Wiring (TODO — Required)

`CraftRecipeRepository.LoadRecipesFromDatabase()` currently only iterates `ItemDatabase.AllEquipment`. Add JSON loader:

```csharp
private void LoadRecipesFromJson()
{
    var jsonAsset = Resources.Load<TextAsset>("Data/dataRecipeEquipment");
    if (jsonAsset == null) return;

    var container = JsonConvert.DeserializeObject<RecipeContainer>(jsonAsset.text);
    foreach (var recipe in container?.Recipes ?? new List<RecipeData>())
    {
        var craftData = new CraftRecipeData
        {
            RecipeId = recipe.Id,
            // ... map fields
        };
        _allRecipes[recipe.Id] = craftData;
    }
}
```

Call `LoadRecipesFromJson()` from `Initialize()` (line 31).

### 8.2 DTO Classes

```csharp
[Serializable]
public class RecipeContainer
{
    public List<RecipeData> Recipes;
}

[Serializable]
public class RecipeData
{
    public string Id;
    public string Name;
    public string Category;
    public string EquipmentType;
    public int Rarity;
    public List<RecipeIngredient> Ingredients;
}

[Serializable]
public class RecipeIngredient
{
    public string ItemId;
    public int Quantity;
}
```

### 8.3 ItemId Resolution

Each ingredient's `ItemId` must resolve via `ItemDatabase.Instance.GetItem(itemId)` (line 113 of `ItemDatabase.cs`). Resolution failures should log a warning and skip the ingredient at validation time, not at load time (allows partial loading during development).

### 8.4 Validation Rules (Enforced)

The following are design invariants. Any recipe violating them must be rejected at load:

1. All 11 equipment categories must be present.
2. Every recipe must include `water` as an ingredient.
3. All ingredient `ItemId`s must resolve in `ItemDatabase`.
4. No duplicate `craft_*` IDs.
5. Material rarity must match or be lower than recipe rarity.
6. R1 recipes must not use any R5/R6 materials.
7. R6 recipes must use at least one special/essence material.
8. Armor quantity must exceed all other categories for the same rarity.
9. Ring/Earring primary quantity ≤ 3.
10. Shoes must contain Leather + Wood.
11. Armor must contain Leather + Metal.
12. Jewelry must contain Mineral + Metal.
13. Cape primary must be Thread.
14. Hat primary must be Thread.
15. Gloves must contain Leather + Thread.
16. Belt must contain Leather + Metal.
17. Pants must contain Leather + Thread.
18. Bracelet primary must be Metal + Mineral secondary.

### 8.5 Save Compatibility

Recipe JSON changes are **balance changes**, not save schema changes. Existing saves remain valid because:

- `CraftRecipeRepository` persists only `UnlockedRecipeIds` + `KnownRecipeIds` (line 167-176).
- Recipe definitions are reloaded from JSON on every session start.
- Removed recipes leave dangling IDs in old saves — handled by `LoadFromSaveData` line 184: `if (_allRecipes.ContainsKey(id)) _unlockedRecipeIds.Add(id);` already filters dangling IDs.

## 9. Event Flow

```
Player taps "Craft" UI
    ↓
CraftService.StartCraft(recipeId)
    ↓
CraftValidator.CanCraft(recipeId)         → checks ingredients via ItemDatabase
    ↓
CraftTransactionService.BeginTransaction() → reserves materials atomically
    ↓
CraftQueueService.StartCraft()             → creates job, fires OnCraftStarted
    ↓
Update() loop ticks → CraftQueueService.OnJobProgress → OnCraftProgress event
    ↓
OnJobCompleted → CraftCompletionService.Complete()
    ↓
CraftRewardService creates equipment item (ItemGenerator)
    ↓
OnCraftResult(recipeId, resultItems)       → UI displays result
```

## 10. Files Touched

| File | Change |
|---|---|
| `Assets/Resources/Data/dataRecipeEquipment.json` | New — 66 recipes (DONE) |
| `Assets/Resources/Data/dataItems.json` | Pre-existing — registers decomposed_<rarity> + water ItemIds (DONE) |
| `Assets/Scripts/Items/CraftRecipeRepository.cs` | Add `LoadRecipesFromJson()` and call from `Initialize()` (TODO) |
| `Assets/Scripts/Items/CraftService.cs` | No change — already calls `_repository.Initialize()` |
| `Assets/Scripts/Items/ItemDatabase.cs` | No change — `GetItem()` already exists |
| `Assets/Resources/Data/Design/Crafting_Design.md` | This file (DONE) |

## 11. Follow-Up Tasks (Owners)

1. **Craft domain owner**: implement `LoadRecipesFromJson()` in `CraftRecipeRepository.cs`. Add `RecipeContainer` / `RecipeData` / `RecipeIngredient` DTOs.
2. **Item domain owner**: confirm `decomposed_*` ItemIds have correct `Category: 3` (Material) and `StackSize` (recommend 999, consistent with other materials).
3. **QA**: write EditMode test asserting 66 recipes loaded, all R≥2 have water + decomposed, all `ItemId`s resolve.
4. **UI**: update Craft UI to display decomposed requirement visually (currently UI may only show regular materials).

## 12. Non-Goals

- Recipe discovery/quest system — out of scope.
- Recipe modification/upgrade — out of scope.
- Cross-slot material substitution — identity rules are strict.
- Stack-based discount — recipes consume flat quantity.
