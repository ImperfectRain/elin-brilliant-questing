using System;
using System.Collections.Generic;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    // Transient adapter probe results; never saved. The drill can only remove support.
    internal sealed class VanillaCapabilityReport
    {
        private readonly HashSet<VanillaCapability> _available = new HashSet<VanillaCapability>();
        private readonly Dictionary<VanillaCapability, string> _evidence = new Dictionary<VanillaCapability, string>();
        private readonly VanillaCapability? _disabled;

        internal VanillaCapabilityReport(string disabled, Action<string> log)
        {
            string name = (disabled ?? string.Empty).Trim();
            if (name.Length == 0) return;
            // Match names explicitly: enum parsing also accepts numeric values and comma lists.
            foreach (VanillaCapability capability in Enum.GetValues(typeof(VanillaCapability)))
                if (string.Equals(name, capability.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    _disabled = capability;
                    log("BQ-109 drill: disabling " + capability + "; native probe skipped. Restart with an empty DisabledCapability to restore normal detection.");
                    return;
                }
            log("BQ-109 drill INVALID DisabledCapability='" + name + "'; no capability disabled. Use one exact capability name from the capability report.");
        }

        internal int Count => _available.Count;
        internal bool IsDisabled(VanillaCapability capability) => _disabled == capability;
        internal bool Supports(VanillaCapability capability) => _available.Contains(capability);
        internal void Clear() { _available.Clear(); _evidence.Clear(); }

        internal void Probe(VanillaCapability capability, Func<string> probe, string absentReason = null)
        {
            if (IsDisabled(capability)) { MarkUnsupported(capability, "native probe skipped"); return; }
            try
            {
                string evidence = probe();
                if (string.IsNullOrEmpty(evidence))
                    MarkUnsupported(capability, absentReason ?? "probe returned no runtime object");
                else
                {
                    _available.Add(capability);
                    _evidence[capability] = evidence;
                }
            }
            catch (Exception ex) { MarkUnsupported(capability, ex.GetType().Name + ": " + ex.Message); }
        }

        internal void MarkUnsupported(VanillaCapability capability, string reason)
        {
            _available.Remove(capability);
            _evidence[capability] = IsDisabled(capability)
                ? "disabled by BQ-109 drill; " + reason
                : "unsupported: " + reason;
        }

        internal void Report(Action<string> log)
        {
            foreach (VanillaCapability capability in Enum.GetValues(typeof(VanillaCapability)))
            {
                _evidence.TryGetValue(capability, out string evidence);
                log("  capability " + capability + ": " + (Supports(capability) ? "available" : "unavailable")
                    + " - " + (evidence ?? "not probed"));
            }
        }
    }
}
