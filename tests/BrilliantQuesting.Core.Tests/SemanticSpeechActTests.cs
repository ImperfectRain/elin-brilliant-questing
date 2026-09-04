using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-070. Meaning before wording, and the tests that stop the two from merging.
    ///
    /// The step exists because a dialogue system that starts from sentences can never afterwards
    /// say what was communicated: the accusation, the denial and the apology are all just strings,
    /// so nothing downstream can tell that somebody was charged with something, that they said it
    /// was not so, or that what they said contradicts what they believe. Every later step in the
    /// stack - disclosure, lying, conversation state, realization - is a consumer of the
    /// distinction this file protects.
    ///
    /// So the file is organised around four things:
    /// the ten acts exist and are representable with no words in them; distinct acts stay distinct
    /// however they are later rendered; the vocabulary never becomes a second gameplay action
    /// system beside BQ-134's; and an act carries nothing that a realizer would have to invent.
    /// </summary>
    public class SemanticSpeechActTests
    {
        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// The step's condition: an act is produced with no text attached, and the log shows its
        /// full semantic content.
        ///
        /// The act is a real one taken from the live projection rather than hand-built - the
        /// player, knowing the theft, telling the guard about it - and every part of what was
        /// communicated is legible afterwards: who spoke, to whom, about which claim, against
        /// whom, what the act does to that claim and which way it moves. No part of the dump is a
        /// line of dialogue, and the dump says so rather than leaving it to be inferred.
        /// </summary>
        [Fact]
        public void AnActIsProducedWithNoTextAndTheLogShowsItsFullSemanticContent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Learn(lab, lab.Player);
            ActionContext context = Focus(lab, lab.Situation.WitnessId);

            SpeechAct act = SpeechActMeaning.Of("expose", context);

            Assert.NotNull(act);
            Assert.Equal(SpeechActType.Accuse, act.Type);
            Assert.Equal(lab.Player, act.Speaker);
            Assert.Equal(new[] { lab.Situation.WitnessId }, act.Addressees.ToArray());
            Assert.Equal(lab.Situation.TheftFactId, act.About);
            Assert.Equal(lab.Situation.ThiefId, act.Referent);
            Assert.Equal(SpeechActStance.Affirms, act.Stance);
            Assert.Equal(SpeechActDirection.GivesInformation, act.Direction);

            string log = NarrativeInspector.DescribeSpeechAct(lab.World, act);

            Assert.Contains("speech act: Accuse", log);
            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.WitnessId), log);
            Assert.Contains(lab.Situation.TheftFactId.Value, log);
            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.ThiefId), log);
            Assert.Contains("stance:      Affirms", log);
            Assert.Contains("direction: GivesInformation", log);
            Assert.Contains("wording:     none", log);
        }

        /// <summary>
        /// The structural half of the same condition, and the one that survives future editing:
        /// the contract has nowhere to put a word.
        ///
        /// A test that only checks today's fields passes the day somebody adds `Line`, `Tone` or
        /// `Fragment` "just to carry the draft through". This one fails then. The single string on
        /// the type is the wording-free signature, which is ids and act names and nothing a
        /// player could read.
        /// </summary>
        [Fact]
        public void TheContractHasNowhereToPutWording()
        {
            PropertyInfo[] properties = typeof(SpeechAct)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            string[] strings = properties
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name)
                .ToArray();

            Assert.Equal(new[] { "Signature" }, strings);

            TheftLaboratory lab = TheftLaboratory.Create();
            SpeechAct act = Charge(lab);
            Assert.DoesNotContain(" ", act.Signature);
        }

        // -- the vocabulary ----------------------------------------------------------------------

        /// <summary>
        /// All ten acts the step names are representable, and each of them means something
        /// different from all the others while carrying no words at all.
        ///
        /// Distinctness is asserted on the wording-free signature rather than on the act type, so
        /// the assertion is about meaning and not about the name of the enum member.
        /// </summary>
        [Fact]
        public void EveryRequiredActIsRepresentableAndDistinctWithoutWording()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Dictionary<SpeechActType, SpeechAct> acts = OneOfEach(lab);

            foreach (SpeechActType type in SpeechActProfile.Vocabulary)
            {
                Assert.True(acts.ContainsKey(type), type + " could not be represented");
                Assert.Equal(type, acts[type].Type);
            }

            Assert.Equal(SpeechActProfile.Vocabulary.Count, acts.Count);
            Assert.Equal(acts.Count, acts.Values.Select(a => a.Signature).Distinct().Count());
        }

        /// <summary>
        /// The vocabulary is the enum and the table agrees with it. A member with no profile would
        /// be an act nothing can reason about; a profile for a member that no longer exists would
        /// be a rule nothing enforces.
        /// </summary>
        [Fact]
        public void EveryActTypeHasASemanticProfile()
        {
            SpeechActType[] declared = (SpeechActType[])Enum.GetValues(typeof(SpeechActType));

            Assert.Equal(declared.OrderBy(t => t).ToArray(), SpeechActProfile.Vocabulary.OrderBy(t => t).ToArray());
            Assert.All(declared, type => Assert.NotNull(SpeechActProfile.Of(type)));
        }

        /// <summary>
        /// The point of the whole layer, stated as a test: put every act through a realizer that
        /// says exactly the same thing about all of them, and the acts are still ten different
        /// things. Meaning does not live in the rendering, so losing the rendering loses nothing.
        /// </summary>
        [Fact]
        public void DistinctActsStayDistinctUnderPlaceholderRealization()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            List<SpeechAct> acts = OneOfEach(lab).Values.ToList();

            // The whole of BQ-074, stubbed: every act renders identically.
            string[] rendered = acts.Select(_ => "...").ToArray();
            string[] signatures = acts.Select(a => a.Signature).ToArray();

            Assert.Single(rendered.Distinct());
            Assert.Equal(acts.Count, signatures.Distinct().Count());
            Assert.Equal(acts.Count, acts.Select(a => NarrativeInspector.DescribeSpeechAct(lab.World, a)).Distinct().Count());
        }

        // -- what keeps the acts apart -----------------------------------------------------------

        /// <summary>
        /// A charge and an admission are the same claim pointed at different people, so the
        /// referent is what separates them and the layer refuses to let either wear the other's
        /// name. Getting this wrong would make a confession indistinguishable from an accusation
        /// in every consumer downstream.
        /// </summary>
        [Fact]
        public void AChargeNamesSomebodyElseAndAnAdmissionOnlyTheSpeaker()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionBinding theft = Theft(lab);

            Assert.NotNull(SpeechAct.Compose(
                SpeechActType.Accuse, lab.Situation.VictimId, lab.Player, theft, lab.Situation.ThiefId));
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Accuse, lab.Situation.VictimId, lab.Player, theft, lab.Situation.VictimId));
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Accuse, lab.Situation.VictimId, lab.Player, theft));

            SpeechAct admission = SpeechAct.Compose(SpeechActType.Admit, lab.Situation.ThiefId, lab.Player, theft);
            Assert.NotNull(admission);
            Assert.Equal(lab.Situation.ThiefId, admission.Referent);
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Admit, lab.Situation.ThiefId, lab.Player, theft, lab.Situation.VictimId));
        }

        /// <summary>
        /// Gossip is defined by who is not there. Told to its own subject it is something else -
        /// a charge, a warning, a remark - and calling it gossip would let the rumour layer treat
        /// a confrontation as talk behind somebody's back.
        /// </summary>
        [Fact]
        public void GossipRequiresAnAbsentSubject()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionBinding theft = Theft(lab);

            Assert.NotNull(SpeechAct.Compose(
                SpeechActType.Gossip, lab.Situation.WitnessId, lab.Player, theft, lab.Situation.ThiefId));
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Gossip, lab.Situation.WitnessId, lab.Situation.ThiefId, theft, lab.Situation.ThiefId));
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Gossip, lab.Situation.ThiefId, lab.Player, theft, lab.Situation.ThiefId));
        }

        /// <summary>
        /// An answer nobody asked for is not an answer, and a refusal of nothing is not a refusal.
        /// The pairing is part of what the act means, so it is checked at composition rather than
        /// left for a conversation layer to reconstruct from adjacency.
        /// </summary>
        [Fact]
        public void ResponsesRequireSomethingToRespondTo()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionBinding theft = Theft(lab);

            SpeechAct question = SpeechAct.Compose(
                SpeechActType.Ask, lab.Player, lab.Situation.WitnessId, theft);
            Assert.NotNull(question);

            Assert.Null(SpeechAct.Compose(SpeechActType.Answer, lab.Situation.WitnessId, lab.Player, theft));
            Assert.Null(SpeechAct.Compose(SpeechActType.Refuse, lab.Situation.WitnessId, lab.Player, theft));

            SpeechAct answer = SpeechAct.Compose(
                SpeechActType.Answer, lab.Situation.WitnessId, lab.Player, theft, EntityId.None, question);
            Assert.NotNull(answer);
            Assert.Same(question, answer.InReplyTo);

            // An answer answers a question. It does not answer a demand, and it does not answer
            // the speaker's own move.
            SpeechAct demand = SpeechAct.Compose(SpeechActType.Request, lab.Player, lab.Situation.WitnessId, theft);
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Answer, lab.Situation.WitnessId, lab.Player, theft, EntityId.None, demand));
            Assert.NotNull(SpeechAct.Compose(
                SpeechActType.Refuse, lab.Situation.WitnessId, lab.Player, ActionBinding.Empty, EntityId.None, demand));
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Answer, lab.Player, lab.Situation.WitnessId, theft, EntityId.None, question));

            // A response is addressed to whoever it responds to; a remark on the same subject
            // aimed elsewhere is a different move.
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Answer, lab.Situation.WitnessId, lab.Situation.VictimId, theft, EntityId.None, question));
        }

        /// <summary>
        /// A false accusation is well formed, and nothing here quietly corrects it.
        ///
        /// The layer knows who the claim is about and knows who was named, and it lets them
        /// differ, because an accusation that could only ever name the real culprit is an
        /// accusation that can never be wrong - and a world where nobody can be wrongly accused
        /// has no investigation in it.
        /// </summary>
        [Fact]
        public void AnAccusationMayNameSomebodyTheClaimDoesNot()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Fact theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);

            SpeechAct wrong = SpeechAct.Compose(
                SpeechActType.Accuse, lab.Situation.VictimId, lab.Player, Theft(lab), lab.Situation.WitnessId);

            Assert.NotNull(wrong);
            Assert.Equal(lab.Situation.WitnessId, wrong.Referent);
            Assert.NotEqual(theft.Subject, wrong.Referent);
            Assert.Equal(lab.Situation.ThiefId, theft.Subject);
        }

        /// <summary>
        /// The hook BQ-073 needs, and the whole of what BQ-070 owes it: an act's stance is fixed
        /// by its type, so "what the speaker put forward" and "what the speaker believes" are two
        /// separately readable things and a lie is the gap between them.
        ///
        /// Nothing here decides to lie, records a lie or judges one. It only makes the
        /// contradiction computable without a single word having been chosen.
        /// </summary>
        [Fact]
        public void StanceAgainstBeliefIsComputableWithoutAnyWording()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Learn(lab, lab.Situation.ThiefId);
            ActionBinding theft = Theft(lab);

            SpeechAct denial = SpeechAct.Compose(SpeechActType.Deny, lab.Situation.ThiefId, lab.Player, theft);
            SpeechAct admission = SpeechAct.Compose(SpeechActType.Admit, lab.Situation.ThiefId, lab.Player, theft);

            Assert.Equal(SpeechActStance.Denies, denial.Stance);
            Assert.Equal(SpeechActStance.Affirms, admission.Stance);
            Assert.Equal(lab.Situation.ThiefId, denial.Referent);

            bool speakerBelievesIt = lab.World.Knowledge.BelievesConfidently(denial.Speaker, denial.About);
            Assert.True(speakerBelievesIt);
            Assert.True(speakerBelievesIt && denial.Stance == SpeechActStance.Denies);
            Assert.False(speakerBelievesIt && admission.Stance == SpeechActStance.Denies);
        }

        // -- the BQ-134 boundary -----------------------------------------------------------------

        /// <summary>
        /// The seam runs one way. A projected contextual intent (BQ-134) has a meaning, and this
        /// layer reads it off the projection that already happened rather than re-deriving one.
        /// The content is the projection's own binding, so speech has no private notion of what an
        /// attempt is about.
        /// </summary>
        [Fact]
        public void AProjectedIntentCarriesItsMeaning()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // Asking is on offer while the witness holds something the player does not.
            ActionContext asking = Focus(lab, lab.Situation.WitnessId);
            ActionIntentOption question = Project(lab, asking).Single(o => o.Action.Id == "question");
            SpeechAct ask = SpeechActMeaning.Of(question, asking);

            Assert.Equal(SpeechActType.Ask, ask.Type);
            Assert.Equal(SpeechActDirection.SeeksInformation, ask.Direction);
            Assert.Equal(SpeechActStance.Questions, ask.Stance);
            Assert.Equal(ActionBinding.Infer(asking).PropositionFact, ask.Content.PropositionFact);
            Assert.Equal(ActionBinding.Infer(asking).Item, ask.Content.Item);

            // Pressing the thief is on offer once the player knows what to press them about.
            Learn(lab, lab.Player);
            ActionContext pressing = Focus(lab, lab.Situation.ThiefId);
            ActionIntentOption pressure = Project(lab, pressing).Single(o => o.Action.Id == "intimidate");
            SpeechAct threat = SpeechActMeaning.Of(pressure, pressing);

            Assert.Equal(SpeechActType.Threaten, threat.Type);
            Assert.Equal(SpeechActDirection.SeeksAction, threat.Direction);
            Assert.Equal(lab.Situation.ThiefId, threat.Addressees.Single());
            Assert.Equal(ActionBinding.Infer(pressing).PropositionFact, threat.Content.PropositionFact);
        }

        /// <summary>
        /// The vocabulary is not a mirror of the verb registry, in both directions.
        ///
        /// Two different attempts with different consequences - telling a neighbour, reporting to
        /// a guard - are one communicative act, and most verbs communicate nothing at all. If the
        /// mapping were one-to-one the speech layer would be the action registry wearing a
        /// different name, which is exactly the second action system the step must not create.
        /// </summary>
        [Fact]
        public void MeaningIsManyToOneAndPartialOverTheVerbRegistry()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Learn(lab, lab.Player);
            ActionContext context = Focus(lab, lab.Situation.WitnessId);

            Assert.Equal(SpeechActType.Accuse, SpeechActMeaning.Of("expose", context).Type);
            Assert.Equal(SpeechActType.Accuse, SpeechActMeaning.Of("report", context).Type);
            Assert.Equal(SpeechActType.Request, SpeechActMeaning.Of("persuade", context).Type);
            Assert.Equal(SpeechActType.Request, SpeechActMeaning.Of("bribe", context).Type);

            Assert.Null(SpeechActMeaning.Of("pickpocket", context));
            Assert.Null(SpeechActMeaning.Of("search", context));
            Assert.Null(SpeechActMeaning.Of("return_item", context));

            // A lie is a stance held against belief, not an act type, so BQ-073 - not this table -
            // decides which act carries one.
            Assert.Null(SpeechActMeaning.Of("lie", context));
            Assert.False(SpeechActMeaning.IsCommunicative("lie"));

            // Seven of the eleven have no player verb at all, because answering, denying, owning
            // up, refusing, evading, apologizing and passing something on are moves inside a
            // conversation rather than options on a menu.
            IEnumerable<SpeechActType> projectable = lab.Actions
                .Discover(context, includeUnavailable: true)
                .Select(offer => SpeechActMeaning.Of(offer.Action.Id, context))
                .Where(act => act != null)
                .Select(act => act.Type)
                .Distinct();

            Assert.Equal(
                new[] { SpeechActType.Ask, SpeechActType.Accuse, SpeechActType.Request, SpeechActType.Threaten }.OrderBy(t => t),
                projectable.OrderBy(t => t));
        }

        /// <summary>
        /// The layer has no gameplay authority: it never decides whether something may be done and
        /// it never does anything.
        ///
        /// The first half is proved by asking what an unavailable attempt would mean and getting
        /// an answer - meaning does not consult availability, so it cannot gate on it. The second
        /// half is proved by composing the entire vocabulary and reading every authoritative
        /// ledger afterwards: no event, no fact, no belief, no thread, no memory, no obligation.
        /// </summary>
        [Fact]
        public void MeaningNeitherGatesNorWrites()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Learn(lab, lab.Player);
            ActionContext context = Focus(lab, lab.Situation.WitnessId);

            // The witness holds no authority, so reporting to them is not on offer - and it still
            // has a meaning, because saying what an attempt would communicate is not permission.
            Availability report = lab.Actions.Get("report").GetAvailability(context);
            Assert.False(report.IsAvailable);
            Assert.NotNull(SpeechActMeaning.Of("report", context));

            int events = lab.World.Ledger.Count;
            int facts = lab.World.Knowledge.Facts.Count;
            int threads = lab.World.Threads.Count;
            int beliefs = lab.World.Registry.Npcs.Sum(npc => lab.World.Knowledge.BeliefsOf(npc.Key).Count());
            int memories = lab.World.Memories.All.Sum(owner => owner.Value.Count);

            Dictionary<SpeechActType, SpeechAct> everything = OneOfEach(lab);
            foreach (ActionOffer offer in lab.Actions.Discover(context, includeUnavailable: true))
            {
                SpeechActMeaning.Of(offer.Action.Id, context);
            }

            foreach (SpeechAct act in everything.Values)
            {
                NarrativeInspector.DescribeSpeechAct(lab.World, act);
            }

            Assert.Equal(events, lab.World.Ledger.Count);
            Assert.Equal(facts, lab.World.Knowledge.Facts.Count);
            Assert.Equal(threads, lab.World.Threads.Count);
            Assert.Equal(beliefs, lab.World.Registry.Npcs.Sum(npc => lab.World.Knowledge.BeliefsOf(npc.Key).Count()));
            Assert.Equal(memories, lab.World.Memories.All.Sum(owner => owner.Value.Count));
        }

        /// <summary>
        /// The knowledge rule BQ-134 applies to labels, applied to meaning, where it matters more:
        /// a label that leaks an unknown culprit is a display bug, but an act that names one is
        /// the simulation asserting somebody said what they had no way to say.
        ///
        /// So the question a player asks knowing nothing names nobody, and the charge they cannot
        /// make is not composed at all rather than composed against a blank.
        /// </summary>
        [Fact]
        public void AnActNeverNamesSomebodyTheSpeakerDoesNotKnowAbout()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext ignorant = Focus(lab, lab.Situation.WitnessId);

            SpeechAct blindQuestion = SpeechActMeaning.Of("question", ignorant);
            Assert.NotNull(blindQuestion);
            Assert.True(blindQuestion.Referent.IsNone);
            Assert.Null(SpeechActMeaning.Of("expose", ignorant));

            Learn(lab, lab.Player);
            ActionContext informed = Focus(lab, lab.Situation.WitnessId);

            Assert.Equal(lab.Situation.ThiefId, SpeechActMeaning.Of("question", informed).Referent);
            Assert.Equal(lab.Situation.ThiefId, SpeechActMeaning.Of("expose", informed).Referent);
        }

        // -- determinism -------------------------------------------------------------------------

        /// <summary>
        /// The same meaning composed twice is the same meaning, an audience is a set rather than a
        /// running order, and an act does not change after it was composed.
        ///
        /// The last of those is the one with teeth: the content slot is the action layer's own
        /// mutable per-attempt binding, and an act that held it by reference would be a record of
        /// something nobody said the moment the next attempt reused it.
        /// </summary>
        [Fact]
        public void MeaningIsDeterministicAndDoesNotDriftAfterComposition()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Learn(lab, lab.Player);

            Assert.Equal(
                SpeechActMeaning.Of("expose", Focus(lab, lab.Situation.WitnessId)).Signature,
                SpeechActMeaning.Of("expose", Focus(lab, lab.Situation.WitnessId)).Signature);

            ActionBinding theft = Theft(lab);
            SpeechAct told = SpeechAct.Compose(
                SpeechActType.Gossip,
                lab.Situation.WitnessId,
                new[] { lab.Player, lab.Situation.VictimId },
                theft,
                lab.Situation.ThiefId);
            SpeechAct toldAgain = SpeechAct.Compose(
                SpeechActType.Gossip,
                lab.Situation.WitnessId,
                new[] { lab.Situation.VictimId, lab.Player },
                theft,
                lab.Situation.ThiefId);

            Assert.Equal(told.Signature, toldAgain.Signature);

            string before = told.Signature;
            theft.PropositionFact = lab.Situation.OwnershipFactId;
            theft.Purpose = "something else entirely";

            Assert.Equal(before, told.Signature);
            Assert.Equal(lab.Situation.TheftFactId, told.About);
        }

        // -- helpers -----------------------------------------------------------------------------

        /// <summary>
        /// One well-formed instance of every act in the vocabulary, built around the laboratory
        /// theft so that each is a thing somebody in that scenario would actually communicate.
        /// </summary>
        private static Dictionary<SpeechActType, SpeechAct> OneOfEach(TheftLaboratory lab)
        {
            EntityId player = lab.Player;
            EntityId thief = lab.Situation.ThiefId;
            EntityId victim = lab.Situation.VictimId;
            EntityId witness = lab.Situation.WitnessId;
            ActionBinding theft = Theft(lab);
            ActionBinding help = new ActionBinding { Purpose = "getting the ring back" };

            SpeechAct ask = SpeechAct.Compose(SpeechActType.Ask, player, witness, theft);
            SpeechAct request = SpeechAct.Compose(SpeechActType.Request, victim, player, help);

            Dictionary<SpeechActType, SpeechAct> acts = new Dictionary<SpeechActType, SpeechAct>
            {
                { SpeechActType.Ask, ask },
                { SpeechActType.Answer, SpeechAct.Compose(SpeechActType.Answer, witness, player, theft, EntityId.None, ask) },
                { SpeechActType.Accuse, SpeechAct.Compose(SpeechActType.Accuse, victim, thief, theft, thief) },
                { SpeechActType.Deny, SpeechAct.Compose(SpeechActType.Deny, thief, victim, theft) },
                { SpeechActType.Admit, SpeechAct.Compose(SpeechActType.Admit, thief, victim, theft) },
                { SpeechActType.Request, request },
                { SpeechActType.Refuse, SpeechAct.Compose(SpeechActType.Refuse, player, victim, ActionBinding.Empty, EntityId.None, request) },
                { SpeechActType.Threaten, SpeechAct.Compose(SpeechActType.Threaten, victim, thief, theft) },
                { SpeechActType.Apologize, SpeechAct.Compose(SpeechActType.Apologize, thief, victim, theft) },
                { SpeechActType.Gossip, SpeechAct.Compose(SpeechActType.Gossip, witness, player, theft, thief) },
                { SpeechActType.Evade, SpeechAct.Compose(SpeechActType.Evade, witness, player, ActionBinding.Empty, EntityId.None, ask) },
                { SpeechActType.Promise, SpeechAct.Compose(SpeechActType.Promise, thief, victim, help) },
                { SpeechActType.Inform, SpeechAct.Compose(SpeechActType.Inform, witness, victim, theft) },
                { SpeechActType.Warn, SpeechAct.Compose(SpeechActType.Warn, witness, player, help) },
                { SpeechActType.Offer, SpeechAct.Compose(SpeechActType.Offer, thief, victim, help) },
                { SpeechActType.Forgive, SpeechAct.Compose(SpeechActType.Forgive, victim, thief, ActionBinding.Empty, thief) }
            };

            foreach (KeyValuePair<SpeechActType, SpeechAct> pair in acts)
            {
                Assert.True(pair.Value != null, pair.Key + " was refused: " + pair.Key);
            }

            return acts;
        }

        private static SpeechAct Charge(TheftLaboratory lab)
        {
            return SpeechAct.Compose(
                SpeechActType.Accuse, lab.Situation.VictimId, lab.Player, Theft(lab), lab.Situation.ThiefId);
        }

        private static ActionBinding Theft(TheftLaboratory lab)
        {
            return new ActionBinding
            {
                PropositionFact = lab.Situation.TheftFactId,
                Item = lab.Situation.ItemId
            };
        }

        private static void Learn(TheftLaboratory lab, EntityId knower)
        {
            lab.World.Knowledge.Teach(
                knower, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 0.9, lab.Vanilla.Now, true);
        }

        private static ActionContext Focus(TheftLaboratory lab, EntityId target)
        {
            ActionContext context = lab.Context(target);
            context.SubjectFact = lab.Situation.TheftFactId;
            context.SubjectItem = lab.Situation.ItemId;
            return context;
        }

        private static List<ActionIntentOption> Project(TheftLaboratory lab, ActionContext context)
        {
            List<ActionOffer> available = new List<ActionOffer>(lab.Actions.Discover(context));
            return ContextualActionProjection.Project(available, context, 12);
        }
    }
}
