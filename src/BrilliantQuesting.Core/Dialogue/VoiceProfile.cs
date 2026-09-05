using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// The closed vocabulary of stable linguistic habit a fragment may declare, and the tags
    /// <see cref="RealizationRequest.Idiolect"/> asks for (BQ-142).
    ///
    /// Three axes, six marked poles, and nothing else. <see cref="DialogueTones"/> already owns how
    /// a line is <em>pitched</em> - warm, curt, formal, wry - and that is affect: the same speaker
    /// is warm on Tuesday and cold on Wednesday without becoming a different person. This owns how
    /// a speaker <em>habitually builds a sentence</em>, which is the half of "voice" tone cannot
    /// carry, because it does not move with the mood:
    ///
    /// <list type="bullet">
    /// <item><b>length</b> - <see cref="Terse"/> against <see cref="Expansive"/>: how much wording
    /// the point is given;</item>
    /// <item><b>cadence</b> - <see cref="Clipped"/> against <see cref="Flowing"/>: whether the
    /// phrasing stops and restarts or is built into one carried sentence;</item>
    /// <item><b>figuration</b> - <see cref="Literal"/> against <see cref="Figurative"/>: whether
    /// the point is named or made through an image.</item>
    /// </list>
    ///
    /// <b>Register is deliberately absent, because it is already here.</b> CD §19's list is
    /// sentence length, formality, directness, hedging, sarcasm and metaphor use; formality
    /// <em>is</em> register, and <see cref="VoiceProfile.Formality"/> has requested
    /// <see cref="DialogueTones.Formal"/> against <see cref="DialogueTones.Plain"/> since BQ-075.
    /// A second word-stock axis beside it would be two names for one question and the beginning of
    /// a parallel voice system, which is the thing this step exists not to build. What BQ-075 left
    /// open by name - "sentence length and metaphor use ... a seam for whenever the fragment pool
    /// grows enough to need them" - is exactly length and figuration; cadence is the third because
    /// the corpus visibly separates "No. Find another ear." from "There are doors in this
    /// conversation I am not opening." and nothing could ask for one over the other.
    ///
    /// <b>The axes are orthogonal, and the corpus proves it rather than the comment.</b> Length is
    /// how much is said and cadence is how it is joined, so all four corners exist and are
    /// authored: "No. Find another ear." is terse and clipped, "That part is not for you." is
    /// terse and unbroken, "You ask a great deal for somebody bringing nothing." is expansive and
    /// carried, and a line may be terse and figurative at once - an ear standing in for a listener
    /// costs no extra words.
    ///
    /// <b>Every tag has an opposite, unlike tone.</b> <see cref="DialogueTones.Wry"/> has none
    /// because sincerity is the unmarked baseline; here both ends of all three axes are things a
    /// line visibly is, so <see cref="Opposite"/> is total over the vocabulary. A mark can
    /// therefore always be contradicted by a voice that took the other pole, and there is no tag
    /// that quietly fits everybody.
    /// </summary>
    public static class DialogueIdiolect
    {
        /// <summary>As few words as will carry it.</summary>
        public const string Terse = "terse";

        /// <summary>The thought filled out rather than pared down.</summary>
        public const string Expansive = "expansive";

        /// <summary>Short stops. Sentences that end and begin again.</summary>
        public const string Clipped = "clipped";

        /// <summary>One sentence, carried through to its end.</summary>
        public const string Flowing = "flowing";

        /// <summary>The point named as itself.</summary>
        public const string Literal = "literal";

        /// <summary>The point made through an image.</summary>
        public const string Figurative = "figurative";

        public static IReadOnlyList<string> Vocabulary { get; } = new[]
        {
            Terse, Expansive, Clipped, Flowing, Literal, Figurative
        };

        /// <summary>
        /// The tag at the other end of the same axis. Total over
        /// <see cref="Vocabulary"/> and null for anything outside it, so a tag this vocabulary does
        /// not own is left alone rather than paired with a guess.
        /// </summary>
        public static string Opposite(string tag)
        {
            switch (tag)
            {
                case Terse:
                    return Expansive;
                case Expansive:
                    return Terse;
                case Clipped:
                    return Flowing;
                case Flowing:
                    return Clipped;
                case Literal:
                    return Figurative;
                case Figurative:
                    return Literal;
                default:
                    return null;
            }
        }

        public static bool IsIdiolect(string tag)
        {
            for (int i = 0; i < Vocabulary.Count; i++)
            {
                if (string.Equals(Vocabulary[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The two voice vocabularies read as one, for the single reader that has to ask about both -
    /// <see cref="DialogueFragment.FitsVoice"/> (BQ-149).
    ///
    /// A fragment may declare that a way of speaking is not merely compatible with it but
    /// <em>required</em> by it: somebody would only say this line if they were the sort of person
    /// who talks this way. The trait it names is always one a <see cref="VoiceProfile"/> can ask
    /// for, because that is the whole of what a persistent linguistic trait is here, and the two
    /// lists a voice produces are <see cref="DialogueTones"/>' and <see cref="DialogueIdiolect"/>'s.
    /// So this is a union of two closed vocabularies and never a third one: nothing is named here
    /// that is not already named there, and widening what a line may demand means widening one of
    /// the two rather than this.
    ///
    /// <b>A disposition is deliberately not demandable.</b> <c>PersonalityWeights</c> already
    /// decides what a character wants and how forthcoming they are, and <c>D034</c> keeps voice and
    /// personality from reading each other. A demand on a disposition would be personality reaching
    /// the words a second time, after it had already reached the decision that produced them - so
    /// what a line may require of its speaker is how they talk, never what they want.
    /// </summary>
    public static class DialogueVoiceTraits
    {
        /// <summary>Whether a voice could ever ask for this tag.</summary>
        public static bool IsTrait(string tag)
        {
            return DialogueTones.IsTone(tag) || DialogueIdiolect.IsIdiolect(tag);
        }

        /// <summary>
        /// The tag at the other end of the same axis, in whichever vocabulary owns it. Null for a
        /// tag neither owns, and null for <see cref="DialogueTones.Wry"/>, whose axis has an
        /// unmarked far end - which is exactly why wryness has to be <em>demanded</em> to be
        /// narrowed on at all, and cannot be narrowed on by contradiction the way warmth can.
        /// </summary>
        public static string Opposite(string tag)
        {
            return DialogueTones.Opposite(tag) ?? DialogueIdiolect.Opposite(tag);
        }
    }

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
    /// ever reaches <see cref="RealizationRequest.Tone"/> and
    /// <see cref="RealizationRequest.Idiolect"/>, the two places BQ-074 and BQ-142 already
    /// restricted to narrowing choice among ways of saying a thing.
    ///
    /// <b>Deliberately not derived from anything about who the speaker is.</b> There is no
    /// constructor and no factory that reads a race, an archetype, an occupation or a hobby into a
    /// profile - a table from "innkeeper" to "warm" is exactly the stereotype BQ-076 is written to
    /// avoid by reading only work actually observed, and voice sits a layer below where any of
    /// those labels live. A profile is simply given to whoever is speaking, by whatever assigns
    /// one - which is a later, content- or character-authoring concern this step does not reach.
    ///
    /// <b>Seven axes in two vocabularies, and the split is the point.</b>
    /// <see cref="Formality"/>, <see cref="Directness"/> and <see cref="Sarcasm"/> each pick out,
    /// at their extremes, one of the six <see cref="DialogueTones"/> tags BQ-074 shipped besides
    /// warm and cold; <see cref="Warmth"/> picks out those two. Between them, every tag in the
    /// tonal vocabulary has exactly one axis that can request it. <see cref="Verbosity"/>,
    /// <see cref="Cadence"/> and <see cref="Figuration"/> do the same for
    /// <see cref="DialogueIdiolect"/>, and they are a second list rather than three more entries in
    /// the first because they are a different kind of fact: tone is a pitch a speaker can take
    /// today and not tomorrow, and idiolect is a habit that makes two people saying the identical
    /// thing in the identical mood still sound like two people. Both reach realization the same
    /// way - a request that can only narrow - which is what keeps the second vocabulary from being
    /// a second system.
    /// </summary>
    public sealed class VoiceProfile
    {
        private const double Low = 0.35;
        private const double High = 0.65;

        /// <summary>No preference on any axis - requesting nothing narrows nothing.</summary>
        public static readonly VoiceProfile Neutral = new VoiceProfile();

        /// <summary>0 = plain and unadorned, 1 = formal and elevated. This is register.</summary>
        public double Formality { get; set; } = 0.5;

        /// <summary>0 = indirect and hedging, 1 = blunt and to the point.</summary>
        public double Directness { get; set; } = 0.5;

        /// <summary>0 = sincere, 1 = wry. Sincerity has no tone tag of its own; it is the baseline.</summary>
        public double Sarcasm { get; set; } = 0.5;

        /// <summary>0 = cold, 1 = warm.</summary>
        public double Warmth { get; set; } = 0.5;

        /// <summary>0 = terse, 1 = expansive. How much wording a point is given (BQ-142).</summary>
        public double Verbosity { get; set; } = 0.5;

        /// <summary>0 = clipped, 1 = flowing. How the phrasing is joined (BQ-142).</summary>
        public double Cadence { get; set; } = 0.5;

        /// <summary>0 = literal, 1 = figurative. Whether the point is named or pictured (BQ-142).</summary>
        public double Figuration { get; set; } = 0.5;

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

        /// <summary>
        /// The linguistic habits this voice asks for - <see cref="DialogueIdiolect"/> tags for
        /// <see cref="RealizationRequest.Idiolect"/> (BQ-142). Pure and deterministic on exactly
        /// the same terms as <see cref="RequestedTone"/>, and read by exactly the same kind of
        /// check, so the guarantee is the same one: a request can remove ways of saying the point
        /// and can never reach the point itself.
        ///
        /// A voice sitting in the middle of an axis asks nothing on it, which is what makes a
        /// partly specified idiolect - somebody notably terse and unremarkable in everything else -
        /// expressible rather than rounded to a caricature.
        /// </summary>
        public IReadOnlyList<string> RequestedIdiolect()
        {
            List<string> idiolect = new List<string>(3);
            AddAtExtreme(idiolect, Verbosity, DialogueIdiolect.Expansive, DialogueIdiolect.Terse);
            AddAtExtreme(idiolect, Cadence, DialogueIdiolect.Flowing, DialogueIdiolect.Clipped);
            AddAtExtreme(idiolect, Figuration, DialogueIdiolect.Figurative, DialogueIdiolect.Literal);
            return idiolect;
        }

        private static void AddAtExtreme(List<string> tags, double value, string atHigh, string atLow)
        {
            if (value >= High)
            {
                tags.Add(atHigh);
            }
            else if (value <= Low)
            {
                tags.Add(atLow);
            }
        }
    }
}
