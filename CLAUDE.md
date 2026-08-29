# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) and other coding agents when working with this repository.

---

# 1. PROJECT MANDATE - Build Manual Unity project to check compilation

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
| `dataConsumables.json` | consumables (potions etc.) |
| `dataAttribute.json` | CON/STR/INT/DEX attribute bonuses |
| `dataAttributeMainValuePerLevel.json` | Main-attribute value per level curve |
| `dataSOTValuePerLevel.json` | Secondary-attribute value per level curve |
| `dataConfigSocket.json` | Socket/gem rules |
| `dataConfigCrafting.json` | Crafting station config |
| `dataBaseEquipment.json` | Base equipment templates (Crafting/Equipment/) |
| `dataRecipeHat.json` ... `dataRecipeShoes.json` | Per-slot crafting recipes |
| `dataAffixes.json` | Affix (prefix/suffix) definitions for equipment rolls |
| `dataSets.json` | Equipment set bonus definitions |
| `dataBelt.json` ... `dataShoes.json` | Per-slot equipment definitions |
| `dataGems.json` | Gem definitions, base stats, upgrade curve |
| `dataHerbs.json` | Herb definitions (alchemical ingredient) |
| `dataMinerals.json` | Mineral material definitions |
| `dataOtherMaterials.json` | Other crafting materials (logs, glue, etc.) |
| `dataOtherItems.json` | Miscellaneous items (tickets, special items) |

Sibling design-doc folder (`Assets/Resources/Data/Design/`) is the authoritative
catalog of these JSONs — see §53 Design Documentation Index.

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

The intended equipment model uses these slot identities (verified from `EquipmentTypeExtensions.GetDisplayName`):

| Index | Slot | Type enum value |
|---|---|---|
| 0 | Hat | `EquipmentType.Hat` |
| 1 | Gloves | `EquipmentType.Gloves` |
| 2 | Cape | `EquipmentType.Cape` |
| 3 | Armor | `EquipmentType.Armor` |
| 4 | Belt | `EquipmentType.Belt` |
| 5 | Pants | `EquipmentType.Pants` |
| 6 | Pendant | `EquipmentType.Pendant` |
| 7 | Ring | `EquipmentType.Ring` |
| 8 | Earring | `EquipmentType.Earring` |
| 9 | Bracelet | `EquipmentType.Bracelet` |
| 10 | Shoes | `EquipmentType.Shoes` |

Total: **11 slots**. `GetIndex()` returns `(int)type - 1` (zero-based array access).

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

Current rarity model (`dataCard.json`, verified) — **six** tiers:

- Common
- Rare
- Epic
- Legendary
- Mythic
- **Divine** (rarest; multiplier `0.006` — extreme outlier tier)

Weight/multiplier values per rarity (verified):

| Rarity | Multiplier |
|---|---|
| Common | `1000.0` |
| Rare | `300.0` |
| Epic | `80.0` |
| Legendary | `15.0` |
| Mythic | `1.0` |
| Divine | `0.006` |

Pity thresholds (verified in `dataCard.json`):

| Rarity | Pity count |
|---|---|
| Epic | 51 |
| Legendary | 153 |
| Mythic | 505 |

Divine has no pity threshold — its base multiplier already produces very low pull rates.

## 14.2 Card leveling

Duplicate cards increase card level.

Current progression (`CardUpgradeService.cs`, verified):

```
Lv1→Lv2 : 2
Lv2→Lv3 : 4
Lv3→Lv4 : 7
Lv4→Lv5 : 11
Lv5→Lv6 : 19
Lv6→Lv7 : 31
Lv7→Lv8 : 47
Lv8→Lv9 : 69
Lv9→Lv10: 99
```

Cumulative duplicates required through level 10:

`289`

Do not change this progression without updating the relevant design/balance document.

## 14.3 Card slots

Card equipment has a defined maximum slot count.

Verified values (`Constantku.cs`):

- `CARD_START_SLOT = 1`
- `CARD_MAX_SLOT = 19`

`CARD_SLOT_EXPANSION_COSTS[]` is a cost curve array, length-gated in code. Do not invent new slot costs — add a row to the array.

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

Verified (`Constantku.cs`, `CardRollService.cs`):

- 1x = 20 gems (`ROLL1X_GEM_COST`)
- 10x = 190 gems (`ROLL10X_GEM_COST`)
- 100x = 1800 gems (`ROLL100X_GEM_COST`)

**Bundle calculation** (`CalculateRollGemCost(int amount)`): integer-divides amount into hundreds/tens/singles:

```csharp
hundreds = amount / 100
tens     = (amount % 100) / 10
singles  = amount % 10
total    = hundreds * 1800 + tens * 190 + singles * 20
```

This is **not** `amount * 20`. Preserves the bundled-discount tier behavior.

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

Verified values (`Constantku.cs`, `WaveManager.cs`):

- `MAX_WAVE_PER_TIER = 350`
- `CurrentWave` clamped to `[1, _maxWave]` — `WaveManager.cs:79`
- Inter-wave duration and active-wave duration read from `_interWaveDuration` / `_waveDuration` fields, scaled by `ProgressionSpeed`
- Difficulty scaling uses `Utilityku.WaveMultiplier(DecayCount, CurrentWave, _maxWave)` — see `Utilityku.cs` for the formula; do not duplicate the curve anywhere else
- Wave progress fraction `GetWaveProgressMultiplier()` = `Clamp01((CurrentWave - 1) / (_maxWave - 1))` — used by reward/difficulty interpolation

After wave `_maxWave` (350):

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

Current ultimate families include (8) — registered via `UltimateFactory.RegisterHandler(...)` from `UltimateManager.Awake`:

- Void
- Tank
- Root
- Bomb
- Fountain
- Cloud
- Lightning
- Shockwave

`UltimateFactory` itself is a static registry of `(string id → IUltimateHandler)` plus an active-count map. Handlers live under `Scripts/Ultimate/` and implement `IUltimateHandler` (interface file). When adding a handler, also append the `ultimateId` to `dataUltimate.json` and any spawn-trigger condition in `UltimateManager`.

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

Verified constants (`Scripts/Utilities/Constantku.cs`):

- `REWARD_COUNT = 7`
- `COOLDOWN_MINUTES = 5`
- `DAILY_GOLD_REWARD = 10_000`     ← doc lama menulis 100.000; angka sebenarnya **10.000**
- `DAILY_MEAT_REWARD = 500`         ← doc lama menulis 1.000; angka sebenarnya **500**
- `DAILY_EXP_REWARD = 2_500`        ← doc lama menulis 3.000; angka sebenarnya **2.500**
- `DATE_FORMAT = "yyyy-MM-dd"` (string compare for daily reset)

Behavior (`Scripts/Daily/DailyRewardService.cs`):

