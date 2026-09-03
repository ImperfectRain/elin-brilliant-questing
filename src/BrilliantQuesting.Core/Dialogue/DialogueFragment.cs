using System;
using System.Collections.Generic;
using BrilliantQuesting.Continuity;

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
        /// A reference back: to what is already in the conversation, or to something that happened
        /// long enough ago to be worth remarking on.
        ///
        /// The first is grounded in the act's own antecedent. The second is grounded in a
        /// <c>CallbackHook</c> the caller selected (BQ-081), which is a reference to a recorded
        /// event and carries the whole of whether this speaker may know it - so wording still
        /// invents nothing, it is simply no longer the case that the state is missing.
        /// </summary>
        Callback = 3,

        /// <summary>What the surroundings do to the saying of it - who else is within earshot.</summary>
        Context = 4,

        /// <summary>What is said after the point.</summary>
        Closer = 5
    }

    /// <summary>
    /// The one translation between a name the semantic layer holds and the slug wording reads it
    /// by, and the reason "closed vocabulary" and "derived vocabulary" are the same thing here.
    ///
    /// <c>InConfidence</c> becomes <c>in_confidence</c>. There is nothing clever in that; what
    /// matters is that <see cref="RealizationReading"/> and <see cref="DialogueReadings"/> both
    /// call it, so the value a reading produces and the value content is allowed to name cannot
    /// be produced by two rules that disagree. Anything that would drift is a value one of them
    /// invented, and neither of them invents any.
    /// </summary>
    internal static class DialogueSlug
    {
        public static string Of(string name)
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

        /// <summary>
        /// Every value of a semantic enum, as slugs, plus whatever the reading adds that is not a
        /// member of it - <c>absent</c> for a key nothing was given for, <c>none</c> for an act
        /// that answers nothing.
        /// </summary>
        public static HashSet<string> Every<TEnum>(params string[] alsoReads)
            where TEnum : struct
        {
            HashSet<string> slugs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < alsoReads.Length; i++)
            {
                slugs.Add(alsoReads[i]);
            }

            Array values = Enum.GetValues(typeof(TEnum));
            for (int i = 0; i < values.Length; i++)
            {
                slugs.Add(Of(values.GetValue(i).ToString()));
            }

            return slugs;
        }
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
        /// What old business the speaker has to hand, as a <see cref="CallbackKind"/> (BQ-081).
        ///
        /// It says which kind of material is in play - never what happened, or when. A fragment
        /// conditioned on it is one authored for referring back to a promise or an injury in
        /// general, which is why no event ever needs prose written for it.
        /// </summary>
        public const string Callback = "callback";

        /// <summary>
        /// Where the other side of that old business is standing now: the person being spoken to,
        /// somebody else, or nobody at all.
        ///
        /// Separated from <see cref="Callback"/> because "after what I did for you" and "after
        /// what I did for your brother" are different claims, and a fragment that could be chosen
        /// for either would be wording deciding which of them is true.
        /// </summary>
        public const string CallbackParty = "callback_party";

        /// <summary>
        /// How the speaker comes to the old business, as a <see cref="CallbackRoute"/>: they did
        /// it, it was done to them, they watched, or they were told.
        ///
        /// The third and last thing wording learns about a hook, and it is there for the same
        /// reason the second is. "After what I did for you" and "after what you did for me" are
        /// opposite claims about one event, and a pool that admitted both would let the words
        /// decide which way round it was.
        /// </summary>
        public const string CallbackRoute = "callback_route";

        /// <summary>
        /// Nothing was given to read: no decision was passed, or the caller supplied no fact. Not
        /// the same as a decision that came out empty - <c>strategy: nothing_to_disclose</c> is a
        /// speaker who holds nothing, and this is a wording layer that was told nothing.
        /// </summary>
        public const string Absent = "absent";

        private static readonly Dictionary<string, HashSet<string>> Allowed = BuildAllowed();

        /// <summary>
        /// Every key, so content validation and the reading itself cannot come apart. Read off
        /// <see cref="BuildAllowed"/> rather than listed again, for the same reason the values are.
        /// </summary>
        public static IReadOnlyList<string> Vocabulary { get; } = new List<string>(Allowed.Keys);

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

        /// <summary>
        /// What each key may read as, derived from the semantic layer wherever the semantic layer
        /// has a say.
        ///
        /// The keys whose values are a semantic enum take them from that enum rather than from a
        /// second list written out here. That is not tidiness: a hand-kept copy is a copy somebody
        /// has to remember to update, and BQ-083 proved it - <see cref="SpeechActType.Promise"/>
        /// and <see cref="SpeechActDirection.CommitsToAction"/> entered the vocabulary of meaning
        /// and never reached this table, so a well-formed promise could not be authored a wording
        /// and the layer refused a line for an act the simulation was perfectly happy to produce.
        /// Derivation makes that particular failure unrepresentable.
        ///
        /// Deriving is not collapsing. Nothing here decides what an act means or adds a value the
        /// semantic layer does not already hold: this is wording being told what meanings exist,
        /// in exactly the vocabulary they exist in, and the arrow only ever points this way.
        ///
        /// The keys whose values are not an enum stay written out, because there is nothing to
        /// derive them from - they are readings <see cref="RealizationReading"/> computes about
        /// the shape of an act (who the referent is relative to the room, whether one person is
        /// being spoken to or several) rather than a name the semantic layer already has.
        /// </summary>
        private static Dictionary<string, HashSet<string>> BuildAllowed()
        {
            Dictionary<string, HashSet<string>> allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            allowed[Act] = DialogueSlug.Every<SpeechActType>();
            allowed[Stance] = DialogueSlug.Every<SpeechActStance>();
            allowed[Direction] = DialogueSlug.Every<SpeechActDirection>();
            allowed[Strategy] = DialogueSlug.Every<DisclosureStrategy>(Absent);
            allowed[Depth] = DialogueSlug.Every<DisclosureDepth>(Absent);

            // No `falsify`. The tactic axis reaches wording only for the ways of not answering
            // that a listener is meant to be able to hear, so this one value is subtracted from
            // the derived set - deliberately, by name, and not by the table having quietly never
            // heard of it. Every other tactic the semantic layer grows arrives here on its own.
            HashSet<string> tactics = DialogueSlug.Every<DisclosureTactic>(Absent);
            tactics.Remove(DialogueSlug.Of(DisclosureTactic.Falsify.ToString()));
            allowed[Tactic] = tactics;

            allowed[Commitment] = Set(Absent, "unspoken", "hedged", "committed");
            allowed[HeldBack] = Set(Absent, "yes", "no");
            allowed[Referent] = Set("none", "speaker", "listener", "other");
            allowed[Claim] = Set("present", "absent");
            allowed[ClaimPredicate] = null;

            // `none` on top of the act vocabulary: an act that responds to nothing still reads.
            allowed[Reply] = DialogueSlug.Every<SpeechActType>("none");
            allowed[Audience] = Set("one", "several");

            // The kind slugs are `CallbackKind`'s own names, so the enum stays the authority and
            // content cannot condition on a kind the simulation does not derive.
            allowed[Callback] = DialogueSlug.Every<CallbackKind>(Absent);
            allowed[CallbackParty] = Set(Absent, "none", "speaker", "listener", "other");
            allowed[CallbackRoute] = DialogueSlug.Every<CallbackRoute>(Absent);
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

        /// <summary>
        /// The other side of the callback the caller supplied (BQ-081), named from the cast.
        ///
        /// Nothing else about the recalled event has a placeholder. What it was is carried by the
        /// fragment that was authored for that kind of business, and a placeholder that phrased
        /// the event itself would be prose standing in for history.
        /// </summary>
        public const string Recalled = "recalled";

        public static IReadOnlyList<string> Vocabulary { get; } = new[] { Speaker, Listener, Referent, Subject, Matter, Recalled };

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
        /// Free tags for the layers that constrain selection beyond tone. BQ-075's voice reached
        /// <see cref="ToneTags"/> instead, so <see cref="FitsVocabulary"/> (BQ-076) is this
        /// vocabulary's first reader: a tag in <see cref="DialogueVocabulary"/> marks a fragment as
        /// lived-context flavour for one identity domain. <see cref="FitsManner"/> (BQ-077) is the
        /// second: a tag in <see cref="DialogueManners"/> marks a fragment as speaking in a way a
        /// personal line can rule out. The two vocabularies are disjoint and are read separately,
        /// and a tag in neither is still carried and left alone, the same way an unmarked fragment
        /// is.
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

        /// <summary>
        /// Whether it suits the lived-context vocabulary an identity makes plausible (BQ-076).
        ///
        /// A fragment carrying none of <see cref="DialogueVocabulary"/>'s tags has no occupational
        /// opinion and fits whatever is requested, including nothing - the same neutral default
        /// every fragment had before this step, and true of any tag this vocabulary does not know
        /// (BQ-077's, for instance). A fragment that does carry one of this vocabulary's tags fits
        /// only when the request names it: unlike <see cref="FitsTone"/>, asking for nothing here
        /// excludes such a fragment rather than admitting it, because a flavoured line let through
        /// by an unread identity would be exactly the guessed vocabulary BQ-145 already refuses to
        /// derive.
        /// </summary>
        public bool FitsVocabulary(IReadOnlyList<string> requested)
        {
            bool hasVocabularyTag = false;
            for (int i = 0; i < Tags.Count; i++)
            {
                if (!DialogueVocabulary.IsVocabulary(Tags[i]))
                {
                    continue;
                }

                hasVocabularyTag = true;
                if (requested != null)
                {
                    for (int j = 0; j < requested.Count; j++)
                    {
                        if (string.Equals(Tags[i], requested[j], StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }

            return !hasVocabularyTag;
        }

        /// <summary>
        /// Whether a speaker whose lines rule out these manners can say it (BQ-077).
        ///
        /// The mirror image of <see cref="FitsVocabulary"/> in both senses. What is passed is what
        /// is <em>forbidden</em> rather than what is wanted, so a fragment carrying no manner tag
        /// - which is nearly all of them - is always eligible and an empty list rules nothing out;
        /// and the list is a set of rulings already taken elsewhere rather than a request, because
        /// a manner is only ever removed by a line that is currently holding.
        ///
        /// A fragment carrying a manner tag outside <see cref="DialogueManners"/>'s vocabulary is
        /// left alone here, exactly as a non-vocabulary tag is left alone by
        /// <see cref="FitsVocabulary"/>: neither reader claims a tag it does not own.
        /// </summary>
        public bool FitsManner(IReadOnlyList<string> forbidden)
        {
            if (forbidden == null || forbidden.Count == 0 || Tags.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < Tags.Count; i++)
            {
                if (!DialogueManners.IsManner(Tags[i]))
                {
                    continue;
                }

                for (int j = 0; j < forbidden.Count; j++)
                {
                    if (string.Equals(Tags[i], forbidden[j], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a scene's weirdness budget still admits it (BQ-079). A null budget asks for no
        /// constraint at all, the same neutral default every other Fits* narrowing offers a caller
        /// who does not track one.
        /// </summary>
        public bool FitsWeirdness(WeirdnessBudget budget)
        {
            return budget == null || budget.IsAdmissible(this);
        }

        public override string ToString() => Id + " [" + Position + "]";
    }
}
