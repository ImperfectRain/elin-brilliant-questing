using System;
using System.Collections.Generic;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// The closed vocabulary of <em>manners</em> - ways of speaking a personal line rules out -
    /// and the second reader of <see cref="DialogueFragment.Tags"/> after BQ-076's vocabulary
    /// tags (BQ-077).
    ///
    /// A manner is not a tone. A tone is how somebody sounds and any speaker can want any of
    /// them; a manner is a thing a particular speaker will not do with words, and the only thing
    /// that ever rules one out is a <see cref="PersonalProhibition"/> that is currently holding.
    /// Keeping them in a separate closed list rather than as more <see cref="DialogueTones"/> is
    /// what stops a voice profile from being able to ask for one: nothing requests a manner, and
    /// the only operation on the vocabulary is exclusion.
    ///
    /// <b>One entry, because exactly one prohibition in this vocabulary has a wording face that
    /// can fire.</b> <see cref="PersonalProhibition.NeverInvolvesAuthority"/> and
    /// <see cref="PersonalProhibition.NeverLiesDirectly"/> forbid moves rather than registers, and
    /// are handled where those moves are chosen. <see cref="PersonalProhibition.NeverSpeaksBadlyOfFamily"/>
    /// looks like it should have one and does not, for a reason worth writing down: a line that
    /// takes the claim itself off the table leaves no sentence behind to filter, so a manner paired
    /// with it could never apply to anything. Authoring one anyway would be a rule that fires never
    /// - the same mistake BQ-074 declined to make with tone and BQ-075 with sentence length.
    ///
    /// <see cref="PersonalProhibition.NeverBegs"/> is the one that reaches both surfaces, and the
    /// two halves are genuinely different: it takes asking others to carry the trouble off the
    /// table as an <em>action</em>, and it takes the appealing register out of the questions the
    /// same person is still perfectly willing to ask. Neither half stands in for the other.
    /// </summary>
    public static class DialogueManners
    {
        /// <summary>Asking to be helped, in the appealing register. Ruled out by <see cref="PersonalProhibition.NeverBegs"/>.</summary>
        public const string Pleading = "pleading";

        public static IReadOnlyList<string> Vocabulary { get; } = new[] { Pleading };

        public static bool IsManner(string tag)
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

        /// <summary>
        /// The manner a prohibition rules out, or an empty string for a prohibition that has no
        /// wording face.
        /// </summary>
        public static string ForbiddenBy(PersonalProhibition kind)
        {
            switch (kind)
            {
                case PersonalProhibition.NeverBegs:
                    return Pleading;
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// BQ-077 reaching wording: which manners a speaker's still-holding lines take away from the
    /// fragment pool.
    ///
    /// <b>Realization is handed rulings, never a profile.</b> That is the whole shape of this
    /// type and the reason it takes an <see cref="IReadOnlyList{T}"/> of
    /// <see cref="ProhibitionRuling"/> rather than a <see cref="NegativeSpaceProfile"/>: a line
    /// that broke where the decision was taken must not still gag the words, and a line the
    /// decision layer never consulted must not be enforced here for the first time. Wording
    /// carries out a constraint that was already decided; it never decides one, and it is never
    /// the only place one applies - a prohibited semantic move is refused where it would have
    /// been selected, so it never reaches a request at all.
    ///
    /// The consequence worth stating plainly: this cannot hide a prohibited action inside an
    /// acceptable sentence, because by the time there is a sentence the action was already either
    /// permitted or never chosen.
    /// </summary>
    public static class NegativeSpaceVoice
    {
        private static readonly string[] None = new string[0];

        /// <summary>
        /// The manner tags ruled out by the rulings that are still holding, in vocabulary order.
        /// Empty when nothing holds - which is also what a speaker with no lines produces.
        /// </summary>
        public static IReadOnlyList<string> ForbiddenManners(IReadOnlyList<ProhibitionRuling> rulings)
        {
            if (rulings == null || rulings.Count == 0)
            {
                return None;
            }

            // Built in vocabulary order rather than in ruling order, so two callers that settled
            // the same lines in a different sequence hand the realizer the identical list.
            List<string> manners = null;
            IReadOnlyList<string> vocabulary = DialogueManners.Vocabulary;
            for (int i = 0; i < vocabulary.Count; i++)
            {
                for (int j = 0; j < rulings.Count; j++)
                {
                    if (!rulings[j].Forbids
                        || !string.Equals(DialogueManners.ForbiddenBy(rulings[j].Kind), vocabulary[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (manners == null)
                    {
                        manners = new List<string>();
                    }

                    manners.Add(vocabulary[i]);
                    break;
                }
            }

            return manners == null ? (IReadOnlyList<string>)None : manners;
        }
    }
}