- All 7 rewards claimable in one day, sequential.
- 5-minute cooldown between claims (`utcNow.AddMinutes(COOLDOWN_MINUTES)` stored in `nextUnlockUtcTicks`).
- Daily reset via `lastResetDate` string compare on every `EnsureReset(utcNow)`.
- After reward 7: `completedToday = true`, all buttons disabled until reset.
- VIP: `SaveManager.Instance.IsDailyEnabled()` forces `Waiting → Claimable` in `GetState`.

Reward contents (`Scripts/Daily/DailyRewardData.cs`, `DailyRewardProvider.GetReward(index)`):

| # | Type | Source | Amount rule |
|---|---|---|---|
| 0 | Gold | `EconomyManager.AddCurrency(Gold, amount, "Daily reward")` | `Math.Max(DAILY_GOLD_REWARD, SaveManager.GetHighestGoldEarned())` |
| 1 | Gem | `EconomyManager.AddCurrency(Gem, 11, …)` | **hardcoded `11`** |
| 2 | Meat | `EconomyManager.AddCurrency(Meat, amount, …)` | `Math.Max(DAILY_MEAT_REWARD, SaveManager.GetHighestMeatEarned())` |
| 3 | Item | `InventoryManager.AddItem("CardRoll", 1)` | accumulatable free-roll ticket |
| 4 | EXP | `AccountManager.AddExp(amount, …)` | `Math.Max(DAILY_EXP_REWARD, HighestExpEarned / 2 * tier)` |
| 5 | Item | `InventoryManager.AddItem("UltimateStone", 3)` then roll N variants | **3 random UltimateStones** |
| 6 | Item | `InventoryManager.AddItem("SkinShard", 1)` | permanent skin exchange progress |

UltimateStone reward rolls from 8 variants (`DailyRewardService.cs:15-25`):

```
UltimateStone_None
UltimateStone_Metal
UltimateStone_Wood
UltimateStone_Fire
UltimateStone_Water
UltimateStone_Earth
UltimateStone_Lightning
UltimateStone_Wind
```

Each `count` pick rolls independently: `UnityEngine.Random.Range(0, variants.Length)`. If non-`None` is desired, gate the reward provider or the inventory accept rule — current code has **no** filter for `_None`, so the roll can yield a no-op token.

### Persistence requirement

`DailyRewardSaveData` (`Scripts/Daily/DailyRewardSaveData.cs`):

- `currentRewardIndex` (0..7)
- `nextUnlockUtcTicks` (`DateTime.UtcNow.Ticks` for next claim)
- `completedToday` (bool)
- `lastResetDate` (`"yyyy-MM-dd"`)
- `claimedToday` (counter, mostly informational)

Must survive scene changes / app close / app restart. Eligibility is never derived from UI state — `DailyRewardService.GetState(utcNow)` is the source of truth.

---

# 23. IDLE REWARD

Idle rewards calculate offline progression.

## 23.1 Verified scope

Idle reward currently grants **only Gold + Meat**. EXP is not part of the offline calculation (see `IdleRewardManager` public surface: `GoldReward`, `MeatReward`, `CanClaim`, `Progress` — no EXP property).

## 23.2 Owner files

- `Scripts/IdleReward/IdleRewardManager.cs` (singleton MonoBehaviour, DontDestroyOnLoad)
- `Scripts/IdleReward/IdleRewardData.cs` (persisted via `SaveManager.GetIdleRewardData()`)
- `Scripts/IdleReward/IdleRewardUI.cs` (display only)

## 23.3 Persistence (`IdleRewardData`)

```csharp
public long lastClaimUtcTicks = DateTime.UtcNow.Ticks;  // UTC ticks
public int  maxDurationSeconds = 4 * 3600;              // 4h cap on offline accumulation
public int  minimumClaimSeconds = 600;                  // 10 min before claim available
public float rewardMultiplier = 1f;
```

`GetAccumulatedSeconds()` returns `min((UtcNow - lastClaim).TotalSeconds, maxDurationSeconds)`. The 4h cap means a player offline 24h still gets only 4h worth.

## 23.4 Gold formula (verified `IdleRewardManager.CalculateGoldReward`)

```text
totalWaveProgress = (highestTier - 1) * MAX_WAVE_PER_TIER + highestWaveInTier
waveMultiplier   = 1.0 + totalWaveProgress / MAX_WAVE_PER_TIER
tierMultiplier   = 1.35^(highestTier - 1)
goldPerMinute    = 15.0 * tierMultiplier * waveMultiplier
minutes          = GetAccumulatedSeconds() / 60
gold             = round(goldPerMinute * minutes * rewardMultiplier)
```

`highestTier` and `highestWaveInTier` come from `SaveManager.GetHighestUnlockedTier()` + `SaveManager.GetHighestWave(tier)`. These are the player's record, not current state.

## 23.5 Meat formula

`MeatReward = roundToInt(GoldReward / 30)`. No separate scaling.

## 23.6 Claim semantics

- `IsClaimAvailable()` ⇒ `GetAccumulatedSeconds() >= minimumClaimSeconds` (10 min).
- `Progress` UI bar ⇒ `GetAccumulatedSeconds() / minimumClaimSeconds`.
- `ResetCount()` stamps `lastClaimUtcTicks = UtcNow.Ticks` then `SaveManager.SaveAll()`. **It does not grant the reward** — grant happens at the UI/claim call site, not here. Do not assume `ResetCount` pays out.

## 23.7 VIP / multipliers

`rewardMultiplier` defaults to `1.0`. If VIP integration exists in `IdleRewardManager`, check the current file before assuming — older docs reference a VIP multiplier but the current field is a single scalar and no VIP write is visible in the read-first scan.

## 23.8 Persistence requirement

The data is owned by `SaveManager.GetIdleRewardData()`. Do not rely on the scene staying alive. The `IdleRewardManager` itself is DontDestroyOnLoad, but the data survives via `SaveData.idleReward.*` regardless of whether the manager instance is alive.

---

# 24. SCENE AND PERSISTENCE ARCHITECTURE

Important scenes include:

- `Bootstrap.unity`
- `MainMenu.unity`
- `Game.unity`
- `CardCollection.unity`
- `Inventory.unity`
- `Crafting.unity`

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

Verified against `Assets/Scripts/` and `Assets/Resources/Data/`. Paths are repo-relative.

