# Equipment System Design — IdleDefenseSurvival

Live-service idle RPG, 2D auto-shooter. Four core attributes (CON/STR/INT/DEX) are the foundation of ALL character power. Equipment exists to move those attributes — never to hand out combat stats directly in bulk.

Core rule: **an item is never just a bigger number. Every rarity adds a new mechanic.** When two items differ only by magnitude, one of them is worthless.

---

## 1. Equipment Philosophy

**80/20 power split.** ~80% of an item's value comes from core attributes, ~20% from combat-stat secondaries. Attributes funnel through the attribute pool (AttributeModifierManager → ModifierSource.AccountLevel → ModifierManager), secondaries through the equipment pool (ModifierSource.Equipment). Two pools, one item — the split is enforced by data, not code.

Four laws:

1. **Slot identity.** A hat and a pair of shoes answer different questions. Slots never compete directly.
2. **Rarity = mechanic, not magnitude.** Divine is not "legendary with ×3 numbers". Divine unlocks something legendary cannot do at any level.
3. **Meaningful choice.** Two items of the same slot must ask: do I want INT or CON? Crit or attack speed? Tradeoffs, not tiers.
4. **Progression through composition.** Power comes from *which* items you combine, not from having *the* item. Early game: any hat is a hat. Endgame: your hat defines your build.

---

## 2. Slot Identities

| Slot | Identity | Core Attribute(s) | Secondary Stats (complement identity) |
|---|---|---|---|
| Hat | Magic Caster | INT (+CON) | ElementMastery, UltimateAttack, ManaPoint, ManaRegen |
| Gloves | Physical DPS | STR | CriticalDamage, KnockbackForce |
| Cape | Versatile Ranger | DEX (+INT) | AttackSpeed, Evasion |
| Armor | Tank | CON | DefenseAmount, HealthRegen |
| Belt | Hybrid Tank/Bruiser | CON + STR | HealthRegen, LifeSteal |
| Pants | Guardian | CON + DEX | HealthRegen, Evasion |
| Pendant | Spell Power | STR + INT | UltimateAttack, ElementMastery |
| Ring | Crit Specialist | INT | CriticalChance, CriticalDamage |
| Earring | Swift Assassin | DEX | AttackSpeed, MultiShootChance |
| Bracelet | Life Stealer | STR + DEX | LifeSteal, AttackSpeed |
| Shoes | Speedster | DEX | AttackSpeed, Evasion |

**Build math:** CON = survival (Armor/Pants/Belt), STR = raw damage (Gloves/Bracelet), INT = magic/crit (Hat/Ring/Pendant), DEX = speed/evasion (Shoes/Earring/Cape). Every attribute has ≥2 main slots, no orphaned stat. Cross-focus slots (Belt, Pendant, Cape) are the tradeoff points — they pull two attributes, so building them costs focus.

---

## 3. Rarity = New Mechanic

| Rarity | Attribute | Secondary | Socket | Passive | Visual |
|---|---|---|---|---|---|
| Common | 1 attr | — | 0 | — | — |
| Uncommon | 1–2 attr | 1 | 0 | — | — |
| Rare | 1–2 attr | 1 | 1 | — | — |
| Epic | 1–2 attr | 2 | 1 | — | — |
| Legendary | 1–2 attr | 3 | 2 | 1 passive | — |
| Mythic | 2 attr | 4 | 2 | 1 passive | — |
| Ancient | 2 attr | 5 | 3 | 1 strong passive | glow |
| Divine | 2 attr | 6 | 3 | 1 unique passive | particle aura + sound |

**Mechanics ladder (each rarity adds a DOOR):**
- **Uncommon** — a secondary stat appears. First choice.
- **Rare** — first socket. First gem decision.
- **Epic** — a second secondary. Two axes of choice.
- **Legendary** — a passive effect. Gameplay changes, not just math.
- **Mythic** — second attribute (hybrid). Build diversification.
- **Ancient** — strong passive. A build-defining tier.
- **Divine** — unique passive + FX. Signature item, trade-able, one per slot.

Sockets unlock gem economy (which is itself a secondary-stat sink). Passives unlock the effect registry. Never rebalance passives and base numbers together — **design rule: magnitude changes at rarity boundaries must be ≤15%**; mechanics do the heavy lifting.

---

## 4. Secondary Stat Budget (the 20%)

Secondaries are chosen from the SkillType list only. Everything maps into the existing pool:

