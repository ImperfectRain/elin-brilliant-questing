using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using HarmonyLib;
using UnityEngine;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Adds one derived Brilliant Questing page to Elin's native journal.
    /// </summary>
    internal static class NativeJournalSurface
    {
        private const string TabId = "Brilliant Questing";
        private static bool _installed;
        private static bool _patchesAvailable;
        private static bool _disabled;
        private static ManualLogSource _log;
        private static NarrativeWorldState _world;
        private static ElinVanillaState _vanilla;

        internal static bool UseDialogueFallback => !_patchesAvailable || _disabled;

        internal static void Bind(NarrativeWorldState world, ElinVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        internal static void Install(ManualLogSource log)
        {
            if (_installed)
            {
                return;
            }

            _log = log;
            Harmony harmony = new Harmony(ModInfo.Guid + ".journal_native");
            MethodInfo target = AccessTools.Method(typeof(Window), nameof(Window.BuildTabs), new[] { typeof(int) });
            MethodInfo prefix = AccessTools.Method(typeof(NativeJournalSurface), nameof(BeforeBuildTabs));
            if (target == null || prefix == null)
            {
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Native Brilliant Questing journal disabled: Window.BuildTabs(int) could not be resolved. Dialogue/log fallback remains enabled.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _installed = true;
                _patchesAvailable = true;
                log.LogInfo("Native Brilliant Questing journal patch installed.");
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Native Brilliant Questing journal disabled after patch failure: " + ex.GetType().Name + ": " + ex.Message + ". Dialogue/log fallback remains enabled.");
            }
        }

        private static void BeforeBuildTabs(Window __instance)
        {
            try
            {
                if (_disabled || __instance == null || !IsJournal(__instance) || HasBrilliantQuestingTab(__instance))
                {
                    return;
                }

                UIContent content = CreateContent(__instance);
                if (content == null)
                {
                    _disabled = true;
                    _log?.LogWarning("Native Brilliant Questing journal disabled: no usable journal content template was available. Dialogue/log fallback remains enabled.");
                    return;
                }

                __instance.AddTab(TabId, content, null, null, TabId);
                _log?.LogInfo("Native Brilliant Questing journal tab added to LayerJournal window " + __instance.GetInstanceID() + ".");
            }
            catch (Exception ex)
            {
                _disabled = true;
                _log?.LogWarning("Native Brilliant Questing journal failed closed: " + ex.GetType().Name + ": " + ex.Message + ". Vanilla journal remains untouched after this point; dialogue/log fallback remains enabled.");
            }
        }

        private static UIContent CreateContent(Window window)
        {
            UIContent template = FirstEnabledContent(window);
            if (template == null)
            {
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject);
            clone.name = "BrilliantQuestingJournalContent";
            clone.SetActive(false);

            UIContent clonedTemplate = clone.GetComponent<UIContent>();
            BrilliantQuestingJournalContent content = clone.AddComponent<BrilliantQuestingJournalContent>();
            if (clonedTemplate != null)
            {
                content.target = clonedTemplate.target;
                content.prof = clonedTemplate.prof;
                content.skinType = clonedTemplate.skinType;
                content.idDefaultText = clonedTemplate.idDefaultText;
                content.layout = clonedTemplate.layout;
            }

            return content;
        }

        private static UIContent FirstEnabledContent(Window window)
        {
            object setting = ReadField(window, "setting");
            IList tabs = ReadField(setting, "tabs") as IList;
            if (tabs == null)
            {
                return null;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                object tab = tabs[i];
                if (ReadField(tab, "content") is UIContent content && content != null && !IsDisabled(tab))
                {
                    return content;
                }
            }

            return null;
        }

        private static bool HasBrilliantQuestingTab(Window window)
        {
            object setting = ReadField(window, "setting");
            IList tabs = ReadField(setting, "tabs") as IList;
            if (tabs == null)
            {
                return false;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                object tab = tabs[i];
                if (StringValue(ReadField(tab, "idLang")) == TabId)
                {
                    return true;
                }

                if (ReadField(tab, "content") is BrilliantQuestingJournalContent)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsJournal(Window window)
        {
            if (window.GetComponentInParent<LayerJournal>() != null)
            {
                return true;
            }

            object controller = ReadField(window, "controller");
            return controller != null
                   && controller.GetType().Name.IndexOf("Journal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDisabled(object tab)
        {
            object disabled = ReadField(tab, "disable");
            return disabled is bool value && value;
        }

        private static object ReadField(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field == null ? null : field.GetValue(instance);
        }

        private static string StringValue(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private sealed class BrilliantQuestingJournalContent : UIContent
        {
            public override void OnInstantiate()
            {
                Refresh();
            }

            public override void OnSwitchContent(int idTab)
            {
                Refresh();
            }

            private void Refresh()
            {
                Clear();
                AddHeader("Brilliant Questing", null);

                NarrativeWorldState world = _world;
                ElinVanillaState vanilla = _vanilla;
                if (world == null || vanilla == null)
                {
                    AddText("No Brilliant Questing state is loaded.", FontColor.Default);
                    Build();
                    return;
                }

                EntityId player = vanilla.PlayerId;
                AddActiveMatters(world, player);
                AddKnownPeople(world, player);
                AddKnownClaims(world, player);
                AddResolvedMatters(world, player);
                Build();
            }

            private void AddActiveMatters(NarrativeWorldState world, EntityId player)
            {
                AddHeader("Active content", null);
                IReadOnlyList<NarrativeContentEntry> entries = NarrativeContentProjection.Entries(world, player);
                if (entries.Count == 0)
                {
                    AddText("No active matters.", FontColor.Default);
                    return;
                }

                AddContentGroup(world, player, entries, NarrativeContentClass.Situation, "Situations");
                AddContentGroup(world, player, entries, NarrativeContentClass.Request, "Requests");
                AddContentGroup(world, player, entries, NarrativeContentClass.Opportunity, "Opportunities");
                AddContentGroup(world, player, entries, NarrativeContentClass.Event, "Events");
            }

            private void AddContentGroup(
                NarrativeWorldState world,
                EntityId player,
                IReadOnlyList<NarrativeContentEntry> entries,
                NarrativeContentClass contentClass,
                string heading)
            {
                bool any = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    NarrativeContentEntry entry = entries[i];
                    if (entry.ContentClass != contentClass)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        AddHeader(heading, null);
                        any = true;
                    }

                    AddText(entry.Title + (string.IsNullOrEmpty(entry.Detail) ? string.Empty : "  " + entry.Detail), FontColor.Topic);
                    if (contentClass == NarrativeContentClass.Situation)
                    {
                        NarrativeThread thread = world.GetThread(entry.ThreadId);
                        if (thread != null)
                        {
                            AddCaseNotes(world, player, thread);
                        }
                    }
                }
            }

            private void AddCaseNotes(NarrativeWorldState world, EntityId player, NarrativeThread thread)
            {
                IReadOnlyList<JournalEntry> entries = KnownEntriesForThread(world, player, thread);
                if (entries.Count == 0)
                {
                    AddText("You do not know enough to summarize this matter yet.", FontColor.Default);
                    return;
                }

                AddText("Known people: " + Names(world, KnownPeopleIn(world, entries)), FontColor.Default);
                for (int i = 0; i < entries.Count; i++)
                {
                    JournalEntry entry = entries[i];
                    AddText(EntryLine(entry), entry.CanProve ? FontColor.Good : FontColor.Default);
                }
            }

            private void AddKnownPeople(NarrativeWorldState world, EntityId player)
            {
                AddHeader("Known people", null);
                IReadOnlyList<JournalEntry> entries = NarrativeJournal.Entries(world, player);
                List<EntityId> known = KnownPeopleIn(world, entries);
                for (int i = 0; i < known.Count; i++)
                {
                    NarrativeNpc npc = world.Registry.GetNpc(known[i]);
                    AddText(npc.Name + "  " + npc.Importance, FontColor.Default);
                }

                if (known.Count == 0)
                {
                    AddText("No one tied to a known claim yet.", FontColor.Default);
                }
            }

            private void AddKnownClaims(NarrativeWorldState world, EntityId player)
            {
                AddHeader("Known claims and proof", null);
                IReadOnlyList<JournalEntry> entries = NarrativeJournal.Entries(world, player);
                if (entries.Count == 0)
                {
                    AddText("Nothing known yet.", FontColor.Default);
                    return;
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    JournalEntry entry = entries[i];
                    AddText(EntryLine(entry), entry.CanProve ? FontColor.Good : FontColor.Default);
                }
            }

            private void AddResolvedMatters(NarrativeWorldState world, EntityId player)
            {
                AddHeader("Resolved matters", null);
                IReadOnlyList<ChronicleEntry> entries = Chronicle.Entries(world, player);
                if (entries.Count == 0)
                {
                    AddText("Nothing resolved yet.", FontColor.Default);
                    return;
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    ChronicleEntry entry = entries[i];
                    AddText(entry.ArchetypeId + "  " + Words(entry.Outcome) + "  day " + entry.ResolvedAt.TotalDays, FontColor.Topic);
                    for (int a = 0; a < entry.WhatThePlayerDid.Count; a++)
                    {
                        ChronicleAct act = entry.WhatThePlayerDid[a];
                        AddText("You: " + Words(act.Type.ToString()) + (act.Towards.IsNone ? string.Empty : " - " + world.Registry.NameOf(act.Towards)), FontColor.Default);
                    }
                }
            }

            private static string EntryLine(JournalEntry entry)
            {
                return "[" + entry.Tag + "] " + entry.Text + " (" + entry.Source + ", confidence "
                       + entry.Confidence.ToString("0.00") + (entry.CanProve ? ", proof)" : ", no proof)");
            }

            private static IReadOnlyList<JournalEntry> KnownEntriesForThread(NarrativeWorldState world, EntityId player, NarrativeThread thread)
            {
                List<JournalEntry> known = new List<JournalEntry>();
                IReadOnlyList<JournalEntry> entries = NarrativeJournal.Entries(world, player);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (thread.FactIds.Contains(entries[i].FactId))
                    {
                        known.Add(entries[i]);
                    }
                }

                return known;
            }

            private static List<EntityId> KnownPeopleIn(NarrativeWorldState world, IReadOnlyList<JournalEntry> entries)
            {
                List<EntityId> known = new List<EntityId>();
                for (int i = 0; i < entries.Count; i++)
                {
                    Fact fact = world.Knowledge.GetFact(entries[i].FactId);
                    if (fact == null)
                    {
                        continue;
                    }

                    AddKnownPerson(world, known, fact.Subject);
                    AddKnownPerson(world, known, fact.Object);
                }

                return known;
            }

            private static void AddKnownPerson(NarrativeWorldState world, List<EntityId> known, EntityId id)
            {
                if (id.IsNone || world.Registry.GetNpc(id) == null || known.Contains(id))
                {
                    return;
                }

                known.Add(id);
            }

            private static string Names(NarrativeWorldState world, IReadOnlyList<EntityId> ids)
            {
                if (ids == null || ids.Count == 0)
                {
                    return "none";
                }

                List<string> names = new List<string>();
                for (int i = 0; i < ids.Count; i++)
                {
                    names.Add(world.Registry.NameOf(ids[i]));
                }

                return string.Join(", ", names);
            }

            private static string Words(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "resolved";
                }

                return value.Replace('_', ' ');
            }
        }
    }
}
