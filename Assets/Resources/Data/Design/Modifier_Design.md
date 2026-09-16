# Modifier System Design — IdleDefenseSurvival

**Purpose:** Core stat modification pipeline — foundation untuk semua bonus stat dari attributes, equipment, cards, buffs.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Attribute_Design.md](./Attribute_Design.md) — attribute → modifier conversion
- [Card_Design.md](./Card_Design.md) — card effects
- [Equipment_Design.md](./Equipment_Design.md) — equipment stat bonuses
- [Combat_Design.md](./Combat_Design.md) — final damage calculation

---

## 1. Core Formula

**GLOBAL FORMULA** — digunakan di seluruh sistem:

```
FinalValue = (Base + Flat) × (1 + Percent / 100)
```

**Data storage format:** semua nilai disimpan dalam bentuk final.
- Contoh: data `7.25` → jika percent berarti **7.25%** (bukan 0.0725)

**Calculation order:**
1. Sum all flat modifiers
2. Apply percent modifiers on `(base + flat sum)`
3. Special cases (e.g., `DamagePerRange`) have custom formulas

---

## 2. Modifier Types

### 2.1 Flat Modifiers

**Additive** — langsung ditambahkan ke base value.

```csharp
FlatTotal = Σ(all flat modifiers)
```

**Example:**
- Base AttackDamage: 100
- Flat +10 from equipment
- Flat +5 from card
- Result: 100 + 10 + 5 = **115**

### 2.2 Percent Modifiers

**Multiplicative** — applied on `(base + flat)`.

```csharp
PercentTotal = 1 + (Σ(all percent modifiers) / 100)
```

**Example:**
- Base + Flat: 115
- +20% from attribute
- +15% from card
- Result: 115 × (1 + (20 + 15) / 100) = 115 × 1.35 = **155.25**

**Important:** Percent modifiers are **additive with each other**, then multiplicative on base.

---

## 3. Modifier Sources

| Source | ModifierSource Enum | Owner | Pipeline Entry Point |
|--------|---------------------|-------|---------------------|
| Base stats | `BaseStats` | `BaseStatLoader` | `dataPlayer.json` |
| Attributes (CON/STR/INT/DEX) | `AccountLevel` | `AttributeModifierManager` | `AccountData.attributes` |
| Cards | `Card` | `CardModifierService` | `CardInventory.EquippedCards` |
| Equipment (main stats) | `Equipment` | `EquipmentModifierService` | `EquipmentService.EquippedItems` |
| Equipment (gems) | `Equipment` | `GemModifierService` | via `EquipmentModifierService` |
| Equipment (set bonus) | `Equipment` | `EquipmentSetBonusService` | via `EquipmentModifierService` |
| Equipment (special effects) | `Equipment` | `EquipmentEffectService` | via `EquipmentModifierService` |
| Temporary buffs | `Buff` | `BuffManager` (if exists) | Runtime buffs |
| Wave/Tier multipliers | N/A | `WaveManager` | Enemy stats only, not player |

---

## 4. Pipeline Architecture

```
ModifierCalculator (core math)
    ↑
    ├── AttributeModifierManager (CON/STR/INT/DEX → secondary stats)
    │       ↑
    │       └── AttributeStatLoader (loads dataMainAttribute.json)
    │
    ├── CardModifierService (card effects → modifiers)
    │       ↑
    │       └── CardInventory (equipped cards)
    │
    ├── EquipmentModifierService (equipment → modifiers)
    │       ↑
    │       ├── EquipmentStatCalculator (main stats)
    │       ├── GemModifierService (socketed gems)
    │       ├── EquipmentSetBonusService (set bonuses)
    │       └── EquipmentEffectService (special effects)
    │
    └── EffectRegistry (registers all effect types)
            ↑
            ├── Buff.cs (temporary buffs)
            └── EquipmentEffect.cs (equipment specials)
```

**Final aggregation:**
```
PlayerStatsManager.GetFinalStat(statType)
    → reads all ModifierSources
    → calls ModifierCalculator.Calculate(base, modifiers)
    → caches result
    → returns FinalValue
```

---

## 5. Owner Files

| Concern | Owner | Location |
|---------|-------|----------|
| Core math | `ModifierCalculator.cs` | `Scripts/Modifier/` |
| Effect registry | `EffectRegistry.cs` | `Scripts/Modifiers/` |
| Attribute conversion | `AttributeModifierManager.cs` | `Scripts/Manager/` |
| Card effects | `CardModifierService.cs` | `Scripts/Card/` |
| Equipment effects | `EquipmentModifierService.cs` | `Scripts/Equipment/` |
| Final stat aggregation | `PlayerStatsManager.cs` | `Scripts/Manager/` |

---

## 6. Cache Invalidation Rules

