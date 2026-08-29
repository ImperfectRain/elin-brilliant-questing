using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The laboratory for physical/world verbs: a mine road stopped by a rockfall.
    ///
    /// Nobody is hiding the answer and nobody can authorize the stones out of the way. A social
    /// player can still ask the foreman to be let through by the old service crawl, but the
    /// direct route belongs to a build that can put hands, picks or force on the barrier. The
    /// situation is deliberately narrow: it proves that a world obstacle can be answered through
    /// the world rather than by adding a one-off quest flag.
    /// </summary>
    public sealed class BlockedPassageSituation
    {
        public const string ArchetypeId = "blocked_passage";

        private BlockedPassageSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId ForemanId { get; private set; }

        public EntityId MineId { get; private set; }

        public EntityId RockfallId { get; private set; }

        public EntityId BlockageFactId { get; private set; }

        public EntityId TrailZoneId { get; private set; }

        public EntityId MineZoneId { get; private set; }

        public static BlockedPassageSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId trail, GameTime now)
        {
            BlockedPassageSituation situation = new BlockedPassageSituation
            {
                TrailZoneId = trail,
                MineZoneId = world.NewId("zone"),
                MineId = world.NewId("place"),
                RockfallId = world.NewId("item")
            };

            world.Registry.Add(new NarrativeSite(trail, "Rillford road", "road"));
            NarrativeSite mine = world.Registry.Add(new NarrativeSite(situation.MineZoneId, "the old garnet mine", "mine")
            {
                Restricted = true,
                Persistence = SitePersistence.Persistent
            });
            mine.ImportantObjectIds.Add(situation.RockfallId);

            NarrativeNpc foreman = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Mara")
            {
                Occupation = "mine foreman",
                Importance = NarrativeImportance.Known
            });
            situation.ForemanId = foreman.Id;
            foreman.Personality.Honesty = 0.8;

            stager.StageCharacter(foreman.Id, new CharacterBlueprint(foreman.Name)
                    .With(VanillaAttribute.Will, 12)
                    .With(VanillaAttribute.Strength, 12),
                trail);

            ItemDescriptor rockfall = new ItemDescriptor(situation.RockfallId, "the rockfall", "rockfall", 1000, "stone");
            stager.StageItem(trail, rockfall);

            Fact blockage = new Fact(
                world.NewId("fact"),
                situation.MineZoneId,
                FactPredicates.BlocksAccessTo,
                situation.RockfallId,
                PhysicalObstacleSpec.Rockfall.ToFactValue(),
                TruthState.True);
            blockage.EvidenceIds.Add(situation.RockfallId);
            world.Knowledge.AddFact(blockage);
            situation.BlockageFactId = blockage.Id;

            world.Knowledge.Teach(foreman.Id, blockage.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(player, blockage.Id, KnowledgeSource.Witnessed, 1.0, now, true);

            foreman.Goals.Add(new NpcGoal("open_the_mine", situation.MineZoneId, 75));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 25,
                Importance = 45,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(foreman.Id);
            thread.FactIds.Add(blockage.Id);
            thread.SiteIds.Add(trail);
            thread.SiteIds.Add(situation.MineZoneId);
            thread.OpenQuestions.Add("How can anyone reach the old garnet mine?");
            thread.Escalation.Add(new EscalationStep("miners_leave", 5, "The idle miners leave Rillford."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        public void StockThePlayer(NarrativeWorldState world, ISituationStager stager, EntityId player)
        {
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "an iron pick", "tool", 80, "pick"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a coil of rope", "tool", 30, "rope"));
        }
    }
}
