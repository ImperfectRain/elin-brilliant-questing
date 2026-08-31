using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class EmotionalStateTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");
        private static readonly EntityId FactId = EntityId.Parse("fact_theft");

        [Fact]
        public void AngerChangesTheSameAnswerUntilItDecays()
        {
            Scene calm = Scene.Create();
            ActionOutcome calmAnswer = calm.AskWithRoll(12);

            Assert.Equal(CheckOutcome.Pass, calmAnswer.Check.Outcome);
            Assert.True(calm.World.Knowledge.Knows(Player, FactId));

            Scene angry = Scene.Create();
            angry.WitnessNpc.Emotions.Affect(EmotionalState.Anger, 1.0, angry.Vanilla.Now);
            ActionOutcome angryAnswer = angry.AskWithRoll(12);

            Assert.Equal(CheckOutcome.Fail, angryAnswer.Check.Outcome);
            Assert.False(angry.World.Knowledge.Knows(Player, FactId));
            Assert.Contains("emotional state", angryAnswer.Check.Explain());

            angry.Vanilla.AdvanceTime(EmotionalStateProfile.FullDecayMinutes);
            angry.WitnessNpc.Emotions.DecayTo(angry.Vanilla.Now);
            ActionOutcome settledAnswer = angry.AskWithRoll(12);

            Assert.Equal(CheckOutcome.Pass, settledAnswer.Check.Outcome);
            Assert.True(angry.World.Knowledge.Knows(Player, FactId));
            Assert.Equal(0.0, angry.WitnessNpc.Emotions.Anger, 3);
        }

        [Fact]
        public void EmotionalStateSurvivesSaveAndAppearsInTheInspector()
        {
            Scene scene = Scene.Create();
            scene.WitnessNpc.Emotions.Affect(EmotionalState.Anger, 0.75, GameTime.FromHours(2));
            scene.WitnessNpc.Emotions.Affect(EmotionalState.Suspicion, 0.5, GameTime.FromHours(2));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(scene.World));
            NarrativeNpc witness = reloaded.Registry.GetNpc(Witness);

            Assert.Equal(0.75, witness.Emotions.Anger, 3);
            Assert.Equal(0.5, witness.Emotions.Suspicion, 3);
            Assert.Equal(GameTime.FromHours(2), witness.Emotions.LastUpdatedAt);

            string report = NarrativeInspector.DescribeCharacter(reloaded, scene.Vanilla, Witness);
            Assert.Contains("emotions:", report);
            Assert.Contains("anger 0.75", report);
            Assert.Contains("suspicion 0.50", report);
        }

        private sealed class Scene
        {
            private Scene(NarrativeWorldState world, SandboxVanillaState vanilla, NarrativeNpc witness)
            {
                World = world;
                Vanilla = vanilla;
                WitnessNpc = witness;
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public NarrativeNpc WitnessNpc { get; }

            public static Scene Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(63);
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                NarrativeNpc witness = world.Registry.Add(new NarrativeNpc(Witness, "Hedda"));
                world.Registry.Add(new NarrativeNpc(Player, "You") { Importance = NarrativeImportance.Major });
                world.Registry.Add(new NarrativeNpc(Thief, "Kip"));

                vanilla.Define(Player);
                vanilla.Define(Witness);
                vanilla.Define(Thief);

                Fact theft = new Fact(FactId, Thief, FactPredicates.Stole, Ring, "ring");
                world.Knowledge.AddFact(theft);
                world.Knowledge.Teach(Witness, FactId, KnowledgeSource.Witnessed, 1.0, vanilla.Now, true);

                return new Scene(world, vanilla, witness);
            }

            public ActionOutcome AskWithRoll(int roll)
            {
                ActionContext context = new ActionContext(
                    World,
                    Vanilla,
                    new VanillaStyleCheckResolver(Vanilla),
                    RngThatRolls(ProceduralCheckProfiles.Interrogation.Dice, roll),
                    Player,
                    Witness)
                {
                    SubjectFact = FactId
                };

                return new QuestionAction().Perform(context);
            }
        }

        private static DeterministicRng RngThatRolls(int dice, int roll)
        {
            for (ulong seed = 0; seed < 10000; seed++)
            {
                DeterministicRng rng = new DeterministicRng(seed);
                if (rng.Roll(dice) == roll)
                {
                    return new DeterministicRng(seed);
                }
            }

            throw new Xunit.Sdk.XunitException("No seed found for roll " + roll + " on d" + dice + ".");
        }
    }
}
