using System;
using System.Collections.Generic;
using BrilliantQuesting.Content;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Fragments come out of the content bundle, like storylets do.
    ///
    /// The alternative - a table of English in Core - was the thing to avoid. There is already one
    /// authored-content pipeline with a compiler, a bundle format, freshness checking and
    /// diagnostics that point at a file and a line; a second one living in C# string literals
    /// would mean two ways to add a line, two ways to break one, and a headless simulation
    /// assembly that has to be recompiled to change a comma.
    ///
    /// Loading is strict for the same reason composing an act is. A fragment with a misspelt
    /// condition is not a fragment that says a bit less - it is one that says the wrong thing in
    /// the wrong situation forever, and nobody would find it. So every condition key, every
    /// condition value, every tone tag, every idiolect mark and every placeholder is checked
    /// against the closed vocabularies here, and a record that fails is reported rather than
    /// partially loaded.
    /// </summary>
    public static class DialogueFragmentContent
    {
        /// <summary>The record kind. One record carries a set of fragments, not a single phrase.</summary>
        public const string Kind = "dialogueFragments";

        public static IReadOnlyList<DialogueFragment> LoadFragments(ContentBundle bundle, out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            List<DialogueFragment> fragments = new List<DialogueFragment>();
            List<ContentDiagnostic> problems = new List<ContentDiagnostic>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (bundle == null)
            {
                diagnostics = problems.AsReadOnly();
                return fragments.AsReadOnly();
            }

            for (int i = 0; i < bundle.Records.Count; i++)
            {
                ContentRecord record = bundle.Records[i];
                if (!string.Equals(record.Kind, Kind, StringComparison.Ordinal))
                {
                    continue;
                }

                Read(record, ids, fragments, problems);
            }

            diagnostics = problems.AsReadOnly();
            return fragments.AsReadOnly();
        }

        /// <summary>Every fragment in the bundle, indexed. Diagnostics are the caller's to look at.</summary>
        public static DialogueFragmentLibrary CreateLibrary(ContentBundle bundle, out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            DialogueFragmentLibrary library = new DialogueFragmentLibrary();
            IReadOnlyList<DialogueFragment> fragments = LoadFragments(bundle, out diagnostics);
            for (int i = 0; i < fragments.Count; i++)
            {
                library.Register(fragments[i]);
            }

            return library;
        }

        private static void Read(
            ContentRecord record,
            HashSet<string> ids,
            List<DialogueFragment> fragments,
            List<ContentDiagnostic> problems)
        {
            JsonValue entries = record.Payload["fragments"];
            if (entries == null || entries.Kind != JsonKind.Array || entries.Items.Count == 0)
            {
                problems.Add(Invalid(record, "fragments", "A fragment record must carry a non-empty fragments array."));
                return;
            }

            for (int i = 0; i < entries.Items.Count; i++)
            {
                DialogueFragment fragment;
                ContentDiagnostic diagnostic;
                if (TryRead(record, entries.Items[i], i, ids, out fragment, out diagnostic))
                {
                    fragments.Add(fragment);
                }
                else
                {
                    problems.Add(diagnostic);
                }
            }
        }

        private static bool TryRead(
            ContentRecord record,
            JsonValue json,
            int index,
            HashSet<string> ids,
            out DialogueFragment fragment,
            out ContentDiagnostic diagnostic)
        {
            fragment = null;
            string where = "fragments[" + index + "]";
            if (json == null || json.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, where, "A fragment must be a map.");
                return false;
            }

            string id = json.GetString("id", null);
            if (string.IsNullOrWhiteSpace(id))
            {
                diagnostic = Invalid(record, where, "A fragment must have an id.");
                return false;
            }

            where = id;
            if (!ids.Add(id))
            {
                diagnostic = Invalid(record, where, "Fragment id is duplicated: " + id + ".");
                return false;
            }

            FragmentPosition position;
            if (!TryPosition(json.GetString("position", null), out position))
            {
                diagnostic = Invalid(record, where, "Fragment position must be one of opener, core, modifier, callback, context, closer.");
                return false;
            }

            string text = json.GetString("text", null);
            if (string.IsNullOrWhiteSpace(text))
            {
                diagnostic = Invalid(record, where, "A fragment must have text.");
                return false;
            }

            string problem;
            IReadOnlyList<string> slots = DialogueSlots.Read(text, out problem);
            if (slots == null)
            {
                diagnostic = Invalid(record, where, "Fragment text has an " + problem + ".");
                return false;
            }

            List<FragmentRequirement> requires = new List<FragmentRequirement>();
            List<FragmentRequirement> forbids = new List<FragmentRequirement>();
            if (!TryConditions(record, where, json["requires"], "requires", requires, out diagnostic)
                || !TryConditions(record, where, json["forbids"], "forbids", forbids, out diagnostic))
            {
                return false;
            }

            // A core fragment is the sentence that carries the point, so it has to know which
            // point. Without this, one wording could be chosen for an act it was never written
            // for - a refusal said as an answer - which is the single worst thing this layer
            // could do, because the meaning would be right and the words would be a lie about it.
            if (position == FragmentPosition.Core && !Declares(requires, DialogueReadings.Act))
            {
                diagnostic = Invalid(record, where, "A core fragment must declare which act it says.");
                return false;
            }

            // BQ-147. A fragment that names somebody has to say where they are standing, because
            // the placeholder resolves to a name and a name said in the third person is a claim
            // about who is not in the conversation. "{referent} took it" with no `referent`
            // condition is eligible when the referent is the person being spoken to, and then the
            // line says the listener's name to their face about a third party who is them - not a
            // wording preference, a sentence that is false about the room. The same holds for the
            // claim's subject and for the other side of a callback.
            //
            // Mechanical on purpose, and deliberately not a check on the English: this asks only
            // whether the author declared the reading that decides who the name belongs to. Which
            // values they declared is theirs - a line may be written for a listener - but leaving
            // the question unanswered is the one thing that cannot be right.
            if (!NamesArePlaced(where, slots, requires, record, out diagnostic))
            {
                return false;
            }

            List<string> tones = new List<string>();
            if (!TryTones(record, where, json["tone"], tones, out diagnostic))
            {
                return false;
            }

            List<string> idiolect = new List<string>();
            if (!TryIdiolect(record, where, json["idiolect"], idiolect, out diagnostic))
            {
                return false;
            }

            List<string> voice = new List<string>();
            if (!TryVoice(record, where, json["voice"], position, tones, idiolect, voice, out diagnostic))
            {
                return false;
            }

            List<string> tags = new List<string>();
            if (!TryTags(record, where, json["tags"], tags, out diagnostic))
            {
                return false;
            }

            // Unmarked means utility, which is what every fragment was before the vocabulary
            // existed. A misspelt tier is rejected rather than defaulted: silently downgrading a
            // line somebody marked "protected" to the least protected tier is exactly the failure
            // mode the vocabulary is closed to prevent.
            string memorability = json.GetString("memorability", DialogueMemorability.Utility);
            if (!DialogueMemorability.IsMemorability(memorability))
            {
                diagnostic = Invalid(record, where, "Unknown memorability: " + memorability + ".");
                return false;
            }

            fragment = new DialogueFragment(
                id,
                position,
                text.Trim(),
                requires.ToArray(),
                forbids.ToArray(),
                tones.ToArray(),
                idiolect.ToArray(),
                tags.ToArray(),
                json.GetString("repetitionGroup", string.Empty),
                slots,
                memorability,
                voice.ToArray());
            diagnostic = null;
            return true;
        }

        private static bool TryConditions(
            ContentRecord record,
            string where,
            JsonValue json,
            string field,
            List<FragmentRequirement> into,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, where, field + " must be a map of reading to value.");
                return false;
            }

            for (int i = 0; i < json.Members.Count; i++)
            {
                string key = json.Members[i].Key;
                if (!DialogueReadings.IsKey(key))
                {
                    diagnostic = Invalid(record, where, "Unknown fragment condition: " + key + ".");
                    return false;
                }

                List<string> values = new List<string>();
                if (!TryValues(record, where, key, json.Members[i].Value, values, out diagnostic))
                {
                    return false;
                }

                into.Add(new FragmentRequirement(key, values.ToArray()));
            }

            return true;
        }

        private static bool TryValues(
            ContentRecord record,
            string where,
            string key,
            JsonValue json,
            List<string> values,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json != null && json.Kind == JsonKind.String)
            {
                return TryValue(record, where, key, json.StringValue, values, out diagnostic);
            }

            if (json == null || json.Kind != JsonKind.Array || json.Items.Count == 0)
            {
                diagnostic = Invalid(record, where, "Condition " + key + " needs a value or a non-empty list of values.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.String || !TryValue(record, where, key, item.StringValue, values, out diagnostic))
                {
                    diagnostic = diagnostic ?? Invalid(record, where, "Condition " + key + " must list strings.");
                    return false;
                }
            }

            return true;
        }

        private static bool TryValue(
            ContentRecord record,
            string where,
            string key,
            string value,
            List<string> values,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;

            // The one refusal in this file that is about the architecture rather than about a
            // typo. Wording is never selected on whether the speaker is lying: a fragment pool
            // that shifted when somebody falsified would put the tell in the words, and BQ-073
            // holds that a lie is catchable from what the listener knows and from nothing else.
            if (string.Equals(key, DialogueReadings.Tactic, StringComparison.Ordinal)
                && string.Equals(value, "falsify", StringComparison.Ordinal))
            {
                diagnostic = Invalid(record, where, "Wording may not be chosen on whether the speaker is lying.");
                return false;
            }

            if (!DialogueReadings.IsValue(key, value))
            {
                diagnostic = Invalid(record, where, "Condition " + key + " cannot read as " + value + ".");
                return false;
            }

            values.Add(value);
            return true;
        }

        private static bool TryTones(ContentRecord record, string where, JsonValue json, List<string> into, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where, "tone must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.String || !DialogueTones.IsTone(item.StringValue))
                {
                    diagnostic = Invalid(record, where, "Unknown tone tag.");
                    return false;
                }

                into.Add(item.StringValue);
            }

            return true;
        }

        /// <summary>
        /// The habits a phrase declares (BQ-142), against a closed vocabulary and against itself.
        ///
        /// Two refusals, and the second is the one worth having. An unknown tag is the ordinary
        /// typo refusal every other vocabulary here makes: a misspelt <c>figurative</c> would be a
        /// mark no voice can ever ask for and no author would find. Both poles of one axis is a
        /// contradiction rather than a refinement - the same reading <see cref="FragmentRequirement"/>
        /// already takes of two conditions on one key - and it is worse than useless: a fragment
        /// marked terse <em>and</em> expansive is refused by every voice with an opinion about
        /// length and admitted only by voices that were never going to narrow on it, so it would
        /// quietly disappear from exactly the pools it was written for.
        /// </summary>
        private static bool TryIdiolect(ContentRecord record, string where, JsonValue json, List<string> into, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where, "idiolect must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.String || !DialogueIdiolect.IsIdiolect(item.StringValue))
                {
                    diagnostic = Invalid(record, where, "Unknown idiolect tag.");
                    return false;
                }

                if (into.Contains(DialogueIdiolect.Opposite(item.StringValue)))
                {
                    diagnostic = Invalid(
                        record,
                        where,
                        "A fragment cannot be both " + DialogueIdiolect.Opposite(item.StringValue)
                            + " and " + item.StringValue + ".");
                    return false;
                }

                into.Add(item.StringValue);
            }

            return true;
        }

        /// <summary>
        /// The persistent voice traits a phrase requires of whoever says it (BQ-149), against the
        /// two vocabularies a <see cref="VoiceProfile"/> can request from and against the fragment
        /// itself.
        ///
        /// Four refusals, and three of them are the same refusal <see cref="TryIdiolect"/> already
        /// makes: an unknown tag is a demand no voice can ever satisfy, both poles of one axis is a
        /// contradiction rather than a refinement, and a demand contradicting the phrase's own
        /// <c>tone</c> or <c>idiolect</c> mark - a line marked warm that only a cold speaker may
        /// say - is a rule that fires never. All three fail silently rather than loudly if allowed
        /// through: the fragment simply disappears from every pool it was written for.
        ///
        /// The fourth is this field's own, and it is what keeps a demand from being able to take a
        /// line away rather than a way of saying it. Exactly one slot is required, and a core
        /// narrowed to nothing is a refused act (<c>D060</c>); every other slot is drawn against
        /// saying nothing and falls silent when its pool empties. So a core may not demand a trait
        /// at all, and the guarantee that "an unsupported combination falls back to neutral
        /// material" is structural rather than a property of how carefully the corpus was authored.
        /// </summary>
        private static bool TryVoice(
            ContentRecord record,
            string where,
            JsonValue json,
            FragmentPosition position,
            List<string> tones,
            List<string> idiolect,
            List<string> into,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where, "voice must be an array.");
                return false;
            }

            if (position == FragmentPosition.Core && json.Items.Count > 0)
            {
                diagnostic = Invalid(
                    record,
                    where,
                    "A core fragment may not demand a voice trait: the core is the one slot that "
                        + "cannot fall silent, so narrowing it on a speaker's habits would refuse the act.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.String || !DialogueVoiceTraits.IsTrait(item.StringValue))
                {
                    diagnostic = Invalid(record, where, "Unknown voice trait.");
                    return false;
                }

                string opposite = DialogueVoiceTraits.Opposite(item.StringValue);
                if (opposite != null
                    && (into.Contains(opposite) || tones.Contains(opposite) || idiolect.Contains(opposite)))
                {
                    diagnostic = Invalid(
                        record,
                        where,
                        "A fragment cannot demand " + item.StringValue + " and be " + opposite + ".");
                    return false;
                }

                into.Add(item.StringValue);
            }

            return true;
        }

        private static bool TryTags(ContentRecord record, string where, JsonValue json, List<string> into, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where, "tags must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.String || string.IsNullOrWhiteSpace(item.StringValue))
                {
                    diagnostic = Invalid(record, where, "tags must be non-empty strings.");
                    return false;
                }

                into.Add(item.StringValue);
            }

            return true;
        }

        /// <summary>
        /// Every placeholder that names a person, and the reading that says where that person is
        /// standing. Nothing else in <see cref="DialogueSlots"/> names anybody:
        /// <c>{speaker}</c> and <c>{listener}</c> are the two people the act already places, and
        /// <c>{matter}</c> is a label rather than a person.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] NamedPeople = new[]
        {
            new KeyValuePair<string, string>(DialogueSlots.Referent, DialogueReadings.Referent),
            new KeyValuePair<string, string>(DialogueSlots.Subject, DialogueReadings.Subject),
            new KeyValuePair<string, string>(DialogueSlots.Recalled, DialogueReadings.CallbackParty)
        };

        /// <summary>
        /// Whether every person this text names has a declared position in the conversation.
        /// </summary>
        private static bool NamesArePlaced(
            string where,
            IReadOnlyList<string> slots,
            List<FragmentRequirement> requires,
            ContentRecord record,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            for (int i = 0; i < NamedPeople.Length; i++)
            {
                string slot = NamedPeople[i].Key;
                string reading = NamedPeople[i].Value;
                if (!Contains(slots, slot) || Declares(requires, reading))
                {
                    continue;
                }

                diagnostic = Invalid(
                    record,
                    where,
                    "A fragment that names {" + slot + "} must declare " + reading
                        + ", so it cannot be said in the third person to the person it names.");
                return false;
            }

            return true;
        }

        private static bool Contains(IReadOnlyList<string> slots, string slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i], slot, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Declares(List<FragmentRequirement> requires, string key)
        {
            for (int i = 0; i < requires.Count; i++)
            {
                if (string.Equals(requires[i].Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPosition(string name, out FragmentPosition position)
        {
            switch (name)
            {
                case "opener":
                    position = FragmentPosition.Opener;
                    return true;
                case "core":
                    position = FragmentPosition.Core;
                    return true;
                case "modifier":
                    position = FragmentPosition.Modifier;
                    return true;
                case "callback":
                    position = FragmentPosition.Callback;
                    return true;
                case "context":
                    position = FragmentPosition.Context;
                    return true;
                case "closer":
                    position = FragmentPosition.Closer;
                    return true;
                default:
                    position = FragmentPosition.Core;
                    return false;
            }
        }

        private static ContentDiagnostic Invalid(ContentRecord record, string path, string message)
        {
            return new ContentDiagnostic("content.fragment.invalid", record.Id + "." + path, message);
        }
    }
}