**Damage:** AttackDamage, CriticalDamage, CriticalChance, DamagePerRange, KnockbackForce, KnockbackChance, MultiShootCount, MultiShootChance, BounceCount, BounceChance
**Survival:** HealthPoint, DefenseAmount, HealthRegen, Evasion, DeathDefy, LifeSteal
**Speed/Utility:** AttackSpeed, AttackRange, StuntChance, StuntDuration
**Economy:** InterestWave, UltimateAttack, ElementMastery, ManaPoint, ManaRegen (INT-linked)

Budget per rarity (flat base values, scale with level/enhance):

| Rarity | Budget (approx. stat points at max level) |
|---|---|
| Common | 0 |
| Uncommon | 12 |
| Rare | 16 |
| Epic | 22 |
| Legendary | 30 |
| Mythic | 40 |
| Ancient | 55 |
| Divine | 75 |

Secondaries = **at most 20%** of total item value; attributes are always ≥80%.

---

## 4b. Affix System (random prefixes/suffixes)

**Critique 7 answer:** the design previously had no random modifiers — every item of a kind was identical, so drops were interchangeable. Affixes make each drop roll an identity.

- Every generated equipment item rolls **prefix + suffix** from `dataItems.json → Affixes` (AffixType: Prefix=0, Suffix=1). Prefix then suffix alternate per roll — a 2-affix item always gets one of each, never two prefixes.
- Affix count scales with rarity (Common 0, Uncommon/Rare 1, Epic 2, Legendary 2, Mythic 3, Ancient 3, Divine 4 — see `AffixGeneratorConfig`).
- **Slot + rarity restriction (the pool is filtered, not flat):** each affix declares `MinRarity/MaxRarity` (gates the pool) + `ApplicableTypes` (which equipment slots it can land on — slot identity preserved: Ring rolls Crit/CritDmg/INT but never LifeRegen/Knockback/Defense), + `Weight` (drop weight, cumulative-sum roll). Generator removes same-affix-id after each pick so no duplicate on one item.
- **Three affix classes** (an affix grants one or more):
  1. **Stat affix** (original): `Stats` = `CombatStatEntry` curve → combat-secondary pool, `ModifierSource.Equipment`.
  2. **Attribute affix** (new): `AttributeStats` = `AttributeStatEntry` curve → feeds the **attribute pool** (`AttributeModifierManager`) via `GetItemAttributeBonuses`. Prefix like *"of Wisdom"* grants +INT. The 80/20 rule holds because affix-attributes are kept rare/low (see §4b note below).
  3. **Passive affix** (new): `PassiveEffect` = a `SpecialEffectEntry` (EffectType/Value/Chance/Cooldown). On equip `EquipmentEffectService.ActivateItemEffects` activates them like item specials (`EffectFactory.Create`); `Projectile.HitTarget` pumps `TriggerEffects(OnHit, TriggerData{Enemy})` so "of Glacier: 12% chance to FreezeEnemy" actually fires. Lifecycle keyed to the owning item (`GetRuntimeData` stamps `ItemInstanceId`) so unequip wipes them symmetrically.
- Rolled values = `BaseValue × rarity multiplier × tier multiplier × variance(0.9–1.1)` — same item name, different rolls. This is the "hunt a good roll" chase.
- Secondary affixes feed the combat-stat pool (`ModifierSource.Equipment`). Attribute affixes are the deliberate rarity in the pool (see weighting below).

**Attribute-affix budget:** secondary affixes currently outnumber attribute affixes in the pool (14 secondary vs 1 attribute), so attribute grants stay rare. Adjust by weighting (a lower `Weight` for attribute affixes) — no code change needed. Naming: prefix goes before, suffix after: *"Strong Leather Hat of the Hunter"*. Rendering affix names in tooltips is a follow-up (data + generator land first).

**Build profile steering:** `BuildProfile` is an enum (`All/Tank/Warrior/Mage/Assassin`), on `IEquipmentRepository`, feeding `AttributeWeightsConfig.ForBuild(profile)` → auto-equip scores. No string `"all"/"strength"` remnants — typos are compile errors.

---

## 5. Socket Progression

- Common–Uncommon: 0 sockets.
- Rare: 1. Epic: 1. Legendary–Mythic: 2. Ancient–Divine: 3.
- Socket 1 unlocks at level 10, socket 2 at level 30, socket 3 at level 50 (item level gates, so sockets feel earned, not granted).
- Gem stat values are **pre-scaled, never multiplied by item rarity**. A gem is a gem. Rarity of item determines how many gems it can hold, not how strong they are — otherwise gems become double-dipping.
- Socket colors restrict gem types (see dataItems.json SocketConfigData) so gem choice is a decision, not an autofill.

