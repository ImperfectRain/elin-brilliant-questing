using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class NarrativeContentProjectionTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Village = EntityId.Parse("zone_village");

        [Fact]
        public void OneCausalChainProjectsSituationRequestOpportunityAndEvent()
        {
            Lab lab = Lab.Create();
            lab.TeachPlayer(lab.Situation.BreadDemandId, lab.Situation.RemedyDemandId, lab.Situation.WheelDamageId);
            lab.World.Record(
                WorldEventType.ThreadEscalated,
                lab.Situation.ReeveId,
                EntityId.None,
                lab.Vanilla.Now.PlusDays(1),
                related: new[] { lab.Situation.BreadDemandId },
                threadId: lab.Situation.Thread.Id);

            IReadOnlyList<NarrativeContentEntry> entries = NarrativeContentProjection.Entries(lab.World, Player);
            Dictionary<NarrativeContentClass, int> counts = entries
                .GroupBy(e => e.ContentClass)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.Equal(1, counts[NarrativeContentClass.Situation]);
            Assert.Equal(2, counts[NarrativeContentClass.Request]);
            Assert.Equal(1, counts[NarrativeContentClass.Opportunity]);
            Assert.Equal(1, counts[NarrativeContentClass.Event]);
            Assert.All(entries, entry => Assert.Equal(lab.Situation.Thread.Id, entry.ThreadId));
        }

        [Fact]
        public void BoardProjectionContainsOnlyRequests()
        {
            Lab lab = Lab.Create();
            lab.TeachPlayer(lab.Situation.BreadDemandId, lab.Situation.RemedyDemandId, lab.Situation.WheelDamageId);
            lab.World.Record(
                WorldEventType.ThreadEscalated,
                lab.Situation.ReeveId,
                EntityId.None,
                lab.Vanilla.Now.PlusDays(1),
                related: new[] { lab.Situation.BreadDemandId },
                threadId: lab.Situation.Thread.Id);

            IReadOnlyList<NarrativeContentEntry> board = NarrativeContentProjection.BoardEntries(lab.World, Player);

            Assert.Equal(2, board.Count);
            Assert.All(board, entry => Assert.Equal(NarrativeContentClass.Request, entry.ContentClass));
            Assert.Contains(board, entry => entry.FactId == lab.Situation.BreadDemandId);
            Assert.Contains(board, entry => entry.FactId == lab.Situation.RemedyDemandId);
            Assert.DoesNotContain(board, entry => entry.ContentClass == NarrativeContentClass.Situation);
            Assert.DoesNotContain(board, entry => entry.ContentClass == NarrativeContentClass.Opportunity);
            Assert.DoesNotContain(board, entry => entry.ContentClass == NarrativeContentClass.Event);
        }

        [Fact]
        public void UnknownFactsAndEventsDoNotLeakIntoPlayerContent()
        {
            Lab lab = Lab.Create();
            lab.World.Record(
                WorldEventType.ThreadEscalated,
                lab.Situation.ReeveId,
                EntityId.None,
                lab.Vanilla.Now.PlusDays(1),
                related: new[] { lab.Situation.BreadDemandId },
                threadId: lab.Situation.Thread.Id);

            IReadOnlyList<NarrativeContentEntry> entries = NarrativeContentProjection.Entries(lab.World, Player);

            Assert.Empty(entries);
            Assert.Empty(NarrativeContentProjection.BoardEntries(lab.World, Player));
        }

        [Fact]
        public void ContentProjectionSurvivesReloadWithoutPersistedQuestEntries()
        {
            Lab lab = Lab.Create();
            lab.TeachPlayer(lab.Situation.BreadDemandId, lab.Situation.RemedyDemandId, lab.Situation.WheelDamageId);
            lab.World.Record(
                WorldEventType.ThreadEscalated,
                lab.Situation.ReeveId,
                EntityId.None,
                lab.Vanilla.Now.PlusDays(1),
                related: new[] { lab.Situation.BreadDemandId },
                threadId: lab.Situation.Thread.Id);

            string before = Signature(NarrativeContentProjection.Entries(lab.World, Player));
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(before, Signature(NarrativeContentProjection.Entries(reloaded, Player)));
            Assert.DoesNotContain("contentClass", WorldStateSerializer.Save(lab.World, indented: false));
        }

        private static string Signature(IReadOnlyList<NarrativeContentEntry> entries)
        {
            return string.Join(
                "|",
                entries.Select(e => e.ContentClass + ":" + e.ThreadId + ":" + e.FactId + ":" + e.EventId));
        }

        private sealed class Lab
        {
            public readonly NarrativeWorldState World = new NarrativeWorldState(99);
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            public ShortageSituation Situation;

            private Lab()
            {
            }

            public static Lab Create()
            {
                Lab lab = new Lab();
                lab.Vanilla.Define(Player, zone: Village);
                lab.Situation = ShortageSituation.Create(
                    lab.World,
                    new SandboxStager(lab.Vanilla),
                    Player,
                    Village,
                    lab.Vanilla.Now);
                return lab;
            }

            public void TeachPlayer(params EntityId[] factIds)
            {
                for (int i = 0; i < factIds.Length; i++)
                {
                    World.Knowledge.Teach(Player, factIds[i], KnowledgeSource.Hearsay, 0.85, Vanilla.Now.PlusMinutes(i), false, Situation.ReeveId);
                }
            }
        }
    }
}
