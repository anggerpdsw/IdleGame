# TASK: IMPLEMENT PRODUCTION-READY AAA DYNAMIC EVENT SYSTEM

You are a senior Unity AAA gameplay architect and C# engineer.

You are working on an existing Unity game project named:

**IdleDefenseSurvival**

Your task is to implement a complete, production-ready, data-driven **Dynamic Event System** that integrates into the existing game architecture without breaking existing gameplay.

The Event System must feel like a real temporary gameplay mode, NOT merely:

* a collection of missions,
* a temporary currency,
* a reward shop,
* or a set of enemy stat multipliers.

The event must dynamically alter gameplay, create player decisions, escalate based on player behavior, and produce meaningful event-specific progression.

---

# 1. CRITICAL PROJECT RULES

These rules are mandatory.

## 1.1 DO NOT break existing gameplay

Existing gameplay must continue working exactly as before when no event is active.

Do NOT modify existing gameplay behavior unnecessarily.

Do NOT redesign:

* Player movement
* Enemy AI
* Enemy targeting
* Weapon system
* Card system
* Pet system
* Wave progression
* Crafting
* Equipment
* Potion
* Economy
* Mission behavior
* Drop behavior

unless an integration hook is strictly required.

If an existing method must be modified, make the smallest possible backward-compatible change.

---

# 2. EVENT MUST NOT REQUIRE A NEW SCENE

Do NOT create a dedicated Unity scene for normal events.

All standard events must run inside the existing gameplay scene.

The architecture must be:

```text
Existing Gameplay Scene
        |
        +-- Player
        +-- Enemy Systems
        +-- WaveManager
        +-- MissionService
        +-- EconomyManager
        +-- DropBagManager
        +-- RewardManager
        |
        +-- EventService
              |
              +-- EventDefinition
              +-- EventRuntimeState
              +-- EventRules
              +-- EventObjective
              +-- EventThreat
              +-- EventChoice
              +-- EventEscalation
              +-- EventBoss
              +-- EventReward
```

The Event System is a gameplay orchestration/modifier layer.

Only future events that require fundamentally different gameplay may use a separate scene.

Do NOT create a new scene for:

* Abyss Awakens
* Blood Moon
* Boss Hunt
* Predator Night
* Enemy Mutation
* Last Fortress
* similar events that use the existing combat/wave loop.

---

# 3. PRIMARY DESIGN GOAL

The event gameplay loop must be:

```text
EVENT START
    ↓
PLAYER ENTERS EXISTING GAMEPLAY
    ↓
EVENT RULES BECOME ACTIVE
    ↓
PLAYER PLAYS NORMAL WAVES
    ↓
EVENT OBJECTIVES / INCIDENTS APPEAR
    ↓
PLAYER MAKES A CHOICE
    ↓
EVENT STATE CHANGES
    ↓
THREAT / ESCALATION CHANGES
    ↓
GAMEPLAY BECOMES MORE DANGEROUS
    ↓
HIGHER RISK = HIGHER REWARD
    ↓
EVENT BOSS / FINAL INCIDENT
    ↓
EVENT SCORE
    ↓
REWARDS / RELICS / PET PROGRESS / COLLECTION
```

The event must create meaningful decisions.

The player should sometimes think:

> "Do I play safely, or do I intentionally increase the risk for a better reward?"

---

# 4. FIRST IMPLEMENTATION: THE ABYSS AWAKENS

Implement the first production event:

## Event ID

```text
abyss_awakens
```

Display name:

```text
The Abyss Awakens
```

Fantasy:

An unstable dimensional rift has appeared around the battlefield.

The player can:

* stabilize it,
* harvest it,
* or feed it.

Each decision changes the event state.

---

# 5. EVENT THREAT SYSTEM

Create a runtime Threat value.

Range:

```text
0 - 100
```

Threat must be clamped.

Example:

```text
0–25   = Stable
26–50  = Unstable
51–75  = Dangerous
76–90  = Critical
91–99  = Catastrophic
100    = Abyss Collapse
```

