# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) and other coding agents when working with this repository.

---

# 1. PROJECT MANDATE

## Project

**IdleDefenseSurvival** is a 2D auto-shooter / idle defense survival game built with **Unity 6000.3.18f1 (Unity 6)**.

The project is designed around:

- automatic combat;
- long-term progression;
- wave/tier progression;
- cards;
- equipment;
- inventory;
- crafting;
- gems/sockets;
- attributes and modifiers;
- ultimates;
- idle/offline rewards;
- daily rewards;
- persistent save data;
- data-driven balancing.

The reference game is **Wild Survival - Idle Defense**. It is a reference for game feel, progression, presentation, and broad gameplay patterns — **not a source to blindly copy architecture or numbers**.

## Non-negotiable development rule

Before changing code:

1. Understand the existing architecture.
2. Search for the existing implementation of the behavior.
3. Check the relevant design/data documentation under:
   `Assets/Resources/Data/Design`
4. Check related JSON data under:
   `Assets/Resources/Data`
5. Preserve existing save compatibility unless the task explicitly requires a migration.
6. Make the smallest correct change that fits the existing architecture.
7. Update the relevant design documentation when the behavior, formula, data schema, or architecture changes.
8. If no suitable design document exists, create one.
9. Do not leave the repository documentation describing behavior that the code no longer implements.

**Documentation is part of the implementation.**

Every significant change to a system must leave behind enough design information for a future developer/agent to understand:

- what changed;
- why it changed;
- the intended behavior;
- relevant formulas;
- data schema;
- dependencies;
- save implications;
- edge cases;
- known constraints.

Design documentation location:

`Assets/Resources/Data/Design`

---

# 2. PROJECT TRUTH HIERARCHY

When sources disagree, use this order:

1. **Current compiled/runtime code**
2. **Current JSON data actually loaded by the game**
3. **Current design documentation**
4. **This CLAUDE.md**
5. Historical chat notes / assumptions
6. Generic Unity or game-development assumptions

If code and documentation disagree, **do not silently assume the documentation is correct**.

Instead:

- inspect the implementation;
- determine the intended behavior;
- fix the source of truth;
- update the documentation.

Never preserve an obsolete value merely because it appears in this file.

Examples of values that must always be verified from the current project before changing them:

- card roll costs;
- pity thresholds;
- maximum card slots;
- equipment slot count;
- wave limits;
- daily reward timers;
- currency limits;
- enhancement costs;
- socket rules;
- stat formulas.

---

# 3. DEVELOPMENT PHILOSOPHY

## 3.1 Correctness before cleverness

Prefer:

> simple + explicit + testable + maintainable

over:

> clever + abstract + generic + difficult to debug

Do not introduce abstraction merely because an abstraction is technically possible.

## 3.2 YAGNI, but do not oversimplify the domain

The original project philosophy used a strict YAGNI ladder. Keep that principle, but apply it correctly:

1. Is the behavior already implemented somewhere?
2. Can an existing service/system own this responsibility?
3. Can existing C#/Unity functionality solve it?
4. Can an existing project dependency solve it?
5. Only then introduce new architecture.

However:

**Do not use YAGNI as an excuse to put business logic into UI classes, duplicate systems, or create fragile shortcuts.**

The goal is minimum necessary architecture, not minimum number of files.

## 3.3 One source of truth

A gameplay rule should have one authoritative owner.

Avoid:

- duplicated formulas;
- duplicated item lookup;
- duplicated stat aggregation;
- duplicated save logic;
- duplicated currency mutation;
- duplicated equipment validation;
- duplicated card effect application.

If two systems need the same rule, extract or reuse the authoritative service/calculator.

---

# 4. TECH STACK

- Unity **6000.3.18f1**
- 2D Render Pipeline
- Unity Input System
- UGUI
- Physics2D
- Unity Test Framework
- DOTween
- Newtonsoft.Json
- JSON runtime data under `Assets/Resources/Data`

Target platform may currently be Windows during development, but gameplay architecture should remain suitable for the intended 2D Android game.

---

# 5. DATA-DRIVEN ARCHITECTURE

## 5.1 JSON is the balance/configuration source

Game balance and definitions are primarily stored in JSON:

- attribute data;
- card data;
- socket configuration;
- enemy data;
- item data;
- player data;
- ultimate data;
- wave data;
- recipes and related definitions.

Runtime systems consume these definitions.

Do not hardcode balance values in gameplay classes unless the value is genuinely an implementation constant.

