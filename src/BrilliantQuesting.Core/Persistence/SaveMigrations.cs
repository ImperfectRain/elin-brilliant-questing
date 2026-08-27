using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Persistence
{
    /// <summary>
    /// Upgrades an old save document to the current schema, one version at a time.
    ///
    /// Empty today because version 1 is the first, but the mechanism ships now on purpose: a
    /// persistent world model whose format changes without a migration path silently deletes
    /// people's fifty-hour saves, and adding this later is much harder than starting with it.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Func<JsonValue, JsonValue>> Steps = new Dictionary<int, Func<JsonValue, JsonValue>>();

        /// <summary>Registers an upgrade from <paramref name="fromVersion"/> to the next version.</summary>
        public static void Register(int fromVersion, Func<JsonValue, JsonValue> step)
        {
            Steps[fromVersion] = step;
        }

        public static JsonValue Migrate(JsonValue root, int targetVersion)
        {
            int version = root.GetInt("schemaVersion", 0);
            if (version > targetVersion)
            {
                throw new NotSupportedException(
                    "Save was written by a newer version of the mod (schema " + version + " > " + targetVersion + ").");
            }

            while (version < targetVersion)
            {
                if (!Steps.TryGetValue(version, out Func<JsonValue, JsonValue> step))
                {
                    throw new NotSupportedException("No migration registered from schema version " + version + ".");
                }

                int before = version;
                root = step(root);
                version = root.GetInt("schemaVersion", version + 1);

                // A step that does not advance the version would loop forever; that is a bug in
                // the migration, and it should say so rather than hang the game on load.
                if (version <= before)
                {
                    throw new InvalidOperationException(
                        "Migration from schema version " + before + " did not advance the version.");
                }
            }

            return root;
        }
    }
}
