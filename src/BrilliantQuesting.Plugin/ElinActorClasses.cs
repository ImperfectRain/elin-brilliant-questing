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
        private static bool _reportedShape;
        private static bool _reportedKindShape;

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
                bool pc = VanillaApiReflection.HasTrueFlag(chara, "IsPC");
                bool party = VanillaApiReflection.HasTrueFlag(chara, "IsPCParty");
                if (pc)
                {
                    return NarrativeActorClass.Player;
                }

                if (party)
                {
                    return NarrativeActorClass.StoryCritical;
                }

                bool unique = VanillaApiReflection.HasTrueFlag(chara, "IsUnique")
                              || VanillaApiReflection.ReadObject(chara, "c_uniqueData") != null
                              || TraitLooksUnique(chara);
                bool important = VanillaApiReflection.HasTrueFlag(chara, "IsImportant")
                                 || VanillaApiReflection.HasTrueFlag(chara, "c_isImportant")
                                 || VanillaApiReflection.ReadObject(chara, "quest") != null;
                bool homeOrBranch = VanillaApiReflection.HasTrueFlag(chara, "IsHomeMember")
                                    || VanillaApiReflection.HasTrueFlag(chara, "IsBranchMember");

                Report(log);

                if (important)
                {
                    return NarrativeActorClass.StoryCritical;
                }

                if (unique || homeOrBranch)
                {
                    return NarrativeActorClass.UniqueService;
                }

                return NarrativeActorClass.OrdinaryCitizen;
            }
            catch (Exception)
            {
                return NarrativeActorClass.Unknown;
            }
        }

        internal static NarrativeActorKind ClassifyKind(Chara chara, ManualLogSource log)
        {
            if (chara == null)
            {
                return NarrativeActorKind.Unknown;
            }

            try
            {
                ActorSignals signals = Signals(chara);
                ReportKind(log);

                if (ContainsAny(signals.All, "animal", "livestock", "pet", "mount", "fish", "bird"))
                {
                    return NarrativeActorKind.Animal;
                }

                if (ContainsAny(signals.All, "monster", "creature", "beast", "vermin"))
                {
                    return NarrativeActorKind.Creature;
                }

                if (!string.IsNullOrEmpty(signals.Job)
                    || ContainsAny(signals.All, "human", "citizen", "merchant", "guard", "resident", "adventurer"))
                {
                    return NarrativeActorKind.Person;
                }

                return NarrativeActorKind.Unknown;
            }
            catch (Exception)
            {
                return NarrativeActorKind.Unknown;
            }
        }

        internal static SocialAgency ClassifySocialAgency(Chara chara, ManualLogSource log)
        {
            if (chara == null)
            {
                return SocialAgency.Unknown;
            }

            try
            {
                ActorSignals signals = Signals(chara);
                ReportKind(log);

                if (!string.IsNullOrEmpty(signals.Job)
                    || ContainsAny(signals.All, "merchant", "shop", "guard", "guild", "citizen", "resident", "adventurer"))
                {
                    return SocialAgency.Full;
                }

                if (ContainsAny(signals.All, "animal", "livestock", "pet", "mount", "fish", "bird"))
                {
                    return SocialAgency.None;
                }

                if (ContainsAny(signals.All, "monster", "creature", "beast", "vermin"))
                {
                    return SocialAgency.Limited;
                }

                return SocialAgency.Unknown;
            }
            catch (Exception)
            {
                return SocialAgency.Unknown;
            }
        }

        private static void Report(ManualLogSource log)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            log.LogInfo("BQ actor classification: using IsPC/IsPCParty, IsUnique, IsImportant, "
                        + "c_uniqueData, c_isImportant, TraitUniqueChara, quest and Home/branch flags. "
                        + "Unreadable actors remain Unknown and close relocation/removal.");
        }

        private static bool TraitLooksUnique(Chara chara)
        {
            object trait = VanillaApiReflection.ReadObject(chara, "trait");
            return trait != null
                   && trait.GetType().Name.IndexOf("TraitUniqueChara", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ActorSignals Signals(Chara chara)
        {
            object source = VanillaApiReflection.ReadObject(chara, "source");
            object race = VanillaApiReflection.ReadObject(source, "race")
                          ?? VanillaApiReflection.ReadObject(chara, "race");
            string job = VanillaApiReflection.ReadText(source, "job", "idJob", "Job", "hobby", "work");
            string all = string.Join(
                " ",
                new[]
                {
                    VanillaApiReflection.ReadText(source, "id", "Id", "name", "Name"),
                    job,
                    VanillaApiReflection.ReadText(source, "category", "tag", "tags", "type"),
                    VanillaApiReflection.ReadText(source, "race", "idRace"),
                    VanillaApiReflection.ReadText(race, "id", "Id", "name", "Name", "category", "tag", "tags", "type"),
                    VanillaApiReflection.ReadText(chara, "idRace", "idActor", "idChara"),
                    chara.trait == null ? string.Empty : chara.trait.GetType().Name
                });
            return new ActorSignals(job, all);
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReportKind(ManualLogSource log)
        {
            if (_reportedKindShape || log == null)
            {
                return;
            }

            _reportedKindShape = true;
            log.LogInfo("BQ actor kind classification: reading Chara.source job/category/tag/type, race "
                        + "metadata and trait type. Unreadable actors remain Unknown; social roles require "
                        + "Full social agency.");
        }

        private readonly struct ActorSignals
        {
            public ActorSignals(string job, string all)
            {
                Job = job ?? string.Empty;
                All = all ?? string.Empty;
            }

            public string Job { get; }
            public string All { get; }
        }
    }
}