## 5.2 Resources/Data

Main location:

`Assets/Resources/Data`

Important files:

| File | Purpose |
|---|---|
| `dataPlayer.json` | Player base stats, main attributes, skill base values |
| `dataEnemy.json` | Enemy types, stats, spawn weights, rewards, roles, elements |
| `dataWave.json` | Wave duration, difficulty, spawn configuration, progression |
| `dataUltimate.json` | Ultimate definitions |
| `dataCard.json` | Card definitions, rarity, scaling, effects |
| `dataItems.json` | Items, equipment, gems, affixes, sets, materials |
| `dataAttribute.json` | CON/STR/INT/DEX attribute bonuses |
| `dataConfigSocket.json` | Socket/gem rules |

## 5.3 Design documentation

Location:

`Assets/Resources/Data/Design`

Design files should explain **intent and rules**, while JSON should contain the actual balance/configuration values.

Do not duplicate hundreds of balance values into design documents unless needed for explanation.

---

# 6. IDENTIFIERS AND PERSISTENCE

This is a critical architectural rule.

## 6.1 Definition ID vs Instance ID

Definitions and runtime instances are different concepts.

Example:

- `ItemId = "potion_hp"` identifies the item definition.
- `InstanceId = GUID` identifies one persistent item instance.

### ItemId

Used for:

- lookup;
- loading definition data;
- item type/category;
- icon resolution;
- static configuration.

Do not use display names as persistent identifiers.

### InstanceId

Used when the player owns a specific persistent instance.

It is especially important for:

- equipment;
- individually tracked items;
- durability;
- enhancement;
- sockets;
- affixes;
- enchantment;
- unique state.

**Never use item display names as save keys or logic keys.**

Changing:

- `Name`
- localized display text
- description

must not invalidate persistent data.

Changing an `Id` is a data migration problem.

---

# 7. ICONS AND RESOURCE LOOKUP

Item/card/equipment icons must be resolved through stable identifiers.

Do not make gameplay logic depend on human-readable names.

If an icon path/key is changed:

1. update the definition;
2. update the resolver if required;
3. verify all affected assets;
4. do not silently introduce fallback paths that hide broken data.

If a resolver accepts hierarchical paths, preserve the expected path convention consistently.

Example:

`potion/hp`

is preferable to accidentally duplicating path segments such as:

`potion/potion/hp`

unless the project explicitly defines that structure.

---

# 8. SAVE SYSTEM

## 8.1 SaveData

Persistent data is stored in:

`SaveData.json`

at:

`Application.persistentDataPath`

The save system is centralized in:

`Scripts/Manager/SaveManager.cs`

Current persistent domains include:

- account data;
- VIP data;
- game state;
- wave progress;
- idle rewards;
- daily rewards;
- card inventory;
- inventory;
- equipment;
- crafting queue;
- other persistent progression data.

## 8.2 Save ownership

Gameplay systems should not independently write arbitrary JSON files unless explicitly designed to do so.

Prefer:

`System Service -> SaveManager -> SaveData`

rather than:

`UI -> JSON`

or:

`Gameplay object -> JSON`

## 8.3 Save compatibility

Before changing a persistent data structure:

1. identify existing fields;
2. identify old saves that may exist;
3. determine whether the change is backward compatible;
4. add migration/default handling when necessary;
5. test loading an old save;
6. test loading a new save;
7. test missing/null collections.

Never assume a fresh save is the only save that matters.

## 8.4 Dirty-state saving

When a system changes persistent state, it should correctly mark that state dirty or notify the persistence layer.

Do not add save calls everywhere.

The save architecture should remain centralized and predictable.

---

# 9. INVENTORY ARCHITECTURE

Inventory is a persistent domain, not a UI feature.

Primary systems:

`Scripts/Inventory/`

and:

`Scripts/Items/`

Responsibilities include:

- item ownership;
- slot/capacity management;
- item quantities;
- instance identity;
- consumables;
- materials;
- equipment;
- gems;
- item state;
- inventory persistence.

## 9.1 Inventory events

The inventory has multiple kinds of changes, for example:

- structural inventory changes;
- quantity changes;
- item addition/removal.

Do not blindly trigger every event for every mutation.

A single operation such as consuming one potion must not accidentally cause duplicate UI refreshes or duplicate gameplay effects because multiple overlapping events fire.

When changing inventory events:

1. identify which event represents the semantic change;
2. identify which consumers subscribe to it;
3. ensure one logical mutation produces one logical reaction.

