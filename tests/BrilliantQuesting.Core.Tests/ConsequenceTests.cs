using System.Linq;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ConsequenceTests
    {
        [Fact]
        public void ACleanTheftChangesNothingAnyoneCanSee()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

            EntityId thief = lab.Situation.ThiefId;
            int affinityBefore = lab.Vanilla.GetAffinity(thief);
            int karmaBefore = lab.Vanilla.Karma;

            lab.Perform("pickpocket", thief);

            Assert.Single(lab.Vanilla.GetInventory(lab.Player));
            Assert.Empty(lab.Vanilla.GetInventory(thief));

            // The theft is in history. Nobody has reacted to it, because nobody saw it.
            Assert.Equal(affinityBefore, lab.Vanilla.GetAffinity(thief));
            Assert.Equal(karmaBefore, lab.Vanilla.Karma);
            Assert.Empty(lab.World.Memories.MemoriesAbout(thief, lab.Player));
        }

        [Fact]
        public void GettingCaughtCreatesWitnessesWhoCanProveIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);

            EntityId thief = lab.Situation.ThiefId;
            int karmaBefore = lab.Vanilla.Karma;

            var outcome = lab.Perform("pickpocket", thief);

            Assert.True(lab.Vanilla.Karma < karmaBefore);
            Assert.True(lab.Vanilla.GetAffinity(thief) < 0);

            // The witness who happened to be standing there now holds a provable fact about you.
            EntityId witness = lab.Situation.WitnessId;
            bool witnessKnowsSomethingNew = false;
            foreach (var belief in lab.World.Knowledge.BeliefsOf(witness))
            {
                var fact = lab.World.Knowledge.GetFact(belief.FactId);
                if (fact.Subject == lab.Player && belief.CanProve)
                {
                    witnessKnowsSomethingNew = true;
                }
            }

            Assert.True(witnessKnowsSomethingNew, outcome.Explain());
        }

        [Fact]
        public void HelpingSomeoneMovesVanillaAffinityAndLeavesAReasonBehind()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            EntityId victim = lab.Situation.VictimId;

            lab.Perform("pickpocket", lab.Situation.ThiefId);
            int before = lab.Vanilla.GetAffinity(victim);
            lab.Perform("return_item", victim);

            Assert.True(lab.Vanilla.GetAffinity(victim) > before);

            var memory = Assert.Single(lab.World.Memories.MemoriesAbout(victim, lab.Player));
            Assert.Equal("got_property_back", memory.SummaryTag);
            Assert.Equal(lab.Vanilla.GetAffinity(victim) - before, memory.AffinityContribution);
        }

        [Fact]
        public void ConsequencesDoNotCascadeWithoutBound()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalFail);

            // A critical failure fans out into several events; the ledger must settle, not loop.
            lab.Perform("intimidate", lab.Situation.WitnessId);

            Assert.True(lab.World.Ledger.Count > 0);
            Assert.True(lab.World.Ledger.Count < 20);
        }

        [Fact]
        public void EveryWorldEventTypeHasAConsequenceProfileOrNamedExemption()
        {
            WorldEventType[] missing = ConsequenceProfiles.MissingProfiles().ToArray();

            Assert.Empty(missing);
            Assert.Empty(ConsequenceProfiles.Exemptions);
        }
    }
}
