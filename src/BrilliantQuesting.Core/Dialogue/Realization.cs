using System;
using System.Collections.Generic;
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
    /// <see cref="Fact"/> the caller passed and the names the caller supplied. There is no fifth
    /// source, which is why no wording can carry a meaning the simulation did not already have.
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
            ReadSlots(reading, act, claim, cast ?? DialogueCast.Anonymous);
            return reading;
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

        private static void ReadSlots(RealizationReading reading, SpeechAct act, Fact claim, DialogueCast cast)
        {
            Fill(reading, DialogueSlots.Speaker, cast.NameOf(act.Speaker));
            Fill(reading, DialogueSlots.Listener, act.Addressees.Count == 1 ? cast.NameOf(act.Addressees[0]) : null);
            Fill(reading, DialogueSlots.Referent, cast.NameOf(act.Referent));
            Fill(reading, DialogueSlots.Subject, claim == null ? null : cast.NameOf(claim.Subject));

            string matter = claim == null || string.IsNullOrWhiteSpace(claim.Value) ? act.Content.Purpose : claim.Value;
            Fill(reading, DialogueSlots.Matter, matter);
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

            return string.Empty;
        }
    }
}