---

## 6. Passive Effect Progression

Legendary+ only. Progression = gameplay complexity, not damage:

| Rarity | Passive tier | Examples |
|---|---|---|
| Legendary | Minor | On kill: +3% AttackSpeed for 3s; 15% chance to fire an extra projectile |
| Mythic | Standard | On critical: chain to 1 nearby enemy; +10% Ultimate damage while at full HP |
| Ancient | Strong | While above 50% HP: +25% AttackDamage; on death: revive once with 30% HP per wave |
| Divine | Unique | "Vampiric Shots": every 5th attack heals 2% max HP; "Arcane Storm": kill grants UltimateAttack stack, max 10, resets on damage |

Rules: passives live in the existing effect registry (SpecialEffectEntry / PassiveSkillEntry), one per item, no stacking same-id passives, all condition-based (no passive that is "just a bigger number"). Design ceiling: a player runs 11 items → 11 passive interactions max; keep each readable in one line of tooltip.

---

## 7. Set Bonus Progression

Sets exist to reward **cohesion**, not to beat raw stats:

- 2/3/4/5/6/8/11 piece tiers, stats ramp, but tier effects always include ONE non-math bonus (special effect).
- Set bonuses prefer **attributes** (per our pipeline: set AttributeBonuses feed AttributeModifierManager) — a 4-piece CON bonus is a build direction, not a damage line.
- Full-set (11): one strong passive + visual aura.
- Mixing sets: 2+2 is viable but never beats 4-of-a-kind at the same rarity — because the 4-piece tier carries the mechanic, not the numbers.
- Set items are the one place secondary stats may exceed budget slightly — the set is the tax you pay for them.

---

## 8. Balancing Philosophy

1. **Attribute power is sacred.** All damage/HP flows from attributes. If an item's secondary makes it feel mandatory, the secondary is wrong, not underpowered.
2. **One axis per item.** An item is either tanky, or fast, or crit, or caster — never all. Item budget is spent on ONE identity (2 stats max).
3. **Multiplicative game, additive items.** Keep item bonuses additive within an item; multiplicative stacking happens through the attribute system, which is the only global multiplier. This is what keeps "thousands of items" balanceable: per-item math never compounds.
4. **The 15% rule.** A rarity's *numbers* never exceed +15% over the tier below. Mechanics are the differentiator, so numbers can afford to be boring.
5. **Power spike check:** at any point, the strongest hypothetical loadout (all Ancient+) must be beatable by a deliberately-built Common/Uncommon set with good gems — otherwise rarity replaces skill/build and the game dies.

---

## 9. Drop Rate Philosophy

Drop chance scales with **item level + rarity weight**, but the REAL currency is attribute pressure:

- Drop table rolls rarity first (Common 40% / Uncommon 30% / Rare 18% / Epic 8% / Legendary 3.2% / Mythic 0.7% / Ancient 0.1% / Divine 0.01% — tunable per zone/wave).
- Within rarity, roll which slot — weighted by what the player is missing (guaranteed pity: no repeated slot within a drop window).
- Within slot, roll the item — each item in a slot is equally likely (design must keep per-slot item counts balanced, or the pity is wasted).
- **Guaranteed drops:** every Nth kill (N = rarity weight × 10) grants a Rare+; first kill of a boss grants an Epic+.
- Gems and crafting materials drop alongside — sockets become the sink that makes dupes valuable (gem upgrades eat dupes, not just gold).

---

## 10. Auto-Equip Score Calculation

Auto-equip must reproduce a human's "is this better?" — attributes weighted at ~80%:

```
score(item) = Σ (attrValue × attrWeight)       // ~80% share, per-build weights
            + Σ (secondaryValue × statWeight)  // ~20% share, per-build weights
            + socketBonus(0.5 × sockets)       // more sockets = more value
            + passiveBonus (mechanic bonus, small — never raw power)
```