Threat must be persisted.

Threat must survive:

* scene reload,
* application restart,
* save/load,
* mid-event resume.

---

# 6. THREAT CHANGES

Use configurable data values.

Default behavior:

```text
Void Enemy Kill       +1
Elite Kill            +5
Boss Kill             +15

Seal Rift             -20
Harvest Rift          +10
Feed Rift             +25

Failed Objective      +15
```

Do NOT hard-code these values inside gameplay code.

All balancing values must come from `dataEvent.json`.

---

# 7. THREAT ESCALATION

Threat must dynamically affect gameplay.

Default configuration:

### Threat < 25

Normal event state.

### Threat >= 25

Increase event-specific Void activity.

### Threat >= 50

Enable stronger Void enemy behavior/spawn weighting.

### Threat >= 75

Enable mutated/elite event enemies.

### Threat >= 90

Enable catastrophic event behavior.

### Threat == 100

Trigger:

```text
ABYSS COLLAPSE
```

Do not modify global player/enemy systems permanently.

All modifiers must be:

```text
event-scoped
temporary
reversible
```

When the event ends, every event modifier must be removed cleanly.

---

# 8. PLAYER CHOICES

Implement three event choices.

## SEAL

Effect:

```text
Threat -20
Reward Multiplier x0.8
Risk decreases
```

Purpose:

Safe strategy.

---

## HARVEST

Effect:

```text
Threat +10
Reward Multiplier x1.5
```

Purpose:

Risk/reward strategy.

---

## FEED

Effect:

```text
Threat +25
Reward Multiplier x2.0
```

Purpose:

High-risk strategy.

---

# 9. CHOICE UI

The player must receive a proper event decision UI.

Example:

```text
========================================

          THE ABYSS AWAKENS

An unstable Rift has appeared.

Choose what to do:

[ SEAL ]
Stabilize the Rift
Threat -20
Reward x0.8

[ HARVEST ]
Extract its energy
Threat +10
Reward x1.5

[ FEED ]
Feed the Rift
Threat +25
Reward x2.0

========================================
```

The UI must:

* pause decision-sensitive event logic if appropriate,
* prevent double-click execution,
* prevent duplicate rewards,
* clearly show consequences,
* animate state changes,
* work with keyboard/controller/touch if the existing project supports them.

Do not create a second gameplay scene for this UI.

---

# 10. EVENT INCIDENTS

Do NOT make the event purely passive.

Implement event incidents that appear during gameplay.

Examples:

## Rift Detected

```text
Abyss Rift detected.

Destroy the Rift before 3 waves pass.
```

## Mutated Elite

```text
A Mutated Elite has appeared.

Defeat it.
```

## Critical Rift

```text
The Rift is becoming unstable.

Survive 2 waves.
```

Incident generation must be data-driven.

Do not hard-code event-specific logic into WaveManager.

---

# 11. EVENT OBJECTIVES

The Event System must support multiple objective types.

Minimum architecture:

```text
SurviveWaves
KillEnemies
KillSpecificEnemy
KillElite
KillBoss
DestroyRift
ProtectObjective
ReachThreat
SurviveThreatLevel
```

The first implementation does not need every objective type to be used.

However, the architecture must support adding them without rewriting EventService.

---

# 12. EVENT BOSS

At Threat 100, trigger:

```text
ABYSS COLLAPSE
```

Then spawn the event boss:

```text
abyss_devourer
```

The boss must have state based on the achieved Threat.

Example:

```text
Threat < 50:
Normal

Threat 50–74:
Empowered

Threat 75–89:
Mutated

Threat 90–99:
Ascended

Threat 100:
Abyss Collapse version
```

Do not duplicate the entire Enemy AI system.

Reuse the existing enemy/boss architecture.

Only provide event-specific modifiers/data where possible.

---

# 13. ABYSS COLLAPSE

When Threat reaches 100:

Display:

```text
⚠ ABYSS COLLAPSE ⚠

The Rift has awakened.

SURVIVE.
```

For the Collapse phase:

