using System;
using System.Collections.Generic;
using BrilliantQuesting.Integration;

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
        /// Which registered verbs say they could advance that kind of state change (BQa-010).
        ///
        /// Metadata only, and deliberately contextless: this is the question a consumer asks
        /// before it has a place, a target or a binding - which verbs are even worth building a
        /// candidate for. <see cref="Discover"/> still answers whether any of them applies here.
        /// Reading it changes nothing, including the verbs it reads.
        /// </summary>
        public List<NarrativeAction> Advancing(string effectKind)
        {
            List<NarrativeAction> matched = new List<NarrativeAction>();
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Effects.Advances(effectKind))
                {
                    matched.Add(_actions[i]);
                }
            }

            return matched;
        }

        /// <summary>
        /// The same question, narrowed to the effects this build could actually carry: a verb
        /// whose possession half is a vanilla item move is not a route on a build that cannot
        /// move items, and saying so here is cheaper and more honest than finding out at the
        /// seam. A null build answers nothing, so every delegated effect is refused.
        /// </summary>
        public List<NarrativeAction> Advancing(string effectKind, IVanillaState vanilla)
        {
            List<NarrativeAction> matched = new List<NarrativeAction>();
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].CanPotentiallyAdvance(effectKind, vanilla, out string _))
                {
                    matched.Add(_actions[i]);
                }
            }

            return matched;
        }

        /// <summary>
        /// What the library has said about itself and where it has said nothing (BQa-010).
        ///
        /// Missing coverage is reported rather than defaulted, because to a consumer that only
        /// asks what matches, an undeclared verb and a kind of want nothing answers both read as
        /// "there is nothing to be done".
        /// </summary>
        public ActionEffectCoverage EffectCoverage()
        {
            List<NarrativeAction> undeclared = new List<NarrativeAction>();
            HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
            List<string> unregistered = new List<string>();

            for (int i = 0; i < _actions.Count; i++)
            {
                ActionEffects effects = _actions[i].Effects;
                if (!effects.IsDeclared)
                {
                    undeclared.Add(_actions[i]);
                    continue;
                }

                for (int e = 0; e < effects.Effects.Count; e++)
                {
                    string kind = effects.Effects[e].Kind;
                    declared.Add(kind);
                    if (!SemanticEffects.IsRegistered(kind) && !unregistered.Contains(kind))
                    {
                        unregistered.Add(kind);
                    }
                }
            }

            List<string> declaredInOrder = new List<string>();
            List<string> unanswered = new List<string>();
            for (int i = 0; i < SemanticEffects.All.Count; i++)
            {
                string kind = SemanticEffects.All[i];
                if (declared.Contains(kind))
                {
                    declaredInOrder.Add(kind);
                }
                else
                {
                    unanswered.Add(kind);
                }
            }

            return new ActionEffectCoverage(undeclared, declaredInOrder, unanswered, unregistered);
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
