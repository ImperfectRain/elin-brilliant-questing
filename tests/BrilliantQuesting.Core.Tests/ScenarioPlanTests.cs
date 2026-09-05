using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-139. The abstract scenario plan for a bounded location, before anything realizes it.
    /// </summary>
    public class ScenarioPlanTests
    {
        // -- the done-when ---------------------------------------------------------------------

        /// <summary>
        /// Two grammars, one errand, two plans that are both readable without anything having
        /// drawn them. The camp and the mine keep what they keep in different regions, reached
        /// differently, and neither plan is the other.
        /// </summary>
        [Fact]
        public void TwoGrammarsEachProduceAnExplainedPlanForTheSameErrand()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan camp = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);
            ScenarioPlan mine = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache);

            Assert.True(camp.Valid, Trace(fixture, camp));
            Assert.True(mine.Valid, Trace(fixture, mine));
            Assert.NotEqual(camp.PlanId, mine.PlanId);
            Assert.NotEqual(camp.ObjectiveRegionId, mine.ObjectiveRegionId);

            Explains(fixture, camp);
            Explains(fixture, mine);
        }

        /// <summary>
        /// Every part, every edge and every requirement is in the trace, and so is every part the
        /// kind allows that this seed did not draw. A plan somebody has to render to understand is
        /// not this step's plan.
        /// </summary>
        [Fact]
        public void TheTraceExplainsEveryRegionEveryRouteAndEveryRequirement()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioPlan plan = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);
            string trace = Trace(fixture, plan);

            for (int i = 0; i < plan.Regions.Count; i++)
            {
                Assert.Contains(plan.Regions[i].Id, trace);
                for (int a = 0; a < plan.Regions[i].Affordances.Count; a++)
                {
                    Assert.Contains(plan.Regions[i].Affordances[a].ToString(), trace);
                }
            }

            for (int i = 0; i < plan.Routes.Count; i++)
            {
                ScenarioRoute route = plan.Routes[i];
                Assert.Contains(route.From + " -> " + route.To, trace);
                Assert.Contains(route.Support.ToString(), trace);
            }

            for (int i = 0; i < plan.Layout.Omitted.Count; i++)
            {
                Assert.Contains(plan.Layout.Omitted[i].Id, trace);
            }

            for (int i = 0; i < plan.Sockets.Count; i++)
            {
                Assert.Contains(plan.Sockets[i].Socket, trace);
            }

            for (int i = 0; i < plan.Validation.Findings.Count; i++)
            {
                Assert.Contains(plan.Validation.Findings[i].Invariant.ToString(), trace);
            }
        }

        /// <summary>
        /// The camp holds people only where it drew a pen, and every way to that pen waits on the
        /// doorman while the place still advertises a way in that waits on nobody. So an errand
        /// after somebody held has no plan here, and the trace says which seed failed on what.
        /// </summary>
        [Fact]
        public void TheTraceExplainsWhyEveryRefusedPlanWasRefused()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Capture(fixture.VictimId);

            ScenarioPlan plan = fixture.Plan(CampGrammar, SiteAffordance.PrisonCell);
            string trace = Trace(fixture, plan);

            Assert.False(plan.Planned);
            Assert.NotEmpty(plan.Refusals);
            Assert.Contains(SiteFlaw.UnreachableObjective.ToString(), trace);
            Assert.Contains(SiteFlaw.AccessOrderingFailure.ToString(), trace);
            Assert.Contains("prisoner_pen", trace);
        }

        /// <summary>Replaying the seed selects the same plan, down to the identity and the trace.</summary>
        [Fact]
        public void ReplayingTheSeedReproducesTheSelectedPlan()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan first = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache, seed: 12);
            ScenarioPlan again = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache, seed: 12);

            Assert.Equal(first.Seed, again.Seed);
            Assert.Equal(first.PlanId, again.PlanId);
            Assert.Equal(Trace(fixture, first), Trace(fixture, again));

            ScenarioPlan other = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache, seed: 13);
            Assert.NotEqual(first.PlanId, other.PlanId);
        }

        /// <summary>
        /// The identity is a fact about what the plan says, not about the session that made it. A
        /// second world built the same way, out of a second reading of the catalogue, plans the
        /// same place - which is what "the same semantic inputs and the same seed" has to mean if
        /// a plan is ever to be replayed from a save.
        /// </summary>
        [Fact]
        public void ThePlanIsIdentifiedByWhatItMeansRatherThanByWhenItWasMade()
        {
            ScenarioPlan first = Fixture.ATheftByACrew().Plan(CampGrammar, SiteAffordance.EvidenceCache);
            ScenarioPlan second = Fixture.ATheftByACrew().Plan(CampGrammar, SiteAffordance.EvidenceCache);

            Assert.NotEqual(string.Empty, first.PlanId);
            Assert.Equal(first.PlanId, second.PlanId);
        }

        // -- anchors, occupancy and the causal links -------------------------------------------

        /// <summary>
        /// The anchors carry the ledger's own ids and nothing copied out of it. The strongbox is
        /// anchored because this matter's theft put it there, and the claim it proves is the one
        /// the knowledge graph holds - so a plan cannot drift from the history it was derived from.
        /// </summary>
        [Fact]
        public void EveryAnchorKeepsTheRecordItCameFrom()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioPlan plan = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);

            List<ScenarioAnchor> evidence = plan.AnchorsOf(ScenarioAnchorKind.Evidence);
            ScenarioAnchor anchor = Assert.Single(evidence);

            Assert.Equal(fixture.StolenId, anchor.SubjectId);
            Assert.Equal(fixture.TheftEventId, anchor.EventId);
            Assert.Equal(fixture.TheftFactId, anchor.FactId);
            Assert.NotNull(fixture.World.Knowledge.GetFact(anchor.FactId));

            ScenarioAnchor objective = Assert.Single(plan.AnchorsOf(ScenarioAnchorKind.Objective));
            Assert.Equal(plan.ObjectiveRegionId, objective.RegionId);

            Assert.True(plan.Validation.Get(ScenarioInvariant.CausalReferencesIntact).Held);
        }

        /// <summary>
        /// The prison holds people in its cells and its ledger somewhere else, so an errand after
        /// the prisoner has to reach a region the errand did not come for. That is the whole point
        /// of validating the evidence separately from the objective.
        /// </summary>
        [Fact]
        public void EvidenceIsReachedSeparatelyFromWhatTheErrandCameFor()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Capture(fixture.VictimId);

            ScenarioPlan plan = fixture.FirstPlan(PrisonGrammar, SiteAffordance.PrisonCell);

            ScenarioAnchor captive = Assert.Single(plan.AnchorsOf(ScenarioAnchorKind.Captive));
            ScenarioAnchor evidence = Assert.Single(plan.AnchorsOf(ScenarioAnchorKind.Evidence));

            Assert.Equal(fixture.VictimId, captive.SubjectId);
            Assert.Equal(plan.ObjectiveRegionId, captive.RegionId);
            Assert.NotEqual(captive.RegionId, evidence.RegionId);
            Assert.True(plan.GetRegion(evidence.RegionId).Reachable);
            Assert.True(plan.Validation.Get(ScenarioInvariant.EvidenceReachable).Held);
        }

        /// <summary>
        /// The plan says a region is occupied only where something asserts it is. The camp's
        /// threshold is stood at by the crew that holds the place; the mine, which has no such
        /// region, puts the same three people at the place and in no region at all rather than
        /// scattering them through the workings.
        /// </summary>
        [Fact]
        public void OnlyARegionThatAssertsSomebodyIsInItIsCalledOccupied()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan camp = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);
            ScenarioOccupantRegion garrison = Assert.Single(camp.OccupantRegions);

            Assert.Equal(ScenarioOccupancyKind.Garrison, garrison.Kind);
            Assert.Equal(SiteAffordance.GuardedThreshold, garrison.Affordance);
            Assert.Contains(fixture.ThiefId, Ids(garrison.Occupants));
            Assert.Equal(fixture.CrewId, garrison.OrganizationId);
            Assert.Empty(camp.UnplacedOccupants);

            ScenarioPlan mine = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache);
            Assert.Empty(mine.OccupantRegions);
            Assert.Contains(fixture.ThiefId, Ids(mine.UnplacedOccupants));
        }

        // -- cycles and alternatives -----------------------------------------------------------

        /// <summary>
        /// A cycle is a ring somebody can go round, which in the camp is the bolthole: in at the
        /// gate, out through the leader's quarters. The mine's two ways past the fall rejoin and
        /// are drawn as a ring, and nobody can walk round them, so they are alternatives here and
        /// not cycles - which is the difference between a claim about the drawing and a claim
        /// about play.
        /// </summary>
        [Fact]
        public void ACycleIsARingSomebodyCanActuallyGoRound()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan camp = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);
            ScenarioCycle ring = Assert.Single(camp.Cycles);
            Assert.Equal(ScenarioCycleKind.Reentrant, ring.Kind);
            Assert.Contains("bolthole", ring.Regions);
            Assert.False(ring.Cosmetic);

            ScenarioPlan mine = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache);
            Assert.Empty(mine.Cycles);
            Assert.True(mine.Alternatives.Count > 1);
        }

        /// <summary>
        /// Two ways that ask the same things in the same order are one play however differently
        /// the rooms are spelled, and the second is reported as the rewording it is rather than
        /// counted as a choice.
        /// </summary>
        [Fact]
        public void AnAlternativeIsCountedByWhatItAsksAndNotByTheRoomsItCrosses()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan plan = fixture.Plan(
                Sketch(
                    new[] { Node("hall"), Node("side"), Node("vault", SiteAffordance.EvidenceCache) },
                    Entry("hall", "trespass"),
                    Entry("side", "trespass"),
                    Route("hall", "vault", "trespass", SiteAffordance.LockedBarrier),
                    Route("hall", "vault", "persuade", SiteAffordance.GuardedThreshold),
                    Route("side", "vault", "trespass", SiteAffordance.LockedBarrier)),
                SiteAffordance.EvidenceCache,
                seed: 7,
                build: fixture.Vanilla);

            Assert.True(plan.Planned, Trace(fixture, plan));
            Assert.Equal(2, plan.Alternatives.Count);

            ScenarioAlternative collapsed = Assert.Single(plan.CollapsedAlternatives);
            Assert.Contains("side", collapsed.Regions);
            Assert.NotEqual(string.Empty, collapsed.CollapsedInto);
            Assert.True(plan.Validation.Get(ScenarioInvariant.AlternativesAreStructural).Held);
            Assert.Contains("the same play as", Trace(fixture, plan));
        }

        // -- what a valid plan may not contain -------------------------------------------------

        /// <summary>
        /// A route can be walkable and still demand a mechanic nobody has written a verb for. The
        /// plan may hold one; what it may not do is put it on the way to something required, which
        /// would be promising a route through a mechanic this build does not have.
        /// </summary>
        [Fact]
        public void ARequiredPathThroughSomethingNobodyAnswersIsNotAValidPlan()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan plan = fixture.Plan(
                Sketch(
                    new[] { Node("hall"), Node("vault", SiteAffordance.EvidenceCache) },
                    Entry("hall", "trespass"),
                    Route("hall", "vault", "persuade", SiteAffordance.TrapCluster)),
                SiteAffordance.EvidenceCache,
                seed: 7,
                build: fixture.Vanilla);

            Assert.True(plan.Planned, Trace(fixture, plan));
            Assert.True(plan.Validation.Get(ScenarioInvariant.ObjectiveReachable).Held);

            Assert.False(plan.Valid);
            ScenarioFinding finding = plan.Validation.Get(ScenarioInvariant.RequiredPathsSupported);
            Assert.False(finding.Held);
            Assert.Contains(ScenarioSupport.Unanswered.ToString(), finding.Reason);
            Assert.Contains(SiteAffordance.TrapCluster.ToString(), Trace(fixture, plan));
        }

        /// <summary>
        /// A place whose only way to what the matter came for is dug is not a plan on a build that
        /// cannot read what stands in a place (`D067`). The reason travels with the refusal.
        /// </summary>
        [Fact]
        public void ARouteThisBuildCannotKeepNeverEntersAPlan()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Vanilla.SetCapability(VanillaCapability.ReadPlaceContents, false);

            ScenarioPlan plan = fixture.Plan(
                Sketch(
                    new[] { Node("shaft"), Node("vault", SiteAffordance.EvidenceCache) },
                    Entry("shaft", "mine_bypass", SiteAffordance.DiggableBypass),
                    Route("shaft", "vault", "mine_bypass", SiteAffordance.DiggableBypass)),
                SiteAffordance.EvidenceCache,
                seed: 7,
                build: fixture.Vanilla);

            Assert.False(plan.Planned);
            Assert.Contains(SiteFlaw.RoutePromiseUnsupported.ToString(), Trace(fixture, plan));
            Assert.Contains(VanillaCapability.ReadPlaceContents.ToString(), Trace(fixture, plan));
        }

        /// <summary>
        /// The other side of the same rule. The mine can be dug into and can also be talked into,
        /// so a build that cannot dig still has a plan - the dug routes stand in it, marked as
        /// routes this build cannot keep, and nothing required runs through one.
        /// </summary>
        [Fact]
        public void ARouteThisBuildCannotKeepMayStandInAPlanNothingRequiredNeeds()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Vanilla.SetCapability(VanillaCapability.ReadPlaceContents, false);

            ScenarioPlan plan = fixture.Plan(MineGrammar, SiteAffordance.EvidenceCache);

            Assert.True(plan.Valid, Trace(fixture, plan));

            bool unsupported = false;
            for (int i = 0; i < plan.Routes.Count; i++)
            {
                unsupported |= plan.Routes[i].Support == ScenarioSupport.Unsupported;
            }

            Assert.True(unsupported, "no route was refused on a build that cannot dig");
            Assert.Contains(ScenarioSupport.Unsupported.ToString(), Trace(fixture, plan));
        }

        /// <summary>A matter that leaves nothing at a place has no scenario to plan.</summary>
        [Fact]
        public void AMatterWithNothingAtThePlaceIsNotAPlan()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            ScenarioPlan plan = ScenarioPlanner.Plan(
                fixture.World,
                EntityId.None,
                Fixture.Library().Get(CampGrammar),
                SiteAffordance.EvidenceCache,
                7,
                SiteCandidates.DefaultBatch,
                StandardActions.CreateRegistry(),
                fixture.Vanilla);

            Assert.False(plan.Planned);
            Assert.NotEmpty(plan.Refusals);
            Assert.Contains("no plan", Trace(fixture, plan));
        }

        // -- what this step deliberately does not do -------------------------------------------

        /// <summary>
        /// The sockets are carried, named and empty. Filling one needs a physical realization, and
        /// no BQ site has one (BQ-140) - so the plan says which authored piece each region waits
        /// on and fills none of them.
        /// </summary>
        [Fact]
        public void SocketsAreCarriedAndNothingFillsThem()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioPlan plan = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);

            ScenarioSocket socket = Assert.Single(plan.Sockets);
            Assert.Equal("camp_gate", socket.Socket);
            Assert.Equal("approach", socket.RegionId);
            Assert.False(socket.Filled);
            Assert.NotNull(plan.GetRegion(socket.RegionId));
        }

        /// <summary>
        /// The plan does not get a second description of a place. It fills in the one genesis
        /// already validates, contents and all, which is what keeps BQ-087's refusals the only
        /// rules about what a place must be.
        /// </summary>
        [Fact]
        public void ThePlanHandsGenesisTheOneVocabularyForAPlace()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioPlan plan = fixture.Plan(CampGrammar, SiteAffordance.EvidenceCache);

            SitePlan handover = plan.NewSitePlan(
                fixture.World.NewId("zone"), "the camp off the drove road");

            Assert.NotNull(handover);
            Assert.Equal(plan.ThreadId, handover.ThreadId);
            Assert.Equal(plan.GrammarId, handover.GrammarId);
            Assert.Equal(plan.Seed, handover.Seed);

            SiteGenesisResult result = SiteGenesis.Establish(
                fixture.World, handover, new SandboxStager(fixture.Vanilla), fixture.Vanilla.Now);
            Assert.True(result.Created, string.Join("; ", result.Reasons));
        }

        // -- helpers ---------------------------------------------------------------------------

        private const string CampGrammar = "site.bandit_camp";
        private const string MineGrammar = "site.collapsed_mine";
        private const string PrisonGrammar = "site.makeshift_prison";

        private static string Trace(Fixture fixture, ScenarioPlan plan)
        {
            return NarrativeInspector.DescribeScenarioPlan(fixture.World, plan);
        }

        private static void Explains(Fixture fixture, ScenarioPlan plan)
        {
            string trace = Trace(fixture, plan);

            Assert.Contains(plan.PlanId, trace);
            Assert.Contains(plan.GrammarId, trace);
            Assert.Contains(plan.ObjectiveRegionId, trace);
            Assert.Contains("regions " + plan.Regions.Count, trace);
            Assert.Contains("routes " + plan.Routes.Count, trace);
            Assert.Contains("invariants", trace);
        }

        private static List<EntityId> Ids(IReadOnlyList<ScenarioOccupant> occupants)
        {
            List<EntityId> ids = new List<EntityId>();
            for (int i = 0; i < occupants.Count; i++)
            {
                ids.Add(occupants[i].NpcId);
            }

            return ids;
        }

        /// <summary>
        /// A place sketched in code rather than authored, for the shapes no shipped grammar should
        /// be bent into. Everything is required, so composition draws the whole of it.
        /// </summary>
        private static SiteGrammar Sketch(SiteNodeSpec[] nodes, params SiteRouteSpec[] routes)
        {
            return new SiteGrammar("sketch", "hideout", true, nodes, routes);
        }

        private static SiteNodeSpec Node(string id, params SiteAffordance[] affordances)
        {
            return new SiteNodeSpec(id, true, affordances, string.Empty);
        }

        private static SiteRouteSpec Entry(string to, string actionId, params SiteAffordance[] affordances)
        {
            return new SiteRouteSpec(SiteGrammar.Outside, to, actionId, false, affordances);
        }

        private static SiteRouteSpec Route(
            string from, string to, string actionId, params SiteAffordance[] affordances)
        {
            return new SiteRouteSpec(from, to, actionId, false, affordances);
        }

        /// <summary>
        /// A theft a crew committed: the goods are still in the thief's hands, the crew is real,
        /// and the people it was done to are elsewhere.
        /// </summary>
        internal sealed class Fixture
        {
            private Organization _crew;

            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                CrewIds = new List<EntityId>();
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal NarrativeThread Thread { get; private set; }

            internal EntityId ThiefId { get; private set; }

            internal EntityId VictimId { get; private set; }

            internal EntityId StolenId { get; private set; }

            internal EntityId TheftFactId { get; private set; }

            internal EntityId TheftEventId { get; private set; }

            internal List<EntityId> CrewIds { get; }

            internal static Fixture ATheftByACrew()
            {
                NarrativeWorldState world = new NarrativeWorldState(91);
                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                Fixture fixture = new Fixture(world, vanilla);

                EntityId town = world.NewId("zone");

                fixture.ThiefId = fixture.Person("Renn", town);
                fixture.VictimId = fixture.Person("Mab", town);
                EntityId witness = fixture.Person("Coll", town);

                fixture.CrewIds.Add(fixture.ThiefId);
                fixture.CrewIds.Add(fixture.Person("Bryn", town));
                fixture.CrewIds.Add(fixture.Person("Tace", town));

                fixture._crew = world.Registry.Add(
                    new Organization(world.NewId("org"), "the road crew", "criminal_crew")
                    {
                        LeaderId = fixture.ThiefId
                    });
                for (int i = 0; i < fixture.CrewIds.Count; i++)
                {
                    fixture._crew.MemberIds.Add(fixture.CrewIds[i]);
                    world.Registry.GetNpc(fixture.CrewIds[i]).OrganizationIds.Add(fixture._crew.Id);
                }

                fixture.StolenId = fixture.Object(fixture.ThiefId, "a banded strongbox", 400);

                WorldEvent theft = world.Record(
                    WorldEventType.Theft,
                    fixture.ThiefId,
                    fixture.VictimId,
                    vanilla.Now,
                    magnitude: 0.6,
                    zone: town,
                    witnesses: new[] { witness },
                    evidence: new[] { fixture.StolenId });
                fixture.TheftEventId = theft.Id;

                Fact stole = new Fact(
                    world.NewId("fact"),
                    fixture.ThiefId,
                    "stole",
                    fixture.StolenId,
                    string.Empty,
                    TruthState.True,
                    secrecy: 60,
                    originEvent: theft.Id);
                stole.EvidenceIds.Add(fixture.StolenId);
                world.Knowledge.AddFact(stole);
                fixture.TheftFactId = stole.Id;

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "road_crew", vanilla.Now)
                {
                    State = ThreadState.Active,
                    OriginEventId = theft.Id
                };
                thread.FactIds.Add(stole.Id);
                thread.ParticipantIds.Add(fixture.ThiefId);
                thread.ParticipantIds.Add(fixture.VictimId);
                world.Threads.Add(thread);
                fixture.Thread = thread;

                return fixture;
            }

            internal EntityId CrewId => _crew.Id;

            internal ScenarioPlan Plan(string grammarId, SiteAffordance objective, ulong seed = 7)
            {
                return Plan(Library().Get(grammarId), objective, seed, Vanilla);
            }

            /// <summary>
            /// The first batch seed this kind of place has a plan at. Searched rather than
            /// hardcoded, so a corrected grammar moves the seed instead of breaking the claim.
            /// </summary>
            internal ScenarioPlan FirstPlan(string grammarId, SiteAffordance objective)
            {
                SiteGrammar grammar = Library().Get(grammarId);
                for (ulong seed = 0; seed < 40; seed++)
                {
                    ScenarioPlan plan = Plan(grammar, objective, seed, Vanilla);
                    if (plan.Valid)
                    {
                        return plan;
                    }
                }

                throw new InvalidOperationException(grammarId + " has no plan for an errand after " + objective);
            }

            internal ScenarioPlan Plan(
                SiteGrammar grammar, SiteAffordance objective, ulong seed, IVanillaState build)
            {
                Assert.NotNull(grammar);
                return ScenarioPlanner.Plan(
                    World,
                    Thread.Id,
                    grammar,
                    objective,
                    seed,
                    SiteCandidates.DefaultBatch,
                    StandardActions.CreateRegistry(),
                    build);
            }

            internal void Capture(EntityId who)
            {
                World.Record(
                    WorldEventType.Captured,
                    ThiefId,
                    who,
                    Vanilla.Now,
                    magnitude: 0.7,
                    threadId: Thread.Id);
            }

            private EntityId Person(string name, EntityId zone)
            {
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name));
                Vanilla.Define(npc.Id, level: 3, money: 20, zone: zone);
                return npc.Id;
            }

            private EntityId Object(EntityId holder, string name, int value)
            {
                ItemDescriptor item = new ItemDescriptor(World.NewId("item"), name, "goods", value, "chest");
                Vanilla.GiveItem(holder, item);
                return item.Id;
            }

            internal static SiteGrammarLibrary Library()
            {
                ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                    Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
                Assert.Empty(loaded.Diagnostics);

                IReadOnlyList<ContentDiagnostic> diagnostics;
                SiteGrammarLibrary library = SiteGrammarContent.CreateLibrary(loaded.Bundle, out diagnostics);
                Assert.Empty(diagnostics);
                return library;
            }

            private static string RepositoryRoot()
            {
                DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                {
                    directory = directory.Parent;
                }

                if (directory == null)
                {
                    throw new InvalidOperationException("Could not locate repository root.");
                }

                return directory.FullName;
            }
        }
    }
}
