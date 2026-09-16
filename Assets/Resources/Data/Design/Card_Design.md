# Card System Design — IdleDefenseSurvival

**Purpose:** Card collection, rolling, leveling, equipment, effects — major progression system.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Modifier_Design.md](./Modifier_Design.md) — card effects → stat pipeline
- [Player_Design.md](./Player_Design.md) — stat aggregation
- [Economy_Design.md](./Economy_Design.md) — gem costs

---

## 1. Card Identity

**Owner files:**
- `Scripts/Card/CardManager.cs` (UI façade)
- `Scripts/Manager/CardManager.cs` (legacy, being phased out)

**Services (in `Scripts/Card/`):**
- `CardDatabase` — loads `Card/dataCard.json`
- `CardInventory` — owned cards, duplicates, pity counters
- `CardUpgradeService` — duplicate → level conversion
- `CardRollService` — gem-based rolling, pity, bundle pricing
- `CardEquipmentService` — equip/unequip to slots (max 19)
- `CardModifierService` — card effects → modifier pipeline
- `VirtualCardInventorySnapshot` — UI snapshot for collection view

**Data source:** `Assets/Resources/Data/Card/dataCard.json`

---

## 2. Card Rarities

**6-tier system (verified from dataCard.json):**

| Rarity | Weight Multiplier | Visual Color |
|--------|-------------------|--------------|
| Common | 1000.0 | White/Gray |
| Rare | 300.0 | Blue |
| Epic | 80.0 | Purple |
| Legendary | 15.0 | Gold |
| Mythic | 1.0 | Red |
| **Divine** | **0.006** | Rainbow/Special |

**Divine is extreme outlier** — multiplier 0.006 vs Mythic 1.0 = 166× rarer.

---

## 3. Pity System

**Pity thresholds (verified `Constantku.cs`):**

| Rarity | Pity Count | Explanation |
|--------|------------|-------------|
| Epic | 51 | Guaranteed Epic after 51 non-Epic rolls |
| Legendary | 153 | Guaranteed Legendary after 153 non-Legendary |
| Mythic | 505 | Guaranteed Mythic after 505 non-Mythic |

**Divine has NO pity** — base weight already extremely low.

**Pity mechanics:**
- Separate counter per rarity
- Counter increments on every roll that DOESN'T get that rarity or higher
- Counter resets when you pull that rarity or higher
- Pity trigger guarantees that rarity (not higher)

**Example:**
- Roll 50× → no Epic → pityEpic = 50
- Roll 51st → Epic guaranteed (pity triggered)
- pityEpic reset to 0

---

## 4. Card Roll Costs

**Verified constants (`GameConstants.cs`):**

```csharp
ROLL1X_GEM_COST = 20
ROLL10X_GEM_COST = 190
ROLL100X_GEM_COST = 1800
```

**Bundle discount:**
- 1× = 20 gems/card
- 10× = 190 gems = **19 gems/card** (5% discount)
- 100× = 1800 gems = **18 gems/card** (10% discount)

**Bundle calculation (verified `CardRollService.CalculateRollGemCost`):**

```csharp
int CalculateRollGemCost(int amount)
{
    int hundreds = amount / 100;
    int tens = (amount % 100) / 10;
    int singles = amount % 10;
    
    return hundreds * 1800 + tens * 190 + singles * 20;
}
```

**Example:**
- 1 roll: 20 gems
- 10 rolls: 190 gems (NOT 200)
- 100 rolls: 1800 gems (NOT 2000)
- 237 rolls: (2×1800) + (3×190) + (7×20) = 3600 + 570 + 140 = **4310 gems**

**This is NOT simple multiplication** — preserves bundle tiers.

---

## 5. Card Leveling

**Duplicate → level conversion (verified `CardUpgradeService.cs`):**

| Level | Duplicates Required | Cumulative Total |
|-------|---------------------|------------------|
| 1→2 | 2 | 2 |
| 2→3 | 4 | 6 |
| 3→4 | 7 | 13 |
| 4→5 | 11 | 24 |
| 5→6 | 19 | 43 |
| 6→7 | 31 | 74 |
| 7→8 | 47 | 121 |
| 8→9 | 69 | 190 |
| 9→10 | 99 | **289** |

**289 total duplicates** required to max-level a card from level 1 → 10.

**Stat scaling per level:**
- Flat bonuses: `BaseFlat × Level`
- Percent bonuses: `BasePercent × Level`

