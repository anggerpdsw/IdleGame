using System.Collections.Generic;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Data;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// PURE RNG/RESULT GENERATOR - NO SIDE EFFECTS.
    /// Accepts current pity counters + virtual inventory snapshot as input,
    /// returns new pity counters + updated virtual inventory in result.
    /// Zero SaveManager / Inventory mutations. Pure function = easily testable.
    /// </summary>
    public static class CardRollService
    {
        private static bool _debug = false;

        public static int CalculateRollGemCost(int amount)
        {
            if (amount <= 0) return 0;

            int hundredRolls = amount / 100;
            int remaining = amount % 100;

            int tenRolls = remaining / 10;
            int singleRolls = remaining % 10;

            return
                (hundredRolls * GameConstants.ROLL100X_GEM_COST) +
                (tenRolls * GameConstants.ROLL10X_GEM_COST) +
                (singleRolls * GameConstants.ROLL1X_GEM_COST);
        }

        /// <summary>
        /// Result of a batch roll, including updated pity and virtual inventory.
        /// </summary>
        public sealed class RollBatchResult
        {
            public CardRollResult RollResult;
            public VirtualCardInventorySnapshot VirtualInventory;
        }

        /// <summary>
        /// Perform `amount` rolls, returning combined result with updated virtual inventory.
        /// PURE FUNCTION: No mutations, no SaveManager calls, no real inventory access.
        /// Caller (CardManager) is responsible for persisting pity and applying virtual inventory.
        /// </summary>
        /// <param name="amount">Number of rolls to perform</param>
        /// <param name="rollsSinceEpic">Current pity counter for Epic rarity</param>
        /// <param name="rollsSinceLegendary">Current pity counter for Legendary rarity</param>
        /// <param name="rollsSinceMythic">Current pity counter for Mythic rarity</param>
        /// <param name="virtualInventory">Virtual inventory snapshot (updated during roll)</param>
        /// <returns>Roll result including new pity counters and updated virtual inventory</returns>
        public static RollBatchResult Roll(
            int amount,
            int rollsSinceEpic,
            int rollsSinceLegendary,
            int rollsSinceMythic,
            VirtualCardInventorySnapshot virtualInventory)
        {
            var rewardMap = new Dictionary<string, CardReward>();
            int cost = CalculateRollGemCost(amount);
            int gemCostForOneRoll = amount > 0 ? cost / amount : 0;

            var result = new CardRollResult
            {
                Cards = new List<CardReward>(amount),
                GemSpent = cost,
                GemRefunded = 0,
                IsLucky = false,
                HasNewCard = false,
                RollsSinceEpic = rollsSinceEpic,
                RollsSinceLegendary = rollsSinceLegendary,
                RollsSinceMythic = rollsSinceMythic
            };

            int epicPity = rollsSinceEpic;
            int legendaryPity = rollsSinceLegendary;
            int mythicPity = rollsSinceMythic;

            for (int i = 0; i < amount; i++)
            {
                // Increment pity counters
                epicPity++;
                legendaryPity++;
                mythicPity++;

                bool guaranteedEpic = epicPity >= GameConstants.PITY_EPIC_THRESHOLD;
                bool guaranteedLegendary = legendaryPity >= GameConstants.PITY_LEGENDARY_THRESHOLD;
                bool guaranteedMythic = mythicPity >= GameConstants.PITY_MYTHIC_THRESHOLD;

                Rarity chosenRarity = SelectRarity(guaranteedEpic, guaranteedLegendary, guaranteedMythic, virtualInventory);
                string chosenCardId = PickRandomCard(chosenRarity, virtualInventory);

                // If no valid card can be picked (all cards at max level), refund gems for this roll
                if (string.IsNullOrEmpty(chosenCardId))
                {
                    result.GemRefunded += gemCostForOneRoll;
                    result.GemSpent -= gemCostForOneRoll;

                    // Pity counters still increment for the attempted roll
                    // (they're already incremented above, no reset needed)
                    if (_debug) Debug.Log($"All cards at max level, refunded {gemCostForOneRoll} gems for roll {i + 1}/{amount}");
                    continue;
                }

                // Reset pity counters when guaranteed rarity is awarded
                if (guaranteedMythic)
                {
                    mythicPity = 0;
                    legendaryPity = 0;
                    epicPity = 0;
                }
                else if (guaranteedLegendary)
                {
                    legendaryPity = 0;
                    epicPity = 0;
                }
                else if (guaranteedEpic)
                {
                    epicPity = 0;
                }

                CardData cardData = CardDatabase.Instance.GetCard(chosenCardId);

                // Use virtual inventory for duplicate/new card detection
                bool isDuplicate = virtualInventory.HasCard(chosenCardId);
                bool isNewCard = !isDuplicate;
                bool isGuaranteed = guaranteedEpic || guaranteedLegendary || guaranteedMythic;

                if (!rewardMap.TryGetValue(chosenCardId, out var reward))
                {
                    reward = new CardReward
                    {
                        CardId = chosenCardId,
                        CardRarity = cardData.CardRarity,
                        Quantity = 1,
                        IsDuplicate = isDuplicate,
                        IsNewCard = isNewCard,
                        IsPityGuaranteed = isGuaranteed
                    };

                    rewardMap.Add(chosenCardId, reward);
                    result.Cards.Add(reward);
                }
                else
                {
                    reward.Quantity++;
                    reward.IsDuplicate |= isDuplicate;
                    reward.IsNewCard |= isNewCard;
                    reward.IsPityGuaranteed |= isGuaranteed;
                }

                if (reward.IsPityGuaranteed) result.IsLucky = true;
                if (isNewCard) result.HasNewCard = true;

                // Update virtual inventory to reflect this acquisition
                var (IsNewCard, IsDuplicate, NewLevel, NewDuplicateCount, ExcessCopiesRefunded) = virtualInventory.SimulateAcquire(chosenCardId, GameConstants.CARD_MAX_LEVEL);

                // If excess copies were refunded (card at max level), refund gems
                if (ExcessCopiesRefunded > 0)
                {
                    result.GemRefunded += ExcessCopiesRefunded * gemCostForOneRoll;
                    result.GemSpent -= ExcessCopiesRefunded * gemCostForOneRoll;
                    if (_debug) Debug.Log($"Card {chosenCardId} at max level, refunded {ExcessCopiesRefunded * gemCostForOneRoll} gems for excess copies");
                }
            }

            // Return updated pity counters for caller to persist
            result.RollsSinceEpic = epicPity;
            result.RollsSinceLegendary = legendaryPity;
            result.RollsSinceMythic = mythicPity;

            result.Cards.Sort((a, b) =>
            {
                int rarityCompare = a.CardRarity.CompareTo(b.CardRarity);
                if (rarityCompare != 0) return rarityCompare;
                return string.CompareOrdinal(a.CardId, b.CardId);
            });

            return new RollBatchResult
            {
                RollResult = result,
                VirtualInventory = virtualInventory
            };
        }

        // ----------------------------------------------------
        // Rarity selection logic (pure, no side effects)
        // Uses virtual inventory to filter out max-level cards
        // ----------------------------------------------------
        private static Rarity SelectRarity(
            bool guaranteedEpic,
            bool guaranteedLegendary,
            bool guaranteedMythic,
            VirtualCardInventorySnapshot virtualInventory)
        {
            var db = CardDatabase.Instance;
            if (guaranteedMythic && HasAvailableCardsOfRarity(db, virtualInventory, Rarity.Mythic))
                return Rarity.Mythic;
            if (guaranteedLegendary && HasAvailableCardsOfRarity(db, virtualInventory, Rarity.Legendary))
                return Rarity.Legendary;
            if (guaranteedEpic && HasAvailableCardsOfRarity(db, virtualInventory, Rarity.Epic))
                return Rarity.Epic;

            // Build list of rarities that have available cards
            var availableRarities = new List<Rarity>();
            float totalWeight = 0f;
            foreach (var rarity in db.RarityConfigs.Values)
            {
                if (HasAvailableCardsOfRarity(db, virtualInventory, rarity.Id))
                {
                    availableRarities.Add(rarity.Id);
                    totalWeight += rarity.Multiplier;
                }
            }

            if (availableRarities.Count == 0)
            {
                if (_debug) Debug.LogWarning("No available cards of any rarity (all at max level)");
                return Rarity.Common; // fallback
            }

            float roll = Random.value * totalWeight;
            float accum = 0f;
            foreach (var rarity in availableRarities)
            {
                var config = db.GetRarityMultiplier(rarity);
                accum += config;
                if (roll <= accum) return rarity;
            }

            return availableRarities[^1]; // fallback to last available
        }

        /// <summary>
        /// Checks if there are any available (non-max-level) cards of the given rarity using virtual inventory.
        /// </summary>
        private static bool HasAvailableCardsOfRarity(CardDatabase db, VirtualCardInventorySnapshot virtualInventory, Rarity rarity)
        {
            var allCards = db.GetCardsByRarity(rarity);
            foreach (var cardId in allCards)
            {
                int level = virtualInventory.GetLevel(cardId);
                if (level < GameConstants.CARD_MAX_LEVEL)
                    return true;
            }
            return false;
        }

        // ----------------------------------------------------
        // Pick a random card of the given rarity (pure, no side effects)
        // Uses virtual inventory to filter out max-level cards
        // ----------------------------------------------------
        private static string PickRandomCard(Rarity rarity, VirtualCardInventorySnapshot virtualInventory)
        {
            for (int rarityIndex = (int)rarity; rarityIndex >= (int)Rarity.Common; rarityIndex--)
            {
                Rarity currentRarity = (Rarity)rarityIndex;
                List<string> candidates = new(CardDatabase.Instance.GetCardsByRarity(currentRarity));
                candidates.RemoveAll(cardId =>
                {
                    int level = virtualInventory.GetLevel(cardId);
                    return level >= GameConstants.CARD_MAX_LEVEL;
                });

                if (candidates.Count > 0)
                {
                    if (_debug)
                    {
                        if (currentRarity != rarity)
                        {
                            Debug.LogWarning(
                                $"No available {rarity} cards. " +
                                $"Falling back to {currentRarity}. " +
                                $"Valid candidates = {candidates.Count}"
                            );
                        }
                        else
                        {
                            Debug.Log($"[{currentRarity}] Valid candidates = {candidates.Count}");
                        }
                    }

                    return candidates[Random.Range(0, candidates.Count)];
                }

                if (_debug)
                {
                    Debug.LogWarning(
                        $"No {currentRarity} cards available " +
                        $"(all cards are at max level)."
                    );
                }
            }

            if (_debug)
            {
                Debug.LogWarning(
                    $"All card rarities from {rarity} down to {Rarity.Common} " +
                    $"are unavailable. Cannot roll card."
                );
            }

            return null;
        }
    }
}