using BrilliantQuesting.Foundation;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-008: a scene is revalidated against the world as it is now, not as it was when the
    /// thread was written. The first playtest had the player attack the thief within ten minutes,
    /// so a dead cast member is not a corner case — it is the first thing a player tries.
    /// </summary>
    public class SceneStatusTests
    {
        [Fact]
        public void AnUntouchedSituationIsPlayable()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.ThiefId);

            Assert.True(scene.IsPlayable);
            Assert.Empty(scene.Missing);
            Assert.Equal(string.Empty, scene.Reason);
        }

        [Fact]
        public void TalkingToSomebodyWhoIsDeadIsNotAScene()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Kill(lab.Situation.ThiefId);

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.ThiefId);

            Assert.False(scene.IsPlayable);
            Assert.Contains("past being asked", scene.Reason);
        }

        /// <summary>
        /// A theft with a dead witness is still a theft, and arguably a better one. Losing one of
        /// the cast degrades a situation; it does not end it.
        /// </summary>
        [Fact]
        public void LosingOneOfTheCastDoesNotEndTheSituation()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Kill(lab.Situation.WitnessId);

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.VictimId);

            Assert.True(scene.IsPlayable);
            Assert.Contains(lab.Situation.WitnessId, scene.Missing);
        }

        [Fact]
        public void LosingEveryoneDoes()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Kill(lab.Situation.WitnessId);
            lab.Vanilla.Kill(lab.Situation.ThiefId);

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.VictimId);

            Assert.False(scene.IsPlayable);
            Assert.Contains("nobody left", scene.Reason);
        }

        [Fact]
        public void AResolvedThreadIsNotPlayed()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Situation.Thread.State = ThreadState.Resolved;

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.ThiefId);

            Assert.False(scene.IsPlayable);
            Assert.Contains("settled", scene.Reason);
        }

        [Fact]
        public void SomebodyOutsideTheCastIsNotOfferedTheSituation()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Player);

            Assert.False(scene.IsPlayable);
            Assert.Contains("nothing to do with this", scene.Reason);
        }

        /// <summary>
        /// The description must name the dead rather than go on describing them as present, which
        /// is the "lying dialogue" the step is named for.
        /// </summary>
        [Fact]
        public void TheDeadAreNamedRatherThanDescribedAsPresent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Kill(lab.Situation.WitnessId);

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, EntityId.None);
            string missing = scene.DescribeMissing(lab.World);

            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.WitnessId), missing);
            Assert.Contains("is dead", missing);
        }

        [Fact]
        public void NobodyMissingSaysNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            SceneStatus scene = SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, EntityId.None);

            Assert.Equal(string.Empty, scene.DescribeMissing(lab.World));
        }

        [Fact]
        public void MissingPiecesAreSafeToAskAbout()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.False(SceneStatus.Check(null, lab.Vanilla, lab.Situation.Thread, lab.Player).IsPlayable);
            Assert.False(SceneStatus.Check(lab.World, lab.Vanilla, null, lab.Player).IsPlayable);
            Assert.Equal(string.Empty, SceneStatus.Check(null, null, null, EntityId.None).DescribeMissing(null));
        }

        [Fact]
        public void AFocusMustStillBelongToTheThread()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.True(SceneStatus.FocusStillResolvable(lab.World, lab.Situation.Thread, lab.Situation.TheftFactId));
            Assert.False(SceneStatus.FocusStillResolvable(lab.World, lab.Situation.Thread, EntityId.None));
            Assert.False(SceneStatus.FocusStillResolvable(lab.World, lab.Situation.Thread, lab.World.NewId("fact")));
        }
    }
}