```text
Enemy HP          +100%
Enemy Move Speed  +50%
Spawn Pressure    +30%
Reward Multiplier ×3
```

All values must be configurable.

Collapse should be a temporary state.

After the event ends:

```text
all modifiers removed
event state finalized
rewards calculated
```

---

# 14. EVENT SCORE

Implement an Event Score system.

Default values:

```text
Normal Kill        +1
Elite Kill         +10
Boss Kill          +100
Rift Destroyed     +50
Rift Harvested     +75
Rift Survived      +100
Perfect Objective  +50
Abyss Collapse     +300
```

Final score:

```text
FinalScore =
BaseScore * RiskMultiplier
```

RiskMultiplier must be data-driven.

Do not use floating-point accumulation where an integer/long is appropriate.

Protect against overflow.

---

# 15. EVENT CURRENCY

Use the existing economy architecture.

Do NOT create a separate standalone currency manager.

The event currency can be:

```text
AbyssEssence
```

It must integrate with the existing EconomyManager.

The event currency must have:

* validation,
* transaction logging,
* overflow protection,
* save/load support,
* reward integration.

Do not bypass EconomyManager.

---

# 16. EVENT REWARDS

Rewards must use the existing reward pipeline wherever possible.

Possible rewards:

```text
Gold
Gem
Shard
Card Fragment
Pet Fragment
Event Material
Event Relic
```

Do not create a duplicate RewardManager.

---

# 17. EVENT RELICS

Implement event-exclusive relic definitions.

Initial relics:

## Abyss Fang

```text
+5% Damage
```

Additional event interaction:

```text
+1% damage while Threat >= 75%
```

---

## Void Heart

```text
+5% HP
```

---

## Rift Eye

```text
+5% Critical Chance
```

Do not directly hard-code these bonuses into EventService.

Relics must be represented as data/configuration compatible with the existing progression architecture.

---

# 18. RELIC SYNERGY

Support set-based synergy.

If player owns:

```text
Abyss Fang
Void Heart
Rift Eye
```

activate:

```text
ABYSS SET
```

Effect:

```text
Void enemies take +15% damage
```

This should be implemented in a reusable synergy architecture.

Do not create an Abyss-specific one-off conditional inside unrelated combat code.

---

# 19. PET INTEGRATION

The event should support Pet progression.

Create:

```text
Riftling
```

as an event Pet/progression target.

The exact implementation must reuse the existing Pet system.

If the Pet does not exist:

```text
Unlock Pet
```

If it already exists:

```text
Add duplicate progress
```

Do not create a separate event Pet inventory.

Do not duplicate PetManager.

---

# 20. EVENT COLLECTION / CODEX

Create support for:

```text
ABYSS CODEX
```

Possible entries:

```text
Abyss Stalker
Void Brute
Rift Mage
Mutated Elite
Abyss Devourer
Ascended Devourer
Abyss Collapse
```

Collection progress must be persistent.

Example:

```text
3 entries  → Event Chest
6 entries  → Event Relic
10 entries → Riftling Fragment
```

The collection system should be reusable for future events.

---

# 21. EVENT SHOP

Keep Event Shop functionality, but do not make it the core gameplay.

Use existing EconomyManager.

Example:

```text
ABYSS SHOP

COMBAT
Abyss Fang

SURVIVAL
Void Heart

CRITICAL
Rift Eye

PET
Riftling Fragment
```

Purchase limits must be persisted.

Event shop inventory must be data-driven.

Do not hard-code item prices.

---

# 22. EVENT CHEST

Create support for:

```text
Rift Chest
```

Possible rewards:

```text
Gold
Gem
Card Fragment
Pet Fragment
Abyss Essence
Event Relic
Event Material
```

Support pity.

Example:

```text
20 chests guarantees
Event Relic OR Pet Fragment
```

Pity progress must be saved.

Pity must reset only according to event definition.

---

# 23. EVENT ROTATION

Implement data-driven event scheduling.

Initial event pool:

```text
abyss_awakens
last_fortress
predator_night
blood_moon
rift_expedition
invasion_protocol
world_eater
```

