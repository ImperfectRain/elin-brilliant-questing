using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The BQ-044 laboratory: true theft, planted physical evidence, an innocent target and an
    /// authority that can be made to act on the wrong proof while the real fact remains recoverable.
    /// </summary>
    public sealed class FalseAccusationSituation
    {
        public const string ArchetypeId = "false_accusation";

        private FalseAccusationSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId ThiefId { get; private set; }

        public EntityId VictimId { get; private set; }

        public EntityId InnocentId { get; private set; }

        public EntityId GuardId { get; private set; }

        public EntityId ItemId { get; private set; }

        public EntityId TrueTheftFactId { get; private set; }

        public EntityId MarketZoneId { get; private set; }

        public static FalseAccusationSituation Create(
            NarrativeWorldState world,
            ISituationStager stager,
            EntityId player,
            EntityId market,
            GameTime now)
        {
            FalseAccusationSituation situation = new FalseAccusationSituation
            {
                MarketZoneId = market,
                ItemId = world.NewId("item")
            };

            world.Registry.Add(new NarrativeSite(market, "Hollen market", "market"));

            NarrativeNpc thief = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Kest")
            {
                Occupation = "porter",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc victim = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Marda")
            {
                Occupation = "reliquary keeper",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc innocent = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Tovan")
            {
                Occupation = "fishmonger",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc guard = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Ovel")
            {
                Occupation = "guard",
                Importance = NarrativeImportance.Known
            });
            guard.Roles.Add(AuthorityPolicy.GuardRole);

            situation.ThiefId = thief.Id;
            situation.VictimId = victim.Id;
            situation.InnocentId = innocent.Id;
            situation.GuardId = guard.Id;

            ItemDescriptor reliquary = new ItemDescriptor(
                situation.ItemId,
                "silver reliquary",
                "jewelry",
                650,
                "jewelry");

            stager.StageCharacter(thief.Id, new CharacterBlueprint(thief.Name)
                    .With(VanillaAttribute.Dexterity, 14),
                market);
            stager.StageCharacter(victim.Id, new CharacterBlueprint(victim.Name)
                    .With(VanillaAttribute.Will, 12),
                market);
            stager.StageCharacter(innocent.Id, new CharacterBlueprint(innocent.Name)
                    .With(VanillaAttribute.Will, 9),
                market);
            stager.StageCharacter(guard.Id, new CharacterBlueprint(guard.Name)
                    .With(VanillaAttribute.Will, 13)
                    .With(VanillaAttribute.Perception, 12),
                market);
            stager.StageItem(player, reliquary);

            WorldEvent theft = world.Record(
                WorldEventType.Theft,
                thief.Id,
                victim.Id,
                now,
                0.7,
                market,
                evidence: new[] { situation.ItemId },
                tags: new[] { EventTags.Unnoticed });

            Fact trueTheft = new Fact(
                world.NewId("fact"),
                thief.Id,
                FactPredicates.Stole,
                situation.ItemId,
                reliquary.Name,
                TruthState.True,
                secrecy: 40,
                originEvent: theft.Id);
            trueTheft.EvidenceIds.Add(situation.ItemId);
            world.Knowledge.AddFact(trueTheft);
            situation.TrueTheftFactId = trueTheft.Id;

            world.Knowledge.Teach(thief.Id, trueTheft.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(player, trueTheft.Id, KnowledgeSource.Hearsay, 0.65, now, false, victim.Id);

            thief.Goals.Add(new NpcGoal("avoid_exposure", trueTheft.Id, 80));
            victim.Goals.Add(new NpcGoal("recover_property", situation.ItemId, 70));
            innocent.Goals.Add(new NpcGoal("keep_reputation", trueTheft.Id, 60));
            guard.Goals.Add(new NpcGoal("settle_the_case", victim.Id, 55));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 35,
                Importance = 55,
                State = ThreadState.Active,
                OriginEventId = theft.Id
            };
            thread.ParticipantIds.Add(thief.Id);
            thread.ParticipantIds.Add(victim.Id);
            thread.ParticipantIds.Add(innocent.Id);
            thread.ParticipantIds.Add(guard.Id);
            thread.FactIds.Add(trueTheft.Id);
            thread.SiteIds.Add(market);
            thread.OpenQuestions.Add("Who will the market believe stole the reliquary?");
            thread.OpenQuestions.Add("What proof survives the accusation?");

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
