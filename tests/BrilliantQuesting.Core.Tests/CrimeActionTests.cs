using System.Collections.Generic;
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
using BrilliantQuesting.World;
using BrilliantQuesting.Persistence;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class CrimeActionTests
    {
        /// <summary>
        /// The step's done-when: a route that ends with an authority acting, made of nothing but
        /// crimes, and turning on a step that is closed to anybody the trade has never heard of.
        ///
        /// Note what the honest half of the route achieves on its own - a true belief nobody will
        /// act on. Everything after that is the criminal half.
        /// </summary>
        [Fact]
        public void ACriminalRouteClosesACaseThatTheHonestEvidenceCannotClose()
        {
            RacketLab lab = RacketLab.Create(criminal: true);

            // Ilsabet will tell you who is bleeding her. She cannot show you, and neither can you.
            lab.Run("question", lab.Market, lab.Situation.VictimId, lab.Situation.RacketFactId);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.RacketFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.RacketFactId));

            ActionOutcome refused = lab.Run("report", lab.Market, lab.Situation.GuardId, lab.Situation.RacketFactId);
            Assert.Contains(refused.Notes, note => note.Contains("rejected for want of proof"));

            // The counting house is behind a lock. Letting yourself in is what opens the shelf.
            ActionOutcome breakIn = lab.Run("trespass", lab.CountingHouse, EntityId.None);
            Assert.True(breakIn.Succeeded);
            Assert.True(lab.Site(lab.CountingHouse).Admits(lab.Player));

            lab.Run("search", lab.CountingHouse, EntityId.None, lab.Situation.EmploymentFactId);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);

            // The book proves he employs a collector. That is not a crime, and it is all the
            // honest evidence in this world amounts to.
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.RacketFactId));

            // Orin makes the book say the rest of it.
            ActionOutcome forged = lab.Run("forge", lab.BackRoom, lab.Situation.ForgerId, lab.Situation.RacketFactId);
            Assert.True(forged.Succeeded);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.RacketFactId));

            ActionOutcome report = lab.Run("report", lab.Market, lab.Situation.GuardId, lab.Situation.RacketFactId);
            Assert.Contains(report.Notes, note => note.Contains("accepted it on PhysicalProof"));
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.CrimeReported
                                                          && e.Related.Contains(lab.Situation.RacketFactId));
            Assert.True(lab.World.Knowledge.CanProve(lab.Situation.GuardId, lab.Situation.RacketFactId));

            // And the sting: the claim the guard acted on is true, the proof he acted on is not,
            // and the graph holds both of those separately rather than blurring them.
            Assert.Equal(TruthState.True, lab.World.Knowledge.GetFact(lab.Situation.RacketFactId).Truth);
            Fact forgery = lab.Forgery();
            Assert.Equal(TruthState.True, forgery.Truth);
            Assert.Contains(lab.Situation.LetterBookId, forgery.EvidenceIds);
        }

        /// <summary>
        /// The other half of the done-when. The lawful build takes every step the criminal one
        /// took, right up to the one that needs somebody who will do that kind of work - and the
        /// wall it hits is standing, not a bad roll.
        /// </summary>
        [Fact]
        public void ALawfulBuildWalksTheSameRouteUntilItNeedsSomebodyWhoWillForge()
        {
            RacketLab lab = RacketLab.Create(criminal: false);

            lab.Run("question", lab.Market, lab.Situation.VictimId, lab.Situation.RacketFactId);

            // Breaking in is not gated on anything. A lawful character is perfectly able to do it.
            Assert.True(lab.Can("trespass", lab.CountingHouse, EntityId.None).IsAvailable);
            lab.Run("trespass", lab.CountingHouse, EntityId.None);
            lab.Run("search", lab.CountingHouse, EntityId.None, lab.Situation.EmploymentFactId);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);

            Availability forging = lab.Can("forge", lab.BackRoom, lab.Situation.ForgerId, lab.Situation.RacketFactId);
            Assert.False(forging.IsAvailable);
            Assert.Contains("does not do that kind of work", forging.Reason);

            // So the case stops exactly where the honest evidence stops.
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.RacketFactId));
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Type == WorldEventType.CrimeReported);
        }

        /// <summary>
        /// A guild card is one credential of three. A record the law already keeps, or having been
        /// dealt with long enough to be vouched for, open the same door.
        /// </summary>
        [Fact]
        public void ThereIsMoreThanOneWayToBeKnownToTheTrade()
        {
            RacketLab byKarma = RacketLab.Create(criminal: false);
            byKarma.Vanilla.ChangeKarma(UnderworldPolicy.KnownToTheTrade);
            Assert.True(byKarma.Reaches(byKarma.Situation.ForgerId));

            RacketLab byName = RacketLab.Create(criminal: false);
            byName.Vanilla.SetAffinity(byName.Situation.ForgerId, UnderworldPolicy.VouchedForAt);
            Assert.True(byName.Reaches(byName.Situation.ForgerId));

            RacketLab stranger = RacketLab.Create(criminal: false);
            Assert.False(stranger.Reaches(stranger.Situation.ForgerId));
        }

        /// <summary>
        /// The rule the roadmap states outright: standing gates who will deal with you, never
        /// whether you may try something with your own hands.
        /// </summary>
        [Fact]
        public void StandingGatesContactsAndNeverTheAttemptItself()
        {
            RacketLab lab = RacketLab.Create(criminal: false);

            Assert.True(lab.Can("trespass", lab.CountingHouse, EntityId.None).IsAvailable);
            Assert.True(lab.Can("sabotage", lab.Market, lab.Situation.CollectorId).IsAvailable);

            lab.Run("question", lab.Market, lab.Situation.VictimId, lab.Situation.RacketFactId);
            Assert.True(lab.Can("extort", lab.CountingHouse, lab.Situation.RacketeerId, lab.Situation.RacketFactId).IsAvailable);

            // Every one of those went to somebody with clean hands and no contacts at all.
            Assert.Equal(0, lab.Vanilla.GetGuildRank(GuildId.Thieves));
            Assert.True(lab.Vanilla.Karma > UnderworldPolicy.KnownToTheTrade);
        }

        [Fact]
        public void WhatAPlaceKeepsIsOutOfReachUntilYouAreInside()
        {
            RacketLab lab = RacketLab.Create(criminal: true);

            ActionOutcome outside = lab.Run("search", lab.CountingHouse, EntityId.None, lab.Situation.EmploymentFactId);

            Assert.True(outside.Succeeded);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.EmploymentFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));

            lab.Run("trespass", lab.CountingHouse, EntityId.None);
            lab.Run("search", lab.CountingHouse, EntityId.None, lab.Situation.EmploymentFactId);

            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);
        }

        /// <summary>An open room is unchanged by any of this; only a locked one asks the question.</summary>
        [Fact]
        public void AnOrdinaryRoomNeedsNoBreakingIntoAtAll()
        {
            RacketLab lab = RacketLab.Create(criminal: true);

            Availability nothingShut = lab.Can("trespass", lab.Market, EntityId.None);

            Assert.False(nothingShut.IsAvailable);
            Assert.Contains("nothing shut to you", nothingShut.Reason);
        }

        /// <summary>Standing rule 13: a critical failure creates a problem rather than refusing.</summary>
        [Fact]
        public void ABotchedBreakInStillGetsYouInsideAndPutsAWitnessOnYou()
        {
            RacketLab lab = RacketLab.Create(criminal: true, outcome: CheckOutcome.CriticalFail);
            ActionContext context = lab.Context(lab.CountingHouse, EntityId.None);
            context.Witnesses.Add(lab.Situation.RacketeerId);

            ActionOutcome outcome = lab.Actions.Get("trespass").Perform(context);

            Assert.Equal(CheckOutcome.CriticalFail, outcome.Outcome);
            Assert.True(lab.Site(lab.CountingHouse).Admits(lab.Player));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Trespass && e.Witnesses.Count == 1);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.CrimeWitnessed);
        }

        /// <summary>A lock that has been picked stays picked across a save.</summary>
        [Fact]
        public void GettingInSurvivesASaveAndReload()
        {
            RacketLab lab = RacketLab.Create(criminal: true);
            lab.Run("trespass", lab.CountingHouse, EntityId.None);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            NarrativeSite site = reloaded.Registry.GetSite(lab.CountingHouse);

            Assert.True(site.Restricted);
            Assert.True(site.Admits(lab.Player));
        }

        /// <summary>
        /// A forgery is proof of a claim and a fact about itself, and the quality of the work
        /// decides which verb could later take it apart.
        /// </summary>
        [Fact]
        public void AForgeryProvesTheClaimAndRecordsThatItWasMade()
        {
            RacketLab rushed = RacketLab.Reached(criminal: true);
            rushed.Run("forge", rushed.BackRoom, rushed.Situation.ForgerId, rushed.Situation.RacketFactId);
            Assert.True(rushed.Forgery().Secrecy < ReadDocumentAction.ObscuredAt);

            RacketLab clean = RacketLab.Reached(criminal: true, outcome: CheckOutcome.CriticalPass);
            clean.Run("forge", clean.BackRoom, clean.Situation.ForgerId, clean.Situation.RacketFactId);
            Assert.True(clean.Forgery().Secrecy >= ReadDocumentAction.ObscuredAt);
        }

        /// <summary>Forging is work somebody does, and it is paid for in real orens.</summary>
        [Fact]
        public void AForgerChargesForTheWorkAndCannotBeAffordedByAnEmptyPurse()
        {
            RacketLab lab = RacketLab.Reached(criminal: true);
            int before = lab.Vanilla.GetMoney(lab.Player);

            lab.Run("forge", lab.BackRoom, lab.Situation.ForgerId, lab.Situation.RacketFactId);
            Assert.True(lab.Vanilla.GetMoney(lab.Player) < before);
            Assert.True(lab.Vanilla.GetMoney(lab.Situation.ForgerId) > 0);

            RacketLab broke = RacketLab.Reached(criminal: true, money: 10);
            Availability cannotPay = broke.Can("forge", broke.BackRoom, broke.Situation.ForgerId, broke.Situation.RacketFactId);
            Assert.False(cannotPay.IsAvailable);
            Assert.Contains("orens this would cost", cannotPay.Reason);
        }

        /// <summary>A botched job destroys the specimen, which took getting hold of.</summary>
        [Fact]
        public void ABotchedForgeryRuinsTheExemplarItWasCopying()
        {
            RacketLab lab = RacketLab.Reached(criminal: true, outcome: CheckOutcome.CriticalFail);

            ActionOutcome outcome = lab.Run("forge", lab.BackRoom, lab.Situation.ForgerId, lab.Situation.RacketFactId);

            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.EmploymentFactId));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.EvidenceDestroyed);
        }

        /// <summary>Selling evidence is a real trade: money now, and no case afterwards.</summary>
        [Fact]
        public void FencingEvidenceBuysMoneyAndCostsTheAbilityToShowIt()
        {
            RacketLab lab = RacketLab.Reached(criminal: true);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));
            int before = lab.Vanilla.GetMoney(lab.Player);

            ActionOutcome sold = lab.Run("fence", lab.BackRoom, lab.Situation.FenceId);

            Assert.True(sold.Succeeded);
            Assert.True(lab.Vanilla.GetMoney(lab.Player) > before);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Situation.FenceId), item => item.Id == lab.Situation.LetterBookId);
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));

            // Believing it is not the same as being able to show it, and only the second went.
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.EmploymentFactId));
        }

        /// <summary>
        /// Selling takes the object out of your hands. Burning it takes it out of everybody's,
        /// which is a different and larger thing.
        /// </summary>
        [Fact]
        public void BurningEvidenceTakesItFromEverybodyAndSellingItOnlyFromYou()
        {
            RacketLab sold = RacketLab.Reached(criminal: true);
            sold.World.Knowledge.Teach(
                sold.Situation.GuardId, sold.Situation.EmploymentFactId, KnowledgeSource.Document, 0.9, sold.Vanilla.Now, true);
            sold.Run("fence", sold.BackRoom, sold.Situation.FenceId);
            Assert.True(sold.World.Knowledge.CanProve(sold.Situation.GuardId, sold.Situation.EmploymentFactId));

            RacketLab burned = RacketLab.Reached(criminal: true);
            burned.World.Knowledge.Teach(
                burned.Situation.GuardId, burned.Situation.EmploymentFactId, KnowledgeSource.Document, 0.9, burned.Vanilla.Now, true);

            ActionOutcome outcome = burned.Run("destroy_evidence", burned.BackRoom, EntityId.None);

            Assert.True(outcome.Succeeded);
            Assert.DoesNotContain(burned.Vanilla.GetInventory(burned.Player), item => item.Id == burned.Situation.LetterBookId);
            Assert.False(burned.World.Knowledge.CanProve(burned.Situation.GuardId, burned.Situation.EmploymentFactId));
            Assert.True(burned.World.Knowledge.Knows(burned.Situation.GuardId, burned.Situation.EmploymentFactId));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.EvidenceDestroyed);
        }

        /// <summary>
        /// A player who pointed at one thing does not get another burned instead, and pointing at
        /// something that proves nothing is not offered at all.
        /// </summary>
        [Fact]
        public void DestroyingEvidenceBurnsTheObjectYouNamedOrNothing()
        {
            RacketLab lab = RacketLab.WithPapers(criminal: true);
            EntityId keepsake = lab.World.NewId("item");
            lab.Vanilla.GiveItem(lab.Player, new ItemDescriptor(keepsake, "a tin whistle", "misc", 4));

            ActionContext pointless = lab.Context(lab.Market, EntityId.None);
            pointless.SubjectItem = keepsake;
            Assert.False(lab.Actions.Get("destroy_evidence").GetAvailability(pointless).IsAvailable);

            ActionContext named = lab.Context(lab.Market, EntityId.None);
            named.SubjectItem = lab.Situation.LetterBookId;
            lab.Actions.Get("destroy_evidence").Perform(named);

            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LetterBookId);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == keepsake);
        }

        [Fact]
        public void SabotageBreaksSomethingOfTheirsAndIsRecordedAgainstThem()
        {
            RacketLab lab = RacketLab.Create(criminal: true);

            ActionOutcome outcome = lab.Run("sabotage", lab.Market, lab.Situation.CollectorId);

            Assert.True(outcome.Succeeded);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Situation.CollectorId), item => item.Id == lab.Situation.CudgelId);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Harmed && e.Target == lab.Situation.CollectorId);
        }

        /// <summary>`PM 62`'s canonical impossible precondition, stated as such.</summary>
        [Fact]
        public void BlackmailWithoutLeverageIsNotAnAttempt()
        {
            RacketLab lab = RacketLab.Create(criminal: true);

            Availability nothing = lab.Can("extort", lab.Market, lab.Situation.GuardId);

            Assert.False(nothing.IsAvailable);
            Assert.Equal("you have nothing on them", nothing.Reason);
        }

        [Fact]
        public void ExtortionTakesRealMoneyAndTellsThemExactlyWhoHasThemByTheThroat()
        {
            RacketLab lab = RacketLab.Create(criminal: true);
            lab.Run("question", lab.Market, lab.Situation.VictimId, lab.Situation.RacketFactId);
            int before = lab.Vanilla.GetMoney(lab.Player);

            ActionOutcome outcome = lab.Run("extort", lab.CountingHouse, lab.Situation.RacketeerId, lab.Situation.RacketFactId);

            Assert.True(outcome.Succeeded);
            Assert.True(lab.Vanilla.GetMoney(lab.Player) > before);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Threatened);

            Fact watching = lab.World.Knowledge.Facts.Values.Single(
                f => f.Predicate == FactPredicates.Investigating && f.Object == lab.Situation.RacketeerId);
            Assert.True(lab.World.Knowledge.Knows(lab.Situation.RacketeerId, watching.Id));
        }

        [Fact]
        public void SmugglingReachesSomebodyYouAreNotStandingNextTo()
        {
            RacketLab lab = RacketLab.Reached(criminal: true);

            ActionContext context = lab.Context(lab.BackRoom, lab.Situation.FenceId);
            context.ThirdParty = lab.Situation.VictimId;
            ActionOutcome outcome = lab.Actions.Get("smuggle").Perform(context);

            Assert.True(outcome.Succeeded);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Situation.VictimId), item => item.Id == lab.Situation.LetterBookId);
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.EmploymentFactId));
        }

        /// <summary>Nobody needs a smuggler to hand something to the person in front of them.</summary>
        [Fact]
        public void SmugglingIsNotOfferedToSomebodyStandingRightThere()
        {
            RacketLab lab = RacketLab.Reached(criminal: true);

            ActionContext context = lab.Context(lab.BackRoom, lab.Situation.FenceId);
            context.ThirdParty = lab.Situation.ForgerId;
            Availability pointless = lab.Actions.Get("smuggle").GetAvailability(context);

            Assert.False(pointless.IsAvailable);
            Assert.Contains("standing right there", pointless.Reason);
        }

        [Fact]
        public void ImpersonationNeedsAPropAndAStranger()
        {
            RacketLab bare = RacketLab.Create(criminal: true);
            Availability nothingToShow = bare.Can("impersonate", bare.Market, bare.Situation.CollectorId);
            Assert.False(nothingToShow.IsAvailable);
            Assert.Contains("nothing about you says you are anyone else", nothingToShow.Reason);

            RacketLab known = RacketLab.WithPapers(criminal: true);
            known.Vanilla.SetAffinity(known.Situation.CollectorId, ImpersonateAction.KnowsYourFaceAt);
            Availability tooFamiliar = known.Can("impersonate", known.Market, known.Situation.CollectorId);
            Assert.False(tooFamiliar.IsAvailable);
            Assert.Contains("knows your face too well", tooFamiliar.Reason);
        }

        /// <summary>What a costume buys is what the station would have been told - and no more.</summary>
        [Fact]
        public void BeingTakenForSomebodyElseGetsYouWhatThatManWouldHaveBeenTold()
        {
            // Papers but no belief yet: Vurl has something the player has not been told.
            RacketLab lab = RacketLab.WithPapers(criminal: true);
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.RacketFactId));

            ActionOutcome outcome = lab.Run("impersonate", lab.Market, lab.Situation.CollectorId, lab.Situation.RacketFactId);

            Assert.True(outcome.Succeeded);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.RacketFactId));

            // Told, not shown. A story a stranger swallowed is not something a guard would accept.
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.RacketFactId));
            lab.World.Knowledge.TryGetBelief(lab.Player, lab.Situation.RacketFactId, out KnowledgeRecord belief);
            Assert.Equal(lab.Situation.CollectorId, belief.ToldBy);
        }

        [Fact]
        public void EveryCrimeVerbIsRegisteredWithACheckAndAFamily()
        {
            string[] verbs =
            {
                "trespass", "forge", "fence", "smuggle", "sabotage", "extort", "destroy_evidence", "impersonate"
            };

            ActionRegistry registry = StandardActions.CreateRegistry();
            foreach (string verb in verbs)
            {
                Assert.NotNull(registry.Get(verb));
                Assert.NotNull(ProceduralCheckProfiles.ForAction(verb));
                Assert.Equal(ActionFamily.Crime, registry.Get(verb).Family);
            }
        }

        /// <summary>A racket nobody wrote down, and the people who could do something about it.</summary>
        private sealed class RacketLab
        {
            private RacketLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public ProtectionRacketSituation Situation { get; private set; }

            public EntityId Market => Situation.MarketZoneId;

            public EntityId CountingHouse => Situation.CountingHouseZoneId;

            public EntityId BackRoom => Situation.BackRoomZoneId;

            public static RacketLab Create(bool criminal, CheckOutcome outcome = CheckOutcome.Pass, int money = 2000)
            {
                RacketLab lab = new RacketLab();
                NarrativeWorldState world = new NarrativeWorldState(25025);
                EntityId player = world.NewId("npc");
                EntityId market = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 6, money: money, zone: market);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                if (criminal)
                {
                    vanilla.SetGuildRank(GuildId.Thieves, 2);
                }

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);
                lab.Situation = ProtectionRacketSituation.Create(world, new SandboxStager(vanilla), player, market, vanilla.Now);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            /// <summary>
            /// The letter-book in hand, and still nothing known about the racket. That is the state
            /// the two verbs that want a document but not a belief - impersonating somebody,
            /// selling the thing on - are actually interesting from.
            /// </summary>
            public static RacketLab WithPapers(bool criminal, CheckOutcome outcome = CheckOutcome.Pass, int money = 2000)
            {
                RacketLab lab = Create(criminal, CheckOutcome.Pass, money);
                lab.Run("trespass", lab.CountingHouse, EntityId.None);
                lab.Run("search", lab.CountingHouse, EntityId.None, lab.Situation.EmploymentFactId);
                lab.Checks.Standing = outcome;
                return lab;
            }

            /// <summary>
            /// The state the criminal route reaches after the honest half of it: the racket
            /// believed and unprovable, the letter-book in hand. Several verbs are only interesting
            /// from here, and re-walking those three calls in each test would bury what is being
            /// asserted.
            /// </summary>
            public static RacketLab Reached(bool criminal, CheckOutcome outcome = CheckOutcome.Pass, int money = 2000)
            {
                RacketLab lab = WithPapers(criminal, CheckOutcome.Pass, money);
                lab.Run("question", lab.Market, lab.Situation.VictimId, lab.Situation.RacketFactId);
                lab.Checks.Standing = outcome;
                return lab;
            }

            public NarrativeSite Site(EntityId zone) => World.Registry.GetSite(zone);

            /// <summary>The forgery this world now contains. There is only ever meant to be one.</summary>
            public Fact Forgery()
            {
                return World.Knowledge.Facts.Values.Single(f => f.Predicate == FactPredicates.Forged);
            }

            public bool Reaches(EntityId contact)
            {
                return UnderworldPolicy.WillDealWith(Context(BackRoom, contact), contact);
            }

            public ActionContext Context(EntityId zone, EntityId target, EntityId subjectFact = default)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread,
                    SubjectFact = subjectFact
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target, EntityId subjectFact = default)
            {
                return Actions.Get(actionId).Perform(Context(zone, target, subjectFact));
            }

            public Availability Can(string actionId, EntityId zone, EntityId target, EntityId subjectFact = default)
            {
                return Actions.Get(actionId).GetAvailability(Context(zone, target, subjectFact));
            }
        }
    }
}
