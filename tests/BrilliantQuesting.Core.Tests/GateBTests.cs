using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// The design document's Gate B: run the three-NPC scenario, let ten or more in-game days
    /// pass through different outcomes, and check that the resulting world is explainable,
    /// persistent and replayable.
    /// </summary>
    public class GateBTests
    {
        [Fact]
        public void IgnoringTheSituationEntirelyStillChangesTheWorld()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId thief = lab.Situation.ThiefId;
            EntityId victim = lab.Situation.VictimId;

            Assert.Single(lab.Vanilla.GetInventory(thief));
            Assert.False(lab.World.Knowledge.Knows(victim, lab.Situation.TheftFactId));

            lab.AdvanceDays(15);

            // Day 4: he stops carrying it. The easy route is gone because time passed.
            Assert.Empty(lab.Vanilla.GetInventory(thief));

            // Day 7: the witness lets something slip, so the victim now suspects - without proof.
            Assert.True(lab.World.Knowledge.Knows(victim, lab.Situation.TheftFactId));
            Assert.False(lab.World.Knowledge.CanProve(victim, lab.Situation.TheftFactId));

            // Day 10: an accusation they cannot support. Day 14: the households fall out.
            Assert.Contains(lab.World.Ledger.OfType(WorldEventType.FalseAccusation), e => e.Actor == victim && e.Target == thief);
            RelationshipEdge edge = lab.World.Relationships.Find(victim, thief);
            Assert.NotNull(edge);
            Assert.Equal(RelationKind.Enemy, edge.Kind);
        }

        [Fact]
        public void EscalationFiresInOrderAndOnlyOnce()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            lab.AdvanceDays(3);
            Assert.Equal(new List<string> { "victim_asks_around" }, lab.Situation.Thread.CompletedSteps);

            lab.AdvanceDays(5);
            Assert.Equal(new List<string> { "victim_asks_around", "thief_hides_it", "witness_talks" }, lab.Situation.Thread.CompletedSteps);

            lab.AdvanceDays(20);
            Assert.Equal(5, lab.Situation.Thread.CompletedSteps.Count);

            // Nothing left to fire: the thread goes quiet rather than nagging forever.
            lab.AdvanceDays(30);
            Assert.Equal(5, lab.Situation.Thread.CompletedSteps.Count);
            Assert.Equal(ThreadState.Dormant, lab.Situation.Thread.State);
        }

        [Fact]
        public void SolvingItEarlyStopsTheEscalationAndLeavesADifferentWorld()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            EntityId victim = lab.Situation.VictimId;

            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", victim);

            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("property_returned", lab.Situation.Thread.Resolution);

            lab.AdvanceDays(20);

            // A resolved thread does not keep escalating, and the feud never happens.
            Assert.Empty(lab.Situation.Thread.CompletedSteps);
            Assert.Null(lab.World.Relationships.Find(victim, lab.Situation.ThiefId));
            Assert.True(lab.Vanilla.GetAffinity(victim) > 0);
        }

        [Fact]
        public void KeepingItLeavesAQuietBrokenPromiseRatherThanAnAlarm()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            EntityId victim = lab.Situation.VictimId;

            lab.Perform("persuade", victim);      // the victim asks you to look into it
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("keep_item", victim, ctx => ctx.SubjectItem = lab.Situation.ItemId);

            Assert.Equal("property_kept", lab.Situation.Thread.Resolution);
            Assert.Single(lab.Vanilla.GetInventory(lab.Player));
            Assert.Contains(lab.World.Ledger.OfType(WorldEventType.PromiseBroken), e => e.Target == victim);
        }

        [Fact]
        public void TheSameSeedReplaysTheSameStory()
        {
            List<string> first = RunScriptedPlaythrough(4242);
            List<string> second = RunScriptedPlaythrough(4242);
            List<string> different = RunScriptedPlaythrough(99);

            Assert.Equal(first, second);
            Assert.NotEqual(first, different);
        }

        [Fact]
        public void EveryConsequenceCanBeTracedBackToAnEvent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);

            lab.Perform("pickpocket", lab.Situation.ThiefId);

            // Anything an NPC remembers must correspond to something that actually happened.
            foreach (KeyValuePair<EntityId, List<Memory.MemoryRecord>> pair in lab.World.Memories.All)
            {
                foreach (Memory.MemoryRecord memory in pair.Value)
                {
                    Assert.Contains(lab.World.Ledger.Events, e => e.Type == memory.EventType && e.Time == memory.When);
                }
            }
        }

        /// <summary>Runs a fixed sequence of verbs with the real dice and records what happened.</summary>
        private static List<string> RunScriptedPlaythrough(ulong seed)
        {
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            List<string> log = new List<string>();

            log.Add(lab.Perform("question", lab.Situation.WitnessId).Outcome.ToString());
            log.Add(lab.Perform("intimidate", lab.Situation.WitnessId).Outcome.ToString());
            log.Add(lab.Perform("pickpocket", lab.Situation.ThiefId).Outcome.ToString());
            lab.AdvanceDays(12);

            foreach (WorldEvent worldEvent in lab.World.Ledger.Events)
            {
                log.Add(worldEvent.Type + "|" + worldEvent.Actor + "|" + worldEvent.Target);
            }

            return log;
        }
    }
}
