using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// What one conversation has already said, kept only so the next line does not sound like a
    /// machine repeating itself (BQ-078, CD §21).
    ///
    /// Counts, not memory. <see cref="Note"/> tallies one spoken fragment by its id, its
    /// <see cref="DialogueFragment.RepetitionGroup"/>, the metaphor-family tags BQ-076's vocabulary
    /// already reads and the tone tags that stand in for cadence; <see cref="NoteAct"/> tallies the
    /// semantic act a rendered line carried, because CD §21 names that axis too, even though no
    /// fragment can be chosen on it - every candidate left for a slot already answers the one act
    /// the request carries. <see cref="IsFresh"/> answers whether a candidate has already said its
    /// piece, its group, its family or its cadence as often as a conversation should let it before
    /// the realizer starts steering around it.
    ///
    /// This is not conversation state. It holds no belief, no fact and no memory a character keeps
    /// - that is BQ-083's, and does not exist yet. A caller builds one per conversation and lets it
    /// go when the conversation ends; nothing here is saved, because nothing here should outlive the
    /// exchange that produced it.
    /// </summary>
    public sealed class DialogueExpressionHistory
    {
        /// <summary>
        /// How many times one way of saying something may recur before selection starts avoiding
        /// it. CD §21's own example is the bound: not heard ten times in one town, and a cap this
        /// small is what makes "no opener more than twice" hold by construction rather than by luck.
        /// </summary>
        public const int DefaultCap = 2;

        private readonly int _cap;
        private readonly Dictionary<string, int> _fragments = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _groups = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _families = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _cadences = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _acts = new Dictionary<string, int>(StringComparer.Ordinal);

        public DialogueExpressionHistory(int cap = DefaultCap)
        {
            _cap = cap > 0 ? cap : DefaultCap;
        }

        /// <summary>
        /// Whether this fragment has not yet said its piece, its group, its metaphor family or its
        /// cadence as often as the cap allows. All-of, not any-of: a fragment sharing an overused
        /// group is exactly as stale as one repeating its own id, because both are the repetition
        /// CD §21 names.
        /// </summary>
        public bool IsFresh(DialogueFragment fragment)
        {
            if (fragment == null)
            {
                return false;
            }

            if (Uses(_fragments, fragment.Id) >= _cap)
            {
                return false;
            }

            if (fragment.RepetitionGroup.Length != 0 && Uses(_groups, fragment.RepetitionGroup) >= _cap)
            {
                return false;
            }

            for (int i = 0; i < fragment.Tags.Count; i++)
            {
                if (DialogueVocabulary.IsVocabulary(fragment.Tags[i]) && Uses(_families, fragment.Tags[i]) >= _cap)
                {
                    return false;
                }
            }

            for (int i = 0; i < fragment.ToneTags.Count; i++)
            {
                if (Uses(_cadences, fragment.ToneTags[i]) >= _cap)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Records that this fragment was just spoken.</summary>
        public void Note(DialogueFragment fragment)
        {
            if (fragment == null)
            {
                return;
            }

            Increment(_fragments, fragment.Id);
            if (fragment.RepetitionGroup.Length != 0)
            {
                Increment(_groups, fragment.RepetitionGroup);
            }

            for (int i = 0; i < fragment.Tags.Count; i++)
            {
                if (DialogueVocabulary.IsVocabulary(fragment.Tags[i]))
                {
                    Increment(_families, fragment.Tags[i]);
                }
            }

            for (int i = 0; i < fragment.ToneTags.Count; i++)
            {
                Increment(_cadences, fragment.ToneTags[i]);
            }
        }

        /// <summary>Records which semantic act a rendered line carried.</summary>
        public void NoteAct(string act)
        {
            if (!string.IsNullOrEmpty(act))
            {
                Increment(_acts, act);
            }
        }

        public int UsesOf(string fragmentId) => Uses(_fragments, fragmentId);

        public int UsesOfGroup(string group) => Uses(_groups, group);

        public int UsesOfAct(string act) => Uses(_acts, act);

        private static int Uses(Dictionary<string, int> counts, string key)
        {
            return !string.IsNullOrEmpty(key) && counts.TryGetValue(key, out int count) ? count : 0;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }
    }
}
