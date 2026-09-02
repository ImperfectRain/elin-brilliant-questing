using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Storylets
{
    public sealed class StoryletEngine
    {
        private readonly List<StoryletDefinition> _definitions = new List<StoryletDefinition>();

        public void Register(StoryletDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _definitions.Add(definition);
        }

        public IReadOnlyList<StoryletOpportunity> Find(
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            EntityId focusFactId)
        {
            return Find(Context(world, vanilla, thread, actor, target, focusFactId));
        }

        /// <summary>
        /// Every storylet that can play here, cast from whoever qualifies in the context's place.
        ///
        /// The route that does not require the caller to have decided who anybody is: a thread, a
        /// fact and a town are enough, and the same definitions produce a different cast in the
        /// next town without a word of content changing.
        /// </summary>
        public IReadOnlyList<StoryletOpportunity> Find(StoryletCastingContext context)
        {
            List<StoryletOpportunity> opportunities = new List<StoryletOpportunity>();
            for (int i = 0; i < _definitions.Count; i++)
            {
                StoryletOpportunity opportunity = Evaluate(_definitions[i], context);
                if (opportunity.IsAvailable)
                {
                    opportunities.Add(opportunity);
                }
            }

            return opportunities;
        }

        public StoryletFiring Fire(
            StoryletOpportunity opportunity,
            NarrativeThread thread,
            GameTime now)
        {
            if (opportunity == null)
            {
                throw new ArgumentNullException(nameof(opportunity));
            }

            if (thread == null)
            {
                throw new ArgumentNullException(nameof(thread));
            }

            if (!opportunity.IsAvailable)
            {
                throw new InvalidOperationException("Cannot fire unavailable storylet: " + opportunity.RefusalReason);
            }

            StoryletFiring firing = new StoryletFiring(opportunity.Definition.Id, opportunity.FocusFactId, now);
            foreach (KeyValuePair<string, EntityId> binding in opportunity.RoleBindings)
            {
                firing.RoleBindings[binding.Key] = binding.Value;
            }

            for (int i = 0; i < opportunity.Definition.Beats.Count; i++)
            {
                firing.BeatIds.Add(opportunity.Definition.Beats[i].Id);
            }

            for (int i = 0; i < opportunity.Definition.ConsequenceHooks.Count; i++)
            {
                firing.ConsequenceHookIds.Add(opportunity.Definition.ConsequenceHooks[i].Id);
            }

            thread.StoryletFirings.Add(firing);
            return firing;
        }

        public static StoryletOpportunity Evaluate(
            StoryletDefinition definition,
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            EntityId focusFactId)
        {
            return Evaluate(definition, Context(world, vanilla, thread, actor, target, focusFactId));
        }

        public static StoryletOpportunity Evaluate(StoryletDefinition definition, StoryletCastingContext context)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            NarrativeWorldState world = context.World;
            IVanillaState vanilla = context.Vanilla;
            NarrativeThread thread = context.Thread;
            EntityId focusFactId = context.FocusFactId;

            if (world == null || vanilla == null || thread == null)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "there is no live world context");
            }

            // The subject the caller named is the one checked hardest, exactly as before; a scene
            // cast entirely from the place names nobody, and the thread itself is what is checked.
            if (!SceneStatus.Check(world, vanilla, thread, context.Target).IsPlayable)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "scene preconditions no longer hold");
            }

            Fact focus = world.Knowledge.GetFact(focusFactId);
            if (focus == null)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "focus fact no longer exists");
            }

            StoryletCastingResult casting = StoryletCasting.Cast(definition, context, focus);
            if (!casting.IsCast)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "required role " + casting.UncastRequiredRole + " cannot be cast");
            }

            for (int i = 0; i < definition.Preconditions.Count; i++)
            {
                string reason = CheckPrecondition(definition.Preconditions[i], world, vanilla, thread, focus, casting.Bindings);
                if (reason != null)
                {
                    return StoryletOpportunity.Refused(definition, focusFactId, reason);
                }
            }

            return StoryletOpportunity.Available(definition, focusFactId, casting);
        }

        private static StoryletCastingContext Context(
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            EntityId focusFactId)
        {
            StoryletCastingContext context = new StoryletCastingContext(world, vanilla, thread, focusFactId);
            context.Actor = actor;
            context.Target = target;
            return context;
        }

        private static string CheckPrecondition(
            StoryletPrecondition precondition,
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            Fact focus,
            Dictionary<string, EntityId> roles)
        {
            switch (precondition.Kind)
            {
                case StoryletPreconditionKind.FactBelongsToThread:
                    return thread.FactIds.Contains(focus.Id) ? null : "focus fact is no longer part of the thread";
                case StoryletPreconditionKind.FocusPredicate:
                    return string.Equals(focus.Predicate, precondition.Value, StringComparison.Ordinal)
                        ? null
                        : "focus predicate is not " + precondition.Value;
                case StoryletPreconditionKind.FocusTruth:
                    return string.Equals(focus.Truth.ToString(), precondition.Value, StringComparison.Ordinal)
                        ? null
                        : "focus truth is not " + precondition.Value;
                case StoryletPreconditionKind.RoleKnowsFocus:
                    return TryGetRole(roles, precondition.Value, out EntityId knower)
                           && world.Knowledge.Knows(knower, focus.Id)
                        ? null
                        : "role " + precondition.Value + " does not know the focus";
                case StoryletPreconditionKind.RoleCanProveFocus:
                    return TryGetRole(roles, precondition.Value, out EntityId prover)
                           && world.Knowledge.CanProve(prover, focus.Id)
                        ? null
                        : "role " + precondition.Value + " cannot prove the focus";
                case StoryletPreconditionKind.RoleAlive:
                    return TryGetRole(roles, precondition.Value, out EntityId actor)
                           && vanilla.IsAlive(actor)
                        ? null
                        : "role " + precondition.Value + " is not alive";
                default:
                    return "unknown storylet precondition";
            }
        }

        private static bool TryGetRole(Dictionary<string, EntityId> roles, string roleId, out EntityId actor)
        {
            actor = EntityId.None;
            return !string.IsNullOrEmpty(roleId) && roles.TryGetValue(roleId, out actor) && !actor.IsNone;
        }
    }
}
