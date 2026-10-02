# Projectile System Design — IdleDefenseSurvival

**Purpose:** Damage delivery, collision handling, special effects (bounce, stun, life steal).

**Last Updated:** 2026-10-01

---

## Related Design Documents

- [Player_Design.md](./Player_Design.md) — projectile spawning
- [Combat_Design.md](./Combat_Design.md) — damage calculation
- [Enemy_Design.md](./Enemy_Design.md) — target handling
- [StatusEffect_Design.md](./StatusEffect_Design.md) — status application
- [Card_Design.md](./Card_Design.md) — card-owned projectile behaviors

---

## 1. Projectile Identity

**Owner:** `Scripts/Player/Projectile.cs`

**Purpose:** Deliver player's attack damage to target enemy.

**Lifecycle:**
1. Spawn from player position
2. Move toward target
3. Collide with target
4. Apply damage + effects
5. Handle bounce (if any)
6. Return to pool

**Pooling:** `Scripts/Manager/ProjectilePool.cs` — reuse instances, avoid Instantiate/Destroy spam.

---

## 2. Spawning

**Spawner:** `Player.cs` calls `ProjectilePool.Get()`

**Initialization:**

```csharp
public void Initialize(
    Transform target,
    float damage,
    bool isCritical,
    Vector2 spawnPosition
)
{
    _target = target;
    _damage = damage;
    _isCritical = isCritical;
    
    transform.position = spawnPosition;
    
    _bounceCount = PlayerStatsManager.GetFinalStat(BounceCount);
    _bounceRange = PlayerStatsManager.GetFinalStat(BounceRange);
    
    gameObject.SetActive(true);
}
```

**Critical state:** Projectile knows if it's a critical hit. Affects visuals + damage multiplier already baked into `_damage`.

---

## 3. Movement

**Simple homing:**

```csharp
void Update()
{
    if (_target == null)
    {
        ReturnToPool();
        return;
    }
    
    Vector2 direction = (_target.position - transform.position).normalized;
    transform.position += (Vector3)direction * _speed * Time.deltaTime;
}
```

**Speed:** Read from `ProjectileSpeed` stat or hardcoded constant.

**No physics movement** — direct position manipulation is cheaper for pooled projectiles.

---

## 4. Collision

**Trigger detection:**

```csharp
void OnTriggerEnter2D(Collider2D other)
{
    if (other.CompareTag("Enemy"))
    {
        HitEnemy(other.GetComponent<EnemyAi>());
    }
}
```

**Requirements:**
- Projectile has `Collider2D` (IsTrigger = true)
- Enemy has `Collider2D`
- Projectile layer and enemy layer can interact

---

## 5. Damage Application

**On hit:**

```csharp
void HitEnemy(EnemyAi enemy)
{
    if (enemy == null) return;
    
    // 1. Roll hit chance
    if (!RollHit(enemy))
    {
        ShowMissIndicator();
        HandleBounce(enemy);
        return;
    }
    
    // 2. Apply damage
    float finalDamage = CalculateFinalDamage(enemy);
    enemy.TakeDamage(finalDamage);
    
    // 3. Apply effects
    ApplyOnHitEffects(enemy);
    
    // 4. Handle bounce
    HandleBounce(enemy);
}
```

---

## 6. Hit Chance Roll

**Formula:** `Clamp(PlayerHitRate - EnemyEvasion, 5%, 100%)`

```csharp
bool RollHit(EnemyAi enemy)
{
    float playerHitRate = PlayerStatsManager.GetFinalStat(HitRate);
    float enemyEvasion = enemy.GetEvasion();
    
    float hitChance = Mathf.Clamp(playerHitRate - enemyEvasion, 5f, 100f);
    
    return Utilityku.Chance(hitChance);
}
```

**Minimum 5%** — player can never miss completely.

---

## 7. Final Damage Calculation

**Pipeline (recalculated on hit):**

```csharp
float CalculateFinalDamage(EnemyAi enemy)
{
    float damage = _damage;  // already includes critical multiplier
    
    // 1. Element modifiers (if implemented)
    damage *= GetElementModifier(enemy);
    
    // 2. Defense and penetration
    float effectiveDefense = Mathf.Max(0, enemy.GetDefense() - GetPenetration());
    damage *= 100f / (100f + effectiveDefense);
    
    // 3. Boss/Elite modifier
    if (enemy.IsBoss())
        damage *= 0.70f;  // 30% reduction
    else if (enemy.IsElite())
        damage *= 0.85f;  // 15% reduction
    
    // 4. DamagePerRange (if applicable)
    damage += CalculateDamagePerRange(enemy);
    
    return damage;
}
```