- **attrWeight:** per-build `AttributeWeightsConfig` (`EquipmentAutoEquipService`). Default profile `"all"` = CON/STR/INT/DEX all ×1 (flat equivalence). Focus profiles (`"strength"`, `"constitution"`, `"intelligence"`, `"dexterity"`) weight the build's primary attribute ×3 and the others ×0.5 — so a DEX build auto-equips DEX gear, CON gear still scores but no longer ties.
- **statWeight:** per-build config (e.g., crit build weighs CriticalChance ×3, others ×1). Default profile = even weights, tuned to ~20% share.
- Normalize by item level: `score / sqrt(itemLevel)` so a level-5 Ancient doesn't auto-beat a level-50 Common in a slot the player is under-leveled for.
- Anti-power-creep: score displayed to player as 4 bars (Attribute / Combat / Sockets / Passive). Auto-equip defaults to the best *attribute* score; secondaries only break ties. **Player sees the tradeoff, never a blind "best"**.

---

## 11. Full Examples — Every Rarity, 4 Slots

All values = BaseValue / ValuePerLevel / ValuePerEnhance. Slots not shown follow the same ladder.

### Hat (Magic Caster — INT)

**Common — Leather Hat** — INT 4 / 0.3 / 0.5
**Rare — Apprentice Hat** — INT 8 / 0.6 / 1.0, 1 socket
**Epic — Sorcerer Hat** — INT 12 / 0.9 / 1.5, +Fire Damage 2%, 1 socket
**Legendary — Archmage Crown** — INT 15 / 1.1 / 2.0, +Water Damage 3%, +UltimateAttack 4%, 2 sockets, passive: "On ultimate cast: +10% UltimateAttack for 5s"
**Mythic — Star Caller Crown** — INT 18 / 1.3 / 2.5, +Lightning Damage 4%, +UltimateAttack 6%, +CriticalChance 2%, 2 sockets, passive: "On kill: -0.5s ultimate cooldown"
**Ancient — Void Sovereign Crown** — INT 22 / 1.5 / 3.0, +Fire Damage 6%, +UltimateAttack 8%, +CriticalChance 3%, +BounceChance 5%, 3 sockets, strong passive: "While above 50% HP: +25% UltimateAttack; on death: revive once with 30% HP per wave"
**Divine — Crown of the First Flame** — INT 28 / 1.8 / 3.5, +Fire Damage 8%, +UltimateAttack 10%, +CriticalChance 4%, +BounceChance 8%, +AttackSpeed 3%, 3 sockets, unique passive: "Arcane Storm — every kill grants 1 stack of +2% Fire Damage, max 10; resets on taking damage", FX: flame aura

### Gloves (Physical DPS — STR)

**Common — Cloth Gloves** — STR 4 / 0.3 / 0.5
**Rare — Fighter Gloves** — STR 10 / 0.8 / 1.2, +CriticalDamage 5%, 1 socket
**Epic — Berserker Gauntlets** — STR 14 / 1.0 / 1.8, +CriticalDamage 8%, +KnockbackForce 10, 1 socket
**Legendary — Dragonclaw Gauntlets** — STR 18 / 1.2 / 2.2, +CriticalDamage 10%, +KnockbackForce 15, +DamagePerRange 5%, 2 sockets, passive: "On crit: 10% chance to chain 1 extra projectile"
**Mythic — Titan Fists** — STR 22 / 1.4 / 2.8, +CriticalDamage 12%, +KnockbackForce 20, +DamagePerRange 7%, +MultiShootChance 3%, 2 sockets, passive: "On kill: +3% AttackDamage stacking, max 10, lasts 5s"
**Ancient — Fist of the Unmaker** — STR 26 / 1.6 / 3.2, +CriticalDamage 15%, +KnockbackForce 25, +DamagePerRange 10%, +MultiShootChance 5%, +BounceChance 5%, 3 sockets, strong passive: "Deal +15% damage to enemies below 30% HP"
**Divine — Godslayer Gauntlets** — STR 32 / 1.9 / 3.8, +CriticalDamage 18%, +KnockbackForce 30, +DamagePerRange 12%, +MultiShootChance 7%, +BounceChance 8%, +AttackSpeed 2%, 3 sockets, unique passive: "Vampiric Shots — every 5th attack heals 2% max HP", FX: red crackle

### Armor (Tank — CON)

