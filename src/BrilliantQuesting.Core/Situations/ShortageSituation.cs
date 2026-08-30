using System.Collections.Generic;
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
    /// The laboratory for production: a village short of two things, where nothing that would
    /// answer either exists yet.
    ///
    /// The earlier labs closed a route by hiding something - the death lab made sure nobody knew
    /// anything, the racket lab made sure there was nothing to find. This one closes every route
    /// but making by a plainer means: the goods are not in the world. Nobody in Kell's Ford is
    /// holding bread anyone would call bread or a remedy anyone would drink, so there is nothing
    /// to buy, lift, extort or be given, and no secret whose telling would produce a loaf. Two
    /// people are short, and the only objects in the village are the raw stuff the shortage is
    /// made of.
    ///
    /// Both halves of the family get a route through it, and they are genuinely different answers.
    /// The bread can be baked, sack by sack, for as long as anyone wants bread; or the mill wheel
    /// that stopped can be mended once, which ends the shortage rather than covering it. The
    /// remedy has no such cause behind it and has to be compounded either way.
    ///
    /// The threshold is the other half of the point, and it is deliberately not a matter of odds.
    /// The player starts with a sack of coarse meal, which is food, and which Herrick will not
    /// take: it is quality 10 against a demand for 30, and a demand is a property constraint, so
    /// the meal is the wrong object rather than a bad roll. The verb says so in as many words
    /// instead of offering a route that could never land.
    /// </summary>
    public sealed class ShortageSituation
    {
        public const string ArchetypeId = "village_shortage";

        private ShortageSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>Speaks for the village's stores. Wants bread anybody would eat.</summary>
        public EntityId ReeveId { get; private set; }

        /// <summary>Has a fevered child and no remedy worth the name.</summary>
        public EntityId PhysicianId { get; private set; }

        /// <summary>Owns the mill, and has not been able to turn it since the spring.</summary>
        public EntityId MillerId { get; private set; }

        /// <summary>The thing that stopped. Mending it ends the bread shortage at its cause.</summary>
        public EntityId MillWheelId { get; private set; }

        /// <summary>"The village is short of bread." Caused by the wheel, and says so.</summary>
        public EntityId BreadDemandId { get; private set; }

        /// <summary>"There is no remedy for the child." Caused by nothing; only making answers it.</summary>
        public EntityId RemedyDemandId { get; private set; }

        /// <summary>"The wheel is broken." Subject is the wheel itself.</summary>
        public EntityId WheelDamageId { get; private set; }

        public EntityId VillageZoneId { get; private set; }

        public EntityId MillZoneId { get; private set; }

        /// <summary>What the reeve will accept: quality 30, which coarse meal is not.</summary>
        public static readonly ProductionSpec Bread = new ProductionSpec("food", 30);

        /// <summary>What the physician will accept. Dearer work, and it eats more herbs for it.</summary>
        public static readonly ProductionSpec Remedy = new ProductionSpec("medicine", 40);

        public static ShortageSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId village, GameTime now)
        {
            ShortageSituation situation = new ShortageSituation
            {
                VillageZoneId = village,
                MillZoneId = world.NewId("zone")
            };

            world.Registry.Add(new NarrativeSite(village, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeSite(situation.MillZoneId, "the mill", "workshop"));

            NarrativeNpc reeve = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Herrick")
            {
                Occupation = "reeve",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc physician = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Sabra")
            {
                Occupation = "physician",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc miller = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Doran")
            {
                Occupation = "miller",
                Importance = NarrativeImportance.Known
            });

            situation.ReeveId = reeve.Id;
            situation.PhysicianId = physician.Id;
            situation.MillerId = miller.Id;
            situation.MillWheelId = world.NewId("item");

            // Nobody here is hiding anything. There is no secret to be got out of them, which is
            // exactly why no amount of talking produces a loaf.
            reeve.Personality.Honesty = 0.9;
            physician.Personality.Honesty = 0.9;
            miller.Personality.Honesty = 0.8;

            ItemDescriptor wheel = new ItemDescriptor(situation.MillWheelId, "the mill wheel", "machine", 900, "mill_wheel");

            stager.StageCharacter(reeve.Id, new CharacterBlueprint(reeve.Name)
                    .With(VanillaAttribute.Will, 11).With(VanillaAttribute.Charisma, 11),
                village);
            stager.StageCharacter(physician.Id, new CharacterBlueprint(physician.Name)
                    .With(VanillaAttribute.Learning, 13).With(VanillaSkill.Alchemy, 9),
                village);
            stager.StageCharacter(miller.Id, new CharacterBlueprint(miller.Name)
                    .With(VanillaAttribute.Strength, 12),
                situation.MillZoneId);

            // The wheel belongs to the mill, not to the miller's pockets. That is what makes
            // mending it a thing you do by standing in front of it.
            stager.StageItem(situation.MillZoneId, wheel);

            // -- what is true -----------------------------------------------------------------
            // Ownership is a fact, so the repair credits the man whose mill it is rather than
            // whoever happened to be stood nearby.
            Fact owns = new Fact(
                world.NewId("fact"), miller.Id, FactPredicates.Possesses, situation.MillWheelId,
                wheel.Name, TruthState.True);
            world.Knowledge.AddFact(owns);

            Fact damage = new Fact(
                world.NewId("fact"), situation.MillWheelId, FactPredicates.Damaged, EntityId.None,
                "the shaft is split", TruthState.True);
            damage.EvidenceIds.Add(situation.MillWheelId);
            world.Knowledge.AddFact(damage);
            situation.WheelDamageId = damage.Id;

            // The bread shortage names the wheel as its cause. That link is the whole of why one
            // repair can close it: the demand is not a quest step, it is a consequence.
            Fact bread = new Fact(
                world.NewId("fact"), reeve.Id, FactPredicates.Needs, situation.MillWheelId,
                Bread.ToFactValue(), TruthState.True);
            world.Knowledge.AddFact(bread);
            situation.BreadDemandId = bread.Id;

            // The child's fever has no broken machine behind it, so there is nothing to mend and
            // the only route is the compounding itself.
            Fact remedy = new Fact(
                world.NewId("fact"), physician.Id, FactPredicates.Needs, EntityId.None,
                Remedy.ToFactValue(), TruthState.True);
            world.Knowledge.AddFact(remedy);
            situation.RemedyDemandId = remedy.Id;

            // -- who knows what ---------------------------------------------------------------
            // All of it is common knowledge. A village does not keep quiet about having no bread,
            // and an investigation route would have nothing to uncover even if one were tried.
            world.Knowledge.Teach(reeve.Id, bread.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(physician.Id, remedy.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(miller.Id, damage.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(reeve.Id, damage.Id, KnowledgeSource.Witnessed, 0.9, now, true);

            reeve.Goals.Add(new NpcGoal("feed_the_village", village, 80));
            physician.Goals.Add(new NpcGoal("protect", physician.Id, 85));
            miller.Goals.Add(new NpcGoal("turn_the_mill", situation.MillWheelId, 70));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 35,
                Importance = 55,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(reeve.Id);
            thread.ParticipantIds.Add(physician.Id);
            thread.ParticipantIds.Add(miller.Id);
            thread.FactIds.Add(damage.Id);
            thread.FactIds.Add(bread.Id);
            thread.FactIds.Add(remedy.Id);
            thread.SiteIds.Add(village);
            thread.SiteIds.Add(situation.MillZoneId);
            thread.OpenQuestions.Add("Where is Kell's Ford to get bread?");
            thread.OpenQuestions.Add("Who can compound something the child will keep down?");

            thread.Escalation.Add(new EscalationStep("child_worsens", 4, "The child stops taking water."));
            thread.Escalation.Add(new EscalationStep("village_leaves", 10, "Kell's Ford starts to empty."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        /// <summary>
        /// What the player has in their pack when they walk in: raw stuff, and one thing that is
        /// food but is not bread.
        ///
        /// Staged apart from the village because it is the player's state rather than the world's,
        /// and because the tests need to take pieces of it away to reach the states worth pinning.
        /// </summary>
        public void StockThePlayer(NarrativeWorldState world, ISituationStager stager, EntityId player)
        {
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a sack of coarse meal", "food", 5, "flour", 10));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a sack of grain", "grain", 12, "wheat"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a second sack of grain", "grain", 12, "wheat"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a bundle of feverfew", "herb", 20, "herb"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a bundle of sorrel", "herb", 18, "herb"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a jar of resin", "reagent", 25, "resin"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "an oak plank", "plank", 30, "plank"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a second oak plank", "plank", 30, "plank"));
        }
    }

    /// <summary>
    /// What happens when a shortage is allowed to run.
    ///
    /// The handler does not invent hidden goods or move vanilla actors. It records the durable
    /// deterioration: people are harmed by the lack, then the settlement starts losing confidence
    /// and influence while the unmet demand remains true.
    /// </summary>
    public sealed class ShortageEscalation : IThreadEscalationHandler
    {
        private readonly IVanillaState _vanilla;

        public ShortageEscalation(IVanillaState vanilla)
        {
            _vanilla = vanilla;
        }

        public void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now)
        {
            EntityId village = thread.SiteIds.Count > 0 ? thread.SiteIds[0] : EntityId.None;
            EntityId spokesman = thread.ParticipantIds.Count > 0 ? thread.ParticipantIds[0] : EntityId.None;

            switch (step.Id)
            {
                case "child_worsens":
                    thread.Tension += 20;
                    world.Record(
                        WorldEventType.Harmed,
                        spokesman,
                        FindFirstOpenDemandSubject(world, thread),
                        now,
                        0.45,
                        village,
                        related: OpenDemandIds(world, thread),
                        threadId: thread.Id);
                    break;

                case "village_leaves":
                    thread.Tension += 25;
                    _vanilla.ChangeInfluence(village, -8);
                    world.Record(
                        WorldEventType.ThreadEscalated,
                        spokesman,
                        village,
                        now,
                        0.7,
                        village,
                        related: OpenDemandIds(world, thread),
                        threadId: thread.Id);
                    break;
            }
        }

        private static EntityId FindFirstOpenDemandSubject(NarrativeWorldState world, NarrativeThread thread)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    return fact.Subject;
                }
            }

            return EntityId.None;
        }

        private static IReadOnlyList<EntityId> OpenDemandIds(NarrativeWorldState world, NarrativeThread thread)
        {
            List<EntityId> ids = new List<EntityId>();
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    ids.Add(fact.Id);
                }
            }

            return ids;
        }
    }
}
