# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**IdleDefenseSurvival** — a 2D auto-shooter idle defense game built in **Unity 6000.3.18f1 (Unity 6)**. Inspired by "Wild Survival - Idle Defense" on Android.

**Core Mechanic:** Player stays at the center of the screen, auto-attacks enemies. Enemies spawn outside the player's attack range and move toward the player, stopping when they reach their own attack range. Player attacks can knock enemies back, restarting their approach.

**Tech Stack:**
- **2D Render Pipeline** (com.unity.feature.2d)
- **Input System** (com.unity.inputsystem 1.19.0)
- **UGUI** for UI (com.unity.ugui 2.0.0)
- **Physics2D** for movement and collisions
- **Test Framework** (com.unity.test-framework 1.6.0)
- **DOTween** (Demigiant) for animations
- **Newtonsoft.Json** for JSON serialization

## Unity Editor

- Open in Unity Hub by selecting the repository root folder.
- Target platform: **Standalone** (Windows), configurable in Project Settings.
- Main gameplay scene: `Assets/Scenes/MainGame.unity`
- Other scenes: `Bootstrap.unity`, `MainMenu.unity`, `CardCollection.unity`

## Commands

### Building the project

- **Unity Editor**: File → Build Settings → Build (or Build And Run)
- **Command-line**: `Unity.exe -quit -batchmode -buildTarget StandaloneWindows64 -projectPath .`

### Running tests

- **All tests**: Window → General → Test Runner → Run All (`Ctrl+Shift+R`)
- **Single test**: Open Test Runner, select test, click "Run"
- **CLI**: `Unity.exe -runTests -testPlatform EditMode -projectPath .`

## Code Conventions

Unity's C# conventions:
- **PascalCase** for public fields, methods, properties, types
- **camelCase with `_` prefix** for private fields
- `[SerializeField]` for Inspector-exposed private fields (never make them public)
- **One MonoBehaviour per file**, filename = class name exactly
- **Namespace**: `IdleDefenseSurvival.*` for runtime, `IdleDefenseSurvival.Editor.*` for editor scripts
- `[Tooltip("...")]` on all serialized fields

## Development Philosophy: Ponytail (YAGNI)

Before writing ANY code, apply the **6-rung ladder** of efficiency. Stop at the first rung that holds:

1. **Based on "Wild Survival - Idle Defense"** — check if the reference game solves this already
2. **Does this need to exist?** Apply YAGNI rigorously. Can the feature be avoided entirely?
3. **Stdlib does it?** Use .NET/C# built-ins first (System.*, Unity built-ins)
4. **Native Unity feature?** Unity provides 80% of what games need out-of-the-box
5. **Installed dependency?** Use existing packages (check Packages/manifest.json)
6. **Only then:** Build the minimum that works

**Why this matters:** Idle defense games are simple. Resist over-engineering. Write the shortest working code first; refactor only when profiling shows a bottleneck.

**Examples:**
- ❌ Don't build custom object pools until GC profiling shows pressure (Unity 6 is efficient)
- ✅ Use `Physics2D.OverlapCircle()` for attack range checks (not custom distance loops)
- ✅ Use `LineRenderer` for attack range visualization (not custom mesh generation)
- ✅ Use `ScriptableObject` for game data (not custom JSON parsers)
- ✅ Use `Vector2.MoveTowards()` for enemy movement (not custom lerping)

## Current Architecture

### Folder Structure

