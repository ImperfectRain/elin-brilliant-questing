using System;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Asks Elin what somebody is doing now, and carries the answer across the seam without
    /// interpreting it.
    ///
    /// The activity counterpart of <see cref="ElinCharacterIdentity"/>, and deliberately the only
    /// place in the plugin that reaches for a timetable, an `AIAct` or a `GlobalData`. Keeping it
    /// in one file is the whole point of BQ-135: the S8 systems get one reader to repair when an
    /// Early Access build moves something, instead of a reflective probe each.
    ///
    /// Every facet is read on its own. A member this build renamed costs its own facet and leaves
    /// the others alone, and a facet that could not be read is unknown - never idle, never awake,
    /// never "not travelling" (`D017`).
    ///
    /// <b>Reads only, and conservatively.</b> Nothing here sets a timetable, writes a goal, calls
    /// a goal factory or moves anybody. In particular it does <em>not</em> call
    /// `GetGoalFromTimeTable`, `GetGoalWork` or `GetGoalHobby`: those construct goal objects, and
    /// whether constructing one is free of side effects is exactly what the live build has never
    /// been asked (`VS 7.2`, `ELIN-Q-0014`). The roadmap requires this read to have no side
    /// effects, so the projected-routine goal stays unread until a live probe says the calls are
    /// safe, and the current goal is read from state the actor is already holding.
    ///
    /// <b>Nothing here decides what an answer means.</b> A `GoalWork` is carried across as
    /// <see cref="ActivityFamily.Work"/> and nothing more; whether that is an opportunity, an
    /// obstacle or a reason to leave somebody alone is decided above the seam and deliberately
    /// nowhere near it.
    /// </summary>
    internal static class ElinActorActivity
    {
        /// <summary>
        /// Elin's conventional "no row" markers in an id column, as
        /// <see cref="ElinCharacterIdentity"/> reads them. Applied to the timetable id for the
        /// same reason and in the same direction: it only ever removes a claim. Whether ordinary
        /// citizens carry a timetable at all is unanswered on the live build, so a column reading
        /// `0` is taken as the sheet saying nothing rather than as a timetable named zero.
        /// </summary>
        private static readonly string[] EmptyIdMarkers = { "0", "-1" };


        /// <summary>
        /// Global goal types the source shows can move an actor between zones through
        /// `Chara.MoveZone`. A global goal named here is
        /// <see cref="GlobalActivityKind.Travelling"/>; any other is
        /// <see cref="GlobalActivityKind.Other"/>, which deliberately does not report that nobody
        /// is being moved - an unfamiliar global goal may move somebody perfectly well.
        /// </summary>
        private static readonly string[] TravellingGlobalGoals =
        {
            "GlobalGoalAdv", "GlobalGoalVisitTown", "GlobalGoalVisitAndStay"
        };

        /// <summary>
        /// The names this build might have for the span of the day an actor's routine is in. The
        /// source calls it `CurrentSpan`; the alternatives are here so that a rename costs a log
        /// line rather than the facet.
        /// </summary>
        private static readonly string[] SpanNames = { "CurrentSpan", "currentSpan", "Span" };

        private static bool _reportedShape;

        /// <summary>
        /// What this build can tell us the actor is doing. Never throws and never returns null: an
        /// unresolvable actor is somebody every facet is unknown about.
        /// </summary>
        /// <param name="currentZone">
        /// Where the seam already says this actor is, passed in rather than read again so the
        /// snapshot and <see cref="IVanillaState.GetZoneOf"/> cannot disagree about somebody's
        /// whereabouts. <see cref="EntityId.None"/> leaves the facet unread.
        /// </param>
        internal static ActorActivity Read(Chara chara, EntityId actor, EntityId currentZone, ManualLogSource log)
        {
            ActorActivityBuilder builder = new ActorActivityBuilder(actor).WithZone(currentZone);
            if (chara == null)
            {
                return builder.Build();
            }

            ReadPresence(builder, chara);
            ReadTimeTable(builder, chara);
            ReadSpan(builder, chara);
            ReadCurrentAct(builder, chara);
            ReadGlobalGoalEligibility(builder, chara);
            ReadGlobalActivity(builder, chara);

            Report(log);
            return builder.Build();
        }

        /// <summary>
        /// Whether the game is running this actor moment to moment, decided by the zone they are
        /// in against the one the game is running - the same comparison the rest of the plugin
        /// makes. Either side missing leaves the facet unknown rather than concluding "elsewhere",
        /// which would report every unresolvable actor as out of reach.
        /// </summary>
        private static void ReadPresence(ActorActivityBuilder builder, Chara chara)
        {
            Zone active = TryRead(() => EClass._zone);
            Zone standing = TryRead(() => chara.currentZone);
            if (active == null || standing == null)
            {
                return;
            }

            builder.WithPresence(standing == active
                ? PhysicalPresence.InActiveZone
                : PhysicalPresence.OutsideActiveZone);
        }

        /// <summary>
        /// The game's own timetable id, carried verbatim. An id and not a schedule: BQ does not
        /// know what an unfamiliar one means and does not have to - two actors sharing an id keep
        /// the same hours, which is the whole of what it is for.
        /// </summary>
        private static void ReadTimeTable(ActorActivityBuilder builder, Chara chara)
        {
            string id = TryRead(() => VanillaApiReflection.ReadText(chara, "idTimeTable"));
            if (IsAnswer(id))
            {
                builder.WithTimeTable(id);
            }
        }

        /// <summary>
        /// The stretch of the day the routine puts them in. The enum's real name and full member
        /// list are unverified on the live build, so this matches on the value's own name and a
        /// member nothing here recognises leaves the span unknown rather than guessing which of
        /// the four it is closest to.
        /// </summary>
        private static void ReadSpan(ActorActivityBuilder builder, Chara chara)
        {
            object span = TryRead(() => ReadFirst(chara, SpanNames));
            if (span == null)
            {
                return;
            }

            string name = TryRead(() => span.ToString());
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (Same(name, "Sleep"))
            {
                builder.WithSpan(ActivitySpan.Sleep);
            }
            else if (Same(name, "Eat"))
            {
                builder.WithSpan(ActivitySpan.Eat);
            }
            else if (Same(name, "Work"))
            {
                builder.WithSpan(ActivitySpan.Work);
            }
            else if (Same(name, "Free"))
            {
                builder.WithSpan(ActivitySpan.Free);
            }
        }

        /// <summary>
        /// What the actor is actually at, projected onto a family. Read off the act the `Chara` is
        /// already holding - state, not a goal this read asked the game to build.
        ///
        /// The concrete type is checked first and then each of its base types, so an act deriving
        /// from a goal this project has evidence for is reported as that goal's family rather than
        /// as unfamiliar. An act that matches nothing is <see cref="ActivityFamily.Other"/>: the
        /// game answered, and this build has no family for the answer.
        /// </summary>
        private static void ReadCurrentAct(ActorActivityBuilder builder, Chara chara)
        {
            if (VanillaApiReflection.ResolveReadableMember(chara.GetType(), "ai") == null)
            {
                return;
            }

            object act = TryRead(() => VanillaApiReflection.ReadObject(chara, "ai"));
            if (act == null)
            {
                // The member is there and answered nothing. Left unknown rather than called idle:
                // an actor the game is not currently running an act for is not thereby an actor
                // doing nothing in particular, and idle is a conclusion a caller may act on.
                // Only `GoalIdle` reports idle here, because only `GoalIdle` is the game saying so.
                return;
            }

            builder.WithActivity(FamilyOf(act.GetType()));
        }

        /// <summary>
        /// The family for an act type, or <see cref="ActivityFamily.Other"/> for one this build
        /// does not recognise. Internal so the mapping table can be walked in a diagnostic without
        /// a second copy of it existing anywhere.
        /// </summary>
        internal static ActivityFamily FamilyOf(Type actType)
        {
            for (Type type = actType; type != null; type = type.BaseType)
            {
                switch (type.Name)
                {
                    case "GoalSleep":
                        return ActivityFamily.Sleep;
                    case "GoalCombat":
                    case "GoalSiege":
                        return ActivityFamily.Combat;

                    // `GoalNeeds` dispatches to these two from real `Chara` state, so an actor
                    // found at one of them is at a bodily need whether or not the goal above it
                    // is still the current act.
                    case "GoalNeeds":
                    case "AI_Eat":
                    case "AI_Bladder":
                        return ActivityFamily.Needs;
                    case "GoalWork":
                        return ActivityFamily.Work;
                    case "GoalHobby":
                        return ActivityFamily.Hobby;
                    case "GoalTask":
                        return ActivityFamily.Task;
                    case "GoalIdle":
                        return ActivityFamily.Idle;
                }
            }

            return ActivityFamily.Other;
        }

        /// <summary>
        /// Whether vanilla's hourly off-screen mechanism runs this actor at all - the flag
        /// `GameDate.AdvanceHour` itself tests.
        ///
        /// Read as a tri-state on purpose. Which real traits report true is the single most
        /// load-bearing unanswered question behind this whole read (`ELIN-Q-0014`), and a build
        /// that cannot read the flag must say so rather than report every citizen as ineligible -
        /// which would let a caller conclude that Elin is moving nobody.
        /// </summary>
        private static void ReadGlobalGoalEligibility(ActorActivityBuilder builder, Chara chara)
        {
            object trait = TryRead(() => (object)chara.trait);
            if (trait == null)
            {
                return;
            }

            if (VanillaApiReflection.TryReadBool(trait, "UseGlobalGoal", out bool eligible))
            {
                builder.WithGlobalGoalEligibility(eligible
                    ? GlobalGoalEligibility.Eligible
                    : GlobalGoalEligibility.NotEligible);
            }
        }

        /// <summary>
        /// What vanilla's off-screen mechanism currently has this actor doing, and whether it is
        /// holding a zone move for them.
        ///
        /// Both facets hang off the same `global` record, so they are read together, and the
        /// distinction that matters is between a member this build does not expose and a member
        /// that answered nothing. An absent `global` member leaves both unknown; a `global` that
        /// is null is the game saying this actor has no global record, which settles both as
        /// <see cref="GlobalActivityKind.None"/> and <see cref="ZoneTransitionState.None"/>.
        /// </summary>
        private static void ReadGlobalActivity(ActorActivityBuilder builder, Chara chara)
        {
            if (VanillaApiReflection.ResolveReadableMember(chara.GetType(), "global") == null)
            {
                return;
            }

            object global = TryRead(() => VanillaApiReflection.ReadObject(chara, "global"));
            if (global == null)
            {
                builder.WithGlobalActivity(GlobalActivityKind.None)
                    .WithZoneTransition(ZoneTransitionState.None);
                return;
            }

            if (VanillaApiReflection.ResolveReadableMember(global.GetType(), "goal") != null)
            {
                object goal = TryRead(() => VanillaApiReflection.ReadObject(global, "goal"));
                builder.WithGlobalActivity(goal == null
                    ? GlobalActivityKind.None
                    : GlobalKindOf(goal.GetType()));
            }

            if (VanillaApiReflection.ResolveReadableMember(global.GetType(), "transition") != null)
            {
                object transition = TryRead(() => VanillaApiReflection.ReadObject(global, "transition"));
                builder.WithZoneTransition(transition == null
                    ? ZoneTransitionState.None
                    : ZoneTransitionState.Pending);
            }
        }

        /// <summary>
        /// Whether a global goal is one the source shows can move an actor between zones. Walks
        /// the base chain for the same reason the act mapping does, and answers
        /// <see cref="GlobalActivityKind.Other"/> - never "not travelling" - for a goal it does
        /// not recognise.
        /// </summary>
        internal static GlobalActivityKind GlobalKindOf(Type goalType)
        {
            for (Type type = goalType; type != null; type = type.BaseType)
            {
                for (int i = 0; i < TravellingGlobalGoals.Length; i++)
                {
                    if (type.Name == TravellingGlobalGoals[i])
                    {
                        return GlobalActivityKind.Travelling;
                    }
                }
            }

            return GlobalActivityKind.Other;
        }

        private static object ReadFirst(object target, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                MemberInfo member = VanillaApiReflection.ResolveReadableMember(target?.GetType(), names[i]);
                if (member == null)
                {
                    continue;
                }

                object read = VanillaApiReflection.ReadObject(target, names[i]);
                if (read != null)
                {
                    return read;
                }
            }

            return null;
        }

        private static bool Same(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether this is an answer at all. Empty is nothing, and so are the sheet's own "no row"
        /// markers - carrying either through would turn a silence into a claim.
        /// </summary>
        private static bool IsAnswer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < EmptyIdMarkers.Length; i++)
            {
                if (id == EmptyIdMarkers[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// One facet's read, and only that facet's. A member this build renamed, a record that is
        /// not there and a getter that threw all cost the datum they were being asked for and
        /// leave the others alone.
        /// </summary>
        private static T TryRead<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static void Report(ManualLogSource log)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            log.LogInfo("BQ actor activity: reading Chara.currentZone against the active zone, "
                        + "Chara.idTimeTable, the current routine span, the AIAct on Chara.ai, "
                        + "trait.UseGlobalGoal and Chara.global's goal and transition. "
                        + "GetGoalFromTimeTable/GetGoalWork/GetGoalHobby are deliberately NOT "
                        + "called: they build goal objects and this read must have no side "
                        + "effects. Each facet fails on its own; an unread facet is unknown and "
                        + "is never idle, awake or 'not travelling'.");
        }
    }
}
