using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class StoryletEngineTests
    {
        [Fact]
        public void AStoryletFiresOnTheExistingTheftWithoutAuthoringFacts()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            int factsBefore = lab.World.Knowledge.Facts.Count;
            StoryletEngine engine = EngineWithTheftStorylet();

            StoryletOpportunity opportunity = Assert.Single(engine.Find(
                lab.World,
                lab.Vanilla,
                lab.Situation.Thread,
                lab.Situation.WitnessId,
                lab.Situation.ThiefId,
                lab.Situation.TheftFactId));

            StoryletFiring firing = engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);

            Assert.Equal("storylet.test.theft_witness_confronts_thief", firing.StoryletId);
            Assert.Equal(lab.Situation.TheftFactId, firing.FocusFactId);
            Assert.Equal(lab.Situation.WitnessId, firing.RoleBindings["accuser"]);
            Assert.Equal(lab.Situation.ThiefId, firing.RoleBindings["accused"]);
            Assert.Equal(new[] { "name_charge", "invite_answer" }, firing.BeatIds);
            Assert.Equal(new[] { "record_social_pressure" }, firing.ConsequenceHookIds);
            Assert.Equal(factsBefore, lab.World.Knowledge.Facts.Count);
            Assert.Single(lab.Situation.Thread.StoryletFirings);
        }

        [Fact]
        public void AStoryletRefusesWhenItsFocusNoLongerBelongsToTheThread()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = TheftStorylet();
            lab.Situation.Thread.FactIds.Remove(lab.Situation.TheftFactId);

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                definition,
                lab.World,
                lab.Vanilla,
                lab.Situation.Thread,
                lab.Situation.WitnessId,
                lab.Situation.ThiefId,
                lab.Situation.TheftFactId);

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("no longer part", opportunity.RefusalReason);
            Assert.Empty(lab.Situation.Thread.StoryletFirings);
        }

        [Fact]
        public void AStoryletRefusesWhenTheScenePreconditionsLapse()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = TheftStorylet();
            lab.Vanilla.Kill(lab.Situation.ThiefId);

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                definition,
                lab.World,
                lab.Vanilla,
                lab.Situation.Thread,
                lab.Situation.WitnessId,
                lab.Situation.ThiefId,
                lab.Situation.TheftFactId);

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("scene preconditions", opportunity.RefusalReason);
        }

        [Fact]
        public void FiredStoryletHistorySurvivesARoundTripWithoutContent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = EngineWithTheftStorylet();
            StoryletOpportunity opportunity = Assert.Single(engine.Find(
                lab.World,
                lab.Vanilla,
                lab.Situation.Thread,
                lab.Situation.WitnessId,
                lab.Situation.ThiefId,
                lab.Situation.TheftFactId));
            engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            StoryletFiring firing = Assert.Single(Assert.Single(reloaded.Threads).StoryletFirings);

            Assert.Equal("storylet.test.theft_witness_confronts_thief", firing.StoryletId);
            Assert.Equal(lab.Situation.TheftFactId, firing.FocusFactId);
            Assert.Equal(lab.Situation.WitnessId, firing.RoleBindings["accuser"]);
            Assert.Equal(new[] { "name_charge", "invite_answer" }, firing.BeatIds);
            Assert.Equal(new[] { "record_social_pressure" }, firing.ConsequenceHookIds);
        }

        private static StoryletEngine EngineWithTheftStorylet()
        {
            StoryletEngine engine = new StoryletEngine();
            engine.Register(TheftStorylet());
            return engine;
        }

        private static StoryletDefinition TheftStorylet()
        {
            StoryletDefinition definition = new StoryletDefinition("storylet.test.theft_witness_confronts_thief");
            definition.SituationTags.Add("theft");
            definition.ToneTags.Add("tense");
            definition.RequiredRoles.Add(new StoryletRole("accuser", StoryletRoleSource.Actor));
            definition.RequiredRoles.Add(new StoryletRole("accused", StoryletRoleSource.Target));
            definition.RequiredRoles.Add(new StoryletRole("knower", StoryletRoleSource.AnyParticipantWhoKnowsFocus));
            definition.Preconditions.Add(StoryletPrecondition.FactBelongsToThread());
            definition.Preconditions.Add(StoryletPrecondition.FocusPredicate(FactPredicates.Stole));
            definition.Preconditions.Add(StoryletPrecondition.FocusTruth(TruthState.True));
            definition.Preconditions.Add(StoryletPrecondition.RoleKnowsFocus("accuser"));
            definition.Preconditions.Add(StoryletPrecondition.RoleAlive("accuser"));
            definition.Preconditions.Add(StoryletPrecondition.RoleAlive("accused"));
            definition.Beats.Add(new StoryletBeat("name_charge"));
            definition.Beats.Add(new StoryletBeat("invite_answer"));
            definition.ConsequenceHooks.Add(new StoryletConsequenceHook("record_social_pressure"));
            return definition;
        }
    }
}
