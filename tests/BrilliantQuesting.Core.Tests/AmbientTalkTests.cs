using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-035: a player who has been told nothing should still be able to find out that something
    /// is going on, by standing where people are talking. These pin what makes that safe — it is
    /// somebody's voice rather than an announcement, it only carries what the town is repeating,
    /// nothing is learned that was not said, and it cannot be pumped by walking in circles.
    /// </summary>
    public class AmbientTalkTests
    {
        /// <summary>The theft laboratory with a crowd of ordinary townspeople in the player's zone.</summary>
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

        /// <summary>Somebody in the zone who was told about the theft rather than seeing it.</summary>
        private static EntityId Neighbour(TheftLaboratory lab, string name, double confidence = 0.8)
        {
            EntityId id = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(id, name));
            lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
            lab.World.Knowledge.Teach(
                id,
                lab.Situation.TheftFactId,
                KnowledgeSource.Hearsay,
                confidence,
                lab.Vanilla.Now,
                false,
                lab.Situation.WitnessId);
            return id;
        }

        private static SpokenRemark Hear(TheftLaboratory lab)
        {
            return lab.Ambient.Next(lab.World, lab.Vanilla, lab.Vanilla.Now);
        }

        /// <summary>
        /// The step's own completion test: the player learns that a theft happened without any
        /// part of the mod telling them so. What reaches them is a person's line, attributed to
        /// that person, and afterwards their journal holds the claim as a rumour.
        /// </summary>
        [Fact]
        public void APlayerToldNothingLearnsOfTheTheftBecauseSomebodyMentionedIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId gossip = Neighbour(lab, "Hedda");
            Assert.Empty(NarrativeJournal.Entries(lab.World, lab.Player));

            SpokenRemark remark = Hear(lab);

            Assert.NotNull(remark);
            Assert.Equal(gossip, remark.Speaker);
            Assert.Equal("Hedda", remark.SpeakerName);
            Assert.Equal(lab.Situation.TheftFactId, remark.FactId);
            Assert.Contains("stole", remark.Line);
            Assert.True(lab.Ambient.Deliver(lab.World, lab.Vanilla, remark, lab.Vanilla.Now));

            IReadOnlyList<JournalEntry> journal = NarrativeJournal.Entries(lab.World, lab.Player);
            JournalEntry entry = Assert.Single(journal);
            Assert.Equal(lab.Situation.TheftFactId, entry.FactId);
            Assert.Equal(JournalTag.Rumour, entry.Tag);
        }

        /// <summary>
        /// What is heard is a lead, not a case. It comes in weaker than the speaker held it and
        /// with nothing to show a guard, exactly as any other retelling does.
        /// </summary>
        [Fact]
        public void WhatThePlayerOverhearsCannotBeProved()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Neighbour(lab, "Hedda");

            lab.Ambient.Deliver(lab.World, lab.Vanilla, Hear(lab), lab.Vanilla.Now);

            Assert.True(lab.World.Knowledge.TryGetBelief(lab.Player, lab.Situation.TheftFactId, out KnowledgeRecord heard));
            Assert.Equal(KnowledgeSource.Hearsay, heard.Source);
            Assert.False(heard.CanProve);
            Assert.True(heard.Confidence < 0.8, "the player took it on as firmly as the person repeating it");
        }

        /// <summary>
        /// The line between this and the verbs the player chooses. The witness watched it happen
        /// and says nothing about it in the street; that is what questioning and eavesdropping are
        /// for, and both of those cost a check.
        /// </summary>
        [Fact]
        public void NobodyVolunteersWhatTheyThemselvesSaw()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.True(lab.World.Knowledge.Knows(lab.Situation.WitnessId, lab.Situation.TheftFactId));
            Assert.Null(Hear(lab));
        }

        /// <summary>
        /// The one person who never brings a matter up is the person it is about. Circulation's
        /// rule, for circulation's reason: the thief's strongest goal is not being found out.
        /// </summary>
        [Fact]
        public void TheSubjectOfTheStoryDoesNotRepeatIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // A claim about the thief that he himself has only heard, which is the only way the
            // subject rule can be told apart from the first-hand rule.
            EntityId debt = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(debt, lab.Situation.ThiefId, FactPredicates.Owes, lab.Situation.VictimId, "80 orens"));
            lab.World.Knowledge.Teach(lab.Situation.ThiefId, debt, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, false, lab.Situation.VictimId);

            Assert.Null(Hear(lab));

            // Somebody else holding the same secondhand claim will happily mention it.
            EntityId neighbour = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(neighbour, "Hedda"));
            lab.Vanilla.Define(neighbour, level: 3, zone: lab.Zone);
            lab.World.Knowledge.Teach(neighbour, debt, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, false, lab.Situation.VictimId);

            SpokenRemark remark = Hear(lab);
            Assert.NotNull(remark);
            Assert.Equal(neighbour, remark.Speaker);
            Assert.Equal(debt, remark.FactId);
        }

        /// <summary>
        /// Something people are actively keeping quiet is not what falls out of walking past. It
        /// stays reachable by the routes that cost something.
        /// </summary>
        [Fact]
        public void NothingIsSaidAloudAboutWhatIsBeingKeptQuiet()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hidden = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(hidden, lab.Situation.VictimId, FactPredicates.Extorted, lab.Situation.ThiefId, secrecy: 95));

            EntityId neighbour = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(neighbour, "Hedda"));
            lab.Vanilla.Define(neighbour, level: 3, zone: lab.Zone);
            lab.World.Knowledge.Teach(neighbour, hidden, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, false, lab.Situation.WitnessId);

            Assert.Null(Hear(lab));
        }

        /// <summary>
        /// Reading what somebody would say must not be the same as their having said it. The
        /// presentation layer decides whether the line reached the player, and until it says so
        /// the player has learned nothing.
        /// </summary>
        [Fact]
        public void LookingForARemarkTeachesNobodyAnything()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Neighbour(lab, "Hedda");

            Assert.NotNull(Hear(lab));
            Assert.NotNull(Hear(lab));

            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));
            Assert.Equal(NarrativeWorldState.NothingSaidYet, lab.World.LastAmbientRemarkMinute);
        }

        /// <summary>
        /// A town is a place where people occasionally say things, not a feed. Walking back and
        /// forth past the same person must not empty their head into the journal.
        /// </summary>
        [Fact]
        public void OneRemarkAtATimeAndTheClockHasToMove()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId neighbour = Neighbour(lab, "Hedda");

            EntityId debt = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(debt, lab.Situation.VictimId, FactPredicates.Owes, lab.Situation.ThiefId, "80 orens"));
            lab.World.Knowledge.Teach(neighbour, debt, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, false, lab.Situation.WitnessId);

            lab.Ambient.Deliver(lab.World, lab.Vanilla, Hear(lab), lab.Vanilla.Now);
            Assert.Null(Hear(lab));

            lab.Vanilla.Now = lab.Vanilla.Now.PlusMinutes(lab.Ambient.MinutesBetweenRemarks);
            SpokenRemark second = Hear(lab);
            Assert.NotNull(second);
            Assert.NotEqual(second.FactId, NarrativeJournal.Entries(lab.World, lab.Player)[0].FactId);
        }

        /// <summary>Nothing is repeated to somebody who already has it.</summary>
        [Fact]
        public void ThePlayerIsNotToldTheSameThingTwice()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Neighbour(lab, "Hedda");

            lab.Ambient.Deliver(lab.World, lab.Vanilla, Hear(lab), lab.Vanilla.Now);
            lab.Vanilla.Now = lab.Vanilla.Now.PlusMinutes(lab.Ambient.MinutesBetweenRemarks * 4);

            Assert.Null(Hear(lab));
        }

        /// <summary>
        /// The cooldown is world state, not adapter state. If it reset on load, saving and
        /// reloading would be a way to hear the whole town at once.
        /// </summary>
        [Fact]
        public void TheCooldownSurvivesSaveAndLoad()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Neighbour(lab, "Hedda");
            lab.Ambient.Deliver(lab.World, lab.Vanilla, Hear(lab), lab.Vanilla.Now);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(lab.World.LastAmbientRemarkMinute, reloaded.LastAmbientRemarkMinute);
            Assert.Equal(lab.Vanilla.Now.TotalMinutes, reloaded.LastAmbientRemarkMinute);
        }

        /// <summary>An empty street says nothing, and a dead man says nothing.</summary>
        [Fact]
        public void NobodySpeaksWhenThereIsNobodyThere()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId neighbour = Neighbour(lab, "Hedda");

            lab.Vanilla.Define(neighbour, level: 3, zone: lab.World.NewId("zone"));
            Assert.Null(Hear(lab));

            lab.Vanilla.Define(neighbour, level: 3, zone: lab.Zone);
            lab.Vanilla.Kill(neighbour);
            Assert.Null(Hear(lab));
        }

        /// <summary>
        /// The whole route, end to end and with nothing scripted: one person watched a theft, the
        /// town talked for a week, and the player who was never told anything walks through the
        /// market and hears about it.
        /// </summary>
        [Fact]
        public void AWeekOfGossipReachesThePlayerWhoAskedNobodyAnything()
        {
            TheftLaboratory lab = Town();

            for (int day = 1; day <= 6; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            SpokenRemark remark = Hear(lab);
            Assert.NotNull(remark);
            Assert.NotEqual(lab.Player, remark.Speaker);
            Assert.NotEqual(lab.Situation.WitnessId, remark.Speaker);
            Assert.True(lab.Ambient.Deliver(lab.World, lab.Vanilla, remark, lab.Vanilla.Now));
            Assert.NotEmpty(NarrativeJournal.Entries(lab.World, lab.Player));

            foreach (JournalEntry entry in NarrativeJournal.Entries(lab.World, lab.Player))
            {
                Assert.False(entry.CanProve, "hearing about it in the street made the player a witness");
            }
        }
    }
}
