namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// A reusable RPG verb.
    ///
    /// The content strategy of the whole mod rests on this class: the project invests in a small
    /// number of well-understood verbs that know how real Elin mechanics work, and lets generated
    /// situations decide when they become relevant. It does not invest in quest scripts.
    /// </summary>
    public abstract class NarrativeAction
    {
        protected NarrativeAction(string id, ActionFamily family, string label)
        {
            Id = id;
            Family = family;
            Label = label;
        }

        public string Id { get; }

        public ActionFamily Family { get; }

        /// <summary>Short player-facing verb ("Lie", "Pick their pocket").</summary>
        public string Label { get; }

        /// <summary>
        /// Whether this makes sense here at all. Must be side-effect free: the discovery pass
        /// calls it for every registered action, including ones it will never show.
        /// </summary>
        public abstract Availability GetAvailability(ActionContext context);

        /// <summary>Resolves the attempt and writes its consequences into world history.</summary>
        public abstract ActionOutcome Perform(ActionContext context);
    }
}
