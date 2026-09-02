using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Relationships
{
    /// <summary>
    /// How well the player already knows one person, and what says so.
    ///
    /// A reading, never a fact about the world: nothing here is stored, and a person is not made
    /// more or less real by whether the player has met them. Zero is a stranger, which is an
    /// ordinary and permanent answer for most of a town.
    /// </summary>
    public sealed class FamiliarityReading
    {
        internal FamiliarityReading(EntityId actor, int score, string because)
        {
            Actor = actor;
            Score = score;
            Because = because ?? string.Empty;
        }

        public EntityId Actor { get; }

        /// <summary>0 for a stranger, up to <see cref="PlayerFamiliarity.Ceiling"/> for the household.</summary>
        public int Score { get; }

        /// <summary>Whether the player has any recorded dealings with them at all.</summary>
        public bool IsKnown => Score > 0;

        /// <summary>Inspector-only sentence naming the grounds. Empty for a stranger.</summary>
        public string Because { get; }

        public override string ToString() => Actor + " familiarity " + Score;
    }

    /// <summary>
    /// BQ-114. Who the player has actually met, traded with, or lives beside.
    ///
    /// The one place that answers that question, because the alternative is every archetype and
    /// every casting surface inventing its own idea of an acquaintance from a different half of the
    /// evidence. Four grounds, all of them history that already exists somewhere:
    ///
    /// - **Household.** Somebody living on the player's own land is the strongest tie the game has,
    ///   and it is the one Elin players already form (`SP §3`).
    /// - **Vanilla affinity.** Elin's own record of the player's dealings with a character, which
    ///   is the only ground that exists in a save the mod has just attached to: ordinary talking,
    ///   trading and gift-giving happen entirely in vanilla and leave no BQ event behind. Reading
    ///   the game's number rather than keeping a private acquaintance table is `D010`, and it is
    ///   also what keeps this step out of the save file.
    /// - **The event ledger.** What the player and this person have done to each other since the
    ///   mod attached, which is what makes a contact who arrived through play count.
    /// - **A recorded tie.** A relationship edge either way between the player and them.
    ///
    /// Every ground only ever raises the reading. An unreadable one contributes nothing rather than
    /// zero-as-a-fact (`D017`): a build that cannot read affinity produces strangers, not enemies.
    ///
    /// Deliberately not a gate. Familiarity decides who a situation is *about* when the world
    /// already supports several; it never decides whether there is a situation, never opens or
    /// closes a route, and is never read as affection - somebody the player wronged is not a
    /// stranger.
    /// </summary>
    public sealed class PlayerFamiliarity
    {
        /// <summary>Living on the player's own land.</summary>
        public const int HouseholdWeight = 18;

        /// <summary>A relationship the world model records between the player and them.</summary>
        public const int RecordedTieWeight = 10;

        /// <summary>Dealings the ledger holds: helped, given to, paid, returned to, owed.</summary>
        public const int DealingsWeight = 12;

        /// <summary>Anything else the two of them were both party to - talk, harm, a theft.</summary>
        public const int EncounterWeight = 6;

        /// <summary>Vanilla's own affinity, in points of reading per point of affinity.</summary>
        public const int AffinityPerPoint = 5;

        /// <summary>The most vanilla affinity alone can say. A long friendship is not four terms.</summary>
        public const int AffinityCeiling = 14;

        /// <summary>
        /// The most anybody can be known by. Held under BQ-125's family weighting on purpose:
        /// recognising somebody is a reason to cast them, not a reason to hurt them.
        /// </summary>
        public const int Ceiling = 35;

        private static readonly FamiliarityReading NobodyKnown =
            new FamiliarityReading(EntityId.None, 0, string.Empty);

        private readonly Dictionary<EntityId, FamiliarityReading> _readings =
            new Dictionary<EntityId, FamiliarityReading>();

        private PlayerFamiliarity()
        {
        }

        /// <summary>
        /// Reads the player's history once and answers about anybody afterwards.
        ///
        /// The ledger is walked a single time for the whole pass rather than per question, because
        /// the settlement generator asks about every ordered pair in a town and a scan per question
        /// made recognising a face cost more than generating the situation it belonged to.
        /// </summary>
        public static PlayerFamiliarity Read(World.NarrativeWorldState world, IVanillaState vanilla)
        {
            PlayerFamiliarity familiarity = new PlayerFamiliarity();
            if (world == null || vanilla == null || vanilla.PlayerId.IsNone)
            {
                return familiarity;
            }

            EntityId player = vanilla.PlayerId;
            Dictionary<EntityId, int> dealings = new Dictionary<EntityId, int>();
            Dictionary<EntityId, int> encounters = new Dictionary<EntityId, int>();
            foreach (WorldEvent past in world.Ledger.Involving(player))
            {
                EntityId other = Other(past, player);
                if (other.IsNone || other == player)
                {
                    continue;
                }

                Dictionary<EntityId, int> bucket = IsDealing(past.Type) ? dealings : encounters;
                bucket.TryGetValue(other, out int seen);
                bucket[other] = seen + 1;
            }

            HashSet<EntityId> household = new HashSet<EntityId>();
            HomeState home = vanilla.GetHomeState();
            if (home != null)
            {
                for (int i = 0; i < home.Residents.Count; i++)
                {
                    household.Add(home.Residents[i].Id);
                }
            }

            familiarity.Fill(world, vanilla, player, household, dealings, encounters);
            return familiarity;
        }

        /// <summary>What the player's history says about this person. Never null.</summary>
        public FamiliarityReading Of(EntityId actor)
        {
            if (actor.IsNone)
            {
                return NobodyKnown;
            }

            return _readings.TryGetValue(actor, out FamiliarityReading reading)
                ? reading
                : new FamiliarityReading(actor, 0, string.Empty);
        }

        /// <summary>
        /// How well they are known, without building a reading for the stranger they usually are.
        /// Asked once per comparison while a casting pool is ordered, which is why it does not go
        /// through <see cref="Of"/>.
        /// </summary>
        public int ScoreOf(EntityId actor) =>
            !actor.IsNone && _readings.TryGetValue(actor, out FamiliarityReading reading) ? reading.Score : 0;

        public bool Knows(EntityId actor) => ScoreOf(actor) > 0;

        private void Fill(
            World.NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId player,
            HashSet<EntityId> household,
            Dictionary<EntityId, int> dealings,
            Dictionary<EntityId, int> encounters)
        {
            HashSet<EntityId> people = new HashSet<EntityId>(household);
            foreach (EntityId dealt in dealings.Keys)
            {
                people.Add(dealt);
            }

            foreach (EntityId met in encounters.Keys)
            {
                people.Add(met);
            }

            IReadOnlyList<RelationshipEdge> outgoing = world.Relationships.EdgesOf(player);
            for (int i = 0; i < outgoing.Count; i++)
            {
                people.Add(outgoing[i].To);
            }

            foreach (RelationshipEdge incoming in world.Relationships.EdgesTo(player))
            {
                people.Add(incoming.From);
            }

            // Vanilla affinity is the one ground that exists for somebody the mod has never seen
            // do anything, so everybody the registry knows is asked for it rather than only the
            // people already named by BQ's own history.
            foreach (KeyValuePair<EntityId, World.NarrativeNpc> known in world.Registry.Npcs)
            {
                people.Add(known.Key);
            }

            foreach (EntityId person in people)
            {
                if (person.IsNone || person == player)
                {
                    continue;
                }

                FamiliarityReading reading = Assess(
                    world,
                    vanilla,
                    person,
                    household.Contains(person),
                    dealings.TryGetValue(person, out int dealt) ? dealt : 0,
                    encounters.TryGetValue(person, out int met) ? met : 0);

                if (reading.IsKnown)
                {
                    _readings[person] = reading;
                }
            }
        }

        private static FamiliarityReading Assess(
            World.NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId person,
            bool resident,
            int dealings,
            int encounters)
        {
            int score = 0;
            List<string> grounds = new List<string>();

            if (resident)
            {
                score += HouseholdWeight;
                grounds.Add("lives on the player's own land");
            }

            if (dealings > 0)
            {
                score += DealingsWeight;
                grounds.Add("has dealt with the player " + dealings + " time(s)");
            }

            if (encounters > 0)
            {
                score += EncounterWeight;
                grounds.Add("has crossed the player " + encounters + " time(s)");
            }

            RelationshipEdge tie = world.Relationships.Find(vanilla.PlayerId, person)
                                  ?? world.Relationships.Find(person, vanilla.PlayerId);
            if (tie != null)
            {
                score += RecordedTieWeight;
                grounds.Add("stands to the player as " + tie.Kind);
            }

            // D017: a build that cannot read affinity has no answer here, and no answer is not the
            // same as a stranger's zero - the other grounds still stand on their own.
            if (vanilla.Supports(VanillaCapability.ReadWriteAffinity))
            {
                int affinity = Math.Abs(vanilla.GetAffinity(person));
                int fromAffinity = Math.Min(AffinityCeiling, affinity / AffinityPerPoint);
                if (fromAffinity > 0)
                {
                    score += fromAffinity;
                    grounds.Add("the game records affinity " + vanilla.GetAffinity(person) + " toward the player");
                }
            }

            if (score <= 0)
            {
                return new FamiliarityReading(person, 0, string.Empty);
            }

            score = Math.Min(Ceiling, score);
            return new FamiliarityReading(
                person,
                score,
                world.Registry.NameOf(person) + " is known to the player: " + string.Join("; ", grounds));
        }

        /// <summary>Whichever party of an event is not the player, or nobody.</summary>
        private static EntityId Other(WorldEvent past, EntityId player)
        {
            if (past.Actor == player)
            {
                return past.Target;
            }

            return past.Target == player ? past.Actor : EntityId.None;
        }

        /// <summary>
        /// Dealings are the things a player and a person do *with* each other rather than near each
        /// other. A theft or a killing is still an encounter and still makes somebody memorable;
        /// it is simply not the same weight as a favour.
        /// </summary>
        private static bool IsDealing(WorldEventType type)
        {
            switch (type)
            {
                case WorldEventType.Helped:
                case WorldEventType.Rescued:
                case WorldEventType.ItemGiven:
                case WorldEventType.ItemReturned:
                case WorldEventType.DebtCreated:
                case WorldEventType.DebtPaid:
                case WorldEventType.FavorOwed:
                case WorldEventType.FavorRedeemed:
                case WorldEventType.PromiseMade:
                    return true;
                default:
                    return false;
            }
        }
    }
}
