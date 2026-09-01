using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The three-NPC laboratory from the design document's Phase 1.
    ///
    /// A stole something from B; C saw it. That is the entire scenario, and it is deliberately
    /// tiny - the point is not the story, it is whether the architecture can carry one. If a
    /// player can approach this through conversation, investigation, theft, coercion, money or
    /// violence; if failing produces new state rather than a wall; and if the world still makes
    /// sense ten days later, then the expensive parts of the mod are worth building.
    ///
    /// Note what is generated: a cause, three motivated people, an object, and who knows what.
    /// No quest, no objectives, no branches. Those emerge from the verb library.
    /// </summary>
    public sealed class PettyTheftSituation
    {
        public const string ArchetypeId = "petty_theft";

        private static readonly string[] VictimNames = { "Mara", "Elna", "Tovar", "Sibylla", "Garron" };
        private static readonly string[] ThiefNames = { "Dorren", "Kip", "Ansel", "Vess", "Rulf" };
        private static readonly string[] WitnessNames = { "Odile", "Bram", "Nessa", "Corin", "Thalia" };
        private static readonly string[] Valuables = { "silver ring", "brass locket", "engraved cup", "ivory comb", "old signet" };

        private PettyTheftSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>The person who lost something and wants it back.</summary>
        public EntityId VictimId { get; private set; }

        /// <summary>The person who took it, and is still carrying it.</summary>
        public EntityId ThiefId { get; private set; }

        /// <summary>The person who saw it happen and would rather not be involved.</summary>
        public EntityId WitnessId { get; private set; }

        public EntityId ItemId { get; private set; }

        /// <summary>The fact "the thief stole the item". True from the start; almost nobody knows it.</summary>
        public EntityId TheftFactId { get; private set; }

        /// <summary>The fact "the victim owns the item". What makes returning it meaningful.</summary>
        public EntityId OwnershipFactId { get; private set; }

        public EntityId ZoneId { get; private set; }

        public static PettyTheftSituation Create(NarrativeWorldState world, ISituationStager stager, EntityId zone, GameTime now, ulong seed)
        {
            DeterministicRng rng = new DeterministicRng(seed).Fork(ArchetypeId);
            PettyTheftSituation situation = new PettyTheftSituation { ZoneId = zone };

            NarrativeNpc victim = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), Pick(VictimNames, rng))
            {
                Occupation = "shopkeeper",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc thief = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), Pick(ThiefNames, rng))
            {
                Occupation = "labourer",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc witness = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), Pick(WitnessNames, rng))
            {
                Occupation = "neighbour",
                Importance = NarrativeImportance.Known
            });

            situation.VictimId = victim.Id;
            situation.ThiefId = thief.Id;
            situation.WitnessId = witness.Id;

            // Personalities decide who is worth which approach: the thief can be bought, the
            // witness can be frightened, the victim can be talked to.
            thief.Personality.Greed = 0.8;
            thief.Personality.Honesty = 0.2;
            thief.Personality.Courage = 0.5;
            witness.Personality.Courage = 0.2;
            witness.Personality.Sociability = 0.7;
            witness.Personality.Greed = 0.4;
            victim.Personality.Vengefulness = 0.6;
            victim.Personality.Honesty = 0.8;

            string valuable = Pick(Valuables, rng);
            situation.ItemId = world.NewId("item");
            ItemDescriptor item = new ItemDescriptor(situation.ItemId, valuable, "jewelry", 400 + rng.NextInt(600), "ring");

            stager.StageCharacter(victim.Id, new CharacterBlueprint(victim.Name)
                    .With(VanillaAttribute.Charisma, 12).With(VanillaAttribute.Will, 11)
                    .With(VanillaAttribute.Perception, 9).With(VanillaSkill.Negotiation, 8),
                zone);

            // The thief is still carrying it. That is what makes pickpocketing a real route.
            stager.StageCharacter(thief.Id, new CharacterBlueprint(thief.Name)
                    .With(VanillaAttribute.Strength, 14).With(VanillaAttribute.Will, 9)
                    .With(VanillaAttribute.Perception, 12).With(VanillaSkill.Pickpocket, 6)
                    .Carrying(item),
                zone);

            stager.StageCharacter(witness.Id, new CharacterBlueprint(witness.Name)
                    .With(VanillaAttribute.Perception, 14).With(VanillaAttribute.Will, 7)
                    .With(VanillaAttribute.Charisma, 10),
                zone);

            // -- the act itself ------------------------------------------------------------
            // The theft is a fact, but a fact is a statement about the world, not something that
            // happened at a time, in a place, in front of people. Without the event the ledger
            // has no record of the founding act, the witness's belief has no observable it could
            // have come from, and the inspector can only answer "which event caused this?" with
            // silence - which is what the first live run reported.
            WorldEvent origin = world.Record(
                WorldEventType.Theft,
                thief.Id,
                victim.Id,
                now,
                magnitude: 0.6,
                zone: zone,
                related: new[] { situation.ItemId },
                witnesses: new[] { witness.Id },
                evidence: new[] { situation.ItemId });

            // -- what is true, and who knows it --------------------------------------------
            Fact theft = new Fact(world.NewId("fact"), thief.Id, FactPredicates.Stole, situation.ItemId, valuable, TruthState.True, secrecy: 60, originEvent: origin.Id);
            theft.EvidenceIds.Add(situation.ItemId);
            world.Knowledge.AddFact(theft);
            situation.TheftFactId = theft.Id;

            Fact ownership = new Fact(world.NewId("fact"), victim.Id, FactPredicates.Possesses, situation.ItemId, valuable);
            world.Knowledge.AddFact(ownership);
            situation.OwnershipFactId = ownership.Id;

            // The thief knows because he did it. The witness knows because she saw it - but she
            // has no object to show anyone, so she cannot prove a thing. The victim knows only
            // that it is gone. The player knows nothing at all.
            world.Knowledge.Teach(thief.Id, theft.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(witness.Id, theft.Id, KnowledgeSource.Witnessed, 1.0, now, false);
            world.Knowledge.Teach(victim.Id, ownership.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(thief.Id, ownership.Id, KnowledgeSource.Participant, 1.0, now, false);

            // -- who cares, and how much ---------------------------------------------------
            victim.Goals.Add(new NpcGoal("recover_property", situation.ItemId, 90));
            thief.Goals.Add(new NpcGoal("avoid_exposure", theft.Id, 85));
            thief.Goals.Add(new NpcGoal("raise_money", thief.Id, 70));
            witness.Goals.Add(new NpcGoal("stay_out_of_trouble", witness.Id, 75));

            world.Relationships.ConnectMutual(victim.Id, witness.Id, Relationships.RelationKind.Acquaintance, 30);
            world.Relationships.Connect(witness.Id, thief.Id, Relationships.RelationKind.Acquaintance, -10);

            // -- the thread ----------------------------------------------------------------
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 20,
                Importance = 30,
                State = ThreadState.Active,
                OriginEventId = origin.Id
            };
            thread.ParticipantIds.Add(victim.Id);
            thread.ParticipantIds.Add(thief.Id);
            thread.ParticipantIds.Add(witness.Id);
            thread.FactIds.Add(theft.Id);
            thread.FactIds.Add(ownership.Id);
            thread.SiteIds.Add(zone);
            thread.OpenQuestions.Add("Where is " + victim.Name + "'s " + valuable + "?");
            thread.OpenQuestions.Add("Did anyone see what happened?");

            // Nothing here fails the player. Each step is the world getting on with it.
            thread.Escalation.Add(new EscalationStep("victim_asks_around", 2, "The victim starts asking neighbours."));
            thread.Escalation.Add(new EscalationStep("thief_hides_it", 4, "The thief stops carrying it."));
            thread.Escalation.Add(new EscalationStep("witness_talks", 7, "The witness lets something slip."));
            thread.Escalation.Add(new EscalationStep("thief_deflects", 8, "The thief points at somebody else."));
            thread.Escalation.Add(new EscalationStep("accusation", 10, "The victim acts on what they believe."));
            thread.Escalation.Add(new EscalationStep("feud", 14, "The two households stop speaking."));
            ArchetypeRecoveryRoutes.AddPettyTheft(thread);

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        /// <summary>
        /// Builds the situation a settlement's own state proposed.
        ///
        /// The counterpart of <see cref="Create"/>: nobody is staged, everybody already exists, and
        /// the physical transfer has already happened through the vanilla seam before this is
        /// called. All that remains is the narrative half - what happened, who knows it, and what
        /// each of them now wants.
        ///
        /// A witness is optional. A theft two people committed with nobody else about is an
        /// ordinary theft, not a broken one, and the difference has to be carried all the way
        /// through: no phantom witness on the event, nobody taught a fact they could not have seen,
        /// and no escalation step about somebody letting it slip.
        /// </summary>
        public static PettyTheftSituation FromLocalAffordance(
            NarrativeWorldState world,
            SituationCandidate candidate,
            EntityId zone,
            GameTime now)
        {
            PettyTheftCandidate theft = new PettyTheftCandidate(candidate);
            PettyTheftSituation situation = new PettyTheftSituation
            {
                ZoneId = zone,
                VictimId = theft.VictimId,
                ThiefId = theft.ThiefId,
                WitnessId = theft.WitnessId,
                ItemId = theft.Item.Id
            };

            NarrativeNpc victim = world.Registry.GetNpc(situation.VictimId);
            NarrativeNpc thief = world.Registry.GetNpc(situation.ThiefId);
            NarrativeNpc witness = world.Registry.GetNpc(situation.WitnessId);
            string valuable = theft.Item.Name;

            victim?.Promote(NarrativeImportance.Known);
            thief?.Promote(NarrativeImportance.Known);
            witness?.Promote(NarrativeImportance.Known);

            IReadOnlyList<EntityId> witnesses = theft.WitnessIds;
            WorldEvent origin = world.Record(
                WorldEventType.Theft,
                situation.ThiefId,
                situation.VictimId,
                now,
                magnitude: 0.6,
                zone: zone,
                related: new[] { situation.ItemId },
                witnesses: witnesses,
                evidence: new[] { situation.ItemId });

            Fact theftFact = new Fact(
                world.NewId("fact"),
                situation.ThiefId,
                FactPredicates.Stole,
                situation.ItemId,
                valuable,
                TruthState.True,
                secrecy: 60,
                originEvent: origin.Id);
            theftFact.EvidenceIds.Add(situation.ItemId);
            world.Knowledge.AddFact(theftFact);
            situation.TheftFactId = theftFact.Id;

            Fact ownership = new Fact(world.NewId("fact"), situation.VictimId, FactPredicates.Possesses, situation.ItemId, valuable);
            world.Knowledge.AddFact(ownership);
            situation.OwnershipFactId = ownership.Id;

            world.Knowledge.Teach(situation.ThiefId, theftFact.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(situation.VictimId, ownership.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(situation.ThiefId, ownership.Id, KnowledgeSource.Participant, 1.0, now, false);

            thief?.Goals.Add(new NpcGoal("avoid_exposure", theftFact.Id, 85));
            thief?.Goals.Add(new NpcGoal("raise_money", situation.ThiefId, 70));
            victim?.Goals.Add(new NpcGoal("recover_property", situation.ItemId, 90));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = Math.Min(60, 20 + candidate.Score / 4),
                Importance = Math.Min(70, 25 + theft.Item.Value / 25),
                State = ThreadState.Active,
                OriginEventId = origin.Id
            };
            thread.ParticipantIds.Add(situation.VictimId);
            thread.ParticipantIds.Add(situation.ThiefId);
            thread.FactIds.Add(theftFact.Id);
            thread.FactIds.Add(ownership.Id);
            thread.SiteIds.Add(zone);
            thread.OpenQuestions.Add("Where is " + world.Registry.NameOf(situation.VictimId) + "'s " + valuable + "?");
            thread.OpenQuestions.Add("Did anyone see what happened?");

            for (int i = 0; i < candidate.Causes.Count; i++)
            {
                thread.GenerationCauses.Add(candidate.Causes[i]);
            }

            thread.Escalation.Add(new EscalationStep("victim_asks_around", 2, "The victim starts asking neighbours."));
            thread.Escalation.Add(new EscalationStep("thief_hides_it", 4, "The thief stops carrying it."));

            // Only somebody who was actually there can be taught what they saw, want to stay out of
            // it, or later let it slip. An unwitnessed theft has none of that, and inventing any of
            // it would be the mod knowing something the world does not.
            for (int i = 0; i < witnesses.Count; i++)
            {
                EntityId seen = witnesses[i];
                world.Knowledge.Teach(seen, theftFact.Id, KnowledgeSource.Witnessed, 1.0, now, false);
                world.Registry.GetNpc(seen)?.Goals.Add(new NpcGoal("stay_out_of_trouble", seen, 75));
                world.Relationships.ConnectMutual(situation.VictimId, seen, Relationships.RelationKind.Acquaintance, 20);
                world.Relationships.Connect(seen, situation.ThiefId, Relationships.RelationKind.Acquaintance, -10);
                thread.ParticipantIds.Add(seen);
            }

            if (witnesses.Count > 0)
            {
                thread.Escalation.Add(new EscalationStep("witness_talks", 7, "The witness lets something slip."));
            }

            thread.Escalation.Add(new EscalationStep("thief_deflects", 8, "The thief points at somebody else."));
            thread.Escalation.Add(new EscalationStep("accusation", 10, "The victim acts on what they believe."));
            thread.Escalation.Add(new EscalationStep("feud", 14, "The two households stop speaking."));
            ArchetypeRecoveryRoutes.AddPettyTheft(thread);

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        private static string Pick(string[] options, DeterministicRng rng)
        {
            return options[rng.NextInt(options.Length)];
        }
    }
}
