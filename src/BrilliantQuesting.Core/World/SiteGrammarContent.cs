using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// BQ-089. Reads curated location grammars out of the compiled content bundle.
    ///
    /// A place kind is a catalogue entry - a writer should be able to add "sewer refuge" without a
    /// build - so grammars are authored under `content/sites/` like storylets and fragments, and
    /// this is the reader the compiler validates through. Every refusal below is therefore a build
    /// error with a file name rather than a diagnostic nobody is watching at load
    /// (`content-pipeline.md §3`).
    ///
    /// The refusals are all one idea: a grammar may not describe a place the rest of the codebase
    /// would then have to guess about. A required room nothing required leads to, a way in that
    /// names a verb nobody built, a set of ways in that all wait on the same permission - each of
    /// those is a plan that reads fine and produces a site somebody has to make excuses for.
    /// </summary>
    public static class SiteGrammarContent
    {
        public const string Kind = "site-grammar";

        private static IReadOnlyList<string> _actionIds;

        public static IReadOnlyList<SiteGrammar> LoadGrammars(
            ContentBundle bundle,
            out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            List<SiteGrammar> grammars = new List<SiteGrammar>();
            List<ContentDiagnostic> problems = new List<ContentDiagnostic>();
            if (bundle == null)
            {
                diagnostics = problems.AsReadOnly();
                return grammars.AsReadOnly();
            }

            for (int i = 0; i < bundle.Records.Count; i++)
            {
                ContentRecord record = bundle.Records[i];
                if (!string.Equals(record.Kind, Kind, StringComparison.Ordinal))
                {
                    continue;
                }

                SiteGrammar grammar;
                ContentDiagnostic diagnostic;
                if (TryRead(record, out grammar, out diagnostic))
                {
                    grammars.Add(grammar);
                }
                else
                {
                    problems.Add(diagnostic);
                }
            }

            diagnostics = problems.AsReadOnly();
            return grammars.AsReadOnly();
        }

        public static SiteGrammarLibrary CreateLibrary(
            ContentBundle bundle,
            out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            return new SiteGrammarLibrary(LoadGrammars(bundle, out diagnostics));
        }

        private static bool TryRead(ContentRecord record, out SiteGrammar grammar, out ContentDiagnostic diagnostic)
        {
            grammar = null;
            diagnostic = null;

            if (record.Payload == null || record.Payload.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, "payload", "Site grammar payload must be an object.");
                return false;
            }

            // Every string in a grammar is an id, a kind or a name from a closed vocabulary. A
            // place's *name* comes from the matter that needed it, never from the kind, so a value
            // that is not a slug is either wording that does not belong here or a typo.
            if (!EverythingIsAToken(record, out diagnostic))
            {
                return false;
            }

            string siteType = record.Payload.GetString("siteType", null);
            if (string.IsNullOrWhiteSpace(siteType))
            {
                diagnostic = Invalid(record, "siteType", "Site grammar must say what kind of place it makes.");
                return false;
            }

            List<SiteNodeSpec> nodes = new List<SiteNodeSpec>();
            if (!ReadNodes(record, nodes, out diagnostic))
            {
                return false;
            }

            List<SiteRouteSpec> routes = new List<SiteRouteSpec>();
            if (!ReadRoutes(record, nodes, routes, out diagnostic))
            {
                return false;
            }

            SiteGrammar built = new SiteGrammar(
                record.Id,
                siteType,
                record.Payload.GetBool("restricted"),
                nodes,
                routes);

            if (!WaysInAreTwo(record, built, out diagnostic)
                || !RequiredCoreIsReachable(record, built, out diagnostic))
            {
                return false;
            }

            grammar = built;
            return true;
        }

        private static bool ReadNodes(ContentRecord record, List<SiteNodeSpec> nodes, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sockets = new HashSet<string>(StringComparer.Ordinal);

            if (!ReadNodeList(record, "requiredNodes", true, nodes, ids, sockets, out diagnostic)
                || !ReadNodeList(record, "optionalNodes", false, nodes, ids, sockets, out diagnostic))
            {
                return false;
            }

            bool anyRequired = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                anyRequired |= nodes[i].Required;
            }

            if (!anyRequired)
            {
                diagnostic = Invalid(record, "requiredNodes",
                    "A kind of place with nothing every one of them has is not a kind of place.");
                return false;
            }

            return true;
        }

        private static bool ReadNodeList(
            ContentRecord record,
            string field,
            bool required,
            List<SiteNodeSpec> nodes,
            HashSet<string> ids,
            HashSet<string> sockets,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            IReadOnlyList<JsonValue> items = record.Payload.GetArray(field);
            for (int i = 0; i < items.Count; i++)
            {
                string path = field + "[" + i + "]";
                JsonValue json = items[i];
                if (json == null || json.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, path, "A node must be a map.");
                    return false;
                }

                string id = json.GetString("id", null);
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostic = Invalid(record, path, "A node must have an id.");
                    return false;
                }

                if (string.Equals(id, SiteGrammar.Outside, StringComparison.Ordinal))
                {
                    diagnostic = Invalid(record, path,
                        "'" + SiteGrammar.Outside + "' is everywhere that is not this place and is never one of its parts.");
                    return false;
                }

                if (!ids.Add(id))
                {
                    diagnostic = Invalid(record, path, "Node " + id + " is declared twice.");
                    return false;
                }

                List<SiteAffordance> affordances = new List<SiteAffordance>();
                if (!ReadAffordances(record, path, json, affordances, out diagnostic))
                {
                    return false;
                }

                string socket = json.GetString("socket", null) ?? string.Empty;
                if (socket.Length > 0 && !sockets.Add(socket))
                {
                    diagnostic = Invalid(record, path,
                        "Socket " + socket + " is claimed by two nodes; a socket is filled once.");
                    return false;
                }

                nodes.Add(new SiteNodeSpec(id, required, affordances, socket));
            }

            return true;
        }

        private static bool ReadAffordances(
            ContentRecord record,
            string path,
            JsonValue json,
            List<SiteAffordance> affordances,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            IReadOnlyList<JsonValue> items = json.GetArray("affordances");
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || items[i].Kind != JsonKind.String)
                {
                    diagnostic = Invalid(record, path + ".affordances[" + i + "]", "An affordance must be a name.");
                    return false;
                }

                SiteAffordance affordance;
                if (!TryParseAffordance(items[i].StringValue, out affordance))
                {
                    diagnostic = Invalid(record, path + ".affordances[" + i + "]",
                        "No such spatial affordance: " + items[i].StringValue + ".");
                    return false;
                }

                if (affordances.Contains(affordance))
                {
                    diagnostic = Invalid(record, path + ".affordances[" + i + "]",
                        "Affordance " + items[i].StringValue + " is required twice.");
                    return false;
                }

                affordances.Add(affordance);
            }

            return true;
        }

        /// <summary>Authored as slugs, held as the enum. `locked_barrier` is `LockedBarrier`.</summary>
        public static bool TryParseAffordance(string name, out SiteAffordance affordance)
        {
            affordance = default(SiteAffordance);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string flattened = name.Replace("_", string.Empty);
            foreach (SiteAffordance candidate in Enum.GetValues(typeof(SiteAffordance)))
            {
                if (string.Equals(candidate.ToString(), flattened, StringComparison.OrdinalIgnoreCase))
                {
                    affordance = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool ReadRoutes(
            ContentRecord record,
            List<SiteNodeSpec> nodes,
            List<SiteRouteSpec> routes,
            out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };
            for (int i = 0; i < nodes.Count; i++)
            {
                declared.Add(nodes[i].Id);
            }

            HashSet<string> pairs = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<JsonValue> items = record.Payload.GetArray("routes");
            for (int i = 0; i < items.Count; i++)
            {
                string path = "routes[" + i + "]";
                JsonValue json = items[i];
                if (json == null || json.Kind != JsonKind.Object)
                {
                    diagnostic = Invalid(record, path, "A route must be a map.");
                    return false;
                }

                string from = json.GetString("from", null);
                string to = json.GetString("to", null);
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    diagnostic = Invalid(record, path, "A route joins two named parts of the place.");
                    return false;
                }

                if (!declared.Contains(from) || !declared.Contains(to))
                {
                    diagnostic = Invalid(record, path,
                        "A route runs between parts this grammar declares: " + from + " -> " + to + ".");
                    return false;
                }

                if (string.Equals(from, to, StringComparison.Ordinal))
                {
                    diagnostic = Invalid(record, path, "A route from " + from + " to itself is not a route.");
                    return false;
                }

                string actionId = json.GetString("via", null) ?? string.Empty;
                bool entry = string.Equals(from, SiteGrammar.Outside, StringComparison.Ordinal);

                // Two routes between the same two parts are two ways through, not a duplicate, so
                // long as they are taken differently - clearing the fall and digging past it are
                // the distinction BQ-090 exists to make real. What is refused is the same way
                // through written twice.
                if (!pairs.Add(from + " -> " + to + " by " + actionId))
                {
                    diagnostic = Invalid(record, path,
                        "Route " + from + " -> " + to + " is declared twice the same way.");
                    return false;
                }

                if (entry && actionId.Length == 0)
                {
                    diagnostic = Invalid(record, path, "A way in has to say which verb it is taken with.");
                    return false;
                }

                if (actionId.Length > 0 && !IsRegisteredAction(actionId))
                {
                    diagnostic = Invalid(record, path, "No such verb: " + actionId + ".");
                    return false;
                }

                List<SiteAffordance> affordances = new List<SiteAffordance>();
                if (!ReadAffordances(record, path, json, affordances, out diagnostic))
                {
                    return false;
                }

                routes.Add(new SiteRouteSpec(from, to, actionId, json.GetBool("admission"), affordances));
            }

            return true;
        }

        /// <summary>
        /// The ways in have to be two ways in, on the required routes alone.
        ///
        /// This is <see cref="SiteGenesis"/>'s own rule, checked one layer earlier and against the
        /// part of the grammar every seed keeps: two verbs that both wait on the same permission
        /// are one approach spelled twice. Checking the required entries rather than all of them is
        /// what makes the guarantee hold for every composition instead of for most of them.
        /// </summary>
        private static bool WaysInAreTwo(ContentRecord record, SiteGrammar grammar, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            int required = 0;
            bool admitted = false;
            bool uninvited = false;
            HashSet<string> verbs = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < grammar.Routes.Count; i++)
            {
                SiteRouteSpec route = grammar.Routes[i];
                if (!route.IsEntry)
                {
                    continue;
                }

                if (!verbs.Add(route.ActionId))
                {
                    diagnostic = Invalid(record, "routes",
                        "Two ways in are taken with the same verb, " + route.ActionId + ".");
                    return false;
                }

                if (!grammar.IsRequired(route))
                {
                    continue;
                }

                required++;
                admitted |= route.NeedsAdmission;
                uninvited |= !route.NeedsAdmission;
            }

            if (required < SiteGenesis.MinimumApproaches || !admitted || !uninvited)
            {
                diagnostic = Invalid(record, "routes",
                    "Every place of this kind needs a way in that waits on somebody and a way in that does not.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Every required part is reached by required routes.
        ///
        /// The done-when is that the inspector can explain every required node and edge in the
        /// plan, and there is no honest explanation of a room every place of this kind has and no
        /// place of this kind can get to. Optional parts are allowed to be unreachable at a given
        /// seed - composing drops those, and says which and why.
        /// </summary>
        private static bool RequiredCoreIsReachable(ContentRecord record, SiteGrammar grammar, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < grammar.Routes.Count; i++)
                {
                    SiteRouteSpec route = grammar.Routes[i];
                    if (grammar.IsRequired(route) && reached.Contains(route.From) && reached.Add(route.To))
                    {
                        grew = true;
                    }
                }
            }

            for (int i = 0; i < grammar.Nodes.Count; i++)
            {
                SiteNodeSpec node = grammar.Nodes[i];
                if (node.Required && !reached.Contains(node.Id))
                {
                    diagnostic = Invalid(record, "routes",
                        "Every place of this kind has a " + node.Id + " and no route reaches it.");
                    return false;
                }
            }

            return true;
        }

        private static bool EverythingIsAToken(ContentRecord record, out ContentDiagnostic diagnostic)
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
                if (!IsToken(value.StringValue))
                {
                    diagnostic = Invalid(record, path,
                        "A grammar names meaning and never words it: '" + value.StringValue + "' is not a slug.");
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
                if (!Scan(record, path + "." + value.Members[i].Key, value.Members[i].Value, ref diagnostic))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Every verb the action library registers, derived rather than listed, for the reason
        /// <see cref="Storylets.StoryletContent"/> derives it: a hand-kept copy is the thing that
        /// lets authored content promise a way in nobody built.
        /// </summary>
        private static bool IsRegisteredAction(string actionId)
        {
            if (_actionIds == null)
            {
                List<string> ids = new List<string>();
                IReadOnlyList<NarrativeAction> actions = StandardActions.CreateRegistry().Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    ids.Add(actions[i].Id);
                }

                _actionIds = ids;
            }

            for (int i = 0; i < _actionIds.Count; i++)
            {
                if (string.Equals(_actionIds[i], actionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static ContentDiagnostic Invalid(ContentRecord record, string field, string message)
        {
            return new ContentDiagnostic("content.site_grammar.invalid", record.Id + "." + field, message);
        }
    }
}
