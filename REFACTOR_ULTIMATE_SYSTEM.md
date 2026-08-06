# Refactor Ultimate System - Dokumentasi Lengkap

## Overview

Sistem Ultimate telah di-refactor dari arsitektur monolitik (hard-coded di UltimateManager) menjadi arsitektur modular yang scalable untuk mendukung 30-50 ultimate dengan mudah.

## Arsitektur Baru

### 1. **IUltimateHandler Interface**
Defines contract untuk semua ultimate handlers:
- `bool TrySpawn(Player, Vector3, UltimateData)` - Main spawn logic
- `int GetActiveCount()` - Get active instance count
- `void OnInstanceDestroyed()` - Notify factory saat destroyed
- `string UltimateId` - Unique ID (e.g., "bomb", "tank")
- `GameObject GetPrefab()` - Get prefab asset

**File:** [IUltimateHandler.cs](../Ultimate/IUltimateHandler.cs)

### 2. **UltimateFactory Static Registry**
Centralized registry untuk semua handlers:
- `RegisterHandler(id, handler)` - Register handler (called by UltimateManager.Awake)
- `TrySpawn(id, player, position, data)` - Spawn via handler
- `GetActiveCount(id)` - Get active count
- `OnInstanceDestroyed(id)` - Decrement active count
- `GetHandler(id)` - Get handler reference
- `Reset()` - Reset factory (for testing)

**File:** [UltimateFactory.cs](../Ultimate/UltimateFactory.cs)

### 3. **UltimateManager - Refactored**
Handles:
- Loading ultimate data dari dataUltimate.json
- Registering all handlers
- Trigger logic: **cooldown**, **chance**, **active checks**
- Public API untuk spawn dengan ID

**Key Changes:**
- Removed enum `UltimateType` (no longer needed)
- Removed hard-coded methods `TrySpawnBomb()`, `TrySpawnToxicCloud()`, etc.
- New API: `TrySpawn(ultimateId, position, player)` - universal spawn method
- Cooldown tracking per ultimate

**File:** [UltimateManager.cs](../Ultimate/UltimateManager.cs)

### 4. **Individual Handlers** (one per ultimate)
Each handler manages spawn logic for a specific ultimate:
- `BombHandler` → spawn Bomb
- `ToxicCloudHandler` → spawn ToxicDeathCloud
- `ShockwaveHandler` → spawn Shockwave
- `TankHandler` → spawn Tank

**Files:** `*Handler.cs` (BombHandler.cs, TankHandler.cs, etc.)

### 5. **Individual Instances** (actual GameObject components)
Each instance handles runtime behavior:
- `Bomb` - Explodes on collision/lifetime
- `ToxicDeathCloud` - Damages & slows enemies over time
- `Shockwave` - Instant radial damage
- `Tank` - Combat unit that moves and attacks

**Files:** `*Instance.cs` renamed to original class names (Bomb.cs, Tank.cs, etc.)

## Data Flow

```
Player.cs / EnemyAi.cs
    ↓
    UltimateManager.TrySpawn("bomb", position, player)
    ↓
    Check: active, cooldown, chance
    ↓
    UltimateFactory.TrySpawn("bomb", ...)
    ↓
    BombHandler.TrySpawn(...)
    ↓
    Instantiate prefab + Get Bomb component
    ↓
    Initialize + Track active count
    ↓
    Return true
    ↓
    Bomb.Explosion() → UltimateFactory.OnInstanceDestroyed("bomb")
```

## How to Add a New Ultimate (Template)

### Step 1: Create Handler
**File:** `Assets/Scripts/Ultimate/NewUltimateHandler.cs`