**Example:**
- Card base: +10 AttackDamage
- Level 1: +10
- Level 5: +50
- Level 10: +100

---

## 6. Card Equipment Slots

**Slot progression (verified `GameConstants.cs`):**

```csharp
CARD_START_SLOT = 1          // Start with 1 slot
CARD_MAX_SLOT = 19           // Maximum 19 slots
CARD_SLOT_EXPANSION_COSTS[]  // Array of 18 costs (slots 2-19)
```

**Unlock costs:** Each slot costs gold (amount from `CARD_SLOT_EXPANSION_COSTS` array).

**Example progression:**
- Start: 1 slot
- Unlock slot 2: X gold
- Unlock slot 3: Y gold
- ...
- Unlock slot 19: Z gold

**Equipped cards:** Only equipped cards contribute stats.

**Unequipped cards:** Stored in collection, no effect.

---

## 7. Card Effects

**Effect types:**

### 7.1 Stat Modifiers

**Flat bonuses:**
```json
{
  "statType": "AttackDamage",
  "flatValue": 10.0
}
```

**Percent bonuses:**
```json
{
  "statType": "AttackSpeed",
  "percentValue": 5.0
}
```

**Multiple stats per card:**
```json
{
  "effects": [
    {"statType": "AttackDamage", "flatValue": 10.0},
    {"statType": "CriticalChance", "percentValue": 3.0}
  ]
}
```

### 7.2 Special Effects

**Verified special effects:**
- **FrostAura** — slows enemies in range
- **Shield** — absorbs damage
- **TimeFast** — increases game speed
- **Gold** — bonus gold per kill
- **Meat** — bonus meat per kill

**Implementation:** Special effects have dedicated handlers, not just stat modifiers.

---

## 8. Card Data Schema

**File:** `Assets/Resources/Data/Card/dataCard.json`

**Schema example:**
```json
{
  "id": "card_warrior_strength",
  "name": "Warrior's Strength",
  "rarity": "Epic",
  "description": "Increases attack damage and critical chance",
  "iconPath": "Cards/warrior_strength",
  "effects": [
    {
      "statType": "AttackDamage",
      "flatValue": 15.0,
      "percentValue": 0.0
    },
    {
      "statType": "CriticalChance",
      "flatValue": 0.0,
      "percentValue": 3.0
    }
  ]
}
```

---

## 9. Card Rolling Algorithm

**Weighted random (simplified):**

```csharp
CardRarity RollCard()
{
    // 1. Check pity
    if (_pityEpic >= 51) return CardRarity.Epic;
    if (_pityLegendary >= 153) return CardRarity.Legendary;
    if (_pityMythic >= 505) return CardRarity.Mythic;
    
    // 2. Calculate total weight
    float totalWeight = 0f;
    foreach (var rarity in rarities)
        totalWeight += GetWeight(rarity);
    
    // 3. Roll
    float roll = Random.Range(0f, totalWeight);
    
    // 4. Find rarity
    float cumulative = 0f;
    foreach (var rarity in rarities)
    {
        cumulative += GetWeight(rarity);
        if (roll < cumulative)
        {
            UpdatePity(rarity);
            return rarity;
        }
    }
}

float GetWeight(CardRarity rarity)
{
    // From dataCard.json weight multiplier
    switch (rarity)
    {
        case Common: return 1000.0f;
        case Rare: return 300.0f;
        case Epic: return 80.0f;
        case Legendary: return 15.0f;
        case Mythic: return 1.0f;
        case Divine: return 0.006f;
    }
}

void UpdatePity(CardRarity rolled)
{
    if (rolled < Epic) _pityEpic++;
    else _pityEpic = 0;
    
    if (rolled < Legendary) _pityLegendary++;
    else _pityLegendary = 0;
    
    if (rolled < Mythic) _pityMythic++;
    else _pityMythic = 0;
}
```

---

## 10. Card Inventory

**Storage:**
- `OwnedCards`: Dictionary<CardId, CardInstance>
- `DuplicateCount`: int per card
- `Level`: int (1-10)
- `IsEquipped`: bool

**Ownership:**
- Each card has persistent `CardId`
- Multiple copies tracked as `DuplicateCount`
- Leveling consumes duplicates

**Persistence:** Saved in `SaveData.cardInventory`.

---

## 11. Card Equipment

**Equip flow:**

