using System.Linq;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ThreadLifecycleTests
    {
        [Fact]
        public void AThreadWithNoLivingParticipantIsQuarantinedRatherThanAdvanced()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            KillParticipants(lab);

            int changed = ThreadLifecycle.Review(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(1, changed);
            Assert.Equal(ThreadState.Quarantined, lab.Situation.Thread.State);
            Assert.Contains("no living participant", lab.Situation.Thread.LifecycleReason);
            Assert.Single(lab.World.Ledger.OfType(WorldEventType.ThreadQuarantined), e => e.ThreadId == lab.Situation.Thread.Id);
            Assert.False(lab.Situation.Thread.IsLive);

            int advanced = lab.Threads.Advance(lab.World, lab.Vanilla.Now.PlusDays(20));
            Assert.Equal(0, advanced);
        }

        [Fact]
        public void AThreadWithAnHeirCreatesASuccessorAndPreservesTheOriginal()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId heir = AddHeir(lab, lab.Situation.VictimId);
            KillParticipants(lab);

            int changed = ThreadLifecycle.Review(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(1, changed);
            Assert.Equal(ThreadState.Inherited, lab.Situation.Thread.State);
            Assert.False(lab.Situation.Thread.SuccessorThreadId.IsNone);

            NarrativeThread successor = lab.World.GetThread(lab.Situation.Thread.SuccessorThreadId);
            Assert.NotNull(successor);
            Assert.Equal(ThreadState.Active, successor.State);
            Assert.Equal(lab.Situation.Thread.Id, successor.ParentThreadId);
            Assert.Contains(heir, successor.ParticipantIds);
            Assert.DoesNotContain(lab.Situation.VictimId, successor.ParticipantIds);
            Assert.Equal(lab.Situation.Thread.FactIds, successor.FactIds);
            Assert.Single(lab.World.Ledger.OfType(WorldEventType.ThreadInherited), e => e.ThreadId == lab.Situation.Thread.Id);
        }

        [Fact]
        public void LifecycleStateSurvivesSaveLoadWithoutRedispatching()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId heir = AddHeir(lab, lab.Situation.VictimId);
            KillParticipants(lab);
            ThreadLifecycle.Review(lab.World, lab.Vanilla, lab.Vanilla.Now);
            int eventsBefore = lab.World.Ledger.Count;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            NarrativeThread original = reloaded.GetThread(lab.Situation.Thread.Id);
            Assert.Equal(ThreadState.Inherited, original.State);
            Assert.Equal(lab.Situation.Thread.SuccessorThreadId, original.SuccessorThreadId);
            Assert.Contains("inherited by", original.LifecycleReason);

            NarrativeThread successor = reloaded.GetThread(original.SuccessorThreadId);
            Assert.Equal(original.Id, successor.ParentThreadId);
            Assert.Contains(heir, successor.ParticipantIds);
            Assert.Equal(eventsBefore, reloaded.Ledger.Count);
        }

        [Fact]
        public void DormantThreadsCanBeReactivatedWithAnInspectableReason()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Situation.Thread.State = ThreadState.Dormant;

            bool changed = ThreadLifecycle.Reactivate(lab.World, lab.Situation.Thread, lab.Vanilla.Now, "new evidence surfaced");

            Assert.True(changed);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Equal("new evidence surfaced", lab.Situation.Thread.LifecycleReason);
            Assert.Single(lab.World.Ledger.OfType(WorldEventType.ThreadReactivated), e => e.ThreadId == lab.Situation.Thread.Id);
        }

        [Fact]
        public void MergingThreadsMovesCausalStateAndRetiresTheSource()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            NarrativeThread target = lab.Situation.Thread;
            NarrativeThread source = new NarrativeThread(lab.World.NewId("thread"), target.ArchetypeId, lab.Vanilla.Now)
            {
                State = ThreadState.Active,
                Tension = target.Tension + 20,
                Importance = target.Importance + 1
            };
            EntityId participant = lab.World.NewId("npc");
            source.ParticipantIds.Add(participant);
            source.FactIds.Add(target.FactIds[0]);
            source.OpenQuestions.Add("Who else heard?");
            lab.World.Threads.Add(source);

            bool changed = ThreadLifecycle.Merge(lab.World, target, source, lab.Vanilla.Now, "same theft reached the same cast");

            Assert.True(changed);
            Assert.Equal(ThreadState.Inherited, source.State);
            Assert.Equal(target.Id, source.SuccessorThreadId);
            Assert.Contains(participant, target.ParticipantIds);
            Assert.Contains("Who else heard?", target.OpenQuestions);
            Assert.Equal(source.Tension, target.Tension);
            Assert.Equal(source.Importance, target.Importance);
            Assert.Single(lab.World.Ledger.OfType(WorldEventType.ThreadMerged), e => e.ThreadId == source.Id);
        }

        [Fact]
        public void MalformedThreadsAreQuarantinedWithAReason()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Situation.Thread.FactIds.Add(lab.World.NewId("fact"));

            int changed = ThreadLifecycle.Review(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(1, changed);
            Assert.Equal(ThreadState.Quarantined, lab.Situation.Thread.State);
            Assert.Contains("missing fact", lab.Situation.Thread.LifecycleReason);
        }

        private static EntityId AddHeir(TheftLaboratory lab, EntityId subject)
        {
            EntityId heir = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(heir, "Mira") { Importance = NarrativeImportance.Known });
            lab.Vanilla.Define(heir, zone: lab.Zone);
            lab.World.Relationships.Connect(heir, subject, RelationKind.Family, 80);
            return heir;
        }

        private static void KillParticipants(TheftLaboratory lab)
        {
            foreach (EntityId participant in lab.Situation.Thread.ParticipantIds.ToArray())
            {
                lab.Vanilla.Kill(participant);
                NarrativeNpc npc = lab.World.Registry.GetNpc(participant);
                if (npc != null)
                {
                    npc.Alive = false;
                }
            }
        }
    }
}