Only implement complete gameplay for:

```text
abyss_awakens
```

For the others, create extensible definition support/stubs only if necessary.

Do not fake unfinished gameplay as production-ready.

---

# 24. EVENT SCHEDULE

Support:

```text
Start UTC
End UTC
Event ID
Rotation ID
```

The client must calculate active event state safely.

Do not rely exclusively on local device time for competitive/reward-sensitive logic.

If the existing project has a trusted time abstraction, reuse it.

Do not introduce a second time system.

---

# 25. SAVE SYSTEM

Integrate with the existing SaveManager.

Add versioned EventSaveData.

Minimum state:

```csharp
EventSaveData
{
    activeEventId;
    eventEndsAt;
    threat;
    eventScore;
    riskMultiplier;
    eventCurrency;
    objectiveProgress;
    objectiveClaimed;
    milestoneProgress;
    milestoneClaimed;
    choiceHistory;
    shopPurchases;
    codexEntries;
    chestPity;
    completedEventIds;
}
```

Use the existing save architecture.

Add migration.

Fresh saves must work.

Existing saves must continue loading.

Never assume event data exists in older save versions.

---

# 26. EVENT LIFECYCLE

Implement explicit lifecycle:

```text
Inactive
    ↓
Scheduled
    ↓
Active
    ↓
Running
    ↓
Escalated
    ↓
Collapse
    ↓
Completed
    ↓
Expired
```

Not every event must use every state.

But the architecture must support them.

Events must be idempotent.

Calling:

```text
StartEvent()
```

twice must not duplicate state.

Calling:

```text
EndEvent()
```

twice must not duplicate rewards.

---

# 27. EVENT SERVICE API

Design a clean interface similar to:

```csharp
public interface IEventService
{
    bool IsEventActive { get; }

    EventDefinition CurrentEvent { get; }

    EventRuntimeState RuntimeState { get; }

    void Initialize();

    void StartEvent(string eventId);

    void EndEvent();

    void RegisterKill(...);

    void RegisterEliteKill(...);

    void RegisterBossKill(...);

    void RegisterWaveCompleted(...);

    void RegisterObjectiveProgress(...);

    void RegisterChoice(string choiceId);

    void AddThreat(int amount);

    void RemoveThreat(int amount);

    bool TryClaimReward(string rewardId);

    bool TryPurchase(string itemId);
}
```

Adapt method signatures to the existing project conventions.

Do NOT blindly copy this interface if the existing architecture uses a better abstraction.

---

# 28. EVENT MODIFIER ARCHITECTURE

Avoid this anti-pattern:

```csharp
if (eventId == "abyss_awakens")
{
    ...
}
```

Do not scatter event IDs throughout the project.

Prefer:

```text
EventDefinition
      ↓
EventRule
      ↓
EventRuntimeState
      ↓
Event Modifier
```

Event-specific behavior must be data-driven whenever practical.

---

# 29. DATA-DRIVEN DESIGN

Create:

```text
Assets/Resources/Data/Event/dataEvent.json
```

The JSON must define:

```text
eventId
displayName
description
schedule
rules
threat
choices
objectives
escalation
boss
score
currency
rewards
shop
codex
chest
pet
relics
```

Use the existing JSON loading/cache architecture.

Do not create a second JSON loading framework.

---

# 30. PROPOSED FOLDER STRUCTURE

Use the project's existing conventions, but target something similar to:

```text
Scripts/
    Event/
        IEventService.cs
        EventService.cs
        EventDefinition.cs
        EventRuntimeState.cs
        EventSchedule.cs
        EventRule.cs
        EventObjective.cs
        EventChoice.cs
        EventEscalation.cs
        EventReward.cs
        EventModifier.cs
        EventIncident.cs

Scripts/UI/Event/
    EventHUD.cs
    EventThreatBar.cs
    EventObjectiveUI.cs
    EventChoiceUI.cs
    EventIncidentUI.cs
    EventResultUI.cs
    EventShopUI.cs
    EventCodexUI.cs

Assets/Resources/Data/Event/
    dataEvent.json
```

