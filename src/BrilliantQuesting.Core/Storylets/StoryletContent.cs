using System;
using System.Collections.Generic;
using BrilliantQuesting.Content;
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

            StoryletDefinition built = new StoryletDefinition(record.Id);
            if (!ReadStringArray(record, "situationTags", built.SituationTags, out diagnostic)
                || !ReadStringArray(record, "toneTags", built.ToneTags, out diagnostic)
                || !ReadRoles(record, "requiredRoles", built.RequiredRoles, out diagnostic)
                || !ReadRoles(record, "optionalRoles", built.OptionalRoles, out diagnostic)
                || !ReadPreconditions(record, built, out diagnostic)
                || !ReadStringItems(record, "beats", delegate(string id) { built.Beats.Add(new StoryletBeat(id)); }, out diagnostic)
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