| Domain | Main files |
|---|---|
| Player | `Scripts/Player/Player.cs`, `Scripts/Player/PlayerStats.cs`, `Scripts/Player/AuraCollider.cs`, `Scripts/Player/AttributeService.cs`; managers: `Scripts/Manager/PlayerStatsManager.cs`, `Scripts/Manager/BaseStatLoader.cs`, `Scripts/Manager/AttributeStatLoader.cs` |
| Attributes | `Assets/Resources/Data/Player/dataAttribute.json` + `dataAttributeMainValuePerLevel.json` + `dataSOTValuePerLevel.json`; pipeline = `Scripts/Manager/AttributeModifierManager.cs` → `Scripts/Modifier/ModifierCalculator.cs` |
| Enemy | `Scripts/Enemy/EnemyAi.cs`, `Scripts/Enemy/EnemySpawner.cs`, `Scripts/Enemy/EnemyData.cs`; stats aggregation: `Scripts/Manager/EnemyStatisticsManager.cs` |
| Status | `Scripts/Enemy/EnemyStatusEffectController.cs`, `Scripts/Enemy/StatusEffects/IStatusEffect.cs`, `BaseStatusEffect.cs`, `ConcreteStatusEffects.cs` |
| Projectile | `Scripts/Player/Projectile.cs`, `Scripts/Manager/ProjectilePool.cs` |
| Wave | `Scripts/Manager/WaveManager.cs`, `Assets/Resources/Data/dataWave.json` |
| Cards | `Scripts/Card/CardManager.cs` (UI façade), `Scripts/Manager/CardManager.cs`, services in `Scripts/Card/`: `CardDatabase`, `CardInventory`, `CardEquipmentService`, `CardRollService`, `CardUpgradeService`, `CardModifierService`, `VirtualCardInventorySnapshot` |
| Equipment | One entry-point: `Scripts/Equipment/IEquipmentService.cs` + `EquipmentService.cs`. Sub-services live in `Scripts/Equipment/`: `EquipmentSlotService`, `EquipmentPersistenceService`, `EquipmentDurabilityService`, `EquipmentAutoEquipService`, `EquipmentComparisonService`, `EquipmentComparer`, `EquipmentEffectService`, `EquipmentModifierService`, `EquipmentSetBonusService`, `EquipmentEventDispatcher`, `EquipmentVisualService`, `EquipmentStatCalculator`, `EquipmentAttributeData`, `AttributeWeightsConfig`, `RarityMechanicConfig`, `SlotIdentityService`, `EquipmentType` |
| Inventory | `Scripts/Inventory/InventoryService.cs` + `IInventoryService`, `InventoryManager.cs`, `InventoryItem.cs`, `InventoryItemExtensions.cs`; category/state enums: `Scripts/Item/ItemCategory.cs`, `Scripts/Item/ItemState.cs` |
| Items (gem/repair/drop/random) | `Scripts/Items/`: `DropTable.cs`, `AutoRepairService.cs`, `DurabilityService.cs`, `DurabilityColorTable.cs`, `RepairService.cs`, `RepairTransactionService.cs`, `IRepairCostProvider.cs`, `GemFactory.cs`, `GemExperienceService.cs`, `GemSocketService.cs`, `GemUpgradeService.cs`, `SocketValidationService.cs`, `SpecialEffectType.cs`, `Random/IRandomProvider.cs`, `Random/SeedRandomProvider.cs`, `Random/UnityRandomProvider.cs` |
| Gems | `Scripts/Items/GemFactory.cs` + `GemExperienceService.cs` + `GemSocketService.cs` + `GemUpgradeService.cs`; data: `Assets/Resources/Data/Gems/dataGems.json`, `dataConfigSocket.json` |
| Crafting (own domain) | `Scripts/Crafting/CraftingManager.cs` is the entry point. Pipeline files in `Scripts/Crafting/`: `CraftRollService.cs`, `CraftValidator.cs`, `CraftRecipeValidationRunner.cs`, `CraftCostResolver.cs`, `CraftTransactionService.cs`, `CraftContextBuilder.cs`, `CraftPipeline.cs`, `CraftResultValidator.cs`, `CraftRewardBuilder.cs`, `CraftRewardService.cs`, `CraftCompletionService.cs`, `CraftPersistenceService.cs`, `CraftQueueService.cs`, `CraftModifiers.cs`, `CraftRecipeData.cs`, `CraftRecipeRepository.cs`, `CraftingConfig.cs`, `CraftData.cs`, `CraftJob.cs`, `AttributeRollService.cs`. UI: `Scripts/Crafting/JobEntryUI.cs`, `Scripts/Controller/CraftingController.cs`, `CraftingUIController.cs`, `CraftingRecipeEntry.cs`. Data: `Assets/Resources/Data/Crafting/dataConfigCrafting.json`, `Crafting/Equipment/dataBaseEquipment.json`, per-slot `Crafting/Equipment/dataRecipeHat.json` … `dataRecipeShoes.json`, `Crafting/Potion/dataRecipeHealthPotion.json`, `dataRecipeManaPotion.json`. **There is no `Scripts/Items/CraftService.cs`** — the file map in old CLAUDE.md is wrong. |
| Potion (consumable subtypes) | `Assets/Resources/Data/Items/Potion/dataHealthPotion.json`, `dataManaPotion.json`; consumed via `Scripts/UI/Game/ItemConsumableUI.cs` |
| Economy | `Scripts/Economy/EconomyManager.cs`, `Scripts/Economy/CurrencyData.cs`, `Scripts/Core/Interfaces/IEconomyService.cs` |
| Save | `Scripts/Manager/SaveManager.cs` (root), `Scripts/Data/SaveData.cs`, `Scripts/Save/EquipmentSerializer.cs`, `Scripts/Save/InventorySerializer.cs`; constants: `Scripts/Utilities/Constantku.cs` (`CURRENT_SAVE_VERSION = 3`) |
| Daily | `Scripts/Daily/DailyRewardService.cs` (logic), `DailyRewardManager.cs`, `DailyRewardSaveData.cs`, `DailyRewardSlot.cs`, `DailyRewardUI.cs` |
| Idle | `Scripts/IdleReward/IdleRewardManager.cs`, `IdleRewardUI.cs`, `IdleRewardData.cs` |
| Ultimates | `Scripts/Ultimate/UltimateManager.cs` (registration host), `UltimateFactory.cs` (static registry), `IUltimateHandler.cs` (interface). 8 handler ids registered from `UltimateManager.Awake`: Void, Tank, Root, Bomb, Fountain, Cloud, Lightning, Shockwave. Definitions: `Assets/Resources/Data/Player/dataUltimate.json`. **There is no `Scripts/Ultimate/<Name>Handler.cs` per-ultimate file** — handlers are wired through `UltimateFactory` and `dataUltimate.json`; treat the 8 names as handler keys, not filenames. |
| Modifier / effect registry | `Scripts/Modifier/ModifierCalculator.cs`, `Scripts/Modifiers/EffectRegistry.cs`, `Scripts/Manager/AttributeModifierManager.cs`, `Scripts/Player/AttributeService.cs` |
| Stats | `Scripts/Stats/SecondaryStatMode.cs` (secondary-stat computation mode) |
| Reward | `Scripts/Reward/RewardData.cs`, `RewardManager.cs`, `RewardPopup.cs`, `RewardSlot.cs`; UI: `Scripts/UI/.../RewardUI/*` |
| Drop bag (post-combat pickup) | `Scripts/Manager/DropBag.cs`, `Scripts/Manager/DropBagManager.cs`; hooked from enemy death; backed by `Scripts/Items/DropTable.cs` |
| Mission | `Scripts/Mission/MissionService.cs` (singleton, DontDestroyOnLoad), `MissionUI.cs`, `MissionSlot.cs`. Templates: `Assets/Resources/Data/Player/dataMission.json` |
| Account | `Scripts/Manager/AccountManager.cs` (wraps `SaveManager.GetAccountData`) |
| VIP | `Scripts/Data/VIPData.cs`; integrated via `DailyRewardService.IsDailyEnabled`, `GameSpeedController`, `CraftContextBuilder` |
| Analytics | `Scripts/Manager/AnalyticsManager.cs`, `Scripts/Core/Interfaces/IAnalyticsService.cs` |
| Audio | `Scripts/Manager/AudioManager.cs`, `Scripts/Core/Interfaces/IAudioService.cs` |
| Advertising | `Scripts/Manager/AdvertisingManager.cs`, `Scripts/Core/Interfaces/IAdsService.cs` |
| Game speed | `Scripts/Controller/GameSpeedController.cs` |
| UI | `Scripts/UI/` (per-domain panels), `Scripts/Controller/` (scene controllers: `MainMenuController`, `GameController`, `BootstrapController`, `SettingsController`, `VictoryController`, `InventoryController`, `CardCollectionController`, `CraftingController` + `CraftingUIController` + `CraftingRecipeEntry`, `GameSpeedController`) |
| Core / boot | `Scripts/Core/BootstrapInitializer.cs`, `CanvasRoot.cs`, `SceneCleanupHandler.cs`, `ServiceLocator.cs`, `Interfaces/*` |
| Utilities | `Scripts/Utilities/Constantku.cs`, `Utilityku.cs`, `Colorku.cs`, `ResourceCache.cs`, `Enumku.cs` |

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
│   ├── Crafting/
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
│   ├── Mission/
│   ├── Modifier/
│   ├── Modifiers/         ← plural; effect registry + Buff/EquipmentEffect types
│   ├── Player/
│   ├── Reward/
│   ├── Save/              ← EquipmentSerializer, InventorySerializer, CustomDataConverter
│   ├── Stats/             ← SecondaryStat, MainAttributeExtensions, SecondaryStatMode, mapping extensions
│   ├── UI/
│   ├── Ultimate/
│   └── Utilities/         ← Constantku, Utilityku, Colorku, ResourceCache, Enumku
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

