# Inventory System Design — IdleDefenseSurvival

> Catatan desain sistem Inventory + Equipment paper-doll (Profile panel) di scene `Assets/Scenes/Inventory.unity`.

---

## 1. Tujuan UI Profile (Paper-Doll Equipment)

Panel **Canvas → Panel → Profile** menampilkan 11 slot equipment yang sedang dipakai player:

| GameObject | EquipmentType | Index |
|---|---|---|
| Hat | Hat (1) | 0 |
| Gloves | Gloves (2) | 1 |
| Cape | Cape (3) | 2 |
| Armor | Armor (4) | 3 |
| Belt | Belt (5) | 4 |
| Pants | Pants (6) | 5 |
| Pendant | Pendant (7) | 6 |
| Ring | Ring (8) | 7 |
| Earring | Earring (9) | 8 |
| Bracelet | Bracelet (10) | 9 |
| Shoes | Shoes (11) | 10 |

Setiap GameObject slot punya komponen `EquipmentSlotUI` (view murni) yang menampilkan
`EquipmentSlotViewData` hasil build `EquipmentUI` dari `EquipmentService` + `EquipmentPresentationService`.

---

## 2. Arsitektur View — Presenter — Model

Prinsip: **UI tidak pernah menanyai service/database.** View hanya menerima ViewData.

```
EquipmentService ──► EquipmentUI ──► EquipmentPresentationService ──► EquipmentSlotViewData ──► EquipmentSlotUI (view)
   (data/source)      (controller,     (builds view model —        (pure data                      (murni apply,
                       holds service      all lookups &             object, no                            tooltip,
                       deps)             colors/format)            service ref)                         drag/drop)
```

Alur refresh:

```
EquipmentService.OnItemEquipped(slot) ──► EquipmentUI.RefreshSlot(slot)
                                       └─► BuildSlotViewData(slot)      (service lookups + presenter)
                                       └─► slotUI.ApplyViewData(data)  (set visuals only)
```

### File yang baru

| File | Peran |
|---|---|
| `Assets/Scripts/UI/Equipment/EquipmentSlotViewData.cs` | Viewmodel murni (state enum `EquipmentSlotState`: Locked/Empty/Occupied) + semua field visual |
| `Assets/Scripts/UI/Equipment/EquipmentPresentationService.cs` | Membangun `EquipmentSlotViewData` dari `EquipmentSlotViewSource`; **satu-satunya** tempat keputusan visual |
| `Assets/Scripts/UI/Equipment/CachedItemDefinition.cs` | Cache per-`itemId` dari `ItemDatabase` (icon + rarity) supaya `GetItem()` tidak dipanggil di tiap refresh |

### Viewmodel (`EquipmentSlotViewData`)

```csharp
public class EquipmentSlotViewData
{
    public EquipmentSlotState State;          // Locked / Empty / Occupied
    public Sprite Icon;       public bool ShowIcon;
    public Color BorderColor; public bool ShowBorder;
    public float Durability;  public bool ShowDurability;
    public Color DurabilityColor;
    public bool ShowEnhance;  public string EnhanceText;
    public bool ShowSetBonusGlow;
    public InventoryItem ReferenceItem;       // untuk tooltip / compare / drag
}
```

### Perubahan pada slot view (`EquipmentSlotUI`)
- `ApplyViewData(EquipmentSlotViewData)` — **hanya** set icon/border/durability/enhance/glow + status set (`SetActive`).
- TIDAK ada lagi `ItemDatabase.Instance` / `EquipmentService.Instance` di view.
- `CurrentItem` (pakai dari `ReferenceItem`) untuk klik → compare; tip.

### Presenter (`EquipmentPresentationService`)
- Membaca `CachedItemDefinition` (cache), menentukan rarity color, durability %, enhance text, set-bonus glow.
- Perubahan tampilan (warna rarity, format teks) cukup di satu tempat, dipakai juga untuk shop/compare/inspector nanti.

### Change yang memungkinkan
- `OnEquipmentChanged` dengan `EditSlot` → `RefreshSlot(slot)` (bukan `RefreshUI` penuh).
- `OnItemEquipped` / `OnItemUnequipped` / `OnDurabilityChanged` / `OnSlotUnlocked` → `RefreshSlot(slot)` (hanya 1 slot).
- `OnSetBonusChanged` → `RefreshAllSlots()` (11 slot, bukan seluruh panel UI + stats).
- `OnInventoryChanged` → `RefreshAllSlots()` (urutan, karena pengaruhi Set-count).

