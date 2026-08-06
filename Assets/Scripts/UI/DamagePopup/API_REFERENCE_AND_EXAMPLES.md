# Damage Popup System - API Reference & Examples

## Core Classes

### DamageType Enum

```csharp
public enum DamageType
{
    Normal,      // Regular damage (white)
    Critical,    // Crit hit (gold)
    Heal,        // Healing (green)
    Poison,      // DoT poison (purple)
    Burn,        // Fire damage (orange)
    Ice,         // Freeze/chill (cyan)
    TrueDamage,  // Armor-piercing (red)
    Miss         // Dodged/blocked (gray)
}
```

---

### DamageInfo Struct

**Purpose:** Carries all damage data from source to target. Scalable design.

#### Constructor

```csharp
// Basic constructor
DamageInfo damageInfo = new DamageInfo(
    damage: 25f,
    type: DamageType.Normal,
    isCritical: false,
    source: "player"
);

// With all fields
DamageInfo criticalHit = new DamageInfo(50f, DamageType.Critical, true, "player");
damageHit.DamageMultiplier = 1.5f;
damageHit.HasKnockback = true;
damageHit.KnockbackForce = 10f;
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Damage` | float | Raw damage value |
| `Type` | DamageType | Damage type (affects color & scale) |
| `IsCritical` | bool | Whether this is a crit |
| `Source` | string | Who dealt the damage ("player", "bomb", etc.) |
| `DamageMultiplier` | float | Damage multiplier (default 1.0) |
| `HasKnockback` | bool | Whether knockback applies |
| `KnockbackForce` | float | Knockback magnitude |

#### Methods

```csharp
// Get final damage after applying multipliers
float finalDamage = damageInfo.GetFinalDamage();
// Example: 50 damage * 1.5 multiplier = 75 final damage
```

---

### DamagePopupData Struct

**Purpose:** Display configuration for popups. Separate from damage calculation.

#### Constructor

```csharp
// Basic - uses type-based color
DamagePopupData popupData = new DamagePopupData(
    damage: 75f,
    type: DamageType.Normal,
    isCritical: false,
    prefix: ""
);

// With prefix
DamagePopupData healPopup = new DamagePopupData(100f, DamageType.Heal, false, "+");
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Damage` | float | Value to display |
| `Type` | DamageType | Type (affects color & scale) |
| `IsCritical` | bool | Critical hit (affects size) |
| `OverrideColor` | Color? | Custom color (if null, uses type color) |
| `Prefix` | string | Text prefix ("+", "-", "Miss", etc.) |
| `Duration` | float | How long popup stays visible (default 1.5s) |

#### Methods

```csharp
// Get color based on type
Color typeColor = popupData.GetTypeColor();

// Get scale multiplier
float scale = popupData.GetScale();

// Get formatted display text
string text = popupData.GetDisplayText(); // e.g., "50!" or "+100"
```

---

### DamagePopupManager (Singleton)

**Purpose:** Main entry point for showing damage popups.

#### Static Access

```csharp
DamagePopupManager manager = DamagePopupManager.Instance;
```

#### Methods

```csharp
// Main method - recommended
void ShowDamage(Vector3 worldPosition, DamagePopupData data)

// Legacy method (if not using World Space canvas)
void ShowDamageLegacy(Vector3 worldPosition, DamagePopupData data)

// Smart method - auto-detect canvas type
void ShowDamageSmart(Vector3 worldPosition, DamagePopupData data, bool useCanvasBased = true)

// Debug only
void DebugShowPoolStats()
void DebugClearAllPopups()
```

---

### DamagePopupPool

**Purpose:** Object pool management (auto-managed by manager).

#### Inspector Configuration

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Popup Prefab | GameObject | – | Prefab to pool |
| Initial Pool Size | int | 20 | Pre-instantiate count |
| Expandable | bool | true | Grow if needed |
| Max Pool Size | int | 100 | Upper limit (0 = unlimited) |

#### Methods (Internal — rarely called directly)

```csharp
DamagePopup Get()              // Get from pool (or create)
void Return(DamagePopup popup) // Return to pool
(int total, int available, int active) GetStats()
```

---

### DamagePopup Component

**Purpose:** Individual popup animation & rendering.

#### Inspector Configuration

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Animation Duration | float | 1.5s | Total animation time |
| Move Distance | float | 2f | Distance traveled upward |
| Horizontal Random Offset | float | 0.5f | Random X offset |
| Pop Scale Multiplier | float | 1.2f | Initial scale |
| Pop Duration | float | 0.2s | Pop animation length |
| Base Font Size | float | 36f | Normal damage size |
| Critical Font Multiplier | float | 1.3f | Crit size multiplier |

