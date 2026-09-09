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
        public void ReuseAndCreationShareSelectionWithoutConservationCosts()
        {
            SituationProposal reuse = Reuse();
            SituationProposal create = Create();
            var ranked = SituationProposalSelection.Rank(new[] { reuse, create });
            Assert.Same(create, ranked[0]); // Equal quality: ordinal key, not a reuse preference.
            Assert.Same(reuse, SituationProposalSelection.Rank(new[] { Create(9), reuse })[0]);
            Assert.Same(reuse.Candidate, ranked[1].Candidate);
            Assert.Equal(Actor, Assert.Single(reuse.Candidate.ActorRequirements).ExistingActor);
            var requirement = Assert.Single(create.Candidate.ActorRequirements);
            Assert.True(requirement.RequiresCreation);
            Assert.True(requirement.ExistingActor.IsNone);
            Assert.False(create.Candidate.HasRole("actor"));
            Assert.Contains("requires new actor principal", create.Explain());
            Assert.Contains("reuses actor npc_existing", reuse.Explain());
        }

        [Fact]
        public void ConstructRankInspectAndReplayLeaveAllSavedStateAndIdSequenceUntouched()
        {
            var world = new NarrativeWorldState(123);
            world.Registry.Add(new NarrativeNpc(Actor, "Existing"));
            string before = WorldStateSerializer.Save(world);
            string[] report = SituationProposalSelection.Rank(new[] { Reuse(), Create() })
                .Select(p => p.Explain()).ToArray();
            Assert.Equal(before, WorldStateSerializer.Save(world));
            var restored = WorldStateSerializer.Load(before);
            string restoredBefore = WorldStateSerializer.Save(restored);
            Assert.Equal(report, SituationProposalSelection.Rank(new[] { Create(), Reuse() })
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
