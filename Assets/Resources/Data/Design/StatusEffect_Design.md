# Status Effect System Design — IdleDefenseSurvival

**Purpose:** Enemy status effects (slow, defense break, stun, heartbreak), duration, stacking, removal.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Enemy_Design.md](./Enemy_Design.md) — enemy behavior
- [Combat_Design.md](./Combat_Design.md) — damage pipeline
- [Projectile_Design.md](./Projectile_Design.md) — status application

---

## 1. Status Effect Identity

**Owner:** `Scripts/Enemy/EnemyStatusEffectController.cs`

**Interface:** `Scripts/Enemy/StatusEffects/IStatusEffect.cs`

**Base class:** `Scripts/Enemy/StatusEffects/BaseStatusEffect.cs`

**Concrete implementations:** `Scripts/Enemy/StatusEffects/ConcreteStatusEffects.cs`

---

## 2. Status Types

**Current status effects (verified):**

| Status | Effect | Duration Type | Stack Behavior |
|--------|--------|---------------|----------------|
| **Slow** | Reduces MoveSpeed + AttackSpeed | Temporary/Aura | Strongest wins |
| **Defense Break** | Reduces Defense | Temporary/Aura/Permanent | Additive |
| **Stun** | Freezes movement + attacks | Temporary | Extends duration |
| **HeartBreak** | Reduces MaxHP | Temporary | Strongest wins |

---

## 3. Status Effect Duration Types

### 3.1 Permanent

**Never expires.**

```csharp
public class PermanentSlowEffect : BaseStatusEffect
{
    public override EffectType Type => EffectType.Permanent;
    // No duration check
}
```

**Use case:** Passive debuffs from equipment/cards.

### 3.2 Aura

**Expires when source leaves range.**

```csharp
public class AuraSlowEffect : BaseStatusEffect
{
    public override EffectType Type => EffectType.Aura;
    
    public override bool ShouldExpire()
    {
        return Vector2.Distance(_source.position, _target.position) > _auraRange;
    }
}
```

**Use case:** Area effects, player aura, equipment aura.

### 3.3 Temporary

**Expires after duration.**

```csharp
public class TemporarySlowEffect : BaseStatusEffect
{
    public override EffectType Type => EffectType.Temporary;
    private float _remainingDuration;
    
    public override void Update(float deltaTime)
    {
        _remainingDuration -= deltaTime;
    }
    
    public override bool ShouldExpire()
    {
        return _remainingDuration <= 0;
    }
}
```

**Use case:** Projectile on-hit effects, skill debuffs.

---

## 4. Slow Status

**Effect:** Reduces enemy MoveSpeed and AttackSpeed.

**Formula:**
```
ModifiedSpeed = BaseSpeed × (1 - SlowPercent / 100)
```

**Example:**
- Base MoveSpeed: 5.0
- Slow: 50%
- Result: 5.0 × 0.5 = **2.5**

**Stack behavior:** Strongest slow wins (no additive stacking).

```csharp
public class SlowEffect : BaseStatusEffect
{
    public float SlowPercent { get; private set; }
    
    public override void Apply(EnemyAi target)
    {
        target.ApplySpeedModifier(1f - SlowPercent / 100f);
    }
    
    public override void Remove(EnemyAi target)
    {
        target.RemoveSpeedModifier();
    }
}
```

---

## 5. Defense Break Status

**Effect:** Reduces enemy Defense.

**Formula:**
```
EffectiveDefense = Max(0, BaseDefense - DefenseBreakValue - Penetration)
```

**Example:**
- Base Defense: 100
- Defense Break: -30
- Penetration: 20
- Result: Max(0, 100 - 30 - 20) = **50**

**Stack behavior:** Multiple defense breaks stack additively.

```csharp
public class DefenseBreakEffect : BaseStatusEffect
{
    public float DefenseReduction { get; private set; }
    
    public override void Apply(EnemyAi target)
    {
        target.AddDefenseModifier(-DefenseReduction);
    }
    
    public override void Remove(EnemyAi target)
    {
        target.RemoveDefenseModifier(-DefenseReduction);
    }
}
```

