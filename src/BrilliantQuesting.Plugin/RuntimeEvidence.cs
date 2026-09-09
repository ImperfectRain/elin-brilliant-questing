using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    // Opt-in, transient instrumentation. No native hooks, saved state or simulation decisions.
    internal sealed class RuntimeEvidence
    {
        internal enum Callback { Act, Attach, Save, Dialogue, Reconcile }
        internal static RuntimeEvidence Current { get; set; }
        private readonly Action<string> _log;
        private readonly double[] _frames = new double[8192];
        private readonly CallbackTotals[] _callbacks = new CallbackTotals[5];
        private int _count;
        private double _previous = -1;
        private double _windowStart = -1;
        private bool _failed;

        internal RuntimeEvidence(Action<string> log) { _log = log; }
        internal static double Milliseconds => Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency);

        internal static Scope Measure(Callback callback) => new Scope(Current, callback);

        internal readonly struct Scope : IDisposable
        {
            private readonly RuntimeEvidence _owner;
            private readonly Callback _callback;
            private readonly double _start;
            internal Scope(RuntimeEvidence owner, Callback callback)
            {
                _owner = owner;
                _callback = callback;
                _start = owner == null ? 0 : Milliseconds;
            }
            public void Dispose()
            {
                if (_owner == null || _owner._failed) return;
                double elapsed = Milliseconds - _start;
                ref CallbackTotals totals = ref _owner._callbacks[(int)_callback];
                totals.Count++;
                totals.Total += elapsed;
                totals.Max = Math.Max(totals.Max, elapsed);
            }
        }

        private struct CallbackTotals { internal long Count; internal double Total, Max; }

        internal void Frame(double now, bool active, NarrativeWorldState world)
        {
            if (_failed) return;
            if (!active) { _previous = -1; return; }
            if (_windowStart < 0) _windowStart = now;
            if (_previous >= 0) _frames[_count++] = Math.Max(0, now - _previous);
            _previous = now;
            if (_count == _frames.Length || now - _windowStart >= 30000)
            {
                Flush("window", world);
                _windowStart = now;
            }
        }

        internal void Flush(string reason, NarrativeWorldState world)
        {
            if (_failed) return;
            try
            {
                var line = new StringBuilder("BQ-PERF reason=").Append(reason)
                    .Append(" utc=").Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                    .Append(" actors=").Append(world?.Registry.Npcs.Count ?? 0)
                    .Append(" events=").Append(world?.Ledger.Count ?? 0)
                    .Append(" facts=").Append(world?.Knowledge.Facts.Count ?? 0)
                    .Append(" threads=").Append(world?.Threads.Count ?? 0)
                    .Append(" frames=").Append(_count);
                if (_count > 0)
                {
                    Array.Sort(_frames, 0, _count);
                    double total = 0;
                    int over50 = 0;
                    for (int i = 0; i < _count; i++) { total += _frames[i]; if (_frames[i] > 50) over50++; }
                    line.Append(" frame_ms(mean/p95/p99/max)=").Append(Number(total / _count)).Append('/')
                        .Append(Number(_frames[(int)Math.Ceiling(_count * .95) - 1])).Append('/')
                        .Append(Number(_frames[(int)Math.Ceiling(_count * .99) - 1])).Append('/')
                        .Append(Number(_frames[_count - 1])).Append(" frames_over50ms=").Append(over50);
                }
                for (int i = 0; i < _callbacks.Length; i++)
                {
                    var t = _callbacks[i];
                    line.Append(' ').Append((Callback)i).Append("(count/total_ms/mean_ms/max_ms)=")
                        .Append(t.Count).Append('/').Append(Number(t.Total)).Append('/')
                        .Append(Number(t.Count == 0 ? 0 : t.Total / t.Count)).Append('/').Append(Number(t.Max));
                }
                _log(line.ToString());
            }
            catch { _failed = true; } // instrumentation must never interrupt gameplay
            _count = 0;
            Array.Clear(_callbacks, 0, _callbacks.Length);
        }

        internal void ResetFrames() { _previous = -1; _windowStart = -1; }

        internal void Home(string phase, NarrativeWorldState world, IVanillaState vanilla)
        {
            if (_failed || world == null || vanilla == null) return;
            try
            {
                var home = vanilla.GetHomeState();
                var line = new StringBuilder("BQ-HOME phase=").Append(phase)
                    .Append(" utc=").Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                    .Append(" minute=").Append(vanilla.Now.TotalMinutes)
                    .Append(" player_zone=").Append(vanilla.GetZoneOf(vanilla.PlayerId))
                    .Append(" events=").Append(world.Ledger.Count)
                    .Append(" home=").Append(home == null ? "unreadable" : home.Describe());
                if (home != null)
                {
                    line.Append(" home_zone=").Append(home.ZoneId);
                    int sampled = Math.Min(64, home.Residents.Count);
                    line.Append(" resident_sample=").Append(sampled).Append('/').Append(home.Residents.Count);
                    for (int i = 0; i < sampled; i++)
                    {
                        var id = home.Residents[i].Id;
                        var npc = world.Registry.GetNpc(id);
                        line.Append(" [").Append(id).Append(" clock=")
                            .Append(npc == null ? "unregistered" : npc.LastSimulatedAt.TotalMinutes.ToString(CultureInfo.InvariantCulture))
                            .Append(" presence=").Append(vanilla.GetActorActivity(id)?.Presence.ToString() ?? "Unknown").Append(']');
                    }
                }
                _log(line.ToString());
            }
            catch (Exception ex)
            {
                try { _log("BQ-HOME phase=" + phase + " unavailable=" + ex.GetType().Name); }
                catch { _failed = true; }
            }
        }

        private static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
