using System;
using System.Collections.Generic;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Relationships;
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
    /// What the speaker is feeling strongly enough to be heard doing it (BQ-146).
    ///
    /// One emotion and one number, taken from the profile the world already keeps and decays. It
    /// is passed to wording rather than read there for the same reason the cast is: the realizer
    /// holds nothing it could write to, and "wording writes no world state" stays a fact about its
    /// signature.
    ///
    /// <see cref="Of"/> is the only sanctioned way to derive one, so "which feeling is the audible
    /// one" is answered in a single place instead of at every call site. Nothing is audible below
    /// <see cref="Floor"/>: somebody faintly several things at once is not visibly any of them,
    /// and a grief-marked line said by somebody slightly sad is the vocabulary talking rather than
    /// the character.
    /// </summary>
    public readonly struct SpeakerFeeling
    {
        /// <summary>Below this, nothing shows. A quiet mood is not a mood a listener can hear.</summary>
        public const double Floor = 0.35;

        /// <summary>Nothing audible. What a calm speaker and an unread one both produce.</summary>
        public static readonly SpeakerFeeling None = new SpeakerFeeling(false, default(EmotionalState), 0.0);

        private SpeakerFeeling(bool audible, EmotionalState state, double intensity)
        {
            IsAudible = audible;
            State = state;
            Intensity = intensity;
        }

        public bool IsAudible { get; }

        public EmotionalState State { get; }

        public double Intensity { get; }

        /// <summary>
        /// The one emotion above the floor, or <see cref="None"/>.
        ///
        /// Ties break on the enum's own order, so the same profile always reads the same way and a
        /// line does not change because two feelings happened to be equal.
        /// </summary>
        public static SpeakerFeeling Of(EmotionalStateProfile profile, GameTime now)
        {
            if (profile == null)
            {
                return None;
            }

            SpeakerFeeling strongest = None;
            foreach (EmotionalState state in Enum.GetValues(typeof(EmotionalState)))
            {
                double intensity = profile.Get(state, now);
                if (intensity >= Floor && intensity > strongest.Intensity)
                {
                    strongest = new SpeakerFeeling(true, state, intensity);
                }
            }

            return strongest;
        }

        /// <summary>An audible feeling named directly, for a caller that has one without a profile.</summary>
        public static SpeakerFeeling Felt(EmotionalState state, double intensity)
        {
            return intensity >= Floor ? new SpeakerFeeling(true, state, intensity) : None;
        }
    }

    /// <summary>
    /// What the speaker is to one particular listener, as the relationship graph already holds it
    /// (BQ-146).
    ///
    /// Directed, because the graph is: a creditor's view of a debtor is not the debtor's view of
    /// the creditor, and a line authored for one of those said by the other would be wording
    /// asserting the wrong relationship.
    ///
    /// Three states, not two. <see cref="Tied"/> is a tie the world holds; <see cref="Stranger"/>
    /// is the world saying there is none, which is a real thing to have a line for; and
    /// <see cref="Unread"/> is a caller who did not look. Collapsing the last two would let a
    /// stranger's line be said to somebody's spouse because nobody checked.
    /// </summary>
    public readonly struct SpeakerTie
    {
        /// <summary>Nobody looked. Reads as <see cref="DialogueReadings.Absent"/>.</summary>
        public static readonly SpeakerTie Unread = new SpeakerTie(false, false, default(RelationKind), EntityId.None);

        private SpeakerTie(bool read, bool tied, RelationKind kind, EntityId listener)
        {
            IsRead = read;
            IsTied = tied;
            Kind = kind;
            Listener = listener;
        }

        public bool IsRead { get; }

        public bool IsTied { get; }

        public RelationKind Kind { get; }

        /// <summary>Who the tie is with. The act has to be addressing them, or the request is refused.</summary>
        public EntityId Listener { get; }

        public static SpeakerTie Stranger(EntityId listener)
        {
            return listener.IsNone ? Unread : new SpeakerTie(true, false, default(RelationKind), listener);
        }

        public static SpeakerTie Tied(RelationKind kind, EntityId listener)
        {
            return listener.IsNone ? Unread : new SpeakerTie(true, true, kind, listener);
        }

        /// <summary>
        /// The tie the graph holds from speaker to listener, or <see cref="Stranger"/> when it
        /// holds none. Reading the graph here rather than inside realization keeps the realizer
        /// world-free.
        /// </summary>
        public static SpeakerTie Of(RelationshipGraph graph, EntityId speaker, EntityId listener)
        {
            if (graph == null || speaker.IsNone || listener.IsNone)
            {
                return Unread;
            }

            RelationshipEdge edge = graph.Find(speaker, listener);
            return edge == null ? Stranger(listener) : Tied(edge.Kind, listener);
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
            return Of(act, decision, claim, cast, callback, SpeakerFeeling.None, SpeakerTie.Unread);
        }

        /// <summary>
        /// The same reading, plus what the speaker is feeling and what they are to the person
        /// opposite (BQ-146).
        ///
        /// Two more inputs of exactly the kind the other five already are: both are read off state
        /// the world holds before this call, neither can be composed here, and both read as
        /// <see cref="DialogueReadings.Absent"/> when nothing was supplied - so a fragment authored
        /// for anger is not eligible in a scene nobody read a mood for, and a fragment authored for
        /// strangers is not eligible for a caller who never looked at the graph.
        /// </summary>
        public static RealizationReading Of(
            SpeechAct act,
            DisclosureDecision decision,
            Fact claim,
            DialogueCast cast,
            CallbackHook callback,
            SpeakerFeeling feeling,
            SpeakerTie tie)
        {
            RealizationReading reading = new RealizationReading();
            if (act == null)
            {
                return reading;
            }

            reading._readings[DialogueReadings.Act] = Snake(act.Type.ToString());
            reading._readings[DialogueReadings.Stance] = Snake(act.Stance.ToString());
            reading._readings[DialogueReadings.Direction] = Snake(act.Direction.ToString());
            reading._readings[DialogueReadings.Referent] = ReadReferent(act);
            reading._readings[DialogueReadings.Claim] = act.Content.HasProposition ? "present" : "absent";
            reading._readings[DialogueReadings.ClaimPredicate] = claim == null ? DialogueReadings.Absent : claim.Predicate;
            reading._readings[DialogueReadings.Reply] = act.InReplyTo == null ? "none" : Snake(act.InReplyTo.Type.ToString());
            reading._readings[DialogueReadings.Audience] = act.Addressees.Count > 1 ? "several" : "one";
            ReadDecision(reading, decision);
            ReadCallback(reading, act, callback);
            ReadFeeling(reading, feeling);
            ReadTie(reading, act, tie);
            ReadSlots(reading, act, claim, cast ?? DialogueCast.Anonymous, callback);
            return reading;
        }

        /// <summary>
        /// What the speaker is audibly feeling, or <see cref="DialogueReadings.Absent"/>. One
        /// value: which feeling, never how much of it, because a fragment chosen on a number would
        /// be content deciding where a threshold lies.
        /// </summary>
        private static void ReadFeeling(RealizationReading reading, SpeakerFeeling feeling)
        {
            reading._readings[DialogueReadings.Emotion] =
                feeling.IsAudible ? Snake(feeling.State.ToString()) : DialogueReadings.Absent;
        }

        /// <summary>
        /// What the speaker is to whoever they are addressing.
        ///
        /// A tie read for somebody this act does not address is read as no tie at all rather than
        /// refused, exactly as a callback belonging to somebody else is:
        /// <see cref="RealizationRequest.WhyNot"/> has already turned the request down by the time
        /// wording runs, and this keeps the reading honest for a caller that built one by hand.
        /// </summary>
        private static void ReadTie(RealizationReading reading, SpeechAct act, SpeakerTie tie)
        {
            if (!tie.IsRead || !act.IsAddressedTo(tie.Listener))
            {
                reading._readings[DialogueReadings.Relationship] = DialogueReadings.Absent;
                return;
            }

            reading._readings[DialogueReadings.Relationship] =
                tie.IsTied ? Snake(tie.Kind.ToString()) : "none";
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

        /// <summary>
        /// <c>InConfidence</c> becomes <c>in_confidence</c>. The enum names are the authority, and
        /// <see cref="DialogueSlug"/> is the single rule that turns one into the other -
        /// <see cref="DialogueReadings"/> builds the values content may name with the identical
        /// call, so what a reading says and what content may match on come from one place.
        /// </summary>
        private static string Snake(string name) => DialogueSlug.Of(name);
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
        /// Old business the speaker is entitled to bring up <em>and</em> willing to bring up with
        /// this listener, when the caller has selected one (BQ-081). Null asks for no reference
        /// back at all, which is what most lines are.
        ///
        /// It is a reference to a recorded event and never a retelling of it: what reaches wording
        /// is the kind of material and where its other party is standing, and nothing that could
        /// assert what happened. Selecting it is <c>CallbackHooks</c>' - including the whole of
        /// whether this speaker may know it - so a request cannot be the place a callback is
        /// invented, and <see cref="WhyNot"/> refuses one that belongs to somebody else.
        ///
        /// <b>A permit rather than a hook, on purpose.</b> Remembering something and being willing
        /// to say it to the person opposite are different questions with different answers, and the
        /// second one has a listener in it. Taking the clearance instead of the material means the
        /// second question cannot be skipped by a caller who did not think to ask it:
        /// <c>CallbackDisclosure.Permit</c> is the only thing that makes one, it makes it by asking
        /// <c>Disclosure</c>, and <see cref="WhyNot"/> refuses a permit that was withheld or was
        /// cleared for somebody this act does not address.
        /// </summary>
        public CallbackPermit Callback { get; set; }

        /// <summary>
        /// What the speaker is feeling strongly enough to be heard doing it (BQ-146), when the
        /// caller read it. <see cref="SpeakerFeeling.None"/> asks for no emotional constraint at
        /// all, which is what a calm speaker and an unread one both produce.
        ///
        /// Like <see cref="Tone"/> and unlike <see cref="Decision"/>, it narrows which fragment
        /// says the point and can never change which point is said. Nothing here writes back: the
        /// profile it came from is the world's, and reading it is not touching it.
        /// </summary>
        public SpeakerFeeling Feeling { get; set; } = SpeakerFeeling.None;

        /// <summary>
        /// What the speaker is to the person being addressed (BQ-146), when the caller read the
        /// graph. <see cref="SpeakerTie.Unread"/> asks for no relational constraint.
        ///
        /// <see cref="WhyNot"/> refuses a tie read for somebody the act does not address, for the
        /// reason it refuses a callback cleared for the wrong listener: a line authored for a
        /// friend, said to a stranger because the tie was measured against a third party, is
        /// wording asserting a relationship the world never held.
        /// </summary>
        public SpeakerTie Tie { get; set; } = SpeakerTie.Unread;

        /// <summary>
        /// The hook wording may actually read, or null when there is none it may.
        ///
        /// Every path into the fragment pool goes through this rather than through
        /// <see cref="Callback"/>, so a candidate listing - which does not run
        /// <see cref="WhyNot"/> - cannot see material the request would have been refused for.
        /// </summary>
        internal CallbackHook Recalled
        {
            get
            {
                if (Callback == null || !Callback.Allowed || Callback.Hook == null || Act == null)
                {
                    return null;
                }

                return Callback.Hook.Recaller == Act.Speaker
                       && Act.Addressees.Count == 1
                       && Act.IsAddressedTo(Callback.Listener)
                    ? Callback.Hook
                    : null;
            }
        }

        /// <summary>
        /// The stream the choices are drawn from. Only forked, never advanced, so the same
        /// semantic state and the same seed produce the same line however many other lines were
        /// realized in between.
        /// </summary>
        public DeterministicRng Rng { get; set; }

        /// <summary>
        /// Why this request could not be realized, or an empty string when it can be.
        ///
        /// Every refusal here is the same refusal: the caller has described a situation the
        /// semantic layer never produced. A realizer that quietly picked one of the two speakers,
        /// worded a decision about one claim as an act about another, or spoke a memory the speaker
        /// would have kept from the person opposite, would be inventing the missing half in prose -
        /// which is exactly the failure this layer exists to make impossible. Refusing rather than
        /// dropping the offending part matters just as much: a line that silently came out without
        /// the callback would leave the caller believing a permission question had been answered
        /// when it had only been discarded.
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
            if (Callback != null && Callback.Hook == null)
            {
                return "the callback permit names no material";
            }

            if (Callback != null && Callback.Hook.Recaller != Act.Speaker)
            {
                return "the callback belongs to somebody other than the speaker";
            }

            // The last two, and the ones that keep the knowledge gate from being spent as the
            // willingness gate. A permit is a clearance to say a particular thing to a particular
            // person: it has to be that person and only them - willingness was weighed against one
            // listener, and an act addressed to several would spend the clearance in front of
            // people nobody weighed it against - and it has to have been granted. Wording is the
            // wrong place to discover either, so the request is refused rather than quietly worded
            // without the callback.
            if (Callback != null && (Act.Addressees.Count != 1 || !Act.IsAddressedTo(Callback.Listener)))
            {
                return "the callback was cleared for somebody other than the person being addressed";
            }

            if (Callback != null && !Callback.Allowed)
            {
                return "the speaker would not bring this up with this listener";
            }

            // BQ-146's one refusal, and the same refusal as the callback's: a tie is directed and
            // is measured against one person, so wording it at somebody else would let a line
            // authored for a friend be said to a stranger.
            if (Tie.IsRead && !Act.IsAddressedTo(Tie.Listener))
            {
                return "the relationship was read against somebody the act does not address";
            }

            return string.Empty;
        }
    }
}