**Important:** Defense Break is a real gameplay effect, not just UI icon.

---

## 6. Stun Status

**Effect:** Freezes enemy movement and attacks.

**Duration:** Fixed seconds (e.g., 2s).

**Stack behavior:** New stun extends duration.

```csharp
public class StunEffect : BaseStatusEffect
{
    private float _remainingDuration;
    
    public override void Apply(EnemyAi target)
    {
        target.SetStunned(true);
    }
    
    public override void Remove(EnemyAi target)
    {
        target.SetStunned(false);
    }
    
    public override void Update(float deltaTime)
    {
        _remainingDuration -= deltaTime;
    }
    
    public override bool ShouldExpire()
    {
        return _remainingDuration <= 0;
    }
}
```

**Stun blocks:**
- Movement
- Attacks
- All AI behavior

---

## 7. HeartBreak Status

**Effect:** Reduces enemy MaxHP percentage.

**Formula:**
```
ModifiedMaxHP = BaseMaxHP × (1 - HeartBreakPercent / 100)
CurrentHP = Min(CurrentHP, ModifiedMaxHP)
```

**Example:**
- Base MaxHP: 1000
- CurrentHP: 800
- HeartBreak: 30%
- ModifiedMaxHP: 1000 × 0.70 = **700**
- CurrentHP clamped to: **700**

**Stack behavior:** Strongest HeartBreak wins.

```csharp
public class HeartBreakEffect : BaseStatusEffect
{
    public float ReductionPercent { get; private set; }
    
    public override void Apply(EnemyAi target)
    {
        target.ApplyMaxHPModifier(1f - ReductionPercent / 100f);
    }
    
    public override void Remove(EnemyAi target)
    {
        target.RemoveMaxHPModifier();
    }
}
```

---

## 8. Status Controller

**Owner:** `EnemyStatusEffectController.cs` (one per enemy)

**Responsibilities:**
- Store active effects
- Apply/remove effects
- Update durations
- Handle stacking
- Fire status events

**API:**

```csharp
public class EnemyStatusEffectController
{
    private List<IStatusEffect> _activeEffects = new();
    
    public void AddEffect(IStatusEffect effect)
    {
        // Check stacking rules
        var existing = _activeEffects.Find(e => e.GetType() == effect.GetType());
        
        if (existing != null)
        {
            HandleStacking(existing, effect);
        }
        else
        {
            _activeEffects.Add(effect);
            effect.Apply(_owner);
            OnStatusAdded?.Invoke(effect);
        }
    }
    
    public void RemoveEffect(IStatusEffect effect)
    {
        effect.Remove(_owner);
        _activeEffects.Remove(effect);
        OnStatusRemoved?.Invoke(effect);
    }
    
    public void Update(float deltaTime)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeEffects[i];
            effect.Update(deltaTime);
            
            if (effect.ShouldExpire())
            {
                RemoveEffect(effect);
            }
        }
    }
}
```

---

## 9. Stacking Rules

**Rules per status:**

| Status | Rule | Implementation |
|--------|------|----------------|
| Slow | Strongest wins | Replace if new > existing |
| Defense Break | Additive | Keep both, sum values |
| Stun | Extend duration | Add new duration to existing |
| HeartBreak | Strongest wins | Replace if new > existing |

**Example (Slow):**
```csharp
void HandleSlowStacking(SlowEffect existing, SlowEffect newEffect)
{
    if (newEffect.SlowPercent > existing.SlowPercent)
    {
        RemoveEffect(existing);
        AddEffect(newEffect);
    }
    // Else ignore weaker slow
}
```

**Example (Defense Break):**
```csharp
void HandleDefenseBreakStacking(DefenseBreakEffect existing, DefenseBreakEffect newEffect)
{
    // Both coexist, values sum
    AddEffect(newEffect);
}
```

