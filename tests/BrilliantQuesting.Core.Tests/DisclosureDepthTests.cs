using System;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-072. Willingness is not the whole answer: how much of what somebody holds comes out
    /// depends on what they are to the person asking.
    ///
    /// BQ-071 established that knowing and telling are different questions. Left there, a
    /// relationship only decides whether a fact is spent, and every informant in the world is a
    /// switch: the stranger gets nothing and the friend gets everything. This step adds the second
    /// axis - the same true claim comes out as a bare answer, as its particulars, or as the part
    /// that did not go into the official account - and stages it on the relationship.
    ///
    /// So this file guards the staging and three boundaries: depth never exceeds what the speaker
    /// actually holds; a warm tie does not buy its way past a fear, a loyalty or a privacy; and a
    /// shallower answer is a smaller true answer rather than a shaded one, because falsehood is
    /// still BQ-073's.
    /// </summary>
    public class DisclosureDepthTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// The step's condition: raising affinity on one NPC unlocks strictly more of one fact, in
        /// stages.
        ///
        /// One witness, one belief, one question, one moment. Only the sentiment on the tie moves,
        /// and the answer climbs the depth ladder a rung at a time - and the belief behind it is
        /// asserted unchanged at every stage, because what is being shown is that a relationship
        /// bought more of a fact, not that the witness learned anything.
        /// </summary>
        [Fact]
        public void RaisingAffinityUnlocksStrictlyMoreOfOneFactInStages()
        {
            Town town = Town.Create();
            town.Calm();

            DisclosureDecision distant = town.AskAs(RelationKind.Friend, 10);
            DisclosureDecision closer = town.AskAs(RelationKind.Friend, 60);
            DisclosureDecision trusted = town.AskAs(RelationKind.Friend, 95);

            Assert.Equal(DisclosureDepth.Gist, distant.Depth);
            Assert.Equal(DisclosureDepth.Detail, closer.Depth);
            Assert.Equal(DisclosureDepth.InConfidence, trusted.Depth);

            // Stages, and strictly so: the ladder is ordered and every step up is a step.
            Assert.True(distant.Depth < closer.Depth);
            Assert.True(closer.Depth < trusted.Depth);
            Assert.True(trusted.Reaches(DisclosureDepth.Detail));
            Assert.False(distant.Reaches(DisclosureDepth.Detail));

            // What moved was the tie. The knowledge is the same knowledge throughout, and the
            // shallow answers were held back by the relationship rather than by anything else.
            Assert.True(town.World.Knowledge.TryGetBelief(town.Witness, town.TheftFact, out KnowledgeRecord belief));
            Assert.Equal(0.9, belief.Confidence, 9);
            Assert.Equal(DisclosureDepth.InConfidence, distant.KnownDepth);
            Assert.Equal(DisclosureDepth.InConfidence, closer.KnownDepth);
            Assert.Equal(DisclosureLimit.Standing, distant.Limit);
            Assert.Equal(DisclosureLimit.Standing, closer.Limit);
            Assert.Equal(DisclosureLimit.None, trusted.Limit);
        }

        // -- depth never exceeds knowledge ------------------------------------------------------------

        /// <summary>
        /// The hard cap. A claim with no particulars in it has no second rung to unlock, however
        /// close the two of them are: a spouse who would tell this person anything still only has
        /// the bare fact to tell.
        /// </summary>
        [Fact]
        public void ABareClaimStaysBareHoweverDeepTheTieIs()
        {
            Town town = Town.Create();
            town.Calm();
            town.World.Relationships.ConnectMutual(town.Witness, town.Asker, RelationKind.Spouse, 100);

            DisclosureDecision decision = Disclosure.Decide(town.World, town.Witness, town.Asker, town.RumourFact, town.Now);

            Assert.True(decision.WillDisclose);
            Assert.Equal(DisclosureDepth.Gist, decision.Depth);
            Assert.Equal(DisclosureDepth.Gist, decision.KnownDepth);
            Assert.Equal(DisclosureLimit.Knowledge, decision.Limit);
            Assert.Equal(DisclosureDepth.InConfidence, decision.StandingDepth);
        }

        /// <summary>
        /// And the softer half of the same cap: somebody who holds the particulars but cannot say
        /// how they came by them - hearsay from nobody in particular, nothing to show for it -
        /// gives the particulars and stops, because there is no provenance in their head to give.
        ///
        /// The test that would fail if depth were a reward for affection rather than a reading of
        /// knowledge: the tie here reaches the top rung and the answer does not.
        /// </summary>
        [Fact]
        public void ProvenanceCannotBeGivenBySomebodyWhoHasNone()
        {
            Town town = Town.Create();
            town.Calm();
            town.World.Relationships.ConnectMutual(town.Witness, town.Asker, RelationKind.Spouse, 100);

            // The same fact, held the other way: a story that reached her, from nobody she can
            // name, with nothing to produce.
            town.World.Knowledge.Teach(
                town.Hearer, town.TheftFact, KnowledgeSource.Hearsay, 0.8, town.Now, canProve: false);
            town.World.Relationships.ConnectMutual(town.Hearer, town.Asker, RelationKind.Spouse, 100);

            DisclosureDecision witnessed = town.Ask();
            DisclosureDecision heard = Disclosure.Decide(town.World, town.Hearer, town.Asker, town.TheftFact, town.Now);

            Assert.Equal(DisclosureDepth.InConfidence, witnessed.Depth);
            Assert.Equal(DisclosureDepth.Detail, heard.Depth);
            Assert.Equal(DisclosureDepth.Detail, heard.KnownDepth);
            Assert.Equal(DisclosureLimit.Knowledge, heard.Limit);
            Assert.Equal(DisclosureDepth.InConfidence, heard.StandingDepth);

            // The same story from somebody she can name is provenance she could give, and the
            // ceiling moves with it. Nothing about the tie is different.
            Town named = Town.Create();
            named.Calm();
            named.World.Knowledge.Teach(
                named.Hearer, named.TheftFact, KnowledgeSource.Hearsay, 0.8, named.Now, canProve: false, toldBy: named.Witness);
            named.World.Relationships.ConnectMutual(named.Hearer, named.Asker, RelationKind.Spouse, 100);
            DisclosureDecision sourced = Disclosure.Decide(named.World, named.Hearer, named.Asker, named.TheftFact, named.Now);

            Assert.Equal(DisclosureDepth.InConfidence, sourced.KnownDepth);
            Assert.Equal(DisclosureDepth.InConfidence, sourced.Depth);
        }

        /// <summary>Nobody discloses at any depth what they do not believe at all.</summary>
        [Fact]
        public void SomebodyWhoHoldsNothingRevealsNothing()
        {
            Town town = Town.Create();
            town.World.Relationships.ConnectMutual(town.Bystander, town.Asker, RelationKind.Spouse, 100);

            DisclosureDecision decision = Disclosure.Decide(town.World, town.Bystander, town.Asker, town.TheftFact, town.Now);

            Assert.Equal(DisclosureStrategy.NothingToDisclose, decision.Strategy);
            Assert.Equal(DisclosureDepth.Nothing, decision.Depth);
            Assert.Equal(DisclosureDepth.Nothing, decision.KnownDepth);
        }

        // -- the relationship is more than a number ---------------------------------------------------

        /// <summary>
        /// Two ties at the same warmth and the same kind, and one of them is deeper - because of
        /// what the two people have actually done for each other.
        ///
        /// The point of the step that a sentiment slider would miss. A kept promise and a shelter
        /// given are in the ledger already, they are history rather than feeling, and they are
        /// what makes one friend the person you tell the rest to.
        /// </summary>
        [Fact]
        public void HistoryBetweenTwoPeopleDeepensATieAffinityAloneWouldNot()
        {
            Town plain = Town.Create();
            plain.Calm();
            DisclosureDecision withoutHistory = plain.AskAs(RelationKind.Friend, 50);

            Town shared = Town.Create();
            shared.Calm();
            shared.Sheltered();
            shared.KeptAPromise();
            DisclosureDecision withHistory = shared.AskAs(RelationKind.Friend, 50);

            Assert.Equal(DisclosureDepth.Detail, withoutHistory.Depth);
            Assert.Equal(DisclosureDepth.InConfidence, withHistory.Depth);
            Assert.True(withHistory.Standing > withoutHistory.Standing);

            // And it runs both ways: a broken obligation is history too.
            Town betrayed = Town.Create();
            betrayed.Calm();
            betrayed.BrokeAPromise();
            DisclosureDecision afterwards = betrayed.AskAs(RelationKind.Friend, 50);

            Assert.Equal(DisclosureDepth.Gist, afterwards.Depth);
            Assert.True(afterwards.Standing < withoutHistory.Standing);
        }

        /// <summary>
        /// What the tie <em>is</em> counts alongside how warm it is, and the same table willingness
        /// reads is the one depth reads - so there is never a second opinion about what a spouse is.
        /// </summary>
        [Fact]
        public void WhatTheTieIsCountsAndNotOnlyHowWarmItIs()
        {
            Town town = Town.Create();
            town.Calm();

            DisclosureDecision acquaintance = town.AskAs(RelationKind.Acquaintance, 70);
            DisclosureDecision spouse = town.AskAs(RelationKind.Spouse, 70);

            Assert.True(spouse.Standing > acquaintance.Standing);
            Assert.Equal(DisclosureDepth.Detail, acquaintance.Depth);
            Assert.Equal(DisclosureDepth.InConfidence, spouse.Depth);
        }

        // -- affection does not override everything else -----------------------------------------------

        /// <summary>
        /// The boundary that keeps this from being a reward track. A frightened witness who
        /// answers her husband anyway does not go on to tell him how she knows - the fear is still
        /// there, and the tie did not cancel it.
        ///
        /// Asserted as a cap rather than as a subtraction: her willingness is untouched, she
        /// discloses either way, and what the fear takes is the last rung.
        /// </summary>
        [Fact]
        public void AWarmTieDoesNotBuyItsWayPastFear()
        {
            Town town = Town.Create();
            town.World.Relationships.ConnectMutual(town.Witness, town.Asker, RelationKind.Spouse, 95);

            town.Npc(town.Witness).Emotions.Set(EmotionalState.Fear, 0.7);
            DisclosureDecision frightened = town.Ask();

            town.Calm();
            DisclosureDecision settled = town.Ask();

            Assert.True(frightened.WillDisclose);
            Assert.Equal(DisclosureDepth.Detail, frightened.Depth);
            Assert.Equal(DisclosureLimit.Restraint, frightened.Limit);
            Assert.Equal(DisclosureDepth.InConfidence, frightened.StandingDepth);
            Assert.Equal(DisclosureDepth.InConfidence, frightened.KnownDepth);

            // The tie never moved. The fear did.
            Assert.Equal(DisclosureDepth.InConfidence, settled.Depth);
            Assert.Equal(frightened.Standing, settled.Standing, 9);
        }

        /// <summary>
        /// The same cap from a different constraint, to show it is the pressures doing it rather
        /// than one special-cased emotion: a claim kept close is given more shallowly than an open
        /// one, to the same person, at the same tie.
        /// </summary>
        [Fact]
        public void AKeptClaimComesOutMoreShallowlyThanAnOpenOne()
        {
            Town town = Town.Create();
            town.Calm();
            town.World.Relationships.ConnectMutual(town.Witness, town.Asker, RelationKind.Spouse, 95);

            DisclosureDecision ordinary = town.Ask();
            town.World.Knowledge.GetFact(town.TheftFact).Secrecy = 95;
            DisclosureDecision buried = town.Ask();

            Assert.Equal(DisclosureDepth.InConfidence, ordinary.Depth);
            Assert.Equal(DisclosureDepth.Detail, buried.Depth);
            Assert.Equal(DisclosureLimit.Restraint, buried.Limit);
        }

        // -- BQ-071 is extended, not replaced -----------------------------------------------------------

        /// <summary>
        /// Willingness still decides whether anything is said, and depth never contradicts it: an
        /// unwilling speaker reveals nothing at any rung, and the two axes agree about that.
        /// </summary>
        [Fact]
        public void AnUnwillingSpeakerRevealsNothingAtAnyDepth()
        {
            Town town = Town.Create();

            DisclosureDecision deflected = town.AskAs(RelationKind.Rival, -20);
            DisclosureDecision refused = town.AskAs(RelationKind.Enemy, -70);

            Assert.Equal(DisclosureStrategy.Deflect, deflected.Strategy);
            Assert.Equal(DisclosureStrategy.Refuse, refused.Strategy);

            foreach (DisclosureDecision decision in new[] { deflected, refused })
            {
                Assert.False(decision.WillDisclose);
                Assert.Equal(DisclosureDepth.Nothing, decision.Depth);
                Assert.Equal(DisclosureLimit.Unspoken, decision.Limit);
                Assert.False(decision.Reaches(DisclosureDepth.Gist));
            }
        }

        /// <summary>
        /// Depth is a second axis rather than a rung of the first. A hedge is a weaker commitment
        /// to the whole claim (BQ-071) and can still carry every particular the speaker holds; a
        /// confident answer to somebody further away can be bare. The two vary independently, and
        /// nothing here changed which strategy anybody chose.
        /// </summary>
        [Fact]
        public void CommitmentAndDepthAreIndependent()
        {
            Town town = Town.Create();
            town.Calm();

            // Warm enough to be forthcoming with, close enough for the particulars.
            DisclosureDecision committed = town.AskAs(RelationKind.Friend, 60);

            // The same tie, from somebody who is not sure of what she saw: she will say it without
            // standing behind it, and the particulars she holds come with it all the same.
            Town unsure = Town.Create();
            unsure.Calm();
            unsure.Doubts(0.35);
            DisclosureDecision hedged = unsure.AskAs(RelationKind.Friend, 60);

            Assert.Equal(DisclosureStrategy.Disclose, committed.Strategy);
            Assert.Equal(DisclosureStrategy.Hedge, hedged.Strategy);
            Assert.True(hedged.WillDisclose);
            Assert.Equal(DisclosureDepth.Detail, committed.Depth);
            Assert.Equal(DisclosureDepth.Detail, hedged.Depth);

            // A bare answer from somebody who is standing behind it: committed and shallow.
            DisclosureDecision distant = town.AskAs(RelationKind.Friend, 20);
            Assert.Equal(DisclosureStrategy.Disclose, distant.Strategy);
            Assert.True(distant.Committed);
            Assert.Equal(DisclosureDepth.Gist, distant.Depth);
        }

        /// <summary>
        /// Composing is untouched: how deep an answer goes does not change which act it is, or
        /// which claim it puts forward. What a realizer does with the depth is BQ-074's, and
        /// nothing about a shallow answer is a different or a lesser truth.
        /// </summary>
        [Fact]
        public void DepthChangesWhatIsSaidAndNotWhichClaimIsPutForward()
        {
            Town town = Town.Create();
            town.Calm();
            SpeechAct question = town.Question(town.Witness);

            SpeechAct shallow = Disclosure.Compose(town.AskAs(RelationKind.Friend, 15), question);
            SpeechAct deep = Disclosure.Compose(town.AskAs(RelationKind.Spouse, 95), question);

            Assert.NotNull(shallow);
            Assert.NotNull(deep);
            Assert.Equal(SpeechActType.Answer, shallow.Type);
            Assert.Equal(deep.About, shallow.About);
            Assert.Equal(deep.Signature, shallow.Signature);
            Assert.Equal(town.TheftFact, shallow.About);
        }

        /// <summary>
        /// The ladder has no rung for a half-truth. Every depth is the truth, less of it; shading
        /// what one holds remains BQ-073's and there is no way to express it here.
        /// </summary>
        [Fact]
        public void NoDepthIsAFalsehood()
        {
            string[] depths = Enum.GetNames(typeof(DisclosureDepth));

            Assert.Equal(
                new[] { "Nothing", "Gist", "Detail", "InConfidence" }.OrderBy(n => n),
                depths.OrderBy(n => n));
            Assert.DoesNotContain("Lie", depths);
            Assert.DoesNotContain("Evade", depths);
            Assert.DoesNotContain("Mislead", depths);
            Assert.DoesNotContain("HalfTruth", depths);

            // And BQ-071's ladder is still exactly BQ-071's ladder: this step extended the
            // decision, it did not replace the decision system underneath it.
            Assert.Equal(
                new[] { "NothingToDisclose", "Refuse", "Deflect", "Hedge", "Disclose" }.OrderBy(n => n),
                Enum.GetNames(typeof(DisclosureStrategy)).OrderBy(n => n));
        }

        // -- readable, repeatable, and it writes nothing -------------------------------------------------

        /// <summary>The same state asked the same question reveals the same amount, every time.</summary>
        [Fact]
        public void TheSameStateAlwaysGivesTheSameDepth()
        {
            Town town = Town.Create();
            town.Calm();
            town.KeptAPromise();
            town.World.Relationships.Connect(town.Witness, town.Asker, RelationKind.Friend, 60);

            DisclosureDecision first = town.Ask();
            for (int i = 0; i < 5; i++)
            {
                DisclosureDecision again = town.Ask();
                Assert.Equal(first.Depth, again.Depth);
                Assert.Equal(first.KnownDepth, again.KnownDepth);
                Assert.Equal(first.StandingDepth, again.StandingDepth);
                Assert.Equal(first.Limit, again.Limit);
                Assert.Equal(first.Standing, again.Standing, 9);
            }
        }

        /// <summary>Reading how deep somebody would go teaches nobody anything and records nothing.</summary>
        [Fact]
        public void DecidingDepthWritesNothing()
        {
            Town town = Town.Create();
            town.KeptAPromise();
            int facts = town.World.Knowledge.Facts.Count;
            int beliefs = town.World.Knowledge.BeliefsOf(town.Witness).Count();
            int events = town.World.Ledger.Events.Count;
            int obligations = town.World.Obligations.Records.Count;

            town.AskAs(RelationKind.Spouse, 95);
            town.AskAs(RelationKind.Enemy, -70);

            Assert.Equal(facts, town.World.Knowledge.Facts.Count);
            Assert.Equal(beliefs, town.World.Knowledge.BeliefsOf(town.Witness).Count());
            Assert.Equal(events, town.World.Ledger.Events.Count);
            Assert.Equal(obligations, town.World.Obligations.Records.Count);
        }

        /// <summary>
        /// A shallow answer from a friend is otherwise indistinguishable from a bug, so the
        /// inspector says how deep it went and which of the three ceilings held it there.
        /// </summary>
        [Fact]
        public void TheInspectorSaysHowDeepAndWhatHeldIt()
        {
            Town town = Town.Create();
            town.World.Relationships.ConnectMutual(town.Witness, town.Asker, RelationKind.Spouse, 95);
            town.Npc(town.Witness).Emotions.Set(EmotionalState.Fear, 0.7);

            string log = NarrativeInspector.DescribeDisclosure(town.World, town.Ask());

            Assert.Contains("depth:", log);
            Assert.Contains("Detail", log);
            Assert.Contains("something other than the relationship keeps the rest back", log);
            Assert.Contains("knows InConfidence", log);
        }

        // -- fixture ---------------------------------------------------------------------------------

        /// <summary>
        /// BQ-071's town, with what this step needs: a second knower of the same theft, so the
        /// same claim can be held two ways, and a claim with no particulars in it at all.
        /// </summary>
        private sealed class Town
        {
            private Town(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal EntityId Witness { get; private set; }

            internal EntityId Hearer { get; private set; }

            internal EntityId Thief { get; private set; }

            internal EntityId Asker { get; private set; }

            internal EntityId Bystander { get; private set; }

            internal EntityId TheftFact { get; private set; }

            internal EntityId RumourFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Town Create()
            {
                Town town = new Town(new NarrativeWorldState(20260902UL));
                town.Witness = town.Person("Mira");
                town.Hearer = town.Person("Nel");
                town.Thief = town.Person("Kip");
                town.Asker = town.Person("You");
                town.Bystander = town.Person("Tovar");

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

                // A claim that is all shape and no particulars: there is nothing in it to unlock.
                Fact rumour = new Fact(
                    town.World.NewId("fact"),
                    town.Thief,
                    FactPredicates.Stole,
                    EntityId.None,
                    null,
                    TruthState.True,
                    secrecy: 0);
                town.World.Knowledge.AddFact(rumour);
                town.RumourFact = rumour.Id;

                town.World.Knowledge.Teach(
                    town.Witness, theft.Id, KnowledgeSource.Witnessed, 0.9, town.Now, canProve: false);
                town.World.Knowledge.Teach(
                    town.Witness, rumour.Id, KnowledgeSource.Witnessed, 0.9, town.Now, canProve: false);

                town.Npc(town.Witness).Emotions.Affect(EmotionalState.Fear, 0.3, town.Now);
                return town;
            }

            internal NarrativeNpc Npc(EntityId id) => World.Registry.GetNpc(id);

            /// <summary>Leaves her holding the same claim less firmly.</summary>
            internal void Doubts(double confidence)
            {
                World.Knowledge.TryGetBelief(Witness, TheftFact, out KnowledgeRecord belief);
                belief.Confidence = confidence;
            }

            internal void Calm()
            {
                Npc(Witness).Emotions.Set(EmotionalState.Fear, 0.0);
                Npc(Hearer).Emotions.Set(EmotionalState.Fear, 0.0);
            }

            /// <summary>A promise between the two of them, kept. History, not affinity.</summary>
            internal void KeptAPromise()
            {
                Obligation(SocialObligationKind.Promise, Witness, Asker, 60).Fulfill(Now);
            }

            /// <summary>The same promise, broken.</summary>
            internal void BrokeAPromise()
            {
                Obligation(SocialObligationKind.Promise, Asker, Witness, 100).Break(Now);
            }

            /// <summary>Shelter still standing: the obligation somebody took a risk to enter.</summary>
            internal void Sheltered()
            {
                Obligation(SocialObligationKind.Sanctuary, Witness, Asker, 70);
            }

            internal DisclosureDecision Ask()
            {
                return Disclosure.Decide(World, Witness, Asker, TheftFact, Now);
            }

            internal DisclosureDecision AskAs(RelationKind kind, int sentiment)
            {
                World.Relationships.Connect(Witness, Asker, kind, sentiment);
                return Ask();
            }

            internal SpeechAct Question(EntityId asked)
            {
                return SpeechAct.Compose(
                    SpeechActType.Ask,
                    Asker,
                    asked,
                    new ActionBinding { PropositionFact = TheftFact });
            }

            private SocialObligation Obligation(SocialObligationKind kind, EntityId debtor, EntityId creditor, int strength)
            {
                SocialObligation obligation = new SocialObligation(
                    World.NewId("obl"),
                    kind,
                    debtor,
                    creditor,
                    EntityId.None,
                    kind.ToString(),
                    Now,
                    EntityId.None,
                    strength);
                World.Obligations.Add(obligation);
                return obligation;
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
