using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// A low-stakes conversation beat. It does not reveal the secret directly, but it makes later
    /// social checks less hostile and gives non-social characters a quiet way to move the case.
    /// </summary>
    public sealed class BuildRapportAction : NarrativeAction
    {
        public BuildRapportAction() : base("rapport", ActionFamily.Social, "Build rapport")
        {
        }

        /// <summary>
        /// How warm small talk can make somebody before it stops being small talk. Chosen low on
        /// purpose: rapport is a way in, not a substitute for doing anything.
        /// </summary>
        private const int WarmthCeiling = 20;

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to talk with");
            }

            // The first live run had a player choose this three times in a row against the same
            // person, banking affinity each time. Pleasantries do not compound forever: past a
            // point somebody is as well-disposed towards a near-stranger as small talk can make
            // them, and going round again is the player farming a number rather than playing.
            if (context.Affinity >= WarmthCeiling)
            {
                return Availability.NotRelevant("small talk has taken you as far as it will with them");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            string who = context.NameOf(context.Target);
            ActionOutcome outcome = new ActionOutcome(Id, null, "You keep the conversation light. " + who + " seems a little more willing to hear you out.");
            outcome.Events.Add(context.World.Record(WorldEventType.Helped, context.Actor, context.Target, context.Now, 0.2, context.Zone));
            outcome.Events.Add(context.World.Record(WorldEventType.Conversed, context.Actor, context.Target, context.Now, 0.2, context.Zone));
            outcome.Notes.Add("rapport improved; future social checks should be less hostile");
            return outcome;
        }
    }

    /// <summary>
    /// Ask someone what they know.
    ///
    /// A failure here is not a dead end. Push a witness badly enough and they mention that
    /// somebody has been asking questions - to exactly the person you were asking about.
    /// </summary>
    public sealed class QuestionAction : NarrativeAction
    {
        public QuestionAction() : base("question", ActionFamily.Information, "Ask what they know")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to ask");
            }

            if (ActionSupport.FindTeachableFact(context).IsNone)
            {
                return Availability.NotRelevant("they know nothing you do not");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId factId = ActionSupport.FindTeachableFact(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Interrogation, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context))
                .With(SituationalModifiers.Grudge(context))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: false));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    // They tell you, and they hand over what backs it up.
                    context.World.Knowledge.TryGetBelief(context.Target, factId, out KnowledgeRecord targetBelief);
                    bool canProve = targetBelief != null && targetBelief.CanProve;
                    KnowledgeSource source = ActionSupport.DisclosureSource(context, factId);
                    context.World.Knowledge.Teach(
                        context.Actor,
                        factId,
                        source,
                        0.95,
                        context.Now,
                        canProve,
                        canProve ? targetBelief.Proofs : null,
                        context.Target);
                    outcome = new ActionOutcome(Id, check, who + " tells you everything, and offers to back it up.");
                    outcome.Notes.Add("learned: " + ActionSupport.Describe(context, factId));
                    outcome.Events.Add(context.World.Record(WorldEventType.Conversed, context.Actor, context.Target, context.Now, 0.4, context.Zone, new[] { factId }));
                    break;

                case CheckOutcome.Pass:
                    context.World.Knowledge.Teach(context.Actor, factId, ActionSupport.DisclosureSource(context, factId), 0.6, context.Now, false, context.Target);
                    outcome = new ActionOutcome(Id, check, who + " tells you what they heard.");
                    outcome.Notes.Add("learned (hearsay, unprovable): " + ActionSupport.Describe(context, factId));
                    outcome.Events.Add(context.World.Record(WorldEventType.Conversed, context.Actor, context.Target, context.Now, 0.3, context.Zone));
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " has nothing to say to you.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Conversed, context.Actor, context.Target, context.Now, 0.1, context.Zone));
                    break;

                default:
                    outcome = new ActionOutcome(Id, check, who + " tells you nothing - and mentions your interest to the wrong person.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Conversed, context.Actor, context.Target, context.Now, 0.2, context.Zone));
                    WarnTheSubject(context, factId, outcome);
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// The critical failure that makes investigation risky: the person you are investigating
        /// finds out you are investigating them.
        /// </summary>
        private static void WarnTheSubject(ActionContext context, EntityId factId, ActionOutcome outcome)
        {
            Fact fact = context.World.Knowledge.GetFact(factId);
            if (fact == null || fact.Subject.IsNone || fact.Subject == context.Actor)
            {
                return;
            }

            ActionSupport.WarnUnderInvestigation(context, fact.Subject, context.Target, outcome, confidence: 0.7);
        }
    }

    /// <summary>Ask straight out for cooperation.</summary>
    public sealed class PersuadeAction : NarrativeAction
    {
        public PersuadeAction() : base("persuade", ActionFamily.Social, "Persuade")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to persuade");
            }

            if (!ActionBinding.HasRequiredSemanticSlots(Id, context))
            {
                return Availability.NotRelevant("nothing specific to ask for");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Persuasion, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context))
                .With(SituationalModifiers.Grudge(context))
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: true));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            ActionBinding binding = ActionBinding.Infer(context);
            string purpose = binding.Describe(context);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                {
                    ActionOutcome outcome = new ActionOutcome(Id, check, who + " agrees to help with " + purpose + ", and seems glad to have been asked.");
                    outcome.Events.Add(context.World.Record(WorldEventType.PromiseMade, context.Target, context.Actor, context.Now, 0.6, context.Zone, related: Related(binding), threadId: ThreadId(context)));
                    outcome.Events.Add(context.World.Record(WorldEventType.Helped, context.Actor, context.Target, context.Now, 0.4, context.Zone));
                    AdmitRestrictedSite(context, outcome);
                    return outcome;
                }

                case CheckOutcome.Pass:
                {
                    ActionOutcome outcome = new ActionOutcome(Id, check, who + " agrees to help with " + purpose + ".");
                    outcome.Events.Add(context.World.Record(WorldEventType.PromiseMade, context.Target, context.Actor, context.Now, 0.5, context.Zone, related: Related(binding), threadId: ThreadId(context)));
                    AdmitRestrictedSite(context, outcome);
                    return outcome;
                }

                case CheckOutcome.Fail:
                    return new ActionOutcome(Id, check, who + " turns you down.");

                default:
                {
                    // Pushed too hard: the ask stopped reading as an ask.
                    ActionOutcome outcome = new ActionOutcome(Id, check, "You press too hard. " + who + " takes it as a threat.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.3, context.Zone, witnesses: ActionSupport.Bystanders(context, true)));
                    return outcome;
                }
            }
        }

        private static EntityId ThreadId(ActionContext context)
        {
            return context.Thread?.Id ?? EntityId.None;
        }

        private static EntityId[] Related(ActionBinding binding)
        {
            if (binding != null && !binding.PropositionFact.IsNone)
            {
                return new[] { binding.PropositionFact };
            }

            return null;
        }

        private static void AdmitRestrictedSite(ActionContext context, ActionOutcome outcome)
        {
            NarrativeSite site = RestrictedSiteInReach(context);
            if (site == null || site.Admits(context.Actor))
            {
                return;
            }

            site.Admit(context.Actor);
            outcome.Notes.Add("talked your way into " + site.Name);
        }

        private static NarrativeSite RestrictedSiteInReach(ActionContext context)
        {
            NarrativeSite here = ActionSupport.SiteHere(context);
            if (here != null && !here.Admits(context.Actor))
            {
                return here;
            }

            if (context.Thread == null)
            {
                return null;
            }

            for (int i = 0; i < context.Thread.SiteIds.Count; i++)
            {
                NarrativeSite site = context.World.Registry.GetSite(context.Thread.SiteIds[i]);
                if (site != null && !site.Admits(context.Actor))
                {
                    return site;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Deny something you know to be true.
    ///
    /// Hard requirement, not a stat gate: you cannot lie about a thing you have never heard of.
    /// A hopeless liar is still allowed to try, and the critical failure is the interesting part.
    /// </summary>
    public sealed class LieAction : NarrativeAction
    {
        public LieAction() : base("lie", ActionFamily.Social, "Lie about it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to lie to");
            }

            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing specific to deny");
            }

            if (!context.World.Knowledge.Knows(context.Actor, context.SubjectFact))
            {
                return Availability.Impossible("you cannot deny something you have never heard of");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId factId = context.SubjectFact;
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Deception, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context))
                .With(SituationalModifiers.Grudge(context))
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: false))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: false));

            // Someone who can prove it is not going to be talked out of it.
            if (context.World.Knowledge.CanProve(context.Target, factId))
            {
                request.WithModifier("they have proof", 8);
            }

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    Shake(context, factId, 0.05);
                    outcome = new ActionOutcome(Id, check, who + " believes you completely, and apologises for doubting.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Deceived, context.Actor, context.Target, context.Now, 0.5, context.Zone, new[] { factId }));
                    break;

                case CheckOutcome.Pass:
                    Shake(context, factId, 0.35);
                    outcome = new ActionOutcome(Id, check, who + " accepts your version of it.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Deceived, context.Actor, context.Target, context.Now, 0.4, context.Zone, new[] { factId }));
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " does not believe a word of it.");
                    break;

                default:
                {
                    // Denying it this badly is itself evidence. Their confidence goes up, not down.
                    Harden(context, factId, 0.9);
                    outcome = new ActionOutcome(Id, check, "You contradict yourself. " + who + " is now certain you are hiding something.");
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome.Events.Add(context.World.Record(WorldEventType.DeceptionExposed, context.Actor, context.Target, context.Now, 0.6, context.Zone, new[] { factId }, seen));
                    break;
                }
            }

            return outcome;
        }

        private static void Shake(ActionContext context, EntityId factId, double factor)
        {
            if (context.World.Knowledge.TryGetBelief(context.Target, factId, out KnowledgeRecord belief))
            {
                belief.Confidence *= factor;
            }
        }

        private static void Harden(ActionContext context, EntityId factId, double confidence)
        {
            if (context.World.Knowledge.TryGetBelief(context.Target, factId, out KnowledgeRecord belief))
            {
                if (belief.Confidence < confidence)
                {
                    belief.Confidence = confidence;
                }
            }
            else
            {
                context.World.Knowledge.Teach(context.Target, factId, KnowledgeSource.Inference, 0.5, context.Now, false, context.Actor);
            }
        }
    }
}
