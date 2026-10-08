# Card System Design — IdleDefenseSurvival

**Purpose:** Card definitions, progression, rolling, equipment, stat modifiers, and runtime effects.

**Last Updated:** 2026-10-01

---

## Related Design Documents

- [Modifier_Design.md](./Modifier_Design.md) — card stat modifiers and percent units
- [Player_Design.md](./Player_Design.md) — player stat and combat integration
- [Projectile_Design.md](./Projectile_Design.md) — projectile-triggered card effects
- [Combat_Design.md](./Combat_Design.md) — damage, overkill, and death-prevention ordering
- [Wave_Design.md](./Wave_Design.md) — wave-scoped card events and progression effects
- [StatusEffect_Design.md](./StatusEffect_Design.md) — Chain Reaction's Volatile status
- [Economy_Design.md](./Economy_Design.md) — gem/card-roll transactions
- [SaveManager_Design.md](./SaveManager_Design.md) — card inventory and pity persistence

---

## 1. Source Of Truth

`Assets/Resources/Data/Card/dataCard.json` is the single authored source for card definitions and card balance. Its current schema version is `3`.

The file owns:

- `Progression`: starting/max slots, max level, roll costs, pity thresholds, slot expansion costs, and duplicate requirements.
- `RarityConfig`: rarity weights.
- `Cards`: card identity, display text, rarity, effect/stat identifier, level scaling, custom `Parameters`, and Apex `Mutations`.

There is no separate card balance JSON. Card roll/level/slot values must not be copied into `GameConstants`, behavior fallbacks, or UI code. `GameConstants.KEY_PITY_*` remain persistence keys only; they are not balance values.

`CardDatabase` loads this file with Newtonsoft.Json, validates each card's ID/mode/rarity/effect-or-stat identifier and progression array sizes, then exposes cached definitions. An invalid or duplicate definition is rejected and logged. Custom behavior parameters are read from the matching card definition by stable card ID.

---

## 2. Card Definition Schema

A definition uses exactly one of `EffectType` or `SkillType`:

```json
{
  "Id": "example_card",
  "Name": "Example Card",
  "Description": "Displayed in the card detail view.",
  "SkillType": "AttackDamage",
  "Mode": "Percent",
  "BaseValue": 3,
  "ValuePerLevel": 1,
  "CardRarity": "Common"
}
```

Special behavior cards use `EffectType` and may have one `Parameters` object. Stat cards use `SkillType`. Apex Evolution mutation definitions are nested under its `Mutations` property; each mutation has exactly one `SkillType` or mutation `EffectType`. There is no supported multi-effect `Effects[]` schema.

The level value is:

```text
Value(level) = BaseValue + ValuePerLevel * (level - 1)
```

`Mode` is `Flat` or `Percent`. For stat modifiers, percent values are percentage points consumed by the modifier pipeline. For card query effects, `CardModifierService.GetEffectResult` converts `Percent` values into fractions; `GetCardParameter` returns a raw configured parameter. Parameter names should state their units (for example, `CooldownSeconds`, `EvasionBonusPercent`, or `ProjectileDamageMultiplier`).

---

## 3. Progression Configuration

All progression values live under `Progression` in `dataCard.json`:

- `StartingSlots` and `MaximumSlots` define the initial and upper equipment slot counts.
- `MaximumLevel` controls upgrade limits and card availability during rolls.
- `RollCosts.Single`, `Ten`, and `Hundred` define bundle costs. `CardRollService.CalculateRollGemCost` decomposes any batch into hundred-, ten-, and single-roll bundles.
- `PityThresholds` define the guarantee count for Epic, Legendary, and Mythic. Divine has no pity counter.
- `SlotExpansionCosts` is indexed by current slot count; index zero is the initial free slot, and index `n` is the cost to unlock slot `n + 1`. Its length must equal `MaximumSlots`.
- `DuplicateRequirements` is indexed by `currentLevel - 1`; its length must equal `MaximumLevel - 1`.

Only `CardDatabase.Progression` is used by roll, inventory, upgrade, slot, and collection UI code. Do not introduce a second copy of these values.

---

## 4. Rolling And Pity

