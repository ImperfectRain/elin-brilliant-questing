using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// How central an absurd premise is to a piece of content (CD §22.2). Ordinal, so a ceiling can
    /// be compared against it directly.
    /// </summary>
    public enum WeirdnessLevel
    {
        /// <summary>Nothing absurd. The default for a fragment that carries no weirdness tag.</summary>
        Mundane = 0,

        /// <summary>One odd detail, nothing the scene is about.</summary>
        OddDetail = 1,

        /// <summary>Distinctly Elin, still incidental.</summary>
        DistinctlyElin = 2,

        /// <summary>An absurd premise the scene is actually about.</summary>
        AbsurdPremiseCentral = 3,

        /// <summary>A rare fever-dream event.</summary>
        FeverDream = 4
    }

    /// <summary>
    /// The closed taxonomy of what an absurd premise is about (CD §22.1), and the third reader of
    /// <see cref="DialogueFragment.Tags"/> after BQ-076's vocabulary and BQ-077's manners.
    ///
    /// Two disjoint tag families live in the same free <see cref="DialogueFragment.Tags"/> list: a
    /// category tag says what kind of absurd premise a fragment is grounded in, and a level tag
    /// says how central it is (<see cref="WeirdnessLevel"/>). Both are read from actual content, the
    /// same way BQ-076's domains and BQ-077's manners are - never invented by this layer, which
    /// words nothing on its own. A fragment carrying neither is <see cref="WeirdnessLevel.Mundane"/>
    /// and belongs to no category, which is the ordinary case nearly every fragment already is.
    /// </summary>
    public static class DialogueWeirdness
    {
        public const string Bureaucratic = "weird_bureaucratic";
        public const string Biological = "weird_biological";
        public const string Religious = "weird_religious";
        public const string Domestic = "weird_domestic";
        public const string Criminal = "weird_criminal";
        public const string Economic = "weird_economic";
        public const string Adventurer = "weird_adventurer";
        public const string Cosmic = "weird_cosmic";

        /// <summary>
        /// The prefix marking which absurd premise a fragment is about, as opposed to which
        /// <see cref="Categories">category</see> of premise it belongs to.
        ///
        /// A category is a taxonomy - "this is bureaucratic weirdness" - and CD §22's formula asks
        /// for one absurd <em>premise</em>, not one absurd genre. Two unrelated bizarre tax
        /// premises are both bureaucratic, so a category on its own cannot tell follow-on material
        /// about one premise apart from the start of a second, which is the whole of what the
        /// anti-stacking rule has to decide. A tag such as "premise_tax_on_ghosts" names the
        /// premise itself, and every fragment that is part of the same premise carries the same
        /// one.
        ///
        /// It lives in the free <see cref="DialogueFragment.Tags"/> list beside the category and
        /// level families, disjoint from both and from BQ-076's vocabulary and BQ-077's manners,
        /// and it is read from authored content exactly as they are. Nothing here decides what an
        /// absurd premise is or when two fragments belong to the same one - the author does, by
        /// tagging them alike. That is deliberately not an ontology of comedy: the vocabulary is
        /// open, the tag is opaque to this layer, and its only meaning is "same string, same
        /// premise".
        /// </summary>
        public const string PremisePrefix = "premise_";

        public const string Level1 = "weird_level_1";
        public const string Level2 = "weird_level_2";
        public const string Level3 = "weird_level_3";
        public const string Level4 = "weird_level_4";

        public static IReadOnlyList<string> Categories { get; } = new[]
        {
            Bureaucratic, Biological, Religious, Domestic, Criminal, Economic, Adventurer, Cosmic
        };

        private static readonly string[] LevelTags = { Level1, Level2, Level3, Level4 };

        public static bool IsCategory(string tag)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                if (string.Equals(Categories[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLevelTag(string tag)
        {
            for (int i = 0; i < LevelTags.Length; i++)
            {
                if (string.Equals(LevelTags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The highest level tag a fragment carries, or <see cref="WeirdnessLevel.Mundane"/> for none.</summary>
        public static WeirdnessLevel LevelOf(IReadOnlyList<string> tags)
        {
            WeirdnessLevel level = WeirdnessLevel.Mundane;
            if (tags == null)
            {
                return level;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (TryLevel(tags[i], out WeirdnessLevel found) && found > level)
                {
                    level = found;
                }
            }

            return level;
        }

        /// <summary>Whether a tag names an absurd premise rather than a category or a level.</summary>
        public static bool IsPremise(string tag)
        {
            return tag != null
                && tag.Length > PremisePrefix.Length
                && tag.StartsWith(PremisePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The absurd premise a fragment is about, or null when its content named none. Unnamed is
        /// not the same as shared: a fragment that names no premise speaks for no premise but its
        /// own, which is what <see cref="WeirdnessBudget"/> falls back on.
        /// </summary>
        public static string PremiseOf(IReadOnlyList<string> tags)
        {
            if (tags == null)
            {
                return null;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (IsPremise(tags[i]))
                {
                    return tags[i];
                }
            }

            return null;
        }

        /// <summary>The category a fragment's absurd premise belongs to, or null when it carries none.</summary>
        public static string CategoryOf(IReadOnlyList<string> tags)
        {
            if (tags == null)
            {
                return null;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (IsCategory(tags[i]))
                {
                    return tags[i];
                }
            }

            return null;
        }

        private static bool TryLevel(string tag, out WeirdnessLevel level)
        {
            switch (tag)
            {
                case Level1:
                    level = WeirdnessLevel.OddDetail;
                    return true;
                case Level2:
                    level = WeirdnessLevel.DistinctlyElin;
                    return true;
                case Level3:
                    level = WeirdnessLevel.AbsurdPremiseCentral;
                    return true;
                case Level4:
                    level = WeirdnessLevel.FeverDream;
                    return true;
                default:
                    level = WeirdnessLevel.Mundane;
                    return false;
            }
        }
    }

    /// <summary>
    /// BQ-079. One scene's allowance of Elin-style weirdness (CD §22, §22.2, §23): a ceiling drawn
    /// once and skewed toward the low end, and an admission rule that keeps a scene from ever
    /// landing two unrelated absurd premises - CD §22's own formula asks for exactly one.
    ///
    /// Mirrors <see cref="DialogueExpressionHistory"/> in shape: built once per scene, saved
    /// nowhere, and read only through a narrowing that can remove a candidate
    /// <see cref="DialogueFragment.Fits"/> already admitted but can never add one it did not.
    ///
    /// <b>Ordinary content is never gated.</b> A fragment with no weirdness tag reads as
    /// <see cref="WeirdnessLevel.Mundane"/> and is always admissible, budget or no budget - the same
    /// "unmarked fragment stays eligible" rule <see cref="DialogueFragment.FitsVocabulary"/> and
    /// <see cref="DialogueFragment.FitsManner"/> already follow. A budget only ever narrows the tagged
    /// minority, which is what keeps a scene with no weird premise reading as ordinary rather than
    /// as a weirdness roll that happened to come up empty.
    ///
    /// <b>The ceiling bounds level; the category rule bounds count.</b> Both matter: a scene capped
    /// at <see cref="WeirdnessLevel.DistinctlyElin"/> never reaches for an absurd premise at all,
    /// and a scene whose ceiling reaches <see cref="WeirdnessLevel.AbsurdPremiseCentral"/> or higher
    /// still commits to at most one category of premise - CD §22's "one absurd premise", not the
    /// first one the fragment pool happens to offer.
    /// </summary>
    public sealed class WeirdnessBudget
    {
        /// <summary>
        /// Weight per level, Mundane through FeverDream, summing to 100. Ninety percent lands at
        /// Mundane through DistinctlyElin - CD §22.2's "most content should remain 0-2" - and
        /// FeverDream is the rarest by a wide margin, matching "rare fever-dream event".
        /// </summary>
        private static readonly int[] Weights = { 42, 28, 20, 8, 2 };

        public WeirdnessBudget(WeirdnessLevel ceiling)
        {
            Ceiling = ceiling;
        }

        /// <summary>The most this scene may spend. Content above it stays out, whatever it is about.</summary>
        public WeirdnessLevel Ceiling { get; }

        /// <summary>The highest level actually admitted so far. Starts at Mundane.</summary>
        public WeirdnessLevel Spent { get; private set; } = WeirdnessLevel.Mundane;

        /// <summary>
        /// The category the scene's absurd premise belongs to, or null for none yet. Descriptive
        /// taxonomy - it says what kind of weirdness this scene turned out to be about, and is what
        /// a distribution check over generated scenes reads. It is no longer what the anti-stacking
        /// rule gates on; <see cref="AdmittedPremise"/> is.
        /// </summary>
        public string AdmittedCategory { get; private set; }

        /// <summary>
        /// The one absurd premise this scene has committed to, or null for none yet.
        ///
        /// CD §22's formula asks for one absurd premise, and this is the identity of that premise:
        /// a <see cref="DialogueWeirdness.PremisePrefix"/> tag when the content named one, and the
        /// fragment's own id when it did not. The fallback is what makes an unnamed premise safe -
        /// a fragment that never said which premise it belongs to speaks only for itself, so a
        /// second unnamed premise never passes as follow-on material for the first.
        /// </summary>
        public string AdmittedPremise { get; private set; }

        /// <summary>A budget whose ceiling is drawn from CD §22.2's distribution.</summary>
        public static WeirdnessBudget Roll(DeterministicRng rng)
        {
            return new WeirdnessBudget(SelectLevel(rng));
        }

        /// <summary>
        /// One ceiling level, drawn from <see cref="Weights"/>. Exposed on its own because "what
        /// would this scene have been allowed" is worth asking without spending a budget on it.
        /// </summary>
        public static WeirdnessLevel SelectLevel(DeterministicRng rng)
        {
            if (rng == null)
            {
                return WeirdnessLevel.Mundane;
            }

            int roll = rng.NextInt(100);
            int threshold = 0;
            for (int level = 0; level < Weights.Length; level++)
            {
                threshold += Weights[level];
                if (roll < threshold)
                {
                    return (WeirdnessLevel)level;
                }
            }

            return WeirdnessLevel.FeverDream;
        }

        /// <summary>
        /// Whether this fragment may still be said. Mundane content is always admissible; tagged
        /// content needs its level within <see cref="Ceiling"/>, and - once its level reaches
        /// <see cref="WeirdnessLevel.AbsurdPremiseCentral"/> - a premise that either matches the one
        /// already committed to or has not been committed yet.
        ///
        /// <b>Premise, not category.</b> Gating on the category admitted a second absurd premise
        /// whenever it happened to share a genre with the first - two unrelated bizarre tax
        /// premises are both bureaucratic - which approximated CD §22's "one absurd premise" rather
        /// than enforcing it. What distinguishes further material about the scene's premise from
        /// the start of a new one is which premise it is about, so that is what is compared;
        /// the category is still recorded, and still says what kind of scene this became.
        /// </summary>
        public bool IsAdmissible(DialogueFragment fragment)
        {
            if (fragment == null)
            {
                return false;
            }

            WeirdnessLevel level = DialogueWeirdness.LevelOf(fragment.Tags);
            if (level == WeirdnessLevel.Mundane)
            {
                return true;
            }

            if (level > Ceiling)
            {
                return false;
            }

            if (level >= WeirdnessLevel.AbsurdPremiseCentral && AdmittedPremise != null)
            {
                string premise = PremiseIdentity(fragment);
                if (premise.Length == 0 || !string.Equals(premise, AdmittedPremise, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Which absurd premise a fragment belongs to: the one its content named, or itself when it
        /// named none. Never a category - a genre two premises share is not an identity either of
        /// them has.
        /// </summary>
        private static string PremiseIdentity(DialogueFragment fragment)
        {
            return DialogueWeirdness.PremiseOf(fragment.Tags) ?? fragment.Id;
        }

        /// <summary>Records that this fragment was actually said.</summary>
        public void Note(DialogueFragment fragment)
        {
            if (fragment == null)
            {
                return;
            }

            WeirdnessLevel level = DialogueWeirdness.LevelOf(fragment.Tags);
            if (level > Spent)
            {
                Spent = level;
            }

            if (level >= WeirdnessLevel.AbsurdPremiseCentral && AdmittedPremise == null)
            {
                string premise = PremiseIdentity(fragment);
                if (premise.Length != 0)
                {
                    AdmittedPremise = premise;
                    AdmittedCategory = DialogueWeirdness.CategoryOf(fragment.Tags);
                }
            }
        }
    }
}