```
Assets/
  Art/
    Enemy/        — Enemy sprites (Thorn Golem, etc.)
    Player/       — Player sprite (BasePlayer.png)
    UI/           — UI sprites, fonts, icons
  Prefabs/
    Enemy.prefab  — Enemy prefab with EnemyAi component
    Bullet.prefab — Player projectile
    Tank.prefab   — Tank ultimate instance
    Bomb.prefab   — Bomb ultimate
    Root.prefab   — Root ultimate
    Fountain.prefab — Fountain ultimate
    Cloud.prefab  — Cloud ultimate
    Shockwave.prefab — Shockwave ultimate
    DamagePopup.prefab — Damage number popup
    HealthBar.prefab — Enemy health bar
    UpgradeButton.prefab — Upgrade button UI
  Resources/
    Data/         — JSON data files (dataPlayer, dataEnemy, dataWave, dataUltimate, dataCard)
    Art/
      Card/       — Card sprites
      Enemy/      — Enemy sprites (including Monsterpack)
    Enemies/      — Enemy prefabs (Basic)
    Reward/       — Reward UI prefabs
  Scenes/
    Bootstrap.unity   — Bootstrap scene (initializes services)
    MainMenu.unity    — Main menu scene
    MainGame.unity    — Main gameplay scene
    CardCollection.unity — Card collection UI scene
  Scripts/
    Core/           — Bootstrap, ServiceLocator, SceneLoader, SceneCleanupHandler, interfaces
    Camera/         — CameraFollow, BackgroundScaler
    Card/           — Card system (CardDatabase, CardRollService, CardUpgradeService, CardEquipmentService, CardManager, CardCollectionController)
    Controller/     — UI Controllers (MainMenuController, SettingsController, VictoryController, CardCollectionController, GameSpeedController)
    Data/           — All data classes (PlayerData, EnemyData, UpgradeData, CardData, DamageData, etc.)
    Economy/        — EconomyManager, CurrencyData
    Enemy/          — EnemyAi, EnemySpawner, EnemyHealthBarManager
    IdleReward/     — IdleRewardManager, IdleRewardUI
    Item/           — Items, ItemState, ItemClickManager
    Manager/        — All managers (GameManager, WaveManager, SaveManager, UpgradeManager, EconomyManager, UltimateManager, ProjectilePool, DamagePopupManager, etc.)
    Modifier/       — ModifierCalculator, ModifierManager
    Player/         — Player, PlayerStats, Projectile, StatLoader, TankInstance
    Reward/         — RewardData, RewardManager, RewardPopup, RewardSlot, CardRewardSlot
    UI/             — UI components (CardCollection, CurrencyDisplay, DamagePopup, EnemyHealthBar, Settings, Status, Upgrade)
    Ultimate/       — Ultimate system (UltimateManager, UltimateFactory, handlers for each ultimate: Void, Tank, Root, Bomb, Fountain, Cloud, Shockwave)
    VisualScripting/ — Visual scripting graphs (if any)
  Settings/         — Input Action assets, Render Pipeline assets
  InputSystem_Actions.inputactions — Input mappings (currently unused, game is idle)
```

### Core Systems

**Player (`Scripts/Player/Player.cs`)**
- Fixed at screen center (0, 0)
- Attributes: attack range, attack damage, attack speed, knockback force, health, regen, evasion, critical chance, multi-shoot, bounce, life steal, etc.
- Visual: dashed circle showing attack range (via `LineRenderer` or Gizmos)
- Auto-attacks enemies within range
- Handles player health, regeneration, death defy (immunity)
- Spawns tanks at attack range boundary
- Triggers ultimates (Void, Root, Fountain, Shockwave, Tank, Bomb, Cloud)

**Enemy (`Scripts/Enemy/EnemyAi.cs`)**
- Spawns outside player's attack range
- Moves toward player using `Vector2.MoveTowards()`
- Stops when within its own attack range to player
- Gets knocked back by player attacks → resumes movement after knockback ends
- Attributes: move speed, attack range, health, damage, evasion, element, role
- Separation behavior to avoid stacking (uses `Physics2D.OverlapCircle` with ContactFilter2D)
- Damage system with elemental multipliers, critical hits, knockback, stun
- Rewards: gold, gems (daily limit), meat, EXP
- Health bar managed by `EnemyHealthBarManager`

**EnemySpawner (`Scripts/Enemy/EnemySpawner.cs`)**
- Spawns enemies at random positions outside player's attack range
- Spawn position: random angle, distance > player attack range + buffer
- Uses timed spawning (interval decreases per wave)
- Loads enemy data from `Resources/Data/dataEnemy.json`
- Scales enemy stats by wave/tier difficulty
- Calculates rewards based on enemy stats and wave/tier

