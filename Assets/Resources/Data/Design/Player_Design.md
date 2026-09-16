# Player System Design — IdleDefenseSurvival

**Purpose:** Player character behavior, stats, auto-attack mechanics, positioning, aura.

**Last Updated:** 2026-09-16

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

## 1. Player Identity

**Fixed position:** Player stays centered in arena. Enemies approach player.

**Auto-combat:** Player attacks automatically. No manual aiming/shooting.

**Stats-driven:** All combat behavior determined by final stats from pipeline.

**Key scripts:**
- `Scripts/Player/Player.cs` — main MonoBehaviour, attack loop
- `Scripts/Player/PlayerStats.cs` — stat container
- `Scripts/Manager/PlayerStatsManager.cs` — stat aggregation + cache
- `Scripts/Player/AuraCollider.cs` — visual aura ring

---

## 2. Base Stats

**Source:** `Assets/Resources/Data/dataPlayer.json`

**Schema example:**
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

**Loading:** `BaseStatLoader.cs` reads JSON → stores in memory → feeds to `PlayerStatsManager`.

**Do NOT hardcode base stats in Player.cs.**

---

## 3. Final Stats

**Pipeline:**
```
dataPlayer.json (base)
    ↓
AttributeModifierManager (CON/STR/INT/DEX → secondary stats)
    ↓
CardModifierService (equipped cards)
    ↓
EquipmentModifierService (equipment + gems + sets + effects)
    ↓
BuffManager (temporary buffs)
    ↓
ModifierCalculator.Calculate()
    ↓
PlayerStatsManager.GetFinalStat(statType)
    ↓
Player.cs reads final values
```

**Never compute stats in Player.cs.** Always read from `PlayerStatsManager`.

---

## 4. Auto-Attack Flow

**Attack loop (simplified):**

```csharp
void Update()
{
    _attackTimer += Time.deltaTime;
    
    if (_attackTimer >= GetAttackInterval())
    {
        _attackTimer = 0f;
        PerformAttack();
    }
}

float GetAttackInterval()
{
    float attackSpeed = PlayerStatsManager.GetFinalStat(AttackSpeed);
    return 1f / attackSpeed;  // seconds between attacks
}

void PerformAttack()
{
    // 1. Find enemies in range
    Collider2D[] enemies = Physics2D.OverlapCircleAll(
        transform.position,
        GetAttackRange(),
        _enemyLayerMask
    );
    
    if (enemies.Length == 0) return;
    
    // 2. Select target (usually closest)
    Transform target = SelectTarget(enemies);
    
    // 3. Roll MultiShoot
    int projectileCount = RollMultiShoot() ? GetMultiShootCount() : 1;
    
    // 4. Spawn projectile(s)
    for (int i = 0; i < projectileCount; i++)
    {
        SpawnProjectile(target);
    }
}
```

**Attack speed:**
- AttackSpeed = 1.0 → 1 attack/second
- AttackSpeed = 2.0 → 2 attacks/second
- AttackSpeed = 0.5 → 0.5 attacks/second

**Attack range:**
- Radius around player position
- Visualized by aura ring

---

## 5. Target Selection

**Default:** Closest enemy within range.

```csharp
Transform SelectTarget(Collider2D[] enemies)
{
    Transform closest = null;
    float minDistance = float.MaxValue;
    
    foreach (var col in enemies)
    {
        float dist = Vector2.Distance(transform.position, col.transform.position);
        if (dist < minDistance)
        {
            minDistance = dist;
            closest = col.transform;
        }
    }
    
    return closest;
}
```

**Future extensions:**
- Prioritize low-HP enemies
- Prioritize bosses
- Prioritize nearest to player

---

## 6. MultiShoot

**Roll chance each attack:**

```csharp
bool RollMultiShoot()
{
    float chance = PlayerStatsManager.GetFinalStat(MultiShootChance);
    return Random.Range(0f, 100f) < chance;
}
```

**If triggered:** spawn `MultiShootCount` projectiles instead of 1.

**Example:**
- MultiShootChance: 20%
- MultiShootCount: 3
- Result: 20% chance to fire 3 projectiles, 80% fire 1

**Note:** Each projectile targets independently (may hit same or different enemies).

---

## 7. Aura Visualization

**Purpose:** Show player's attack range.

**Component:** `Scripts/Player/AuraCollider.cs`

**Behavior:**
- Circle sprite scaled to match AttackRange
- Follows player position
- Semi-transparent ring
- No gameplay collision (visual only)

**Sync:**
```csharp
void UpdateAuraSize()
{
    float range = PlayerStatsManager.GetFinalStat(AttackRange);
    transform.localScale = Vector3.one * (range * 2f);  // diameter = range * 2
}
```

**Call when:**
- Scene start
- Equipment change
- Card change
- Attribute change
- Any modifier source change

---

## 8. Health System

### 8.1 Health Pool

**Max HP:**
```
MaxHP = BaseHP + (CON × BonusPerPoint) + EquipmentFlat + CardFlat + ...
```

**Current HP:** tracked in `PlayerStats.CurrentHP`.

**Damage taken:**
```csharp
public void TakeDamage(float amount)
{
    CurrentHP -= amount;
    
    if (CurrentHP <= 0)
    {
        // Roll DeathDefy
        if (RollDeathDefy())
        {
            CurrentHP = MaxHP * 0.1f;  // survive with 10% HP
            return;
        }
        
        Die();
    }
    
    OnDamageTaken?.Invoke(amount);
}
```

### 8.2 Health Regen

**Passive regeneration:**

