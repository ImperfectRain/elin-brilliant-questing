using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// A narrow debt laboratory for the economic route: a debtor owes a creditor a fixed amount,
    /// and the player can end the matter by paying it.
    /// </summary>
    public sealed class DebtSituation
    {
        public const string ArchetypeId = "debt_default";

        private DebtSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId DebtorId { get; private set; }

        public EntityId CreditorId { get; private set; }

        public EntityId DebtFactId { get; private set; }

        public int Amount { get; private set; }

        public EntityId ZoneId { get; private set; }

        public static DebtSituation Create(NarrativeWorldState world, ISituationStager stager, EntityId zone, GameTime now, int amount = 750)
        {
            DebtSituation situation = new DebtSituation
            {
                Amount = amount,
                ZoneId = zone
            };

            NarrativeNpc debtor = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Mira")
            {
                Occupation = "porter",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc creditor = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Haron")
            {
                Occupation = "merchant",
                Importance = NarrativeImportance.Known
            });

            situation.DebtorId = debtor.Id;
            situation.CreditorId = creditor.Id;

            stager.StageCharacter(debtor.Id, new CharacterBlueprint(debtor.Name)
            {
                Money = amount / 5
            }.With(VanillaAttribute.Endurance, 12), zone);

            stager.StageCharacter(creditor.Id, new CharacterBlueprint(creditor.Name)
            {
                Money = 3000
            }.With(VanillaSkill.Investing, 10), zone);

            WorldEvent origin = world.Record(
                WorldEventType.DebtCreated,
                debtor.Id,
                creditor.Id,
                now,
                magnitude: 0.5,
                zone: zone);

            Fact debt = new Fact(world.NewId("fact"), debtor.Id, FactPredicates.Owes, creditor.Id, amount + " orens", TruthState.True, originEvent: origin.Id);
            world.Knowledge.AddFact(debt);
            world.Knowledge.Teach(debtor.Id, debt.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(creditor.Id, debt.Id, KnowledgeSource.Participant, 1.0, now, true);
            situation.DebtFactId = debt.Id;

            debtor.Goals.Add(new NpcGoal("repay_debt", creditor.Id, 90));
            creditor.Goals.Add(new NpcGoal("recover_money", debtor.Id, 80));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 25,
                Importance = 25,
                State = ThreadState.Active,
                OriginEventId = origin.Id
            };
            thread.ParticipantIds.Add(debtor.Id);
            thread.ParticipantIds.Add(creditor.Id);
            thread.FactIds.Add(debt.Id);
            thread.SiteIds.Add(zone);
            thread.OpenQuestions.Add("How will " + debtor.Name + " settle the debt?");
            thread.Escalation.Add(new EscalationStep("creditor_presses", 2, "The creditor starts pressing harder."));
            thread.Escalation.Add(new EscalationStep("debtor_defaults", 6, "The debtor misses the promised date."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
