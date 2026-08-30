using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// The second way word travels: membership rather than proximity.
    ///
    /// <see cref="RumorCirculation"/> moves a claim between people who are standing in the same
    /// place, which is how a town half-learns something and how it stops at the town's edge. A
    /// guild is the channel that does not stop there - the point of belonging to one is that a
    /// thing reported in one hall is known in the next (`PM §50`, `LW §6.5`). This runs on the
    /// same day boundary and inside the same round, because it is one more route by which the
    /// world's news moves and not a system of its own.
    ///
    /// Four rules make it a network rather than a broadcast.
    ///
    /// **It carries only what the network reads.** <see cref="GuildNetworks.Reads"/> decides, so
    /// the Fighters hear about the killing on the road, the Thieves about the cargo, and the Mages
    /// about neither. A member who happens to know something outside their guild's interest still
    /// knows it - they simply do not report it up, and it travels the ordinary way or not at all.
    ///
    /// **It draws no die.** Circulation rolls because a bystander overhearing something is
    /// chance; a member reporting to their guild is not. Deterministic also means this can be
    /// added to an existing round without moving a single downstream roll, which matters in a
    /// simulation where the world RNG is a shared stream.
    ///
    /// **It never touches the player.** Not as a speaker, because nobody carries word on their
    /// behalf, and not as a listener, because knowledge arriving in the player's head while they
    /// are elsewhere is the omniscient journal standing rule 22 forbids - a card in their pocket
    /// does not change that. A guild reaches the player through a contact who says something, and
    /// what a contact will say is <see cref="TalkRepertoire"/>'s.
    ///
    /// **Knowing is not telling.** No secrecy ceiling applies here, and that is the difference
    /// between a network and a rumour: the Thieves hearing that a ring is being fenced before the
    /// victim's family hears who has it is exactly the case `PM §50` names. Whether a member will
    /// then repeat it to the player is a separate question, and one the speech layer still answers
    /// with a secrecy ceiling of its own.
    ///
    /// Proof does not travel, for the same reason it does not travel in the street: what arrives
    /// is <see cref="RumorSystem.Tell"/>'s hearsay at the speaker's conviction, so a guild can be
    /// certain of something and still have nothing to show a guard.
    /// </summary>
    public sealed class GuildRouting
    {
        private readonly RumorSystem _rumors;

        public GuildRouting(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
        }

        /// <summary>Claims one network passes on in one day. The rest wait their turn.</summary>
        public int MaxFactsPerNetwork { get; set; } = 3;

        /// <summary>Members reached about one claim in one day.</summary>
        public int MaxListenersPerFact { get; set; } = 4;

        /// <summary>Hard ceiling on network retellings per day, across every guild.</summary>
        public int MaxTellsPerDay { get; set; } = 8;

        /// <summary>
        /// Confidence a member needs before reporting something up. Lower than the street's,
        /// because passing on a thing you are only fairly sure of is what a network is for -
        /// <see cref="RumorSystem.CanTell"/> is still the real floor under it.
        /// </summary>
        public double SpeakerFloor { get; set; } = 0.2;

        /// <summary>
        /// One day of routing through every guild, recorded on the round the caller is running.
        /// Returns the retellings that happened.
        /// </summary>
        public int Route(NarrativeWorldState world, IVanillaState vanilla, GameTime now, RumorRound round)
        {
            if (world == null || vanilla == null || round == null)
            {
                return 0;
            }

            int told = 0;
            for (int i = 0; i < GuildNetworks.All.Count && told < MaxTellsPerDay; i++)
            {
                told += RouteOne(world, vanilla, now, GuildNetworks.All[i], round, MaxTellsPerDay - told);
            }

            return told;
        }

        private int RouteOne(NarrativeWorldState world, IVanillaState vanilla, GameTime now, GuildId guild, RumorRound round, int budget)
        {
            List<EntityId> members = Members(world, vanilla, guild);
            if (members.Count < 2)
            {
                return 0;
            }

            List<EntityId> facts = Carried(world, vanilla, guild, members);
            int told = 0;

            for (int i = 0; i < facts.Count && told < budget; i++)
            {
                told += Pass(world, vanilla, now, guild, members, facts[i], round, budget - told);
            }

            return told;
        }

        /// <summary>
        /// One claim, offered to whoever in the network does not have it yet.
        ///
        /// Speakers are walked in id order so the same world routes the same way twice; the first
        /// member who can pass it on does, which is enough - who in a guild happened to say it is
        /// not something the simulation has any way to be right about.
        /// </summary>
        private int Pass(
            NarrativeWorldState world,
            IVanillaState vanilla,
            GameTime now,
            GuildId guild,
            List<EntityId> members,
            EntityId factId,
            RumorRound round,
            int budget)
        {
            Fact fact = world.Knowledge.GetFact(factId);
            if (fact == null)
            {
                return 0;
            }

            int told = 0;
            int reached = 0;

            for (int i = 0; i < members.Count && told < budget && reached < MaxListenersPerFact; i++)
            {
                EntityId speaker = members[i];
                if (!CanReport(world, vanilla, fact, speaker))
                {
                    continue;
                }

                for (int j = 0; j < members.Count && told < budget && reached < MaxListenersPerFact; j++)
                {
                    EntityId listener = members[j];
                    if (listener == speaker
                        || !vanilla.IsAlive(listener)
                        || world.Knowledge.Knows(listener, factId)
                        || !_rumors.Tell(speaker, listener, factId, now))
                    {
                        continue;
                    }

                    told++;
                    reached++;
                }
            }

            if (reached > 0)
            {
                round.Notes.Add(FactPhrasing.Claim(world.Registry, fact)
                                + " [" + factId + "] reached " + reached + " more in the "
                                + guild + " network.");
            }

            return told;
        }

        /// <summary>
        /// Members of this guild the simulation can speak for: in the world model, alive, and not
        /// the player.
        /// </summary>
        private static List<EntityId> Members(NarrativeWorldState world, IVanillaState vanilla, GuildId guild)
        {
            List<EntityId> members = new List<EntityId>();
            foreach (KeyValuePair<EntityId, NarrativeNpc> pair in world.Registry.Npcs)
            {
                if (pair.Key != vanilla.PlayerId
                    && GuildNetworks.BelongsTo(pair.Value, guild)
                    && vanilla.IsAlive(pair.Key))
                {
                    members.Add(pair.Key);
                }
            }

            members.Sort(CompareIds);
            return members;
        }

        /// <summary>
        /// The claims this network would pass on today.
        ///
        /// A claim every member already holds is not carried, which is what keeps the day's few
        /// slots from being held forever by the oldest thing the guild knows: once a matter has
        /// gone round it drops out and the next one moves up. Ordering among the rest is by id,
        /// because a dictionary's enumeration order is never something a save may depend on.
        /// </summary>
        private List<EntityId> Carried(NarrativeWorldState world, IVanillaState vanilla, GuildId guild, List<EntityId> members)
        {
            List<EntityId> carried = new List<EntityId>();
            for (int i = 0; i < members.Count; i++)
            {
                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(members[i]))
                {
                    if (carried.Contains(belief.FactId))
                    {
                        continue;
                    }

                    Fact fact = world.Knowledge.GetFact(belief.FactId);
                    if (GuildNetworks.Reads(world, guild, fact) != GuildFraming.None
                        && CanReport(world, vanilla, fact, members[i])
                        && SomebodyLacks(world, members, belief.FactId))
                    {
                        carried.Add(belief.FactId);
                    }
                }
            }

            carried.Sort(CompareIds);
            if (carried.Count > Math.Max(0, MaxFactsPerNetwork))
            {
                carried.RemoveRange(MaxFactsPerNetwork, carried.Count - MaxFactsPerNetwork);
            }

            return carried;
        }

        /// <summary>
        /// Somebody who would report this: alive, believing it firmly enough to pass on, and not
        /// the person it is about.
        ///
        /// The subject clause is <see cref="RumorCirculation"/>'s and is doing the same work here,
        /// harder: a guild is exactly where a member would least want their own name raised, and a
        /// network without the clause would have a thief filing a report on himself.
        /// </summary>
        private bool CanReport(NarrativeWorldState world, IVanillaState vanilla, Fact fact, EntityId member)
        {
            return fact != null
                   && member != fact.Subject
                   && vanilla.IsAlive(member)
                   && world.Knowledge.TryGetBelief(member, fact.Id, out KnowledgeRecord belief)
                   && belief.Confidence >= SpeakerFloor;
        }

        /// <summary>Whether anybody in the network still has this to learn.</summary>
        private static bool SomebodyLacks(NarrativeWorldState world, List<EntityId> members, EntityId factId)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (!world.Knowledge.Knows(members[i], factId))
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