## 9.2 Consumables

Consumable use should:

1. validate the item;
2. validate the quantity;
3. apply the gameplay effect;
4. remove the consumed quantity;
5. trigger the correct inventory/save events;
6. update UI through existing event flow.

Do not put the entire consumable system inside a UI click handler.

---

# 10. EQUIPMENT SYSTEM

Equipment is a major progression system.

Primary location:

`Scripts/Equipment/`

Equipment supports:

- equipment slots;
- level;
- enhancement;
- durability;
- sockets;
- gems;
- affixes;
- enchantments;
- set bonuses;
- special effects;
- comparison;
- auto-equip;
- persistence;
- visual representation.

## 10.1 Current conceptual equipment slots

The intended equipment model uses these slot identities:

- Hat
- Gloves
- Cape
- Armor
- Belt
- Pants
- Pendant
- Ring
- Earring
- Bracelet
- Shoes

Do not introduce alternate slot names such as:

- Boots vs Shoes
- Necklace vs Pendant
- Artifact vs Bracelet

unless the actual project data has intentionally changed.

Slot identity must be stable for save data.

## 10.2 Equipment service responsibilities

The equipment service owns operations such as:

- equip;
- unequip;
- swap;
- equip by `InstanceId`;
- auto-equip;
- persistence;
- slot validation.

UI must request equipment operations from the equipment domain instead of directly mutating equipment state.

## 10.3 Stat aggregation

Final equipment stats should be calculated from the authoritative equipment aggregation pipeline.

Relevant sources may include:

- main stats;
- enhancement;
- enchantment;
- gems;
- affixes;
- set bonuses;
- special effects.

Do not manually reconstruct equipment bonuses inside Player UI, tooltips, or individual gameplay classes.

---

# 11. SOCKET AND GEM SYSTEM

Primary systems:

- `GemService`
- `SocketConfigData`

Current design concepts include:

- maximum sockets per item;
- socket unlock requirements;
- allowed gem types;
- adding sockets;
- removing gems;
- destroying gems;
- gem experience;
- gem upgrading.

The current configuration must be read from:

`dataConfigSocket.json`

Do not hardcode socket rules into UI.

Gem operations must produce the appropriate domain events:

- gem socketed;
- gem removed;
- gem destroyed;
- gem upgraded;
- gem experience changed.

---

# 12. ATTRIBUTE SYSTEM

Main attributes:

- **CONSTITUTION**
- **STRENGTH**
- **INTELLIGENCE**
- **DEXTERITY**

Default starting attributes are defined by the attribute system/data.

Conceptually:

### Constitution

Contributes to:

- HealthPoint
- DefenseAmount
- HealthRegen
- DeathDefy

### Strength

Contributes to:

- AttackDamage
- KnockbackChance
- Penetration
- UltimateAttack

### Intelligence

Contributes to:

- ManaPoint
- ManaRegen
- ElementMastery where defined by the combat system
- AttackRange

### Dexterity

Contributes to:

- AttackSpeed
- CriticalChance
- Evasion
- DamagePerRange

The exact values must come from:

`dataAttribute.json`

Do not duplicate attribute-to-stat conversion tables in multiple scripts.

---

# 13. MODIFIER ARCHITECTURE

The modifier pipeline is central to player progression.

Sources can include:

- base player stats;
- attributes;
- cards;
- equipment;
- gems;
- set bonuses;
- upgrades;
- temporary effects;
- other explicitly supported systems.

Primary systems:

- `ModifierManager`
- `CardModifierService`
- equipment modifier services
- `PlayerStatsManager`

## 13.1 Flat vs Percent

Modifiers must clearly distinguish:

- Flat
- Percent

Do not mix them accidentally.

A stat calculation should follow one defined order.

When changing modifier order:

1. document the order;
2. test the result;
3. update all affected systems.

## 13.2 Do not cache stale final stats

If a source of modifiers changes, the authoritative stat pipeline must be refreshed.

Examples:

- equip item;
- unequip item;
- card equipped;
- card removed;
- attribute changed;
- gem changed;
- set bonus changed;
- enhancement changed.

---

# 14. CARD SYSTEM

Primary location:

`Scripts/Card/`

Core components include:

- `CardManager`
- `CardDatabase`
- `CardInventory`
- `CardUpgradeService`
- `CardRollService`
- `CardEquipmentService`
- `CardModifierService`

## 14.1 Rarities

Current rarity model:

- Common
- Rare
- Epic
- Legendary
- Mythic

