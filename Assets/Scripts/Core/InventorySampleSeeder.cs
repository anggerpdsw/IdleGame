using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Dev convenience: seed sample items once when the save is truly fresh
    /// (no save file). Injected via BootstrapController so the UI layer stays
    /// free of save/seed concerns.
    /// </summary>
    public static class InventorySampleSeeder
    {
        public static void SeedIfEmpty()
        {
            var inv = InventoryService.Instance;
            if (inv == null || ItemDatabase.Instance == null || inv.AllItems.Count > 0) return;

            inv.AddItem("potion_hp", 12);
            inv.AddItem("iron_ore", 40);
            inv.AddItem("magic_crystal", 9);
            inv.AddItem("gold_pouch", 3);
            inv.AddItem("equip_hat_leather");
            inv.AddItem("equip_gloves_fighter");
            inv.AddItem("equip_armor_iron");
            inv.AddItem("equip_ring_ruby");
            inv.AddItem("gem_sapphire", 3);
        }
    }
}