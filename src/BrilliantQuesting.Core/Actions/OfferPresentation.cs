using System.Collections.Generic;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Decides which offers a presentation surface shows when it cannot show them all.
    ///
    /// Any surface with a choice limit - Drama's dialogue node today, a menu or a board later -
    /// has to drop something. Doing that by registration order is the trap: the verbs that end a
    /// situation are registered last, so a plain cap keeps the ones that stir the problem and
    /// silently hides the only routes out of it. The player then reads a dead end that the
    /// simulation does not actually have.
    ///
    /// Ranking is by what a verb *does*, and the order is stable within a rank so the same world
    /// state always produces the same list.
    /// </summary>
    public static class OfferPresentation
    {
        /// <summary>Rank given to any verb this table does not name. Shown last, dropped first.</summary>
        public const int UnrankedTier = 4;

        /// <summary>Lower sorts earlier and survives truncation.</summary>
        public static int Rank(string actionId)
        {
            switch (actionId)
            {
                // Resolves the situation. Hiding one of these is hiding the ending.
                case "return_item":
                case "keep_item":
                case "expose":
                case "report":
                case "pay_debt":
                // The crafting family's guaranteed route. It answers any demand and works from
                // anything, so it is available wherever a named craft is - which makes it the one
                // production verb whose absence would leave a maker with no route at all, and the
                // one that belongs on the tier that must never be dropped.
                case "craft_to_property":
                    return 0;

                // Earns the standing or the proof the resolutions need.
                case "question":
                case "search":
                case "inspect":
                case "examine_corpse":
                case "read":
                case "translate":
                case "identify_substance":
                case "search_records":
                case "compare_testimony":
                case "track":
                // Criminal routes to the same two things. Breaking in is what makes a locked
                // place searchable and having papers made is what makes an unprovable belief
                // provable, so on the "earns the proof" tier they sit exactly where the honest
                // verbs that do the same job sit. A surface that quietly hid them would be
                // telling a criminal build it had no route, which is the failure this whole
                // table exists to prevent.
                case "trespass":
                case "forge":
                // The named crafts. Each is a better route than the generalist where it applies -
                // a cook reads Cooking rather than Handicraft - but never the only one, because
                // `craft_to_property` is available everywhere they are. Dropping one costs the
                // player the specialist's odds, not the ending, so they rank below the verbs whose
                // absence would hide a route entirely.
                case "cook":
                case "brew":
                case "alchemy":
                case "build":
                case "repair":
                    return 1;

                // Moves someone who is not yet willing.
                case "persuade":
                case "intimidate":
                case "bribe":
                case "pickpocket":
                case "extort":
                case "impersonate":
                    return 2;

                // Real routes - standing rule 14, a valid ugly solution is still a solution - but
                // never worth displacing one that resolves.
                case "rapport":
                case "lie":
                case "frame":
                case "attack":
                // Reconnaissance. Real routes to proof, but speculative and aimed past the person
                // in front of the player, so they never displace a verb that moves the situation.
                case "follow":
                case "eavesdrop":
                // Disposal and damage. Each is somebody's whole answer to some problem, and none
                // of them advances the one in front of the player - covering your tracks is what
                // you do instead of solving it.
                case "fence":
                case "smuggle":
                case "sabotage":
                case "destroy_evidence":
                    return 3;

                default:
                    return UnrankedTier;
            }
        }

        /// <summary>
        /// The available offers a surface should show, best first, capped at <paramref name="max"/>.
        /// Unavailable offers are the caller's business and are not filtered here.
        /// </summary>
        public static List<ActionOffer> TakeForDisplay(IReadOnlyList<ActionOffer> offers, int max)
        {
            List<ActionOffer> ordered = new List<ActionOffer>();
            if (offers == null || max <= 0)
            {
                return ordered;
            }

            for (int rank = 0; rank <= UnrankedTier && ordered.Count < max; rank++)
            {
                for (int i = 0; i < offers.Count && ordered.Count < max; i++)
                {
                    if (offers[i] != null && Rank(offers[i].Action.Id) == rank)
                    {
                        ordered.Add(offers[i]);
                    }
                }
            }

            return ordered;
        }
    }
}
