# Player System Design — IdleDefenseSurvival

**Purpose:** Player character behavior, stats, auto-attack, shielding, movement, ultimates, and UI façade.

**Last Updated:** 2026-10-06

---

## Related Design Documents

- [Modifier_Design.md](./Modifier_Design.md) — stat calculation pipeline
- [Attribute_Design.md](./Attribute_Design.md) — CON/STR/INT/DEX contributions
- [Combat_Design.md](./Combat_Design.md) — damage calculation
- [Projectile_Design.md](./Projectile_Design.md) — attack delivery
- [Card_Design.md](./Card_Design.md) — card effects
- [Equipment_Design.md](./Equipment_Design.md) — equipment bonuses
- [Ultimate_Design.md](./Ultimate_Design.md) — ultimate abilities

---

## 1. Architecture Overview

The player is now a thin façade (`Player.cs`) that composes a set of dedicated controllers. Each controller owns a single responsibility and exposes only the data needed by other systems.

| Component | Owner | Key Responsibilities |
|---|---|---|
| `PlayerCombatController` | Player | Auto-attack loop, target selection, projectile spawning, multi-shoot, special card attacks |
| `PlayerVitalsController` | Player | Health / mana pools, regeneration, damage intake, healing, death/defy logic |
| `PlayerShieldController` | Player | Shield / guardian shield values, absorption, cooldown, visual update |
| `PlayerMovementController` | Player | Joystick-driven movement (used by Movement ultimate), mana cost handling |
| `PlayerUltimateController` | Player | Ultimate spawning, tank management, manual casting API |
| `PlayerUIController` | Player | HUD sync – health/mana bars, cooldown icons, attack-range ring |
| `PlayerEffectsView` | Player | Visual effect toggles only – barrier, ice, burn, etc. |
| `Player` (facade) | — | Provides singleton instance, forwards calls to the above controllers, ensures backward-compatible public API. |

All gameplay logic lives in these services; UI classes merely read state.

---

## 2. Base Stats

**Source:** `Assets/Resources/Data/dataPlayer.json`

```json
{
  "baseAttackDamage": 20.0,
  "baseAttackSpeed": 1.0,
  "baseAttackRange": 5.0,
  "baseHealthPoint": 100.0,
  "baseHealthRegen": 0.5,
  "baseManaPoint": 50.0,
  "baseManaRegen": 0.3,
  "baseDefenseAmount": 10.0,
  "baseCriticalChance": 5.0,
  "baseHitRate": 95.0,
  "baseEvasion": 0.0
}
```

**Loading:** `BaseStatLoader.cs` reads the JSON → populates `PlayerStatsManager`.

*Never hard-code base stats in any player script.*

---

## 3. Final Stats Pipeline

```
dataPlayer.json (base)                     
    ↓                                        
AttributeModifierManager (CON/STR/INT/DEX → secondary) 
    ↓                                        
CardRuntimeManager (equipped card effects) 
    ↓                                        
EquipmentModifierService (equipment/gems/sets) 
    ↓                                        
BuffManager (temporary buffs)              
    ↓                                        
ModifierCalculator.Calculate()              
    ↓                                        
PlayerStatsManager.GetFinalStat(statType)   
    ↓                                        
Controllers read via PlayerStatsManager    
```

Never compute stats directly in any controller; always query `PlayerStatsManager`.

---

## 4. Auto-Attack Flow (PlayerCombatController)

```csharp
void Update() {
    _attackTimer += Time.deltaTime;
    if (_attackTimer >= GetAttackInterval()) {
        _attackTimer = 0f;
        Attack();
    }
}

float GetAttackInterval() {
    float attackSpeed = PlayerStatsManager.Instance.GetStat(SkillType.AttackSpeed);
    return attackSpeed > 0f ? 1f / attackSpeed : float.MaxValue;
}
```

- Attack speed derived from final stat.
- `Attack()` queries enemies via `Physics2D.OverlapCircleAll` using the `Enemy` layer mask.
- Multi-shoot chance handled by `SkillType.MultiShootChance`.
- Projectile count derived from `SkillType.MultiShootCount` and available mana.
- Projectile spawning uses `ProjectilePool` (no per-frame `Instantiate`).
- Card-specific modifiers (e.g., `last_bullet`, `bullet_storm`, `infinite_arsenal`) are applied through `CardRuntimeManager`.

---

## 5. Vitals (PlayerVitalsController)

### 5.1 Health & Mana Pools

- `CurrentHealth`, `CurrentMana` stored locally; max values fetched from `PlayerStatsManager` (`SkillType.HealthPoint`, `SkillType.ManaPoint`).
- `ResetToMax()` sets both pools to their current maxima and notifies UI.

### 5.2 Regeneration

```csharp
while (_regenTimer >= 1f) {
    _regenTimer -= 1f;
    if (CurrentHealth < MaxHealth) Heal(PlayerStatsManager.Instance.GetStat(SkillType.HealthRegen));
    if (CurrentMana   < MaxMana)   GainMana(PlayerStatsManager.Instance.GetStat(SkillType.ManaRegen));
}
```

Regeneration halts when shield reports `IsUnregenerationActive()`.

### 5.3 Damage Intake