## 14.2 Card leveling

Duplicate cards increase card level.

Current progression is designed around duplicate requirements:

`1, 2, 4, 7, 11, 19, 31, 47, 69, 99`

Cumulative duplicates required through level 10:

`290`

Do not change this progression without updating the relevant design/balance document.

## 14.3 Card slots

Card equipment has a defined maximum slot count.

The actual authoritative value must be taken from the current `CardEquipmentService` / project data rather than copied from stale documentation.

## 14.4 Card effects

Card effects may be:

- flat stat modifiers;
- percentage modifiers;
- special effects.

Examples include:

- FrostAura;
- Shield;
- TimeFast;
- Gold;
- Meat.

Special effects must have an explicit owner.

Do not implement special card behavior inside generic UI classes.

## 14.5 Card roll costs

The roll cost is a balance value and must come from the current implementation/data.

Known design target from previous development:

- 1x = 20 gems
- 10x = 190 gems
- 100x = 1800 gems

If the current code/data differs, verify which is authoritative before changing anything.

Roll calculation should preserve the intended bundled-discount behavior rather than naïvely doing:

`amount * singleCost`.

## 14.6 CardRoll item

The project supports a free card-roll item.

If the player uses a `CardRoll` inventory item:

- consume the item only when the roll succeeds;
- if the operation is invalid because card capacity is full, refund/preserve the item;
- do not substitute a gem refund for an item refund;
- keep item-based rolls separate from gem-based rolls.

---

# 15. PLAYER COMBAT

Primary systems:

- `Player`
- `PlayerStats`
- `PlayerStatsManager`
- `Projectile`

The player is centered in the gameplay arena and auto-attacks.

Player stats include concepts such as:

- AttackDamage
- AttackSpeed
- AttackRange
- CriticalChance
- Critical/SuperCritical/UltraCritical
- MultiShoot
- Bounce
- Knockback
- LifeSteal
- Health
- HealthRegen
- Defense
- Evasion
- Mana
- ManaRegen
- UltimateWeaponAttack

The exact final values must be calculated by the authoritative stat pipeline.

---

# 16. PROJECTILE SYSTEM

`Scripts/Player/Projectile.cs`

Projectiles are pooled and reused.

Projectile responsibilities include:

- movement;
- target handling;
- collision;
- damage;
- critical states;
- bounce;
- knockback;
- stun;
- life steal;
- range-based effects;
- status effects where explicitly configured.

## 16.1 Defense Break

Defense Break is a combat status/effect, not merely a visual indicator.

When a projectile applies Defense Break:

1. determine the source and effect value;
2. apply it through the authoritative enemy status/effect system;
3. let the target's defense calculation consume the active effect;
4. preserve duration/type/stacking rules;
5. display the effect through UI separately.

Do not make `Projectile` directly manipulate UI state.

---

# 17. ENEMY SYSTEM

Primary systems:

- `EnemyAi`
- `EnemySpawner`
- `EnemyStatusEffectController`
- health bar management.

Enemy behavior:

1. spawn outside the player's attack range;
2. approach the player;
3. stop at its attack range;
4. attack according to cooldown;
5. respond to knockback;
6. respond to slow/stun/defense break/HeartBreak;
7. die and distribute rewards.

Movement should use the current steering implementation rather than introducing pathfinding unless the game design actually requires pathfinding.

---

# 18. STATUS EFFECTS

Current status concepts include:

- Slow
- Defense Break
- Stun
- HeartBreak / Max Health Reduction

Slow and Defense Break support different effect types such as:

- Permanent
- Aura
- Temporary

Stacking behavior must follow the existing status controller rules.

Do not create a second status-effect implementation in another system.

UI indicators should observe status state; they should not become the source of truth.

---

# 19. WAVE SYSTEM

Primary system:

`Scripts/Manager/WaveManager.cs`

Wave flow:

- InterWave
- ActiveWave
- InterWave
- repeat

The current design uses:

- wave progression;
- tier progression;
- difficulty scaling;
- spawn scaling;
- reward scaling;
- victory/defeat handling.

Known design target:

- wave duration: 30 seconds
- inter-wave duration: 10 seconds
- maximum wave: 350

After the maximum wave:

- the tier progresses;
- wave numbering resets according to the current progression design;
- difficulty continues through tier progression.

Do not modify formulas without documenting the reason and expected progression impact.

---

# 20. ULTIMATE SYSTEM

Primary architecture:

- `UltimateManager`
- `UltimateFactory`
- individual ultimate handlers.