**WaveManager (`Scripts/Manager/WaveManager.cs`)**
- Central brain for wave-based gameplay
- Reads config from `dataWave.json`
- Flow: InterWave (10s) → ActiveWave (30s) → InterWave → ...
- Each wave increases difficulty up to maxWave (350)
- After wave 350, tier increases and wave resets to 1 (infinite progression)
- Difficulty multipliers: Health, Speed, Damage, SpawnInterval
- Tier multiplier adds additional scaling for infinite progression
- Tracks damage stats per wave per enemy
- Handles victory (tier complete) and defeat

**Projectile (`Scripts/Player/Projectile.cs`)**
- Pooled via `ProjectilePool` (pre-instantiated, reusable)
- Moves toward target using `Rigidbody2D`
- Supports: bounce (chain to nearby enemies), knockback, stun, critical hits (Critical, SuperCritical, UltraCritical), life steal, damage per range
- Geometric damage reduction on bounce (50% per bounce)
- Hits player or enemies based on owner

**Enemy Health Bar (`Scripts/UI/EnemyHealthBarManager.cs`)**
- Manages health bar pooling and display for enemies
- Updates health in real-time
- Shows above enemy with offset

### Game Mechanics

1. **Attack Range Visualization**
   - Draw dashed circle around player using `LineRenderer` (material set to dashed)
   - Radius = player's attack range
   - Updates if attack range increases via upgrades

2. **Enemy Spawning**
   - Spawn position: `player.position + Random.insideUnitCircle.normalized * (playerAttackRange + spawnBuffer)`
   - Ensure enemies spawn outside camera view or outside attack range
   - Instantiate from `enemyPrefab`

3. **Enemy AI States**
   - **Approaching**: `Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime)`
   - **Attacking**: Stop movement when `Vector2.Distance(transform.position, player.position) <= attackRange`
   - **Knockback**: Apply force via `Rigidbody2D.AddForce()`, resume approaching after knockback ends

4. **Player Auto-Attack**
   - Every `attackSpeed` seconds, find all enemies in attack range
   - Use `Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer)`
   - Deal damage + apply knockback to enemies
   - No manual aiming (idle game)

5. **Ultimate Abilities**
   - **Void**: Black hole that pulls enemies
   - **Tank**: Stationary turret at attack range boundary
   - **Root**: Immobilizes enemies in area
   - **Bomb**: Explosion on enemy death (chain reaction)
   - **Fountain**: Healing fountain for player
   - **Cloud**: Toxic cloud damaging enemies over time
   - **Shockwave**: Expanding wave damaging enemies
   - Managed by `UltimateManager` + `UltimateFactory` + individual handlers
   - Each has cooldown, chance, and active toggle

6. **Card System**
   - Cards provide stat bonuses (flat or percent)
   - Rarities: Common, Rare, Epic, Legendary, Mythic
   - Pity system for Epic/Legendary/Mythic
   - Cards can be equipped (max 5 slots)
   - Duplicates upgrade card level
   - Card collection UI in separate scene

7. **Wave System**
   - Wave difficulty = f(waveTier, waveNumber)
   - Max wave: 350 (after that, only spawn count increases)
   - Tier progression: complete wave 350 → Victory UI → return to Main Menu → select next tier
   - Use `ScriptableObject` for wave definitions (enemy count, types, spawn rate)

8. **Economy**
   - **Gold**: Main currency, earned from kills, used for upgrades
   - **Gem**: Premium currency, daily limit (20/day), used for unlocks/cards
   - **Meat**: Special resource, dropped by enemies
   - **EXP**: Permanent account EXP from enemy kills
   - `EconomyManager` singleton handles all currency operations
   - `SaveManager` handles persistence (JSON at `Application.persistentDataPath`)

### Data-Driven Design

- **ScriptableObject/JSON** for game balance data (enemy stats, upgrade costs, wave definitions, ultimate data, cards)
- **MonoBehaviour** only for runtime behavior tied to GameObjects
- **Pure C# classes** for backend systems (economy calculations, wave generators, damage formulas) — test these in EditMode tests without a scene

### Testing Strategy

- **EditMode tests** for pure logic: economy calculations, wave formulas, upgrade cost curves, damage formulas
- **PlayMode tests** for interactions between systems: spawning + defense placement, input → player action
- Test files go in `Assets/Scripts/<Domain>/Tests/` or root `Tests/` folder

### Input System

