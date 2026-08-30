using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class FalseAccusationSituationTests
    {
        [Fact]
        public void PlantedStolenEvidenceCanConvictTheInnocentWhileTruthSurvives()
        {
            FrameLab lab = FrameLab.Create();

            ActionOutcome planted = lab.FrameInnocent();

            Assert.True(planted.Succeeded);
            Fact falseTheft = lab.FalseTheftAgainstInnocent();
            Assert.NotNull(falseTheft);
            Assert.Equal(TruthState.False, falseTheft.Truth);
            Assert.Equal(lab.Situation.ItemId, falseTheft.Object);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, falseTheft.Id));

            ActionOutcome report = lab.Report(falseTheft.Id);

            Assert.Contains(report.Events, e =>
                e.Type == WorldEventType.CrimeReported
                && e.Target == lab.Situation.InnocentId
                && e.Related.Contains(falseTheft.Id));
            Assert.True(lab.World.Knowledge.CanProve(lab.Situation.GuardId, falseTheft.Id));

            Fact truth = lab.World.Knowledge.GetFact(lab.Situation.TrueTheftFactId);
            Assert.Equal(TruthState.True, truth.Truth);
            Assert.Equal(lab.Situation.ThiefId, truth.Subject);
            Assert.DoesNotContain(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.CrimeReported
                && e.Target == lab.Situation.ThiefId);
        }

        [Fact]
        public void TheTrueTheftCanStillBeRecoveredAfterTheFalseReport()
        {
            FrameLab lab = FrameLab.Create();
            lab.FrameInnocent();
            Fact falseTheft = lab.FalseTheftAgainstInnocent();
            lab.Report(falseTheft.Id);

            ActionOutcome search = lab.SearchForTruth();

            Assert.True(search.Succeeded);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.TrueTheftFactId));

            ActionOutcome correction = lab.Report(lab.Situation.TrueTheftFactId);

            Assert.Contains(correction.Events, e =>
                e.Type == WorldEventType.CrimeReported
                && e.Target == lab.Situation.ThiefId
                && e.Related.Contains(lab.Situation.TrueTheftFactId));
            Assert.Equal(TruthState.False, lab.FalseTheftAgainstInnocent().Truth);
            Assert.Equal(TruthState.True, lab.World.Knowledge.GetFact(lab.Situation.TrueTheftFactId).Truth);
        }

        [Fact]
        public void FramingAnOrdinaryObjectStillRecordsFalsePossession()
        {
            FrameLab lab = FrameLab.CreateWithoutKnownTheft();

            lab.FrameInnocent();

            Fact lie = lab.World.Knowledge.Facts.Values.Single(f =>
                f.Subject == lab.Situation.InnocentId
                && f.Object == lab.Situation.ItemId
                && f.Truth == TruthState.False);
            Assert.Equal(FactPredicates.Possesses, lie.Predicate);
        }

        private sealed class FrameLab
        {
            private FrameLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public FalseAccusationSituation Situation { get; private set; }

            public static FrameLab Create()
            {
                return Build(removeTheftFact: false);
            }

            public static FrameLab CreateWithoutKnownTheft()
            {
                return Build(removeTheftFact: true);
            }

            private static FrameLab Build(bool removeTheftFact)
            {
                FrameLab lab = new FrameLab();
                NarrativeWorldState world = new NarrativeWorldState(44044);
                EntityId player = world.NewId("npc");
                EntityId market = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: 250, zone: market);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
                lab.Situation = FalseAccusationSituation.Create(world, new SandboxStager(vanilla), player, market, vanilla.Now);

                if (removeTheftFact)
                {
                    world.Knowledge.GetFact(lab.Situation.TrueTheftFactId).Truth = TruthState.Superseded;
                }

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public ActionOutcome FrameInnocent()
            {
                ActionContext context = Context(EntityId.None, EntityId.None);
                context.SubjectItem = Situation.ItemId;
                context.ThirdParty = Situation.InnocentId;
                context.Witnesses.Clear();
                return Actions.Get("frame").Perform(context);
            }

            public ActionOutcome Report(EntityId factId)
            {
                return Actions.Get("report").Perform(Context(Situation.GuardId, factId));
            }

            public ActionOutcome SearchForTruth()
            {
                return Actions.Get("search").Perform(Context(EntityId.None, Situation.TrueTheftFactId));
            }

            public Fact FalseTheftAgainstInnocent()
            {
                return World.Knowledge.Facts.Values.SingleOrDefault(f =>
                    f.Subject == Situation.InnocentId
                    && f.Predicate == FactPredicates.Stole
                    && f.Object == Situation.ItemId
                    && f.Truth == TruthState.False);
            }

            private ActionContext Context(EntityId target, EntityId factId)
            {
                ActionContext context = new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread,
                    SubjectFact = factId
                };

                foreach (EntityId here in Vanilla.GetCharactersInZone(Situation.MarketZoneId))
                {
                    if (here != Player && here != target)
                    {
                        context.Witnesses.Add(here);
                    }
                }

                return context;
            }
        }
    }
}
