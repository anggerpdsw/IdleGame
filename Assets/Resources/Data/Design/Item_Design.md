# Item System Design — IdleDefenseSurvival

**Purpose:** Item definitions, categories, state management, item operations.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Inventory_Design.md](./Inventory_Design.md) — item storage and capacity
- [Consumable_Design.md](./Consumable_Design.md) — consumable usage
- [Material_Design.md](./Material_Design.md) — crafting materials
- [Herb_Design.md](./Herb_Design.md) — herb materials
- [Equipment_Design.md](./Equipment_Design.md) — equipment items
- [Crafting_Design.md](./Crafting_Design.md) — item creation
- [EnemyDrop_Design.md](./EnemyDrop_Design.md) — item drops

---

## 1. Item Identity

**Core principle:** Items use stable `ItemId` for definitions, `InstanceId` for owned instances.

**Key scripts:**
- `Scripts/Item/ItemCategory.cs` — item type enum
- `Scripts/Item/ItemState.cs` — item state enum
- `Scripts/Items/` — item-related services
- `Scripts/Inventory/InventoryItem.cs` — item instance data

---

## 2. Item Categories

**Enum:** `ItemCategory`

| Category | Purpose | Examples |
|----------|---------|----------|
| **Equipment** | Equippable gear | Hat, Gloves, Armor, Ring |
| **Material** | Crafting materials | Coal, Iron Ore, Herbs |
| **Consumable** | Usable items | Potions, scrolls |
| **Gem** | Socketable gems | Ruby, Sapphire |
| **Currency** | Money items | Gold, Gems (if stored as items) |
| **Special** | Unique items | CardRoll ticket, UltimateStone |
| **Quest** | Mission items | Quest tokens |

**Category determines:**
- Stackability
- Usability
- Drop behavior
- Inventory filtering
- Icon display

---

## 3. Item State

**Enum:** `ItemState`

| State | Meaning |
|-------|---------|
| **InInventory** | Owned, not equipped |
| **Equipped** | Equipped on player |
| **InCraftQueue** | Being crafted |
| **Locked** | Cannot be modified/sold |

**State transitions:**
```
InInventory → Equipped (equip action)
Equipped → InInventory (unequip action)
InInventory → InCraftQueue (craft start)
InCraftQueue → InInventory (craft complete)
```

---

## 4. ItemId vs InstanceId

### 4.1 ItemId (Definition)

**Purpose:** Identify item type/template.

**Examples:**
- `"potion_hp_small"`
- `"iron_ore"`
- `"hat_leather_1"`

**Used for:**
- Looking up definition data
- Icon resolution
- Recipe requirements
- Drop tables
- Spawning new items

**Never use display names as ItemId.**

### 4.2 InstanceId (Owned Item)

**Purpose:** Track individual owned items.

**Format:** GUID string

**Examples:**
- `"a3f5b8c2-4d6e-7f8a-9b0c-1d2e3f4g5h6i"`

**Used for:**
- Equipment tracking (each piece is unique)
- Sockets (gem instances in equipment instances)
- Durability (per-instance)
- Enhancement level (per-instance)

**Stack items may share InstanceId** or use quantity field.

---

## 5. Item Data Sources

**Per category:**

| Category | Data File |
|----------|-----------|
| Equipment | `Crafting/Equipment/dataBaseEquipment.json` + per-slot JSONs |
| Consumables | `Items/dataConsumables.json` |
| Health Potions | `Items/Potion/dataHealthPotion.json` |
| Mana Potions | `Items/Potion/dataManaPotion.json` |
| Herbs | `Items/Material/dataHerbs.json` |
| Minerals | `Items/Material/dataMinerals.json` |
| Other Materials | `Items/Material/dataOtherMaterials.json` |
| Gems | `Gems/dataGems.json` |
| Other Items | `Items/dataOtherItems.json` |

---

## 6. Item Schema

**Common fields (all items):**

