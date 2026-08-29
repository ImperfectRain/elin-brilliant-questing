using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-029. A mine road stopped by stone, and two ways past it: move the world, or talk to
    /// the person who knows the crawl. The physical route changes the obstruction itself; the
    /// social route grants access without pretending the rockfall went away.
    /// </summary>
    public class PhysicalActionTests
    {
        [Fact]
        public void PhysicalWorldVerbsAreRegisteredWithProfilesAndFamily()
        {
            string[] verbs =
            {
                "clear_obstruction", "carry", "rescue", "mine_bypass", "break_barrier",
                "transport", "capture", "restrain", "escort"
            };

            ActionRegistry registry = StandardActions.CreateRegistry();
            foreach (string verb in verbs)
            {
                Assert.NotNull(registry.Get(verb));
                Assert.Equal(ActionFamily.Physical, registry.Get(verb).Family);
                Assert.NotNull(ProceduralCheckProfiles.ForAction(verb));
            }
        }

        // -- the done-when ------------------------------------------------------------------

        [Fact]
        public void APhysicalBuildCanBypassABarrierThatASocialBuildMustTalkThrough()
        {
            BlockageLab physical = BlockageLab.Create(CheckOutcome.Pass);

            ActionOutcome bypass = physical.Run("mine_bypass", physical.Trail, EntityId.None);

            Assert.True(bypass.Succeeded);
            Assert.Equal(TruthState.Superseded, physical.Fact(physical.Situation.BlockageFactId).Truth);
            Assert.True(physical.MineSite.Admits(physical.Player));
            Assert.Equal(ThreadState.Resolved, physical.Situation.Thread.State);
            Assert.Equal("passage_opened", physical.Situation.Thread.Resolution);
            Assert.Contains(bypass.Events, e => e.Type == WorldEventType.SiteCleared);

            BlockageLab social = BlockageLab.Create(CheckOutcome.Pass);

            ActionOutcome talkedThrough = social.Run("persuade", social.Trail, social.Foreman);

            Assert.True(talkedThrough.Succeeded);
            Assert.True(social.MineSite.Admits(social.Player));
            Assert.Equal(TruthState.True, social.Fact(social.Situation.BlockageFactId).Truth);
            Assert.Equal(ThreadState.Active, social.Situation.Thread.State);
            Assert.Contains(talkedThrough.Events, e => e.Type == WorldEventType.PromiseMade);
        }

        // -- barriers ----------------------------------------------------------------------

        [Fact]
        public void BarrierVerbsArePlaceBoundRatherThanMenuEntries()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.Pass);

            Availability away = lab.Can("mine_bypass", lab.Mine, EntityId.None);

            Assert.False(away.IsAvailable);
            Assert.Contains("barrier", away.Reason);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BlockageFactId).Truth);
        }

        [Fact]
        public void ClearingABarrierNeedsAWriteThatCanRemoveTheObject()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.Pass);
            lab.Vanilla.SetCapability(VanillaCapability.DestroyItems, false);

            Availability clear = lab.Can("clear_obstruction", lab.Trail, EntityId.None);

            Assert.False(clear.IsAvailable);
            Assert.Contains("removed", clear.Reason);
        }

        [Fact]
        public void CarryingNeedsAWriteThatCanMoveTheObject()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.Pass);
            lab.Vanilla.SetCapability(VanillaCapability.TransferItems, false);

            Availability carry = lab.Can("carry", lab.Trail, EntityId.None);
            ActionOutcome outcome = lab.Run("carry", lab.Trail, EntityId.None);

            Assert.False(carry.IsAvailable);
            Assert.Contains("moved", carry.Reason);
            Assert.Empty(outcome.Events);
            Assert.Single(lab.Vanilla.GetInventory(lab.Trail));
        }

        [Fact]
        public void AFailedPhysicalAttemptLeavesTheBarrierStanding()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Run("mine_bypass", lab.Trail, EntityId.None);

            Assert.False(outcome.Succeeded);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BlockageFactId).Truth);
            Assert.False(lab.MineSite.Admits(lab.Player));
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
        }

        [Fact]
        public void ACriticalFailureCreatesHistoryButDoesNotInventAccess()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.CriticalFail);

            ActionOutcome outcome = lab.Run("mine_bypass", lab.Trail, EntityId.None);

            Assert.False(outcome.Succeeded);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BlockageFactId).Truth);
            Assert.False(lab.MineSite.Admits(lab.Player));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Harmed && e.Target == lab.Player);
        }

        [Fact]
        public void ClearingTheObstructionRemovesThePhysicalEvidenceOfTheBarrier()
        {
            BlockageLab lab = BlockageLab.Create(CheckOutcome.CriticalPass);

            ActionOutcome outcome = lab.Run("clear_obstruction", lab.Trail, EntityId.None);

            Assert.True(outcome.Succeeded);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.BlockageFactId).Truth);
            Assert.Empty(lab.Vanilla.GetInventory(lab.Trail));
        }

        private sealed class BlockageLab
        {
            private BlockageLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public BlockedPassageSituation Situation { get; private set; }

            public EntityId Trail => Situation.TrailZoneId;

            public EntityId Mine => Situation.MineZoneId;

            public EntityId Foreman => Situation.ForemanId;

            public NarrativeSite MineSite => World.Registry.GetSite(Mine);

            public static BlockageLab Create(CheckOutcome outcome)
            {
                BlockageLab lab = new BlockageLab();
                NarrativeWorldState world = new NarrativeWorldState(29029);
                EntityId player = world.NewId("npc");
                EntityId trail = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: 100, zone: trail);
                vanilla.SetAttribute(player, VanillaAttribute.Strength, 18);
                vanilla.SetAttribute(player, VanillaAttribute.Endurance, 16);
                vanilla.SetSkill(player, VanillaSkill.Mining, 18);
                vanilla.SetSkill(player, VanillaSkill.Travel, 12);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 16);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = BlockedPassageSituation.Create(world, stager, player, trail, vanilla.Now);
                lab.Situation.StockThePlayer(world, stager, player);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            public ActionContext Context(EntityId zone, EntityId target)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target)
            {
                return Actions.Get(actionId).Perform(Context(zone, target));
            }

            public Availability Can(string actionId, EntityId zone, EntityId target)
            {
                return Actions.Get(actionId).GetAvailability(Context(zone, target));
            }
        }
    }
}
