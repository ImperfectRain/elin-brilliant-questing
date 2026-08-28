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

            ProjectChoices(manager, manager.lastTalk);
        }

        internal void ProjectChoices(DramaManager manager, DramaEventTalk talk)
        {
            if (_world == null || manager?.tg?.chara == null || talk == null)
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

            ApplySituationText(talk, thread, target, subjectFact);

            if (AlreadyProjected(talk))
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
                string text = SafeChoiceText(actionToRun, context);
                DramaChoice choice = new DramaChoice(text, "", "bq:" + actionToRun.Id, "", "")
                    .SetOnClick(() => Perform(manager, thread, target, subjectFact, subjectItem, actionToRun));
                talk.AddChoice(choice);
                added++;
            }

            if (added > 0)
            {
                _log.LogInfo("Projected " + added + " Brilliant Questing option(s) for "
                             + _world.Registry.NameOf(target) + ".");
            }
        }

        private string SafeChoiceText(NarrativeAction action, ActionContext context)
        {
            try
            {
                return ChoiceText(action, context);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not describe dialogue option '" + action.Id + "': " + ex.Message);
                return action.Label;
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
                string summary = action.Label + ": " + outcome.Narration + " Talk again to continue.";
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
            string text = "BQ: " + action.Label;
            return string.IsNullOrEmpty(difficulty) ? text : text + " (" + difficulty + ")";
        }

        private void ApplySituationText(DramaEventTalk talk, NarrativeThread thread, EntityId target, EntityId theftFactId)
        {
            string text = SituationText(thread, target, theftFactId);
            if (string.IsNullOrEmpty(text) || talk.text == text)
            {
                return;
            }

            talk.text = text;
            talk.funcText = () => text;
            _log.LogInfo("Applied Brilliant Questing situation text for " + _world.Registry.NameOf(target) + ".");
        }

        internal bool TryReplaceRenderedText(ref string text)
        {
            DramaManager manager = LayerDrama.Instance?.drama;
            if (_world == null || manager?.tg?.chara == null)
            {
                return false;
            }

            if (!_bindings.TryGetEntity(manager.tg.chara.uid, out EntityId target))
            {
                return false;
            }

            NarrativeThread thread = FindThread(target);
            if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out _))
            {
                return false;
            }

            string replacement = SituationText(thread, target, subjectFact);
            if (string.IsNullOrEmpty(replacement))
            {
                return false;
            }

            text = replacement;
            _log.LogInfo("Rendered Brilliant Questing situation text for " + _world.Registry.NameOf(target) + ".");
            return true;
        }

        private string SituationText(NarrativeThread thread, EntityId target, EntityId theftFactId)
        {
            Fact theft = _world.Knowledge.GetFact(theftFactId);
            if (theft == null)
            {
                return "Something is wrong here, but the details are still unclear.";
            }

            EntityId victim = FindVictim(thread, theft.Object);
            EntityId witness = FindWitness(theftFactId, theft.Subject);
            string victimName = _world.Registry.NameOf(victim);
            string thiefName = _world.Registry.NameOf(theft.Subject);
            string targetName = _world.Registry.NameOf(target);
            string item = string.IsNullOrEmpty(theft.Value) ? "a missing item" : theft.Value;

            List<string> lines = new List<string>
            {
                "A local theft is unfolding.",
                victimName + " is missing " + item + ". Someone nearby knows more than they are saying."
            };

            if (target == victim)
            {
                lines.Add(targetName + " is the injured party. They want the property recovered, but cannot prove who took it.");
            }
            else if (target == theft.Subject)
            {
                lines.Add(targetName + " is tied to the missing " + item + ". Press carefully: confession, proof, theft, or leverage could all move this forward.");
            }
            else if (target == witness)
            {
                lines.Add(targetName + " may have seen what happened, but does not want to be dragged into it.");
            }
            else
            {
                lines.Add(targetName + " is connected to the dispute.");
            }

            if (_world.Knowledge.Knows(_vanilla.PlayerId, theftFactId))
            {
                string proof = _world.Knowledge.CanProve(_vanilla.PlayerId, theftFactId)
                    ? "You can prove it."
                    : "You know the claim, but still lack proof.";
                lines.Add("Current lead: " + thiefName + " stole the " + item + ". " + proof);
            }
            else
            {
                lines.Add("Objective: learn who took the " + item + ", find proof if possible, then decide whether to expose them, return it, keep it, or let the dispute run.");
            }

            if (thread.OpenQuestions.Count > 0)
            {
                lines.Add("Open question: " + thread.OpenQuestions[0]);
            }

            return string.Join("\n", lines);
        }

        private EntityId FindVictim(NarrativeThread thread, EntityId item)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = _world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Possesses && fact.Object == item)
                {
                    return fact.Subject;
                }
            }

            return EntityId.None;
        }

        private EntityId FindWitness(EntityId theftFactId, EntityId thief)
        {
            foreach (EntityId knower in _world.Knowledge.Knowers(theftFactId))
            {
                if (knower == thief)
                {
                    continue;
                }

                if (_world.Knowledge.TryGetBelief(knower, theftFactId, out KnowledgeRecord record)
                    && record.Source == KnowledgeSource.Witnessed)
                {
                    return knower;
                }
            }

            return EntityId.None;
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

        [HarmonyPatch(typeof(DramaEventTalk), nameof(DramaEventTalk.InitDialog))]
        private static class DramaEventTalkInitDialogPatch
        {
            private static void Postfix(DramaEventTalk __instance)
            {
                Current?.ProjectChoices(__instance?.manager, __instance);
            }
        }

        [HarmonyPatch(typeof(DialogDrama), nameof(DialogDrama.SetText))]
        private static class DialogDramaSetTextPatch
        {
            private static void Prefix(ref string text)
            {
                Current?.TryReplaceRenderedText(ref text);
            }
        }
    }
}
