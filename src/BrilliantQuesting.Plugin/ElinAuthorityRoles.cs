using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Brings the standing this simulation records into line with the identity affordances BQ
    /// derives from the seam's observation.
    ///
    /// `AuthorityPolicy` decides what a guard or a guild will act on, and it reads
    /// `NarrativeNpc.Roles` to know who is which. Nothing populated that for anyone the mod did
    /// not stage itself, so every real townsperson resolved to `AuthorityRole.None` and the report
    /// verb could never be offered to anybody in an actual game.
    ///
    /// This file used to read `Chara.trait` itself, and then briefly decided for itself which
    /// observed office counted as which BQ authority word. It does neither now: the trait is read
    /// once at the seam as the institutional facet of <see cref="CharacterIdentity"/>, what that
    /// office implies is derived once in <see cref="IdentityAffordances"/> (BQ-145), and what is
    /// left here is plumbing. One read of the game, one interpretation of it, and no identity
    /// vocabulary in the adapter at all.
    ///
    /// The derivation is taken from the observation alone rather than through
    /// <see cref="IdentityAffordances.Of(NarrativeNpc, IVanillaState)"/>: reconciling standing
    /// must be about what the game currently says, and folding in the standing BQ wrote down
    /// earlier would make a dismissal unable to reach the simulation.
    /// </summary>
    internal static class ElinAuthorityRoles
    {
        /// <summary>What the observed offices amount to, in the role words the policy speaks.</summary>
        internal static IReadOnlyList<string> RolesFrom(CharacterIdentity identity)
        {
            return AuthorityPolicy.RoleWordsFor(IdentityAffordances.Derive(identity));
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