Do not blindly create files if equivalent existing abstractions already exist.

Reuse existing architecture whenever possible.

---

# 31. SERVICE LOCATOR

If the project uses:

```text
ServiceLocator
```

register EventService consistently with existing services.

Do not introduce a competing dependency injection system.

Do not introduce another singleton pattern if the project already has an established service registration convention.

---

# 32. UNITY LIFECYCLE SAFETY

Handle:

* scene reload,
* scene transition,
* application pause,
* application resume,
* application quit,
* save/load,
* event expiration,
* event start while gameplay is active,
* event ending while gameplay is active.

No duplicate event listeners.

No duplicate subscriptions.

No dangling delegates.

No memory leaks.

No destroyed-object references.

---

# 33. EVENT MODIFIER CLEANUP

This is mandatory.

Every modifier applied by an event must have an inverse/cleanup path.

Example:

```text
Event starts
    ↓
Apply modifiers

Event ends
    ↓
Remove modifiers

Scene reload
    ↓
Reconstruct only active modifiers
```

Never leave event modifiers permanently applied after event expiration.

---

# 34. EVENT HOOKS

Integrate with existing systems through minimal hooks.

Examples:

```text
Enemy death
    ↓
EventService.RegisterKill()

Elite death
    ↓
EventService.RegisterEliteKill()

Boss death
    ↓
EventService.RegisterBossKill()

Wave completed
    ↓
EventService.RegisterWaveCompleted()
```

Do not duplicate enemy death processing.

Do not duplicate reward processing.

Do not duplicate mission processing.

---

# 35. MISSION INTEGRATION

Reuse existing MissionService where appropriate.

Event objectives may use MissionService infrastructure if compatible.

Do not create:

```text
EventMissionService
```

unless the existing MissionService genuinely cannot support the required lifecycle.

If extension is necessary, extend it minimally and preserve all existing behavior.

---

# 36. DROP INTEGRATION

Reuse:

```text
DropBagManager
DropTable
```

where possible.

Do not create:

```text
EventDropSystem
```

unless technically unavoidable.

Event drops must use the same validation and pickup pipeline as normal drops.

---

# 37. ECONOMY INTEGRATION

Reuse:

```text
EconomyManager
```

Do not directly modify player currency fields.

Every event transaction must go through the established economy transaction flow.

---

# 38. REWARD INTEGRATION

Reuse:

```text
RewardManager
```

for:

* milestone rewards,
* chest rewards,
* event completion rewards,
* boss rewards.

Do not create another reward popup system.

---

# 39. UI/UX REQUIREMENTS

The event UI must be polished and production-oriented.

Main event HUD:

```text
┌─────────────────────────────────────┐
│ THE ABYSS AWAKENS          02:41:18 │
│                                     │
│ THREAT                               │
│ ███████████████░░░░  76%             │
│                                     │
│ Current Objective                    │
│ Defeat the Mutated Elite             │
│                                     │
│ Event Score        x2.1 Risk         │
│ 12,480                              │
└─────────────────────────────────────┘
```

Use clear visual hierarchy.

Do not obstruct the combat area unnecessarily.

The HUD must be scalable for different aspect ratios.

Use existing UI framework/style conventions.

---

# 40. ACCESSIBILITY

Support where the existing game allows:

* readable text,
* sufficient contrast,
* controller navigation,
* keyboard navigation,
* touch interaction,
* scalable UI,
* no critical information communicated only through color.

Do not introduce inaccessible UI patterns.

---

# 41. AUDIO / VFX HOOKS

Provide event hooks for:

```text
OnEventStarted
OnThreatChanged
OnIncidentStarted
OnChoiceMade
OnThreatCritical
OnCollapseStarted
OnBossSpawned
OnEventCompleted
```

These should allow Audio/VFX systems to react without hard coupling.

Do not require custom VFX implementation if the project does not currently have the required assets.

Create clean hooks/placeholders.

---

