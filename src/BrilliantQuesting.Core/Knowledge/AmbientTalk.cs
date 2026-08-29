using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// One thing somebody near the player is about to say out loud, and what the player is left
    /// believing if they do.
    ///
    /// It carries no tag, no confidence bar and no thread name, because it is a line of speech
    /// rather than a report. What the player makes of it is what the journal is for.
    /// </summary>
    public sealed class AmbientRemark
    {
        internal AmbientRemark(EntityId speaker, string speakerName, EntityId factId, string line)
        {
            Speaker = speaker;
            SpeakerName = speakerName;
            FactId = factId;
            Line = line;
        }

        /// <summary>Who says it. Never the player, and never the person the claim is about.</summary>
        public EntityId Speaker { get; }

        /// <summary>Their name, so a presentation layer that has lost the binding can still attribute it.</summary>
        public string SpeakerName { get; }

        /// <summary>What they are talking about. The player does not hold it yet.</summary>
        public EntityId FactId { get; }

        /// <summary>The words, hedged to match how sure the speaker actually is.</summary>
        public string Line { get; }
    }

    /// <summary>
    /// How a situation reaches a player who was never told about it: somebody standing near them
    /// mentions it.
    ///
    /// <see cref="RumorCirculation"/> deliberately leaves the player out of gossip in both
    /// directions - nobody spreads anything on their behalf, and nothing arrives in their head
    /// while they are elsewhere. That is the right rule and it leaves a hole: a town can end up
    /// half-knowing about a theft the player never hears a word of, and the only way in was a
    /// dialogue menu or a notification announcing that a situation exists. This is the way in that
    /// the design actually asks for (`PM §36`, `LW §3.2`, `CD §44`): the world says it, in
    /// somebody's voice, and the player picks it up by being there.
    ///
    /// Three properties make it safe to let the player learn things this way.
    ///
    /// **Nothing is learned that was not heard.** The pick and the telling are separate calls.
    /// <see cref="Next"/> reads the world and returns words; <see cref="Deliver"/> is what teaches
    /// the player, and the caller only makes it once the line is actually in front of them. A
    /// belief that arrived because a bark failed to render is precisely the omniscient journal
    /// that standing rule 22 forbids.
    ///
    /// **It never draws a die.** Circulation runs on a day boundary and can afford the world's
    /// RNG; this runs whenever the player acts, which is not a schedule any save can reproduce.
    /// Drawing here would make every downstream roll in the game depend on how many steps the
    /// player took, and reloading would quietly hand them a different world. So who speaks and
    /// what they say is a deterministic read of who is standing where, and the pacing comes from a
    /// cooldown on the world clock rather than from chance.
    ///
    /// **It carries gossip, not testimony.** A speaker may only mention something they were told
    /// - never something they saw, did or read. That is the line between this and the verbs the
    /// player chooses: questioning a witness and listening in on a private conversation both reach
    /// first-hand knowledge, and both cost a check and some social exposure to get it. What falls
    /// out of walking past is what the town has been repeating, which is also why this step needs
    /// `BQ-019` under it - until gossip actually circulates, an honest ambient layer has nothing to
    /// say. The witness who watched the theft stays quiet about it in the street; the neighbour who
    /// heard about it secondhand is the one who mentions it.
    ///
    /// The rest is circulation's rule: they must believe it firmly enough to repeat, and they are
    /// never the person the story is about - the thief does not mention his own theft to a passing
    /// stranger. What the player ends up with is hearsay at the speaker's own conviction, minus the
    /// usual cost of a retelling, with no proof attached, which is what makes it a lead rather than
    /// a case.
    /// </summary>
    public sealed class AmbientTalk
    {
        private readonly RumorSystem _rumors;

        public AmbientTalk(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
        }

        /// <summary>
        /// In-game minutes between remarks. Standing in a busy market should feel like a place
        /// where people talk, not like a feed.
        /// </summary>
        public int MinutesBetweenRemarks { get; set; } = 90;

        /// <summary>
        /// Confidence a speaker needs before mentioning something unprompted to somebody they may
        /// never have met. Deliberately above <see cref="RumorCirculation.SpeakerFloor"/>: the
        /// half-remembered end of what a town believes is reachable by asking or by listening in,
        /// which are things the player chooses to do.
        /// </summary>
        public double SpeakerFloor { get; set; } = 0.3;

        /// <summary>
        /// Above this secrecy, nobody says it where a stranger can hear. It is still reachable -
        /// eavesdropping and questioning both go higher - but it is not something that falls out
        /// of walking past.
        /// </summary>
        public int SecrecyCeiling { get; set; } = 60;

        /// <summary>
        /// The remark somebody here would make right now, or null.
        ///
        /// Pure: it reads the world and touches nothing. Calling it twice with the clock unmoved
        /// returns the same remark, and a caller that decides not to render it has cost the player
        /// nothing.
        /// </summary>
        public AmbientRemark Next(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            if (world == null || vanilla == null || !IsDue(world, now))
            {
                return null;
            }

            EntityId player = vanilla.PlayerId;
            if (player.IsNone || !vanilla.IsAlive(player))
            {
                return null;
            }

            EntityId zone = vanilla.GetZoneOf(player);
            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zone);
            if (present == null || present.Count == 0)
            {
                return null;
            }

            List<EntityId> speakers = new List<EntityId>();
            for (int i = 0; i < present.Count; i++)
            {
                if (CanSpeakHere(world, vanilla, present[i], player))
                {
                    speakers.Add(present[i]);
                }
            }

            speakers.Sort(CompareIds);

            EntityId bestSpeaker = EntityId.None;
            Fact bestFact = null;
            KnowledgeRecord bestBelief = null;
            double bestScore = double.NegativeInfinity;

            for (int i = 0; i < speakers.Count; i++)
            {
                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(speakers[i]))
                {
                    Fact fact = world.Knowledge.GetFact(belief.FactId);
                    if (!IsWorthMentioning(world, speakers[i], player, fact, belief))
                    {
                        continue;
                    }

                    double score = Score(world, fact, belief);
                    if (bestFact != null && !Beats(score, bestScore, fact.Id, bestFact.Id))
                    {
                        continue;
                    }

                    bestScore = score;
                    bestSpeaker = speakers[i];
                    bestFact = fact;
                    bestBelief = belief;
                }
            }

            return bestFact == null
                ? null
                : new AmbientRemark(
                    bestSpeaker,
                    world.Registry.NameOf(bestSpeaker),
                    bestFact.Id,
                    Words(world, bestFact, bestBelief));
        }

        /// <summary>
        /// The remark happened. Teaches the player what they just heard and starts the cooldown.
        ///
        /// The cooldown starts whether or not the belief took, because the beat was spent either
        /// way: a person said a thing out loud, and the player has heard as much of it as they are
        /// going to. Returns whether the player is left believing it - <see cref="RumorSystem"/>
        /// refuses a claim somebody is in a position to know is wrong, and being told a garbled
        /// story about yourself is exactly that case.
        /// </summary>
        public bool Deliver(NarrativeWorldState world, IVanillaState vanilla, AmbientRemark remark, GameTime now)
        {
            if (world == null || vanilla == null || remark == null)
            {
                return false;
            }

            world.LastAmbientRemarkMinute = now.TotalMinutes;
            return _rumors.Tell(remark.Speaker, vanilla.PlayerId, remark.FactId, now);
        }

        /// <summary>
        /// Whether enough of the clock has passed.
        ///
        /// A clock reading earlier than the stamp counts as due rather than as a very long wait.
        /// Nothing writes here - the stamp is corrected by the next remark that actually happens -
        /// because a read that quietly repaired the world would make <see cref="Next"/> something
        /// a caller has to be careful about calling.
        /// </summary>
        private bool IsDue(NarrativeWorldState world, GameTime now)
        {
            long last = world.LastAmbientRemarkMinute;
            return last == NarrativeWorldState.NothingSaidYet
                   || now.TotalMinutes < last
                   || now.TotalMinutes - last >= Math.Max(0, MinutesBetweenRemarks);
        }

        /// <summary>
        /// Somebody in the room who could mention something: alive, known to the simulation, and
        /// not the player.
        ///
        /// No mutation policy is consulted, and none applies. Speech is the one thing every class
        /// of actor permits by construction (<see cref="MutationKind.Speech"/> is the bottom rung),
        /// because putting a line in somebody's mouth changes nothing about them - it does not move
        /// their affinity, their inventory or where they stand. What it changes is what the player
        /// knows, and that is guarded here rather than there.
        /// </summary>
        private static bool CanSpeakHere(NarrativeWorldState world, IVanillaState vanilla, EntityId candidate, EntityId player)
        {
            return candidate != player
                   && world.Registry.GetNpc(candidate) != null
                   && vanilla.IsAlive(candidate);
        }

        /// <summary>
        /// Whether this belief is something that speaker would mention to this player, here.
        ///
        /// The subject clause is <see cref="RumorCirculation"/>'s and is doing the same work: the
        /// one person who can never be relied on to bring a matter up is the person it is about.
        /// The hearsay clause is what separates an idle remark from the routes the player chooses:
        /// first-hand knowledge is testimony, and testimony is asked for. The rest is ordinary - it
        /// has to be news, they have to half-believe it, it must not be something being actively
        /// kept quiet, and it must be something the player does not already hold.
        /// </summary>
        private bool IsWorthMentioning(
            NarrativeWorldState world,
            EntityId speaker,
            EntityId player,
            Fact fact,
            KnowledgeRecord belief)
        {
            return fact != null
                   && belief.Source == KnowledgeSource.Hearsay
                   && fact.Subject != speaker
                   && fact.Truth != TruthState.Superseded
                   && FactPredicates.IsNewsworthy(fact.Predicate)
                   && fact.Secrecy <= SecrecyCeiling
                   && belief.Confidence >= SpeakerFloor
                   && !world.Knowledge.Knows(player, fact.Id)
                   && _rumors.CanTell(speaker, player, fact.Id);
        }

        /// <summary>
        /// What people bring up first: how sure they are, whether it is part of something still
        /// going on, and how far it is from the sort of thing said in the open.
        /// </summary>
        private double Score(NarrativeWorldState world, Fact fact, KnowledgeRecord belief)
        {
            return belief.Confidence
                   + (BelongsToLiveThread(world, fact.Id) ? 1.0 : 0.0)
                   - fact.Secrecy / 100.0;
        }

        /// <summary>
        /// Ties break on fact id, so the same world always produces the same remark. Enumeration
        /// order over a dictionary is not something a save may depend on.
        /// </summary>
        private static bool Beats(double score, double bestScore, EntityId factId, EntityId bestFactId)
        {
            return score > bestScore
                   || (score == bestScore && string.CompareOrdinal(factId.Value, bestFactId.Value) < 0);
        }

        /// <summary>
        /// The claim, hedged the way a person hedges rather than scored the way a database scores.
        ///
        /// The speaker's confidence is in here and it is not printed. Somebody near the start of
        /// the chain repeats it as settled; somebody at the end of a long one says the thing people
        /// say when they are passing on something they only half have. That wording is the player's
        /// only handle on how good the lead is, which is the point of `LW §3.1`.
        /// </summary>
        private static string Words(NarrativeWorldState world, Fact fact, KnowledgeRecord belief)
        {
            string claim = FactPhrasing.Claim(world.Registry, fact);

            if (belief.Confidence >= 0.7)
            {
                return "Everyone knows it by now: " + claim + ".";
            }

            if (belief.Confidence >= 0.5)
            {
                return "Word going round is " + claim + ".";
            }

            return "Somebody was saying " + claim + ". Make of that what you like.";
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
    }
}
