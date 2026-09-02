using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Turns the institutional facet of the identity observation into standing the simulation can
    /// act on.
    ///
    /// `AuthorityPolicy` decides what a guard or a guild will act on, and it reads
    /// `NarrativeNpc.Roles` to know who is which. Nothing populated that for anyone the mod did
    /// not stage itself, so every real townsperson resolved to `AuthorityRole.None` and the report
    /// verb could never be offered to anybody in an actual game.
    ///
    /// This file used to read `Chara.trait` itself. It no longer does: the trait is read once, at
    /// the seam, as the institutional facet of <see cref="CharacterIdentity"/>, and what is left
    /// here is the interpretation - which observed office counts as which BQ authority word. One
    /// read of the game, one place it is interpreted, and the identity vocabulary that crosses the
    /// seam stays Elin's own.
    ///
    /// There is no court in vanilla Elin; `AuthorityRole.Court` stays reachable only through a
    /// staged or modded character, which is why nothing here produces it.
    /// </summary>
    internal static class ElinAuthorityRoles
    {
        internal const string Guard = AuthorityPolicy.GuardRole;
        internal const string Guild = AuthorityPolicy.GuildRole;

        /// <summary>
        /// Which BQ authority words the observed offices amount to.
        ///
        /// Matched on the office id - the game's own trait type name - rather than on a trait
        /// object, so an office this build spells differently is simply not recognised and grants
        /// nothing, instead of throwing away the rest of the observation.
        /// </summary>
        internal static IReadOnlyList<string> RolesFrom(CharacterIdentity identity)
        {
            List<string> roles = new List<string>();
            if (identity == null)
            {
                return roles;
            }

            for (int i = 0; i < identity.Institutions.Count; i++)
            {
                string office = identity.Institutions[i].Role.VanillaId;
                if (office.IndexOf("Guard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Add(roles, Guard);
                }
                else if (office.IndexOf("Guild", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Add(roles, Guild);
                }
            }

            return roles;
        }

        /// <summary>One word each: two guild offices on one person is still one guild standing.</summary>
        private static void Add(List<string> roles, string role)
        {
            if (!roles.Contains(role))
            {
                roles.Add(role);
            }
        }

        /// <summary>
        /// Brings an NPC's standing into line with what the game says about them now, and reports
        /// whether anything moved.
        ///
        /// Grants the role the observed office implies and withdraws any this policy owns that the
        /// observation no longer supports, so a character who stops being a guard stops being able
        /// to take a crime report. An observation whose institutional facet went unread changes
        /// nothing at all: not being able to look is not the game saying somebody was dismissed.
        /// </summary>
        internal static bool Apply(NarrativeNpc npc, CharacterIdentity identity)
        {
            return AuthorityPolicy.Reconcile(
                npc,
                RolesFrom(identity),
                identity != null && identity.InstitutionsRead);
        }

        /// <summary>
        /// Brings every character the game can currently answer for up to date, so a save made
        /// before this existed - or one where somebody has since become a guard - reports
        /// correctly.
        ///
        /// Goes through the seam rather than through the bindings: an actor the adapter cannot
        /// resolve comes back with every facet unknown, which is the same "the game was not asked"
        /// that a null Chara used to mean, and <see cref="Apply"/> leaves them alone.
        /// </summary>
        internal static void RefreshAll(NarrativeWorldState world, IVanillaState vanilla, ManualLogSource log)
        {
            if (world == null || vanilla == null)
            {
                return;
            }

            int updated = 0;
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                if (Apply(npc, vanilla.GetCharacterIdentity(npc.Id)))
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