- Evade chance via `SkillType.Evasion`.
- Immunity from Angel card or temporary barrier (`_immune`).
- Defense reduction computed by `Utilityku.FinalDamage(raw, defense)`.
- Shield and guardian shield absorption applied before health loss.
- On lethal damage, `Die()` runs death-defy chance (`SkillType.DeathDefy`) and Angel revival before delegating defeat to `WaveManager`.

### 5.4 Healing & HoT

- `Heal(float amount, bool lifeSteal = false)` caps at `MaxHealth` and dispatches `CardRuntimeManager` events.
- Over-time heals use coroutines (`HealRoutine`).

---

## 6. Shield System (PlayerShieldController)

- Tracks current shield (`_currentShield`) and guardian shield (`_guardianShield`).
- Shield granted when `CurrentHealth >= MaxHealth` and not already granted; amount = `HealthPoint * CardModifierService.GetEffectResult(CardEffectType.Shield, 0f)`.
- Absorption methods return amount absorbed and trigger cooldown when depleted.
- Visual scaling and color interpolation handled in `RefreshVisual()`.
- `IsUnregenerationActive()` proxies `PlayerStatusEffectManager.IsUnregenerationActive` to Vitals.

---

## 7. Movement (PlayerMovementController)

- Enabled only when a joystick asset is assigned (used for **Movement** ultimate).
- Reads `Joystick.joyStickVec` each `FixedUpdate`.
- Applies speed from `SkillType.MoveSpeed` to `Rigidbody2D.velocity`.
- Ultimate movement costs mana per second (`UltimateManager.TryGetUltimate("Movement", out var ultimate)`).
- Accumulates fractional mana cost; spends whole mana when enough accumulated.
- Stops movement and resets accumulator when mana insufficient.

*Note:* Standard idle-defense gameplay disables movement; this controller is activated solely by the **Movement** ultimate.

---

## 8. Ultimate Management (PlayerUltimateController)

- Auto-spawns non-manual ultimates each frame via `UltimateManager.TrySpawn` (Void, Root, Fountain, Shockwave).
- Manual casting via `ManualCastUltimate(string id)`.
- Tank spawning logic ensures new tanks do not overlap existing ones; uses `PlayerStatsManager.GetStat(SkillType.AttackRange)` for positioning.

---

## 9. UI Synchronization (PlayerUIController)

- Refreshes health/mana bars, shield cooldown, status effect icons, attack-range ring, and card-bonus UI.
- `RefreshAll()` called after `Vitals.ResetToMax()` and on stat reload.
- `DrawAttackRange(float range)` scales aura visual to match `SkillType.AttackRange`.
- Card-bonus icons driven by `CardModifierService` queries.
- All UI updates are **read-only**; no gameplay logic resides here.

---

## 10. Visual Effects (PlayerEffectsView)

- Pure visual toggles – barrier, ice, burn, berserker, vampire.
- No state changes; controllers call the appropriate `SetX(bool)` methods.

---

## 11. Player Facade (`Player.cs`)

- Enforces singleton pattern.
- Instantiates and configures all sub-controllers in `Awake()`.
- Provides backward-compatible public API (e.g., `TakeDamage`, `Heal`, `SpendMana`).
- Orchestrates initialization sequence after save load:
  1. Wait for `BootstrapController`.
  2. Wait for `PlayerStatsManager` & `BaseStatLoader`.
  3. Load base stats, compute `AttackRange`, reset vitals.
  4. Initialize combat, movement, ultimate, shield.
  5. Refresh card modifiers and UI.
- Listens to `CardModifierService.OnModifierChanged` and `CardRuntimeManager.OnBehaviorsUpdated` to keep UI current.

---

## 12. Performance Notes

- Player is a singleton – one instance per scene.
- No per-frame allocations: target list built with LINQ only in `FindTargets()` (acceptable as it runs per attack, not per frame).
- Cached references to `PlayerStatsManager`, `ProjectilePool`, and layer masks.
- Shield visual updates avoid allocation by reusing `Vector3` and `Color` structs.
- Movement uses `Rigidbody2D.velocity` (no `AddForce`).

---

## 13. Testing Checklist (EditMode)

```
[ ] Player spawns at arena centre
[ ] Auto-attack interval matches AttackSpeed stat
[ ] Attack range matches aura visual
[ ] Damage reduces HP correctly
[ ] Health regen restores HP over time
[ ] DeathDefy chance works
[ ] Mana regen restores Mana over time
[ ] MultiShoot triggers per chance
[ ] Shield grants, absorbs, and cools down correctly
[ ] Movement ultimate consumes mana and respects joystick input
[ ] UI updates reflect all state changes
[ ] Save/load persists health, mana, shield, and position
[ ] Events fire correctly (OnDamageTaken, OnPlayerDeath, etc.)
```

---

## 14. Future Extensions

- **Player Movement:** expand joystick control to full avatar movement (requires redesign of idle-defense premise).
- **Manual Aiming:** mouse-directed attacks, target lock-on.
- **Active Skills:** expose non-ultimate abilities via UI.
- **Weapon Switching:** multiple weapon types with distinct projectile behaviours.

---

## 15. Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-10-06 | Split player logic into dedicated controllers; added movement ultimate, shield cooldown, UI updates | Align with recent architecture refactor |
| 2026-09-16 | Initial design doc | Documentation refactor |

---

## 16. Architectural Rule

`Player` remains a thin façade; all gameplay rules live in the dedicated controller/services listed above.