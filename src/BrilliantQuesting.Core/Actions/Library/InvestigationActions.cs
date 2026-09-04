using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Turn over a place looking for the physical thing that proves what happened.
    ///
    /// This is the route that matters for a player with no social game at all: testimony can be
    /// denied, but a ledger in your pack cannot, and evidence found this way is provable.
    /// </summary>
    public sealed class SearchForEvidenceAction : NarrativeAction
    {
        public SearchForEvidenceAction() : base("search", ActionFamily.Information, "Search the scene")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing here worth searching for");
            }

            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            if (fact == null || fact.EvidenceIds.Count == 0)
            {
                return Availability.NotRelevant("this leaves no physical trace");
            }

            if (context.World.Knowledge.CanProve(context.Actor, context.SubjectFact))
            {
                return Availability.NotRelevant("you already have what you need");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Investigation, context.Actor, EntityId.None);
            request.WithModifier("how well it was hidden", fact.Secrecy / 20);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    bool recovered = TryRecoverEvidence(context, fact);
                    context.World.Knowledge.Teach(context.Actor, fact.Id, KnowledgeSource.Document, check.Outcome == CheckOutcome.CriticalPass ? 1.0 : 0.85, context.Now, recovered);
                    outcome = new ActionOutcome(Id, check, recovered
                        ? "You find it - and you can carry it out with you."
                        : "You find the trace, though there is nothing here you could show anyone.");
                    outcome.Notes.Add("learned: " + ActionSupport.Describe(context, fact.Id) + (recovered ? " (provable)" : " (unprovable)"));
                    outcome.Events.Add(context.World.Record(WorldEventType.SecretLearned, context.Actor, fact.Subject, context.Now, 0.4, context.Zone, new[] { fact.Id }));
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "Nothing. If there was anything here, you have missed it.");
                    break;

                default:
                    // Ransacking the place is itself a thing bystanders can report.
                    outcome = new ActionOutcome(Id, check, "You make a mess of the search, and someone notices you where you should not be.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Trespass, context.Actor, fact.Subject, context.Now, 0.4, context.Zone, witnesses: ActionSupport.Bystanders(context, true)));
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Evidence is an ordinary Elin item sitting somewhere. If it is loose at the scene the
        /// player simply picks it up; if someone is carrying it, finding it is not taking it.
        ///
        /// Only what is *here* can be picked up. Searching a room used to reach through the world
        /// to whoever the fact was about and take the object off them wherever they happened to
        /// be, which made every other way of getting hold of evidence - going where it is,
        /// following the person carrying it, lifting it out of a pocket - redundant, and quietly
        /// contradicted the trace the search was supposed to be reading. A search that finds the
        /// story but not the object still teaches the fact; it just cannot prove it.
        /// </summary>
        private static bool TryRecoverEvidence(ActionContext context, Fact fact)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return false;
            }

            for (int i = 0; i < fact.EvidenceIds.Count; i++)
            {
                EntityId evidenceId = fact.EvidenceIds[i];
                EntityId holder = EvidenceHolder(context, evidenceId, fact);
                if (!holder.IsNone && context.Vanilla.TryTransferItem(evidenceId, holder, context.Actor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whoever in this zone has the object, or nobody.</summary>
        private static EntityId EvidenceHolder(ActionContext context, EntityId evidenceId, Fact fact)
        {
            IReadOnlyList<EntityId> present = context.Vanilla.GetCharactersInZone(context.Zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (Carries(context, present[i], evidenceId))
                {
                    return present[i];
                }
            }

            // A dead owner is not listed among the people in a zone, and a body with the evidence
            // still on it is the ordinary case for anything that killed somebody.
            if (!fact.Subject.IsNone
                && context.Vanilla.GetZoneOf(fact.Subject) == context.Zone
                && Carries(context, fact.Subject, evidenceId))
            {
                return fact.Subject;
            }

            // What the place itself keeps - a shelf, a strongbox, a locked cabinet - is reachable
            // only if the place opens to you. A shop does; a counting house behind a lock does not,
            // until somebody either lets you in or you let yourself in. Sites say nothing about
            // this unless a situation makes a point of it, so the ordinary room is unchanged.
            if (!Carries(context, context.Zone, evidenceId))
            {
                return EntityId.None;
            }

            NarrativeSite site = ActionSupport.SiteHere(context);
            return site == null || site.Admits(context.Actor) ? context.Zone : EntityId.None;
        }

        private static bool Carries(ActionContext context, EntityId owner, EntityId itemId)
        {
            if (owner.IsNone)
            {
                return false;
            }

            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(owner);
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Tell someone what you know.
    ///
    /// Whether it lands depends on whether you can prove it, who you are, and what your legal
    /// standing is. An unprovable accusation from a known criminal is not an accusation, it is a
    /// rumour - and it can rebound as one.
    /// </summary>
    public sealed class ExposeSecretAction : NarrativeAction
    {
        public ExposeSecretAction() : base("expose", ActionFamily.Social, "Tell them what you know")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to tell");
            }

            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing to reveal");
            }

            if (!context.World.Knowledge.Knows(context.Actor, context.SubjectFact))
            {
                return Availability.Impossible("you cannot reveal something you do not know");
            }

            if (context.World.Knowledge.BelievesConfidently(context.Target, context.SubjectFact))
            {
                return Availability.NotRelevant("they already know");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId factId = context.SubjectFact;
            Fact fact = context.World.Knowledge.GetFact(factId);
            bool canProve = context.World.Knowledge.CanProve(context.Actor, factId);

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Credibility, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context))
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: true))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: false));

            // Proof is worth more than charm. This is the term that makes burglary a social tool.
            request.WithModifier(canProve ? "you can prove it" : "your word alone", canProve ? -8 : 4);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    double confidence = check.Outcome == CheckOutcome.CriticalPass ? 1.0 : 0.8;
                    context.World.Knowledge.Teach(context.Target, factId, canProve ? KnowledgeSource.Document : KnowledgeSource.Hearsay, confidence, context.Now, canProve, context.Actor);
                    outcome = new ActionOutcome(Id, check, who + " believes you.");
                    outcome.Events.Add(context.World.Record(WorldEventType.SecretRevealed, context.Actor, fact.Subject, context.Now, canProve ? 0.9 : 0.6, context.Zone, new[] { factId }, seen));
                    outcome.Notes.Add(who + " now " + (canProve ? "can prove it too" : "believes it but cannot prove it"));
                    break;
                }

                case CheckOutcome.Fail:
                    // Both halves of this branch used to assume the player had nothing to show.
                    // A tester with proof in hand was told, twice, to go and find evidence - the
                    // one piece of advice that could not help, and the same class of lying
                    // message BQ-009 was about. Somebody who produced proof and was still not
                    // believed has a different problem, and a different route out of it.
                    outcome = new ActionOutcome(Id, check, canProve
                        ? who + " looks at what you have and still will not have it."
                        : who + " does not take your word for it.");
                    outcome.Notes.Add(canProve
                        ? "proof was not the problem; try someone who already half suspects, or make them owe you first"
                        : "try again with evidence, or find someone who already half suspects");
                    break;

                default:
                {
                    // The accusation rebounds: you have announced your interest to your target.
                    outcome = new ActionOutcome(Id, check, who + " thinks you are inventing it - and word gets back.");
                    outcome.Events.Add(context.World.Record(WorldEventType.FalseAccusation, context.Actor, fact.Subject, context.Now, 0.5, context.Zone, new[] { factId }, seen));
                    ActionSupport.WarnUnderInvestigation(
                        context, fact.Subject, context.Target, outcome,
                        note: context.NameOf(fact.Subject) + " learns you are accusing them");

                    break;
                }
            }

            return outcome;
        }
    }

    /// <summary>
    /// Put an object in front of somebody and let them place it (BQ-085).
    ///
    /// The mirror of <see cref="ExposeSecretAction"/>, and deliberately its opposite in every
    /// respect. Telling someone what you know needs you to know it, and lands on how credible you
    /// are. Showing them a thing needs you to be carrying it, and lands on whether *they* have a
    /// history with it: the player who picks a silver ring out of a drain a year later can hand it
    /// to the daughter without ever having learned whose it was.
    ///
    /// <b>There is no roll.</b> Recognition is not a skill - either the person opposite has a route
    /// to the history the object carries or they have not - and a check here would make "show her
    /// the ring again" a way to reroll her memory. What the verb costs is having found the thing
    /// and having got it to her.
    ///
    /// <b>It teaches nobody anything.</b> The event it records names no claim, because recognition
    /// is gated on history the recognizer already had a route to. A ring surfacing does not tell
    /// its owner who took it, and it must not tell the bystanders either.
    /// </summary>
    public sealed class ShowItemAction : NarrativeAction
    {
        public ShowItemAction() : base("show_item", ActionFamily.Information, "Show it to them")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to show it to");
            }

            if (!context.Vanilla.Supports(VanillaCapability.ReadInventory))
            {
                return Availability.Impossible("this build cannot see what you are carrying");
            }

            // Nothing in the pack that this person has any history with. "Nothing at stake" rather
            // than "unlikely to work": a matter that is settled, or an object they never had
            // anything to do with, is not a long shot the player should be allowed to take.
            return Subject(context) == null
                ? Availability.NotRelevant("nothing you are carrying means anything to them")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = Subject(context);
            IReadOnlyList<ProvenanceEntry> recognized =
                ItemProvenance.RecognizedBy(context.World, item.Id, context.Target, context.Now);
            IReadOnlyList<NarrativeThread> matters = ItemProvenance.OpenMatters(context.World, recognized);

            string who = context.NameOf(context.Target);
            ActionOutcome outcome = new ActionOutcome(
                Id, null, who + " knows the " + item.Name + " the moment they see it.");

            // Recorded before anything is reopened, and with no claim on it: what happened is that
            // an object surfaced in front of somebody who could place it. Whoever is standing about
            // sees that much and no more, which is why the witness list is real and the related
            // claims are empty - `ConsequenceEngine` teaches witnesses the claims an event names.
            outcome.Events.Add(context.World.Record(
                WorldEventType.ObjectRecognized,
                context.Actor,
                context.Target,
                context.Now,
                0.4,
                context.Zone,
                witnesses: ActionSupport.Bystanders(context, true),
                evidence: new[] { item.Id }));

            for (int i = 0; i < matters.Count; i++)
            {
                NarrativeThread matter = matters[i];
                ProvenanceEntry link = LinkTo(context.World, recognized, matter);
                string how = link == null
                    ? matter.ArchetypeId
                    : link.Role + " " + link.AgeInDays + "d ago, " + link.RecognizedVia;

                outcome.Notes.Add(who + " places the " + item.Name + " in " + matter.ArchetypeId + " (" + how + ")");

                if (ThreadLifecycle.Reactivate(
                        context.World, matter, context.Now,
                        who + " recognized the " + item.Name + " (" + how + ")"))
                {
                    outcome.Notes.Add("reopened " + matter.Id);
                }
            }

            return outcome;
        }

        /// <summary>
        /// The object the player means, and never one they are not holding (`D011`).
        ///
        /// A named <see cref="ActionContext.SubjectItem"/> wins when it qualifies; with nothing
        /// named, the first thing in carry order that this person has an open matter with is taken,
        /// so the same pack answers the same way twice.
        /// </summary>
        private static ItemDescriptor Subject(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, item => Reopenable(context, item.Id));
        }

        private static bool Reopenable(ActionContext context, EntityId itemId)
        {
            IReadOnlyList<ProvenanceEntry> recognized =
                ItemProvenance.RecognizedBy(context.World, itemId, context.Target, context.Now);
            return recognized.Count > 0 && ItemProvenance.OpenMatters(context.World, recognized).Count > 0;
        }

        /// <summary>
        /// The recognized entry that actually reaches this matter, so the reason recorded on the
        /// reopening names the history it came from rather than whichever entry happened to be
        /// first. Null where the link is one this action cannot attribute to a single entry.
        /// </summary>
        private static ProvenanceEntry LinkTo(
            NarrativeWorldState world,
            IReadOnlyList<ProvenanceEntry> recognized,
            NarrativeThread matter)
        {
            ProvenanceEntry[] one = new ProvenanceEntry[1];
            for (int i = 0; i < recognized.Count; i++)
            {
                one[0] = recognized[i];
                IReadOnlyList<NarrativeThread> reached = ItemProvenance.OpenMatters(world, one);
                for (int r = 0; r < reached.Count; r++)
                {
                    if (reached[r] == matter)
                    {
                        return recognized[i];
                    }
                }
            }

            return null;
        }
    }
}
