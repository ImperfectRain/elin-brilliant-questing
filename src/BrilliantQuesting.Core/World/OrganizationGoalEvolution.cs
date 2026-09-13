using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The goal kinds institutional formation mints (BQa-018).
    ///
    /// Short, and every one of them names a <em>desired end</em> rather than a way of getting
    /// there, for the reason <see cref="EvolvedGoalKinds"/> gives: "get the cart back" and "lean on
    /// whoever took it" are the same want reached two ways, and which way a body takes is the
    /// operation question BQa-019 owns. These are the body's ends; existing
    /// <see cref="OrganizationActivity"/> verbs remain what it does about them.
    /// </summary>
    public static class OrganizationGoalKinds
    {
        /// <summary>A shortage pressing at a place the body answers for eases.</summary>
        public const string RelieveShortage = "relieve_shortage";

        /// <summary>Something recorded as the body's is recorded as the body's again.</summary>
        public const string RecoverHolding = "recover_holding";

        /// <summary>An undertaking the body is party to stops being outstanding.</summary>
        public const string SettleObligation = "settle_obligation";

        /// <summary>One of its people is still alive.</summary>
        public const string ProtectMember = "protect_member";

        /// <summary>Nobody outside the body can demonstrate the claim against it.</summary>
        public const string AnswerClaim = "answer_claim";
    }

    /// <summary>What one institutional evolution pass did to one organization goal.</summary>
    public sealed class OrganizationGoalChange
    {
        internal OrganizationGoalChange(
            EntityId organizationId, GoalChangeKind kind, string pressureId, OrganizationGoal goal, string because)
        {
            OrganizationId = organizationId;
            Kind = kind;
            PressureId = pressureId ?? string.Empty;
            Goal = goal;
            Because = because ?? string.Empty;
        }

        public EntityId OrganizationId { get; }

        public GoalChangeKind Kind { get; }

        /// <summary>The institutional reading this came out of, or empty for a retirement.</summary>
        public string PressureId { get; }

        /// <summary>The goal affected, or null when the pass formed nothing.</summary>
        public OrganizationGoal Goal { get; }

        /// <summary>A code, not a sentence. Nothing branches on it.</summary>
        public string Because { get; }

        public override string ToString()
        {
            return Kind + (Goal == null ? " -" : " " + Goal.Identity)
                   + (Because.Length > 0 ? " (" + Because + ")" : string.Empty);
        }
    }

    /// <summary>
    /// What kind of body answers what kind of trouble with what kind of end (BQa-018).
    ///
    /// A policy table, not a personality. Organizations do not get values, sensitivities or
    /// appetites - duplicating the individual character authorities onto institutions is exactly
    /// what this step is told not to do - and what stands in their place is the one thing an
    /// institution genuinely has that a person does not: a charter. A carters' guild answers a
    /// shortage because moving goods is what it is for; a crew answers a loss by getting the thing
    /// back because it has no other recourse; a family answers harm to one of its own first.
    ///
    /// Ordered, and the first admitted response for a reading wins, so two bodies looking at the
    /// identical matter can legitimately land on different ends without either of them being
    /// wrong. A type nobody registered gets the narrow default rather than a guess, and a body
    /// whose charter admits nothing for a matter forms nothing - which is a charter and not a gap.
    /// </summary>
    public static class OrganizationPolicy
    {
        private static readonly string[] Trade =
        {
            OrganizationGoalKinds.RelieveShortage,
            OrganizationGoalKinds.SettleObligation,
            OrganizationGoalKinds.RecoverHolding,
            OrganizationGoalKinds.AnswerClaim
        };

        private static readonly string[] Underworld =
        {
            OrganizationGoalKinds.RecoverHolding,
            OrganizationGoalKinds.AnswerClaim,
            OrganizationGoalKinds.ProtectMember
        };

        private static readonly string[] Household =
        {
            OrganizationGoalKinds.ProtectMember,
            OrganizationGoalKinds.SettleObligation,
            OrganizationGoalKinds.RelieveShortage
        };

        private static readonly string[] Order =
        {
            OrganizationGoalKinds.ProtectMember,
            OrganizationGoalKinds.RecoverHolding,
            OrganizationGoalKinds.AnswerClaim
        };

        /// <summary>
        /// Narrow on purpose. A body whose charter nobody has written answers only for what it is
        /// plainly party to, rather than inheriting a trade guild's appetite for the town's troubles.
        /// </summary>
        private static readonly string[] Default =
        {
            OrganizationGoalKinds.SettleObligation,
            OrganizationGoalKinds.AnswerClaim
        };

        /// <summary>The ends this kind of body answers with, in the order it prefers them.</summary>
        public static IReadOnlyList<string> Charter(string type)
        {
            switch (type)
            {
                case "merchant_association":
                case "guild":
                case "caravan":
                    return Trade;
                case "criminal_crew":
                case "bandits":
                case "cult":
                    return Underworld;
                case "family":
                    return Household;
                case "watch":
                case "militia":
                    return Order;
                default:
                    return Default;
            }
        }

        /// <summary>Whether this kind of body answers a matter with this end at all.</summary>
        public static bool Admits(string type, string goalKind)
        {
            IReadOnlyList<string> charter = Charter(type);
            for (int i = 0; i < charter.Count; i++)
            {
                if (string.Equals(charter[i], goalKind, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// How one body's goals change as what it has in front of it changes (BQa-018).
    ///
    /// The whole of what this reads to decide <em>whether a body wants something</em> is
    /// <see cref="OrganizationPressureView"/>'s output. That is the knowledge gate and there is no
    /// second one: a condition the world is holding that nobody put in front of this body produces
    /// no reading, no option and therefore no goal - and a claim that is false but still standing
    /// on file produces a reading, and so produces a goal, because what an institution acts on is
    /// what it has been told rather than what is so.
    ///
    /// Authoritative state is read twice here, and neither read decides what the body wants. It is
    /// read to <b>bind</b> the concrete records a reading already names - the demand record at the
    /// place, the ownership fact, the undertaking - because BQa-008 conditions take entity
    /// references and a reading carries ids. And it is read to <b>evaluate</b> an existing goal's
    /// condition, but only once that goal's cause has already left the body's own view: it has
    /// stopped having the matter in front of it, and only then does the world get asked whether the
    /// thing it wanted is so. A goal whose cause is still on file is never closed by a background
    /// pass however true its condition has quietly become, because closing it would hand the
    /// institution the save's knowledge for free.
    ///
    /// <b>The pressure never chooses the goal.</b> A reading offers the ends the body's position
    /// admits; which one it takes is its charter's answer. Two bodies reading the identical matter
    /// can land on different ends, and a body whose charter admits none of them forms nothing.
    ///
    /// Nothing here schedules and nothing here acts. One call is one bounded pass over readings a
    /// caller supplies; scheduling belongs to the production cycle and operations to BQa-019.
    /// </summary>
    public static class OrganizationGoalEvolution
    {
        /// <summary>
        /// How many retired institutional goals one body keeps. History belongs to the event
        /// ledger; this is the body's current state, and an unbounded tail of it would grow the
        /// save for as long as the world turns.
        /// </summary>
        public const int MaxRetained = 16;

        private static readonly IReadOnlyList<OrganizationGoalChange> NoChanges = new OrganizationGoalChange[0];

        /// <summary>
        /// Runs one pass for one body: forms and revises the ends it is pursuing from what it has
        /// in front of it now, and retires the ones that have stopped pressing.
        ///
        /// <paramref name="pressures"/> is normally
        /// <see cref="OrganizationPressureView.Of(NarrativeWorldState, EntityId, IReadOnlyList{Development})"/>
        /// for this body. Passing an empty list is meaningful and is not the same as not calling: it
        /// says nothing is in front of the body, and the goals it formed from pressure are then
        /// judged on whether what it wanted came about.
        /// </summary>
        public static IReadOnlyList<OrganizationGoalChange> Advance(
            NarrativeWorldState world,
            EntityId organizationId,
            IReadOnlyList<OrganizationPressure> pressures,
            GameTime now)
        {
            Organization body = world == null ? null : world.Registry.GetOrganization(organizationId);
            if (body == null)
            {
                return NoChanges;
            }

            List<OrganizationPressure> readings = Its(pressures, organizationId);
            List<OrganizationGoalChange> changes = new List<OrganizationGoalChange>();

            Form(world, body, readings, now, changes);
            Retire(world, body, readings, now, changes);
            Trim(body);
            return changes;
        }

        // -- forming and revising ----------------------------------------------------------------

        private static void Form(
            NarrativeWorldState world,
            Organization body,
            List<OrganizationPressure> readings,
            GameTime now,
            List<OrganizationGoalChange> changes)
        {
            for (int i = 0; i < readings.Count; i++)
            {
                OrganizationPressure reading = readings[i];
                Option option = Appraise(world, body, reading);
                if (option == null)
                {
                    changes.Add(new OrganizationGoalChange(
                        body.Id, GoalChangeKind.NoResponse, reading.Id, null, "charter_admits_nothing"));
                    continue;
                }

                OrganizationGoal formed = new OrganizationGoal(
                    option.Kind,
                    option.Subject,
                    option.Weight,
                    option.Condition,
                    GoalOrigin.FromInstitution(reading, now));

                // Re-aimed rather than newly wanted: the same cause, read again, now comes out as a
                // different end. That is what a correction looks like from here.
                OrganizationGoal standing = StandingFor(body, formed);
                if (standing != null)
                {
                    standing.SupersedeWith(formed, now, "reappraised");
                    changes.Add(new OrganizationGoalChange(
                        body.Id, GoalChangeKind.Superseded, reading.Id, standing, "reappraised"));
                }

                OrganizationGoal adopted = Adopt(body, formed);
                if (ReferenceEquals(adopted, formed))
                {
                    changes.Add(new OrganizationGoalChange(
                        body.Id, GoalChangeKind.Created, reading.Id, adopted, option.Because));
                    continue;
                }

                // Already held. What this pass revises on it is how hard it presses - never its
                // identity, and never the effort already spent on it.
                bool moved = adopted.Weight != option.Weight;
                adopted.Weight = option.Weight;
                changes.Add(new OrganizationGoalChange(
                    body.Id,
                    moved ? GoalChangeKind.Reweighted : GoalChangeKind.Held,
                    reading.Id,
                    adopted,
                    option.Because));
            }
        }

        /// <summary>
        /// The end the body's charter takes for this reading, or null.
        ///
        /// Two stages, and the order is the whole of it. What the <em>position</em> admits is about
        /// the matter - what it names, what the body is party to, where it is - and is the same for
        /// every body standing there. Which admitted end the body <em>takes</em> is its charter's
        /// answer, and is the only place the body's own character enters.
        /// </summary>
        private static Option Appraise(NarrativeWorldState world, Organization body, OrganizationPressure reading)
        {
            List<Option> admitted = new List<Option>();
            AdmitShortageItAnswersFor(world, body, reading, admitted);
            AdmitItsOwnHoldingTaken(world, body, reading, admitted);
            AdmitItsOwnUndertaking(world, body, reading, admitted);
            AdmitHarmToItsOwn(world, body, reading, admitted);
            AdmitClaimAgainstIt(world, body, reading, admitted);
            if (admitted.Count == 0)
            {
                return null;
            }

            IReadOnlyList<string> charter = OrganizationPolicy.Charter(body.Type);
            for (int preference = 0; preference < charter.Count; preference++)
            {
                for (int i = 0; i < admitted.Count; i++)
                {
                    Option option = admitted[i];
                    if (!string.Equals(option.Kind, charter[preference], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    option.Weight = reading.Urgency;
                    return option;
                }
            }

            return null;
        }

        /// <summary>
        /// A shortage at a place the body answers for. Bound to the record that states it, because
        /// <c>demand.relieved</c> is read off that record and a shortage with none recorded is a
        /// question nothing can answer rather than an end to form blind.
        /// </summary>
        private static void AdmitShortageItAnswersFor(
            NarrativeWorldState world, Organization body, OrganizationPressure reading, List<Option> into)
        {
            if (!reading.HasPressure(DevelopmentPressures.Shortage) || reading.FocusFactId.IsNone)
            {
                return;
            }

            EntityId place = PlaceHolding(world, reading);
            if (place.IsNone)
            {
                return;
            }

            into.Add(new Option(
                OrganizationGoalKinds.RelieveShortage,
                place,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.DemandRelieved,
                    new GoalBinding("place", place),
                    new GoalBinding("source", reading.FocusFactId)),
                "short_where_it_trades"));
        }

        /// <summary>
        /// Something the ownership record says is the body's, which the matter on file is about.
        ///
        /// Bound to the ownership record rather than to the claim's wording, because
        /// <c>property.owned_by</c> is answered off that record - and a body that "recovers"
        /// something no record calls its own is helping itself to it.
        /// </summary>
        private static void AdmitItsOwnHoldingTaken(
            NarrativeWorldState world, Organization body, OrganizationPressure reading, List<Option> into)
        {
            if (!(reading.HasPressure(DevelopmentPressures.UnresolvedCrime)
                  || reading.HasPressure(DevelopmentPressures.DamagedProperty))
                || reading.FocusFactId.IsNone)
            {
                return;
            }

            Fact claim = world.Knowledge.GetFact(reading.FocusFactId);
            if (claim == null || claim.Object.IsNone)
            {
                return;
            }

            // Asked of the ownership authority rather than of the claim's wording, because
            // `property.owned_by` is answered off that same record - and a body that "recovers"
            // something no record calls its own is helping itself to it.
            if (Ownership.OwnerOf(world, claim.Object) != body.Id)
            {
                return;
            }

            into.Add(new Option(
                OrganizationGoalKinds.RecoverHolding,
                claim.Object,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.PropertyOwnedBy,
                    new GoalBinding("item", claim.Object),
                    new GoalBinding("owner", body.Id)),
                "its_own_holding"));
        }

        /// <summary>
        /// An open undertaking the body is party to. The ledger names both sides, so which record it
        /// is and which side the body is on are one question, answered once.
        /// </summary>
        private static void AdmitItsOwnUndertaking(
            NarrativeWorldState world, Organization body, OrganizationPressure reading, List<Option> into)
        {
            if (!reading.HasPressure(DevelopmentPressures.UnmetObligation))
            {
                return;
            }

            SocialObligation debt = SoleOpenUndertaking(world, body.Id, reading);
            if (debt == null)
            {
                return;
            }

            into.Add(new Option(
                OrganizationGoalKinds.SettleObligation,
                debt.Debtor == body.Id ? debt.Creditor : debt.Debtor,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ObligationDischarged,
                    new GoalBinding("obligation", debt.Id)),
                "its_own_undertaking"));
        }

        /// <summary>
        /// Harm to one of its own. The member stake is the route's answer to "whose person is this",
        /// so nothing here re-derives membership from the claim.
        /// </summary>
        private static void AdmitHarmToItsOwn(
            NarrativeWorldState world, Organization body, OrganizationPressure reading, List<Option> into)
        {
            if (!(reading.HasPressure(DevelopmentPressures.UnresolvedCrime)
                  || reading.HasPressure(DevelopmentPressures.Adversarial)
                  || reading.HasPressure(DevelopmentPressures.Contested)))
            {
                return;
            }

            for (int i = 0; i < reading.Stakes.Count; i++)
            {
                OrganizationStake stake = reading.Stakes[i];
                if (stake.Kind != OrganizationStakeKind.Member || world.Registry.GetNpc(stake.SubjectId) == null)
                {
                    continue;
                }

                into.Add(new Option(
                    OrganizationGoalKinds.ProtectMember,
                    stake.SubjectId,
                    GoalConditionRegistry.Create(
                        GoalConditionKinds.PersonAlive,
                        new GoalBinding("person", stake.SubjectId)),
                    "one_of_its_own"));
                return;
            }
        }

        /// <summary>
        /// A claim the body itself is named in. The counter-end: what the matter admits for whoever
        /// is answering it runs against whoever is pursuing it.
        ///
        /// Being named is not a route to knowing. There is no reading here unless the body was told,
        /// so a body accused behind its back forms nothing and one that has had it reported forms
        /// this whether or not the claim is true.
        /// </summary>
        private static void AdmitClaimAgainstIt(
            NarrativeWorldState world, Organization body, OrganizationPressure reading, List<Option> into)
        {
            if (reading.FocusFactId.IsNone || !reading.HasStake(OrganizationStakeKind.Charge))
            {
                return;
            }

            if (world.Knowledge.GetFact(reading.FocusFactId) == null)
            {
                return;
            }

            into.Add(new Option(
                OrganizationGoalKinds.AnswerClaim,
                body.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ClaimUnproven,
                    new GoalBinding("claim", reading.FocusFactId),
                    new GoalBinding("subject", body.Id)),
                "named_in_it"));
        }

        /// <summary>
        /// The active institutional goal that came from the same cause and is a different end, or
        /// null. Matched on the causing reading and on the record it was about, so a cause that
        /// changes key between passes - a matter the detector stops reporting while the body's file
        /// still stands - is recognised as the same cause rather than as a second one.
        /// </summary>
        private static OrganizationGoal StandingFor(Organization body, OrganizationGoal formed)
        {
            for (int i = 0; i < body.Goals.Count; i++)
            {
                OrganizationGoal goal = body.Goals[i];
                if (goal == null
                    || !goal.IsActive
                    || goal.Origin.Kind != GoalSourceKind.InstitutionalReading
                    || string.Equals(goal.Identity, formed.Identity, StringComparison.Ordinal))
                {
                    continue;
                }

                if (SameCause(goal.Origin, formed.Origin))
                {
                    return goal;
                }
            }

            return null;
        }

        private static bool SameCause(GoalOrigin left, GoalOrigin right)
        {
            if (left.SourceId.Length > 0
                && string.Equals(left.SourceId, right.SourceId, StringComparison.Ordinal))
            {
                return true;
            }

            return !left.RecordId.IsNone && left.RecordId == right.RecordId;
        }

        /// <summary>
        /// Adds the end, or hands back the one the body already holds. Identity rather than a fresh
        /// record, so a pass that reads the same matter again lands on the goal it formed last time
        /// instead of stacking another copy of it - including after a reload.
        /// </summary>
        private static OrganizationGoal Adopt(Organization body, OrganizationGoal formed)
        {
            for (int i = 0; i < body.Goals.Count; i++)
            {
                OrganizationGoal goal = body.Goals[i];
                if (goal != null
                    && goal.IsActive
                    && string.Equals(goal.Identity, formed.Identity, StringComparison.Ordinal))
                {
                    return goal;
                }
            }

            body.Goals.Add(formed);
            return formed;
        }

        // -- retiring ----------------------------------------------------------------------------

        private static void Retire(
            NarrativeWorldState world,
            Organization body,
            List<OrganizationPressure> readings,
            GameTime now,
            List<OrganizationGoalChange> changes)
        {
            List<OrganizationGoal> active = new List<OrganizationGoal>();
            for (int i = 0; i < body.Goals.Count; i++)
            {
                OrganizationGoal goal = body.Goals[i];
                if (goal != null && goal.IsActive && goal.Origin.Kind == GoalSourceKind.InstitutionalReading)
                {
                    active.Add(goal);
                }
            }

            for (int i = 0; i < active.Count; i++)
            {
                OrganizationGoal goal = active[i];
                if (StillPressing(goal, readings))
                {
                    continue;
                }

                // Nothing is in front of the body about this any more - the filing was corrected or
                // withdrawn, or the condition stopped being reported. Only now is the world asked
                // whether what it wanted is so.
                if (goal.Evaluate(world) == GoalConditionState.Met)
                {
                    goal.Satisfy(now, "condition_met");
                    changes.Add(new OrganizationGoalChange(
                        body.Id, GoalChangeKind.Satisfied, string.Empty, goal, "condition_met"));
                    continue;
                }

                goal.Abandon(now, "lapsed");
                changes.Add(new OrganizationGoalChange(
                    body.Id, GoalChangeKind.Abandoned, string.Empty, goal, "lapsed"));
            }
        }

        private static bool StillPressing(OrganizationGoal goal, List<OrganizationPressure> readings)
        {
            for (int i = 0; i < readings.Count; i++)
            {
                OrganizationPressure reading = readings[i];
                if (goal.Origin.SourceId.Length > 0
                    && string.Equals(goal.Origin.SourceId, reading.Id, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!goal.Origin.RecordId.IsNone && goal.Origin.RecordId == reading.FocusFactId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drops the oldest retired institutional goals past <see cref="MaxRetained"/>. Only ones
        /// this owner formed: a goal a generator or a fixture established is not this pass's to
        /// throw away.
        /// </summary>
        private static void Trim(Organization body)
        {
            int retired = 0;
            for (int i = 0; i < body.Goals.Count; i++)
            {
                OrganizationGoal goal = body.Goals[i];
                if (goal != null && !goal.IsActive && goal.Origin.Kind == GoalSourceKind.InstitutionalReading)
                {
                    retired++;
                }
            }

            for (int i = 0; i < body.Goals.Count && retired > MaxRetained; i++)
            {
                OrganizationGoal goal = body.Goals[i];
                if (goal == null || goal.IsActive || goal.Origin.Kind != GoalSourceKind.InstitutionalReading)
                {
                    continue;
                }

                body.Goals.RemoveAt(i--);
                retired--;
            }
        }

        // -- binding concrete records a reading already names ------------------------------------

        private static EntityId PlaceHolding(NarrativeWorldState world, OrganizationPressure reading)
        {
            for (int i = 0; i < reading.SiteIds.Count; i++)
            {
                EntityId site = reading.SiteIds[i];
                IReadOnlyList<LocalDemandPressure> demands = world.Demands.At(site);
                for (int d = 0; d < demands.Count; d++)
                {
                    if (demands[d].SourceFactId == reading.FocusFactId && demands[d].Active)
                    {
                        return site;
                    }
                }
            }

            return EntityId.None;
        }

        /// <summary>
        /// The one open undertaking this reading is about that the body is party to, or null when
        /// there is none or more than one. Refusing an ambiguous answer costs an end; guessing one
        /// would bind a goal to a debt the body never meant.
        /// </summary>
        private static SocialObligation SoleOpenUndertaking(
            NarrativeWorldState world, EntityId bodyId, OrganizationPressure reading)
        {
            SocialObligation found = null;
            IReadOnlyList<SocialObligation> debts = world.Obligations.Records;
            for (int i = 0; i < debts.Count; i++)
            {
                SocialObligation debt = debts[i];
                if (!debt.IsOpen || (debt.Debtor != bodyId && debt.Creditor != bodyId))
                {
                    continue;
                }

                if (reading.HasObjectiveCause
                    && !string.Equals(
                        reading.DevelopmentId, "dev.unmet_obligation:" + debt.Id.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = debt;
            }

            return found;
        }

        private static List<OrganizationPressure> Its(
            IReadOnlyList<OrganizationPressure> pressures, EntityId organizationId)
        {
            List<OrganizationPressure> mine = new List<OrganizationPressure>();
            if (pressures == null)
            {
                return mine;
            }

            for (int i = 0; i < pressures.Count; i++)
            {
                OrganizationPressure reading = pressures[i];
                if (reading != null && reading.OrganizationId == organizationId)
                {
                    mine.Add(reading);
                }
            }

            // The caller's order is not a contract, and a pass that forms goals in a different
            // order forms a different set once anything caps or supersedes.
            mine.Sort(ById);
            return mine;
        }

        private static int ById(OrganizationPressure left, OrganizationPressure right) =>
            string.CompareOrdinal(left.Id, right.Id);

        /// <summary>One end a body's position admits, before its charter has chosen between them.</summary>
        private sealed class Option
        {
            internal Option(string kind, EntityId subject, GoalCondition condition, string because)
            {
                Kind = kind;
                Subject = subject;
                Condition = condition;
                Because = because;
            }

            internal string Kind { get; }

            internal EntityId Subject { get; }

            internal GoalCondition Condition { get; }

            /// <summary>A code, not a sentence.</summary>
            internal string Because { get; }

            internal int Weight { get; set; }
        }
    }
}