1. Add relate definition to script `Data/Equipment`.
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

1. Add relate definition to script `dataConsumables.json`.
2. Give it a stable `ItemId`.
3. Define stack size and use rules.
4. Implement gameplay behavior in the item/domain system.
5. Let `ItemClickManager` / UI invoke the domain operation.
6. Remove quantity only after successful validation/application.
7. Trigger the correct inventory/save events.
8. Test repeated use and insufficient quantity.
9. Update item documentation.

---

# 45. MISSION SYSTEM

Owner: `Scripts/Mission/MissionService.cs` (singleton, `DontDestroyOnLoad`).

Templates load from `Resources/Data/Player/dataMission.json` (parsed via `JsonUtility.FromJson<MissionTemplateData>` in `Awake`). Slots and runtime state are persisted as part of the main `SaveData.missions` list — missions are not a separate domain save.

## 45.1 Event types (`MissionEventType`)

| Type | Trigger | `targetId` semantics |
|---|---|---|
| `EnemyKilled` | any non-boss kill | not matched |
| `BossKilled` | any boss kill | not matched |
| `SpecificEnemyKilled` | kill of a chosen non-boss enemy | runtime-picked from `DatabaseJSONCache.DatabaseEnemy` (`role != Role.BOSS`); stored on `MissionInstance.targetId` |
| `CurrencyEarned` | economy reward added | `template.targetId` (`Gold`/`Gem`/`Meat`) |
| `WaveCompleted` | wave clear | not matched — confirmed in `WaveManager.CompleteWave` |
| `Blacksmithing` | equipment crafted | runtime-picked from `Enum.GetValues(typeof(EquipmentType))` excluding `None`; stored on `MissionInstance.targetId` |

Routing: callers invoke `MissionService.Instance?.UpdateProgress(MissionEventType type, string targetId, long amount)`. The service matches each active mission whose template type + target match and increments `currentCount` up to `targetCount`. On completion it flips status to `Completed` and stamps `completedAt`.

## 45.2 Slot cap

`MissionService._maxMission` is sourced from `SaveData.account.maxMission` (default `1`, minimum `1`). Public mutation: `SetMaxMission(int)` — clamps ≥ 1, writes through `SaveManager.Instance.GetAccountData()` and calls `GenerateMissingMissions()` + `SaveMissions()`. Cap can grow but should never silently shrink inside `MissionUI.EnsurePool` — slot layout stays stable across cap changes.

## 45.3 Mission lifecycle

```
Active → Completed → Claimed          (cooldownUntil, default 30 min, template-overridable)
Active → Cancelled                    (cooldownUntil, default 15 min, template-overridable)
Claimed/Cancelled → new Active        (when cooldown expires, CheckCooldowns regenerates per slot)
```

Cooldowns are `DateTimeOffset` strings serialized via `"o"`. `MissionService.Update()` ticks every frame only to check expiry — keep that check cheap.

## 45.4 Reward flow

Claiming goes through `RewardManager.Instance.Show(rewardList, onClose)` when any reward > 0 (popup path); otherwise rewards are granted inline via `ServiceLocator.EconomyService.AddCurrency(...)` with reason `"MissionReward"`. Both paths call `NotifyClaimed` which saves and fires `OnMissionStatusChanged` + `OnMissionsChanged`.

UI may only call `Service.ClaimMission(instanceId)`. Do not give rewards directly from UI or `MissionSlot`.

## 45.5 UI

- `MissionUI` (in `IdleDefenseSurvival.UI` namespace) — pool grows up to `GetMaxMission()`, never shrinks. Subscribes to `OnMissionsChanged`, `OnMissionStatusChanged`, `OnMissionProgressChanged`. Icon resolved per event type via `EnemyResources.GetEnemySprite` or `ItemResources.GetItemSource`.
- `MissionSlot` — single row, refreshed every 1s while panel is open (countdown timer). Background color: `Completed = green`, `Claimed = gray`, `Cancelled = red`. Button color via `ButtonResources.GetColor("Green"|"Red"|"Grey")`.
- Enter animation uses DOTween (`SetLink(gameObject)`).