Current ultimate families include:

- Void
- Tank
- Root
- Bomb
- Fountain
- Cloud
- Lightning
- Shockwave

Each ultimate may define:

- cooldown;
- activation chance;
- active duration;
- damage;
- element;
- crowd control;
- special effects.

New ultimates should follow the existing handler architecture.

Do not create one giant `UltimateManager` switch containing all gameplay logic.

---

# 21. ECONOMY

Main currencies/resources:

- Gold
- Gem
- Meat
- EXP

Economy mutation should be centralized.

Do not directly mutate currency values from UI.

For example, avoid:

```csharp
playerGold += amount;
```

inside UI code.

Prefer the authoritative economy service/manager.

Every currency mutation must consider:

- validation;
- balance;
- save state;
- relevant events;
- UI refresh.

---

# 22. DAILY REWARD

Daily Reward is a **7-reward sequence within one day**, not merely a conventional 7-day login streak.

Current intended behavior:

- all 7 rewards can be claimed on the same day;
- claims are sequential;
- a 5-minute cooldown exists between claims;
- cooldown persistence uses `DateTime.UtcNow`;
- after reward 7 is claimed, the system enters `Completed Today`;
- buttons remain disabled until the next daily reset.

Reward concepts:

1. Gold
2. Gem
3. Meat
4. Free Card Roll item
5. EXP
6. Ultimate Stone
7. Skin Shard

Important reward scaling rules previously established:

- Gold uses `HighestGoldEarned` from passed tiers, minimum 100,000.
- Meat uses `HighestMeatEarned` from passed tiers, minimum 1,000.
- EXP uses half of `HighestExpEarned` from passed tiers, minimum 3,000.
- Free Card Roll must be added to Inventory as an accumulatable item.
- Skin Shard contributes toward the permanent skin exchange target.

## Persistence requirement

The current reward index, claim timestamps, completion state, and required reset state must survive:

- scene changes;
- application close;
- application restart.

Never calculate claim eligibility solely from UI state.

---

# 23. IDLE REWARD

Idle rewards calculate offline progression.

The system may accumulate:

- Gold
- Meat
- EXP

Offline calculations must use persisted timestamps and authoritative progression data.

Do not rely on the scene remaining alive to preserve idle state.

---

# 24. SCENE AND PERSISTENCE ARCHITECTURE

Important scenes include:

- `Bootstrap.unity`
- `MainMenu.unity`
- `MainGame.unity`
- `CardCollection.unity`
- `Inventory.unity`

Persistent services/managers must not accidentally duplicate when changing scenes.

## UI persistence rule

A UI component that exists in multiple scenes must not assume that it is globally unique unless the architecture explicitly guarantees it.

Example:

If `TooltipUI` exists in both Main Menu and Inventory:

- do not blindly mark every copy `DontDestroyOnLoad`;
- do not destroy the new scene's instance because an old instance still exists;
- decide whether TooltipUI is:
  - scene-local, or
  - a single persistent service/view.

The architecture must have one clear ownership model.

---

# 25. UI ARCHITECTURE

UI is a presentation layer.

UI should:

- display state;
- send user intent;
- subscribe to domain events;
- request operations from services.

UI should not own:

- save logic;
- item definitions;
- currency mutation;
- equipment state;
- card progression;
- combat formulas.

## Tooltips

Tooltip positioning must account for:

- canvas size;
- tooltip size;
- mouse/screen position;
- offset;
- screen boundaries.

Do not hardcode offsets that only work at one resolution.

When a tooltip is required across scenes, decide persistence ownership explicitly.

---

# 26. ITEM DATA AND MATERIALS

Items are divided conceptually into:

- Consumables
- Materials
- Equipment
- Gems
- Tickets / special items

Material naming and item definitions should remain stable.

Do not change IDs merely to make names prettier.

If a material needs a new display name:

- preserve the `Id`;
- change the display `Name`;
- update localization/data as required.

---

# 27. CRAFTING

Crafting is a domain system, not a UI timer.

Relevant systems include:

- `CraftService`
- `CraftJob`
- `CraftContextBuilder`
- `CraftCompletionService`
- `CraftModifiers`
- `RecipeData`

Craft queue state must be persisted.

When changing crafting:

- preserve queued jobs;
- define behavior for completed jobs after restart;
- avoid duplicating completion logic between UI and service.

---

# 28. DESIGN OF NEW FEATURES

When adding a new feature, use this workflow.

## Step 1 — Identify the domain

