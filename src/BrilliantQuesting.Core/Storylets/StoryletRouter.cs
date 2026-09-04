using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// One beat as it actually went: who was there, what they decided, how the check came out,
    /// what was said, what history recorded, and where the scene went next.
    ///
    /// Everything on it is a record of something the layers below already produced. The act is the
    /// one <see cref="ActorIntent"/> chose, the line is the one <see cref="DialogueRealizer"/>
    /// selected, the check is the one <see cref="ICheckResolver"/> resolved, and the consequences
    /// are events the ledger already accepted. Nothing here is a second source of any of them.
    /// </summary>
    public sealed class PlayedBeat
    {
        private static readonly string[] Nothing = new string[0];

        internal PlayedBeat(
            StoryletBeat beat,
            EntityId speaker,
            EntityId listener,
            IntentChoice choice,
            CheckResult check,
            RealizedLine line,
            IReadOnlyList<string> consequences,
            IReadOnlyList<string> intersections,
            BeatRoute route,
            string skipped)
        {
            Beat = beat;
            Speaker = speaker;
            Listener = listener;
            Choice = choice;
            Check = check;
            Line = line;
            Consequences = consequences ?? Nothing;
            PlayerIntersections = intersections ?? Nothing;
            Route = route;
            Skipped = skipped ?? string.Empty;
        }

        public StoryletBeat Beat { get; }

        public string BeatId => Beat.Id;

        public EntityId Speaker { get; }

        public EntityId Listener { get; }

        /// <summary>What the speaker weighed and what they picked, or null for a beat nobody speaks in.</summary>
        public IntentChoice Choice { get; }

        public SpeechAct Act => Choice?.Act;

        public CheckResult Check { get; }

        /// <summary>
        /// The words, when a realizer was supplied and had any. Null is an ordinary outcome: a
        /// scene plays perfectly well with nobody rendering it, which is what makes the routing
        /// layer testable without content.
        /// </summary>
        public RealizedLine Line { get; }

        public IReadOnlyList<string> Consequences { get; }

        public IReadOnlyList<string> PlayerIntersections { get; }

        /// <summary>The route taken out of this beat, or null when the scene stopped here.</summary>
        public BeatRoute Route { get; }

        /// <summary>Why the beat did not play at all, or empty when it did.</summary>
        public string Skipped { get; }

        public bool Played => Skipped.Length == 0;

        public override string ToString()
        {
            return BeatId + (Played ? (Act == null ? " (silent)" : " " + Act.Type) : " (skipped: " + Skipped + ")");
        }
    }

    /// <summary>One scene, played through.</summary>
    public sealed class StoryletPlay
    {
        internal StoryletPlay(StoryletFiring firing, IReadOnlyList<PlayedBeat> beats, string resolution, string refusal)
        {
            Firing = firing;
            Beats = beats ?? new PlayedBeat[0];
            Resolution = resolution ?? string.Empty;
            Refusal = refusal ?? string.Empty;
        }

        /// <summary>The durable record on the thread, or null when the scene could not be played.</summary>
        public StoryletFiring Firing { get; }

        public IReadOnlyList<PlayedBeat> Beats { get; }

        /// <summary>Which declared terminal state it stopped in, or empty when it stopped without one.</summary>
        public string Resolution { get; }

        public string Refusal { get; }

        public bool Played => Refusal.Length == 0;
    }

    /// <summary>Everything a scene needs to be played, and nothing it could play without.</summary>
    public sealed class StoryletPlayContext
    {
        public StoryletPlayContext(NarrativeWorldState world, IVanillaState vanilla, NarrativeThread thread)
        {
            World = world;
            Vanilla = vanilla;
            Thread = thread;
        }

        public NarrativeWorldState World { get; }

        public IVanillaState Vanilla { get; }

        public NarrativeThread Thread { get; }

        /// <summary>
        /// Whether this is happening where people can hear. Read by <see cref="ActorIntent"/>, and
        /// the reason a timid actor takes a matter aside that a bold one names in the street.
        /// </summary>
        public bool InPublic { get; set; }

        /// <summary>
        /// The stream every decision, check and wording is forked from. One stream, so a scene
        /// replays identically from a seed however many other scenes ran first.
        /// </summary>
        public DeterministicRng Rng { get; set; }

        /// <summary>
        /// Whether authoritative consequences are applied. False plays the scene for inspection -
        /// the same routes, the same decisions, the same lines, and nothing written to the world.
        /// </summary>
        public bool ApplyConsequences { get; set; } = true;

        /// <summary>
        /// The most beats one scene may play, so a route cycle is a bounded scene rather than a
        /// hang. Reaching it stops the scene without a resolution, which is a visible outcome
        /// rather than a silent one.
        /// </summary>
        public int MaxBeats { get; set; } = 32;
    }

    /// <summary>
    /// Plays a cast storylet through its own beats (BQ-146).
    ///
    /// The missing middle of the pipeline. Before this, a storylet could be found, cast and fired,
    /// and firing wrote down a list of beat labels; anything that actually happened in the scene
    /// had to be written by a caller who already knew what those labels meant, which is precisely
    /// the pressure that turns storylets into scripts or into bespoke C#. This walks the beats
    /// instead, and every decision inside the walk is delegated:
    ///
    /// <list type="bullet">
    /// <item>who is in a role - <see cref="StoryletCasting"/>, already done before we are called;</item>
    /// <item>what an actor tries to communicate - <see cref="ActorIntent"/>, from their own state;</item>
    /// <item>whether the act is well formed - <see cref="SpeechAct.Compose"/>, which refuses rather than repairs;</item>
    /// <item>how an uncertainty resolves - <see cref="ICheckResolver"/>, the one the action library already uses;</item>
    /// <item>what the words are - <see cref="DialogueRealizer"/>, over authored content;</item>
    /// <item>what a consequence costs - the event ledger and <c>ConsequenceEngine</c>.</item>
    /// </list>
    ///
    /// What is left is routing, and routing is the whole of this file: pick the next beat whose
    /// requirements hold, let its speaker decide, resolve what is in doubt, record what happened,
    /// take the first route whose trigger fired. There is no storylet-specific branch anywhere in
    /// it and there must never be one - the moment this file mentions a storylet by name, the
    /// forty-first storylet has stopped being cheaper than the sixth.
    ///
    /// <b>It authors nothing on its own.</b> The only writes are the events a beat's consequences
    /// declare, and those go through <c>NarrativeWorldState.Record</c> so that memory, affinity,
    /// knowledge propagation and thread tension all happen where they already happen. Wording
    /// writes nothing at all, as ever: a scene played with no realizer reaches exactly the same
    /// routes and leaves exactly the same history.
    /// </summary>
    public sealed class StoryletRouter
    {
        private readonly DialogueRealizer _realizer;
        private readonly ICheckResolver _checks;

        public StoryletRouter(DialogueRealizer realizer = null, ICheckResolver checks = null)
        {
            _realizer = realizer;
            _checks = checks;
        }

        /// <summary>
        /// Plays a cast opportunity, recording the firing on the thread exactly as
        /// <see cref="StoryletEngine.Fire"/> does - the beats it writes down are the ones actually
        /// reached rather than every beat declared, which is the difference between a record of a
        /// scene and a copy of its definition.
        /// </summary>
        public StoryletPlay Play(StoryletOpportunity opportunity, StoryletPlayContext context)
        {
            if (opportunity == null || context == null || context.World == null || context.Thread == null)
            {
                return new StoryletPlay(null, null, string.Empty, "there is no scene to play");
            }

            if (!opportunity.IsAvailable)
            {
                return new StoryletPlay(null, null, string.Empty, opportunity.RefusalReason);
            }

            StoryletDefinition definition = opportunity.Definition;
            Fact focus = context.World.Knowledge.GetFact(opportunity.FocusFactId);
            GameTime now = context.Vanilla == null ? GameTime.Zero : context.Vanilla.Now;
            DeterministicRng rng = context.Rng ?? context.World.Rng;

            StoryletFiring firing = new StoryletFiring(definition.Id, opportunity.FocusFactId, now);
            foreach (KeyValuePair<string, EntityId> binding in opportunity.RoleBindings)
            {
                firing.RoleBindings[binding.Key] = binding.Value;
            }

            List<PlayedBeat> played = new List<PlayedBeat>();
            string resolution = string.Empty;
            HashSet<string> guard = new HashSet<string>(StringComparer.Ordinal);

            StoryletBeat beat = definition.Beats.Count == 0 ? null : definition.Beats[0];
            int steps = 0;
            while (beat != null && steps < context.MaxBeats)
            {
                steps++;
                guard.Add(beat.Id);

                PlayedBeat outcome = PlayBeat(definition, beat, opportunity, context, focus, now, rng, firing);
                played.Add(outcome);

                if (outcome.Route == null)
                {
                    break;
                }

                if (outcome.Route.IsTerminal)
                {
                    resolution = outcome.Route.Ends;
                    break;
                }

                StoryletBeat next = definition.Beat(outcome.Route.To);
                if (next == null)
                {
                    break;
                }

                beat = next;
            }

            for (int i = 0; i < played.Count; i++)
            {
                if (played[i].Played)
                {
                    firing.BeatIds.Add(played[i].BeatId);
                }
            }

            // The storylet-level hooks stay exactly what they were: markers recorded on every
            // firing. Beat-level consequences are added where they fired, in order, so a reader
            // can tell "this scene is of a kind that applies pressure" from "this pressure was
            // actually applied here".
            for (int i = 0; i < definition.ConsequenceHooks.Count; i++)
            {
                firing.ConsequenceHookIds.Add(definition.ConsequenceHooks[i].Id);
            }

            for (int i = 0; i < played.Count; i++)
            {
                for (int j = 0; j < played[i].Consequences.Count; j++)
                {
                    firing.ConsequenceHookIds.Add(played[i].Consequences[j]);
                }
            }

            context.Thread.StoryletFirings.Add(firing);
            return new StoryletPlay(firing, played, resolution, string.Empty);
        }

        private PlayedBeat PlayBeat(
            StoryletDefinition definition,
            StoryletBeat beat,
            StoryletOpportunity opportunity,
            StoryletPlayContext context,
            Fact focus,
            GameTime now,
            DeterministicRng rng,
            StoryletFiring firing)
        {
            // A beat whose requirements have lapsed is skipped rather than played anyway. The
            // scene still routes out of it, so the world moving under a scene degrades it instead
            // of stopping it - which is what lets a witness walk away mid-conversation without
            // leaving a thread in a state nothing can finish.
            string lapsed = Lapsed(beat, context, focus, opportunity.RoleBindings);
            if (lapsed.Length != 0)
            {
                return new PlayedBeat(beat, EntityId.None, EntityId.None, null, null, null, null, null,
                    Route(beat, null, null), lapsed);
            }

            EntityId speaker = Role(opportunity, beat.SpeakerRole);
            EntityId listener = Role(opportunity, beat.ListenerRole);

            IntentChoice choice = null;
            if (!speaker.IsNone && !listener.IsNone && beat.Intentions.Count > 0)
            {
                choice = ActorIntent.Choose(
                    context.World, context.Vanilla, speaker, listener, focus,
                    beat.Intentions, opportunity.RoleBindings, context.InPublic, beat.Id, rng);
            }

            CheckResult check = Resolve(beat, opportunity, context, rng);
            RealizedLine line = Say(choice, focus, context, opportunity, speaker, listener, rng, beat);
            List<string> consequences = Apply(beat, opportunity, context, focus, now, firing);
            List<string> intersections = new List<string>(beat.PlayerIntersections);

            return new PlayedBeat(beat, speaker, listener, choice, check, line, consequences, intersections,
                Route(beat, choice, check), string.Empty);
        }

        /// <summary>
        /// Why this beat cannot play now, or empty. Reuses the storylet precondition vocabulary
        /// rather than growing a second one, so what an author may ask of a beat and what they may
        /// ask of a scene are the same question in the same words.
        /// </summary>
        private static string Lapsed(
            StoryletBeat beat,
            StoryletPlayContext context,
            Fact focus,
            IReadOnlyDictionary<string, EntityId> roles)
        {
            for (int i = 0; i < beat.Requires.Count; i++)
            {
                string reason = StoryletEngine.WhyPreconditionFails(
                    beat.Requires[i], context.World, context.Vanilla, context.Thread, focus, roles);
                if (reason != null)
                {
                    return reason;
                }
            }

            return string.Empty;
        }

        private CheckResult Resolve(
            StoryletBeat beat,
            StoryletOpportunity opportunity,
            StoryletPlayContext context,
            DeterministicRng rng)
        {
            if (beat.Check == null || _checks == null)
            {
                return null;
            }

            CheckProfile profile = ProceduralCheckProfiles.ById(beat.Check.ProfileId);
            EntityId actor = Role(opportunity, beat.Check.ActorRole);
            if (profile == null || actor.IsNone)
            {
                return null;
            }

            CheckRequest request = new CheckRequest(profile, actor, Role(opportunity, beat.Check.TargetRole));
            return _checks.Resolve(request, rng.Fork("bq146|check|" + beat.Id + "|" + beat.Check.Question));
        }

        /// <summary>
        /// The words, if anybody is rendering. Everything the realizer narrows on is read here
        /// from the world - the names, the mood, the tie - because the realizer holds no world and
        /// must not start to.
        /// </summary>
        private RealizedLine Say(
            IntentChoice choice,
            Fact focus,
            StoryletPlayContext context,
            StoryletOpportunity opportunity,
            EntityId speaker,
            EntityId listener,
            DeterministicRng rng,
            StoryletBeat beat)
        {
            if (_realizer == null || choice == null || !choice.Spoke)
            {
                return null;
            }

            NarrativeNpc actor = context.World.Registry.AllNpcs.TryGetValue(speaker, out NarrativeNpc npc) ? npc : null;
            GameTime now = context.Vanilla == null ? GameTime.Zero : context.Vanilla.Now;

            List<EntityId> named = new List<EntityId> { speaker, listener };
            foreach (KeyValuePair<string, EntityId> binding in opportunity.RoleBindings)
            {
                named.Add(binding.Value);
            }

            if (focus != null)
            {
                named.Add(focus.Subject);
            }

            RealizationRequest request = new RealizationRequest(choice.Act)
            {
                Claim = focus,
                Cast = DialogueCast.From(context.World, named.ToArray()),
                Feeling = actor == null ? SpeakerFeeling.None : SpeakerFeeling.Of(actor.Emotions, now),
                Tie = SpeakerTie.Of(context.World.Relationships, speaker, listener),
                Rng = rng.Fork("bq146|line|" + beat.Id)
            };

            return _realizer.Realize(request);
        }

        /// <summary>
        /// What history records. A hook with no event is written down and nothing else happens; a
        /// hook with one is appended to the ledger, which is where every consequence in this
        /// codebase already comes from.
        /// </summary>
        private static List<string> Apply(
            StoryletBeat beat,
            StoryletOpportunity opportunity,
            StoryletPlayContext context,
            Fact focus,
            GameTime now,
            StoryletFiring firing)
        {
            List<string> applied = new List<string>();
            for (int i = 0; i < beat.Consequences.Count; i++)
            {
                BeatConsequence consequence = beat.Consequences[i];
                applied.Add(consequence.HookId);

                if (!consequence.Event.HasValue || !context.ApplyConsequences)
                {
                    continue;
                }

                EntityId actor = Role(opportunity, consequence.ActorRole);
                EntityId target = Role(opportunity, consequence.TargetRole);
                if (actor.IsNone)
                {
                    continue;
                }

                context.World.Record(
                    consequence.Event.Value,
                    actor,
                    target,
                    now,
                    consequence.Magnitude,
                    default(EntityId),
                    focus == null ? null : new[] { focus.Id },
                    null,
                    null,
                    null,
                    context.Thread.Id);
            }

            return applied;
        }

        /// <summary>
        /// The first route whose trigger fired, in authored order. Nothing here reads the world:
        /// a route turns on what this beat produced, and questions about the world belong in the
        /// next beat's own requirements.
        /// </summary>
        private static BeatRoute Route(StoryletBeat beat, IntentChoice choice, CheckResult check)
        {
            for (int i = 0; i < beat.Routes.Count; i++)
            {
                if (Fired(beat.Routes[i], choice, check))
                {
                    return beat.Routes[i];
                }
            }

            return null;
        }

        private static bool Fired(BeatRoute route, IntentChoice choice, CheckResult check)
        {
            bool spoke = choice != null && choice.Spoke;
            if (route.Act.HasValue && (!spoke || choice.Act.Type != route.Act.Value))
            {
                return false;
            }

            switch (route.When)
            {
                case BeatTrigger.Always:
                    return true;
                case BeatTrigger.Spoke:
                    return spoke;
                case BeatTrigger.Silent:
                    return !spoke;
                case BeatTrigger.CheckPass:
                    return check != null && check.Outcome.IsSuccess();
                case BeatTrigger.CheckFail:
                    return check != null && !check.Outcome.IsSuccess();
                case BeatTrigger.CheckCriticalPass:
                    return check != null && check.Outcome == CheckOutcome.CriticalPass;
                case BeatTrigger.CheckCriticalFail:
                    return check != null && check.Outcome == CheckOutcome.CriticalFail;
                default:
                    return false;
            }
        }

        private static EntityId Role(StoryletOpportunity opportunity, string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
            {
                return EntityId.None;
            }

            return opportunity.RoleBindings.TryGetValue(roleId, out EntityId bound) ? bound : EntityId.None;
        }
    }
}
