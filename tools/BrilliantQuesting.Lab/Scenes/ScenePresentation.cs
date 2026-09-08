using System.IO;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Scenes
{
    /// <summary>
    /// Headless presentation review surface. Only acknowledged wording and speaker names cross
    /// this boundary; intent, casting, budget and routing metadata belong to the inspector.
    /// This is not evidence of live Elin presentation or player knowledge eligibility.
    /// </summary>
    public static class ScenePresentation
    {
        public static bool Write(TextWriter output, NarrativeWorldState world, StoryletPlay play)
        {
            if (!play.Played) return false;
            bool delivered = false;
            foreach (PlayedBeat beat in play.Beats)
            {
                if (!beat.Played || beat.Line == null || !beat.Line.Rendered) continue;
                output.WriteLine(world.Registry.NameOf(beat.Speaker) + ": " + beat.Line.Text);
                delivered = true;
            }
            return delivered;
        }
    }
}
