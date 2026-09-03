using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-081. Old business, read off the history that already exists.
    ///
    /// The step's condition is that a scene refers back to something at least ten in-game days old
    /// without being asked about it, so the first test plays a situation through the real action
    /// pipeline, lets a fortnight pass, and makes somebody say a line about it. Everything after
    /// that is the half that is easier to get wrong: a callback must be grounded in the ledger and
    /// in nothing else, must never be available to somebody with no way of knowing, must not speak
    /// of people the world can no longer produce, and must come out the same on both sides of a
    /// save.
    /// </summary>
    public class CallbackHookTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// The condition, end to end: a played situation, twelve days of world, and a line from the
        /// person it happened to that refers back to it.
        ///
        /// Nothing in the request names the event. The scene asks what old business this speaker
        /// has with the person in front of them and is handed one, which is what "unprompted"
        /// means here - the player did not raise it and the caller did not pick it.
        /// </summary>
        [Fact]
        public void ASceneRefersBackToAnEventTwelveInGameDaysOldWithoutBeingAskedAbout()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            lab.AdvanceDays(12);

            EntityId victim = lab.Situation.VictimId;
            CallbackHook hook = CallbackHooks.Best(
                lab.World, lab.Vanilla, victim, lab.Vanilla.Now, new CallbackSelection { About = lab.Player });

            Assert.NotNull(hook);
            Assert.True(hook.AgeInDays >= CallbackHooks.SettledDays, "callback material was not old enough");
            Assert.Equal(WorldEventType.ItemReturned, hook.EventType);
            Assert.Equal(CallbackKind.Kindness, hook.PrimaryKind);
            Assert.Equal(CallbackRoute.Involved, hook.Route);
            Assert.Equal(lab.Player, hook.Counterpart);

            string line = SaidWithTheCallback(lab, victim, hook);
            Assert.Contains("After what you did for me.", line);
        }

        /// <summary>The same material, shown in the surface a developer asks "why" of.</summary>
        [Fact]
        public void TheInspectorShowsBothWhatIsAvailableAndHowMuchIsNotTheirsToKnow()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(12);

            string dump = NarrativeInspector.DescribeCallbacks(
                lab.World, lab.Vanilla, lab.Situation.VictimId, lab.Vanilla.Now);

            Assert.Contains("ItemReturned", dump);
            Assert.Contains("Kindness", dump);
            Assert.Contains("not theirs to know", dump);
        }

        // -- grounded in history, and in nothing else ----------------------------------------------

        /// <summary>
        /// Every hook names an event that is in the ledger, and says about it exactly what the
        /// ledger says. There is no second record to disagree with.
        /// </summary>
        [Fact]
        public void EveryHookNamesAnEventTheLedgerStillHolds()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(20);

            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(
                lab.World, lab.Vanilla, lab.Player, lab.Vanilla.Now, new CallbackSelection { Limit = 0 });

            Assert.NotEmpty(hooks);
            foreach (CallbackHook hook in hooks)
            {
                WorldEvent source = Find(lab.World, hook.EventId);
                Assert.NotNull(source);
                Assert.Equal(source.Type, hook.EventType);
                Assert.Equal(source.Time, hook.At);
                Assert.Equal(source.Zone, hook.Place);
            }
        }

        /// <summary>
        /// Material the world has not moved on from is not offered. Ten days is the bar the step
        /// names, and a caller that wants everything has to say so.
        /// </summary>
        [Fact]
        public void RecentBusinessIsNotOfferedUntilTheWorldHasMovedOn()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(4);

            EntityId victim = lab.Situation.VictimId;
            Assert.Empty(CallbackHooks.For(lab.World, lab.Vanilla, victim, lab.Vanilla.Now));
            Assert.NotEmpty(CallbackHooks.For(
                lab.World, lab.Vanilla, victim, lab.Vanilla.Now, new CallbackSelection { MinimumAgeInDays = 0 }));
        }

        /// <summary>
        /// The bookkeeping verbs leave nothing reusable, and are not dressed up as though they had.
        /// A conversation is not material; being robbed is.
        /// </summary>
        [Fact]
        public void AnEventWithNoReusableMaterialProducesNoHook()
        {
            Stage stage = Stage.Create();
            WorldEvent chat = stage.Record(WorldEventType.Conversed, stage.Alice, stage.Bram);

            Assert.Empty(CallbackHooks.KindsOf(WorldEventType.Conversed));
            Assert.Null(CallbackHooks.Of(stage.World, stage.Vanilla, chat, stage.Bram, stage.Now));
        }

        // -- what a person is entitled to know ------------------------------------------------------

        /// <summary>
        /// A theft nobody noticed is history, and it is the thief's alone. The person it was taken
        /// from has nothing to refer back to, because as far as they know nothing happened - which
        /// is the same rule that stops an unnoticed act moving their affinity.
        /// </summary>
        [Fact]
        public void AnUnnoticedActLeavesNothingForThePersonItWasDoneTo()
        {
            Stage stage = Stage.Create();
            WorldEvent theft = stage.Record(
                WorldEventType.Theft, stage.Alice, stage.Bram, tags: new[] { EventTags.Unnoticed });

            Assert.NotNull(CallbackHooks.Of(stage.World, stage.Vanilla, theft, stage.Alice, stage.Now));
            Assert.Null(CallbackHooks.Of(stage.World, stage.Vanilla, theft, stage.Bram, stage.Now));
            Assert.Null(CallbackHooks.Of(stage.World, stage.Vanilla, theft, stage.Cass, stage.Now));
        }

        /// <summary>
        /// A bystander who was not there and was never told holds nothing. Being told - carrying a
        /// confident belief in a claim the event began - is what turns it into theirs.
        /// </summary>
        [Fact]
        public void HistoryBecomesTheirsOnlyWhenSomethingInTheWorldGivesItToThem()
        {
            Stage stage = Stage.Create();
            Fact struck = stage.Claim(stage.Alice, FactPredicates.Extorted, stage.Bram);
            WorldEvent blow = stage.Record(WorldEventType.Attacked, stage.Alice, stage.Bram, related: new[] { struck.Id });
            struck = stage.Origin(struck, blow);

            Assert.Null(CallbackHooks.Of(stage.World, stage.Vanilla, blow, stage.Cass, stage.Now));

            stage.World.Knowledge.Teach(stage.Cass, struck.Id, KnowledgeSource.Hearsay, 0.9, stage.Now, false, stage.Bram);

            CallbackHook heard = CallbackHooks.Of(stage.World, stage.Vanilla, blow, stage.Cass, stage.Now);
            Assert.NotNull(heard);
            Assert.Equal(CallbackRoute.Heard, heard.Route);
        }

        /// <summary>
        /// A garbled retelling is knowledge of a story, not of what happened. Letting it through
        /// would let a callback speak with history's authority about a version history never
        /// recorded.
        /// </summary>
        [Fact]
        public void AGarbledVersionIsNotARouteBackToTheEvent()
        {
            Stage stage = Stage.Create();
            Fact struck = stage.Claim(stage.Alice, FactPredicates.Extorted, stage.Bram);
            WorldEvent blow = stage.Record(WorldEventType.Attacked, stage.Alice, stage.Bram, related: new[] { struck.Id });
            struck = stage.Origin(struck, blow);

            Fact garbled = stage.Claim(stage.Cass, FactPredicates.Extorted, stage.Bram);
            garbled = stage.Origin(garbled, blow);
            garbled.DistortionOf = struck.Id;
            stage.World.Knowledge.Teach(stage.Cass, garbled.Id, KnowledgeSource.Hearsay, 1.0, stage.Now, false, stage.Bram);

            Assert.Null(CallbackHooks.Of(stage.World, stage.Vanilla, blow, stage.Cass, stage.Now));
        }

        /// <summary>
        /// A hook belongs to one person, and wording refuses to put it in anybody else's mouth.
        /// The gate is therefore structural: there is no path from a private event to a line about
        /// it, even for a caller who assembled the request by hand.
        /// </summary>
        [Fact]
        public void WordingRefusesACallbackThatIsNotTheSpeakersToMake()
        {
            Stage stage = Stage.Create();
            WorldEvent helped = stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram);
            CallbackHook alices = CallbackHooks.Of(stage.World, stage.Vanilla, helped, stage.Alice, stage.Now);

            RealizationRequest borrowed = stage.Ask(stage.Cass, stage.Bram);
            borrowed.Callback = CallbackDisclosure.Permit(stage.World, alices, stage.Bram, stage.Now);

            Assert.True(borrowed.Callback.Allowed);
            Assert.Equal("the callback belongs to somebody other than the speaker", borrowed.WhyNot());
            Assert.False(stage.Realizer.Realize(borrowed).Rendered);
        }

        /// <summary>
        /// With nothing to refer back to, nothing refers back. A fragment written for old business
        /// is never chosen for a scene that has none.
        /// </summary>
        [Fact]
        public void ASceneWithNoOldBusinessNeverSaysAnyOfIt()
        {
            Stage stage = Stage.Create();

            for (ulong seed = 0; seed < 60; seed++)
            {
                RealizationRequest request = stage.Ask(stage.Alice, stage.Bram);
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = stage.Realizer.Realize(request);
                foreach (string fragment in line.Fragments)
                {
                    Assert.DoesNotContain("call.history.", fragment);
                }
            }
        }

        // -- people the world can no longer produce -------------------------------------------------

        /// <summary>
        /// Somebody who has died is still somebody to remember. The history is untouched and so is
        /// the offering of it: what dying costs is being produced, not being referred to, and the
        /// hook says which of the two it lost.
        /// </summary>
        [Fact]
        public void AParticipantWhoHasDiedIsStillReferableAndSaysSo()
        {
            Stage stage = Stage.Create();
            stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram);
            stage.Vanilla.AdvanceDays(14);

            Assert.NotEmpty(CallbackHooks.For(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now));

            stage.Vanilla.Kill(stage.Alice);

            CallbackHook kept = Assert.Single(CallbackHooks.For(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now));
            Assert.Equal(CallbackParty.Gone, kept.Party);
            Assert.True(CallbackHooks.IsReferable(kept.Party));
            Assert.False(CallbackHooks.IsStageable(kept.Party));

            // The caller whose use of the hook needs the person themself asks the narrower question
            // and is told no, which is the whole of what dying takes away here.
            Assert.Empty(CallbackHooks.For(
                stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now,
                new CallbackSelection { Parties = CallbackParties.Stageable }));
        }

        /// <summary>
        /// Somebody the registry cannot produce at all is a different case from a dead person and
        /// still drops out: there is no name to say and nothing to describe, so the material is not
        /// offered unprompted. A caller that wants to see even that asks for everything.
        /// </summary>
        [Fact]
        public void APartyTheRegistryCannotProduceIsNotOfferedUnprompted()
        {
            Stage stage = Stage.Create();
            EntityId nobodyModels = stage.World.NewId("npc");
            stage.World.Record(
                WorldEventType.Helped, nobodyModels, stage.Bram, stage.Now, 0.6, EntityId.None, null, null, null, null, EntityId.None);
            stage.Vanilla.AdvanceDays(14);

            Assert.Empty(CallbackHooks.For(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now));

            CallbackHook kept = Assert.Single(CallbackHooks.For(
                stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now,
                new CallbackSelection { Parties = CallbackParties.Any }));
            Assert.Equal(CallbackParty.Unknown, kept.Party);
            Assert.False(CallbackHooks.IsReferable(kept.Party));
        }

        /// <summary>Being away is not being gone: somebody who left town is still referable.</summary>
        [Fact]
        public void SomebodyWhoHasMerelyLeftTownIsStillReferable()
        {
            Stage stage = Stage.Create();
            stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram);
            stage.Vanilla.AdvanceDays(14);
            stage.World.Absences.TryAdd(new ActorAbsence(
                stage.Alice, AbsenceGrade.Physical, "gone travelling", stage.Vanilla.Now, ActorAbsence.NoScheduledReturn));

            CallbackHook hook = Assert.Single(CallbackHooks.For(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now));
            Assert.Equal(CallbackParty.Away, hook.Party);
        }

        // -- what the readings say, and what they refuse to say --------------------------------------

        /// <summary>
        /// Embarrassment is the recaller's own exposure. The person who broke the promise carries
        /// it; the person they broke it to does not, and asking for theirs is how a scene finds out
        /// what it would cost the other side.
        /// </summary>
        [Fact]
        public void EmbarrassmentIsTheRecallersOwnAndNobodyElses()
        {
            Stage stage = Stage.Create();
            WorldEvent broken = stage.Record(
                WorldEventType.PromiseBroken, stage.Alice, stage.Bram, magnitude: 1.0, witnesses: new[] { stage.Cass });

            CallbackHook hers = CallbackHooks.Of(stage.World, stage.Vanilla, broken, stage.Alice, stage.Now);
            CallbackHook his = CallbackHooks.Of(stage.World, stage.Vanilla, broken, stage.Bram, stage.Now);

            Assert.True(hers.Embarrassment > 0);
            Assert.Equal(0, his.Embarrassment);
            Assert.Equal(hers.Publicity, his.Publicity);
        }

        /// <summary>
        /// An act nobody saw is not a talking point, however large it was. Publicity is read off the
        /// event's own witness list and the secrecy of the claims it names.
        /// </summary>
        [Fact]
        public void SomethingNobodySawCarriesNoPublicity()
        {
            Stage stage = Stage.Create();
            WorldEvent quiet = stage.Record(
                WorldEventType.Theft, stage.Alice, stage.Bram, magnitude: 1.0, tags: new[] { EventTags.Unnoticed });

            Assert.Equal(0, CallbackHooks.Of(stage.World, stage.Vanilla, quiet, stage.Alice, stage.Now).Publicity);
        }

        /// <summary>
        /// "After what I did for you" and "after what you did for me" are opposite claims about one
        /// event, so wording is told which way round it was and cannot choose the wrong one.
        /// </summary>
        [Fact]
        public void WhichWayRoundItWentIsReadFromTheHookAndNeverFromTheWords()
        {
            Stage stage = Stage.Create();
            WorldEvent helped = stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram);

            RealizationReading hers = RealizationReading.Of(
                stage.Ask(stage.Alice, stage.Bram).Act, null, null, stage.Cast,
                CallbackHooks.Of(stage.World, stage.Vanilla, helped, stage.Alice, stage.Now));
            RealizationReading his = RealizationReading.Of(
                stage.Ask(stage.Bram, stage.Alice).Act, null, null, stage.Cast,
                CallbackHooks.Of(stage.World, stage.Vanilla, helped, stage.Bram, stage.Now));

            Assert.Equal("first_hand", hers.Value(DialogueReadings.CallbackRoute));
            Assert.Equal("involved", his.Value(DialogueReadings.CallbackRoute));
            Assert.Equal("kindness", hers.Value(DialogueReadings.Callback));
            Assert.Equal("listener", hers.Value(DialogueReadings.CallbackParty));
        }

        /// <summary>Nothing given reads as nothing given, not as an unfinished reading.</summary>
        [Fact]
        public void NoCallbackReadsAsAbsentRatherThanAsSomeDefaultKind()
        {
            Stage stage = Stage.Create();
            RealizationReading reading = RealizationReading.Of(stage.Ask(stage.Alice, stage.Bram).Act, null, null, stage.Cast);

            Assert.Equal(DialogueReadings.Absent, reading.Value(DialogueReadings.Callback));
            Assert.Equal(DialogueReadings.Absent, reading.Value(DialogueReadings.CallbackParty));
            Assert.Equal(DialogueReadings.Absent, reading.Value(DialogueReadings.CallbackRoute));
        }

        /// <summary>
        /// A callback about somebody nobody put on stage makes the fragments that would have named
        /// them ineligible, rather than reaching for a way to describe them.
        /// </summary>
        [Fact]
        public void ACallbackAboutSomebodyUnnamedIsNotWordedAroundThem()
        {
            Stage stage = Stage.Create();
            WorldEvent helped = stage.Record(WorldEventType.Helped, stage.Alice, stage.Cass);
            CallbackHook hook = CallbackHooks.Of(stage.World, stage.Vanilla, helped, stage.Alice, stage.Now);

            RealizationRequest request = stage.Ask(stage.Alice, stage.Bram);
            request.Callback = CallbackDisclosure.Permit(stage.World, hook, stage.Bram, stage.Now);
            request.Cast = DialogueCast.From(stage.World, stage.Alice, stage.Bram);

            foreach (DialogueFragment fragment in stage.Realizer.Candidates(FragmentPosition.Callback, request))
            {
                Assert.DoesNotContain("{recalled}", fragment.Text);
            }

            request.Cast = stage.Cast;
            Assert.Contains(
                stage.Realizer.Candidates(FragmentPosition.Callback, request),
                fragment => fragment.Id == "call.history.kindness.mine.other");
        }

        // -- determinism and persistence -------------------------------------------------------------

        /// <summary>
        /// Nothing is stored, so nothing has to be migrated - and the material still reads back
        /// identically after a reload, for the same reason the ledger does.
        /// </summary>
        [Fact]
        public void TheSameMaterialComesBackAfterASaveAndReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(15);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(
                Signatures(CallbackHooks.For(lab.World, lab.Vanilla, lab.Situation.VictimId, lab.Vanilla.Now)),
                Signatures(CallbackHooks.For(reloaded, lab.Vanilla, lab.Situation.VictimId, lab.Vanilla.Now)));

            Assert.Equal(
                NarrativeInspector.DescribeCallbacks(lab.World, lab.Vanilla, lab.Player, lab.Vanilla.Now),
                NarrativeInspector.DescribeCallbacks(reloaded, lab.Vanilla, lab.Player, lab.Vanilla.Now));
        }

        /// <summary>
        /// The same world gives the same answer in the same order. Ordering is salience with ties
        /// broken on event id, so nothing depends on how the ledger happened to be walked.
        /// </summary>
        [Fact]
        public void SelectionIsDeterministic()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(15);

            CallbackSelection everything = new CallbackSelection { Limit = 0 };
            IReadOnlyList<CallbackHook> first = CallbackHooks.For(lab.World, lab.Vanilla, lab.Player, lab.Vanilla.Now, everything);
            IReadOnlyList<CallbackHook> second = CallbackHooks.For(lab.World, lab.Vanilla, lab.Player, lab.Vanilla.Now, everything);

            Assert.Equal(Signatures(first), Signatures(second));
            for (int i = 1; i < first.Count; i++)
            {
                Assert.True(
                    CallbackHooks.SalienceOf(first[i - 1]) >= CallbackHooks.SalienceOf(first[i]),
                    "hooks came back out of salience order");
            }
        }

        // -- helpers ---------------------------------------------------------------------------------

        private static IReadOnlyList<string> Signatures(IReadOnlyList<CallbackHook> hooks)
        {
            List<string> signatures = new List<string>();
            for (int i = 0; i < hooks.Count; i++)
            {
                signatures.Add(hooks[i].Signature);
            }

            return signatures;
        }

        private static WorldEvent Find(NarrativeWorldState world, EntityId eventId)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Id == eventId)
                {
                    return events[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The first line this speaker says that actually uses the callback. Seeds are walked
        /// because an optional slot is drawn against saying nothing - a speaker who called back
        /// every single time would be a machine talking - and the step's claim is that the
        /// reference can be made, not that it is compulsory.
        /// </summary>
        private static string SaidWithTheCallback(TheftLaboratory lab, EntityId speaker, CallbackHook hook)
        {
            DialogueRealizer realizer = Realizer();
            SpeechAct act = SpeechAct.Compose(
                SpeechActType.Ask,
                speaker,
                lab.Player,
                new ActionBinding { PropositionFact = lab.Situation.TheftFactId });
            Assert.NotNull(act);

            for (ulong seed = 0; seed < 80; seed++)
            {
                RealizedLine line = realizer.Realize(new RealizationRequest(act)
                {
                    Cast = DialogueCast.From(lab.World, speaker, lab.Player, hook.Counterpart),
                    Callback = CallbackDisclosure.Permit(lab.World, hook, lab.Player, lab.Vanilla.Now),
                    Rng = new DeterministicRng(seed)
                });

                if (line.Rendered && line.Text.Contains("After what you did for me."))
                {
                    return line.Text;
                }
            }

            return string.Empty;
        }

        private static DialogueRealizer Realizer()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            DialogueRealizer realizer = new DialogueRealizer(
                DialogueFragmentContent.CreateLibrary(ShippedBundle(), out diagnostics));
            Assert.Empty(diagnostics);
            return realizer;
        }

        private static ContentBundle ShippedBundle()
        {
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);
            return bundle.Bundle;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return directory.FullName;
        }

        /// <summary>
        /// Three people and a ledger, and nothing else. Used where a test is about the gate rather
        /// than about play, so the events under test are exactly the ones written here.
        /// </summary>
        private sealed class Stage
        {
            private Stage(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal DialogueRealizer Realizer { get; private set; }

            internal DialogueCast Cast { get; private set; }

            internal EntityId Alice { get; private set; }

            internal EntityId Bram { get; private set; }

            internal EntityId Cass { get; private set; }

            internal GameTime Now => Vanilla.Now;

            internal static Stage Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(20260903UL);
                EntityId alice = Person(world, "Alice");
                Stage stage = new Stage(world, new SandboxVanillaState(alice));
                stage.Alice = alice;
                stage.Bram = Person(world, "Bram");
                stage.Cass = Person(world, "Cass");
                stage.Realizer = CallbackHookTests.Realizer();
                stage.Cast = DialogueCast.From(world, stage.Alice, stage.Bram, stage.Cass);
                return stage;
            }

            internal WorldEvent Record(
                WorldEventType type,
                EntityId actor,
                EntityId target,
                double magnitude = 0.6,
                IReadOnlyList<EntityId> related = null,
                IReadOnlyList<EntityId> witnesses = null,
                IReadOnlyList<string> tags = null)
            {
                return World.Record(type, actor, target, Now, magnitude, EntityId.None, related, witnesses, null, tags);
            }

            internal Fact Claim(EntityId subject, string predicate, EntityId obj)
            {
                Fact fact = new Fact(World.NewId("fact"), subject, predicate, obj);
                World.Knowledge.AddFact(fact);
                return fact;
            }

            /// <summary>
            /// Rewrites the fact with the event it came out of. `OriginEvent` is set at
            /// construction in the simulation, so a test that records the event afterwards has to
            /// replace the fact rather than mutate it.
            /// </summary>
            internal Fact Origin(Fact fact, WorldEvent source)
            {
                Fact linked = new Fact(
                    fact.Id, fact.Subject, fact.Predicate, fact.Object, fact.Value, fact.Truth, fact.Secrecy, source.Id);
                World.Knowledge.AddFact(linked);
                return linked;
            }

            internal RealizationRequest Ask(EntityId speaker, EntityId listener)
            {
                SpeechAct act = SpeechAct.Compose(
                    SpeechActType.Ask, speaker, listener, new ActionBinding { Purpose = "the matter" });
                Assert.NotNull(act);
                return new RealizationRequest(act) { Cast = Cast, Rng = new DeterministicRng(7UL) };
            }

            private static EntityId Person(NarrativeWorldState world, string name)
            {
                EntityId id = world.NewId("npc");
                world.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }
        }
    }
}