Determine whether the feature belongs to:

- Player
- Enemy
- Combat
- Card
- Equipment
- Inventory
- Item
- Crafting
- Economy
- Reward
- Daily
- Idle
- Wave
- Ultimate
- UI
- Save/Persistence
- Core

## Step 2 — Find the owner

Ask:

> Which existing service should own this state and behavior?

Do not immediately create a new manager.

## Step 3 — Define data

If the feature has balance/configuration:

- add it to the correct JSON;
- create/update the data class;
- document the schema.

## Step 4 — Implement domain behavior

Put rules in the domain service/system.

## Step 5 — Connect persistence

If the state survives restart:

- add it to SaveData;
- add load logic;
- add save logic;
- add migration/default behavior where needed.

## Step 6 — Connect events

Only publish events that represent meaningful domain changes.

## Step 7 — Connect UI

UI consumes the domain state/events.

## Step 8 — Test edge cases

At minimum test:

- fresh save;
- existing save;
- null/empty data;
- maximum capacity;
- minimum value;
- invalid operation;
- repeated operation;
- scene transition;
- application restart;
- save/load;
- duplicate events.

## Step 9 — Update design documentation

Document the feature before considering it complete.

---

# 29. REFACTORING RULES

When refactoring:

1. Preserve behavior first.
2. Do not mix unrelated feature changes into the same refactor.
3. Identify duplicated responsibilities.
4. Move logic toward the correct domain owner.
5. Keep public APIs stable where practical.
6. If an API must change, update all consumers in the same change.
7. Remove dead code only when you can verify it is unused.
8. Do not leave compatibility wrappers indefinitely without a reason.
9. Test save/load after refactoring persistent systems.

A refactor is not successful merely because the code is shorter.

It is successful when:

- responsibilities are clearer;
- dependencies are reduced;
- behavior is preserved;
- future changes become safer.

---

# 30. TESTING STRATEGY

## EditMode

Use EditMode tests for pure logic:

- damage formulas;
- modifier calculations;
- economy calculations;
- wave formulas;
- roll cost calculations;
- upgrade curves;
- reward calculations;
- inventory calculations;
- equipment stat aggregation;
- socket rules;
- save migration logic where practical.

## PlayMode

Use PlayMode tests for:

- scene/system interaction;
- projectile collision;
- enemy spawning;
- player/enemy interaction;
- UI-domain integration where necessary;
- persistent service initialization.

## Regression testing

When fixing a bug, add a regression test if the behavior is deterministic and testable.

Do not fix a bug only at the visual/UI level when the underlying domain state is wrong.

---

# 31. PERFORMANCE

Do not optimize blindly.

Use profiling to justify significant performance work.

However, some patterns are already established:

- projectile pooling;
- damage popup pooling;
- enemy health bar pooling;
- reuse `ContactFilter2D` where practical;
- avoid per-frame allocations;
- avoid unnecessary `Instantiate/Destroy`;
- use physics layers correctly;
- use timer-driven spawning rather than per-frame spawning.

## Important correction to YAGNI

Object pooling is already part of the project for high-frequency objects.

Do not remove established pooling merely because profiling is not currently showing a problem.

Likewise, do not add five new pooling systems without evidence.

---

# 32. PHYSICS2D

Use Physics2D consistently.

Typical attack range query:

```csharp
Collider2D[] enemies =
    Physics2D.OverlapCircleAll(
        transform.position,
        attackRange,
        enemyLayerMask);
```

For high-frequency queries, prefer allocation-conscious APIs where appropriate.

Knockback:

```csharp
Vector2 direction =
    (enemy.position - transform.position).normalized;

enemyRb.AddForce(
    direction * knockbackForce,
    ForceMode2D.Impulse);
```

Enemy separation should use the existing physics-based approach rather than introducing a separate spatial system unless profiling/design requires it.

---

# 33. CODE STYLE

Use project conventions:

- `PascalCase` for public members, methods, properties, types.
- `_camelCase` for private fields.
- `[SerializeField]` for Inspector-exposed private fields.
- One primary MonoBehaviour per file.
- Filename matches the primary class.
- Runtime namespaces begin with `IdleDefenseSurvival`.
- Editor namespaces use `IdleDefenseSurvival.Editor`.
- Use `[Tooltip]` on serialized Inspector fields where useful.

Do not make fields public merely to make Inspector assignment easier.

Prefer:

```csharp
[SerializeField]
private SomeType _someReference;
```

---

