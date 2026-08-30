using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// What a household undertakes when it takes somebody in, written into the fact that records
    /// it. The value is the difference between the four person-verbs; everything else about them
    /// is the same act.
    /// </summary>
    public static class Undertakings
    {
        /// <summary>A bed, indefinitely. The person lives here now.</summary>
        public const string Resident = "resident";

        /// <summary>A roof for as long as it is wanted, and no claim on the settlement.</summary>
        public const string Guest = "guest";

        /// <summary>A bed, and a trade the settlement did not have.</summary>
        public const string Specialist = "specialist";

        /// <summary>No bed. Somebody of this household stands between them and what is after them.</summary>
        public const string Watched = "watched";

        /// <summary>Escalation id for a resident undertaking that leaks in an unsafe Home.</summary>
        public const string ResidentDiscoveredStep = "resident_discovered";
    }

    /// <summary>
    /// Shared lookups for the Home/Community family. No rules live here - each verb decides what
    /// it will and will not do - but every one of them asks these same four questions.
    /// </summary>
    internal static class Household
    {
        /// <summary>
        /// A watch cannot be kept in a place that cannot keep itself. Public Safety is Elin's own
        /// number for that, and an unread one refuses: promising protection a slum could not give
        /// is worse than declining to promise it (decision D017).
        /// </summary>
        public const int SafetyForAWatch = 10;

        /// <summary>
        /// Below this, taking somebody in still gives them a bed but does not make the Home quiet
        /// enough to end the matter cleanly. This reads Elin's Public Safety instead of inventing
        /// a stealth/sanctuary stat: a rough settlement leaks where people are hiding.
        /// </summary>
        public const int SafetyForQuietSanctuary = 25;

        /// <summary>Below this a settlement is feeding itself, not anybody else.</summary>
        public const int SupplyToSpare = 10;

        /// <summary>
        /// The open claim that this person is not safe, if the actor has actually heard it.
        ///
        /// Knowing is required on purpose. A situation may put somebody in danger off-screen, and
        /// a player who has not been told must not be offered a sanctuary for a plight they have
        /// no way of knowing about - that is the omniscience rule, applied to an offer rather than
        /// to a dialogue line.
        /// </summary>
        public static Fact FindExposure(ActionContext context, EntityId person)
        {
            if (person.IsNone)
            {
                return null;
            }

            List<EntityId> candidates = new List<EntityId>();
            if (!context.SubjectFact.IsNone)
            {
                candidates.Add(context.SubjectFact);
            }

            if (context.Thread != null)
            {
                candidates.AddRange(context.Thread.FactIds);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(candidates[i]);
                if (fact != null
                    && fact.Predicate == FactPredicates.AtRisk
                    && fact.Truth == TruthState.True
                    && fact.Subject == person
                    && context.World.Knowledge.Knows(context.Actor, fact.Id))
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>The standing undertaking this household has already made about somebody, or null.</summary>
        public static Fact FindUndertaking(ActionContext context, EntityId person)
        {
            Fact fact = context.World.Knowledge.FindFact(person, FactPredicates.ShelteredBy);
            return fact != null && fact.Truth == TruthState.True && fact.Object == context.Actor ? fact : null;
        }

        /// <summary>
        /// Somebody who lives here and is neither of the two people this is about.
        ///
        /// The reason every Home verb that needs hands asks for one: a settlement is its people,
        /// and an empty Home can shelter but cannot watch, haul or hold anything.
        /// </summary>
        public static HomeResident FindResident(HomeState home, EntityId excludeA, EntityId excludeB)
        {
            if (home == null)
            {
                return null;
            }

            for (int i = 0; i < home.Residents.Count; i++)
            {
                HomeResident resident = home.Residents[i];
                if (resident.Id != excludeA && resident.Id != excludeB)
                {
                    return resident;
                }
            }

            return null;
        }

        /// <summary>Where the Home is, falling back to where the actor is when the zone was unread.</summary>
        public static EntityId PlaceOf(ActionContext context, HomeState home)
        {
            return home != null && !home.ZoneId.IsNone ? home.ZoneId : context.Zone;
        }
    }

    /// <summary>
    /// Taking a person into the player's household.
    ///
    /// Four verbs, one act. A household accepts an exposure it did not have before, and what
    /// separates them is what was undertaken and what it costs: a bed and a place on the roll, a
    /// night by the fire, a trade the settlement lacked, or somebody standing a watch. Writing
    /// them as one class is deliberate - four near-identical implementations would drift, and the
    /// interesting differences (who is eligible, whether Elin's own resident roll changes, whether
    /// the danger is actually answered) are exactly the three things the subclasses state.
    ///
    /// What is *not* written here is any of Elin's settlement arithmetic. Admitting somebody moves
    /// the resident roll through the game's own call and stops; Food Supply, Public Safety and the
    /// rest are vanilla's to recompute from who lives there, and a mod that set them directly
    /// would be a second settlement economy disagreeing with the one on the player's Home board
    /// (decision D018).
    /// </summary>
    public abstract class HomeTakeInAction : NarrativeAction
    {
        protected HomeTakeInAction(string id, string label, CheckProfile profile, string undertaking)
            : base(id, ActionFamily.HomeCommunity, label)
        {
            Profile = profile;
            Undertaking = undertaking;
        }

        protected CheckProfile Profile { get; }

        /// <summary>What the household is promising, recorded in the fact this writes.</summary>
        protected string Undertaking { get; }

        /// <summary>Whether this spends one of the Home's beds, and so writes Elin's resident roll.</summary>
        protected virtual bool SpendsABed => false;

        /// <summary>
        /// Whether the danger is actually over afterwards. A bed under your roof and a watch on
        /// the door both end an exposure; a night as a guest does not, and saying it did would
        /// make hosting a free answer to being hunted.
        /// </summary>
        protected virtual bool AnswersTheDanger => SpendsABed;

        /// <summary>Whether this verb applies to this person at all, beyond the shared checks.</summary>
        protected abstract Availability Eligible(ActionContext context, HomeState home);

        public override Availability GetAvailability(ActionContext context)
        {
            HomeState home = context.Vanilla.GetHomeState();
            if (home == null)
            {
                return Availability.Impossible("you have no home to take anybody into");
            }

            if (!ActionSupport.Present(context, context.Target)
                || context.Target == context.Actor
                || context.World.Registry.GetNpc(context.Target) == null)
            {
                return Availability.NotRelevant("there is nobody here to take in");
            }

            if (Household.FindUndertaking(context, context.Target) != null)
            {
                return Availability.NotRelevant(context.NameOf(context.Target) + " is already under your roof");
            }

            if (SpendsABed)
            {
                if (!context.Vanilla.Supports(VanillaCapability.WriteHomeResidents))
                {
                    return Availability.Impossible("nobody can be moved into a home on this build");
                }

                // Moving somebody onto the settlement roll is a permanent relocation, and the
                // mutation policy is what decides whether this person may be relocated at all.
                // Asking here rather than only at the seam is the difference between an offer that
                // is absent and one that is made and then refused: the game will never move a
                // story-critical NPC, nor anybody this build cannot classify, so the route is
                // impossible rather than unlikely. Hosting and standing a watch are untouched -
                // they move nobody.
                if (!context.Vanilla.MayMutate(context.Target, MutationKind.Relocate))
                {
                    return Availability.Impossible(context.NameOf(context.Target)
                                                   + " is not somebody this world will let you move house");
                }

                if (home.IsResident(context.Target))
                {
                    return Availability.NotRelevant(context.NameOf(context.Target) + " already lives here");
                }

                if (home.FreeCapacity <= 0)
                {
                    return Availability.Impossible(home.CapacityKnown
                        ? "your home is full"
                        : "this build will not say whether your home has room");
                }
            }

            return Eligible(context, home);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Availability availability = GetAvailability(context);
            if (!availability.IsAvailable)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is nobody here you can do that for.");
                refused.Notes.Add(availability.Reason);
                return refused;
            }

            HomeState home = context.Vanilla.GetHomeState();
            Fact exposure = Household.FindExposure(context, context.Target);

            CheckRequest request = new CheckRequest(Profile, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context));
            Prepare(context, home, request);
            CheckResult check = context.Checks.Resolve(request, context.Rng);

            if (!check.Outcome.IsSuccess())
            {
                return Declined(context, exposure, check);
            }

            if (SpendsABed && !context.Vanilla.TryAdmitResident(context.Target))
            {
                // The game refused. Nothing is recorded, because a `sheltered_by` fact written
                // over a settlement that never took anybody would be exactly the stale binding
                // the evidence rules exist to prevent.
                ActionOutcome unmoved = new ActionOutcome(Id, check,
                    context.NameOf(context.Target) + " does not end up living here after all.");
                unmoved.Notes.Add("the game refused to move " + context.Target + " into the home");
                return unmoved;
            }

            return Accepted(context, home, exposure, check);
        }

        /// <summary>Adds whatever this verb reads off the settlement to the roll. Nothing, by default.</summary>
        protected virtual void Prepare(ActionContext context, HomeState home, CheckRequest request)
        {
        }

        protected abstract string NarrateAcceptance(ActionContext context);

        /// <summary>
        /// The offer was made and did not land.
        ///
        /// A critical failure is what makes it a decision rather than a free attempt: the offer
        /// was made in front of people, and what they take away from it is that this person has
        /// something to be afraid of. The exposure stands, and now more of the town knows about it.
        /// </summary>
        private ActionOutcome Declined(ActionContext context, Fact exposure, CheckResult check)
        {
            ActionOutcome outcome = new ActionOutcome(Id, check,
                context.NameOf(context.Target) + " will not have it.");

            if (check.Outcome == CheckOutcome.CriticalFail && exposure != null)
            {
                outcome.Events.Add(context.World.Record(
                    WorldEventType.Conversed,
                    context.Actor,
                    context.Target,
                    context.Now,
                    0.3,
                    context.Zone,
                    related: new[] { exposure.Id },
                    witnesses: ActionSupport.Bystanders(context, true),
                    threadId: context.Thread?.Id ?? EntityId.None));
                outcome.Notes.Add("the offer was public, and so is what it said about "
                                  + context.NameOf(context.Target));
            }
            else
            {
                outcome.Notes.Add("nothing was undertaken and nothing was recorded");
            }

            return outcome;
        }

        private ActionOutcome Accepted(ActionContext context, HomeState home, Fact exposure, CheckResult check)
        {
            EntityId place = Household.PlaceOf(context, home);

            Fact undertaking = new Fact(
                context.World.NewId("fact"),
                context.Target,
                FactPredicates.ShelteredBy,
                context.Actor,
                Undertaking,
                TruthState.True);
            context.World.Knowledge.AddFact(undertaking);
            context.World.Knowledge.Teach(context.Target, undertaking.Id, KnowledgeSource.Participant, 1.0, context.Now, true);
            context.World.Knowledge.Teach(context.Actor, undertaking.Id, KnowledgeSource.Participant, 1.0, context.Now, true);

            ActionOutcome outcome = new ActionOutcome(Id, check, NarrateAcceptance(context));
            outcome.Events.Add(context.World.Record(
                WorldEventType.TakenIn,
                context.Actor,
                context.Target,
                context.Now,
                check.Outcome == CheckOutcome.CriticalPass ? 0.9 : 0.7,
                place,
                related: new[] { undertaking.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));

            if (SpendsABed)
            {
                HomeState after = context.Vanilla.GetHomeState();
                outcome.Notes.Add("the home now holds " + (after == null ? "?" : after.ResidentCount.ToString())
                                  + " resident(s), room for "
                                  + (after == null || !after.CapacityKnown ? "?" : after.FreeCapacity.ToString())
                                  + " more");
            }

            if (exposure != null && AnswersTheDanger)
            {
                exposure.Truth = TruthState.Superseded;
                outcome.Notes.Add(context.NameOf(context.Target) + " is no longer exposed");

                if (KeepsDiscoveryRisk(context, home, outcome))
                {
                    EnsureSanctuaryDiscoveryStep(context.Thread);
                }
                else
                {
                    ResolveIfNothingIsStillExposed(context, outcome);
                }
            }
            else if (exposure != null)
            {
                outcome.Notes.Add("a roof for tonight is not safety: " + context.NameOf(context.Target)
                                  + " is still exposed");
            }

            return outcome;
        }

        private bool KeepsDiscoveryRisk(ActionContext context, HomeState home, ActionOutcome outcome)
        {
            if (context.Thread == null || Undertaking != Undertakings.Resident)
            {
                return false;
            }

            if (!home.TryGetMetric(HomeMetric.Safety, out int safety))
            {
                outcome.Notes.Add("the danger is answered, but this build will not say whether the home can keep quiet");
                return true;
            }

            if (safety >= Household.SafetyForQuietSanctuary)
            {
                return false;
            }

            outcome.Notes.Add("Public Safety " + safety + " leaves a chance that word reaches whoever was hunting them");
            return true;
        }

        private static void EnsureSanctuaryDiscoveryStep(NarrativeThread thread)
        {
            if (thread == null)
            {
                return;
            }

            for (int i = 0; i < thread.Escalation.Count; i++)
            {
                if (thread.Escalation[i].Id == Undertakings.ResidentDiscoveredStep)
                {
                    return;
                }
            }

            thread.Escalation.Add(new EscalationStep(
                Undertakings.ResidentDiscoveredStep,
                4,
                "Word reaches the person looking for them."));
        }

        /// <summary>
        /// Closes the thread when nobody it is about is in danger any more. Asked of the thread's
        /// own facts rather than of the whole store, exactly as the other families ask it.
        /// </summary>
        private static void ResolveIfNothingIsStillExposed(ActionContext context, ActionOutcome outcome)
        {
            if (context.Thread == null)
            {
                return;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.AtRisk && fact.Truth == TruthState.True)
                {
                    return;
                }
            }

            ActionSupport.Resolve(context, outcome, "sheltered", 0.7);
        }
    }

    /// <summary>
    /// A bed for somebody with nowhere safe to be.
    ///
    /// The route the whole family is measured by: it spends one of Elin's own beds, moves the
    /// settlement's resident roll through the game's own call, and ends the danger. It refuses on
    /// room rather than on odds - a full home cannot take another person however persuasive the
    /// player is, and a build that will not say how much room there is refuses for the same
    /// reason a threshold refuses an unread number.
    /// </summary>
    public sealed class ShelterAction : HomeTakeInAction
    {
        public ShelterAction() : base("shelter", "Take them in", ProceduralCheckProfiles.Hospitality, Undertakings.Resident)
        {
        }

        protected override bool SpendsABed => true;

        protected override Availability Eligible(ActionContext context, HomeState home)
        {
            return Household.FindExposure(context, context.Target) == null
                ? Availability.NotRelevant("nobody here has told you they have nowhere to go")
                : Availability.Available("there is room for one more");
        }

        protected override string NarrateAcceptance(ActionContext context)
        {
            return context.NameOf(context.Target) + " comes home with you, and stays.";
        }
    }

    /// <summary>
    /// A trade the settlement did not have.
    ///
    /// The same act as sheltering - a bed, and a name on Elin's resident roll - reached from the
    /// other direction: this person is worth having rather than in need of having somewhere. What
    /// they then do for the place is the game's arithmetic over residents and jobs, which is why
    /// nothing here sets one.
    /// </summary>
    public sealed class RecruitSpecialistAction : HomeTakeInAction
    {
        public RecruitSpecialistAction()
            : base("recruit_specialist", "Offer them a place here", ProceduralCheckProfiles.Hospitality, Undertakings.Specialist)
        {
        }

        protected override bool SpendsABed => true;

        protected override Availability Eligible(ActionContext context, HomeState home)
        {
            NarrativeNpc npc = context.World.Registry.GetNpc(context.Target);
            string trade = npc?.Occupation ?? string.Empty;
            if (trade.Length == 0)
            {
                return Availability.NotRelevant("they have no trade to bring");
            }

            string[] words = { trade };
            for (int i = 0; i < home.Residents.Count; i++)
            {
                // An unread job is not somebody doing nothing, so it cannot rule the offer out.
                // The permissive direction is the safe one here: a settlement with two smiths is
                // a small waste, and a route closed by a job nobody could read is a missing family.
                if (home.Residents[i].HasJob && ActionSupport.LooksLike(home.Residents[i].Job, words))
                {
                    return Availability.NotRelevant("somebody here already does that");
                }
            }

            return Availability.Available("nobody here is a " + trade);
        }

        protected override string NarrateAcceptance(ActionContext context)
        {
            NarrativeNpc npc = context.World.Registry.GetNpc(context.Target);
            string trade = npc == null || npc.Occupation.Length == 0 ? "their trade" : "their work as a " + npc.Occupation;
            return context.NameOf(context.Target) + " brings " + trade + " to the settlement.";
        }
    }

    /// <summary>
    /// A roof for as long as it is wanted, and nothing more.
    ///
    /// The route that survives a full house: it spends no bed, needs no capacity and needs no
    /// write into the settlement, so it is available on the build and the save where sheltering is
    /// not. It buys presence, not safety - somebody who is being hunted is still being hunted in
    /// the morning - which is the whole reason it is not simply a cheaper `shelter`.
    /// </summary>
    public sealed class HostAction : HomeTakeInAction
    {
        public HostAction() : base("host", "Put them up", ProceduralCheckProfiles.Hospitality, Undertakings.Guest)
        {
        }

        protected override Availability Eligible(ActionContext context, HomeState home)
        {
            if (Household.FindExposure(context, context.Target) != null)
            {
                return Availability.Available("a roof for tonight, at least");
            }

            if (context.Thread != null && context.Thread.ParticipantIds.Contains(context.Target))
            {
                return Availability.Available("somewhere to talk that is yours");
            }

            return Availability.NotRelevant("there is no reason to have them at your table");
        }

        protected override string NarrateAcceptance(ActionContext context)
        {
            return context.NameOf(context.Target) + " takes a place by your fire.";
        }
    }

    /// <summary>
    /// Somebody of this household stands between a person and what is after them.
    ///
    /// The answer for a full home, and the one that spends people rather than beds. What decides
    /// whether it can be offered at all is Elin's own Public Safety: a settlement that cannot keep
    /// itself cannot keep anybody else, and one whose safety this build never read cannot promise
    /// it either.
    /// </summary>
    public sealed class AssignProtectionAction : HomeTakeInAction
    {
        public AssignProtectionAction()
            : base("assign_protection", "Put somebody on them", ProceduralCheckProfiles.Vigilance, Undertakings.Watched)
        {
        }

        protected override bool AnswersTheDanger => true;

        protected override Availability Eligible(ActionContext context, HomeState home)
        {
            if (Household.FindExposure(context, context.Target) == null)
            {
                return Availability.NotRelevant("nobody here needs watching over");
            }

            if (Household.FindResident(home, context.Target, context.Actor) == null)
            {
                return Availability.Impossible("there is nobody at your home to stand a watch");
            }

            if (!home.TryGetMetric(HomeMetric.Safety, out int safety))
            {
                return Availability.Impossible("this build will not say how safe your home is");
            }

            return safety < Household.SafetyForAWatch
                ? Availability.Impossible("your home cannot keep itself safe, let alone anybody else")
                : Availability.Available("there are people here who can watch for them");
        }

        protected override void Prepare(ActionContext context, HomeState home, CheckRequest request)
        {
            request.With(SituationalModifiers.Settlement(home, HomeMetric.Safety));
        }

        protected override string NarrateAcceptance(ActionContext context)
        {
            return "Somebody of yours will be where " + context.NameOf(context.Target) + " is, from now on.";
        }
    }

    /// <summary>
    /// Answering a shortage out of the settlement's own stores.
    ///
    /// The Home's route to the same demand the crafts answer, and deliberately not a cheaper
    /// version of them: it makes nothing, needs no stock in the pack and no craft skill, and it
    /// costs what a settlement route should cost - it can only be offered by a place that actually
    /// has a surplus and people to move it. Elin's own Food Supply and Administration decide that,
    /// read rather than written, and an element this build could not read refuses the route rather
    /// than assuming a bare larder is a full one.
    /// </summary>
    public sealed class ProvideSuppliesAction : NarrativeAction
    {
        public ProvideSuppliesAction()
            : base("provide_supplies", ActionFamily.HomeCommunity, "Send supplies from home")
        {
        }

        /// <summary>
        /// Which of the settlement's numbers answers a demand. Food comes out of the food supply;
        /// everything else comes out of how well the place is run, because a settlement that can
        /// find timber, cloth or medicine at short notice is a settlement with an administration.
        /// </summary>
        internal static HomeMetric MetricFor(ProductionSpec spec)
        {
            string[] edible = { "food", "meal", "bread", "ration", "drink", "ale", "crop", "grain" };
            return spec != null && ActionSupport.LooksLike(spec.CategoryTag, edible)
                ? HomeMetric.Food
                : HomeMetric.Administration;
        }

        public override Availability GetAvailability(ActionContext context)
        {
            HomeState home = context.Vanilla.GetHomeState();
            if (home == null)
            {
                return Availability.Impossible("you have no home to send anything from");
            }

            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here is short of anything");
            }

            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null)
            {
                return Availability.NotRelevant("nobody here has asked for anything");
            }

            if (Household.FindResident(home, context.Target, context.Actor) == null)
            {
                return Availability.Impossible("there is nobody at your home to send anything with");
            }

            HomeMetric metric = MetricFor(spec);
            string named = metric.ToString().ToLowerInvariant();
            if (!home.TryGetMetric(metric, out int level))
            {
                return Availability.Impossible("this build will not say what your home's " + named + " is");
            }

            return level < Household.SupplyToSpare
                ? Availability.Impossible("your home has no " + named + " to spare")
                : Availability.Available("your settlement can find " + spec.Describe());
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Availability availability = GetAvailability(context);
            if (!availability.IsAvailable)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is nothing you can send.");
                refused.Notes.Add(availability.Reason);
                return refused;
            }

            HomeState home = context.Vanilla.GetHomeState();
            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Logistics, context.Actor, EntityId.None)
                .With(SituationalModifiers.Settlement(home, HomeMetric.Administration));
            request.WithModifier("the standard they set", spec.MinimumQuality / 4);
            request.WithModifier("and what it must be worth", spec.MinimumValue / 200);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            if (check.Outcome.IsSuccess())
            {
                return Delivered(context, demand, spec, check);
            }

            ActionOutcome failed = new ActionOutcome(Id, check, check.Outcome == CheckOutcome.CriticalFail
                ? "Nothing comes. " + context.NameOf(context.Target) + " waited on your word for it."
                : "What you can spare does not reach them in time.");
            failed.Notes.Add("the demand still stands");

            if (check.Outcome == CheckOutcome.CriticalFail)
            {
                failed.Events.Add(context.World.Record(
                    WorldEventType.PromiseBroken,
                    context.Actor,
                    context.Target,
                    context.Now,
                    0.6,
                    context.Zone,
                    related: new[] { demand.Id },
                    witnesses: ActionSupport.Bystanders(context, true),
                    threadId: context.Thread?.Id ?? EntityId.None));
            }

            return failed;
        }

        private ActionOutcome Delivered(ActionContext context, Fact demand, ProductionSpec spec, CheckResult check)
        {
            demand.Truth = TruthState.Superseded;

            ActionOutcome outcome = new ActionOutcome(Id, check,
                "A load comes out of your settlement, and " + context.NameOf(context.Target) + " has "
                + spec.Describe() + ".");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                context.Target,
                context.Now,
                check.Outcome == CheckOutcome.CriticalPass ? 0.9 : 0.7,
                context.Zone,
                related: new[] { demand.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome.Notes.Add(context.NameOf(context.Target) + " is no longer short of " + spec.Describe());

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                ActionSupport.Resolve(context, outcome, "supplied", 0.7);
            }

            return outcome;
        }
    }

    /// <summary>
    /// Putting the thing that proves something into your own household's keeping.
    ///
    /// Not a skill test and not a roll: either there is a home with somebody in it to hold the
    /// object, or there is not. What it buys is that the object leaves the actor's pack, which is
    /// the one place `destroy_evidence` can reach - so a case that rests on a ledger stops being a
    /// case that can be lost by being robbed, threatened or talked into burning it. The proof
    /// itself is untouched, because moving a thing is not unmaking it (decision D013).
    ///
    /// It refuses to hand the object to the person it incriminates, which is the only rule in it
    /// and the only one it needs.
    /// </summary>
    public sealed class StoreEvidenceAction : NarrativeAction
    {
        public StoreEvidenceAction()
            : base("store_evidence", ActionFamily.HomeCommunity, "Put it somewhere safe")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("items cannot be moved on this build");
            }

            HomeState home = context.Vanilla.GetHomeState();
            if (home == null)
            {
                return Availability.Impossible("you have no home to keep anything at");
            }

            ItemDescriptor proof = FindProof(context);
            if (proof == null)
            {
                return Availability.NotRelevant("you are carrying nothing that proves anything");
            }

            return Custodian(context, home) == null
                ? Availability.Impossible("there is nobody at your home to leave it with")
                : Availability.Available("somebody at home can keep " + proof.Name);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Availability availability = GetAvailability(context);
            if (!availability.IsAvailable)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is nothing to put away.");
                refused.Notes.Add(availability.Reason);
                return refused;
            }

            HomeState home = context.Vanilla.GetHomeState();
            ItemDescriptor proof = FindProof(context);
            HomeResident custodian = Custodian(context, home);

            if (!context.Vanilla.TryTransferItem(proof.Id, context.Actor, custodian.Id))
            {
                ActionOutcome unmoved = new ActionOutcome(Id, null, "The " + proof.Name + " stays where it is.");
                unmoved.Notes.Add("hand-over refused: " + proof.Id + " did not reach " + custodian.Id);
                return unmoved;
            }

            ActionOutcome outcome = new ActionOutcome(Id, null,
                "The " + proof.Name + " goes home with somebody who will keep it.");
            outcome.Notes.Add("no check: keeping a thing in your own house is not a skill");
            outcome.Notes.Add(proof.Name + " is out of reach of anyone who would get rid of it");

            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            outcome.Events.Add(context.World.Record(
                WorldEventType.TakenIn,
                context.Actor,
                EntityId.None,
                context.Now,
                0.4,
                Household.PlaceOf(context, home),
                witnesses: seen,
                evidence: new[] { proof.Id },
                // Nobody's opinion of anybody moves for a thing quietly put away, and a household
                // whose name travels for it is a household that was seen doing it.
                tags: seen.Count == 0 ? new[] { EventTags.Unnoticed } : null,
                threadId: context.Thread?.Id ?? EntityId.None));

            return outcome;
        }

        /// <summary>
        /// The first thing in the pack that substantiates something, asked of the fact store once
        /// rather than once per object - the same reading, and the same cost,
        /// <see cref="DestroyEvidenceAction"/> uses to find what it would burn.
        /// </summary>
        private static ItemDescriptor FindProof(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            Dictionary<EntityId, int> order = new Dictionary<EntityId, int>();
            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i] != null && !order.ContainsKey(carried[i].Id))
                {
                    order[carried[i].Id] = i;
                }
            }

            int best = int.MaxValue;
            foreach (Fact evidenced in context.World.Knowledge.FactsEvidencedBy(order.Keys))
            {
                for (int i = 0; i < evidenced.EvidenceIds.Count; i++)
                {
                    if (!order.TryGetValue(evidenced.EvidenceIds[i], out int index) || index >= best)
                    {
                        continue;
                    }

                    if (context.SubjectItem.IsNone || evidenced.EvidenceIds[i] == context.SubjectItem)
                    {
                        best = index;
                    }
                }
            }

            return best == int.MaxValue ? null : carried[best];
        }

        /// <summary>
        /// Somebody at home who is not the actor and not whoever this evidence is about. Leaving
        /// the proof of a theft with the thief is not safekeeping.
        /// </summary>
        private static HomeResident Custodian(ActionContext context, HomeState home)
        {
            return Household.FindResident(home, context.Actor, context.Target);
        }
    }
}
