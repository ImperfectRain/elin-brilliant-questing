using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// One way somebody could try to make a desired condition hold: a registered verb, the kind of
    /// change it would be taken for, and the concrete things it is pointed at (BQa-011).
    ///
    /// A proposal and nothing more. Whether it is possible here is still
    /// <see cref="NarrativeAction.GetAvailability"/>'s answer, what happens is still the check's
    /// and the seam's, and taking it is still <see cref="ActionAttempt.Run"/>. What this adds is
    /// the one thing neither end had: that this verb, pointed at these entities, would move that
    /// want - reached through the effect vocabulary rather than through anybody's list of which
    /// verbs answer which goals.
    /// </summary>
    public sealed class GoalRoute
    {
        internal GoalRoute(
            NpcGoal goal,
            string effectKind,
            NarrativeAction action,
            EntityId target,
            ActionBinding binding,
            string because)
        {
            Goal = goal;
            EffectKind = effectKind;
            Action = action;
            Target = target;
            Binding = binding;
            Because = because ?? string.Empty;
        }

        /// <summary>The want this would serve.</summary>
        public NpcGoal Goal { get; }

        /// <summary>The <see cref="SemanticEffects"/> key that connects the want to the verb.</summary>
        public string EffectKind { get; }

        public NarrativeAction Action { get; }

        /// <summary>Who it would be aimed at, or none for an act with no other party.</summary>
        public EntityId Target { get; }

        /// <summary>What it is about: the proposition, object or destination the condition named.</summary>
        public ActionBinding Binding { get; }

        /// <summary>The claim this attempt is about, or none.</summary>
        public EntityId SubjectFact => Binding.PropositionFact;

        /// <summary>The object this attempt is about, or none.</summary>
        public EntityId SubjectItem => Binding.Item;

        /// <summary>Inspector-facing, for people. Nothing reads it.</summary>
        public string Because { get; }

        public override string ToString() =>
            Action.Id + " to " + EffectKind + (Target.IsNone ? string.Empty : " at " + Target.Value);
    }

    /// <summary>
    /// What the library could offer one want, and - where it could offer nothing - why (BQa-011).
    ///
    /// The reason for the second half is the same reason
    /// <see cref="ActionEffectCoverage"/> exists: to a consumer that only asks what matched, a want
    /// nothing can read, a want no verb answers on this build and a want nothing points a verb at
    /// all look exactly like "there is nothing to be done", and only one of those is a fact about
    /// the world. A supported search with no routes is an honest wait; an unsupported one is a want
    /// this build cannot reason about, and neither may fall through to an unrelated verb.
    /// </summary>
    public sealed class GoalRouteSearch
    {
        private static readonly GoalRoute[] NoRoutes = new GoalRoute[0];
        private static readonly string[] NoNotes = new string[0];

        internal GoalRouteSearch(string unsupported)
        {
            Unsupported = unsupported ?? string.Empty;
            Routes = NoRoutes;
            Notes = NoNotes;
        }

        internal GoalRouteSearch(IReadOnlyList<GoalRoute> routes, IReadOnlyList<string> notes)
        {
            Unsupported = string.Empty;
            Routes = routes ?? NoRoutes;
            Notes = notes ?? NoNotes;
        }

        /// <summary>Every route found, in a stable order.</summary>
        public IReadOnlyList<GoalRoute> Routes { get; }

        /// <summary>
        /// Why this want cannot be read at all, or empty when it can. Not the same as having no
        /// routes: a readable want with nothing available is somebody with nothing to do about it.
        /// </summary>
        public string Unsupported { get; }

        /// <summary>Where the search came up empty and why, for the inspector.</summary>
        public IReadOnlyList<string> Notes { get; }

        /// <summary>Whether the want is one this build can look for routes to.</summary>
        public bool IsSupported => Unsupported.Length == 0;

        public bool HasRoutes => Routes.Count > 0;
    }

    /// <summary>
    /// The bridge from a machine-readable desired condition to the verbs that could move it
    /// (BQa-011).
    ///
    /// Three declarations already existed and none of them knew about the others: a goal says what
    /// state it wants (BQa-008), a condition term says which kinds of change would move it and what
    /// each of its bindings is, and a verb says which kinds of change it could make and what it has
    /// to be pointed at (BQa-010). This joins them in that order - term, effect kind, verb - so
    /// that a verb registered tomorrow with today's vocabulary answers every want that vocabulary
    /// answers, and nothing anywhere holds a list of which verbs serve which goals.
    ///
    /// It is a read. Nothing here rolls, records, teaches or mutates, including the goal it was
    /// asked about: the whole output is proposals, and a proposal that is never taken has changed
    /// nothing. Ranking is not its business either - the goal weight, the actor's problem-solving
    /// preferences and their opportunity are the caller's existing authorities, and duplicating
    /// them here would be a second selector.
    ///
    /// <b>What it will not do.</b> It never reads authoritative state the actor has no route to.
    /// The people a route can be aimed at come from the condition's own bindings, from the records
    /// those bindings name, and from what the actor themself believes - never from a sweep of the
    /// world for whoever happens to be holding the thing. An actor who does not know who took their
    /// ring is offered no route to that person, which is the correct answer and not a gap.
    /// </summary>
    public static class GoalRoutes
    {
        /// <summary>
        /// How many other parties one want may be pointed at. The search runs every pass for every
        /// active goal of every due actor, so it is bounded for the reason the scheme pass itself
        /// is: the cost must not grow with how long the save has been played.
        /// </summary>
        public const int MostTargets = 4;

        /// <summary>
        /// How many proposals one want may produce. A backstop rather than a routine trim: the
        /// verb library is a fixed size and the other two bounds are small, so a want that reaches
        /// this has found something wrong rather than something rich.
        /// </summary>
        public const int MostRoutes = 64;

        /// <summary>
        /// How many of the actor's own claims about the thing are read for a party to aim at.
        ///
        /// Applied after sorting rather than during the scan, because belief iteration order is not
        /// part of the save and a bound that depended on it would answer differently after a
        /// reload. The scan itself is bounded by that one actor's memory, never by the world.
        /// </summary>
        public const int MostClaimsRead = 8;

        /// <summary>
        /// Every way this actor could try to make this goal's condition hold, or why there is no
        /// way to look.
        ///
        /// <paramref name="vanilla"/> is asked only what this build can carry, through the same
        /// BQ-090 gate a spatial route uses: a possession effect vanilla would have to perform is
        /// not offered on a build that cannot move items. A null build answers nothing, so every
        /// delegated effect is refused rather than promised.
        /// </summary>
        public static GoalRouteSearch Discover(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ActionRegistry registry,
            NarrativeNpc actor,
            NpcGoal goal)
        {
            if (world == null || registry == null || actor == null || goal == null)
            {
                return new GoalRouteSearch("nothing to look for");
            }

            if (!goal.HasCondition)
            {
                return new GoalRouteSearch("the want names no machine-readable condition");
            }

            GoalCondition condition = goal.Condition;
            if (!GoalConditionRegistry.IsSupported(condition))
            {
                return new GoalRouteSearch("'" + condition.Kind + "' is not a condition term this build can read");
            }

            IReadOnlyList<string> effects = GoalConditionRegistry.AdvancedBy(condition.Kind);
            if (effects == null || effects.Count == 0)
            {
                return new GoalRouteSearch("nothing is registered that would move '" + condition.Kind + "'");
            }

            Subject subject = Subject.Read(world, actor, condition);
            if (subject.Incomplete.Length > 0)
            {
                return new GoalRouteSearch(subject.Incomplete);
            }

            List<GoalRoute> routes = new List<GoalRoute>();
            List<string> notes = new List<string>();
            List<EntityId> targets = subject.Targets;

            for (int e = 0; e < effects.Count && routes.Count < MostRoutes; e++)
            {
                string effect = effects[e];
                List<NarrativeAction> verbs = registry.Advancing(effect, vanilla);
                if (verbs.Count == 0)
                {
                    notes.Add("no registered verb on this build could " + effect);
                    continue;
                }

                for (int v = 0; v < verbs.Count && routes.Count < MostRoutes; v++)
                {
                    NarrativeAction verb = verbs[v];
                    ActionBinding binding = subject.Bind();
                    if (!ActionBinding.HasRequiredSemanticSlots(verb, binding))
                    {
                        notes.Add(verb.Id + " could " + effect + " but nothing in the want points it at anything");
                        continue;
                    }

                    string because = "'" + condition.Kind + "' is moved by " + effect + ", which " + verb.Id + " could do";
                    for (int t = 0; t < targets.Count && routes.Count < MostRoutes; t++)
                    {
                        routes.Add(new GoalRoute(goal, effect, verb, targets[t], subject.Bind(), because));
                    }

                    if (routes.Count < MostRoutes)
                    {
                        // An act with no other party. `destroy_evidence` is the standing example:
                        // it is aimed at a thing, and requiring somebody to aim it at would lose
                        // the route rather than make it honest.
                        routes.Add(new GoalRoute(goal, effect, verb, EntityId.None, subject.Bind(), because));
                    }
                }
            }

            if (routes.Count == 0 && notes.Count == 0)
            {
                notes.Add("nothing the want names could be reached from where the actor stands");
            }

            return new GoalRouteSearch(routes, notes);
        }

        /// <summary>
        /// What one condition is about, read through the term's own declaration of what each of its
        /// bindings is, and who the actor could legitimately aim an attempt at.
        /// </summary>
        private sealed class Subject
        {
            private Subject()
            {
                Targets = new List<EntityId>();
            }

            internal EntityId Item { get; private set; }

            internal EntityId Proposition { get; private set; }

            internal EntityId Destination { get; private set; }

            internal List<EntityId> Targets { get; }

            /// <summary>Why the term could not be read into a subject at all, or empty.</summary>
            internal string Incomplete { get; private set; } = string.Empty;

            internal static Subject Read(NarrativeWorldState world, NarrativeNpc actor, GoalCondition condition)
            {
                Subject subject = new Subject();
                IReadOnlyList<GoalConditionSlot> slots = GoalConditionRegistry.Slots(condition.Kind);
                List<EntityId> people = new List<EntityId>();
                bool anyRoleDeclared = false;

                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    EntityId reference = condition.Reference(slots[i].Name);
                    if (reference.IsNone)
                    {
                        continue;
                    }

                    switch (slots[i].Role)
                    {
                        case GoalBindingRole.Object:
                            anyRoleDeclared = true;
                            subject.Item = reference;
                            break;
                        case GoalBindingRole.Claim:
                            anyRoleDeclared = true;
                            if (world.Knowledge.GetFact(reference) != null)
                            {
                                subject.Proposition = reference;
                                AddParties(world, world.Knowledge.GetFact(reference), people);
                            }

                            break;
                        case GoalBindingRole.Undertaking:
                            anyRoleDeclared = true;
                            ReadUndertaking(world, reference, subject, people);
                            break;
                        case GoalBindingRole.Place:
                            anyRoleDeclared = true;
                            subject.Destination = reference;
                            break;
                        case GoalBindingRole.Person:
                            anyRoleDeclared = true;
                            Offer(world, reference, people);
                            break;
                    }
                }

                if (!anyRoleDeclared)
                {
                    subject.Incomplete = "'" + condition.Kind + "' has not said what its bindings are";
                    return subject;
                }

                subject.ReadWhatTheActorKnows(world, actor, people);

                people.Sort(CompareIds);
                for (int i = 0; i < people.Count && subject.Targets.Count < MostTargets; i++)
                {
                    if (people[i] != actor.Id)
                    {
                        subject.Targets.Add(people[i]);
                    }
                }

                return subject;
            }

            /// <summary>A fresh binding each time: a verb may keep the one it was handed.</summary>
            internal ActionBinding Bind()
            {
                return new ActionBinding
                {
                    PropositionFact = Proposition,
                    Item = Item,
                    Destination = Destination
                };
            }

            /// <summary>
            /// The rest of the people a route could be aimed at, taken from what this actor
            /// believes and from nowhere else.
            ///
            /// This is the whole of "actor-accessible". Somebody whose ring was taken knows they
            /// have lost it; whether they know who has it depends on whether they hold a claim that
            /// says so, and if they do not, the honest answer is that they have no route to that
            /// person yet. Reading the world's own possession records here instead would hand every
            /// victim the thief's name the moment the theft happened.
            /// </summary>
            private void ReadWhatTheActorKnows(NarrativeWorldState world, NarrativeNpc actor, List<EntityId> people)
            {
                if (Item.IsNone && Proposition.IsNone)
                {
                    return;
                }

                List<EntityId> held = new List<EntityId>();
                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(actor.Id))
                {
                    Fact fact = world.Knowledge.GetFact(belief.FactId);
                    if (fact == null)
                    {
                        continue;
                    }

                    bool aboutTheThing = !Item.IsNone && (fact.Subject == Item || fact.Object == Item);
                    bool aboutTheClaim = !Proposition.IsNone && fact.Id == Proposition;
                    if (aboutTheThing || aboutTheClaim)
                    {
                        held.Add(fact.Id);
                    }
                }

                held.Sort(CompareIds);
                for (int i = 0; i < held.Count && i < MostClaimsRead; i++)
                {
                    Fact fact = world.Knowledge.GetFact(held[i]);
                    AddParties(world, fact, people);

                    // A want about a thing has no claim of its own until the actor holds one that
                    // mentions it. The least id, so the same want answers the same way after a
                    // reload rather than however the belief store happened to come back.
                    if (Proposition.IsNone)
                    {
                        Proposition = fact.Id;
                    }
                }
            }

            private static void ReadUndertaking(
                NarrativeWorldState world,
                EntityId reference,
                Subject subject,
                List<EntityId> people)
            {
                SocialObligation obligation = world.Obligations.Find(reference);
                if (obligation == null)
                {
                    return;
                }

                Offer(world, obligation.Debtor, people);
                Offer(world, obligation.Creditor, people);
                if (subject.Proposition.IsNone && world.Knowledge.GetFact(obligation.Subject) != null)
                {
                    subject.Proposition = obligation.Subject;
                }
            }

            private static void AddParties(NarrativeWorldState world, Fact fact, List<EntityId> people)
            {
                if (fact == null)
                {
                    return;
                }

                Offer(world, fact.Subject, people);
                Offer(world, fact.Object, people);
            }

            private static void Offer(NarrativeWorldState world, EntityId who, List<EntityId> people)
            {
                if (who.IsNone || people.Contains(who) || !world.Registry.IsActor(who))
                {
                    return;
                }

                NarrativeNpc npc = world.Registry.GetNpc(who);
                if (npc == null || !npc.Alive)
                {
                    return;
                }

                people.Add(who);
            }

            private static int CompareIds(EntityId left, EntityId right) =>
                string.CompareOrdinal(left.Value, right.Value);
        }
    }
}