`CardRollService` generates roll results without mutating real inventory, economy, or save state; it updates only the caller-provided virtual inventory snapshot. `CardManager` owns the transaction: spend gems or consume CardRoll items, load and save pity counters, apply the virtual inventory result, auto-upgrade, and notify UI.

Rarity weights come from `RarityConfig`. Selection excludes cards already at `MaximumLevel`; if a selected rarity has no available card, selection falls back to lower rarities. If no card is available, the roll is refunded.

Three independent pity counters are persisted with card inventory data. Each attempted roll increments the counters. When a card is awarded:

- Epic or higher resets Epic pity.
- Legendary or higher resets Epic and Legendary pity.
- Mythic or higher resets all three.
- Divine is above Mythic and therefore resets all three, but is not itself guaranteed by a pity counter.

A reward is marked pity-guaranteed only when the awarded card meets a currently triggered guarantee. Pity thresholds and roll weights/costs are read from `dataCard.json`, not repeated in code or documentation tables.

---

## 5. Ownership, Leveling, And Equipment

Owned card instances are keyed by stable card ID and store level plus remaining duplicate count. World Breaker stacks and partial kill progress persist on its owned card data, surviving unequip, scene changes, and save/load. Other temporary behavior state is not separately persisted.

`CardUpgradeService` reads duplicate requirements and the level cap from `CardDatabase.Progression`. Batch rolls use `VirtualCardInventorySnapshot` to simulate acquisitions and upgrades before mutating real inventory. `CardManager` injects the database's duplicate-requirement list into the snapshot; the simulation owns no copied progression curve or runtime singleton dependency.

`CardEquipmentService` reads starting/max slots and expansion costs from the same progression object. Only equipped cards have active behaviors or affect stats. Equip/unequip/upgrade refreshes `CardRuntimeManager` and the modifier pipeline. The collection view reads `CardData.Description` directly and displays values using the definition's level formula.

---

## 6. Runtime Architecture

- `CardBehaviorRegistry` maps special `CardEffectType` values to behavior implementations. Simple data-driven/query effects use `DefaultCardBehavior`.
- `CardRuntimeManager` creates one state and behavior per equipped card, subscribes behaviors to required events, dispatches domain events, and calls behavior `Update` once per frame.
- `CardModifierService` is the backward-compatible query/refresh facade. It does not own the balance or behavior state.
- `EnemyDeathHandler` dispatches one kill event with enemy context and damage source. This supports both kill counters and effects based on enemy max HP without a second kill subscription.
- `Player.Heal(amount, isLifeSteal)` separates genuine projectile Life Steal from unrelated healing for Vampiric Frenzy.
- Projectile-only state such as Infinite Arsenal is stored on the pooled projectile instance and reset when returned to the pool.

Unknown data identifiers are rejected by `CardDatabase`; do not rely on a silent default behavior to hide a typo.

### 6.1 HUD Provider Pattern

**Purpose:** Decouple card effect presentation from `PlayerUIController`, enabling scalable card HUD without modifying UI code for each new card effect.

**Architecture:**

```
CardRuntimeManager
    ↓
ICardHUDProvider[]
    ↓
CardBonusUI
    ↓
CardBonusWidget[]
```

**Implementation:**

Behaviors implementing `ICardHUDProvider` expose HUD state via `GetHUDData()`:

```csharp
public interface ICardHUDProvider
{
    CardEffectType EffectType { get; }
    CardHUDData GetHUDData();
}

public readonly struct CardHUDData
{
    public readonly Sprite Icon;       // optional icon for widget
    public readonly string Value;      // formatted display ("12%" or "3/5")
    public readonly float FillAmount;  // 0-1 radial cooldown/progress (default 1f)

    public CardHUDData(Sprite icon, string value, float fillAmount = 1f)
    {
        Icon = icon;
        Value = value;
        FillAmount = fillAmount;
    }
}
```

`CardRuntimeManager.GetHUDProviders()` returns all active behaviors implementing `ICardHUDProvider`. `CardBonusUI` iterates providers and routes `CardHUDData` to matching widgets via `CardEffectType`.

**Current implementations:**
- `ArcaneResonanceCardBehavior` — attack count + remaining duration
- `CrazyGamblerCardBehavior` — current bonus percentage
- `DesperadosCardBehavior` — current bonus percentage
- `DeathChainCardBehavior` — current stack count
- `SoulHarvesterCardBehavior` — soul count + max stack

