using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Autonomy
{
    /// <summary>
    /// Coarse continuation of actor-local intentions while the player is elsewhere (BQ-095).
    ///
    /// This schedules opportunities, not outcomes. An actor's saved <see cref="NarrativeNpc.LastSimulatedAt"/>
    /// says whether another coarse opening is due; their existing <see cref="NpcGoal"/> says why
    /// they care; BQ-135 activity says whether vanilla is already carrying them; and the selected
    /// intention is an ordinary <see cref="NarrativeAction"/> resolved through BQ-093.
    /// </summary>
    public sealed class OffScreenSchemes
    {
        public const long DefaultIntervalDays = 7;

        public long IntervalDays { get; set; } = DefaultIntervalDays;

        public double OpportunityFloor { get; set; } = 0.25;

        public int MostActorsPerPass { get; set; } = 12;

        public int MostAttemptsPerPass { get; set; } = 4;

        public int MostColdActorsPerPass { get; set; } = 2;
        public long ColdIntervalDays { get; set; } = 30;
        public int LastActorsInspected { get; private set; }

        /// <summary>Fresh vanilla readback, never a saved or extrapolated Home economy.</summary>
        public HomeState LastHomeObservation { get; private set; }

        public static SimulationTier TierOf(NarrativeNpc npc, ActorActivity activity)
        {
            if (npc.BackgroundTier == SimulationTier.Archived) return SimulationTier.Archived;
            return activity?.Presence == PhysicalPresence.InActiveZone ? SimulationTier.Active : npc.BackgroundTier;
        }

        /// <summary>
        /// Called after native zone entry. Read what vanilla caught up; never call Simulate or replay
        /// production, hobbies, needs or travel. Unread Home state stays absent.
        /// </summary>
        public void ReconcileZone(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            LastHomeObservation = vanilla.GetHomeState();
            if (LastHomeObservation == null || LastHomeObservation.ZoneId != vanilla.GetZoneOf(vanilla.PlayerId)) return;
            foreach (HomeResident resident in LastHomeObservation.Residents)
            {
                NarrativeNpc npc = world.Registry.GetNpc(resident.Id);
                if (npc != null && vanilla.GetActorActivity(npc.Id)?.Presence == PhysicalPresence.InActiveZone
                    && now.TotalMinutes > npc.LastSimulatedAt.TotalMinutes) npc.LastSimulatedAt = now;
            }
        }

        public List<OffScreenSchemeTrace> LastPass { get; } = new List<OffScreenSchemeTrace>();

        public int Advance(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            GameTime now)
        {
            LastPass.Clear();
            LastActorsInspected = 0;
            if (world == null || vanilla == null || checks == null || registry == null || IntervalDays <= 0)
            {
                return 0;
            }

            int considered = 0;
            int acted = 0;
            List<NarrativeNpc> actors = DueActors(world, vanilla, now);
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].LastSimulatedAt = now;
            }

            for (int i = 0; i < actors.Count && considered < MostActorsPerPass; i++)
            {
                NarrativeNpc actor = actors[i];
                if (!HasOpenGoal(actor))
                {
                    continue;
                }

                considered++;
                OffScreenSchemeTrace trace = Consider(world, vanilla, checks, registry, actor, now);
                if (trace != null)
                {
                    LastPass.Add(trace);
                    if (trace.Acted)
                    {
                        acted++;
                    }
                }

                if (acted >= MostAttemptsPerPass)
                {
                    break;
                }
            }

            return acted;
        }

        private OffScreenSchemeTrace Consider(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            NarrativeNpc actor,
            GameTime now)
        {
            OffScreenSchemeTrace trace = new OffScreenSchemeTrace(actor.Id, now);

            ActorActivity activity = vanilla.GetActorActivity(actor.Id);
            if (activity.VanillaMovementState() == VanillaMovement.Moving)
            {
                trace.Refusal = "vanilla is already carrying them between zones; BQ leaves embodiment to Elin";
                trace.Activity = activity;
                return trace;
            }

            if (world.Absences.IsAbsent(actor.Id))
            {
                trace.Refusal = "they are already under an absence record";
                trace.Activity = activity;
                return trace;
            }

            SchemeCandidate best = null;
            ActionContext bestContext = null;
            List<NpcGoal> goals = GoalsByWeight(actor);

            for (int g = 0; g < goals.Count; g++)
            {
                NpcGoal goal = goals[g];
                trace.Goals.Add(goal);
                List<SchemeCandidate> candidates = CandidatesFor(world, vanilla, registry, actor, goal, trace);
                for (int c = 0; c < candidates.Count; c++)
                {
                    SchemeCandidate candidate = candidates[c];
                    InterventionOpportunity opportunity = InterventionOpportunity.Read(
                        vanilla,
                        actor.Id,
                        MatterZone(world, vanilla, candidate));

                    string contextRefusal;
                    ActionContext context;
                    bool built = ActorContexts.TryBuildOffScreen(
                        world,
                        vanilla,
                        checks,
                        world.Rng,
                        actor.Id,
                        candidate.Target,
                        out context,
                        out contextRefusal);

                    NarrativeAction action = registry.Get(candidate.ActionId);
                    Availability availability = Availability.Impossible("no registered verb with id '" + candidate.ActionId + "'");
                    string barred = string.Empty;
                    if (built && action != null)
                    {
                        context.Thread = candidate.Thread;
                        context.SubjectFact = candidate.SubjectFact;
                        context.SubjectItem = candidate.SubjectItem;
                        context.ThirdParty = candidate.ThirdParty;
                        context.Binding = candidate.Binding;
                        // The whole feasibility order, not just the verb's half: a coarse pass
                        // that asked only about availability would select an act across two
                        // zones and discover at `Run` that the world never allowed it (BQa-014).
                        AttemptFeasibility feasibility = AttemptFeasibility.Classify(action, context);
                        availability = feasibility.Availability;
                        opportunity = opportunity.Refined(feasibility.Opportunity);
                        barred = OffScreenBar(action);
                    }
                    else if (!built)
                    {
                        availability = Availability.Impossible(contextRefusal);
                    }

                    OffScreenSchemeOption option = Weigh(actor, goal, candidate, opportunity, availability, barred);
                    trace.Options.Add(option);

                    if (!option.Eligible(OpportunityFloor))
                    {
                        continue;
                    }

                    if (best == null || Beats(option, best.Option))
                    {
                        best = candidate.With(option);
                        bestContext = context;
                    }
                }
            }

            if (best == null)
            {
                trace.Refusal = trace.Options.Count == 0
                    ? "no existing action represented an open goal"
                    : "every represented intention was unavailable, barred, or implausible";
                return trace;
            }

            ActionIntent intent = new ActionIntent(actor.Id, best.ActionId, best.Target, best.Option.GoalReason)
            {
                SubjectFact = best.SubjectFact,
                SubjectItem = best.SubjectItem,
                Thread = best.Thread
            };

            trace.Attempt = ActionAttempt.Run(registry, intent, bestContext);
            trace.Chosen = best.Option;
            ReadTheWantAgainst(world, best.Goal, trace, now);

            return trace;
        }

        private List<NarrativeNpc> DueActors(
            NarrativeWorldState world,
            IVanillaState vanilla,
            GameTime now)
        {
            List<NarrativeNpc> actors = new List<NarrativeNpc>();
            foreach (NarrativeNpc npc in world.Registry.TakeSimulationActors(MostActorsPerPass, MostColdActorsPerPass))
            {
                LastActorsInspected++;
                if (npc == null
                    || !npc.IsCanonical
                    || npc.Id == vanilla.PlayerId
                    || !npc.Alive
                    || !vanilla.IsAlive(npc.Id))
                {
                    continue;
                }

                SimulationTier tier = TierOf(npc, vanilla.GetActorActivity(npc.Id));
                if (tier == SimulationTier.Active)
                {
                    if (now.TotalMinutes > npc.LastSimulatedAt.TotalMinutes) npc.LastSimulatedAt = now;
                    continue;
                }
                long intervalDays = tier == SimulationTier.Cold ? ColdIntervalDays : IntervalDays;
                if (intervalDays > 0 && now.DaysSince(npc.LastSimulatedAt) >= intervalDays)
                {
                    actors.Add(npc);
                }
            }

            actors.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            return actors;
        }

        /// <summary>
        /// Whether anything is still wanted. Active, not merely unsatisfied: BQa-009 retires a want
        /// somebody gave up or reappraised into a different one, and "not satisfied" reads both of
        /// those as still being pursued, which would have this pass keep chasing a want its owner
        /// has already stopped holding.
        /// </summary>
        private static bool HasOpenGoal(NarrativeNpc npc)
        {
            for (int i = 0; i < npc.Goals.Count; i++)
            {
                if (npc.Goals[i] != null && npc.Goals[i].IsActive && npc.Goals[i].Weight > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<NpcGoal> GoalsByWeight(NarrativeNpc npc)
        {
            List<NpcGoal> goals = new List<NpcGoal>();
            for (int i = 0; i < npc.Goals.Count; i++)
            {
                NpcGoal goal = npc.Goals[i];
                if (goal != null && goal.IsActive && goal.Weight > 0)
                {
                    goals.Add(goal);
                }
            }

            goals.Sort((a, b) =>
            {
                int weight = b.Weight.CompareTo(a.Weight);
                return weight != 0 ? weight : string.CompareOrdinal(a.Kind, b.Kind);
            });
            return goals;
        }

        private static List<SchemeCandidate> CandidatesFor(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ActionRegistry registry,
            NarrativeNpc actor,
            NpcGoal goal,
            OffScreenSchemeTrace trace)
        {
            List<SchemeCandidate> candidates = new List<SchemeCandidate>();

            // A want that says what it wants is answered by the bridge and by nothing else
            // (BQa-011). The name-matching below is what remains for wants that have no
            // machine-readable condition to read - an old save's goals and hand-established
            // fixtures - and falling through to it for a covered want would put an unrelated
            // verb behind a want that had already said no verb here could serve it.
            GoalRouteSearch search = GoalRoutes.Discover(world, vanilla, registry, actor, goal);
            trace.Searches.Add(search);
            if (search.IsSupported)
            {
                for (int i = 0; i < search.Routes.Count; i++)
                {
                    candidates.Add(SchemeCandidate.FromRoute(search.Routes[i], world));
                }

                return candidates;
            }

            string kind = goal.Kind ?? string.Empty;

            if (Has(kind, "repay") || Has(kind, "settle_debt") || Has(kind, "pay_debt"))
            {
                Fact debt = FindDebt(world, actor.Id, goal.Subject, debtorSide: true);
                if (debt != null)
                {
                    candidates.Add(new SchemeCandidate(goal, "pay_debt", debt.Object)
                        .WithFact(debt.Id)
                        .WithThread(FindThread(world, debt.Id, debt.Subject, debt.Object))
                        .WithStyle(ProblemSolvingStyle.PaySomeone, "debt fact " + debt.Id));
                }
            }

            if (Has(kind, "flee") || Has(kind, "avoid_debt") || Has(kind, "go_to_ground"))
            {
                Fact debt = FindDebt(world, actor.Id, goal.Subject, debtorSide: true);
                if (debt != null)
                {
                    candidates.Add(new SchemeCandidate(goal, "go_to_ground", debt.Object)
                        .WithFact(debt.Id)
                        .WithThread(FindThread(world, debt.Id, debt.Subject, debt.Object))
                        .WithStyle(ProblemSolvingStyle.Flee, "debt fact " + debt.Id));
                }
            }

            if (Has(kind, "recover_money") || Has(kind, "collect_debt"))
            {
                Fact debt = FindDebt(world, actor.Id, goal.Subject, debtorSide: false);
                if (debt != null)
                {
                    candidates.Add(new SchemeCandidate(goal, "intimidate", debt.Subject)
                        .WithFact(debt.Id)
                        .WithThread(FindThread(world, debt.Id, debt.Subject, debt.Object))
                        .WithStyle(ProblemSolvingStyle.Confront, "creditor pressure over " + debt.Id));
                }
            }

            if (Has(kind, "steal") || Has(kind, "acquire") || Has(kind, "recover_property"))
            {
                EntityId target = TargetForTheft(world, vanilla, actor.Id, goal.Subject, out EntityId item);
                if (!target.IsNone)
                {
                    candidates.Add(new SchemeCandidate(goal, "pickpocket", target)
                        .WithItem(item)
                        .WithStyle(ProblemSolvingStyle.Conceal, item.IsNone ? "target carries valuables" : "target holds " + item));
                }
            }

            if (Has(kind, "court") || Has(kind, "befriend") || Has(kind, "build_rapport"))
            {
                if (world.Registry.IsActor(goal.Subject))
                {
                    candidates.Add(new SchemeCandidate(goal, "rapport", goal.Subject)
                        .WithStyle(ProblemSolvingStyle.AskFriends, "relationship goal"));
                }
            }

            if (Has(kind, "hire") || Has(kind, "ask_help") || Has(kind, "recruit_help"))
            {
                if (world.Registry.IsActor(goal.Subject))
                {
                    candidates.Add(new SchemeCandidate(goal, "persuade", goal.Subject)
                        .WithBinding(new ActionBinding { Purpose = Purpose(goal, "help") })
                        .WithStyle(ProblemSolvingStyle.AskFriends, "requested help"));
                    candidates.Add(new SchemeCandidate(goal, "bribe", goal.Subject)
                        .WithBinding(new ActionBinding { Purpose = Purpose(goal, "paid help") })
                        .WithStyle(ProblemSolvingStyle.PaySomeone, "paid help"));
                }
            }

            if (Has(kind, "hide_evidence") || Has(kind, "avoid_exposure") || Has(kind, "conceal"))
            {
                Fact secret = FindExposure(world, actor.Id, goal.Subject);
                if (secret != null)
                {
                    candidates.Add(new SchemeCandidate(goal, "destroy_evidence", EntityId.None)
                        .WithFact(secret.Id)
                        .WithItem(FirstEvidenceHeldBy(vanilla, actor.Id, secret))
                        .WithThread(FindThread(world, secret.Id, secret.Subject, secret.Object))
                        .WithStyle(ProblemSolvingStyle.Conceal, "evidence on " + secret.Id));

                    EntityId knower = FirstOtherKnower(world, vanilla, actor.Id, secret.Id);
                    if (!knower.IsNone)
                    {
                        candidates.Add(new SchemeCandidate(goal, "lie", knower)
                            .WithFact(secret.Id)
                            .WithThread(FindThread(world, secret.Id, secret.Subject, secret.Object))
                            .WithStyle(ProblemSolvingStyle.Manipulate, "someone else knows " + secret.Id));
                    }
                }
            }

            if (Has(kind, "revenge") || Has(kind, "retaliate"))
            {
                if (world.Registry.IsActor(goal.Subject))
                {
                    candidates.Add(new SchemeCandidate(goal, "intimidate", goal.Subject)
                        .WithBinding(new ActionBinding { Purpose = Purpose(goal, "revenge") })
                        .WithStyle(ProblemSolvingStyle.Confront, "revenge target"));
                    candidates.Add(new SchemeCandidate(goal, "sabotage", goal.Subject)
                        .WithStyle(ProblemSolvingStyle.UseViolence, "revenge target"));
                }
            }

            if (Has(kind, "invest") || Has(kind, "fund_supplier") || Has(kind, "support_supplier"))
            {
                Fact pressure = FindEconomicPressure(world, goal.Subject);
                NarrativeThread thread = pressure == null
                    ? FindThread(world, EntityId.None, goal.Subject, EntityId.None)
                    : FindThread(world, pressure.Id, pressure.Subject, pressure.Object);
                EntityId target = pressure == null ? goal.Subject : TargetForEconomicPressure(world, pressure);
                if (!target.IsNone)
                {
                    candidates.Add(new SchemeCandidate(goal, "invest_in_supplier", target)
                        .WithFact(pressure?.Id ?? EntityId.None)
                        .WithThread(thread)
                        .WithStyle(ProblemSolvingStyle.PaySomeone, "economic pressure"));
                    candidates.Add(new SchemeCandidate(goal, "buy_supplies", target)
                        .WithFact(pressure?.Id ?? EntityId.None)
                        .WithThread(thread)
                        .WithStyle(ProblemSolvingStyle.PaySomeone, "economic pressure"));
                }
            }

            return candidates;
        }

        private static OffScreenSchemeOption Weigh(
            NarrativeNpc actor,
            NpcGoal goal,
            SchemeCandidate candidate,
            InterventionOpportunity opportunity,
            Availability availability,
            string barred)
        {
            double motive = goal.Weight / 100.0;
            double styleFit = actor.ProblemSolving.Get(candidate.Style);
            double score = opportunity.Plausibility * motive * (0.35 + styleFit);
            List<string> terms = new List<string>
            {
                "goal " + goal.Kind + " weight " + Number(motive),
                "reads as " + candidate.Style + ", which they favour " + Number(styleFit),
                "opportunity " + Number(opportunity.Plausibility),
                candidate.Reason
            };

            if (candidate.EffectKind.Length > 0)
            {
                terms.Insert(1, "would " + candidate.EffectKind + ", which is what the want asks for");
            }

            return new OffScreenSchemeOption(
                actor.Id,
                candidate.Target,
                goal.Kind,
                candidate.ActionId,
                candidate.Style,
                availability,
                opportunity,
                score,
                terms,
                barred);
        }

        /// <summary>
        /// The one bar that is about the verb rather than about the world.
        ///
        /// It used to also refuse a target vanilla was carrying between zones. That reading moved
        /// to <see cref="ActionOpportunity"/> with BQa-014, where it is asked of both parties, in
        /// both evidence modes, and for every surface rather than only for this pass.
        /// </summary>
        private static string OffScreenBar(NarrativeAction action)
        {
            return action.Embodiment.Mode == EmbodimentMode.Delegated
                ? "it needs a delegated vanilla body write, and no such write is verified off screen"
                : string.Empty;
        }

        /// <summary>
        /// What the attempt did to the want, asked of authoritative state rather than of the
        /// outcome's label (BQa-011).
        ///
        /// The rule this replaces was "it succeeded and it recorded something, so the want is
        /// done", which closed a recovery on a pleasant conversation. Two questions are asked
        /// instead, and they are deliberately different questions.
        ///
        /// <b>Does the condition hold?</b> That is the world's answer and it is recorded on the
        /// trace whatever it is. It is objective, and it is not the owner's to have.
        ///
        /// <b>Does the owner know?</b> Only when what they themself did succeeded and the world
        /// now says the condition holds. Somebody who tries a thing and it works knows what they
        /// did; somebody whose cargo was quietly recovered by a stranger while they were asleep
        /// does not, and closing their want here would be exactly the omniscience the step forbids.
        /// The want stays open, stays stale, and is retired by their own goal evolution when the
        /// pressure behind it stops pressing on them.
        ///
        /// A want that never said what it wanted keeps the old rule, because there is nothing here
        /// to ask instead and stranding a hand-established scenario's goals forever would be a
        /// worse answer than the shortcut. It is the covered wants that the shortcut is wrong for,
        /// and those are exactly the ones that can now be asked properly.
        /// </summary>
        private static void ReadTheWantAgainst(
            NarrativeWorldState world,
            NpcGoal goal,
            OffScreenSchemeTrace trace,
            GameTime now)
        {
            if (goal == null)
            {
                return;
            }

            if (!goal.HasCondition)
            {
                if (trace.Attempt?.Outcome != null
                    && trace.Attempt.Outcome.Succeeded
                    && trace.Attempt.Outcome.Events.Count > 0)
                {
                    goal.Satisfy(now, "attempt_succeeded");
                    trace.GoalSatisfied = true;
                    trace.ClosedByTheAttemptAlone = true;
                }

                return;
            }

            trace.WantWasReadable = true;

            GoalConditionState state = goal.Evaluate(world);
            trace.ConditionAfter = state;
            if (state != GoalConditionState.Met)
            {
                return;
            }

            if (trace.Attempt?.Outcome == null || !trace.Attempt.Outcome.Succeeded)
            {
                trace.ConditionHoldsUntaught = true;
                return;
            }

            goal.ActorAssessment = GoalAssessment.BelievedMet;
            goal.Satisfy(now, "condition_met");
            trace.GoalSatisfied = true;
        }

        private static bool Beats(OffScreenSchemeOption option, OffScreenSchemeOption best)
        {
            if (option.Score > best.Score)
            {
                return true;
            }

            if (option.Score < best.Score)
            {
                return false;
            }

            int action = string.CompareOrdinal(option.ActionId, best.ActionId);
            if (action != 0)
            {
                return action < 0;
            }

            return string.CompareOrdinal(option.Target.Value, best.Target.Value) < 0;
        }

        private static EntityId MatterZone(NarrativeWorldState world, IVanillaState vanilla, SchemeCandidate candidate)
        {
            if (candidate.Thread != null && candidate.Thread.SiteIds.Count > 0)
            {
                return candidate.Thread.SiteIds[0];
            }

            if (!candidate.Target.IsNone)
            {
                return vanilla.GetZoneOf(candidate.Target);
            }

            Fact fact = world.Knowledge.GetFact(candidate.SubjectFact);
            if (fact != null && world.Registry.IsActor(fact.Subject))
            {
                return vanilla.GetZoneOf(fact.Subject);
            }

            return EntityId.None;
        }

        private static bool Has(string text, string word)
        {
            return text != null && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Purpose(NpcGoal goal, string fallback)
        {
            return !string.IsNullOrEmpty(goal.Reason) ? goal.Reason : fallback + " for " + goal.Kind;
        }

        private static Fact FindDebt(NarrativeWorldState world, EntityId actor, EntityId subject, bool debtorSide)
        {
            Fact named = world.Knowledge.GetFact(subject);
            if (IsDebtFor(named, actor, EntityId.None, debtorSide))
            {
                return named;
            }

            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (IsDebtFor(fact, actor, subject, debtorSide))
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsDebtFor(Fact fact, EntityId actor, EntityId subject, bool debtorSide)
        {
            if (fact == null || fact.Predicate != FactPredicates.Owes || fact.Truth != TruthState.True)
            {
                return false;
            }

            if (debtorSide)
            {
                return fact.Subject == actor && (subject.IsNone || subject == fact.Object || subject == fact.Id);
            }

            return fact.Object == actor && (subject.IsNone || subject == fact.Subject || subject == fact.Id);
        }

        private static Fact FindExposure(NarrativeWorldState world, EntityId actor, EntityId subject)
        {
            Fact named = world.Knowledge.GetFact(subject);
            if (IsExposure(named, actor))
            {
                return named;
            }

            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (IsExposure(fact, actor))
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsExposure(Fact fact, EntityId actor)
        {
            return fact != null
                   && fact.Subject == actor
                   && fact.Truth == TruthState.True
                   && fact.Secrecy > 0;
        }

        private static Fact FindEconomicPressure(NarrativeWorldState world, EntityId subject)
        {
            Fact named = world.Knowledge.GetFact(subject);
            if (IsEconomicPressure(named))
            {
                return named;
            }

            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if ((subject.IsNone || fact.Subject == subject || fact.Object == subject)
                    && IsEconomicPressure(fact))
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsEconomicPressure(Fact fact)
        {
            return fact != null
                   && fact.Truth == TruthState.True
                   && (fact.Predicate == FactPredicates.Needs || fact.Predicate == FactPredicates.Damaged);
        }

        private static EntityId TargetForEconomicPressure(NarrativeWorldState world, Fact pressure)
        {
            if (pressure == null)
            {
                return EntityId.None;
            }

            if (pressure.Predicate == FactPredicates.Needs && world.Registry.IsActor(pressure.Subject))
            {
                return pressure.Subject;
            }

            if (pressure.Predicate == FactPredicates.Damaged)
            {
                foreach (Fact fact in world.Knowledge.Facts.Values)
                {
                    if (fact.Predicate == FactPredicates.Possesses
                        && fact.Object == pressure.Subject
                        && fact.Truth == TruthState.True
                        && world.Registry.IsActor(fact.Subject))
                    {
                        return fact.Subject;
                    }
                }
            }

            return EntityId.None;
        }

        private static EntityId TargetForTheft(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId actor,
            EntityId subject,
            out EntityId item)
        {
            item = EntityId.None;
            if (world.Registry.IsActor(subject))
            {
                return subject;
            }

            EntityId holder = HolderOf(vanilla, world, subject, actor);
            if (!holder.IsNone)
            {
                item = subject;
                return holder;
            }

            return EntityId.None;
        }

        private static EntityId FirstEvidenceHeldBy(IVanillaState vanilla, EntityId actor, Fact fact)
        {
            if (fact == null)
            {
                return EntityId.None;
            }

            IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(actor);
            for (int i = 0; i < inventory.Count; i++)
            {
                for (int e = 0; e < fact.EvidenceIds.Count; e++)
                {
                    if (inventory[i].Id == fact.EvidenceIds[e])
                    {
                        return inventory[i].Id;
                    }
                }
            }

            return EntityId.None;
        }

        private static EntityId HolderOf(
            IVanillaState vanilla,
            NarrativeWorldState world,
            EntityId item,
            EntityId except)
        {
            if (item.IsNone)
            {
                return EntityId.None;
            }

            List<NarrativeNpc> actors = new List<NarrativeNpc>(world.Registry.Npcs.Values);
            actors.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            for (int i = 0; i < actors.Count; i++)
            {
                EntityId who = actors[i].Id;
                if (who == except)
                {
                    continue;
                }

                IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(who);
                for (int j = 0; j < inventory.Count; j++)
                {
                    if (inventory[j].Id == item)
                    {
                        return who;
                    }
                }
            }

            return EntityId.None;
        }

        private static EntityId FirstOtherKnower(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId actor,
            EntityId factId)
        {
            List<NarrativeNpc> actors = new List<NarrativeNpc>(world.Registry.Npcs.Values);
            actors.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            for (int i = 0; i < actors.Count; i++)
            {
                EntityId who = actors[i].Id;
                if (who != actor && vanilla.IsAlive(who) && world.Knowledge.Knows(who, factId))
                {
                    return who;
                }
            }

            return EntityId.None;
        }

        private static NarrativeThread FindThread(
            NarrativeWorldState world,
            EntityId fact,
            EntityId a,
            EntityId b)
        {
            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread == null || !thread.IsLive)
                {
                    continue;
                }

                if ((!fact.IsNone && thread.FactIds.Contains(fact))
                    || (!a.IsNone && thread.ParticipantIds.Contains(a))
                    || (!b.IsNone && thread.ParticipantIds.Contains(b)))
                {
                    return thread;
                }
            }

            return null;
        }

        private static string Number(double value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed class SchemeCandidate
        {
            public SchemeCandidate(NpcGoal goal, string actionId, EntityId target)
            {
                Goal = goal;
                ActionId = actionId;
                Target = target;
                Style = InterventionStyles.For(ActionFamily.Social);
                Reason = string.Empty;
                EffectKind = string.Empty;
            }

            /// <summary>
            /// A candidate the bridge proposed (BQa-011). The approach it reads as comes from the
            /// same <see cref="InterventionStyles"/> reading of the verb's family that the other
            /// autonomy pass already uses, so the actor's existing problem-solving preferences
            /// weigh a discovered route exactly as they weigh a staged one.
            /// </summary>
            public static SchemeCandidate FromRoute(GoalRoute route, NarrativeWorldState world)
            {
                SchemeCandidate candidate = new SchemeCandidate(route.Goal, route.Action.Id, route.Target)
                {
                    EffectKind = route.EffectKind
                };

                candidate.WithFact(route.SubjectFact);
                candidate.WithItem(route.SubjectItem);
                candidate.WithBinding(route.Binding);
                candidate.WithThread(FindThread(world, route.SubjectFact, route.Goal.Subject, route.Target));
                return candidate.WithStyle(InterventionStyles.For(route.Action.Family), route.Because);
            }

            public NpcGoal Goal { get; }

            /// <summary>The effect kind that joined the want to the verb, or empty for a staged candidate.</summary>
            public string EffectKind { get; private set; }

            public string ActionId { get; }

            public EntityId Target { get; }

            public EntityId SubjectFact { get; private set; }

            public EntityId SubjectItem { get; private set; }

            public EntityId ThirdParty { get; private set; }

            public ActionBinding Binding { get; private set; }

            public NarrativeThread Thread { get; private set; }

            public ProblemSolvingStyle Style { get; private set; }

            public string Reason { get; private set; }

            public OffScreenSchemeOption Option { get; private set; }

            public SchemeCandidate WithFact(EntityId fact)
            {
                SubjectFact = fact;
                return this;
            }

            public SchemeCandidate WithItem(EntityId item)
            {
                SubjectItem = item;
                return this;
            }

            public SchemeCandidate WithBinding(ActionBinding binding)
            {
                Binding = binding;
                return this;
            }

            public SchemeCandidate WithThread(NarrativeThread thread)
            {
                Thread = thread;
                return this;
            }

            public SchemeCandidate WithStyle(ProblemSolvingStyle style, string reason)
            {
                Style = style;
                Reason = reason ?? string.Empty;
                return this;
            }

            public SchemeCandidate With(OffScreenSchemeOption option)
            {
                Option = option;
                return this;
            }
        }
    }

    public sealed class OffScreenSchemeOption
    {
        public OffScreenSchemeOption(
            EntityId actor,
            EntityId target,
            string goalKind,
            string actionId,
            ProblemSolvingStyle style,
            Availability availability,
            InterventionOpportunity opportunity,
            double score,
            IReadOnlyList<string> scoreTerms,
            string barred)
        {
            Actor = actor;
            Target = target;
            GoalKind = goalKind ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Style = style;
            Availability = availability;
            Opportunity = opportunity;
            Score = score;
            ScoreTerms = scoreTerms ?? new string[0];
            Barred = barred ?? string.Empty;
        }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        public string GoalKind { get; }

        public string ActionId { get; }

        public ProblemSolvingStyle Style { get; }

        public Availability Availability { get; }

        public InterventionOpportunity Opportunity { get; }

        public double Score { get; }

        public IReadOnlyList<string> ScoreTerms { get; }

        public string Barred { get; }

        public string GoalReason => "off-screen continuation of " + GoalKind;

        public bool Eligible(double opportunityFloor)
        {
            return Availability.IsAvailable
                   && Opportunity != null
                   && Opportunity.Plausibility >= opportunityFloor
                   && Barred.Length == 0;
        }

        public override string ToString()
        {
            return ActionId + " for " + GoalKind + " score "
                   + Score.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                   + (Availability.IsAvailable && Barred.Length == 0 ? string.Empty : " (not eligible)");
        }
    }

    public sealed class OffScreenSchemeTrace
    {
        public OffScreenSchemeTrace(EntityId actor, GameTime consideredAt)
        {
            Actor = actor;
            ConsideredAt = consideredAt;
            Goals = new List<NpcGoal>();
            Options = new List<OffScreenSchemeOption>();
            Searches = new List<GoalRouteSearch>();
        }

        public EntityId Actor { get; }

        public GameTime ConsideredAt { get; }

        public ActorActivity Activity { get; set; }

        public List<NpcGoal> Goals { get; }

        public List<OffScreenSchemeOption> Options { get; }

        /// <summary>What the bridge could offer each want it was asked about (BQa-011).</summary>
        public List<GoalRouteSearch> Searches { get; }

        public OffScreenSchemeOption Chosen { get; set; }

        public ActionAttempt Attempt { get; set; }

        public bool GoalSatisfied { get; set; }

        /// <summary>
        /// How the chosen want's condition read against authoritative state after the attempt.
        /// <see cref="GoalConditionState.Unsupported"/> when nothing could answer, which includes
        /// every want that has no machine-readable condition.
        /// </summary>
        public GoalConditionState ConditionAfter { get; set; } = GoalConditionState.Unsupported;

        /// <summary>
        /// The condition holds, and nothing that happened here told its owner so. Recorded because
        /// it is the case that must never become a satisfied goal (BQa-011).
        /// </summary>
        public bool ConditionHoldsUntaught { get; set; }

        /// <summary>Whether the chosen want said what it wanted in a way the world could answer.</summary>
        public bool WantWasReadable { get; set; }

        /// <summary>
        /// The want was closed on the attempt having worked, because it named no condition anything
        /// could be asked about. The pre-BQa-011 rule, kept only where there is nothing better.
        /// </summary>
        public bool ClosedByTheAttemptAlone { get; set; }

        public string Refusal { get; set; } = string.Empty;

        public bool Acted => Attempt != null;

        public string Describe(NarrativeWorldState world)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("actor: ").Append(world == null ? Actor.ToString() : world.Registry.NameOf(Actor));
            sb.Append(" at ").Append(ConsideredAt);
            if (Activity != null)
            {
                sb.Append("\n  vanilla activity: ").Append(Activity.Describe());
            }

            for (int i = 0; i < Goals.Count; i++)
            {
                sb.Append("\n  goal mattered: ").Append(Goals[i]);
            }

            for (int i = 0; i < Searches.Count; i++)
            {
                GoalRouteSearch search = Searches[i];
                if (!search.IsSupported)
                {
                    sb.Append("\n  no route could be looked for: ").Append(search.Unsupported);
                    continue;
                }

                sb.Append("\n  routes discovered: ").Append(search.Routes.Count);
                for (int n = 0; n < search.Notes.Count; n++)
                {
                    sb.Append("\n      ").Append(search.Notes[n]);
                }
            }

            for (int i = 0; i < Options.Count; i++)
            {
                OffScreenSchemeOption option = Options[i];
                sb.Append("\n  option ").Append(option);
                if (option.Opportunity != null)
                {
                    sb.Append("\n      ").Append(option.Opportunity.Describe().Replace("\n", "\n      "));
                }

                for (int t = 0; t < option.ScoreTerms.Count; t++)
                {
                    sb.Append("\n      ").Append(option.ScoreTerms[t]);
                }

                if (!option.Availability.IsAvailable)
                {
                    sb.Append("\n      ").Append(option.Availability);
                }

                if (option.Barred.Length > 0)
                {
                    sb.Append("\n      not asked for off screen: ").Append(option.Barred);
                }
            }

            if (!Acted)
            {
                sb.Append("\n  nobody acted: ").Append(Refusal.Length == 0 ? "no reason recorded" : Refusal);
            }
            else
            {
                sb.Append("\n  selected NarrativeAction: ").Append(Chosen?.ActionId ?? Attempt.Intent.ActionId);
                sb.Append("\n  ").Append(Attempt.Explain().Replace("\n", "\n  "));
                if (WantWasReadable)
                {
                    sb.Append("\n  the want now reads ").Append(ConditionAfter).Append(" against the world");
                }

                if (ClosedByTheAttemptAlone)
                {
                    sb.Append("\n  goal satisfied: the want named no condition to ask about, so the attempt having worked is all there is to go on");
                }
                else if (GoalSatisfied)
                {
                    sb.Append("\n  goal satisfied: the condition holds and their own successful attempt is how they know");
                }
                else if (ConditionHoldsUntaught)
                {
                    sb.Append("\n  the condition holds, but nothing here taught its owner so; the want stays open");
                }
            }

            return sb.ToString();
        }
    }
}
