# Attribute System Design — IdleDefenseSurvival

**Purpose:** Main attribute system (CON/STR/INT/DEX) — foundation 80% dari character power.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Modifier_Design.md](./Modifier_Design.md) — attribute → stat conversion pipeline
- [Player_Design.md](./Player_Design.md) — player stats
- [Combat_Design.md](./Combat_Design.md) — damage calculation
- [Equipment_Design.md](./Equipment_Design.md) — equipment attribute bonuses

---

## 1. Core Attributes

**4 main attributes** — sumber utama semua character power:

| Attribute | Abbreviation | Primary Role |
|-----------|--------------|--------------|
| **Constitution** | CON | Survival / Tank |
| **Strength** | STR | Physical Damage |
| **Intelligence** | INT | Magic / Crit |
| **Dexterity** | DEX | Speed / Evasion |

---

## 2. Starting Values

**Level 1 start:**
- CON: 5
- STR: 5
- INT: 5
- DEX: 5
- **Unspent Points:** 0

**Per level-up:**
- +5 unspent attribute points
- Can allocate freely to any attribute

**Source:** `GameConstants.cs`
```csharp
public const int STARTING_STAT_POINTS = 5;  // Level 1 start
public const int POINTS_PER_LEVEL = 5;      // Bonus per level-up
```

**Persistence:** `SaveData.account.attributes` (CON/STR/INT/DEX)

---

## 3. Attribute Contributions

### 3.1 Constitution (CON)

Contributes to:

| Secondary Stat | Formula | Notes |
|----------------|---------|-------|
| **HealthPoint** | Base + (CON × BonusPerPoint) | Primary survival stat |
| **DefenseAmount** | Base + (CON × BonusPerPoint) | Damage reduction |
| **HealthRegen** | Base + (CON × BonusPerPoint) | HP per second |
| **DeathDefy** | Base + (CON × BonusPerPoint) | Chance to survive lethal hit |

**Build identity:** Tank, survival, durability.

---

### 3.2 Strength (STR)

Contributes to:

| Secondary Stat | Formula | Notes |
|----------------|---------|-------|
| **AttackDamage** | Base + (STR × BonusPerPoint) | Primary damage stat |
| **KnockbackChance** | Base + (STR × BonusPerPoint) | CC utility |
| **Penetration** | Base + (STR × BonusPerPoint) | Ignore enemy defense |
| **UltimateAttack** | Base + (STR × BonusPerPoint) | Ultimate damage boost |

**Build identity:** Physical DPS, raw damage, knockback.

---

### 3.3 Intelligence (INT)

Contributes to:

| Secondary Stat | Formula | Notes |
|----------------|---------|-------|
| **ManaPoint** | Base + (INT × BonusPerPoint) | Mana pool |
| **ManaRegen** | Base + (INT × BonusPerPoint) | Mana per second |
| **ElementMastery** | Base + (INT × BonusPerPoint) | Elemental damage boost |
| **AttackRange** | Base + (INT × BonusPerPoint) | Attack radius |

**Build identity:** Magic caster, elemental damage, range.

---

### 3.4 Dexterity (DEX)

Contributes to:

| Secondary Stat | Formula | Notes |
|----------------|---------|-------|
| **AttackSpeed** | Base + (DEX × BonusPerPoint) | Attacks per second |
| **CriticalChance** | Base + (DEX × BonusPerPoint) | Crit chance % |
| **Evasion** | Base + (DEX × BonusPerPoint) | Dodge chance % |
| **DamagePerRange** | Base + (DEX × BonusPerPoint) | Damage scaling with distance |

**Build identity:** Swift assassin, crit, evasion, speed.

---

## 4. Data Sources

### 4.1 Per-Point Bonuses

**File:** `Assets/Resources/Data/Player/dataMainAttribute.json`

**Schema example:**
```json
{
  "constitution": {
    "healthPoint": 10.0,
    "defenseAmount": 0.5,
    "healthRegen": 0.2,
    "deathDefy": 0.1
  },
  "strength": {
    "attackDamage": 2.0,
    "knockbackChance": 0.3,
    "penetration": 0.5,
    "ultimateAttack": 1.0
  },
  "intelligence": {
    "manaPoint": 8.0,
    "manaRegen": 0.3,
    "elementMastery": 0.5,
    "attackRange": 0.1
  },
  "dexterity": {
    "attackSpeed": 0.02,
    "criticalChance": 0.3,
    "evasion": 0.2,
    "damagePerRange": 0.1
  }
}
```

**Important:** Values adalah per-point bonus. Total = `AttributeValue × BonusPerPoint`.

---

### 4.2 Level Scaling Curves

**Main attribute value per level:**
- **File:** `Assets/Resources/Data/Player/dataAttributeMainValuePerLevel.json`
- **Purpose:** Attribute point value scaling by level (if implemented)

**Secondary stat value per level:**
- **File:** `Assets/Resources/Data/Player/dataSOTValuePerLevel.json`
- **Purpose:** Secondary stat base value curve

**Note:** Current implementation uses fixed per-point bonuses. Curve files exist for future level-scaling if needed.

---

## 5. Pipeline Architecture

```
AccountData.attributes (CON/STR/INT/DEX values)
    ↓
AttributeStatLoader.Load()
    ↓
AttributeModifierManager.GetAttributeModifiers()
    ↓
ModifierCalculator.Calculate()
    ↓
PlayerStatsManager.GetFinalStat()
    ↓
Combat / UI
```

**Key files:**
- `Scripts/Manager/AttributeStatLoader.cs` — loads JSON data
- `Scripts/Manager/AttributeModifierManager.cs` — converts attributes → modifiers
- `Scripts/Player/AttributeService.cs` — attribute allocation UI service
- `Scripts/Modifier/ModifierCalculator.cs` — final calculation
- `Scripts/Manager/PlayerStatsManager.cs` — stat aggregation + cache

