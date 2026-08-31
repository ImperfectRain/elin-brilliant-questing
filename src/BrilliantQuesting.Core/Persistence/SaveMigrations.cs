using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Persistence
{
    /// <summary>
    /// Upgrades an old save document to the current schema, one version at a time.
    ///
    /// A persistent world model whose format changes without a migration path silently deletes
    /// people's fifty-hour saves, so every persisted shape change is registered here.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Func<JsonValue, JsonValue>> Steps = new Dictionary<int, Func<JsonValue, JsonValue>>();

        static SaveMigrations()
        {
            Register(1, MigratePersonalityWeightsToBehavioralDimensions);
            Register(2, AddProblemSolvingProfiles);
            Register(3, AddSensitivityProfiles);
            Register(4, AddContradictionProfiles);
        }

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

        private static JsonValue MigratePersonalityWeightsToBehavioralDimensions(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                JsonValue personality = npc["personality"];
                if (personality == null || personality.Kind != JsonKind.Object)
                {
                    continue;
                }

                double greed = personality.GetNumber("greed", 0.5);
                double mercy = personality.GetNumber("mercy", 0.5);
                double courage = personality.GetNumber("courage", 0.5);
                double honesty = personality.GetNumber("honesty", 0.5);
                double ambition = personality.GetNumber("ambition", 0.5);
                double loyalty = personality.GetNumber("loyalty", 0.5);
                double sociability = personality.GetNumber("sociability", 0.5);
                double curiosity = personality.GetNumber("curiosity", 0.5);
                double vengefulness = personality.GetNumber("vengefulness", 0.5);

                personality
                    .Set("boldness", courage)
                    .Set("patience", 0.5)
                    .Set("warmth", sociability)
                    .Set("earnestness", 0.5)
                    .Set("optimism", 0.5)
                    .Set("orderliness", 0.5)
                    .Set("mercy", (mercy + (1.0 - vengefulness)) / 2.0)
                    .Set("honesty", honesty)
                    .Set("generosity", 1.0 - greed)
                    .Set("loyalty", loyalty)
                    .Set("trust", 0.5)
                    .Set("humility", 1.0 - ambition)
                    .Set("curiosity", curiosity)
                    .Set("conventionality", 0.5)
                    .Set("statusBlindness", 1.0 - ambition);
            }

            return root.Set("schemaVersion", 2);
        }

        private static JsonValue AddProblemSolvingProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["problemSolving"] == null)
                {
                    npc.Set("problemSolving", NeutralProblemSolvingProfile());
                }
            }

            return root.Set("schemaVersion", 3);
        }

        private static JsonValue NeutralProblemSolvingProfile()
        {
            return JsonValue.Object()
                .Set("confront", 0.5)
                .Set("avoid", 0.5)
                .Set("askAuthority", 0.5)
                .Set("askFriends", 0.5)
                .Set("paySomeone", 0.5)
                .Set("doItSelf", 0.5)
                .Set("manipulate", 0.5)
                .Set("useViolence", 0.5)
                .Set("seekGuild", 0.5)
                .Set("seekReligiousHelp", 0.5)
                .Set("wait", 0.5)
                .Set("flee", 0.5)
                .Set("publicize", 0.5)
                .Set("conceal", 0.5);
        }

        private static JsonValue AddSensitivityProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["sensitivities"] == null)
                {
                    npc.Set("sensitivities", NeutralSensitivityProfile());
                }
            }

            return root.Set("schemaVersion", 4);
        }

        private static JsonValue NeutralSensitivityProfile()
        {
            return JsonValue.Object()
                .Set("publicEmbarrassment", 0.5)
                .Set("unpaidDebt", 0.5)
                .Set("familyThreat", 0.5)
                .Set("animals", 0.5)
                .Set("status", 0.5)
                .Set("theft", 0.5)
                .Set("violence", 0.5)
                .Set("dishonesty", 0.5);
        }

        private static JsonValue AddContradictionProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["contradiction"] == null)
                {
                    npc.Set("contradiction", NeutralContradictionProfile());
                }
            }

            return root.Set("schemaVersion", 5);
        }

        private static JsonValue NeutralContradictionProfile()
        {
            return JsonValue.Object()
                .Set("kind", "None")
                .Set("strength", 1.0);
        }
    }
}