#### Methods (Called by Manager)

```csharp
void Initialize(Vector3 worldPosition, DamagePopupData data, DamagePopupPool pool)
```

---

## Usage Examples

### Example 1: Basic Enemy Damage

```csharp
// In EnemyAi.cs or Projectile.cs
public void DealDamageToEnemy(EnemyAi enemy, float damage)
{
    DamageInfo damageInfo = new DamageInfo(
        damage: damage,
        type: DamageType.Normal,
        isCritical: false,
        source: "player"
    );

    enemy.TakeDamage(damageInfo);
    // Popup appears automatically in TakeDamage()
}
```

### Example 2: Critical Hit with Knockback

```csharp
public void DealCriticalDamage(EnemyAi enemy, float baseDamage, float criticalMultiplier)
{
    bool isCritical = true;
    float finalDamage = baseDamage * criticalMultiplier;

    DamageInfo damageInfo = new DamageInfo(
        damage: finalDamage,
        type: DamageType.Critical,
        isCritical: true,
        source: "player"
    );
    
    damageInfo.HasKnockback = true;
    damageInfo.KnockbackForce = 15f;

    enemy.TakeDamage(damageInfo);
}
```

### Example 3: Poison/DoT Damage

```csharp
public void ApplyPoisonDamage(EnemyAi enemy, float poisonDamage)
{
    DamageInfo poisonInfo = new DamageInfo(
        damage: poisonDamage,
        type: DamageType.Poison,
        isCritical: false,
        source: "poison"
    );

    enemy.TakeDamage(poisonInfo);
    // Green popup displays with "Poison" styling
}
```

### Example 4: Heal Popup

```csharp
public void HealEnemy(Transform targetPosition, float healAmount)
{
    if (DamagePopupManager.Instance == null) return;

    DamagePopupData healData = new DamagePopupData(
        damage: healAmount,
        type: DamageType.Heal,
        isCritical: false,
        prefix: "↑"
    );

    DamagePopupManager.Instance.ShowDamage(targetPosition, healData);
}
```

### Example 5: Miss/Dodge Popup

```csharp
public void ShowMissPopup(Transform targetPosition)
{
    if (DamagePopupManager.Instance == null) return;

    DamagePopupData missData = new DamagePopupData(
        damage: 0,
        type: DamageType.Miss,
        isCritical: false,
        prefix: "MISS"
    );
    missData.Duration = 1.0f; // Shorter duration for miss

    DamagePopupManager.Instance.ShowDamage(targetPosition, missData);
}
```

### Example 6: Custom Color Override

```csharp
public void ShowCustomDamagePopup(Vector3 position, float damage, Color customColor)
{
    if (DamagePopupManager.Instance == null) return;

    DamagePopupData customData = new DamagePopupData(
        damage: damage,
        type: DamageType.Normal,
        isCritical: false,
        prefix: ""
    );
    customData.OverrideColor = customColor; // Custom color (e.g., purple for special ability)

    DamagePopupManager.Instance.ShowDamage(position, customData);
}
```

### Example 7: Combo Counter

```csharp
private int _comboCount = 0;

public void DealComboHit(EnemyAi enemy, float baseDamage)
{
    _comboCount++;
    float comboMultiplier = 1f + (_comboCount * 0.1f); // 10% per combo
    float finalDamage = baseDamage * comboMultiplier;

    DamageInfo comboInfo = new DamageInfo(
        damage: finalDamage,
        type: _comboCount > 3 ? DamageType.Critical : DamageType.Normal,
        isCritical: _comboCount > 3,
        source: "combo"
    );

    enemy.TakeDamage(comboInfo);
    // Popup scales based on combo level
}
```

---

## Integration Points

### With Player.cs

```csharp
// Player deals damage to enemies
private void Attack()
{
    Collider2D[] hits = Physics2D.OverlapCircleAll(
        transform.position, 
        _stats.GetFloat(PlayerStatType.AttackRange), 
        _enemyLayerMask
    );

    foreach (Collider2D hit in hits)
    {
        if (hit.TryGetComponent<EnemyAi>(out var enemy))
        {
            float damage = _stats.GetFloat(PlayerStatType.AttackDamage);
            bool isCrit = Utilityku.Chance(_stats.GetFloat(PlayerStatType.CriticalChance));
            
            DamageInfo info = new DamageInfo(damage, isCrit ? DamageType.Critical : DamageType.Normal, isCrit, "player");
            if (isCrit) info.DamageMultiplier = 1.5f;
            
            enemy.TakeDamage(info);
        }
    }
}
```

