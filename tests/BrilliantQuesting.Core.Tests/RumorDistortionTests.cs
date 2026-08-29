using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-020: a rumour that only ever loses confidence is a rumour that is still true. These pin
    /// the two ways a story goes wrong — it garbles on its own, or somebody says it wrong on
    /// purpose — and the property that makes either survivable: the world still knows what
    /// actually happened.
    /// </summary>
    public class RumorDistortionTests
    {
        private static TheftLaboratory Town(int bystanders = 10)
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            for (int i = 0; i < bystanders; i++)
            {
                EntityId id = lab.World.NewId("npc");
                lab.World.Registry.Add(new NarrativeNpc(id, "Townsperson " + i));
                lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
            }

            lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            return lab;
        }

        private static List<Fact> VersionsOf(TheftLaboratory lab, EntityId trueFactId)
        {
            List<Fact> versions = new List<Fact>();
            foreach (Fact fact in lab.World.Knowledge.Facts.Values)
            {
                if (fact.DistortionOf == trueFactId)
                {
                    versions.Add(fact);
                }
            }

            return versions;
        }

        // -- distortion --------------------------------------------------------------------

        /// <summary>
        /// The step's completion test, first half: a false belief propagates, and the truth is
        /// still sitting there untouched beside it.
        /// </summary>
        [Fact]
        public void AStoryGarblesAsItTravelsAndTheTruthIsStillRecorded()
        {
            TheftLaboratory lab = Town();
            lab.Circulation.Distortion.Chance = 1.0;
            EntityId theft = lab.Situation.TheftFactId;

            for (int day = 1; day <= 8; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            List<Fact> garbled = VersionsOf(lab, theft);
            Assert.NotEmpty(garbled);

            foreach (Fact version in garbled)
            {
                Assert.True(version.IsUntrue, "a garbled retelling was recorded as true");
                Assert.NotEqual(lab.Situation.ThiefId, version.Subject);
                Assert.Empty(version.EvidenceIds);
            }

            Fact truth = lab.World.Knowledge.GetFact(theft);
            Assert.Equal(TruthState.True, truth.Truth);
            Assert.Equal(lab.Situation.ThiefId, truth.Subject);
            Assert.True(lab.World.Knowledge.BelievesConfidently(lab.Situation.WitnessId, theft, 0.5));
        }

        /// <summary>
        /// Everyone who mishears the same story the same way must hold the *same* fact. Minting
        /// one per retelling makes "the town thinks it was Kel" unaskable and grows the fact
        /// store with every conversation.
        /// </summary>
        [Fact]
        public void MishearingTheSameWayIsOneBeliefNotMany()
        {
            TheftLaboratory lab = Town(bystanders: 20);
            lab.Circulation.Distortion.Chance = 1.0;

            for (int day = 1; day <= 10; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            List<Fact> versions = VersionsOf(lab, lab.Situation.TheftFactId);
            List<EntityId> blamed = new List<EntityId>();
            foreach (Fact version in versions)
            {
                Assert.DoesNotContain(version.Subject, blamed);
                blamed.Add(version.Subject);
            }

            // One false version per person blamed, however many people repeated it.
            Assert.True(versions.Count <= blamed.Count);
        }

        [Fact]
        public void NothingGarbledCanEverBeProved()
        {
            TheftLaboratory lab = Town();
            lab.Circulation.Distortion.Chance = 1.0;

            for (int day = 1; day <= 8; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            foreach (Fact version in VersionsOf(lab, lab.Situation.TheftFactId))
            {
                foreach (EntityId knower in lab.World.Knowledge.Knowers(version.Id))
                {
                    Assert.False(lab.World.Knowledge.CanProve(knower, version.Id));
                }
            }
        }

        /// <summary>
        /// A clear retelling stays clear. Garbling belongs at the far end of a chain, not at the
        /// start of one, or the witness's own account would be unreliable.
        /// </summary>
        [Fact]
        public void AConfidentRetellingDoesNotGarble()
        {
            TheftLaboratory lab = Town();
            RumorDistortion distortion = new RumorDistortion { Chance = 1.0 };
            Fact theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);

            Fact said = distortion.Retell(
                lab.World, lab.Vanilla, theft, lab.Situation.WitnessId, lab.Situation.VictimId,
                transmitted: 0.9, rng: lab.World.Rng);

            Assert.Same(theft, said);
        }

        /// <summary>
        /// Nobody is ever told they did it themselves. Without this the thief told the victim the
        /// victim had robbed himself, and two days later the ledger recorded him accusing himself.
        /// </summary>
        [Fact]
        public void NobodyIsToldTheyDidItThemselves()
        {
            TheftLaboratory lab = Town();
            RumorDistortion distortion = new RumorDistortion { Chance = 1.0 };
            Fact theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);

            for (int attempt = 0; attempt < 50; attempt++)
            {
                Fact said = distortion.Retell(
                    lab.World, lab.Vanilla, theft, lab.Situation.WitnessId, lab.Situation.VictimId,
                    transmitted: 0.3, rng: lab.World.Rng);

                Assert.NotEqual(lab.Situation.VictimId, said.Subject);
            }
        }

        /// <summary>
        /// The town deciding on its own that the player is a thief is a situation the mod owes the
        /// player an entrance to — that is BQ-044 and its decline surface, not a side effect of a
        /// background scheduler.
        /// </summary>
        [Fact]
        public void TheStoryIsNeverPinnedOnThePlayer()
        {
            TheftLaboratory lab = Town();
            lab.Circulation.Distortion.Chance = 1.0;

            for (int day = 1; day <= 12; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            foreach (Fact version in VersionsOf(lab, lab.Situation.TheftFactId))
            {
                Assert.NotEqual(lab.Player, version.Subject);
            }
        }

        [Fact]
        public void ATownThatNeverMisremembersIsStillAllowed()
        {
            TheftLaboratory lab = Town();
            lab.Circulation.Distortion = null;

            for (int day = 1; day <= 10; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            Assert.Empty(VersionsOf(lab, lab.Situation.TheftFactId));
        }

        // -- deliberate lies ---------------------------------------------------------------

        /// <summary>
        /// The step's completion test, second half: a false belief is acted on. The thief names
        /// the one person who saw him, the victim believes the more convincing account, and the
        /// accusation lands on somebody innocent — while the graph still says who did it.
        /// </summary>
        [Fact]
        public void AThiefWhoLiesGetsSomebodyElseAccused()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.AdvanceDays(15);

            WorldEvent accusation = Assert.Single(lab.World.Ledger.OfType(WorldEventType.FalseAccusation));
            Assert.Equal(lab.Situation.VictimId, accusation.Actor);
            Assert.NotEqual(lab.Situation.ThiefId, accusation.Target);

            Fact truth = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);
            Assert.Equal(TruthState.True, truth.Truth);
            Assert.Equal(lab.Situation.ThiefId, truth.Subject);
        }

        /// <summary>
        /// The lie itself is a true fact of the world. Nobody but the liar knows it yet, which is
        /// what leaves it there to be found (BQ-073) instead of merely regretted.
        /// </summary>
        [Fact]
        public void TheWorldRecordsThatTheLieWasTold()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.AdvanceDays(15);

            Fact lie = null;
            foreach (Fact fact in lab.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.LiedAbout)
                {
                    lie = fact;
                }
            }

            Assert.NotNull(lie);
            Assert.Equal(lab.Situation.ThiefId, lie.Subject);
            Assert.Equal(lab.Situation.TheftFactId, lie.Object);
            Assert.Equal(TruthState.True, lie.Truth);

            List<EntityId> knowers = new List<EntityId>(lab.World.Knowledge.Knowers(lie.Id));
            Assert.Equal(new List<EntityId> { lab.Situation.ThiefId }, knowers);
        }

        /// <summary>
        /// An honest thief takes what is coming. If every culprit lies, the lie stops meaning
        /// anything and the personality layer is decoration.
        /// </summary>
        [Fact]
        public void AnHonestThiefDoesNotLie()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Registry.GetNpc(lab.Situation.ThiefId).Personality.Honesty = 0.9;

            lab.AdvanceDays(15);

            Assert.Empty(VersionsOf(lab, lab.Situation.TheftFactId));
            Assert.Empty(lab.World.Ledger.OfType(WorldEventType.FalseAccusation));

            // Right about who did it, unable to show anyone why — which is not the same as lying.
            WorldEvent accusation = Assert.Single(lab.World.Ledger.OfType(WorldEventType.AccusationMade));
            Assert.Equal(lab.Situation.ThiefId, accusation.Target);
        }

        /// <summary>
        /// You cannot lie about what you do not know. Somebody repeating something they believe is
        /// mistaken, not dishonest, and the world must not record otherwise.
        /// </summary>
        [Fact]
        public void SomebodyWhoBelievesWhatTheySayIsNotLying()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId theft = lab.Situation.TheftFactId;
            EntityId bystander = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(bystander, "Bystander"));
            lab.Vanilla.Define(bystander, level: 1, zone: lab.Zone);

            Fact invented = new Fact(lab.World.NewId("fact"), bystander, FactPredicates.Stole, lab.Situation.ItemId, truth: TruthState.False)
            {
                DistortionOf = theft
            };
            lab.World.Knowledge.AddFact(invented);

            bool lied = lab.Rumors.Lie(bystander, lab.Situation.VictimId, theft, invented.Id, lab.Vanilla.Now, 0.9);

            Assert.False(lied, "somebody who knows nothing about the theft managed to lie about it");
            Assert.False(lab.World.Knowledge.Knows(lab.Situation.VictimId, invented.Id));
        }

        /// <summary>
        /// Repeating one lie to six people is one lie. Six identical facts would turn the graph
        /// into a transcript.
        /// </summary>
        [Fact]
        public void RepeatingALieDoesNotMultiplyIt()
        {
            TheftLaboratory lab = Town(bystanders: 6);
            EntityId theft = lab.Situation.TheftFactId;
            Fact blamed = lab.Circulation.Distortion.Blame(
                lab.World, lab.Vanilla, lab.World.Knowledge.GetFact(theft),
                lab.Situation.ThiefId, lab.Situation.VictimId, lab.World.Rng);

            foreach (NarrativeNpc npc in new List<NarrativeNpc>(lab.World.Registry.Npcs.Values))
            {
                lab.Rumors.Lie(lab.Situation.ThiefId, npc.Id, theft, blamed.Id, lab.Vanilla.Now, 0.8);
            }

            int lies = 0;
            foreach (Fact fact in lab.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.LiedAbout)
                {
                    lies++;
                }
            }

            Assert.Equal(1, lies);
        }

        // -- what a listener will not have -------------------------------------------------

        /// <summary>
        /// Nobody believes a rumour that they themselves did it. The distortion policy refuses to
        /// name the listener, but once a false fact exists it circulates like any other, and the
        /// first run had townspeople hearing — and accepting — that they were the thief.
        /// </summary>
        [Fact]
        public void NobodyBelievesARumourThatTheyDidItThemselves()
        {
            TheftLaboratory lab = Town(bystanders: 12);
            lab.Circulation.Distortion.Chance = 1.0;

            for (int day = 1; day <= 14; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            List<Fact> versions = VersionsOf(lab, lab.Situation.TheftFactId);
            Assert.NotEmpty(versions);

            foreach (Fact version in versions)
            {
                Assert.False(
                    lab.World.Knowledge.Knows(version.Subject, version.Id),
                    lab.World.Registry.NameOf(version.Subject) + " was talked into believing they were the thief");
            }
        }

        /// <summary>
        /// The people who actually know cannot be gossiped out of it. Without this the witness who
        /// watched the theft also held all three rival stories about who else had done it, which
        /// makes "who believes the lie" a question with everybody as its answer.
        /// </summary>
        [Fact]
        public void SeeingItHappenBeatsHearingOtherwise()
        {
            TheftLaboratory lab = Town(bystanders: 12);
            lab.Circulation.Distortion.Chance = 1.0;

            for (int day = 1; day <= 14; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            List<Fact> versions = VersionsOf(lab, lab.Situation.TheftFactId);
            Assert.NotEmpty(versions);

            foreach (Fact version in versions)
            {
                Assert.False(lab.World.Knowledge.Knows(lab.Situation.WitnessId, version.Id), "the witness was talked out of what she saw");
                Assert.False(lab.World.Knowledge.Knows(lab.Situation.ThiefId, version.Id), "the thief was talked out of what he did");
            }

            Assert.True(lab.World.Knowledge.BelievesConfidently(lab.Situation.WitnessId, lab.Situation.TheftFactId, 0.9));
        }

        /// <summary>
        /// Hearsay against hearsay still competes, though — that is how one rumour beats another,
        /// and it is what lets the victim be talked round in the first place.
        /// </summary>
        [Fact]
        public void OneRumourCanStillBeatAnother()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId victim = lab.Situation.VictimId;
            EntityId theft = lab.Situation.TheftFactId;

            Fact blamed = lab.Circulation.Distortion.Blame(
                lab.World, lab.Vanilla, lab.World.Knowledge.GetFact(theft),
                lab.Situation.ThiefId, victim, lab.World.Rng);

            lab.World.Knowledge.Teach(victim, theft, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, false);
            Assert.True(lab.Rumors.Lie(lab.Situation.ThiefId, victim, theft, blamed.Id, lab.Vanilla.Now, 0.9));

            lab.World.Knowledge.TryGetBelief(victim, blamed.Id, out KnowledgeRecord took);
            Assert.Equal(0.9, took.Confidence, 3);
        }

        // -- acting on it ------------------------------------------------------------------

        /// <summary>
        /// Ties go to the truth. Somebody equally sure of two accounts has no reason to prefer
        /// the wrong one, and a coin-flip there would make the outcome unexplainable.
        /// </summary>
        [Fact]
        public void EqualConvictionFavoursTheTruth()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId victim = lab.Situation.VictimId;
            EntityId theft = lab.Situation.TheftFactId;

            // An honest thief, so the only two accounts the victim holds are the ones set here.
            lab.World.Registry.GetNpc(lab.Situation.ThiefId).Personality.Honesty = 0.9;

            Fact blamed = lab.Circulation.Distortion.Blame(
                lab.World, lab.Vanilla, lab.World.Knowledge.GetFact(theft),
                lab.Situation.ThiefId, victim, lab.World.Rng);

            lab.World.Knowledge.Teach(victim, theft, KnowledgeSource.Hearsay, 0.7, lab.Vanilla.Now, false);
            lab.World.Knowledge.Teach(victim, blamed.Id, KnowledgeSource.Hearsay, 0.7, lab.Vanilla.Now, false);

            lab.AdvanceDays(15);

            Assert.Empty(lab.World.Ledger.OfType(WorldEventType.FalseAccusation));
        }

        /// <summary>
        /// An innocent who gets named must not be taught that they are a participant in the crime,
        /// or the graph starts agreeing with the rumour and the truth has nowhere left to live.
        /// </summary>
        [Fact]
        public void BeingAccusedDoesNotMakeSomebodyGuiltyInTheGraph()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.AdvanceDays(15);

            WorldEvent accusation = Assert.Single(lab.World.Ledger.OfType(WorldEventType.FalseAccusation));
            EntityId innocent = accusation.Target;

            // They may well know about the theft - the person the thief pins it on is often the
            // one who saw him. What must never happen is the graph recording them as having been
            // in on it, which is what teaching Participant off the back of an accusation does.
            if (lab.World.Knowledge.TryGetBelief(innocent, lab.Situation.TheftFactId, out KnowledgeRecord belief))
            {
                Assert.NotEqual(KnowledgeSource.Participant, belief.Source);
            }

            Fact truth = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);
            Assert.NotEqual(innocent, truth.Subject);
        }

        [Fact]
        public void ThreadsStillResolveWhenNobodyBelievesAnything()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Kill(lab.Situation.WitnessId);
            lab.World.Registry.GetNpc(lab.Situation.ThiefId).Personality.Honesty = 0.9;

            lab.AdvanceDays(15);

            Assert.Equal("unsolved", lab.Situation.Thread.Resolution);
            Assert.Equal(ThreadState.Dormant, lab.Situation.Thread.State);
        }
    }
}
