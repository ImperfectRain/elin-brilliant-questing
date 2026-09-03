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
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-082. One absurd/memorable incident resurfacing, meaningfully, in a second and otherwise
    /// unrelated context - built entirely on <c>CallbackHooks</c> and nothing else.
    ///
    /// The done-when is proved end to end exactly the way BQ-081's own test proved "unprompted":
    /// record one real incident where it happened, let the world move on, and hand somebody else's
    /// scene the material without naming the event. What is new here is the extra gate - not every
    /// old business earns the recall, and this occasion has to genuinely not be where it happened -
    /// and the proof that removing either half of that (no route, no distance from the origin, the
    /// wrong kind of history) removes the recurrence with it.
    /// </summary>
    public class CallbackRecurrenceTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// A theft recorded in one thread and site is heard of by a third party, and resurfaces -
        /// selected and spoken - in a second thread and site that share neither with the first.
        /// </summary>
        [Fact]
        public void AMemorableIncidentResurfacesInASecondUnrelatedContext()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            Fact stolen = stage.Claim(stage.Bram, FactPredicates.Stole, stage.Alice);
            WorldEvent theft = stage.Record(
                WorldEventType.Theft, stage.Alice, stage.Bram, originThread, originSite, related: new[] { stolen.Id });
            stolen = stage.Origin(stolen, theft);
            stage.World.Knowledge.Teach(stage.Cass, stolen.Id, KnowledgeSource.Hearsay, 0.9, stage.Now, false, stage.Bram);

            stage.Vanilla.AdvanceDays(15);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            CallbackHook hook = CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Cass, festival, stage.Now);

            Assert.NotNull(hook);
            Assert.Equal(theft.Id, hook.EventId);
            Assert.Equal(CallbackKind.Scandal, hook.PrimaryKind);
            Assert.Equal(CallbackRoute.Heard, hook.Route);
            Assert.True(hook.AgeInDays >= CallbackHooks.SettledDays);

            string line = SaidWithTheCallback(stage, stage.Cass, stage.Dana, hook);
            Assert.Contains("is said to have done", line);
        }

        /// <summary>
        /// The gate reads only the kind and the two recorded ids a hook already carries - nothing
        /// about <c>Theft</c> specifically. A different event type that also leaves scandal earns
        /// the same recurrence the same way, which is what "not bespoke to one incident" means.
        /// </summary>
        [Fact]
        public void AnyScandalKindEventQualifiesNotJustTheft()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            WorldEvent revealed = stage.Record(
                WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);

            stage.Vanilla.AdvanceDays(15);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            CallbackHook hook = CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, festival, stage.Now);

            Assert.NotNull(hook);
            Assert.Equal(revealed.Id, hook.EventId);
            Assert.Equal(CallbackKind.Scandal, hook.PrimaryKind);
        }

        // -- not every callback earns it -------------------------------------------------------------

        /// <summary>
        /// A kindness is exactly as old, exactly as unrelated to this context, and exactly as
        /// available as a scandal would be - and still earns nothing, because a settled kindness is
        /// not the kind of history a town keeps repeating.
        /// </summary>
        [Fact]
        public void AnOrdinaryCallbackDoesNotBecomeHumourJustByBeingAvailable()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.Helped, stage.Alice, stage.Bram, originThread, originSite);

            stage.Vanilla.AdvanceDays(15);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));

            Assert.NotEmpty(CallbackHooks.For(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now));
            Assert.Null(CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, festival, stage.Vanilla.Now));
        }

        // -- it has to actually be a second context ---------------------------------------------------

        /// <summary>
        /// The same scandal, asked about back where it happened, is not a recurrence - it is the
        /// original context. Sharing the thread is enough to disqualify it even if the site differs.
        /// </summary>
        [Fact]
        public void TheSameThreadIsNotASecondContextEvenAtADifferentSite()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            WorldEvent revealed = stage.Record(
                WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(15);

            ContinuityContext stillTheSameMatter = new ContinuityContext(originThread, stage.World.NewId("zone"));
            Assert.Null(CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, stillTheSameMatter, stage.Vanilla.Now));

            ContinuityContext genuinelyElsewhere = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            Assert.Equal(
                revealed.Id,
                CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, genuinelyElsewhere, stage.Vanilla.Now).EventId);
        }

        /// <summary>Sharing the site is just as disqualifying as sharing the thread.</summary>
        [Fact]
        public void TheSameSiteIsNotASecondContextEvenInADifferentThread()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(15);

            ContinuityContext sameGround = new ContinuityContext(stage.World.NewId("thread"), originSite);
            Assert.Null(CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, sameGround, stage.Vanilla.Now));
        }

        /// <summary>
        /// An occasion that recorded no context of its own proves no distance from anything. Two
        /// blanks are not a difference: neither side can compare, so the second-context claim is
        /// unproved and the recurrence is not offered.
        ///
        /// This is the direction the old reading got backwards. "Not the same thread" was satisfied
        /// by there being no threads to compare, so an event with nothing recorded about where it
        /// happened resurfaced everywhere - including, invisibly, where it actually happened.
        /// </summary>
        [Fact]
        public void AnOccasionThatRecordedNoContextProvesNoSeparation()
        {
            Stage stage = Stage.Create();
            stage.World.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, stage.Now, 0.6);
            stage.Vanilla.AdvanceDays(15);

            CallbackHook hook = CallbackHooks.Best(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now);
            Assert.NotNull(hook);
            Assert.True(hook.ThreadId.IsNone);
            Assert.True(hook.Place.IsNone);

            ContinuityContext nowhereInParticular = new ContinuityContext(EntityId.None, EntityId.None);
            Assert.False(CallbackRecurrence.IsUnrelatedContext(hook, nowhereInParticular));
            Assert.Null(CallbackRecurrence.Best(
                stage.World, stage.Vanilla, stage.Bram, nowhereInParticular, stage.Vanilla.Now));
        }

        /// <summary>
        /// A hook that recorded its context is no better off against an occasion that recorded
        /// none, and neither is the other way round. One known half on each side is still nothing
        /// to compare.
        /// </summary>
        [Fact]
        public void NeitherSideCanEstablishSeparationAlone()
        {
            Stage stage = Stage.Create();
            EntityId originThread = stage.World.NewId("thread");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, EntityId.None);
            stage.Vanilla.AdvanceDays(15);

            CallbackHook hook = CallbackHooks.Best(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now);

            // The hook knows only its thread; the occasion knows only its site. Nothing lines up.
            ContinuityContext siteOnly = new ContinuityContext(EntityId.None, stage.World.NewId("zone"));
            Assert.False(CallbackRecurrence.IsUnrelatedContext(hook, siteOnly));

            // Give the occasion a thread and the comparison becomes possible - and it differs.
            ContinuityContext elsewhere = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            Assert.True(CallbackRecurrence.IsUnrelatedContext(hook, elsewhere));
        }

        /// <summary>
        /// One comparable dimension is enough to prove separation, and the blank one neither helps
        /// nor blocks. This is what "an unknown dimension is not evidence either way" was always
        /// meant to say.
        /// </summary>
        [Fact]
        public void OneKnownDimensionThatDiffersIsEnough()
        {
            Stage stage = Stage.Create();
            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(15);

            CallbackHook hook = CallbackHooks.Best(stage.World, stage.Vanilla, stage.Bram, stage.Vanilla.Now);

            ContinuityContext anotherSite = new ContinuityContext(EntityId.None, stage.World.NewId("zone"));
            Assert.True(CallbackRecurrence.IsUnrelatedContext(hook, anotherSite));

            // And the known half still rules it out when it matches, blank thread or not.
            Assert.False(CallbackRecurrence.IsUnrelatedContext(hook, new ContinuityContext(EntityId.None, originSite)));
        }

        // -- unavailable material prevents the recurrence ---------------------------------------------

        /// <summary>Nobody the world can offer this to is nobody it resurfaces for.</summary>
        [Fact]
        public void WithNoRouteThereIsNothingToResurface()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(15);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));

            Assert.Null(CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Dana, festival, stage.Vanilla.Now));
        }

        /// <summary>History that has not settled yet is not offered here either - the same floor BQ-081 set.</summary>
        [Fact]
        public void MaterialTooRecentToBeOfferedDoesNotResurfaceEither()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(4);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            Assert.Null(CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, festival, stage.Vanilla.Now));
        }

        // -- deterministic --------------------------------------------------------------------------

        [Fact]
        public void SelectionIsDeterministic()
        {
            Stage stage = Stage.Create();

            EntityId originThread = stage.World.NewId("thread");
            EntityId originSite = stage.World.NewId("zone");
            stage.Record(WorldEventType.SecretRevealed, stage.Alice, stage.Bram, originThread, originSite);
            stage.Vanilla.AdvanceDays(15);

            ContinuityContext festival = new ContinuityContext(stage.World.NewId("thread"), stage.World.NewId("zone"));
            CallbackHook first = CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, festival, stage.Vanilla.Now);
            CallbackHook second = CallbackRecurrence.Best(stage.World, stage.Vanilla, stage.Bram, festival, stage.Vanilla.Now);

            Assert.Equal(first.Signature, second.Signature);
        }

        // -- helpers ---------------------------------------------------------------------------------

        private static string SaidWithTheCallback(Stage stage, EntityId speaker, EntityId listener, CallbackHook hook)
        {
            for (ulong seed = 0; seed < 80; seed++)
            {
                SpeechAct act = SpeechAct.Compose(
                    SpeechActType.Ask, speaker, listener, new ActionBinding { Purpose = "the festival" });
                Assert.NotNull(act);

                RealizedLine line = stage.Realizer.Realize(new RealizationRequest(act)
                {
                    Cast = DialogueCast.From(stage.World, speaker, listener, hook.Counterpart),
                    Callback = CallbackDisclosure.Permit(stage.World, hook, listener, stage.Now),
                    Rng = new DeterministicRng(seed)
                });

                if (line.Rendered && line.Text.Contains("is said to have done"))
                {
                    return line.Text;
                }
            }

            return string.Empty;
        }

        /// <summary>Four people and a ledger, and nothing else - the events under test are exactly the ones written here.</summary>
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
                stage.Realizer = BuildRealizer();
                return stage;
            }

            internal WorldEvent Record(
                WorldEventType type,
                EntityId actor,
                EntityId target,
                EntityId thread,
                EntityId site,
                double magnitude = 0.6,
                IReadOnlyList<EntityId> related = null)
            {
                return World.Record(type, actor, target, Now, magnitude, site, related, null, null, null, thread);
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
