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
    }
}