## 45.6 Adding a new mission event type

1. Add value to `MissionEventType` enum.
2. Update `MissionService.DoesEventMatchMission` switch — return `true` for unconditional types, `IsTargetMatch` for target-keyed types.
3. Add at least one template row to `dataMission.json` exercising the type.
4. Find the producer(s) and call `MissionService.Instance?.UpdateProgress(newType, targetId, amount)` at the right hook point.
5. If the icon is target-keyed, extend `MissionUI.GetMissionIcon` switch.
6. Verify: fresh save shows the template, save→load preserves instanceId/slotIndex/cooldownUntil, claim routes through `RewardManager`, cancel regenerates after cooldown.

---

# 46. VIP

Owner: `Scripts/Data/VIPData.cs` (plain serializable class).

```csharp
public class VipData {
    public bool daily;        // unlocks DailyReward claim-while-Waiting (see DailyRewardService.IsDailyEnabled)
    public bool maxSpeed;     // raises GameSpeedController max from 5.5x → 7.5x
    public bool autoCollect;  // reserved for Phase 2 item auto-collect delay
}
```

Stored under `SaveData.account.vip` (single `VipData` instance per save — no per-account VIP level/tier model yet).

## 46.1 Integration points (verified live)

| Consumer | Behavior |
|---|---|
| `DailyRewardService` (`IsDailyEnabled`) | When `Waiting`, VIP can force state to `Claimable` |
| `GameSpeedController` | `CheckVIP()` re-clamps `currentSpeed` to the new max on toggle |
| `CraftContextBuilder` | Reads VIP via context to influence modifier aggregation |
| `CraftRecipeData` | `JobTag.VIP = 8` reserved for VIP-gated recipes |
| `SaveManager.IsDailyEnabled` | Pass-through accessor to `_currentVip.daily` |

## 46.2 Extending VIP

1. Add field to `VipData` with default `false`.
2. Newtsoft round-trip via `SaveManager` handles a new bool field without migration; old saves load with `false`.
3. Add consumer call site under the right domain owner — never let UI flip VIP state directly. Toggle goes through `SaveManager` or an explicit VIP service.
4. If the perk gates a feature, surface it in `MainMenuController` (VIP button) as an explicit on/off.
5. Update §49.1 table.

Avoid numeric VIP levels until design requires progression (YAGNI). The current `bool` model is the source of truth.

---

# 47. EXTENSION MAP

Start by reading the listed owner file, then the matching §39–§44 workflow, then the §37 file map for sibling services.

| If you want to add… | Open first | Then read | Data file |
|---|---|---|---|
| a new ultimate | `Scripts/Ultimate/UltimateFactory.cs` | existing handler in `Scripts/Ultimate/` (e.g. `BombHandler`) | `Resources/Data/dataUltimate.json` |
| a new card | `Scripts/Card/CardRollService.cs` | `CardModifierService` + `CardUpgradeService` | `Resources/Data/dataCard.json` |
| a new enemy | `Scripts/Enemy/EnemyAi.cs` | `EnemySpawner.cs` (spawn weights), `EnemyStatusEffectController.cs` | `Resources/Data/dataEnemy.json` |
| a new equipment slot or piece | `Scripts/Equipment/IEquipmentService.cs` | `SlotIdentityService`, `AttributeWeightsConfig` | `Resources/Data/dataBaseEquipment.json` + per-slot JSON |
| a new affix or set | `Scripts/Items/EquipmentSetBonusService` (or wherever affix roll lives) | equipment persistence path | `Resources/Data/dataAffixes.json` / `dataSets.json` |
| a new status effect | `Scripts/Enemy/EnemyStatusEffectController.cs` | `Scripts/Enemy/StatusEffects/ConcreteStatusEffects/` | none — code-only |
| a new socket/gem rule | `Scripts/Items/GemSocketService.cs` + `SocketValidationService.cs` | `GemExperienceService`, `GemUpgradeService` | `Resources/Data/dataConfigSocket.json`, `dataGems.json` |
| a new crafting recipe | `Scripts/Items/CraftService.cs` | `CraftContextBuilder`, `CraftCompletionService` | `Resources/Data/Player/dataRecipe*.json` + `dataConfigCrafting.json` |
| a new consumable | `dataConsumables.json` consumer | `Scripts/Item/Items.cs` (`ItemClickManager`) | `Resources/Data/dataConsumables.json` |
| a new daily reward slot | `Scripts/Daily/DailyRewardService.cs` | `DailyRewardSaveData`, `DailyRewardUI` | `Resources/Data/Player/dataDailyReward.json` (verify path) |
| a new mission event | `Scripts/Mission/MissionService.cs` | `MissionUI.GetMissionIcon` switch | `Resources/Data/Player/dataMission.json` |
| a new VIP perk | `Scripts/Data/VIPData.cs` | `SaveManager.IsDailyEnabled`, `GameSpeedController` | none — bool flag |
| a new save domain | `Scripts/Data/SaveData.cs` | `Scripts/Save/EquipmentSerializer.cs` pattern, `SaveManager.OnSaveLoaded` | none — code-only, requires §52 version bump |
| a new scene | `Scripts/Core/SceneLoader.cs` + `BootstrapInitializer.cs` | §24 scene list | none — must update §24 |

## 47.2 New / emerging domains (not in §47 table)

These domains exist in the codebase but were not in the original extension table. Use the same read-first rule: open the listed owner, then sibling services, then JSON.

