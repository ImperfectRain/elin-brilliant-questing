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
            List<StoryletOpportunity> opportunities = new List<StoryletOpportunity>();
            for (int i = 0; i < _definitions.Count; i++)
            {
                StoryletOpportunity opportunity = Evaluate(_definitions[i], world, vanilla, thread, actor, target, focusFactId);
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
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (world == null || vanilla == null || thread == null)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "there is no live world context");
            }

            if (!SceneStatus.Check(world, vanilla, thread, target).IsPlayable)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "scene preconditions no longer hold");
            }

            Fact focus = world.Knowledge.GetFact(focusFactId);
            if (focus == null)
            {
                return StoryletOpportunity.Refused(definition, focusFactId, "focus fact no longer exists");
            }

            Dictionary<string, EntityId> roles = BindRoles(definition, world, thread, actor, target, focus);
            for (int i = 0; i < definition.RequiredRoles.Count; i++)
            {
                if (!roles.ContainsKey(definition.RequiredRoles[i].Id))
                {
                    return StoryletOpportunity.Refused(definition, focusFactId, "required role " + definition.RequiredRoles[i].Id + " cannot be cast");
                }
            }

            for (int i = 0; i < definition.Preconditions.Count; i++)
            {
                string reason = CheckPrecondition(definition.Preconditions[i], world, vanilla, thread, focus, roles);
                if (reason != null)
                {
                    return StoryletOpportunity.Refused(definition, focusFactId, reason);
                }
            }

            return StoryletOpportunity.Available(definition, focusFactId, roles);
        }

        private static Dictionary<string, EntityId> BindRoles(
            StoryletDefinition definition,
            NarrativeWorldState world,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            Fact focus)
        {
            Dictionary<string, EntityId> roles = new Dictionary<string, EntityId>();
            BindRoles(definition.RequiredRoles, world, thread, actor, target, focus, roles);
            BindRoles(definition.OptionalRoles, world, thread, actor, target, focus, roles);
            return roles;
        }

        private static void BindRoles(
            IReadOnlyList<StoryletRole> definitions,
            NarrativeWorldState world,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            Fact focus,
            Dictionary<string, EntityId> roles)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                EntityId binding = ResolveRole(definitions[i].Source, world, thread, actor, target, focus);
                if (!binding.IsNone)
                {
                    roles[definitions[i].Id] = binding;
                }
            }
        }

        private static EntityId ResolveRole(
            StoryletRoleSource source,
            NarrativeWorldState world,
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            Fact focus)
        {
            switch (source)
            {
                case StoryletRoleSource.Actor:
                    return actor;
                case StoryletRoleSource.Target:
                    return target;
                case StoryletRoleSource.FactSubject:
                    return focus.Subject;
                case StoryletRoleSource.FactObject:
                    return focus.Object;
                case StoryletRoleSource.AnyParticipantWhoKnowsFocus:
                    for (int i = 0; i < thread.ParticipantIds.Count; i++)
                    {
                        EntityId participant = thread.ParticipantIds[i];
                        if (world.Knowledge.Knows(participant, focus.Id))
                        {
                            return participant;
                        }
                    }

                    return EntityId.None;
                default:
                    return EntityId.None;
            }
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
