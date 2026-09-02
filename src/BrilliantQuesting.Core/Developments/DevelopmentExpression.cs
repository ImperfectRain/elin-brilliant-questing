using System.Collections.Generic;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// Step 7 to step 8 of the expression pipeline (CD §37): the one seam between a pressure and
    /// the authored patterns that might dramatize it.
    ///
    /// It exists so the direction of the dependency is fixed and visible. Developments know that
    /// storylets are one thing a pressure can turn into; storylets know nothing about
    /// developments, and neither knows about scenes. Nothing here casts, scores, stages or fires
    /// anything - it hands the storylet engine the thread and the focus the pressure already names
    /// and lets the engine's own preconditions answer.
    ///
    /// Returning nothing is a correct and common answer, and it is not a failure: a pressure with
    /// no claim at its centre has nothing to build roles around, a pressure whose thread is
    /// settled has no live scene to check against, and a town where nobody qualifies casts
    /// nobody. In every one of those cases the development still exists, the world still holds the
    /// pressure, and no quest, thread or fact is invented to give it somewhere to go.
    /// </summary>
    public static class DevelopmentExpression
    {
        private static readonly IReadOnlyList<StoryletOpportunity> Nothing = new StoryletOpportunity[0];

        public static IReadOnlyList<StoryletOpportunity> Opportunities(
            StoryletEngine engine,
            NarrativeWorldState world,
            IVanillaState vanilla,
            Development development)
        {
            if (engine == null || world == null || vanilla == null || development == null)
            {
                return Nothing;
            }

            if (!development.CanBeExpressedAsStorylet)
            {
                return Nothing;
            }

            NarrativeThread thread = world.GetThread(development.ThreadId);
            if (thread == null)
            {
                return Nothing;
            }

            return engine.Find(new StoryletCastingContext(world, vanilla, thread, development.FocusFactId));
        }
    }
}