| If you want to add… | Open first | Then read | Data file |
|---|---|---|---|
| a crafting pipeline stage / new station | `Scripts/Crafting/CraftingManager.cs` | `CraftPipeline.cs`, `CraftRollService.cs`, `CraftValidator.cs`, `CraftTransactionService.cs`, `CraftRewardService.cs`, `CraftPersistenceService.cs`, `CraftContextBuilder.cs` | `Assets/Resources/Data/Crafting/dataConfigCrafting.json`, `Crafting/Equipment/dataRecipe*.json`, `Crafting/Potion/dataRecipe*.json` |
| a new potion / consumable subtype | `Scripts/UI/Game/ItemConsumableUI.cs` (consumer) | `Scripts/Inventory/InventoryService.cs`, `Scripts/Item/ItemCategory.cs` | `Assets/Resources/Data/Items/Potion/dataHealthPotion.json`, `dataManaPotion.json`, `dataConsumables.json` |
| a new drop-bag entry / drop rule | `Scripts/Manager/DropBagManager.cs` | `Scripts/Manager/DropBag.cs`, `Scripts/Items/DropTable.cs` | none — code + `Random/*` providers; tie to enemy death in `EnemyAi` |
| a new equipment sub-service (durability/auto-equip/comparison/effect) | `Scripts/Equipment/IEquipmentService.cs` | the matching `Equipment*Service.cs` (Durability / AutoEquip / Comparison / Effect / Modifier), `EquipmentStatCalculator.cs`, `EquipmentAttributeData.cs`, `RarityMechanicConfig.cs` | `Assets/Resources/Data/Equipment/dataAffixes.json`, `dataSets.json`, per-slot `dataHat.json`…`dataShoes.json`, `dataBaseEquipment.json` |
| a new rarity tier (e.g. above Mythic, like Divine) | `Scripts/Equipment/RarityMechanicConfig.cs` | `EquipmentEffect.cs`, `EffectRegistry.cs` (in `Scripts/Modifiers/`) | `Assets/Resources/Data/Equipment/dataAffixes.json`, `dataSets.json` |
| a new buff / equipment effect type | `Scripts/Modifiers/EffectRegistry.cs` | `Scripts/Modifiers/Buff.cs`, `Scripts/Modifiers/EquipmentEffect.cs`, `Scripts/Modifier/ModifierCalculator.cs` | none — register through `EffectRegistry`; route final value through `ModifierCalculator` |
| an attribute roll variant | `Scripts/Crafting/AttributeRollService.cs` | `Scripts/Equipment/AttributeWeightsConfig.cs`, `Scripts/Stats/MainAttributeExtensions.cs`, `Scripts/Stats/SecondaryStat.cs`, `Scripts/Stats/SecondaryStatMode.cs`, `Scripts/Stats/SecondaryStatMappingExtensions.cs` | `Assets/Resources/Data/Player/dataAttribute.json`, `dataAttributeMainValuePerLevel.json`, `dataSOTValuePerLevel.json` |
| account progression / profile field | `Scripts/Manager/AccountManager.cs` | `Scripts/Data/SaveData.cs` (account sub-section), `Scripts/Manager/SaveManager.cs` (`GetAccountData`) | none — code-only, requires §48 version bump if shape changes |
| a new analytics event | `Scripts/Manager/AnalyticsManager.cs` | `Scripts/Core/Interfaces/IAnalyticsService.cs`, `Scripts/Core/ServiceLocator.cs` | none — define event name + payload schema in `AnalyticsManager` |
| a new audio cue / BGM layer | `Scripts/Manager/AudioManager.cs` | `Scripts/Core/Interfaces/IAudioService.cs` | none — AudioClip assets, mixer groups, channel priority in `AudioManager` |
| an ad placement hook | `Scripts/Manager/AdvertisingManager.cs` | `Scripts/Core/Interfaces/IAdsService.cs` | none — provider-specific ad unit ids in `AdvertisingManager` |
| a new game-speed tier (e.g. VIP unlock) | `Scripts/Controller/GameSpeedController.cs` | `Scripts/Data/VIPData.cs`, `Scripts/Manager/SaveManager.cs` (`_currentVip.maxSpeed`) | none — step values + max in `GameSpeedController`, gated by VIP bool |
| a random-source policy (seeded vs Unity) | `Scripts/Items/Random/IRandomProvider.cs` | `UnityRandomProvider.cs`, `SeedRandomProvider.cs` | none — plug into services that take `IRandomProvider` |
| a new scene | `Scripts/Core/SceneLoader.cs` + `BootstrapInitializer.cs` | §24 scene list, `SceneCleanupHandler.cs` | none — must update §24, register load entry in `BootstrapInitializer` |
| a new global service | `Scripts/Core/ServiceLocator.cs` | §35 service list, the matching interface in `Scripts/Core/Interfaces/` | depends on service |
| inventory serialization variant | `Scripts/Save/InventorySerializer.cs` | `EquipmentSerializer.cs`, `CustomDataConverter.cs` (Newtonsoft contract) | none — if `SaveData.Items[]` shape changes, bump version (§47.1) |

## 47.1 Save-version bumps

Any non-additive `SaveData` shape change requires:

1. Bump `GameConstants.CURRENT_SAVE_VERSION` in `Scripts/Utilities/Constantku.cs`.
2. Add migration case in `SaveManager.LoadFromDisk` (or equivalent).
3. Append the version row to §52 of this document.
4. Test: load save at `version - 1` → upgrade → reload → confirm shape.

---

# 48. SAVE VERSION LOG

`GameConstants.CURRENT_SAVE_VERSION` is the authoritative version stamp written into every `SaveData`. Source of truth lives in `Scripts/Utilities/Constantku.cs`.

| Version | Date | Schema break | Migration |
|---|---|---|---|
| 3 | 2026-08 | flat `Items[]` save; category derived from `ItemId`; slot via `SlotIndex` | see `Scripts/Save/InventorySerializer.cs` and `Scripts/Save/EquipmentSerializer.cs`; old nested save shapes are normalized on load |
| 2 | prior | per-domain nested save sections | superseded by v3 flat layout |
| 1 | initial | first shipping save | superseded by v2 |

## 48.1 Reading the version

`SaveData.version` is read by `SaveManager` before deserializing the rest of the payload. If on-disk version is below `CURRENT_SAVE_VERSION`, `SaveManager` runs the appropriate migration path, then writes back at the current version.

If on-disk version is **above** `CURRENT_SAVE_VERSION`, reject the save (do not silently downgrade — data loss). Surface a user-facing recovery flow.

## 48.2 Adding a row

```text
| <N+1> | <YYYY-MM> | <one-line schema break description> | <migration path summary + key files> |
```

Update this table in the same commit as the version bump.

---

# 54. CLOSING HANDOFF

When a feature, refactor, or extension lands in this project, the canonical handoff sequence is:

1. §51 Extension Map → pick the owner file for the domain you touched.
2. §39–§44 workflow → follow the per-domain workflow (ultimate / card / enemy / equipment / status / consumable / mission / etc.).
3. §52 Save Version Log → if `SaveData` shape changed, bump `CURRENT_SAVE_VERSION` and append a row.
4. §53 Design Documentation Index → if you authored or rewrote a design doc, register it here.
5. Test against §45 Definition of Done and §46 Agent Checklist before considering the change complete.

---

# 55. KEY SCRIPTS FOR FUTURE EXPANSION

Domain → owner file → sibling services → JSON. Read owner first, always.

