using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Every fragment that could be said, indexed by where in a line it goes.
    ///
    /// Held in id order within each slot rather than load order, so a line does not change because
    /// two content files were compiled in a different sequence. Determinism at this layer is worth
    /// more than it looks: it is what lets a situation be replayed from a seed and read the same
    /// way twice, which is how anything here is ever debugged.
    /// </summary>
    public sealed class DialogueFragmentLibrary
    {
        private static readonly DialogueFragment[] Nothing = new DialogueFragment[0];

        private readonly Dictionary<FragmentPosition, List<DialogueFragment>> _byPosition =
            new Dictionary<FragmentPosition, List<DialogueFragment>>();

        private readonly Dictionary<string, DialogueFragment> _byId = new Dictionary<string, DialogueFragment>(StringComparer.Ordinal);

        public int Count => _byId.Count;

        /// <summary>False when the id is already taken - two ways of saying something are two ids.</summary>
        public bool Register(DialogueFragment fragment)
        {
            if (fragment == null || string.IsNullOrEmpty(fragment.Id) || _byId.ContainsKey(fragment.Id))
            {
                return false;
            }

            _byId[fragment.Id] = fragment;
            if (!_byPosition.TryGetValue(fragment.Position, out List<DialogueFragment> slot))
            {
                slot = new List<DialogueFragment>();
                _byPosition[fragment.Position] = slot;
            }

            int at = slot.Count;
            while (at > 0 && string.CompareOrdinal(slot[at - 1].Id, fragment.Id) > 0)
            {
                at--;
            }

            slot.Insert(at, fragment);
            return true;
        }

        public IReadOnlyList<DialogueFragment> At(FragmentPosition position)
        {
            return _byPosition.TryGetValue(position, out List<DialogueFragment> slot) ? slot : (IReadOnlyList<DialogueFragment>)Nothing;
        }

        public bool TryGet(string id, out DialogueFragment fragment)
        {
            if (id == null)
            {
                fragment = null;
                return false;
            }

            return _byId.TryGetValue(id, out fragment);
        }
    }
}
