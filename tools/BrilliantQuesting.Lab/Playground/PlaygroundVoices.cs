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
    /// and <see cref="VoiceProfile.RequestedIdiolect"/> turn the axes into
    /// <see cref="DialogueTones"/> and <see cref="DialogueIdiolect"/> tags, and nothing here
    /// interprets them further, which is why <see cref="Describe"/> and
    /// <see cref="DescribeIdiolect"/> read the requested tags back rather than printing prose of
    /// their own.
    ///
    /// <b>The last two voices exist to isolate BQ-142.</b> They sit at the middle of all four tonal
    /// axes, so they request no tone at all and differ from each other in nothing but length,
    /// cadence and figuration. Comparing them is the only way to see idiolect on its own: a voice
    /// that moved tone as well would leave "the wording changed" with two possible causes, which is
    /// exactly the reading a sweep exists to prevent.
    /// </summary>
    public static class PlaygroundVoices
    {
        public const string Neutral = "neutral";
        public const string FormalCold = "formal-cold";
        public const string PlainBlunt = "plain-blunt";
        public const string WarmOpen = "warm-open";
        public const string WryGuarded = "wry-guarded";
        public const string TerseLiteral = "terse-literal";
        public const string ExpansiveFigurative = "expansive-figurative";

        private static readonly string[] Names =
        {
            Neutral, FormalCold, PlainBlunt, WarmOpen, WryGuarded, TerseLiteral, ExpansiveFigurative
        };

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
                case TerseLiteral:
                    return new VoiceProfile { Verbosity = 0.1, Cadence = 0.1, Figuration = 0.1 };
                case ExpansiveFigurative:
                    return new VoiceProfile { Verbosity = 0.9, Cadence = 0.9, Figuration = 0.9 };
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

            return Join(voice.RequestedTone(), "no tonal constraint");
        }

        /// <summary>
        /// The linguistic habits this voice actually asks for (BQ-142), as production derives them.
        /// Empty reads as "no habit requested", which is the honest description of the five voices
        /// that predate the vocabulary as well as of a neutral one.
        /// </summary>
        public static string DescribeIdiolect(VoiceProfile voice)
        {
            if (voice == null)
            {
                return "no voice";
            }

            return Join(voice.RequestedIdiolect(), "no habit requested");
        }

        private static string Join(IReadOnlyList<string> tags, string ifEmpty)
        {
            if (tags.Count == 0)
            {
                return ifEmpty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" / ");
                }

                sb.Append(tags[i]);
            }

            return sb.ToString();
        }
    }
}