```csharp
public bool EquipCard(string cardId, int slotIndex)
{
    // 1. Validate slot unlocked
    if (slotIndex >= GetUnlockedSlotCount())
        return false;
    
    // 2. Validate owns card
    if (!_inventory.HasCard(cardId))
        return false;
    
    // 3. Check if already equipped elsewhere
    int currentSlot = GetEquippedSlot(cardId);
    if (currentSlot >= 0)
        UnequipCard(currentSlot);
    
    // 4. Unequip card in target slot
    if (_equippedCards[slotIndex] != null)
        UnequipCard(slotIndex);
    
    // 5. Equip new card
    _equippedCards[slotIndex] = cardId;
    
    // 6. Invalidate stat cache
    PlayerStatsManager.InvalidateCache();
    
    // 7. Save
    SaveCardEquipment();
    
    return true;
}
```

**Unequip:**
```csharp
public void UnequipCard(int slotIndex)
{
    _equippedCards[slotIndex] = null;
    PlayerStatsManager.InvalidateCache();
    SaveCardEquipment();
}
```

---

## 12. Card Modifier Pipeline

**Service:** `CardModifierService.cs`

**Flow:**

```
Equipped Cards
    ↓
CardModifierService.GetCardModifiers()
    ↓
For each equipped card:
    - Load card data
    - Get card level
    - Calculate scaled effects
    - Convert to ModifierEntry[]
    ↓
Return all modifiers
    ↓
PlayerStatsManager aggregates
```

**Scaling:**
```csharp
ModifierEntry ConvertToModifier(CardEffect effect, int level)
{
    return new ModifierEntry
    {
        StatType = effect.statType,
        FlatValue = effect.flatValue * level,
        PercentValue = effect.percentValue * level,
        Source = ModifierSource.Card
    };
}
```

---

## 13. CardRoll Item

**Free roll item:** `CardRoll` consumable in inventory.

**Usage:**
1. Player uses `CardRoll` item
2. Consume 1 from inventory
3. Perform 1 card roll (no gem cost)
4. Apply same pity/rarity logic
5. Grant card to inventory

**Refund on failure:**
- If roll fails (e.g., inventory full), refund item
- Do NOT substitute gem refund for item refund

---

## 14. Card Collection View

**UI:** `CardCollectionController.cs` + virtual inventory snapshot

**Features:**
- View all owned cards
- Filter by rarity
- Sort by level/name/rarity
- Equip/unequip directly
- Level up cards

**Snapshot:** `VirtualCardInventorySnapshot` creates UI-friendly data structure from raw inventory.

---

## 15. Performance

**Card system is NOT performance-critical:**
- Rolling happens on user action (not per-frame)
- Equipment changes are rare
- Stat aggregation is cached

**Do NOT:**
- Recalculate card modifiers every frame (cached)
- Duplicate card effect logic in UI

---

## 16. Testing Checklist

```
[ ] Card roll costs match constants (20/190/1800)
[ ] Bundle discount applies correctly
[ ] Pity triggers at correct thresholds (51/153/505)
[ ] Pity resets after trigger
[ ] Card leveling consumes duplicates correctly
[ ] Level scaling applies (flat/percent × level)
[ ] Slot unlock costs gold
[ ] Max 19 slots enforced
[ ] Equipped cards contribute stats
[ ] Unequipped cards don't contribute
[ ] Card effects apply through modifier pipeline
[ ] Cache invalidates on equip/unequip
[ ] CardRoll item consumes from inventory
[ ] Save/load preserves card inventory
[ ] Duplicate count tracks correctly
```

---

## 17. Common Issues

### Issue: Pity never triggers
**Cause:** Pity counter not incrementing, or threshold check wrong.
**Fix:** Verify counter increments on non-Epic rolls, check threshold logic.

### Issue: Card costs too high
**Cause:** Bundle discount not applied, or cost calculation wrong.
**Fix:** Use `CalculateRollGemCost` from `CardRollService`, not simple multiplication.

### Issue: Equipped card has no effect
**Cause:** Cache not invalidated, or modifier pipeline broken.
**Fix:** Call `PlayerStatsManager.InvalidateCache()` after equip.

### Issue: Level scaling wrong
**Cause:** Scaling formula incorrect, or level not applied.
**Fix:** Verify `flatValue × level` formula, check card level value.

---

## 18. Future Extensions

### Card Sets
- Equip multiple cards from same set for bonus

### Card Fusion
- Combine cards for stronger version

### Card Trading
- Trade with other players

### Card Skins
- Visual variants of cards

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
