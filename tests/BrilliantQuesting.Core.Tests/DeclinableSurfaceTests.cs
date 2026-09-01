using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
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
    /// <summary>BQ-121: every player-facing surface can be declined without cost.</summary>
    public class DeclinableSurfaceTests
    {
        [Fact]
        public void DecliningEverySurfaceForAMonthWritesNoPlayerFacingPenalty()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            PlayerFacingSnapshot before = PlayerFacingSnapshot.Capture(lab);

            for (int day = 0; day < 30; day++)
            {
                InspectAndDeclineCurrentSurfaces(lab);
                lab.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            InspectAndDeclineCurrentSurfaces(lab);
            PlayerFacingSnapshot after = PlayerFacingSnapshot.Capture(lab);

            Assert.Equal(before, after);
            Assert.Empty(NarrativeJournal.Entries(lab.World, lab.Player));
            Assert.Empty(NarrativeContentProjection.Entries(lab.World, lab.Player));
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Actor == lab.Player || e.Target == lab.Player);
            Assert.DoesNotContain(lab.World.Memories.MemoriesOf(lab.Player), m => m.AffinityContribution != 0);

            NarrativeWorldState savedAfterDecline = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            Assert.Empty(NarrativeJournal.Entries(savedAfterDecline, lab.Player));
            Assert.Empty(NarrativeContentProjection.Entries(savedAfterDecline, lab.Player));
        }

        [Fact]
        public void UnknownLiveThreadsAreNotPlayerFacingContent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.Empty(NarrativeJournal.Entries(lab.World, lab.Player));
            Assert.Empty(NarrativeContentProjection.Entries(lab.World, lab.Player));

            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, false);

            Assert.Contains(
                NarrativeContentProjection.Entries(lab.World, lab.Player),
                entry => entry.ContentClass == NarrativeContentClass.Situation
                         && entry.ThreadId == lab.Situation.Thread.Id);
        }

        [Fact]
        public void GeneratedSituationCommitMovesVanillaStateSoItIsNotADeclineSurface()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId victim = lab.World.NewId("npc");
            EntityId thief = lab.World.NewId("npc");
            EntityId witness = lab.World.NewId("npc");
            EntityId item = lab.World.NewId("item");

            lab.World.Registry.Add(new NarrativeNpc(victim, "Mara"));
            lab.World.Registry.Add(new NarrativeNpc(thief, "Kip"));
            lab.World.Registry.Add(new NarrativeNpc(witness, "Bram"));
            lab.Vanilla.Define(victim, level: 5, zone: lab.Zone);
            lab.Vanilla.Define(thief, level: 3, zone: lab.Zone);
            lab.Vanilla.Define(witness, level: 4, zone: lab.Zone);
            lab.Vanilla.SetSkill(thief, VanillaSkill.Pickpocket, 12);
            lab.Vanilla.SetSkill(witness, VanillaSkill.SpotHidden, 12);
            lab.Vanilla.GiveItem(victim, new ItemDescriptor(item, "silver ring", "jewelry", 400));

            NarrativeWorldState world = new NarrativeWorldState(91);
            world.Registry.Add(new NarrativeNpc(victim, "Mara"));
            world.Registry.Add(new NarrativeNpc(thief, "Kip"));
            world.Registry.Add(new NarrativeNpc(witness, "Bram"));
            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            SettlementSituationPlan plan = generator.Evaluate(world, lab.Vanilla, lab.Zone);

            Assert.NotEmpty(plan.Candidates);
            Assert.Contains(lab.Vanilla.GetInventory(victim), i => i.Id == item);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(thief), i => i.Id == item);

            PettyTheftSituation committed = generator.TryGenerate(world, lab.Vanilla, plan, lab.Zone, lab.Vanilla.Now);

            Assert.NotNull(committed);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(victim), i => i.Id == item);
            Assert.Contains(lab.Vanilla.GetInventory(thief), i => i.Id == item);
        }

        private static void InspectAndDeclineCurrentSurfaces(TheftLaboratory lab)
        {
            NarrativeJournal.Entries(lab.World, lab.Player);
            NarrativeContentProjection.Entries(lab.World, lab.Player);
            lab.Ambient.Next(lab.World, lab.Vanilla, lab.Vanilla.Now);

            IReadOnlyList<EntityId> present = lab.Vanilla.GetCharactersInZone(lab.Zone);
            for (int i = 0; i < present.Count; i++)
            {
                EntityId target = present[i];
                if (target == lab.Player)
                {
                    continue;
                }

                lab.News.Ask(lab.World, lab.Vanilla, target);
                ContextualActionProjection.Project(lab.Actions.Discover(lab.Context(target)), lab.Context(target), 7);
            }
        }

        private sealed class PlayerFacingSnapshot
        {
            private PlayerFacingSnapshot(string value)
            {
                Value = value;
            }

            private string Value { get; }

            public static PlayerFacingSnapshot Capture(TheftLaboratory lab)
            {
                List<string> parts = new List<string>
                {
                    "karma=" + lab.Vanilla.Karma,
                    "fame=" + lab.Vanilla.Fame,
                    "money=" + lab.Vanilla.GetMoney(lab.Player),
                    "zone=" + lab.Vanilla.GetZoneOf(lab.Player),
                    "ambient=" + lab.World.LastAmbientRemarkMinute,
                    "journal=" + string.Join("|", NarrativeJournal.Entries(lab.World, lab.Player).Select(e => e.FactId.Value)),
                    "content=" + string.Join("|", NarrativeContentProjection.Entries(lab.World, lab.Player).Select(e => e.ContentClass + ":" + e.ThreadId.Value + ":" + e.FactId.Value + ":" + e.EventId.Value)),
                    "beliefs=" + string.Join("|", lab.World.Knowledge.BeliefsOf(lab.Player).Select(b => b.FactId.Value).OrderBy(v => v)),
                    "inventory=" + string.Join("|", lab.Vanilla.GetInventory(lab.Player).Select(i => i.Id.Value).OrderBy(v => v))
                };

                foreach (NarrativeNpc npc in lab.World.Registry.Npcs.Values.OrderBy(n => n.Id.Value))
                {
                    parts.Add("affinity:" + npc.Id.Value + "=" + lab.Vanilla.GetAffinity(npc.Id));
                }

                return new PlayerFacingSnapshot(string.Join("\n", parts));
            }

            public override bool Equals(object obj) =>
                obj is PlayerFacingSnapshot other && Value == other.Value;

            public override int GetHashCode() => Value.GetHashCode();

            public override string ToString() => Value;
        }
    }
}
