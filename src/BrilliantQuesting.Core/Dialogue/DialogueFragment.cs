using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Which part of a line a fragment is (CD §18).
    ///
    /// Six slots, in the order they are spoken, and the order is the whole of the grammar. There
    /// is no parse tree here and no sentence model: a line is a short sequence of authored
    /// phrases, and the reason to keep it that small is that anything larger starts to look like
    /// a system that could decide what a sentence means.
    /// </summary>
    public enum FragmentPosition
    {
        /// <summary>What is said before the point. Optional, and usually empty.</summary>
        Opener = 0,

        /// <summary>
        /// The point. Exactly one per line, and the only slot that may not be empty: a line whose
        /// core could not be filled is not a quieter line, it is a line nobody has words for, and
        /// <see cref="DialogueRealizer"/> refuses rather than assembling one from the trimmings.
        /// </summary>
        Core = 1,

        /// <summary>How the speaker holds the point: firmly, reluctantly, having said their piece.</summary>
        Modifier = 2,

        /// <summary>
        /// A reference back to what is already in the conversation. Grounded in the act's own
        /// antecedent and nothing else - the relationship and history callbacks CD §18 sketches
        /// need state this layer does not have and must not invent.
        /// </summary>
        Callback = 3,

        /// <summary>What the surroundings do to the saying of it - who else is within earshot.</summary>
        Context = 4,

        /// <summary>What is said after the point.</summary>
        Closer = 5
    }

    /// <summary>
    /// The closed vocabulary of things a fragment may be chosen on, and the only view of the
    /// semantic layer that wording ever gets.
    ///
    /// Every key here is a <em>reading</em> of a <see cref="SpeechAct"/> and, where one was given,
    /// the <see cref="DisclosureDecision"/> behind it. None of them is a new fact: there is
    /// nothing in this table that the semantic layer did not already decide, which is what stops a
    /// fragment condition from becoming a quiet second place where meaning is made.
    ///
    /// The vocabulary is closed and validated at load time on purpose. A misspelt key that
    /// silently matched nothing would be an unusable fragment nobody noticed; a misspelt key that
    /// silently matched everything would be a line said in the wrong situation forever.
    /// </summary>
    public static class DialogueReadings
    {
        /// <summary>Which act it is. The one condition every core fragment must declare.</summary>
        public const string Act = "act";

        /// <summary>What the act does to its proposition: affirms, denies, questions, none.</summary>
        public const string Stance = "stance";

        /// <summary>Which way it moves information or compliance.</summary>
        public const string Direction = "direction";

        /// <summary>How forthcoming the speaker decided to be (BQ-071).</summary>
        public const string Strategy = "strategy";

        /// <summary>How much of what they hold comes with it (BQ-072).</summary>
        public const string Depth = "depth";

        /// <summary>
        /// What was done instead of answering (BQ-073).
        ///
        /// <c>falsify</c> is deliberately not a value a fragment may name; see
        /// <see cref="RealizationReading"/> for why the wording layer is never told that the
        /// speaker is lying.
        /// </summary>
        public const string Tactic = "tactic";

        /// <summary>Whether the speaker will stand behind what they say.</summary>
        public const string Commitment = "commitment";

        /// <summary>Whether they are knowingly giving less than they hold - still every word true.</summary>
        public const string HeldBack = "held_back";

        /// <summary>Who the content is about, relative to the people in the act.</summary>
        public const string Referent = "referent";

        /// <summary>Whether the act carries a claim at all.</summary>
        public const string Claim = "claim";

        /// <summary>
        /// Which predicate the claim uses, when the caller supplied the fact. Open rather than
        /// enumerated, because the predicate ontology is <c>FactPredicates</c>' to grow and a
        /// second copy of it here would be a second opinion about what claims exist.
        /// </summary>
        public const string ClaimPredicate = "claim_predicate";

        /// <summary>Which act this one responds to, or <c>none</c>.</summary>
        public const string Reply = "reply";

        /// <summary>Whether it is said to one person or to several.</summary>
        public const string Audience = "audience";

        /// <summary>
        /// Nothing was given to read: no decision was passed, or the caller supplied no fact. Not
        /// the same as a decision that came out empty - <c>strategy: nothing_to_disclose</c> is a
        /// speaker who holds nothing, and this is a wording layer that was told nothing.
        /// </summary>
        public const string Absent = "absent";

        private static readonly Dictionary<string, HashSet<string>> Allowed = BuildAllowed();

        /// <summary>Every key, so content validation and the reading itself cannot come apart.</summary>
        public static IReadOnlyList<string> Vocabulary { get; } = new[]
        {
            Act, Stance, Direction, Strategy, Depth, Tactic, Commitment, HeldBack,
            Referent, Claim, ClaimPredicate, Reply, Audience
        };

        public static bool IsKey(string key) => key != null && Allowed.ContainsKey(key);

        /// <summary>
        /// Whether a value is one this key can ever read. True for any non-empty value on the open
        /// keys, so the predicate ontology stays <c>FactPredicates</c>'.
        /// </summary>
        public static bool IsValue(string key, string value)
        {
            if (string.IsNullOrEmpty(value) || !Allowed.TryGetValue(key, out HashSet<string> values))
            {
                return false;
            }

            return values == null || values.Contains(value);
        }

        private static Dictionary<string, HashSet<string>> BuildAllowed()
        {
            Dictionary<string, HashSet<string>> allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            allowed[Act] = Set("ask", "answer", "accuse", "deny", "admit", "request", "refuse", "threaten", "apologize", "gossip", "evade");
            allowed[Stance] = Set("affirms", "denies", "questions", "none");
            allowed[Direction] = Set("seeks_information", "gives_information", "seeks_action", "withholds_action", "withholds_information", "repairs");
            allowed[Strategy] = Set(Absent, "nothing_to_disclose", "refuse", "deflect", "hedge", "disclose");
            allowed[Depth] = Set(Absent, "nothing", "gist", "detail", "in_confidence");

            // No `falsify`. The tactic axis reaches wording only for the ways of not answering
            // that a listener is meant to be able to hear.
            allowed[Tactic] = Set(Absent, "none", "decline", "change_subject", "answer_elsewhere");
            allowed[Commitment] = Set(Absent, "unspoken", "hedged", "committed");
            allowed[HeldBack] = Set(Absent, "yes", "no");
            allowed[Referent] = Set("none", "speaker", "listener", "other");
            allowed[Claim] = Set("present", "absent");
            allowed[ClaimPredicate] = null;
            allowed[Reply] = Set("none", "ask", "answer", "accuse", "deny", "admit", "request", "refuse", "threaten", "apologize", "gossip", "evade");
            allowed[Audience] = Set("one", "several");
            return allowed;
        }

        private static HashSet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
    }

    /// <summary>
    /// The closed set of things a fragment may name in its text.
    ///
    /// Names of people the act already involves, and the label the claim already carries. There is
    /// no placeholder for the claim itself, and that absence is deliberate: putting a proposition
    /// into words needs a predicate lexicon, and a lexicon that phrased predicates would be a
    /// second place where what a fact says gets decided. A fragment that wants to word a
    /// particular kind of claim says so with a <see cref="DialogueReadings.ClaimPredicate"/>
    /// condition and writes the sentence itself.
    ///
    /// A placeholder that cannot be filled from the semantic input makes its fragment ineligible
    /// rather than resolving to something. An unnamed person is not "someone".
    /// </summary>
    public static class DialogueSlots
    {
        public const string Speaker = "speaker";
        public const string Listener = "listener";
        public const string Referent = "referent";

        /// <summary>Who the claim is about, which is not always who the act names.</summary>
        public const string Subject = "subject";

        /// <summary>The label the claim or the binding already carries: "silver ring", "12000 orens".</summary>
        public const string Matter = "matter";

        public static IReadOnlyList<string> Vocabulary { get; } = new[] { Speaker, Listener, Referent, Subject, Matter };

        public static bool IsSlot(string name)
        {
            for (int i = 0; i < Vocabulary.Count; i++)
            {
                if (string.Equals(Vocabulary[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Every slot the text names, in order of appearance, or null when the text is malformed -
        /// an unclosed brace, or a name outside the vocabulary. Malformed is a content error and
        /// is reported at load; nothing repairs a template at realization time.
        /// </summary>
        public static IReadOnlyList<string> Read(string text, out string problem)
        {
            List<string> slots = new List<string>();
            problem = null;
            string source = text ?? string.Empty;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != '{')
                {
                    continue;
                }

                int close = source.IndexOf('}', i + 1);
                if (close < 0)
                {
                    problem = "unclosed placeholder";
                    return null;
                }

                string name = source.Substring(i + 1, close - i - 1);
                if (!IsSlot(name))
                {
                    problem = "unknown placeholder {" + name + "}";
                    return null;
                }

                if (!slots.Contains(name))
                {
                    slots.Add(name);
                }

                i = close;
            }

            return slots;
        }
    }

    /// <summary>
    /// The tone tags a fragment may carry, and the ones a caller may ask for.
    ///
    /// Small on purpose. Tone is how something is said, and BQ-075's voice profiles are what will
    /// actually choose between these; shipping a large tonal taxonomy now would be authoring the
    /// vocabulary of a system that does not exist yet.
    /// </summary>
    public static class DialogueTones
    {
        public const string Plain = "plain";
        public const string Warm = "warm";
        public const string Cold = "cold";
        public const string Curt = "curt";
        public const string Formal = "formal";
        public const string Wary = "wary";
        public const string Wry = "wry";

        public static IReadOnlyList<string> Vocabulary { get; } = new[] { Plain, Warm, Cold, Curt, Formal, Wary, Wry };

        public static bool IsTone(string tag)
        {
            for (int i = 0; i < Vocabulary.Count; i++)
            {
                if (string.Equals(Vocabulary[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// One condition on a reading: this key must read as one of these values.
    ///
    /// Any-of rather than one value, because the alternative is three near-identical fragments
    /// differing only in which rung they answer to. Every-of is not offered: two conditions on the
    /// same key would be a contradiction rather than a refinement.
    /// </summary>
    public sealed class FragmentRequirement
    {
        private static readonly string[] Nothing = new string[0];

        public FragmentRequirement(string key, IReadOnlyList<string> values)
        {
            Key = key ?? string.Empty;
            Values = values ?? Nothing;
        }

        public string Key { get; }

        public IReadOnlyList<string> Values { get; }

        public bool IsMetBy(string reading)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                if (string.Equals(Values[i], reading, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return Key + "=" + string.Join("|", Values);
        }
    }

    /// <summary>
    /// One authored phrase, and the conditions under which somebody would say it (CD §18).
    ///
    /// A fragment is wording and only wording. It carries no proposition, names no person the act
    /// did not already involve, and asserts nothing the semantic layer did not already decide:
    /// what it can be chosen on is <see cref="DialogueReadings"/>, and what it can name is
    /// <see cref="DialogueSlots"/>, and both of those are readings of state that exists before any
    /// of this runs. That is the whole of the "expression may express meaning and may never create
    /// it" rule, made structural rather than promised.
    ///
    /// Fragments are content (D-pipeline), not code: they are compiled from <c>content/</c> into
    /// the bundle beside storylets, which is why there is no authored English anywhere in this
    /// file and why adding a way of saying something is a content change.
    /// </summary>
    public sealed class DialogueFragment
    {
        private static readonly string[] NoTags = new string[0];
        private static readonly FragmentRequirement[] NoConditions = new FragmentRequirement[0];

        public DialogueFragment(
            string id,
            FragmentPosition position,
            string text,
            IReadOnlyList<FragmentRequirement> requires,
            IReadOnlyList<FragmentRequirement> forbids,
            IReadOnlyList<string> toneTags,
            IReadOnlyList<string> tags,
            string repetitionGroup,
            IReadOnlyList<string> slots)
        {
            Id = id ?? string.Empty;
            Position = position;
            Text = text ?? string.Empty;
            Requires = requires ?? NoConditions;
            Forbids = forbids ?? NoConditions;
            ToneTags = toneTags ?? NoTags;
            Tags = tags ?? NoTags;
            RepetitionGroup = repetitionGroup ?? string.Empty;
            Slots = slots ?? NoTags;
        }

        public string Id { get; }

        public FragmentPosition Position { get; }

        /// <summary>The phrase, with <see cref="DialogueSlots"/> placeholders unresolved.</summary>
        public string Text { get; }

        /// <summary>Every condition that must hold. All of them, or the fragment is not eligible.</summary>
        public IReadOnlyList<FragmentRequirement> Requires { get; }

        /// <summary>
        /// Conditions that disqualify it. CD §18's forbidden facts, kept because the negative case
        /// is often the short one: "anything but an admission" is one line to author and four to
        /// enumerate.
        /// </summary>
        public IReadOnlyList<FragmentRequirement> Forbids { get; }

        /// <summary>
        /// How it sounds. Empty means it fits any tone rather than none, so an unmarked fragment
        /// stays available while the tonal vocabulary grows around it.
        /// </summary>
        public IReadOnlyList<string> ToneTags { get; }

        /// <summary>
        /// Free tags for the layers that will constrain selection later - voice (BQ-075),
        /// occupational vocabulary (BQ-076), what a character will not say (BQ-077). Declared and
        /// carried; nothing in this step reads them, and nothing in this step should.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// What this fragment counts as for the purpose of not repeating oneself. The seam
        /// BQ-078 needs and the whole of what BQ-074 owes it: tracking recent use is a
        /// conversation's business, and there is no conversation here.
        /// </summary>
        public string RepetitionGroup { get; }

        /// <summary>Which placeholders the text names. Computed once at load, never re-parsed.</summary>
        public IReadOnlyList<string> Slots { get; }

        /// <summary>Whether every condition holds and no disqualifier does.</summary>
        public bool Fits(RealizationReading reading)
        {
            if (reading == null)
            {
                return false;
            }

            for (int i = 0; i < Requires.Count; i++)
            {
                if (!Requires[i].IsMetBy(reading.Value(Requires[i].Key)))
                {
                    return false;
                }
            }

            for (int i = 0; i < Forbids.Count; i++)
            {
                if (Forbids[i].IsMetBy(reading.Value(Forbids[i].Key)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether it suits a requested tone. An unmarked fragment suits every tone, and a caller
        /// who asks for none is asking for no tonal constraint at all rather than for silence.
        /// </summary>
        public bool FitsTone(IReadOnlyList<string> requested)
        {
            if (ToneTags.Count == 0 || requested == null || requested.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < ToneTags.Count; i++)
            {
                for (int j = 0; j < requested.Count; j++)
                {
                    if (string.Equals(ToneTags[i], requested[j], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override string ToString() => Id + " [" + Position + "]";
    }
}
