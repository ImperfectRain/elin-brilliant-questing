using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Presentation;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class JournalPagesTests
    {
        [Fact]
        public void OwnedPresentationSourcesContainNoLegacyQuestFieldsOrTestContent()
        {
            foreach (string resource in new[] { "JournalRendererSource", "JournalPagesSource" })
            {
                using var stream = typeof(JournalPagesTests).Assembly.GetManifestResourceStream(resource);
                using var reader = new System.IO.StreamReader(stream);
                string source = reader.ReadToEnd();
                foreach (string forbidden in new[] { "credit", "sfsfesfesf", "textReward", "textClient",
                    "textHours", "textZone", "buttonAbandon", "Quests in progress", "Remaining Time" })
                    Assert.DoesNotContain(forbidden, source);
            }
        }

        [Fact]
        public void RegistryPreservesOrderRejectsDuplicateIdsAndResolvesUnknownSelection()
        {
            var registry = JournalPageRegistry.CreateDefault();
            Assert.Equal(new[] { "overview", "chronicle" }, registry.Pages.Select(p => p.Id));
            Assert.Same(registry.Pages[0], registry.Resolve("removed-page"));
            Assert.Throws<ArgumentException>(() => new JournalPageRegistry(new[] { registry.Pages[0], registry.Pages[0] }));
            Assert.Throws<ArgumentException>(() => new JournalPageRegistry(Array.Empty<JournalPageDescriptor>()));
            var extended = new JournalPageRegistry(registry.Pages.Concat(new[]
            {
                new JournalPageDescriptor("extra", "More", (w, v) => Array.Empty<JournalSection>())
            }));
            Assert.Empty(extended.Resolve("extra").Project(null, null));
        }

        [Fact]
        public void ModelsOwnImmutableCopiesOfTheirItems()
        {
            var items = new List<JournalItem> { new JournalItem("A claim") };
            var section = new JournalSection("Claims", items, "Nothing known.");
            items.Clear();
            Assert.Single(section.Items);
            Assert.Throws<NotSupportedException>(() => ((IList<JournalItem>)section.Items).Clear());
        }

        [Fact]
        public void AvailabilityIsReevaluatedWithoutProjectingUnavailablePages()
        {
            bool available = false;
            var page = new JournalPageDescriptor("later", "Later", (w, v) => throw new Exception("Must not project"),
                (w, v) => available);
            var registry = new JournalPageRegistry(new[] { page });
            Assert.Empty(registry.Available(null, null));
            available = true;
            Assert.Same(page, Assert.Single(registry.Available(null, null)));
        }

        [Fact]
        public void UnloadedAndNewWorldHaveTruthfulEmptyStates()
        {
            foreach (var page in JournalPageRegistry.CreateDefault().Pages)
                Assert.Contains("No Brilliant Questing state is loaded.", Packet(page.Project(null, null)));
            var world = new NarrativeWorldState(1);
            var vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
            string packet = Packet(JournalPages.Overview(world, vanilla));
            Assert.Contains("No active matters known to you.", packet);
            Assert.Contains("Nothing known yet.", packet);
            Assert.Contains("Nothing to tell yet.", Packet(JournalPages.ChroniclePage(world, vanilla)));
            AssertClean(packet);
        }

        [Fact]
        public void SwitchingAndRepeatedProjectionDoNotWriteWorldStateOrCacheOldKnowledge()
        {
            var lab = TheftLaboratory.Create();
            var pages = JournalPageRegistry.CreateDefault();
            string before = WorldStateSerializer.Save(lab.World);
            string first = Packet(pages.Resolve("overview").Project(lab.World, lab.Vanilla));
            for (int i = 0; i < 4; i++)
                foreach (var page in pages.Pages) page.Project(lab.World, lab.Vanilla);
            Assert.Equal(before, WorldStateSerializer.Save(lab.World));
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay,
                0.7, lab.Vanilla.Now, false, lab.Situation.WitnessId);
            string changed = Packet(pages.Resolve("overview").Project(lab.World, lab.Vanilla));
            Assert.NotEqual(first, changed);
            Assert.Contains("[Reported]", changed);
            AssertClean(changed);
        }

        [Fact]
        public void ResolvedTheftPacketPreservesDisputedKnowledgeAndNamedHistory()
        {
            var lab = TheftLaboratory.Create();
            var theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);
            // Give this headless history the names/item observed in the live save.
            lab.World.Registry.GetNpc(lab.Situation.VictimId).Name = "Garron";
            lab.World.Registry.GetNpc(lab.Situation.ThiefId).Name = "Vess";
            theft = new Fact(theft.Id, theft.Subject, theft.Predicate, theft.Object, "silver ring", theft.Truth);
            lab.World.Knowledge.AddFact(theft);
            var falseClaim = new Fact(lab.World.NewId("fact"), lab.Situation.VictimId,
                theft.Predicate, theft.Object, theft.Value, TruthState.False) { DistortionOf = theft.Id };
            lab.World.Knowledge.AddFact(falseClaim);
            lab.World.Knowledge.Teach(lab.Player, theft.Id, KnowledgeSource.Witnessed, 0.9, lab.Vanilla.Now, true);
            lab.World.Knowledge.Teach(lab.Player, falseClaim.Id, KnowledgeSource.Hearsay, 0.7, lab.Vanilla.Now, false);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            string packet = Packet(JournalPages.ChroniclePage(lab.World, lab.Vanilla));
            Assert.Contains("Garron", packet);
            Assert.Contains("petty theft", packet);
            Assert.Contains("property returned", packet);
            Assert.Contains("[Disputed] Vess stole silver ring", packet);
            Assert.Contains("what you did:", packet);
            AssertClean(packet);
            foreach (var id in lab.World.Knowledge.Facts.Keys) Assert.DoesNotContain(id.Value, packet);
            Assert.DoesNotContain(lab.Situation.Thread.Id.Value, packet);
            string saved = WorldStateSerializer.Save(lab.World);
            Assert.Equal(packet, Packet(JournalPages.ChroniclePage(WorldStateSerializer.Load(saved), lab.Vanilla)));
        }

        [Fact]
        public void InternalToneProvenanceAndScoresCannotChangePlayerPresentation()
        {
            var lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed,
                0.9, lab.Vanilla.Now, true);
            string before = Packet(JournalPages.Overview(lab.World, lab.Vanilla));
            lab.Situation.Thread.GenerationCauses.Add("rare_sincerity director score=100 casting trace");
            lab.Situation.Thread.Tension += 20;
            lab.World.Record(WorldEventType.Conversed, lab.Player, lab.Situation.VictimId, lab.Vanilla.Now,
                tags: new[] { "rare_sincerity", "director_score=999" });
            Assert.Equal(before, Packet(JournalPages.Overview(lab.World, lab.Vanilla)));
            AssertClean(before);
        }

        [Fact]
        public void MissingNamesAndManyClaimsRemainReadableWithoutRawIds()
        {
            var world = new NarrativeWorldState(1);
            var player = EntityId.Parse("npc_player");
            var vanilla = new SandboxVanillaState(player);
            for (int i = 0; i < 150; i++)
            {
                var fact = new Fact(world.NewId("fact"), EntityId.Parse("npc_missing"), FactPredicates.Stole,
                    EntityId.Parse("item_missing_" + i));
                world.Knowledge.AddFact(fact);
                world.Knowledge.Teach(player, fact.Id, KnowledgeSource.Hearsay, 0.6, GameTime.Zero, false);
            }
            var sections = JournalPages.Overview(world, vanilla);
            Assert.Equal(150, sections.Single(s => s.Heading == "Known claims").Items.Count);
            string packet = Packet(sections);
            Assert.Contains("unnamed", packet);
            Assert.DoesNotContain("npc_missing", packet);
            Assert.DoesNotContain("item_missing", packet);
        }

        private static string Packet(IReadOnlyList<JournalSection> sections) => string.Join("\n",
            sections.Select(s => s.Heading + "\n" + (s.Items.Count == 0 ? s.EmptyText : string.Join("\n", s.Items.Select(i => i.Text)))));

        private static void AssertClean(string packet)
        {
            foreach (string forbidden in new[] { "Reward", "Remaining Time", "Client", "Abandon", "credit", "sfsfesfesf",
                "rare_sincerity", "director", "confidence", "tension", "ItemReturned", "SecretLearned", "casting" })
                Assert.DoesNotContain(forbidden, packet);
        }
    }
}