**Note:** Raw damage already includes critical multiplier from `Player.cs`.

---

## 8. Bounce Mechanic

**After hit:**

```csharp
void HandleBounce(EnemyAi hitEnemy)
{
    if (_bounceCount <= 0)
    {
        ReturnToPool();
        return;
    }
    
    _bounceCount--;
    
    // Find next target
    EnemyAi nextTarget = FindNearestEnemy(hitEnemy.transform.position, _bounceRange);
    
    if (nextTarget != null)
    {
        _target = nextTarget.transform;
        // Continue flying
    }
    else
    {
        ReturnToPool();
    }
}
```

**Damage reduction per bounce:** Usually none (100% damage each bounce). If needed, apply multiplier.

**Max bounces:** Read from `BounceCount` stat (default 0 = no bounce).

---

## 9. On-Hit Effects

**Effects applied on successful hit:**

### 9.1 Life Steal

```csharp
void ApplyLifeSteal(float damageDealt)
{
    float lifeSteal = PlayerStatsManager.GetFinalStat(LifeSteal);
    if (lifeSteal <= 0) return;
    
    float healAmount = damageDealt * (lifeSteal / 100f);
    Player.Instance.Heal(healAmount);
}
```

### 9.2 Knockback

```csharp
void ApplyKnockback(EnemyAi enemy)
{
    float knockbackChance = PlayerStatsManager.GetFinalStat(KnockbackChance);
    if (!Utilityku.Chance(knockbackChance)) return;
    
    float knockbackForce = PlayerStatsManager.GetFinalStat(KnockbackForce);
    Vector2 direction = (enemy.transform.position - Player.Instance.transform.position).normalized;
    
    enemy.ApplyKnockback(direction * knockbackForce);
}
```

### 9.3 Stun

```csharp
void ApplyStun(EnemyAi enemy)
{
    float stunChance = PlayerStatsManager.GetFinalStat(StunChance);
    if (!Utilityku.Chance(stunChance)) return;
    
    float stunDuration = PlayerStatsManager.GetFinalStat(StunDuration);
    enemy.ApplyStun(stunDuration);
}
```

### 9.4 Defense Break

```csharp
void ApplyDefenseBreak(EnemyAi enemy)
{
    float defenseBreakChance = PlayerStatsManager.GetFinalStat(DefenseBreakChance);
    if (!Utilityku.Chance(defenseBreakChance)) return;
    
    float defenseBreakValue = PlayerStatsManager.GetFinalStat(DefenseBreakValue);
    float defenseBreakDuration = PlayerStatsManager.GetFinalStat(DefenseBreakDuration);
    
    enemy.ApplyDefenseBreak(defenseBreakValue, defenseBreakDuration);
}
```

---

## 10. Special Formulas

### 10.1 DamagePerRange

**Bonus damage based on distance:**

```csharp
float CalculateDamagePerRange(EnemyAi enemy)
{
    float baseAttack = PlayerStatsManager.GetFinalStat(AttackDamage);
    float damagePerRange = PlayerStatsManager.GetFinalStat(DamagePerRange);
    float currentRange = Vector2.Distance(Player.Instance.transform.position, enemy.transform.position);
    
    return baseAttack * (damagePerRange / 100f) * currentRange;
}
```

**Example:**
- Base Attack: 100
- DamagePerRange: 5%
- Distance: 10 units
- Bonus: 100 × 0.05 × 10 = **50 bonus damage**

---

## 11. Pooling

**Pool manager:** `Scripts/Manager/ProjectilePool.cs`

**Get projectile:**
```csharp
Projectile proj = ProjectilePool.Instance.Get();
proj.Initialize(target, damage, isCrit, spawnPos);
```

**Return projectile:**
```csharp
public void ReturnToPool()
{
    gameObject.SetActive(false);
    ProjectilePool.Instance.Return(this);
}
```

**Pool size:** Pre-instantiate ~50-100 projectiles (adjust based on profiling).

**Do NOT:**
- `Instantiate` every attack
- `Destroy` every hit

---

## 12. Visual States

### Critical Projectile

### Miss Indicator

**Show a "MISS" popup at the hit location.**

```csharp
void ShowMissIndicator()
{
    DamagePopup.Spawn(transform.position, "MISS", Color.gray);
}
```

**Indicator:** Larger size, different color, particle trail.