**Adding new card HUD:**

1. Implement `ICardHUDProvider` on behavior:
   ```csharp
   CardEffectType ICardHUDProvider.EffectType => CardEffectType.NewCard;
   
   public CardHUDData GetHUDData()
   {
       return new CardHUDData(GetCardIcon(), $"{_value:0}%", _cooldownFill);
   }
   ```

2. Add `CardBonusWidget` prefab in Inspector with `Effect Type` assigned to `NewCard`.

3. No changes to `CardBonusUI`, `PlayerUIController`, or `CardRuntimeManager` needed.

**Benefits:**
- Zero coupling between UI and specific card behaviors
- New card effects require no UI code changes
- UI queries generic provider interface, not concrete implementations
- Widget assignment is data-driven via Inspector

---

## 7. Effect Behavior Contract

Behavioral values are authored on the card that consumes them. The current custom parameter keys are in `dataCard.json`; code should not repeat their balance defaults.

- **Gold / Meat:** multiply enemy currency rewards using the card's level-scaled percent value.
- **Frost Aura:** slows enemies within player attack range using the level-scaled percent value.
- **Shield:** grants the configured fraction of max HP while the player is at full HP.
- **Time Fast:** scales wave and inter-wave durations through `WaveManager.ProgressionSpeed`.
- **Enemy Balance:** modifies minimum spawn interval through `WaveManager`.
- **Add Tank:** adds one Tank stack and applies the level-scaled duration multiplier.
- **Crazy Gambler / Desperados:** after their configured activation wave, roll once per wave for the configured positive/negative stat change, bounded by configured stack count.
- **Bat Stalker / Heal On Kill:** on a player-attributed kill, heal by the card's percent of that enemy's max HP. Bat Stalker is the current authored card; both identifiers share the same implementation.
- **Berserker:** converts missing player HP into Attack Damage, capped by its level-scaled card value.
- **Death Chain:** each player kill adds one Attack Damage stack; stacks cap at the configured maximum and all reset after the configured no-kill window.
- **Bullet Storm:** fires the configured number of additional projectiles every `ceil(Value(level))` player attacks. Per-projectile damage multiplier is configured on the card.
- **Execution Protocol:** on hit, checks its level-scaled chance against normal or Boss/Special HP thresholds from its parameters.
- **Overkill:** transfers the configured fraction of damage in excess of the enemy's remaining HP after mitigation. Transfer is capped by `MaximumTransferMultiplier` times the triggering hit's final damage.
- **Vampiric Frenzy:** each configured fraction of max HP actually recovered via Life Steal adds an Attack Speed stack. Stacks expire independently after their configured duration and are capped.
- **Guardian Instinct:** when damage leaves HP at/below its configured threshold, grants a max-HP shield and flat Evasion bonus. Active duration and cooldown are separate configured values. It does not reset on wave start.
- **Critical Cascade:** a critical hit rolls the configured chance and fires a normal critical-capable projectile with the configured damage multiplier.
- **War Machine:** uses elapsed time between attacks; continuous attacks activate its level-scaled three-stat bonus, and the configured idle interval removes it.
- **Apocalypse Engine:** player kills accumulate toward `KillsPerStack`; each stack applies Attack Damage and configured Attack Speed/Critical Damage ratios for the current wave.
- **Infinite Arsenal:** every `ceil(Value(level))` player attacks creates a special per-projectile state: forced critical, configured bounce count, pierce through unhit enemies in attack range, and configured damage multiplier.
- **Soul Harvester:** player kills grant souls and Attack Damage per soul, with its initial cap and cap growth controlled by card parameters.
- **Death Reversal:** stores player HP/position snapshots at the configured interval, retains the configured rewind window, triggers once per wave on lethal damage, restores the higher of snapshot HP or the card's max-HP fraction, and clears enemy projectiles in its configured radius. It resolves before Death Defy.
- **Immortal (Angel):** triggers only after Death Defy fails, restores full HP, then grants immunity until the wave completes. Its existing card value drives the cooldown display/state.
- **Void Overlord:** its timer continues across wave transitions. While active, player projectiles pierce unhit enemies in attack range, gain the card's percent damage bonus, and enemy healing is suppressed.
- **Chain Reaction:** a player kill has the card's level-scaled chance to mark nearby enemies Volatile. A Volatile death explodes, damages nearby enemies, and can spread Volatile. Mark radius, duration, explosion radius, and damage multiplier are configured on the card.
- **World Breaker (Divine):** every configured player-owned kill milestone adds the level-scaled percent to all player-owned damage sources and persists stacks/progress on the owned card. Every configured stack milestone ignores the configured percent of enemy defense, up to its configured cap.
- **Divine Retribution (Divine):** actual HP lost after shields/mitigation is accumulated over a rolling configured window. Reaching the max-HP threshold triggers an Attack-Damage-based blast against every active enemy and starts the configured cooldown.
- **Celestial Arsenal (Divine):** every configured player-attack count randomly summons a piercing beam, an orbital area strike centered on the highest-current-HP enemy, or chain lightning. Using each distinct weapon type permanently increases Celestial Weapon damage for the active battle.
- **Law of Collapse (Divine):** on its configured interval, selects the active enemy with highest current HP and removes the configured fraction of current HP. Bosses use their separate fraction. Volatile is the game's marked status and applies the configured damage multiplier.
- **Apex Evolution (Divine):** player-owned kills accumulate toward its level-scaled offer interval. The UI presents up to the configured number of distinct, unchosen mutations from `Mutations`; the player selects one. A mutation can be chosen once per active wave/battle, resetting on `OnWaveStart`. Stat mutations add Card-source modifiers; Devourer and Storm Heart are read by enemy/projectile damage owners.

