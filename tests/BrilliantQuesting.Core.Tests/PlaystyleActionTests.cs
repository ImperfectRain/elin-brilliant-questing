using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
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
    /// <summary>BQ-122: route situations into performing, museum, ranch, fishing and farming playstyles.</summary>
    public class PlaystyleActionTests
    {
        [Fact]
        public void Bq122PlaystyleVerbsAreRegisteredWithTheirMechanicalProfiles()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            Assert.Equal(ActionFamily.Social, registry.Get("perform").Family);
            Assert.Equal("proc_performance", ProceduralCheckProfiles.ForAction("perform").Id);
            Assert.Equal(ActionFamily.Economic, registry.Get("donate_to_museum").Family);
            Assert.Null(ProceduralCheckProfiles.ForAction("donate_to_museum"));
            Assert.Equal(ActionFamily.HomeCommunity, registry.Get("give_bred_animal").Family);
            Assert.Null(ProceduralCheckProfiles.ForAction("give_bred_animal"));
            Assert.Equal(ActionFamily.Crafting, registry.Get("deliver_fishing_haul").Family);
            Assert.Equal("proc_fishing_haul", ProceduralCheckProfiles.ForAction("deliver_fishing_haul").Id);
            Assert.Equal(ActionFamily.Crafting, registry.Get("deliver_harvest").Family);
            Assert.Equal("proc_harvest", ProceduralCheckProfiles.ForAction("deliver_harvest").Id);
        }

        [Fact]
        public void PerformanceCanResolveASocialProblem()
        {
            Lab lab = Lab.Create();
            EntityId trouble = lab.SocialTrouble(lab.Neighbor, "social shame after the failed feast");

            ActionContext context = lab.Context(lab.Neighbor);
            context.SubjectFact = trouble;
            ActionOutcome outcome = lab.Actions.Get("perform").Perform(context);

            Assert.True(outcome.Succeeded);
            Assert.Equal("proc_performance", outcome.Check.ProfileId);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(trouble).Truth);
            Assert.Equal(ThreadState.Resolved, lab.Thread.State);
            Assert.Equal("performed_for_them", lab.Thread.Resolution);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Target == lab.Neighbor);
        }

        [Fact]
        public void MuseumDonationSettlesADebtOfHonourByMovingARealObject()
        {
            Lab lab = Lab.Create();
            EntityId debt = lab.DebtOfHonour(lab.Neighbor, lab.Curator);
            EntityId relic = lab.PlayerItem("a foxfire reliquary", "artifact", 600);

            ActionContext context = lab.Context(lab.Curator);
            context.ThirdParty = lab.Neighbor;
            ActionOutcome outcome = lab.Actions.Get("donate_to_museum").Perform(context);

            Assert.Null(outcome.Check);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(debt).Truth);
            Assert.Contains(relic, lab.Held(lab.Curator));
            Assert.DoesNotContain(relic, lab.Held(lab.Player));
            Assert.Equal(ThreadState.Resolved, lab.Thread.State);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.ItemGiven && e.Evidence.Contains(relic));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.DebtPaid && e.Related.Contains(debt));
        }

        [Fact]
        public void BredAnimalGiftMovesTheAnimalAndChangesRelationship()
        {
            Lab lab = Lab.Create();
            EntityId giftMatter = lab.SocialTrouble(lab.Neighbor, "lonely and asking for animal companionship");
            int before = lab.Vanilla.GetAffinity(lab.Neighbor);
            EntityId goat = lab.PlayerBredAnimal("a piebald goat");

            ActionContext context = lab.Context(lab.Neighbor);
            context.SubjectFact = giftMatter;
            ActionOutcome outcome = lab.Actions.Get("give_bred_animal").Perform(context);

            Assert.Null(outcome.Check);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(giftMatter).Truth);
            Assert.Contains(goat, lab.Held(lab.Neighbor));
            Assert.True(lab.Vanilla.GetAffinity(lab.Neighbor) > before);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.ItemGiven && e.Evidence.Contains(goat));
        }

        [Fact]
        public void BredAnimalGiftDoesNotResolveAnUnrelatedThread()
        {
            Lab lab = Lab.Create();
            lab.PlayerBredAnimal("a piebald goat");

            Availability availability = lab.Actions.Get("give_bred_animal").GetAvailability(lab.Context(lab.Neighbor));

            Assert.False(availability.IsAvailable);
            Assert.Equal(ThreadState.Active, lab.Thread.State);
        }

        [Fact]
        public void FishingHaulCanAnswerAShortage()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.LeaveOnly("river trout");
            EntityId fish = lab.PlayerItem("river trout", "fish", 35);

            ActionOutcome outcome = lab.Run("deliver_fishing_haul", lab.Situation.ReeveId);

            Assert.True(outcome.Succeeded);
            Assert.Equal("proc_fishing_haul", outcome.Check.ProfileId);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.BreadDemandId).Truth);
            Assert.DoesNotContain(fish, lab.Held(lab.Player));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Evidence.Contains(fish));
        }

        [Fact]
        public void HarvestCanAnswerAShortageAsAFarmingRoute()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.LeaveOnly("winter turnips");
            EntityId harvest = lab.PlayerItem("winter turnips", "vegetable", 30);

            ActionOutcome outcome = lab.Run("deliver_harvest", lab.Situation.ReeveId);

            Assert.True(outcome.Succeeded);
            Assert.Equal("proc_harvest", outcome.Check.ProfileId);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.BreadDemandId).Truth);
            Assert.DoesNotContain(harvest, lab.Held(lab.Player));
        }

        [Fact]
        public void FishingHaulDoesNotAnswerMedicineJustBecauseItIsAShortage()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.LeaveOnly("river trout");
            lab.PlayerItem("river trout", "fish", 35);

            Availability availability = lab.Can("deliver_fishing_haul", lab.Situation.PhysicianId);

            Assert.False(availability.IsAvailable);
            Assert.Contains("does not answer medicine", availability.Reason);
            Assert.Equal(TruthState.True, lab.World.Knowledge.GetFact(lab.Situation.RemedyDemandId).Truth);
        }

        [Fact]
        public void Bq122ResolvedRoutesStayInRewardVocabularyAfterSaveReload()
        {
            Lab lab = Lab.Create();
            lab.DebtOfHonour(lab.Neighbor, lab.Curator);
            lab.PlayerItem("a silver mural fragment", "artifact", 800);
            lab.Actions.Get("donate_to_museum").Perform(lab.Context(lab.Curator));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            Rewards.ResolutionRewardReport report = new Rewards.ResolutionRewardAudit(reloaded, lab.Player).AuditResolvedThreads();

            Assert.Empty(report.ForbiddenItemPayouts);
            Assert.Contains(Rewards.ResolutionRewardKind.Property, report.Kinds);
        }

        private sealed class Lab
        {
            private Lab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public NarrativeThread Thread { get; private set; }

            public EntityId Player { get; private set; }

            public EntityId Neighbor { get; private set; }

            public EntityId Curator { get; private set; }

            public EntityId Zone { get; private set; }

            public static Lab Create()
            {
                Lab lab = new Lab();
                lab.World = new NarrativeWorldState(122);
                lab.Player = lab.World.NewId("npc");
                lab.Neighbor = lab.World.NewId("npc");
                lab.Curator = lab.World.NewId("npc");
                lab.Zone = lab.World.NewId("zone");
                lab.Vanilla = new SandboxVanillaState(lab.Player);
                lab.Vanilla.Define(lab.Player, level: 6, money: 500, zone: lab.Zone)
                    .SetSkill(lab.Player, VanillaSkill.Music, 12)
                    .SetSkill(lab.Player, VanillaSkill.Fishing, 12)
                    .SetSkill(lab.Player, VanillaSkill.Farming, 12);
                lab.Vanilla.Define(lab.Neighbor, zone: lab.Zone);
                lab.Vanilla.Define(lab.Curator, zone: lab.Zone);
                lab.World.Registry.Add(new NarrativeNpc(lab.Player, "You") { Importance = NarrativeImportance.Major });
                lab.World.Registry.Add(new NarrativeNpc(lab.Neighbor, "Nessa") { Importance = NarrativeImportance.Known });
                lab.World.Registry.Add(new NarrativeNpc(lab.Curator, "Calder") { Occupation = "curator", Importance = NarrativeImportance.Known });
                lab.World.Registry.Add(new NarrativeSite(lab.Zone, "Kell's Ford museum", "museum"));
                lab.Actions = StandardActions.CreateRegistry();
                lab.Thread = new NarrativeThread(lab.World.NewId("thread"), "bq122_playstyle_lab", lab.Vanilla.Now)
                {
                    State = ThreadState.Active
                };
                lab.Thread.ParticipantIds.Add(lab.Player);
                lab.Thread.ParticipantIds.Add(lab.Neighbor);
                lab.Thread.ParticipantIds.Add(lab.Curator);
                lab.Thread.SiteIds.Add(lab.Zone);
                lab.World.Threads.Add(lab.Thread);
                new ConsequenceEngine(lab.World, lab.Vanilla).Attach();
                return lab;
            }

            public EntityId SocialTrouble(EntityId subject, string value)
            {
                Fact fact = new Fact(World.NewId("fact"), subject, FactPredicates.AtRisk, EntityId.None, value, TruthState.True);
                World.Knowledge.AddFact(fact);
                Thread.FactIds.Add(fact.Id);
                return fact.Id;
            }

            public EntityId DebtOfHonour(EntityId debtor, EntityId creditor)
            {
                Fact fact = new Fact(World.NewId("fact"), debtor, FactPredicates.Owes, creditor, "an honour debt", TruthState.True);
                World.Knowledge.AddFact(fact);
                Thread.FactIds.Add(fact.Id);
                return fact.Id;
            }

            public EntityId PlayerItem(string name, string category, int value)
            {
                EntityId item = World.NewId("item");
                Vanilla.GiveItem(Player, new ItemDescriptor(item, name, category, value, category));
                return item;
            }

            public EntityId PlayerBredAnimal(string name)
            {
                EntityId animal = PlayerItem(name, "livestock", 120);
                Fact produced = new Fact(World.NewId("fact"), Player, FactPredicates.Produced, animal, "bred at Home", TruthState.True);
                produced.EvidenceIds.Add(animal);
                World.Knowledge.AddFact(produced);
                return animal;
            }

            public ActionContext Context(EntityId target)
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target)
                {
                    Thread = Thread
                };
            }

            public System.Collections.Generic.List<EntityId> Held(EntityId owner)
            {
                return Vanilla.GetInventory(owner).Select(item => item.Id).ToList();
            }
        }

        private sealed class ShortageLab
        {
            private ShortageLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public ShortageSituation Situation { get; private set; }

            public EntityId Player { get; private set; }

            public static ShortageLab Create()
            {
                ShortageLab lab = new ShortageLab();
                lab.World = new NarrativeWorldState(1221);
                lab.Player = lab.World.NewId("npc");
                lab.Vanilla = new SandboxVanillaState(lab.Player);
                EntityId village = lab.World.NewId("zone");
                lab.Vanilla.Define(lab.Player, level: 6, zone: village)
                    .SetSkill(lab.Player, VanillaSkill.Fishing, 12)
                    .SetSkill(lab.Player, VanillaSkill.Farming, 12);
                lab.World.Registry.Add(new NarrativeNpc(lab.Player, "You") { Importance = NarrativeImportance.Major });
                lab.Situation = ShortageSituation.Create(lab.World, new SandboxStager(lab.Vanilla), lab.Player, village, lab.Vanilla.Now);
                lab.Actions = StandardActions.CreateRegistry();
                new ConsequenceEngine(lab.World, lab.Vanilla).Attach();
                return lab;
            }

            public EntityId PlayerItem(string name, string category, int value)
            {
                EntityId item = World.NewId("item");
                Vanilla.GiveItem(Player, new ItemDescriptor(item, name, category, value, category, 40));
                return item;
            }

            public void LeaveOnly(string keep)
            {
                foreach (ItemDescriptor item in Vanilla.GetInventory(Player).ToList())
                {
                    if (!item.Name.Contains(keep))
                    {
                        Vanilla.DestroyItem(item.Id);
                    }
                }
            }

            public ActionOutcome Run(string actionId, EntityId target)
            {
                return Actions.Get(actionId).Perform(new ActionContext(
                    World,
                    Vanilla,
                    new FixedCheckResolver(CheckOutcome.Pass),
                    World.Rng,
                    Player,
                    target)
                {
                    Thread = Situation.Thread
                });
            }

            public Availability Can(string actionId, EntityId target)
            {
                return Actions.Get(actionId).GetAvailability(new ActionContext(
                    World,
                    Vanilla,
                    new FixedCheckResolver(CheckOutcome.Pass),
                    World.Rng,
                    Player,
                    target)
                {
                    Thread = Situation.Thread
                });
            }

            public System.Collections.Generic.List<EntityId> Held(EntityId owner)
            {
                return Vanilla.GetInventory(owner).Select(item => item.Id).ToList();
            }
        }
    }
}