# 42. PERFORMANCE REQUIREMENTS

The Event System must be lightweight.

Do NOT:

* allocate garbage every frame,
* LINQ repeatedly inside Update loops,
* create/destroy unnecessary GameObjects,
* poll every event component every frame,
* perform expensive JSON parsing during combat,
* scan all enemies unnecessarily.

Prefer event-driven updates.

For example:

```text
EnemyKilled
WaveCompleted
ObjectiveUpdated
ThreatChanged
```

rather than:

```text
Update()
{
    FindAllEnemies();
    CheckEverything();
}
```

---

# 43. NO GOD OBJECT

Do not put all functionality into:

```text
EventService.cs
```

EventService should orchestrate.

Separate:

```text
Scheduling
Runtime State
Threat
Objectives
Choices
Escalation
Rewards
Persistence
```

where complexity justifies it.

Avoid overengineering trivial abstractions.

---

# 44. ERROR HANDLING

Gracefully handle:

* missing event definition,
* malformed JSON,
* missing reward,
* missing item,
* missing Pet,
* missing Relic,
* expired event,
* corrupted event state,
* invalid choice,
* duplicate claim,
* duplicate purchase,
* save migration failure.

The game must not crash because of event content.

---

# 45. ANTI-DUPLICATION

Never allow:

```text
double reward
double purchase
double event start
double event completion
double listener registration
```

Use server-like idempotency principles even if the game is currently offline.

---

# 46. DEBUG TOOLS

Add development-only debug functionality.

Example:

```text
Force Start Event
Force End Event
Set Threat
Add Event Currency
Trigger Incident
Trigger Collapse
Spawn Event Boss
Reset Event
```

These must be disabled or inaccessible in production builds.

Do not pollute normal player UI.

---

# 47. TESTING

Create tests for at least:

## Threat

```text
AddThreat()
RemoveThreat()
ClampThreat()
Threat escalation
Threat 100 transition
```

## Choices

```text
Seal
Harvest
Feed
Duplicate choice prevention
Invalid choice
```

## Save

```text
Save active event
Load active event
Load old save without EventSaveData
Event expiration after reload
```

## Rewards

```text
Claim once
Cannot claim twice
Expired event
Missing reward
```

## Shop

```text
Purchase
Insufficient currency
Purchase limit
Duplicate purchase
```

## Lifecycle

```text
Start once
End once
Reload scene
Application pause
Application resume
```

## Modifier cleanup

```text
Event starts → modifier applied
Event ends → modifier removed
```

---

# 48. BACKWARD COMPATIBILITY

This is mandatory.

After implementation:

```text
Normal gameplay with no event
```

must behave exactly as before.

Existing save files must remain loadable.

Existing cards must behave exactly as before.

Existing pets must behave exactly as before.

Existing crafting must behave exactly as before.

Existing missions must behave exactly as before.

Existing wave progression must behave exactly as before.

---

# 49. DO NOT MAKE THESE MISTAKES

Do NOT:

```text
Create EventManager + EventService + EventController
all doing the same thing.

Create separate currency manager.

Create separate reward manager.

Create separate drop system.

Create separate mission system.

Create a scene for every event.

Hard-code event IDs everywhere.

Put event logic inside Player.

Put event logic inside EnemyAi.

Put event logic inside every weapon.

Put event logic inside PetManager.

Use Update() for all event logic.

Rewrite existing systems unnecessarily.
```

---

# 50. IMPLEMENTATION STRATEGY

Before writing code:

1. Inspect the entire existing project architecture.
2. Locate:

   * ServiceLocator
   * MissionService
   * WaveManager
   * EconomyManager
   * DropBagManager
   * RewardManager
   * SaveManager
   * JSON/database cache
   * Player
   * EnemyAi
   * PetManager
   * UI architecture
3. Identify existing interfaces and extension points.
4. Reuse them.
5. Do not assume class names or APIs if the project contains different implementations.

Then implement in this order:

