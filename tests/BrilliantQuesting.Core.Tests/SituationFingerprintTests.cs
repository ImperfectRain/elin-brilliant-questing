using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SituationFingerprintTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Speaker = EntityId.Parse("npc_speaker");
        private static readonly EntityId Zone = EntityId.Parse("zone_market");

        [Fact]
        public void QualityDiversitySelectsFestivalOverThirdViolentMatterAndExplainsWhy()
        {
            var (world, vanilla) = Town();
            var talk = new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            for (int i = 0; i < 2; i++)
            {
                var prior = Matter(world, vanilla, "violence_" + i, FactPredicates.Killed);
                prior.Tension = 100;
                Assert.True(talk.Deliver(world, vanilla, talk.Next(world, vanilla, vanilla.Now), vanilla.Now));
                prior.State = ThreadState.Resolved;
                vanilla.Now = vanilla.Now.PlusMinutes(100);
            }
            var violent = Matter(world, vanilla, "third_violence", FactPredicates.Killed);
            violent.Tension = 100;
            var festival = Matter(world, vanilla, "festival_rivalry", FactPredicates.WonCompetition);
            string saved = WorldStateSerializer.Save(world);
            var selected = talk.Next(world, vanilla, vanilla.Now, out _, out var scores);
            var violenceScore = scores.Single(s => s.FactId == violent.FactIds[0]);
            var festivalScore = scores.Single(s => s.FactId == festival.FactIds[0]);
            // The BQ-101 ranking alone still favors violence. The new niche term changes delivery.
            Assert.True(violenceScore.Total - violenceScore.NicheDiversity
                > festivalScore.Total - festivalScore.NicheDiversity);
            Assert.Equal(festival.FactIds[0], selected.FactId);
            string report = NarrativeInspector.DescribeAmbientTalk(world, vanilla);
            Assert.Contains("niche=social, recent occupancy=0/2", report);
            Assert.Contains("niche=violent, recent occupancy=2/2", report);
            Assert.Contains("niche diversity bonus=0.750", report);
            Assert.Contains("director " + festival.FactIds[0] + " via " + Speaker + ": selected for delivery", report);
            Assert.Equal(saved, WorldStateSerializer.Save(world));
            world = WorldStateSerializer.Load(saved);
            world.Threads.Reverse();
            Assert.Equal(report, NarrativeInspector.DescribeAmbientTalk(world, vanilla));
            talk = new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            Assert.True(talk.Deliver(world, vanilla, talk.Next(world, vanilla, vanilla.Now), vanilla.Now));
            var nextFestival = Matter(world, vanilla, "another_festival", FactPredicates.WonCompetition);
            Assert.True(Score(world, vanilla, nextFestival).NicheDiversity < festivalScore.NicheDiversity);
        }

        [Fact]
        public void NicheOccupancyRewardsUnderrepresentationWithoutOverridingQuality()
        {
            var (world, vanilla) = Town();
            foreach (string predicate in new[] { FactPredicates.Killed, FactPredicates.Killed, FactPredicates.WonCompetition })
            {
                var prior = Matter(world, vanilla, world.Threads.Count.ToString(), predicate);
                Learn(world, prior);
                prior.State = ThreadState.Resolved;
            }
            var social = Matter(world, vanilla, "social", FactPredicates.WonCompetition);
            var violent = Matter(world, vanilla, "violent", FactPredicates.Killed);
            Assert.Equal(0.5, Score(world, vanilla, social).NicheDiversity, 8);
            Assert.Equal(0.25, Score(world, vanilla, violent).NicheDiversity, 8);
            var highQuality = DevelopmentScoring.Read(world, vanilla, Speaker, violent.FactIds[0], 5, vanilla.Now);
            Assert.True(highQuality.Total > Score(world, vanilla, social).Total);
        }

        [Fact]
        public void NicheBonusRequiresKnownCandidateAndRecentClassifiedHistory()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Killed);
            var social = Matter(world, vanilla, "social", FactPredicates.WonCompetition);
            var unknown = Matter(world, vanilla, "unknown", FactPredicates.IsDead);
            Assert.Equal(0, Score(world, vanilla, social).NicheDiversity);
            Learn(world, unknown);
            Assert.Equal(0, Score(world, vanilla, social).NicheDiversity);
            Learn(world, prior);
            Assert.Equal(0, Score(world, vanilla, unknown).NicheDiversity);
            Assert.Equal(0.75, Score(world, vanilla, social).NicheDiversity);
            prior.State = ThreadState.Quarantined;
            Assert.Equal(0, Score(world, vanilla, social).NicheDiversity);
            prior.State = ThreadState.Resolved;
            vanilla.Now = new GameTime(-1);
            Assert.Equal(0.75, Score(world, vanilla, social).NicheDiversity);
            vanilla.Now = GameTime.FromDays(7);
            Assert.Equal(0, Score(world, vanilla, social).NicheDiversity);
        }

        [Fact]
        public void DuplicateCarriersAndHiddenClaimsDoNotAlterNicheOccupancy()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Killed);
            Learn(world, prior);
            var social = Matter(world, vanilla, "social", FactPredicates.WonCompetition);
            Learn(world, social);
            var next = Matter(world, vanilla, "next", FactPredicates.WonCompetition);
            double before = Score(world, vanilla, next).NicheDiversity;
            for (int i = 0; i < 4; i++)
            {
                var copy = new NarrativeThread(world.NewId("thread"), "copy", GameTime.Zero);
                copy.FactIds.Add(prior.FactIds[0]);
                world.Threads.Add(copy);
            }
            var hidden = new Fact(world.NewId("fact"), Speaker, FactPredicates.Killed, Player, secrecy: 100);
            world.Knowledge.AddFact(hidden);
            next.FactIds.Add(hidden.Id);
            social.FactIds.Add(hidden.Id);
            Assert.Equal(before, Score(world, vanilla, next).NicheDiversity);
            Assert.Equal(0.75, Score(world, vanilla, prior).NicheDiversity); // Own carriers excluded.
        }

        [Fact]
        public void DifferentNounsAndArchetypesStillRepeatTheSameExperience()
        {
            var (world, vanilla) = Town();
            var first = Matter(world, vanilla, "first", FactPredicates.Owes);
            var next = Matter(world, vanilla, "unrelated_name", FactPredicates.Needs);
            var alternative = Matter(world, vanilla, "third", FactPredicates.Killed);
            double before = Score(world, vanilla, next).Total;
            Learn(world, first);
            var repeated = Score(world, vanilla, next);
            Assert.Equal(0, repeated.Repetition); // Different archetypes defeat the old term.
            Assert.True(repeated.ShapeRepetition > 0);
            Assert.True(repeated.Total < before);
            Assert.True(repeated.Total < Score(world, vanilla, alternative).Total);
            Assert.Contains("economic", repeated.FingerprintEvidence);
            Assert.Contains(first.Id.Value, repeated.FingerprintEvidence);
        }

        [Fact]
        public void DeliveryChangesTheNextAmbientChoiceWithoutSelectionSpendingExposure()
        {
            var (world, vanilla) = Town();
            var first = Matter(world, vanilla, "first", FactPredicates.Owes);
            var second = Matter(world, vanilla, "second", FactPredicates.Owes);
            var alternative = Matter(world, vanilla, "alternative", FactPredicates.Owes);
            // The same economic problem offers a different mechanical route.
            first.RecoveryRoutes.Add(Route("persuade"));
            second.RecoveryRoutes.Add(Route("persuade"));
            alternative.RecoveryRoutes.Add(Route("craft_supplies"));
            var talk = new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            var proposal = talk.Next(world, vanilla, vanilla.Now);
            Assert.Equal(first.FactIds[0], proposal.FactId);
            Assert.Equal(0, Score(world, vanilla, second).ShapeRepetition);
            Assert.True(talk.Deliver(world, vanilla, proposal, vanilla.Now));
            vanilla.Now = vanilla.Now.PlusMinutes(100);
            Assert.Equal(alternative.FactIds[0], talk.Next(world, vanilla, vanilla.Now).FactId);
            string report = NarrativeInspector.DescribeAmbientTalk(world, vanilla);
            Assert.Contains("shape repetition penalty=", report);
            Assert.Contains("declared routes=persuade", report);
            Assert.False(world.Knowledge.Knows(Player, second.FactIds[0]));
        }

        [Fact]
        public void HiddenFactsAndUnseenSituationsDoNotManufactureRepetition()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Owes);
            var next = Matter(world, vanilla, "next", FactPredicates.Owes);
            Assert.Equal(0, Score(world, vanilla, next).ShapeRepetition);
            Learn(world, prior);
            string before = Shape(world, next).Explain();
            double penalty = Score(world, vanilla, next).ShapeRepetition;
            var secret = new Fact(world.NewId("fact"), Speaker, FactPredicates.Killed, Player, secrecy: 100);
            world.Knowledge.AddFact(secret);
            next.FactIds.Add(secret.Id);
            prior.FactIds.Add(secret.Id);
            Assert.Equal(before, Shape(world, next).Explain());
            Assert.Equal(penalty, Score(world, vanilla, next).ShapeRepetition);
            Assert.DoesNotContain("violent", Score(world, vanilla, next).FingerprintEvidence);
        }

        [Fact]
        public void UnclassifiedClaimsDoNotMatchEachOtherOrInventViolentDeaths()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", "custom_unknown");
            var next = Matter(world, vanilla, "next", FactPredicates.IsDead);
            Learn(world, prior);
            Assert.Null(Shape(world, next).Domains);
            Assert.Equal(0, Score(world, vanilla, next).ShapeRepetition);
            Assert.Contains("domains=unknown", Score(world, vanilla, next).FingerprintEvidence);
            Assert.Null(Shape(world, next).Routes);
        }

        [Fact]
        public void PaceSecrecyAndRecordedFamiliarityDistinguishShapes()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Owes);
            var next = Matter(world, vanilla, "next", FactPredicates.Owes);
            var original = Shape(world, next);
            next.Tension = 80;
            world.Knowledge.GetFact(next.FactIds[0]).Secrecy = 50;
            Assert.True(original.Similarity(Shape(world, next)) < original.Similarity(original));
            Assert.False(original.RecurringPeople);
            Fact subject = world.Knowledge.GetFact(next.FactIds[0]);
            var history = new Fact(world.NewId("fact"), subject.Subject, FactPredicates.RelatedTo, Speaker);
            world.Knowledge.AddFact(history);
            world.Knowledge.Teach(Player, history.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, false, Speaker);
            Assert.True(Shape(world, next).RecurringPeople);
        }

        [Fact]
        public void ResolvedRecentMattersCountButOldOrQuarantinedOnesDoNot()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Owes);
            var next = Matter(world, vanilla, "next", FactPredicates.Owes);
            Learn(world, prior);
            prior.State = ThreadState.Resolved;
            Assert.True(Score(world, vanilla, next).ShapeRepetition > 0);
            prior.State = ThreadState.Quarantined;
            Assert.Equal(0, Score(world, vanilla, next).ShapeRepetition);
            prior.State = ThreadState.Resolved;
            vanilla.Now = GameTime.FromDays(7);
            Assert.Equal(0, Score(world, vanilla, next).ShapeRepetition);
        }

        [Fact]
        public void OnlyTheThreeMostRecentlyEncounteredMattersContribute()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Owes);
            Learn(world, prior);
            var next = Matter(world, vanilla, "next", FactPredicates.Owes);
            for (int i = 1; i <= 3; i++)
            {
                var other = Matter(world, vanilla, "other" + i, FactPredicates.Killed);
                world.Knowledge.Teach(Player, other.FactIds[0], KnowledgeSource.Hearsay,
                    0.8, new GameTime(i), false, Speaker);
            }
            Assert.Equal(0, Score(world, vanilla, next).ShapeRepetition);
        }

        [Fact]
        public void SharedClaimsDoNotCountAsASecondExperienceAndReloadIsReadOnly()
        {
            var (world, vanilla) = Town();
            var prior = Matter(world, vanilla, "prior", FactPredicates.Owes);
            var next = Matter(world, vanilla, "next", FactPredicates.Owes);
            Learn(world, prior);
            var copy = new NarrativeThread(world.NewId("thread"), "copy", GameTime.Zero);
            copy.FactIds.Add(prior.FactIds[0]);
            world.Threads.Add(copy);
            Assert.Equal(0, Score(world, vanilla, prior).ShapeRepetition);
            string saved = WorldStateSerializer.Save(world);
            string reading = Score(world, vanilla, next).Explain();
            Assert.Equal(saved, WorldStateSerializer.Save(world));
            world = WorldStateSerializer.Load(saved);
            world.Threads.Reverse();
            Assert.Equal(reading, Score(world, vanilla, world.GetThread(next.Id)).Explain());
            vanilla.Now = new GameTime(-1);
            Assert.True(Score(world, vanilla, world.GetThread(next.Id)).ShapeRepetition > 0);
        }

        [Fact]
        public void DuplicateCarriersCannotCrowdADistinctEncounterOutOfTheRecentWindow()
        {
            var (world, vanilla) = Town();
            var economic = Matter(world, vanilla, "economic", FactPredicates.Owes);
            Learn(world, economic);
            var violent = Matter(world, vanilla, "violent", FactPredicates.Killed);
            world.Knowledge.Teach(Player, violent.FactIds[0], KnowledgeSource.Hearsay,
                0.8, new GameTime(20), false, Speaker);
            for (int i = 0; i < 4; i++)
            {
                var carrier = new NarrativeThread(world.NewId("thread"), "carrier", GameTime.Zero);
                carrier.FactIds.Add(violent.FactIds[0]);
                world.Threads.Add(carrier);
            }
            var next = Matter(world, vanilla, "next", FactPredicates.Needs);
            Assert.True(Score(world, vanilla, next).ShapeRepetition > 0);
        }

        [Fact]
        public void RouteOrderMultiplicityAndClaimTruthDoNotChangeShape()
        {
            var (world, vanilla) = Town();
            var matter = Matter(world, vanilla, "matter", FactPredicates.Owes);
            matter.RecoveryRoutes.Add(Route("persuade"));
            matter.RecoveryRoutes.Add(Route("pay_debt"));
            string before = Shape(world, matter).Explain();
            matter.RecoveryRoutes.Reverse();
            matter.RecoveryRoutes.Add(Route("persuade"));
            world.Knowledge.GetFact(matter.FactIds[0]).Truth = TruthState.False;
            Assert.Equal(before, Shape(world, matter).Explain());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FamiliarityReadsObservedPriorHistoryWithoutExposingOffScreenPeople(bool observed)
        {
            var (world, vanilla) = Town();
            var matter = Matter(world, vanilla, "matter", FactPredicates.Owes);
            var actor = world.Knowledge.GetFact(matter.FactIds[0]).Subject;
            world.Ledger.Append(new WorldEvent(world.NewId("event"), WorldEventType.Helped,
                actor, Speaker, GameTime.Zero, witnesses: observed ? new[] { Player } : new EntityId[0]));
            Assert.Equal(observed, Shape(world, matter).RecurringPeople);
        }

        private static RecoveryRoute Route(string id) => new RecoveryRoute("loss", id, "", "", "");
        private static SituationFingerprint Shape(NarrativeWorldState world, NarrativeThread matter) =>
            SituationFingerprint.Read(world, Player, matter, new GameTime(100), matter.FactIds[0]);
        private static DevelopmentScore Score(NarrativeWorldState world, SandboxVanillaState vanilla, NarrativeThread matter) =>
            DevelopmentScoring.Read(world, vanilla, Speaker, matter.FactIds[0], 1.9, vanilla.Now);
        private static void Learn(NarrativeWorldState world, NarrativeThread matter) =>
            world.Knowledge.Teach(Player, matter.FactIds[0], KnowledgeSource.Hearsay, 0.8, GameTime.Zero, false, Speaker);

        private static (NarrativeWorldState, SandboxVanillaState) Town()
        {
            var world = new NarrativeWorldState(101);
            var vanilla = new SandboxVanillaState(Player) { Now = new GameTime(100) };
            foreach (var actor in new[] { Player, Speaker })
            {
                world.Registry.Add(new NarrativeNpc(actor, actor.Value));
                vanilla.Define(actor, zone: Zone);
            }
            return (world, vanilla);
        }

        private static NarrativeThread Matter(NarrativeWorldState world, SandboxVanillaState vanilla,
            string archetype, string predicate)
        {
            var actor = world.NewId("npc");
            world.Registry.Add(new NarrativeNpc(actor, archetype));
            vanilla.Define(actor, zone: Zone);
            var fact = new Fact(world.NewId("fact"), actor, predicate, Player, archetype + " goods");
            world.Knowledge.AddFact(fact);
            world.Knowledge.Teach(Speaker, fact.Id, KnowledgeSource.Hearsay, 0.9, GameTime.Zero, false, actor);
            var matter = new NarrativeThread(world.NewId("thread"), archetype, GameTime.Zero) { State = ThreadState.Active };
            matter.FactIds.Add(fact.Id);
            world.Threads.Add(matter);
            return matter;
        }
    }
}