## 55.1 Persistence backbone

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Save/load | `Scripts/Manager/SaveManager.cs` | `Scripts/Data/SaveData.cs`, `Scripts/Save/EquipmentSerializer.cs`, `Scripts/Save/InventorySerializer.cs`, `Scripts/Save/CustomDataConverter.cs` | none — code-only; bump `Constantku.CURRENT_SAVE_VERSION` on shape change |
| Boot/ServiceLocator | `Scripts/Core/BootstrapInitializer.cs` | `Scripts/Core/ServiceLocator.cs`, `Scripts/Core/SceneLoader.cs`, `Scripts/Core/SceneCleanupHandler.cs`, `Scripts/Core/CanvasRoot.cs` | none |
| Account field | `Scripts/Manager/AccountManager.cs` | `SaveData.account.*` sub-section, `SaveManager.GetAccountData()` | none |

If the new feature survives restart: append to `SaveData`, write serializer if new collection, register save callback in `SaveManager`. If shape changes → §47.1 + §48.

## 55.2 Stat / modifier pipeline (single source of truth)

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Final player stats | `Scripts/Player/PlayerStats.cs` | `Scripts/Player/AttributeService.cs`, `Scripts/Manager/PlayerStatsManager.cs`, `Scripts/Manager/AttributeModifierManager.cs`, `Scripts/Manager/BaseStatLoader.cs`, `Scripts/Manager/AttributeStatLoader.cs` | none |
| Modifier math | `Scripts/Modifier/ModifierCalculator.cs` | `Scripts/Modifiers/EffectRegistry.cs`, `Scripts/Modifiers/Buff.cs`, `Scripts/Modifiers/EquipmentEffect.cs` | none |
| Attribute → secondary stat | `Scripts/Stats/SecondaryStat.cs` | `Scripts/Stats/SecondaryStatMode.cs`, `Scripts/Stats/MainAttributeExtensions.cs`, `Scripts/Stats/SecondaryStatMappingExtensions.cs` | `Player/dataAttribute.json`, `dataAttributeMainValuePerLevel.json`, `dataSOTValuePerLevel.json` |

New modifiers must register in `EffectRegistry` and feed through `ModifierCalculator`. Never rebuild final stats in UI.

## 55.3 Equipment pipeline (11 slots)

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Equip/unequip/swap | `Scripts/Equipment/IEquipmentService.cs` + `EquipmentService.cs` | `EquipmentSlotService`, `EquipmentEventDispatcher` | none |
| Persistence | `EquipmentPersistenceService.cs` | `Scripts/Save/EquipmentSerializer.cs` | none |
| Durability | `EquipmentDurabilityService.cs` | `Scripts/Items/DurabilityService.cs`, `AutoRepairService.cs`, `RepairService.cs`, `RepairTransactionService.cs`, `Items/DurabilityColorTable.cs`, `Items/IRepairCostProvider.cs` | per-slot `dataHat.json`…`dataShoes.json`, `dataBaseEquipment.json` |
| Auto-equip | `EquipmentAutoEquipService.cs` | `EquipmentComparer.cs`, `EquipmentComparisonService.cs` | per-slot JSON |
| Effect roll / affix | `EquipmentEffectService.cs` | `EquipmentModifierService.cs`, `EquipmentStatCalculator.cs`, `EquipmentAttributeData.cs`, `AttributeWeightsConfig.cs`, entry "Effect roll/affix" pakai `RarityMechanicConfig.cs` | `dataAffixes.json`, `dataSets.json` |
| Set bonus | `EquipmentSetBonusService.cs` | `EquipmentEffect.cs` (Modifiers/) | `dataSets.json` |
| Visual | `EquipmentVisualService.cs` | `EquipmentType.cs` + `SlotIdentityService.cs` | none |
| Slot identity | `SlotIdentityService.cs` | `EquipmentType.cs`, `EquipmentTypeExtensions.cs` | none |

## 55.4 Cards (roll → inventory → upgrade → equip → effect)

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Roll cost | `Scripts/Card/CardRollService.cs` | `Constantku.cs` (`ROLL1X/10X/100X_GEM_COST`) | `Card/dataCard.json` |
| Roll item vs gem | `CardRollService.cs` (gem path) | `Scripts/Inventory/InventoryService.cs` (`CardRoll` item path), `Scripts/Item/ItemCategory.cs` | `dataCard.json`, `dataConsumables.json` |
| Inventory | `CardInventory.cs` | `VirtualCardInventorySnapshot.cs` (snapshot for UI) | none |
| Duplicate → level | `CardUpgradeService.cs` | constants in `Constantku.cs` | none — curve `[2,4,7,11,19,31,47,69,99]` |
| Equip | `CardEquipmentService.cs` | `Constantku.cs` (`CARD_MAX_SLOT=19`) | none |
| Stat effect | `CardModifierService.cs` | `Scripts/Modifiers/EffectRegistry.cs` | `dataCard.json` |
| UI façade | `Scripts/Manager/CardManager.cs` | `Scripts/UI/CardCollection/CardCollectionUI.cs`, `CardRollButtonUI.cs`, `CardLevelValueItemUI.cs`, `Scripts/Controller/CardCollectionController.cs` | none |

## 55.5 Crafting (single domain, multi-stage pipeline)

Owner: `Scripts/Crafting/CraftingManager.cs`.

Pipeline order (do not reorder without §45 docs):
CraftRollService
↓
CraftValidator + CraftRecipeValidationRunner
↓
CraftCostResolver
↓
CraftTransactionService   (consume materials from InventoryService)
↓
CraftPipeline
↓
CraftResultValidator
↓
CraftRewardBuilder → CraftRewardService
↓
CraftPersistenceService + CraftCompletionService
↓
CraftQueueService (timed jobs)


Sibling services:
- `CraftContextBuilder.cs` — builds `CraftContext` for VIP/attribute aggregation.
- `CraftModifiers.cs` — output modifier roll helpers.
- `CraftData.cs`, `CraftJob.cs`, `CraftingConfig.cs` — DTO/config.
- `CraftRecipeRepository.cs`, `CraftRecipeData.cs` — recipe lookup.
- `AttributeRollService.cs` — secondary/attribute roll during craft.

Data files:
- `Crafting/dataConfigCrafting.json`
- `Crafting/Equipment/dataBaseEquipment.json`
- `Crafting/Equipment/dataRecipe{Hat,Gloves,Cape,Armor,Belt,Pants,Pendant,Ring,Earring,Bracelet,Shoes}.json`
- `Crafting/Potion/dataRecipe{HealthPotion,ManaPotion}.json`

UI: `Scripts/Crafting/JobEntryUI.cs`, `Scripts/Controller/CraftingController.cs`, `CraftingUIController.cs`, `CraftingRecipeEntry.cs`.

