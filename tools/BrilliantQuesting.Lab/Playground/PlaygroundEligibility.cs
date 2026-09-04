using System;
using System.Collections.Generic;
using BrilliantQuesting.Dialogue;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// How many ways of saying this the library allowed, slot by slot.
    ///
    /// Read from <see cref="DialogueRealizer.Candidates"/>, which is production's own answer to
    /// "which wordings were available" and is a pure query: it builds the same eligible list
    /// <see cref="DialogueRealizer.Realize"/> builds and chooses nothing, notes nothing and spends
    /// nothing. Nothing here re-implements eligibility, and nothing here can widen a pool.
    ///
    /// <b>It is the eligible pool, not the pool that was drawn from.</b> Repetition narrows this
    /// further inside the realizer and the narrowing is deliberately not visible here, because the
    /// two facts are different: this says what the state and the constraints allowed, and
    /// <see cref="DialogueExpressionHistory"/> says what this conversation had already spent.
    /// </summary>
    public sealed class PlaygroundEligibility
    {
        /// <summary>The slots in the order a line is spoken, which is the order they print in.</summary>
        public static readonly FragmentPosition[] Slots =
        {
            FragmentPosition.Opener,
            FragmentPosition.Core,
            FragmentPosition.Modifier,
            FragmentPosition.Callback,
            FragmentPosition.Context,
            FragmentPosition.Closer
        };

        private readonly Dictionary<FragmentPosition, IReadOnlyList<string>> _bySlot;

        private PlaygroundEligibility(Dictionary<FragmentPosition, IReadOnlyList<string>> bySlot)
        {
            _bySlot = bySlot;
        }

        /// <summary>The fragments each slot could have taken, by id, in the library's own order.</summary>
        public static PlaygroundEligibility Of(DialogueRealizer realizer, RealizationRequest request)
        {
            if (realizer == null)
            {
                throw new ArgumentNullException(nameof(realizer));
            }

            Dictionary<FragmentPosition, IReadOnlyList<string>> bySlot =
                new Dictionary<FragmentPosition, IReadOnlyList<string>>();

            for (int i = 0; i < Slots.Length; i++)
            {
                IReadOnlyList<DialogueFragment> candidates = realizer.Candidates(Slots[i], request);
                string[] ids = new string[candidates.Count];
                for (int j = 0; j < candidates.Count; j++)
                {
                    ids[j] = candidates[j].Id;
                }

                bySlot[Slots[i]] = ids;
            }

            return new PlaygroundEligibility(bySlot);
        }

        public IReadOnlyList<string> At(FragmentPosition slot)
        {
            return _bySlot.TryGetValue(slot, out IReadOnlyList<string> ids) ? ids : new string[0];
        }

        public int CountAt(FragmentPosition slot) => At(slot).Count;

        /// <summary>Every slot's count, as "opener 3 / core 4 / ..." for a report row.</summary>
        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(Name(Slots[i])).Append(' ').Append(CountAt(Slots[i]));
            }

            return sb.ToString();
        }

        public static string Name(FragmentPosition slot) => slot.ToString().ToLowerInvariant();
    }
}
