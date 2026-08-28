using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-019: gossip has to run through a real town on a schedule, not through one scripted line
    /// in an escalation ladder. These pin the three properties that make that safe to turn loose —
    /// it is bounded, it does not run twice for the same day, and it never hands over proof.
    /// </summary>
    public class RumorCirculationTests
    {
        /// <summary>
        /// The theft laboratory plus a crowd of ordinary townspeople standing in the same zone,
        /// which is what the three-person cast has never had.
        /// </summary>
        private static TheftLaboratory Town(int bystanders = 10)
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            for (int i = 0; i < bystanders; i++)
            {
                EntityId id = lab.World.NewId("npc");
                lab.World.Registry.Add(new NarrativeNpc(id, "Townsperson " + i));
                lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
            }

            // The scheduler refuses to back-date, so establish "today" before moving the clock.
            lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            return lab;
        }

        private static int Knowers(TheftLaboratory lab, EntityId factId)
        {
            int count = 0;
            foreach (EntityId knower in lab.World.Knowledge.Knowers(factId))
            {
                count++;
            }

            return count;
        }

        /// <summary>The step's own completion test: one witness, and days later the town half-knows.</summary>
        [Fact]
        public void AFactOneWitnessSawReachesTheTownOverDays()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;
            int before = Knowers(lab, theft);

            for (int day = 1; day <= 6; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            Assert.True(
                Knowers(lab, theft) > before,
                "nobody new heard about the theft in six days of a busy zone");
        }

        [Fact]
        public void WhatTheTownHeardItCannotProve()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;

            for (int day = 1; day <= 6; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            int hearsay = 0;
            foreach (EntityId knower in lab.World.Knowledge.Knowers(theft))
            {
                lab.World.Knowledge.TryGetBelief(knower, theft, out KnowledgeRecord belief);
                if (belief.Source != KnowledgeSource.Hearsay)
                {
                    continue;
                }

                hearsay++;
                Assert.False(belief.CanProve, knower + " came out of gossip able to prove a theft");
            }

            Assert.True(hearsay > 0, "the test proves nothing if nothing was ever retold");
        }

        /// <summary>
        /// Confidence has to fall along the chain. A story that has been round the market twice is
        /// not as good as the account of the person who watched it happen.
        /// </summary>
        [Fact]
        public void WhatTheTownHeardItBelievesLessThanTheWitnessDoes()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;

            for (int day = 1; day <= 6; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            lab.World.Knowledge.TryGetBelief(lab.Situation.WitnessId, theft, out KnowledgeRecord seen);
            foreach (EntityId knower in lab.World.Knowledge.Knowers(theft))
            {
                if (!lab.World.Knowledge.TryGetBelief(knower, theft, out KnowledgeRecord belief)
                    || belief.Source != KnowledgeSource.Hearsay)
                {
                    continue;
                }

                Assert.True(belief.Confidence < seen.Confidence);
            }
        }

        /// <summary>
        /// The save-scum guard. Loading the same day five times must circulate once — otherwise a
        /// player who dislikes what the neighbours started saying can re-roll it from the load
        /// screen, and the town's memory becomes a function of how often somebody pressed load.
        /// </summary>
        [Fact]
        public void ReloadingTheSameDayDoesNotCirculateAgain()
        {
            TheftLaboratory lab = Town();
            lab.Vanilla.AdvanceDays(1);

            RumorRound first = lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            RumorRound second = lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            RumorRound third = lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(1, first.DaysRun);
            Assert.False(second.DidAnything);
            Assert.False(third.DidAnything);
        }

        /// <summary>
        /// A fortnight away should feel like a fortnight, but nobody pays for a hundred rounds of
        /// gossip on one load screen. The catch-up is capped, and the round says so.
        /// </summary>
        [Fact]
        public void ALongAbsenceIsCaughtUpButCapped()
        {
            TheftLaboratory lab = Town();
            lab.Vanilla.AdvanceDays(40);

            RumorRound round = lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(40, round.DaysOwed);
            Assert.Equal(lab.Circulation.MaxCatchUpDays, round.DaysRun);
            Assert.True(round.Tells <= lab.Circulation.MaxTellsPerDay);
        }

        /// <summary>
        /// Nobody gossips on the player's behalf, and nothing arrives in the player's head that
        /// they were never told. Both directions of standing rule 22.
        /// </summary>
        [Fact]
        public void ThePlayerNeitherSpreadsNorSilentlyLearns()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;

            // Give the player something worth spreading, learned first-hand.
            lab.World.Knowledge.Teach(lab.Player, theft, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, canProve: true);

            EntityId secret = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(secret, lab.Situation.ThiefId, "owes", lab.Situation.VictimId));
            lab.World.Knowledge.Teach(lab.Situation.WitnessId, secret, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, canProve: false);

            for (int day = 1; day <= 8; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            Assert.False(
                lab.World.Knowledge.Knows(lab.Player, secret),
                "the player learned something in the background that nobody ever told them");

            foreach (EntityId knower in lab.World.Knowledge.Knowers(theft))
            {
                lab.World.Knowledge.TryGetBelief(knower, theft, out KnowledgeRecord belief);
                Assert.NotEqual(lab.Player, belief.ToldBy);
            }
        }

        /// <summary>
        /// The player's own beliefs must not quietly weaken. A dialogue option that disappears
        /// because a background timer ran is content deletion, which standing rule 11 forbids.
        /// </summary>
        [Fact]
        public void ThePlayersOwnBeliefsDoNotFade()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;
            lab.World.Knowledge.Teach(lab.Player, theft, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            for (int day = 1; day <= 10; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            lab.World.Knowledge.TryGetBelief(lab.Player, theft, out KnowledgeRecord belief);
            Assert.Equal(0.6, belief.Confidence, 3);
        }

        /// <summary>
        /// A rumour nobody repeats gets weaker; what somebody saw with their own eyes does not.
        /// </summary>
        [Fact]
        public void UnrepeatedHearsayFadesAndFirstHandKnowledgeDoesNot()
        {
            TheftLaboratory lab = Town(bystanders: 0);
            EntityId theft = lab.Situation.TheftFactId;

            EntityId hermit = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(hermit, "Hermit"));
            lab.Vanilla.Define(hermit, level: 1, zone: lab.World.NewId("zone"));
            lab.World.Knowledge.Teach(hermit, theft, KnowledgeSource.Hearsay, 0.8, lab.Vanilla.Now, canProve: false);

            lab.World.Knowledge.TryGetBelief(lab.Situation.WitnessId, theft, out KnowledgeRecord seenBefore);
            double witnessedBefore = seenBefore.Confidence;

            for (int day = 1; day <= 5; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            lab.World.Knowledge.TryGetBelief(hermit, theft, out KnowledgeRecord faded);
            lab.World.Knowledge.TryGetBelief(lab.Situation.WitnessId, theft, out KnowledgeRecord seenAfter);

            Assert.True(faded.Confidence < 0.8, "an unrepeated rumour held its confidence for five days");
            Assert.True(faded.Confidence >= lab.Circulation.FadedFloor);
            Assert.Equal(witnessedBefore, seenAfter.Confidence, 3);
        }

        [Fact]
        public void ADeadTownHasNothingToSay()
        {
            TheftLaboratory lab = Town();
            foreach (NarrativeNpc npc in new List<NarrativeNpc>(lab.World.Registry.Npcs.Values))
            {
                if (npc.Id != lab.Player)
                {
                    lab.Vanilla.Kill(npc.Id);
                }
            }

            lab.Vanilla.AdvanceDays(3);
            RumorRound round = lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);

            Assert.Equal(0, round.Tells);
        }

        /// <summary>
        /// Determinism. The fact store is a dictionary, so selection sorts rather than trusting
        /// enumeration order — two runs from the same seed must gossip about the same things.
        /// </summary>
        [Fact]
        public void TheSameWorldGossipsTheSameWay()
        {
            List<string> Play()
            {
                TheftLaboratory lab = Town();
                List<string> log = new List<string>();
                for (int day = 1; day <= 6; day++)
                {
                    lab.Vanilla.AdvanceDays(1);
                    log.AddRange(lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now).Notes);
                }

                return log;
            }

            Assert.Equal(Play(), Play());
        }

        /// <summary>
        /// Who owns what is true, queryable and nobody's news. The first live circulation spent
        /// half of day one's budget telling the town that the victim owned a ring.
        /// </summary>
        [Fact]
        public void StandingArrangementsAreNotGossip()
        {
            TheftLaboratory lab = Town();
            EntityId ownership = lab.Situation.OwnershipFactId;
            int before = Knowers(lab, ownership);

            for (int day = 1; day <= 10; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            Assert.Equal(before, Knowers(lab, ownership));
            Assert.False(FactPredicates.IsNewsworthy(FactPredicates.Possesses));
            Assert.True(FactPredicates.IsNewsworthy(FactPredicates.Stole));
            Assert.False(FactPredicates.IsNewsworthy("something_invented_later"));
        }

        /// <summary>
        /// The culprit knows about the crime better than anybody and wants it forgotten. A
        /// scheduler that treats every knower as a speaker has him telling the market what he did.
        /// </summary>
        [Fact]
        public void NobodyGossipsAboutTheirOwnCrime()
        {
            TheftLaboratory lab = Town();
            EntityId theft = lab.Situation.TheftFactId;

            // Take the witness out of it, so the thief is the only person left who could talk.
            lab.Vanilla.Kill(lab.Situation.WitnessId);

            for (int day = 1; day <= 10; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            foreach (EntityId knower in lab.World.Knowledge.Knowers(theft))
            {
                lab.World.Knowledge.TryGetBelief(knower, theft, out KnowledgeRecord belief);
                Assert.NotEqual(lab.Situation.ThiefId, belief.ToldBy);
            }
        }

        [Fact]
        public void MissingPiecesAreSafeToAskAbout()
        {
            TheftLaboratory lab = Town();

            Assert.False(lab.Circulation.Run(null, lab.Vanilla, lab.Vanilla.Now).DidAnything);
            Assert.False(lab.Circulation.Run(lab.World, null, lab.Vanilla.Now).DidAnything);
        }
    }
}