## 55.6 Combat pipeline

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Player attack | `Scripts/Player/Player.cs` | `PlayerStats.cs`, `AuraCollider.cs` | `Player/dataPlayer.json` |
| Projectile | `Scripts/Player/Projectile.cs` | `Scripts/Manager/ProjectilePool.cs` | `Player/dataPlayer.json` |
| Enemy | `Scripts/Enemy/EnemyAi.cs` | `EnemySpawner.cs`, `EnemyData.cs`, `EnemyStatisticsManager.cs` | `dataEnemy.json` |
| Status | `Scripts/Enemy/EnemyStatusEffectController.cs` | `Scripts/Enemy/StatusEffects/IStatusEffect.cs`, `BaseStatusEffect.cs`, `ConcreteStatusEffects.cs` | none |
| Wave | `Scripts/Manager/WaveManager.cs` | `Utilityku.WaveMultiplier` (single source), `dataWave.json` (`MAX_WAVE_PER_TIER=350`) | `dataWave.json` |

## 55.7 Ultimates (8 keys, not 8 files)

Owner: `Scripts/Ultimate/UltimateManager.cs`.
Registry: `Scripts/Ultimate/UltimateFactory.cs` (static).
Interface: `Scripts/Ultimate/IUltimateHandler.cs`.

Handler ids (registered from `UltimateManager.Awake`):
Void, Tank, Root, Bomb, Fountain, Cloud, Lightning, Shockwave.

To add a new ultimate: add `ultimateId` to `dataUltimate.json`, append registration line in `UltimateManager.Awake`, implement `IUltimateHandler` (file may live in any subdirectory — there is no per-handler file naming convention).

## 55.8 Reward / economy

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Currency | `Scripts/Economy/EconomyManager.cs` | `Scripts/Economy/CurrencyData.cs`, `Scripts/Core/Interfaces/IEconomyService.cs` | none |
| Reward popup | `Scripts/Reward/RewardManager.cs` | `RewardData.cs`, `RewardPopup.cs`, `RewardSlot.cs` | none |
| Daily | `Scripts/Daily/DailyRewardService.cs` | `DailyRewardManager.cs`, `DailyRewardSaveData.cs`, `DailyRewardData.cs`, `DailyRewardSlot.cs`, `DailyRewardUI.cs` | `Player/dataMission.json` (verify path) |
| Idle | `Scripts/IdleReward/IdleRewardManager.cs` | `IdleRewardUI.cs`, `IdleRewardData.cs` | none |
| VIP | `Scripts/Data/VIPData.cs` | `SaveManager.IsDailyEnabled`, `GameSpeedController.CheckVIP`, `CraftContextBuilder` | none — bool flags |
| Drop bag | `Scripts/Manager/DropBagManager.cs` | `DropBag.cs`, `Scripts/Items/DropTable.cs`, `Scripts/Items/Random/IRandomProvider.cs` | none |

## 55.9 Mission system

Owner: `Scripts/Mission/MissionService.cs` (singleton, `DontDestroyOnLoad`).
Templates: `Assets/Resources/Data/Player/dataMission.json`.
UI: `Scripts/Mission/MissionUI.cs`, `MissionSlot.cs`.

To add a new event type: enum → `MissionService.DoesEventMatchMission` switch → template row → producer hook → icon in `MissionUI.GetMissionIcon`. See §45.6.

## 55.10 Inventory

Owner: `Scripts/Inventory/InventoryService.cs` (`IInventoryService`).
Siblings: `InventoryManager.cs`, `InventoryItem.cs`, `InventoryItemExtensions.cs`.
Enums: `Scripts/Item/ItemCategory.cs`, `Scripts/Item/ItemState.cs`.

Persistence: `Scripts/Save/InventorySerializer.cs` (flat `Items[]`, see §48 v3).

To add a new item category: extend `ItemCategory`, update `InventorySerializer`, update `CustomDataConverter` if the converter references the enum, bump `CURRENT_SAVE_VERSION` only if shape changes.

## 55.11 Consoles / cross-cutting

| Concern | Owner | Sibling services | Data file |
|---|---|---|---|
| Game speed | `Scripts/Controller/GameSpeedController.cs` | `VIPData.maxSpeed` | none |
| Settings | `Scripts/Controller/SettingsController.cs` | `Scripts/UI/Settings/GameSettings.cs` | none |
| Victory | `Scripts/Controller/VictoryController.cs` | `WaveManager`, `Data/VictoryData.cs` | none |
| Audio | `Scripts/Manager/AudioManager.cs` | `Scripts/Core/Interfaces/IAudioService.cs` | AudioClip assets |
| Analytics | `Scripts/Manager/AnalyticsManager.cs` | `Scripts/Core/Interfaces/IAnalyticsService.cs` | none |
| Ads | `Scripts/Manager/AdvertisingManager.cs` | `Scripts/Core/Interfaces/IAdsService.cs` | provider-specific |
| Damage popup | `Scripts/UI/DamagePopup/DamagePopup.cs` | `DamagePopupPool.cs`, `Scripts/Manager/DamagePopupManager.cs`, `Scripts/Data/DamagePopupData.cs` | none |
| Camera | `Scripts/Camera/CameraFollow.cs`, `Scripts/Camera/BackgroundScaler.cs` | none | none |

## 55.12 Read-first checklist (use before any new feature)

1. Identify domain from §28.
2. Open the owner file in §55.x table — read top 100 lines.
3. Open sibling services listed in that row.
4. Open the JSON data file.
5. If persistent: read `SaveData` shape, decide if version bump is needed (§47.1).
6. If modifier: read `ModifierCalculator` + `EffectRegistry`, plan registration.
7. UI work: read §25 (UI never owns logic).
8. Tests: pure formulas → EditMode; interactions → PlayMode.

## 55.13 Forbidden shortcuts (do not, even when convenient)

- Adding a new singleton before checking `ServiceLocator` and existing managers (§34).
- Putting gameplay logic in a UI controller (§25).
- Hardcoding balance values that should live in JSON (§5).
- Re-implementing equipment stat aggregation in UI/tooltips (§10.3).
- Building a second status-effect implementation outside `StatusEffects/` (§18).
- Using display `Name` as save key or logic key (§6).
- Adding caching without invalidating on modifier change (§13.2).
- Skipping §47.1 + §48 when `SaveData` shape changes.

---

# I., # II., and # III. below are the final closing commands for this document.

---

# I. DEFINITION OF DONE

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

# II. AGENT CHECKLIST BEFORE MODIFYING CODE

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
[ ] Manual Compile errors checked
[ ] Existing consumers checked
[ ] Save/load checked if applicable
[ ] Duplicate events checked
[ ] Edge cases checked
[ ] Regression test considered/added
[ ] Design documentation updated
[ ] CLAUDE.md updated if architecture/rules changed
```

---

# III. FINAL PRINCIPLE

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
> **Put the rule in the correct domain.**
> **Keep data separate from behavior.**
> **Keep UI separate from domain logic.**
> **Preserve persistent data.**
> **Document the decision to Design.md**
