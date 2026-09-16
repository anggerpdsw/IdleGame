# Design Documentation Index — IdleDefenseSurvival

**Purpose:** Master index for all gameplay/system design specifications.

**Last Updated:** 2026-09-16

---

## How To Use This Documentation

Before modifying ANY game system:

1. **Start here** — locate the relevant design document(s)
2. **Read the design doc** for the system you're changing
3. **Read related docs** (check "Related Design Documents" section)
4. **Make implementation changes**
5. **Update ALL affected design docs**
6. **Verify cross-references still valid**

**Documentation is part of implementation.** Code without updated docs = incomplete work.

---

## Design Documentation Map

### Core Systems (Foundation)

| Domain | File | Scope |
|--------|------|-------|
| **Modifier Pipeline** | [Modifier_Design.md](./Modifier_Design.md) | Core formula, flat/percent, pipeline, cache invalidation |
| **Attributes** | [Attribute_Design.md](./Attribute_Design.md) | CON/STR/INT/DEX, per-point bonuses, level curves |
| **Combat** | [Combat_Design.md](./Combat_Design.md) | Damage pipeline, defense formula, hit chance |

### Player Systems

| Domain | File | Scope |
|--------|------|-------|
| **Player** | [Player_Design.md](./Player_Design.md) | Auto-attack, stat list, combat behavior |
| **Projectile** | [Projectile_Design.md](./Projectile_Design.md) | Movement, collision, damage, bounce, pooling |
| **Cards** | [Card_Design.md](./Card_Design.md) | Rarities, pity, leveling, slots, roll costs |
| **Ultimate** | [Ultimate_Design.md](./Ultimate_Design.md) | 8 handler types, factory, cooldowns |

### Enemy & Wave Systems

| Domain | File | Scope |
|--------|------|-------|
| **Enemy** | [Enemy_Design.md](./Enemy_Design.md) | AI behavior, movement, attack, death |
| **Status Effects** | [StatusEffect_Design.md](./StatusEffect_Design.md) | Slow/Stun/DefenseBreak/HeartBreak, stacking |
| **Spawn** | [Spawn_Design.md](./Spawn_Design.md) | Spawn positions, weights, timing |
| **Wave** | [Wave_Design.md](./Wave_Design.md) | InterWave/ActiveWave, tier progression, difficulty |

### Equipment & Items

| Domain | File | Scope |
|--------|------|-------|
| **Equipment** | [Equipment_Design.md](./Equipment_Design.md) | 11 slots, rarities, affixes, sockets, set bonuses |
| **Inventory** | [Inventory_Design.md](./Inventory_Design.md) | Capacity, drag-drop, view architecture |
| **Item** | [Item_Design.md](./Item_Design.md) | ItemId vs InstanceId, categories, icons |
| **Consumable** | [Consumable_Design.md](./Consumable_Design.md) | Use workflow, validation, inventory integration |

### Crafting & Materials

| Domain | File | Scope |
|--------|------|-------|
| **Crafting** | [Crafting_Design.md](./Crafting_Design.md) | Pipeline stages, queue, VIP modifiers |
| **Material** | [Material_Design.md](./Material_Design.md) | 45 materials, T1-T6 tiers, crafting usage |
| **Herb** | [Herb_Design.md](./Herb_Design.md) | 35 herbs, potion categories |

### Economy & Progression

| Domain | File | Scope |
|--------|------|-------|
| **Economy** | [Economy_Design.md](./Economy_Design.md) | Gold/Gem/Meat/EXP, mutation rules |
| **Reward** | [Reward_Design.md](./Reward_Design.md) | Calculation, fractional handling |
| **Daily Reward** | [DailyReward_Design.md](./DailyReward_Design.md) | 7-reward sequence, cooldown, VIP |
| **Idle Reward** | [IdleReward_Design.md](./IdleReward_Design.md) | Offline progression, 4h cap, formulas |
| **Mission** | [Mission_Design.md](./Mission_Design.md) | Event types, lifecycle, rewards |
| **VIP** | [VIP_Design.md](./VIP_Design.md) | 3 bool flags, integration points |

### Enemy Drops

| Domain | File | Scope |
|--------|------|-------|
| **Enemy Drop** | [EnemyDrop_Design.md](./EnemyDrop_Design.md) | Drop tables, material gates, rates |
| **Drop Bag** | [DropBag_Design.md](./DropBag_Design.md) | Run recap UI, aggregation |

### Persistence & Technical

| Domain | File | Scope |
|--------|------|-------|
| **Save Manager** | [SaveManager_Design.md](./SaveManager_Design.md) | Schema, versioning (v1-v4), migration |
| **Scene Persistence** | [ScenePersistence_Design.md](./ScenePersistence_Design.md) | 6 scenes, DontDestroyOnLoad rules |
| **UI Architecture** | [UI_Design.md](./UI_Design.md) | View responsibilities, forbidden ownership |

### Companion Systems

| Domain | File | Scope |
|--------|------|-------|
| **Pet System** | [PetSystem.md](./PetSystem.md) | Architecture, Voidling, skills, emergency mode |

