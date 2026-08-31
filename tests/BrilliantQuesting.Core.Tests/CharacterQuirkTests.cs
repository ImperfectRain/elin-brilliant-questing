using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class CharacterQuirkTests
    {
        [Fact]
        public void QuirkStaysTheSameAfterSaveReloadAndThirtyInGameDays()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            NarrativeNpc npc = world.Registry.Add(new NarrativeNpc(EntityId.Parse("npc_00000001"), "Mira"));
            AssignNonOrdinaryQuirk(npc);

            CharacterQuirk originalKind = npc.Quirk.Kind;
            CharacterWeirdnessTier originalWeirdness = npc.Quirk.Weirdness;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world, indented: false));
            NarrativeNpc loaded = reloaded.Registry.GetNpc(npc.Id);
            loaded.LastSimulatedAt = loaded.LastSimulatedAt.PlusDays(30);

            bool reassigned = CharacterQuirkAssignment.AssignIfMissing(
                loaded,
                new DeterministicRng(999).Fork("different-future"));

            Assert.False(reassigned);
            Assert.True(loaded.Quirk.Assigned);
            Assert.Equal(originalKind, loaded.Quirk.Kind);
            Assert.Equal(originalWeirdness, loaded.Quirk.Weirdness);
        }

        [Fact]
        public void MostlyOrdinaryIsAnAssignedOutcomeAndDoesNotReroll()
        {
            NarrativeNpc npc = new NarrativeNpc(EntityId.Parse("npc_ordinary"), "Haron");

            bool assigned = CharacterQuirkAssignment.AssignIfMissing(npc, new DeterministicRng(5));
            bool reassigned = CharacterQuirkAssignment.AssignIfMissing(npc, new DeterministicRng(12345));

            Assert.True(assigned);
            Assert.False(reassigned);
            Assert.True(npc.Quirk.Assigned);
        }

        private static void AssignNonOrdinaryQuirk(NarrativeNpc npc)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                NarrativeNpc candidate = new NarrativeNpc(npc.Id, npc.Name);
                CharacterQuirkAssignment.AssignIfMissing(candidate, new DeterministicRng(seed));
                if (candidate.Quirk.HasQuirk)
                {
                    npc.Quirk.Assigned = true;
                    npc.Quirk.Kind = candidate.Quirk.Kind;
                    npc.Quirk.Weirdness = candidate.Quirk.Weirdness;
                    return;
                }
            }

            Assert.Fail("expected to find a non-ordinary deterministic quirk seed");
        }
    }
}
