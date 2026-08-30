using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-036: "what's been happening?" - the asked half of ambient talk. The step's own condition
    /// is that two people in one town answer differently because they were told different things,
    /// and the rest of these pin what keeps that honest: it hands out the town's gossip and not
    /// anybody's testimony, it teaches nothing until an answer has actually reached the player,
    /// and it neither spends nor is spent by the street's cooldown.
    /// </summary>
    public class TownNewsTests
    {
        /// <summary>Somebody standing where the player is, in the world model and alive.</summary>
        private static EntityId Local(TheftLaboratory lab, string name)
        {
            EntityId id = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(id, name));
            lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
            return id;
        }

        /// <summary>A claim nobody has any way of proving, of the kind towns repeat.</summary>
        private static EntityId Claim(TheftLaboratory lab, EntityId subject, string predicate, EntityId about, string value = null, int secrecy = 0)
        {
            EntityId id = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(id, subject, predicate, about, value, secrecy: secrecy));
            return id;
        }

        /// <summary>Teaches somebody a claim the way being told it teaches them.</summary>
        private static void Told(TheftLaboratory lab, EntityId who, EntityId factId, double confidence = 0.8)
        {
            lab.World.Knowledge.Teach(who, factId, KnowledgeSource.Hearsay, confidence, lab.Vanilla.Now, false, lab.Situation.WitnessId);
        }

        private static List<EntityId> Subjects(IReadOnlyList<SpokenRemark> answer)
        {
            List<EntityId> facts = new List<EntityId>();
            for (int i = 0; i < answer.Count; i++)
            {
                facts.Add(answer[i].FactId);
            }

            return facts;
        }

        /// <summary>
        /// The step's completion test. Two people in the same market, asked the same question at
        /// the same moment, say different things - not because either was scripted to, but because
        /// the town told them different things.
        /// </summary>
        [Fact]
        public void TwoPeopleInOneTownAnswerDifferentlyBecauseTheyKnowDifferentThings()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            EntityId tovar = Local(lab, "Tovar");

            EntityId debt = Claim(lab, lab.Situation.VictimId, FactPredicates.Owes, lab.Situation.ThiefId, "80 orens");
            Told(lab, hedda, lab.Situation.TheftFactId);
            Told(lab, tovar, debt);

            IReadOnlyList<SpokenRemark> fromHedda = lab.News.Ask(lab.World, lab.Vanilla, hedda);
            IReadOnlyList<SpokenRemark> fromTovar = lab.News.Ask(lab.World, lab.Vanilla, tovar);

            Assert.Equal(new List<EntityId> { lab.Situation.TheftFactId }, Subjects(fromHedda));
            Assert.Equal(new List<EntityId> { debt }, Subjects(fromTovar));
            Assert.NotEqual(fromHedda[0].Line, fromTovar[0].Line);

            // Standing in the same place, so the difference is what they hold and nothing else.
            Assert.Equal(lab.Vanilla.GetZoneOf(hedda), lab.Vanilla.GetZoneOf(tovar));
        }

        /// <summary>
        /// The same difference with nothing hand-fed: two things happen in one town, ordinary
        /// gossip carries them unevenly, and the townspeople end up holding different subsets of
        /// what has been going on. Nobody was assigned an answer - the answers are what the
        /// circulation left in each head.
        /// </summary>
        [Fact]
        public void AWeekOfGossipLeavesNeighboursWithDifferentAnswers()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            List<EntityId> town = new List<EntityId>();
            for (int i = 0; i < 12; i++)
            {
                town.Add(Local(lab, "Townsperson " + i));
            }

            // A second development, so the town has more than one thing to be unevenly informed
            // about. The victim saw it happen; everything after that is the gossip scheduler.
            EntityId sten = Local(lab, "Sten");
            EntityId debt = Claim(lab, sten, FactPredicates.Owes, lab.Situation.VictimId, "80 orens");
            lab.World.Knowledge.Teach(lab.Situation.VictimId, debt, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, false);

            lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            for (int day = 1; day <= 5; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            HashSet<string> distinctAnswers = new HashSet<string>();
            HashSet<EntityId> mentioned = new HashSet<EntityId>();
            for (int i = 0; i < town.Count; i++)
            {
                IReadOnlyList<SpokenRemark> answer = lab.News.Ask(lab.World, lab.Vanilla, town[i]);
                if (answer.Count == 0)
                {
                    continue;
                }

                List<EntityId> facts = Subjects(answer);
                distinctAnswers.Add(string.Join("|", facts));
                for (int f = 0; f < facts.Count; f++)
                {
                    mentioned.Add(facts[f]);
                }
            }

            Assert.True(distinctAnswers.Count > 1,
                "the whole town gave one answer: " + string.Join(" ;; ", distinctAnswers));
            Assert.Contains(lab.Situation.TheftFactId, mentioned);
            Assert.Contains(debt, mentioned);
        }

        /// <summary>
        /// Asking for the news does not get you testimony. The witness watched the theft and says
        /// nothing about it; that is what `question` is for, and it costs a check.
        /// </summary>
        [Fact]
        public void NobodyHandsOverWhatTheySawThemselves()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.True(lab.World.Knowledge.Knows(lab.Situation.WitnessId, lab.Situation.TheftFactId));
            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, lab.Situation.WitnessId));
        }

        /// <summary>
        /// The one person who never brings a matter up is the person it is about, however the
        /// player raises it. Somebody else holding the same secondhand claim will pass it on.
        /// </summary>
        [Fact]
        public void TheSubjectOfTheStoryDoesNotRelateItAsNews()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId debt = Claim(lab, lab.Situation.ThiefId, FactPredicates.Owes, lab.Situation.VictimId, "80 orens");
            Told(lab, lab.Situation.ThiefId, debt, 0.9);

            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, lab.Situation.ThiefId));

            EntityId hedda = Local(lab, "Hedda");
            Told(lab, hedda, debt, 0.9);
            Assert.Equal(debt, Assert.Single(lab.News.Ask(lab.World, lab.Vanilla, hedda)).FactId);
        }

        /// <summary>
        /// A person catching you up, not a briefing: the three they would raise first, in that
        /// order, and the rest left unsaid.
        /// </summary>
        [Fact]
        public void AtMostThreeDevelopmentsAndTheLoudestFirst()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");

            List<EntityId> byConfidence = new List<EntityId>();
            double[] confidences = { 0.9, 0.8, 0.7, 0.6, 0.45 };
            for (int i = 0; i < confidences.Length; i++)
            {
                EntityId claim = Claim(lab, Local(lab, "Debtor " + i), FactPredicates.Owes, lab.Situation.VictimId, (i + 1) * 10 + " orens");
                Told(lab, hedda, claim, confidences[i]);
                byConfidence.Add(claim);
            }

            IReadOnlyList<SpokenRemark> answer = lab.News.Ask(lab.World, lab.Vanilla, hedda);

            Assert.Equal(3, answer.Count);
            Assert.Equal(byConfidence.GetRange(0, 3), Subjects(answer));
        }

        /// <summary>
        /// Reading somebody's answer is not the same as their having given it. Only a line the
        /// presentation layer put in front of the player may teach them anything.
        /// </summary>
        [Fact]
        public void AskingTeachesThePlayerNothingUntilTheAnswerReachesThem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            Told(lab, hedda, lab.Situation.TheftFactId);

            Assert.NotEmpty(lab.News.Ask(lab.World, lab.Vanilla, hedda));
            Assert.NotEmpty(lab.News.Ask(lab.World, lab.Vanilla, hedda));
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));
            Assert.Empty(NarrativeJournal.Entries(lab.World, lab.Player));

            IReadOnlyList<SpokenRemark> answer = lab.News.Ask(lab.World, lab.Vanilla, hedda);
            Assert.True(lab.News.Deliver(lab.World, lab.Vanilla, answer[0], lab.Vanilla.Now));

            JournalEntry entry = Assert.Single(NarrativeJournal.Entries(lab.World, lab.Player));
            Assert.Equal(lab.Situation.TheftFactId, entry.FactId);
            Assert.Equal(JournalTag.Rumour, entry.Tag);
        }

        /// <summary>
        /// What the player is given is a lead, not a case: weaker than the person repeating it
        /// held it, and with nothing to show a guard.
        /// </summary>
        [Fact]
        public void WhatYouAreToldIsHearsayAndUnprovable()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            Told(lab, hedda, lab.Situation.TheftFactId, 0.9);

            lab.News.Deliver(lab.World, lab.Vanilla, lab.News.Ask(lab.World, lab.Vanilla, hedda)[0], lab.Vanilla.Now);

            Assert.True(lab.World.Knowledge.TryGetBelief(lab.Player, lab.Situation.TheftFactId, out KnowledgeRecord heard));
            Assert.Equal(KnowledgeSource.Hearsay, heard.Source);
            Assert.False(heard.CanProve);
            Assert.True(heard.Confidence < 0.9, "the player took it on as firmly as the person repeating it");
        }

        /// <summary>
        /// Being asked does not make somebody indiscreet. What a town is keeping quiet stays
        /// behind the verbs that cost something.
        /// </summary>
        [Fact]
        public void NothingIsRelatedAboutWhatIsBeingKeptQuiet()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            EntityId hidden = Claim(lab, lab.Situation.VictimId, FactPredicates.Extorted, lab.Situation.ThiefId, secrecy: 95);
            Told(lab, hedda, hidden, 0.9);

            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, hedda));
        }

        /// <summary>
        /// The topic empties itself. Asked twice, somebody relates what the player has picked up
        /// since and otherwise has nothing to add - which is also why hiding it is honest.
        /// </summary>
        [Fact]
        public void AskingAgainGetsWhatIsNewAndNothingElse()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            Told(lab, hedda, lab.Situation.TheftFactId);

            lab.News.Deliver(lab.World, lab.Vanilla, lab.News.Ask(lab.World, lab.Vanilla, hedda)[0], lab.Vanilla.Now);
            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, hedda));

            EntityId debt = Claim(lab, lab.Situation.VictimId, FactPredicates.Owes, lab.Situation.ThiefId, "80 orens");
            Told(lab, hedda, debt);
            Assert.Equal(debt, Assert.Single(lab.News.Ask(lab.World, lab.Vanilla, hedda)).FactId);
        }

        /// <summary>
        /// The two routes do not share a budget. A quiet street must not silence the person
        /// standing in front of the player, and a conversation must not silence the street.
        /// </summary>
        [Fact]
        public void AskingIsNeitherPacedByTheStreetNorPacesIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            EntityId tovar = Local(lab, "Tovar");
            EntityId debt = Claim(lab, lab.Situation.VictimId, FactPredicates.Owes, lab.Situation.ThiefId, "80 orens");
            Told(lab, hedda, lab.Situation.TheftFactId);
            Told(lab, tovar, debt);

            // Somebody says something in the street, which spends the ambient cooldown.
            lab.Ambient.Deliver(lab.World, lab.Vanilla, lab.Ambient.Next(lab.World, lab.Vanilla, lab.Vanilla.Now), lab.Vanilla.Now);
            long stamp = lab.World.LastAmbientRemarkMinute;
            Assert.Null(lab.Ambient.Next(lab.World, lab.Vanilla, lab.Vanilla.Now));

            IReadOnlyList<SpokenRemark> answer = lab.News.Ask(lab.World, lab.Vanilla, tovar);
            Assert.Equal(debt, Assert.Single(answer).FactId);
            Assert.True(lab.News.Deliver(lab.World, lab.Vanilla, answer[0], lab.Vanilla.Now));
            Assert.Equal(stamp, lab.World.LastAmbientRemarkMinute);
        }

        /// <summary>
        /// Nobody answers who is not there to answer, and the player does not tell themselves the
        /// news.
        /// </summary>
        [Fact]
        public void TheDeadAndThePlayerHaveNothingToSay()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId hedda = Local(lab, "Hedda");
            Told(lab, hedda, lab.Situation.TheftFactId);

            Told(lab, lab.Player, lab.Situation.TheftFactId);
            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, lab.Player));

            lab.Vanilla.Kill(hedda);
            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, hedda));
            Assert.Empty(lab.News.Ask(lab.World, lab.Vanilla, lab.World.NewId("npc")));
        }

        private static List<string> Lines(IReadOnlyList<SpokenRemark> answer)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < answer.Count; i++)
            {
                lines.Add(answer[i].Line);
            }

            return lines;
        }
    }
}
