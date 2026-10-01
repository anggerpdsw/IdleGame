using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Handles card upgrades using duplicate counts.
    /// </summary>
    public static class CardUpgradeService
    {
        public static int GetRequiredDuplicates(int currentLevel)
        {
            var progression = CardDatabase.Instance?.Progression;
            if (progression == null || currentLevel < 1 || currentLevel >= progression.MaximumLevel)
                return 0;

            return progression.DuplicateRequirements[currentLevel - 1];
        }

        public static bool ProcessAutoUpgrade(string cardId)
        {
            CardInventory inventory = CardInventory.Instance;
            int maximumLevel = CardDatabase.Instance.Progression.MaximumLevel;

            OwnedCardData card = inventory.GetOwnedCard(cardId);

            if (card == null) return false;

            bool upgraded = false;

            while (card.Level < maximumLevel)
            {
                int required = GetRequiredDuplicates(card.Level);

                if (card.DuplicateCount < required) break;

                card.DuplicateCount -= required;
                card.Level++;

                upgraded = true;
            }

            return upgraded;
        }
        
    }
}