```csharp
void SetCriticalVisuals(bool isCritical)
{
    if (isCritical)
    {
        _spriteRenderer.color = Color.red;
        _trailRenderer.enabled = true;
        transform.localScale = Vector3.one * 1.5f;
    }
    else
    {
        _spriteRenderer.color = Color.white;
        _trailRenderer.enabled = false;
        transform.localScale = Vector3.one;
    }
}
```

---

## 13. Card Integration

Player projectiles read card balance from the active card definitions in `dataCard.json` through `CardModifierService`. Runtime behavior state is owned by `CardRuntimeManager`.

- Infinite Arsenal marks the individual pooled projectile during initialization. It forces an Arsenal critical, uses the configured bounce count, and retargets unhit enemies within player attack range. The flag is cleared by `ResetState`; it is not a static cross-projectile flag.
- Void Overlord uses a per-hit damage multiplier and continues to unhit enemies in attack range while active. Critical/void multipliers are calculated locally per hit, so bounces do not accumulate previous-hit modifiers.
- Bullet Storm and Critical Cascade create pooled projectiles using their card parameters.
- Celestial Arsenal's piercing beam, orbital strike, and chain lightning resolve against the active enemy snapshot; Storm Heart accelerates its attack interval and projectile-related proc checks.
- World Breaker and Apex Devourer are applied centrally by `EnemyAi.TakeDamage`, so all player-owned hits receive their applicable bonuses rather than only standard player projectiles.
- Storm Heart also accelerates Chain Reaction's projectile-caused Volatile proc chance; timed global effects such as Law of Collapse are not accelerated.
- Execution Protocol reads normal and Boss/Special HP thresholds from its card parameters.
- Overkill consumes `EnemyAi.LastOverkillDamage`, which is the final post-defense/post-reduction damage beyond the target's remaining HP. It transfers the configured fraction, capped by the configured multiplier of the triggering hit's final damage.

Keep card chances/thresholds and the projectile's data-source units documented in [Card_Design.md](./Card_Design.md). Do not add card tuning constants to `Projectile.cs` or a second config asset.

---

## 14. Performance

**High-frequency system** — spawns every attack (multiple per second).

**Optimizations:**
- **Pooling** — avoid Instantiate/Destroy
- **Direct movement** — no Rigidbody2D physics
- **Cached references** — no GetComponent per frame
- **Layer masks** — filter collision candidates

**Do NOT:**
- Recalculate stats every frame (cache from `PlayerStatsManager`)
- Use `FindObjectsOfType` to find enemies
- Allocate new arrays every bounce

---

## 15. Edge Cases

### Target dies mid-flight

**Handle gracefully:**

```csharp
void Update()
{
    if (_target == null)
    {
        ReturnToPool();  // target destroyed
        return;
    }
    
    // Continue movement
}
```

### Bounce target not found

**Return to pool:**

```csharp
if (nextTarget == null)
{
    ReturnToPool();
    return;
}
```

### Multiple hits on same enemy

**Should NOT happen** — projectile applies damage once per target, then either bounces to new target or returns to pool.

---

## 16. Testing Checklist

```
[ ] Projectile spawns at player position
[ ] Projectile moves toward target
[ ] Collision triggers on enemy hit
[ ] Hit chance roll enforces 5% minimum
[ ] Damage applies correctly
[ ] Miss shows "MISS" indicator
[ ] Critical projectiles have visual indicator
[ ] Life steal heals player
[ ] Knockback pushes enemy away
[ ] Stun freezes enemy
[ ] Defense Break reduces enemy defense
[ ] Bounce redirects to next enemy
[ ] Bounce count decrements correctly
[ ] Projectile returns to pool after final hit
[ ] Pool reuses instances (no Instantiate spam)
[ ] No errors when target dies mid-flight
```

---

## 17. Common Issues

### Issue: Projectiles miss target
**Cause:** Movement speed too slow, or target moves out of range.
**Fix:** Increase speed, or add homing logic.

### Issue: Damage too high/low
**Cause:** Defense formula wrong, or critical state incorrect.
**Fix:** Verify damage pipeline, check critical multiplier.

### Issue: Bounce never triggers
**Cause:** BounceCount = 0, or no enemies in BounceRange.
**Fix:** Verify BounceCount stat, check enemy positions.

### Issue: Pool runs out
**Cause:** Too many simultaneous projectiles, pool size too small.
**Fix:** Increase pool size, or optimize projectile lifetime.

---

## 18. Future Extensions

### Homing Projectiles
- Curve toward moving targets

### AoE Explosion
- Splash damage on impact

### Piercing
- Hit multiple enemies in a line

### Elemental Trails
- Leave damaging trail behind projectile

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
