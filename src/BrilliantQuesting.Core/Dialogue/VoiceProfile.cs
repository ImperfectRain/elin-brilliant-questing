using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// How a speaker sounds, independent of what they mean or why they said it (CD §19).
    ///
    /// BQ-074 left this seam open on purpose. <see cref="RealizationRequest.Tone"/> asks for no
    /// tonal constraint by default, and <see cref="DialogueTones"/> was kept to seven tags "while
    /// the tonal vocabulary grows around it." <see cref="VoiceProfile"/> is what grows around it: a
    /// per-speaker constant that turns into the tone a line is requested in, via
    /// <see cref="RequestedTone"/>, so the identical disclosure decision reaches
    /// <see cref="DialogueRealizer"/> worded differently depending on who is saying it - and only
    /// on who is saying it. It narrows <see cref="DialogueFragmentLibrary"/> candidates through the
    /// same <see cref="DialogueFragment.FitsTone"/> check every tone request already goes through,
    /// so it inherits that check's guarantee for free: a fragment's <em>eligibility</em> can move,
    /// but nothing about which act, stance, claim or slot a fragment reads ever does, which is why
    /// <see cref="RealizedLine.Meaning"/> cannot change because of a voice.
    ///
    /// <b>Voice is a wording constraint, not a second personality.</b>
    /// <see cref="World.PersonalityWeights"/> decides what a character wants to do; this decides how
    /// what they say comes out. Neither reads the other: two speakers who want the identical thing
    /// can sound nothing alike, and two who want opposite things can sound the same. Nothing here
    /// touches <see cref="DisclosureDecision"/> or <see cref="SpeechAct"/> either - a profile only
    /// ever reaches <see cref="RealizationRequest.Tone"/>, the one place BQ-074 already restricted
    /// to narrowing choice among ways of saying a thing.
    ///
    /// <b>Deliberately not derived from anything about who the speaker is.</b> There is no
    /// constructor and no factory that reads a race, an archetype, an occupation or a hobby into a
    /// profile - a table from "innkeeper" to "warm" is exactly the stereotype BQ-076 is written to
    /// avoid by reading only work actually observed, and voice sits a layer below where any of
    /// those labels live. A profile is simply given to whoever is speaking, by whatever assigns
    /// one - which is a later, content- or character-authoring concern this step does not reach.
    ///
    /// <b>Four axes, not CD §19's whole list.</b> <see cref="Formality"/>, <see cref="Directness"/>
    /// and <see cref="Sarcasm"/> each pick out, at their extremes, one of the six
    /// <see cref="DialogueTones"/> tags BQ-074 shipped besides warm and cold; <see cref="Warmth"/>
    /// picks out those two. Between them, every tag in the vocabulary has exactly one axis that can
    /// request it. Sentence length and metaphor use are in CD §19's struct and in this step's
    /// roadmap line, but no shipped fragment carries a length or figuration marker to choose
    /// between - adding tags nothing yet uses would be authoring vocabulary for a system that does
    /// not exist, the exact thing BQ-074 declined to do with tone itself. They are a seam for
    /// whenever the fragment pool grows enough to need them, not fields with nothing behind them.
    /// </summary>
    public sealed class VoiceProfile
    {
        private const double Low = 0.35;
        private const double High = 0.65;

        /// <summary>No tonal preference on any axis - requesting nothing narrows nothing.</summary>
        public static readonly VoiceProfile Neutral = new VoiceProfile();

        /// <summary>0 = plain and unadorned, 1 = formal and elevated.</summary>
        public double Formality { get; set; } = 0.5;

        /// <summary>0 = indirect and hedging, 1 = blunt and to the point.</summary>
        public double Directness { get; set; } = 0.5;

        /// <summary>0 = sincere, 1 = wry. Sincerity has no tone tag of its own; it is the baseline.</summary>
        public double Sarcasm { get; set; } = 0.5;

        /// <summary>0 = cold, 1 = warm.</summary>
        public double Warmth { get; set; } = 0.5;

        /// <summary>
        /// The tone this voice asks a line to be said in - <see cref="DialogueTones"/> tags for
        /// <see cref="RealizationRequest.Tone"/>, and nothing else. Pure and deterministic: the same
        /// four numbers always request the same tags, in the same order, regardless of the act,
        /// the decision or any world state, which is what lets a voice be a fact about a speaker
        /// rather than a fact about a conversation.
        /// </summary>
        public IReadOnlyList<string> RequestedTone()
        {
            List<string> tone = new List<string>(4);
            AddAtExtreme(tone, Formality, DialogueTones.Formal, DialogueTones.Plain);
            AddAtExtreme(tone, Directness, DialogueTones.Curt, DialogueTones.Wary);
            AddAtExtreme(tone, Warmth, DialogueTones.Warm, DialogueTones.Cold);
            if (Sarcasm >= High)
            {
                tone.Add(DialogueTones.Wry);
            }

            return tone;
        }

        private static void AddAtExtreme(List<string> tone, double value, string atHigh, string atLow)
        {
            if (value >= High)
            {
                tone.Add(atHigh);
            }
            else if (value <= Low)
            {
                tone.Add(atLow);
            }
        }
    }
}
