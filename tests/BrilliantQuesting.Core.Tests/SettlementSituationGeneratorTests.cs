using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SettlementSituationGeneratorTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Market = EntityId.Parse("zone_market");
        private static readonly EntityId Hamlet = EntityId.Parse("zone_hamlet");

        [Fact]
        public void QuietSettlementDoesNotGenerateBecauseAQuestIsNeeded()
        {
            Lab lab = new Lab(Market);
            lab.Local("baker", "Baker", money: 300, pickpocket: 0, carriedValue: 0);
            lab.Local("neighbour", "Neighbour", money: 220, pickpocket: 0, carriedValue: 0);
            lab.Local("porter", "Porter", money: 180, pickpocket: 0, carriedValue: 0);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.Empty(plan.Candidates);
            Assert.Contains("valuable carried objects: 0", plan.Profile.Features);
        }

        [Fact]
        public void FreshSaveGeneratesTheftFromLocalPressure()
        {
            Lab lab = PressuredMarket();

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Single(lab.World.Threads);
            Assert.Equal(ThreadState.Active, situation.Thread.State);
            Assert.NotEmpty(situation.Thread.GenerationCauses);
            Assert.DoesNotContain(situation.Thread.OpenQuestions, q => q.StartsWith("Cause: "));
            Assert.Contains(lab.Item, lab.Vanilla.GetInventory(lab.Thief).Select(i => i.Id));
            Assert.DoesNotContain(lab.Item, lab.Vanilla.GetInventory(lab.Victim).Select(i => i.Id));
            Assert.False(lab.World.Knowledge.Knows(Player, situation.TheftFactId));
            Assert.True(lab.World.Knowledge.TryGetBelief(lab.Witness, situation.TheftFactId, out KnowledgeRecord witnessed));
            Assert.Equal(KnowledgeSource.Witnessed, witnessed.Source);
        }

        [Fact]
        public void GeneratedSituationSurvivesSaveReloadWithoutRedispatch()
        {
            Lab lab = PressuredMarket();
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Single(reloaded.Threads);
            NarrativeThread thread = reloaded.Threads[0];
            Assert.Equal(PettyTheftSituation.ArchetypeId, thread.ArchetypeId);
            Assert.Equal(situation.Thread.OriginEventId, thread.OriginEventId);
            Assert.NotEmpty(thread.GenerationCauses);
            Assert.Single(reloaded.Ledger.Events, e => e.Type == BrilliantQuesting.Events.WorldEventType.Theft);
        }

        [Fact]
        public void InspectorNamesTheWorldStateThatCausedGeneration()
        {
            Lab lab = PressuredMarket();
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);
            ActionContext context = new ActionContext(
                lab.World,
                lab.Vanilla,
                new FixedCheckResolver(CheckOutcome.Pass),
                lab.World.Rng,
                Player,
                lab.Victim);

            string report = NarrativeInspector.Explain(
                lab.World,
                lab.Vanilla,
                StandardActions.CreateRegistry(),
                context,
                situation.Thread);

            Assert.Contains("generated from world state", report);
            Assert.Contains("Cutpurse has motive", report);
            Assert.Contains("Merchant is a target", report);
        }

        [Fact]
        public void DifferentSettlementStructuresYieldDifferentCandidateDistributions()
        {
            Lab market = PressuredMarket();
            Lab hamlet = new Lab(Hamlet);
            hamlet.Local("farmer", "Farmer", money: 80, pickpocket: 0, carriedValue: 120);
            hamlet.Local("miner", "Miner", money: 70, pickpocket: 1, carriedValue: 0);
            hamlet.Local("herbalist", "Herbalist", money: 60, pickpocket: 0, carriedValue: 90);

            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            SettlementSituationPlan marketPlan = generator.Evaluate(market.World, market.Vanilla, Market);
            SettlementSituationPlan hamletPlan = generator.Evaluate(hamlet.World, hamlet.Vanilla, Hamlet);

            Assert.NotEmpty(marketPlan.Candidates);
            Assert.Empty(hamletPlan.Candidates);
            Assert.NotEqual(
                string.Join("|", marketPlan.Profile.Features),
                string.Join("|", hamletPlan.Profile.Features));
        }

        private static Lab PressuredMarket()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, pickpocket: 0, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, pickpocket: 8, carriedValue: 0, stealth: 6);
            lab.Witness = lab.Local("clerk", "Clerk", money: 140, pickpocket: 0, carriedValue: 0);
            lab.Item = EntityId.Parse("item_merchant_valuable");
            return lab;
        }

        private sealed class Lab
        {
            public readonly NarrativeWorldState World = new NarrativeWorldState(42);
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            private readonly EntityId _zone;

            public EntityId Victim;
            public EntityId Thief;
            public EntityId Witness;
            public EntityId Item;

            public Lab(EntityId zone)
            {
                _zone = zone;
                Vanilla.Define(Player, zone: zone);
            }

            public EntityId Local(
                string key,
                string name,
                int money,
                int pickpocket,
                int carriedValue,
                int stealth = 0,
                string occupation = "local")
            {
                EntityId id = EntityId.Parse("npc_" + key);
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(id, name)
                {
                    Occupation = occupation,
                    Importance = NarrativeImportance.Background
                });
                npc.Personality.Greed = money < 80 ? 0.8 : 0.35;

                Vanilla.Define(id, money: money, zone: _zone)
                    .SetSkill(id, VanillaSkill.Pickpocket, pickpocket)
                    .SetSkill(id, VanillaSkill.Stealth, stealth)
                    .SetAttribute(id, VanillaAttribute.Dexterity, pickpocket + stealth);

                if (carriedValue > 0)
                {
                    EntityId item = EntityId.Parse("item_" + key + "_valuable");
                    Vanilla.GiveItem(id, new ItemDescriptor(item, name.ToLowerInvariant() + " heirloom", "jewelry", carriedValue, "ring"));
                    if (Item.IsNone)
                    {
                        Item = item;
                    }
                }

                return id;
            }
        }
    }
}
