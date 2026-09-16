# Crafting System Design — IdleDefenseSurvival

**Purpose:** Item creation pipeline, recipe validation, queue management, crafting modifiers, persistence.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Material_Design.md](./Material_Design.md) — crafting ingredients
- [Herb_Design.md](./Herb_Design.md) — potion ingredients
- [Equipment_Design.md](./Equipment_Design.md) — crafted equipment
- [Consumable_Design.md](./Consumable_Design.md) — crafted potions
- [Economy_Design.md](./Economy_Design.md) — gold costs
- [Modifier_Design.md](./Modifier_Design.md) — stat calculation
- [VIP_Design.md](./VIP_Design.md) — VIP speed multiplier
- [SaveManager_Design.md](./SaveManager_Design.md) — queue persistence

---

## 1. Crafting Identity

**Purpose:** Transform materials + gold → equipment/consumables over time.

**Primary location:** `Scripts/Crafting/`

**Entry point:** `Scripts/Crafting/CraftingManager.cs`

**Pipeline files:**
- `CraftRollService.cs` — random stat rolls
- `CraftValidator.cs` — validation
- `CraftRecipeValidationRunner.cs` — recipe checks
- `CraftCostResolver.cs` — cost calculation
- `CraftTransactionService.cs` — atomic execution
- `CraftContextBuilder.cs` — context aggregation
- `CraftPipeline.cs` — orchestration
- `CraftResultValidator.cs` — result verification
- `CraftRewardBuilder.cs` — reward construction
- `CraftRewardService.cs` — reward granting
- `CraftCompletionService.cs` — completion handling
- `CraftPersistenceService.cs` — save/load
- `CraftQueueService.cs` — queue management
- `CraftModifiers.cs` — crafting bonuses
- `CraftRecipeData.cs` — recipe definitions
- `CraftRecipeRepository.cs` — recipe lookup
- `CraftingConfig.cs` — global config
- `CraftData.cs` — crafting data container
- `CraftJob.cs` — job instance

**UI:**
- `Scripts/Crafting/JobEntryUI.cs` — queue slot
- `Scripts/Controller/CraftingController.cs` — scene controller
- `Scripts/Controller/CraftingUIController.cs` — panel controller
- `Scripts/Crafting/CraftingRecipeEntry.cs` — recipe slot

---

## 2. Data Sources

**Global config:**
- `Assets/Resources/Data/Crafting/dataConfigCrafting.json` — queue limits, speed multipliers

**Equipment recipes:**
- `Assets/Resources/Data/Crafting/Equipment/dataBaseEquipment.json` — base templates
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeHat.json` — Hat recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeGloves.json` — Gloves recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeCape.json` — Cape recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeArmor.json` — Armor recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeBelt.json` — Belt recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipePants.json` — Pants recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipePendant.json` — Pendant recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeRing.json` — Ring recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeEarring.json` — Earring recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeBracelet.json` — Bracelet recipes
- `Assets/Resources/Data/Crafting/Equipment/dataRecipeShoes.json` — Shoes recipes

**Potion recipes:**
- `Assets/Resources/Data/Crafting/Potion/dataRecipeHealthPotion.json` — HP potions
- `Assets/Resources/Data/Crafting/Potion/dataRecipeManaPotion.json` — Mana potions

---

## 3. Recipe Schema

**Equipment recipe:**

```json
{
  "recipeId": "recipe_hat_leather_1",
  "resultItemId": "hat_leather_1",
  "resultQuantity": 1,
  "craftTime": 60.0,
  "goldCost": 100,
  "materials": [
    {"itemId": "leather", "quantity": 5},
    {"itemId": "thread", "quantity": 2}
  ],
  "requiredLevel": 1,
  "requiredTier": 1,
  "jobTags": []
}
```

**Potion recipe:**

```json
{
  "recipeId": "recipe_potion_hp_small",
  "resultItemId": "potion_hp_small",
  "resultQuantity": 3,
  "craftTime": 10.0,
  "goldCost": 50,
  "materials": [
    {"itemId": "herb_healing", "quantity": 2},
    {"itemId": "water", "quantity": 1}
  ]
}
```

---

## 4. Crafting Pipeline

**Full flow:**

