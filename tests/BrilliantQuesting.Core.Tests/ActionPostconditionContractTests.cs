using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-013. What each of the three endings of a verb actually leaves behind, classified by
    /// the verb and held to by the library rather than described in a document.
    ///
    /// The failure this exists to prevent is quiet in both directions: an attempt that changed
    /// nothing reported as a deed, and a deed reported with no change behind it. Both produce a
    /// plausible narration, a plausible ledger entry and a world that has not moved.
    /// </summary>
    public class ActionPostconditionContractTests
    {
        /// <summary>
        /// The families the beta's property, economic and false-belief chains run through are
        /// Crime, Economic, Information and Social, and every verb in them says what each of its
        /// endings leaves behind - except the three whose effects nobody has been able to declare
        /// at all, which stay the earlier open question (BQa-010) rather than being given an
        /// answer here they cannot support.
        /// </summary>
        [Fact]
        public void EveryChainFamilyVerbClassifiesItsEndings()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();
            ActionPostconditionCoverage coverage = registry.PostconditionCoverage();

            HashSet<ActionFamily> chains = new HashSet<ActionFamily>
            {
                ActionFamily.Crime,
                ActionFamily.Economic,
                ActionFamily.Information,
                ActionFamily.Social
            };

            List<string> gaps = registry.Actions
                .Where(a => chains.Contains(a.Family) && a.Effects.IsDeclared && !a.Postconditions.IsDeclared)
                .Select(a => a.Family + "/" + a.Id)
                .ToList();
            Assert.Empty(gaps);

            // Reported rather than defaulted: a verb with nothing said about what it changes
            // cannot have its endings classified either, and it is named instead of being counted
            // as done.
            Assert.Equal(
                new[] { "buy_business", "host", "invoke_authority", "invoke_blessing", "make_offering", "recruit_specialist", "reopen_business" },
                coverage.WithoutDeclaredEffects.Select(a => a.Id).OrderBy(id => id, System.StringComparer.Ordinal).ToArray());

            // Nothing invented a failure class, and no success postcondition claims a change its
            // own verb never said it could make.
            Assert.Empty(coverage.UnregisteredFailureClasses);
            Assert.Empty(coverage.SuccessChangesOutsideEffects);

            // Every classified verb in those families names at least one of the five shapes for
            // its failures, or says in as many words that it has none - a verb with a roll and no
            // declared failure would be the silent default this report exists to prevent.
            foreach (NarrativeAction action in registry.Actions.Where(a => chains.Contains(a.Family) && a.Postconditions.IsDeclared))
            {
                foreach (string failureClass in action.Postconditions.FailureClasses)
                {
                    Assert.True(FailureOutcomes.IsRegistered(failureClass), action.Id + " names " + failureClass);
                }
            }
        }

        /// <summary>
        /// A failure that changes a later pressure, and a failure that legitimately changes
        /// nothing, read through one production authority so the contrast is the same
        /// measurement twice.
        ///
        /// <see cref="PressureFeedback"/> is BQa-012's seam: it collects what changed and whom
        /// the changed records name, and it interprets nothing. Nobody is told to react and no
        /// follow-up goal is injected - an accusation rebounds, the ledger carries it, and the
        /// person it was about is in the next pass. A lie nobody believed is not, because there
        /// is nothing for it to be in.
        /// </summary>
        [Fact]
        public void AFailedAccusationReachesTheNextPressureReadingAndADisbelievedLieDoesNot()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId thief = lab.Situation.ThiefId;
            TellThePlayerWhoTookIt(lab);

            PressureFeedback feedback = new PressureFeedback(lab.World);
            feedback.Attach();
            feedback.Take();

            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);
            ActionOutcome botched = lab.Perform("expose", lab.Situation.VictimId);

            // Performed and did not come off - not a refusal, and not a success.
            Assert.Equal(ActionResolution.Failed, botched.Resolution);
            Assert.False(botched.Succeeded);
            Assert.Empty(botched.Changed);
            Assert.Empty(botched.ContractViolations);

            // The knowledge route the failure actually walked: the accused was told, by somebody.
            Fact investigating = lab.World.Knowledge.Facts.Values
                .Single(f => f.Predicate == FactPredicates.Investigating && f.Object == thief);
            Assert.True(lab.World.Knowledge.Knows(thief, investigating.Id));

            PressurePass afterFailure = feedback.Take();
            Assert.False(afterFailure.IsEmpty);
            Assert.Contains(thief, afterFailure.Actors);

            // Now the other kind of failure, in the same world and through the same seam.
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);
            ActionOutcome disbelieved = lab.Perform("lie", lab.Situation.VictimId);

            Assert.Equal(ActionResolution.Failed, disbelieved.Resolution);
            Assert.Empty(disbelieved.Events);
            Assert.True(feedback.Take().IsEmpty, "a lie nobody believed reached the next pressure reading");
        }

        /// <summary>
        /// And a failure that legitimately changes nothing. A lie nobody believes is the cleanest
        /// case in the library: no event, no belief moved, no standing spent, and the whole saved
        /// world byte-identical afterwards.
        ///
        /// It is here because the opposite rule is the tempting one. "Every failure must add a
        /// complication" would make this branch invent something, and the roll already has a
        /// branch that costs the liar everything - contradicting yourself in front of the room.
        /// </summary>
        [Fact]
        public void ALieNobodyBelievesLeavesTheWorldExactlyAsItWas()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            TellThePlayerWhoTookIt(lab);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);

            string before = WorldStateSerializer.Save(lab.World);
            int eventsBefore = lab.World.Ledger.Events.Count;

            ActionOutcome disbelieved = lab.Perform("lie", lab.Situation.VictimId);

            Assert.Equal(ActionResolution.Failed, disbelieved.Resolution);
            Assert.Empty(disbelieved.Changed);
            Assert.Empty(disbelieved.Events);
            Assert.Empty(disbelieved.ContractViolations);

            Assert.Equal(before, WorldStateSerializer.Save(lab.World));
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);

            // The verb says so in advance, which is what lets a caller distinguish this from a
            // verb nobody has classified.
            Assert.True(lab.Actions.Get("lie").Postconditions.CanFailAs(FailureOutcomes.NoMaterialChange));
        }

        /// <summary>
        /// A native write the build will not carry cannot be narrated as a deed.
        ///
        /// The capability is advertised, so availability offers the verb; the write policy
        /// refuses this particular reach, which is exactly the shape of an operation-level
        /// unsupported exit. Before BQa-013 the body recorded `ItemReturned`, resolved the matter
        /// and reported success, leaving a settled theft with the ring still in the actor's pack.
        /// </summary>
        [Fact]
        public void AnUnsupportedNativeWriteIsRefusedRatherThanRecordedAsADeed()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId victim = lab.Situation.VictimId;
            EntityId item = lab.Situation.ItemId;

            GiveToPlayer(lab, item);
            Assert.True(lab.Vanilla.Supports(VanillaCapability.TransferItems));

            // Somebody vanilla content depends on: reachable socially, not reachable in the pack.
            lab.Vanilla.SetActorClass(victim, NarrativeActorClass.StoryCritical);

            int eventsBefore = lab.World.Ledger.Events.Count;
            ActionOutcome handedBack = lab.Perform("return_item", victim);

            Assert.Equal(ActionResolution.Refused, handedBack.Resolution);
            Assert.False(handedBack.Succeeded);
            Assert.True(handedBack.Refused);
            Assert.Empty(handedBack.Changed);

            // Nothing became history, and nothing was closed on the strength of a label.
            Assert.Empty(handedBack.Events);
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);
            Assert.Null(lab.Situation.Thread.Resolution);
            Assert.Empty(handedBack.ContractViolations);

            // With the same build and the same reach permitted, the same call is a deed.
            lab.Vanilla.SetActorClass(victim, NarrativeActorClass.OrdinaryCitizen);
            ActionOutcome carried = lab.Perform("return_item", victim);

            Assert.Equal(ActionResolution.Succeeded, carried.Resolution);
            Assert.Contains(SemanticEffects.PossessionTransferred, carried.Changed);
            Assert.Contains(carried.Events, e => e.Type == WorldEventType.ItemReturned);
        }

        /// <summary>
        /// The guard is the shared one, not this verb's memory: a success claim with nothing
        /// behind it is demoted where every attempt passes through, so a verb whose author forgot
        /// cannot report a deed either.
        /// </summary>
        [Fact]
        public void ASuccessClaimWithNoDeclaredChangeBehindItIsDemotedByTheLibrary()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ForgetfulAction forgetful = new ForgetfulAction();

            ActionOutcome outcome = forgetful.Perform(lab.Context(lab.Situation.VictimId));

            Assert.Equal(ActionResolution.Refused, outcome.Resolution);
            Assert.False(outcome.Succeeded);
            Assert.Contains(outcome.Notes, n => n.Contains("claims success only where it changes something it declared"));

            // A verb nobody has classified is left alone: an unclassified verb is a reported
            // coverage gap, never a breach invented against it.
            UnclassifiedAction unclassified = new UnclassifiedAction();
            ActionOutcome untouched = unclassified.Perform(lab.Context(lab.Situation.VictimId));
            Assert.Equal(ActionResolution.Succeeded, untouched.Resolution);
            Assert.Empty(untouched.ContractViolations);
        }

        /// <summary>
        /// Native refusal is audited apart from a performed-but-unsuccessful act, and the reason
        /// it has to be is that both end with the actor holding nothing. A refusal records no
        /// history and no change; a failure is a real occurrence with real fallout.
        /// </summary>
        [Fact]
        public void ARefusalIsNotAFailureAndRecordsNoHistory()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            TellThePlayerWhoTookIt(lab);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

            // Resolution time, not the availability gate: the verb is performed and discovers
            // that what it needed is not there. Nothing was attempted, so nothing may read as an
            // attempt - and no roll was spent finding that out.
            ActionContext nothingHeld = lab.Context(lab.Situation.WitnessId);
            nothingHeld.SubjectFact = EntityId.None;
            ActionOutcome noLeverage = lab.Actions.Get("extort").Perform(nothingHeld);

            Assert.Equal(ActionResolution.Refused, noLeverage.Resolution);
            Assert.Null(noLeverage.Check);
            Assert.Empty(noLeverage.Events);
            Assert.Empty(noLeverage.Changed);
            Assert.Empty(noLeverage.ContractViolations);

            // And a performed failure of a verb that does have something to work with keeps its
            // own answer: it happened, and it did not come off.
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);
            ActionOutcome disbelieved = lab.Perform("lie", lab.Situation.VictimId);
            Assert.Equal(ActionResolution.Failed, disbelieved.Resolution);
            Assert.NotNull(disbelieved.Check);
            Assert.False(disbelieved.Refused);
        }

        /// <summary>
        /// A failure nobody was watching cannot produce witnesses, and so cannot cost anybody
        /// trust. The room is not read off screen, so an empty witness list there is a gap rather
        /// than an observation, and manufacturing eyewitnesses out of it is the one route by
        /// which a hidden failure could reach somebody's opinion of the actor.
        /// </summary>
        [Fact]
        public void AHiddenFailureCreatesNoWitnessesEvenWhereTheCallerNamedSome()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            TellThePlayerWhoTookIt(lab);
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);

            ActionContext watched = lab.Context(lab.Situation.VictimId);
            Assert.NotEmpty(watched.Witnesses);
            ActionOutcome inTheOpen = lab.Actions.Get("lie").Perform(watched);

            Assert.Contains(inTheOpen.Events, e => e.Type == WorldEventType.DeceptionExposed && e.Witnesses.Count > 0);
            Assert.Empty(inTheOpen.ContractViolations);

            // The same failure, the same room on the caller's list, and nobody's presence read.
            TheftLaboratory unwatched = TheftLaboratory.Create();
            TellThePlayerWhoTookIt(unwatched);
            unwatched.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);
            ActionContext offScreen = unwatched.Context(unwatched.Situation.VictimId);
            Assert.NotEmpty(offScreen.Witnesses);
            offScreen.Observation = ContextObservation.OffScreen;

            ActionOutcome hidden = unwatched.Actions.Get("lie").Perform(offScreen);

            Assert.Equal(ContextObservation.OffScreen, hidden.Observation);
            Assert.All(hidden.Events, e => Assert.Empty(e.Witnesses));
            Assert.Empty(hidden.ContractViolations);
        }

        /// <summary>
        /// Reading a verb's classification is a read. Asking every registered verb what each of
        /// its endings leaves behind, and asking the registry where it has said nothing, leaves
        /// the world byte-identical.
        /// </summary>
        [Fact]
        public void ClassificationInspectionMutatesNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            string before = WorldStateSerializer.Save(lab.World);
            int eventsBefore = lab.World.Ledger.Events.Count;

            foreach (NarrativeAction action in lab.Actions.Actions)
            {
                ActionPostconditions declared = action.Postconditions;
                foreach (string failureClass in FailureOutcomes.All)
                {
                    declared.CanFailAs(failureClass);
                }

                foreach (string kind in SemanticEffects.All)
                {
                    declared.SucceedsBy(kind);
                }
            }

            lab.Actions.PostconditionCoverage();

            Assert.Equal(before, WorldStateSerializer.Save(lab.World));
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);
        }

        /// <summary>
        /// Every ending of every classified verb the property chain runs through, driven through
        /// production `Perform`, agrees with what the verb said it would leave behind.
        ///
        /// The point is not that these particular verbs are correct; it is that the audit runs on
        /// every attempt, so a body that starts moving state its classification does not mention
        /// is caught at the seam rather than by somebody reading the diff.
        /// </summary>
        [Theory]
        [InlineData(CheckOutcome.CriticalPass)]
        [InlineData(CheckOutcome.Pass)]
        [InlineData(CheckOutcome.Fail)]
        [InlineData(CheckOutcome.CriticalFail)]
        public void EveryEndingOfThePropertyChainAgreesWithItsClassification(CheckOutcome scripted)
        {
            string[] chain = { "pickpocket", "search", "expose", "lie", "keep_item", "return_item", "destroy_evidence" };
            int resolved = 0;

            foreach (string verb in chain)
            {
                TheftLaboratory lab = TheftLaboratory.Create();
                TellThePlayerWhoTookIt(lab);
                lab.Checks = new FixedCheckResolver(scripted);
                GiveToPlayer(lab, lab.Situation.ItemId);

                EntityId target = verb == "pickpocket" ? lab.Situation.ThiefId : lab.Situation.VictimId;
                ActionOutcome outcome = lab.Perform(verb, target);

                Assert.Empty(ActionPostconditionAudit.Check(lab.Actions.Get(verb), outcome));
                Assert.Empty(outcome.ContractViolations);

                if (!outcome.Refused)
                {
                    resolved++;
                }
            }

            // The audit has to have had something to look at: a run where every verb was refused
            // at the gate would pass this vacuously.
            Assert.True(resolved >= 3, "only " + resolved + " of the chain resolved at " + scripted);
        }

        /// <summary>
        /// The same sweep over the whole registry, so a verb outside the property chain cannot
        /// quietly start disagreeing with its own classification either.
        ///
        /// Most of the library is unavailable in a three-person theft, and a refusal is audited
        /// just as a deed is - what this catches is the branch that runs. It is the cheap net
        /// under every future edit to a body whose verb has already been classified.
        /// </summary>
        [Theory]
        [InlineData(CheckOutcome.CriticalPass)]
        [InlineData(CheckOutcome.Pass)]
        [InlineData(CheckOutcome.Fail)]
        [InlineData(CheckOutcome.CriticalFail)]
        public void NoClassifiedVerbContradictsItselfOnAnyEndingItCanReach(CheckOutcome scripted)
        {
            List<string> breaches = new List<string>();

            foreach (string verb in StandardActions.CreateRegistry().Actions
                         .Where(a => a.Postconditions.IsDeclared)
                         .Select(a => a.Id))
            {
                for (int which = 0; which < 3; which++)
                {
                    TheftLaboratory lab = TheftLaboratory.Create();
                    TellThePlayerWhoTookIt(lab);
                    lab.Checks = new FixedCheckResolver(scripted);
                    GiveToPlayer(lab, lab.Situation.ItemId);

                    ActionOutcome outcome = lab.Perform(
                        verb, PartyOf(lab, which), c => c.ThirdParty = lab.Situation.WitnessId);

                    breaches.AddRange(outcome.ContractViolations.Select(v => verb + " @ " + scripted + ": " + v));
                }
            }

            Assert.Empty(breaches);
        }

        /// <summary>Victim, thief and witness in turn, so a verb meets somebody it can act on.</summary>
        private static EntityId PartyOf(TheftLaboratory lab, int which)
        {
            switch (which)
            {
                case 1:
                    return lab.Situation.ThiefId;
                case 2:
                    return lab.Situation.WitnessId;
                default:
                    return lab.Situation.VictimId;
            }
        }

        /// <summary>
        /// The player learns who took it, provably. Fixture input - the theft already happened in
        /// the situation's own setup, and this is only the player coming to hold it, which is
        /// what the verbs that deny, disclose or report a claim require before they mean anything.
        /// </summary>
        private static void TellThePlayerWhoTookIt(TheftLaboratory lab)
        {
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, true);
        }

        private static void GiveToPlayer(TheftLaboratory lab, EntityId item)
        {
            lab.Vanilla.TryTransferItem(item, lab.Situation.ThiefId, lab.Player);
        }

        /// <summary>
        /// A verb this repository does not ship. It declares that success means an object changed
        /// hands and then never records one, which is the mistake the shared gate exists to catch.
        /// </summary>
        private sealed class ForgetfulAction : NarrativeAction
        {
            public ForgetfulAction() : base("forgetful", ActionFamily.Crime, "Forget to say what changed")
            {
            }

            public override ActionEffects Effects => ActionEffects
                .Declaring(ActionEffect.Recorded(SemanticEffects.PossessionTransferred));

            public override ActionPostconditions Postconditions => ActionPostconditions
                .Succeeding(SemanticEffects.PossessionTransferred);

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context) =>
                new ActionOutcome(Id, null, "Something happened, apparently.");
        }

        /// <summary>The same verb with nothing said about it, which must be left exactly alone.</summary>
        private sealed class UnclassifiedAction : NarrativeAction
        {
            public UnclassifiedAction() : base("unclassified", ActionFamily.Crime, "Say nothing about it")
            {
            }

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context) =>
                new ActionOutcome(Id, null, "Something happened, apparently.");
        }
    }
}
