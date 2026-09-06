using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Content;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// BQ-140. Reads authored site pieces out of the compiled content bundle.
    ///
    /// The reader the compiler validates through, so a piece that could not be built out of - a
    /// socket nobody declares, a footprint of nothing, an affordance spelled wrong, a capability
    /// this contract has no name for - is a build error with a file name rather than a site that
    /// quietly comes up short at generation time (`content-pipeline.md §3`).
    /// </summary>
    public static class SitePieceContent
    {
        public const string Kind = "site-piece";

        public static IReadOnlyList<SitePiece> LoadPieces(
            ContentBundle bundle,
            out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            List<SitePiece> pieces = new List<SitePiece>();
            List<ContentDiagnostic> problems = new List<ContentDiagnostic>();
            if (bundle == null)
            {
                diagnostics = problems.AsReadOnly();
                return pieces.AsReadOnly();
            }

            for (int i = 0; i < bundle.Records.Count; i++)
            {
                ContentRecord record = bundle.Records[i];
                if (!string.Equals(record.Kind, Kind, StringComparison.Ordinal))
                {
                    continue;
                }

                SitePiece piece;
                ContentDiagnostic diagnostic;
                if (TryRead(record, out piece, out diagnostic))
                {
                    pieces.Add(piece);
                }
                else
                {
                    problems.Add(diagnostic);
                }
            }

            diagnostics = problems.AsReadOnly();
            return pieces.AsReadOnly();
        }

        /// <summary>Every piece of one family, ready for assembly.</summary>
        public static SitePieceCatalogue CreateCatalogue(
            ContentBundle bundle,
            string family,
            out IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            return new SitePieceCatalogue(family, LoadPieces(bundle, out diagnostics));
        }

        private static bool TryRead(ContentRecord record, out SitePiece piece, out ContentDiagnostic diagnostic)
        {
            piece = null;
            diagnostic = null;

            if (record.Payload == null || record.Payload.Kind != JsonKind.Object)
            {
                diagnostic = Invalid(record, "payload", "Site piece payload must be an object.");
                return false;
            }

            string family = record.Payload.GetString("family", null);
            if (!IsToken(family))
            {
                diagnostic = Invalid(record, "family", "A piece must say which family of site it belongs to.");
                return false;
            }

            string socket = record.Payload.GetString("socket", null);
            if (!IsToken(socket))
            {
                diagnostic = Invalid(record, "socket", "A piece must say which socket it fills.");
                return false;
            }

            int width = record.Payload.GetInt("width");
            int height = record.Payload.GetInt("height");
            if (width <= 0 || height <= 0)
            {
                diagnostic = Invalid(record, "width", "A piece takes up room: width and height must both be positive.");
                return false;
            }

            int ways = record.Payload.GetInt("ways");
            if (ways <= 0)
            {
                diagnostic = Invalid(record, "ways", "A piece nothing can lead to or from is not part of a place.");
                return false;
            }

            List<SiteAffordance> provides = new List<SiteAffordance>();
            if (!ReadProvides(record, provides, out diagnostic))
            {
                return false;
            }

            RouteEvidence evidence;
            string grade = record.Payload.GetString("evidence", null);
            if (string.IsNullOrEmpty(grade))
            {
                // A piece that says nothing about what it leans on leans on nothing beyond making
                // the place at all, which the capability above already gates. Recording that as
                // "authored by this mod" is the truth about it rather than an optimistic default.
                evidence = RouteEvidence.BqAuthored;
            }
            else if (!TryParseEvidence(grade, out evidence))
            {
                diagnostic = Invalid(record, "evidence", "No such evidence grade: " + grade + ".");
                return false;
            }

            List<VanillaCapability> needs = new List<VanillaCapability>();
            if (!ReadNeeds(record, needs, out diagnostic))
            {
                return false;
            }

            string leansOn = record.Payload.GetString("leansOn", null) ?? string.Empty;
            if (evidence != RouteEvidence.BqAuthored && leansOn.Length == 0)
            {
                diagnostic = Invalid(record, "leansOn",
                    "A piece graded " + evidence + " must name what on the live build it leans on.");
                return false;
            }

            piece = new SitePiece(record.Id, family, socket, width, height, ways, provides, evidence, leansOn, needs);
            return true;
        }

        private static bool ReadProvides(ContentRecord record, List<SiteAffordance> provides, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            IReadOnlyList<JsonValue> items = record.Payload.GetArray("provides");
            for (int i = 0; i < items.Count; i++)
            {
                string path = "provides[" + i + "]";
                if (items[i] == null || items[i].Kind != JsonKind.String)
                {
                    diagnostic = Invalid(record, path, "An affordance must be a name.");
                    return false;
                }

                SiteAffordance affordance;
                if (!SiteGrammarContent.TryParseAffordance(items[i].StringValue, out affordance))
                {
                    diagnostic = Invalid(record, path, "No such spatial affordance: " + items[i].StringValue + ".");
                    return false;
                }

                if (provides.Contains(affordance))
                {
                    diagnostic = Invalid(record, path, "Affordance " + items[i].StringValue + " is provided twice.");
                    return false;
                }

                provides.Add(affordance);
            }

            return true;
        }

        private static bool ReadNeeds(ContentRecord record, List<VanillaCapability> needs, out ContentDiagnostic diagnostic)
        {
            diagnostic = null;
            IReadOnlyList<JsonValue> items = record.Payload.GetArray("needs");
            for (int i = 0; i < items.Count; i++)
            {
                string path = "needs[" + i + "]";
                if (items[i] == null || items[i].Kind != JsonKind.String)
                {
                    diagnostic = Invalid(record, path, "A capability must be a name.");
                    return false;
                }

                VanillaCapability capability;
                if (!TryParseCapability(items[i].StringValue, out capability))
                {
                    diagnostic = Invalid(record, path, "No such vanilla capability: " + items[i].StringValue + ".");
                    return false;
                }

                if (!needs.Contains(capability))
                {
                    needs.Add(capability);
                }
            }

            return true;
        }

        /// <summary>Authored as slugs, held as the enum. `source_observed` is `SourceObserved`.</summary>
        public static bool TryParseEvidence(string name, out RouteEvidence evidence)
        {
            evidence = default(RouteEvidence);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string flattened = name.Replace("_", string.Empty);
            foreach (RouteEvidence candidate in Enum.GetValues(typeof(RouteEvidence)))
            {
                if (string.Equals(candidate.ToString(), flattened, StringComparison.OrdinalIgnoreCase))
                {
                    evidence = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryParseCapability(string name, out VanillaCapability capability)
        {
            capability = default(VanillaCapability);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string flattened = name.Replace("_", string.Empty);
            foreach (VanillaCapability candidate in Enum.GetValues(typeof(VanillaCapability)))
            {
                if (string.Equals(candidate.ToString(), flattened, StringComparison.OrdinalIgnoreCase))
                {
                    capability = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static ContentDiagnostic Invalid(ContentRecord record, string field, string message)
        {
            return new ContentDiagnostic("content.site_piece.invalid", record.Id + "." + field, message);
        }
    }
}
