using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Tells the simulation which live characters carry authority.
    ///
    /// `AuthorityPolicy` decides what a guard or a guild will act on, and it reads
    /// `NarrativeNpc.Occupation` to know who is which. Nothing populated that field for anyone the
    /// mod did not stage itself: characters registered from observed vanilla play were created
    /// with a name and nothing else, so every real townsperson resolved to `AuthorityRole.None`
    /// and the report verb could never be offered to anybody in an actual game.
    ///
    /// Elin does not carry an occupation string. It marks these people with trait subclasses -
    /// `TraitGuard`, and `TraitGuildPersonnel` for the clerks and doormen who staff a guild - so
    /// the adapter reads the trait and writes the word the policy is expecting.
    ///
    /// There is no court in vanilla Elin; `AuthorityRole.Court` stays reachable only through a
    /// staged or modded character, which is why nothing here produces it.
    /// </summary>
    internal static class ElinAuthorityRoles
    {
        internal const string Guard = "guard";
        internal const string Guild = "guild officer";

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
        /// Stamps the authority word onto an NPC, without ever clearing one that is already there.
        ///
        /// A staged character carries an occupation the situation gave it - "shopkeeper",
        /// "neighbour" - and the live Chara behind it is an ordinary villager with no trait to
        /// read. Overwriting would throw away the only description those characters have.
        /// </summary>
        internal static bool Apply(NarrativeNpc npc, Chara chara)
        {
            if (npc == null)
            {
                return false;
            }

            string role = For(chara);
            if (role == null || npc.Occupation == role)
            {
                return false;
            }

            npc.Occupation = role;
            return true;
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
                if (Apply(npc, bindings.ResolveChara(npc.Id)))
                {
                    updated++;
                }
            }

            if (updated > 0)
            {
                log?.LogInfo("Recognised " + updated + " character(s) as holding authority.");
            }
        }
    }
}
