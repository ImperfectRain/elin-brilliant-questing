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
    /// The laboratory for the criminal route: a man everybody knows about and nobody can touch.
    ///
    /// Where <see cref="UnexplainedDeathSituation"/> closed the social route by making sure nobody
    /// knew anything, this one closes the *investigative* route by making sure there is nothing to
    /// find. Dassen is careful. He signs nothing about the money, he keeps no tally, and he sends
    /// Vurl so his own name is never said in the shop. The fact that he is bleeding Ilsabet dry
    /// carries no evidence at all, which means `search`, `inspect`, `read` and every other verb
    /// that reads an object has nothing here to be pointed at.
    ///
    /// What is left is a route made entirely of crimes. There is one paper in the world in Dassen's
    /// hand - his own letter-book, in a counting house behind a lock - and it proves only that he
    /// employs Vurl, which is not an offence. Getting at it means breaking in. Turning it into
    /// proof of the racket means paying somebody to make it say more than it says. And that last
    /// step is the one a lawful character cannot take: not because they would roll badly, but
    /// because Orin does not do that kind of work for people the trade has never heard of.
    ///
    /// That is the step's done-when, and it is deliberately a matter of standing rather than skill.
    /// The lawful build can pick the lock, take the book and read it. What they cannot do is find
    /// anybody who will forge for them, and so the case stops where the honest evidence stops.
    ///
    /// The route is also, on purpose, a bad thing to have done. The claim it proves is true; the
    /// proof is a lie, and the lie is minted as its own true fact in the graph with the paper as
    /// its evidence. Somebody who works at that document later can show what was done to it.
    /// </summary>
    public sealed class ProtectionRacketSituation
    {
        public const string ArchetypeId = "protection_racket";

        private ProtectionRacketSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>Being bled. Knows exactly who is doing it and will not say so where it counts.</summary>
        public EntityId VictimId { get; private set; }

        /// <summary>Collects the money. Says nothing, ever, about who sent him.</summary>
        public EntityId CollectorId { get; private set; }

        /// <summary>The man behind it, in his counting house, entirely respectable.</summary>
        public EntityId RacketeerId { get; private set; }

        /// <summary>Makes papers say things. Will not work for a stranger.</summary>
        public EntityId ForgerId { get; private set; }

        /// <summary>Takes goods off people. Will not deal with a stranger either.</summary>
        public EntityId FenceId { get; private set; }

        /// <summary>Honest, and no use whatever without something he can hold.</summary>
        public EntityId GuardId { get; private set; }

        /// <summary>The one document in the world written in the racketeer's hand.</summary>
        public EntityId LetterBookId { get; private set; }

        /// <summary>What the collector carries, and what he would miss.</summary>
        public EntityId CudgelId { get; private set; }

        /// <summary>"He is bleeding her." True, secret, and evidenced by absolutely nothing.</summary>
        public EntityId RacketFactId { get; private set; }

        /// <summary>"He employs the collector." True, provable, and not a crime.</summary>
        public EntityId EmploymentFactId { get; private set; }

        public EntityId MarketZoneId { get; private set; }

        /// <summary>Restricted. What it keeps is behind a lock somebody else holds the key to.</summary>
        public EntityId CountingHouseZoneId { get; private set; }

        public EntityId BackRoomZoneId { get; private set; }

        public static ProtectionRacketSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId market, GameTime now)
        {
            ProtectionRacketSituation situation = new ProtectionRacketSituation
            {
                MarketZoneId = market,
                CountingHouseZoneId = world.NewId("zone"),
                BackRoomZoneId = world.NewId("zone")
            };

            world.Registry.Add(new NarrativeSite(market, "the market row", "shop"));
            world.Registry.Add(new NarrativeSite(situation.CountingHouseZoneId, "the counting house", "shop")
            {
                Restricted = true
            });
            world.Registry.Add(new NarrativeSite(situation.BackRoomZoneId, "the room behind the tannery", "hideout"));

            NarrativeNpc victim = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Ilsabet")
            {
                Occupation = "shopkeeper",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc collector = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Vurl")
            {
                Occupation = "collector",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc racketeer = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Dassen")
            {
                Occupation = "merchant",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc forger = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Orin")
            {
                Occupation = "scribe",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc fence = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Kessa")
            {
                Occupation = "pedlar",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc guard = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Rook")
            {
                Occupation = "guard",
                Importance = NarrativeImportance.Known
            });

            guard.Roles.Add(AuthorityPolicy.GuardRole);
            forger.Roles.Add(UnderworldPolicy.ForgerRole);
            fence.Roles.Add(UnderworldPolicy.FenceRole);

            // The trade's carrier is the same woman who takes goods; a back room this size does not
            // run to one person per speciality.
            fence.Roles.Add(UnderworldPolicy.SmugglerRole);

            situation.VictimId = victim.Id;
            situation.CollectorId = collector.Id;
            situation.RacketeerId = racketeer.Id;
            situation.ForgerId = forger.Id;
            situation.FenceId = fence.Id;
            situation.GuardId = guard.Id;

            // Nobody here is going to volunteer anything, and each of them for a different reason.
            victim.Personality.Courage = 0.1;
            collector.Personality.Loyalty = 0.95;
            collector.Personality.Honesty = 0.05;
            racketeer.Personality.Honesty = 0.05;
            racketeer.Personality.Ambition = 0.9;

            situation.LetterBookId = world.NewId("item");
            situation.CudgelId = world.NewId("item");

            ItemDescriptor letterBook = new ItemDescriptor(situation.LetterBookId, "Dassen's letter-book", "book", 30, "book");
            ItemDescriptor cudgel = new ItemDescriptor(situation.CudgelId, "a weighted cudgel", "weapon", 220, "club");

            stager.StageCharacter(victim.Id, new CharacterBlueprint(victim.Name)
                    .With(VanillaAttribute.Will, 8),
                market);

            stager.StageCharacter(guard.Id, new CharacterBlueprint(guard.Name)
                    .With(VanillaAttribute.Will, 12).With(VanillaAttribute.Perception, 11),
                market);

            stager.StageCharacter(collector.Id, new CharacterBlueprint(collector.Name)
                    .With(VanillaAttribute.Strength, 14).With(VanillaAttribute.Will, 11)
                    .Carrying(cudgel),
                market);

            stager.StageCharacter(racketeer.Id, new CharacterBlueprint(racketeer.Name)
                {
                    Money = 6000
                }
                    .With(VanillaAttribute.Will, 14).With(VanillaAttribute.Learning, 13),
                situation.CountingHouseZoneId);

            stager.StageCharacter(forger.Id, new CharacterBlueprint(forger.Name)
                    .With(VanillaAttribute.Learning, 14).With(VanillaSkill.Literacy, 12),
                situation.BackRoomZoneId);

            stager.StageCharacter(fence.Id, new CharacterBlueprint(fence.Name)
                {
                    Money = 2500
                }
                    .With(VanillaAttribute.Charisma, 12).With(VanillaSkill.Appraising, 11),
                situation.BackRoomZoneId);

            // The letter-book belongs to the room, not to a person. That is what makes the lock
            // matter: Dassen does not carry it about, and nobody can lift it off him.
            stager.StageItem(situation.CountingHouseZoneId, letterBook);

            // -- what is true ---------------------------------------------------------------
            // The racket carries no evidence at all. This is the constraint the whole laboratory
            // is built on, and the honest way to state it is an empty EvidenceIds list.
            Fact racket = new Fact(
                world.NewId("fact"), racketeer.Id, FactPredicates.Extorted, victim.Id,
                "the shop's takings, every month", TruthState.True, secrecy: 85);
            world.Knowledge.AddFact(racket);
            situation.RacketFactId = racket.Id;

            // And the one thing that is written down is not a crime.
            Fact employment = new Fact(
                world.NewId("fact"), racketeer.Id, FactPredicates.Hired, collector.Id,
                "as his collector", TruthState.True, secrecy: 40);
            employment.EvidenceIds.Add(situation.LetterBookId);
            world.Knowledge.AddFact(employment);
            situation.EmploymentFactId = employment.Id;

            // -- who knows what ---------------------------------------------------------------
            // Three people know about the racket. Two of them did it, and the third is the one
            // being ruined by it, which is the least useful witness a court ever gets.
            world.Knowledge.Teach(racketeer.Id, racket.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(collector.Id, racket.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(victim.Id, racket.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(racketeer.Id, employment.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(collector.Id, employment.Id, KnowledgeSource.Participant, 1.0, now, false);

            racketeer.Goals.Add(new NpcGoal("avoid_exposure", racket.Id, 90));
            victim.Goals.Add(new NpcGoal("protect", victim.Id, 80));
            guard.Goals.Add(new NpcGoal("keep_the_peace", market, 50));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 45,
                Importance = 60,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(victim.Id);
            thread.ParticipantIds.Add(collector.Id);
            thread.ParticipantIds.Add(racketeer.Id);
            thread.ParticipantIds.Add(guard.Id);
            thread.FactIds.Add(racket.Id);
            thread.FactIds.Add(employment.Id);
            thread.SiteIds.Add(market);
            thread.SiteIds.Add(situation.CountingHouseZoneId);
            thread.SiteIds.Add(situation.BackRoomZoneId);
            thread.OpenQuestions.Add("Who is taking " + victim.Name + "'s money?");
            thread.OpenQuestions.Add("What could anyone put in front of a guard?");

            thread.Escalation.Add(new EscalationStep("shop_closes", 6, victim.Name + " gives up the shop."));
            thread.Escalation.Add(new EscalationStep("next_street", 12, "The same arrangement starts on the next street."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
