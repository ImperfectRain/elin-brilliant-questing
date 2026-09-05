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

            // BQ-147's two, and the reason to count them is the reason to count the rest: a route
            // no fragment answers is a speaker the library falls silent for, and an eyewitness who
            // cannot be worded is exactly the hole this axis was added to close.
            Axis(report, fragments, DialogueReadings.ClaimSource, "how the speaker knows");
            Axis(report, fragments, DialogueReadings.ClaimProof, "whether it can be shown");
            Tone(report, fragments);
            Idiolect(report, fragments);
            VoiceDemands(report, fragments);
            Memorability(report, fragments);
            AlwaysOnSlots(report, fragments);
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

        /// <summary>
        /// How far BQ-142's habits actually reach into the corpus, counted where they were marked
        /// rather than where they would be eligible.
        ///
        /// The other tables count eligibility, because a hole there is a situation nobody can speak
        /// in. This one counts marks, because the failure it exists to catch is the opposite: an
        /// axis a voice can request and no authored line has taken a side on is a dimension the
        /// corpus does not support, and a speaker specified on it sounds exactly like a speaker who
        /// was not. A zero column is that dimension; a pole marked far more often than its opposite
        /// is a voice that can only ever be narrowed in one direction.
        ///
        /// Unmarked is reported beside them and is expected to be most of the library. It is the
        /// wording every voice can still reach, so a small marked count is a young vocabulary
        /// rather than a broken one.
        /// </summary>
        private static void Idiolect(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "idiolect mark x position");
            report.Append(Pad("mark", 12));
            foreach (FragmentPosition position in Positions())
            {
                report.Append(Pad(position.ToString().ToLowerInvariant(), 10));
            }

            report.AppendLine();

            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                report.Append(Pad(tag, 12));
                foreach (FragmentPosition position in Positions())
                {
                    int count = 0;
                    for (int i = 0; i < fragments.Count; i++)
                    {
                        if (fragments[i].Position == position && Marks(fragments[i], tag))
                        {
                            count++;
                        }
                    }

                    report.Append(Pad(count.ToString(), 10));
                }

                report.AppendLine();
            }

            int unmarked = 0;
            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].IdiolectTags.Count == 0)
                {
                    unmarked++;
                }
            }

            report.AppendLine();
            report.Append("unmarked: ").Append(unmarked).Append(" of ").Append(fragments.Count)
                .AppendLine(" - wording every voice can still reach").AppendLine();
        }

        private static bool Marks(DialogueFragment fragment, string tag)
        {
            for (int i = 0; i < fragment.IdiolectTags.Count; i++)
            {
                if (string.Equals(fragment.IdiolectTags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How many lines are reserved for a particular kind of speaker (BQ-149), and which trait
        /// each is reserved for.
        ///
        /// The number to watch is the total rather than any one row. A demand is the only narrowing
        /// in this file that a middling voice fails rather than passes, so every demanding fragment
        /// is wording most speakers never reach - which is the point for a line whose temperament is
        /// the line, and caricature the moment it is the ordinary way a tie or a mood is worded. A
        /// demanding fraction that climbs is a corpus deciding personalities for people the
        /// simulation described only as rivals.
        /// </summary>
        private static void VoiceDemands(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "voice demanded");
            int demanding = 0;
            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].VoiceDemands.Count != 0)
                {
                    demanding++;
                }
            }

            foreach (string tag in Demandable())
            {
                int count = 0;
                for (int i = 0; i < fragments.Count; i++)
                {
                    if (Demands(fragments[i], tag))
                    {
                        count++;
                    }
                }

                if (count != 0)
                {
                    report.Append(Pad(tag, 12)).Append(count).AppendLine();
                }
            }

            report.AppendLine();
            report.Append("demanding: ").Append(demanding).Append(" of ").Append(fragments.Count)
                .AppendLine(" - wording only a speaker described that way reaches").AppendLine();
        }

        private static IEnumerable<string> Demandable()
        {
            foreach (string tag in DialogueTones.Vocabulary)
            {
                yield return tag;
            }

            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                yield return tag;
            }
        }

        private static bool Demands(DialogueFragment fragment, string tag)
        {
            for (int i = 0; i < fragment.VoiceDemands.Count; i++)
            {
                if (string.Equals(fragment.VoiceDemands[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// The optional slots that fire in every line, counted against the reading that actually
        /// gates them, with how much of each is plain (BQ-151).
        ///
        /// Every other table in this file is act-by-something, because a core is chosen on its act.
        /// A relationship modifier, a mood modifier, a context line and a callback are not: they
        /// declare a tie, a feeling, a room or a kind of recalled history and usually say nothing
        /// about the act at all. So they appeared in <see cref="ActsByPosition"/> as one flat
        /// column repeated down every row - 34 callbacks for every act, because 34 callbacks
        /// answer every act - and the report could not see their distribution at all. It was
        /// blind to exactly the families it most needed to watch, since a core file is read once
        /// per act and these are read once per <em>line</em>.
        ///
        /// <b>Written counts declarations, not eligibility.</b> A modifier with no opinion about
        /// the tie is available to a friend, but it is not wording <em>for</em> having a friend
        /// opposite: it does not express the tie, and the question here is whether the tie has an
        /// ordinary way of being expressed as well as a striking one. Counting the unconditioned
        /// fragments would answer a different question and would answer it reassuringly.
        ///
        /// It scores nothing and fails nothing. `plain` well under `written` in a row is the
        /// reading to act on: a value the library can only say memorably is a situation the world
        /// always says strikingly, and the optional slots are where that costs the most (CD §19;
        /// `dialogue-writing-inspiration-research.md` §11, §19).
        /// </summary>
        private static void AlwaysOnSlots(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            Header(report, "the always-on slots, and how much of each is plain");
            report.Append(Pad("slot", 10)).Append(Pad("reading", 16)).Append(Pad("value", 16))
                .Append(Pad("written", 9)).Append("plain").AppendLine();

            foreach (KeyValuePair<FragmentPosition, string> family in AlwaysOn())
            {
                IReadOnlyList<string> values = DialogueReadings.ValuesOf(family.Value);
                if (values == null)
                {
                    continue;
                }

                foreach (string value in values)
                {
                    int written = Declaring(fragments, family.Key, family.Value, value, false);
                    if (written == 0)
                    {
                        continue;
                    }

                    int plain = Declaring(fragments, family.Key, family.Value, value, true);
                    report.Append(Pad(family.Key.ToString().ToLowerInvariant(), 10))
                        .Append(Pad(family.Value, 16))
                        .Append(Pad(value, 16))
                        .Append(Pad(Cell(written), 9))
                        .Append(Cell(plain))
                        .AppendLine();
                }
            }

            report.AppendLine();
        }

        /// <summary>
        /// The slot-and-reading pairs whose wording is chosen on something other than the act. Not
        /// a taxonomy: it is the list of places where <see cref="ActsByPosition"/> cannot see.
        /// </summary>
        private static IEnumerable<KeyValuePair<FragmentPosition, string>> AlwaysOn()
        {
            // The act belongs here as well as in `act x position`, and the two readings disagree
            // on purpose. That table counts eligibility, which for an optional slot is dominated by
            // the fragments with no act opinion at all - so `warn / modifier` read 37 while exactly
            // one modifier had been written for warning, and it was a signature. Declaration is the
            // reading that finds that.
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Modifier, DialogueReadings.Act);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Modifier, DialogueReadings.Relationship);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Modifier, DialogueReadings.Emotion);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Context, DialogueReadings.Audience);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Callback, DialogueReadings.Callback);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Callback, DialogueReadings.CallbackRoute);
        }

        /// <summary>
        /// How many fragments in this slot are written for this value of this reading - they name
        /// it in a <c>requires</c> - optionally counting only the ones that ask for no repetition
        /// protection beyond the ordinary.
        /// </summary>
        private static int Declaring(
            IReadOnlyList<DialogueFragment> fragments,
            FragmentPosition position,
            string key,
            string value,
            bool plainOnly)
        {
            int count = 0;
            for (int i = 0; i < fragments.Count; i++)
            {
                DialogueFragment fragment = fragments[i];
                if (fragment.Position != position || !Declares(fragment, key, value))
                {
                    continue;
                }

                if (plainOnly && !string.Equals(
                    fragment.Memorability, DialogueMemorability.Utility, StringComparison.Ordinal))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Whether a fragment names this value of this key in its own conditions. Stricter than
        /// <see cref="Answers"/> on purpose - silence is not a declaration.
        /// </summary>
        private static bool Declares(DialogueFragment fragment, string key, string value)
        {
            for (int i = 0; i < fragment.Requires.Count; i++)
            {
                if (string.Equals(fragment.Requires[i].Key, key, StringComparison.Ordinal))
                {
                    return fragment.Requires[i].IsMetBy(value);
                }
            }

            return false;
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
        /// fill, every one exactly one thing can, and every always-on situation the library can
        /// only say memorably (BQ-151).
        ///
        /// The third list is a different kind of hole from the first two and is worth stating as
        /// such. Nothing is missing there - the situation has wording, and the wording is good. It
        /// is that the only wording is a line written to be noticed, in a slot that fires in every
        /// line, so a tie or a mood or a kind of recalled history is heard strikingly every single
        /// time it is heard at all. That is how a signature line becomes a catchphrase without
        /// anybody authoring a catchphrase.
        /// </summary>
        private static void Holes(StringBuilder report, IReadOnlyList<DialogueFragment> fragments)
        {
            List<string> empty = new List<string>();
            List<string> single = new List<string>();
            List<string> neverPlain = new List<string>();

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

            foreach (KeyValuePair<FragmentPosition, string> family in AlwaysOn())
            {
                IReadOnlyList<string> values = DialogueReadings.ValuesOf(family.Value);
                if (values == null)
                {
                    continue;
                }

                foreach (string value in values)
                {
                    if (Declaring(fragments, family.Key, family.Value, value, false) != 0
                        && Declaring(fragments, family.Key, family.Value, value, true) == 0)
                    {
                        neverPlain.Add(
                            family.Key.ToString().ToLowerInvariant() + " / " + family.Value + " = " + value);
                    }
                }
            }

            Header(report, "holes");
            report.AppendLine("nothing at all (" + empty.Count + "):");
            Bullets(report, empty);
            report.AppendLine("exactly one, so it is a catchphrase (" + single.Count + "):");
            Bullets(report, single);
            report.AppendLine(
                "worded, but never plainly, in a slot that fires every line (" + neverPlain.Count + "):");
            Bullets(report, neverPlain);
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
