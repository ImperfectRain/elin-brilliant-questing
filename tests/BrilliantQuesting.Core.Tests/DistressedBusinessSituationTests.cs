using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
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
    public class DistressedBusinessSituationTests
    {
        [Fact]
        public void AFailingShopCanBeSavedBySettlingItsDebt()
        {
            Lab lab = Lab.Create(2000);
            ActionContext context = lab.Context(lab.Situation.CreditorId);
            context.ThirdParty = lab.Situation.OwnerId;

            ActionOutcome outcome = lab.Actions.Get("pay_debt").Perform(context);

            Assert.True(outcome.Succeeded);
            Assert.Equal(BusinessContinuityState.Recovered, lab.Business.State);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("debt_paid", lab.Situation.Thread.Resolution);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.DebtFactId).Truth);
        }

        [Fact]
        public void AFailingShopCanBeBought()
        {
            Lab lab = Lab.Create(4000);
            ActionOutcome outcome = lab.Actions.Get("buy_business").Perform(lab.Context(lab.Situation.CreditorId));

            Assert.True(outcome.Succeeded);
            Assert.Equal(1600, lab.Vanilla.GetMoney(lab.Player));
            Assert.Equal(BusinessContinuityState.BoughtOut, lab.Business.State);
            Assert.Equal(lab.Player, lab.Business.ReplacementOperatorId);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("business_bought", lab.Situation.Thread.Resolution);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.BusinessStateChanged && e.Tags.Contains("BoughtOut"));
        }

        [Fact]
        public void AFailingShopCanBeExtorted()
        {
            Lab lab = Lab.Create(500);
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.LeverageFactId, KnowledgeSource.Document, 1.0, lab.Vanilla.Now, true);
            ActionContext context = lab.Context(lab.Situation.CreditorId, CheckOutcome.Pass);
            context.SubjectFact = lab.Situation.LeverageFactId;

            ActionOutcome outcome = lab.Actions.Get("extort").Perform(context);

            Assert.True(outcome.Succeeded);
            Assert.Equal(BusinessContinuityState.Extorted, lab.Business.State);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("business_extorted", lab.Situation.Thread.Resolution);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.DebtFactId).Truth);
        }

        [Fact]
        public void AFailingShopCanBeAllowedToFailAndThatStateSurvives()
        {
            Lab lab = Lab.Create(100);
            ThreadEngine engine = new ThreadEngine();
            engine.Register(DistressedBusinessSituation.ArchetypeId, new DistressedBusinessEscalation());

            engine.Advance(lab.World, GameTime.FromDays(8));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            BusinessRecord business = reloaded.Businesses.Of(lab.Situation.BusinessId);
            NarrativeThread thread = reloaded.GetThread(lab.Situation.Thread.Id);
            Assert.Equal(BusinessContinuityState.Failed, business.State);
            Assert.Equal(ThreadState.Resolved, thread.State);
            Assert.Equal("business_failed", thread.Resolution);
        }

        [Fact]
        public void MerchantNetworkReadsTheDebtAsAContract()
        {
            Lab lab = Lab.Create(2000);
            Fact debt = lab.World.Knowledge.GetFact(lab.Situation.DebtFactId);

            Assert.Equal(GuildFraming.Contract, GuildNetworks.Reads(lab.World, GuildId.Merchants, debt));
            Assert.Equal(GuildFraming.None, GuildNetworks.Reads(lab.World, GuildId.Fighters, debt));
        }

        private sealed class Lab
        {
            private Lab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public DistressedBusinessSituation Situation { get; private set; }

            public BusinessRecord Business => World.Businesses.Of(Situation.BusinessId);

            public static Lab Create(int playerMoney)
            {
                Lab lab = new Lab();
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

                return lab;
            }

            public ActionContext Context(EntityId target, CheckOutcome check = CheckOutcome.Pass)
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(check), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }
        }
    }
}
