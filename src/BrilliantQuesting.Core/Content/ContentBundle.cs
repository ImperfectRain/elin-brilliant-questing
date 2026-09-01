using System.Collections.Generic;
using BrilliantQuesting.Persistence;

namespace BrilliantQuesting.Content
{
    public sealed class ContentBundle
    {
        public const int CurrentVersion = 1;

        public static readonly ContentBundle Empty = new ContentBundle(CurrentVersion, new ContentRecord[0]);

        private readonly Dictionary<string, ContentRecord> _byId;

        public ContentBundle(int version, IEnumerable<ContentRecord> records)
        {
            Version = version;
            Records = new List<ContentRecord>(records ?? new ContentRecord[0]).AsReadOnly();
            _byId = new Dictionary<string, ContentRecord>();
            for (int i = 0; i < Records.Count; i++)
            {
                _byId[Records[i].Id] = Records[i];
            }
        }

        public int Version { get; }

        public IReadOnlyList<ContentRecord> Records { get; }

        public bool TryGet(string id, out ContentRecord record)
        {
            if (id == null)
            {
                record = null;
                return false;
            }

            return _byId.TryGetValue(id, out record);
        }
    }

    public sealed class ContentRecord
    {
        public ContentRecord(string id, string kind, JsonValue payload)
        {
            Id = id;
            Kind = kind;
            Payload = payload ?? JsonValue.Object();
        }

        public string Id { get; }

        public string Kind { get; }

        public JsonValue Payload { get; }
    }
}
