using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Resolves procedural checks through BQ's deterministic resolver while using Elin's Check
    /// rows for presentation where they exist.
    ///
    /// The hybrid is not a compromise, it is what the data shapes require. A vanilla SourceCheck
    /// row is single-element: one actor element with a factor, one target element with a factor,
    /// a level modifier. Several procedural profiles are deliberately composite - intimidation
    /// reads Negotiation, Charisma and Strength against Will and level, so that a mute bruiser has
    /// a social route. Those cannot be one row without throwing away the thing that makes them
    /// interesting, so composition stays on our side and everything the situation contributes is
    /// handed to vanilla as its dcMod.
    ///
    /// Installed Check.Perform uses Elin RNG. That is correct for vanilla gameplay and wrong for
    /// replay-authoritative BQ composite checks, so native rows do not decide authoritative
    /// outcomes merely because the method exists.
    /// </summary>
    internal sealed class ElinCheckResolver : ICheckResolver
    {
        private readonly ElinBindings _bindings;
        private readonly ICheckResolver _fallback;
        private readonly ManualLogSource _log;
        private readonly HashSet<string> _missingRows = new HashSet<string>();
        private readonly HashSet<string> _pathReported = new HashSet<string>();

        internal ElinCheckResolver(ElinBindings bindings, ICheckResolver fallback, ManualLogSource log)
        {
            _bindings = bindings;
            _fallback = fallback;
            _log = log;
        }

        /// <summary>
        /// Kept for diagnostic comparisons only. Authoritative BQ checks remain deterministic
        /// unless a future implementation adds an explicit non-replay-authoritative check type.
        /// </summary>
        internal bool PreferNativeChecks { get; set; }

        public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
        {
            if (!PreferNativeChecks)
            {
                ReportPath(request.Profile.Id, "portable", "native checks are switched off");
                return _fallback.Resolve(request, rng);
            }

            if (!CanResolveNatively(request.Profile))
            {
                ReportPath(
                    request.Profile.Id,
                    "portable",
                    "the profile is composite (" + request.Profile.ActorSkills.Count + " actor skill(s), "
                    + request.Profile.ActorAttributes.Count + " actor attribute(s), "
                    + request.Profile.TargetAttributes.Count + " target attribute(s)) and a vanilla row is single-element");
                return _fallback.Resolve(request, rng);
            }

            Chara actor = _bindings.ResolveChara(request.Actor);
            if (actor == null)
            {
                ReportPath(request.Profile.Id, "portable", "the actor " + request.Actor + " is not bound to a live Chara");
                return _fallback.Resolve(request, rng);
            }

            Card target = request.Target.IsNone ? null : _bindings.ResolveChara(request.Target);

            // Everything the situation contributes - rapport, fame, a criminal record, whether
            // they can already prove it - becomes vanilla's dcMod.
            int situational = 0;
            List<CheckTerm> terms = new List<CheckTerm>();
            foreach (SituationalModifier modifier in request.Modifiers)
            {
                situational += modifier.DcDelta;
                terms.Add(new CheckTerm(modifier.Label, modifier.DcDelta));
            }

            Check check = TryGetCheck(request.Profile.Id, situational);
            if (check == null)
            {
                ReportPath(request.Profile.Id, "portable", "no usable vanilla Check row");
                return _fallback.Resolve(request, rng);
            }

            int finalDc;
            Check.Result result;
            try
            {
                finalDc = check.GetFinalDC(actor, target);
                result = check.Perform(actor, target);
            }
            catch (Exception ex)
            {
                // A vanilla check that throws in an unexpected context must not take the player's
                // turn with it. Fall back, once, loudly.
                _log.LogWarning("Native check '" + request.Profile.Id + "' threw (" + ex.GetType().Name
                                + "); using the portable resolver for it from now on.");
                _missingRows.Add(request.Profile.Id);
                return _fallback.Resolve(request, rng);
            }

            ReportPath(request.Profile.Id, "native", "vanilla Check row resolved it");
            terms.Add(new CheckTerm("resolved by vanilla Check", 0));

            // Vanilla hands back an outcome, not the face it rolled. The trace records the
            // difficulty it was measured against and says the roll is the game's, rather than
            // reporting a zero that would read as a roll of zero.
            return new CheckResult(
                request.Profile.Id, request.Profile.BaseDifficulty, terms, finalDc, CheckResult.UnknownRoll, Translate(result));
        }

        /// <summary>Vanilla's own difficulty wording, for presenting an option to the player.</summary>
        internal string DescribeDifficulty(CheckRequest request, bool inDialog)
        {
            Chara actor = _bindings.ResolveChara(request.Actor);
            Card target = request.Target.IsNone ? null : _bindings.ResolveChara(request.Target);
            Check check = actor == null ? null : TryGetCheck(request.Profile.Id, 0);
            if (check == null)
            {
                return string.Empty;
            }

            try
            {
                return check.GetText(actor, target, inDialog);
            }
            catch (Exception ex)
            {
                _log.LogInfo("Native difficulty text for '" + request.Profile.Id + "' is unavailable ("
                             + ex.GetType().Name + "); hiding the difficulty label.");
                _missingRows.Add(request.Profile.Id);
                return string.Empty;
            }
        }

        /// <summary>
        /// A vanilla `SourceCheck` row is single-element: one actor element, one target element.
        /// Every procedural profile is deliberately composite - intimidation reads Negotiation,
        /// Charisma and Strength - so in practice none of them qualifies, and the rows installed
        /// by BQ-006 earn their keep through `Check.GetText` alone, giving the player vanilla's
        /// own difficulty wording over our arithmetic.
        ///
        /// That is the intended design and not a fault, but it was invisible: this branch fell
        /// through to the fallback without saying anything, so a log full of portable traces sat
        /// underneath nine lines reporting the rows as available and looked like a contradiction.
        /// It now says which resolver ran and why.
        /// </summary>
        private static bool CanResolveNatively(CheckProfile profile)
        {
            return false;
        }

        /// <summary>
        /// Says which resolver actually ran, once per profile.
        ///
        /// The first live run installed all nine Check rows, reported each one readable through
        /// `Check.Get`, and then produced traces that were unmistakably the portable resolver's -
        /// composite profile terms and a roll of our own, where the native path prints "rolled by
        /// the game". Neither of `TryGetCheck`'s failure lines appeared either, so the log could
        /// not say where the two disagreed. It can now.
        /// </summary>
        private void ReportPath(string profileId, string path, string why)
        {
            if (_pathReported.Add(profileId))
            {
                _log.LogInfo("Check '" + profileId + "' resolves through the " + path + " path: " + why + ".");
            }
        }

        private Check TryGetCheck(string id, int dcMod)
        {
            if (_missingRows.Contains(id))
            {
                return null;
            }

            try
            {
                Check check = Check.Get(id, dcMod);
                if (check != null)
                {
                    return check;
                }
            }
            catch (Exception)
            {
                // No such row on this build. Expected for any profile the mod has not shipped as
                // a source sheet row yet.
            }

            _missingRows.Add(id);
            _log.LogInfo("No vanilla Check row '" + id + "'; using the portable resolver for it.");
            return null;
        }

        private static CheckOutcome Translate(Check.Result result)
        {
            switch (result)
            {
                case Check.Result.CriticalPass: return CheckOutcome.CriticalPass;
                case Check.Result.Pass: return CheckOutcome.Pass;
                case Check.Result.CriticalFail: return CheckOutcome.CriticalFail;
                default: return CheckOutcome.Fail;
            }
        }
    }
}
