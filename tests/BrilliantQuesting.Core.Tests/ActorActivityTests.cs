using System;
using System.Reflection;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-135: the game can be asked what somebody is doing now, and what it did not answer says
    /// so.
    ///
    /// These drive <see cref="SandboxVanillaState"/>, the reference implementation of the seam.
    /// The live adapter cannot be exercised without a running game, so the rules pinned here are
    /// the rules it is written to honour: eight facets that fail independently, an unread facet
    /// that is unknown rather than a plausible default, an observation that is never saved, and a
    /// travel reading that is vanilla's alone.
    /// </summary>
    public class ActorActivityTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Smith = EntityId.Parse("npc_smith");
        private static readonly EntityId Sleeper = EntityId.Parse("npc_sleeper");
        private static readonly EntityId Traveller = EntityId.Parse("npc_traveller");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Road = EntityId.Parse("zone_road");

        private static SandboxVanillaState Town1()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Town);
            vanilla.Define(Smith, zone: Town);
            vanilla.Define(Sleeper, zone: Town);
            vanilla.Define(Traveller, zone: Road);

            vanilla.SetActorActivity(Smith, new ActorActivityBuilder(Smith)
                .WithPresence(PhysicalPresence.InActiveZone)
                .WithTimeTable("default")
                .WithSpan(ActivitySpan.Work)
                .WithActivity(ActivityFamily.Work)
                .WithGlobalGoalEligibility(GlobalGoalEligibility.NotEligible)
                .WithGlobalActivity(GlobalActivityKind.None)
                .WithZoneTransition(ZoneTransitionState.None)
                .Build());

            vanilla.SetActorActivity(Sleeper, new ActorActivityBuilder(Sleeper)
                .WithPresence(PhysicalPresence.InActiveZone)
                .WithTimeTable("owl")
                .WithSpan(ActivitySpan.Sleep)
                .WithActivity(ActivityFamily.Sleep)
                .Build());

            vanilla.SetActorActivity(Traveller, new ActorActivityBuilder(Traveller)
                .WithPresence(PhysicalPresence.OutsideActiveZone)
                .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                .WithGlobalActivity(GlobalActivityKind.Travelling)
                .WithZoneTransition(ZoneTransitionState.Pending)
                .Build());

            return vanilla;
        }

        // -- the snapshot ----------------------------------------------------------------------

        [Fact]
        public void EachFacetIsItsOwnFieldCarryingTheGamesOwnAnswer()
        {
            ActorActivity activity = Town1().GetActorActivity(Smith);

            Assert.Equal(Smith, activity.Actor);
            Assert.Equal(PhysicalPresence.InActiveZone, activity.Presence);
            Assert.Equal(Town, activity.CurrentZone);
            Assert.True(activity.TimeTableKnown);
            Assert.Equal("default", activity.TimeTableId);
            Assert.Equal(ActivitySpan.Work, activity.CurrentSpan);
            Assert.Equal(ActivityFamily.Work, activity.CurrentActivity);
            Assert.Equal(GlobalGoalEligibility.NotEligible, activity.UsesGlobalGoal);
            Assert.Equal(GlobalActivityKind.None, activity.GlobalActivity);
            Assert.Equal(ZoneTransitionState.None, activity.PendingZoneTransition);
            Assert.Empty(activity.UnreadFacets);
        }

        [Fact]
        public void TheSnapshotAndTheSeamAgreeAboutWhereSomebodyIs()
        {
            // One question, one answer. A snapshot that carried its own idea of somebody's
            // whereabouts would be a second reconciliation target disagreeing with the first.
            SandboxVanillaState vanilla = Town1();

            Assert.Equal(vanilla.GetZoneOf(Smith), vanilla.GetActorActivity(Smith).CurrentZone);
            Assert.Equal(vanilla.GetZoneOf(Traveller), vanilla.GetActorActivity(Traveller).CurrentZone);
        }

        [Fact]
        public void ASpanAndAnActivityAreReadSeparatelyAndMayDisagree()
        {
            // Where the routine says they are in their day, and what they are actually at, are two
            // reads. A guard being attacked during a Work span is in combat, and the span is still
            // Work; deriving either from the other would lose one of the two facts.
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorActivity(Smith, new ActorActivityBuilder(Smith)
                .WithSpan(ActivitySpan.Work)
                .WithActivity(ActivityFamily.Combat)
                .Build());

            ActorActivity activity = vanilla.GetActorActivity(Smith);

            Assert.Equal(ActivitySpan.Work, activity.CurrentSpan);
            Assert.Equal(ActivityFamily.Combat, activity.CurrentActivity);
        }

        // -- no Elin in the vocabulary ---------------------------------------------------------

        [Fact]
        public void TheActivityVocabularyIsSemanticAndNamesNoGameType()
        {
            // The whole reason the projection exists. Goal subclasses, `Chara`, timetable types
            // and reflection handles stay in the adapter; what crosses is these enums (`D001`).
            string[] gameTypes =
            {
                "Chara", "AIAct", "Goal", "GoalSleep", "GoalWork", "GoalHobby", "GoalNeeds",
                "GoalCombat", "GoalSiege", "GoalTask", "GoalIdle", "GlobalGoal", "GlobalGoalAdv",
                "GlobalGoalVisitTown", "GlobalGoalVisitAndStay", "GlobalData", "TraitChara",
                "SourceChara", "AI_Eat", "AI_Bladder"
            };

            Type[] vocabulary =
            {
                typeof(ActivityFamily), typeof(ActivitySpan), typeof(PhysicalPresence),
                typeof(GlobalActivityKind), typeof(GlobalGoalEligibility),
                typeof(ZoneTransitionState), typeof(VanillaMovement), typeof(ActivityFacetKind)
            };

            foreach (Type enumType in vocabulary)
            {
                foreach (string member in Enum.GetNames(enumType))
                {
                    Assert.DoesNotContain(gameTypes, name => name == member);
                }
            }

            // And nothing on the observation's own surface is anything but a Core or BCL type.
            foreach (PropertyInfo property in typeof(ActorActivity).GetProperties())
            {
                string ns = property.PropertyType.Namespace ?? string.Empty;
                Assert.True(
                    ns.StartsWith("BrilliantQuesting", StringComparison.Ordinal)
                    || ns.StartsWith("System", StringComparison.Ordinal),
                    property.Name + " crosses the seam as " + property.PropertyType.FullName);
            }
        }

        [Fact]
        public void TheShippedCoreAssemblyStillReferencesNoGameAssembly()
        {
            AssemblyName[] references = typeof(ActorActivity).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(references, r => r.Name.IndexOf("Elin", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, r => r.Name.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, r => r.Name.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // -- unknown stays unknown -------------------------------------------------------------

        [Fact]
        public void AnActorNobodyAuthoredIsUnknownRatherThanIdleAndAvailable()
        {
            ActorActivity activity = Town1().GetActorActivity(Stranger);

            Assert.True(activity.IsFullyUnknown);
            Assert.Equal(PhysicalPresence.Unknown, activity.Presence);
            Assert.Equal(ActivitySpan.Unknown, activity.CurrentSpan);
            Assert.Equal(ActivityFamily.Unknown, activity.CurrentActivity);
            Assert.NotEqual(ActivityFamily.Idle, activity.CurrentActivity);
            Assert.False(activity.TimeTableKnown);
            Assert.Equal(string.Empty, activity.TimeTableId);
            Assert.True(activity.CurrentZone.IsNone);
            Assert.Equal(GlobalGoalEligibility.Unknown, activity.UsesGlobalGoal);
            Assert.Equal(GlobalActivityKind.Unknown, activity.GlobalActivity);
            Assert.Equal(ZoneTransitionState.Unknown, activity.PendingZoneTransition);
            Assert.Equal(VanillaMovement.Unknown, activity.VanillaMovementState());
        }

        [Fact]
        public void SomebodyTheWorldPlacedButSaysNothingElseAboutIsWhereTheyAreAndNothingMore()
        {
            // The seam has one answer to whereabouts and gives it whatever else it could read, so
            // an actor with no activity read is somebody standing in a known place doing something
            // nobody can name - not somebody nowhere, and not somebody idle.
            SandboxVanillaState vanilla = Town1();
            vanilla.Define(Stranger, zone: Town);

            ActorActivity activity = vanilla.GetActorActivity(Stranger);

            Assert.Equal(Town, activity.CurrentZone);
            Assert.Equal(vanilla.GetZoneOf(Stranger), activity.CurrentZone);
            Assert.False(activity.IsFullyUnknown);
            Assert.Equal(ActivityFamily.Unknown, activity.CurrentActivity);
            Assert.Equal(PhysicalPresence.Unknown, activity.Presence);
            Assert.Equal(VanillaMovement.Unknown, activity.VanillaMovementState());
            Assert.Equal(ActorActivity.FacetKindCount - 1, activity.UnreadFacets.Count);
        }

        [Fact]
        public void NobodyAtAllIsUnknownRatherThanAnError()
        {
            ActorActivity activity = Town1().GetActorActivity(EntityId.None);

            Assert.True(activity.IsFullyUnknown);
        }

        [Fact]
        public void OneUnreadableFacetCostsOnlyItself()
        {
            // The whole shape of the read. A build that stops answering the timetable must not
            // take presence, the current act and the travel state with it.
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorActivity(Smith, new ActorActivityBuilder(Smith)
                .WithPresence(PhysicalPresence.InActiveZone)
                .WithSpan(ActivitySpan.Work)
                .WithActivity(ActivityFamily.Work)
                .WithGlobalActivity(GlobalActivityKind.None)
                .WithZoneTransition(ZoneTransitionState.None)
                .Build());

            ActorActivity activity = vanilla.GetActorActivity(Smith);

            Assert.False(activity.IsKnown(ActivityFacetKind.TimeTable));
            Assert.Single(activity.UnreadFacets, ActivityFacetKind.TimeTable);
            Assert.Equal(ActivityFamily.Work, activity.CurrentActivity);
            Assert.Equal(VanillaMovement.NotMoving, activity.VanillaMovementState());
        }

        [Fact]
        public void AnEmptyTimeTableIdIsSilenceRatherThanATimeTableCalledNothing()
        {
            ActorActivity activity = new ActorActivityBuilder(Smith).WithTimeTable(string.Empty).Build();

            Assert.False(activity.TimeTableKnown);
            Assert.Equal("?", ActivityWord(activity.Describe(), "timetable"));
        }

        [Fact]
        public void AnUnfamiliarGoalIsAnAnswerAndAnUnreadOneIsNot()
        {
            // The distinction the adapter depends on. `Other` is the game answering with something
            // this build has no family for, which is an observation; `Unknown` is the build
            // answering nothing, which is a gap. Collapsing them would hide every gap behind a
            // reading.
            ActorActivity answered = new ActorActivityBuilder(Smith)
                .WithActivity(ActivityFamily.Other).Build();
            ActorActivity silent = new ActorActivityBuilder(Smith).Build();

            Assert.True(answered.IsKnown(ActivityFacetKind.Activity));
            Assert.False(silent.IsKnown(ActivityFacetKind.Activity));
            Assert.Contains("activity Other", answered.Describe());
            Assert.Contains("activity ?", silent.Describe());
        }

        // -- transience ------------------------------------------------------------------------

        [Fact]
        public void ChangingWhatSomebodyIsDoingChangesTheNextRead()
        {
            // Transient means transient. A consumer that held the first answer would be acting on
            // a day that has moved on, which is why nothing above the seam caches this.
            SandboxVanillaState vanilla = Town1();
            Assert.Equal(ActivityFamily.Work, vanilla.GetActorActivity(Smith).CurrentActivity);

            vanilla.SetActorActivity(Smith, new ActorActivityBuilder(Smith)
                .WithSpan(ActivitySpan.Free)
                .WithActivity(ActivityFamily.Hobby)
                .Build());

            ActorActivity after = vanilla.GetActorActivity(Smith);
            Assert.Equal(ActivityFamily.Hobby, after.CurrentActivity);
            Assert.Equal(ActivitySpan.Free, after.CurrentSpan);

            // And the old answer is not still lying around under it.
            Assert.NotEqual(ActivityFamily.Work, after.CurrentActivity);
        }

        [Fact]
        public void TheObservationIsNeverWrittenIntoTheSave()
        {
            // `D004`/`D005`: the save holds meaning, not a mirror of a `Chara`. A save that
            // carried this would come back after a patch cycle still claiming somebody is asleep.
            NarrativeWorldState world = new NarrativeWorldState(7);
            world.Registry.Add(new NarrativeNpc(Smith, "Smith"));

            SandboxVanillaState vanilla = Town1();
            ActorActivity observed = vanilla.GetActorActivity(Smith);
            Assert.Equal(ActivityFamily.Work, observed.CurrentActivity);

            string json = WorldStateSerializer.Save(world);

            foreach (string word in new[]
                     {
                         "activity", "timeTable", "timetable", "currentSpan", "globalGoal",
                         "globalActivity", "zoneTransition", "presence"
                     })
            {
                Assert.DoesNotContain(word, json, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ReadingActivityRegistersNobodyAndChangesNothing()
        {
            SandboxVanillaState vanilla = Town1();

            Assert.True(vanilla.GetActorActivity(Stranger).IsFullyUnknown);

            // The read did not invent a character to answer about: the stranger is still nowhere,
            // still not in the zone roll, and no refusal was logged on their behalf.
            Assert.True(vanilla.GetZoneOf(Stranger).IsNone);
            Assert.DoesNotContain(Stranger, vanilla.GetCharactersInZone(Town));
            Assert.Empty(vanilla.Refusals);
        }

        // -- activity is not identity, and neither is a BQ goal --------------------------------

        [Fact]
        public void ActivityAndIdentityAreSeparateReadsThatDoNotAnswerEachOther()
        {
            // An actor at a work goal is activity; the job they hold is identity; and the job is
            // still theirs while they sleep (`VS 4.1`, `VS 4.2`).
            SandboxVanillaState vanilla = Town1();
            vanilla.SetCharacterIdentity(Sleeper, new CharacterIdentityBuilder(Sleeper)
                .WithWork("blacksmith", "Blacksmith")
                .Build());

            ActorActivity asleep = vanilla.GetActorActivity(Sleeper);
            CharacterIdentity identity = vanilla.GetCharacterIdentity(Sleeper);

            Assert.Equal(ActivityFamily.Sleep, asleep.CurrentActivity);
            Assert.Equal("blacksmith", identity.Work.VanillaId);

            // Losing one read does not cost the other, in either direction.
            vanilla.SetCapability(VanillaCapability.ReadActorActivity, false);
            Assert.True(vanilla.GetActorActivity(Sleeper).IsFullyUnknown);
            Assert.Equal("blacksmith", vanilla.GetCharacterIdentity(Sleeper).Work.VanillaId);

            vanilla.SetCapability(VanillaCapability.ReadActorActivity, true);
            vanilla.SetCapability(VanillaCapability.ReadCharacterIdentity, false);
            Assert.True(vanilla.GetCharacterIdentity(Sleeper).IsFullyUnknown);
            Assert.Equal(ActivityFamily.Sleep, vanilla.GetActorActivity(Sleeper).CurrentActivity);
        }

        [Fact]
        public void AVanillaWorkGoalIsNotABqMotiveAndDoesNotTouchOne()
        {
            // The two goal vocabularies never meet. What Elin has somebody doing is an
            // observation; what the simulation wants of them is `NpcGoal`, and reading the first
            // neither creates, satisfies, reweights nor replaces the second (`D021`).
            NarrativeWorldState world = new NarrativeWorldState(11);
            NarrativeNpc smith = world.Registry.Add(new NarrativeNpc(Smith, "Smith"));
            smith.Goals.Add(new NpcGoal("repay_debt", Traveller, 70, "the loan falls due"));

            SandboxVanillaState vanilla = Town1();
            ActorActivity observed = vanilla.GetActorActivity(Smith);

            Assert.Equal(ActivityFamily.Work, observed.CurrentActivity);
            Assert.Single(smith.Goals);
            Assert.Equal("repay_debt", smith.Goals[0].Kind);
            Assert.Equal(70, smith.Goals[0].Weight);
            Assert.False(smith.Goals[0].Satisfied);

            // And the observation carries no motive of its own for anything to confuse with one.
            Assert.DoesNotContain("repay_debt", observed.Describe());
        }

        // -- degradation -----------------------------------------------------------------------

        [Fact]
        public void AnUnavailableCapabilityMakesEveryFacetUnknownForEverybody()
        {
            SandboxVanillaState vanilla = Town1();
            vanilla.SetCapability(VanillaCapability.ReadActorActivity, false);

            Assert.True(vanilla.GetActorActivity(Smith).IsFullyUnknown);
            Assert.True(vanilla.GetActorActivity(Traveller).IsFullyUnknown);

            // Degrading is not the same as blocking: presence, whereabouts and life are read
            // elsewhere and are unaffected.
            Assert.Equal(Town, vanilla.GetZoneOf(Smith));
            Assert.True(vanilla.IsAlive(Smith));
        }

        [Fact]
        public void AnUnavailableCapabilityNeverReportsNobodyTravelling()
        {
            // The dangerous degradation, stated on its own. A build that cannot see travel must
            // not answer "not moving" for the actor Elin is carrying down the road.
            SandboxVanillaState vanilla = Town1();
            Assert.Equal(VanillaMovement.Moving, vanilla.GetActorActivity(Traveller).VanillaMovementState());

            vanilla.SetCapability(VanillaCapability.ReadActorActivity, false);

            Assert.Equal(VanillaMovement.Unknown, vanilla.GetActorActivity(Traveller).VanillaMovementState());
            Assert.NotEqual(VanillaMovement.NotMoving, vanilla.GetActorActivity(Traveller).VanillaMovementState());
        }

        // -- vanilla travel is vanilla's ------------------------------------------------------

        [Fact]
        public void VanillaCarryingAnActorIsReadableAsSuch()
        {
            ActorActivity activity = Town1().GetActorActivity(Traveller);

            Assert.Equal(GlobalGoalEligibility.Eligible, activity.UsesGlobalGoal);
            Assert.Equal(GlobalActivityKind.Travelling, activity.GlobalActivity);
            Assert.Equal(ZoneTransitionState.Pending, activity.PendingZoneTransition);
            Assert.Equal(VanillaMovement.Moving, activity.VanillaMovementState());
        }

        [Fact]
        public void AnUnfamiliarGlobalGoalNeverReadsAsNobodyMoving()
        {
            // Fail closed. An unrecognised global goal may move somebody perfectly well, so it
            // must not be the evidence on which BQ schedules a second journey.
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorActivity(Traveller, new ActorActivityBuilder(Traveller)
                .WithGlobalActivity(GlobalActivityKind.Other)
                .WithZoneTransition(ZoneTransitionState.None)
                .Build());

            Assert.Equal(VanillaMovement.Unknown, vanilla.GetActorActivity(Traveller).VanillaMovementState());
        }

        [Fact]
        public void AnUnreadTransitionLeavesTheQuestionOpenHoweverClearTheGoalIs()
        {
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorActivity(Traveller, new ActorActivityBuilder(Traveller)
                .WithGlobalActivity(GlobalActivityKind.None)
                .Build());

            Assert.Equal(VanillaMovement.Unknown, vanilla.GetActorActivity(Traveller).VanillaMovementState());
        }

        [Fact]
        public void IneligibilityAndNoPendingMoveIsASettledNo()
        {
            // Two independent routes to the one answer a caller may act on: no global goal, or a
            // build saying the hourly mechanism does not run this actor at all.
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorActivity(Traveller, new ActorActivityBuilder(Traveller)
                .WithGlobalGoalEligibility(GlobalGoalEligibility.NotEligible)
                .WithZoneTransition(ZoneTransitionState.None)
                .Build());

            Assert.Equal(VanillaMovement.NotMoving, vanilla.GetActorActivity(Traveller).VanillaMovementState());
        }

        [Fact]
        public void VanillaTravelIsNotABqAbsenceAndReadingItRecordsNothing()
        {
            // `D020`/`D021`. Elin moving somebody on its own is a fact to reconcile against, not a
            // move BQ made: the absence ledger is the only record of a BQ-owned departure, and
            // observing vanilla travel neither writes one nor is one.
            NarrativeWorldState world = new NarrativeWorldState(13);
            world.Registry.Add(new NarrativeNpc(Traveller, "Traveller"));

            SandboxVanillaState vanilla = Town1();
            ActorActivity activity = vanilla.GetActorActivity(Traveller);

            Assert.Equal(VanillaMovement.Moving, activity.VanillaMovementState());
            Assert.Equal(0, world.Absences.Count);
            Assert.False(world.Absences.IsAbsent(Traveller));
            Assert.Equal(AbsenceGrade.None, world.Absences.GradeOf(Traveller));

            // And no write was attempted on the way: the observation moved nobody.
            Assert.Empty(vanilla.Refusals);
            Assert.Equal(Road, vanilla.GetZoneOf(Traveller));
        }

        // -- the diagnostic line ---------------------------------------------------------------

        [Fact]
        public void TheLoggedLineDistinguishesAnAnswerFromASilence()
        {
            string described = Town1().GetActorActivity(Sleeper).Describe();

            Assert.Contains("presence InActiveZone", described);
            Assert.Contains("timetable owl", described);
            Assert.Contains("activity Sleep", described);
            Assert.Contains("global-goal ?", described);
            Assert.Contains("global-activity ?", described);
            Assert.Contains("transition ?", described);
            Assert.Contains("vanilla-moving Unknown", described);
        }

        /// <summary>The value the description gives one named facet, for a targeted assertion.</summary>
        private static string ActivityWord(string described, string facet)
        {
            foreach (string part in described.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith(facet + " ", StringComparison.Ordinal))
                {
                    return trimmed.Substring(facet.Length + 1);
                }
            }

            return null;
        }
    }
}
