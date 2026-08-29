using System;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Works out how far the mod may reach into one of the game's characters.
    ///
    /// The mutation policy needs one fact per actor and the game is the only thing that has it:
    /// whether a vanilla quest line, a shop or a whole story depends on this person still standing
    /// where they are. Elin says so through a handful of members on `Chara` and its source sheet,
    /// none of which has been read off a running game - so like everything else in that position
    /// they are resolved by name against a candidate list.
    ///
    /// The failure direction is the whole point. A build with none of these names does not fall
    /// back to "ordinary": it answers <see cref="NarrativeActorClass.Unknown"/>, and Unknown keeps
    /// the reversible reaches and refuses every irreversible one. So a story-critical NPC this
    /// build cannot recognise is still unmovable and still unkillable - the guarantee does not
    /// rest on the guess being right, only on the guess never being an upgrade.
    ///
    /// <see cref="NarrativeActorClass.OrdinaryCitizen"/> - the one class that opens relocation and
    /// absence - is therefore returned only when the flags were actually readable and actually
    /// said no.
    /// </summary>
    internal static class ElinActorClasses
    {
        /// <summary>
        /// "This character is one of a kind." Elin's own notion of a named NPC, which is the line
        /// between somebody the world will regenerate and somebody it will not.
        /// </summary>
        private static readonly string[] UniqueNames = { "IsUnique", "isUnique", "IsUniqueName" };

        /// <summary>
        /// "This character belongs to the main story." Read off the source sheet as well as the
        /// Chara, because a story role is a property of who they are rather than of the instance.
        /// </summary>
        private static readonly string[] StoryNames =
        {
            "IsMainCharacter", "isMainCharacter", "IsUniqueCharacter", "IsImportantNPC", "isImportant"
        };

        /// <summary>The source-sheet row behind a Chara, where the story-ness of a role lives.</summary>
        private static readonly string[] SourceNames = { "source" };

        /// <summary>
        /// A source-sheet tag list. Elin tags rows with things like "god" and "noRandomProduct";
        /// a story tag on the row is a stronger statement than any flag on one instance.
        /// </summary>
        private static readonly string[] TagNames = { "tag", "tags" };

        /// <summary>
        /// Deliberately narrow. A wider list - "quest", "unique", "boss" - would only ever
        /// over-protect, which is the safe direction, but over-protecting half the town would
        /// quietly close the shelter routes for everybody and look like a bug rather than a
        /// policy. These three say "main story" and nothing else does.
        /// </summary>
        private static readonly string[] StoryTags = { "story", "mainquest", "main_quest" };

        private static bool _reportedShape;

        /// <summary>
        /// What this character is, as far as this build can tell. Never throws: a classification
        /// that failed is Unknown, and Unknown is safe.
        /// </summary>
        internal static NarrativeActorClass Classify(Chara chara, ManualLogSource log)
        {
            if (chara == null)
            {
                return NarrativeActorClass.Unknown;
            }

            try
            {
                bool? story = ReadFlag(chara, StoryNames);
                object source = ReadMember(chara, SourceNames);
                if (story != true && source != null)
                {
                    story = ReadFlag(source, StoryNames) ?? story;
                }

                if (story != true && HasStoryTag(source))
                {
                    story = true;
                }

                bool? unique = ReadFlag(chara, UniqueNames) ?? ReadFlag(source, UniqueNames);
                Report(log, story.HasValue, unique.HasValue);

                if (story == true)
                {
                    return NarrativeActorClass.StoryCritical;
                }

                if (unique == true)
                {
                    return NarrativeActorClass.UniqueService;
                }

                // An ordinary citizen only when both questions were actually answered. One
                // unreadable flag and this is an actor the mod may talk to, pay and rob but must
                // never move or remove.
                return story.HasValue && unique.HasValue
                    ? NarrativeActorClass.OrdinaryCitizen
                    : NarrativeActorClass.Unknown;
            }
            catch (Exception)
            {
                return NarrativeActorClass.Unknown;
            }
        }

        private static void Report(ManualLogSource log, bool storyReadable, bool uniqueReadable)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            log.LogInfo("BQ actor classification: story flag "
                        + (storyReadable ? "readable" : "UNREADABLE")
                        + ", unique flag " + (uniqueReadable ? "readable" : "UNREADABLE")
                        + ". Unreadable means every character is Unknown, which keeps social and "
                        + "inventory routes and closes relocation and removal.");
        }

        private static bool HasStoryTag(object source)
        {
            object tags = ReadMember(source, TagNames);
            string text = TagText(tags);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < StoryTags.Length; i++)
            {
                if (text.IndexOf(StoryTags[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TagText(object tags)
        {
            if (tags == null)
            {
                return null;
            }

            if (tags is string single)
            {
                return single;
            }

            if (tags is System.Collections.IEnumerable list)
            {
                string joined = string.Empty;
                foreach (object item in list)
                {
                    joined += (item == null ? string.Empty : item.ToString()) + ",";
                }

                return joined;
            }

            return tags.ToString();
        }

        /// <summary>A bool member by any of these names, or null when this build has none of them.</summary>
        private static bool? ReadFlag(object target, string[] names)
        {
            object value = ReadMember(target, names);
            return value is bool flag ? flag : (bool?)null;
        }

        private static object ReadMember(object target, string[] names)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo property = type.GetProperty(
                    names[i], BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(target, null);
                }

                FieldInfo field = type.GetField(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(target);
                }
            }

            return null;
        }
    }
}
