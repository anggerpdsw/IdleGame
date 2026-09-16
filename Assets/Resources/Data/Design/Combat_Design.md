# Combat System Design — IdleDefenseSurvival

**Purpose:** Damage pipeline, defense formula, hit chance, combat flow.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Modifier_Design.md](./Modifier_Design.md) — stat calculation
- [Attribute_Design.md](./Attribute_Design.md) — attribute contributions
- [Player_Design.md](./Player_Design.md) — player combat behavior
- [Enemy_Design.md](./Enemy_Design.md) — enemy combat behavior
- [Projectile_Design.md](./Projectile_Design.md) — damage delivery
- [StatusEffect_Design.md](./StatusEffect_Design.md) — combat effects

---

## 1. Damage Pipeline

**Complete flow from attack to final damage:**

```
1. Raw Damage (attacker's AttackDamage)
   ↓
2. Element Modifiers (if attacker/defender have elements)
   ↓
3. Defense and Armor Penetration
   ↓
4. Boss/Elite Modifiers (if target is boss/elite)
   ↓
5. Final Damage → apply to target HP
```

---

## 2. Raw Damage Calculation

**Player attacking enemy:**

```
RawDamage = PlayerAttackDamage × CriticalMultiplier × RandomVariance
```

**Critical multiplier:**
- Normal hit: 1.0×
- Critical: 2.0× (default, configurable)
- SuperCritical: 3.0×
- UltraCritical: 5.0×

**Random variance:**
- ±5% variance: `Random.Range(0.95f, 1.05f)`
- Adds unpredictability, prevents same damage every hit

**Enemy attacking player:**

```
RawDamage = EnemyDamage × (1 + DifficultyModifier)
```

**DifficultyModifier:** Wave/tier scaling (see `Wave_Design.md`)

---

## 3. Element Modifiers

**Element interactions** (if implemented):

| Attacker Element | Defender Element | Modifier |
|------------------|------------------|----------|
| Fire | Earth | +25% damage |
| Water | Fire | +25% damage |
| Earth | Lightning | +25% damage |
| Lightning | Water | +25% damage |
| Same | Same | -15% damage |
| None | Any | 0% modifier |

**Formula:**
```
ElementModifiedDamage = RawDamage × (1 + ElementModifier)
```

**Current implementation:** Element system exists (`dataEnemy.json` has element field), but modifiers may not be fully active. Verify in code before relying on this.

---

## 4. Defense and Armor Penetration

**Core defense formula:**

```
EffectiveDefense = Max(0, TargetDefense - AttackerPenetration)

DamageAfterDefense = ElementModifiedDamage × (100 / (100 + EffectiveDefense))
```

**Example 1 (no penetration):**
- Damage: 100
- Target Defense: 50
- Penetration: 0
- EffectiveDefense: 50
- Result: 100 × (100 / 150) = **66.67 damage**

**Example 2 (with penetration):**
- Damage: 100
- Target Defense: 50
- Penetration: 30
- EffectiveDefense: 20
- Result: 100 × (100 / 120) = **83.33 damage**

**Key insights:**
- Defense has **diminishing returns** (each point less effective at high defense)
- Penetration is **multiplicative** on final damage when target has high defense
- Defense cannot reduce damage below certain threshold (100 in denominator prevents division by zero)

---

## 5. Defense Break Status

**Defense Break is a real gameplay status**, not just a UI icon.

**When applied:**
1. Projectile/skill triggers Defense Break
2. `EnemyStatusEffectController.AddEffect(DefenseBreakStatus)` called
3. Status stored with:
   - Value: defense reduction amount or percentage
   - Duration: time in seconds
   - Source: who applied it
   - Type: Permanent / Aura / Temporary

**Effect on damage calculation:**
```
EffectiveDefense = Max(0, TargetDefense - DefenseBreakValue - AttackerPenetration)
```

**Example:**
- Target Defense: 100
- Defense Break: -30 (from status)
- Attacker Penetration: 20
- EffectiveDefense: 100 - 30 - 20 = **50**

**See:** `StatusEffect_Design.md` for full Defense Break mechanics.

---

## 6. Boss/Elite Modifiers

**Special enemies have damage modifiers:**

