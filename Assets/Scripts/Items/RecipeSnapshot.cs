using System;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Immutable recipe data captured at StartCraft (I-16).
    /// Roll MUST NOT query the live repository — uses this snapshot.
    ///</summary>
    [Serializable]
    public struct RecipeSnapshot
    {
        public string RecipeId;
        public int RecipeVersion;
        public string EquipmentType;
        public int Rarity;
        public CraftIngredientSnapshot[] Ingredients;   // per-unit quantities

        public RecipeSnapshot(
            string recipeId,
            int recipeVersion,
            string equipmentType,
            int rarity,
            CraftIngredientSnapshot[] ingredients)
        {
            RecipeId = recipeId;
            RecipeVersion = recipeVersion;
            EquipmentType = equipmentType;
            Rarity = rarity;
            Ingredients = ingredients ?? Array.Empty<CraftIngredientSnapshot>();
        }
    }
    // ponytail: RollConfiguration omitted — add when affix/quality weights required.
}
