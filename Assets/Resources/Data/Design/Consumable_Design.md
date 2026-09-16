# Consumable System Design — IdleDefenseSurvival

**Purpose:** Consumable items (potions, scrolls, tickets), usage, effects, cooldowns.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Item_Design.md](./Item_Design.md) — item identity and operations
- [Inventory_Design.md](./Inventory_Design.md) — consumable storage
- [Economy_Design.md](./Economy_Design.md) — purchase costs
- [Crafting_Design.md](./Crafting_Design.md) — potion crafting

---

## 1. Consumable Identity

**Definition:** Items that are used once and consumed.

**Data sources:**
- `Assets/Resources/Data/Items/dataConsumables.json`
- `Assets/Resources/Data/Items/Potion/dataHealthPotion.json`
- `Assets/Resources/Data/Items/Potion/dataManaPotion.json`

**UI handler:** `Scripts/UI/Game/ItemConsumableUI.cs`

---

## 2. Consumable Types

### 2.1 Health Potions

**Effect:** Restore player HP

**Variants:**
- Small: +50 HP
- Medium: +150 HP
- Large: +500 HP
- Ultimate: Full HP

**Usage:** Instant heal, no duration

### 2.2 Mana Potions

**Effect:** Restore player Mana

**Variants:**
- Small: +30 Mana
- Medium: +100 Mana
- Large: +300 Mana
- Ultimate: Full Mana

**Usage:** Instant restore

### 2.3 Buff Scrolls

**Effect:** Temporary stat boost

**Examples:**
- Strength Scroll: +20% Attack for 60s
- Speed Scroll: +30% AttackSpeed for 60s
- Defense Scroll: +50 Defense for 60s

**Usage:** Duration-based buff

### 2.4 Special Tickets

**Effect:** Unlock specific actions

**Examples:**
- `CardRoll`: Free card roll (no gem cost)
- `UltimateStone_Fire`: Unlock Fire ultimate
- `RerollTicket`: Reroll crafting affixes

**Usage:** Consumed on action, no stats

---

## 3. Consumable Data Schema

**Schema:**

```json
{
  "itemId": "potion_hp_small",
  "name": "Small Health Potion",
  "description": "Restores 50 HP",
  "category": "Consumable",
  "iconPath": "Items/Potion/hp_small",
  "maxStackSize": 99,
  "sellPrice": 10,
  "effect": {
    "type": "HealHP",
    "value": 50.0,
    "duration": 0.0,
    "cooldown": 1.0
  },
  "craftable": true
}
```

**Effect types:**
- `HealHP` — restore HP
- `HealMana` — restore Mana
- `BuffStat` — temporary stat boost
- `GrantAction` — enable action (tickets)

---

## 4. Usage Flow

**From UI:**

```csharp
public void OnPotionClick(string itemId)
{
    // 1. Validate has item
    if (!InventoryService.HasItem(itemId, 1))
    {
        ShowError("No potion available");
        return;
    }
    
    // 2. Check cooldown
    if (IsOnCooldown(itemId))
    {
        ShowError("Potion on cooldown");
        return;
    }
    
    // 3. Validate effect applicable
    if (!CanApplyEffect(itemId))
    {
        ShowError("HP already full");
        return;
    }
    
    // 4. Apply effect
    ApplyConsumableEffect(itemId);
    
    // 5. Remove from inventory
    InventoryService.RemoveItem(itemId, 1);
    
    // 6. Start cooldown
    StartCooldown(itemId);
    
    // 7. Update UI
    RefreshInventoryUI();
}
```

---

## 5. Effect Application

### 5.1 Health Potion

```csharp
void ApplyHealthPotion(float value)
{
    Player player = Player.Instance;
    
    // Calculate heal amount
    float healAmount = value;
    float currentHP = player.CurrentHP;
    float maxHP = player.MaxHP;
    
    // Cap at max HP
    float newHP = Mathf.Min(currentHP + healAmount, maxHP);
    float actualHeal = newHP - currentHP;
    
    // Apply heal
    player.Heal(actualHeal);
    
    // Show effect
    EffectPopup.Spawn(player.transform.position, $"+{actualHeal:F0} HP", Color.green);
}
```

### 5.2 Mana Potion

```csharp
void ApplyManaPotion(float value)
{
    Player player = Player.Instance;
    
    float currentMana = player.CurrentMana;
    float maxMana = player.MaxMana;
    
    float newMana = Mathf.Min(currentMana + value, maxMana);
    float actualRestore = newMana - currentMana;
    
    player.RestoreMana(actualRestore);
    
    EffectPopup.Spawn(player.transform.position, $"+{actualRestore:F0} Mana", Color.blue);
}
```

### 5.3 Buff Scroll

```csharp
void ApplyBuffScroll(StatType stat, float value, float duration)
{
    // 1. Create buff modifier
    ModifierEntry buff = new ModifierEntry
    {
        StatType = stat,
        PercentValue = value,
        Source = ModifierSource.Buff,
        Duration = duration
    };
    
    // 2. Add to buff manager
    BuffManager.Instance.AddBuff(buff);
    
    // 3. Invalidate stat cache
    PlayerStatsManager.InvalidateCache();
    
    // 4. Show buff icon
    BuffIconUI.Show(stat, duration);
}
```

---

## 6. Cooldown System

**Purpose:** Prevent potion spam.

**Implementation:**

