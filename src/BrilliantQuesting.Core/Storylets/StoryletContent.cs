using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.Storylets
{
    public static class StoryletContent
    {
        public static IReadOnlyList<StoryletDefinition> LoadDefinitions(ContentBundle bundle, out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            List<StoryletDefinition> definitions = new List<StoryletDefinition>();
            List<ContentDiagnostic> problems = new List<ContentDiagnostic>();
            if (bundle == null)
            {
                diagnostics = problems.AsReadOnly();
                return definitions.AsReadOnly();
            }

            for (int i = 0; i < bundle.Records.Count; i++)
            {
                ContentRecord record = bundle.Records[i];
                if (!string.Equals(record.Kind, "storylet", StringComparison.Ordinal))
                {
                    continue;
                }

                StoryletDefinition definition;
                ContentDiagnostic diagnostic;
                if (TryRead(record, out definition, out diagnostic))
                {
                    definitions.Add(definition);
                }
                else
                {
                    problems.Add(diagnostic);
                }
            }

            diagnostics = problems.AsReadOnly();
            return definitions.AsReadOnly();
        }

        public static StoryletEngine CreateEngine(ContentBundle bundle, out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            StoryletEngine engine = new StoryletEngine();
            IReadOnlyList<StoryletDefinition> definitions = LoadDefinitions(bundle, out diagnostics);
            for (int i = 0; i < definitions.Count; i++)
            {
                engine.Register(definitions[i]);
            }

            return engine;
        }

        private static bool TryRead(ContentRecord record, out StoryletDefinition definition, out ContentDiagnostic diagnostic)
        {
            definition = null;
            diagnostic = null;

            if (record.Payload == null || record.Payload.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, "payload", "Storylet payload must be an object.");
                return false;
            }

            // The first gate, and the one that matters most for what this file is for. Every
            // string in a storylet payload is an id, a tag or the name of something in a closed
            // vocabulary - so a value with a space or a full stop in it is authored prose, in the
            // one file that must never contain any. Catching it here means "storylets reference
            // meaning, they do not contain dialogue" is enforced rather than reviewed for.
            if (!NothingReadsAsProse(record, out diagnostic))
            {
                return false;
            }

            StoryletDefinition built = new StoryletDefinition(record.Id);
            if (!ReadStringArray(record, "situationTags", built.SituationTags, out diagnostic)
                || !ReadStringArray(record, "toneTags", built.ToneTags, out diagnostic)
                || !ReadRoles(record, "requiredRoles", built.RequiredRoles, out diagnostic)
                || !ReadRoles(record, "optionalRoles", built.OptionalRoles, out diagnostic)
                || !ReadPreconditions(record, built, out diagnostic)
                || !ReadStringItems(record, "resolutions", delegate(string id) { built.Resolutions.Add(new StoryletResolution(id)); }, out diagnostic)
                || !ReadBeats(record, built, out diagnostic)
                || !ReadStringItems(record, "consequenceHooks", delegate(string id) { built.ConsequenceHooks.Add(new StoryletConsequenceHook(id)); }, out diagnostic))
            {
                return false;
            }

            if (built.RequiredRoles.Count == 0)
            {
                diagnostic = Invalid(record, "requiredRoles", "Storylet must declare at least one required role.");
                return false;
            }

            if (built.Beats.Count == 0)
            {
                diagnostic = Invalid(record, "beats", "Storylet must declare at least one beat.");
                return false;
            }

            if (!RoutesAreSound(record, built, out diagnostic))
            {
                return false;
            }

            definition = built;
            return true;
        }

        private static bool ReadStringArray(ContentRecord record, string name, List<string> values, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            JsonValue json = record.Payload[name];
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, name, name + " must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                if (json.Items[i].Kind != JsonKind.String || string.IsNullOrWhiteSpace(json.Items[i].StringValue))
                {
                    diagnostic = Invalid(record, name + "[" + i + "]", name + " entries must be non-empty strings.");
                    return false;
                }

                values.Add(json.Items[i].StringValue);
            }

            return true;
        }

        private static bool ReadStringItems(ContentRecord record, string name, Action<string> add, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            JsonValue json = record.Payload[name];
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, name, name + " must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                string value = StringItem(json.Items[i]);
                if (string.IsNullOrWhiteSpace(value))
                {
                    diagnostic = Invalid(record, name + "[" + i + "]", name + " entries must name an id.");
                    return false;
                }

                add(value);
            }

            return true;
        }

        private static bool ReadRoles(ContentRecord record, string name, List<StoryletRole> roles, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            JsonValue json = record.Payload[name];
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, name, name + " must be an array.");
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, name + "[" + i + "]", "Role entries must be objects.");
                    return false;
                }

                string id = item.GetString("id", null);
                string source = item.GetString("source", null);
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostic = Invalid(record, name + "[" + i + "].id", "Role id is required.");
                    return false;
                }

                if (!seen.Add(id))
                {
                    diagnostic = Invalid(record, name + "[" + i + "].id", "Role id is duplicated: " + id + ".");
                    return false;
                }

                StoryletRoleSource parsed;
                if (!TryParseRoleSource(source, out parsed))
                {
                    diagnostic = Invalid(record, name + "[" + i + "].source", "Role source is unknown: " + source + ".");
                    return false;
                }

                roles.Add(new StoryletRole(id, parsed));
            }

            return true;
        }

        private static bool ReadPreconditions(ContentRecord record, StoryletDefinition definition, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            JsonValue json = record.Payload["preconditions"];
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, "preconditions", "preconditions must be an array.");
                return false;
            }

            HashSet<string> roleIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.RequiredRoles.Count; i++)
            {
                roleIds.Add(definition.RequiredRoles[i].Id);
            }

            for (int i = 0; i < definition.OptionalRoles.Count; i++)
            {
                roleIds.Add(definition.OptionalRoles[i].Id);
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, "preconditions[" + i + "]", "Precondition entries must be objects.");
                    return false;
                }

                string kind = item.GetString("kind", null);
                StoryletPrecondition precondition;
                if (!TryReadPrecondition(record, item, kind, roleIds, i, out precondition, out diagnostic))
                {
                    return false;
                }

                definition.Preconditions.Add(precondition);
            }

            return true;
        }

        private static bool TryReadPrecondition(
            ContentRecord record,
            JsonValue item,
            string kind,
            HashSet<string> roleIds,
            int index,
            out StoryletPrecondition precondition,
            out ContentDiagnostic diagnostic)
        {
            precondition = null;
            diagnostic = null;

            switch (kind)
            {
                case "FactBelongsToThread":
                    precondition = StoryletPrecondition.FactBelongsToThread();
                    return true;
                case "FocusPredicate":
                    string predicate = item.GetString("value", null);
                    if (string.IsNullOrWhiteSpace(predicate))
                    {
                        diagnostic = Invalid(record, "preconditions[" + index + "].value", "FocusPredicate requires value.");
                        return false;
                    }

                    precondition = StoryletPrecondition.FocusPredicate(predicate);
                    return true;
                case "FocusTruth":
                    string truth = item.GetString("value", null);
                    TruthState parsedTruth;
                    if (!Enum.TryParse(truth, false, out parsedTruth))
                    {
                        diagnostic = Invalid(record, "preconditions[" + index + "].value", "FocusTruth is unknown: " + truth + ".");
                        return false;
                    }

                    precondition = StoryletPrecondition.FocusTruth(parsedTruth);
                    return true;
                case "RoleKnowsFocus":
                    return TryReadRolePrecondition(record, item, roleIds, index, StoryletPrecondition.RoleKnowsFocus, out precondition, out diagnostic);
                case "RoleCanProveFocus":
                    return TryReadRolePrecondition(record, item, roleIds, index, StoryletPrecondition.RoleCanProveFocus, out precondition, out diagnostic);
                case "RoleAlive":
                    return TryReadRolePrecondition(record, item, roleIds, index, StoryletPrecondition.RoleAlive, out precondition, out diagnostic);
                default:
                    diagnostic = Invalid(record, "preconditions[" + index + "].kind", "Precondition kind is unknown: " + kind + ".");
                    return false;
            }
        }

        private static bool TryReadRolePrecondition(
            ContentRecord record,
            JsonValue item,
            HashSet<string> roleIds,
            int index,
            Func<string, StoryletPrecondition> create,
            out StoryletPrecondition precondition,
            out ContentDiagnostic diagnostic)
        {
            precondition = null;
            diagnostic = null;
            string role = item.GetString("role", item.GetString("value", null));
            if (string.IsNullOrWhiteSpace(role))
            {
                diagnostic = Invalid(record, "preconditions[" + index + "].role", "Role precondition requires role.");
                return false;
            }

            if (!roleIds.Contains(role))
            {
                diagnostic = Invalid(record, "preconditions[" + index + "].role", "Precondition references undefined role: " + role + ".");
                return false;
            }

            precondition = create(role);
            return true;
        }

        // -- BQ-146: routed beats -------------------------------------------------------------------

        /// <summary>
        /// The keys a storylet may never carry, whatever it calls them.
        ///
        /// Belt as well as braces: <see cref="NothingReadsAsProse"/> already refuses a value that
        /// reads as a sentence, and a one-word <c>text:</c> would slip past it. Naming the keys
        /// makes the intent legible to whoever tries - a field for a line of dialogue is not a
        /// field this schema is missing, it is the thing the schema exists to not have.
        /// </summary>
        private static readonly string[] ProseKeys = { "text", "line", "say", "says", "dialogue", "speech", "prose", "description" };

        private static IReadOnlyList<string> _actionIds;

        /// <summary>
        /// Every verb the action library actually registers, so a beat cannot advertise a mechanic
        /// nobody built. Derived from the registry rather than listed here, for the reason every
        /// vocabulary in this codebase is derived: a hand-kept copy would be the thing that lets a
        /// storylet promise the player an action that does not exist.
        /// </summary>
        private static IReadOnlyList<string> ActionIds()
        {
            if (_actionIds != null)
            {
                return _actionIds;
            }

            List<string> ids = new List<string>();
            IReadOnlyList<NarrativeAction> actions = StandardActions.CreateRegistry().Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                ids.Add(actions[i].Id);
            }

            _actionIds = ids;
            return _actionIds;
        }

        /// <summary>
        /// Whether any string anywhere in the payload reads as authored English rather than as a
        /// reference.
        ///
        /// The rule is deliberately blunt, because a subtle version of it would be arguable and an
        /// arguable rule is one that erodes. Ids, tags, role names, act names, event names and
        /// check questions are all slugs; a sentence is not. So a string containing whitespace or
        /// sentence punctuation is refused wherever it appears, and the diagnostic says which path
        /// it was at.
        /// </summary>
        private static bool NothingReadsAsProse(ContentRecord record, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            return Scan(record, "payload", record.Payload, ref diagnostic);
        }

        private static bool Scan(ContentRecord record, string path, JsonValue value, ref ContentDiagnostic diagnostic)
        {
            if (value == null)
            {
                return true;
            }

            if (value.Kind == JsonKind.String)
            {
                string problem = WhyProse(value.StringValue);
                if (problem != null)
                {
                    diagnostic = Invalid(record, path,
                        "A storylet may reference meaning but may not contain prose: " + problem + ".");
                    return false;
                }

                return true;
            }

            if (value.Kind == JsonKind.Array)
            {
                for (int i = 0; i < value.Items.Count; i++)
                {
                    if (!Scan(record, path + "[" + i + "]", value.Items[i], ref diagnostic))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (value.Kind != JsonKind.Object)
            {
                return true;
            }

            for (int i = 0; i < value.Members.Count; i++)
            {
                string key = value.Members[i].Key;
                for (int j = 0; j < ProseKeys.Length; j++)
                {
                    if (string.Equals(key, ProseKeys[j], StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostic = Invalid(record, path + "." + key,
                            "A storylet may not carry authored wording; say which act is meant and let the dialogue library word it.");
                        return false;
                    }
                }

                if (!Scan(record, path + "." + key, value.Members[i].Value, ref diagnostic))
                {
                    return false;
                }
            }

            return true;
        }

        private static string WhyProse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    return "\"" + value + "\" contains whitespace";
                }

                if (c == '.' && i + 1 < value.Length && !char.IsLetterOrDigit(value[i + 1]))
                {
                    return "\"" + value + "\" reads as a sentence";
                }

                if (c == ',' || c == '?' || c == '!' || c == ';' || c == ':' || c == '\'' || c == '"')
                {
                    return "\"" + value + "\" contains sentence punctuation";
                }
            }

            return null;
        }

        private static bool ReadBeats(ContentRecord record, StoryletDefinition definition, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            JsonValue json = record.Payload["beats"];
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, "beats", "beats must be an array.");
                return false;
            }

            HashSet<string> roleIds = RoleIds(definition);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                string where = "beats[" + i + "]";

                // A bare string is still a beat: the storylets that shipped before routing existed
                // are lists of labels, and they keep meaning exactly what they meant.
                string id = StringItem(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostic = Invalid(record, where, "beats entries must name an id.");
                    return false;
                }

                if (!seen.Add(id))
                {
                    diagnostic = Invalid(record, where, "Beat id is duplicated: " + id + ".");
                    return false;
                }

                StoryletBeat beat = new StoryletBeat(id);
                if (item.Kind == JsonKind.Object && !ReadBeatBody(record, where + "(" + id + ")", item, beat, roleIds, out diagnostic))
                {
                    return false;
                }

                definition.Beats.Add(beat);
            }

            return true;
        }

        private static bool ReadBeatBody(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            beat.SpeakerRole = json.GetString("speaker", string.Empty);
            beat.ListenerRole = json.GetString("listener", string.Empty);

            if (!KnownRole(record, where + ".speaker", beat.SpeakerRole, roleIds, out diagnostic)
                || !KnownRole(record, where + ".listener", beat.ListenerRole, roleIds, out diagnostic))
            {
                return false;
            }

            if (!ReadIntentions(record, where, json["intentions"], beat, roleIds, out diagnostic))
            {
                return false;
            }

            // Somebody has to be spoken to. An intention with no listener cannot be composed into
            // an act at all - SpeechAct refuses an act addressed to nobody - so accepting it here
            // would mean authoring a beat that is guaranteed to fall silent.
            if (beat.Intentions.Count > 0 && (beat.SpeakerRole.Length == 0 || beat.ListenerRole.Length == 0))
            {
                diagnostic = Invalid(record, where, "A beat with intentions needs both a speaker and a listener.");
                return false;
            }

            // Order matters: the check has to be read before consequences and routes, because both
            // may only turn on a check the beat actually makes, and intentions before both, because
            // both may only name an act somebody here can decide to say.
            if (!ReadBeatRequires(record, where, json["requires"], beat, roleIds, out diagnostic)
                || !ReadBeatCheck(record, where, json["check"], beat, roleIds, out diagnostic)
                || !ReadIntersections(record, where, json["playerIntersections"], beat, out diagnostic)
                || !ReadBeatConsequences(record, where, json["consequences"], beat, roleIds, out diagnostic)
                || !ReadRoutes(record, where, json["routes"], beat, out diagnostic))
            {
                return false;
            }

            return true;
        }

        private static bool ReadIntentions(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array || json.Items.Count == 0)
            {
                diagnostic = Invalid(record, where + ".intentions", "intentions must be a non-empty array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                string at = where + ".intentions[" + i + "]";
                string actName = item.Kind == JsonKind.String ? item.StringValue : item.GetString("act", null);

                SpeechActType act;
                if (!TryAct(actName, out act))
                {
                    diagnostic = Invalid(record, at, "Semantic act is unknown: " + actName + ".");
                    return false;
                }

                string referent = item.Kind == JsonKind.Object ? item.GetString("referent", string.Empty) : string.Empty;
                if (!KnownRole(record, at + ".referent", referent, roleIds, out diagnostic))
                {
                    return false;
                }

                BeatContentSource content = BeatContentSource.Focus;
                string contentName = item.Kind == JsonKind.Object ? item.GetString("content", null) : null;
                if (contentName != null && !TryContent(contentName, out content))
                {
                    diagnostic = Invalid(record, at + ".content", "Beat content must be focus, focus_object or none.");
                    return false;
                }

                // An act whose profile is about a proposition has to be given one. Letting it
                // through would author a move the semantic layer will refuse at run time, and the
                // scene would fall silent for a reason nobody could see from the file.
                SpeechActProfile profile = SpeechActProfile.Of(act);
                if (profile.Content == SpeechActContentRule.PropositionRequired && content != BeatContentSource.Focus)
                {
                    diagnostic = Invalid(record, at + ".content", act + " is about a claim, so its content must be focus.");
                    return false;
                }

                if (profile.Referent == SpeechActReferentRule.MustNotBeSpeaker && referent.Length == 0)
                {
                    diagnostic = Invalid(record, at + ".referent", act + " must name the role it is about.");
                    return false;
                }

                beat.Intentions.Add(new BeatIntention(act, referent, content));
            }

            return true;
        }

        private static bool ReadBeatRequires(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where + ".requires", "requires must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                if (item.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, where + ".requires[" + i + "]", "Precondition entries must be objects.");
                    return false;
                }

                StoryletPrecondition precondition;
                if (!TryReadPrecondition(record, item, item.GetString("kind", null), roleIds, i, out precondition, out diagnostic))
                {
                    return false;
                }

                beat.Requires.Add(precondition);
            }

            return true;
        }

        private static bool ReadBeatCheck(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, where + ".check", "check must be a map.");
                return false;
            }

            string profileId = json.GetString("profile", null);
            if (ProceduralCheckProfiles.ById(profileId) == null)
            {
                diagnostic = Invalid(record, where + ".check.profile", "Check profile is unknown: " + profileId + ".");
                return false;
            }

            string actor = json.GetString("actor", string.Empty);
            string target = json.GetString("target", string.Empty);
            if (!KnownRole(record, where + ".check.actor", actor, roleIds, out diagnostic)
                || !KnownRole(record, where + ".check.target", target, roleIds, out diagnostic))
            {
                return false;
            }

            if (actor.Length == 0)
            {
                diagnostic = Invalid(record, where + ".check.actor", "A check needs somebody attempting it.");
                return false;
            }

            // The rule that keeps dice out of the atmosphere business: somebody has to be able to
            // name what was in doubt. A check with no question is a roll for its own sake.
            string question = json.GetString("question", string.Empty);
            if (question.Length == 0)
            {
                diagnostic = Invalid(record, where + ".check.question", "A check must name the uncertainty it settles.");
                return false;
            }

            beat.Check = new BeatCheck(profileId, actor, target, question);
            return true;
        }

        private static bool ReadIntersections(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where + ".playerIntersections", "playerIntersections must be an array.");
                return false;
            }

            IReadOnlyList<string> known = ActionIds();
            for (int i = 0; i < json.Items.Count; i++)
            {
                string id = json.Items[i].Kind == JsonKind.String ? json.Items[i].StringValue : null;
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostic = Invalid(record, where + ".playerIntersections[" + i + "]", "playerIntersections entries must name an action.");
                    return false;
                }

                bool found = false;
                for (int j = 0; j < known.Count; j++)
                {
                    found = found || string.Equals(known[j], id, StringComparison.Ordinal);
                }

                if (!found)
                {
                    diagnostic = Invalid(record, where + ".playerIntersections[" + i + "]",
                        "No such action: " + id + ". A storylet may only offer verbs the action library actually registers.");
                    return false;
                }

                beat.PlayerIntersections.Add(id);
            }

            return true;
        }

        private static bool ReadBeatConsequences(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where + ".consequences", "consequences must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                string at = where + ".consequences[" + i + "]";
                string hook = item.Kind == JsonKind.String ? item.StringValue : item.GetString("hook", null);
                if (string.IsNullOrWhiteSpace(hook))
                {
                    diagnostic = Invalid(record, at, "A consequence must name a hook.");
                    return false;
                }

                WorldEventType? eventType = null;
                string actor = string.Empty;
                string target = string.Empty;
                double magnitude = 0.5;
                BeatTrigger when = BeatTrigger.Always;
                SpeechActType? act = null;

                if (item.Kind == JsonKind.Object)
                {
                    string eventName = item.GetString("event", null);
                    if (eventName != null)
                    {
                        WorldEventType parsed;
                        if (!Enum.TryParse(eventName, false, out parsed) || !Enum.IsDefined(typeof(WorldEventType), parsed))
                        {
                            diagnostic = Invalid(record, at + ".event", "World event type is unknown: " + eventName + ".");
                            return false;
                        }

                        eventType = parsed;
                    }

                    actor = item.GetString("actor", string.Empty);
                    target = item.GetString("target", string.Empty);
                    magnitude = item.GetNumber("magnitude", 0.5);

                    if (!KnownRole(record, at + ".actor", actor, roleIds, out diagnostic)
                        || !KnownRole(record, at + ".target", target, roleIds, out diagnostic))
                    {
                        return false;
                    }

                    if (eventType.HasValue && actor.Length == 0)
                    {
                        diagnostic = Invalid(record, at + ".actor", "A consequence that records history must name who did it.");
                        return false;
                    }

                    if (magnitude < 0.0 || magnitude > 1.0)
                    {
                        diagnostic = Invalid(record, at + ".magnitude", "Consequence magnitude must be between 0 and 1.");
                        return false;
                    }

                    string whenName = item.GetString("when", "always");
                    if (!TryTrigger(whenName, out when))
                    {
                        diagnostic = Invalid(record, at + ".when", "Consequence trigger is unknown: " + whenName + ".");
                        return false;
                    }

                    if (when != BeatTrigger.Always && when != BeatTrigger.Spoke && when != BeatTrigger.Silent && beat.Check == null)
                    {
                        diagnostic = Invalid(record, at + ".when", "This consequence turns on a check the beat does not make.");
                        return false;
                    }

                    string actName = item.GetString("act", null);
                    if (actName != null)
                    {
                        SpeechActType parsed;
                        if (!TryAct(actName, out parsed))
                        {
                            diagnostic = Invalid(record, at + ".act", "Semantic act is unknown: " + actName + ".");
                            return false;
                        }

                        if (!Offers(beat, parsed))
                        {
                            diagnostic = Invalid(record, at + ".act",
                                "This consequence records " + parsed + ", which nobody here can decide to say.");
                            return false;
                        }

                        act = parsed;
                    }
                }

                beat.Consequences.Add(new BeatConsequence(hook, eventType, actor, target, magnitude, when, act));
            }

            return true;
        }

        private static bool ReadRoutes(
            ContentRecord record,
            string where,
            JsonValue json,
            StoryletBeat beat,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (json == null)
            {
                return true;
            }

            if (json.Kind != JsonKind.Array)
            {
                diagnostic = Invalid(record, where + ".routes", "routes must be an array.");
                return false;
            }

            for (int i = 0; i < json.Items.Count; i++)
            {
                JsonValue item = json.Items[i];
                string at = where + ".routes[" + i + "]";
                if (item.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, at, "Route entries must be maps.");
                    return false;
                }

                BeatTrigger trigger;
                string when = item.GetString("when", "always");
                if (!TryTrigger(when, out trigger))
                {
                    diagnostic = Invalid(record, at + ".when",
                        "Route trigger is unknown: " + when + ". Use always, spoke, silent, check_pass, check_fail, check_critical_pass or check_critical_fail.");
                    return false;
                }

                if (trigger != BeatTrigger.Always && trigger != BeatTrigger.Spoke && trigger != BeatTrigger.Silent && beat.Check == null)
                {
                    diagnostic = Invalid(record, at + ".when", "This beat routes on a check it does not make.");
                    return false;
                }

                SpeechActType? act = null;
                string actName = item.GetString("act", null);
                if (actName != null)
                {
                    SpeechActType parsed;
                    if (!TryAct(actName, out parsed))
                    {
                        diagnostic = Invalid(record, at + ".act", "Semantic act is unknown: " + actName + ".");
                        return false;
                    }

                    if (!Offers(beat, parsed))
                    {
                        diagnostic = Invalid(record, at + ".act",
                            "This beat routes on " + parsed + ", which nobody here can decide to say.");
                        return false;
                    }

                    act = parsed;
                }

                string to = item.GetString("to", string.Empty);
                string ends = item.GetString("ends", string.Empty);
                if (to.Length != 0 && ends.Length != 0)
                {
                    diagnostic = Invalid(record, at, "A route either continues or ends; it cannot do both.");
                    return false;
                }

                if (to.Length == 0 && ends.Length == 0)
                {
                    diagnostic = Invalid(record, at, "A route must name the next beat or the state it ends in.");
                    return false;
                }

                beat.Routes.Add(new BeatRoute(trigger, act, to, ends));
            }

            return true;
        }

        /// <summary>
        /// The graph checks, run once every beat is read: no route into nothing, no resolution
        /// nobody declared, no beat nothing can reach, and no scene that cannot stop.
        ///
        /// All four are statically detectable and all four are the kind of defect that would
        /// otherwise be found by a player rather than by a build. The last one is the important
        /// one: a scene with no terminal path is a scene that runs to its step bound and stops for
        /// a reason nobody wrote.
        /// </summary>
        private static bool RoutesAreSound(ContentRecord record, StoryletDefinition definition, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (!definition.IsRouted)
            {
                return true;
            }

            HashSet<string> resolutions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Resolutions.Count; i++)
            {
                resolutions.Add(definition.Resolutions[i].Id);
            }

            HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> terminating = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < definition.Beats.Count; i++)
            {
                StoryletBeat beat = definition.Beats[i];
                for (int j = 0; j < beat.Routes.Count; j++)
                {
                    BeatRoute route = beat.Routes[j];
                    if (route.IsTerminal)
                    {
                        if (!resolutions.Contains(route.Ends))
                        {
                            diagnostic = Invalid(record, "beats." + beat.Id + ".routes",
                                "Route ends in a state the storylet does not declare: " + route.Ends + ".");
                            return false;
                        }

                        terminating.Add(beat.Id);
                        reached.Add(route.Ends);
                        continue;
                    }

                    if (definition.Beat(route.To) == null)
                    {
                        diagnostic = Invalid(record, "beats." + beat.Id + ".routes",
                            "Route names a beat that does not exist: " + route.To + ".");
                        return false;
                    }

                    reachable.Add(route.To);
                }

                if (beat.Routes.Count == 0)
                {
                    terminating.Add(beat.Id);
                }
            }

            // The first beat is where a scene starts, so it is reachable by definition.
            reachable.Add(definition.Beats[0].Id);
            for (int i = 0; i < definition.Beats.Count; i++)
            {
                if (!reachable.Contains(definition.Beats[i].Id))
                {
                    diagnostic = Invalid(record, "beats." + definition.Beats[i].Id,
                        "No route reaches this beat.");
                    return false;
                }
            }

            // Reverse reachability: a beat can stop if it stops itself or routes to something that
            // can. Iterating to a fixed point is enough - the graph is small and this is a build.
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < definition.Beats.Count; i++)
                {
                    StoryletBeat beat = definition.Beats[i];
                    if (terminating.Contains(beat.Id))
                    {
                        continue;
                    }

                    for (int j = 0; j < beat.Routes.Count; j++)
                    {
                        if (!beat.Routes[j].IsTerminal && terminating.Contains(beat.Routes[j].To))
                        {
                            grew = terminating.Add(beat.Id) || grew;
                        }
                    }
                }
            }

            for (int i = 0; i < definition.Beats.Count; i++)
            {
                if (!terminating.Contains(definition.Beats[i].Id))
                {
                    diagnostic = Invalid(record, "beats." + definition.Beats[i].Id,
                        "No path from this beat ever ends the scene.");
                    return false;
                }
            }

            // A resolution nothing routes to is a promise the storylet does not keep: it reads on the
            // page as an outcome the scene supports, and no play can ever reach it.
            for (int i = 0; i < definition.Resolutions.Count; i++)
            {
                if (!reached.Contains(definition.Resolutions[i].Id))
                {
                    diagnostic = Invalid(record, "resolutions",
                        "No route ends in this state: " + definition.Resolutions[i].Id + ".");
                    return false;
                }
            }

            return AntecedentsAreReachable(record, definition, out diagnostic);
        }

        /// <summary>
        /// Several acts are unintelligible without something to respond to - an evasion of nothing
        /// is just talk, and a refusal of nothing is not a refusal. A beat that offers one must be
        /// reachable from a beat whose speaker was addressing *this* beat's speaker, or the
        /// intention can never be composed and the author has written a move nobody can make.
        ///
        /// Statically detectable, and worth detecting: the failure is silent at run time. The
        /// intention is simply dropped, the beat falls back on whatever else it offered, and the
        /// scene looks like it is working.
        /// </summary>
        private static bool AntecedentsAreReachable(ContentRecord record, StoryletDefinition definition, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            for (int i = 0; i < definition.Beats.Count; i++)
            {
                StoryletBeat beat = definition.Beats[i];
                SpeechActType needs = default(SpeechActType);
                bool needsAntecedent = false;
                for (int j = 0; j < beat.Intentions.Count && !needsAntecedent; j++)
                {
                    SpeechActProfile profile = SpeechActProfile.Of(beat.Intentions[j].Act);
                    needsAntecedent = profile != null && profile.AntecedentRequired;
                    needs = beat.Intentions[j].Act;
                }

                if (!needsAntecedent || beat.SpeakerRole.Length == 0)
                {
                    continue;
                }

                bool answerable = false;
                for (int j = 0; j < definition.Beats.Count && !answerable; j++)
                {
                    StoryletBeat earlier = definition.Beats[j];
                    if (earlier == beat
                        || !string.Equals(earlier.SpeakerRole, beat.ListenerRole, StringComparison.Ordinal)
                        || !string.Equals(earlier.ListenerRole, beat.SpeakerRole, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    for (int k = 0; k < earlier.Routes.Count && !answerable; k++)
                    {
                        answerable = string.Equals(earlier.Routes[k].To, beat.Id, StringComparison.Ordinal);
                    }
                }

                if (!answerable)
                {
                    diagnostic = Invalid(record, "beats." + beat.Id + ".intentions",
                        needs + " responds to something, and no beat that routes here has anybody speaking to "
                        + beat.SpeakerRole + ".");
                    return false;
                }
            }

            return true;
        }

        private static bool Offers(StoryletBeat beat, SpeechActType act)
        {
            for (int i = 0; i < beat.Intentions.Count; i++)
            {
                if (beat.Intentions[i].Act == act)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> RoleIds(StoryletDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.RequiredRoles.Count; i++)
            {
                ids.Add(definition.RequiredRoles[i].Id);
            }

            for (int i = 0; i < definition.OptionalRoles.Count; i++)
            {
                ids.Add(definition.OptionalRoles[i].Id);
            }

            return ids;
        }

        private static bool KnownRole(
            ContentRecord record,
            string path,
            string roleId,
            HashSet<string> roleIds,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            if (string.IsNullOrEmpty(roleId) || roleIds.Contains(roleId))
            {
                return true;
            }

            diagnostic = Invalid(record, path, "References undefined role: " + roleId + ".");
            return false;
        }

        /// <summary>
        /// Acts are named in content by the slug the dialogue layer already uses for them, so one
        /// spelling of <c>accuse</c> serves a fragment condition and a beat intention alike.
        /// </summary>
        private static bool TryAct(string name, out SpeechActType act)
        {
            act = default(SpeechActType);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            IReadOnlyList<SpeechActType> vocabulary = SpeechActProfile.Vocabulary;
            for (int i = 0; i < vocabulary.Count; i++)
            {
                if (string.Equals(Slug(vocabulary[i].ToString()), name, StringComparison.Ordinal))
                {
                    act = vocabulary[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryContent(string name, out BeatContentSource content)
        {
            switch (name)
            {
                case "focus":
                    content = BeatContentSource.Focus;
                    return true;
                case "focus_object":
                    content = BeatContentSource.FocusObject;
                    return true;
                case "none":
                    content = BeatContentSource.None;
                    return true;
                default:
                    content = BeatContentSource.Focus;
                    return false;
            }
        }

        private static bool TryTrigger(string name, out BeatTrigger trigger)
        {
            switch (name)
            {
                case "always":
                    trigger = BeatTrigger.Always;
                    return true;
                case "spoke":
                    trigger = BeatTrigger.Spoke;
                    return true;
                case "silent":
                    trigger = BeatTrigger.Silent;
                    return true;
                case "check_pass":
                    trigger = BeatTrigger.CheckPass;
                    return true;
                case "check_fail":
                    trigger = BeatTrigger.CheckFail;
                    return true;
                case "check_critical_pass":
                    trigger = BeatTrigger.CheckCriticalPass;
                    return true;
                case "check_critical_fail":
                    trigger = BeatTrigger.CheckCriticalFail;
                    return true;
                default:
                    trigger = BeatTrigger.Always;
                    return false;
            }
        }

        private static string Slug(string name)
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

        private static string StringItem(JsonValue item)
        {
            if (item.Kind == JsonKind.String)
            {
                return item.StringValue;
            }

            if (item.Kind == JsonKind.Object)
            {
                return item.GetString("id", null);
            }

            return null;
        }

        private static bool TryParseRoleSource(string value, out StoryletRoleSource source)
        {
            return Enum.TryParse(value, false, out source);
        }

        private static ContentDiagnostic Invalid(ContentRecord record, string path, string message)
        {
            return new ContentDiagnostic("content.storylet.invalid", record.Id + "." + path, message);
        }
    }
}
