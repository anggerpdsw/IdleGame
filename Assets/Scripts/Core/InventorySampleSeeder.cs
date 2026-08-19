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

            inv.AddItem("potion_hp", 10);
            inv.AddItem("potion_mp", 11);
            inv.AddItem("essence_of_hope", 9);
            inv.AddItem("water", 999);
            inv.AddItem("leather", 999);
            inv.AddItem("disposed_logs", 999);
            inv.AddItem("organic_glue", 999);
            inv.AddItem("cotton_thread", 999);
        }
    }
}