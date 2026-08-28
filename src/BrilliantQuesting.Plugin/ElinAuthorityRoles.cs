using BepInEx.Logging;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Tells the simulation which live characters carry authority.
    ///
    /// `AuthorityPolicy` decides what a guard or a guild will act on, and it reads
    /// `NarrativeNpc.Roles` to know who is which. Nothing populated that for anyone the
    /// mod did not stage itself: characters registered from observed vanilla play were created
    /// with a name and nothing else, so every real townsperson resolved to `AuthorityRole.None`
    /// and the report verb could never be offered to anybody in an actual game.
    ///
    /// Elin marks these people with trait subclasses - `TraitGuard`, and `TraitGuildPersonnel`
    /// for the clerks and doormen who staff a guild - so the adapter reads the trait and grants
    /// the role the policy is expecting, withdrawing it again if the trait ever stops saying so.
    ///
    /// There is no court in vanilla Elin; `AuthorityRole.Court` stays reachable only through a
    /// staged or modded character, which is why nothing here produces it.
    /// </summary>
    internal static class ElinAuthorityRoles
    {
        internal const string Guard = AuthorityPolicy.GuardRole;
        internal const string Guild = AuthorityPolicy.GuildRole;

        /// <summary>The occupation word for this character, or null when they hold no authority.</summary>
        internal static string For(Chara chara)
        {
            Trait trait = chara?.trait;
            if (trait == null)
            {
                return null;
            }

            if (trait is TraitGuard)
            {
                return Guard;
            }

            if (trait is TraitGuildPersonnel || trait is TraitGuildDoorman)
            {
                return Guild;
            }

            return null;
        }

        /// <summary>
        /// Brings an NPC's standing into line with what the game says about them now.
        ///
        /// Grants the role the trait implies and withdraws any this policy owns that the trait no
        /// longer supports, so a character who stops being a guard stops being able to take a
        /// crime report. That withdrawal is only safe because roles are their own field: while
        /// authority was kept in the occupation string there was no way to say "not any more"
        /// without erasing what the person does for a living.
        ///
        /// Only roles the authority policy owns are touched. Anything a situation or an
        /// organization granted is left alone - it is not this adapter's to take away.
        /// </summary>
        internal static bool Apply(NarrativeNpc npc, Chara chara)
        {
            if (npc == null)
            {
                return false;
            }

            string role = For(chara);
            bool changed = false;

            foreach (string owned in AuthorityPolicy.AuthorityRoles)
            {
                if (owned != role && npc.Roles.Remove(owned))
                {
                    changed = true;
                }
            }

            if (role != null && npc.Roles.Add(role))
            {
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Brings every bound character's authority up to date on load, so a save made before this
        /// existed - or one where somebody has since become a guard - reports correctly.
        /// </summary>
        internal static void RefreshAll(NarrativeWorldState world, ElinBindings bindings, ManualLogSource log)
        {
            if (world == null || bindings == null)
            {
                return;
            }

            int updated = 0;
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                // Only characters the game can currently show us. An NPC who is off-map or
                // unloaded resolves to null, and null is not the game saying they stopped being
                // a guard - it is the game not being asked.
                Chara chara = bindings.ResolveChara(npc.Id);
                if (chara != null && Apply(npc, chara))
                {
                    updated++;
                }
            }

            if (updated > 0)
            {
                log?.LogInfo("Refreshed authority for " + updated + " character(s).");
            }
        }
    }
}