---

## Dependency Map

```
Combat_Design
 ├── Modifier_Design (damage calculation)
 ├── Attribute_Design (stat sources)
 ├── Player_Design (player stats)
 ├── Enemy_Design (enemy stats)
 ├── Projectile_Design (damage delivery)
 └── StatusEffect_Design (combat effects)

Equipment_Design
 ├── Modifier_Design (stat bonuses)
 ├── Attribute_Design (attribute bonuses)
 ├── Item_Design (ItemId/InstanceId)
 └── SaveManager_Design (persistence)

Card_Design
 ├── Modifier_Design (card effects)
 ├── Economy_Design (gem costs)
 └── SaveManager_Design (card inventory)

Wave_Design
 ├── Enemy_Design (spawn targets)
 ├── Spawn_Design (spawn mechanics)
 └── Reward_Design (wave rewards)

Crafting_Design
 ├── Material_Design (ingredients)
 ├── Herb_Design (potion ingredients)
 ├── Equipment_Design (output)
 └── Economy_Design (costs)

PetSystem
 ├── Combat_Design (damage)
 ├── Projectile_Design (attacks)
 ├── StatusEffect_Design (pet skills)
 └── SaveManager_Design (pet save)
```

---

## Task → Document Routing

| If you want to... | Read these docs first |
|-------------------|----------------------|
| Change damage calculation | Combat_Design, Modifier_Design, Attribute_Design |
| Add/modify enemy | Enemy_Design, Combat_Design, Spawn_Design, EnemyDrop_Design |
| Change card system | Card_Design, Modifier_Design, Economy_Design |
| Modify equipment | Equipment_Design, Modifier_Design, Item_Design |
| Add crafting recipe | Crafting_Design, Material_Design, Equipment_Design |
| Change wave progression | Wave_Design, Spawn_Design, Enemy_Design, Reward_Design |
| Modify save format | SaveManager_Design + all affected system docs |
| Add UI feature | UI_Design + relevant domain doc (e.g., Equipment_Design for equipment UI) |
| Change pet behavior | PetSystem, Combat_Design, StatusEffect_Design |
| Modify economy/currency | Economy_Design, Reward_Design, DailyReward_Design |

---

## Design-Aware Development Workflow

### Before Implementation

```
[ ] Identify affected system(s)
[ ] Read relevant design doc(s)
[ ] Read related/dependency docs
[ ] Understand formulas, constraints, edge cases
[ ] Verify current implementation matches docs (if not, fix docs first)
```

### During Implementation

```
[ ] Keep changes aligned with documented design
[ ] Do not silently introduce new behavior
[ ] Test formulas/calculations match design spec
```

### After Implementation

```
[ ] Determine if design changed
[ ] Update relevant design doc(s)
[ ] Update cross-references if dependencies changed
[ ] Update this README.md if new doc added
[ ] Verify no contradictory design remains
[ ] Verify CLAUDE.md still points correctly
```

---

## Source of Truth Hierarchy

When sources disagree:

1. **Current runtime code** (what actually runs)
2. **Current JSON data** (what's actually loaded)
3. **Design documentation** (intended behavior)
4. **CLAUDE.md** (agent instructions)
5. Historical notes/assumptions

If code and docs disagree: inspect implementation → determine intended behavior → fix source of truth → update docs.

---

## Adding New Design Documents

1. Create `.md` file in `Assets/Resources/Data/Design/`
2. Add row to **Design Documentation Map** table above
3. Add entry to **Dependency Map** if it depends on other systems
4. Add routing entries to **Task → Document Routing** table
5. Include in the new doc:
   - Purpose section
   - Scope (what it covers)
   - Related Design Documents (with links)
   - Core concepts/rules
   - Formulas (if applicable)
   - Data schema (if applicable)
   - Integration points
   - Edge cases/constraints

---

## Documentation Synchronization Rules

**MANDATORY:** Every implementation change that affects design MUST update design docs.

| Change Type | Action Required |
|-------------|----------------|
| **Feature addition** | Update relevant doc + add cross-references |
| **Feature modification** | Update design doc + verify related docs |
| **Feature removal** | Remove from docs + check dangling references |
| **Bug fix (behavior change)** | Update doc if intended behavior changed |
| **Balance/formula change** | Update formulas in doc |
| **Data schema change** | Update schema section + SaveManager_Design if persistent |
| **New interaction** | Document in both system docs |

**Remember:** Design docs are NOT passive. They are active development artifacts that must stay synchronized with implementation.

---

## Validation Checklist

Before considering any system change complete:

```
[ ] Design doc updated with new behavior
[ ] Cross-references verified/updated
[ ] No contradictions with related docs
[ ] Formulas/calculations documented
[ ] Edge cases documented
[ ] Integration points documented
[ ] CLAUDE.md routing still correct
[ ] README.md index still correct
```

---

## Notes

- Design docs explain **WHAT** and **WHY**, code explains **HOW**
- Keep docs concise — only document what cannot be derived from code/data
- Use cross-references liberally — avoid duplication
- When in doubt, read the code first, then update docs to match reality
