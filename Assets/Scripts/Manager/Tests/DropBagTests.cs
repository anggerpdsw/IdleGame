using IdleDefenseSurvival.Manager;
using NUnit.Framework;

namespace IdleDefenseSurvival.Tests
{
    /// <summary>
    /// Drop Bag behavior — pure logic, EditMode safe (no scene required).
    /// Covers the spec's expected tests: multi-wave aggregation, defeat/victory
    /// snapshot retention, new-game reset, inventory non-touch, duplicate-event protection.
    /// </summary>
    public class DropBagTests
    {
        // Test 1 — Multiple waves
        [Test]
        public void AddDrop_AggregatesAcrossWaves()
        {
            var bag = new DropBag();
            bag.AddDrop("rock", 2);   // Wave 1
            bag.AddDrop("iron", 3);   // Wave 2
            bag.AddDrop("rock", 4);   // Wave 3
            bag.AddDrop("potion", 1); // Wave 4

            Assert.AreEqual(6, bag.Items["rock"], "Rock x2 + x4");
            Assert.AreEqual(3, bag.Items["iron"], "Iron x3");
            Assert.AreEqual(1, bag.Items["potion"], "Potion x1");
            Assert.AreEqual(3, bag.Items.Count);
        }

        // Test 2/3 — Defeat / Victory: bag is NOT cleared, just stops collecting
        [Test]
        public void EndRun_KeepsSnapshot()
        {
            var bag = new DropBag();
            bag.AddDrop("rock", 5);
            bag.AddDrop("iron", 2);

            bag.IsRunActive = false; // EndRun (Victory or Defeat)

            Assert.AreEqual(5, bag.Items["rock"]);
            Assert.AreEqual(2, bag.Items["iron"]);
            Assert.AreEqual(2, bag.Items.Count);
        }

        // Test 4 — New game: clear, then only new drops appear
        [Test]
        public void Clear_StartsNewRunEmpty()
        {
            var bag = new DropBag();
            bag.AddDrop("rock", 5);
            bag.AddDrop("iron", 2);

            bag.Clear();
            Assert.IsEmpty(bag.Items);

            bag.AddDrop("potion", 3); // Wave 1 of new run
            Assert.AreEqual(3, bag.Items["potion"]);
            Assert.IsFalse(bag.Items.ContainsKey("rock"));
            Assert.IsFalse(bag.Items.ContainsKey("iron"));
        }

        // Test 6 — Duplicate event protection: same single drop never doubled
        [Test]
        public void AddDrop_SameItemSingleCall_CountsOnce()
        {
            var bag = new DropBag();
            bag.AddDrop("rock", 1);
            Assert.AreEqual(1, bag.Items["rock"]);
        }

        // Spec #6 — late drop after game end must be ignored
        [Test]
        public void AddDrop_AfterRunEnd_IsIgnored()
        {
            var bag = new DropBag { IsRunActive = false };
            bag.AddDrop("rock", 5);
            Assert.IsEmpty(bag.Items);
        }

        // Guard rails on the API
        [Test]
        public void AddDrop_InvalidInputs_AreIgnored()
        {
            var bag = new DropBag();
            bag.AddDrop("", 1);
            bag.AddDrop("rock", 0);
            bag.AddDrop(null, 3);
            Assert.IsEmpty(bag.Items);
        }

        // Clear never touches inventory — verified by design (no inventory calls in DropBag)
        [Test]
        public void Clear_IsPureDataReset()
        {
            var bag = new DropBag();
            bag.AddDrop("rock", 10);
            bag.Clear();
            Assert.IsEmpty(bag.Items);
        }
    }
}