**Common — Cloth Armor** — CON 8 / 0.5 / 0.8
**Rare — Iron Armor** — CON 20 / 1.5 / 2.5, +DefenseAmount 5, 1 socket
**Epic — Steel Plate** — CON 26 / 1.8 / 3.0, +DefenseAmount 8, +HealthRegen 1, 1 socket
**Legendary — Aegis Cuirass** — CON 32 / 2.0 / 3.5, +DefenseAmount 10, +HealthRegen 2, +Evasion 2%, 2 sockets, passive: "On hit: 15% chance to reflect 50% of damage taken"
**Mythic — Titan Shell** — CON 38 / 2.2 / 4.0, +DefenseAmount 13, +HealthRegen 3, +Evasion 3%, +LifeSteal 1%, 2 sockets, passive: "While above 70% HP: take 10% less damage"
**Ancient — Mountain's Heart** — CON 45 / 2.5 / 4.5, +DefenseAmount 16, +HealthRegen 4, +Evasion 4%, +LifeSteal 2%, +DeathDefy 5%, 3 sockets, strong passive: "On death: survive with 50% HP once per 60s"
**Divine — Dragonhide Vest** — CON 55 / 3.0 / 5.5, +DefenseAmount 20, +HealthRegen 5, +Evasion 5%, +LifeSteal 3%, +DeathDefy 8%, +StuntChance 5%, 3 sockets, unique passive: "Dragon's Blood — heal 5% max HP per second while below 30% HP", FX: molten veins

### Ring (Crit Specialist — INT)

**Common — Copper Ring** — INT 3 / 0.2 / 0.4
**Rare — Ruby Ring** — INT 10 / 0.7 / 1.3, +CriticalRate 3%, 1 socket
**Epic — Garnet Ring** — INT 14 / 0.9 / 1.6, +CriticalRate 5%, +CriticalDamage 5%, 1 socket
**Legendary — Eternity Ring** — INT 18 / 1.1 / 2.0, +CriticalRate 7%, +CriticalDamage 8%, +BounceChance 3%, 2 sockets, passive: "Crits deal +10% damage for every 100 INT"
**Mythic — Soulfire Ring** — INT 22 / 1.3 / 2.4, +CriticalRate 9%, +CriticalDamage 10%, +BounceChance 5%, +AttackSpeed 1%, 2 sockets, passive: "On crit: 5% chance to reset ultimate cooldown"
**Ancient — Ring of the Undying** — INT 26 / 1.5 / 2.8, +CriticalRate 11%, +CriticalDamage 13%, +BounceChance 7%, +AttackSpeed 2%, +MultiShootChance 2%, 3 sockets, strong passive: "Crits apply a mark; killing marked enemies grants +5% CriticalChance for 5s"
**Divine — Primordial Ring** — INT 32 / 1.8 / 3.2, +CriticalRate 14%, +CriticalDamage 16%, +BounceChance 10%, +AttackSpeed 3%, +MultiShootChance 3%, +BounceCount 1, 3 sockets, unique passive: "Echoing Crits — every 3rd crit fires a copy of the shot at a random enemy", FX: orbiting runes

### Belt (Hybrid — CON+STR)

**Common — Rope Belt** — CON 4 / 0.3 / 0.5
**Rare — Warrior Belt** — CON 10 / 0.8 / 1.3, +STR 6 / 0.4 / 0.8, +HealthRegen 1, 1 socket
**Epic — Plated Belt** — CON 14 / 1.0 / 1.7, +STR 8 / 0.6 / 1.0, +HealthRegen 2, +LifeSteal 1%, 1 socket
**Legendary — Dragonbone Belt** — CON 18 / 1.2 / 2.0, +STR 10 / 0.8 / 1.3, +HealthRegen 3, +LifeSteal 2%, +DefenseAmount 4, 2 sockets, passive: "On kill: +2% max HP (capped at 30% bonus)"
**Mythic — Titan Girdle** — CON 22 / 1.4 / 2.4, +STR 12 / 1.0 / 1.6, +HealthRegen 4, +LifeSteal 3%, +DefenseAmount 6, +Evasion 2%, 2 sockets, passive: "While above 80% HP: +10% AttackDamage"
**Ancient — Girdle of the War God** — CON 26 / 1.6 / 2.8, +STR 15 / 1.2 / 1.9, +HealthRegen 5, +LifeSteal 4%, +DefenseAmount 8, +Evasion 3%, +StuntChance 3%, 3 sockets, strong passive: "On hit taken: 20% chance to gain 10% AttackSpeed for 3s"
**Divine — Infinite Waist** — CON 32 / 1.9 / 3.3, +STR 18 / 1.4 / 2.2, +HealthRegen 6, +LifeSteal 5%, +DefenseAmount 10, +Evasion 4%, +StuntChance 5%, +CriticalDamage 3%, 3 sockets, unique passive: "Unbreakable — below 50% HP, take 30% less damage", FX: golden chain aura

### Cape / Pants / Pendant / Earring / Bracelet / Shoes

