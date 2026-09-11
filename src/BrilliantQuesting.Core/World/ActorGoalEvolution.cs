using System;
using System.Collections.Generic;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The goal kinds automatic formation mints, as constants rather than as strings at call sites.
    ///
    /// Short on purpose, and every one of them names a <em>desired end</em> rather than a way of
    /// getting there. "Clear my name" and "frighten the one witness who could show it" are the same
    /// want - <c>claim.unproven</c> - reached two ways, and which way somebody takes is the action
    /// question BQa-010 and BQa-011 own. Minting a second goal kind for the second route would put
    /// method into goal identity, and an actor would then hold two wants where they have one.
    /// </summary>
    public static class EvolvedGoalKinds
    {
        /// <summary>Nobody but them can show the claim they are named in.</summary>
        public const string ClearName = "clear_name";

        /// <summary>An undertaking of theirs stops being outstanding.</summary>
        public const string RepayDebt = "repay_debt";

        /// <summary>The shortage pressing on a place they are in eases.</summary>
        public const string RelieveShortage = "relieve_shortage";
    }

    /// <summary>What one evolution pass did to one goal. Derived, transient, for people and tests.</summary>
    public enum GoalChangeKind
    {
        /// <summary>A want the actor did not hold before.</summary>
        Created,

        /// <summary>The same want, pressing differently than it did last pass.</summary>
        Reweighted,

        /// <summary>The same want, unchanged. Recorded so "nothing happened" is visible.</summary>
        Held,

        /// <summary>Another want took over from this one for the same cause.</summary>
        Superseded,

        /// <summary>The condition holds and the actor has a legitimate route to knowing it.</summary>
        Satisfied,

        /// <summary>The want ended without its condition holding.</summary>
        Abandoned,

        /// <summary>
        /// The actor is legitimately under this pressure and formed nothing from it. A real answer:
        /// waiting is a response, and so is a matter no registered want covers. Silence here would
        /// make a coverage gap and a deliberate wait look identical.
        /// </summary>
        NoResponse
    }

    /// <summary>One thing an evolution pass did, in references and codes rather than in prose.</summary>
    public sealed class GoalChange
    {
        internal GoalChange(EntityId actorId, GoalChangeKind kind, string pressureId, NpcGoal goal, string because)
        {
            ActorId = actorId;
            Kind = kind;
            PressureId = pressureId ?? string.Empty;
            Goal = goal;
            Because = because ?? string.Empty;
        }

        public EntityId ActorId { get; }

        public GoalChangeKind Kind { get; }

        /// <summary>The actor-local reading this came out of, or empty for a retirement.</summary>
        public string PressureId { get; }

        /// <summary>The goal affected, or null when the pass formed nothing.</summary>
        public NpcGoal Goal { get; }

        /// <summary>A code, not a sentence. Nothing branches on it.</summary>
        public string Because { get; }

        public override string ToString()
        {
            return Kind + (Goal == null ? " -" : " " + Goal.Identity)
                   + (Because.Length > 0 ? " (" + Because + ")" : string.Empty);
        }
    }

    /// <summary>
    /// How one actor's wants change as what presses on them changes (BQa-009).
    ///
    /// The whole of what this reads to decide <em>whether somebody wants something</em> is the
    /// actor-local pressure view BQa-007 produces. That is the knowledge gate and there is no
    /// second one: a condition the world is holding that this actor has no route to produces no
    /// reading, produces no option, and therefore produces no goal - and a claim that is false but
    /// sincerely held produces a reading, and so produces a goal, because what somebody is prepared
    /// to act on is what they believe rather than what is so.
    ///
    /// Authoritative state is read twice here and for two narrow jobs, neither of which is deciding
    /// what somebody wants. It is read to <b>bind</b> the concrete records a reading already names -
    /// the claim it focuses on, the debt the actor is party to, the shortage record at the place
    /// they are in - because BQa-008 conditions take entity references and a reading carries ids.
    /// And it is read to <b>evaluate</b> an existing goal's condition, but only once that goal's
    /// cause has already left the actor's own view: they have stopped being under pressure about it
    /// by their own lights, and only then does the world get asked whether the thing they wanted is
    /// so. A goal whose cause is still pressing is never closed by a background pass, however true
    /// its condition has quietly become - that is BQa-011's separation, and closing it here would
    /// hand every actor the save's knowledge for free.
    ///
    /// <b>The pressure never chooses the goal.</b> A reading offers the wants its position admits;
    /// which one this person forms is decided by the authorities that already exist for it - values,
    /// sensitivities, personality, problem-solving tendency and the lines in their negative space.
    /// Two people reading the identical pressure can land on different wants, and a person whose
    /// every admitted want is one they will not take forms none, which is a legitimate answer and
    /// not a gap.
    ///
    /// Nothing here schedules anything, and nothing here acts. One call is one bounded pass over
    /// readings a caller supplies; the autonomous month that would call it belongs to BQa-016.
    /// </summary>
    public static class ActorGoalEvolution
    {
        private static readonly IReadOnlyList<GoalChange> NoChanges = new GoalChange[0];

        /// <summary>Retirement code for a want its owner stopped holding while it still pressed.</summary>
        private const string GaveUp = "gave_up";

        /// <summary>
        /// Runs one pass for one actor: forms and revises wants from what presses on them now, and
        /// retires the ones that have stopped pressing or that they have stopped holding.
        ///
        /// <paramref name="pressures"/> is normally
        /// <see cref="ActorPressureView.Of(NarrativeWorldState, EntityId, IReadOnlyList{Development}, Integration.IVanillaState)"/>
        /// for this actor. Passing an empty list is meaningful and is not the same as not calling:
        /// it says nothing presses on them, and the goals they formed from pressure are then judged
        /// on whether what they wanted came about.
        /// </summary>
        public static IReadOnlyList<GoalChange> Advance(
            NarrativeWorldState world,
            EntityId actorId,
            IReadOnlyList<ActorLocalPressure> pressures,
            GameTime now)
        {
            NarrativeNpc actor = world == null ? null : world.Registry.GetNpc(actorId);
            if (actor == null)
            {
                return NoChanges;
            }

            List<ActorLocalPressure> readings = Mine(pressures, actorId);
            List<GoalChange> changes = new List<GoalChange>();

            Form(world, actor, readings, now, changes);
            Retire(world, actor, readings, now, changes);
            return changes;
        }

        // -- forming and revising ----------------------------------------------------------------

        private static void Form(
            NarrativeWorldState world,
            NarrativeNpc actor,
            List<ActorLocalPressure> readings,
            GameTime now,
            List<GoalChange> changes)
        {
            for (int i = 0; i < readings.Count; i++)
            {
                ActorLocalPressure reading = readings[i];
                Option option = Appraise(world, actor, reading);
                if (option == null)
                {
                    changes.Add(new GoalChange(
                        actor.Id, GoalChangeKind.NoResponse, reading.Id, null, "no_want_taken"));
                    continue;
                }

                NpcGoal formed = new NpcGoal(
                    option.Kind,
                    option.Subject,
                    option.Weight,
                    option.Reason,
                    option.Condition,
                    GoalOrigin.FromPressure(reading, now));

                if (RecentlyGaveUp(actor, formed, now))
                {
                    // They held this exact want and let it go, and the thing that made them let it
                    // go has not changed. Taking it straight back on would make giving up mean
                    // nothing: a person who gave up would be back on it the next morning, forever.
                    changes.Add(new GoalChange(
                        actor.Id, GoalChangeKind.NoResponse, reading.Id, null, "given_up_recently"));
                    continue;
                }

                // Re-aimed rather than newly wanted: the same cause, appraised again, now comes out
                // as a different want. Retiring the old one first also frees the active slot, so the
                // collection's crowding-out never has to choose between a want and its replacement.
                NpcGoal standing = StandingFor(actor, formed);
                if (standing != null)
                {
                    standing.SupersedeWith(formed, now, "reappraised");
                    changes.Add(new GoalChange(
                        actor.Id, GoalChangeKind.Superseded, reading.Id, standing, "reappraised"));
                }

                NpcGoal adopted = actor.Goals.Adopt(formed);
                if (ReferenceEquals(adopted, formed))
                {
                    changes.Add(new GoalChange(
                        actor.Id, GoalChangeKind.Created, reading.Id, adopted, option.Because));
                    continue;
                }

                // Already held. Adopt hands back the want rather than a second copy of it, and what
                // this pass revises on it is how hard it presses and why - never its identity.
                bool moved = adopted.Weight != option.Weight;
                adopted.Weight = option.Weight;
                adopted.Reason = option.Reason;
                changes.Add(new GoalChange(
                    actor.Id,
                    moved ? GoalChangeKind.Reweighted : GoalChangeKind.Held,
                    reading.Id,
                    adopted,
                    option.Because));
            }
        }

        /// <summary>
        /// The active pressure-formed goal that came from the same cause and is a different want, or
        /// null. Matched on the causing reading and on the record it focused on, so a cause that
        /// changes key between passes - a matter the detector stops reporting while its holder goes
        /// on believing it - is still recognised as the same cause rather than as a second one.
        /// </summary>
        private static NpcGoal StandingFor(NarrativeNpc actor, NpcGoal formed)
        {
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                NpcGoal goal = actor.Goals[i];
                if (goal == null
                    || !goal.IsActive
                    || goal.Origin.Kind != GoalSourceKind.ActorPressure
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

        // -- retiring ----------------------------------------------------------------------------

        private static void Retire(
            NarrativeWorldState world,
            NarrativeNpc actor,
            List<ActorLocalPressure> readings,
            GameTime now,
            List<GoalChange> changes)
        {
            List<NpcGoal> active = new List<NpcGoal>();
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                NpcGoal goal = actor.Goals[i];
                if (goal != null && goal.IsActive && goal.Origin.Kind == GoalSourceKind.ActorPressure)
                {
                    active.Add(goal);
                }
            }

            for (int i = 0; i < active.Count; i++)
            {
                NpcGoal goal = active[i];
                if (StillPressing(goal, readings))
                {
                    // It still presses. The only thing that ends a want under live pressure is the
                    // person's own resolve running out, which is a legitimate outcome and the
                    // reason nothing here escalates an unfulfilled goal on its behalf.
                    if (ResolveSpent(actor, goal, now))
                    {
                        goal.Abandon(now, GaveUp);
                        changes.Add(new GoalChange(
                            actor.Id, GoalChangeKind.Abandoned, string.Empty, goal, GaveUp));
                    }

                    continue;
                }

                // Nothing presses on them about this any more, by their own lights. Only now is the
                // world asked whether what they wanted is so.
                if (goal.Evaluate(world) == GoalConditionState.Met)
                {
                    goal.ActorAssessment = GoalAssessment.BelievedMet;
                    goal.Satisfy(now, "condition_met");
                    changes.Add(new GoalChange(
                        actor.Id, GoalChangeKind.Satisfied, string.Empty, goal, "condition_met"));
                    continue;
                }

                goal.Abandon(now, "lapsed");
                changes.Add(new GoalChange(
                    actor.Id, GoalChangeKind.Abandoned, string.Empty, goal, "lapsed"));
            }
        }

        private static bool StillPressing(NpcGoal goal, List<ActorLocalPressure> readings)
        {
            for (int i = 0; i < readings.Count; i++)
            {
                ActorLocalPressure reading = readings[i];
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
        /// Whether this person has been holding this want longer than they have it in them to.
        ///
        /// Read off character and off how hard the want presses, never off whether progress was
        /// made: there is no durable progress marker here and inventing one would be a saved field
        /// this step does not need.
        /// </summary>
        private static bool ResolveSpent(NarrativeNpc actor, NpcGoal goal, GameTime now)
        {
            long held = now.DaysSince(goal.Origin.FormedAt);
            return held > 0 && held > ResolveWindow(actor, goal.Weight);
        }

        /// <summary>
        /// Whether they gave this exact want up recently enough that taking it straight back on
        /// would be churn rather than a change of heart.
        ///
        /// Read off the retirement the goal already records, so nothing new is saved for it. The
        /// window is the same measure as <see cref="ResolveWindow"/> on purpose: how long somebody
        /// stays with a thing is also how long they stay away from it, and one measure for both
        /// keeps a patient character from being both slow to give up and quick to relapse.
        /// </summary>
        private static bool RecentlyGaveUp(NarrativeNpc actor, NpcGoal formed, GameTime now)
        {
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                NpcGoal goal = actor.Goals[i];
                if (goal == null
                    || goal.Lifecycle != GoalLifecycle.Abandoned
                    || !string.Equals(goal.RetirementCode, GaveUp, StringComparison.Ordinal)
                    || !string.Equals(goal.Identity, formed.Identity, StringComparison.Ordinal))
                {
                    continue;
                }

                if (now.DaysSince(goal.RetiredAt) <= ResolveWindow(actor, goal.Weight))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How many days of unchanged pressure this person has in them for a want of this weight.
        /// A patient, earnest person holding something heavy keeps it for seasons; an impatient one
        /// holding something light lets it go within the week.
        /// </summary>
        private static long ResolveWindow(NarrativeNpc actor, int weight)
        {
            PersonalityWeights personality = actor.Personality;
            double resolve = Clamp01(
                (0.55 * personality.Patience)
                + (0.25 * personality.Earnestness)
                + (0.20 * (weight / 100.0)));

            return 3 + (long)Math.Round(57.0 * resolve);
        }

        // -- what a reading admits, and which of it this person takes ----------------------------

        /// <summary>
        /// The one want this person forms from this reading, or null for none.
        ///
        /// The two halves are deliberately separate. What a reading <em>admits</em> is a question
        /// about position - who the matter names, what they are party to, where they are - and is
        /// the same for everybody standing there. Which admitted want somebody <em>takes</em> is a
        /// question about them, and is answered by the character authorities that already exist
        /// rather than by anything this file decides for itself.
        /// </summary>
        private static Option Appraise(NarrativeWorldState world, NarrativeNpc actor, ActorLocalPressure reading)
        {
            List<Option> admitted = new List<Option>();
            AdmitNamedInAClaim(world, actor, reading, admitted);
            AdmitOwnDebt(world, actor, reading, admitted);
            AdmitShortageWhereTheyAre(world, actor, reading, admitted);
            if (admitted.Count == 0)
            {
                return null;
            }

            double pressing = reading.Urgency / 100.0;
            Option best = null;
            double bestScore = 0.0;
            for (int i = 0; i < admitted.Count; i++)
            {
                Option option = admitted[i];
                ProhibitionRuling ruling = NegativeSpace.Rule(
                    actor.NegativeSpace, option.Style, pressing, "pressure " + reading.Id);
                if (ruling.Forbids)
                {
                    // A line they hold takes this want off the table however much it appeals. If it
                    // takes all of them off, they form nothing and go on waiting, which is a
                    // character and not a hole in the vocabulary.
                    continue;
                }

                double score = option.Appetite + (0.35 * actor.ProblemSolving.Get(option.Style));
                if (best == null || score > bestScore)
                {
                    best = option;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                return null;
            }

            best.Weight = Weigh(reading.Urgency, best.Appetite);
            return best;
        }

        /// <summary>
        /// How hard the want presses: the reading's own urgency, which is already this actor's
        /// rather than the world's, moved by how much the want appeals to them. Never above what
        /// the matter presses at, so appetite can colour a want and cannot manufacture one.
        /// </summary>
        private static int Weigh(int urgency, double appetite)
        {
            int weight = (int)Math.Round(urgency * (0.55 + (0.45 * Clamp01(appetite))));
            return weight < 0 ? 0 : weight > 100 ? 100 : weight;
        }

        /// <summary>
        /// They are the one a claim names. The counter-goal case: the pressure belongs to whoever
        /// is pursuing the matter, and what it admits for its subject runs against it.
        ///
        /// Being named is not by itself a route to knowing - the reading is, and there is no reading
        /// here unless BQa-007 already established one, which for a claim means they hold it. So
        /// somebody accused behind their back forms nothing, and somebody who has heard of it forms
        /// this whether or not the claim is true.
        /// </summary>
        private static void AdmitNamedInAClaim(
            NarrativeWorldState world,
            NarrativeNpc actor,
            ActorLocalPressure reading,
            List<Option> into)
        {
            if (!reading.HasPressure(DevelopmentPressures.UnresolvedCrime))
            {
                return;
            }

            Fact claim = world.Knowledge.GetFact(reading.FocusFactId);
            if (claim == null || claim.Subject != actor.Id)
            {
                return;
            }

            SensitivityProfile sensitivities = actor.Sensitivities;
            into.Add(new Option(
                EvolvedGoalKinds.ClearName,
                actor.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ClaimUnproven,
                    new GoalBinding("claim", claim.Id),
                    new GoalBinding("subject", actor.Id)),
                ProblemSolvingStyle.AskAuthority,
                Clamp01((0.45 * sensitivities.PublicEmbarrassment)
                        + (0.30 * actor.Values.Status.Importance)
                        + (0.25 * actor.Personality.Boldness)),
                "named_in_a_claim",
                "named in a matter they have heard of and want nobody able to demonstrate"));

            // The other end they could want instead: an undertaking of their own that came out of
            // the same occurrence. Somebody who makes it good is not clearing their name, they are
            // ending a different thing, and the two are rival wants rather than two routes to one.
            SocialObligation amends = OpenDebtFromOccurrence(world, actor.Id, claim.OriginEvent);
            if (amends == null)
            {
                return;
            }

            PersonalityWeights personality = actor.Personality;
            into.Add(new Option(
                EvolvedGoalKinds.RepayDebt,
                amends.Creditor,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ObligationDischarged,
                    new GoalBinding("obligation", amends.Id)),
                ProblemSolvingStyle.DoItSelf,
                Clamp01((0.40 * personality.Honesty)
                        + (0.25 * personality.Conventionality)
                        + (0.20 * actor.Values.Law.Importance)
                        + (0.15 * personality.Mercy)),
                "amends_for_the_same_occurrence",
                "an undertaking of theirs from the same occurrence, which they would rather end"));
        }

        /// <summary>
        /// A debt they owe. The reading establishes that they are party to it; which record it is
        /// comes from the ledger, and only when the answer is unambiguous.
        /// </summary>
        private static void AdmitOwnDebt(
            NarrativeWorldState world,
            NarrativeNpc actor,
            ActorLocalPressure reading,
            List<Option> into)
        {
            if (!reading.HasPressure(DevelopmentPressures.UnmetObligation))
            {
                return;
            }

            for (int i = 0; i < reading.Stakes.Count; i++)
            {
                ActorStake stake = reading.Stakes[i];
                if (stake.Kind != ActorStakeKind.Obligation)
                {
                    continue;
                }

                // The stake names the other party. Asking the ledger for the one open undertaking
                // running from this actor to them answers both which record it is and which side
                // they are on: a creditor has none running that way and admits nothing here.
                SocialObligation debt = SoleOpenDebt(world, actor.Id, stake.SubjectId);
                if (debt == null)
                {
                    continue;
                }

                into.Add(new Option(
                    EvolvedGoalKinds.RepayDebt,
                    debt.Creditor,
                    GoalConditionRegistry.Create(
                        GoalConditionKinds.ObligationDischarged,
                        new GoalBinding("obligation", debt.Id)),
                    ProblemSolvingStyle.DoItSelf,
                    Clamp01((0.40 * actor.Sensitivities.UnpaidDebt)
                            + (0.30 * actor.Personality.Honesty)
                            + (0.30 * actor.Values.Law.Importance)),
                    "owes_it",
                    "an open undertaking of theirs they would rather have done with"));
                return;
            }
        }

        /// <summary>
        /// A shortage at a place they are in. Bound to the record that states it, because
        /// <c>demand.relieved</c> is answered off that record and a shortage with none recorded is
        /// a question nothing can answer rather than a want to form blind.
        /// </summary>
        private static void AdmitShortageWhereTheyAre(
            NarrativeWorldState world,
            NarrativeNpc actor,
            ActorLocalPressure reading,
            List<Option> into)
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

            PersonalityWeights personality = actor.Personality;
            into.Add(new Option(
                EvolvedGoalKinds.RelieveShortage,
                place,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.DemandRelieved,
                    new GoalBinding("place", place),
                    new GoalBinding("source", reading.FocusFactId)),
                ProblemSolvingStyle.DoItSelf,
                Clamp01((0.35 * actor.Values.Wealth.Importance)
                        + (0.25 * personality.Generosity)
                        + (0.20 * personality.Warmth)
                        + (reading.HasStake(ActorStakeKind.Property) ? 0.20 : 0.0)),
                "short_where_they_are",
                "a shortage where they are, which they would rather see eased"));
        }

        // -- binding concrete records a reading already names ------------------------------------

        private static EntityId PlaceHolding(NarrativeWorldState world, ActorLocalPressure reading)
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
        /// The one open undertaking running from this debtor to this creditor, or null when there
        /// is none or more than one. Refusing an ambiguous answer costs a want; guessing one would
        /// bind a goal to a debt its owner never meant.
        /// </summary>
        private static SocialObligation SoleOpenDebt(NarrativeWorldState world, EntityId debtor, EntityId creditor)
        {
            if (debtor.IsNone || creditor.IsNone)
            {
                return null;
            }

            SocialObligation found = null;
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation record = records[i];
                if (!record.IsOpen || record.Debtor != debtor || record.Creditor != creditor)
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = record;
            }

            return found;
        }

        /// <summary>The first open undertaking of this debtor's that came out of the same occurrence.</summary>
        private static SocialObligation OpenDebtFromOccurrence(
            NarrativeWorldState world,
            EntityId debtor,
            EntityId occurrence)
        {
            if (occurrence.IsNone)
            {
                return null;
            }

            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation record = records[i];
                if (record.IsOpen && record.Debtor == debtor && record.SourceEventId == occurrence)
                {
                    return record;
                }
            }

            return null;
        }

        // -- plumbing ----------------------------------------------------------------------------

        private static List<ActorLocalPressure> Mine(IReadOnlyList<ActorLocalPressure> pressures, EntityId actorId)
        {
            List<ActorLocalPressure> mine = new List<ActorLocalPressure>();
            if (pressures == null)
            {
                return mine;
            }

            for (int i = 0; i < pressures.Count; i++)
            {
                ActorLocalPressure reading = pressures[i];
                if (reading != null && reading.ActorId == actorId)
                {
                    mine.Add(reading);
                }
            }

            mine.Sort(ById);
            return mine;
        }

        private static int ById(ActorLocalPressure left, ActorLocalPressure right) =>
            string.CompareOrdinal(left.Id, right.Id);

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;

        /// <summary>One want a reading admits, before anybody has decided whether to take it.</summary>
        private sealed class Option
        {
            internal Option(
                string kind,
                EntityId subject,
                GoalCondition condition,
                ProblemSolvingStyle style,
                double appetite,
                string because,
                string reason)
            {
                Kind = kind;
                Subject = subject;
                Condition = condition;
                Style = style;
                Appetite = appetite;
                Because = because;
                Reason = reason;
            }

            internal string Kind { get; }

            internal EntityId Subject { get; }

            internal GoalCondition Condition { get; }

            /// <summary>
            /// The move this want leans on, so the lines in an actor's negative space can bear on
            /// it. It decides eligibility here and nothing else: what is actually attempted for a
            /// goal is BQa-011's question, not this one's.
            /// </summary>
            internal ProblemSolvingStyle Style { get; }

            /// <summary>0..1, how much this want appeals to this person.</summary>
            internal double Appetite { get; }

            /// <summary>A code for the inspector.</summary>
            internal string Because { get; }

            /// <summary>The sentence that goes on the goal. For people; nothing parses it.</summary>
            internal string Reason { get; }

            internal int Weight { get; set; }
        }
    }
}