| Target Type | Damage Modifier | Source |
|-------------|----------------|--------|
| Normal | 1.0× | Default |
| Elite | 0.85× | 15% damage reduction |
| Boss | 0.70× | 30% damage reduction |

**Formula:**
```
FinalDamage = DamageAfterDefense × BossEliteModifier
```

**Reason:** Bosses/elites have much higher HP pools, damage reduction prevents them from being one-shot by high-damage builds.

**Note:** Values are approximate, verify in `EnemyData.cs` or combat code.

---

## 7. Hit Chance

**Hit calculation:**

```
HitChance = Clamp(AttackerHitRate - TargetEvasion, 5%, 100%)
```

**Roll:**
```csharp
bool hit = Random.Range(0f, 100f) < HitChance;
```

**Minimum hit chance:** 5% (cannot fully avoid all attacks)
**Maximum hit chance:** 100% (always hit if HitRate >> Evasion)

**Example 1 (low evasion):**
- HitRate: 95%
- Evasion: 10%
- HitChance: 95 - 10 = **85%**

**Example 2 (high evasion):**
- HitRate: 95%
- Evasion: 100%
- HitChance: Clamp(95 - 100, 5%, 100%) = **5%** (minimum)

---

## 8. Critical Hit System

**Critical chance roll:**

```csharp
bool isCrit = Random.Range(0f, 100f) < CriticalChance;
```

**Critical tiers:**

| Tier | Chance Source | Multiplier |
|------|---------------|------------|
| Critical | Base CriticalChance stat | 2.0× |
| SuperCritical | Random chance when crit | 3.0× |
| UltraCritical | Extremely rare | 5.0× |

**SuperCritical/UltraCritical:** May trigger on top of base crit (check implementation for exact trigger conditions).

**Critical damage scaling:**
- Base: `Damage × CritMultiplier`
- With CriticalDamage stat: `Damage × (CritMultiplier + CriticalDamage%)`

**Example:**
- Base damage: 100
- Crit multiplier: 2.0×
- CriticalDamage stat: +50%
- Final crit damage: 100 × (2.0 + 0.5) = **250**

---

## 9. Player Auto-Attack Flow

**Player combat is automatic:**

```
1. Player spawns centered in arena
2. Every AttackSpeed seconds:
   a. Find enemies in AttackRange
   b. Select target (usually closest)
   c. Roll hit chance
   d. If hit:
      - Roll critical
      - Calculate raw damage
      - Spawn projectile
   e. Else: Miss (no projectile)
3. Projectile travels to target
4. On collision:
   - Apply damage pipeline
   - Trigger on-hit effects (bounce, status, etc.)
5. Repeat
```

**See:** `Player_Design.md` and `Projectile_Design.md` for details.

---

## 10. Enemy Attack Flow

**Enemy combat:**

```
1. Enemy spawns outside player AttackRange
2. Move toward player (steering behavior)
3. Stop at enemy's AttackRange
4. Every AttackSpeed seconds:
   a. Roll hit chance vs player Evasion
   b. If hit:
      - Calculate damage (enemy damage × modifiers)
      - Apply to player HP
   c. Else: Miss
5. Player knockback → enemy pushed away
6. Re-approach and repeat
```

**See:** `Enemy_Design.md` for full AI behavior.

---

## 11. Special Mechanics

### 11.1 Life Steal

**Formula:**
```
HealAmount = FinalDamageDealt × LifeStealPercent
PlayerHP += HealAmount
```

**Example:**
- Damage dealt: 100
- Life Steal: 10%
- Heal: 100 × 0.10 = **10 HP**

**Cap:** Cannot heal above MaxHP.

---

### 11.2 Knockback

**When knockback triggers:**
1. Roll KnockbackChance
2. If success:
   - Apply knockback force to enemy Rigidbody2D
   - Direction: away from player
   - Force: KnockbackForce stat

**Formula:**
```csharp
Vector2 direction = (enemy.position - player.position).normalized;
enemyRb.AddForce(direction * KnockbackForce, ForceMode2D.Impulse);
```

**Effect:** Pushes enemy away, interrupts attack, resets approach cycle.

---

### 11.3 Stun

**Stun status:**
- Freezes enemy movement
- Prevents enemy attacks
- Duration-based (seconds)
- Visual indicator (if implemented)

