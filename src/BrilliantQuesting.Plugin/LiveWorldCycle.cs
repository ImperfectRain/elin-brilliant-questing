using System;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// The live host's join to the Core production cycle (BQa-017).
    ///
    /// It is deliberately almost nothing. <see cref="ProductionCycle"/> already owns where a pass
    /// starts and ends, how much work it may do, whose turn it is and which openings are gone, and
    /// the whole point of this step is that the game gets that runner rather than a second one
    /// written against Elin. So there is no batching here, no fairness rule, no goal selection and
    /// no clock: the host says when it thinks time has moved, and the cycle's own persisted marker
    /// decides whether that interval is still owed.
    ///
    /// What it does add is the two things a hosted pass needs and a headless one does not.
    ///
    /// <b>A failure is the mod's to absorb.</b> Elin callbacks belong to Elin. A pass that throws -
    /// an adapter read that came back on a character the game has already destroyed, a capability
    /// that went away under it - must leave the player's game exactly where it was, so the
    /// exception stops here and the interval is closed rather than retried. Retrying would not
    /// resume the pass; it would re-run the half that finished.
    ///
    /// <b>A native outcome the host already wrote down still owes its opening.</b> The live
    /// observer records an act the moment Elin reports it, which is the right moment, so the
    /// observation cannot also be handed to <see cref="ProductionCycle.Run"/> - that would mint
    /// the same history twice. <see cref="Observed"/> supplies the half that is still owed.
    ///
    /// The bodies are in that same pass and need nothing here (BQa-020). The cycle enrolls them,
    /// reads what each may legitimately notice and calls the organization owner itself, so the host
    /// gains a live institutional tick without gaining a second schedule to keep in step with the
    /// first - which is this type's whole argument, applied once more.
    /// </summary>
    internal sealed class LiveWorldCycle
    {
        private readonly ProductionCycle _cycle;
        private readonly Action<string> _note;
        private readonly Action<string> _warn;
        private bool _reportedFailure;

        internal LiveWorldCycle(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry actions,
            Action<string> note,
            Action<string> warn)
        {
            _note = note ?? (message => { });
            _warn = warn ?? (message => { });

            // Attached here, after the load, for the reason the consequence engine is: restored
            // events are not dispatched, so a collector added now sees new changes only and a
            // reload is not a replay of everything the save remembers.
            _cycle = new ProductionCycle(world, vanilla, checks, actions);
            _cycle.Attach();
        }

        /// <summary>The Core runner itself, for an inspector. Not a second scheduler.</summary>
        internal ProductionCycle Cycle => _cycle;

        /// <summary>
        /// Runs the interval <paramref name="now"/> falls in, if it is still owed.
        ///
        /// Safe to call from as many hooks as the host likes, and it is called from several: an
        /// already-consumed interval is refused by the cycle rather than gated here, which is what
        /// keeps the Plugin from carrying a cursor of its own that a save could disagree with.
        /// </summary>
        internal ProductionCyclePass Advance(GameTime now)
        {
            try
            {
                ProductionCyclePass pass = _cycle.Run(now);
                _reportedFailure = false;
                if (pass.Ran)
                {
                    _note("Production cycle: " + pass + ".");
                }

                return pass;
            }
            catch (Exception ex)
            {
                // Reported once per run of failures. A capability that has gone away stays gone,
                // and a warning every act would bury the log it is meant to explain.
                if (!_reportedFailure)
                {
                    _warn("Production cycle failed partway; the interval is closed rather than "
                          + "replayed, and the next one is unaffected: " + ex.Message);
                    _reportedFailure = true;
                }

                return _cycle.LastPass;
            }
        }

        /// <summary>
        /// Closes the opening a native outcome the host has already recorded took.
        ///
        /// Called with whatever the observer made of an act, including nothing: most acts are not
        /// something this simulation needed to hear about, and an unrecognized one closes nothing.
        /// </summary>
        internal void Observed(ObservedVanillaAction observed, GameTime when)
        {
            if (observed == null)
            {
                return;
            }

            try
            {
                ConsumedOpening closed = _cycle.Close(observed, when);
                if (closed != null)
                {
                    _note("Production cycle: " + closed + ".");
                }
            }
            catch (Exception ex)
            {
                _warn("Observed opening left open after an exception: " + ex.Message);
            }
        }
    }
}
