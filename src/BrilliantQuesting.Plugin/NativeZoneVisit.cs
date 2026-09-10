using System;
using BepInEx.Logging;
using HarmonyLib;

namespace BrilliantQuesting.Plugin
{
    /// <summary>Observes completed vanilla catch-up; never invokes Simulate itself.</summary>
    internal static class NativeZoneVisit
    {
        private static Harmony _patch;
        private static Action<Zone> _completed;
        private static ManualLogSource _log;

        internal static void Install(ManualLogSource log, Action<Zone> completed)
        {
            _completed = completed;
            _log = log;
            if (_patch != null) return;
            var method = AccessTools.Method(typeof(Zone), "OnVisit", Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(void))
            {
                log.LogWarning("Zone.OnVisit unavailable; Home return reconciliation needs action fallback.");
                return;
            }
            var patch = new Harmony(ModInfo.Guid + ".zone_visit");
            try
            {
                patch.Patch(method, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(NativeZoneVisit), nameof(AfterVisit))));
                _patch = patch;
                log.LogInfo("Zone.OnVisit completion observer installed; reconciliation follows vanilla catch-up.");
            }
            catch (Exception ex)
            {
                patch.UnpatchSelf();
                log.LogWarning("Zone.OnVisit observer unavailable: " + ex.Message);
            }
        }

        internal static void AfterVisit(Zone __instance)
        {
            try { _completed?.Invoke(__instance); }
            catch (Exception ex) { _log?.LogWarning("Zone visit reconciliation deferred: " + ex.Message); }
        }

        internal static void Uninstall()
        {
            _completed = null;
            _patch?.UnpatchSelf();
            _patch = null;
        }
    }
}
