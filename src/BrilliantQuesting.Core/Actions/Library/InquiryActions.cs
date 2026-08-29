using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Shared handling of where somebody is, for the verbs that answer that.</summary>
    internal static class Whereabouts
    {
        /// <summary>
        /// The live `located_at` claim about somebody, ignoring versions the world has moved past.
        /// </summary>
        public static Fact Current(NarrativeWorldState world, EntityId subject)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Subject == subject
                    && fact.Predicate == FactPredicates.LocatedAt
                    && fact.Truth == TruthState.True)
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>
        /// Records where somebody is now, superseding the last place the world said they were.
        ///
        /// A location is the one fact that goes stale by itself, so it is never edited in place:
        /// the old claim is marked superseded and a new one is minted beside it. Anybody still
        /// believing the old one is simply out of date, which is the correct state for them to be
        /// in and one that overwriting would erase.
        /// </summary>
        public static Fact Record(NarrativeWorldState world, EntityId subject, EntityId zone, string zoneName)
        {
            Fact existing = Current(world, subject);
            if (existing != null && existing.Object == zone)
            {
                return existing;
            }

            if (existing != null)
            {
                existing.Truth = TruthState.Superseded;
            }

            Fact placed = new Fact(world.NewId("fact"), subject, FactPredicates.LocatedAt, zone, zoneName);
            world.Knowledge.AddFact(placed);
            return placed;
        }
    }

    /// <summary>
    /// Read a place for what happened in it and where whoever did it went.
    ///
    /// The entry point of an evidence-only route. It needs no fact handed to it and nobody willing
    /// to talk: it asks the ledger what happened here, notices that whoever was involved is no
    /// longer here, and turns that into somewhere to go next.
    ///
    /// That property is shared by every verb in this file, and it is what the whole step turns on.
    /// Where the examination verbs need an object in hand, these need only that the actor is
    /// somewhere and paying attention - so none of them has to be handed the fact it is chasing,
    /// and an investigator who has to be handed one has been told, however the surface dresses
    /// it up.
    /// </summary>
    public sealed class TrackAction : NarrativeAction
    {
        /// <summary>How long a trail stays readable. Older than this and the place has moved on.</summary>
        public const int TrailDays = 3;

        public TrackAction() : base("track", ActionFamily.Information, "Read the ground")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Zone.IsNone)
            {
                return Availability.NotRelevant("you are nowhere with ground worth reading");
            }

            return FindQuarry(context).IsNone
                ? Availability.NotRelevant("nothing here left a trail you could follow")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId quarry = FindQuarry(context);
            if (quarry.IsNone)
            {
                ActionOutcome cold = new ActionOutcome(Id, null, "The ground here has nothing left to say.");
                cold.Notes.Add("no readable trail in " + context.Zone);
                return cold;
            }

            CheckResult check = context.Checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Tracking, context.Actor, EntityId.None),
                context.Rng);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    EntityId where = context.Vanilla.GetZoneOf(quarry);
                    if (where.IsNone)
                    {
                        ActionOutcome lost = new ActionOutcome(Id, check, "The trail runs out somewhere you cannot follow.");
                        lost.Notes.Add("the world has no current location for " + context.NameOf(quarry));
                        return lost;
                    }

                    Fact placed = Whereabouts.Record(context.World, quarry, where, context.NameOf(where));

                    // Tracks are not something you can pick up and show a guard.
                    context.World.Knowledge.Teach(
                        context.Actor,
                        placed.Id,
                        KnowledgeSource.Inference,
                        check.Outcome == CheckOutcome.CriticalPass ? 0.95 : 0.8,
                        context.Now,
                        false);

                    ActionOutcome outcome = new ActionOutcome(Id, check, "The ground says " + context.NameOf(quarry) + " was here, and where they went.");
                    outcome.Notes.Add("learned: " + ActionSupport.Describe(context, placed.Id) + " (unprovable)");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.SecretLearned,
                        context.Actor,
                        quarry,
                        context.Now,
                        0.3,
                        context.Zone,
                        new[] { placed.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    return outcome;
                }

                case CheckOutcome.Fail:
                {
                    ActionOutcome missed = new ActionOutcome(Id, check, "Too many people have been through here since.");
                    missed.Notes.Add("no trail read; the ground is unchanged and can be read again");
                    return missed;
                }

                default:
                {
                    ActionOutcome caught = new ActionOutcome(Id, check, "You cast about so long that somebody wants to know what you are doing here.");
                    caught.Events.Add(context.World.Record(
                        WorldEventType.Trespass,
                        context.Actor,
                        quarry,
                        context.Now,
                        0.35,
                        context.Zone,
                        witnesses: ActionSupport.Bystanders(context, true),
                        threadId: context.Thread?.Id ?? EntityId.None));
                    return caught;
                }
            }
        }

        /// <summary>
        /// Somebody who did something here recently and is not here now.
        ///
        /// Deliberately read off the ledger rather than off anybody's beliefs. What a place shows
        /// is a property of what happened in it, not of who has been told about it, and going
        /// through beliefs would make tracking depend on somebody having talked.
        /// </summary>
        private static EntityId FindQuarry(ActionContext context)
        {
            // Newest first, stopping the moment the window closes. The ledger is the whole history
            // of the world and this runs every time the game asks what can be attempted here.
            IReadOnlyList<WorldEvent> history = context.World.Ledger.Events;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                WorldEvent past = history[i];
                if (context.Now.DaysSince(past.Time) > TrailDays)
                {
                    break;
                }

                if (past.Zone != context.Zone
                    || past.Actor.IsNone
                    || past.Actor == context.Actor
                    || context.World.Registry.GetNpc(past.Actor) == null
                    || !context.Vanilla.IsAlive(past.Actor)
                    || context.Vanilla.GetZoneOf(past.Actor) == context.Zone)
                {
                    continue;
                }

                Fact known = Whereabouts.Current(context.World, past.Actor);
                if (known != null
                    && known.Object == context.Vanilla.GetZoneOf(past.Actor)
                    && context.World.Knowledge.Knows(context.Actor, known.Id))
                {
                    continue;
                }

                return past.Actor;
            }

            return EntityId.None;
        }
    }

    /// <summary>
    /// Stay with somebody and see where they go.
    ///
    /// The one investigation verb the other side gets a say in, which is why it is the one that
    /// can go badly: a tail that is noticed tells the quarry exactly what the player is doing.
    /// </summary>
    public sealed class FollowAction : NarrativeAction
    {
        public FollowAction() : base("follow", ActionFamily.Information, "Follow them")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target) || context.Target == context.Actor)
            {
                return Availability.NotRelevant("nobody here to follow");
            }

            if (context.Vanilla.GetZoneOf(context.Target) != context.Zone)
            {
                return Availability.NotRelevant("they are not here to be followed");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            CheckResult check = context.Checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Shadowing, context.Actor, context.Target),
                context.Rng);

            string who = context.NameOf(context.Target);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                {
                    ActionOutcome outcome = Placed(context, check, "You stay with " + who + " the whole way.");
                    Fact caught = FirstHandFact(context);
                    if (caught != null)
                    {
                        // Seeing somebody do a thing is the one route to proof that needs no
                        // object at all: the witness is the player.
                        context.World.Knowledge.Teach(context.Actor, caught.Id, KnowledgeSource.Witnessed, 1.0, context.Now, true);
                        outcome.Notes.Add("saw it for yourself: " + ActionSupport.Describe(context, caught.Id) + " (provable)");
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.SecretLearned,
                            context.Actor,
                            caught.Subject,
                            context.Now,
                            0.5,
                            context.Zone,
                            new[] { caught.Id },
                            threadId: context.Thread?.Id ?? EntityId.None));
                    }

                    return outcome;
                }

                case CheckOutcome.Pass:
                    return Placed(context, check, "You keep " + who + " in sight long enough to see where they settle.");

                case CheckOutcome.Fail:
                {
                    ActionOutcome lost = new ActionOutcome(Id, check, "You lose " + who + " in the traffic.");
                    lost.Notes.Add("nothing learned, and nothing given away");
                    return lost;
                }

                default:
                {
                    ActionOutcome spotted = new ActionOutcome(Id, check, who + " turns round and looks straight at you.");
                    ActionSupport.WarnUnderInvestigation(context, context.Target, EntityId.None, spotted);
                    return spotted;
                }
            }
        }

        private ActionOutcome Placed(ActionContext context, CheckResult check, string narration)
        {
            EntityId where = context.Vanilla.GetZoneOf(context.Target);
            ActionOutcome outcome = new ActionOutcome(Id, check, narration);
            if (where.IsNone)
            {
                outcome.Notes.Add("the world has no current location for " + context.NameOf(context.Target));
                return outcome;
            }

            Fact placed = Whereabouts.Record(context.World, context.Target, where, context.NameOf(where));
            context.World.Knowledge.Teach(context.Actor, placed.Id, KnowledgeSource.Inference, 0.85, context.Now, false);
            outcome.Notes.Add("learned: " + ActionSupport.Describe(context, placed.Id) + " (unprovable)");
            return outcome;
        }

        /// <summary>
        /// Something the quarry did themselves that the follower does not know about.
        ///
        /// Two filters, and both are the difference between catching somebody and merely watching
        /// them. It has to be first-hand: their own doing, not their opinion of somebody else's.
        /// And it has to be an act rather than a standing arrangement - owning a thing is not
        /// something a person can be caught in the middle of, which is exactly the line the
        /// newsworthy predicates already draw.
        /// </summary>
        private static Fact FirstHandFact(ActionContext context)
        {
            Fact best = null;
            foreach (KnowledgeRecord record in context.World.Knowledge.BeliefsOf(context.Target))
            {
                if (record.Source != KnowledgeSource.Participant
                    || context.World.Knowledge.Knows(context.Actor, record.FactId))
                {
                    continue;
                }

                Fact fact = context.World.Knowledge.GetFact(record.FactId);
                if (fact == null
                    || fact.Truth != TruthState.True
                    || fact.Subject != context.Target
                    || !FactPredicates.IsNewsworthy(fact.Predicate))
                {
                    continue;
                }

                if (best == null || string.CompareOrdinal(fact.Id.Value, best.Id.Value) < 0)
                {
                    best = fact;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Stand close enough to hear what people say to each other rather than to you.
    ///
    /// What it produces is hearsay, and it is filed as such. Overhearing a thing is still being
    /// told it by somebody who does not know you are listening - the difference from asking is
    /// that they were not choosing their words for you, not that the claim got any harder.
    /// </summary>
    public sealed class EavesdropAction : NarrativeAction
    {
        public EavesdropAction() : base("eavesdrop", ActionFamily.Information, "Listen in")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            List<EntityId> present = Talkers(context);
            if (present.Count < 2)
            {
                return Availability.NotRelevant("nobody here is talking to anybody");
            }

            return FindOverheard(context, present, out EntityId _) == null
                ? Availability.NotRelevant("nothing being said here is new to you")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            List<EntityId> present = Talkers(context);
            Fact overheard = FindOverheard(context, present, out EntityId speaker);
            if (overheard == null)
            {
                ActionOutcome quiet = new ActionOutcome(Id, null, "Nothing worth hearing is being said.");
                quiet.Notes.Add("no fact present that the actor does not already hold");
                return quiet;
            }

            CheckResult check = context.Checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Shadowing, context.Actor, speaker),
                context.Rng);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    context.World.Knowledge.Teach(
                        context.Actor,
                        overheard.Id,
                        KnowledgeSource.Hearsay,
                        check.Outcome == CheckOutcome.CriticalPass ? 0.7 : 0.5,
                        context.Now,
                        false,
                        speaker);

                    ActionOutcome outcome = new ActionOutcome(Id, check, "You catch enough of it to be worth the standing about.");
                    outcome.Notes.Add("overheard from " + context.NameOf(speaker) + ": " + ActionSupport.Describe(context, overheard.Id) + " (hearsay, unprovable)");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.SecretLearned,
                        context.Actor,
                        overheard.Subject,
                        context.Now,
                        0.3,
                        context.Zone,
                        new[] { overheard.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    return outcome;
                }

                case CheckOutcome.Fail:
                {
                    ActionOutcome missed = new ActionOutcome(Id, check, "They drop their voices before you get anything out of it.");
                    missed.Notes.Add("nothing heard, and nobody minded you being there");
                    return missed;
                }

                default:
                {
                    ActionOutcome caught = new ActionOutcome(Id, check, "They stop talking, and they are all looking at you.");
                    caught.Events.Add(context.World.Record(
                        WorldEventType.Trespass,
                        context.Actor,
                        speaker,
                        context.Now,
                        0.35,
                        context.Zone,
                        witnesses: ActionSupport.Bystanders(context, true),
                        threadId: context.Thread?.Id ?? EntityId.None));
                    ActionSupport.WarnUnderInvestigation(context, speaker, EntityId.None, caught);
                    return caught;
                }
            }
        }

        /// <summary>
        /// Live people in the room other than the listener, in a stable order. A conversation
        /// needs two of them; one person alone is not talking to anybody.
        /// </summary>
        private static List<EntityId> Talkers(ActionContext context)
        {
            List<EntityId> present = new List<EntityId>();
            IReadOnlyList<EntityId> here = context.Vanilla.GetCharactersInZone(context.Zone);
            for (int i = 0; i < here.Count; i++)
            {
                if (here[i] != context.Actor && context.Vanilla.IsAlive(here[i]))
                {
                    present.Add(here[i]);
                }
            }

            present.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return present;
        }

        private static Fact FindOverheard(ActionContext context, List<EntityId> present, out EntityId speaker)
        {
            speaker = EntityId.None;
            Fact best = null;

            for (int i = 0; i < present.Count; i++)
            {
                foreach (KnowledgeRecord record in context.World.Knowledge.BeliefsOf(present[i]))
                {
                    if (record.Confidence < 0.5 || context.World.Knowledge.Knows(context.Actor, record.FactId))
                    {
                        continue;
                    }

                    Fact fact = context.World.Knowledge.GetFact(record.FactId);
                    if (fact == null
                        || fact.Truth == TruthState.Superseded
                        || !FactPredicates.IsNewsworthy(fact.Predicate))
                    {
                        continue;
                    }

                    if (best == null || string.CompareOrdinal(fact.Id.Value, best.Id.Value) < 0)
                    {
                        best = fact;
                        speaker = present[i];
                    }
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Put two accounts of the same thing side by side and work out which one does not fit.
    ///
    /// This is the payoff for keeping a garbled or invented story as its own fact linked back to
    /// the truth instead of overwriting it. Because both versions are in the graph and know they
    /// are versions of each other, somebody holding both can be given a real chance to notice -
    /// and, where one of them was told deliberately, to work out who told it.
    /// </summary>
    public sealed class CompareTestimonyAction : NarrativeAction
    {
        public CompareTestimonyAction() : base("compare_testimony", ActionFamily.Information, "Compare what you have been told")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            return FindConflict(context, out Fact _, out Fact _)
                ? Availability.Available()
                : Availability.NotRelevant("nothing you have heard contradicts anything else you have heard");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            if (!FindConflict(context, out Fact truth, out Fact falsehood))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "Everything you have been told hangs together.");
                nothing.Notes.Add("no two beliefs are versions of the same claim");
                return nothing;
            }

            CheckResult check = context.Checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Corroboration, context.Actor, EntityId.None),
                context.Rng);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    Settle(context, keep: truth, drop: falsehood, confidence: check.Outcome == CheckOutcome.CriticalPass ? 0.95 : 0.8);

                    ActionOutcome outcome = new ActionOutcome(Id, check, "Held against each other, one of the two stories stops standing up.");
                    outcome.Notes.Add("kept: " + ActionSupport.Describe(context, truth.Id));
                    outcome.Notes.Add("discarded: " + ActionSupport.Describe(context, falsehood.Id));
                    NameTheLiar(context, truth, falsehood, outcome);
                    return outcome;
                }

                case CheckOutcome.Fail:
                {
                    ActionOutcome stuck = new ActionOutcome(Id, check, "Both versions still sound as likely as each other.");
                    stuck.Notes.Add("no belief changed");
                    return stuck;
                }

                default:
                {
                    // Reconciled the wrong way round. Nothing was lost that could not be argued
                    // back later; what was gained is certainty about the wrong person.
                    Settle(context, keep: falsehood, drop: truth, confidence: 0.85);
                    ActionOutcome wrong = new ActionOutcome(Id, check, "You work out which story fits, and settle firmly on the wrong one.");
                    wrong.Notes.Add("now confident in: " + ActionSupport.Describe(context, falsehood.Id));
                    wrong.Notes.Add("the true version is still in the graph and can be recovered");
                    return wrong;
                }
            }
        }

        private static void Settle(ActionContext context, Fact keep, Fact drop, double confidence)
        {
            context.World.Knowledge.Teach(context.Actor, keep.Id, KnowledgeSource.Inference, confidence, context.Now, false);
            if (context.World.Knowledge.TryGetBelief(context.Actor, drop.Id, out KnowledgeRecord discarded))
            {
                // Stopping believing a thing is not forgetting it. The belief stays, too weak to
                // act on, so a later contradiction has something to argue with.
                discarded.Confidence = 0.1;
            }
        }

        /// <summary>
        /// If the discarded version was told to the actor by somebody who knew better, the world
        /// already holds a `lied_about` fact saying so. Working out which story was false is the
        /// moment that becomes reachable - as an inference, not as proof.
        /// </summary>
        private static void NameTheLiar(ActionContext context, Fact truth, Fact falsehood, ActionOutcome outcome)
        {
            if (!context.World.Knowledge.TryGetBelief(context.Actor, falsehood.Id, out KnowledgeRecord heard)
                || heard.ToldBy.IsNone)
            {
                return;
            }

            foreach (Fact candidate in context.World.Knowledge.Facts.Values)
            {
                if (candidate.Subject != heard.ToldBy
                    || candidate.Predicate != FactPredicates.LiedAbout
                    || candidate.Object != truth.Id)
                {
                    continue;
                }

                context.World.Knowledge.Teach(context.Actor, candidate.Id, KnowledgeSource.Inference, 0.7, context.Now, false);
                outcome.Notes.Add(context.NameOf(heard.ToldBy) + " told you the version that was not true");
                return;
            }
        }

        /// <summary>
        /// Two beliefs the actor holds that are versions of the same claim, truth first.
        /// </summary>
        private static bool FindConflict(ActionContext context, out Fact truth, out Fact falsehood)
        {
            truth = null;
            falsehood = null;

            // Versions of one claim all point at the same root, so they can be bucketed in a
            // single pass rather than compared with each other pair by pair.
            Dictionary<EntityId, List<Fact>> byClaim = new Dictionary<EntityId, List<Fact>>();
            foreach (KnowledgeRecord record in context.World.Knowledge.BeliefsOf(context.Actor))
            {
                Fact fact = context.World.Knowledge.GetFact(record.FactId);
                if (fact == null || fact.Truth == TruthState.Superseded)
                {
                    continue;
                }

                EntityId root = fact.DistortionOf.IsNone ? fact.Id : fact.DistortionOf;
                if (!byClaim.TryGetValue(root, out List<Fact> versions))
                {
                    versions = new List<Fact>();
                    byClaim[root] = versions;
                }

                versions.Add(fact);
            }

            List<EntityId> roots = new List<EntityId>(byClaim.Keys);
            roots.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));

            for (int i = 0; i < roots.Count; i++)
            {
                List<Fact> versions = byClaim[roots[i]];
                if (versions.Count < 2)
                {
                    continue;
                }

                versions.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));

                // Two false versions - two different people blamed for the same theft - are as
                // usable as a true/false pair, so the true one is preferred and never required.
                Fact stands = versions.Find(f => !f.IsUntrue) ?? versions[0];
                truth = stands;
                falsehood = versions.Find(f => f.Id != stands.Id);
                return true;
            }

            return false;
        }
    }
}