The project uses the **Unity Input System** package (not the legacy Input Manager). An Input Action Asset exists at `Assets/InputSystem_Actions.inputactions`. Wire up player actions via `PlayerInput` component or by generating a C# wrapper from the `.inputactions` asset.

### Scene Management

- Menu/main menu scene: `Scenes/MainMenu`
- Gameplay scene: `Scenes/MainGame`
- Card collection: `Scenes/CardCollection`
- Bootstrap: `Scenes/Bootstrap`
- Keep scene loading additive (load scenes on top of a persistent manager scene)

### Common Patterns for This Project Type

- **Object pooling**: Use for bullets, enemies, particle effects (avoid instantiate/destroy churn) — `ProjectilePool`, `DamagePopupPool`
- **Wave system**: Difficulty increases with wave tier level and wave number, max wave 350. After wave 350, only spawn count increases.
- **Economy**: `EconomyManager` singleton handles currency; use JSON data for upgrade/cost tables.
- **Defense placement**: Grid- or slot-based placement system; serialize tower positions as part of save state.
- **Save system**: Use `JsonUtility` or `Newtonsoft.Json` for persistent save data (player progress, unlocks, currency).

## Physics2D Usage

```csharp
// Attack range check (preferred over manual distance loops)
Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayerMask);

// Knockback
Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
Vector2 knockbackDir = (enemy.position - transform.position).normalized;
enemyRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
```

## Enemy Movement

```csharp
// Simple approach (no pathfinding needed for this game)
Vector2 direction = (playerPos - (Vector2)transform.position).normalized;
transform.position = Vector2.MoveTowards(transform.position, playerPos, moveSpeed * Time.deltaTime);
```

## Spawn Position

```csharp
// Random position outside attack range
Vector2 randomDir = Random.insideUnitCircle.normalized;
Vector2 spawnPos = (Vector2)player.position + randomDir * (playerAttackRange + spawnBuffer);
```

## Performance Notes

- **Object pooling**: Only add if profiling shows GC pressure (Unity 6 is efficient, YAGNI applies)
- **Physics layers**: Use layers to avoid unnecessary collision checks
- **Fixed spawn rate**: Don't spawn every frame; use timer-based spawning

## Reference Game

**Wild Survival - Idle Defense (Android)** — reference for game feel, progression, UI patterns. Check gameplay videos when unsure about mechanics.

## Key Files for Common Tasks

| Task | File(s) |
|------|---------|
| Player stats/upgrades | `Scripts/Player/Player.cs`, `Scripts/Manager/UpgradeManager.cs`, `Scripts/Data/PlayerData.cs` |
| Enemy behavior | `Scripts/Enemy/EnemyAi.cs`, `Scripts/Enemy/EnemySpawner.cs`, `Scripts/Data/EnemyData.cs` |
| Wave management | `Scripts/Manager/WaveManager.cs`, `Scripts/Data/WaveProgressData.cs` |
| Currency/Economy | `Scripts/Economy/EconomyManager.cs`, `Scripts/Manager/SaveManager.cs` |
| Ultimate abilities | `Scripts/Ultimate/UltimateManager.cs`, `Scripts/Ultimate/UltimateFactory.cs`, handlers in `Scripts/Ultimate/` |
| Projectiles | `Scripts/Player/Projectile.cs`, `Scripts/Manager/ProjectilePool.cs` |
| Card system | `Scripts/Card/*`, `Scripts/Data/CardData.cs` |
| Save/Load | `Scripts/Manager/SaveManager.cs` |
| Damage/Combat | `Scripts/Data/DamageData.cs`, `Scripts/Manager/DamagePopupManager.cs` |
| UI | `Scripts/UI/*`, `Scripts/Controller/*` |

## JSON Data Files (Resources/Data/)

| File | Purpose |
|------|---------|
| `dataPlayer.json` | Player base stats, upgrade costs, unlock costs |
| `dataEnemy.json` | Enemy types, stats, spawn weights, rewards |
| `dataWave.json` | Wave config: duration, multipliers, spawn intervals |
| `dataUltimate.json` | Ultimate abilities: cooldown, chance, active state |
| `dataCard.json` | Card definitions: rarity, stats, scaling |

## Constants

See `Scripts/Core/GameConstants.cs` for shared constants like `MAX_WAVE_PER_TIER = 350`.