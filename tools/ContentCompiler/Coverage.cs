using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Storylets;

namespace BrilliantQuesting.ContentCompiler
{
    /// <summary>
    /// What the authored library actually covers, and where the holes are (BQ-133, CP §6).
    ///
    /// The report answers one question and refuses a related one. It answers: for each cell an
    /// author can reasonably think in - this act, said in this slot, by a voice that has taken this
    /// position, at this level of commitment, in this mood, to somebody they stand in this relation
    /// to - how many authored fragments are eligible? It refuses to say what should be written
    /// next, and it deliberately never multiplies the axes together into a count of "possible
    /// lines": a product of independent choices is not evidence that anything was written, and
    /// quoting one is the most common way a content report lies about a library.
    ///
    /// The buckets are the whole of the reading. <b>0</b> is a hole: the situation can arise and
    /// nothing can be said in it. <b>1</b> is a repetition bug a player will find before we do -
    /// the line is not a choice, it is a catchphrase. <b>2</b> is thin. <b>3+</b> is covered, which
    /// is the three-realization rule the authoring pack states and this is the check for it.
    ///
    /// Eligibility is computed the way the realizer computes it, one axis at a time: a fragment
    /// with no opinion about a key answers every value of it, and a fragment that names values
    /// answers those. That is a slight over-count against a live scene, which narrows on several
    /// axes at once - which is the right direction for a report whose job is to find zeroes.
    /// </summary>
    internal static class Coverage
    {
        private const int Deep = 3;

        public static string Report(ContentBundle bundle)
        {
            IReadOnlyList<ContentDiagnostic> fragmentProblems;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(bundle, out fragmentProblems);

            IReadOnlyList<ContentDiagnostic> storyletProblems;
            IReadOnlyList<StoryletDefinition> storylets = StoryletContent.LoadDefinitions(bundle, out storyletProblems);

            StringBuilder report = new StringBuilder();
            Header(report, "Brilliant Questing content coverage");
            report.Append("fragments: ").Append(fragments.Count)
                .Append("   storylets: ").Append(storylets.Count)
                .Append("   acts: ").Append(SpeechActProfile.Vocabulary.Count)
                .AppendLine();
            report.AppendLine();

            ActsByPosition(report, fragments);
            Axis(report, fragments, DialogueReadings.Commitment, "commitment");
            Axis(report, fragments, DialogueReadings.Depth, "disclosure depth");
            Axis(report, fragments, DialogueReadings.Audience, "audience");
            Axis(report, fragments, DialogueReadings.Emotion, "audible emotion");
            Axis(report, fragments, DialogueReadings.Relationship, "relationship to the listener");
            Tone(report, fragments);
            Memorability(report, fragments);
            Storylets(report, storylets);
            Holes(report, fragments);
            return report.ToString();
        }

        /// <summary>
        /// The first table, and the one that matters most: an act with no core has no words at all,
        /// whatever else the library holds.
        /// </summary>
        private static void ActsByPosition(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "act x position");
            report.Append(Pad("act", 12));
            foreach (FragmentPosition position in Positions())
            {
                report.Append(Pad(position.ToString().ToLowerInvariant(), 10));
            }

            report.AppendLine();

            foreach (SpeechActType act in SpeechActProfile.Vocabulary)
            {
                report.Append(Pad(Slug(act.ToString()), 12));
                foreach (FragmentPosition position in Positions())
                {
                    report.Append(Pad(Cell(Count(fragments, position, act, null, null)), 10));
                }

                report.AppendLine();
            }

            report.AppendLine();
        }

        /// <summary>One act-by-value table for a closed reading, cores only.</summary>
        private static void Axis(StringBuilder report, IReadOnlyList<DialogueFragment> fragments, string key, string title)
        {
            IReadOnlyList<string> values = DialogueReadings.ValuesOf(key);
            if (values == null)
            {
                return;
            }

            Header(report, "act x " + title + " (cores)");
            report.Append(Pad("act", 12));
            foreach (string value in values)
            {
                report.Append(Pad(value, 14));
            }

            report.AppendLine();

            foreach (SpeechActType act in SpeechActProfile.Vocabulary)
            {
                report.Append(Pad(Slug(act.ToString()), 12));
                foreach (string value in values)
                {
                    report.Append(Pad(Cell(Count(fragments, FragmentPosition.Core, act, key, value)), 14));
                }

                report.AppendLine();
            }

            report.AppendLine();
        }

        /// <summary>
        /// What each act can be said in by a voice that has taken a position. Read through the
        /// realizer's own <see cref="DialogueFragment.FitsTone"/>, so a tag on an axis the voice
        /// said nothing about is left alone here exactly as it is in a live scene.
        /// </summary>
        private static void Tone(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "act x requested tone (cores)");
            report.Append(Pad("act", 12));
            foreach (string tag in DialogueTones.Vocabulary)
            {
                report.Append(Pad(tag, 9));
            }

            report.AppendLine();

            foreach (SpeechActType act in SpeechActProfile.Vocabulary)
            {
                report.Append(Pad(Slug(act.ToString()), 12));
                foreach (string tag in DialogueTones.Vocabulary)
                {
                    string[] requested = { tag };
                    int count = 0;
                    for (int i = 0; i < fragments.Count; i++)
                    {
                        if (fragments[i].Position == FragmentPosition.Core
                            && Answers(fragments[i], DialogueReadings.Act, Slug(act.ToString()))
                            && fragments[i].FitsTone(requested))
                        {
                            count++;
                        }
                    }

                    report.Append(Pad(Cell(count), 9));
                }

                report.AppendLine();
            }

