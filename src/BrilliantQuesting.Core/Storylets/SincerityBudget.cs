using System;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// BQ-127: session presentation telemetry, not story history or speech veracity.
    /// A host keeps one instance across scenes and acknowledges only successful presentation.
    /// No clock, random draw, world mutation or saved content is involved.
    /// </summary>
    public sealed class SincerityBudget
    {
        /// <summary>Authored storylet ToneTags metadata; never a fragment tone or player-facing label.</summary>
        public const string RareSincerityTag = "rare_sincerity";
        public const int RequiredOtherScenes = 9;

        private bool _presenting;
        public long PresentedScenes { get; private set; }
        public long SincereScenes { get; private set; }
        public int OtherScenesSinceSincerity { get; private set; }
        public double? Rate => PresentedScenes == 0 ? (double?)null : (double)SincereScenes / PresentedScenes;

        public static bool IsRareSincerity(StoryletDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return definition.ToneTags.Contains(RareSincerityTag);
        }

        /// <summary>Read-only admission. Inspecting or finding candidates cannot earn allowance.</summary>
        public bool Allows(StoryletDefinition definition)
        {
            return !IsRareSincerity(definition) || OtherScenesSinceSincerity >= RequiredOtherScenes;
        }

        /// <summary>
        /// Recheck immediately before presentation. The callback returns true only once the host
        /// has presented the scene; false or an exception spends/earns nothing. Simulated firings,
        /// unpresented previews and off-screen scenes do not call this method. A declined proposal does not
        /// alter scene eligibility, facts, knowledge or consequences. Nested delivery is refused.
        /// </summary>
        public bool TryPresent(StoryletDefinition definition, Func<bool> present)
        {
            bool sincere = IsRareSincerity(definition);
            if (present == null) throw new ArgumentNullException(nameof(present));
            if (_presenting || !Allows(definition)) return false;
            _presenting = true;
            try
            {
                if (!present()) return false;
                PresentedScenes++;
                if (sincere)
                {
                    SincereScenes++;
                    OtherScenesSinceSincerity = 0;
                }
                else if (OtherScenesSinceSincerity < RequiredOtherScenes)
                {
                    OtherScenesSinceSincerity++;
                }
                return true;
            }
            finally
            {
                _presenting = false;
            }
        }
    }
}
