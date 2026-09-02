using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>An ether-disease premise rooted in an existing Irva cure object: ether antibody.</summary>
    public sealed class EtherDiseaseSituation
    {
        public const string ArchetypeId = "ether_disease";
        public const string CureName = "ether antibody";

        private EtherDiseaseSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId PatientId { get; private set; }

        public EntityId KinId { get; private set; }

        public EntityId ClinicZoneId { get; private set; }

        public EntityId DiseaseFactId { get; private set; }

        public EntityId CureDemandId { get; private set; }

        public static readonly ProductionSpec EtherAntibody = new ProductionSpec("ether_antibody", 0, 600);

        public static EtherDiseaseSituation Create(
            NarrativeWorldState world,
            ISituationStager stager,
            EntityId player,
            EntityId clinic,
            GameTime now)
        {
            EtherDiseaseSituation situation = new EtherDiseaseSituation
            {
                ClinicZoneId = clinic
            };

            world.Registry.Add(new NarrativeSite(clinic, "Noyel ether clinic", "clinic"));
            NarrativeNpc patient = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Mina")
            {
                Occupation = "ether-diseased farmer",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc kin = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Lyle")
            {
                Occupation = "worried sibling",
                Importance = NarrativeImportance.Known
            });

            situation.PatientId = patient.Id;
            situation.KinId = kin.Id;

            stager.StageCharacter(patient.Id, new CharacterBlueprint(patient.Name)
                    .With(VanillaAttribute.Will, 8).With(VanillaAttribute.Perception, 8),
                clinic);
            stager.StageCharacter(kin.Id, new CharacterBlueprint(kin.Name)
                    .With(VanillaAttribute.Charisma, 10).With(VanillaSkill.Negotiation, 6),
                clinic);

            Fact disease = new Fact(
                world.NewId("fact"),
                patient.Id,
                FactPredicates.Damaged,
                EntityId.None,
                "ether disease",
                TruthState.True);
            world.Knowledge.AddFact(disease);
            situation.DiseaseFactId = disease.Id;

            Fact cure = new Fact(
                world.NewId("fact"),
                patient.Id,
                FactPredicates.Needs,
                disease.Id,
                EtherAntibody.ToFactValue(),
                TruthState.True);
            world.Knowledge.AddFact(cure);
            situation.CureDemandId = cure.Id;
            world.Demands.AddOrUpdate(clinic, EtherAntibody.CategoryTag, 70, now, now.PlusDays(12), cure.Id);

            world.Knowledge.Teach(patient.Id, disease.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(patient.Id, cure.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(kin.Id, disease.Id, KnowledgeSource.Witnessed, 1.0, now, false);
            world.Knowledge.Teach(kin.Id, cure.Id, KnowledgeSource.Hearsay, 0.9, now, false);
            world.Knowledge.Teach(player, disease.Id, KnowledgeSource.Witnessed, 1.0, now, true);

            patient.Goals.Add(new NpcGoal("survive_ether_disease", patient.Id, 95));
            kin.Goals.Add(new NpcGoal("find_ether_antibody", patient.Id, 85));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 45,
                Importance = 60,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(patient.Id);
            thread.ParticipantIds.Add(kin.Id);
            thread.FactIds.Add(disease.Id);
            thread.FactIds.Add(cure.Id);
            thread.SiteIds.Add(clinic);
            thread.OpenQuestions.Add("Where can " + patient.Name + " get " + CureName + "?");
            thread.GenerationCauses.Add("setting: Noyel names a vanilla Irva town");
            thread.GenerationCauses.Add("setting: ether disease names a vanilla Irva affliction");
            thread.GenerationCauses.Add("setting: " + CureName + " names the existing Irva cure object");
            thread.Escalation.Add(new EscalationStep("symptoms_worsen", 6, "The ether disease worsens."));
            thread.Escalation.Add(new EscalationStep("family_sells_keepsakes", 12, "The family starts selling keepsakes for ether antibody."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
