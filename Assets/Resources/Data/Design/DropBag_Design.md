# Drop Bag — Game Run Drop Recap

## Intent

`ViewInventory → Viewport → Content` (Game scene) shows the **recap of every item drop
obtained during one game run**, from the first wave until Victory/Defeat. The recap stays
visible after the run ends and is cleared only when a new run actually starts.

This is **result/UI tracking** — it reuses the existing drop pipeline and inventory. It is
**not** a new drop system, not a per-wave tracker.

## Concepts

- **Drop Bag** = runtime-only, per-run aggregation: `ItemId → TotalQuantity`.
- **ItemId** is the canonical identifier from `dataItems.json` (no new identifier system).
- **Inventory** = persistent (existing SaveData). **Drop Bag** = temporary run state,
  never persisted, never restored on app restart.

## Lifecycle (single source of truth)

| Event | Owner | Effect |
|---|---|---|
| New run start | `WaveManager.InitializeRun()` | `DropBagManager.Clear()` + `IsRunActive = true` |
| Monster drop succeeds | `EnemyAi.DropItemDrops()` | `DropBagManager.AddDrop(ItemId, qty)` |
| Victory / Defeat | `WaveManager.EndRun()` | `IsRunActive = false` — snapshot stays visible |
| Next run start | `WaveManager.InitializeRun()` | clear again |

- Wave start/end **never** clears the bag.
- Victory/Defeat **never** clears the bag.
- `Clear()` clears only bag runtime data + UI entries; it never calls
  `InventoryService.Remove/Clear/Reset`.

## Capture point (authoritative, single)

`EnemyAi.DropItemDrops()` (death rewards):

1. rolls `Utilityku.Chance(entry.Weight)` (0-100 percent);
2. checks `MinTier` gate;
3. calls `InventoryService.AddItem(entry.ItemId, quantity)` — success means the item
   actually reached the inventory;
4. **only then** `DropBagManager.AddDrop(...)`.

`OnItemAdded` inventory event is **not** used — it would over-capture loot from other
sources (chests, card rolls, rewards). One drop → exactly one `AddDrop` call.

## Aggregation

`Dictionary<string, int>` keyed by `ItemId`; quantities merge (Rock x2 + Rock x3 = x5).
Runtime data is the source of truth; UI (`Content`) is only its visual representation.

## UI

- `DropBagUI` on `ViewInventory` root, `_content` = `Content` (existing GridLayoutGroup,
  cell 100x100 — layout untouched, no conflicting components; ScrollRect already works).
- Panel starts INACTIVE (opened by the "Bag" button via `PanelOpener`), so `DropBagUI`
  subscribes in `OnEnable`/`OnDisable` and **rebuilds the full list on enable** — drops
  that happened while the panel was closed still appear, no duplicate subscription.
- Entry = `[Icon] Name xQuantity` (icon from `ItemData.Icon`, name from `ItemData.Name`).
  Uses `DropBagEntryUI`; optional prefab, otherwise runtime-created children.
- No `Update()` polling; no `GameObject.Find`.

## Persistence

- Drop Bag has **no** field in `SaveData` / `InventorySaveData` — app restart → empty bag.
- Inventory keeps its own persistence unchanged.

## Files

- `Scripts/Manager/DropBag.cs` — pure aggregator (EditMode-testable).
- `Scripts/Manager/DropBagManager.cs` — singleton facade, Bootstrap-registered.
- `Scripts/UI/DropBag/DropBagEntryUI.cs` — one row.
- `Scripts/UI/DropBag/DropBagUI.cs` — panel ↔ Content binding.
- Hooked: `EnemyAi.DropItemDrops`, `WaveManager.InitializeRun`/`EndRun`,
  `BootstrapController` (EnsureSingleton).
