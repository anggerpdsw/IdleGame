using System;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Immutable-at-creation copy of <see cref="CraftIngredient"/> captured when a craft job is created.
    /// <para>
    /// Contract:
    /// - <see cref="Count"/> is already scaled by job.Count (mirrors <c>CraftTransactionService.BeginTransaction</c> line 49).
    /// - <see cref="SubstituteItemIds"/> is a deep-cloned array (snapshot must not share storage with mutable recipe data).
    /// - All other fields are value-copied at capture time and never re-read from the live recipe.
    ///</para>
    /// <para>
    /// Purpose: temporal isolation for refund/audit paths so that recipe JSON or database mutations
    /// between job creation and job completion/cancellation cannot change what was actually consumed.
    ///</para>
    ///</summary>
    [Serializable]
    public class CraftIngredientSnapshot
    {
        public string ItemId;
        public int Count;
        public bool Consumed;
        public bool CanSubstitute;
        public string[] SubstituteItemIds;
        public int MinQuality;
        public int MinLevel;
        public int MinEnhance;
        public bool ReturnOnFail;

        /// <summary>
        /// Build a snapshot from a live recipe ingredient, scaling Count by the job batch size.
        /// Returns null if <paramref name="ingredient"/> is null so callers can safely chain on a null array.
        ///</summary>
        public static CraftIngredientSnapshot From(CraftIngredient ingredient, int count)
        {
            if (ingredient == null) return null;
            return new CraftIngredientSnapshot
            {
                ItemId = ingredient.ItemId,
                Count = ingredient.Count * count,
                Consumed = ingredient.Consumed,
                CanSubstitute = ingredient.CanSubstitute,
                SubstituteItemIds = ingredient.SubstituteItemIds != null
                    ? (string[])ingredient.SubstituteItemIds.Clone()
                    : null,
                MinQuality = ingredient.MinQuality,
                MinLevel = ingredient.MinLevel,
                MinEnhance = ingredient.MinEnhance,
                ReturnOnFail = ingredient.ReturnOnFail
            };
        }
    }
}