```
1. Player selects recipe
2. CraftValidator.Validate()
   a. Check materials in inventory
   b. Check gold balance
   c. Check queue capacity
   d. Check level/tier requirements
3. CraftCostResolver.CalculateCost()
   a. Base gold cost
   b. Material quantities
   c. Apply VIP modifiers
4. CraftTransactionService.Execute()
   a. Remove materials from inventory
   b. Remove gold from economy
   c. Add job to queue
   d. Save
5. CraftQueueService.AddJob()
   a. Create CraftJob instance
   b. Set start time
   c. Calculate completion time (duration × speed modifier)
   d. Store in queue
6. CraftPipeline.Update()
   a. Check job completion time
   b. If ready, call CraftCompletionService
7. CraftCompletionService.Complete()
   a. CraftResultValidator validates result
   b. CraftRewardBuilder builds result item
   c. Add item to inventory
   d. Fire OnCraftCompleted event
   e. Remove job from queue
   f. Save
```

---

## 5. Craft Context

**Context builder aggregates:**

```csharp
public class CraftContext
{
    public bool IsVIP;                    // VIP flag
    public float CraftSpeedMultiplier;    // Speed bonus (VIP, buffs)
    public float MaterialReduction;       // % material cost reduction
    public float GoldReduction;           // % gold cost reduction
    public Dictionary<string, float> AttributeModifiers;  // Attribute bonuses
}
```

**Built by `CraftContextBuilder`:**
- Reads VIP flags from `SaveManager.GetVipData()`
- Reads attribute bonuses from `AttributeModifierManager`
- Reads card/equipment crafting bonuses
- Reads active buff effects

---

## 6. VIP Integration

**VIP speed multiplier:**

```csharp
if (context.IsVIP)
    craftDuration *= 0.75f;  // 25% faster (configurable)
```

**VIP cost reduction (optional):**

```csharp
if (context.IsVIP)
    goldCost *= 0.9f;  // 10% cheaper
```

**Job tag gating (verified `CraftRecipeData.cs`):**

```csharp
public enum JobTag
{
    None = 0,
    // ... other tags
    VIP = 8  // VIP-only recipes
}
```

**Recipes with `JobTag.VIP` require VIP flag.**

---

## 7. Queue Management

**Queue capacity:**
- Default: 3 concurrent jobs
- Expandable via upgrades (if implemented)

**Job priority:**
- FIFO (first-in-first-out)
- No priority system (all jobs equal)

**Job cancellation:**
- Player can cancel queued job
- Refund materials + gold (configurable refund %)
- Default: 100% refund if not started, 50% if in progress

---

## 8. Craft Job Instance

**Schema:**

```csharp
public class CraftJob
{
    public string JobId;              // GUID
    public string RecipeId;           // Recipe reference
    public long StartTimeUtcTicks;    // When job started
    public long EndTimeUtcTicks;      // When job completes
    public float DurationSeconds;     // Base duration
    public float SpeedMultiplier;     // Applied speed bonus
    public bool IsCompleted;          // Completion flag
}
```

**Persistence:** Saved in `SaveData.craftingJobs` list.

---

## 9. Completion Detection

**Update loop (in `CraftPipeline` or manager):**

```csharp
void Update()
{
    long nowTicks = DateTime.UtcNow.Ticks;
    
    foreach (var job in _craftQueue)
    {
        if (job.IsCompleted)
            continue;
        
        if (nowTicks >= job.EndTimeUtcTicks)
        {
            CompleteCraftJob(job);
        }
    }
}
```

**Completion flow:**
1. Validate job not cancelled
2. Build result item
3. Add to inventory
4. Remove job from queue
5. Fire event
6. Save

---

## 10. Result Building

**Equipment crafting:**

```csharp
EquipmentInstance BuildEquipment(CraftJob job)
{
    // 1. Load recipe
    CraftRecipeData recipe = RecipeRepository.Get(job.RecipeId);
    
    // 2. Load base equipment data
    EquipmentData baseData = EquipmentDatabase.Get(recipe.ResultItemId);
    
    // 3. Create instance
    EquipmentInstance item = new()
    {
        InstanceId = Guid.NewGuid().ToString(),
        ItemId = recipe.ResultItemId,
        Level = 1,
        Enhancement = 0,
        Durability = baseData.MaxDurability
    };
    
    // 4. Roll stats
    CraftRollService.RollStats(item, baseData);
    
    // 5. Roll affixes (optional)
    CraftRollService.RollAffixes(item);
    
    // 6. Assign sockets
    item.Sockets = new SocketData[baseData.MaxSockets];
    
    return item;
}
```

