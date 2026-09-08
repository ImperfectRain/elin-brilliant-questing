using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
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
    /// What anybody is willing to say is <see cref="TalkRepertoire"/>'s, and is shared with the
    /// asked route. Two further properties are this route's own, and they are what make it safe to
    /// let knowledge arrive by presence rather than by choice.
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
    /// </summary>
    public sealed class AmbientTalk
    {
        private readonly RumorSystem _rumors;
        private readonly TalkRepertoire _repertoire;

        public AmbientTalk(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
            _repertoire = new TalkRepertoire(_rumors);
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
        public SpokenRemark Next(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            return Next(world, vanilla, now, out _);
        }

        public SpokenRemark Next(NarrativeWorldState world, IVanillaState vanilla, GameTime now, out string reason)
        {
            return Next(world, vanilla, now, out reason, out _);
        }

        public SpokenRemark Next(NarrativeWorldState world, IVanillaState vanilla, GameTime now,
            out string reason, out List<DevelopmentScore> scores)
        {
            var readings = new List<DevelopmentScore>();
            scores = readings;
            reason = "nobody here has eligible hearsay the player has not heard";
            if (world == null || vanilla == null)
            {
                return null;
            }

            EntityId player = vanilla.PlayerId;
            if (player.IsNone || !vanilla.IsAlive(player))
            {
                return null;
            }

            AttentionSnapshot attention = world.AttentionBudget.Read(world, player, now, MinutesBetweenRemarks);
            if (attention.CoolingDown)
            {
                reason = "unsolicited exposure cooldown";
                return null;
            }

            EntityId zone = vanilla.GetZoneOf(player);
            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zone);
            if (present == null || present.Count == 0)
            {
                return null;
            }

            List<EntityId> speakers = new List<EntityId>(present);
            speakers.Sort(CompareIds);

            TalkRules rules = new TalkRules(SpeakerFloor, SecrecyCeiling);
            SpokenRemark best = null;
            string budgetRefusal = null;
            for (int i = 0; i < speakers.Count; i++)
            {
                // Filter before wording and before taking the best line. A blocked new matter
                // must neither starve an eligible update nor make us realize a whole repertoire.
                var byFact = new Dictionary<EntityId, DevelopmentScore>();
                List<SpokenRemark> said = _repertoire.Of(world, vanilla, speakers[i], player, rules, 1,
                    (fact, salience) =>
                    {
                        string refusal = attention.RemarkRefusal(world, fact, salience);
                        DevelopmentScore score = DevelopmentScoring.Read(world, vanilla, speakers[i], fact, salience, now);
                        score.Refusal = refusal;
                        readings.Add(score);
                        byFact[fact] = score;
                        if (refusal != null) budgetRefusal = refusal;
                        return refusal == null;
                    }, (fact, salience) => byFact[fact].Total);
                if (said.Count > 0 && TalkRepertoire.Beats(said[0], best))
                {
                    best = said[0];
                }
            }

            reason = best != null ? null : budgetRefusal ?? reason;
            return best;
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
        public bool Deliver(NarrativeWorldState world, IVanillaState vanilla, SpokenRemark remark, GameTime now)
        {
            if (world == null || vanilla == null || remark == null)
            {
                return false;
            }

            world.LastAmbientRemarkMinute = now.TotalMinutes;
            return _rumors.Tell(remark.Speaker, vanilla.PlayerId, remark.FactId, now);
        }

        private static int CompareIds(EntityId a, EntityId b)
        {
            return string.CompareOrdinal(a.Value, b.Value);
        }
    }
}
