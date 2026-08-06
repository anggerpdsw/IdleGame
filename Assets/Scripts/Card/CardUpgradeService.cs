using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Handles card upgrades using duplicate counts.
    /// </summary>
    public static class CardUpgradeService
    {
        private static readonly int[] DuplicateRequirements =
        {
            2,  // Lv 1 -> Lv 2
            4,  // Lv 2 -> Lv 3
            7,  // Lv 3 -> Lv 4
            11, // Lv 4 -> Lv 5
            19, // Lv 5 -> Lv 6
            31, // Lv 6 -> Lv 7
            47, // Lv 7 -> Lv 8
            69, // Lv 8 -> Lv 9
            99  // Lv 9 -> Lv 10
        };

        public static int GetRequiredDuplicates(int currentLevel)
        {
            if (currentLevel < 1 || currentLevel >= GameConstants.CARD_MAX_LEVEL)
                return 0;

            return DuplicateRequirements[currentLevel - 1];
        }

        public static bool ProcessAutoUpgrade(string cardId)
        {
            CardInventory inventory = CardInventory.Instance;

            OwnedCardData card = inventory.GetOwnedCard(cardId);

            if (card == null) return false;

            bool upgraded = false;

            while (card.Level < GameConstants.CARD_MAX_LEVEL)
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