### With Projectile.cs

```csharp
// Projectile hits enemy
private void OnTriggerEnter2D(Collider2D collision)
{
    if (collision.TryGetComponent<EnemyAi>(out var enemy))
    {
        DamageInfo info = new DamageInfo(
            _damage,
            DamageType.Normal,
            false,
            "projectile"
        );
        
        enemy.TakeDamage(info);
        
        // Apply knockback
        if (enemy.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
            enemy.ApplyKnockback(knockbackDir, 10f);
        }

        Destroy(gameObject);
    }
}
```

### With Bomb/Ultimate Effects

```csharp
// Bomb explosion deals AoE damage
private void ExplodeBomb(Vector3 explosionCenter)
{
    Collider2D[] enemies = Physics2D.OverlapCircleAll(explosionCenter, _explosionRadius, _enemyLayer);
    
    foreach (Collider2D enemy in enemies)
    {
        if (enemy.TryGetComponent<EnemyAi>(out var enemyAi))
        {
            DamageInfo explosionInfo = new DamageInfo(
                _explosionDamage,
                DamageType.Burn,
                false,
                "bomb"
            );

            enemyAi.TakeDamage(explosionInfo);
        }
    }
}
```

---

## Performance Considerations

### Memory Optimization

```csharp
// For high-volume damage (100+ popups/second)
// Increase pool size in DamagePopupPool inspector:
// Initial Pool Size: 50-100
// Max Pool Size: 200-300

// For low-volume damage games
// Keep defaults:
// Initial Pool Size: 20
// Max Pool Size: 100
```

### Animation Tuning

```csharp
// For action games (fast-paced)
// DamagePopup.cs settings:
// Animation Duration: 0.8s (faster)
// Pop Duration: 0.15s

// For RPG games (slower)
// Animation Duration: 2.0s
// Pop Duration: 0.3s
```

### Debug Monitoring

```csharp
// Call periodically to monitor pool health
void DebugMonitorPool()
{
    var stats = DamagePopupManager.Instance._popupPool.GetStats();
    Debug.Log($"Pool: Total={stats.total}, Active={stats.active}, Available={stats.available}");
    
    // If Active stays near Total, pool is saturated
    // If Available stays near Total, pool is underutilized
}
```

---

## Common Patterns

### Pattern 1: Damage Range Display

```csharp
public void ShowDamageRange(Vector3 position, float minDamage, float maxDamage)
{
    string text = $"{minDamage:F0}-{maxDamage:F0}";
    // Create custom popup with this text
}
```

### Pattern 2: Status Effect Icons

```csharp
public void ShowStatusEffect(Vector3 position, string effectName, float duration)
{
    DamagePopupData effectData = new DamagePopupData(duration, DamageType.Poison);
    effectData.OverrideColor = Color.cyan;
    effectData.Prefix = effectName;
    
    DamagePopupManager.Instance.ShowDamage(position, effectData);
}
```

### Pattern 3: Damage Over Time

```csharp
private IEnumerator ApplyDamageOverTime(EnemyAi enemy, float damagePerTick, int tickCount, float tickInterval)
{
    for (int i = 0; i < tickCount; i++)
    {
        DamageInfo dotInfo = new DamageInfo(damagePerTick, DamageType.Poison, false, "dot");
        enemy.TakeDamage(dotInfo);
        
        yield return new WaitForSeconds(tickInterval);
    }
}
```

---

## Troubleshooting

### Popups Don't Display

**Check:**
1. Canvas → Render Camera is assigned to Main Camera
2. DamagePopupManager.Instance is not null
3. Pool has popups available (check DebugShowPoolStats)
4. Position is not behind camera

**Fix:**
```csharp
// Verify manager exists
if (DamagePopupManager.Instance == null) 
{
    Debug.LogError("DamagePopupManager not found in scene!");
    return;
}

// Verify pool has space
var stats = DamagePopupManager.Instance._popupPool.GetStats();
Debug.Log($"Pool stats: {stats}");
```

### Popups Disappear Too Fast

**Adjust in DamagePopup.cs:**
```csharp
// Increase animation duration
[SerializeField] private float _animationDuration = 2.5f; // was 1.5f

// Or per-popup:
DamagePopupData data = new DamagePopupData(damage, type);
data.Duration = 2.5f; // Custom duration
```

### Pool Memory Growing

**Possible Causes:**
1. Pool not returning popups (check if coroutines finish)
2. Max Pool Size too high
3. Initial Pool Size too small (causing constant expansion)

**Fix:**
```csharp
// Reset pool (debug only)
DamagePopupManager.Instance.DebugClearAllPopups();
```