```csharp
public class ItemData
{
    public string ItemId;           // Stable identifier
    public string Name;             // Display name
    public string Description;      // Tooltip text
    public ItemCategory Category;   // Type
    public string IconPath;         // Icon resource path
    public int MaxStackSize;        // 1 = non-stackable
    public int SellPrice;           // Gold value
    public bool IsTradeable;        // Can trade/sell
}
```

**Equipment-specific:**
```csharp
public class EquipmentData : ItemData
{
    public EquipmentType Slot;
    public Rarity Rarity;
    public int BaseLevel;
    public Dictionary<SecondaryStatType, float> BaseStats;
    public int MaxSockets;
    public int MaxEnhancement;
}
```

**Consumable-specific:**
```csharp
public class ConsumableData : ItemData
{
    public ConsumableEffect Effect;
    public float EffectValue;
    public float Duration;
    public int Cooldown;
}
```

---

## 7. Item Operations

### 7.1 Add Item

```csharp
bool AddItem(string itemId, int quantity = 1)
{
    // 1. Validate item exists
    if (!ItemDatabase.Exists(itemId))
        return false;
    
    // 2. Check inventory capacity
    if (!HasSpace(itemId, quantity))
        return false;
    
    // 3. Stack or create new
    if (IsStackable(itemId))
        AddToExistingStack(itemId, quantity);
    else
        CreateNewInstances(itemId, quantity);
    
    // 4. Fire event
    OnItemAdded?.Invoke(itemId, quantity);
    
    // 5. Save
    SaveInventory();
    
    return true;
}
```

### 7.2 Remove Item

```csharp
bool RemoveItem(string itemId, int quantity = 1)
{
    // 1. Check has item
    if (!HasItem(itemId, quantity))
        return false;
    
    // 2. Remove from stack or destroy instances
    if (IsStackable(itemId))
        RemoveFromStack(itemId, quantity);
    else
        DestroyInstances(itemId, quantity);
    
    // 3. Fire event
    OnItemRemoved?.Invoke(itemId, quantity);
    
    // 4. Save
    SaveInventory();
    
    return true;
}
```

### 7.3 Use Item

```csharp
bool UseItem(string instanceId)
{
    // 1. Get item data
    InventoryItem item = GetItem(instanceId);
    if (item == null || item.Category != ItemCategory.Consumable)
        return false;
    
    // 2. Apply effect
    ApplyConsumableEffect(item);
    
    // 3. Remove consumed item
    RemoveItem(item.ItemId, 1);
    
    // 4. Fire event
    OnItemUsed?.Invoke(item.ItemId);
    
    return true;
}
```

---

## 8. Stackability

**Stack rules:**

| Category | Stackable? | Max Stack |
|----------|------------|-----------|
| Equipment | No | 1 (each unique) |
| Material | Yes | 999 |
| Consumable | Yes | 99 |
| Gem | No | 1 (each has level/XP) |
| Currency | Yes | 999999 |
| Special | Varies | Per item |

**Stack detection:**
```csharp
bool IsStackable(string itemId)
{
    ItemData data = ItemDatabase.Get(itemId);
    return data.MaxStackSize > 1;
}
```

---

## 9. Item Icons

**Resolution:**

```csharp
Sprite GetItemIcon(string itemId)
{
    ItemData data = ItemDatabase.Get(itemId);
    return ResourceCache.Load<Sprite>(data.IconPath);
}
```

**Path convention:**
- `Items/Potion/hp_small`
- `Items/Material/iron_ore`
- `Equipment/Hat/leather_1`

**Do NOT duplicate path segments** (e.g., `Items/Items/...`).

---

## 10. Item Quality/Rarity

**Equipment rarity:** Common, Rare, Epic, Legendary, Mythic, Divine

**Visual indicators:**
- Border color
- Name color
- Background glow
- Star rating

**Non-equipment items:** Usually no rarity (or simple Common/Rare).

---

## 11. Item Comparison

**Equipment comparison:**

```csharp
int CompareEquipment(EquipmentInstance a, EquipmentInstance b)
{
    // 1. Compare total stats
    float scoreA = CalculateTotalScore(a);
    float scoreB = CalculateTotalScore(b);
    
    if (scoreA > scoreB) return 1;  // A better
    if (scoreA < scoreB) return -1; // B better
    return 0;  // Equal
}
```