```csharp
using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    public class NewUltimateHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => "newUltimate";
        [SerializeField] private GameObject _prefab;

        public GameObject GetPrefab() => _prefab;

        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;
            if (!ultimateData.GetActive()) return false;

            int activeCount = UltimateFactory.GetActiveCount(UltimateId);
            if (activeCount >= ultimateData.GetCount()) return false;
            if (!Utilityku.Chance(ultimateData.GetChance())) return false;

            GameObject obj = Instantiate(_prefab, position, Quaternion.identity, player.transform);
            if (obj == null) return false;

            if (!obj.TryGetComponent(out NewUltimate instance))
            {
                Destroy(obj);
                return false;
            }

            instance.Initialize(player, ultimateData.GetDuration());
            UltimateFactory.IncrementActiveCount(UltimateId);
            return true;
        }

        public int GetActiveCount() => UltimateFactory.GetActiveCount(UltimateId);
        public void OnInstanceDestroyed() => UltimateFactory.OnInstanceDestroyed(UltimateId);
    }
}
```

### Step 2: Create Instance (Behavior)
**File:** `Assets/Scripts/Ultimate/NewUltimate.cs`

```csharp
using UnityEngine;

namespace IdleDefenseSurvival.Ultimate
{
    public class NewUltimate : MonoBehaviour
    {
        [SerializeField] private float _duration = 5f;
        private Player.Player _player;
        private float _spawnTime;

        public void Initialize(Player.Player player, float duration)
        {
            _player = player;
            _spawnTime = Time.time;
            _duration = duration;
        }

        private void Update()
        {
            if (Time.time - _spawnTime >= _duration)
            {
                OnExpired();
            }
        }

        private void OnExpired()
        {
            UltimateFactory.OnInstanceDestroyed("newUltimate");
            Destroy(gameObject);
        }
    }
}
```

### Step 3: Add Handler to UltimateManager Inspector
1. In Inspector, find **UltimateManager** GameObject
2. Scroll to **Ultimate Handlers** section
3. Add new field for handler component
4. Attach handler component script to UltimateManager (or separate GameObject)
5. Assign handler in inspector

### Step 4: Add Ultimate Data to dataUltimate.json
```json
{
  "id": "newUltimate",
  "active": true,
  "chance": 50,
  "count": 3,
  "duration": 5,
  "cooldown": 2,
  "damageMultiplier": 1.5,
  "knockbackMultiplier": 2
}
```

### Step 5: Create Prefab
1. Create GameObject with `NewUltimate` component
2. Add required child objects (sprite, effects, etc.)
3. Save as Prefab in `Assets/Resources/Prefabs/`
4. Reference in handler's `_prefab` field

## Benefits of New Architecture

✅ **Modular:** Each ultimate in separate files
✅ **Extensible:** Add ultimates without modifying manager
✅ **Scalable:** Supports 30-50+ ultimates easily
✅ **Maintainable:** Clear separation of concerns
✅ **Testable:** Each handler can be tested independently
✅ **Consistent:** All ultimates follow same pattern
✅ **Data-Driven:** All config from dataUltimate.json

## Key APIs

### UltimateManager
```csharp
public bool TrySpawn(string ultimateId, Vector3 position, Player.Player player)
public UltimateData GetUltimate(string id)
public bool TryGetUltimate(string id, out UltimateData data)
public float GetCooldownRemaining(string ultimateId)
public int GetActiveCount(string ultimateId)
public IReadOnlyCollection<string> GetAllUltimateIds()
```

### UltimateFactory
```csharp
public static void RegisterHandler(string id, IUltimateHandler handler)
public static bool TrySpawn(string id, Player, Vector3, UltimateData)
public static int GetActiveCount(string id)
public static void IncrementActiveCount(string id)
public static void OnInstanceDestroyed(string id)
public static IUltimateHandler GetHandler(string id)
```

### New Code
```csharp
// Old way
ultimateManager.TrySpawnBomb(position, player);
ultimateManager.TrySpawnShockwave(player);

// New way
ultimateManager.TrySpawn("bomb", position, player);
ultimateManager.TrySpawn("shockwave", transform.position, player);
```

## Files Changed

### Modified Files
- [UltimateManager.cs](../Ultimate/UltimateManager.cs) - Complete refactor
- [Player.cs](../Player/Player.cs) - Updated TryTriggerShockwave(), SpawnTank()
- [EnemyAi.cs](../Enemy/EnemyAi.cs) - Updated Die() spawn calls

---

**Architecture Status:** ✅ Ready for scaling to 30-50 ultimates
