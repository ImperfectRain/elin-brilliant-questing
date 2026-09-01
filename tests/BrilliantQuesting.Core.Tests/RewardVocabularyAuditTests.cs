using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Rewards;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>BQ-112: resolutions reward access, relationships, standing, information, property or favors, not loot payouts.</summary>
    public class RewardVocabularyAuditTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Patron = EntityId.Parse("npc_patron");
        private static readonly EntityId Zone = EntityId.Parse("zone_town");

        [Fact]
        public void AResolvedThreadMustNotAttachALootPayout()
        {
            NarrativeWorldState world = World();
            EntityId thread = Thread(world, "synthetic_resolution");
            EntityId gem = EntityId.Parse("item_reward_gem");

            world.Record(
                WorldEventType.ItemGiven,
                Patron,
                Player,
                GameTime.Zero,
                0.5,
                Zone,
                evidence: new[] { gem },
                threadId: thread);
            world.Record(
                WorldEventType.ThreadResolved,
                Player,
                Patron,
                GameTime.Zero,
                0.5,
                Zone,
                threadId: thread);

            ResolutionRewardReport report = new ResolutionRewardAudit(world, Player).AuditResolvedThreads();

            ResolutionRewardFinding finding = Assert.Single(report.ForbiddenItemPayouts);
            Assert.Equal(thread, finding.ThreadId);
            Assert.Equal(gem, finding.ItemId);
        }

        [Fact]
        public void ReturnedRecoveredPropertyIsNotALootPayout()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.TheftFactId,
                KnowledgeSource.Document,
                1.0,
                lab.Vanilla.Now,
                true);
            Assert.True(lab.Vanilla.TryTransferItem(lab.Situation.ItemId, lab.Situation.ThiefId, lab.Player));

            ActionOutcome outcome = lab.Perform("return_item", lab.Situation.VictimId);

            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.ItemReturned);
            ResolutionRewardReport report = new ResolutionRewardAudit(lab.World, lab.Player).AuditResolvedThreads();
            Assert.Empty(report.ForbiddenItemPayouts);
            Assert.Contains(ResolutionRewardKind.Property, report.Kinds);
        }

        [Fact]
        public void DistressedBusinessResolutionsStayInTheRewardVocabulary()
        {
            BusinessLab lab = BusinessLab.Create(playerMoney: 4000);

            ActionOutcome outcome = lab.Actions.Get("buy_business").Perform(lab.Context(lab.Situation.CreditorId));

            Assert.True(outcome.Succeeded);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            ResolutionRewardReport report = new ResolutionRewardAudit(lab.World, lab.Player).AuditResolvedThreads();
            Assert.Empty(report.ForbiddenItemPayouts);
            Assert.Contains(ResolutionRewardKind.Property, report.Kinds);
            Assert.Contains(ResolutionRewardKind.Standing, report.Kinds);
        }

        [Fact]
        public void FestivalCompetitionResolvesAsStandingRatherThanPayout()
        {
            NarrativeWorldState world = World(seed: 107);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Zone);
            vanilla.SetAttribute(Player, VanillaAttribute.Dexterity, 7);
            vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 7);
            vanilla.SetSkill(Player, VanillaSkill.Cooking, 2);
            world.Registry.Add(new NarrativeNpc(Player, "You") { Importance = NarrativeImportance.Major });
            FestivalCompetitionSituation situation = FestivalCompetitionSituation.Create(
                world,
                new SandboxStager(vanilla),
                Player,
                Zone,
                vanilla.Now);
            FixedCheckResolver checks = new FixedCheckResolver(CheckOutcome.Fail);
            checks.Then(CheckOutcome.Fail).Then(CheckOutcome.CriticalPass).Then(CheckOutcome.Pass);

            situation.Resolve(world, vanilla, checks, new DeterministicRng(71), Player, vanilla.Now);

            Assert.Equal(ThreadState.Resolved, situation.Thread.State);
            ResolutionRewardReport report = new ResolutionRewardAudit(world, Player).AuditResolvedThreads();
            Assert.Empty(report.ForbiddenItemPayouts);
            Assert.Contains(ResolutionRewardKind.Standing, report.Kinds);
        }

        private static NarrativeWorldState World(ulong seed = 112)
        {
            NarrativeWorldState world = new NarrativeWorldState(seed);
            world.Registry.Add(new NarrativeNpc(Player, "You") { Importance = NarrativeImportance.Major });
            world.Registry.Add(new NarrativeNpc(Patron, "Patron"));
            return world;
        }

        private static EntityId Thread(NarrativeWorldState world, string archetype)
        {
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), archetype, GameTime.Zero)
            {
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(Player);
            thread.ParticipantIds.Add(Patron);
            thread.SiteIds.Add(Zone);
            world.Threads.Add(thread);
            return thread.Id;
        }

        private sealed class BusinessLab
        {
            private BusinessLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public DistressedBusinessSituation Situation { get; private set; }

            public static BusinessLab Create(int playerMoney)
            {
                BusinessLab lab = new BusinessLab();
                NarrativeWorldState world = new NarrativeWorldState(45045);
                EntityId player = world.NewId("npc");
                EntityId market = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: playerMoney, zone: market);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Situation = DistressedBusinessSituation.Create(world, new SandboxStager(vanilla), market, vanilla.Now);
                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public ActionContext Context(EntityId target)
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }
        }
    }
}
