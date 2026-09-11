using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// The rule under test throughout: options are hidden only for impossibility, never for
    /// incompetence. A bad liar sees "lie"; someone with nothing to lie about does not.
    /// </summary>
    public class ActionAvailabilityTests
    {
        [Fact]
        public void YouCannotRevealSomethingYouDoNotKnow()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Availability availability = lab.Actions.Get("expose").GetAvailability(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("do not know", availability.Reason);
        }

        [Fact]
        public void OnceYouKnowIt_YouCanTryToRevealIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, Knowledge.KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Assert.True(lab.Actions.Get("expose").GetAvailability(context).IsAvailable);
        }

        [Fact]
        public void AHopelessLiarIsStillOfferedTheLie()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.SetSkill(lab.Player, VanillaSkill.Negotiation, 0);
            lab.Vanilla.SetAttribute(lab.Player, VanillaAttribute.Charisma, 3);
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, Knowledge.KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Assert.True(lab.Actions.Get("lie").GetAvailability(context).IsAvailable);
        }

        [Fact]
        public void YouCannotOfferMoneyYouDoNotHave()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Define(lab.Player, level: 5, money: 0, zone: lab.Zone);

            Availability availability = lab.Actions.Get("bribe").GetAvailability(lab.Context(lab.Situation.WitnessId));

            Assert.False(availability.IsAvailable);
            Assert.Contains("orens you do not have", availability.Reason);
        }

        [Fact]
        public void PickpocketingNeedsSomethingToTake()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.True(lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.ThiefId)).IsAvailable);
            Assert.False(lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.WitnessId)).IsAvailable);
        }

        [Fact]
        public void TheStolenItemHasAVanillaSourceId()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            IReadOnlyList<ItemDescriptor> carried = lab.Vanilla.GetInventory(lab.Situation.ThiefId);

            Assert.Single(carried);
            Assert.Equal(lab.Situation.ItemId, carried[0].Id);
            Assert.False(string.IsNullOrEmpty(carried[0].SourceId));
        }

        [Fact]
        public void BuildingRapportIsASoftAvailableAction()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ActionOutcome outcome = lab.Perform("rapport", lab.Situation.WitnessId);

            Assert.Equal("rapport", outcome.ActionId);
            Assert.False(lab.Actions.Get("rapport").GetAvailability(lab.Context(lab.Situation.WitnessId)).IsAvailable);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.Helped);
        }

        [Fact]
        public void AMissingVanillaCapabilityBlocksTheRouteInsteadOfFailingMidway()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.SetCapability(VanillaCapability.TransferItems, false);

            Availability availability = lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.ThiefId));

            Assert.False(availability.IsAvailable);
            Assert.Contains("unavailable on this build", availability.Reason);
        }

        [Fact]
        public void TheSituationOffersSeveralDistinctSolutionFamilies()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (EntityId target in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
            {
                families.UnionWith(lab.Actions.AvailableFamilies(lab.Context(target)));
            }

            // The design target is at least three distinct routes into any major situation.
            Assert.True(families.Count >= 3, "expected 3+ solution families, got " + families.Count);
        }

        [Fact]
        public void RejectedOptionsKeepTheirReasonForTheInspector()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            List<ActionOffer> all = lab.Actions.Discover(lab.Context(lab.Situation.WitnessId), includeUnavailable: true);

            Assert.Equal(lab.Actions.Actions.Count, all.Count);
            foreach (ActionOffer offer in all)
            {
                if (!offer.Availability.IsAvailable)
                {
                    Assert.False(string.IsNullOrEmpty(offer.Availability.Reason));
                }
            }
        }

        // --- BQa-005: feasibility before difficulty ------------------------------------------

        /// <summary>
        /// The impossible path. A secret the actor never heard of is not a hard attempt, and the
        /// proof that it is not is that nothing downstream of the refusal runs: no resolver is
        /// asked, and the actor's RNG stream is exactly where it was.
        /// </summary>
        [Fact]
        public void AnImpossibleAttemptIsRefusedWithItsReasonAndNeverReachesTheDice()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            CountingCheckResolver checks = new CountingCheckResolver(new FixedCheckResolver(CheckOutcome.Pass));
            DeterministicRng rng = new DeterministicRng(4242UL);
            ulong before = rng.State;

            ActionAttempt attempt = Attempt(lab, checks, rng, "expose", lab.Situation.VictimId);

            Assert.False(attempt.Feasibility.IsPossible);
            Assert.Contains("do not know", attempt.Refusal);

            // Not classified at all, rather than classified as certain: "there is no uncertainty
            // here" is permission to resolve without a roll, and a refused attempt must not
            // resolve at all.
            Assert.Null(attempt.Feasibility.Uncertainty);
            Assert.Null(attempt.Feasibility.Profile);
            Assert.False(attempt.Feasibility.IsCertain);

            Assert.Null(attempt.Outcome);
            Assert.Equal(0, checks.Calls);
            Assert.Equal(before, rng.State);
        }

        /// <summary>
        /// The same for a physical impossibility rather than an epistemic one: you cannot hand
        /// back what you are not carrying, and no roll decides whether you managed it.
        /// </summary>
        [Fact]
        public void YouCannotRollToHandBackSomethingYouAreNotCarrying()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            CountingCheckResolver checks = new CountingCheckResolver(new FixedCheckResolver(CheckOutcome.Pass));
            DeterministicRng rng = new DeterministicRng(11UL);
            ulong before = rng.State;

            ActionAttempt attempt = Attempt(lab, checks, rng, "return_item", lab.Situation.VictimId);

            Assert.False(attempt.Feasibility.IsPossible);
            Assert.Contains("not carrying anything of theirs", attempt.Refusal);
            Assert.Null(attempt.Outcome);
            Assert.Equal(0, checks.Calls);
            Assert.Equal(before, rng.State);
        }

        /// <summary>
        /// The semantically certain path. Once the item is in hand and the person it belongs to is
        /// standing there, handing it back is not a skill test, and the verb's own contract - no
        /// profile - is what says so. The outcome carries no check and nothing was rolled.
        /// </summary>
        [Fact]
        public void ACertainAttemptResolvesWithoutInventingARoll()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);

            CountingCheckResolver checks = new CountingCheckResolver(new FixedCheckResolver(CheckOutcome.Pass));
            DeterministicRng rng = new DeterministicRng(11UL);
            ulong before = rng.State;

            ActionAttempt attempt = Attempt(lab, checks, rng, "return_item", lab.Situation.VictimId);

            Assert.True(attempt.Feasibility.IsPossible);
            Assert.True(attempt.Feasibility.IsCertain);
            Assert.Equal(CheckFamily.Certain, attempt.Feasibility.Uncertainty);
            Assert.Null(attempt.Feasibility.Profile);

            Assert.NotNull(attempt.Outcome);
            Assert.Null(attempt.Outcome.Check);
            Assert.True(attempt.Outcome.Succeeded);
            Assert.Equal(0, checks.Calls);
            Assert.Equal(before, rng.State);
        }

        /// <summary>
        /// The genuinely uncertain path, and the line certainty is not allowed to cross. A liar
        /// this far above the person being lied to will almost never fail, and "almost never" is
        /// still a roll: the attempt keeps its classified family, its profile and its dice.
        /// </summary>
        [Fact]
        public void OverwhelmingAdvantageIsStillUncertaintyAndStillRolls()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId mark = lab.Situation.WitnessId;
            Believe(lab, lab.Player);
            Overwhelm(lab, lab.Player, mark);

            CountingCheckResolver checks = new CountingCheckResolver(new VanillaStyleCheckResolver(lab.Vanilla));
            DeterministicRng rng = new DeterministicRng(2026UL);
            ulong before = rng.State;

            ActionAttempt attempt = Attempt(lab, checks, rng, "lie", mark);

            Assert.True(attempt.Feasibility.IsUncertain);
            Assert.Equal(CheckFamily.Opposed, attempt.Feasibility.Uncertainty);
            Assert.Equal(ProceduralCheckProfiles.Deception, attempt.Feasibility.Profile);

            Assert.NotNull(attempt.Outcome.Check);
            Assert.Equal("proc_deception", attempt.Outcome.Check.ProfileId);
            Assert.Equal(1, checks.Calls);
            Assert.NotEqual(before, rng.State);

            // A DC this far below the die is what mastery buys. It is not what certainty is.
            Assert.True(
                attempt.Outcome.Check.FinalDifficulty < ProceduralCheckProfiles.Deception.BaseDifficulty,
                "the extreme advantage never reached the difficulty");
        }

        /// <summary>
        /// Routing survives both extremes. Whichever way round the mismatch runs, an opposed verb
        /// stays opposed and keeps the ratio working BQa-004 gave it, and an absolute one stays a
        /// fixed challenge with nothing on the other side.
        /// </summary>
        [Fact]
        public void AnUncertainAttemptKeepsItsFamilyAtEitherExtreme()
        {
            foreach (bool actorDominates in new[] { true, false })
            {
                TheftLaboratory lab = TheftLaboratory.Create();
                EntityId mark = lab.Situation.WitnessId;
                Believe(lab, lab.Player);
                if (actorDominates)
                {
                    Overwhelm(lab, lab.Player, mark);
                }
                else
                {
                    Overwhelm(lab, mark, lab.Player);
                }

                CountingCheckResolver checks = new CountingCheckResolver(new VanillaStyleCheckResolver(lab.Vanilla));
                DeterministicRng rng = new DeterministicRng(88UL);

                ActionAttempt opposed = Attempt(lab, checks, rng, "lie", mark);
                Assert.Equal(CheckFamily.Opposed, opposed.Feasibility.Uncertainty);
                Assert.NotNull(opposed.Outcome.Check);
                Assert.Equal("proc_deception", opposed.Outcome.Check.ProfileId);
                Assert.NotNull(opposed.Outcome.Check.Opposition);

                ActionAttempt absolute = Attempt(lab, checks, rng, "search", lab.Situation.ThiefId);
                Assert.Equal(CheckFamily.Absolute, absolute.Feasibility.Uncertainty);
                Assert.Equal(ProceduralCheckProfiles.Investigation, absolute.Feasibility.Profile);
                Assert.NotNull(absolute.Outcome.Check);
                Assert.Equal("proc_investigation", absolute.Outcome.Check.ProfileId);

                // A lock does not learn who is picking it, at either extreme.
                Assert.Null(absolute.Outcome.Check.Opposition);
            }
        }

        private static ActionAttempt Attempt(
            TheftLaboratory lab, ICheckResolver checks, DeterministicRng rng, string actionId, EntityId target)
        {
            ActionContext context = new ActionContext(lab.World, lab.Vanilla, checks, rng, lab.Player, target)
            {
                Thread = lab.Situation.Thread,
                SubjectFact = lab.Situation.TheftFactId,
                SubjectItem = lab.Situation.ItemId
            };

            return ActionAttempt.Run(
                lab.Actions, new ActionIntent(lab.Player, actionId, target, "under test"), context);
        }

        /// <summary>Somebody has to have heard of the theft before they can deny it.</summary>
        private static void Believe(TheftLaboratory lab, EntityId who)
        {
            lab.World.Knowledge.Teach(
                who, lab.Situation.TheftFactId, Knowledge.KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);
        }

        /// <summary>A mismatch far outside anything the default builds produce, in one direction.</summary>
        private static void Overwhelm(TheftLaboratory lab, EntityId strong, EntityId weak)
        {
            lab.Vanilla.SetSkill(strong, VanillaSkill.Negotiation, 400);
            lab.Vanilla.SetAttribute(strong, VanillaAttribute.Charisma, 400);
            lab.Vanilla.SetAttribute(strong, VanillaAttribute.Perception, 400);
            lab.Vanilla.SetAttribute(strong, VanillaAttribute.Will, 400);

            lab.Vanilla.SetSkill(weak, VanillaSkill.Negotiation, 1);
            lab.Vanilla.SetAttribute(weak, VanillaAttribute.Charisma, 1);
            lab.Vanilla.SetAttribute(weak, VanillaAttribute.Perception, 1);
            lab.Vanilla.SetAttribute(weak, VanillaAttribute.Will, 1);
        }

        /// <summary>
        /// A resolver that counts. The claim "impossible cases never reach RNG" is only worth
        /// making if something would notice a roll, and an RNG state comparison alone would not
        /// catch a resolver that answered without drawing.
        /// </summary>
        private sealed class CountingCheckResolver : ICheckResolver
        {
            private readonly ICheckResolver _inner;

            public CountingCheckResolver(ICheckResolver inner)
            {
                _inner = inner;
            }

            public int Calls { get; private set; }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                Calls++;
                return _inner.Resolve(request, rng);
            }
        }
    }
}
