using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Bring a known claim to someone with standing to act on it.</summary>
    public sealed class ReportToAuthorityAction : NarrativeAction
    {
        public ReportToAuthorityAction() : base("report", ActionFamily.Social, "Report it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to report to");
            }

            if (AuthorityPolicy.RoleOf(context.TargetNpc) == AuthorityRole.None)
            {
                return Availability.NotRelevant("they have no authority here");
            }

            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing to report");
            }

            if (!context.World.Knowledge.Knows(context.Actor, context.SubjectFact))
            {
                return Availability.Impossible("you cannot report something you do not know");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            AuthorityDecision decision = AuthorityPolicy.Evaluate(context);
            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            // Availability established the fact was there; a projected choice can outlive the
            // state it was drawn against, and every branch below dereferences it.
            if (fact == null)
            {
                outcome = new ActionOutcome(Id, null, "There is nothing left to report.");
                outcome.Notes.Add("the fact behind this report no longer exists");
                return outcome;
            }

            // Saying it again, with nothing new, is not a second accusation. Without this a player
            // could report an unprovable claim over and over, and every rebound would land another
            // FalseAccusation on the accused - a defining memory, -35 affinity and -4 karma each
            // time - which makes an unwinnable accusation into an unlimited weapon.
            if (AlreadyRebounded(context, fact.Id, decision.Response))
            {
                outcome = new ActionOutcome(Id, null, who + " will not hear the same accusation twice without proof.");
                outcome.Notes.Add("repeat accusation at " + decision.Evidence + "; already rebounded once, no new consequence");
                return outcome;
            }

            switch (decision.Response)
            {
                // Every branch below says what the authority did with the report, and nothing
                // about what they will do next. Nobody arrests, investigates or pursues anybody
                // yet - authority autonomy is BQ-093 - and claiming otherwise is the same lie
                // BQ-009 removed from intimidation.
                case AuthorityResponse.Acts:
                    TeachAuthority(context, fact.Id, confidence: 0.95, copyProof: true);
                    outcome = new ActionOutcome(Id, null, who + " takes the report seriously, and writes down what you can show them.");
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.CrimeReported, 0.9, seen: true));
                    outcome.Notes.Add("authority response: " + decision.Role + " accepted it on " + decision.Evidence);
                    outcome.Notes.Add("nobody acts on it yet; authority autonomy arrives at BQ-093");
                    break;

                case AuthorityResponse.OpensInquiry:
                    TeachAuthority(context, fact.Id, confidence: 0.65, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " writes it down, and says they are not willing to act on your word alone.");
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.InquiryOpened, 0.5, seen: false));
                    outcome.Notes.Add("authority response: recorded, not actionable without proof");
                    break;

                case AuthorityResponse.RejectsRumor:
                    TeachAuthority(context, fact.Id, confidence: 0.25, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " files it with the rest of the talk and does nothing.");
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.AccusationRejected, 0.25, seen: false));
                    outcome.Notes.Add("authority response: filed as rumour");
                    break;

                case AuthorityResponse.Rebounds:
                    outcome = new ActionOutcome(Id, null, who + " will not act on this, and it is now known that you said it.");

                    // Truth decides which of these it was, not provability. A player who names
                    // the real thief and simply cannot prove it has not lied about anybody.
                    bool untrue = fact.Truth == TruthState.False;
                    outcome.Events.Add(Accusation(
                        context,
                        fact,
                        untrue ? WorldEventType.FalseAccusation : WorldEventType.AccusationMade,
                        0.6,
                        seen: true));
                    WarnAccused(context, fact, outcome);
                    outcome.Notes.Add(untrue
                        ? "authority response: rejected, and the claim is untrue"
                        : "authority response: rejected for want of proof; the claim itself stands");
                    break;

                default:
                    outcome = new ActionOutcome(Id, null, who + " cannot act on this.");
                    outcome.Notes.Add("authority response: unavailable");
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Records the accusation itself. One shape for all of them, so the ledger describes the
        /// same act consistently however the authority reacted.
        /// </summary>
        private static WorldEvent Accusation(
            ActionContext context, Fact fact, WorldEventType type, double magnitude, bool seen)
        {
            return context.World.Record(
                type,
                context.Actor,
                fact.Subject,
                context.Now,
                magnitude,
                context.Zone,
                new[] { fact.Id },
                seen ? ActionSupport.Bystanders(context, true) : null,
                threadId: context.Thread?.Id ?? EntityId.None);
        }

        /// <summary>
        /// True when this accusation has already rebounded on the player once.
        ///
        /// Making it again with no better evidence is the same accusation, not a second one. It is
        /// read off the ledger rather than off the authority's beliefs, because a rebound is
        /// deliberately something they retain nothing from - and because the harm being guarded
        /// against lands on the accused, who does not care which guard turned it down.
        ///
        /// Only the rebound is limited. Bringing real proof later is always worth hearing, which
        /// is the whole point of the step.
        /// </summary>
        private static bool AlreadyRebounded(ActionContext context, EntityId factId, AuthorityResponse response)
        {
            if (response != AuthorityResponse.Rebounds)
            {
                return false;
            }

            foreach (WorldEvent past in Rebounded(context.World))
            {
                if (past.Actor != context.Actor)
                {
                    continue;
                }

                for (int i = 0; i < past.Related.Count; i++)
                {
                    if (past.Related[i] == factId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Accusations that have already been put on the record and turned down, whichever way
        /// their truth fell.
        /// </summary>
        private static IEnumerable<WorldEvent> Rebounded(NarrativeWorldState world)
        {
            foreach (WorldEvent made in world.Ledger.OfType(WorldEventType.AccusationMade))
            {
                yield return made;
            }

            foreach (WorldEvent falsely in world.Ledger.OfType(WorldEventType.FalseAccusation))
            {
                yield return falsely;
            }
        }

        private static void TeachAuthority(ActionContext context, EntityId factId, double confidence, bool copyProof)
        {
            context.World.Knowledge.TryGetBelief(context.Actor, factId, out KnowledgeRecord actorBelief);
            bool canProve = copyProof && actorBelief != null && actorBelief.CanProve;
            IReadOnlyList<ProofLink> proofs = canProve ? actorBelief.Proofs : null;
            context.World.Knowledge.Teach(
                context.Target,
                factId,
                canProve ? KnowledgeSource.Document : KnowledgeSource.Hearsay,
                confidence,
                context.Now,
                canProve,
                proofs,
                context.Actor);
        }

        private static void WarnAccused(ActionContext context, Fact fact, ActionOutcome outcome)
        {
            if (fact == null || fact.Subject.IsNone || fact.Subject == context.Actor)
            {
                return;
            }

            Fact accusation = context.World.Knowledge.FindFact(context.Actor, FactPredicates.Investigating);
            if (accusation == null)
            {
                accusation = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Investigating, fact.Subject);
                context.World.Knowledge.AddFact(accusation);
            }

            context.World.Knowledge.Teach(fact.Subject, accusation.Id, KnowledgeSource.Hearsay, 0.8, context.Now, false, context.Target);
            outcome.Notes.Add(context.NameOf(fact.Subject) + " learns you made an accusation");
        }
    }
}
