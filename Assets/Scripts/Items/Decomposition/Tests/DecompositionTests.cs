using NUnit.Framework;
using IdleDefenseSurvival.Items.Decomposition;

namespace IdleDefenseSurvival.Items.Decomposition.Tests
{
    public class DecomposedRequirementResolverTests
    {
        [Test]
        public void Compute_R1_ReturnsEmpty()
        {
            var result = DecomposedRequirementResolver.Compute(1);
            Assert.AreEqual(0, result.Count, "R1 should have no decomposed gate");
        }

        [Test]
        public void Compute_R2_ReturnsOneCommon()
        {
            var result = DecomposedRequirementResolver.Compute(2);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(1, result[0].Quantity);
        }

        [Test]
        public void Compute_R3_ReturnsTwoCommonOneRare()
        {
            var result = DecomposedRequirementResolver.Compute(3);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(2, result[0].Quantity);
            Assert.AreEqual("decomposed_rare", result[1].ItemId);
            Assert.AreEqual(1, result[1].Quantity);
        }

        [Test]
        public void Compute_R4_ReturnsThreeCommonTwoRareOneEpic()
        {
            var result = DecomposedRequirementResolver.Compute(4);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(3, result[0].Quantity);
            Assert.AreEqual("decomposed_rare", result[1].ItemId);
            Assert.AreEqual(2, result[1].Quantity);
            Assert.AreEqual("decomposed_epic", result[2].ItemId);
            Assert.AreEqual(1, result[2].Quantity);
        }

        [Test]
        public void Compute_R5_ReturnsFourCommonThreeRareTwoEpicOneLegendary()
        {
            var result = DecomposedRequirementResolver.Compute(5);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(4, result[0].Quantity);
            Assert.AreEqual("decomposed_rare", result[1].ItemId);
            Assert.AreEqual(3, result[1].Quantity);
            Assert.AreEqual("decomposed_epic", result[2].ItemId);
            Assert.AreEqual(2, result[2].Quantity);
            Assert.AreEqual("decomposed_legendary", result[3].ItemId);
            Assert.AreEqual(1, result[3].Quantity);
        }

        [Test]
        public void Compute_R6_ReturnsFiveCommonFourRareThreeEpicTwoLegendaryOneMythic()
        {
            var result = DecomposedRequirementResolver.Compute(6);
            Assert.AreEqual(5, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(5, result[0].Quantity);
            Assert.AreEqual("decomposed_rare", result[1].ItemId);
            Assert.AreEqual(4, result[1].Quantity);
            Assert.AreEqual("decomposed_epic", result[2].ItemId);
            Assert.AreEqual(3, result[2].Quantity);
            Assert.AreEqual("decomposed_legendary", result[3].ItemId);
            Assert.AreEqual(2, result[3].Quantity);
            Assert.AreEqual("decomposed_mythic", result[4].ItemId);
            Assert.AreEqual(1, result[4].Quantity);
        }

        [Test]
        public void Compute_R6_UsesMythicNotDivine()
        {
            var result = DecomposedRequirementResolver.Compute(6);
            foreach (var req in result)
            {
                Assert.AreNotEqual("decomposed_divine", req.ItemId,
                    "R6 must use decomposed_mythic, not decomposed_divine (v3.2 §5.1)");
            }
        }

        [Test]
        public void Compute_InvalidRarity_ReturnsEmpty()
        {
            Assert.AreEqual(0, DecomposedRequirementResolver.Compute(0).Count);
            Assert.AreEqual(0, DecomposedRequirementResolver.Compute(7).Count);
            Assert.AreEqual(0, DecomposedRequirementResolver.Compute(-1).Count);
        }
    }

    public class DecomposedRequirementAggregatorTests
    {
        [Test]
        public void SumPerJob_NullRequirements_ReturnsEmpty()
        {
            var result = DecomposedRequirementAggregator.SumPerJob(null, 5);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SumPerJob_EmptyRequirements_ReturnsEmpty()
        {
            var result = DecomposedRequirementAggregator.SumPerJob(
                System.Array.Empty<DecomposedRequirement>(), 5);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SumPerJob_JobCount1_ReturnsEqualQuantity()
        {
            var reqs = new[]
            {
                new DecomposedRequirement("decomposed_common", 2),
                new DecomposedRequirement("decomposed_rare", 1)
            };
            var result = DecomposedRequirementAggregator.SumPerJob(reqs, 1);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("decomposed_common", result[0].ItemId);
            Assert.AreEqual(2, result[0].Count);
            Assert.AreEqual("decomposed_rare", result[1].ItemId);
            Assert.AreEqual(1, result[1].Count);
        }

        [Test]
        public void SumPerJob_JobCount10_ScalesAllEntries()
        {
            var reqs = new[]
            {
                new DecomposedRequirement("decomposed_common", 2),
                new DecomposedRequirement("decomposed_rare", 1)
            };
            var result = DecomposedRequirementAggregator.SumPerJob(reqs, 10);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(20, result[0].Count);
            Assert.AreEqual(10, result[1].Count);
        }

        [Test]
        public void SumPerJob_ZeroJobCount_ClampsToOne()
        {
            var reqs = new[]
            {
                new DecomposedRequirement("decomposed_common", 5)
            };
            var result = DecomposedRequirementAggregator.SumPerJob(reqs, 0);
            Assert.AreEqual(5, result[0].Count);
        }

        [Test]
        public void SumPerJob_NegativeJobCount_ClampsToOne()
        {
            var reqs = new[]
            {
                new DecomposedRequirement("decomposed_common", 5)
            };
            var result = DecomposedRequirementAggregator.SumPerJob(reqs, -3);
            Assert.AreEqual(5, result[0].Count);
        }
    }

    public class ResolverAggregatorIntegrationTests
    {
        [Test]
        public void R6_FullPipeline_MatchesSpec()
        {
            var requirements = DecomposedRequirementResolver.Compute(6);
            var aggregated = DecomposedRequirementAggregator.SumPerJob(requirements, 5);

            Assert.AreEqual(5, aggregated.Count);
            Assert.AreEqual("decomposed_common", aggregated[0].ItemId);
            Assert.AreEqual(25, aggregated[0].Count);   // 5 * 5
            Assert.AreEqual("decomposed_rare", aggregated[1].ItemId);
            Assert.AreEqual(20, aggregated[1].Count);   // 4 * 5
            Assert.AreEqual("decomposed_epic", aggregated[2].ItemId);
            Assert.AreEqual(15, aggregated[2].Count);   // 3 * 5
            Assert.AreEqual("decomposed_legendary", aggregated[3].ItemId);
            Assert.AreEqual(10, aggregated[3].Count);   // 2 * 5
            Assert.AreEqual("decomposed_mythic", aggregated[4].ItemId);
            Assert.AreEqual(5, aggregated[4].Count);    // 1 * 5
        }

        [Test]
        public void R1_FullPipeline_AlwaysEmpty()
        {
            for (int count = 1; count <= 10; count++)
            {
                var requirements = DecomposedRequirementResolver.Compute(1);
                var aggregated = DecomposedRequirementAggregator.SumPerJob(requirements, count);
                Assert.AreEqual(0, aggregated.Count, $"R1 with count={count} must produce no gate");
            }
        }
    }
}