**Example (Stun):**
```csharp
void HandleStunStacking(StunEffect existing, StunEffect newEffect)
{
    existing.ExtendDuration(newEffect.Duration);
}
```

---

## 10. Visual Indicators

**UI icons above enemy:**

```csharp
void OnStatusAdded(IStatusEffect effect)
{
    // Show icon
    StatusIconUI icon = Instantiate(_iconPrefab, _iconContainer);
    icon.SetIcon(effect.GetIconSprite());
    icon.SetOwner(effect);
    
    _statusIcons.Add(effect, icon);
}

void OnStatusRemoved(IStatusEffect effect)
{
    if (_statusIcons.TryGetValue(effect, out var icon))
    {
        Destroy(icon.gameObject);
        _statusIcons.Remove(effect);
    }
}
```

**Icon position:** Above enemy health bar.

**Icon types:**
- Slow: snowflake/ice
- Defense Break: broken shield
- Stun: dizzy stars
- HeartBreak: broken heart

---

## 11. Status Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnStatusAdded` | Effect applied | UI, stats tracker |
| `OnStatusRemoved` | Effect expired/removed | UI, stats tracker |
| `OnStatusUpdated` | Duration changed | UI (timer display) |

**Subscribe pattern:**
```csharp
enemy.StatusController.OnStatusAdded += HandleStatusAdded;
```

---

## 12. Application Sources

**Where statuses come from:**

1. **Projectile on-hit** — `Projectile.ApplyOnHitEffects()`
2. **Player aura** — continuous aura effect
3. **Equipment passive** — permanent effect while equipped
4. **Card effect** — card-granted status
5. **Ultimate ability** — skill applies status to area

**Application flow:**

```csharp
// From projectile
enemy.StatusController.AddEffect(new TemporarySlowEffect(50f, 3f));

// From aura
enemy.StatusController.AddEffect(new AuraDefenseBreakEffect(source, range, 20f));

// From equipment
enemy.StatusController.AddEffect(new PermanentSlowEffect(10f));
```

---

## 13. Performance

**Per-enemy overhead:**
- Status list iteration every frame (only if has statuses)
- Icon UI updates (only when status changes)

**Optimizations:**
- Skip Update if no active effects
- Pool status effect instances
- Pool status icon UI

**Do NOT:**
- Check all enemies for status every frame (event-driven instead)
- Allocate new effect instances per hit (pool them)

---

## 14. Testing Checklist

```
[ ] Slow reduces MoveSpeed and AttackSpeed
[ ] Defense Break reduces effective Defense
[ ] Stun freezes movement and attacks
[ ] HeartBreak reduces MaxHP and clamps CurrentHP
[ ] Temporary effects expire after duration
[ ] Aura effects expire when out of range
[ ] Permanent effects never expire
[ ] Multiple slows: strongest wins
[ ] Multiple defense breaks: stack additively
[ ] Multiple stuns: extend duration
[ ] Status icons show above enemy
[ ] Status icons remove when effect expires
[ ] Events fire correctly
[ ] No memory leaks from status objects
```

---

## 15. Common Issues

### Issue: Status never expires
**Cause:** `ShouldExpire()` always returns false, or `Update()` not called.
**Fix:** Verify expiry logic, check controller's Update loop.

### Issue: Multiple slows stack
**Cause:** Stacking rule not enforced.
**Fix:** Implement "strongest wins" logic in `HandleStacking`.

### Issue: Status icon stays after enemy death
**Cause:** Icon not destroyed on enemy destroy.
**Fix:** Clean up icons in `EnemyAi.OnDestroy()`.

### Issue: Defense Break not reducing damage taken
**Cause:** Defense calculation not reading status modifiers.
**Fix:** Verify defense pipeline includes status effects.

---

## 16. Future Extensions

### Damage Over Time (DoT)
- Poison, Burn, Bleed

### Buff Status
- Positive effects (speed up allies, shield)

### Dispel Mechanic
- Remove status effects

### Immunity
- Bosses immune to stun

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
