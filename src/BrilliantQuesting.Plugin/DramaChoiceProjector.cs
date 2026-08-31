using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
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
        private static bool _patchesAvailable;

        private readonly NarrativeWorldState _world;
        private readonly ElinVanillaState _vanilla;
        private readonly ElinBindings _bindings;
        private readonly ElinCheckResolver _checks;
        private readonly ActionRegistry _actions;
        private readonly ManualLogSource _log;

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
        }

        internal static DramaChoiceProjector Current { get; set; }

        internal static void Install(ManualLogSource log)
        {
            if (_installed)
            {
                return;
            }

            Harmony harmony = new Harmony(ModInfo.Guid + ".drama");
            bool ok = true;
            ok &= TryPatch(
                harmony,
                log,
                "DramaManager.ParseLine",
                AccessTools.Method(typeof(DramaManager), nameof(DramaManager.ParseLine), new[] { typeof(Dictionary<string, string>) }),
                postfix: AccessTools.Method(typeof(DramaManagerParseLinePatch), nameof(DramaManagerParseLinePatch.Postfix)));
            ok &= TryPatch(
                harmony,
                log,
                "DramaEventTalk.InitDialog",
                AccessTools.Method(typeof(DramaEventTalk), nameof(DramaEventTalk.InitDialog)),
                postfix: AccessTools.Method(typeof(DramaEventTalkInitDialogPatch), nameof(DramaEventTalkInitDialogPatch.Postfix)));
            ok &= TryPatch(
                harmony,
                log,
                "DialogDrama.SetText",
                AccessTools.Method(typeof(DialogDrama), nameof(DialogDrama.SetText), new[] { typeof(string), typeof(bool) }),
                prefix: AccessTools.Method(typeof(DialogDramaSetTextPatch), nameof(DialogDramaSetTextPatch.Prefix)));

            if (!ok)
            {
                harmony.UnpatchSelf();
                _patchesAvailable = false;
                _installed = true;
                log.LogInfo("Drama choice projector disabled because one or more Drama patches could not be applied. Vanilla dialogue is untouched.");
                return;
            }

            _patchesAvailable = true;
            _installed = true;
            log.LogInfo("Drama choice projector installed.");
        }

        internal static bool PatchesAvailable => _patchesAvailable;

        private static bool TryPatch(
            Harmony harmony,
            ManualLogSource log,
            string name,
            System.Reflection.MethodInfo target,
            System.Reflection.MethodInfo prefix = null,
            System.Reflection.MethodInfo postfix = null)
        {
            if (target == null)
            {
                log.LogWarning("Drama patch skipped: could not find " + name + ".");
                return false;
            }

            if (prefix == null && postfix == null)
            {
                log.LogWarning("Drama patch skipped: could not find Brilliant Questing patch method for " + name + ".");
                return false;
            }

            try
            {
                harmony.Patch(
                    target,
                    prefix == null ? null : new HarmonyMethod(prefix),
                    postfix == null ? null : new HarmonyMethod(postfix));
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning("Drama patch skipped: " + name + " could not be patched (" + ex.GetType().Name + ": " + ex.Message + ").");
                return false;
            }
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
            if (_world == null || manager?.tg?.chara == null || talk == null || !IsDefaultTalk(manager))
            {
                return;
            }

            if (!_bindings.TryGetEntity(manager.tg.chara.uid, out EntityId target))
            {
                return;
            }

            AdvanceThreads?.Invoke();

            if (AlreadyProjected(talk))
            {
                return;
            }

            // BQ-036: asking somebody what has been going on is not about them, so it is offered
            // before anything is known about a thread and to people who are in none. It is the
            // one option here that a stranger in a tavern can answer.
            OfferTheNews(manager, talk, target);

            NarrativeThread thread = FindThread(target);
            if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out EntityId subjectItem))
            {
                return;
            }

            // BQ-008: the world may have moved since the thread was written. Offering a route
            // through somebody who is dead produces a screen of options that each refuse for
            // their own reason, which reads as a bug rather than as a situation.
            SceneStatus scene = SceneStatus.Check(_world, _vanilla, thread, target);
            if (!scene.IsPlayable)
            {
                _log.LogInfo("Drama offered nothing for " + _world.Registry.NameOf(target)
                             + ": " + scene.Reason + ".");
                return;
            }

            ApplySituationText(talk, target);

            ActionContext context = Context(thread, target, subjectFact, subjectItem);
            List<ActionOffer> available = new List<ActionOffer>();
            foreach (ActionOffer offer in _actions.Discover(context, includeUnavailable: true))
            {
                if (offer.Availability.IsAvailable)
                {
                    available.Add(offer);
                }
                else
                {
                    _log.LogInfo("Drama hides " + offer.Action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": " + offer.Availability.Reason);
                }
            }

            // Reading the case notes changes nothing, so it is deliberately outside the scope
            // and stays clickable however many times the player wants it.
            DramaChoice notes = new DramaChoice("Review case notes", "", "bq:notes", "", "")
                .SetOnClick(() => ShowCaseNotes(target));
            talk.AddChoice(notes);

            if (NativeJournalSurface.UseDialogueFallback)
            {
                DramaChoice journal = new DramaChoice("Open Brilliant Questing journal", "", "bq:journal", "", "")
                    .SetOnClick(ShowJournal);
                talk.AddChoice(journal);

                DramaChoice chronicle = new DramaChoice("Review resolved matters", "", "bq:chronicle", "", "")
                    .SetOnClick(ShowChronicle);
                talk.AddChoice(chronicle);
            }

            if (ExplainInDialogue)
            {
                DramaChoice why = new DramaChoice("Why? (debug)", "", "bq:why", "", "")
                    .SetOnClick(() => Explain(target));
                talk.AddChoice(why);
            }

            ResolutionScope scope = new ResolutionScope(target);
            List<ActionIntentOption> offered = ContextualActionProjection.Project(available, context, MaxChoices);
            for (int i = 0; i < offered.Count; i++)
            {
                NarrativeAction actionToRun = offered[i].Action;
                string text = SafeChoiceText(offered[i], context);
                DramaChoice choice = new DramaChoice(text, "", "bq:" + actionToRun.Id, "", "")
                    .SetOnClick(() => Perform(manager, scope, target, actionToRun));
                talk.AddChoice(choice);
            }

            if (offered.Count > 0)
            {
                _log.LogInfo("Projected " + offered.Count + " Brilliant Questing option(s) for "
                             + _world.Registry.NameOf(target) + ".");
            }

            if (available.Count > offered.Count)
            {
                _log.LogInfo("Drama held back " + (available.Count - offered.Count)
                             + " lower-priority option(s) for " + _world.Registry.NameOf(target)
                             + " to stay within " + MaxChoices + " choices.");
            }
        }

        private string SafeChoiceText(ActionIntentOption option, ActionContext context)
        {
            try
            {
                return ChoiceText(option, context);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not describe dialogue option '" + option.Action.Id + "': " + ex.Message);
                return option.Action.Label;
            }
        }

        /// <summary>Set from config. The inspector is a developer tool, not player-facing text.</summary>
        internal bool ExplainInDialogue { get; set; }

        /// <summary>
        /// Brings threads up to the current date before anything is read off them. Without it the
        /// player can be shown a situation that the calendar says has already moved on.
        /// </summary>
        internal Action AdvanceThreads { get; set; }

        /// <summary>
        /// The town's news, as whoever the player is talking to has heard it. Set from the plugin
        /// once the rumour layer exists; a null one simply means the topic is never offered.
        /// </summary>
        internal TownNews News { get; set; }

        /// <summary>
        /// BQ-012 in game: the whole "why?" report for whoever the player is standing in front of,
        /// written to the log. It goes to the log rather than to `Msg` because it is dozens of
        /// lines and the message window would swallow it; the player gets one line telling them
        /// where to look.
        /// </summary>
        private void Explain(EntityId target)
        {
            try
            {
                NarrativeThread thread = FindThread(target);
                EntityId subjectFact = EntityId.None;
                EntityId subjectItem = EntityId.None;
                if (thread != null)
                {
                    TryBuildFocus(thread, out subjectFact, out subjectItem);
                }

                ActionContext context = Context(thread, target, subjectFact, subjectItem);
                string report = NarrativeInspector.Explain(_world, _vanilla, _actions, context, thread);
                foreach (string line in report.Split('\n'))
                {
                    _log.LogInfo(line);
                }

                Msg.SayRaw("Brilliant Questing wrote a 'why?' report to BepInEx/LogOutput.log.");
            }
            catch (Exception ex)
            {
                Msg.SayRaw("Brilliant Questing could not build the report; see the log.");
                _log.LogError("Inspector failed: " + ex);
            }
        }

        /// <summary>
        /// Offers "what's been happening?" when this person has something to say, and says so in
        /// the log when they have not.
        ///
        /// Hidden rather than greyed out, and hidden only on genuine emptiness: somebody who has
        /// heard nothing the player has not already heard has no answer to give, which is the
        /// same reason the other verbs disappear rather than refuse.
        /// </summary>
        private void OfferTheNews(DramaManager manager, DramaEventTalk talk, EntityId target)
        {
            if (News == null)
            {
                return;
            }

            if (News.Ask(_world, _vanilla, target).Count == 0)
            {
                _log.LogInfo("Drama hides the news topic for " + _world.Registry.NameOf(target)
                             + ": they have heard nothing the player has not.");
                return;
            }

            DramaChoice news = new DramaChoice("What's been happening?", "", "bq:news", "", "")
                .SetOnClick(() => TellTheNews(manager, speaker: target));
            talk.AddChoice(news);
        }

        /// <summary>
        /// They answer, in their own voice and one line at a time.
        ///
        /// The answer is read again here rather than captured when the button was drawn, for the
        /// same reason the verbs re-read their focus: the world can move while a node is on
        /// screen. Each line is spoken before it is allowed to teach the player anything, which is
        /// BQ-035's rule and matters at least as much here - a menu that silently posts beliefs
        /// into the journal is the announcement this whole route exists to do without.
        ///
        /// The conversation ends afterwards, as it does for the verbs: what somebody says out loud
        /// belongs in the world rather than inside a dialogue box, and the player is meant to be
        /// looking at the person who said it.
        /// </summary>
        private void TellTheNews(DramaManager manager, EntityId speaker)
        {
            try
            {
                IReadOnlyList<SpokenRemark> answer = News.Ask(_world, _vanilla, speaker);
                if (answer.Count == 0)
                {
                    Msg.SayRaw(_world.Registry.NameOf(speaker) + " has nothing new to tell you.");
                    return;
                }

                int heard = 0;
                int took = 0;
                for (int i = 0; i < answer.Count; i++)
                {
                    if (!ElinBark.Speak(_bindings, answer[i], _log))
                    {
                        continue;
                    }

                    heard++;
                    if (News.Deliver(_world, _vanilla, answer[i], _vanilla.Now))
                    {
                        took++;
                    }
                }

                _log.LogInfo("News from " + _world.Registry.NameOf(speaker) + ": " + heard + " of "
                             + answer.Count + " line(s) reached the player, " + took + " believed.");
                manager?.sequence?.Exit();
            }
            catch (Exception ex)
            {
                Msg.SayRaw("Brilliant Questing could not ask about the news; see the log.");
                _log.LogError("News topic failed: " + ex);
            }
        }

        private void ShowCaseNotes(EntityId target)
        {
            NarrativeThread thread = FindThread(target);
            if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out _))
            {
                Msg.SayRaw("Case notes: nothing open here any more.");
                return;
            }

            string notes = CaseNotes(thread, subjectFact, target);
            Msg.SayRaw(notes);
            _log.LogInfo("Case notes shown: " + notes);
        }

        private void ShowJournal()
        {
            try
            {
                string journal = NarrativeJournal.Describe(_world, _vanilla.PlayerId);
                foreach (string line in journal.Split('\n'))
                {
                    _log.LogInfo(line);
                }

                Msg.SayRaw("Brilliant Questing journal fallback wrote to BepInEx/LogOutput.log.");
            }
            catch (Exception ex)
            {
                Msg.SayRaw("Brilliant Questing could not build the journal; see the log.");
                _log.LogError("Journal failed: " + ex);
            }
        }

        /// <summary>
        /// What is finished, as against the journal's account of what is open. Same surface and
        /// same failure behaviour as the journal: a reader that throws must not take a
        /// conversation down with it.
        /// </summary>
        private void ShowChronicle()
        {
            try
            {
                string chronicle = Chronicle.Describe(_world, _vanilla.PlayerId);
                foreach (string line in chronicle.Split('\n'))
                {
                    _log.LogInfo(line);
                }

                Msg.SayRaw("Brilliant Questing chronicle fallback wrote to BepInEx/LogOutput.log.");
            }
            catch (Exception ex)
            {
                Msg.SayRaw("Brilliant Questing could not build the chronicle; see the log.");
                _log.LogError("Chronicle failed: " + ex);
            }
        }

        /// <summary>
        /// Rediscovers the live focus before acting. A projected choice sits on a node that can
        /// outlive the situation it was built for, so the thread, the fact and the item are read
        /// again here rather than captured when the button was drawn. Only the NPC is carried
        /// across, because that is the one thing the player actually chose.
        ///
        /// Three things are checked before anything is written, in the order that costs least:
        /// the offering has not already been spent, the person in front of the player is still the
        /// person the option was drawn against, and the situation still exists.
        /// </summary>
        private void Perform(DramaManager manager, ResolutionScope scope, EntityId target, NarrativeAction action)
        {
            try
            {
                if (scope.IsSpent)
                {
                    _log.LogInfo("Drama ignored a repeat of " + action.Id + " vs "
                                 + _world.Registry.NameOf(target) + ": " + scope.SpentBy
                                 + " already resolved this conversation.");
                    return;
                }

                if (!ActorStillPresent(manager, target))
                {
                    Msg.SayRaw(action.Label + ": you are no longer talking to them.");
                    _log.LogInfo("Drama dropped " + action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": the conversation moved to somebody else.");
                    return;
                }

                NarrativeThread thread = FindThread(target);
                if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out EntityId subjectItem))
                {
                    Msg.SayRaw(action.Label + ": that matter is settled.");
                    _log.LogInfo("Drama dropped " + action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": no live thread when the choice was clicked.");
                    manager?.sequence?.Exit();
                    return;
                }

                // The scene is checked again here, not only when the buttons were drawn. A choice
                // can sit on screen while the world changes around it.
                SceneStatus scene = SceneStatus.Check(_world, _vanilla, thread, target);
                if (!scene.IsPlayable)
                {
                    Msg.SayRaw(action.Label + ": " + scene.Reason + ".");
                    _log.LogInfo("Drama dropped " + action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": " + scene.Reason + ".");
                    manager?.sequence?.Exit();
                    return;
                }

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

                if (!scope.TryClaim(target, action.Id, out string refusal))
                {
                    _log.LogInfo("Drama refused " + action.Id + " vs " + _world.Registry.NameOf(target)
                                 + ": " + refusal);
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
            // The world RNG's state is persisted and restored with the save. A Fork is derived
            // from the seed alone, so a forked stream restarts from the beginning every time the
            // save is opened and replays the rolls it already made. Draw from the persisted
            // stream instead, so reloading continues the sequence rather than repeating it.
            ActionContext context = new ActionContext(_world, _vanilla, _checks, _world.Rng, _vanilla.PlayerId, target)
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

        private string ChoiceText(ActionIntentOption option, ActionContext context)
        {
            return option.Label;
        }

        /// <summary>
        /// Installs the situation text as a live function rather than a captured string.
        ///
        /// `DramaEventTalk.Play` prefers `funcText` over `text`, and the node belongs to the game
        /// rather than to us, so a snapshot keeps speaking after the thread resolves and after the
        /// projector has been torn down. This re-reads the world on every render and hands the node
        /// straight back to whatever it said before the moment the situation stops being ours.
        /// </summary>
        private void ApplySituationText(DramaEventTalk talk, EntityId target)
        {
            string original = talk.text;
            Func<string> originalFunc = talk.funcText;
            talk.funcText = () => LiveSituationText(target) ?? (originalFunc != null ? originalFunc() : original);
            _log.LogInfo("Applied Brilliant Questing situation text for " + _world.Registry.NameOf(target) + ".");
        }

        /// <summary>The situation text for this NPC right now, or null when we have nothing to say.</summary>
        private string LiveSituationText(EntityId target)
        {
            if (_world == null || Current != this)
            {
                return null;
            }

            NarrativeThread thread = FindThread(target);
            if (thread == null || !TryBuildFocus(thread, out EntityId subjectFact, out _))
            {
                return null;
            }

            string text = SituationText(thread, target, subjectFact);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        internal bool TryReplaceRenderedText(ref string text)
        {
            DramaManager manager = LayerDrama.Instance?.drama;
            if (_world == null || manager?.tg?.chara == null || !IsDefaultTalk(manager))
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
            bool named = !string.IsNullOrEmpty(theft.Value);
            string anItem = named ? Article(theft.Value) : "something";
            string theItem = named ? "the " + theft.Value : "it";
            string theMissing = named ? "the missing " + theft.Value : "the missing property";

            List<string> lines = new List<string>
            {
                "A local theft is unfolding.",
                victimName + " is missing " + anItem + ". Someone nearby knows more than they are saying."
            };

            if (target == victim)
            {
                lines.Add(targetName + " is the injured party. They want the property recovered, but cannot prove who took it.");
            }
            else if (target == theft.Subject && _world.Knowledge.Knows(_vanilla.PlayerId, theftFactId))
            {
                lines.Add(targetName + " is tied to " + theMissing + ". Press carefully: confession, proof, theft, or leverage could all move this forward.");
            }
            else if (target == witness)
            {
                lines.Add(targetName + " may have seen what happened, but does not want to be dragged into it.");
            }
            else
            {
                lines.Add(targetName + " is one of the people close enough to the dispute to matter.");
            }

            if (_world.Knowledge.Knows(_vanilla.PlayerId, theftFactId))
            {
                string proof = _world.Knowledge.CanProve(_vanilla.PlayerId, theftFactId)
                    ? "You can prove it."
                    : "You know the claim, but still lack proof.";
                lines.Add("Current lead: " + thiefName + " stole " + theItem + ". " + proof);
            }
            else
            {
                lines.Add("Objective: learn who took " + theItem + ", find proof if possible, then decide whether to expose them, return it, keep it, or let the dispute run.");
            }

            string gone = SceneStatus.Check(_world, _vanilla, thread, EntityId.None).DescribeMissing(_world);
            if (!string.IsNullOrEmpty(gone))
            {
                lines.Add(gone);
            }

            if (thread.OpenQuestions.Count > 0)
            {
                lines.Add("Open question: " + thread.OpenQuestions[0]);
            }

            return string.Join("\n", lines);
        }

        private string CaseNotes(NarrativeThread thread, EntityId theftFactId, EntityId target)
        {
            Fact theft = _world.Knowledge.GetFact(theftFactId);
            if (theft == null)
            {
                return "Case notes: the active thread has no readable theft fact.";
            }

            EntityId victim = FindVictim(thread, theft.Object);
            EntityId witness = FindWitness(theftFactId, theft.Subject);
            EntityId knownWitness = WitnessDisclosure.KnownWitnessToPlayer(
                _world.Knowledge,
                _vanilla.PlayerId,
                theftFactId,
                witness,
                target);
            bool named = !string.IsNullOrEmpty(theft.Value);
            string anItem = named ? Article(theft.Value) : "something";
            string theItem = named ? "the " + theft.Value : "it";
            bool playerKnows = _world.Knowledge.Knows(_vanilla.PlayerId, theftFactId);
            string lead = _world.Knowledge.Knows(_vanilla.PlayerId, theftFactId)
                ? "Lead: " + _world.Registry.NameOf(theft.Subject) + " took " + theItem + "."
                : "Lead: unknown.";
            string proof = _world.Knowledge.CanProve(_vanilla.PlayerId, theftFactId)
                ? "Proof: you have evidence."
                : "Proof: not secured.";

            List<string> lines = new List<string>
            {
                "Case notes: " + _world.Registry.NameOf(victim) + " is missing " + anItem + ".",
                "People: " + _world.Registry.NameOf(victim) + " lost it; "
                + WitnessLine(knownWitness) + "; "
                + (playerKnows
                    ? _world.Registry.NameOf(theft.Subject) + " is the current suspect."
                    : "the thief is still unidentified."),
                lead + " " + proof
            };

            if (thread.State == ThreadState.Resolved)
            {
                lines.Add("Status: resolved (" + thread.Resolution + ").");
            }
            else if (_world.Knowledge.Knows(_vanilla.PlayerId, theftFactId)
                     && !_world.Knowledge.CanProve(_vanilla.PlayerId, theftFactId))
            {
                lines.Add("Next: find proof, convince someone, or accept a messy outcome.");
            }
            else if (_world.Knowledge.CanProve(_vanilla.PlayerId, theftFactId))
            {
                lines.Add("Next: return the item to " + _world.Registry.NameOf(victim)
                          + " or tell someone who will believe the proof.");
            }
            else
            {
                lines.Add(knownWitness.IsNone
                    ? "Next: ask around, search the scene, build rapport, or pressure someone."
                    : "Next: ask " + _world.Registry.NameOf(knownWitness)
                      + ", search the scene, build rapport, or pressure someone.");
            }

            return string.Join(" ", lines);
        }

        /// <summary>
        /// "a" or "an" in front of a bare noun. BQ-005a supplied the missing article and hardcoded
        /// "a", which the first live run rendered as "Elna is missing a old signet" - one of the
        /// five valuables the generator picks from starts with a vowel.
        /// </summary>
        private static string Article(string noun)
        {
            if (string.IsNullOrEmpty(noun))
            {
                return noun;
            }

            char first = char.ToLowerInvariant(noun[0]);
            bool vowel = first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u';
            return (vowel ? "an " : "a ") + noun;
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

        private string WitnessLine(EntityId knownWitness)
        {
            return knownWitness.IsNone
                ? "no known witness has come forward"
                : _world.Registry.NameOf(knownWitness) + " may have seen something";
        }

        /// <summary>
        /// True while the character the player is talking to is still the one an option was drawn
        /// against. Drama can change its actor mid-sequence, and a choice already on screen would
        /// otherwise resolve against whoever happens to be speaking now.
        /// </summary>
        private bool ActorStillPresent(DramaManager manager, EntityId target)
        {
            Chara current = manager?.tg?.chara;
            return current != null
                   && _bindings.TryGetEntity(current.uid, out EntityId entity)
                   && entity == target;
        }

        /// <summary>
        /// True only for Elin's ordinary "talk to someone" conversation.
        ///
        /// `Chara.ShowDialog` opens that as book `_chara`, step `main`. Everything authored passes
        /// something else - a quest names its own book, guild clerks use `guild_clerk`, weddings
        /// and worship use `_adv`, a character with its own sheet uses its id, and `_chara` itself
        /// is reused with other steps for sleeping, escorts, bouts and hiring. Gating on the
        /// generic conversation is what keeps the mod out of dialogue somebody wrote.
        /// </summary>
        private static bool IsDefaultTalk(DramaManager manager)
        {
            DramaSetup setup = manager?.setup;
            return setup != null
                   && string.Equals(setup.book, "_chara", StringComparison.Ordinal)
                   && string.Equals(setup.step, "main", StringComparison.Ordinal);
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

        private static class DramaManagerParseLinePatch
        {
            internal static void Postfix(DramaManager __instance, Dictionary<string, string> item)
            {
                if (!_patchesAvailable)
                {
                    return;
                }

                try
                {
                    Current?.AddChoices(__instance, item);
                }
                catch (Exception ex)
                {
                    Current?._log.LogWarning("Brilliant Questing skipped Drama choice projection after an exception: " + ex.Message);
                }
            }
        }

        private static class DramaEventTalkInitDialogPatch
        {
            internal static void Postfix(DramaEventTalk __instance)
            {
                if (!_patchesAvailable)
                {
                    return;
                }

                try
                {
                    Current?.ProjectChoices(__instance?.manager, __instance);
                }
                catch (Exception ex)
                {
                    Current?._log.LogWarning("Brilliant Questing skipped Drama init projection after an exception: " + ex.Message);
                }
            }
        }

        private static class DialogDramaSetTextPatch
        {
            internal static void Prefix(ref string detail)
            {
                if (!_patchesAvailable)
                {
                    return;
                }

                try
                {
                    Current?.TryReplaceRenderedText(ref detail);
                }
                catch (Exception ex)
                {
                    Current?._log.LogWarning("Brilliant Questing left vanilla Drama text unchanged after an exception: " + ex.Message);
                }
            }
        }
    }
}
