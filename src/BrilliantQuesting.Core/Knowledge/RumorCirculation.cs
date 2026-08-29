using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>What one call to the scheduler did. Exists so the log and the inspector can explain it.</summary>
    public sealed class RumorRound
    {
        /// <summary>Days that had passed since the last round, before any cap was applied.</summary>
        public long DaysOwed { get; set; }

        /// <summary>Days actually simulated. Lower than <see cref="DaysOwed"/> after a long absence.</summary>
        public long DaysRun { get; set; }

        /// <summary>Retellings that happened.</summary>
        public int Tells { get; set; }

        /// <summary>Hearsay beliefs that lost confidence because nobody repeated them.</summary>
        public int Faded { get; set; }

        /// <summary>Retellings that named the wrong person.</summary>
        public int Garbled { get; set; }

        /// <summary>One line per fact that actually moved. Bounded; this is a log, not a record.</summary>
        public List<string> Notes { get; } = new List<string>();

        public bool DidAnything => Tells > 0 || Faded > 0;
    }

    /// <summary>
    /// Runs gossip through a live town on a schedule, so a thing one person saw becomes a thing
    /// the town half-knows.
    ///
    /// `RumorSystem` has always known how to move a belief from one person to another and how to
    /// take a round through a crowd. Nothing called `Circulate`. The only transmission in a real
    /// game was one scripted `Tell` in the theft ladder, which meant a rumour existed exactly
    /// where a designer had written one - the opposite of what the subsystem is for.
    ///
    /// Three things had to be decided before it could be turned loose on real people:
    ///
    /// **It must be bounded.** A town has dozens of characters and a long save has hundreds of
    /// facts; the naive loop is quadratic and runs every day forever. So a round picks a handful
    /// of facts with the most news in them, talks to a handful of people about each, and stops.
    /// Missing a retelling costs nothing - there is always tomorrow.
    ///
    /// **It must not run on reload.** The day the round belongs to is recorded on the world and
    /// persisted, so loading the same save five times circulates once. Without that, a player who
    /// reloads is a player whose town gossips five times as fast, and rerolling a bad result would
    /// be a matter of pressing load.
    ///
    /// **It must leave the player alone.** The player is neither a speaker nor a listener here.
    /// Not a speaker because nobody agreed to spread anything on their behalf; not a listener
    /// because knowledge the player was never told, arriving silently in the background, is the
    /// omniscient journal that `LW §3.3` and standing rule 22 exist to prevent. The player hears
    /// things from people, in conversation. Their own beliefs are also exempt from fading, for the
    /// same reason a dialogue option must never quietly disappear (rule 11).
    ///
    /// Proof never travels. <see cref="RumorSystem.Tell"/> only passes evidence when a speaker
    /// deliberately shows it, and nothing here ever asks it to - which is what keeps a rumour
    /// widely believed and still useless in front of a guard.
    /// </summary>
    public sealed class RumorCirculation
    {
        private readonly RumorSystem _rumors;

        public RumorCirculation(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
        }

        /// <summary>
        /// How stories go wrong on the way. Settable so a test can hold it still, and null to
        /// circulate a town that never misremembers anything.
        /// </summary>
        public RumorDistortion Distortion { get; set; } = new RumorDistortion();

        /// <summary>Facts considered in one day. The rest of the world's gossip waits its turn.</summary>
        public int MaxFactsPerDay { get; set; } = 3;

        /// <summary>People approached about one fact in one day.</summary>
        public int MaxListenersPerFact { get; set; } = 6;

        /// <summary>Hard ceiling on retellings per call, whatever the schedule owes.</summary>
        public int MaxTellsPerDay { get; set; } = 12;

        /// <summary>
        /// Days simulated when catching up. A fortnight away should feel like a fortnight, but
        /// nobody should pay for a hundred rounds of gossip on a single load screen.
        /// </summary>
        public int MaxCatchUpDays { get; set; } = 7;

        /// <summary>Confidence a hearsay belief keeps per day when nobody repeats it.</summary>
        public double DailyDecay { get; set; } = 0.9;

        /// <summary>
        /// Confidence a faded rumour never drops below. Forgetting outright is BQ-021's job -
        /// this only takes a story from "I heard it" down to "there was something about that".
        /// </summary>
        public double FadedFloor { get; set; } = 0.05;

        /// <summary>Below this, a character believes it too weakly to bring it up unprompted.</summary>
        public double SpeakerFloor { get; set; } = 0.25;

        /// <summary>Base odds one bystander hears it from one speaker in one day.</summary>
        public double ChancePerListener { get; set; } = 0.35;

        /// <summary>
        /// Circulates whatever the calendar owes, and records that it did.
        ///
        /// Safe to call from anywhere and as often as you like: the world's own day counter
        /// decides whether there is anything to do.
        /// </summary>
        public RumorRound Run(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            RumorRound round = new RumorRound();
            if (world == null || vanilla == null)
            {
                return round;
            }

            long today = now.TotalDays;

            // First sight of this world. Record where we are and gossip from here; a save that
            // predates the scheduler does not owe eighty days of catch-up.
            if (world.LastRumorDay == NarrativeWorldState.RumorsNeverCirculated || today < world.LastRumorDay)
            {
                world.LastRumorDay = today;
                return round;
            }

            if (today == world.LastRumorDay)
            {
                return round;
            }

            round.DaysOwed = today - world.LastRumorDay;
            round.DaysRun = Math.Min(round.DaysOwed, Math.Max(0, MaxCatchUpDays));
            world.LastRumorDay = today;

            for (long day = 0; day < round.DaysRun; day++)
            {
                RunOneDay(world, vanilla, now, round);
            }

            return round;
        }

        private void RunOneDay(NarrativeWorldState world, IVanillaState vanilla, GameTime now, RumorRound round)
        {
            Fade(world, vanilla, now, round);

            List<EntityId> facts = SelectFacts(world, vanilla);
            for (int i = 0; i < facts.Count; i++)
            {
                if (round.Tells >= MaxTellsPerDay)
                {
                    return;
                }

                CirculateOne(world, vanilla, now, facts[i], round);
            }
        }

        /// <summary>
        /// A story nobody repeats gets weaker. Only hearsay fades: somebody who watched it happen
        /// still watched it happen, and a participant was there.
        /// </summary>
        private void Fade(NarrativeWorldState world, IVanillaState vanilla, GameTime now, RumorRound round)
        {
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                if (npc.Id == vanilla.PlayerId)
                {
                    continue;
                }

                foreach (KnowledgeRecord record in world.Knowledge.BeliefsOf(npc.Id))
                {
                    if (record.Source != KnowledgeSource.Hearsay
                        || record.Confidence <= FadedFloor
                        || now.DaysSince(record.LearnedAt) < 1)
                    {
                        continue;
                    }

                    double faded = record.Confidence * DailyDecay;
                    record.Confidence = faded < FadedFloor ? FadedFloor : faded;
                    round.Faded++;
                }
            }
        }

        /// <summary>
        /// The few facts with the most news in them, worst-first eliminated.
        ///
        /// Ordering is by score and then by id, because the fact store is a dictionary and
        /// enumeration order is not something a save may depend on. Two runs of the same save
        /// must pick the same facts or nothing about this is reproducible.
        /// </summary>
        private List<EntityId> SelectFacts(NarrativeWorldState world, IVanillaState vanilla)
        {
            List<Candidate> candidates = new List<Candidate>();

            foreach (KeyValuePair<EntityId, Fact> pair in world.Knowledge.Facts)
            {
                Fact fact = pair.Value;
                if (!FactPredicates.IsNewsworthy(fact.Predicate))
                {
                    continue;
                }

                int speakers = 0;

                foreach (EntityId knower in world.Knowledge.Knowers(fact.Id))
                {
                    if (CanSpeakOf(world, vanilla, fact, knower))
                    {
                        speakers++;
                    }
                }

                if (speakers == 0)
                {
                    continue;
                }

                // News value: people carrying it, a bonus for belonging to something still
                // happening, and a penalty for being the kind of thing people keep quiet about.
                double score = speakers
                               + (BelongsToLiveThread(world, fact.Id) ? 2.0 : 0.0)
                               - fact.Secrecy / 50.0;

                candidates.Add(new Candidate(fact.Id, score));
            }

            candidates.Sort(Candidate.Compare);

            List<EntityId> chosen = new List<EntityId>();
            int limit = Math.Min(Math.Max(0, MaxFactsPerDay), candidates.Count);
            for (int i = 0; i < limit; i++)
            {
                chosen.Add(candidates[i].FactId);
            }

            return chosen;
        }

        private void CirculateOne(NarrativeWorldState world, IVanillaState vanilla, GameTime now, EntityId factId, RumorRound round)
        {
            Fact fact = world.Knowledge.GetFact(factId);
            if (fact == null)
            {
                return;
            }

            // Something people are actively hiding travels, but slowly. At the secrecy the thief
            // reaches once they have stashed the evidence this is roughly a third of normal.
            double chance = ChancePerListener * (1.0 - Math.Min(100, Math.Max(0, fact.Secrecy)) / 125.0);

            List<EntityId> speakers = new List<EntityId>();
            foreach (EntityId knower in world.Knowledge.Knowers(factId))
            {
                if (CanSpeakOf(world, vanilla, fact, knower))
                {
                    speakers.Add(knower);
                }
            }

            speakers.Sort(CompareIds);

            int reached = 0;
            int garbled = 0;
            for (int i = 0; i < speakers.Count && reached < MaxListenersPerFact && round.Tells < MaxTellsPerDay; i++)
            {
                EntityId speaker = speakers[i];
                IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(vanilla.GetZoneOf(speaker));

                for (int j = 0; j < present.Count && reached < MaxListenersPerFact && round.Tells < MaxTellsPerDay; j++)
                {
                    EntityId listener = present[j];
                    if (!CanHear(world, vanilla, listener, speaker, factId))
                    {
                        continue;
                    }

                    if (!world.Rng.Chance(chance))
                    {
                        continue;
                    }

                    // What the speaker is left with after the chain decides both how convincing
                    // they are and whether they still have the story straight.
                    world.Knowledge.TryGetBelief(speaker, factId, out KnowledgeRecord held);
                    double transmitted = held.Confidence * _rumors.TransmissionDecay;
                    Fact said = Distortion == null
                        ? fact
                        : Distortion.Retell(world, vanilla, fact, speaker, listener, transmitted, world.Rng);

                    if (_rumors.Tell(speaker, listener, factId, now, saidAs: said.Id))
                    {
                        round.Tells++;
                        reached++;
                        if (said.Id != factId)
                        {
                            garbled++;
                        }
                    }
                }
            }

            if (reached > 0)
            {
                round.Garbled += garbled;
                round.Notes.Add(world.Registry.NameOf(fact.Subject) + " " + fact.Predicate
                                + " [" + factId + "] reached " + reached + " more "
                                + (reached == 1 ? "person" : "people")
                                + (garbled > 0 ? "; " + garbled + " of them heard it wrong." : "."));
            }
        }

        /// <summary>
        /// Someone who could bring it up: alive, in the world model, believing it strongly enough
        /// to repeat, not the player, and not the person the story is about.
        ///
        /// That last clause is doing real work. The thief knows about the theft better than
        /// anyone - he committed it - and his strongest goal is `avoid_exposure`, so a scheduler
        /// that treats every knower as a potential speaker has the culprit walking round the
        /// market telling people what he did. Subject-of-the-fact is a blunt stand-in for the
        /// disclosure decision BQ-071 will actually make from privacy, fear, loyalty and legal
        /// risk; it is right about every predicate in the ontology today, and it is a rule this
        /// step can defend rather than a model it cannot yet support.
        /// </summary>
        private bool CanSpeakOf(NarrativeWorldState world, IVanillaState vanilla, Fact fact, EntityId knower)
        {
            return knower != vanilla.PlayerId
                   && knower != fact.Subject
                   && world.Registry.GetNpc(knower) != null
                   && vanilla.IsAlive(knower)
                   && world.Knowledge.TryGetBelief(knower, fact.Id, out KnowledgeRecord belief)
                   && belief.Confidence >= SpeakerFloor;
        }

        private bool CanHear(NarrativeWorldState world, IVanillaState vanilla, EntityId listener, EntityId speaker, EntityId factId)
        {
            return listener != speaker
                   && listener != vanilla.PlayerId
                   && world.Registry.GetNpc(listener) != null
                   && vanilla.IsAlive(listener)
                   && !world.Knowledge.Knows(listener, factId);
        }

        private static bool BelongsToLiveThread(NarrativeWorldState world, EntityId factId)
        {
            for (int i = 0; i < world.Threads.Count; i++)
            {
                if (world.Threads[i].IsLive && world.Threads[i].FactIds.Contains(factId))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareIds(EntityId a, EntityId b)
        {
            return string.CompareOrdinal(a.Value, b.Value);
        }

        private struct Candidate
        {
            public readonly EntityId FactId;
            public readonly double Score;

            public Candidate(EntityId factId, double score)
            {
                FactId = factId;
                Score = score;
            }

            public static int Compare(Candidate a, Candidate b)
            {
                int byScore = b.Score.CompareTo(a.Score);
                return byScore != 0 ? byScore : CompareIds(a.FactId, b.FactId);
            }
        }
    }
}