---

## 3. Item Icon + Durability : Caching & Perbaikan lain

- **CachedItemDefinition**: per-`ItemId` cache single lookup. Dipakai presenter; Inventory/Ship/Craft/Storage ikut hemat.
- **Durability color**: `InventoryItemExtensions.GetDurabilityColor` — warnanya ditaruh di `Scripts/Inventory/InventoryItemExtensions.cs`, bisa sedan ke JSON (TODO), sudah lebih baik dibanding if-scatter di UI.
- **Set bonus magic number**: `EquipmentService.IsSetBonusActive(setId)` baru utk cek "ada tier aktif" tanpa tierIndex magic `0`.

---

## 4. Wiring scene (`Assets/Scenes/Inventory.unity`)

Ke-11 GameObject slot di Panel Profile sudah di-wire ke `EquipmentSlotUI` (guid `33c43e95...`) via parent `EquipmentUI` (guid `228eb26b...`):

- `EquipmentUI._slotUis` → array 11 slot, urutan = index enum.
- Tiap slot punya wiring penuh: `_iconImage`, `_rarityBorder`, `_lockedOverlay`, `_emptyIndicator`, `_durabilityBar`, `_enhanceIndicator`, `_enhanceText`, `_setBonusGlow`.
- Child slot: `Icon` (Image), `Rarity` (Image), `Empty` (Image), `Durability` (Slider→Fill), `Enhance` (+ ) , `Lock`, `SetGlow`.

Alur inisialisasi:

```
BootstrapController
  ├─ EnsureSingleton<ItemDatabase>()       → load item JSON
  ├─ EnsureSingleton<InventoryService>()
  └─ EnsureSingleton<EquipmentService>() → EquippedItems, SlotData, events

EquipmentUI (di GameObject Profile)
  ├─ Awake → slotUI.Initialize(this)
  ├─ OnEnable → RefreshUI() + subscribe Equipment/Inventory events
  ├─ events → RefreshSlot(slot) / RefreshAllSlots()
  └─ BuildSlotViewData(slot) → presenter → ApplyViewData → view set
```

Governance default: Hat, Armor, Pants terbuka; sisanya `_lockedOverlay` (locked state).

---

## 5. Beres (pass 2) — 3 poin TODO

### a. Durability color data-driven (poin 9) ✅
- [DurabilityColorTable.cs](../Scripts/Items/DurabilityColorTable.cs) — tier statis pakai `GameColors` (green/yellow/orange/red per MinPercent 0.75/0.5/0.25/0).
- `DurabilityService.GetDurabilityColor(item)` → table; presenter/UI pakai.
- `InventoryItemExtensions.GetDurabilityColor()` tetap ada (hardcode lama) — tidak dipakai UI paper-doll lagi.
- (Revisi: semula JSON `dataDurabilityColor.json` → dihapus. Hanya 4 warna statis, `GameColors` di `Colorku.cs` sudah ada; JSON cuma indirection tanpa value. Ganti warna = edit satu tempat di Colorku.)

### b. SlotUnlockState (poin 10) ✅
- `EquipmentSlotData.UnlockState` enum `EquipmentSlotUnlockState { Unlocked, LockedByGold, LockedByLevel, LockedByQuest }`.
- `EquipmentSlotService.GetAllSlotData()` recompute state tiap refresh (sumber: `RequiredLevel`, `RequiredQuest`, `IsUnlocked`; player level dari `PlayerStatsManager`).
- ViewModel `EquipmentSlotViewData` tambah `UnlockState`, `ShowUnlockButton`, `UnlockCost`, `UnlockLabel`.
- View `EquipmentSlotUI` punya `_unlockButton` + `_unlockButtonText`; klik → `UnlockSlot(slot)`.
- Wiring button di scene = manual (2 field baru di tiap slot).

### c. Drag end-to-end (poin 11) ✅
- `InventoryDragItem.DraggedItem` — static current-drag; di-reset `OnDestroy`.
- `EquipmentSlotUI.OnDrop`: **bug fix** — resolver drop dari `eventData.pointerDrag.GetComponent<InventoryDragItem>()` TIDAK jalan karena pointerDrag = slot grid (bukan drag visual). Sekarang fallback ke `InventoryDragItem.DraggedItem`.
- `InventoryUI.BeginDrag`: fallback jika `_dragItemPrefab`/`_dragCanvas` belum diisi scene → tetap set `DraggedItem` (drop jalan; visual drag aja belum).
- `InventoryUI.ClearDrag`: clear static drag juga.

