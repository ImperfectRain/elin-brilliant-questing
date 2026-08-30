using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// BQ-138 spike: read the native journal tab lifecycle before adding BQ content.
    ///
    /// The verified API identifies the tab machinery, but not whether a custom content object can
    /// render dynamic BQ lists without clipping or lifecycle leaks. This probe is deliberately
    /// read-only and fail-closed: it observes `Window.BuildTabs` for `LayerJournal` windows and
    /// logs the shape needed by the next implementation pass.
    /// </summary>
    internal static class JournalShapeProbe
    {
        private static readonly HashSet<int> SeenWindows = new HashSet<int>();
        private static bool _installed;
        private static bool _patchesAvailable;
        private static ManualLogSource _log;

        internal static bool PatchesAvailable => _patchesAvailable;

        internal static void Install(ManualLogSource log)
        {
            if (_installed)
            {
                return;
            }

            _log = log;
            Harmony harmony = new Harmony(ModInfo.Guid + ".journal_probe");
            MethodInfo target = AccessTools.Method(typeof(Window), nameof(Window.BuildTabs), new[] { typeof(int) });
            MethodInfo prefix = AccessTools.Method(typeof(JournalShapeProbe), nameof(BeforeBuildTabs));
            if (target == null || prefix == null)
            {
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Journal shape probe disabled: Window.BuildTabs(int) could not be resolved. Vanilla journal is untouched.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _installed = true;
                _patchesAvailable = true;
                log.LogInfo("Journal shape probe installed. Native BQ journal tabs remain disabled until this probe is verified in game.");
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                _installed = true;
                _patchesAvailable = false;
                log.LogInfo("Journal shape probe disabled after patch failure: " + ex.GetType().Name + ": " + ex.Message + ". Vanilla journal is untouched.");
            }
        }

        private static void BeforeBuildTabs(Window __instance)
        {
            try
            {
                if (__instance == null || !IsJournal(__instance))
                {
                    return;
                }

                int id = __instance.GetInstanceID();
                if (SeenWindows.Contains(id))
                {
                    return;
                }

                SeenWindows.Add(id);
                LogJournalShape(__instance);
            }
            catch (Exception ex)
            {
                _log?.LogWarning("Journal shape probe failed closed: " + ex.GetType().Name + ": " + ex.Message);
            }
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

        private static void LogJournalShape(Window window)
        {
            _log?.LogInfo("BQ-138 JournalShapeProbe: LayerJournal window " + window.GetInstanceID()
                          + " before BuildTabs; selected idTab=" + ReadField(window, "idTab") + ".");

            object setting = ReadField(window, "setting");
            IList tabs = ReadField(setting, "tabs") as IList;
            int tabCount = tabs == null ? 0 : tabs.Count;
            _log?.LogInfo("BQ-138 JournalShapeProbe: setting.tabs count before build = " + tabCount + ".");

            for (int i = 0; i < tabCount; i++)
            {
                object tab = tabs[i];
                object content = ReadField(tab, "content");
                object disabled = ReadField(tab, "disable");
                _log?.LogInfo("BQ-138 JournalShapeProbe: tab[" + i + "] idLang="
                              + StringValue(ReadField(tab, "idLang"))
                              + " disabled=" + StringValue(disabled)
                              + " content=" + (content == null ? "<null>" : content.GetType().FullName) + ".");
            }
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
            return value == null ? "<null>" : value.ToString();
        }
    }
}
