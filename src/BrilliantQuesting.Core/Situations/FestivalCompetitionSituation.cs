using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// A public, low-stakes contest: ordinary townspeople and the player enter, the best showing
    /// wins, and the town remembers the result as news rather than as a reward table.
    /// </summary>
    public sealed class FestivalCompetitionSituation
    {
        public const string ArchetypeId = "festival_competition";

        private FestivalCompetitionSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId JudgeId { get; private set; }

        public EntityId BakerId { get; private set; }

        public EntityId FarmerId { get; private set; }

        public EntityId FestivalSiteId { get; private set; }

        public EntityId WinnerId { get; private set; }

        public EntityId ResultFactId { get; private set; }

        public string ContestName { get; private set; }

        public static FestivalCompetitionSituation Create(
            NarrativeWorldState world,
            ISituationStager stager,
            EntityId player,
            EntityId festivalSite,
            GameTime now)
        {
            FestivalCompetitionSituation situation = new FestivalCompetitionSituation
            {
                FestivalSiteId = festivalSite,
                ContestName = "Kell's Ford pie contest"
            };

            world.Registry.Add(new NarrativeSite(festivalSite, "Kell's Ford green", "festival_ground"));

            NarrativeNpc judge = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Calder")
            {
                Occupation = "reeve",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc baker = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Pella")
            {
                Occupation = "baker",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc farmer = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Nessa")
            {
                Occupation = "farmer",
                Importance = NarrativeImportance.Known
            });

            situation.JudgeId = judge.Id;
            situation.BakerId = baker.Id;
            situation.FarmerId = farmer.Id;

            stager.StageCharacter(judge.Id, new CharacterBlueprint(judge.Name)
                    .With(VanillaAttribute.Charisma, 12)
                    .With(VanillaSkill.Negotiation, 8),
                festivalSite);
            stager.StageCharacter(baker.Id, new CharacterBlueprint(baker.Name)
                    .With(VanillaAttribute.Dexterity, 14)
                    .With(VanillaAttribute.Charisma, 9)
                    .With(VanillaSkill.Cooking, 14),
                festivalSite);
            stager.StageCharacter(farmer.Id, new CharacterBlueprint(farmer.Name)
                    .With(VanillaAttribute.Dexterity, 10)
                    .With(VanillaAttribute.Charisma, 11)
                    .With(VanillaSkill.Cooking, 8),
                festivalSite);

            baker.Goals.Add(new NpcGoal("win_competition", festivalSite, 65));
            farmer.Goals.Add(new NpcGoal("win_competition", festivalSite, 55));
            judge.Goals.Add(new NpcGoal("judge_fairly", festivalSite, 70));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 10,
                Importance = 30,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(player);
            thread.ParticipantIds.Add(judge.Id);
            thread.ParticipantIds.Add(baker.Id);
            thread.ParticipantIds.Add(farmer.Id);
            thread.SiteIds.Add(festivalSite);
            thread.OpenQuestions.Add("Who will win " + situation.ContestName + "?");
            thread.OpenQuestions.Add("Will the player outcook the locals?");
            thread.Escalation.Add(new EscalationStep("crowd_repeats_result", 2, "The town starts talking about the winner."));
            ArchetypeRecoveryRoutes.AddFestivalCompetition(thread);

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        public CompetitionResult Resolve(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            DeterministicRng rng,
            EntityId player,
            GameTime now)
        {
            List<ContestantResult> results = new List<ContestantResult>();
            Score(results, checks, rng, player, EntityId.None);
            Score(results, checks, rng, BakerId, EntityId.None);
            Score(results, checks, rng, FarmerId, EntityId.None);

            ContestantResult winner = results[0];
            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].Score > winner.Score)
                {
                    winner = results[i];
                }
            }

            WinnerId = winner.ActorId;
            WorldEvent origin = world.Record(
                WorldEventType.CompetitionWon,
                winner.ActorId,
                JudgeId,
                now,
                0.35,
                FestivalSiteId,
                witnesses: WitnessesFor(),
                threadId: Thread.Id);

            Fact result = new Fact(
                world.NewId("fact"),
                winner.ActorId,
                FactPredicates.WonCompetition,
                FestivalSiteId,
                ContestName,
                TruthState.True,
                originEvent: origin.Id);
            world.Knowledge.AddFact(result);
            ResultFactId = result.Id;

            TeachPublicResult(world, result.Id, now, player);

            Thread.FactIds.Add(result.Id);
            Thread.OpenQuestions.Clear();
            Thread.State = ThreadState.Resolved;
            world.Record(
                WorldEventType.ThreadResolved,
                player,
                winner.ActorId,
                now,
                0.25,
                FestivalSiteId,
                related: new[] { result.Id },
                threadId: Thread.Id);

            return new CompetitionResult(winner.ActorId, result.Id, results);
        }

        private void Score(
            List<ContestantResult> results,
            ICheckResolver checks,
            DeterministicRng rng,
            EntityId actor,
            EntityId target)
        {
            CheckResult check = checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.FestivalCompetition, actor, target),
                rng.Fork(ArchetypeId + "|contestant|" + actor.Value));
            results.Add(new ContestantResult(actor, Score(check), check));
        }

        private void TeachPublicResult(NarrativeWorldState world, EntityId factId, GameTime now, EntityId player)
        {
            world.Knowledge.Teach(JudgeId, factId, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(BakerId, factId, KnowledgeSource.Witnessed, 0.9, now, true);
            world.Knowledge.Teach(FarmerId, factId, KnowledgeSource.Witnessed, 0.9, now, true);

            // The player entered, but the result is still public knowledge rather than player
            // omniscience. Presentation can reveal it immediately; the authoritative state does
            // not have to pre-teach it for the result to exist and travel.
            if (player == WinnerId)
            {
                world.Knowledge.Teach(player, factId, KnowledgeSource.Participant, 1.0, now, true);
            }
        }

        private EntityId[] WitnessesFor()
        {
            return new EntityId[0];
        }

        private static int Score(CheckResult check)
        {
            int outcome = check.Outcome == CheckOutcome.CriticalPass ? 400
                : check.Outcome == CheckOutcome.Pass ? 300
                : check.Outcome == CheckOutcome.Fail ? 100
                : 0;
            int roll = check.RollIsKnown ? check.Roll : 0;
            return outcome + roll - check.FinalDifficulty;
        }
    }

    public sealed class CompetitionResult
    {
        internal CompetitionResult(EntityId winnerId, EntityId resultFactId, IReadOnlyList<ContestantResult> contestants)
        {
            WinnerId = winnerId;
            ResultFactId = resultFactId;
            Contestants = contestants;
        }

        public EntityId WinnerId { get; }

        public EntityId ResultFactId { get; }

        public IReadOnlyList<ContestantResult> Contestants { get; }
    }

    public sealed class ContestantResult
    {
        internal ContestantResult(EntityId actorId, int score, CheckResult check)
        {
            ActorId = actorId;
            Score = score;
            Check = check;
        }

        public EntityId ActorId { get; }

        public int Score { get; }

        public CheckResult Check { get; }
    }
}
