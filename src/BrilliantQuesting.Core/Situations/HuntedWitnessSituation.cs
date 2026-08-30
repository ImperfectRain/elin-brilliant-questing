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
    /// The laboratory for the Home/Community verbs: a woman who saw something, and the man who
    /// knows she saw it.
    ///
    /// Deliberately narrow. Nothing here can be talked away - Brann is not going to be persuaded
    /// out of looking for her, and no amount of evidence stops him walking down the lane - so the
    /// question the situation asks is the one the family exists to answer: has the player anywhere
    /// to *put* her. That makes it a test of the settlement rather than of the player's Charisma:
    /// the same character with the same stats has a route or does not depending on whether their
    /// Home has a bed free, people in it, and Public Safety worth the name.
    ///
    /// The situation itself creates no Home. A Home is the player's own property and the mod
    /// cannot conjure one; what varies between runs of this laboratory is the settlement the
    /// player brings to it, which is exactly the variable under test.
    /// </summary>
    public sealed class HuntedWitnessSituation
    {
        public const string ArchetypeId = "hunted_witness";

        private HuntedWitnessSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>The woman with nowhere safe to be.</summary>
        public EntityId WitnessId { get; private set; }

        /// <summary>The man she saw, who is looking for her.</summary>
        public EntityId HunterId { get; private set; }

        /// <summary>A neighbour, standing in the lane. Somebody for the town's reaction to land on.</summary>
        public EntityId NeighbourId { get; private set; }

        /// <summary>The claim that she is not safe. This is what every Home route answers.</summary>
        public EntityId ExposureFactId { get; private set; }

        /// <summary>What she saw. Provable, and the reason she is worth silencing.</summary>
        public EntityId KillingFactId { get; private set; }

        /// <summary>Her testimony, written down. The object `store_evidence` puts beyond reach.</summary>
        public EntityId DepositionId { get; private set; }

        public EntityId LaneZoneId { get; private set; }

        public static HuntedWitnessSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId lane, GameTime now)
        {
            HuntedWitnessSituation situation = new HuntedWitnessSituation
            {
                LaneZoneId = lane,
                DepositionId = world.NewId("item")
            };

            world.Registry.Add(new NarrativeSite(lane, "Coldbeck lane", "lane"));

            NarrativeNpc witness = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Sella")
            {
                Occupation = "weaver",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc hunter = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Brann")
            {
                Occupation = "drover",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc neighbour = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Hobb")
            {
                Occupation = "carter",
                Importance = NarrativeImportance.Background
            });

            situation.WitnessId = witness.Id;
            situation.HunterId = hunter.Id;
            situation.NeighbourId = neighbour.Id;

            stager.StageCharacter(witness.Id, new CharacterBlueprint(witness.Name)
                    .With(VanillaAttribute.Will, 9)
                    .With(VanillaAttribute.Charisma, 10),
                lane);
            stager.StageCharacter(hunter.Id, new CharacterBlueprint(hunter.Name)
                    .With(VanillaAttribute.Will, 14)
                    .With(VanillaAttribute.Strength, 15),
                lane);
            stager.StageCharacter(neighbour.Id, new CharacterBlueprint(neighbour.Name)
                    .With(VanillaAttribute.Perception, 11),
                lane);

            // What she saw. True, provable, and nobody's business but hers until she says so.
            Fact killing = new Fact(
                world.NewId("fact"), hunter.Id, FactPredicates.Killed, world.NewId("npc"),
                null, TruthState.True, secrecy: 60);
            world.Knowledge.AddFact(killing);
            situation.KillingFactId = killing.Id;

            ItemDescriptor deposition = new ItemDescriptor(
                situation.DepositionId, "Sella's deposition", "document", 5, "deposition");
            killing.EvidenceIds.Add(deposition.Id);
            stager.StageItem(player, deposition);

            world.Knowledge.Teach(witness.Id, killing.Id, KnowledgeSource.Witnessed, 1.0, now, true);
            world.Knowledge.Teach(hunter.Id, killing.Id, KnowledgeSource.Participant, 1.0, now, true);

            // Why she is a problem to somebody, stated once as its own claim. Every Home route
            // reads this and nothing else: the situation does not name shelter, a bed or a watch.
            Fact exposure = new Fact(
                world.NewId("fact"), witness.Id, FactPredicates.AtRisk, hunter.Id, "witness", TruthState.True);
            world.Knowledge.AddFact(exposure);
            situation.ExposureFactId = exposure.Id;

            world.Knowledge.Teach(witness.Id, exposure.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(player, exposure.Id, KnowledgeSource.Hearsay, 0.9, now, false, witness.Id);

            hunter.Goals.Add(new NpcGoal("silence_the_witness", witness.Id, 90));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 45,
                Importance = 60,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(witness.Id);
            thread.ParticipantIds.Add(hunter.Id);
            thread.FactIds.Add(exposure.Id);
            thread.FactIds.Add(killing.Id);
            thread.SiteIds.Add(lane);
            thread.OpenQuestions.Add("Where can Sella be, that Brann is not?");
            thread.Escalation.Add(new EscalationStep("brann_finds_her", 4, "Brann finds Sella alone."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        /// <summary>
        /// A settlement of the shape this laboratory is about: a few people, a little room, and
        /// the two Home Skill elements the family reads.
        ///
        /// Built through <see cref="HomeStateBuilder"/> so that "this build never answered" is
        /// expressible - a caller that leaves out the capacity or a metric gets a Home that is
        /// genuinely unread on that point, which is the state half these routes refuse on.
        /// </summary>
        public static HomeStateBuilder Smallholding(EntityId zone, params EntityId[] residents)
        {
            HomeStateBuilder builder = new HomeStateBuilder(zone, "Coldbeck steading");
            for (int i = 0; i < residents.Length; i++)
            {
                builder.AddResident(residents[i], "resident " + (i + 1));
            }

            return builder;
        }
    }

    /// <summary>
    /// The BQ-048 consequence surface for the Home. Sheltering somebody is not merely a dialogue
    /// ending: the settlement's own Public Safety decides whether that undertaking stays quiet or
    /// becomes a later problem. The handler records only narrative consequences; it does not move
    /// actors or write Elin Home numbers.
    /// </summary>
    public sealed class HuntedWitnessEscalation : IThreadEscalationHandler
    {
        private readonly IVanillaState _vanilla;

        public HuntedWitnessEscalation(IVanillaState vanilla)
        {
            _vanilla = vanilla;
        }

        public void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now)
        {
            if (step.Id != Undertakings.ResidentDiscoveredStep && step.Id != "brann_finds_her")
            {
                return;
            }

            EntityId witness = thread.ParticipantIds.Count > 0 ? thread.ParticipantIds[0] : EntityId.None;
            EntityId hunter = thread.ParticipantIds.Count > 1 ? thread.ParticipantIds[1] : EntityId.None;
            EntityId place = DiscoveryPlace(thread);
            Fact shelter = FindShelter(world, witness);

            if (step.Id == "brann_finds_her" && shelter != null && shelter.Value == Undertakings.Resident)
            {
                return;
            }

            if (step.Id == Undertakings.ResidentDiscoveredStep
                && (shelter == null || shelter.Value != Undertakings.Resident))
            {
                return;
            }

            if (shelter != null && !hunter.IsNone)
            {
                world.Knowledge.Teach(hunter, shelter.Id, KnowledgeSource.Hearsay, 0.85, now, false);
            }

            Fact exposure = new Fact(
                world.NewId("fact"),
                witness,
                FactPredicates.AtRisk,
                hunter,
                step.Id == Undertakings.ResidentDiscoveredStep ? "found_at_home" : "found_alone",
                TruthState.True);
            world.Knowledge.AddFact(exposure);
            thread.FactIds.Add(exposure.Id);
            if (!witness.IsNone)
            {
                world.Knowledge.Teach(witness, exposure.Id, KnowledgeSource.Participant, 1.0, now, true);
            }

            thread.Tension += step.Id == Undertakings.ResidentDiscoveredStep ? 20 : 30;
            thread.State = ThreadState.Active;
            world.Record(
                WorldEventType.Threatened,
                hunter,
                witness,
                now,
                step.Id == Undertakings.ResidentDiscoveredStep ? 0.55 : 0.75,
                place,
                related: shelter == null ? new[] { exposure.Id } : new[] { shelter.Id, exposure.Id },
                threadId: thread.Id);
        }

        private EntityId DiscoveryPlace(NarrativeThread thread)
        {
            HomeState home = _vanilla.GetHomeState();
            if (home != null && !home.ZoneId.IsNone)
            {
                return home.ZoneId;
            }

            return thread.SiteIds.Count > 0 ? thread.SiteIds[0] : EntityId.None;
        }

        private static Fact FindShelter(NarrativeWorldState world, EntityId witness)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Subject == witness
                    && fact.Predicate == FactPredicates.ShelteredBy
                    && fact.Truth == TruthState.True)
                {
                    return fact;
                }
            }

            return null;
        }
    }
}
