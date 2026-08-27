using System.Collections.Generic;

namespace BrilliantQuesting.Actions
{
    /// <summary>One discovered option, kept together with the verdict that produced it.</summary>
    public sealed class ActionOffer
    {
        public ActionOffer(NarrativeAction action, Availability availability)
        {
            Action = action;
            Availability = availability;
        }

        public NarrativeAction Action { get; }

        public Availability Availability { get; }

        public override string ToString() => Action.Id + " (" + Availability + ")";
    }

    /// <summary>
    /// Holds the verb library and answers "what can be attempted here?".
    ///
    /// Discovery returns rejected options too, with their reasons. The player never sees those,
    /// but the debug inspector does, and that is the only practical way to keep a procedural
    /// system maintainable.
    /// </summary>
    public sealed class ActionRegistry
    {
        private readonly List<NarrativeAction> _actions = new List<NarrativeAction>();

        public IReadOnlyList<NarrativeAction> Actions => _actions;

        public ActionRegistry Register(NarrativeAction action)
        {
            _actions.Add(action);
            return this;
        }

        public NarrativeAction Get(string id)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Id == id)
                {
                    return _actions[i];
                }
            }

            return null;
        }

        public List<ActionOffer> Discover(ActionContext context, bool includeUnavailable = false)
        {
            List<ActionOffer> offers = new List<ActionOffer>();
            for (int i = 0; i < _actions.Count; i++)
            {
                Availability availability = _actions[i].GetAvailability(context);
                if (availability.IsAvailable || includeUnavailable)
                {
                    offers.Add(new ActionOffer(_actions[i], availability));
                }
            }

            return offers;
        }

        /// <summary>
        /// Which solution families are currently open. The generator's route-diversity target is
        /// measured with this: a situation offering only Social is a design failure.
        /// </summary>
        public HashSet<ActionFamily> AvailableFamilies(ActionContext context)
        {
            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (ActionOffer offer in Discover(context))
            {
                families.Add(offer.Action.Family);
            }

            return families;
        }
    }
}