**Critical:** When any modifier source changes, **cache must be invalidated**.

Call `PlayerStatsManager.InvalidateCache()` after:

| Event | Reason |
|-------|--------|
| Equip/unequip item | Equipment modifiers changed |
| Card equipped/removed | Card modifiers changed |
| Attribute point allocated | Attribute modifiers changed |
| Gem socketed/removed/upgraded | Gem modifiers changed |
| Set bonus activated/deactivated | Set bonus modifiers changed |
| Enhancement level changed | Equipment stats changed |
| Buff applied/expired | Temporary modifiers changed |

**DO NOT:**
- Cache stale final stats in UI
- Rebuild final stats manually (always use `PlayerStatsManager`)
- Duplicate stat calculations in multiple places

---

## 7. Special Cases

### 7.1 DamagePerRange

Custom formula (not standard flat + percent):

```csharp
bonusDamage = BaseAttackDamage × DamagePerRangePercent × CurrentRange
```

Handled separately in combat calculation.

### 7.2 Critical Multipliers

Multiple critical tiers exist:
- Critical (base)
- SuperCritical
- UltraCritical

Each has its own damage multiplier, not additive.

---

## 8. Data Schema

### Modifier Entry (code)

```csharp
public class ModifierEntry
{
    public SecondaryStatType StatType;    // What stat to modify
    public float FlatValue;               // Flat bonus (0 if none)
    public float PercentValue;            // Percent bonus (0 if none)
    public ModifierSource Source;         // Where it came from
}
```

### JSON Schema (example from cards/equipment)

```json
{
  "statType": "AttackDamage",
  "flatValue": 10.0,
  "percentValue": 5.0
}
```

**Important:** `percentValue: 5.0` means **5%**, not 0.05.

---

## 9. Common Mistakes to Avoid

### ❌ Wrong: Mixing flat and percent incorrectly

```csharp
// WRONG
result = base * (1 + percent) + flat;  // Flat should be added BEFORE percent
```

### ✅ Correct: Flat first, then percent

```csharp
// CORRECT
result = (base + flat) * (1 + percent / 100);
```

### ❌ Wrong: Caching without invalidation

```csharp
// WRONG
float cachedAttack = PlayerStatsManager.GetFinalStat(AttackDamage);
EquipItem(newWeapon);
// cachedAttack is now stale!
```

### ✅ Correct: Always read fresh or invalidate

```csharp
// CORRECT
EquipItem(newWeapon);
PlayerStatsManager.InvalidateCache();
float freshAttack = PlayerStatsManager.GetFinalStat(AttackDamage);
```

### ❌ Wrong: Duplicating stat calculation

```csharp
// WRONG — rebuilding stats in UI
float totalAttack = baseAttack;
foreach (var item in equipped)
    totalAttack += item.attackBonus;
// Missing: cards, attributes, buffs, percent modifiers!
```

### ✅ Correct: Use authoritative pipeline

```csharp
// CORRECT
float totalAttack = PlayerStatsManager.GetFinalStat(SecondaryStatType.AttackDamage);
```

---

## 10. Testing Checklist

When modifying modifier system:

```
[ ] Base + Flat calculation correct
[ ] Percent applied on (Base + Flat)
[ ] Multiple percent modifiers stack additively
[ ] Cache invalidated on every modifier change
[ ] All modifier sources registered
[ ] No duplicate stat calculations
[ ] UI reads from PlayerStatsManager, not raw data
[ ] Save/load preserves modifier state
[ ] Edge case: zero/negative values handled
[ ] Edge case: multiple same-source modifiers
```

---

## 11. Extension Guide

### Adding New Modifier Source

1. Define new `ModifierSource` enum value (if needed)
2. Create service to convert source data → `ModifierEntry[]`
3. Register in `PlayerStatsManager` aggregation
4. Call `InvalidateCache()` when source changes
5. Test full pipeline

### Adding New Stat Type

1. Add to `SecondaryStatType` enum
2. Add base value to `dataPlayer.json`
3. Add attribute conversion (if applicable)
4. Update `ModifierCalculator` (if custom formula needed)
5. Update UI to display new stat

---

## 12. Performance Notes

- **Modifier calculation is NOT per-frame** — cached until invalidated
- **Invalidation is cheap** — just sets dirty flag
- **Recalculation happens on next `GetFinalStat()` call** — lazy evaluation
- **Avoid invalidating every frame** — batch changes when possible

---

## 13. Invariants

**These rules must NEVER be violated:**

1. Flat modifiers are always added before percent modifiers
2. Percent modifiers are additive with each other
3. Cache MUST be invalidated when modifier sources change
4. Only `PlayerStatsManager` computes final stats
5. UI never computes stats directly
6. All modifier sources go through the pipeline
7. No hidden stat calculations outside the pipeline

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
