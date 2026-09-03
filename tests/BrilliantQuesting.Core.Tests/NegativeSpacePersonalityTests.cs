using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-077. What an actor will <em>not</em> do, constraining action selection and wording, and
    /// breakable only under pressure the simulation already established.
    ///
    /// The step's done-when is one assertion - a prohibition visibly costs an NPC an otherwise
    /// optimal action - and most of this file exists to prove it is the real thing rather than its
    /// several near-misses:
    ///
    /// <list type="bullet">
    /// <item>the forbidden action is <em>still the highest-scoring one</em>, so the cost is visible
    /// rather than arranged;</item>
    /// <item>it is refused where it would have been selected, never selected and then dressed
    /// differently in words;</item>
    /// <item>a breakable line gives way to established pressure and says which pressure, and an
    /// unbreakable one does not give way at all;</item>
    /// <item>a line is not a heavy weight - it never moves a score or a disclosure balance, it
    /// removes an option;</item>
    /// <item>a line that broke stops constraining wording, and a line nobody consulted never starts
    /// constraining it; and</item>
    /// <item>nothing anywhere derives a prohibition from race, character archetype or occupation.</item>
    /// </list>
    /// </summary>
    public class NegativeSpacePersonalityTests
    {
        // -- the model itself ------------------------------------------------------------------------

        [Fact]
        public void ACharacterHoldsNoLinesUntilOneIsDeclared()
        {
            NarrativeNpc anybody = new NarrativeNpc(EntityId.Parse("npc_anybody"), "Anybody");

            Assert.False(anybody.NegativeSpace.Any);
            Assert.Empty(anybody.NegativeSpace.Declared);
            foreach (PersonalProhibition kind in NegativeSpaceProfile.Vocabulary)
            {
                Assert.False(anybody.NegativeSpace.Holds(kind));
                Assert.Equal(0.0, anybody.NegativeSpace.FirmnessOf(kind));
                Assert.False(anybody.NegativeSpace.IsBreakable(kind));
            }
        }

        /// <summary>
        /// A line nobody holds forbids nothing and explains nothing. Worth pinning because the
        /// alternative - a ruling that reads as holding by default - would make every unmodelled
        /// character silently more constrained than the ones somebody wrote.
        /// </summary>
        [Fact]
        public void ALineNobodyHoldsRulesNothingOutAtAnyPressure()
        {
            NegativeSpaceProfile empty = new NegativeSpaceProfile();

            ProhibitionRuling ruling = empty.Rule(PersonalProhibition.NeverBegs, 1.0, "everything at stake");

            Assert.False(ruling.Held);
            Assert.False(ruling.Forbids);
            Assert.False(ruling.Broke);
            Assert.Equal(string.Empty, ruling.Because);
        }

        [Fact]
        public void ALineHoldsWhilePressureFallsShortOfItsFirmness()
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(PersonalProhibition.NeverBegs, 0.6);

            ProhibitionRuling ruling = profile.Rule(PersonalProhibition.NeverBegs, 0.59, "the goat is missing");

            Assert.True(ruling.Forbids);
            Assert.False(ruling.Broke);
            Assert.Contains("will not beg", ruling.Because);
            Assert.Contains("the goat is missing", ruling.Because);
            Assert.Contains("0.60", ruling.Because);
        }

        [Fact]
        public void SufficientPressureBreaksABreakableLineAndSaysWhatCarriedIt()
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(PersonalProhibition.NeverBegs, 0.6);

            ProhibitionRuling ruling = profile.Rule(PersonalProhibition.NeverBegs, 0.6, "the child is starving");

            Assert.True(ruling.Broke);
            Assert.False(ruling.Forbids);
            Assert.Contains("breaks the line against beg", ruling.Because);
            Assert.Contains("the child is starving", ruling.Because);
            Assert.Contains("0.60", ruling.Because);
        }

        /// <summary>
        /// A prohibition is not a physical impossibility, and an unbreakable one is not a very
        /// large firmness: it is a statement that no amount of the pressure this surface can
        /// establish is the kind of thing that would move this person.
        /// </summary>
        [Fact]
        public void AnUnbreakableLineDoesNotGiveWayUnderAnyPressure()
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(PersonalProhibition.NeverBegs, 0.1, breakable: false);

            ProhibitionRuling ruling = profile.Rule(PersonalProhibition.NeverBegs, 1.0, "everything at stake");

            Assert.True(ruling.Forbids);
            Assert.False(ruling.Broke);
            Assert.Contains("the line does not break", ruling.Because);
        }

        [Fact]
        public void PressureIsClampedRatherThanTrusted()
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(PersonalProhibition.NeverBegs, 1.0);

            Assert.True(profile.Rule(PersonalProhibition.NeverBegs, -5.0, "nonsense").Forbids);
            Assert.True(profile.Rule(PersonalProhibition.NeverBegs, 5.0, "nonsense").Broke);
            Assert.Equal(1.0, profile.Rule(PersonalProhibition.NeverBegs, 5.0, "nonsense").Pressure);
        }

        /// <summary>
        /// Declared order must not be declaration order, or two identically written characters
        /// would behave differently because of the sequence a generator happened to use.
        /// </summary>
        [Fact]
        public void DeclaredLinesComeBackInVocabularyOrderWhateverOrderTheyWereGivenIn()
        {
            NegativeSpaceProfile forwards = new NegativeSpaceProfile();
            forwards.Declare(PersonalProhibition.NeverBegs, 0.5);
            forwards.Declare(PersonalProhibition.NeverLiesDirectly, 0.5);

            NegativeSpaceProfile backwards = new NegativeSpaceProfile();
            backwards.Declare(PersonalProhibition.NeverLiesDirectly, 0.5);
            backwards.Declare(PersonalProhibition.NeverBegs, 0.5);

            Assert.Equal(
                new[] { PersonalProhibition.NeverBegs, PersonalProhibition.NeverLiesDirectly },
                forwards.Declared);
            Assert.Equal(forwards.Declared, backwards.Declared);
        }

        [Fact]
        public void WithdrawingALineLeavesNothingBehind()
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(PersonalProhibition.NeverBegs, 0.9, breakable: false);
            profile.Withdraw(PersonalProhibition.NeverBegs);

            Assert.False(profile.Any);
            Assert.False(profile.Rule(PersonalProhibition.NeverBegs, 0.0, "nothing").Held);
        }

        /// <summary>
        /// The anti-stereotype gate, held structurally rather than by inspection: a prohibition is
        /// declared onto a character, and there is nowhere in this step's own surface that could
        /// read a race, an archetype or an occupation and hand back a line. Mirrors what
        /// <c>IdentityAffordanceTests</c> and BQ-075 already pin for their own types.
        /// </summary>
        [Fact]
        public void NothingInThisStepDerivesAProhibitionFromIdentity()
        {
            Type[] surface =
            {
                typeof(NegativeSpaceProfile),
                typeof(NegativeSpace),
                typeof(ProhibitionRuling),
                typeof(DialogueManners),
                typeof(NegativeSpaceVoice)
            };

            string[] forbiddenWords = { "race", "occupation", "archetype", "hobby", "chara" };

            foreach (Type type in surface)
            {
                foreach (MethodBase method in type.GetMethods(BindingFlags.Public | BindingFlags.Static
                             | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Cast<MethodBase>()
                         .Concat(type.GetConstructors()))
                {
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        Assert.NotEqual(typeof(CharacterIdentity), parameter.ParameterType);
                        Assert.NotEqual(typeof(IdentityAffordances), parameter.ParameterType);
                        Assert.NotEqual(typeof(IVanillaState), parameter.ParameterType);
                    }

                    foreach (string word in forbiddenWords)
                    {
                        Assert.DoesNotContain(word, method.Name.ToLowerInvariant());
                    }
                }
            }
        }

        // -- action selection: the done-when ----------------------------------------------------------

        /// <summary>
        /// BQ-077's done-when, and the whole of it. The reeve's own preferences make reporting it
        /// to the guards the best answer by a clear margin; a line against involving authority
        /// takes it away, and what she does instead is the second-best thing she is willing to do.
        ///
        /// The margin is asserted rather than assumed. A prohibition that removed an action nobody
        /// was going to take would satisfy the letter of the requirement and prove nothing.
        /// </summary>
        [Fact]
        public void AProhibitionCostsAnNpcTheActionItWouldOtherwiseHaveChosen()
        {
            NarrativeNpc reeve = Reeve();

            MissingGoatDecision unconstrained = MissingGoatProblemSolver.Choose(reeve, MissingGoatProblem.OrdinaryLoss);
            Assert.Equal(MissingGoatResponse.ReportToGuards, unconstrained.Response);

            reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);

            MissingGoatDecision constrained = MissingGoatProblemSolver.Choose(reeve, MissingGoatProblem.OrdinaryLoss);
            Assert.NotEqual(MissingGoatResponse.ReportToGuards, constrained.Response);
            Assert.True(
                constrained.Score < unconstrained.Score,
                "the line has to cost her something, or it removed an action she did not want");
        }

        /// <summary>
        /// The same run, read off the trace: the action she did not take is still there, still
        /// scoring highest, and marked as the one she took off her own table. This is what makes
        /// the cost <em>visible</em> rather than merely real.
        /// </summary>
        [Fact]
        public void TheForbiddenActionStaysInTheTraceStillScoringHighest()
        {
            NarrativeNpc reeve = Reeve();
            reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);

            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(reeve, MissingGoatProblem.OrdinaryLoss, EntityId.None);
            GoalActionTrace guards = Candidate(trace, ProblemSolvingStyle.AskAuthority);

            Assert.True(guards.Forbidden);
            Assert.Equal(guards.Score, trace.CandidateActions.Max(a => a.Score));
            Assert.True(trace.ChosenAction.Score < guards.Score);
            Assert.Contains("will not involve authority", guards.Ruling.Because);
        }

        /// <summary>
        /// The rule that keeps this from being a wording trick: whatever is chosen was permitted
        /// when it was chosen. A pipeline that picked the forbidden action and relied on a later
        /// layer to hide it would pass every test above and fail this one.
        /// </summary>
        [Fact]
        public void TheChosenActionIsNeverOneTheActorHasRuledOut()
        {
            foreach (bool authority in new[] { false, true })
            {
                foreach (bool begging in new[] { false, true })
                {
                    NarrativeNpc reeve = Reeve();
                    if (authority)
                    {
                        reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 1.0, breakable: false);
                    }

                    if (begging)
                    {
                        reeve.NegativeSpace.Declare(PersonalProhibition.NeverBegs, 1.0, breakable: false);
                    }

                    GoalFormationTrace trace = MissingGoatProblemSolver.Trace(
                        reeve, MissingGoatProblem.OrdinaryLoss, EntityId.None);

                    Assert.False(trace.ChosenAction.Forbidden);
                    Assert.False(trace.ChosenAction.Ruling.Forbids);
                }
            }
        }

        /// <summary>
        /// The break, on the surface the roadmap asks the proof of. Nothing about the reeve's
        /// preferences changes; what changes is that her standing is now something she cannot bend
        /// on, and the need pressure that comes out of that is what carries the line.
        /// </summary>
        [Fact]
        public void EnoughEstablishedPressureBreaksTheLineAndTheActionComesBack()
        {
            NarrativeNpc reeve = Reeve();
            reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);
            Assert.NotEqual(
                MissingGoatResponse.ReportToGuards,
                MissingGoatProblemSolver.Choose(reeve, MissingGoatProblem.OrdinaryLoss).Response);

            reeve.Values.Status.Importance = 1.0;
            reeve.Values.Status.Flexibility = 0.0;

            MissingGoatDecision under = MissingGoatProblemSolver.Choose(reeve, MissingGoatProblem.OrdinaryLoss);

            Assert.Equal(MissingGoatResponse.ReportToGuards, under.Response);
            Assert.True(under.Ruling.Broke);
            Assert.Contains("breaks the line against involve authority", under.Ruling.Because);
            Assert.Contains("threatened value status", under.Ruling.Because);
        }

        [Fact]
        public void AnUnbreakableLineStillCostsTheActionUnderTheSamePressure()
        {
            NarrativeNpc reeve = Reeve();
            reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6, breakable: false);
            reeve.Values.Status.Importance = 1.0;
            reeve.Values.Status.Flexibility = 0.0;

            MissingGoatDecision decision = MissingGoatProblemSolver.Choose(reeve, MissingGoatProblem.OrdinaryLoss);

            Assert.NotEqual(MissingGoatResponse.ReportToGuards, decision.Response);
        }

        /// <summary>
        /// A line removes an option; it never moves a number. If it did, a firm enough prohibition
        /// would be indistinguishable from a strong preference, and "will not" would just be
        /// "would rather not" with a bigger coefficient.
        /// </summary>
        [Fact]
        public void ALineChangesWhichActionsAreEligibleAndNeverWhatAnyOfThemScores()
        {
            NarrativeNpc plain = Reeve();
            NarrativeNpc lined = Reeve();
            lined.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);

            GoalFormationTrace before = MissingGoatProblemSolver.Trace(plain, MissingGoatProblem.OrdinaryLoss, EntityId.None);
            GoalFormationTrace after = MissingGoatProblemSolver.Trace(lined, MissingGoatProblem.OrdinaryLoss, EntityId.None);

            Assert.Equal(before.NeedPressure, after.NeedPressure);
            Assert.Equal(before.CandidateActions.Count, after.CandidateActions.Count);
            for (int i = 0; i < before.CandidateActions.Count; i++)
            {
                Assert.Equal(before.CandidateActions[i].Style, after.CandidateActions[i].Style);
                Assert.Equal(before.CandidateActions[i].Score, after.CandidateActions[i].Score);
                Assert.Equal(before.CandidateActions[i].ScoreTerms, after.CandidateActions[i].ScoreTerms);
            }
        }

        /// <summary>
        /// Every line in the vocabulary at once, unbreakable, and she still does something. The
        /// vocabulary is sized so that this cannot fail; the test is here so that widening either
        /// list without thinking about it does fail.
        /// </summary>
        [Fact]
        public void NoCombinationOfLinesLeavesAnActorWithNothingTheyAreWillingToDo()
        {
            NarrativeNpc reeve = Reeve();
            foreach (PersonalProhibition kind in NegativeSpaceProfile.Vocabulary)
            {
                reeve.NegativeSpace.Declare(kind, 1.0, breakable: false);
            }

            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(reeve, MissingGoatProblem.OrdinaryLoss, EntityId.None);

            Assert.False(trace.ChosenAction.Forbidden);
            Assert.Contains(trace.CandidateActions, a => !a.Forbidden);
        }

        [Fact]
        public void TheSameActorInTheSameStateChoosesTheSameWayEveryTime()
        {
            string First()
            {
                NarrativeWorldState world = new NarrativeWorldState(4242UL);
                NarrativeNpc reeve = Reeve();
                reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);
                world.Registry.Add(reeve);
                return NarrativeInspector.DescribeGoalFormation(
                    world,
                    MissingGoatProblemSolver.Trace(reeve, MissingGoatProblem.OrdinaryLoss, EntityId.None));
            }

            Assert.Equal(First(), First());
        }

        [Fact]
        public void TheInspectorNamesTheForbiddenActionAndTheReasonItWasRefused()
        {
            NarrativeWorldState world = new NarrativeWorldState(9UL);
            NarrativeNpc reeve = Reeve();
            reeve.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 0.6);
            world.Registry.Add(reeve);

            string dump = NarrativeInspector.DescribeGoalFormation(
                world,
                MissingGoatProblemSolver.Trace(reeve, MissingGoatProblem.OrdinaryLoss, EntityId.None));

            Assert.Contains("[forbidden] missing_goat.AskAuthority", dump);
            Assert.Contains("will not involve authority", dump);
        }

        [Fact]
        public void TheCharacterSheetSaysWhatSomebodyWillNotDoAndSaysSoWhenThatIsNothing()
        {
            NarrativeWorldState world = new NarrativeWorldState(9UL);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            NarrativeNpc plain = Reeve();
            world.Registry.Add(plain);
            vanilla.Define(plain.Id);

            Assert.Contains("will not: nothing declared", NarrativeInspector.DescribeCharacter(world, vanilla, plain.Id));

            plain.NegativeSpace.Declare(PersonalProhibition.NeverBegs, 0.75, breakable: false);

            string dump = NarrativeInspector.DescribeCharacter(world, vanilla, plain.Id);
            Assert.Contains("will not: beg (firmness 0.75, unbreakable)", dump);
        }

        // -- disclosure: the same model at BQ-073's own gate -------------------------------------------

        /// <summary>
        /// The thief is frightened enough, and candid enough not to be, that BQ-073 has him deny
        /// it. A line against lying directly costs him that denial - and what he does instead is
        /// one of the moves that gate already fell through to, not a fifth thing this step
        /// invented.
        /// </summary>
        [Fact]
        public void ALineAgainstLyingCostsALiarTheDenialTheyWouldOtherwiseHaveMade()
        {
            Interrogation scene = Interrogation.WithAFrightenedThief();

            DisclosureDecision unconstrained = scene.Decide();
            Assert.True(unconstrained.WillLie);
            Assert.Equal(DisclosureTactic.Falsify, unconstrained.Tactic);

            scene.Thief.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.8);

            DisclosureDecision constrained = scene.Decide();
            Assert.False(constrained.WillLie);
            Assert.NotEqual(DisclosureTactic.Falsify, constrained.Tactic);
            Assert.Contains(DisclosureTactic.Decline, new[] { constrained.Tactic });

            ProhibitionRuling ruling = Assert.Single(constrained.Prohibitions);
            Assert.True(ruling.Forbids);
            Assert.Equal(PersonalProhibition.NeverLiesDirectly, ruling.Kind);
        }

        /// <summary>
        /// The line is applied at the honesty gate rather than beside it, so a speaker who was
        /// never going to falsify produces no ruling at all: there is nothing they refrained from,
        /// and reporting one would be a line taking credit for a decision it did not make.
        /// </summary>
        [Fact]
        public void ALineAgainstLyingIsSilentWhereThereWasNoLieToRefrainFrom()
        {
            Interrogation scene = Interrogation.WithAFrightenedThief();
            scene.Thief.Personality.Honesty = 0.9;
            scene.Thief.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.8);

            DisclosureDecision decision = scene.Decide();

            Assert.False(decision.WillLie);
            Assert.Empty(decision.Prohibitions);
        }

        /// <summary>
        /// And it is not a second honesty score: candour is a slope and the line is a declaration,
        /// so the same low-candour thief behaves differently depending on whether he holds it -
        /// which is precisely the character the slope alone could not express.
        /// </summary>
        [Fact]
        public void ALineIsNotASecondCandourFigure()
        {
            Interrogation withLine = Interrogation.WithAFrightenedThief();
            Interrogation without = Interrogation.WithAFrightenedThief();
            withLine.Thief.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.8);

            DisclosureDecision constrained = withLine.Decide();
            DisclosureDecision unconstrained = without.Decide();

            Assert.Equal(unconstrained.Balance, constrained.Balance);
            Assert.Equal(unconstrained.Strategy, constrained.Strategy);
            Assert.Equal(
                unconstrained.Personality(),
                constrained.Personality());
            Assert.NotEqual(unconstrained.Tactic, constrained.Tactic);
        }

        [Fact]
        public void EnoughPressureBreaksTheLineAgainstLying()
        {
            Interrogation scene = Interrogation.WithAFrightenedThief();
            scene.Thief.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.2);

            DisclosureDecision decision = scene.Decide();

            Assert.True(decision.WillLie);
            ProhibitionRuling ruling = Assert.Single(decision.Prohibitions);
            Assert.True(ruling.Broke);
            Assert.Contains("breaks the line against lie directly", ruling.Because);
            Assert.Contains("keeping quiet would itself answer", ruling.Because);
        }

        /// <summary>
        /// The other disclosure line, and the one that changes whether the claim is put forward at
        /// all rather than what is done instead. The witness would say it - loyalty to her brother
        /// is already weighed and already outweighed - and the line is what is still there
        /// afterwards.
        /// </summary>
        [Fact]
        public void ALineAgainstSpeakingBadlyOfFamilyCostsAWillingSpeakerTheClaim()
        {
            Interrogation scene = Interrogation.WithASistersTheft();

            DisclosureDecision unconstrained = scene.Decide();
            Assert.True(unconstrained.WillDisclose);

            scene.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);

            DisclosureDecision constrained = scene.Decide();

            Assert.False(constrained.WillDisclose);
            Assert.Equal(DisclosureStrategy.Refuse, constrained.Strategy);
            Assert.Equal(DisclosureDepth.Nothing, constrained.Depth);
            Assert.Equal(DisclosureTactic.Decline, constrained.Tactic);
            Assert.True(Assert.Single(constrained.Prohibitions).Forbids);
        }

        /// <summary>
        /// And it is a line about kin and about discredit, not a general reticence. Two
        /// near-misses: the same discrediting claim about somebody who is not family, and a claim
        /// about the same brother that says nothing against him.
        /// </summary>
        [Fact]
        public void TheKinLineBearsOnlyOnADiscreditingClaimAboutOnesOwnKin()
        {
            Interrogation stranger = Interrogation.WithASistersTheft(kin: false);
            stranger.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);
            DisclosureDecision aboutAStranger = stranger.Decide();

            Assert.True(aboutAStranger.WillDisclose);
            Assert.Empty(aboutAStranger.Prohibitions);

            Interrogation harmless = Interrogation.WithASistersTheft();
            harmless.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);
            DisclosureDecision aboutSomethingHarmless = harmless.DecideAboutWhereHeWas();

            Assert.True(aboutSomethingHarmless.WillDisclose);
            Assert.Empty(aboutSomethingHarmless.Prohibitions);
        }

        [Fact]
        public void EnoughPressureBreaksTheLineAgainstSpeakingBadlyOfFamily()
        {
            Interrogation scene = Interrogation.WithASistersTheft();
            scene.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.2);

            DisclosureDecision decision = scene.Decide();

            Assert.True(decision.WillDisclose);
            ProhibitionRuling ruling = Assert.Single(decision.Prohibitions);
            Assert.True(ruling.Broke);
            Assert.Contains("breaks the line against speak badly of family", ruling.Because);
        }

        /// <summary>
        /// A line is not another pressure. It never enters the balance, so an inspector reading
        /// the weighing sees the same arithmetic whether or not somebody held a line about it -
        /// and the line is reported separately, where it cannot be mistaken for a heavy weight.
        /// </summary>
        [Fact]
        public void ALineNeverEntersTheDisclosureBalance()
        {
            Interrogation without = Interrogation.WithASistersTheft();
            Interrogation with = Interrogation.WithASistersTheft();
            with.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);

            DisclosureDecision unconstrained = without.Decide();
            DisclosureDecision constrained = with.Decide();

            Assert.Equal(unconstrained.Balance, constrained.Balance);
            Assert.Equal(unconstrained.Pressures.Count, constrained.Pressures.Count);
            Assert.Empty(unconstrained.Prohibitions);
        }

        /// <summary>
        /// `Decisive` means "the pressures whose removal would have changed the answer", which is a
        /// statement about the weighing. A line that capped the strategy did not come from any
        /// pressure, so measuring against the capped value would report every pressure as decisive
        /// for a decision none of them settled.
        /// </summary>
        [Fact]
        public void ALineThatCapsTheStrategyDoesNotRewriteWhichPressuresWereDecisive()
        {
            Interrogation without = Interrogation.WithASistersTheft();
            Interrogation with = Interrogation.WithASistersTheft();
            with.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);

            DisclosureDecision unconstrained = without.Decide();
            DisclosureDecision constrained = with.Decide();

            Assert.Equal(DisclosureStrategy.Refuse, constrained.Strategy);
            Assert.Equal(
                unconstrained.Decisive.Select(p => p.Tag).ToList(),
                constrained.Decisive.Select(p => p.Tag).ToList());
            Assert.NotEqual(constrained.Pressures.Count, constrained.Decisive.Count);
        }

        [Fact]
        public void TheInspectorPrintsALineApartFromThePressures()
        {
            Interrogation scene = Interrogation.WithASistersTheft();
            scene.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);

            string dump = NarrativeInspector.DescribeDisclosure(scene.World, scene.Decide());

            Assert.Contains("lines held:", dump);
            Assert.Contains("NeverSpeaksBadlyOfFamily", dump);
            Assert.Contains("holds", dump);
        }

        [Fact]
        public void TheSameSpeakerAskedTheSameThingRulesTheSameWayEveryTime()
        {
            string Dump()
            {
                Interrogation scene = Interrogation.WithAFrightenedThief();
                scene.Thief.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.8);
                return NarrativeInspector.DescribeDisclosure(scene.World, scene.Decide());
            }

            Assert.Equal(Dump(), Dump());
        }

        // -- wording: carried out, never decided ------------------------------------------------------

        [Fact]
        public void AFragmentWithNoMannerTagIsNeverRuledOut()
        {
            DialogueFragment plain = Fragment("frag.plain");
            DialogueFragment flavoured = Fragment("frag.vocabulary", DialogueVocabulary.Craft);

            Assert.True(plain.FitsManner(new[] { DialogueManners.Pleading }));
            Assert.True(flavoured.FitsManner(new[] { DialogueManners.Pleading }));
        }

        [Fact]
        public void AFragmentInAForbiddenMannerIsRuledOutAndOtherwiseIsNot()
        {
            DialogueFragment pleading = Fragment("frag.pleading", DialogueManners.Pleading);

            Assert.False(pleading.FitsManner(new[] { DialogueManners.Pleading }));
            Assert.True(pleading.FitsManner(new string[0]));
            Assert.True(pleading.FitsManner(null));
        }

        /// <summary>
        /// The realizer honours what was decided elsewhere: the appealing modifiers a question can
        /// carry are candidates for a speaker with no line and are gone for one whose line is
        /// holding, while every other way of asking the same question survives.
        /// </summary>
        [Fact]
        public void AHoldingLineTakesItsRegisterOutOfTheFragmentPoolAndLeavesTheRest()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest question = scene.PlayerAsks();

            List<string> open = Modifiers(scene, question);
            Assert.Contains("mod.ask.kindness", open);
            Assert.Contains("mod.ask.nobody.else", open);

            question.Forbidden = NegativeSpaceVoice.ForbiddenManners(
                new[] { Holding(PersonalProhibition.NeverBegs) });

            List<string> constrained = Modifiers(scene, question);
            Assert.DoesNotContain("mod.ask.kindness", constrained);
            Assert.DoesNotContain("mod.ask.nobody.else", constrained);
            Assert.Equal(
                open.Where(id => id != "mod.ask.kindness" && id != "mod.ask.nobody.else").ToList(),
                constrained);
        }

        /// <summary>
        /// A line that gave way where the decision was taken does not go on gagging the words.
        /// Wording is handed rulings rather than a profile precisely so that this cannot go wrong.
        /// </summary>
        [Fact]
        public void ALineThatBrokeNoLongerConstrainsTheWording()
        {
            Assert.Empty(NegativeSpaceVoice.ForbiddenManners(new[] { Broken(PersonalProhibition.NeverBegs) }));
            Assert.Empty(NegativeSpaceVoice.ForbiddenManners(
                new[] { ProhibitionRuling.NotHeld(PersonalProhibition.NeverBegs) }));
            Assert.Empty(NegativeSpaceVoice.ForbiddenManners(null));
        }

        /// <summary>
        /// The invariant the step turns on, stated as a test. The witness's line against speaking
        /// badly of her brother is settled where the claim is decided, so the act that reaches
        /// wording is already a refusal: there is no rendering of this scene in which she names
        /// him and the words merely soften it.
        /// </summary>
        [Fact]
        public void RealizationNeverGetsTheChanceToBypassAnActiveDecisionConstraint()
        {
            Interrogation scene = Interrogation.WithASistersTheft();
            scene.Witness.NegativeSpace.Declare(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.8);

            DisclosureDecision decision = scene.Decide();
            SpeechAct said = Disclosure.Compose(decision, scene.Question());

            Assert.Equal(SpeechActType.Refuse, said.Type);
            Assert.False(said.Content.HasProposition);
            Assert.Empty(decision.ForbiddenManners);
        }

        [Fact]
        public void AMannerConstraintNarrowsHowSomethingIsSaidAndNeverWhatIsSaid()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest question = scene.PlayerAsks();

            for (ulong seed = 1; seed <= 12; seed++)
            {
                question.Forbidden = new string[0];
                question.Rng = new DeterministicRng(seed);
                RealizedLine open = scene.Realizer.Realize(question);

                question.Forbidden = new[] { DialogueManners.Pleading };
                question.Rng = new DeterministicRng(seed);
                RealizedLine constrained = scene.Realizer.Realize(question);

                Assert.Equal(open.Meaning, constrained.Meaning);
                Assert.True(constrained.Rendered);
                Assert.DoesNotContain("I would take it as a kindness.", constrained.Text);
                Assert.DoesNotContain("There is nobody else I can put this to.", constrained.Text);
            }
        }

        [Fact]
        public void TheSameRequestAndSeedSayTheSameWordsUnderTheSameConstraint()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest question = scene.PlayerAsks();
            question.Forbidden = new[] { DialogueManners.Pleading };

            question.Rng = new DeterministicRng(77UL);
            string first = scene.Realizer.Realize(question).Text;
            question.Rng = new DeterministicRng(77UL);
            string second = scene.Realizer.Realize(question).Text;

            Assert.Equal(first, second);
        }

        /// <summary>
        /// The manner vocabulary and BQ-076's vocabulary read the same free tags field and must not
        /// read each other's entries, or a later step would silently change what an existing
        /// fragment means.
        /// </summary>
        [Fact]
        public void TheMannerAndVocabularyTagListsAreDisjointAndNeitherClaimsTheOthers()
        {
            foreach (string manner in DialogueManners.Vocabulary)
            {
                Assert.False(DialogueVocabulary.IsVocabulary(manner));
            }

            foreach (string vocabulary in DialogueVocabulary.Vocabulary)
            {
                Assert.False(DialogueManners.IsManner(vocabulary));
            }

            DialogueFragment pleading = Fragment("frag.pleading", DialogueManners.Pleading);
            Assert.True(pleading.FitsVocabulary(new string[0]));
            Assert.True(pleading.FitsVocabulary(new[] { DialogueVocabulary.Craft }));
        }

        [Fact]
        public void EveryShippedMannerTagIsOneTheVocabularyKnows()
        {
            DialogueFragmentLibrary library = FragmentRealizationTests.Scene.Create().Realizer.Library;

            foreach (FragmentPosition position in Enum.GetValues(typeof(FragmentPosition)).Cast<FragmentPosition>())
            {
                foreach (DialogueFragment fragment in library.At(position))
                {
                    foreach (string tag in fragment.Tags)
                    {
                        Assert.True(
                            DialogueManners.IsManner(tag) || DialogueVocabulary.IsVocabulary(tag),
                            fragment.Id + " carries the unknown tag '" + tag + "'");
                    }
                }
            }
        }

        // -- persistence -------------------------------------------------------------------------------

        [Fact]
        public void LinesSurviveASaveAndAReload()
        {
            NarrativeWorldState world = new NarrativeWorldState(31UL);
            NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), "Mira");
            npc.NegativeSpace.Declare(PersonalProhibition.NeverBegs, 0.75);
            npc.NegativeSpace.Declare(PersonalProhibition.NeverLiesDirectly, 0.4, breakable: false);
            world.Registry.Add(npc);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            NegativeSpaceProfile after = reloaded.Registry.GetNpc(npc.Id).NegativeSpace;

            Assert.Equal(
                new[] { PersonalProhibition.NeverBegs, PersonalProhibition.NeverLiesDirectly },
                after.Declared);
            Assert.Equal(0.75, after.FirmnessOf(PersonalProhibition.NeverBegs));
            Assert.True(after.IsBreakable(PersonalProhibition.NeverBegs));
            Assert.Equal(0.4, after.FirmnessOf(PersonalProhibition.NeverLiesDirectly));
            Assert.False(after.IsBreakable(PersonalProhibition.NeverLiesDirectly));
        }

        [Fact]
        public void ACharacterWithNoLinesSavesAndReloadsWithNone()
        {
            NarrativeWorldState world = new NarrativeWorldState(31UL);
            NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), "Mira");
            world.Registry.Add(npc);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));

            Assert.False(reloaded.Registry.GetNpc(npc.Id).NegativeSpace.Any);
        }

        /// <summary>
        /// A save written before this step has no such field, and everybody in it holds no lines -
        /// which is the correct reading of that world rather than a placeholder.
        /// </summary>
        [Fact]
        public void ASaveFromBeforeThisStepMigratesToNobodyHoldingAnything()
        {
            NarrativeWorldState world = new NarrativeWorldState(31UL);
            NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), "Mira");
            npc.NegativeSpace.Declare(PersonalProhibition.NeverBegs, 0.75);
            world.Registry.Add(npc);

            // The field is dropped textually rather than by rebuilding the document: the point of
            // the test is a save that never had the node, and reconstructing one by hand would be
            // testing the fixture instead of the migration.
            string aged = WorldStateSerializer.Save(world)
                .Replace("\"schemaVersion\": 10", "\"schemaVersion\": 9");
            aged = WithoutNegativeSpace(aged);
            Assert.DoesNotContain("negativeSpace", aged);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(aged);

            Assert.Equal(NarrativeWorldState.CurrentSchemaVersion, reloaded.SchemaVersion);
            Assert.False(reloaded.Registry.GetNpc(npc.Id).NegativeSpace.Any);
        }

        // -- fixtures ------------------------------------------------------------------------------------

        /// <summary>
        /// Somebody whose own preferences make reporting a loss to the guards clearly the best
        /// answer. Nothing about her is derived from a role: the preference and the orderliness are
        /// set directly, which is exactly how a prohibition is set too.
        /// </summary>
        private static NarrativeNpc Reeve()
        {
            NarrativeNpc reeve = new NarrativeNpc(EntityId.Parse("npc_reeve"), "Reeve");
            reeve.ProblemSolving.AskAuthority = 1.0;
            reeve.Personality.Orderliness = 1.0;
            return reeve;
        }

        /// <summary>
        /// The saved document with every <c>negativeSpace</c> member cut out, as a save written
        /// before this step actually looks.
        /// </summary>
        private static string WithoutNegativeSpace(string json)
        {
            while (true)
            {
                int start = json.IndexOf("\"negativeSpace\"", StringComparison.Ordinal);
                if (start < 0)
                {
                    return json;
                }

                int close = json.IndexOf(']', start);
                int end = close + 1;
                if (end < json.Length && json[end] == ',')
                {
                    end++;
                }

                json = json.Remove(start, end - start);
            }
        }

        private static GoalActionTrace Candidate(GoalFormationTrace trace, ProblemSolvingStyle style)
        {
            return trace.CandidateActions.Single(action => action.Style == style);
        }

        private static ProhibitionRuling Holding(PersonalProhibition kind)
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(kind, 1.0, breakable: false);
            return profile.Rule(kind, 1.0, "nothing that could move it");
        }

        private static ProhibitionRuling Broken(PersonalProhibition kind)
        {
            NegativeSpaceProfile profile = new NegativeSpaceProfile();
            profile.Declare(kind, 0.0);
            return profile.Rule(kind, 1.0, "everything at stake");
        }

        private static DialogueFragment Fragment(string id, params string[] tags)
        {
            return new DialogueFragment(
                id,
                FragmentPosition.Modifier,
                "Something.",
                null,
                null,
                null,
                tags,
                string.Empty,
                null);
        }

        private static List<string> Modifiers(FragmentRealizationTests.Scene scene, RealizationRequest request)
        {
            return scene.Realizer.Candidates(FragmentPosition.Modifier, request).Select(f => f.Id).ToList();
        }

        /// <summary>
        /// One theft, and the two speakers BQ-071 through BQ-073 already use to prove their own
        /// decisions: a thief frightened enough to deny it, and a witness willing to say what she
        /// saw. Built here rather than borrowed so the relationships this step needs - a tie to the
        /// person the claim is about - can be set without disturbing the scene those steps pin.
        /// </summary>
        private sealed class Interrogation
        {
            private Interrogation(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal NarrativeNpc Thief { get; private set; }

            internal NarrativeNpc Witness { get; private set; }

            internal EntityId Player { get; private set; }

            internal EntityId TheftFact { get; private set; }

            internal EntityId Whereabouts { get; private set; }

            private EntityId Speaker { get; set; }

            internal static Interrogation WithAFrightenedThief()
            {
                Interrogation scene = Build();
                scene.World.Knowledge.Teach(
                    scene.Thief.Id, scene.TheftFact, KnowledgeSource.Participant, 1.0, GameTime.Zero, false);
                scene.Thief.Personality.Honesty = 0.1;
                scene.Thief.Emotions.Set(EmotionalState.Fear, 0.8);
                scene.Speaker = scene.Thief.Id;
                return scene;
            }

            internal static Interrogation WithASistersTheft(bool kin = true)
            {
                Interrogation scene = Build();
                scene.World.Knowledge.Teach(
                    scene.Witness.Id, scene.TheftFact, KnowledgeSource.Witnessed, 0.9, GameTime.Zero, false);
                scene.World.Knowledge.Teach(
                    scene.Witness.Id, scene.Whereabouts, KnowledgeSource.Witnessed, 0.9, GameTime.Zero, false);
                scene.Witness.Personality.Honesty = 0.9;
                scene.Witness.Emotions.Set(EmotionalState.Fear, 0.0);
                scene.World.Relationships.Connect(scene.Witness.Id, scene.Player, RelationKind.Friend, 70);
                scene.World.Relationships.Connect(
                    scene.Witness.Id,
                    scene.Thief.Id,
                    kin ? RelationKind.Family : RelationKind.Acquaintance,
                    40);
                scene.Speaker = scene.Witness.Id;
                return scene;
            }

            internal DisclosureDecision Decide()
            {
                return Disclosure.Decide(World, Speaker, Player, TheftFact, GameTime.Zero);
            }

            /// <summary>The same speaker, the same brother, a claim that says nothing against him.</summary>
            internal DisclosureDecision DecideAboutWhereHeWas()
            {
                return Disclosure.Decide(World, Speaker, Player, Whereabouts, GameTime.Zero);
            }

            internal SpeechAct Question()
            {
                return SpeechAct.Compose(
                    SpeechActType.Ask,
                    Player,
                    Speaker,
                    new BrilliantQuesting.Actions.ActionBinding { PropositionFact = TheftFact });
            }

            private static Interrogation Build()
            {
                Interrogation scene = new Interrogation(new NarrativeWorldState(20260903UL));
                scene.Thief = scene.Person("Kip");
                scene.Witness = scene.Person("Mira");
                scene.Player = scene.Person("Wren").Id;

                Fact theft = new Fact(
                    scene.World.NewId("fact"),
                    scene.Thief.Id,
                    FactPredicates.Stole,
                    EntityId.None,
                    "silver ring",
                    TruthState.True,
                    secrecy: 40);
                scene.World.Knowledge.AddFact(theft);
                scene.TheftFact = theft.Id;

                Fact market = new Fact(
                    scene.World.NewId("fact"),
                    scene.Thief.Id,
                    FactPredicates.LocatedAt,
                    EntityId.None,
                    "the north market",
                    TruthState.True,
                    secrecy: 0);
                scene.World.Knowledge.AddFact(market);
                scene.Whereabouts = market.Id;
                return scene;
            }

            private NarrativeNpc Person(string name)
            {
                NarrativeNpc npc = new NarrativeNpc(World.NewId("npc"), name);
                World.Registry.Add(npc);
                return npc;
            }
        }
    }

    internal static class DisclosureDecisionTestExtensions
    {
        /// <summary>
        /// The part of a decision that came from who the speaker is rather than from what they
        /// decided, as a string, so a test can assert that a line changed the outcome without
        /// changing the reading it was taken from.
        /// </summary>
        internal static string Personality(this DisclosureDecision decision)
        {
            return decision.Strategy + "/" + decision.Depth + "/" + decision.Limit + "/"
                + decision.Balance.ToString("0.0000");
        }
    }
}