**Application:** Projectile or skill triggers stun → `EnemyStatusEffectController.AddEffect(StunStatus)`.

---

### 11.4 Bounce

**Projectile bounces to nearby enemies:**

```
1. Projectile hits target A
2. Apply damage to A
3. If BounceCount > 0:
   a. Find enemies in BounceRange of A
   b. Select closest enemy B
   c. Redirect projectile to B
   d. BounceCount -= 1
   e. Repeat until BounceCount = 0
```

**Damage reduction per bounce:** Usually 100% (no reduction), but can be configured.

---

### 11.5 MultiShoot

**Fire multiple projectiles per attack:**

```
if (Random.Range(0f, 100f) < MultiShootChance)
    projectileCount = MultiShootCount;
else
    projectileCount = 1;

for (int i = 0; i < projectileCount; i++)
{
    SpawnProjectile(target);
}
```

**Spread:** Multiple projectiles may target different enemies or same enemy (implementation-dependent).

---

## 12. Damage Events

**Combat events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnDamageTaken` | Entity takes damage | UI (damage popup), stats tracker |
| `OnEnemyKilled` | Enemy HP → 0 | Mission system, rewards, stats |
| `OnPlayerHit` | Player takes damage | Health bar, death check |
| `OnCriticalHit` | Critical damage dealt | Visual effects, stats |

**Purpose:** Decouple combat logic from UI and other systems.

---

## 13. Combat Constants

**Source:** `GameConstants.cs` and `dataPlayer.json`

| Constant | Default Value | Configurable? |
|----------|---------------|---------------|
| Base AttackDamage | 20 | dataPlayer.json |
| Base AttackSpeed | 1.0 (1 attack/sec) | dataPlayer.json |
| Base AttackRange | 5.0 units | dataPlayer.json |
| Critical Multiplier | 2.0× | Code |
| Hit Rate | 95% | dataPlayer.json |
| Damage Variance | ±5% | Code |

---

## 14. Performance Considerations

**Combat is high-frequency:**
- Player attacks multiple times per second
- Up to 5000+ enemies on screen
- Each projectile calculates damage on hit

**Optimizations:**
- **Pooling:** Projectiles, damage popups pooled
- **Cached stats:** Final stats cached, not recalculated per hit
- **Contact filter:** `ContactFilter2D` reused
- **Layer masks:** Physics queries filtered by layer

**Do NOT:**
- Calculate final stats per-hit (use cached)
- Allocate new objects per-hit (use pools)
- Use `GetComponent` per-hit (cache references)

---

## 15. Testing Checklist

```
[ ] Damage pipeline: Raw → Element → Defense → Boss/Elite → Final
[ ] Defense formula: 100/(100+Defense) correct
[ ] Penetration reduces effective defense
[ ] Defense Break status reduces defense
[ ] Hit chance clamped [5%, 100%]
[ ] Critical hit multiplier applied
[ ] Life steal heals correct amount
[ ] Knockback pushes enemy away
[ ] Stun prevents enemy actions
[ ] Bounce redirects projectile
[ ] MultiShoot fires multiple projectiles
[ ] Boss/Elite damage reduction applied
[ ] Damage variance ±5%
[ ] Minimum 5% hit chance enforced
```

---

## 16. Common Issues

### Issue: One-shot kills
**Cause:** No boss/elite damage reduction, or base damage too high.
**Fix:** Apply damage reduction modifier, balance base damage.

### Issue: Invincible bosses
**Cause:** Defense too high, or defense formula broken.
**Fix:** Verify defense calculation, check penetration value.

### Issue: Projectiles miss constantly
**Cause:** Enemy evasion too high, or HitRate too low.
**Fix:** Balance evasion stat, ensure minimum 5% hit chance.

### Issue: Critical never triggers
**Cause:** CriticalChance = 0, or roll logic broken.
**Fix:** Verify CriticalChance > 0, check roll implementation.

---

## 17. Future Extensions

### Elemental Reactions
- Combine element types for bonus effects (e.g., Fire + Lightning = Overload)

### Combo System
- Chain hits increase damage

### Weak Points
- Critical zones on bosses for bonus damage

### Damage Types
- Physical vs Magical damage with separate defenses

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
