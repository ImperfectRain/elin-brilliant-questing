using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ActorLocalInterpretationTests
    {
        private static readonly EntityId Crop = EntityId.Parse("item_blighted_crop");
        private static readonly EntityId Sample = EntityId.Parse("item_crop_sample");
        private static readonly EntityId SourceFact = EntityId.Parse("fact_crop_damage");

        [Fact]
        public void ThreeObserversDeriveThreeDifferentFactsFromOnePieceOfEvidence()
        {
            NarrativeWorldState world = WorldWithCropEvidence();
            NarrativeNpc farmer = Actor("farmer", "farmer");
            NarrativeNpc alchemist = Actor("alchemist", "alchemist");
            NarrativeNpc reeve = Actor("reeve", "reeve");
            reeve.Roles.Add("authority");
            reeve.Values.Law.Importance = 1.0;
            reeve.Sensitivities.Dishonesty = 1.0;

            world.Registry.Add(farmer);
            world.Registry.Add(alchemist);
            world.Registry.Add(reeve);

            ActorInterpretationTrace farmTrace = ActorLocalInterpreter.Interpret(world, farmer.Id, SourceFact, GameTime.Zero);
            ActorInterpretationTrace alchemyTrace = ActorLocalInterpreter.Interpret(world, alchemist.Id, SourceFact, GameTime.Zero);
            ActorInterpretationTrace lawTrace = ActorLocalInterpreter.Interpret(world, reeve.Id, SourceFact, GameTime.Zero);

            HashSet<string> predicates = new HashSet<string>
            {
                farmTrace.DerivedPredicate,
                alchemyTrace.DerivedPredicate,
                lawTrace.DerivedPredicate
            };

            Assert.Equal(3, predicates.Count);
            Assert.Equal(FactPredicates.HasSoilTrouble, farmTrace.DerivedPredicate);
            Assert.Equal(FactPredicates.IsContaminated, alchemyTrace.DerivedPredicate);
            Assert.Equal(FactPredicates.MayBeSabotaged, lawTrace.DerivedPredicate);

            AssertOnlyKnows(world, farmer.Id, farmTrace.DerivedFactId);
            AssertOnlyKnows(world, alchemist.Id, alchemyTrace.DerivedFactId);
            AssertOnlyKnows(world, reeve.Id, lawTrace.DerivedFactId);

            foreach (ActorInterpretationTrace trace in new[] { farmTrace, alchemyTrace, lawTrace })
            {
                Fact derived = world.Knowledge.GetFact(trace.DerivedFactId);
                Assert.Equal(TruthState.Uncertain, derived.Truth);
                Assert.Equal(SourceFact, derived.DistortionOf);
                Assert.Contains(Sample, derived.EvidenceIds);
                Assert.False(world.Knowledge.CanProve(trace.ActorId, trace.DerivedFactId));
                Assert.Equal(KnowledgeSource.Inference, world.Knowledge.BeliefsOf(trace.ActorId).Single().Source);
            }
        }

        [Fact]
        public void InterpretationTraceIsInspectable()
        {
            NarrativeWorldState world = WorldWithCropEvidence();
            NarrativeNpc alchemist = Actor("alchemist", "apothecary");
            alchemist.Values.Knowledge.Importance = 1.0;
            world.Registry.Add(alchemist);

            ActorInterpretationTrace trace = ActorLocalInterpreter.Interpret(world, alchemist.Id, SourceFact, GameTime.Zero);
            string report = NarrativeInspector.DescribeInterpretation(world, trace);

            Assert.Contains("interpretation for alchemist", report);
            Assert.Contains("source: damaged", report);
            Assert.Contains("lens: alchemical", report);
            Assert.Contains("derived fact: is contaminated", report);
            Assert.Contains("via inference", report);
            Assert.Contains("occupation alchemy", report);
            Assert.Contains("value knowledge", report);
        }

        private static NarrativeWorldState WorldWithCropEvidence()
        {
            NarrativeWorldState world = new NarrativeWorldState(64);
            Fact damaged = new Fact(SourceFact, Crop, FactPredicates.Damaged, EntityId.None, "blighted crop");
            damaged.EvidenceIds.Add(Sample);
            world.Knowledge.AddFact(damaged);
            return world;
        }

        private static NarrativeNpc Actor(string key, string occupation)
        {
            NarrativeNpc npc = new NarrativeNpc(EntityId.Parse("npc_" + key), key)
            {
                Occupation = occupation
            };

            npc.Values.Wealth.Importance = 0.0;
            npc.Values.Animals.Importance = 0.0;
            npc.Values.Knowledge.Importance = 0.0;
            npc.Values.Law.Importance = 0.0;
            npc.Sensitivities.Animals = 0.0;
            npc.Sensitivities.Theft = 0.0;
            npc.Sensitivities.Dishonesty = 0.0;
            return npc;
        }

        private static void AssertOnlyKnows(NarrativeWorldState world, EntityId actor, EntityId factId)
        {
            Assert.True(world.Knowledge.Knows(actor, factId));
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Id != factId)
                {
                    Assert.False(world.Knowledge.Knows(actor, fact.Id));
                }
            }
        }
    }
}
