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

        public override ActionEffects Effects => ActionEffects
            .Declaring(ActionEffect.Recorded(SemanticEffects.InformationDisclosed))
            .NeedingAnyOf(
                SemanticSlots.Proposition,
                SemanticSlots.Item,
                SemanticSlots.Destination,
                SemanticSlots.Purpose);

        /// <summary>
        /// Success is the authority coming to hold the claim (BQa-013) - acted on, written down
        /// as needing proof, or filed with the rest of the talk. All three are disclosures that
        /// landed, and none of them is a promise that anybody will do anything next: what the law
        /// then does is its own autonomy, not this verb's postcondition.
        ///
        /// A rejection that rebounds is a performed failure, not a refusal. Nobody was taught,
        /// and what changed instead is that it is now known the accuser said it - which is why
        /// the accused is told through the same route somebody would actually have heard it.
        ///
        /// Saying it a second time with nothing new is refused outright. Without that it was not
        /// a repeated accusation but an unlimited weapon: every rebound landed another defining
        /// memory and another standing loss on the accused, for a claim nobody could prove.
        /// </summary>
        public override ActionPostconditions Postconditions => ActionPostconditions
            .Succeeding(SemanticEffects.InformationDisclosed)
            .Failing(FailureOutcomes.InformationRevealed, FailureOutcomes.OptionsTransformed);

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to report to");
            }

            if (AuthorityPolicy.RoleOf(context, context.Target) == AuthorityRole.None)
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

        protected override ActionOutcome PerformCore(ActionContext context)
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
                return outcome.Refuse("the fact behind this report no longer exists");
            }

            // Saying it again, with nothing new, is not a second accusation. Without this a player
            // could report an unprovable claim over and over, and every rebound would land another
            // FalseAccusation on the accused - a defining memory, -35 affinity and -4 karma each
            // time - which makes an unwinnable accusation into an unlimited weapon.
            if (AlreadyRebounded(context, fact.Id, decision.Response))
            {
                outcome = new ActionOutcome(Id, null, who + " will not hear the same accusation twice without proof.");
                return outcome.Refuse("repeat accusation at " + decision.Evidence + "; already rebounded once, no new consequence");
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
                    outcome.Change(SemanticEffects.InformationDisclosed);
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.CrimeReported, 0.9, seen: true, decision));
                    outcome.Notes.Add("authority response: " + decision.Role + " accepted it on " + decision.Evidence);
                    outcome.Notes.Add("nobody acts on it yet; authority autonomy arrives at BQ-093");
                    break;

                case AuthorityResponse.OpensInquiry:
                    TeachAuthority(context, fact.Id, confidence: 0.65, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " writes it down, and says they are not willing to act on your word alone.");
                    outcome.Change(SemanticEffects.InformationDisclosed);
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.InquiryOpened, 0.5, seen: false, decision));
                    outcome.Notes.Add("authority response: recorded, not actionable without proof");
                    break;

                case AuthorityResponse.RejectsRumor:
                    TeachAuthority(context, fact.Id, confidence: 0.25, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " files it with the rest of the talk and does nothing.");
                    outcome.Change(SemanticEffects.InformationDisclosed);
                    outcome.Events.Add(Accusation(context, fact, WorldEventType.AccusationRejected, 0.25, seen: false, decision));
                    outcome.Notes.Add("authority response: filed as rumour");
                    break;

                case AuthorityResponse.Rebounds:
                    outcome = new ActionOutcome(Id, null, who + " will not act on this, and it is now known that you said it.").Fail();

                    // Truth decides which of these it was, not provability. A player who names
                    // the real thief and simply cannot prove it has not lied about anybody.
                    bool untrue = fact.Truth == TruthState.False;
                    outcome.Events.Add(Accusation(
                        context,
                        fact,
                        untrue ? WorldEventType.FalseAccusation : WorldEventType.AccusationMade,
                        0.6,
                        seen: true,
                        decision));
                    WarnAccused(context, fact, outcome);
                    outcome.Notes.Add(untrue
                        ? "authority response: rejected, and the claim is untrue"
                        : "authority response: rejected for want of proof; the claim itself stands");
                    break;

                default:
                    outcome = new ActionOutcome(Id, null, who + " cannot act on this.");
                    outcome.Refuse("authority response: unavailable");
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Records the accusation itself. One shape for all of them, so the ledger describes the
        /// same act consistently however the authority reacted.
        ///
        /// The provenance is the whole reason an accusation is worth reading back. The claim is a
        /// <see cref="CausalRole.Motive"/>, never a cause: the accuser acted on it, and whether it
        /// is true is the fact's business and nobody else's - a frame is a sincere accusation
        /// about a false claim, and the two have to stay distinguishable. The claim's own origin
        /// is what the accusation is <see cref="CausalRole.About"/>, so an accusation about a
        /// theft points at that theft rather than at the stolen thing: one object can be stolen
        /// twice, and item identity cannot tell the two occurrences apart.
        ///
        /// The authority's answer is kept as reason codes, because by the time anyone asks why a
        /// report was filed as rumour, the evidence that decided it has moved on.
        /// </summary>
        private static WorldEvent Accusation(
            ActionContext context, Fact fact, WorldEventType type, double magnitude, bool seen, AuthorityDecision decision)
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
                threadId: context.Thread?.Id ?? EntityId.None,
                provenance: EventProvenance.Draft()
                    .Motive(fact.Id)
                    .About(fact.OriginEvent)
                    .Decided(
                        "authority." + decision.Response,
                        "evidence:" + decision.Evidence,
                        "role:" + decision.Role)
                    .Build());
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
            if (fact == null)
            {
                return;
            }

            ActionSupport.WarnUnderInvestigation(
                context, fact.Subject, context.Target, outcome,
                note: context.NameOf(fact.Subject) + " learns you made an accusation");
        }
    }
}
