using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Checks
{
    /// <summary>One attempt by one character against (optionally) another, plus its context.</summary>
    public sealed class CheckRequest
    {
        public CheckRequest(CheckProfile profile, EntityId actor, EntityId target)
        {
            Profile = profile;
            Actor = actor;
            Target = target;
            Modifiers = new List<SituationalModifier>();
        }

        public CheckProfile Profile { get; }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        /// <summary>
        /// Everything the situation contributes: affinity, fame, guild standing, whether the
        /// target already caught the player stealing last week. Positive makes it harder.
        /// </summary>
        public List<SituationalModifier> Modifiers { get; }

        public CheckRequest WithModifier(string label, int dcDelta)
        {
            if (dcDelta != 0)
            {
                Modifiers.Add(new SituationalModifier(label, dcDelta));
            }

            return this;
        }
    }

    public readonly struct SituationalModifier
    {
        public SituationalModifier(string label, int dcDelta)
        {
            Label = label;
            DcDelta = dcDelta;
        }

        public string Label { get; }

        /// <summary>Positive raises the difficulty; negative lowers it.</summary>
        public int DcDelta { get; }

        public override string ToString()
        {
            return (DcDelta >= 0 ? "+" : string.Empty) + DcDelta + " " + Label;
        }
    }
}
