using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Dialogue;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// A handful of named <see cref="VoiceProfile"/>s for the playground to hand a speaker.
    ///
    /// <b>This is laboratory authorship, not a production authority, and it has to stay labelled
    /// as such.</b> BQ-075 says in as many words that a profile "is simply given to whoever is
    /// speaking, by whatever assigns one - which is a later, content- or character-authoring
    /// concern this step does not reach." Nothing in Core assigns one today, so the playground has
    /// to supply one to show the axis at all, and the alternative - deriving a voice from an
    /// occupation, a race or an archetype - is exactly the stereotype the same step refuses. These
    /// four numbers are therefore an input a developer chooses, reported as such by
    /// <see cref="PlaygroundAvailability"/>, and never a claim that the simulation picked them.
    ///
    /// What a voice does with them is entirely production's: <see cref="VoiceProfile.RequestedTone"/>
    /// turns the axes into <see cref="DialogueTones"/> tags and nothing here interprets them
    /// further, which is why <see cref="Describe"/> reads the requested tags back rather than
    /// printing prose of its own.
    /// </summary>
    public static class PlaygroundVoices
    {
        public const string Neutral = "neutral";
        public const string FormalCold = "formal-cold";
        public const string PlainBlunt = "plain-blunt";
        public const string WarmOpen = "warm-open";
        public const string WryGuarded = "wry-guarded";

        private static readonly string[] Names = { Neutral, FormalCold, PlainBlunt, WarmOpen, WryGuarded };

        /// <summary>Every voice a caller may name, in the order <c>describe</c> prints them.</summary>
        public static IReadOnlyList<string> All => Names;

        /// <summary>The profile behind a name, or null when nothing is registered under it.</summary>
        public static VoiceProfile Find(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case Neutral:
                    return VoiceProfile.Neutral;
                case FormalCold:
                    return new VoiceProfile { Formality = 0.9, Directness = 0.8, Warmth = 0.1, Sarcasm = 0.5 };
                case PlainBlunt:
                    return new VoiceProfile { Formality = 0.1, Directness = 0.9, Warmth = 0.4, Sarcasm = 0.5 };
                case WarmOpen:
                    return new VoiceProfile { Formality = 0.2, Directness = 0.3, Warmth = 0.9, Sarcasm = 0.2 };
                case WryGuarded:
                    return new VoiceProfile { Formality = 0.6, Directness = 0.1, Warmth = 0.3, Sarcasm = 0.9 };
                default:
                    return null;
            }
        }

        /// <summary>
        /// The tone this voice actually asks for, as production derives it. An empty request reads
        /// as "no tonal constraint", which is the honest description of a neutral voice rather than
        /// a missing one.
        /// </summary>
        public static string Describe(VoiceProfile voice)
        {
            if (voice == null)
            {
                return "no voice";
            }

            IReadOnlyList<string> tone = voice.RequestedTone();
            if (tone.Count == 0)
            {
                return "no tonal constraint";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tone.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" / ");
                }

                sb.Append(tone[i]);
            }

            return sb.ToString();
        }
    }
}
