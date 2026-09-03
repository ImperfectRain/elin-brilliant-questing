using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Content;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// The second gate on a callback: being entitled to remember something is not being willing to
    /// say it to the person opposite.
    ///
    /// BQ-081 settles the first question and settles it structurally - a hook is derived per
    /// recaller, so material nobody had a route to does not exist to be spoken. It says nothing
    /// about the listener, and these tests are about what used to fall through that gap: a claim
    /// its holder would refuse to state if asked outright could still be handed to wording as old
    /// business and come out anyway. Recall permission was being spent as disclosure permission.
    ///
    /// Nothing here is a new authority. Every answer comes from the same <c>Disclosure</c> that
    /// decides willingness for every other claim, asked about the claims the recalled event already
    /// named - which is why the tests move relationships and secrecy rather than any callback
    /// setting.
    /// </summary>
    public class CallbackDisclosureTests
    {
        // -- the gap, closed ------------------------------------------------------------------------

        /// <summary>
        /// The crux. One speaker, one memory, two listeners: the material is equally theirs to
        /// remember in both scenes, and only one of the two hears it. Nothing about the hook
        /// differs between the two calls - what differs is who is being spoken to.
        /// </summary>
        [Fact]
        public void TheSameMemoryIsRaisedWithOnePersonAndKeptFromAnother()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);

            // Dana is a friend Cass would tell; Erik is nobody to them.
            stage.World.Relationships.Connect(stage.Cass, stage.Dana, RelationKind.Friend, 90);

            CallbackPermit toFriend = CallbackDisclosure.Permit(stage.World, hook, stage.Dana, stage.Now);
            CallbackPermit toStranger = CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now);

            Assert.True(toFriend.Allowed);
            Assert.False(toStranger.Allowed);
            Assert.Equal(hook.Signature, toFriend.Hook.Signature);
            Assert.Equal(hook.Signature, toStranger.Hook.Signature);
        }

        /// <summary>
        /// Material the speaker would keep does not become a line, and it is refused rather than
        /// silently dropped: a caller who asked for a callback and got a line without one would
        /// think the permission question had been answered when it had only been discarded.
        /// </summary>
        [Fact]
        public void MaterialTheSpeakerWouldKeepIsRefusedRatherThanWorded()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);

            CallbackPermit permit = CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now);
            Assert.False(permit.Allowed);
            Assert.Equal(stage.SecretClaim, permit.Withheld);
            Assert.False(permit.Strategy == DisclosureStrategy.NothingToDisclose);

            RealizationRequest request = stage.Ask(stage.Cass, stage.Erik, hook.Counterpart);
            request.Callback = permit;

            Assert.Equal("the speaker would not bring this up with this listener", request.WhyNot());
            Assert.False(stage.Realizer.Realize(request).Rendered);
        }

        /// <summary>
        /// The disclosure decision that refuses the callback is the same one that would refuse the
        /// question, which is the whole claim of the seam: no second willingness model, no
        /// callback-specific secrecy, and an answer that moves when the world moves.
        /// </summary>
        [Fact]
        public void TheRefusalIsDisclosuresOwnAndNotASecondJudgement()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);

            DisclosureDecision asked = Disclosure.Decide(stage.World, stage.Cass, stage.Erik, stage.SecretClaim, stage.Now);
            CallbackPermit permit = CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now);

            Assert.False(asked.WillDisclose);
            Assert.False(permit.Allowed);
            Assert.Equal(asked.Strategy, permit.Strategy);

            // Mend the tie and the same claim comes out, so the callback does too. Nothing about
            // the callback was touched.
            stage.World.Relationships.Connect(stage.Cass, stage.Erik, RelationKind.Friend, 90);

            Assert.True(Disclosure.Decide(stage.World, stage.Cass, stage.Erik, stage.SecretClaim, stage.Now).WillDisclose);
            Assert.True(CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now).Allowed);
        }

        // -- and permitted callbacks still work ------------------------------------------------------

        /// <summary>
        /// The gate closes on what it should and nothing else: a cleared callback is still selected,
        /// still permitted and still spoken.
        /// </summary>
        [Fact]
        public void APermittedCallbackStillReachesTheWords()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);
            stage.World.Relationships.Connect(stage.Cass, stage.Dana, RelationKind.Friend, 90);

            CallbackPermit permit = CallbackDisclosure.Permit(stage.World, hook, stage.Dana, stage.Now);
            Assert.True(permit.Allowed);

            Assert.Contains("is said to have done", stage.SaidWith(stage.Cass, stage.Dana, permit));
        }

        /// <summary>
        /// Most old business names no claim at all, and none of it is caught by this gate. That is
        /// not a hole: with no claim recorded and notice suppressed the only route left is
        /// <c>FirstHand</c>, so the speaker is talking about themselves and there is no third
        /// party's secret to leak.
        /// </summary>
        [Fact]
        public void AnEventThatNamedNoClaimIsClearedBecauseThereIsNothingToKeep()
        {
            Stage stage = Stage.Create();
            WorldEvent helped = stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram);
            CallbackHook hook = CallbackHooks.Of(stage.World, stage.Vanilla, helped, stage.Bram, stage.Now);

            Assert.Empty(hook.Claims);
            Assert.True(CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now).Allowed);
        }

        /// <summary>
        /// Holding no belief about a claim is not withholding it. A witness who saw an event without
        /// forming a view about what was filed against it is not keeping anything back, and treating
        /// "nothing to disclose" as a refusal would silence exactly the people who were there.
        /// </summary>
        [Fact]
        public void HoldingNoBeliefAboutTheClaimIsNotWithholdingIt()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftWitnessedBy(stage.Erik);

            Assert.Equal(CallbackRoute.Witnessed, hook.Route);
            Assert.NotEmpty(hook.Claims);
            Assert.Equal(
                DisclosureStrategy.NothingToDisclose,
                Disclosure.Decide(stage.World, stage.Erik, stage.Dana, stage.SecretClaim, stage.Now).Strategy);
            Assert.True(CallbackDisclosure.Permit(stage.World, hook, stage.Dana, stage.Now).Allowed);
        }

        // -- the permit is about one listener, and cannot be re-aimed -------------------------------

        /// <summary>
        /// A clearance to say something to one person says nothing about saying it to another, so
        /// carrying it into a scene with somebody else is refused rather than honoured.
        /// </summary>
        [Fact]
        public void APermitClearedForSomebodyElseIsNotAClearanceHere()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);
            stage.World.Relationships.Connect(stage.Cass, stage.Dana, RelationKind.Friend, 90);

            CallbackPermit forDana = CallbackDisclosure.Permit(stage.World, hook, stage.Dana, stage.Now);
            Assert.True(forDana.Allowed);

            RealizationRequest toErik = stage.Ask(stage.Cass, stage.Erik, hook.Counterpart);
            toErik.Callback = forDana;

            Assert.Equal("the callback was cleared for somebody other than the person being addressed", toErik.WhyNot());
            Assert.False(stage.Realizer.Realize(toErik).Rendered);
        }

        /// <summary>
        /// A clearance for one person is not a clearance for the room. Willingness was weighed
        /// against one listener, and an act addressed to several would spend it in front of people
        /// nobody weighed it against.
        /// </summary>
        [Fact]
        public void AClearanceForOnePersonIsNotAClearanceForTheRoom()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);
            stage.World.Relationships.Connect(stage.Cass, stage.Dana, RelationKind.Friend, 90);

            CallbackPermit forDana = CallbackDisclosure.Permit(stage.World, hook, stage.Dana, stage.Now);
            Assert.True(forDana.Allowed);

            SpeechAct toBoth = SpeechAct.Compose(
                SpeechActType.Ask,
                stage.Cass,
                new[] { stage.Dana, stage.Erik },
                new ActionBinding { Purpose = "the festival" });
            Assert.NotNull(toBoth);

            RealizationRequest request = new RealizationRequest(toBoth)
            {
                Cast = DialogueCast.From(stage.World, stage.Cass, stage.Dana, hook.Counterpart),
                Callback = forDana,
                Rng = new DeterministicRng(0UL)
            };

            Assert.Equal("the callback was cleared for somebody other than the person being addressed", request.WhyNot());
            Assert.False(stage.Realizer.Realize(request).Rendered);
        }

        /// <summary>
        /// Nobody clears a memory for themself. The rule is <c>Disclosure</c>'s own - there is no
        /// disclosure to oneself - rather than a new one invented here.
        /// </summary>
        [Fact]
        public void NobodyBringsSomethingUpWithThemself()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);

            Assert.False(CallbackDisclosure.Permit(stage.World, hook, stage.Cass, stage.Now).Allowed);
        }

        // -- selection that does not throw away what it may say ---------------------------------------

        /// <summary>
        /// Withheld material is stepped over rather than being allowed to end the search: taking the
        /// most salient hook and then discovering it is not sayable would lose every perfectly
        /// sayable callback standing behind it.
        /// </summary>
        [Fact]
        public void SelectionStepsOverWithheldMaterialInsteadOfGivingUp()
        {
            Stage stage = Stage.Create();
            CallbackHook secret = stage.SecretTheftHeardBy(stage.Cass);
            WorldEvent rescue = stage.Record(WorldEventType.Rescued, stage.Cass, stage.Dana, magnitude: 0.3);
            stage.Vanilla.AdvanceDays(15);

            // The secret is the more salient of the two, so it is what an ungated selection returns.
            Assert.Equal(secret.EventId, CallbackHooks.Best(stage.World, stage.Vanilla, stage.Cass, stage.Now).EventId);

            CallbackPermit permitted = CallbackDisclosure.Best(
                stage.World, stage.Vanilla, stage.Cass, stage.Erik, stage.Now);

            Assert.NotNull(permitted);
            Assert.Equal(rescue.Id, permitted.Hook.EventId);
        }

        /// <summary>BQ-082's gate and this one are both applied, and neither substitutes for the other.</summary>
        [Fact]
        public void ARecurrenceStillHasToBeOneTheSpeakerWouldSpendOnThisListener()
        {
            Stage stage = Stage.Create();
            CallbackHook scandal = stage.SecretTheftHeardBy(stage.Cass);
            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));

            Assert.Equal(
                scandal.EventId,
                CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Cass, festival, stage.Now).EventId);
            Assert.Null(CallbackDisclosure.BestRecurrence(
                stage.World, stage.Vanilla, stage.Cass, stage.Erik, festival, stage.Now));

            stage.World.Relationships.Connect(stage.Cass, stage.Erik, RelationKind.Friend, 90);

            CallbackPermit permit = CallbackDisclosure.BestRecurrence(
                stage.World, stage.Vanilla, stage.Cass, stage.Erik, festival, stage.Now);
            Assert.NotNull(permit);
            Assert.Equal(scandal.EventId, permit.Hook.EventId);
        }

        // -- deterministic, and no state of its own ---------------------------------------------------

        /// <summary>
        /// The same world answers the same way twice, and answers the same way after a round trip.
        /// A permit is arithmetic over saved state, like the decision underneath it: nothing about
        /// it is stored, so there is nothing for a reload to lose.
        /// </summary>
        [Fact]
        public void PermissionIsDeterministicAndSurvivesASaveAndReload()
        {
            Stage stage = Stage.Create();
            CallbackHook hook = stage.SecretTheftHeardBy(stage.Cass);
            stage.World.Relationships.Connect(stage.Cass, stage.Dana, RelationKind.Friend, 90);

            Assert.Equal(
                CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now).ToString(),
                CallbackDisclosure.Permit(stage.World, hook, stage.Erik, stage.Now).ToString());

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(stage.World));
            CallbackHook after = CallbackHooks.Best(reloaded, stage.Vanilla, stage.Cass, stage.Now);

            Assert.Equal(hook.Signature, after.Signature);
            Assert.False(CallbackDisclosure.Permit(reloaded, after, stage.Erik, stage.Now).Allowed);
            Assert.True(CallbackDisclosure.Permit(reloaded, after, stage.Dana, stage.Now).Allowed);
        }

        // -- helpers ------------------------------------------------------------------------------

        /// <summary>Five people, a ledger and one secret - written here and nowhere else.</summary>
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

            internal EntityId Alice { get; private set; }

            internal EntityId Bram { get; private set; }

            internal EntityId Cass { get; private set; }

            internal EntityId Dana { get; private set; }

            internal EntityId Erik { get; private set; }

            /// <summary>The kept claim the theft named, once one has been written.</summary>
            internal EntityId SecretClaim { get; private set; }

            internal GameTime Now => Vanilla.Now;

            internal static Stage Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(20260903UL);
                EntityId alice = Person(world, "Alice");
                Stage stage = new Stage(world, new SandboxVanillaState(alice));
                stage.Alice = alice;
                stage.Bram = Person(world, "Bram");
                stage.Cass = Person(world, "Cass");
                stage.Dana = Person(world, "Dana");
                stage.Erik = Person(world, "Erik");
                stage.Realizer = BuildRealizer();
                return stage;
            }

            internal WorldEvent Record(
                WorldEventType type,
                EntityId actor,
                EntityId target,
                double magnitude = 0.6,
                IReadOnlyList<EntityId> related = null,
                IReadOnlyList<EntityId> witnesses = null)
            {
                return World.Record(
                    type, actor, target, Now, magnitude, World.NewId("zone"), related, witnesses, null, null, World.NewId("thread"));
            }

            /// <summary>
            /// A theft that named a kept claim, settled, and reached <paramref name="recaller"/> as
            /// something they were told and believe.
            /// </summary>
            internal CallbackHook SecretTheftHeardBy(EntityId recaller)
            {
                WorldEvent theft = KeptTheft();
                World.Knowledge.Teach(recaller, SecretClaim, KnowledgeSource.Hearsay, 0.9, Now, false, Bram);
                Vanilla.AdvanceDays(15);
                return CallbackHooks.Of(World, Vanilla, theft, recaller, Now);
            }

            /// <summary>
            /// The same theft, reaching <paramref name="witness"/> because they were standing there
            /// - and leaving them holding no belief about the claim filed against it.
            /// </summary>
            internal CallbackHook SecretTheftWitnessedBy(EntityId witness)
            {
                WorldEvent theft = KeptTheft(witness);
                Vanilla.AdvanceDays(15);
                return CallbackHooks.Of(World, Vanilla, theft, witness, Now);
            }

            internal RealizationRequest Ask(EntityId speaker, EntityId listener, EntityId about)
            {
                SpeechAct act = SpeechAct.Compose(
                    SpeechActType.Ask, speaker, listener, new ActionBinding { Purpose = "the festival" });
                Assert.NotNull(act);
                return new RealizationRequest(act)
                {
                    Cast = DialogueCast.From(World, speaker, listener, about),
                    Rng = new DeterministicRng(0UL)
                };
            }

            /// <summary>
            /// The first line this speaker actually spends the callback on. Seeds are walked because
            /// an optional slot is drawn against saying nothing.
            /// </summary>
            internal string SaidWith(EntityId speaker, EntityId listener, CallbackPermit permit)
            {
                for (ulong seed = 0; seed < 120; seed++)
                {
                    RealizationRequest request = Ask(speaker, listener, permit.Hook.Counterpart);
                    request.Callback = permit;
                    request.Rng = new DeterministicRng(seed);

                    RealizedLine line = Realizer.Realize(request);
                    if (line.Rendered && line.Text.Contains("is said to have done"))
                    {
                        return line.Text;
                    }
                }

                return string.Empty;
            }

            private WorldEvent KeptTheft(EntityId witness = default)
            {
                Fact stolen = new Fact(World.NewId("fact"), Alice, FactPredicates.Stole, Bram, null, TruthState.True, 100);
                World.Knowledge.AddFact(stolen);

                WorldEvent theft = Record(
                    WorldEventType.Theft,
                    Alice,
                    Bram,
                    0.8,
                    new[] { stolen.Id },
                    witness.IsNone ? null : new[] { witness });

                // `OriginEvent` is set at construction, so a test that records the event afterwards
                // replaces the fact rather than mutating it.
                World.Knowledge.AddFact(new Fact(
                    stolen.Id, stolen.Subject, stolen.Predicate, stolen.Object, stolen.Value, stolen.Truth, 100, theft.Id));

                SecretClaim = stolen.Id;
                return theft;
            }

            private static EntityId Person(NarrativeWorldState world, string name)
            {
                EntityId id = world.NewId("npc");
                world.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }

            private static DialogueRealizer BuildRealizer()
            {
                ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                    Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
                Assert.Empty(bundle.Diagnostics);

                IReadOnlyList<ContentDiagnostic> diagnostics;
                DialogueRealizer realizer = new DialogueRealizer(
                    DialogueFragmentContent.CreateLibrary(bundle.Bundle, out diagnostics));
                Assert.Empty(diagnostics);
                return realizer;
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
        }
    }
}
