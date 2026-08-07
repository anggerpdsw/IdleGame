using IdleDefenseSurvival;
using IdleDefenseSurvival.Equipment;
using NUnit.Framework;

namespace IdleDefenseSurvival.Equipment.Tests
{
    /// <summary>Per-build attribute weights — equivalence table from design critique 2.</summary>
    public class AttributeWeightsConfigTests
    {
        [Test]
        public void AllProfile_WeightsEveryAttributeOne()
        {
            var config = AttributeWeightsConfig.ForBuild(BuildProfile.All);
            Assert.AreEqual(1f, config.WeightFor(MainAttribute.Strength));
            Assert.AreEqual(1f, config.WeightFor(MainAttribute.Constitution));
            Assert.AreEqual(1f, config.WeightFor(MainAttribute.Intelligence));
            Assert.AreEqual(1f, config.WeightFor(MainAttribute.Dexterity));
            Assert.AreEqual(BuildProfile.All, config.BuildProfile);
        }

        [Test]
        public void TankProfile_WeightsConstitutionHighest()
        {
            var config = AttributeWeightsConfig.ForBuild(BuildProfile.Tank);
            Assert.AreEqual(3f, config.WeightFor(MainAttribute.Constitution));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Strength));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Intelligence));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Dexterity));
        }

        [Test]
        public void WarriorProfile_WeightsStrengthHighest()
        {
            var config = AttributeWeightsConfig.ForBuild(BuildProfile.Warrior);
            Assert.AreEqual(3f, config.WeightFor(MainAttribute.Strength));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Constitution));
        }

        [Test]
        public void MageProfile_WeightsIntelligenceHighest()
        {
            var config = AttributeWeightsConfig.ForBuild(BuildProfile.Mage);
            Assert.AreEqual(3f, config.WeightFor(MainAttribute.Intelligence));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Strength));
        }

        [Test]
        public void AssassinProfile_WeightsDexterityHighest()
        {
            var config = AttributeWeightsConfig.ForBuild(BuildProfile.Assassin);
            Assert.AreEqual(3f, config.WeightFor(MainAttribute.Dexterity));
            Assert.AreEqual(0.5f, config.WeightFor(MainAttribute.Constitution));
        }
    }
}