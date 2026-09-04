using System;
using System.Collections.Generic;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Scenes
{
    /// <summary>
    /// One authoritative starting world the <c>scene</c> scenario can play storylets against.
    ///
    /// A fixture is *only* starting state: people, where they are, what is true, who knows it, what
    /// they want, and the thread that carries it. It selects no storylet, casts nobody, decides
    /// nothing and words nothing - the production engine does all of that afterwards from exactly
    /// this state, which is what makes the probe worth looking at.
    /// </summary>
    public sealed class SceneFixture
    {
        public SceneFixture(
            NarrativeWorldState world,
            SandboxVanillaState vanilla,
            NarrativeThread thread,
            EntityId focusFactId,
            EntityId player)
        {
            World = world;
            Vanilla = vanilla;
            Thread = thread;
            FocusFactId = focusFactId;
            Player = player;
        }

        public NarrativeWorldState World { get; }

        public SandboxVanillaState Vanilla { get; }

        /// <summary>The matter the scenes are about. Storylets require the focus to belong to it.</summary>
        public NarrativeThread Thread { get; }

        /// <summary>The claim the storylets dramatize. Never authored by a storylet, only read.</summary>
        public EntityId FocusFactId { get; }

        public EntityId Player { get; }

        /// <summary>The focus as the knowledge graph holds it, or null when the fixture is broken.</summary>
        public Fact Focus => World.Knowledge.GetFact(FocusFactId);
    }

    /// <summary>
    /// A named starting situation, and how to build one.
    ///
    /// The registry below is the only place that knows which fixtures exist, in the same spirit as
    /// <c>LabCatalog</c>: adding one is a single entry, and nothing in the scenario itself branches
    /// on which situation is being run.
    /// </summary>
    public sealed class SceneSituation
    {
        private readonly Func<ulong, SceneFixture> _build;

        public SceneSituation(string id, string predicate, string summary, Func<ulong, SceneFixture> build)
        {
            Id = id;
            Predicate = predicate;
            Summary = summary;
            _build = build;
        }

        /// <summary>The name passed to <c>--situation</c>.</summary>
        public string Id { get; }

        /// <summary>The focus fact's predicate, so `list-situations` says which storylets it can reach.</summary>
        public string Predicate { get; }

        public string Summary { get; }

        public SceneFixture Build(ulong seed) => _build(seed);
    }

    /// <summary>
    /// Every situation the shipped world builders and storylet content can actually produce a
    /// scene from.
    ///
    /// The list is short on purpose, and each entry earns its place twice: a production situation
    /// builder authors it, and at least one routed storylet in <c>content/storylets</c> declares
    /// that predicate as its focus. A predicate with a builder and no storylet, or a storylet with
    /// no builder, is left out rather than faked - see the scenario's own description for which,
    /// and why.
    /// </summary>
    public static class SceneSituations
    {
        public const string DefaultId = "theft";

        private static readonly SceneSituation[] Registry =
        {
            new SceneSituation(
                "theft", FactPredicates.Stole,
                "the three-NPC petty theft: A took it, B lost it, C saw it",
                Theft),
            new SceneSituation(
                "debt", FactPredicates.Owes,
                "a porter owes a merchant more than she has",
                Debt),
            new SceneSituation(
                "shortage", FactPredicates.Needs,
                "a village reeve needs bread the broken mill cannot make",
                Shortage),
            new SceneSituation(
                "extortion", FactPredicates.Extorted,
                "a shopkeeper is paying a racketeer every month, and can prove nothing",
                Extortion),
            new SceneSituation(
                "danger", FactPredicates.AtRisk,
                "a witness is being hunted for what she saw, and the watch half believes it",
                Danger)
        };

        public static IReadOnlyList<SceneSituation> All => Registry;

        /// <summary>The situation with this id, or null. Ids are matched exactly and case-insensitively.</summary>
        public static SceneSituation Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            string wanted = id.Trim();
            for (int i = 0; i < Registry.Length; i++)
            {
                if (string.Equals(Registry[i].Id, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return Registry[i];
                }
            }

            return null;
        }

        public static string KnownIds()
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < Registry.Length; i++)
            {
                ids.Add(Registry[i].Id);
            }

            return string.Join(", ", ids);
        }

        // -- the fixtures ------------------------------------------------------------------------
        //
        // Each one is the production builder plus the minimum a laboratory has to supply that a
        // running game supplies for free: a player character in the registry, and a stager. Nothing
        // else is added, and in particular nothing is taught to anybody the builder did not teach.

        private static SceneFixture Theft(ulong seed)
        {
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            return new SceneFixture(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId, lab.Player);
        }

        private static SceneFixture Debt(ulong seed)
        {
            Bench bench = Bench.Create(seed);
            DebtSituation situation = DebtSituation.Create(
                bench.World, new SandboxStager(bench.Vanilla), bench.Zone, bench.Vanilla.Now);
            return bench.Around(situation.Thread, situation.DebtFactId);
        }

        private static SceneFixture Shortage(ulong seed)
        {
            Bench bench = Bench.Create(seed);
            ShortageSituation situation = ShortageSituation.Create(
                bench.World, new SandboxStager(bench.Vanilla), bench.Player, bench.Zone, bench.Vanilla.Now);

            // The builder teaches the bread shortage only to the reeve it is about, and one person
            // cannot hold two roles - so the sole knower is the subject, and nobody is left to be
            // the neighbour. The builder's own note is that this is common knowledge ("a village
            // does not keep quiet about having no bread"), so the fixture states that as starting
            // knowledge. The physician rather than the miller, because the miller is staged at the
            // mill and casting draws from whoever is in the thread's first site.
            //
            // Starting state only: no fact is authored, no truth changes, and nothing here decides
            // who ends up cast.
            bench.World.Knowledge.Teach(
                situation.PhysicianId, situation.BreadDemandId, KnowledgeSource.Witnessed, 0.9, bench.Vanilla.Now, false);
            return bench.Around(situation.Thread, situation.BreadDemandId);
        }

        private static SceneFixture Extortion(ulong seed)
        {
            Bench bench = Bench.Create(seed);
            ProtectionRacketSituation situation = ProtectionRacketSituation.Create(
                bench.World, new SandboxStager(bench.Vanilla), bench.Player, bench.Zone, bench.Vanilla.Now);
            return bench.Around(situation.Thread, situation.RacketFactId);
        }

        /// <summary>
        /// The hunted witness rather than the marauding beast, though both author `at_risk`.
        ///
        /// The beast situation's thread has two participants and one of them is the beast, so
        /// `SceneStatus` correctly reports "there is nobody left to take this up with" and every
        /// storylet refuses before casting begins. That is the production rule working, not a
        /// fixture problem, so the fixture uses the situation whose thread is three people.
        /// </summary>
        private static SceneFixture Danger(ulong seed)
        {
            Bench bench = Bench.Create(seed);
            HuntedWitnessSituation situation = HuntedWitnessSituation.Create(
                bench.World, new SandboxStager(bench.Vanilla), bench.Player, bench.Zone, bench.Vanilla.Now);

            // The builder teaches the danger to the witness it is about, to the player (whom
            // searched roles exclude) and to the guard (who is at the watch house rather than in
            // the lane). Hobb is standing in the lane and is described as the person the town's
            // reaction lands on, so the fixture gives him the same hearsay the guard already has.
            bench.World.Knowledge.Teach(
                situation.NeighbourId, situation.ExposureFactId, KnowledgeSource.Hearsay, 0.7,
                bench.Vanilla.Now, false, situation.WitnessId);
            return bench.Around(situation.Thread, situation.ExposureFactId);
        }

        /// <summary>
        /// The empty world the non-theft builders expect to be called against: a seeded state, one
        /// zone, and a player who is a character in the graph like anybody else.
        ///
        /// <see cref="TheftLaboratory"/> does the same thing and a good deal more; the builders here
        /// need only this much, and duplicating the laboratory to get it would be the switch-full-of-
        /// scenario-logic this registry exists to avoid.
        /// </summary>
        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId player, EntityId zone)
            {
                World = world;
                Vanilla = vanilla;
                Player = player;
                Zone = zone;
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public EntityId Player { get; }

            public EntityId Zone { get; }

            public static Bench Create(ulong seed)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                EntityId player = world.NewId("npc");
                EntityId zone = world.NewId("zone");

                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, money: 2000, zone: zone);
                vanilla.SetAttribute(player, VanillaAttribute.Charisma, 11);
                vanilla.SetAttribute(player, VanillaAttribute.Will, 11);
                vanilla.SetAttribute(player, VanillaAttribute.Perception, 12);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 6);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                new ConsequenceEngine(world, vanilla).Attach();
                return new Bench(world, vanilla, player, zone);
            }

            public SceneFixture Around(NarrativeThread thread, EntityId focusFactId)
            {
                return new SceneFixture(World, Vanilla, thread, focusFactId, Player);
            }
        }
    }
}