---

## 6. Attribute Allocation

**UI Flow:**
1. Player opens attribute panel
2. Shows current CON/STR/INT/DEX + unspent points
3. Player clicks + button on desired attribute
4. `AttributeService.AllocatePoint(attributeType)` called
5. Validates unspent points > 0
6. Increments attribute value
7. Decrements unspent points
8. Saves to `AccountData`
9. Calls `PlayerStatsManager.InvalidateCache()`
10. UI refreshes stats

**Validation rules:**
- Cannot allocate if unspent points = 0
- Cannot reduce allocated attributes (no respec yet)
- Attributes have no maximum cap (design decision)

---

## 7. Formulas

### 7.1 Secondary Stat Calculation

```
SecondaryStatValue = (AttributeValue × BonusPerPoint)
```

**Example (CON → HealthPoint):**
- CON = 50
- BonusPerPoint = 10.0 (from dataMainAttribute.json)
- Result: 50 × 10.0 = **500 HP from CON**

### 7.2 Total Stat with Base

```
TotalStat = BaseStat + (AttributeValue × BonusPerPoint) + OtherModifiers
```

**Example (AttackDamage):**
- Base: 20 (from dataPlayer.json)
- STR: 40
- BonusPerPoint: 2.0
- Equipment: +15 flat
- Card: +10% percent
- Result: (20 + (40 × 2.0) + 15) × 1.10 = (20 + 80 + 15) × 1.10 = **126.5**

---

## 8. Build Archetypes

### 8.1 Tank (CON Focus)

**Stat distribution:**
- CON: 60% points
- STR: 20% points
- DEX: 20% points

**Strengths:** High HP, high defense, survival
**Weaknesses:** Low damage, slow clear

---

### 8.2 Warrior (STR Focus)

**Stat distribution:**
- STR: 60% points
- CON: 20% points
- DEX: 20% points

**Strengths:** High damage, knockback CC
**Weaknesses:** Lower crit, lower speed

---

### 8.3 Mage (INT Focus)

**Stat distribution:**
- INT: 60% points
- CON: 20% points
- DEX: 20% points

**Strengths:** Elemental damage, range, mana
**Weaknesses:** Low HP, low physical defense

---

### 8.4 Assassin (DEX Focus)

**Stat distribution:**
- DEX: 60% points
- STR: 20% points
- INT: 20% points

**Strengths:** High crit, high speed, evasion
**Weaknesses:** Lower HP, requires precision

---

### 8.5 Hybrid Builds

**Bruiser (CON + STR):** Tanky DPS
**Spellblade (INT + DEX):** Fast caster with crit
**Ranger (STR + DEX):** Physical crit DPS

**Design principle:** Every attribute has ≥2 main slots in equipment, no orphaned stats.

---

## 9. Equipment Slot Specialization

Attributes guide which equipment slots benefit which builds:

| Build Focus | Priority Slots |
|-------------|----------------|
| CON | Armor, Pants, Belt |
| STR | Gloves, Bracelet, Pendant |
| INT | Hat, Ring, Pendant |
| DEX | Shoes, Earring, Cape |

**Hybrid slots:**
- Belt: CON + STR
- Pendant: STR + INT
- Cape: DEX + INT

**See:** `Equipment_Design.md` §2 Slot Identities

---

## 10. Common Mistakes

### ❌ Wrong: Hardcoding per-point bonuses

```csharp
// WRONG
float hpBonus = conValue * 10; // Magic number
```

### ✅ Correct: Read from data

```csharp
// CORRECT
float hpBonus = conValue * AttributeData.constitution.healthPoint;
```

### ❌ Wrong: Forgetting cache invalidation

```csharp
// WRONG
AccountData.attributes.constitution++;
// Stats not updated!
```

### ✅ Correct: Invalidate after change

```csharp
// CORRECT
AccountData.attributes.constitution++;
PlayerStatsManager.InvalidateCache();
```

### ❌ Wrong: UI computing stat contribution

```csharp
// WRONG — duplicating pipeline logic in UI
float displayHP = baseHP + (con * 10);
```

### ✅ Correct: Read final stat

```csharp
// CORRECT
float displayHP = PlayerStatsManager.GetFinalStat(SecondaryStatType.HealthPoint);
```

---

## 11. Save Integration

**Persistence location:** `SaveData.account.attributes`

**Schema:**
```csharp
public class AccountData
{
    public AttributeData attributes = new AttributeData
    {
        constitution = 5,
        strength = 5,
        intelligence = 5,
        dexterity = 5
    };
    
    public int unspentAttributePoints = 0;
}
```

**Save trigger:** Whenever attributes change, `SaveManager.SaveAll()` called.

---

## 12. Testing Checklist

```
[ ] Attributes start at 5 each at level 1
[ ] +5 unspent points per level-up
[ ] Allocation increments attribute, decrements unspent
[ ] Cannot allocate if unspent = 0
[ ] Cache invalidated after allocation
[ ] Final stats reflect attribute changes
[ ] Save/load preserves attribute values
[ ] UI displays correct total stats
[ ] Per-point bonuses match dataMainAttribute.json
[ ] All 4 attributes contribute to expected stats
```

---

## 13. Future Extensions

### Respec System
- Cost: Gold or special item
- Refund all allocated points
- Reset to base (5 each)

### Attribute Milestones
- Unlock bonuses at specific thresholds (e.g., 50 CON → +1% all damage reduction)

### Soft Caps
- Diminishing returns above threshold (not yet implemented)

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
