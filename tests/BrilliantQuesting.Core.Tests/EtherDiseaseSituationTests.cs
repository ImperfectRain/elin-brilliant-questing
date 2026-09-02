using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class EtherDiseaseSituationTests
    {
        [Fact]
        public void EtherDiseaseRunsEndToEndThroughExistingEtherAntibody()
        {
            EtherLab lab = EtherLab.Create(playerMoney: 500);

            Availability available = lab.Can("buy_supplies", lab.Patient);
            ActionOutcome outcome = lab.Run("buy_supplies", lab.Patient);

            Assert.True(available.IsAvailable);
            Assert.Contains("ether_antibody", available.Reason);
            Assert.True(outcome.Succeeded);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("supplies_bought", lab.Situation.Thread.Resolution);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.CureDemandId).Truth);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.DiseaseFactId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Target == lab.Patient);
            Assert.DoesNotContain(lab.Situation.Thread.GenerationCauses, c => c.Contains("invented", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EtherDiseaseNamesIrvaMaterialAndPersistsIt()
        {
            EtherLab lab = EtherLab.Create(playerMoney: 500);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            NarrativeThread thread = reloaded.GetThread(lab.Situation.Thread.Id);

            Assert.NotNull(thread);
            Assert.Contains(thread.GenerationCauses, c => c.Contains("Noyel"));
            Assert.Contains(thread.GenerationCauses, c => c.Contains("ether disease"));
            Assert.Contains(thread.GenerationCauses, c => c.Contains(EtherDiseaseSituation.CureName));
            Assert.Equal(
                EtherDiseaseSituation.EtherAntibody.ToFactValue(),
                reloaded.Knowledge.GetFact(lab.Situation.CureDemandId).Value);
        }

        [Fact]
        public void EtherAntibodyIsExpensiveRatherThanAFreeCure()
        {
            EtherLab lab = EtherLab.Create(playerMoney: 100);

            Availability available = lab.Can("buy_supplies", lab.Patient);

            Assert.False(available.IsAvailable);
            Assert.Contains("orens", available.Reason);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.CureDemandId).Truth);
        }

        [Fact]
        public void EtherDiseaseProjectsThroughExistingContentClasses()
        {
            EtherLab lab = EtherLab.Create(playerMoney: 500);
            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.CureDemandId,
                KnowledgeSource.Hearsay,
                0.9,
                lab.Vanilla.Now,
                false,
                lab.Situation.KinId);

            var entries = NarrativeContentProjection.Entries(lab.World, lab.Player);
            var board = NarrativeContentProjection.BoardEntries(lab.World, lab.Player);

            Assert.Contains(entries, e => e.ContentClass == NarrativeContentClass.Situation);
            Assert.Contains(entries, e => e.ContentClass == NarrativeContentClass.Opportunity
                                          && e.FactId == lab.Situation.DiseaseFactId);
            Assert.Contains(board, e => e.ContentClass == NarrativeContentClass.Request
                                        && e.FactId == lab.Situation.CureDemandId);
        }

        [Fact]
        public void InspectorShowsTheIrvaPremise()
        {
            EtherLab lab = EtherLab.Create(playerMoney: 500);

            string report = NarrativeInspector.Explain(
                lab.World,
                lab.Vanilla,
                lab.Actions,
                lab.Context(lab.Patient),
                lab.Situation.Thread);

            Assert.Contains("Noyel", report);
            Assert.Contains("ether disease", report);
            Assert.Contains(EtherDiseaseSituation.CureName, report);
        }

        private sealed class EtherLab
        {
            private EtherLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public EtherDiseaseSituation Situation { get; private set; }

            public EntityId Patient => Situation.PatientId;

            public static EtherLab Create(int playerMoney)
            {
                EtherLab lab = new EtherLab();
                NarrativeWorldState world = new NarrativeWorldState(126);
                EntityId player = world.NewId("npc");
                EntityId clinic = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, money: playerMoney, zone: clinic);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Situation = EtherDiseaseSituation.Create(world, new SandboxStager(vanilla), player, clinic, vanilla.Now);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            public ActionContext Context(EntityId target)
            {
                Vanilla.SetZone(Player, Situation.ClinicZoneId);
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }

            public ActionOutcome Run(string actionId, EntityId target) => Actions.Get(actionId).Perform(Context(target));

            public Availability Can(string actionId, EntityId target) => Actions.Get(actionId).GetAvailability(Context(target));
        }
    }
}
