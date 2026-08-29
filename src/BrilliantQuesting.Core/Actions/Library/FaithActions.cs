using System;
using System.Globalization;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// What a god asks of whoever would ask him.
    ///
    /// The faith counterpart to <see cref="ProductionSpec"/>, and it exists for the same reason: a
    /// matter states what is required of the petitioner rather than naming the one character who
    /// may act, so anybody who meets it has the route and nobody who does not. Which god, how well
    /// he has to know you, and what has to be on his ground before he will listen.
    ///
    /// Nothing here knows what Kumiromi is the god of. The situation says whose matter a blighted
    /// field is; the verb only reads who the actor follows and compares. That is what keeps the
    /// faith routes generatable rather than a table of deities and their portfolios that would go
    /// stale the first time Elin added one.
    /// </summary>
    public sealed class DevotionSpec
    {
        public DevotionSpec(string deity, int minimumPiety = 0, int minimumOffering = 0)
        {
            Deity = string.IsNullOrWhiteSpace(deity) ? string.Empty : deity.Trim();
            MinimumPiety = minimumPiety < 0 ? 0 : minimumPiety;
            MinimumOffering = minimumOffering < 0 ? 0 : minimumOffering;
        }

        /// <summary>Whose gift this is, as Elin names them. Empty is not a deity and matches nobody.</summary>
        public string Deity { get; }

        /// <summary>How well he has to know you. Zero means devotion alone is enough.</summary>
        public int MinimumPiety { get; }

        /// <summary>
        /// What has to be lying on his ground, in orens. Zero means he asks for nothing.
        ///
        /// A <see cref="FactPredicates.OfferedTo"/> fact carries the same text read from the other
        /// side: there this is not a threshold but what is actually standing on the altar, which
        /// is the number the threshold is compared against. One format, so an offering and the
        /// demand it answers can never disagree about how to write a sum.
        /// </summary>
        public int MinimumOffering { get; }

        public bool IsNamed => Deity.Length > 0;

        /// <summary>Whether somebody who follows <paramref name="worshipped"/> may ask this.</summary>
        public bool IsFollowedBy(string worshipped) => SameDeity(Deity, worshipped);

        /// <summary>
        /// Whether two names are the same god.
        ///
        /// Containment either way and case-insensitive, because what
        /// <see cref="IVanillaState.GetWorshippedDeity"/> hands back is Elin's own faith id and
        /// this project has not yet seen one on a running game. A match that survives "Kumiromi"
        /// against "godKumiromi" costs nothing; a route that silently vanishes over capitalisation
        /// would cost the whole step. Empty is nobody and matches nothing, including itself - a
        /// build that cannot report a deity must lose the route, not be handed everyone's.
        /// </summary>
        public static bool SameDeity(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0
                   || right.IndexOf(left, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The form a <see cref="FactPredicates.SacredTo"/> fact carries. Round-trips.</summary>
        public string ToFactValue()
        {
            string text = Deity;
            if (MinimumPiety > 0)
            {
                text += " piety " + MinimumPiety.ToString(CultureInfo.InvariantCulture);
            }

            if (MinimumOffering > 0)
            {
                text += " worth " + MinimumOffering.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        /// <summary>
        /// Reads a specification off a fact value, tolerantly. A value that names only a god is
        /// consecrated ground with nothing asked for, which is the ordinary case for an altar.
        /// Returns null only when there is no god at all to work with.
        /// </summary>
        public static DevotionSpec Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] words = value.Trim().Split(' ');
            return new DevotionSpec(
                words[0],
                ActionSupport.ReadNumber(words, "piety"),
                ActionSupport.ReadNumber(words, "worth"));
        }
    }

    /// <summary>
    /// Shared devotional lookups. Who a place belongs to, whose matter this is, and what the actor
    /// has already laid down - all facts in the graph rather than counters on a character sheet.
    /// </summary>
    internal static class Devotion
    {
        /// <summary>
        /// Whether the adapter can say who anybody follows at all.
        ///
        /// Read once at the top of both verbs, and the safe direction is closed: a build that
        /// cannot report devotion loses the faith routes rather than opening them to everybody,
        /// which is the same call quality zero makes on the production side.
        /// </summary>
        public static bool CanRead(ActionContext context) => context.Vanilla.Supports(VanillaCapability.ReadFaith);

        /// <summary>
        /// The god whose ground the actor is standing on, or null.
        ///
        /// Scoped to the thread's own facts, as the matter lookup below is, because it runs on
        /// every discovery pass. Consecrated ground is a fact about the *zone* rather than an
        /// altar object standing in it: the live adapter can only read a character's inventory,
        /// so a verb that went looking for an altar Thing would work headlessly and find nothing
        /// in the game.
        /// </summary>
        public static DevotionSpec GroundHere(ActionContext context)
        {
            if (context.Thread == null)
            {
                return null;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null
                    && fact.Predicate == FactPredicates.SacredTo
                    && fact.Truth == TruthState.True
                    && fact.Subject == context.Zone)
                {
                    DevotionSpec spec = DevotionSpec.Parse(fact.Value);
                    if (spec != null && spec.IsNamed)
                    {
                        return spec;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The open matter here that is in somebody's gift, with the trouble it stands for.
        ///
        /// A matter is a thing that is not working which somebody holds sacred, so it is a pair:
        /// the <see cref="FactPredicates.Damaged"/> fact that a petition would supersede, and the
        /// <see cref="FactPredicates.SacredTo"/> fact that says whose gift lifting it is. Scoped to
        /// the named fact and the thread's own facts, exactly as
        /// <see cref="ProductionDemand.Find"/> is, because this runs on every discovery pass.
        /// </summary>
        public static Fact FindMatter(ActionContext context, out Fact sacred, out DevotionSpec spec)
        {
            sacred = null;
            spec = null;
            if (context.Thread == null)
            {
                return null;
            }

            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsOpenTrouble(named) && TryReadSacred(context, named.Subject, out sacred, out spec))
                {
                    return named;
                }
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (IsOpenTrouble(fact) && TryReadSacred(context, fact.Subject, out sacred, out spec))
                {
                    return fact;
                }
            }

            sacred = null;
            spec = null;
            return null;
        }

        /// <summary>Whether this thread still holds a matter anybody could petition over.</summary>
        public static bool AnyOpenMatterIn(NarrativeThread thread, KnowledgeGraph knowledge)
        {
            if (thread == null)
            {
                return false;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact trouble = knowledge.GetFact(thread.FactIds[i]);
                if (trouble == null
                    || trouble.Predicate != FactPredicates.Damaged
                    || trouble.Truth != TruthState.True)
                {
                    continue;
                }

                for (int j = 0; j < thread.FactIds.Count; j++)
                {
                    Fact sacred = knowledge.GetFact(thread.FactIds[j]);
                    if (sacred != null
                        && sacred.Predicate == FactPredicates.SacredTo
                        && sacred.Truth == TruthState.True
                        && sacred.Subject == trouble.Subject)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// What the actor has standing on this god's ground, and the fact that says so.
        ///
        /// The one lookup here that walks the whole fact store, because an offering belongs to the
        /// giver and the god rather than to any thread, and a devout player may well have laid it
        /// down before the matter was ever mentioned to them. Both callers ask it last, after the
        /// cheap devotional preconditions have already failed for everyone it does not apply to.
        /// </summary>
        public static Fact FindOffering(ActionContext context, string deity, out int worth)
        {
            worth = 0;
            Fact found = null;
            foreach (Fact fact in context.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate != FactPredicates.OfferedTo
                    || fact.Truth != TruthState.True
                    || fact.Subject != context.Actor)
                {
                    continue;
                }

                DevotionSpec laid = DevotionSpec.Parse(fact.Value);
                if (laid == null || !DevotionSpec.SameDeity(laid.Deity, deity))
                {
                    continue;
                }

                found = fact;
                worth = laid.MinimumOffering;
                break;
            }

            return found;
        }

        /// <summary>Why this build may not ask this god, or null when it may.</summary>
        public static string DevotionalShortfall(ActionContext context, DevotionSpec spec)
        {
            string worshipped = context.Vanilla.GetWorshippedDeity(context.Actor);
            if (!spec.IsFollowedBy(worshipped))
            {
                return spec.Deity + " does not answer "
                       + (string.IsNullOrEmpty(worshipped) ? "those who follow nobody" : "a follower of " + worshipped);
            }

            int piety = context.Vanilla.GetPiety(context.Actor);
            if (piety < spec.MinimumPiety)
            {
                return spec.Deity + " does not know you well enough: your piety is " + piety
                       + " and this asks " + spec.MinimumPiety;
            }

            return null;
        }

        private static bool IsOpenTrouble(Fact fact)
        {
            return fact != null && fact.Predicate == FactPredicates.Damaged && fact.Truth == TruthState.True;
        }

        private static bool TryReadSacred(ActionContext context, EntityId subject, out Fact sacred, out DevotionSpec spec)
        {
            sacred = null;
            spec = null;
            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact == null
                    || fact.Predicate != FactPredicates.SacredTo
                    || fact.Truth != TruthState.True
                    || fact.Subject != subject)
                {
                    continue;
                }

                DevotionSpec parsed = DevotionSpec.Parse(fact.Value);
                if (parsed != null && parsed.IsNamed)
                {
                    sacred = fact;
                    spec = parsed;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Laying real goods on your god's altar.
    ///
    /// No roll. Giving a thing away is not a skill test - either it is in your pack and this is
    /// your god's ground, or it is not - so this resolves the way paying a debt and handing back a
    /// ring do. What it costs is the object, permanently, which is the whole reason the petition
    /// it pays for is a decision rather than a menu entry: a basket of first fruits laid on a
    /// shrine is a basket nobody eats, sells or is given.
    ///
    /// Offerings add up. Two small ones the god would not have heard become one he will, and the
    /// standing they make is a fact in the graph rather than a counter, so it survives a save and
    /// a petition can spend it.
    /// </summary>
    public sealed class MakeOfferingAction : NarrativeAction
    {
        public MakeOfferingAction() : base("make_offering", ActionFamily.MagicFaith, "Make an offering")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!Devotion.CanRead(context))
            {
                return Availability.Impossible("this build cannot report who anybody follows");
            }

            DevotionSpec ground = Devotion.GroundHere(context);
            if (ground == null)
            {
                return Availability.NotRelevant("there is no altar here");
            }

            string worshipped = context.Vanilla.GetWorshippedDeity(context.Actor);
            if (!ground.IsFollowedBy(worshipped))
            {
                return Availability.Impossible(
                    "this ground is " + ground.Deity + "'s, and "
                    + (string.IsNullOrEmpty(worshipped) ? "you follow nobody" : "you follow " + worshipped));
            }

            if (!context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("nothing can be given up for good on this build");
            }

            ItemDescriptor gift = ActionSupport.FindItem(context, context.Actor);
            if (gift == null)
            {
                return Availability.Impossible("you have nothing to offer");
            }

            return Availability.Available("lays " + gift.Name + " on " + ground.Deity + "'s altar");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            DevotionSpec ground = Devotion.CanRead(context) ? Devotion.GroundHere(context) : null;
            if (ground == null || !ground.IsFollowedBy(context.Vanilla.GetWorshippedDeity(context.Actor)))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here to offer to.");
                nothing.Notes.Add("no altar of the actor's own god within reach");
                return nothing;
            }

            ItemDescriptor gift = ActionSupport.FindItem(context, context.Actor);
            if (gift == null || !context.Vanilla.TryDestroyItem(gift.Id, context.Actor))
            {
                ActionOutcome empty = new ActionOutcome(Id, null, "You have nothing to lay down.");
                empty.Notes.Add(gift == null ? "nothing in the pack" : "the offering did not leave the pack");
                return empty;
            }

            Fact standing = Devotion.FindOffering(context, ground.Deity, out int already);
            int total = already + (gift.Value < 0 ? 0 : gift.Value);
            if (standing != null)
            {
                // History is append-oriented: the old standing is superseded rather than edited,
                // so the ledger can still say what was laid down and when.
                standing.Truth = TruthState.Superseded;
            }

            Fact offered = new Fact(
                context.World.NewId("fact"),
                context.Actor,
                FactPredicates.OfferedTo,
                context.Zone,
                new DevotionSpec(ground.Deity, 0, total).ToFactValue(),
                TruthState.True);
            context.World.Knowledge.AddFact(offered);
            context.World.Knowledge.Teach(context.Actor, offered.Id, KnowledgeSource.Participant, 1.0, context.Now, true);

            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You lay " + gift.Name + " on the altar. " + ground.Deity + "'s ground has it now.");
            outcome.Notes.Add("no check: an offering is a thing given, not a thing attempted");
            outcome.Notes.Add(already > 0
                ? "standing before " + ground.Deity + " is now worth " + total + ", up from " + already
                : "standing before " + ground.Deity + " is worth " + total);
            outcome.Events.Add(context.World.Record(
                WorldEventType.OfferingMade,
                context.Actor,
                EntityId.None,
                context.Now,
                0.3,
                context.Zone,
                related: new[] { offered.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            return outcome;
        }
    }

    /// <summary>
    /// Asking a god for something that is in his gift.
    ///
    /// The step's route, and the whole of it turns on where the gate sits. Which god a matter
    /// belongs to, how well he knows the asker, and what is lying on his ground decide *whether
    /// there is a route at all*; the dice decide only how the asking goes. So a follower of another
    /// god is not a worshipper with worse odds - the option is not there, in the same class as
    /// invoking guild authority without membership, and it says so in as many words.
    ///
    /// The god is the one doing the work, which is what keeps this inside the rule that standing
    /// gates contacts and never attempts: a deity you do not follow is a contact who will not deal
    /// with you, not a lock you are forbidden to pick.
    ///
    /// A petition is also not free and not repeatable at leisure. It spends what was offered, and
    /// on a botched asking the matter passes out of the god's gift altogether - the route is gone,
    /// for everybody, the way a botched repair finishes the object off.
    /// </summary>
    public sealed class InvokeBlessingAction : NarrativeAction
    {
        public InvokeBlessingAction() : base("invoke_blessing", ActionFamily.MagicFaith, "Ask for a blessing")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!Devotion.CanRead(context))
            {
                return Availability.Impossible("this build cannot report who anybody follows");
            }

            Fact trouble = Devotion.FindMatter(context, out Fact sacred, out DevotionSpec spec);
            if (trouble == null)
            {
                return Availability.NotRelevant("nothing here is in anybody's gift");
            }

            // Knowing whose matter this is has to be learned like anything else. A god's name is
            // not something the world tells the player for standing next to a problem.
            if (!context.World.Knowledge.BelievesConfidently(context.Actor, sacred.Id))
            {
                return Availability.NotRelevant("you do not know whose matter this is");
            }

            string shortfall = Devotion.DevotionalShortfall(context, spec);
            if (shortfall != null)
            {
                return Availability.Impossible(shortfall);
            }

            DevotionSpec ground = Devotion.GroundHere(context);
            if (ground == null || !ground.IsFollowedBy(spec.Deity))
            {
                return Availability.NotRelevant("there is no altar of " + spec.Deity + " here");
            }

            Devotion.FindOffering(context, spec.Deity, out int offered);
            if (offered < spec.MinimumOffering)
            {
                return Availability.Impossible(offered == 0
                    ? "you have laid nothing on " + spec.Deity + "'s ground, and this asks " + spec.MinimumOffering
                    : "you have laid " + offered + " on " + spec.Deity + "'s ground, and this asks " + spec.MinimumOffering);
            }

            return Availability.Available("asks " + spec.Deity + " to lift " + Trouble(trouble));
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact sacred = null;
            DevotionSpec spec = null;
            Fact trouble = Devotion.CanRead(context)
                ? Devotion.FindMatter(context, out sacred, out spec)
                : null;

            if (trouble == null || !context.World.Knowledge.BelievesConfidently(context.Actor, sacred.Id))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here to ask for.");
                nothing.Notes.Add("no sacred matter the actor knows of");
                return nothing;
            }

            string shortfall = Devotion.DevotionalShortfall(context, spec);
            if (shortfall != null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "The words go nowhere.");
                refused.Notes.Add(shortfall);
                return refused;
            }

            Fact offering = Devotion.FindOffering(context, spec.Deity, out int offered);
            DevotionSpec ground = Devotion.GroundHere(context);
            if (ground == null || !ground.IsFollowedBy(spec.Deity) || offered < spec.MinimumOffering)
            {
                ActionOutcome unheard = new ActionOutcome(Id, null, spec.Deity + " does not hear you.");
                unheard.Notes.Add(ground == null || !ground.IsFollowedBy(spec.Deity)
                    ? "not on " + spec.Deity + "'s ground"
                    : "offered " + offered + " against " + spec.MinimumOffering);
                return unheard;
            }

            int piety = context.Vanilla.GetPiety(context.Actor);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Devotion, context.Actor, EntityId.None);
            request.WithModifier("the size of what you are asking", spec.MinimumPiety / 5);
            request.WithModifier("how well " + spec.Deity + " knows you", -((piety - spec.MinimumPiety) / 8));
            request.WithModifier("what is lying on his ground", -((offered - spec.MinimumOffering) / 40));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            if (check.Outcome.IsSuccess())
            {
                return Answered(context, trouble, spec, offering, check);
            }

            // The offering is gone either way. A god who declines still took what was given.
            Spend(offering);
            if (check.Outcome == CheckOutcome.CriticalFail)
            {
                return Spurned(context, trouble, sacred, spec, check);
            }

            ActionOutcome failed = new ActionOutcome(Id, check,
                "Nothing answers, and " + Trouble(trouble) + " is exactly as you found it.");
            failed.Notes.Add("what was offered is spent; the matter is still in " + spec.Deity + "'s gift");
            return failed;
        }

        /// <summary>
        /// The god acts, and what he acts on is the cause rather than its symptoms.
        ///
        /// The same shape mending a broken thing has: whatever was wanted *because of* this is no
        /// longer wanted, once, instead of having to be covered again next season.
        /// </summary>
        private ActionOutcome Answered(
            ActionContext context, Fact trouble, DevotionSpec spec, Fact offering, CheckResult check)
        {
            trouble.Truth = TruthState.Superseded;
            bool generous = check.Outcome == CheckOutcome.CriticalPass;
            if (!generous)
            {
                Spend(offering);
            }

            EntityId owner = Ownership.OwnerOf(context, trouble.Subject);
            if (owner.IsNone)
            {
                owner = context.Target;
            }

            ActionOutcome outcome = new ActionOutcome(Id, check, generous
                ? spec.Deity + " answers, and leaves what you brought where it lies."
                : spec.Deity + " answers, and " + Trouble(trouble) + " is over.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                owner,
                context.Now,
                generous ? 0.9 : 0.7,
                context.Zone,
                related: new[] { trouble.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));

            if (generous)
            {
                outcome.Notes.Add("nothing spent: what was offered still stands before " + spec.Deity);
            }

            int closed = ActionSupport.CloseDemandsOn(context, trouble.Subject, outcome);
            outcome.Notes.Add(closed == 0
                ? "nobody was waiting on it"
                : closed + " demand(s) it was causing are over");

            Settle(context, outcome);
            return outcome;
        }

        /// <summary>
        /// The botch. Not a wasted turn - the matter stops being anybody's to ask about, so the
        /// route through it is gone for every worshipper, not only this one.
        /// </summary>
        private ActionOutcome Spurned(
            ActionContext context, Fact trouble, Fact sacred, DevotionSpec spec, CheckResult check)
        {
            sacred.Truth = TruthState.Superseded;

            EntityId owner = Ownership.OwnerOf(context, trouble.Subject);
            if (owner.IsNone)
            {
                owner = context.Target;
            }

            ActionOutcome outcome = new ActionOutcome(Id, check,
                "Something turns away, and " + Trouble(trouble) + " is no longer " + spec.Deity + "'s to lift.");
            outcome.Notes.Add("the matter has passed out of " + spec.Deity + "'s gift; no petition reaches it again");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Harmed,
                context.Actor,
                owner,
                context.Now,
                0.4,
                context.Zone,
                related: new[] { sacred.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            return outcome;
        }

        private static void Spend(Fact offering)
        {
            if (offering != null)
            {
                offering.Truth = TruthState.Superseded;
            }
        }

        /// <summary>
        /// The thread ends when nothing in it is still wanted and nothing in it is still anybody's
        /// to lift. Both halves, because a thread can carry a shortage and its cause at once.
        /// </summary>
        private static void Settle(ActionContext context, ActionOutcome outcome)
        {
            if (context.Thread == null
                || ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge)
                || Devotion.AnyOpenMatterIn(context.Thread, context.World.Knowledge))
            {
                return;
            }

            context.Thread.State = ThreadState.Resolved;
            context.Thread.Resolution = "blessing_granted";
            outcome.Notes.Add("thread resolved: blessing_granted");
        }

        private static string Trouble(Fact trouble)
        {
            return string.IsNullOrEmpty(trouble.Value) ? "what is wrong here" : trouble.Value;
        }
    }
}
