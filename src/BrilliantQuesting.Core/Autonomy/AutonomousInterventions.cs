using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
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
    /// The world taking one of its own problems up, without being asked (BQ-094).
    ///
    /// BQ-093 proved that an NPC and the player reach the same verb through the same three calls,
    /// and was careful to say that nothing there acted unbidden: the one intention in its
    /// laboratory was asked for by the run. This is what makes anybody act. It is deliberately the
    /// whole of the new behaviour - there is no scheme scheduler, no travel, no ecology of rival
    /// adventurers, and no second resolver. What is new is that somebody chooses, and that the
    /// choosing happens because time passed rather than because a surface called in.
    ///
    /// <b>It is a pass, not a clock.</b> Whoever moves the game's time forward calls
    /// <see cref="Advance"/>, exactly as they already call <see cref="ThreadEngine.Advance"/>, and
    /// the plugin does both on the same hooks. Nothing here polls, and a pass that is never called
    /// simply leaves the world as it was.
    ///
    /// <b>What it may take up.</b> A live matter the player has not touched. "Ignored" is read
    /// from history rather than stored as a flag: a matter nobody has acted in, that has stood for
    /// longer than <see cref="Patience"/>, is a matter the world is entitled to solve. The moment
    /// the player does anything inside it, it stops being available to anybody else - not because
    /// the player owns problems, but because a situation being solved out from under somebody
    /// mid-attempt is a bad experience rather than a living world (`LW 10.5` cuts the other way,
    /// and both are true).
    ///
    /// <b>Who takes it up.</b> Somebody who knows about it and has a stake in it. Knowing is the
    /// knowledge graph's answer and nothing else - an actor who has never heard of the matter
    /// cannot act on it, which is the same rule the player lives under. A stake is either that the
    /// trouble is their own or that they hold a goal bearing on it; without one, nobody acts, so
    /// the town does not fill up with strangers fixing things for the love of it.
    ///
    /// <b>What they do about it.</b> The shared library's own answer:
    /// <see cref="ActionRegistry.Discover"/> against an off-screen context, ranked by how well the
    /// verb's family reads as the actor's own way of solving things, by their stake, and by
    /// whether vanilla's activity read makes the attempt plausible at all. Personal lines take
    /// candidates off the table exactly as they do in goal formation (BQ-077). The winner becomes
    /// an <see cref="ActionIntent"/> and goes through <see cref="ActionAttempt.Run"/> - the same
    /// three calls the player's surface makes, with no branch anywhere that knows this one came
    /// from a pass.
    ///
    /// <b>Succeed, fail, or make it worse.</b> All three are the check layer's own outcomes and
    /// none of them is special-cased here. A success that closes the matter closes it through the
    /// verb's own <c>ActionSupport.Resolve</c>; a failure leaves the matter open and its own
    /// recorded act raises the thread's tension through the consequence layer, which is what
    /// "made it worse" already means everywhere else in this simulation.
    ///
    /// <b>What is recorded.</b> The deed, the ending, and one claim that the ending can be spoken
    /// about. Nothing about the actor's day: the activity read is weighed and thrown away, and no
    /// event, fact or memory carries where anybody was or what their routine said (`VS 5.3`,
    /// `VS 5.4`, `D021`).
    /// </summary>
    public sealed class AutonomousInterventions
    {
        /// <summary>
        /// How long a matter stands before anybody else is entitled to take it up.
        ///
        /// Not a difficulty knob and not a grace period for the player's benefit: a situation that
        /// was solved by a neighbour the same hour it arose never existed as far as the player is
        /// concerned, and a world that does that is not livelier, it is just faster than the
        /// player can look.
        /// </summary>
        public long Patience { get; set; } = 2;

        /// <summary>
        /// How plausible an opening has to look before somebody acts on it.
        ///
        /// Below <see cref="InterventionOpportunity.Unread"/> on purpose, so a build that answers
        /// no activity facets at all still lets the world act rather than falling silent.
        /// </summary>
        public double OpportunityFloor { get; set; } = 0.25;

        /// <summary>
        /// How many matters may be taken up in one pass. One, because a world that solves four
        /// things while the player walks across a room reads as a world that did not need them.
        /// </summary>
        public int MostPerPass { get; set; } = 1;

        /// <summary>How many of a matter's people are weighed. Bounds the discovery sweep.</summary>
        public int MostActorsPerMatter { get; set; } = 4;

        /// <summary>How many people a verb may be pointed at. Bounds the discovery sweep.</summary>
        public int MostTargetsPerMatter { get; set; } = 4;

        /// <summary>
        /// Every matter this pass considered and what it decided, in the order it considered them.
        /// Transient, like <see cref="ThreadEngine.LastApplied"/>, and cleared on each call.
        /// </summary>
        public List<InterventionTrace> LastPass { get; } = new List<InterventionTrace>();

        /// <summary>
        /// Lets the world act on its own matters once, and answers how many attempts it made.
        ///
        /// Deterministic throughout: matters in the order the world holds them, people in the
        /// order the matter names them, verbs in registry order, and ties broken by verb id. The
        /// only randomness is the check roll itself, which is the shared deterministic generator
        /// the player's own attempts use.
        /// </summary>
        public int Advance(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            GameTime now)
        {
            LastPass.Clear();
            if (world == null || vanilla == null || checks == null || registry == null)
            {
                return 0;
            }

            int acted = 0;
            EntityId player = vanilla.PlayerId;

            for (int i = 0; i < world.Threads.Count && acted < MostPerPass; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (!thread.IsLive)
                {
                    continue;
                }

                InterventionTrace trace = Consider(world, vanilla, checks, registry, player, thread, now);
                if (trace == null)
                {
                    continue;
                }

                LastPass.Add(trace);
                if (trace.Acted)
                {
                    acted++;
                }
            }

            return acted;
        }

        private InterventionTrace Consider(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            EntityId player,
            NarrativeThread thread,
            GameTime now)
        {
            if (now.DaysSince(thread.CreatedAt) < Patience)
            {
                return null;
            }

            if (PlayerHasItInHand(world, player, thread))
            {
                return null;
            }

            InterventionTrace trace = new InterventionTrace(thread.Id, thread.ArchetypeId);
            Fact matter = ActionBinding.StandingTrouble(world, thread);
            EntityId matterZone = MatterZone(world, vanilla, thread, matter);

            List<EntityId> actors = Actors(world, vanilla, player, thread, matter);
            if (actors.Count == 0)
            {
                trace.Refusal = "nobody who knows about it has a stake in it";
                return trace;
            }

            List<EntityId> targets = Targets(world, vanilla, player, thread);
            InterventionOption best = null;
            ActionContext bestContext = null;

            for (int a = 0; a < actors.Count; a++)
            {
                EntityId actor = actors[a];
                double motive = Motive(world, actor, thread, matter);
                InterventionOpportunity opportunity = InterventionOpportunity.Read(vanilla, actor, matterZone);
                trace.Opportunities.Add(opportunity);

                if (opportunity.Plausibility < OpportunityFloor)
                {
                    continue;
                }

                for (int t = 0; t < targets.Count; t++)
                {
                    EntityId target = targets[t];
                    if (target == actor)
                    {
                        continue;
                    }

                    if (!ActorContexts.TryBuildOffScreen(
                            world, vanilla, checks, world.Rng, actor, target,
                            out ActionContext context, out string _))
                    {
                        continue;
                    }

                    // The same two people, told nothing about the matter. What the library offers
                    // here is what it would offer any day of the week, and is the baseline the
                    // matter's own routes are found against.
                    ActorContexts.TryBuildOffScreen(
                        world, vanilla, checks, world.Rng, actor, target,
                        out ActionContext anyDay, out string _);

                    context.Thread = thread;
                    if (matter != null)
                    {
                        context.SubjectFact = matter.Id;
                    }

                    List<ActionOffer> offers = RoutesToEndIt(registry, context, anyDay);
                    for (int o = 0; o < offers.Count; o++)
                    {
                        InterventionOption option = Weigh(world, offers[o], actor, target, opportunity, motive, thread);
                        trace.Options.Add(option);

                        if (!option.Eligible)
                        {
                            continue;
                        }

                        if (best == null || Beats(option, best))
                        {
                            best = option;
                            bestContext = context;
                        }
                    }
                }
            }

            if (best == null)
            {
                trace.Refusal = trace.Options.Count == 0
                    ? "none of them has a route that could end it"
                    : "every route that could end it is either forbidden them or beyond them";
                return trace;
            }

            // The context the winning option was discovered in, re-pointed at nothing else: the
            // availability answer that chose it was asked of this actor, this target and this
            // matter, and running the verb in a different one would resolve something else.
            ActionIntent intent = new ActionIntent(
                best.Actor,
                best.ActionId,
                best.Target,
                "no one else had taken up " + thread.ArchetypeId)
            {
                Thread = thread,
                SubjectFact = matter != null ? matter.Id : EntityId.None
            };

            trace.Attempt = ActionAttempt.Run(registry, intent, bestContext);
            RecordEnding(world, thread, best.Actor, matter, trace, now);
            return trace;
        }

        /// <summary>
        /// Mints the claim that somebody put the matter right, when they did.
        ///
        /// The one thing this pass writes that no verb wrote. It exists because the ledger's
        /// ending deliberately names no facts, so a matter solved while the player was elsewhere
        /// has nothing anybody could repeat - and a player finding out because the simulation told
        /// them for free is the omniscience the invariants forbid. A claim can be told, doubted,
        /// garbled and asked after, which is what "the player can find out how" has to mean.
        ///
        /// Held by whoever did it, as a participant, and provable by nobody: this was resolved off
        /// screen, so there is no observation behind it and no evidence to show (`VS 5.4`).
        /// </summary>
        private static void RecordEnding(
            NarrativeWorldState world,
            NarrativeThread thread,
            EntityId actor,
            Fact matter,
            InterventionTrace trace,
            GameTime now)
        {
            if (thread.State != ThreadState.Resolved)
            {
                return;
            }

            WorldEvent ending = LatestEnding(world, thread);
            if (ending == null || ending.Actor != actor)
            {
                return;
            }

            string outcome = ThreadResolution.OutcomeOf(ending);
            trace.Resolved = true;
            trace.Resolution = outcome.Length > 0 ? outcome : thread.Resolution ?? string.Empty;

            Fact settled = new Fact(
                world.NewId("fact"),
                actor,
                FactPredicates.Settled,
                matter != null ? matter.Subject : EntityId.None,
                trace.Resolution,
                originEvent: ending.Id);

            world.Knowledge.AddFact(settled);
            world.Knowledge.Teach(actor, settled.Id, KnowledgeSource.Participant, 1.0, now, canProve: false);
            thread.FactIds.Add(settled.Id);
            trace.SettledFactId = settled.Id;
        }

        private static WorldEvent LatestEnding(NarrativeWorldState world, NarrativeThread thread)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Type == WorldEventType.ThreadResolved && events[i].ThreadId == thread.Id)
                {
                    return events[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the player is already in this matter.
        ///
        /// Read off history through the thread's own attribution rule, so an act counts when the
        /// verb that recorded it said which matter it belonged to - never because the player
        /// happened to be nearby.
        /// </summary>
        private static bool PlayerHasItInHand(NarrativeWorldState world, EntityId player, NarrativeThread thread)
        {
            if (player.IsNone)
            {
                return false;
            }

            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.Actor == player && thread.IsNamedBy(worldEvent))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The people this matter could conceivably fall to: alive, not the player, aware of it,
        /// and with something of their own at stake.
        ///
        /// Both of the last two are gates rather than weights, and both are answered by state that
        /// already exists. An actor who has never heard of the matter cannot act on it, for the
        /// same reason the player cannot report a theft nobody told them about; an actor with no
        /// stake has no reason to, and without that gate every idle townsperson would be weighed
        /// against every open matter in the world on every pass.
        /// </summary>
        private List<EntityId> Actors(NarrativeWorldState world, IVanillaState vanilla, EntityId player, NarrativeThread thread, Fact matter)
        {
            List<EntityId> actors = new List<EntityId>();
            for (int i = 0; i < thread.ParticipantIds.Count && actors.Count < MostActorsPerMatter; i++)
            {
                EntityId who = thread.ParticipantIds[i];
                if (who == player || who.IsNone || actors.Contains(who))
                {
                    continue;
                }

                NarrativeNpc npc = world.Registry.GetNpc(who);
                if (npc == null || !npc.IsCanonical || !vanilla.IsAlive(who))
                {
                    continue;
                }

                if (!KnowsOfIt(world, who, thread) || Motive(world, who, thread, matter) <= 0.0)
                {
                    continue;
                }

                actors.Add(who);
            }

            return actors;
        }

        private List<EntityId> Targets(NarrativeWorldState world, IVanillaState vanilla, EntityId player, NarrativeThread thread)
        {
            // Nobody in particular first: several verbs are about the matter rather than about a
            // person, and a target is a thing the caller offers rather than one it must invent.
            List<EntityId> targets = new List<EntityId> { EntityId.None };
            for (int i = 0; i < thread.ParticipantIds.Count && targets.Count <= MostTargetsPerMatter; i++)
            {
                EntityId who = thread.ParticipantIds[i];
                if (who == player || who.IsNone || targets.Contains(who))
                {
                    continue;
                }

                if (world.Registry.GetNpc(who) == null || !vanilla.IsAlive(who))
                {
                    continue;
                }

                targets.Add(who);
            }

            return targets;
        }

        /// <summary>
        /// Whether this person has heard of the matter at all.
        ///
        /// The knowledge graph's answer and nothing else. An actor who believes none of the facts
        /// a matter rests on cannot act on it, for the same reason the player cannot report a
        /// theft they never learned about.
        /// </summary>
        private static bool KnowsOfIt(NarrativeWorldState world, EntityId who, NarrativeThread thread)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (world.Knowledge.Knows(who, thread.FactIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How much of this matter is theirs. Zero means nothing here is, and nobody acts on zero.
        ///
        /// Two sources, both already in the simulation: the trouble is about them, or they hold a
        /// goal that names somebody or something the matter names. No third source is invented -
        /// an actor with no stated stake stays out of it rather than being given one.
        /// </summary>
        private static double Motive(NarrativeWorldState world, EntityId actor, NarrativeThread thread, Fact matter)
        {
            if (matter != null && (matter.Subject == actor || matter.Object == actor))
            {
                return 1.0;
            }

            NarrativeNpc npc = world.Registry.GetNpc(actor);
            if (npc == null)
            {
                return 0.0;
            }

            double best = 0.0;
            for (int i = 0; i < npc.Goals.Count; i++)
            {
                NpcGoal goal = npc.Goals[i];
                if (goal == null || goal.Satisfied || goal.Subject.IsNone)
                {
                    continue;
                }

                if (!NamesIt(thread, matter, goal.Subject))
                {
                    continue;
                }

                double weight = goal.Weight / 100.0;
                if (weight > best)
                {
                    best = weight > 1.0 ? 1.0 : weight;
                }
            }

            return best;
        }

        private static bool NamesIt(NarrativeThread thread, Fact matter, EntityId subject)
        {
            if (matter != null && (matter.Subject == subject || matter.Object == subject))
            {
                return true;
            }

            return thread.ParticipantIds.Contains(subject)
                   || thread.SiteIds.Contains(subject)
                   || thread.FactIds.Contains(subject);
        }

        /// <summary>
        /// Where the matter is, when anything says so.
        ///
        /// A read and never a guess: the matter's own site if it has one, otherwise wherever the
        /// game says the troubled party is. <see cref="EntityId.None"/> when nothing answered, and
        /// a matter with no place is not thereby somewhere else - the opportunity reading treats
        /// it as unread rather than as distant.
        /// </summary>
        private static EntityId MatterZone(NarrativeWorldState world, IVanillaState vanilla, NarrativeThread thread, Fact matter)
        {
            for (int i = 0; i < thread.SiteIds.Count; i++)
            {
                if (!thread.SiteIds[i].IsNone)
                {
                    return thread.SiteIds[i];
                }
            }

            if (matter == null || matter.Subject.IsNone)
            {
                return EntityId.None;
            }

            return world.Registry.GetNpc(matter.Subject) != null
                ? vanilla.GetZoneOf(matter.Subject)
                : EntityId.None;
        }

        /// <summary>
        /// The routes that could actually end <em>this</em> matter for these two people.
        ///
        /// Two filters, and neither of them is a list of helpful verbs kept beside the library.
        ///
        /// The first is the verb's own <see cref="NarrativeAction.SettlesMatters"/>. Autonomy is
        /// the world solving its own problems, so a verb that cannot close a matter is not a way
        /// of solving one - and availability cannot tell the difference on its own, because a
        /// shortage makes bribing the hungry person exactly as applicable as buying them food.
        ///
        /// The second is a differential, and needs no declaration at all: availability is a pure
        /// question, so it can be asked twice - once with the matter in hand, and once about the
        /// same two people with nothing said about it. A verb open on any day is not open
        /// <em>because of</em> this matter, however capable of ending one it is in general;
        /// performing for a crowd can end a festival and is not an answer to a famine.
        ///
        /// Both filters are about which routes exist, never about which will work. A route that
        /// could fail is still a route, which is what makes an autonomous attempt an attempt.
        /// </summary>
        private static List<ActionOffer> RoutesToEndIt(ActionRegistry registry, ActionContext aboutIt, ActionContext anyDay)
        {
            List<ActionOffer> routes = new List<ActionOffer>();
            List<ActionOffer> offers = registry.Discover(aboutIt);

            for (int i = 0; i < offers.Count; i++)
            {
                ActionOffer offer = offers[i];
                if (!offer.Action.SettlesMatters)
                {
                    continue;
                }

                if (anyDay != null && offer.Action.GetAvailability(anyDay).IsAvailable)
                {
                    continue;
                }

                routes.Add(offer);
            }

            return routes;
        }

        private static InterventionOption Weigh(
            NarrativeWorldState world,
            ActionOffer offer,
            EntityId actor,
            EntityId target,
            InterventionOpportunity opportunity,
            double motive,
            NarrativeThread thread)
        {
            NarrativeNpc npc = world.Registry.GetNpc(actor);
            ProblemSolvingStyle style = InterventionStyles.For(offer.Action.Family);
            double styleFit = npc == null ? 0.0 : npc.ProblemSolving.Get(style);

            // The actor's own stake is the pressure a breakable line is weighed against, exactly
            // as the goal pipeline weighs a personal line against need pressure (BQ-077).
            ProhibitionRuling ruling = NegativeSpace.Rule(
                npc?.NegativeSpace,
                style,
                motive,
                "their stake in " + thread.ArchetypeId);

            double score = opportunity.Plausibility * (0.25 + (0.5 * styleFit) + (0.5 * motive));

            List<string> terms = new List<string>
            {
                "opportunity " + Number(opportunity.Plausibility),
                "reads as " + style + ", which they favour " + Number(styleFit),
                motive > 0.0 ? "their stake in it " + Number(motive) : "nothing here is theirs: 0",
            };

            return new InterventionOption(
                actor, target, offer.Action.Id, offer.Availability, style,
                opportunity.Plausibility, styleFit, motive, score, ruling, terms,
                OffScreenBar(offer.Action));
        }

        /// <summary>
        /// What an off-screen actor may not be asked to do, and why.
        ///
        /// One rule, and it is a capability answer rather than a design preference. A verb
        /// declaring <see cref="EmbodimentMode.Delegated"/> says its physical half is carried by a
        /// write vanilla performs on somebody's body - an object destroyed, a load carried, a way
        /// broken open. Nobody has watched such a write behave for an actor the game is not
        /// running, and BQ-093 was explicit that a coarse resolution exists precisely because no
        /// vanilla path for embodying an absent actor has been verified. So the pass does not ask,
        /// and says which branch it declined rather than resolving the physical act quietly.
        ///
        /// <see cref="EmbodimentMode.Coarse"/> is not barred: claiming nothing physical is exactly
        /// what an off-screen resolution is entitled to do. Neither is
        /// <see cref="EmbodimentMode.Narrative"/>, which never had a body in it.
        /// </summary>
        private static string OffScreenBar(NarrativeAction action)
        {
            if (action.Embodiment.Mode != EmbodimentMode.Delegated)
            {
                return string.Empty;
            }

            return "it needs vanilla to move a body through " + action.Embodiment.LeansOn
                   + ", and no such write has been verified for an actor the game is not running";
        }

        /// <summary>
        /// Higher scores win, and an exact tie is broken by verb id so the same world answers the
        /// same way twice however the registry was iterated.
        /// </summary>
        private static bool Beats(InterventionOption option, InterventionOption best)
        {
            if (option.Score > best.Score)
            {
                return true;
            }

            if (option.Score < best.Score)
            {
                return false;
            }

            return string.CompareOrdinal(option.ActionId, best.ActionId) < 0;
        }

        private static string Number(double value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Which of the personality layer's dispositions a verb's family reads as (BQ-094).
    ///
    /// Coarse on purpose, and the coarseness is the honest part. A verb has no declared
    /// disposition - BQ owns sixty-eight of them and inventing sixty-eight judgements about what
    /// kind of move each one is would be a second personality vocabulary standing beside
    /// <see cref="ProblemSolvingProfile"/>, which is the drift the shared library exists to avoid.
    /// What a verb does declare is its <see cref="ActionFamily"/>, and a family is a real
    /// statement about the kind of move it is.
    ///
    /// So this is a reading, it is named as one in the trace, and it is used only to rank options
    /// somebody could take anyway. It decides nothing about whether a verb applies, what it costs
    /// or what it does - those are the verb's own answers - and a step that later needs a finer
    /// reading should give the verbs a declaration rather than growing a table here.
    /// </summary>
    public static class InterventionStyles
    {
        public static ProblemSolvingStyle For(ActionFamily family)
        {
            switch (family)
            {
                case ActionFamily.Social:
                    return ProblemSolvingStyle.AskFriends;
                case ActionFamily.Information:
                    return ProblemSolvingStyle.DoItSelf;
                case ActionFamily.Crime:
                    return ProblemSolvingStyle.Conceal;
                case ActionFamily.Economic:
                    return ProblemSolvingStyle.PaySomeone;
                case ActionFamily.Physical:
                    return ProblemSolvingStyle.Confront;
                case ActionFamily.Crafting:
                    return ProblemSolvingStyle.DoItSelf;
                case ActionFamily.MagicFaith:
                    return ProblemSolvingStyle.SeekReligiousHelp;
                case ActionFamily.HomeCommunity:
                    return ProblemSolvingStyle.DoItSelf;
                default:
                    throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown action family.");
            }
        }
    }
}
