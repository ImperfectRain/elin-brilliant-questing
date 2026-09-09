using System;
using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SituationProposalTests
    {
        private static readonly EntityId Actor = EntityId.Parse("npc_existing");

        private static SituationProposal Reuse() => new SituationProposal("reuse",
            new SituationCandidateBuilder("test").Bind("actor", Actor)
                .Pressure("motive", 10, "existing pressure").Build());

        private static SituationProposal Create(int quality = 10) => new SituationProposal("create",
            new SituationCandidateBuilder("test").RequireNewActor("actor", "principal")
                .Pressure("motive", quality, "existing pressure").Build());

        [Fact]
        public void ConservationSelectsReuseOverHigherRawQualityCreationAndExplainsWhy()
        {
            SituationProposal reuse = Reuse();
            SituationProposal create = Create(13);
            var ranked = SituationProposalSelection.Rank(new[] { reuse, create });
            Assert.True(create.Candidate.Score > reuse.Candidate.Score);
            Assert.Same(reuse, ranked[0]); // Creation's three-point quality lead loses to its four-point cost.
            Assert.Same(reuse, SituationProposalSelection.Rank(new[] { Create(9), reuse })[0]);
            Assert.Same(reuse.Candidate, ranked[0].Candidate);
            Assert.Equal(0, reuse.CreationCost);
            Assert.Equal(4, create.CreationCost);
            Assert.Contains("quality=13; creation cost=4", create.Explain());
            Assert.Contains("ranking score=9", create.Explain());
            Assert.Equal(Actor, Assert.Single(reuse.Candidate.ActorRequirements).ExistingActor);
            var requirement = Assert.Single(create.Candidate.ActorRequirements);
            Assert.True(requirement.RequiresCreation);
            Assert.True(requirement.ExistingActor.IsNone);
            Assert.False(create.Candidate.HasRole("actor"));
            Assert.Contains("requires new actor principal", create.Explain());
            Assert.Contains("reuses actor npc_existing", reuse.Explain());
        }

        [Fact]
        public void QualityCanOutweighCreationAndAdjustedTiesRemainOrdinal()
        {
            var exceptional = Create(15);
            Assert.Same(exceptional, SituationProposalSelection.Rank(new[] { Reuse(), exceptional })[0]);
            var tied = Create(14);
            Assert.Equal(Reuse().RankingScore, tied.RankingScore);
            Assert.Same(tied, SituationProposalSelection.Rank(new[] { Reuse(), tied })[0]);
            var costly = new SituationProposal("costly", new SituationCandidateBuilder("test")
                .RequireNewActor("actor", "a").Pressure("pressure", int.MinValue, "test").Build());
            Assert.Equal((long)int.MinValue - 4, costly.RankingScore);
            Assert.Equal("reuse", SituationProposalSelection.Rank(new[] { costly, Reuse() })[0].Key);
        }

        [Fact]
        public void DistinctCreationKeysAreChargedOnceAndWeirdPremisesCostMost()
        {
            var builder = new SituationCandidateBuilder("test").RequireNewActor("actor", "a")
                .RequireNewActor("witness", "a").RequireNewActor("target", "b")
                .RequireNewWeirdPremise("z").RequireNewWeirdPremise("z").RequireNewWeirdPremise("a");
            var proposal = new SituationProposal("mixed", builder.Build());
            builder.RequireNewActor("extra", "c").RequireNewWeirdPremise("later");
            Assert.Equal(2, proposal.NewActorCount);
            Assert.Equal(20, proposal.CreationCost);
            Assert.Equal(new[] { "a", "z" }, proposal.Candidate.NewWeirdPremises);
            var weird = new SituationProposal("weird", new SituationCandidateBuilder("test")
                .RequireNewWeirdPremise("a").Pressure("motive", 10, "test pressure").Build());
            Assert.True(weird.CreationCost > Create().CreationCost);
            Assert.Equal(new[] { "reuse", "create", "weird" },
                SituationProposalSelection.Rank(new[] { weird, Create(), Reuse() }).Select(p => p.Key));
            Assert.Contains("requires new weird premise a", weird.Explain());
            Assert.Throws<ArgumentException>(() => builder.RequireNewWeirdPremise(" "));
        }

        [Fact]
        public void ExistingBindingsAndWeirdSettingWordsDoNotImplyCreation()
        {
            var proposal = new SituationProposal("existing", new SituationCandidateBuilder("strange_archetype")
                .Bind("actor", EntityId.Parse("npc_unfamiliar"))
                .BindSite("place", EntityId.Parse("zone_existing"))
                .SettingReference("weird relic", "existing setting").Build());
            Assert.Equal(0, proposal.CreationCost);
            Assert.Empty(proposal.Candidate.NewWeirdPremises);
        }

        [Fact]
        public void ConstructRankInspectAndReplayLeaveAllSavedStateAndIdSequenceUntouched()
        {
            var world = new NarrativeWorldState(123);
            world.Registry.Add(new NarrativeNpc(Actor, "Existing"));
            string before = WorldStateSerializer.Save(world);
            var weird = new SituationProposal("weird", new SituationCandidateBuilder("test").RequireNewWeirdPremise("secret").Build());
            string[] report = SituationProposalSelection.Rank(new[] { Reuse(), Create(), weird })
                .Select(p => p.Explain()).ToArray();
            Assert.Equal(before, WorldStateSerializer.Save(world));
            var restored = WorldStateSerializer.Load(before);
            string restoredBefore = WorldStateSerializer.Save(restored);
            Assert.Equal(report, SituationProposalSelection.Rank(new[] { weird, Create(), Reuse() })
                .Select(p => p.Explain()).ToArray());
            Assert.Equal(restoredBefore, WorldStateSerializer.Save(restored));
            Assert.Equal(restored.NewId("npc"), world.NewId("npc"));
        }

        [Fact]
        public void RequirementsAreDetachedFromBuilderAndSortedByRoleAndIdentity()
        {
            var builder = new SituationCandidateBuilder("test")
                .RequireNewActor("witness", "principal").RequireNewActor("actor", "principal")
                .RequireNewActor("actor", "principal").Bind("target", Actor);
            var candidate = builder.Build();
            builder.RequireNewActor("extra", "other").Bind("target", EntityId.Parse("npc_other"));
            Assert.Equal(new[] { "actor", "target", "witness" }, candidate.ActorRequirements.Select(a => a.Role));
            Assert.Equal(2, candidate.ActorRequirements.Count(a => a.RequiresCreation));
            Assert.Single(candidate.ActorsIn("target"));
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<EntityId>)candidate.ActorsIn("target")).Clear());
        }

        [Fact]
        public void InvalidKeysCannotProduceEnumerationDependentTies()
        {
            Assert.Throws<ArgumentException>(() => SituationProposalSelection.Rank(new[] { Reuse(), Reuse() }));
            Assert.Throws<ArgumentException>(() => new SituationProposal("", Reuse().Candidate));
            Assert.Throws<ArgumentException>(() => new SituationCandidateBuilder("test").RequireNewActor("actor", ""));
            Assert.Empty(SituationProposalSelection.Rank(Array.Empty<SituationProposal>()));
        }
    }
}
