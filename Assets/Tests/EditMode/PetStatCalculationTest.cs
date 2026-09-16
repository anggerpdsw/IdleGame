using NUnit.Framework;
using IdleDefenseSurvival.Pet;

namespace IdleDefenseSurvival.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Pet stat calculation system.
    /// Validates stat growth formulas, rarity multipliers, level scaling.
    /// </summary>
    public class PetStatCalculationTest
    {
        private PetDefinition _voidlingDefinition;
        private PetRuntime _voidlingRuntime;

        [SetUp]
        public void Setup()
        {
            // Create Voidling definition matching dataPet.json
            _voidlingDefinition = new PetDefinition
            {
                id = "pet_voidling",
                name = "Voidling",
                rarity = "Epic",
                baseStats = new PetBaseStats
                {
                    attack = 10f,
                    attackSpeed = 1.5f,
                    skillDamage = 1.5f,
                    moveSpeed = 5f,
                    targetRange = 8f,
                    health = 100f
                },
                growth = new PetGrowth
                {
                    attackPerLevel = 2f,
                    skillDamagePerLevel = 0.1f,
                    healthPerLevel = 10f
                }
            };
        }

        [Test]
        public void VoidlingLevel1_BaseAttack_Returns10()
        {
            // Arrange
            _voidlingRuntime = new PetRuntime("test-id", "pet_voidling", _voidlingDefinition, level: 1);

            // Act
            float attack = _voidlingRuntime.GetAttack();

            // Assert
            // Level 1: (10 + (1-1)*2) * 1.3 = 10 * 1.3 = 13
            Assert.AreEqual(13f, attack, 0.01f, "Level 1 Voidling attack should be 13 (10 base * 1.3 Epic multiplier)");
        }

        [Test]
        public void VoidlingLevel5_WithGrowth_Returns23_4()
        {
            // Arrange
            _voidlingRuntime = new PetRuntime("test-id", "pet_voidling", _voidlingDefinition, level: 5);

            // Act
            float attack = _voidlingRuntime.GetAttack();

            // Assert
            // Level 5: (10 + (5-1)*2) * 1.3 = (10 + 8) * 1.3 = 18 * 1.3 = 23.4
            Assert.AreEqual(23.4f, attack, 0.01f, "Level 5 Voidling attack should be 23.4");
        }

        [Test]
        public void VoidlingLevel10_WithGrowth_Returns31_2()
        {
            // Arrange
            _voidlingRuntime = new PetRuntime("test-id", "pet_voidling", _voidlingDefinition, level: 10);

            // Act
            float attack = _voidlingRuntime.GetAttack();

            // Assert
            // Level 10: (10 + (10-1)*2) * 1.3 = (10 + 18) * 1.3 = 28 * 1.3 = 36.4
            Assert.AreEqual(36.4f, attack, 0.01f, "Level 10 Voidling attack should be 36.4");
        }

        [Test]
        public void EpicRarity_Multiplier_Returns1_3()
        {
            // Act
            float multiplier = _voidlingDefinition.GetRarityMultiplier();

            // Assert
            Assert.AreEqual(1.3f, multiplier, 0.01f, "Epic rarity multiplier should be 1.3");
        }

        [Test]
        public void SkillDamage_ScalesWithLevel()
        {
            // Arrange
            _voidlingRuntime = new PetRuntime("test-id", "pet_voidling", _voidlingDefinition, level: 5);

            // Act
            float skillDamage = _voidlingRuntime.GetSkillDamage();

            // Assert
            // Level 5: (1.5 + (5-1)*0.1) * 1.3 = (1.5 + 0.4) * 1.3 = 1.9 * 1.3 = 2.47
            Assert.AreEqual(2.47f, skillDamage, 0.01f, "Level 5 skill damage multiplier should be 2.47");
        }

        [Test]
        public void Health_ScalesWithLevel()
        {
            // Arrange
            _voidlingRuntime = new PetRuntime("test-id", "pet_voidling", _voidlingDefinition, level: 5);

            // Act
            float health = _voidlingRuntime.CalculateStat(_voidlingDefinition.baseStats.health, _voidlingDefinition.growth.healthPerLevel);

            // Assert
            // Level 5: (100 + (5-1)*10) * 1.3 = (100 + 40) * 1.3 = 140 * 1.3 = 182
            Assert.AreEqual(182f, health, 0.01f, "Level 5 health should be 182");
        }

        [Test]
        public void CommonRarity_Multiplier_Returns1_0()
        {
            // Arrange
            var commonDef = new PetDefinition { rarity = "Common" };

            // Act
            float multiplier = commonDef.GetRarityMultiplier();

            // Assert
            Assert.AreEqual(1.0f, multiplier, 0.01f, "Common rarity multiplier should be 1.0");
        }

        [Test]
        public void LegendaryRarity_Multiplier_Returns1_5()
        {
            // Arrange
            var legendaryDef = new PetDefinition { rarity = "Legendary" };

            // Act
            float multiplier = legendaryDef.GetRarityMultiplier();

            // Assert
            Assert.AreEqual(1.5f, multiplier, 0.01f, "Legendary rarity multiplier should be 1.5");
        }
    }
}
