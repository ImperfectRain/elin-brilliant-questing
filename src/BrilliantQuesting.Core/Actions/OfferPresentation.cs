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
                    return 0;

                // Earns the standing or the proof the resolutions need.
                case "question":
                case "search":
                    return 1;

                // Moves someone who is not yet willing.
                case "persuade":
                case "intimidate":
                case "bribe":
                case "pickpocket":
                    return 2;

                // Real routes - standing rule 14, a valid ugly solution is still a solution - but
                // never worth displacing one that resolves.
                case "rapport":
                case "lie":
                case "frame":
                case "attack":
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