# 34. SINGLETONS AND SERVICES

The project contains existing singleton/service patterns.

Do not introduce a new singleton automatically.

Before adding one:

1. check whether the system already has a service;
2. check `ServiceLocator`;
3. check whether the state should actually be persistent;
4. determine whether dependency injection or an existing manager is sufficient.

A singleton is acceptable when the domain genuinely represents one global runtime authority.

A singleton is not a substitute for architecture.

---

# 35. SERVICE LOCATOR

Existing service concepts include:

- Save service;
- Economy service;
- Audio service;
- Ads service;
- Analytics service;
- game manager access.

When adding a new globally consumed service, first evaluate whether it belongs in the existing service architecture.

Do not register duplicate services under different access paths.

---

# 36. COMMON BUG CLASSES TO GUARD AGAINST

## Duplicate event execution

One action should not produce the same logical effect twice because multiple overlapping events fire.

## UI state mistaken for domain state

A disabled button is not proof that an action is invalid.

The domain must validate the action.

## Scene lifetime bugs

Do not assume objects survive scene transitions unless explicitly persistent.

## Save/load desynchronization

Never assume in-memory state and SaveData are automatically synchronized.

## ID/name coupling

Never use display names as persistent identifiers.

## Duplicate data sources

Do not have:

- one formula in JSON;
- another formula in a manager;
- another formula in UI.

## Stale cached stats

When modifiers change, refresh the authoritative stat pipeline.

## Partial operations

An operation must be atomic from the player's perspective.

Example:

If an item is consumed but its effect fails, the item must not silently disappear unless that failure is explicitly designed.

---

# 37. KEY FILES

| Domain | Main files |
|---|---|
| Player | `Scripts/Player/Player.cs`, `PlayerStats.cs`, `StatLoader.cs` |
| Player stats | `Scripts/Manager/PlayerStatsManager.cs`, `Scripts/Modifier/` |
| Attributes | `dataAttribute.json`, attribute services/modifier pipeline |
| Enemy | `Scripts/Enemy/EnemyAi.cs`, `EnemySpawner.cs` |
| Status | `Scripts/Enemy/EnemyStatusEffectController.cs`, `Scripts/Enemy/StatusEffects/` |
| Projectile | `Scripts/Player/Projectile.cs`, `Scripts/Manager/ProjectilePool.cs` |
| Wave | `Scripts/Manager/WaveManager.cs`, `dataWave.json` |
| Cards | `Scripts/Card/`, `dataCard.json` |
| Equipment | `Scripts/Equipment/`, `dataItems.json` |
| Inventory | `Scripts/Inventory/`, `Scripts/Items/`, `dataItems.json` |
| Gems | `Scripts/Items/GemService.cs`, `dataConfigSocket.json` |
| Crafting | `Scripts/Items/CraftService.cs`, `CraftJob.cs`, recipe data |
| Economy | `Scripts/Economy/EconomyManager.cs` |
| Save | `Scripts/Manager/SaveManager.cs`, `Scripts/Data/SaveData.cs` |
| Daily | `Scripts/Daily/` |
| Idle | `Scripts/IdleReward/` |
| Ultimates | `Scripts/Ultimate/`, `dataUltimate.json` |
| UI | `Scripts/UI/`, `Scripts/Controller/` |
| Core | `Scripts/Core/` |

---

# 38. CURRENT FOLDER STRUCTURE

```text
Assets/
├── Art/
│   ├── Enemy/
│   ├── Player/
│   └── UI/
├── Prefabs/
├── Resources/
│   ├── Data/
│   │   └── Design/
│   └── Art/
├── Scenes/
├── Scripts/
│   ├── Camera/
│   ├── Card/
│   ├── Controller/
│   ├── Core/
│   ├── Daily/
│   ├── Data/
│   ├── Economy/
│   ├── Enemy/
│   ├── Equipment/
│   ├── IdleReward/
│   ├── Inventory/
│   ├── Item/
│   ├── Items/
│   ├── Manager/
│   ├── Modifier/
│   ├── Player/
│   ├── Reward/
│   ├── UI/
│   ├── Ultimate/
│   └── VisualScripting/
├── Settings/
└── InputSystem_Actions.inputactions
```

The actual repository structure is authoritative. Update this section if folders are intentionally reorganized.

---

# 39. NEW ULTIMATE

Workflow:

1. Create a dedicated handler following the existing ultimate handler architecture.
2. Add its definition to `dataUltimate.json`.
3. Register it through the existing ultimate factory/manager mechanism.
4. Do not put all special behavior in `UltimateManager`.
5. Add tests for deterministic formulas/rules.
6. Update the ultimate design document.

