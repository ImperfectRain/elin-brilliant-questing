using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-071. Knowing a thing and being willing to say it are two different questions.
    ///
    /// Without the second one, every secret in the world is one conversation away from being
    /// spent: the only thing anybody ever asks is whether the character knows, and a character who
    /// knows always tells. The step's claim is that disclosure is a decision a person makes about
    /// a person, from state the world already holds, and that it is explainable afterwards.
    ///
    /// So this file guards four things, and three of them are boundaries:
    /// the same knowledge produces different answers under different pressure; nothing unbelieved
    /// is ever disclosed, however plausible it would be for this character to know it; withholding
    /// never becomes a falsehood, because lying is BQ-073's; and hedging is a weaker commitment
    /// rather than a smaller part of the fact, because graduated depth is BQ-072's.
    /// </summary>
    public class DisclosureDecisionTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// The step's condition: the same NPC answers directly, hedges, deflects and refuses
        /// across four relationship levels.
        ///
        /// One witness, one belief, one question, one moment - only the tie to whoever is asking
        /// changes, and the answer walks the whole ladder. That the belief is untouched between
        /// the four is asserted rather than assumed: the thing being demonstrated is that
        /// willingness moved, not that knowledge did.
        /// </summary>
        [Fact]
        public void TheSameWitnessAnswersHedgesDeflectsAndRefusesAcrossFourRelationshipLevels()
        {
            Town town = Town.Create();

            DisclosureDecision toAFriend = town.AskAs(RelationKind.Friend, 70);
            DisclosureDecision toAnAcquaintance = town.AskAs(RelationKind.Acquaintance, 40);
            DisclosureDecision toARival = town.AskAs(RelationKind.Rival, -20);
            DisclosureDecision toAnEnemy = town.AskAs(RelationKind.Enemy, -70);

            Assert.Equal(DisclosureStrategy.Disclose, toAFriend.Strategy);
            Assert.Equal(DisclosureStrategy.Hedge, toAnAcquaintance.Strategy);
            Assert.Equal(DisclosureStrategy.Deflect, toARival.Strategy);
            Assert.Equal(DisclosureStrategy.Refuse, toAnEnemy.Strategy);

            // The ladder is ordered, and downstream steps are entitled to rely on that.
            Assert.True(toAFriend.Strategy > toAnAcquaintance.Strategy);
            Assert.True(toAnAcquaintance.Strategy > toARival.Strategy);
            Assert.True(toARival.Strategy > toAnEnemy.Strategy);

            // Two of the four say the claim; two do not.
            Assert.True(toAFriend.WillDisclose && toAFriend.Committed);
            Assert.True(toAnAcquaintance.WillDisclose && !toAnAcquaintance.Committed);
            Assert.False(toARival.WillDisclose);
            Assert.False(toAnEnemy.WillDisclose);

            // And the witness knew exactly the same thing throughout.
            Assert.True(town.World.Knowledge.TryGetBelief(town.Witness, town.TheftFact, out KnowledgeRecord belief));
            Assert.Equal(KnowledgeSource.Witnessed, belief.Source);
            Assert.Equal(0.9, belief.Confidence, 3);
        }

        /// <summary>
        /// The other half of the condition: every one of the four is explainable, and the
        /// explanation names the pressures that settled it rather than a number.
        /// </summary>
        [Fact]
        public void TheInspectorNamesTheDecisivePressures()
        {
            Town town = Town.Create();
            DisclosureDecision refused = town.AskAs(RelationKind.Enemy, -70);

            string log = NarrativeInspector.DescribeDisclosure(town.World, refused);

            Assert.Contains("disclosure: Refuse", log);
            Assert.Contains("discloses:   no", log);
            Assert.Contains(town.World.Registry.NameOf(town.Witness), log);
            Assert.Contains(town.TheftFact.Value, log);
            Assert.Contains("belief:      Witnessed at 0.90", log);

            // Every pressure that applied, with its direction and the state behind it.
            Assert.Contains(DisclosurePressures.Relationship, log);
            Assert.Contains(DisclosurePressures.Privacy, log);
            Assert.Contains(DisclosurePressures.Fear, log);
            Assert.Contains(DisclosurePressures.Confidence, log);
            Assert.Contains("against", log);
            Assert.Contains("toward", log);

            // And which of them decided it. An enemy asking is the one pressure whose removal
            // would have changed the answer, so it is the one named.
            Assert.Contains("decisive:", log);
            Assert.Contains(DisclosurePressures.Relationship, refused.Decisive.Select(p => p.Tag));
            Assert.Contains("wording:     none", log);
        }

        /// <summary>
        /// Decisive means what it says: a pressure is named only when taking it away would have
        /// produced a different strategy. Asserted by doing exactly that.
        /// </summary>
        [Fact]
        public void ADecisivePressureIsOneWhoseRemovalWouldChangeTheAnswer()
        {
            Town town = Town.Create();
            DisclosureDecision refused = town.AskAs(RelationKind.Enemy, -70);

            Assert.NotEmpty(refused.Decisive);
            foreach (DisclosurePressure pressure in refused.Decisive)
            {
                Assert.Contains(pressure.Tag, refused.Pressures.Select(p => p.Tag));
                Assert.NotEqual(0.0, pressure.Weight);
                Assert.NotEqual(string.Empty, pressure.Because);
            }

            // Remove the enemy tie itself and the same witness answers, from the same belief.
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Acquaintance, 40);
            DisclosureDecision afterwards = town.Ask();
            Assert.NotEqual(refused.Strategy, afterwards.Strategy);
        }

        // -- the same knowledge, different pressure -------------------------------------------------

        /// <summary>
        /// The claim of the step, isolated: two witnesses who believe the identical thing to the
        /// identical degree from the identical source, asked by the identical person, answer
        /// differently - because one is furious with the thief and the other is his sister.
        ///
        /// Nothing about the knowledge differs. What differs is loyalty and grievance, both read
        /// from state the world was already holding for its own reasons.
        /// </summary>
        [Fact]
        public void TheSameBeliefUnderOppositePressuresProducesOppositeAnswers()
        {
            Town furious = Town.Create();
            furious.World.Relationships.Connect(furious.Witness, furious.Thief, RelationKind.Enemy, -80);
            furious.Npc(furious.Witness).Emotions.Affect(EmotionalState.Anger, 0.8, furious.Now);
            furious.Calm();

            Town protective = Town.Create();
            protective.World.Relationships.Connect(protective.Witness, protective.Thief, RelationKind.Family, 80);
            protective.Calm();

            DisclosureDecision spoken = furious.AskAs(RelationKind.Acquaintance, 40);
            DisclosureDecision kept = protective.AskAs(RelationKind.Acquaintance, 40);

            // Identical knowledge.
            Assert.True(furious.World.Knowledge.TryGetBelief(furious.Witness, furious.TheftFact, out KnowledgeRecord a));
            Assert.True(protective.World.Knowledge.TryGetBelief(protective.Witness, protective.TheftFact, out KnowledgeRecord b));
            Assert.Equal(a.Source, b.Source);
            Assert.Equal(a.Confidence, b.Confidence, 6);

            // Opposite answers.
            Assert.True(spoken.WillDisclose);
            Assert.False(kept.WillDisclose);
            Assert.Contains(DisclosurePressures.Grievance, spoken.Pressures.Select(p => p.Tag));
            Assert.Contains(DisclosurePressures.Loyalty, kept.Pressures.Select(p => p.Tag));
        }

        /// <summary>
        /// BQ-063's condition, arriving in dialogue: the same NPC answers the same question
        /// differently when frightened, and returns to baseline over time.
        ///
        /// Fear is read through the emotional profile at the current moment rather than copied, so
        /// the decay that makes emotion transient is the same decay that makes the answer come
        /// back - no disclosure state had to be reset for it.
        /// </summary>
        [Fact]
        public void FrightIsEnoughToChangeTheAnswerAndItWearsOff()
        {
            Town town = Town.Create();
            town.Calm();
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Friend, 70);

            Assert.Equal(DisclosureStrategy.Disclose, town.Ask().Strategy);

            town.Npc(town.Witness).Emotions.Affect(EmotionalState.Fear, 1.0, town.Now);
            DisclosureDecision frightened = town.Ask();
            Assert.False(frightened.WillDisclose);
            Assert.Contains(DisclosurePressures.Fear, frightened.Decisive.Select(p => p.Tag));

            GameTime later = town.Now.PlusHours(12);
            Assert.Equal(DisclosureStrategy.Disclose, town.Ask(later).Strategy);
        }

        /// <summary>
        /// The one pressure that should outweigh a warm tie: a thief asked about his own theft.
        ///
        /// It is also the case that most tempts a disclosure layer into inventing a lie, so what
        /// is asserted is that he declines and that nothing untrue enters the world.
        /// </summary>
        [Fact]
        public void SomebodyAskedAboutTheirOwnCrimeDeclinesEvenToAFriend()
        {
            Town town = Town.Create();
            town.World.Knowledge.Teach(town.Thief, town.TheftFact, KnowledgeSource.Participant, 1.0, town.Now, canProve: false);
            town.World.Relationships.Connect(town.Thief, town.Asker, RelationKind.Friend, 70);

            DisclosureDecision decision = Disclosure.Decide(town.World, town.Thief, town.Asker, town.TheftFact, town.Now);

            Assert.False(decision.WillDisclose);
            Assert.Contains(DisclosurePressures.LegalRisk, decision.Decisive.Select(p => p.Tag));
            Assert.True(decision.Pressures.Single(p => p.Tag == DisclosurePressures.LegalRisk).Weight < 0.0);
        }

        /// <summary>
        /// Leverage: something held over the claim's subject is only worth holding while it is
        /// quiet, and the world already records it as an obligation rather than as a blackmail
        /// flag.
        /// </summary>
        [Fact]
        public void SomethingHeldOverThePersonIsAReasonToKeepQuiet()
        {
            Town town = Town.Create();
            town.Calm();
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Acquaintance, 40);

            DisclosureDecision before = town.Ask();

            town.World.Obligations.Add(new SocialObligation(
                town.World.NewId("obligation"),
                SocialObligationKind.Debt,
                town.Thief,
                town.Witness,
                town.TheftFact,
                "the price of her silence",
                town.Now,
                EntityId.None,
                strength: 80));

            DisclosureDecision after = town.Ask();

            Assert.True(before.Strategy > after.Strategy);
            Assert.Contains(DisclosurePressures.Leverage, after.Pressures.Select(p => p.Tag));
            Assert.True(after.Pressures.Single(p => p.Tag == DisclosurePressures.Leverage).Weight < 0.0);
        }

        // -- nothing unbelieved is ever disclosed ---------------------------------------------------

        /// <summary>
        /// The gate the whole file rests on. Somebody who does not hold a belief has nothing to
        /// disclose, no pressure is weighed at all, and no act comes out - which is a different
        /// state from refusing, and is reported as one.
        /// </summary>
        [Fact]
        public void SomebodyWhoDoesNotBelieveItHasNothingToDiscloseRatherThanSomethingToRefuse()
        {
            Town town = Town.Create();
            town.World.Relationships.Connect(town.Bystander, town.Asker, RelationKind.Friend, 90);

            DisclosureDecision decision = Disclosure.Decide(town.World, town.Bystander, town.Asker, town.TheftFact, town.Now);

            Assert.Equal(DisclosureStrategy.NothingToDisclose, decision.Strategy);
            Assert.NotEqual(DisclosureStrategy.Refuse, decision.Strategy);
            Assert.False(decision.WillDisclose);
            Assert.Empty(decision.Pressures);
            Assert.Empty(decision.Decisive);
            Assert.NotEqual(string.Empty, decision.Note);
            Assert.Null(Disclosure.Compose(decision, town.Question(town.Bystander)));
        }

        /// <summary>
        /// Identity is not knowledge, and this is where getting that wrong would be worst.
        ///
        /// A watchman plausibly knows about a theft in his own town - that is exactly what
        /// <c>IdentityAffordances</c> says, and it is a casting and interpretation input. It is not
        /// a belief, so he still has nothing to disclose, and asking him leaves the graph as empty
        /// as it found it. A disclosure layer that filled the gap would be inventing a fact at the
        /// moment the player was told they were learning one.
        /// </summary>
        [Fact]
        public void PlausibleKnowledgeFromIdentityIsStillNotSomethingToDisclose()
        {
            Town town = Town.Create();
            SandboxVanillaState vanilla = new SandboxVanillaState(town.Asker);
            vanilla.Define(town.Bystander);
            vanilla.SetCharacterIdentity(town.Bystander, new CharacterIdentityBuilder(town.Bystander)
                .WithHobbiesRead()
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build());

            IdentityAffordances affordances = IdentityAffordances.Of(town.Npc(town.Bystander), vanilla);
            Assert.True(affordances.PlausibleKnowledgeOf(IdentityDomain.PublicOrder) > 0.0);

            int beliefsBefore = town.World.Knowledge.BeliefsOf(town.Bystander).Count();
            int factsBefore = town.World.Knowledge.Facts.Count;

            DisclosureDecision decision = Disclosure.Decide(town.World, town.Bystander, town.Asker, town.TheftFact, town.Now);

            Assert.Equal(DisclosureStrategy.NothingToDisclose, decision.Strategy);
            Assert.Equal(beliefsBefore, town.World.Knowledge.BeliefsOf(town.Bystander).Count());
            Assert.Equal(factsBefore, town.World.Knowledge.Facts.Count);
        }

        /// <summary>Deciding is a reading. It teaches nobody anything and records nothing.</summary>
        [Fact]
        public void DecidingWritesNothing()
        {
            Town town = Town.Create();
            int facts = town.World.Knowledge.Facts.Count;
            int beliefs = town.World.Knowledge.BeliefsOf(town.Witness).Count();
            int events = town.World.Ledger.Events.Count;

            town.AskAs(RelationKind.Friend, 70);
            town.AskAs(RelationKind.Enemy, -70);

            Assert.Equal(facts, town.World.Knowledge.Facts.Count);
            Assert.Equal(beliefs, town.World.Knowledge.BeliefsOf(town.Witness).Count());
            Assert.Equal(events, town.World.Ledger.Events.Count);
        }

        // -- a refusal is not a lie -----------------------------------------------------------------

        /// <summary>
        /// The boundary onto BQ-073, structurally: there is no way to express a falsehood here.
        /// The ladder has four rungs and none of them is a lie or an evasion, so nothing can drift
        /// into asserting one while BQ-073 is still unbuilt.
        /// </summary>
        [Fact]
        public void TheLadderHasNoRungForLyingOrEvading()
        {
            string[] strategies = Enum.GetNames(typeof(DisclosureStrategy));

            Assert.DoesNotContain("Lie", strategies);
            Assert.DoesNotContain("Evade", strategies);
            Assert.DoesNotContain("Deceive", strategies);
            Assert.Equal(
                new[] { "NothingToDisclose", "Refuse", "Deflect", "Hedge", "Disclose" }.OrderBy(n => n),
                strategies.OrderBy(n => n));
        }

        /// <summary>
        /// And behaviourally: an unwilling speaker asserts nothing, whichever way they decline.
        ///
        /// BQ-071 left the deflection composing to no act at all, because the vocabulary had
        /// nothing for it and reaching for <c>Refuse</c> would have deleted the difference between
        /// letting a question go and turning it down. BQ-073 makes the call it was left: the
        /// deflection is an <see cref="SpeechActType.Evade"/>, which is a different act from a
        /// refusal and still carries no claim either way.
        ///
        /// The property this test actually guards is unchanged and is the one that matters here -
        /// neither act puts a proposition forward, so neither can be read as having said anything
        /// about the theft.
        /// </summary>
        [Fact]
        public void RefusingAndDeflectingBothAssertNothingAndAreNotTheSameAct()
        {
            Town town = Town.Create();
            SpeechAct question = town.Question(town.Witness);

            DisclosureDecision refused = town.AskAs(RelationKind.Enemy, -70);
            SpeechAct refusal = Disclosure.Compose(refused, question);

            Assert.NotNull(refusal);
            Assert.Equal(SpeechActType.Refuse, refusal.Type);
            Assert.Equal(SpeechActStance.None, refusal.Stance);
            Assert.True(refusal.About.IsNone);
            Assert.False(refusal.Content.HasProposition);

            DisclosureDecision deflected = town.AskAs(RelationKind.Rival, -20);
            SpeechAct evasion = Disclosure.Compose(deflected, question);

            Assert.NotNull(evasion);
            Assert.Equal(SpeechActType.Evade, evasion.Type);
            Assert.Equal(SpeechActStance.None, evasion.Stance);
            Assert.True(evasion.About.IsNone);
            Assert.False(evasion.Content.HasProposition);

            Assert.NotEqual(refusal.Type, evasion.Type);
            Assert.NotEqual(refusal.Signature, evasion.Signature);
        }

        /// <summary>
        /// What a willing speaker says is the claim they hold, and nothing else. The act names the
        /// fact from the decision, which is the fact that was asked about, which is the fact they
        /// believe - so no third claim can appear between the question and the answer.
        /// </summary>
        [Fact]
        public void AnsweringPutsForwardTheClaimTheSpeakerActuallyHolds()
        {
            Town town = Town.Create();
            SpeechAct question = town.Question(town.Witness);

            DisclosureDecision decision = town.AskAs(RelationKind.Friend, 70);
            SpeechAct answer = Disclosure.Compose(decision, question);

            Assert.NotNull(answer);
            Assert.Equal(SpeechActType.Answer, answer.Type);
            Assert.Equal(SpeechActStance.Affirms, answer.Stance);
            Assert.Equal(town.TheftFact, answer.About);
            Assert.Equal(town.Witness, answer.Speaker);
            Assert.Equal(question, answer.InReplyTo);
            Assert.True(town.World.Knowledge.Knows(answer.Speaker, answer.About));
        }

        /// <summary>
        /// A hedge is a weaker commitment to the whole claim, not a smaller piece of it - the line
        /// BQ-072 then built on rather than blurred.
        ///
        /// The hedged act names the same fact the confident one does, and the two are the same act
        /// on the wire. How much of that fact comes out is the separate axis BQ-072 added, and it
        /// is asserted here to be separate: at one tie, both answers reveal the same amount, so
        /// nothing about hedging is a smaller disclosure.
        /// </summary>
        [Fact]
        public void HedgingIsLessCommitmentRatherThanLessOfTheFact()
        {
            Town town = Town.Create();
            SpeechAct question = town.Question(town.Witness);

            SpeechAct hedged = Disclosure.Compose(town.AskAs(RelationKind.Acquaintance, 40), question);
            SpeechAct committed = Disclosure.Compose(town.AskAs(RelationKind.Friend, 70), question);

            Assert.NotNull(hedged);
            Assert.NotNull(committed);
            Assert.Equal(committed.About, hedged.About);
            Assert.Equal(committed.Signature, hedged.Signature);

            // Depth is BQ-072's separate axis, and it is not what separates these two: a hedge
            // still puts the whole claim forward, and how much comes with it is decided by the
            // tie rather than by how firmly the claim is said.
            DisclosureDecision hedging = town.AskAs(RelationKind.Acquaintance, 40);

            Assert.Equal(DisclosureStrategy.Hedge, hedging.Strategy);
            Assert.True(hedging.WillDisclose);
            Assert.True(hedging.Reaches(DisclosureDepth.Gist));
        }

        // -- a character decision, not a difficulty check --------------------------------------------

        /// <summary>
        /// No dice anywhere. The decision layer cannot see a resolver, a check or an action
        /// context, which is what stops "will she tell me?" from quietly becoming a persuasion
        /// roll with extra steps. Persuasion stays the action layer's, and may change the state
        /// this reads - never this reading of it.
        /// </summary>
        [Fact]
        public void DisclosureCannotReachACheckOrARoll()
        {
            Type[] forbidden = typeof(Disclosure).Assembly.GetTypes()
                .Where(t => t.Namespace == "BrilliantQuesting.Checks")
                .Concat(new[] { typeof(ActionContext), typeof(DeterministicRng) })
                .ToArray();

            foreach (MethodInfo method in typeof(Disclosure).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                Assert.DoesNotContain(method.ReturnType, forbidden);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.DoesNotContain(parameter.ParameterType, forbidden);
                }
            }
        }

        /// <summary>The same state asked the same question answers the same way, every time.</summary>
        [Fact]
        public void TheSameStateAlwaysGivesTheSameAnswer()
        {
            Town town = Town.Create();
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Acquaintance, 40);

            DisclosureDecision first = town.Ask();
            for (int i = 0; i < 5; i++)
            {
                DisclosureDecision again = town.Ask();
                Assert.Equal(first.Strategy, again.Strategy);
                Assert.Equal(first.Balance, again.Balance, 9);
                Assert.Equal(
                    first.Pressures.Select(p => p.Tag + p.Weight.ToString("0.000000")),
                    again.Pressures.Select(p => p.Tag + p.Weight.ToString("0.000000")));
            }
        }

        /// <summary>
        /// A question is the natural way in, and taking the three participants off it is what
        /// stops a caller pairing the wrong speaker with the wrong claim by hand. Anything that is
        /// not a question put to this person about a claim is not a disclosure decision.
        /// </summary>
        [Fact]
        public void ADecisionCanBeTakenStraightFromTheQuestion()
        {
            Town town = Town.Create();
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Friend, 70);
            SpeechAct question = town.Question(town.Witness);

            DisclosureDecision fromQuestion = Disclosure.Decide(town.World, question, town.Witness, town.Now);
            DisclosureDecision byHand = town.Ask();

            Assert.NotNull(fromQuestion);
            Assert.Equal(byHand.Strategy, fromQuestion.Strategy);
            Assert.Equal(town.Asker, fromQuestion.Asker);
            Assert.Equal(town.TheftFact, fromQuestion.FactId);

            // Not a question, or not put to this person: no decision to take.
            Assert.Null(Disclosure.Decide(town.World, question, town.Bystander, town.Now));
            Assert.Null(Disclosure.Decide(town.World, null, town.Witness, town.Now));
        }

        /// <summary>Nobody discloses anything to themself, and a missing participant decides nothing.</summary>
        [Fact]
        public void DegenerateAsksAreRefusedRatherThanAnswered()
        {
            Town town = Town.Create();

            Assert.Equal(
                DisclosureStrategy.NothingToDisclose,
                Disclosure.Decide(town.World, town.Witness, town.Witness, town.TheftFact, town.Now).Strategy);
            Assert.Equal(
                DisclosureStrategy.NothingToDisclose,
                Disclosure.Decide(town.World, town.Witness, EntityId.None, town.TheftFact, town.Now).Strategy);
            Assert.Equal(
                DisclosureStrategy.NothingToDisclose,
                Disclosure.Decide(town.World, town.Witness, town.Asker, EntityId.None, town.Now).Strategy);
            Assert.Equal("no disclosure decision.\n", NarrativeInspector.DescribeDisclosure(town.World, null));
        }

        // -- fixture ---------------------------------------------------------------------------------

        /// <summary>
        /// Four people and one theft: a witness who saw it, the thief, somebody doing the asking,
        /// and a bystander who knows nothing.
        ///
        /// Deliberately smaller than <c>TheftLaboratory</c>. What is under test is a decision over
        /// belief, relationship, emotion and value state, and a fixture that also staged actions,
        /// threads and rumours would make it hard to say which of those moved an answer.
        /// </summary>
        private sealed class Town
        {
            private Town(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal EntityId Witness { get; private set; }

            internal EntityId Thief { get; private set; }

            internal EntityId Asker { get; private set; }

            internal EntityId Bystander { get; private set; }

            internal EntityId TheftFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Town Create()
            {
                Town town = new Town(new NarrativeWorldState(20260902UL));
                town.Witness = town.Person("Mira");
                town.Thief = town.Person("Kip");
                town.Asker = town.Person("You");
                town.Bystander = town.Person("Tovar");

                // Kept but not buried, so that a warm tie can still get it out of her - which is
                // what makes the four levels a ladder rather than a switch.
                Fact theft = new Fact(
                    town.World.NewId("fact"),
                    town.Thief,
                    FactPredicates.Stole,
                    EntityId.None,
                    "silver ring",
                    TruthState.True,
                    secrecy: 40);
                town.World.Knowledge.AddFact(theft);
                town.TheftFact = theft.Id;

                town.World.Knowledge.Teach(
                    town.Witness, theft.Id, KnowledgeSource.Witnessed, 0.9, town.Now, canProve: false);

                // Somebody uneasy about what she saw, which is the ordinary state of a witness and
                // not a special case set up to force a result.
                town.Npc(town.Witness).Emotions.Affect(EmotionalState.Fear, 0.3, town.Now);
                return town;
            }

            internal NarrativeNpc Npc(EntityId id) => World.Registry.GetNpc(id);

            /// <summary>Drops the witness's unease, for the tests that are about something else.</summary>
            internal void Calm()
            {
                Npc(Witness).Emotions.Set(EmotionalState.Fear, 0.0);
            }

            internal DisclosureDecision Ask() => Ask(Now);

            internal DisclosureDecision Ask(GameTime when)
            {
                return Disclosure.Decide(World, Witness, Asker, TheftFact, when);
            }

            internal DisclosureDecision AskAs(RelationKind kind, int sentiment)
            {
                World.Relationships.Connect(Witness, Asker, kind, sentiment);
                return Ask();
            }

            /// <summary>The question itself, so an answer has something to be an answer to.</summary>
            internal SpeechAct Question(EntityId asked)
            {
                return SpeechAct.Compose(
                    SpeechActType.Ask,
                    Asker,
                    asked,
                    new ActionBinding { PropositionFact = TheftFact });
            }

            private EntityId Person(string name)
            {
                EntityId id = World.NewId("npc");
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }
        }
    }
}