```csharp
private Dictionary<string, float> _cooldowns = new();

void StartCooldown(string itemId)
{
    ConsumableData data = ConsumableDatabase.Get(itemId);
    _cooldowns[itemId] = Time.time + data.Cooldown;
}

bool IsOnCooldown(string itemId)
{
    if (!_cooldowns.TryGetValue(itemId, out float endTime))
        return false;
    
    return Time.time < endTime;
}

float GetRemainingCooldown(string itemId)
{
    if (!_cooldowns.TryGetValue(itemId, out float endTime))
        return 0f;
    
    return Mathf.Max(0f, endTime - Time.time);
}
```

**UI display:** Show cooldown timer on potion icon.

---

## 7. Validation Rules

**Before use, check:**

```csharp
bool CanApplyEffect(string itemId)
{
    ConsumableData data = ConsumableDatabase.Get(itemId);
    
    switch (data.Effect.Type)
    {
        case EffectType.HealHP:
            // Don't use if HP already full
            return Player.Instance.CurrentHP < Player.Instance.MaxHP;
        
        case EffectType.HealMana:
            // Don't use if Mana already full
            return Player.Instance.CurrentMana < Player.Instance.MaxMana;
        
        case EffectType.BuffStat:
            // Always allow (buff stacking handled by BuffManager)
            return true;
        
        case EffectType.GrantAction:
            // Check if action is available
            return IsActionAvailable(data.Effect.ActionType);
        
        default:
            return true;
    }
}
```

---

## 8. Crafting Integration

**Potions are craftable** (see `Crafting_Design.md`).

**Recipe example:**
```json
{
  "resultItemId": "potion_hp_small",
  "resultQuantity": 3,
  "materials": [
    {"itemId": "herb_healing", "quantity": 2},
    {"itemId": "water", "quantity": 1}
  ],
  "craftTime": 10.0,
  "goldCost": 50
}
```

**Crafting unlocks better potion tiers.**

---

## 9. Purchase vs Craft

**Purchase:**
- Buy from shop (gold cost)
- Instant, no wait
- More expensive

**Craft:**
- Requires materials
- Takes time
- Cheaper per unit
- Bulk production

**Balance:** Crafting should be worth the investment.

---

## 10. Stack Management

**Potions are stackable:**
- Max stack: 99 (typical)
- Consume from stack
- Stack count decrements

**UI:**
```
[Icon] Health Potion (x47)
```

**When stack reaches 0:** Remove from inventory display.

---

## 11. Hotkey Binding

**Quick access:**
- Bind potions to hotkeys (1, 2, 3, ...)
- Press key to use instantly
- No need to open inventory

**Implementation:**
```csharp
void Update()
{
    if (Input.GetKeyDown(KeyCode.Alpha1))
        UsePotion(_hotbarSlot1ItemId);
    
    if (Input.GetKeyDown(KeyCode.Alpha2))
        UsePotion(_hotbarSlot2ItemId);
    
    // ...
}
```

---

## 12. Special Consumables

### 12.1 CardRoll Ticket

**Usage:**
1. Player opens card roll UI
2. Uses `CardRoll` item instead of gems
3. Perform 1 roll
4. Consume ticket
5. Grant card

**No gem cost**, but same pity/rarity logic.

### 12.2 UltimateStone

**Usage:**
1. Player uses `UltimateStone_Fire` (example)
2. Unlock Fire ultimate permanently
3. Consume stone
4. Update ultimate pool

**One-time unlock**, cannot be reused.

### 12.3 Reroll Ticket

**Usage:**
1. After crafting equipment
2. Use ticket to reroll affixes
3. Keep base item, new affixes
4. Consume ticket

**Gambling mechanic** for better rolls.

---

## 13. Buff Duration UI

**When buff active:**
- Show buff icon above player
- Display remaining time
- Color-coded by buff type
- Remove icon when expires

**Multiple buffs:** Stack icons vertically or horizontally.

---

## 14. Performance

**Consumable system is lightweight:**
- Usage triggered by player action (not per-frame)
- Cooldown checks are simple dictionary lookup
- Buff updates handled by BuffManager

**No optimization needed** unless thousands of buffs active.

---

## 15. Testing Checklist

```
[ ] Health potion restores HP correctly
[ ] Mana potion restores Mana correctly
[ ] Buff scroll applies temporary stat boost
[ ] Cooldown prevents spam
[ ] Cooldown timer displays correctly
[ ] Cannot use potion when HP/Mana full
[ ] Quantity decrements after use
[ ] Stack removed when quantity = 0
[ ] CardRoll ticket performs free roll
[ ] UltimateStone unlocks ultimate
[ ] Hotkey binding works
[ ] Buff icon shows/hides correctly
[ ] Multiple buffs stack correctly
[ ] Save/load preserves consumable inventory
```

---

## 16. Common Issues

### Issue: Potion used but no effect
**Cause:** HP already full, or validation failed.
**Fix:** Check validation logic, show error message.

### Issue: Cooldown not working
**Cause:** Cooldown timer not stored, or check logic wrong.
**Fix:** Verify cooldown dictionary update, check `Time.time`.

### Issue: Buff doesn't apply
**Cause:** Stat cache not invalidated, or buff manager broken.
**Fix:** Call `PlayerStatsManager.InvalidateCache()` after adding buff.

### Issue: Stack doesn't decrement
**Cause:** Remove item logic not called, or quantity not updated.
**Fix:** Verify `InventoryService.RemoveItem()` called after effect.

---

## 17. Future Extensions

### Auto-Potion
- Automatically use potion when HP < threshold

### Potion Combos
- Use 2+ potions together for bonus effect

### Potion Quality
- Crafted potions have quality tiers (better effect)

### Buff Stacking Rules
- Define how same-type buffs interact

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