            report.AppendLine();
        }

        private static void Memorability(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "distinctiveness");
            foreach (string tier in DialogueMemorability.Vocabulary)
            {
                int count = 0;
                for (int i = 0; i < fragments.Count; i++)
                {
                    if (string.Equals(fragments[i].Memorability, tier, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                report.Append(Pad(tier, 12)).Append(count).AppendLine();
            }

            report.AppendLine("(most of a library should be utility; a library that is mostly signature is a")
                .AppendLine(" library of catchphrases)")
                .AppendLine();
        }

        private static void Storylets(StringBuilder report, IReadOnlyList<StoryletDefinition> storylets)
        {
            Header(report, "storylets");
            report.Append(Pad("storylet", 34))
                .Append(Pad("beats", 7))
                .Append(Pad("routed", 8))
                .Append(Pad("ends", 6))
                .Append(Pad("acts", 6))
                .Append("checks")
                .AppendLine();

            for (int i = 0; i < storylets.Count; i++)
            {
                StoryletDefinition storylet = storylets[i];
                HashSet<string> acts = new HashSet<string>(StringComparer.Ordinal);
                int checks = 0;
                for (int j = 0; j < storylet.Beats.Count; j++)
                {
                    checks += storylet.Beats[j].Check == null ? 0 : 1;
                    for (int k = 0; k < storylet.Beats[j].Intentions.Count; k++)
                    {
                        acts.Add(Slug(storylet.Beats[j].Intentions[k].Act.ToString()));
                    }
                }

                report.Append(Pad(storylet.Id, 34))
                    .Append(Pad(storylet.Beats.Count.ToString(), 7))
                    .Append(Pad(storylet.IsRouted ? "yes" : "no", 8))
                    .Append(Pad(storylet.Resolutions.Count.ToString(), 6))
                    .Append(Pad(acts.Count.ToString(), 6))
                    .Append(checks)
                    .AppendLine();
            }

            report.AppendLine();
        }

        /// <summary>
        /// The list a content author actually acts on: every act-and-position cell nothing can
        /// fill, and every one exactly one thing can.
        /// </summary>
        private static void Holes(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            List<string> empty = new List<string>();
            List<string> single = new List<string>();

            foreach (SpeechActType act in SpeechActProfile.Vocabulary)
            {
                foreach (FragmentPosition position in Positions())
                {
                    int count = Count(fragments, position, act, null, null);
                    string where = Slug(act.ToString()) + " / " + position.ToString().ToLowerInvariant();
                    if (count == 0)
                    {
                        empty.Add(where);
                    }
                    else if (count == 1)
                    {
                        single.Add(where);
                    }
                }
            }

            Header(report, "holes");
            report.AppendLine("nothing at all (" + empty.Count + "):");
            Bullets(report, empty);
            report.AppendLine("exactly one, so it is a catchphrase (" + single.Count + "):");
            Bullets(report, single);
        }

        private static void Bullets(StringBuilder report, List<string> entries)
        {
            if (entries.Count == 0)
            {
                report.AppendLine("  none").AppendLine();
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                report.Append("  ").AppendLine(entries[i]);
            }

            report.AppendLine();
        }

        /// <summary>
        /// How many fragments could fill this slot for this act, optionally narrowed to one value
        /// of one reading.
        /// </summary>
        private static int Count(
            IReadOnlyList<DialogueFragment> fragments,
            FragmentPosition position,
            SpeechActType act,
            string key,
            string value)
        {
            int count = 0;
            for (int i = 0; i < fragments.Count; i++)
            {
                DialogueFragment fragment = fragments[i];
                if (fragment.Position != position
                    || !Answers(fragment, DialogueReadings.Act, Slug(act.ToString())))
                {
                    continue;
                }

                if (key != null && !Answers(fragment, key, value))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Whether a fragment is eligible for one value of one reading: it either has no opinion,
        /// or names this value, and nothing it forbids matches. The realizer's own rule, applied to
        /// a single key.
        /// </summary>
        private static bool Answers(DialogueFragment fragment, string key, string value)
        {
            for (int i = 0; i < fragment.Forbids.Count; i++)
            {
                if (string.Equals(fragment.Forbids[i].Key, key, StringComparison.Ordinal)
                    && fragment.Forbids[i].IsMetBy(value))
                {
                    return false;
                }
            }

            for (int i = 0; i < fragment.Requires.Count; i++)
            {
                if (string.Equals(fragment.Requires[i].Key, key, StringComparison.Ordinal))
                {
                    return fragment.Requires[i].IsMetBy(value);
                }
            }

            return true;
        }

        private static string Cell(int count)
        {
            if (count == 0)
            {
                return "0 !!";
            }

            if (count == 1)
            {
                return "1 !";
            }

            return count >= Deep ? count.ToString() : count + " ~";
        }

        private static IEnumerable<FragmentPosition> Positions()
        {
            yield return FragmentPosition.Opener;
            yield return FragmentPosition.Core;
            yield return FragmentPosition.Modifier;
            yield return FragmentPosition.Callback;
            yield return FragmentPosition.Context;
            yield return FragmentPosition.Closer;
        }

        private static void Header(StringBuilder report, string title)
        {
            report.AppendLine(title).AppendLine(new string('-', title.Length));
        }

        private static string Pad(string text, int width)
        {
            return text.Length >= width ? text + " " : text + new string(' ', width - text.Length);
        }

        private static string Slug(string name)
        {
            StringBuilder slug = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    slug.Append('_');
                }

                slug.Append(char.ToLowerInvariant(name[i]));
            }

            return slug.ToString();
        }
    }
}
