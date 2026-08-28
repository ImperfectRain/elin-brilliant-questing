using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using HarmonyLib;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Projects procedural verbs into ordinary Elin dialogue.
    ///
    /// On this Elin build `EVENT.DramaParseAction` exists but is not published. The narrow
    /// Harmony postfix below attaches at the same point the event was meant to expose: when Drama
    /// copies its accumulated `_choices` into the current talk node.
    /// </summary>
    internal sealed class DramaChoiceProjector
    {
        private const int MaxChoices = 7;
        private static bool _installed;

        private readonly NarrativeWorldState _world;
        private readonly ElinVanillaState _vanilla;
        private readonly ElinBindings _bindings;
        private readonly ElinCheckResolver _checks;
        private readonly ActionRegistry _actions;
        private readonly ManualLogSource _log;
        private readonly DeterministicRng _rng;

        internal DramaChoiceProjector(
            NarrativeWorldState world,
            ElinVanillaState vanilla,
            ElinBindings bindings,
            ElinCheckResolver checks,
            ActionRegistry actions,
            ManualLogSource log)
        {
            _world = world;
            _vanilla = vanilla;
            _bindings = bindings;
            _checks = checks;
            _actions = actions;
            _log = log;
            _rng = world.Rng.Fork("drama");
        }

        internal static DramaChoiceProjector Current { get; set; }

        internal static void Install(ManualLogSource log)
        {
            if (_installed)
            {
                return;
            }

            new Harmony(ModInfo.Guid + ".drama").PatchAll(typeof(DramaChoiceProjector).Assembly);
            _installed = true;
            log.LogInfo("Drama choice projector installed.");
        }

        internal void AddChoices(DramaManager manager, Dictionary<string, string> line)
        {
            if (_world == null || manager?.tg?.chara == null || manager.lastTalk == null)
            {
                return;
            }

            string action = FirstAction(line);
            if (action != "_choices")
            {
                return;
            }

            if (!_bindings.TryGetEntity(manager.tg.chara.uid, out EntityId target))
            {
                return;
            }

            NarrativeThread thread = FindThread(target);
            if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out EntityId subjectItem))
            {
                return;
            }

            if (AlreadyProjected(manager.lastTalk))
            {
                return;
            }

            ActionContext context = Context(thread, target, subjectFact, subjectItem);
            List<ActionOffer> offers = _actions.Discover(context, includeUnavailable: true);
            int added = 0;

            foreach (ActionOffer offer in offers)
            {
                if (!offer.Availability.IsAvailable)
                {
                    _log.LogInfo("Drama hides " + offer.Action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": " + offer.Availability.Reason);
                    continue;
                }

                if (added >= MaxChoices)
                {
                    break;
                }

                NarrativeAction actionToRun = offer.Action;
                string text = ChoiceText(actionToRun, context);
                DramaChoice choice = new DramaChoice(text, "", "bq:" + actionToRun.Id, "", "")
                    .SetOnClick(() => Perform(manager, thread, target, subjectFact, subjectItem, actionToRun));
                manager.lastTalk.AddChoice(choice);
                added++;
            }

            if (added > 0)
            {
                _log.LogInfo("Projected " + added + " Brilliant Questing option(s) for "
                             + _world.Registry.NameOf(target) + ".");
            }
        }

        private void Perform(
            DramaManager manager,
            NarrativeThread thread,
            EntityId target,
            EntityId subjectFact,
            EntityId subjectItem,
            NarrativeAction action)
        {
            try
            {
                ActionContext context = Context(thread, target, subjectFact, subjectItem);
                Availability availability = action.GetAvailability(context);
                if (!availability.IsAvailable)
                {
                    string blocked = action.Label + ": " + availability.Reason;
                    Msg.SayRaw(blocked);
                    _log.LogInfo("Drama refused " + action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": " + availability.Reason);
                    return;
                }

                ActionOutcome outcome = action.Perform(context);
                string summary = action.Label + ": " + outcome.Narration;
                Msg.SayRaw(summary);
                _log.LogInfo("> dialogue " + action.Id + " " + _world.Registry.NameOf(target));
                foreach (string line in outcome.Explain().Split('\n'))
                {
                    _log.LogInfo("    " + line);
                }

                manager?.sequence?.Exit();
            }
            catch (Exception ex)
            {
                Msg.SayRaw("Brilliant Questing action failed; see the log.");
                _log.LogError("Drama action failed: " + ex);
            }
        }

        private ActionContext Context(NarrativeThread thread, EntityId target, EntityId subjectFact, EntityId subjectItem)
        {
            ActionContext context = new ActionContext(_world, _vanilla, _checks, _rng, _vanilla.PlayerId, target)
            {
                Thread = thread,
                SubjectFact = subjectFact,
                SubjectItem = subjectItem,
                ThirdParty = ChooseThirdParty(thread, target)
            };

            EntityId zone = _vanilla.GetZoneOf(_vanilla.PlayerId);
            IReadOnlyList<EntityId> present = _vanilla.GetCharactersInZone(zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i] != _vanilla.PlayerId && present[i] != target)
                {
                    context.Witnesses.Add(present[i]);
                }
            }

            return context;
        }

        private NarrativeThread FindThread(EntityId target)
        {
            for (int i = 0; i < _world.Threads.Count; i++)
            {
                NarrativeThread thread = _world.Threads[i];
                if (thread.ArchetypeId == "petty_theft" && thread.ParticipantIds.Contains(target)
                    && thread.IsLive)
                {
                    return thread;
                }
            }

            return null;
        }

        private bool TryBuildFocus(NarrativeThread thread, out EntityId subjectFact, out EntityId subjectItem)
        {
            subjectFact = EntityId.None;
            subjectItem = EntityId.None;

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (!_world.Knowledge.Facts.TryGetValue(thread.FactIds[i], out Fact fact))
                {
                    continue;
                }

                if (fact.Predicate == FactPredicates.Stole)
                {
                    subjectFact = fact.Id;
                    subjectItem = fact.Object;
                    return true;
                }
            }

            return false;
        }

        private EntityId ChooseThirdParty(NarrativeThread thread, EntityId target)
        {
            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                EntityId participant = thread.ParticipantIds[i];
                if (participant != target)
                {
                    return participant;
                }
            }

            return EntityId.None;
        }

        private string ChoiceText(NarrativeAction action, ActionContext context)
        {
            CheckProfile profile = ProfileFor(action.Id);
            if (profile == null)
            {
                return action.Label;
            }

            string difficulty = _checks.DescribeDifficulty(new CheckRequest(profile, context.Actor, context.Target), true);
            return string.IsNullOrEmpty(difficulty) ? action.Label : action.Label + " (" + difficulty + ")";
        }

        private static CheckProfile ProfileFor(string actionId)
        {
            switch (actionId)
            {
                case "question": return ProceduralCheckProfiles.Interrogation;
                case "persuade": return ProceduralCheckProfiles.Persuasion;
                case "lie": return ProceduralCheckProfiles.Deception;
                case "intimidate": return ProceduralCheckProfiles.Intimidation;
                case "bribe": return ProceduralCheckProfiles.Bribery;
                case "search": return ProceduralCheckProfiles.Investigation;
                case "expose": return ProceduralCheckProfiles.Credibility;
                case "pickpocket": return ProceduralCheckProfiles.Pickpocketing;
                case "frame": return ProceduralCheckProfiles.Fabrication;
                default: return null;
            }
        }

        private static string FirstAction(Dictionary<string, string> line)
        {
            if (line == null || !line.TryGetValue("action", out string value) || string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int slash = value.IndexOf('/');
            return slash < 0 ? value : value.Substring(0, slash);
        }

        private static bool AlreadyProjected(DramaEventTalk talk)
        {
            for (int i = 0; i < talk.choices.Count; i++)
            {
                if (talk.choices[i].idAction != null && talk.choices[i].idAction.StartsWith("bq:", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(DramaManager), nameof(DramaManager.ParseLine))]
        private static class DramaManagerParseLinePatch
        {
            private static void Postfix(DramaManager __instance, Dictionary<string, string> item)
            {
                Current?.AddChoices(__instance, item);
            }
        }
    }
}
