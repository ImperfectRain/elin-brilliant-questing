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
            Register(5, AddCharacterQuirkProfiles);
            Register(6, AddValueAndNeedProfiles);
            Register(7, AddEmotionalStateProfiles);
            Register(8, AddStoryletFirings);
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

        private static JsonValue AddCharacterQuirkProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["quirk"] == null)
                {
                    npc.Set("quirk", NeutralCharacterQuirkProfile());
                }
            }

            return root.Set("schemaVersion", 6);
        }

        private static JsonValue NeutralCharacterQuirkProfile()
        {
            return JsonValue.Object()
                .Set("assigned", false)
                .Set("weirdness", "MostlyOrdinary")
                .Set("kind", "None");
        }

        private static JsonValue AddValueAndNeedProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["values"] == null)
                {
                    npc.Set("values", NeutralValueProfile());
                }

                if (npc["needs"] == null)
                {
                    npc.Set("needs", NeutralNeedProfile());
                }
            }

            return root.Set("schemaVersion", 7);
        }

        private static JsonValue AddEmotionalStateProfiles(JsonValue root)
        {
            foreach (JsonValue npc in root.GetArray("npcs"))
            {
                if (npc["emotions"] == null)
                {
                    npc.Set("emotions", NeutralEmotionalStateProfile());
                }
            }

            return root.Set("schemaVersion", 8);
        }

        private static JsonValue AddStoryletFirings(JsonValue root)
        {
            foreach (JsonValue thread in root.GetArray("threads"))
            {
                if (thread["storyletFirings"] == null)
                {
                    thread.Set("storyletFirings", JsonValue.Array());
                }
            }

            return root.Set("schemaVersion", 9);
        }

        private static JsonValue NeutralValueProfile()
        {
            return JsonValue.Object()
                .Set("family", NeutralValueConcern())
                .Set("wealth", NeutralValueConcern())
                .Set("law", NeutralValueConcern())
                .Set("faith", NeutralValueConcern())
                .Set("status", NeutralValueConcern())
                .Set("animals", NeutralValueConcern())
                .Set("knowledge", NeutralValueConcern())
                .Set("freedom", NeutralValueConcern());
        }

        private static JsonValue NeutralValueConcern()
        {
            return JsonValue.Object()
                .Set("importance", 0.5)
                .Set("flexibility", 0.5);
        }

        private static JsonValue NeutralNeedProfile()
        {
            return JsonValue.Object()
                .Set("safety", 0.0)
                .Set("belonging", 0.0)
                .Set("debtRelief", 0.0)
                .Set("status", 0.0)
                .Set("loyalty", 0.0)
                .Set("justice", 0.0)
                .Set("secrecy", 0.0)
                .Set("revenge", 0.0)
                .Set("protection", 0.0)
                .Set("materialShortage", 0.0)
                .Set("obligation", 0.0);
        }

        private static JsonValue NeutralEmotionalStateProfile()
        {
            return JsonValue.Object()
                .Set("anger", 0.0)
                .Set("fear", 0.0)
                .Set("shame", 0.0)
                .Set("grief", 0.0)
                .Set("relief", 0.0)
                .Set("suspicion", 0.0)
                .Set("affection", 0.0)
                .Set("stress", 0.0)
                .Set("lastUpdated", 0L);
        }
    }
}