---

# 40. NEW CARD

Workflow:

1. Add card definition to `dataCard.json`.
2. Determine whether it is:
   - stat modifier;
   - percentage modifier;
   - special effect.
3. For special effects, implement the effect in the correct domain.
4. Register/parse the effect through `CardModifierService` where appropriate.
5. Test level scaling.
6. Test equipping/unequipping.
7. Test save/load.
8. Update card design documentation.

---

# 41. NEW ENEMY

Workflow:

1. Add enemy definition to `dataEnemy.json`.
2. Reuse the standard enemy prefab/architecture where possible.
3. Add unique behavior only when the design requires it.
4. Ensure rewards, role, element, stats, and spawn weight are data-driven.
5. Test spawning and wave scaling.
6. Update enemy design documentation.

---

# 42. NEW EQUIPMENT

Workflow:

1. Add definition to `dataItems.json`.
2. Assign a stable `ItemId`.
3. Define equipment type/slot.
4. Define base stats.
5. Define rarity/affixes if applicable.
6. Define sockets according to socket configuration.
7. Ensure persistence uses `InstanceId`.
8. Test equip/unequip/swap.
9. Test stat aggregation.
10. Test save/load.
11. Update equipment design documentation.

---

# 43. NEW STATUS EFFECT

Workflow:

1. Determine whether the existing status system already supports the behavior.
2. If not, implement the smallest extension.
3. Follow `IStatusEffect` and existing concrete status conventions.
4. Register through `EnemyStatusEffectController`.
5. Define:
   - stacking;
   - duration;
   - source;
   - refresh behavior;
   - removal behavior.
6. Add regression tests.
7. Update status-effect design documentation.

---

# 44. NEW CONSUMABLE

Workflow:

1. Add item definition to `dataItems.json`.
2. Give it a stable `ItemId`.
3. Define stack size and use rules.
4. Implement gameplay behavior in the item/domain system.
5. Let `ItemClickManager` / UI invoke the domain operation.
6. Remove quantity only after successful validation/application.
7. Trigger the correct inventory/save events.
8. Test repeated use and insufficient quantity.
9. Update item documentation.

---

# 45. DEFINITION OF DONE

A feature is **not done** when the code compiles.

A feature is done when:

- [ ] domain ownership is clear;
- [ ] existing architecture was reused where appropriate;
- [ ] no duplicated source of truth was introduced;
- [ ] data is in the correct JSON when appropriate;
- [ ] save/load is handled if persistent;
- [ ] scene transitions are safe;
- [ ] events are correct and non-duplicated;
- [ ] UI does not own domain logic;
- [ ] edge cases are handled;
- [ ] relevant tests exist;
- [ ] design documentation is updated;
- [ ] obsolete documentation is corrected;
- [ ] no debug-only workaround remains;
- [ ] no unexplained hardcoded balance value was introduced.

---

# 46. AGENT CHECKLIST BEFORE MODIFYING CODE

Before editing:

```text
[ ] What system owns this behavior?
[ ] Does an existing service already solve it?
[ ] Which JSON data controls it?
[ ] Which design document describes it?
[ ] Does this affect persistent save data?
[ ] Does this affect InstanceId/ItemId?
[ ] Does this affect modifier/stat calculation?
[ ] Does this affect events?
[ ] Does this affect scene lifetime?
[ ] What existing consumers depend on this API?
```

After editing:

```text
[ ] Compile errors checked
[ ] Existing consumers checked
[ ] Save/load checked if applicable
[ ] Duplicate events checked
[ ] Edge cases checked
[ ] Regression test considered/added
[ ] Design documentation updated
[ ] CLAUDE.md updated if architecture/rules changed
```

---

# 47. FINAL PRINCIPLE

The goal is not to produce the most code.

The goal is to build a game that can continue growing without becoming a collection of:

- duplicated managers;
- hidden state;
- hardcoded balance;
- fragile save data;
- UI-driven business logic;
- inconsistent identifiers;
- stale documentation;
- event chains that execute twice;
- systems that do not know who owns their state.

When uncertain:

> **Find the existing source of truth.**
>
> **Put the rule in the correct domain.**
>
> **Keep data separate from behavior.**
>
> **Keep UI separate from domain logic.**
>
> **Preserve persistent data.**
>
> **Test the edge case.**
>
> **Document the decision.**
