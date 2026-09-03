using System;
using System.Collections.Generic;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Who is on stage, by name.
    ///
    /// A snapshot rather than a window onto the world, and that is the point: realization is
    /// handed the names it may use and holds no reference to anything it could write to. A person
    /// the cast does not name is not "someone" or "them" - fragments that would have named them
    /// are simply not eligible, because inventing a way to refer to somebody is the smallest
    /// possible version of inventing a fact about them.
    /// </summary>
    public sealed class DialogueCast
    {
        public static readonly DialogueCast Anonymous = new DialogueCast();

        private readonly Dictionary<EntityId, string> _names = new Dictionary<EntityId, string>();

        public DialogueCast Name(EntityId id, string name)
        {
            if (!id.IsNone && !string.IsNullOrWhiteSpace(name))
            {
                _names[id] = name.Trim();
            }

            return this;
        }

        /// <summary>The name, or null when nobody supplied one.</summary>
        public string NameOf(EntityId id)
        {
            return _names.TryGetValue(id, out string name) ? name : null;
        }

        /// <summary>
        /// Names taken from the registry once, for the people this line is about. Reading the
        /// world here rather than inside realization keeps the realizer itself world-free, which
        /// is what makes "wording writes nothing" a fact about its signature rather than a promise
        /// about its body.
        /// </summary>
        public static DialogueCast From(NarrativeWorldState world, params EntityId[] people)
        {
            DialogueCast cast = new DialogueCast();
            if (world == null || people == null)
            {
                return cast;
            }

            for (int i = 0; i < people.Length; i++)
            {
                cast.Name(people[i], world.Registry.NameOf(people[i]));
            }

            return cast;
        }
    }

    /// <summary>
    /// Everything the wording layer is allowed to know, computed once from the semantic layer.
    ///
    /// Two halves, and neither of them is new information: <see cref="Value"/> answers the
    /// <see cref="DialogueReadings"/> a fragment may be chosen on, and <see cref="Slot"/> answers
    /// the <see cref="DialogueSlots"/> a fragment may name. Everything in both comes off the
    /// <see cref="SpeechAct"/>, the <see cref="DisclosureDecision"/> the caller passed, the
    /// <see cref="Fact"/> the caller passed, the <see cref="CallbackHook"/> the caller selected
    /// and the names the caller supplied. There is no sixth source, which is why no wording can
    /// carry a meaning the simulation did not already have - and the fifth is the narrowest of
    /// them, since a hook is itself derived from a recorded event rather than composed here.
    ///
    /// <b>The wording layer is never told that the speaker is lying.</b> A decision whose tactic
    /// is <see cref="DisclosureTactic.Falsify"/> is read as though no decision had been given at
    /// all: the readings that come off it are <see cref="DialogueReadings.Absent"/>, so a liar's
    /// denial draws from exactly the fragments an honest denial of the same claim draws from, and
    /// at the same seed says the identical words. The decision object is untouched -
    /// <c>WillLie</c> still reads true on it and <c>Deception</c> still classifies from the belief
    /// graph - this is a narrowing of what wording may read, not a rewriting of what was decided.
    /// The reason is BQ-073's own: a lie is a stance held against the speaker's belief rather than
    /// a way of speaking, and a fragment pool that shifted when somebody lied would be a tell in
    /// the words, which would make lies catchable by ear instead of by what the listener knows.
    /// </summary>
    public sealed class RealizationReading
    {
        private readonly Dictionary<string, string> _readings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>(StringComparer.Ordinal);

        private RealizationReading()
        {
        }

        /// <summary>How this key reads, or an empty string for a key outside the vocabulary.</summary>
        public string Value(string key)
        {
            return key != null && _readings.TryGetValue(key, out string value) ? value : string.Empty;
        }

        /// <summary>What fills this placeholder, or null when nothing in the input does.</summary>
        public string Slot(string name)
        {
            return name != null && _slots.TryGetValue(name, out string value) ? value : null;
        }

        public static RealizationReading Of(SpeechAct act, DisclosureDecision decision, Fact claim, DialogueCast cast)
        {
            return Of(act, decision, claim, cast, null);
        }

        /// <summary>
        /// The same reading, plus the old business the caller is bringing (BQ-081).
        ///
        /// The hook is a fifth input in the same sense the other four are: everything read off it
        /// - the kind of material and where its other party is standing - was derived from a
        /// recorded event before this call, and nothing here can add to it. A null hook reads
        /// exactly as no hook: <see cref="DialogueReadings.Absent"/>, so a fragment authored to
        /// refer back is not eligible in a scene with nothing to refer back to.
        /// </summary>
        public static RealizationReading Of(
            SpeechAct act,
            DisclosureDecision decision,
            Fact claim,
            DialogueCast cast,
            CallbackHook callback)
        {
            RealizationReading reading = new RealizationReading();
            if (act == null)
            {
                return reading;
            }

            reading._readings[DialogueReadings.Act] = Lower(act.Type.ToString());
            reading._readings[DialogueReadings.Stance] = Lower(act.Stance.ToString());
            reading._readings[DialogueReadings.Direction] = Snake(act.Direction.ToString());
            reading._readings[DialogueReadings.Referent] = ReadReferent(act);
            reading._readings[DialogueReadings.Claim] = act.Content.HasProposition ? "present" : "absent";
            reading._readings[DialogueReadings.ClaimPredicate] = claim == null ? DialogueReadings.Absent : claim.Predicate;
            reading._readings[DialogueReadings.Reply] = act.InReplyTo == null ? "none" : Lower(act.InReplyTo.Type.ToString());
            reading._readings[DialogueReadings.Audience] = act.Addressees.Count > 1 ? "several" : "one";
            ReadDecision(reading, decision);
            ReadCallback(reading, act, callback);
            ReadSlots(reading, act, claim, cast ?? DialogueCast.Anonymous, callback);
            return reading;
        }

        /// <summary>
        /// What the hook says, and the only three things it is allowed to say here: which kind of
        /// material it is, where its other party is standing, and which way round it went.
        ///
        /// A hook belonging to somebody other than the speaker is read as no hook at all rather
        /// than refused, because <see cref="RealizationRequest.WhyNot"/> has already refused the
        /// request by the time wording runs; this keeps the reading honest for a caller that built
        /// one by hand.
        /// </summary>
        private static void ReadCallback(RealizationReading reading, SpeechAct act, CallbackHook callback)
        {
            if (callback == null || callback.Recaller != act.Speaker)
            {
                reading._readings[DialogueReadings.Callback] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.CallbackParty] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.CallbackRoute] = DialogueReadings.Absent;
                return;
            }

            reading._readings[DialogueReadings.Callback] = Snake(callback.PrimaryKind.ToString());
            reading._readings[DialogueReadings.CallbackParty] = ReadParty(act, callback.Counterpart);
            reading._readings[DialogueReadings.CallbackRoute] = Snake(callback.Route.ToString());
        }

        private static string ReadParty(SpeechAct act, EntityId counterpart)
        {
            if (counterpart.IsNone)
            {
                return "none";
            }

            if (counterpart == act.Speaker)
            {
                return "speaker";
            }

            return act.IsAddressedTo(counterpart) ? "listener" : "other";
        }

        private static void ReadDecision(RealizationReading reading, DisclosureDecision decision)
        {
            // Nothing was given, or what was given is a falsification - which wording is not told
            // about, for the reason on the class.
            if (decision == null || decision.WillLie)
            {
                reading._readings[DialogueReadings.Strategy] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.Depth] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.Tactic] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.Commitment] = DialogueReadings.Absent;
                reading._readings[DialogueReadings.HeldBack] = DialogueReadings.Absent;
                return;
            }

            reading._readings[DialogueReadings.Strategy] = Snake(decision.Strategy.ToString());
            reading._readings[DialogueReadings.Depth] = Snake(decision.Depth.ToString());
            reading._readings[DialogueReadings.Tactic] = Snake(decision.Tactic.ToString());
            reading._readings[DialogueReadings.Commitment] = !decision.WillDisclose
                ? "unspoken"
                : decision.Committed ? "committed" : "hedged";
            reading._readings[DialogueReadings.HeldBack] = decision.HeldBack ? "yes" : "no";
        }

        private static void ReadSlots(RealizationReading reading, SpeechAct act, Fact claim, DialogueCast cast, CallbackHook callback)
        {
            Fill(reading, DialogueSlots.Speaker, cast.NameOf(act.Speaker));
            Fill(reading, DialogueSlots.Listener, act.Addressees.Count == 1 ? cast.NameOf(act.Addressees[0]) : null);
            Fill(reading, DialogueSlots.Referent, cast.NameOf(act.Referent));
            Fill(reading, DialogueSlots.Subject, claim == null ? null : cast.NameOf(claim.Subject));

            string matter = claim == null || string.IsNullOrWhiteSpace(claim.Value) ? act.Content.Purpose : claim.Value;
            Fill(reading, DialogueSlots.Matter, matter);

            // Named from the cast like everybody else, so a callback about somebody nobody put on
            // stage makes the fragments that would have named them ineligible rather than reaching
            // for a way to describe them.
            Fill(reading, DialogueSlots.Recalled,
                callback == null || callback.Recaller != act.Speaker ? null : cast.NameOf(callback.Counterpart));
        }

        private static void Fill(RealizationReading reading, string slot, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                reading._slots[slot] = value.Trim();
            }
        }

        private static string ReadReferent(SpeechAct act)
        {
            if (act.Referent.IsNone)
            {
                return "none";
            }

            if (act.Referent == act.Speaker)
            {
                return "speaker";
            }

            return act.IsAddressedTo(act.Referent) ? "listener" : "other";
        }

        private static string Lower(string name) => name.ToLowerInvariant();

        /// <summary>
        /// <c>InConfidence</c> becomes <c>in_confidence</c>. The enum names are the authority; this
        /// only makes them writable in content without a second table to keep in step.
        /// </summary>
        private static string Snake(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// One thing to say, and the state it may be said from.
    ///
    /// The act is the only required part, because it is the only part that carries meaning. The
    /// rest narrows how the same meaning may be worded: the decision behind it, the fact it names,
    /// who may be named aloud, what tone is wanted, and which deterministic stream chooses between
    /// the ways of saying it.
    /// </summary>
    public sealed class RealizationRequest
    {
        private static readonly string[] NoTone = new string[0];
        private static readonly string[] NoVocabulary = new string[0];
        private static readonly string[] NoManners = new string[0];

        public RealizationRequest(SpeechAct act)
        {
            Act = act;
        }

        /// <summary>What is meant. Never modified, and never re-derived from the wording.</summary>
        public SpeechAct Act { get; }

        /// <summary>
        /// The disclosure decision the act came out of (BQ-071 through BQ-073), when it came out
        /// of one. An input to wording and never rewritten by it.
        /// </summary>
        public DisclosureDecision Decision { get; set; }

        /// <summary>
        /// The claim the act is about, when the caller has it. Read for its predicate, its subject
        /// and the label it already carries - never for its truth, which is not a thing wording is
        /// entitled to know.
        /// </summary>
        public Fact Claim { get; set; }

        /// <summary>Who may be named aloud. <see cref="DialogueCast.Anonymous"/> when nobody is.</summary>
        public DialogueCast Cast { get; set; } = DialogueCast.Anonymous;

        /// <summary>
        /// The tone wanted, as <see cref="DialogueTones"/> tags. Empty asks for no tonal
        /// constraint. This is the seam BQ-075's voice profiles will fill; it constrains choice
        /// among ways of saying the same thing and can never change which thing is said.
        /// </summary>
        public IReadOnlyList<string> Tone { get; set; } = NoTone;

        /// <summary>
        /// The lived-context vocabulary wanted, as <see cref="DialogueVocabulary"/> tags (BQ-076).
        /// Empty asks for none, which is also the only thing
        /// <see cref="OccupationalVocabulary.RequestedVocabulary"/> ever produces for an identity
        /// nobody could read. Exactly like <see cref="Tone"/>, this narrows which fragment says the
        /// point and can never change which point is said - <see cref="DialogueFragment.FitsVocabulary"/>
        /// is the only place either list is read.
        /// </summary>
        public IReadOnlyList<string> Vocabulary { get; set; } = NoVocabulary;

        /// <summary>
        /// The <see cref="DialogueManners"/> the speaker's still-holding personal lines take off
        /// the table (BQ-077). Empty rules nothing out, which is what a speaker with no lines and
        /// a speaker whose lines all broke both produce.
        ///
        /// Unlike <see cref="Tone"/> and <see cref="Vocabulary"/> this is not a request: it is the
        /// result of rulings already taken where the decision was taken, carried here so wording
        /// can honour them rather than re-take them. It is also never the only place a prohibition
        /// applies - a forbidden semantic move is refused where it would have been selected and so
        /// never reaches a request - which is why nothing here can turn a permitted act into a
        /// forbidden one or the other way about.
        /// </summary>
        public IReadOnlyList<string> Forbidden { get; set; } = NoManners;

        /// <summary>
        /// What this conversation has already said, when the caller is tracking one (BQ-078). Null
        /// asks for no repetition control at all - the seam BQ-074 left with no consumer. When
        /// given, it narrows the same eligible pool <see cref="DialogueFragment.Fits"/> already
        /// built rather than adding a second one: repetition avoidance can only remove a candidate
        /// that was already semantically valid, never add one that was not.
        /// </summary>
        public DialogueExpressionHistory History { get; set; }

        /// <summary>
        /// This scene's weirdness allowance, when the caller is tracking one (BQ-079). Null asks
        /// for no weirdness constraint at all - the seam BQ-074 left with no consumer, the same way
        /// <see cref="History"/> did until BQ-078. When given, it narrows the same eligible pool
        /// <see cref="DialogueFragment.Fits"/> already built rather than adding a second one:
        /// <see cref="DialogueFragment.FitsWeirdness"/> can only remove a candidate that was already
        /// semantically valid, never add one that was not.
        /// </summary>
        public WeirdnessBudget WeirdnessBudget { get; set; }

        /// <summary>
        /// Old business the speaker is entitled to bring up, when the caller has selected one
        /// (BQ-081). Null asks for no reference back at all, which is what most lines are.
        ///
        /// It is a reference to a recorded event and never a retelling of it: what reaches wording
        /// is the kind of material and where its other party is standing, and nothing that could
        /// assert what happened. Selecting it is <c>CallbackHooks</c>' - including the whole of
        /// whether this speaker may know it - so a request cannot be the place a callback is
        /// invented, and <see cref="WhyNot"/> refuses one that belongs to somebody else.
        /// </summary>
        public CallbackHook Callback { get; set; }

        /// <summary>
        /// The stream the choices are drawn from. Only forked, never advanced, so the same
        /// semantic state and the same seed produce the same line however many other lines were
        /// realized in between.
        /// </summary>
        public DeterministicRng Rng { get; set; }

        /// <summary>
        /// Why this request could not be realized, or an empty string when it can be.
        ///
        /// The three refusals are all the same refusal: the caller has described a situation the
        /// semantic layer never produced. A realizer that quietly picked one of the two speakers,
        /// or worded a decision about one claim as an act about another, would be inventing the
        /// missing half in prose - which is exactly the failure this layer exists to make
        /// impossible.
        /// </summary>
        public string WhyNot()
        {
            if (Act == null)
            {
                return "there is no act to say";
            }

            if (Decision != null)
            {
                if (Decision.Speaker != Act.Speaker)
                {
                    return "the decision and the act have different speakers";
                }

                if (!Act.IsAddressedTo(Decision.Asker))
                {
                    return "the decision answers somebody the act does not address";
                }
            }

            if (Claim != null && Act.Content.HasProposition && Claim.Id != Act.About)
            {
                return "the claim is not the one the act is about";
            }

            if (Claim != null && Decision != null && !Decision.FactId.IsNone && Claim.Id != Decision.FactId)
            {
                return "the claim is not the one the decision was about";
            }

            // The fourth refusal, and the same refusal: a hook is derived for one person, so
            // putting somebody else's in this speaker's mouth would be wording claiming a memory
            // the simulation never granted them. Refusing here is what makes the knowledge gate
            // structural rather than a convention callers have to keep.
            if (Callback != null && Callback.Recaller != Act.Speaker)
            {
                return "the callback belongs to somebody other than the speaker";
            }

            return string.Empty;
        }
    }
}
