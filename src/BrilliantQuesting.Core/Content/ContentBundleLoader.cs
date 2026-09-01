using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.Content
{
    public static class ContentBundleLoader
    {
        public static ContentBundleLoadResult LoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ContentBundleLoadResult.Failed("content.bundle.path.empty", "Content bundle path is empty.");
            }

            try
            {
                return LoadText(File.ReadAllText(path));
            }
            catch (FileNotFoundException)
            {
                return ContentBundleLoadResult.Failed("content.bundle.missing", "Content bundle is missing: " + path);
            }
            catch (DirectoryNotFoundException)
            {
                return ContentBundleLoadResult.Failed("content.bundle.missing", "Content bundle directory is missing: " + path);
            }
            catch (IOException ex)
            {
                return ContentBundleLoadResult.Failed("content.bundle.unreadable", "Content bundle could not be read: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ContentBundleLoadResult.Failed("content.bundle.unreadable", "Content bundle could not be read: " + ex.Message);
            }
        }

        public static ContentBundleLoadResult LoadText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ContentBundleLoadResult.Failed("content.bundle.empty", "Content bundle is empty.");
            }

            JsonValue root;
            try
            {
                root = JsonValue.Parse(text);
            }
            catch (FormatException ex)
            {
                return ContentBundleLoadResult.Failed("content.bundle.malformed", "Content bundle is malformed: " + ex.Message);
            }

            if (root.Kind != JsonKind.Object)
            {
                return ContentBundleLoadResult.Failed("content.bundle.malformed", "Content bundle root must be an object.");
            }

            string format = root.GetString("format", null);
            if (!string.Equals(format, "brilliant-questing-content", StringComparison.Ordinal))
            {
                return ContentBundleLoadResult.Failed("content.bundle.format", "Content bundle format is not brilliant-questing-content.");
            }

            int version = root.GetInt("version", 0);
            if (version != ContentBundle.CurrentVersion)
            {
                return ContentBundleLoadResult.Failed(
                    "content.bundle.version",
                    "Content bundle version " + version + " is not supported by loader version " + ContentBundle.CurrentVersion + ".");
            }

            List<ContentRecord> records = new List<ContentRecord>();
            List<ContentDiagnostic> diagnostics = new List<ContentDiagnostic>();
            HashSet<string> ids = new HashSet<string>();
            IReadOnlyList<JsonValue> entries = root.GetArray("records");
            for (int i = 0; i < entries.Count; i++)
            {
                ContentRecord record;
                ContentDiagnostic diagnostic;
                if (TryReadRecord(entries[i], i, ids, out record, out diagnostic))
                {
                    records.Add(record);
                }
                else
                {
                    diagnostics.Add(diagnostic);
                }
            }

            return new ContentBundleLoadResult(new ContentBundle(version, records), diagnostics);
        }

        private static bool TryReadRecord(
            JsonValue json,
            int index,
            HashSet<string> ids,
            out ContentRecord record,
            out ContentDiagnostic diagnostic)
        {
            record = null;
            if (json == null || json.Kind != JsonKind.Object)
            {
                diagnostic = InvalidRecord(index, "Content record must be an object.");
                return false;
            }

            string id = json.GetString("id", null);
            if (string.IsNullOrWhiteSpace(id))
            {
                diagnostic = InvalidRecord(index, "Content record id is missing.");
                return false;
            }

            if (!ids.Add(id))
            {
                diagnostic = InvalidRecord(index, "Content record id is duplicated: " + id);
                return false;
            }

            string kind = json.GetString("kind", null);
            if (string.IsNullOrWhiteSpace(kind))
            {
                diagnostic = InvalidRecord(index, "Content record kind is missing for " + id + ".");
                return false;
            }

            JsonValue payload = json["payload"];
            if (payload == null || payload.Kind != JsonKind.Object)
            {
                diagnostic = InvalidRecord(index, "Content record payload must be an object for " + id + ".");
                return false;
            }

            record = new ContentRecord(id, kind, payload);
            diagnostic = null;
            return true;
        }

        private static ContentDiagnostic InvalidRecord(int index, string message)
        {
            return new ContentDiagnostic("content.record.invalid", "records[" + index + "]", message);
        }
    }

    public sealed class ContentBundleLoadResult
    {
        public ContentBundleLoadResult(ContentBundle bundle, IEnumerable<ContentDiagnostic> diagnostics)
        {
            Bundle = bundle ?? ContentBundle.Empty;
            Diagnostics = new List<ContentDiagnostic>(diagnostics ?? new ContentDiagnostic[0]).AsReadOnly();
        }

        public ContentBundle Bundle { get; }

        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }

        public bool IsUsable => Bundle.Records.Count > 0 || Diagnostics.Count == 0;

        public static ContentBundleLoadResult Failed(string code, string message)
        {
            return new ContentBundleLoadResult(
                ContentBundle.Empty,
                new[] { new ContentDiagnostic(code, null, message) });
        }
    }

    public sealed class ContentDiagnostic
    {
        public ContentDiagnostic(string code, string location, string message)
        {
            Code = code;
            Location = location;
            Message = message;
        }

        public string Code { get; }

        public string Location { get; }

        public string Message { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Location)
                ? Code + ": " + Message
                : Code + " at " + Location + ": " + Message;
        }
    }
}
