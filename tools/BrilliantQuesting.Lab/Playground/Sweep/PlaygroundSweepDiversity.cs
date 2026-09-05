using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Dialogue;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One fragment as it bears on diversity: enough to say whether it is shared across profiles,
    /// how loudly it asks not to repeat, and whether it sits in one of the three slots a line's
    /// shape is built from.
    /// </summary>
    public sealed class DialogueDiversityFragment
    {
        public DialogueDiversityFragment(string id, FragmentPosition position, string memorability, string repetitionGroup)
        {
            Id = id ?? string.Empty;
            Position = position;
            Memorability = string.IsNullOrEmpty(memorability) ? DialogueMemorability.Utility : memorability;
            RepetitionGroup = repetitionGroup ?? string.Empty;
        }

        public string Id { get; }

        public FragmentPosition Position { get; }

        public string Memorability { get; }

        public string RepetitionGroup { get; }

        /// <summary>Opener, core and closer are a line's own shape; the other three are trimmings.</summary>
        public bool IsStructural =>
            Position == FragmentPosition.Opener || Position == FragmentPosition.Core || Position == FragmentPosition.Closer;
    }

    /// <summary>
    /// One realized line, read as a point of comparison against every other line the same family
    /// produced - which profile said it, whether it was said at all, and which fragments it spent.
    ///
    /// <b>Taken off a finished row, never off a running one.</b> Exactly the discipline
    /// <see cref="PlaygroundSweepRow"/> already holds itself to: a sample is built once the line is
    /// realized and rendered, so measuring it can never become part of producing it.
    /// </summary>
    public sealed class DialogueDiversitySample
    {
        private static readonly DialogueDiversityFragment[] NoFragments = new DialogueDiversityFragment[0];

        public DialogueDiversitySample(
            string profile, bool realized, string text, string core, IReadOnlyList<DialogueDiversityFragment> fragments)
        {
            Profile = profile ?? string.Empty;
            Realized = realized;
            Text = text ?? string.Empty;
            Core = core ?? string.Empty;
            Fragments = fragments ?? NoFragments;
        }

        /// <summary>The sweep row's own label - the speaker, tie or lived context this line stands for.</summary>
        public string Profile { get; }

        /// <summary>Whether the act reached words at all. False for a fallback: an act composed with nothing to say it.</summary>
        public bool Realized { get; }

        public string Text { get; }

        /// <summary>The core fragment's id. Empty when nothing was realized.</summary>
        public string Core { get; }

        public IReadOnlyList<DialogueDiversityFragment> Fragments { get; }
    }

    /// <summary>
    /// Deterministic, interpretable measures of whether a family of realized lines actually sound
    /// different from one another, or merely differ as strings while saying the point the same way
    /// every time.
    ///
    /// <b>None of this is a quality score.</b> Nothing here reads prose, nothing here scores a line
    /// and nothing here fails a build - the same restraint <see cref="DialogueMemorability"/> and
    /// the coverage report already hold themselves to, applied to a running conversation instead of
    /// to the library on disk. What is reported is overlap and repetition: the two shapes
    /// homogenization actually takes when contrasting speakers keep landing on the same authored
    /// material, so a developer can tell a sweep that found genuine variety from one that didn't,
    /// without either being scored against a threshold a normal content change could trip.
    /// </summary>
    public sealed class DialogueDiversityReport
    {
        public DialogueDiversityReport(
            int samples,
            int realized,
            int distinctCores,
            int distinctFragmentsUsed,
            int fragmentsSharedAcrossProfiles,
            int memorableFragmentUses,
            int totalFragmentUses,
            IReadOnlyList<string> reusedMemorableFragments,
            IReadOnlyList<string> reusedStructuralGroups,
            double averageTextualOverlap,
            double maxTextualOverlap,
            int lineLengthMin,
            int lineLengthMax,
            double lineLengthMean)
        {
            Samples = samples;
            Realized = realized;
            DistinctCores = distinctCores;
            DistinctFragmentsUsed = distinctFragmentsUsed;
            FragmentsSharedAcrossProfiles = fragmentsSharedAcrossProfiles;
            MemorableFragmentUses = memorableFragmentUses;
            TotalFragmentUses = totalFragmentUses;
            ReusedMemorableFragments = reusedMemorableFragments ?? new string[0];
            ReusedStructuralGroups = reusedStructuralGroups ?? new string[0];
            AverageTextualOverlap = averageTextualOverlap;
            MaxTextualOverlap = maxTextualOverlap;
            LineLengthMin = lineLengthMin;
            LineLengthMax = lineLengthMax;
            LineLengthMean = lineLengthMean;
        }

        /// <summary>Every row the family evaluated that had an act to word, realized or not.</summary>
        public int Samples { get; }

        /// <summary>How many of those actually rendered words.</summary>
        public int Realized { get; }

        /// <summary>How many different core fragments the realized lines used.</summary>
        public int DistinctCores { get; }

        /// <summary>Every fragment id seen across the realized lines, at any slot, once each.</summary>
        public int DistinctFragmentsUsed { get; }

        /// <summary>Of those, how many were spoken by more than one profile.</summary>
        public int FragmentsSharedAcrossProfiles { get; }

        /// <summary>How many fragment slots, across every realized line, were filled by anything above <see cref="DialogueMemorability.Utility"/>.</summary>
        public int MemorableFragmentUses { get; }

        /// <summary>Every fragment slot filled across every realized line - the base <see cref="MemorableFragmentUses"/> is a share of.</summary>
        public int TotalFragmentUses { get; }

        /// <summary>
        /// Fragment ids above <see cref="DialogueMemorability.Utility"/> that more than one profile
        /// spoke - the case CD §21 and BQ-146 both warn against, a joke or a piece of restrained
        /// sincerity landing twice.
        /// </summary>
        public IReadOnlyList<string> ReusedMemorableFragments { get; }

        /// <summary>
        /// Repetition groups, restricted to the opener, core and closer slots, that more than one
        /// profile drew from - a line's shape recurring even where its content differs.
        /// </summary>
        public IReadOnlyList<string> ReusedStructuralGroups { get; }

        /// <summary>
        /// The mean of every pairwise word-overlap ratio between two distinct realized lines - a
        /// simple, deterministic stand-in for "these two sentences read alike" that needs no
        /// embedding and no model.
        /// </summary>
        public double AverageTextualOverlap { get; }

        /// <summary>The single most alike pair the family produced, by the same measure.</summary>
        public double MaxTextualOverlap { get; }

        public int LineLengthMin { get; }

        public int LineLengthMax { get; }

        public double LineLengthMean { get; }

        public double UnrealizedRate => Samples == 0 ? 0.0 : (Samples - Realized) / (double)Samples;

        public double DistinctCoreRate => Realized == 0 ? 0.0 : DistinctCores / (double)Realized;

        public double FragmentOverlapRate =>
            DistinctFragmentsUsed == 0 ? 0.0 : FragmentsSharedAcrossProfiles / (double)DistinctFragmentsUsed;

        public double MemorableFragmentShare =>
            TotalFragmentUses == 0 ? 0.0 : MemorableFragmentUses / (double)TotalFragmentUses;

        // -- the formatted lines the report prints, so the arithmetic and the presentation of it never disagree --

        public string UnrealizedSummary =>
            Percent(UnrealizedRate) + " (" + (Samples - Realized) + " of " + Samples + ")";

        public string CoreSummary => Realized == 0
            ? "nothing realized"
            : DistinctCores + " distinct of " + Realized + " realized (" + Percent(DistinctCoreRate) + ")";

        public string OverlapSummary => DistinctFragmentsUsed == 0
            ? "no fragments spoken"
            : FragmentsSharedAcrossProfiles + " of " + DistinctFragmentsUsed
              + " fragments said by more than one profile (" + Percent(FragmentOverlapRate) + ")";

        public string MemorableSummary => TotalFragmentUses == 0
            ? "no fragments spoken"
            : MemorableFragmentUses + " of " + TotalFragmentUses + " fragment slots above utility ("
              + Percent(MemorableFragmentShare) + ")"
              + (ReusedMemorableFragments.Count == 0
                  ? ", none reused across profiles"
                  : ", reused across profiles: " + string.Join(", ", ReusedMemorableFragments));

        public string StructuralGroupSummary => ReusedStructuralGroups.Count == 0
            ? "no opener, core or closer group repeated across profiles"
            : ReusedStructuralGroups.Count + " group(s) shared across profiles: " + string.Join(", ", ReusedStructuralGroups);

        public string TextualOverlapSummary => Realized < 2
            ? "fewer than two realized lines to compare"
            : "average " + Percent(AverageTextualOverlap) + ", widest " + Percent(MaxTextualOverlap)
              + " shared words between any two lines";

        public string LineLengthSummary => Realized == 0
            ? "nothing realized"
            : LineLengthMin + " to " + LineLengthMax + " words, mean "
              + LineLengthMean.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Percent(double rate) =>
            (rate * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Turns a family's own rows into diversity samples, and the samples into
    /// <see cref="DialogueDiversityReport"/>. The seam between the two is deliberate: the metrics
    /// take no <see cref="PlaygroundSweepRow"/>, no <see cref="PlaygroundRun"/> and no library
    /// reference, so they can be tested against fixed fixtures nobody had to run a conversation to
    /// build - <c>BrilliantQuesting.Lab.Tests</c> does exactly that.
    /// </summary>
    public static class DialogueDiversityMetrics
    {
        private static readonly DialogueDiversitySample[] NoSamples = new DialogueDiversitySample[0];

        /// <summary>
        /// One sample per row that had an act to word - a row nothing was ever going to say (no
        /// belief, no reply) is not a fallback and is not counted as one, and reading it as either
        /// would make <see cref="DialogueDiversityReport.UnrealizedRate"/> disagree with
        /// <see cref="PlaygroundSweepRow.Unrealized"/>, which is the definition it must match.
        /// </summary>
        public static IReadOnlyList<DialogueDiversitySample> SamplesFrom(IReadOnlyList<PlaygroundSweepRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return NoSamples;
            }

            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>();
            for (int i = 0; i < rows.Count; i++)
            {
                PlaygroundSweepRow row = rows[i];
                if (!row.Evaluated || row.Turn?.Reply == null)
                {
                    continue;
                }

                RealizedLine line = row.Turn.Line;
                bool realized = line != null && line.Rendered;
                List<DialogueDiversityFragment> fragments = new List<DialogueDiversityFragment>();
                if (realized)
                {
                    DialogueFragmentLibrary library = row.Run.Stage.Realizer.Library;
                    for (int f = 0; f < line.Fragments.Count; f++)
                    {
                        if (library.TryGet(line.Fragments[f], out DialogueFragment fragment))
                        {
                            fragments.Add(new DialogueDiversityFragment(
                                fragment.Id, fragment.Position, fragment.Memorability, fragment.RepetitionGroup));
                        }
                    }
                }

                samples.Add(new DialogueDiversitySample(
                    row.Label, realized, realized ? line.Text : string.Empty, realized ? line.Core : string.Empty, fragments));
            }

            return samples;
        }

        public static DialogueDiversityReport Compute(IReadOnlyList<DialogueDiversitySample> samples)
        {
            samples = samples ?? NoSamples;

            List<DialogueDiversitySample> realized = new List<DialogueDiversitySample>();
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Realized)
                {
                    realized.Add(samples[i]);
                }
            }

            HashSet<string> cores = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> fragmentProfiles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Dictionary<string, string> fragmentMemorability = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> groupProfiles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            int totalFragmentUses = 0;
            int memorableFragmentUses = 0;
            List<List<string>> tokenLists = new List<List<string>>();

            for (int i = 0; i < realized.Count; i++)
            {
                DialogueDiversitySample sample = realized[i];
                if (sample.Core.Length != 0)
                {
                    cores.Add(sample.Core);
                }

                HashSet<string> fragmentsThisSample = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> groupsThisSample = new HashSet<string>(StringComparer.Ordinal);
                for (int f = 0; f < sample.Fragments.Count; f++)
                {
                    DialogueDiversityFragment fragment = sample.Fragments[f];
                    totalFragmentUses++;
                    if (fragment.Memorability != DialogueMemorability.Utility)
                    {
                        memorableFragmentUses++;
                    }

                    if (fragmentsThisSample.Add(fragment.Id))
                    {
                        Profiles(fragmentProfiles, fragment.Id).Add(sample.Profile);
                        fragmentMemorability[fragment.Id] = fragment.Memorability;
                    }

                    if (fragment.IsStructural && fragment.RepetitionGroup.Length != 0
                        && groupsThisSample.Add(fragment.RepetitionGroup))
                    {
                        Profiles(groupProfiles, fragment.RepetitionGroup).Add(sample.Profile);
                    }
                }

                tokenLists.Add(Tokenize(sample.Text));
            }

            int sharedFragments = 0;
            List<string> reusedMemorable = new List<string>();
            foreach (KeyValuePair<string, HashSet<string>> use in fragmentProfiles)
            {
                if (use.Value.Count <= 1)
                {
                    continue;
                }

                sharedFragments++;
                if (fragmentMemorability.TryGetValue(use.Key, out string memorability)
                    && memorability != DialogueMemorability.Utility)
                {
                    reusedMemorable.Add(use.Key);
                }
            }

            reusedMemorable.Sort(StringComparer.Ordinal);

            List<string> reusedGroups = new List<string>();
            foreach (KeyValuePair<string, HashSet<string>> use in groupProfiles)
            {
                if (use.Value.Count > 1)
                {
                    reusedGroups.Add(use.Key);
                }
            }

            reusedGroups.Sort(StringComparer.Ordinal);

            double overlapSum = 0.0;
            double overlapMax = 0.0;
            int pairs = 0;
            for (int i = 0; i < tokenLists.Count; i++)
            {
                HashSet<string> a = new HashSet<string>(tokenLists[i], StringComparer.Ordinal);
                for (int j = i + 1; j < tokenLists.Count; j++)
                {
                    HashSet<string> b = new HashSet<string>(tokenLists[j], StringComparer.Ordinal);
                    double overlap = Jaccard(a, b);
                    overlapSum += overlap;
                    overlapMax = Math.Max(overlapMax, overlap);
                    pairs++;
                }
            }

            int lengthMin = int.MaxValue;
            int lengthMax = 0;
            int lengthSum = 0;
            for (int i = 0; i < tokenLists.Count; i++)
            {
                int length = tokenLists[i].Count;
                lengthMin = Math.Min(lengthMin, length);
                lengthMax = Math.Max(lengthMax, length);
                lengthSum += length;
            }

            if (tokenLists.Count == 0)
            {
                lengthMin = 0;
            }

            return new DialogueDiversityReport(
                samples.Count,
                realized.Count,
                cores.Count,
                fragmentProfiles.Count,
                sharedFragments,
                memorableFragmentUses,
                totalFragmentUses,
                reusedMemorable,
                reusedGroups,
                pairs == 0 ? 0.0 : overlapSum / pairs,
                overlapMax,
                lengthMin,
                lengthMax,
                realized.Count == 0 ? 0.0 : lengthSum / (double)realized.Count);
        }

        private static HashSet<string> Profiles(Dictionary<string, HashSet<string>> map, string key)
        {
            if (!map.TryGetValue(key, out HashSet<string> profiles))
            {
                profiles = new HashSet<string>(StringComparer.Ordinal);
                map[key] = profiles;
            }

            return profiles;
        }

        /// <summary>
        /// A simple, deterministic word-overlap ratio: how much of the smaller line's vocabulary the
        /// larger one also uses. No stemming, no synonym table and no embedding - two lines differing
        /// only in capitalisation or punctuation still read as fully overlapping, and that is exactly
        /// the honest, cheap comparison this exists to make rather than a claim about meaning.
        /// </summary>
        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0)
            {
                return a.Count == 0 && b.Count == 0 ? 1.0 : 0.0;
            }

            int intersection = 0;
            foreach (string token in a)
            {
                if (b.Contains(token))
                {
                    intersection++;
                }
            }

            int union = a.Count + b.Count - intersection;
            return union == 0 ? 0.0 : intersection / (double)union;
        }

        /// <summary>Lower-cased word tokens, split on anything that is not a letter or digit.</summary>
        private static List<string> Tokenize(string text)
        {
            List<string> tokens = new List<string>();
            string source = text ?? string.Empty;
            StringBuilder current = new StringBuilder();
            for (int i = 0; i <= source.Length; i++)
            {
                char c = i < source.Length ? source[i] : ' ';
                if (char.IsLetterOrDigit(c))
                {
                    current.Append(char.ToLowerInvariant(c));
                }
                else if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }

            return tokens;
        }
    }
}