Follow the same ladder with their identities:
- **Cape** (DEX+INT): AttackSpeed, Evasion, UltimateAttack
- **Pants** (CON+DEX): HealthRegen, Evasion, AttackSpeed
- **Pendant** (STR+INT): UltimateAttack, ElementMastery, ManaPoint, ManaRegen
- **Earring** (DEX): AttackSpeed, MultiShootChance, MultiShootCount
- **Bracelet** (STR+DEX): LifeSteal, AttackSpeed, CriticalDamage
- **Shoes** (DEX): AttackSpeed, Evasion, StuntChance

---

## 12. Progression Philosophy (Player Journey)

1. **Level 1–20 (Common→Uncommon):** learn attributes exist. Items = "which 2 attributes do I want?" — the answer trains the player to think in CON/STR/INT/DEX.
2. **Level 20–40 (Rare→Epic):** sockets appear. Gems teach that combat stats come from *investment* (gems, not drops).
3. **Level 40–60 (Epic→Legendary):** passives appear. Builds become identifiable ("I'm a crit ring build").
4. **Level 60+ (Mythic→Divine):** hybrid attributes, strong/unique passives. Item identity = character identity. Trading becomes meaningful because items are build-defining, not number-barrels.
5. **Endgame:** set bonuses, 2+2 mixing, gem optimization, drop-farming for *exact* passive+stat combos. The chase is never "a bigger number".

---

## 13. Expansion Paths (without breaking attributes)

The rule: **every expansion converts to attributes or consumes them — never bypasses.**

- **Relics** — a 6th "overlay" slot above the 11; contribute CON/STR/INT/DEX directly. Designed to make a weak attribute playable late (e.g., an INT relic for a STR build — cross-build fixes).
- **Artifacts** — 3 slots that grant ONE strong secondary each (the 20% side only). Never attributes — keeps attribute share sacred; artifacts are the "spend your secondaries here" endgame sink.
- **Runes** — socket-adjacent: embed in any socket, grant attribute% rather than flat (scales with character, not with item). Attribute% is the only safe multiplier.
- **Pets** — passive stat aura + one click-ability; pets convert *your* attribute thresholds into effects (e.g., "while CON > 100: +5% Defense"). Never give flat attack.
- **Titles** — cosmetic + small attribute% or attribute-dip (equip-specific: "+2% INT while wearing a Hat"). Titles are the identity-defining layer.
- **Wings** — the one slot allowed *flat* secondary stats (AttackSpeed/Evasion) because they're cosmetic-first; cap contribution so they never become mandatory.

**Safety rule for all expansions:** any new system must reference attributes or the attribute pipeline (AttributeModifierManager), or stay strictly within the 20% secondary budget. A system that adds a third power source breaks the 80/20 balance forever.

---

## 14. Implementation Notes (this repo)

- Attribute pipeline already exists: `EquipmentData.AttributeStats` → `EquipmentStatCalculator.GetTotalAttributeBonuses` → `AttributeModifierManager.Apply()` → `ModifierSource.AccountLevel`.
- Secondary pipeline: `EquipmentData.CombatStats` (the list stat curve on the item) → `MainStatMappingExtensions.ToSkillType()` → `ModifierSource.Equipment`. `MainStats` was renamed because the 4 basic attributes are the true "main" stats — combat stats are secondaries.
- Affix pipeline: `dataItems.json → Affixes` → `ItemDatabase.AllAffixes` → `AffixGenerator.GenerateAffixes` (CustomData `["Affixes"]`) → `CreateStatModifiers` (`ModifierSource.Equipment`). Same for `["SecondaryStats"]` (the generation-rolled secondaries from `StatRollService`). Both survive save/load via `CustomDataConverter`.
- `MaxSockets` on `ItemData` + `SocketConfigData` gates rarity socket counts — enforce in `ItemGenerator` (rarity → socket count), not per-item hand-authoring.
- Passive tiers map to existing `SpecialEffectEntry` / `PassiveSkillEntry` / `EffectFactory`.
- Auto-equip scoring (#10) replaces the current simple sum in `EquipmentAutoEquipService` — attributes ×4 weight, secondaries ×1, plus socket/passive/set terms.
- JSON rarity ladder: store `RarityMechanicConfig` (SocketCount/SecondaryCount/PassiveTier per rarity) in `dataItems.json` so tuning a rarity never touches code.

**Design constant:** the attribute system is the product. Equipment is the delivery vehicle.
