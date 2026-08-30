using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// What a guild makes of a claim: the same robbery is a bounty to one network and stock to
    /// another.
    ///
    /// A reading is not a second fact and never contradicts the first. It is the professional
    /// interest a network takes in something that happened, which is why one fact can carry
    /// several - and why <see cref="None"/> is the ordinary answer rather than an error.
    /// </summary>
    public enum GuildFraming
    {
        /// <summary>Not this network's business. Nothing is routed and nothing is framed.</summary>
        None,

        /// <summary>Violence done or threatened: somebody will be asked to put a stop to it.</summary>
        Bounty,

        /// <summary>Goods that have moved outside the law, and will need moving again.</summary>
        Fence,

        /// <summary>A hole in trade - a shortage, a debt, a route closed - that pays to cover.</summary>
        Contract,

        /// <summary>Something somebody would rather was not known, and is worth holding.</summary>
        Leverage,

        /// <summary>Ground or a matter that is not ordinary, and wants looking at.</summary>
        Anomaly
    }

    /// <summary>
    /// Guilds as information networks: who carries what, and what they make of it.
    ///
    /// Elin's four guilds are already in the game and the mod does not replace them
    /// (`MD §13.5`, `PM §9`). What it adds is the thing a guild is for and the game has no place
    /// to put: a channel that carries word between people who have never stood in the same room,
    /// and a professional reading of what that word means.
    ///
    /// Two rules keep this from becoming four quest pools wearing a network's clothes.
    ///
    /// **A network carries what it is interested in, not what the town is talking about.** The
    /// interest table below is keyed on the predicate ontology rather than on any situation, so a
    /// caravan robbery is not a scripted event with four authored guild views - it is a killing, a
    /// theft and a shortage, and each network picks up the half of it that is its own. That is the
    /// whole of why the same event reaches Fighters and Thieves differently, and reaches the Mages
    /// not at all. It is deliberately not <see cref="FactPredicates.IsNewsworthy"/>: gossip is
    /// what people repeat, and a network also carries standing arrangements nobody would bother
    /// mentioning in a tavern.
    ///
    /// **Access gates the reading, never the claim.** Anybody can hear that a shipment was taken,
    /// because that is a thing people say. What membership buys is the contact who tells you what
    /// it means - which is `LW §3.4`'s information-as-progression, and stays on the right side of
    /// decision D012: no route closes for a non-member, they simply hear a rumour where a member
    /// hears their guild's reading of it.
    ///
    /// Membership is a role on <see cref="NarrativeNpc.Roles"/>, grantable by anybody - a
    /// situation, a generator, or an adapter that can tell which guild a live character staffs.
    /// The player's own membership is vanilla's, read through <see cref="IVanillaState"/>, because
    /// the game already owns that answer.
    /// </summary>
    public static class GuildNetworks
    {
        /// <summary>Role names, granted exactly like authority and underworld roles.</summary>
        public const string FightersRole = "guild_fighters";
        public const string MagesRole = "guild_mages";
        public const string ThievesRole = "guild_thieves";
        public const string MerchantsRole = "guild_merchants";

        /// <summary>
        /// The networks, in a fixed order. Iterated rather than enumerated over
        /// <see cref="GuildId"/> so that <see cref="GuildId.None"/> can never become a channel and
        /// so routing order is a property of this file rather than of an enum's declaration.
        /// </summary>
        public static IReadOnlyList<GuildId> All { get; } =
            new List<GuildId> { GuildId.Fighters, GuildId.Mages, GuildId.Thieves, GuildId.Merchants };

        /// <summary>Every role this file owns, so a refresh knows what it may withdraw.</summary>
        public static IReadOnlyList<string> MembershipRoles { get; } =
            new List<string> { FightersRole, MagesRole, ThievesRole, MerchantsRole };

        public static string MembershipRole(GuildId guild)
        {
            switch (guild)
            {
                case GuildId.Fighters: return FightersRole;
                case GuildId.Mages: return MagesRole;
                case GuildId.Thieves: return ThievesRole;
                case GuildId.Merchants: return MerchantsRole;
                default: return null;
            }
        }

        /// <summary>Whether this character is in the guild, as the simulation has it.</summary>
        public static bool BelongsTo(NarrativeNpc npc, GuildId guild)
        {
            string role = MembershipRole(guild);
            return npc != null && role != null && npc.Roles.Contains(role);
        }

        /// <summary>
        /// Whether this listener is inside the network at all - a member NPC, or the player when
        /// the game says they carry the card.
        ///
        /// The player's half is vanilla's answer and not the simulation's: guild membership is a
        /// faction relation Elin already keeps, and a second copy of it in the world model would
        /// be one that drifts.
        /// </summary>
        public static bool Reaches(NarrativeWorldState world, IVanillaState vanilla, EntityId listener, GuildId guild)
        {
            if (world == null || vanilla == null || listener.IsNone)
            {
                return false;
            }

            return listener == vanilla.PlayerId
                ? vanilla.IsGuildMember(guild)
                : BelongsTo(world.Registry.GetNpc(listener), guild);
        }

        /// <summary>
        /// The networks both of these people are inside, in <see cref="All"/> order.
        ///
        /// Computed once per conversation rather than per claim: a speaker's membership does not
        /// change between two sentences, and the ambient route asks this of everybody standing in
        /// the zone every time the player acts.
        /// </summary>
        public static List<GuildId> Shared(NarrativeWorldState world, IVanillaState vanilla, EntityId speaker, EntityId listener)
        {
            List<GuildId> shared = new List<GuildId>();
            if (world == null || vanilla == null)
            {
                return shared;
            }

            NarrativeNpc speakerNpc = world.Registry.GetNpc(speaker);
            if (speakerNpc == null || speakerNpc.Roles.Count == 0)
            {
                return shared;
            }

            for (int i = 0; i < All.Count; i++)
            {
                if (BelongsTo(speakerNpc, All[i]) && Reaches(world, vanilla, listener, All[i]))
                {
                    shared.Add(All[i]);
                }
            }

            return shared;
        }

        /// <summary>
        /// The first reading any of these networks has of the claim, or
        /// <see cref="GuildFraming.None"/>.
        ///
        /// First rather than best because the order is <see cref="All"/>'s and is stable: somebody
        /// who is both a fighter and a fence gives one answer, the same one every time, rather
        /// than an answer that depends on which role was granted first.
        /// </summary>
        public static GuildFraming FirstReading(NarrativeWorldState world, IReadOnlyList<GuildId> networks, Fact fact, out GuildId network)
        {
            network = GuildId.None;
            if (networks == null)
            {
                return GuildFraming.None;
            }

            for (int i = 0; i < networks.Count; i++)
            {
                GuildFraming framing = Reads(world, networks[i], fact);
                if (framing != GuildFraming.None)
                {
                    network = networks[i];
                    return framing;
                }
            }

            return GuildFraming.None;
        }

        /// <summary>
        /// What this guild's network makes of this claim, and whether it carries it at all.
        ///
        /// The table is the design's own division of labour (`PM §9`, `MD §13.5`) expressed over
        /// the predicate ontology:
        ///
        /// - **Fighters** take an interest in force. Somebody killed, how they were killed, and
        ///   somebody who is not safe from a person or a thing - which is what makes a robbery on
        ///   the road their business and a pickpocketing in the market not.
        /// - **Thieves** take an interest in property that has moved outside the law, and in what
        ///   people would rather stayed quiet.
        /// - **Merchants** take an interest in the hole rather than the crime: a town short of
        ///   something, a debt, a thing broken, a route closed. A shipment being robbed reaches
        ///   them as the shortage it causes, which is the chain `PM §52` describes.
        /// - **Mages** take an interest in what is not ordinary. The ontology has exactly one such
        ///   claim today (<see cref="FactPredicates.SacredTo"/>, which is also how a blight or a
        ///   barren herd is stated), so the Mages are the network that most often carries nothing
        ///   - and a caravan robbery is precisely a thing they never hear about.
        ///
        /// A superseded claim is carried by nobody: the network would be passing on a version of
        /// events the world has already replaced.
        /// </summary>
        public static GuildFraming Reads(NarrativeWorldState world, GuildId guild, Fact fact)
        {
            if (fact == null || fact.Truth == TruthState.Superseded)
            {
                return GuildFraming.None;
            }

            switch (guild)
            {
                case GuildId.Fighters:
                    switch (fact.Predicate)
                    {
                        case FactPredicates.Killed:
                        case FactPredicates.KilledBy:
                        case FactPredicates.AtRisk:
                            return GuildFraming.Bounty;
                    }

                    return GuildFraming.None;

                case GuildId.Thieves:
                    switch (fact.Predicate)
                    {
                        case FactPredicates.Stole:
                            return GuildFraming.Fence;

                        // Where a thing ended up is stock; where a person ended up is somebody's
                        // whereabouts, and this network has no more claim on that than any other.
                        case FactPredicates.LocatedAt:
                            return IsCharacter(world, fact.Subject) ? GuildFraming.None : GuildFraming.Fence;

                        case FactPredicates.Extorted:
                        case FactPredicates.Forged:
                            return GuildFraming.Leverage;
                    }

                    return GuildFraming.None;

                case GuildId.Merchants:
                    switch (fact.Predicate)
                    {
                        case FactPredicates.Needs:
                        case FactPredicates.Owes:
                        case FactPredicates.Damaged:
                        case FactPredicates.BlocksAccessTo:
                            return GuildFraming.Contract;
                    }

                    return GuildFraming.None;

                case GuildId.Mages:
                    switch (fact.Predicate)
                    {
                        case FactPredicates.SacredTo:
                            return GuildFraming.Anomaly;
                    }

                    return GuildFraming.None;

                default:
                    return GuildFraming.None;
            }
        }

        /// <summary>
        /// The sentence a member adds after the claim itself.
        ///
        /// Deliberately about what the network makes of it and never about what it pays: guild
        /// reward is vanilla's contribution and rank (`PM §9`: no parallel guild currencies), and
        /// a line that promised coin would be inventing one in words.
        /// </summary>
        public static string Reading(GuildFraming framing)
        {
            switch (framing)
            {
                case GuildFraming.Bounty:
                    return "That one is guild business - somebody will be asked to put a stop to it.";

                case GuildFraming.Fence:
                    return "Goods like that need a buyer who does not ask where they came from.";

                case GuildFraming.Contract:
                    return "Somebody is going to have to cover that, and be paid for covering it.";

                case GuildFraming.Leverage:
                    return "A thing like that is worth more held than told.";

                case GuildFraming.Anomaly:
                    return "That is not ordinary ground, and the guild will want it looked at.";

                default:
                    return null;
            }
        }

        private static bool IsCharacter(NarrativeWorldState world, EntityId id)
        {
            return world != null && world.Registry.GetNpc(id) != null;
        }
    }
}