```text
PHASE 1
Event data model

PHASE 2
Event JSON loading

PHASE 3
Event runtime state

PHASE 4
EventService

PHASE 5
Threat system

PHASE 6
Event choice system

PHASE 7
Objective/incident system

PHASE 8
Wave/enemy integration

PHASE 9
Event boss integration

PHASE 10
Economy/reward integration

PHASE 11
Save migration

PHASE 12
UI

PHASE 13
Relic/Pet/Collection integration

PHASE 14
Debug tools

PHASE 15
Automated tests

PHASE 16
Production cleanup
```

---

# 51. REQUIRED DEVELOPMENT BEHAVIOR

Do not simply generate code and stop.

For every modification:

1. Inspect existing implementation.
2. Explain why the modification is required.
3. Make the smallest compatible change.
4. Compile/check for errors.
5. Check references.
6. Check null safety.
7. Check save compatibility.
8. Check event cleanup.
9. Check duplicate execution.
10. Check normal gameplay regression.

If an existing architecture already provides a suitable solution, reuse it instead of creating another abstraction.

---

# 52. OUTPUT REQUIREMENTS

At the end provide:

## A. Architecture Summary

Explain the final Event architecture.

## B. Files Created

List every new file.

## C. Files Modified

List every existing file modified.

For each:

```text
File
Reason
Behavior impact
```

## D. Data Schema

Show the final `dataEvent.json` schema.

## E. Save Migration

Explain:

```text
old version
→ migration
→ new version
```

## F. Integration Points

Show exactly where EventService connects to:

```text
WaveManager
Enemy
MissionService
EconomyManager
DropBagManager
RewardManager
SaveManager
PetManager
UI
```

## G. Testing

List all tests created and their results.

## H. Regression Verification

Explicitly confirm:

```text
Normal gameplay unchanged
Existing save compatible
Existing cards unchanged
Existing pets unchanged
Existing crafting unchanged
Existing missions unchanged
Existing waves unchanged
```

If any of these cannot be verified, state exactly why.

---

# 53. DEFINITION OF DONE

The task is NOT complete until all of these are true:

```text
[ ] EventService implemented
[ ] Event data is JSON-driven
[ ] No dedicated scene required for normal events
[ ] Abyss Awakens playable
[ ] Threat system working
[ ] Threat escalation working
[ ] Player choices working
[ ] Event incidents working
[ ] Event objectives working
[ ] Abyss Collapse working
[ ] Event boss working
[ ] Event score working
[ ] Event currency integrated
[ ] Event rewards integrated
[ ] Event relics supported
[ ] Relic synergy supported
[ ] Pet progression integrated
[ ] Event Codex supported
[ ] Event Shop supported
[ ] Event Chest supported
[ ] Pity supported
[ ] Save/load supported
[ ] Save migration supported
[ ] Event expiration handled
[ ] Event modifier cleanup handled
[ ] Duplicate reward prevention handled
[ ] Duplicate purchase prevention handled
[ ] Debug tools implemented
[ ] Automated tests implemented
[ ] No known compile errors
[ ] No known null-reference errors
[ ] No unnecessary Update polling
[ ] Existing gameplay behavior preserved
[ ] Existing save compatibility preserved
```

---

# 54. MOST IMPORTANT PRINCIPLE

The final implementation should make the player feel:

> "This is a special gameplay event."

NOT:

> "The game has a temporary mission and shop."

The Event System must therefore prioritize:

```text
PLAYER DECISION
        ↓
CONSEQUENCE
        ↓
ESCALATION
        ↓
RISK
        ↓
REWARD
        ↓
LONG-TERM PROGRESSION
```

while remaining technically:

```text
DATA-DRIVEN
MODULAR
PERFORMANT
TESTABLE
SAVE-SAFE
BACKWARD-COMPATIBLE
EXTENSIBLE
PRODUCTION-READY
```

Do not sacrifice existing gameplay stability for event functionality.

Do not rewrite working systems merely to make the Event System look architecturally cleaner.

Implement the Event System as a high-quality AAA production feature integrated into the existing IdleDefenseSurvival architecture.