The six Apex mutations are data definitions: Fangs, Overclock, Void Skin, and Titan Core are stat modifiers; Devourer checks the target's current-HP fraction; Storm Heart accelerates configured projectile-effect triggers. New values belong in `dataCard.json`; a new executable mutation effect also requires validation/implementation in `ApexMutationEffectType`.

---

## 8. Event And Lifecycle Rules

Event subscriptions are explicit in `CardRuntimeManager.GetEventsForEffect`:

- Player attack: Bullet Storm, Infinite Arsenal, War Machine, Celestial Arsenal.
- Player damage: Berserker, Guardian Instinct, Death Reversal, Divine Retribution.
- Player heal: Berserker; Life Steal only: Vampiric Frenzy.
- Enemy kill: Death Chain, Soul Harvester, Chain Reaction, Apocalypse Engine, World Breaker, Apex Evolution, Bat Stalker, Heal On Kill.
- Wave complete: Crazy Gambler, Desperados, Angel.
- Wave start: Death Reversal, Angel, Apex Evolution.
- Stat/query-only effects do not subscribe to unrelated wave events.

Unequipping calls behavior cleanup, removes temporary modifiers, and unsubscribes events. Wave-scoped state resets only when the effect's documented rule requires it. Do not run card timer updates both in `Player.Update` and `CardRuntimeManager.Update`.

---

## 9. Extension And Verification Checklist

When adding a card:

1. Add one stable ID and exactly one `EffectType` or `SkillType` entry to `dataCard.json`.
2. Define level scaling and custom parameters there; keep units explicit in parameter names.
3. For stat cards, confirm `SkillType` exists and `Mode` is intentional.
4. For behavior cards, register a behavior and subscribe only to required events.
5. Connect effects at the domain owner (Player, Projectile, EnemyDeathHandler, WaveManager, or EnemyAi), not in UI.
6. Update the description to match the actual level-one behavior and scaling.
7. Verify equip/unequip cleanup, save/load ownership, pooled projectile reset, repeated triggers, and max-level roll filtering.
8. Update this document and any affected domain docs.

No save-version migration is required. World Breaker adds two optional fields to the existing owned-card record (`WorldBreakerStacks` and `WorldBreakerKillProgress`); old saves deserialize these as zero. Other card progression/configuration remains runtime data in `dataCard.json`.

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-10-01 | Replaced legacy schema and behavior notes with current single-source JSON/runtime contract | Card data and behavior audit |
| 2026-10-01 | Added five Divine effects, Apex mutations, and World Breaker persistence | Divine card implementation |