**See:** `Equipment_Design.md` for full comparison logic.

---

## 12. Item Tooltips

**Tooltip content:**

```
[Icon] Item Name (Rarity)

Description text.

Stats (if equipment):
  +10 Attack Damage
  +5% Critical Chance

Sockets (if equipment):
  [Ruby] +15 Strength
  [Empty]

Set Bonus (if part of set):
  (2/4) Set Name
  
Level: 5
Durability: 80/100

Sell Price: 1000 Gold
```

**Tooltip positioning:** Account for screen boundaries (see `UI_Design.md`).

---

## 13. Item Locking

**Purpose:** Prevent accidental sale/destruction of valuable items.

**Lock operations:**
```csharp
void LockItem(string instanceId)
{
    InventoryItem item = GetItem(instanceId);
    item.State = ItemState.Locked;
    OnItemLocked?.Invoke(instanceId);
}

void UnlockItem(string instanceId)
{
    InventoryItem item = GetItem(instanceId);
    item.State = ItemState.InInventory;
    OnItemUnlocked?.Invoke(instanceId);
}
```

**Locked items:**
- Cannot be sold
- Cannot be destroyed
- Cannot be used in crafting (optional)
- Show lock icon in UI

---

## 14. Item Validation

**Before operations, validate:**

```csharp
bool ValidateItem(string itemId, int quantity)
{
    // 1. Item exists in database
    if (!ItemDatabase.Exists(itemId))
    {
        Debug.LogError($"Unknown item: {itemId}");
        return false;
    }
    
    // 2. Quantity positive
    if (quantity <= 0)
    {
        Debug.LogError("Quantity must be positive");
        return false;
    }
    
    // 3. Stack limit
    ItemData data = ItemDatabase.Get(itemId);
    if (quantity > data.MaxStackSize)
    {
        Debug.LogError($"Exceeds max stack: {quantity} > {data.MaxStackSize}");
        return false;
    }
    
    return true;
}
```

---

## 15. Item Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnItemAdded` | Item added to inventory | UI refresh, notifications |
| `OnItemRemoved` | Item removed | UI refresh, statistics |
| `OnItemUsed` | Consumable used | Effect application, UI |
| `OnItemEquipped` | Equipment equipped | Stats update, UI |
| `OnItemUnequipped` | Equipment unequipped | Stats update, UI |
| `OnItemLocked` | Item locked | UI icon update |
| `OnItemUpgraded` | Item enhanced | Stats update, UI |

---

## 16. Common Mistakes

### ❌ Wrong: Using display name as key

```csharp
// WRONG
AddItem("Health Potion", 1);  // Display name, not ItemId
```

### ✅ Correct: Use ItemId

```csharp
// CORRECT
AddItem("potion_hp_small", 1);  // Stable ItemId
```

### ❌ Wrong: Assuming all items stackable

```csharp
// WRONG
AddItem(itemId, 50);  // May exceed max stack or be non-stackable
```

### ✅ Correct: Check stack limit

```csharp
// CORRECT
int maxStack = ItemDatabase.Get(itemId).MaxStackSize;
int quantity = Mathf.Min(amount, maxStack);
AddItem(itemId, quantity);
```

---

## 17. Testing Checklist

```
[ ] ItemId stable across renames
[ ] InstanceId unique per equipment piece
[ ] Stackable items stack correctly
[ ] Non-stackable items don't stack
[ ] Max stack size enforced
[ ] Item icons load correctly
[ ] Tooltips display correct data
[ ] Locked items cannot be sold
[ ] Item add/remove triggers events
[ ] Item validation catches errors
[ ] Save/load preserves item state
```

---

## 18. Future Extensions

### Item Crafting Tiers
- Crafted items have quality levels

### Item Transmutation
- Convert items into materials

### Item Trading
- Trade with other players

### Item Auction House
- Buy/sell on market

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
