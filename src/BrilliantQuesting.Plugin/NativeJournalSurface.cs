using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
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
        private static bool _reportedTemplateContent;
        private static bool _reportedContentLifecycleInstantiate;
        private static bool _reportedContentLifecycleSwitch;
        private static bool _reportedContentRefresh;
        private static bool _reportedContentBuilt;
        private static bool _reportedContentMount;

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
            MethodInfo buildTabs = AccessTools.Method(typeof(Window), nameof(Window.BuildTabs), new[] { typeof(int) });
            MethodInfo init = AccessTools.Method(typeof(Window), "Init");
            MethodInfo onKill = AccessTools.Method(typeof(Window), "OnKill");
            MethodInfo buildPrefix = AccessTools.Method(typeof(NativeJournalSurface), nameof(BeforeBuildTabs));
            MethodInfo initPrefix = AccessTools.Method(typeof(NativeJournalSurface), nameof(BeforeInit));
            MethodInfo killPostfix = AccessTools.Method(typeof(NativeJournalSurface), nameof(AfterOnKill));
            if (buildTabs == null || init == null || onKill == null
                || buildPrefix == null || initPrefix == null || killPostfix == null)
            {
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Native Brilliant Questing journal disabled: Window.BuildTabs/Init/OnKill could not all be resolved. Dialogue/log fallback remains enabled.");
                return;
            }

            try
            {
                harmony.Patch(buildTabs, prefix: new HarmonyMethod(buildPrefix));
                harmony.Patch(init, prefix: new HarmonyMethod(initPrefix));
                harmony.Patch(onKill, postfix: new HarmonyMethod(killPostfix));
                _installed = true;
                _patchesAvailable = true;
                log.LogInfo("Native Brilliant Questing journal patch installed with LayerJournal tab-memory guard.");
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Native Brilliant Questing journal disabled after patch failure: " + ex.GetType().Name + ": " + ex.Message + ". Dialogue/log fallback remains enabled.");
            }
        }

        private static void BeforeInit(Window __instance, Layer _layer)
        {
            try
            {
                NormalizeRememberedJournalTab(__instance, _layer, "before Init");
            }
            catch (Exception ex)
            {
                _log?.LogWarning("Native Brilliant Questing journal memory guard skipped before Init: "
                                 + ex.GetType().Name + ": " + ex.Message + ".");
            }
        }

        private static void AfterOnKill(Window __instance)
        {
            try
            {
                NormalizeRememberedJournalTab(__instance, null, "after OnKill");
            }
            catch (Exception ex)
            {
                _log?.LogWarning("Native Brilliant Questing journal memory guard skipped after OnKill: "
                                 + ex.GetType().Name + ": " + ex.Message + ".");
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
                object stored = BrilliantQuestingTabContent(__instance);
                _log?.LogInfo("Native Brilliant Questing journal tab added to LayerJournal window "
                              + __instance.GetInstanceID() + "; stored content "
                              + TypeName(stored) + ".");
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

            UIContent instantiated = Util.Instantiate(template, window.view);
            if (instantiated == null)
            {
                return null;
            }

            GameObject gameObject = instantiated.gameObject;
            gameObject.name = "BrilliantQuestingJournalContent";
            gameObject.SetActive(false);

            if (!_reportedTemplateContent)
            {
                _reportedTemplateContent = true;
                _log?.LogInfo("Native Brilliant Questing journal content template type: "
                              + TypeName(template) + ".");
            }

            Dictionary<string, object> copied = CopyFields(
                instantiated,
                "target",
                "prof",
                "skinType",
                "idDefaultText",
                "layout");
            UnityEngine.Object.DestroyImmediate(instantiated);

            BrilliantQuestingJournalContent content = gameObject.AddComponent<BrilliantQuestingJournalContent>();
            ApplyFields(content, copied);
            content.OnInstantiate();
            if (!_reportedContentMount)
            {
                _reportedContentMount = true;
                _log?.LogInfo("Native Brilliant Questing journal content mounted after Util.Instantiate: parentIsWindowViewTransform="
                              + (window.view != null && content.transform.parent == window.view.transform)
                              + "; isPrefab="
                              + content.IsPrefab()
                              + "; UIContent components="
                              + ContentComponentTypes(gameObject)
                              + ".");
            }

            return content;
        }

        private static void NormalizeRememberedJournalTab(Window window, Layer initLayer, string phase)
        {
            if (_disabled || window == null || !IsJournal(window, initLayer))
            {
                return;
            }

            object setting = ReadField(window, "setting");
            IList tabs = ReadField(setting, "tabs") as IList;
            IDictionary remembered = ReadRememberedTabs(window);
            object idWindow = WindowKey(window, initLayer);
            if (tabs == null || remembered == null || idWindow == null || !remembered.Contains(idWindow))
            {
                return;
            }

            int index = ToInt(remembered[idWindow], -1);
            bool dynamicTab = index >= 0 && index < tabs.Count && IsBrilliantQuestingTab(tabs[index]);
            if (!DynamicTabMemoryPolicy.ShouldResetRememberedTab(index, tabs.Count, dynamicTab))
            {
                return;
            }

            remembered[idWindow] = 0;
            _log?.LogInfo("Native Brilliant Questing journal reset remembered LayerJournal tab "
                          + index + " to vanilla tab 0 " + phase + ".");
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
                if (IsBrilliantQuestingTab(tab))
                {
                    return true;
                }
            }

            return false;
        }

        private static object BrilliantQuestingTabContent(Window window)
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
                if (StringValue(ReadField(tab, "idLang")) == TabId)
                {
                    return ReadField(tab, "content");
                }
            }

            return null;
        }

        private static bool IsJournal(Window window)
        {
            return IsJournal(window, null);
        }

        private static bool IsJournal(Window window, Layer initLayer)
        {
            if (initLayer is LayerJournal)
            {
                return true;
            }

            if (window.GetComponentInParent<LayerJournal>() != null)
            {
                return true;
            }

            object controller = ReadField(window, "controller");
            return controller != null
                   && controller.GetType().Name.IndexOf("Journal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static object WindowKey(Window window, Layer initLayer)
        {
            int windowIndex = window.windowIndex;
            Layer layer = initLayer ?? window.layer;
            string layerUid = layer == null ? null : layer.uid;
            return DynamicTabMemoryPolicy.WindowKey(layerUid, windowIndex);
        }

        private static bool IsBrilliantQuestingTab(object tab)
        {
            if (StringValue(ReadField(tab, "idLang")) == TabId)
            {
                return true;
            }

            return ReadField(tab, "content") is BrilliantQuestingJournalContent;
        }

        private static IDictionary ReadRememberedTabs(Window window)
        {
            object local = ReadField(window, "dictTab");
            if (local is IDictionary localDict)
            {
                return localDict;
            }

            FieldInfo field = AccessTools.Field(typeof(Window), "dictTab");
            return field == null ? null : field.GetValue(field.IsStatic ? null : window) as IDictionary;
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

        private static Dictionary<string, object> CopyFields(object instance, params string[] names)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            if (instance == null || names == null)
            {
                return values;
            }

            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = AccessTools.Field(instance.GetType(), names[i]);
                if (field != null)
                {
                    values[names[i]] = field.GetValue(instance);
                }
            }

            return values;
        }

        private static void ApplyFields(object instance, Dictionary<string, object> values)
        {
            if (instance == null || values == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in values)
            {
                FieldInfo field = AccessTools.Field(instance.GetType(), pair.Key);
                if (field != null)
                {
                    field.SetValue(instance, pair.Value);
                }
            }
        }

        private static string StringValue(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static string ContentComponentTypes(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "none";
            }

            UIContent[] contents = gameObject.GetComponents<UIContent>();
            if (contents == null || contents.Length == 0)
            {
                return "none";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < contents.Length; i++)
            {
                names.Add(TypeName(contents[i]));
            }

            return string.Join(", ", names.ToArray());
        }

        private static string TypeName(object value)
        {
            return value == null ? "null" : value.GetType().FullName;
        }

        private static int ToInt(object value, int fallback)
        {
            if (value is int number)
            {
                return number;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private sealed class BrilliantQuestingJournalContent : UIContent
        {
            public override void OnInstantiate()
            {
                if (!_reportedContentLifecycleInstantiate)
                {
                    _reportedContentLifecycleInstantiate = true;
                    _log?.LogInfo("Native Brilliant Questing journal content OnInstantiate invoked.");
                }

                Refresh();
            }

            public override void OnSwitchContent(int idTab)
            {
                if (!_reportedContentLifecycleSwitch)
                {
                    _reportedContentLifecycleSwitch = true;
                    _log?.LogInfo("Native Brilliant Questing journal content OnSwitchContent invoked for tab " + idTab + ".");
                }

                Refresh();
            }

            private void Refresh()
            {
                if (!_reportedContentRefresh)
                {
                    _reportedContentRefresh = true;
                    _log?.LogInfo("Native Brilliant Questing journal content Refresh invoked.");
                }

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
                if (!_reportedContentBuilt)
                {
                    _reportedContentBuilt = true;
                    _log?.LogInfo("Native Brilliant Questing journal content Build completed with "
                                  + world.Registry.Npcs.Count + " people, "
                                  + world.Ledger.Count + " events, "
                                  + world.Threads.Count + " thread(s).");
                }
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
