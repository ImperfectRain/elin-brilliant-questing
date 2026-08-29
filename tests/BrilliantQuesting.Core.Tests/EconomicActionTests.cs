using System;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class EconomicActionTests
    {
        [Fact]
        public void DebtSituationCanBeResolvedByMoneyAlone()
        {
            DebtLab lab = DebtLab.Create(playerMoney: 1000);
            ActionContext context = lab.Context(lab.Situation.CreditorId);
            context.ThirdParty = lab.Situation.DebtorId;

            ActionOutcome outcome = lab.Actions.Get("pay_debt").Perform(context);

            Assert.Null(outcome.Check);
            Assert.True(outcome.Succeeded);
            Assert.Equal(250, lab.Vanilla.GetMoney(lab.Player));
            Assert.Equal(3750, lab.Vanilla.GetMoney(lab.Situation.CreditorId));
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.DebtPaid && e.Related.Contains(lab.Situation.DebtFactId));
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("debt_paid", lab.Situation.Thread.Resolution);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.DebtFactId).Truth);
        }

        [Fact]
        public void PayingDebtDoesNotAskTheCheckResolver()
        {
            DebtLab lab = DebtLab.Create(playerMoney: 1000);
            ThrowingCheckResolver checks = new ThrowingCheckResolver();

            ActionContext context = new ActionContext(lab.World, lab.Vanilla, checks, lab.World.Rng, lab.Player, lab.Situation.CreditorId)
            {
                Thread = lab.Situation.Thread,
                ThirdParty = lab.Situation.DebtorId
            };

            lab.Actions.Get("pay_debt").Perform(context);

            Assert.False(checks.WasCalled);
        }

        [Fact]
        public void PayDebtIsBlockedByInsufficientRealMoney()
        {
            DebtLab lab = DebtLab.Create(playerMoney: 100);
            ActionContext context = lab.Context(lab.Situation.CreditorId);
            context.ThirdParty = lab.Situation.DebtorId;

            Availability availability = lab.Actions.Get("pay_debt").GetAvailability(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("orens you do not have", availability.Reason);
        }

        [Fact]
        public void PayDebtDisappearsAfterTheDebtIsSettled()
        {
            DebtLab lab = DebtLab.Create(playerMoney: 1000);
            ActionContext context = lab.Context(lab.Situation.CreditorId);
            context.ThirdParty = lab.Situation.DebtorId;

            lab.Actions.Get("pay_debt").Perform(context);

            Availability availability = lab.Actions.Get("pay_debt").GetAvailability(context);
            Assert.False(availability.IsAvailable);
            Assert.Contains("no payable debt", availability.Reason);
        }

        [Fact]
        public void PayDebtRequiresMoneyTransferSupport()
        {
            DebtLab lab = DebtLab.Create(playerMoney: 1000);
            lab.Vanilla.SetCapability(VanillaCapability.SpendMoney, false);
            ActionContext context = lab.Context(lab.Situation.CreditorId);
            context.ThirdParty = lab.Situation.DebtorId;

            Availability availability = lab.Actions.Get("pay_debt").GetAvailability(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("unavailable on this build", availability.Reason);
        }

        private sealed class ThrowingCheckResolver : ICheckResolver
        {
            public bool WasCalled { get; private set; }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                WasCalled = true;
                throw new InvalidOperationException("pay_debt must not resolve a social check");
            }
        }

        private sealed class DebtLab
        {
            private DebtLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public EntityId Zone { get; private set; }

            public DebtSituation Situation { get; private set; }

            public static DebtLab Create(int playerMoney)
            {
                DebtLab lab = new DebtLab();
                NarrativeWorldState world = new NarrativeWorldState(23023);
                EntityId player = world.NewId("npc");
                EntityId zone = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, money: playerMoney, zone: zone);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Zone = zone;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Situation = DebtSituation.Create(world, new SandboxStager(vanilla), zone, vanilla.Now);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public ActionContext Context(EntityId target)
            {
                ActionContext context = new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };

                return context;
            }
        }
    }
}