## 6. Kontrak InventoryUI ↔ InventoryService (revisi arsitektur)

**Prinsip: UI slot ≠ inventory slot.** UI grid menampilkan hasil filter/sort (item bisa berpindah posisi visual),
tapi **drag/drop dan `MoveItem` selalu memakai physical inventory index.**

### Mapping

```
UI Slot #0 ──► entry (item, inventoryIndex=17)
UI Slot #1 ──► entry (item, inventoryIndex=4)
...
```

- `InventorySlotUI` simpan **dua index**: `_slotIndex` (posisi UI grid, tetap 0..capacity-1) dan
  `_inventoryIndex` (index physical di `InventoryService.Slots`, -1 saat kosong; properti `InventoryIndex`).
- `InventoryUI.GetFilteredItems()` → `List<(InventoryItem item, int inventoryIndex)>`:
  iterasi `InventoryService.Slots` (bukan `AllItems`), filter `IsEquipped` + tab, item dibawa
  beserta index aslinya.
- `RefreshUI()` → `_slotUIs[i].SetItem(item, inventoryIndex)`.
- Drag: `OnBeginDrag` kirim `_inventoryIndex`; `OnEndDrag` raycast baca `InventoryIndex` slot target,
  lalu `MoveItem(fromPhysical, toPhysical)`. **Bukan** `MoveItem(UI 0, UI 1)`.
- `InventoryService.Slots` = `IReadOnlyList<InventorySlot>`, list berorder physical; UI source of truth.

### Perubahan lain (pass 3)

- **Seed sample pindah dari UI ke bootstrap** — `Core/InventorySampleSeeder.cs`, di-subscribe
  `BootstrapController` ke `SaveManager.OnSaveLoaded`; jalan sekali hanya saat save fresh
  (inv kosong setelah load). `InventoryUI` tidak lagi punya `_seedSampleItems`/`SeedSampleItemsIfEmpty`
  maupun subscribe `OnSaveLoaded`; `_saveLoaded` dihapus.
- **Drag visual** — `InventoryUI.Update` tidak pakai `Mouse.current` lagi; posisi drag dari
  `PointerEventData.position` (multi-input: touch/pen/Unity Input System).
- Context menu = TODO (bukan di UI langsung): lanjut lewat `ShowContextMenu` di InventoryUI,
  memanggil service, bukan aksi langsung di slot view.

### Sort dihapus (pass 4) ✅

Sistem sort **dihapus total** — user mengatur posisi item sendiri via drag-and-drop; item baru
masuk ke slot kosong pertama yang tersedia (`FindEmptySlot`, urutan physical).

File/fitur yang dihapus:
- `InventorySortUI.cs` + wiring di scene (component + `_sortUI` field + dropdown/toggle objek).
- `InventoryService.Sort()`, `SortByXxx()` — service tidak lagi mengubah urutan slot.
- Event `OnInventorySorted`, enum `InventorySortType`, API sort di `IInventoryService`.

Konsekuensi:
- Tidak ada lagi operasi yang merombak posisi physical item — drag-drop satu-satunya cara
  memindahkan item; posisi = pilihan user, disimpan apa adanya (urgbase slot index di save).
- Refresh UI = baca `Slots` + filter tab + tampilkan berurutan; mapping `InventoryIndex` tetap.

## 7. TODO / Next Step

- [ ] **Selesai (pass 1): ViewModel layer + presenter + per-slot refresh.**
- [ ] **Selesai (pass 2): durability color table, SlotUnlockState, drag E2E.**
- [x] **Unlock slot lewat klik** — zero scene change.
- [x] **Wiring scene unlock button** — ke-11 slot Locked + Requirement → `_unlockButton`/`_unlockButtonText`.
- [x] **Drag E2E visual** — `InventoryDragItem.prefab`, `_dragCanvas` + `_dragItemPrefab` di InventoryUI.
- [ ] **Compare panel** (deferral) — `EquipmentComparePanel` tak ada di scene; ditangani editor: buat obj
      ComparePanel, wire ke `EquipmentUI._comparePanel`, isi prefab stat entry. InfoPanel Inventory tetap ada.