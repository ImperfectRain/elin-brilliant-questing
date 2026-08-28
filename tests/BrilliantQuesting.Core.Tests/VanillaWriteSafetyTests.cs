using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-011: every write refuses cleanly or completes fully. Never half of one.
    ///
    /// These drive the contract through <see cref="SandboxVanillaState"/>, the reference
    /// implementation. `ElinVanillaState` cannot be exercised without a running game, so the rules
    /// pinned here are the rules it is written to honour, and any divergence between the two is a
    /// bug in the adapter rather than a gap in the contract. The hostile inputs are the ones a
    /// procedural system actually produces: an actor who died between offer and resolution, an id
    /// that was never bound, an arithmetic slip that turns a small consequence into a huge one.
    /// </summary>
    public class VanillaWriteSafetyTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Merchant = EntityId.Parse("npc_merchant");
        private static readonly EntityId Ghost = EntityId.Parse("npc_never_bound");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");

        private static SandboxVanillaState World(int playerMoney = 100, int merchantMoney = 0)
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, money: playerMoney);
            vanilla.Define(Merchant, money: merchantMoney);
            return vanilla;
        }

        private static int TotalMoney(SandboxVanillaState vanilla)
        {
            return vanilla.GetMoney(Player) + vanilla.GetMoney(Merchant) + vanilla.GetMoney(Ghost);
        }

        // -- money ---------------------------------------------------------------------------

        [Fact]
        public void APaymentEitherMovesEverythingOrNothing()
        {
            SandboxVanillaState vanilla = World(playerMoney: 100);

            Assert.True(vanilla.TrySpendMoney(Player, Merchant, 30));

            Assert.Equal(70, vanilla.GetMoney(Player));
            Assert.Equal(30, vanilla.GetMoney(Merchant));
            Assert.Equal(100, TotalMoney(vanilla));
        }

        [Fact]
        public void ARefusedPaymentLeavesBothSidesUntouched()
        {
            SandboxVanillaState vanilla = World(playerMoney: 10);

            Assert.False(vanilla.TrySpendMoney(Player, Merchant, 50));

            Assert.Equal(10, vanilla.GetMoney(Player));
            Assert.Equal(0, vanilla.GetMoney(Merchant));
        }

        [Fact]
        public void ANegativePaymentIsRefusedRatherThanReversed()
        {
            SandboxVanillaState vanilla = World(playerMoney: 10);

            Assert.False(vanilla.TrySpendMoney(Player, Merchant, -100));

            Assert.Equal(10, vanilla.GetMoney(Player));
            Assert.Equal(0, vanilla.GetMoney(Merchant));
        }

        [Fact]
        public void AnAbsurdPaymentIsRefusedWithoutOverflowing()
        {
            SandboxVanillaState vanilla = World(playerMoney: 10);

            Assert.False(vanilla.TrySpendMoney(Player, Merchant, int.MaxValue));

            Assert.Equal(10, vanilla.GetMoney(Player));
            Assert.True(vanilla.GetMoney(Merchant) >= 0);
        }

        [Fact]
        public void PayingYourselfIsRefusedRatherThanMintingMoney()
        {
            SandboxVanillaState vanilla = World(playerMoney: 100);

            Assert.False(vanilla.TrySpendMoney(Player, Player, 50));

            Assert.Equal(100, vanilla.GetMoney(Player));
        }

        /// <summary>
        /// The regression behind the adapter fix: an unnamed payee is a sink and the money is
        /// meant to leave the world, but it must leave from a payer who could actually afford it.
        /// </summary>
        [Fact]
        public void AnUnnamedPayeeIsASinkAndStillRespectsTheBalance()
        {
            SandboxVanillaState vanilla = World(playerMoney: 40);

            Assert.True(vanilla.TrySpendMoney(Player, EntityId.None, 40));
            Assert.Equal(0, vanilla.GetMoney(Player));

            Assert.False(vanilla.TrySpendMoney(Player, EntityId.None, 1));
            Assert.Equal(0, vanilla.GetMoney(Player));
        }

        [Fact]
        public void SpendingIsRefusedWhenTheBuildCannotDoIt()
        {
            SandboxVanillaState vanilla = World(playerMoney: 100);
            vanilla.SetCapability(VanillaCapability.SpendMoney, false);

            Assert.False(vanilla.TrySpendMoney(Player, Merchant, 10));
            Assert.Equal(100, vanilla.GetMoney(Player));
        }

        // -- items ---------------------------------------------------------------------------

        private static SandboxVanillaState WithRing()
        {
            SandboxVanillaState vanilla = World();
            vanilla.GiveItem(Merchant, new ItemDescriptor(Ring, "silver ring", "jewelry", 400));
            return vanilla;
        }

        private static int CopiesOfRing(SandboxVanillaState vanilla)
        {
            int count = 0;
            foreach (EntityId owner in new[] { Player, Merchant, Ghost })
            {
                foreach (ItemDescriptor item in vanilla.GetInventory(owner))
                {
                    if (item.Id == Ring)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        [Fact]
        public void AnItemIsInExactlyOneInventoryBeforeAndAfter()
        {
            SandboxVanillaState vanilla = WithRing();
            Assert.Equal(1, CopiesOfRing(vanilla));

            Assert.True(vanilla.TryTransferItem(Ring, Merchant, Player));

            Assert.Equal(1, CopiesOfRing(vanilla));
            Assert.Empty(vanilla.GetInventory(Merchant));
        }

        [Fact]
        public void TransferringToYourselfDoesNotDuplicate()
        {
            SandboxVanillaState vanilla = WithRing();

            Assert.False(vanilla.TryTransferItem(Ring, Merchant, Merchant));

            Assert.Equal(1, CopiesOfRing(vanilla));
        }

        [Fact]
        public void TakingSomethingTheSourceDoesNotHaveChangesNothing()
        {
            SandboxVanillaState vanilla = WithRing();

            Assert.False(vanilla.TryTransferItem(Ring, Player, Merchant));

            Assert.Equal(1, CopiesOfRing(vanilla));
            Assert.Single(vanilla.GetInventory(Merchant));
        }

        [Fact]
        public void TransferringFromSomeoneWhoWasNeverBoundChangesNothing()
        {
            SandboxVanillaState vanilla = WithRing();

            Assert.False(vanilla.TryTransferItem(Ring, Ghost, Player));

            Assert.Equal(1, CopiesOfRing(vanilla));
        }

        /// <summary>
        /// An item handed to nobody is a real object deleted from the world, and anything written
        /// about it afterwards names a person who is not there.
        /// </summary>
        [Fact]
        public void AnItemIsNeverHandedToNobody()
        {
            SandboxVanillaState vanilla = WithRing();

            Assert.False(vanilla.TryTransferItem(Ring, Merchant, EntityId.None));
            Assert.False(vanilla.TryTransferItem(Ring, EntityId.None, Player));

            Assert.Equal(1, CopiesOfRing(vanilla));
            Assert.Single(vanilla.GetInventory(Merchant));
        }

        [Fact]
        public void TransferIsRefusedWhenTheBuildCannotDoIt()
        {
            SandboxVanillaState vanilla = WithRing();
            vanilla.SetCapability(VanillaCapability.TransferItems, false);

            Assert.False(vanilla.TryTransferItem(Ring, Merchant, Player));

            Assert.Equal(1, CopiesOfRing(vanilla));
            Assert.Single(vanilla.GetInventory(Merchant));
        }

        // -- standing ------------------------------------------------------------------------

        [Fact]
        public void StandingWritesAreRefusedWhenTheBuildCannotDoThem()
        {
            SandboxVanillaState vanilla = World();
            vanilla.SetCapability(VanillaCapability.ReadWriteKarma, false);
            vanilla.SetCapability(VanillaCapability.ReadWriteFame, false);

            int karma = vanilla.Karma;
            int fame = vanilla.Fame;

            vanilla.ChangeKarma(-5);
            vanilla.ChangeFame(20);

            Assert.Equal(karma, vanilla.Karma);
            Assert.Equal(fame, vanilla.Fame);
        }

        [Fact]
        public void AZeroDeltaIsANoOpEverywhere()
        {
            SandboxVanillaState vanilla = World();
            int karma = vanilla.Karma;
            int fame = vanilla.Fame;
            int affinity = vanilla.GetAffinity(Merchant);

            vanilla.ChangeKarma(0);
            vanilla.ChangeFame(0);
            vanilla.ChangeAffinity(Merchant, 0);

            Assert.Equal(karma, vanilla.Karma);
            Assert.Equal(fame, vanilla.Fame);
            Assert.Equal(affinity, vanilla.GetAffinity(Merchant));
        }

        // -- reads ---------------------------------------------------------------------------

        [Fact]
        public void ReadingAnUnknownActorNeverThrows()
        {
            SandboxVanillaState vanilla = World();

            Assert.Empty(vanilla.GetInventory(Ghost));
            Assert.Equal(0, vanilla.GetMoney(Ghost));
            Assert.Equal(0, vanilla.GetAffinity(Ghost));
        }
    }
}