```csharp
void Update()
{
    float regenPerSecond = PlayerStatsManager.GetFinalStat(HealthRegen);
    CurrentHP += regenPerSecond * Time.deltaTime;
    CurrentHP = Mathf.Min(CurrentHP, MaxHP);  // cap at max
}
```

### 8.3 DeathDefy

**Last-chance survival mechanic:**

```csharp
bool RollDeathDefy()
{
    float chance = PlayerStatsManager.GetFinalStat(DeathDefy);
    return Random.Range(0f, 100f) < chance;
}
```

**Effect:** Survive lethal hit with 10% HP, consume DeathDefy proc.

**Cooldown:** May have internal cooldown (verify implementation).

---

## 9. Mana System

**Max Mana:**
```
MaxMana = BaseMana + (INT × BonusPerPoint) + EquipmentFlat + ...
```

**Current Mana:** tracked in `PlayerStats.CurrentMana`.

**Mana Regen:**
```csharp
void Update()
{
    float regenPerSecond = PlayerStatsManager.GetFinalStat(ManaRegen);
    CurrentMana += regenPerSecond * Time.deltaTime;
    CurrentMana = Mathf.Min(CurrentMana, MaxMana);
}
```

**Consumption:** Ultimate abilities consume mana (see `Ultimate_Design.md`).

---

## 10. Player Death

**Death flow:**

1. HP → 0 (DeathDefy failed)
2. Stop all auto-attacks
3. Disable movement (player is stationary anyway)
4. Play death animation (if exists)
5. Trigger `OnPlayerDeath` event
6. WaveManager handles defeat
7. Show defeat UI
8. Offer restart/quit

**No respawn in current design** — death ends the run.

---

## 11. Player Events

**Events fired by Player:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnDamageTaken` | Player takes damage | Health bar UI, damage popup |
| `OnPlayerDeath` | HP → 0 | WaveManager, GameController |
| `OnAttack` | Projectile spawned | Stats tracker (optional) |
| `OnCriticalHit` | Critical projectile | Visual effects, stats |

**Subscribe pattern:**
```csharp
Player.Instance.OnDamageTaken += UpdateHealthBar;
```

**Unsubscribe on destroy:**
```csharp
void OnDestroy()
{
    if (Player.Instance != null)
        Player.Instance.OnDamageTaken -= UpdateHealthBar;
}
```

---

## 12. Positioning

**Player is always centered:**

```csharp
void Start()
{
    transform.position = Vector3.zero;  // arena center
}
```

**Do NOT allow player movement** — this is an idle defense game, not a twin-stick shooter.

**Camera follows player** (usually also centered).

---

## 13. Layer and Physics

**Player layer:** `Player`

**Collision:**
- Collider2D (typically CircleCollider2D)
- Rigidbody2D (Kinematic, no gravity)
- Layer mask filters what player detects

**Physics queries:**
- `OverlapCircleAll` for enemy detection
- `LayerMask` to filter enemy layer only

---

## 14. Stats Overview

**Primary stats** (from attributes):
- HealthPoint
- ManaPoint
- AttackDamage
- AttackSpeed
- AttackRange
- DefenseAmount
- HealthRegen
- ManaRegen

**Secondary combat stats:**
- CriticalChance
- CriticalDamage
- HitRate
- Evasion
- Penetration
- KnockbackChance
- KnockbackForce
- MultiShootChance
- MultiShootCount
- BounceCount
- BounceRange
- LifeSteal
- ElementMastery

**Special stats:**
- DeathDefy
- DamagePerRange
- UltimateAttack

**See:** `Attribute_Design.md` for attribute contributions, `Modifier_Design.md` for calculation.

---

## 15. Performance Notes

**Player is a singleton** — one instance per game scene.

**Do NOT:**
- Recalculate final stats every frame (use cached)
- Use `GetComponent` every attack (cache references)
- Allocate new arrays every attack (reuse `ContactFilter2D`)

**DO:**
- Cache final stats from `PlayerStatsManager`
- Invalidate cache when modifiers change
- Pool projectiles
- Use layer masks for physics queries

---

## 16. Testing Checklist

```
[ ] Player spawns at arena center
[ ] Auto-attacks trigger at correct AttackSpeed interval
[ ] Attack range matches aura visualization
[ ] Damage taken reduces HP
[ ] HealthRegen restores HP over time
[ ] Death triggers at HP = 0
[ ] DeathDefy procs at correct chance
[ ] ManaRegen restores Mana over time
[ ] MultiShoot triggers at correct chance
[ ] Stats update when equipment/cards change
[ ] Stats persist across scene transitions
[ ] Events fire correctly (OnDamageTaken, OnPlayerDeath)
```

---

## 17. Common Issues

### Issue: Player attacks too fast/slow
**Cause:** AttackSpeed stat incorrect, or interval calculation wrong.
**Fix:** Verify `1 / AttackSpeed` formula, check final AttackSpeed value.

### Issue: Player attacks nothing
**Cause:** No enemies in range, or layer mask incorrect.
**Fix:** Verify enemy layer mask, check AttackRange value, debug enemy positions.

### Issue: Stats not updating
**Cause:** Cache not invalidated after modifier change.
**Fix:** Call `PlayerStatsManager.InvalidateCache()` after equipment/card/attribute change.

### Issue: Player dies instantly
**Cause:** MaxHP too low, or defense not applied.
**Fix:** Verify base HP, attribute contributions, defense calculation.

---

## 18. Future Extensions

### Player Movement
- Allow limited dodge/dash
- WASD movement (changes game from idle to active)

### Manual Aiming
- Mouse-directed attacks
- Target lock-on

### Active Skills
- Player-triggered abilities (not just ultimates)

### Weapon Switching
- Multiple weapon types with different behaviors

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
