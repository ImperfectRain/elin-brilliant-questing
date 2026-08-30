namespace BrilliantQuesting.Actions
{
    /// <summary>One legal backend action projected as a contextual player intent.</summary>
    public sealed class ActionIntentOption
    {
        public ActionIntentOption(ActionOffer offer, string label, string surface, string intentFamily)
        {
            Offer = offer;
            Label = label;
            Surface = surface;
            IntentFamily = intentFamily;
        }

        public ActionOffer Offer { get; }

        public NarrativeAction Action => Offer.Action;

        public Availability Availability => Offer.Availability;

        public string Label { get; }

        public string Surface { get; }

        public string IntentFamily { get; }
    }
}
