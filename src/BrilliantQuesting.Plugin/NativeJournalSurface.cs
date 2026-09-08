using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;
using HarmonyLib;

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
        private static string _exportedChronicle;

        internal static bool UseDialogueFallback => !_patchesAvailable || _disabled;

        internal static void Bind(NarrativeWorldState world, ElinVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;

            // A different save is a different life, so the next journal open exports afresh
            // rather than being suppressed by the previous save's text.
            _exportedChronicle = null;
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
            UIContent owned = null;
            try
            {
                if (_disabled || __instance == null || !IsJournal(__instance) || HasBrilliantQuestingTab(__instance))
                {
                    return;
                }

                owned = CreateContent(__instance);
                if (owned == null)
                {
                    _disabled = true;
                    _log?.LogWarning("Native Brilliant Questing journal disabled: no usable journal content template was available. Dialogue/log fallback remains enabled.");
                    return;
                }

                __instance.AddTab(TabId, owned, null, null, TabId);
                object stored = BrilliantQuestingTabContent(__instance);
                _log?.LogInfo("Native Brilliant Questing journal tab added to LayerJournal window "
                              + __instance.GetInstanceID() + "; stored content "
                              + TypeName(stored) + ".");
            }
            catch (Exception ex)
            {
                // Roll back only entries pointing at this attempt's owned content, even if
                // AddTab appended before failing. Never remove or destroy a vanilla object.
                try
                {
                    if (owned != null)
                    {
                        IList tabs = ReadField(ReadField(__instance, "setting"), "tabs") as IList;
                        if (tabs != null)
                            for (int i = tabs.Count - 1; i >= 0; i--)
                                if (ReferenceEquals(ReadField(tabs[i], "content"), owned)) tabs.RemoveAt(i);
                        UnityEngine.Object.DestroyImmediate(owned.gameObject);
                    }
                }
                catch (Exception cleanup)
                {
                    _log?.LogWarning("Native Brilliant Questing journal cleanup failed: " + cleanup.Message);
                }
                FailSurface(ex);
            }
        }

        private static UIContent CreateContent(Window window)
        {
            UIContent template = FirstEnabledContent(window);
            if (template == null)
            {
                return null;
            }

            // Copy only RectTransform values. Never instantiate a quest hierarchy or retain
            // its component/layout references. The Window and its view remain vanilla-owned.
            return NativeJournalRenderer.Create(window, template, RefreshPage, FailSurface, _log);
        }

        private static void RefreshPage(NativeJournalRenderer content)
        {
            content.Render(_world, _vanilla);
            if (_world == null || _vanilla == null) return;
            string text = ChronicleNarrative.Export(_world, _vanilla.PlayerId, _vanilla.Now);
            if (string.Equals(text, _exportedChronicle, StringComparison.Ordinal)) return;
            _exportedChronicle = text;
            foreach (string line in text.Split('\n')) _log?.LogInfo(line);
        }

        private static void FailSurface(Exception ex)
        {
            _disabled = true;
            _log?.LogWarning("Native Brilliant Questing journal failed closed: " + ex.GetType().Name
                + ": " + ex.Message + ". Dialogue/log fallback remains enabled.");
        }

        private static void NormalizeRememberedJournalTab(Window window, Layer initLayer, string phase)
        {
            if (window == null || !IsJournal(window, initLayer))
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

            return ReadField(tab, "content") is NativeJournalRenderer;
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

        private static string StringValue(object value)
        {
            return value == null ? string.Empty : value.ToString();
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

    }
}