**Potion crafting:**

```csharp
InventoryItem BuildPotion(CraftJob job)
{
    CraftRecipeData recipe = RecipeRepository.Get(job.RecipeId);
    
    return new InventoryItem
    {
        ItemId = recipe.ResultItemId,
        Quantity = recipe.ResultQuantity,
        State = ItemState.InInventory
    };
}
```

---

## 11. Stat Rolling

**Random stat ranges:**

```csharp
void RollStats(EquipmentInstance item, EquipmentData baseData)
{
    foreach (var stat in baseData.BaseStats)
    {
        float min = stat.Value * 0.8f;  // 80% of base
        float max = stat.Value * 1.2f;  // 120% of base
        
        float rolled = Random.Range(min, max);
        
        item.Stats[stat.Key] = rolled;
    }
}
```

**Quality determines roll range:**
- Common: 80-100%
- Rare: 90-110%
- Epic: 100-120%
- Legendary: 110-130%

---

## 12. Affix Rolling

**Affix system (optional on crafted equipment):**

```csharp
void RollAffixes(EquipmentInstance item)
{
    // 1. Roll affix count (0-2 based on rarity)
    int affixCount = RollAffixCount(item.Rarity);
    
    // 2. Select random affixes
    for (int i = 0; i < affixCount; i++)
    {
        AffixData affix = SelectRandomAffix();
        item.Affixes.Add(affix);
    }
}
```

**Affix data:** `Assets/Resources/Data/Equipment/dataAffixes.json`

---

## 13. Persistence

**Save format:**

```csharp
public class CraftingSaveData
{
    public List<CraftJob> ActiveJobs = new();
    public int MaxQueueSize = 3;
}
```

**Save triggers:**
- Job added to queue
- Job completed
- Job cancelled
- Manual save

**Load on game start:**
- Restore active jobs
- Resume timers
- Check for completed jobs while offline

---

## 14. UI Integration

**Recipe list:**
- Show all unlocked recipes
- Filter by slot/category
- Show material requirements
- Show gold cost
- Show craft time
- Highlight craftable recipes (green)
- Disable uncraftable (red, show missing materials)

**Queue display:**
- Show active jobs
- Show progress bar (time remaining)
- Show completion time
- Allow cancellation
- Auto-refresh on completion

---

## 15. Crafting Modifiers

**Sources:**
- VIP flags (speed, cost reduction)
- Card effects (crafting speed +%, material reduction)
- Equipment bonuses (crafting bonuses)
- Buffs (temporary bonuses)

**Application:**
- Speed: multiplies `craftDuration`
- Material reduction: reduces `materialQuantity`
- Gold reduction: reduces `goldCost`

---

## 16. Performance

**Crafting is lightweight:**
- Jobs checked once per second (not per-frame)
- Queue size capped at 3-10
- Result building on completion (not per-frame)

**No optimization needed** unless thousands of jobs.

---

## 17. Testing Checklist

```
[ ] Recipe loads from JSON correctly
[ ] Validation checks materials
[ ] Validation checks gold
[ ] Validation checks queue capacity
[ ] Transaction removes materials atomically
[ ] Transaction removes gold atomically
[ ] Job added to queue with correct times
[ ] Job completes at correct time
[ ] Completion grants result item
[ ] VIP speed multiplier applies
[ ] Offline completion works (resume timers)
[ ] Save/load preserves queue
[ ] Cancel refunds materials/gold
[ ] UI updates on completion
```

---

## 18. Common Issues

### Issue: Job never completes
**Cause:** Completion check not running, or time comparison wrong.
**Fix:** Verify Update loop, check UTC ticks comparison.

### Issue: Materials not consumed
**Cause:** Transaction not called, or inventory mutation failed.
**Fix:** Verify transaction flow, check inventory service.

### Issue: Result item not granted
**Cause:** Completion service not called, or add item failed.
**Fix:** Verify completion flow, check inventory capacity.

### Issue: Queue lost on restart
**Cause:** Save not called, or load failed.
**Fix:** Verify save triggers, check persistence service.

---

## 19. Future Extensions

### Batch Crafting
- Craft multiple copies in one job

### Crafting Stations
- Different stations for different item types

### Quality Control
- Higher quality with better materials

### Auto-Craft
- Repeat recipe automatically

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
