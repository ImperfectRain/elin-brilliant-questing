using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-084. CD §16's claim is one sentence: theft during a funeral is not socially equivalent to
    /// theft from an unattended warehouse. These prove that the difference exists, that it is
    /// derived from where the act happened rather than declared by anybody, that it never invents a
    /// consequence out of context alone, and that a place with nothing going on in it leaves every
    /// consequence exactly where it was.
    /// </summary>
    public class SocialPracticeTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Hall = EntityId.Parse("zone_hall");
        private static readonly EntityId Warehouse = EntityId.Parse("zone_warehouse");

        // -- A. the done-when ----------------------------------------------------------------

        /// <summary>
        /// The done-when, with everything but the place held identical: the same thief, the same
        /// theft, the same magnitude, the same bystander with no tie to the victim and no stake of
        /// their own. In the room where somebody is being mourned that bystander minds; in the
        /// unattended store room they have no reason to and do not.
        /// </summary>
        [Fact]
        public void TheSameTheftIsTakenHarderWhereSomebodyIsMournedThanInAnUnattendedStoreRoom()
        {
            Room wake = Room.Mourning();
            Room storeRoom = Room.Plain();

            int atTheWake = wake.WitnessedTheft();
            int inTheStoreRoom = storeRoom.WitnessedTheft();

            Assert.True(inTheStoreRoom == 0, "the plain room moved a bystander who has no reason to mind: " + inTheStoreRoom);
            Assert.True(atTheWake < 0, "nobody at the wake minded: " + string.Join("\n", wake.Consequences.Trace));
        }

        /// <summary>
        /// And the room keeps the difference, not only feels it in the moment. Both bystanders
        /// remember the same event; only one of them holds it against the person who did it, and
        /// that is the record everything later reads.
        /// </summary>
        [Fact]
        public void TheRoomThatHoldsANormKeepsAMarkAgainstWhoeverBrokeIt()
        {
            Room wake = Room.Mourning();
            Room storeRoom = Room.Plain();

            wake.WitnessedTheft();
            storeRoom.WitnessedTheft();

            Assert.Equal(WorldEventType.Theft, wake.WitnessMemory().EventType);
            Assert.Equal(WorldEventType.Theft, storeRoom.WitnessMemory().EventType);
            Assert.True(wake.WitnessMemory().AffinityContribution < 0,
                "the wake left no mark: " + string.Join("\n", wake.Consequences.Trace));
            Assert.Equal(0, storeRoom.WitnessMemory().AffinityContribution);
        }

        /// <summary>
        /// The literal unattended warehouse: nobody is there, so the theft is history and nothing
        /// else. The contrast the design draws is between two rooms with people in them; this is
        /// the floor underneath both, and a practice must not reach past an absent audience.
        /// </summary>
        [Fact]
        public void AnUnattendedPlaceProducesNoSocialResponseAtAll()
        {
            Room empty = Room.Plain();
            empty.RecordTheft(new EntityId[0]);

            Assert.Empty(empty.World.Memories.MemoriesAbout(empty.Bystander, Player));
            Assert.True(SocialPractices
                .Read(empty.World, empty.Vanilla, Warehouse, empty.Vanilla.Now)
                .IsOrdinary);
        }

        // -- B. the practice is derived, never declared --------------------------------------

        /// <summary>
        /// Mourning needs both halves: a death recorded in this place, and somebody standing in it
        /// who cared. A killing in an empty street is history; a room of strangers is not solemn
        /// because somebody died two towns away.
        /// </summary>
        [Fact]
        public void MourningNeedsBothADeathHereAndSomebodyHereWhoCared()
        {
            Assert.True(Room.Mourning().Practices().Holds(SocialPracticeKind.Mourning));

            Room deathButNoMourner = Room.Plain();
            deathButNoMourner.RecordDeathHere(daysAgo: 0, mourned: false);
            Assert.False(deathButNoMourner.Practices().Holds(SocialPracticeKind.Mourning));

            Room mournerButNoDeathHere = Room.Plain();
            mournerButNoDeathHere.RecordDeathElsewhere(daysAgo: 0, mourned: true);
            Assert.False(mournerButNoDeathHere.Practices().Holds(SocialPracticeKind.Mourning));
        }

        /// <summary>
        /// A norm is held by an occasion, and occasions pass. The same room further from the death
        /// holds the same practice less firmly, and past the window does not hold it at all.
        /// </summary>
        [Fact]
        public void APracticeDecaysWithTheOccasionThatPutItThere()
        {
            double sameDay = Room.Mourning(daysAgo: 0).Practices().StrengthOf(SocialPracticeKind.Mourning);
            double later = Room.Mourning(daysAgo: 2).Practices().StrengthOf(SocialPracticeKind.Mourning);
            double over = Room.Mourning(daysAgo: SocialPractices.MourningDays + 1)
                .Practices().StrengthOf(SocialPracticeKind.Mourning);

            Assert.True(sameDay > later, sameDay + " should outlast " + later);
            Assert.True(later > 0.0);
            Assert.Equal(0.0, over);
        }

        /// <summary>
        /// Trade is a practice because BQ-145 says somebody here provides a service and the game
        /// says it is open. A build that cannot see whether the counter is open has not said the
        /// shop is shut, and derives nothing rather than assuming either way (D017).
        /// </summary>
        [Fact]
        public void CommerceIsAnIdentityReadAndAnUnreadCounterAssertsNothing()
        {
            Assert.True(Room.Shop(ServiceAvailability.Offered).Practices().Holds(SocialPracticeKind.Commerce));
            Assert.False(Room.Shop(ServiceAvailability.NotOffered).Practices().Holds(SocialPracticeKind.Commerce));
            Assert.False(Room.Shop(ServiceAvailability.Unknown).Practices().Holds(SocialPracticeKind.Commerce));

            // And the practice names the facet it came from, so a number nobody can attribute to a
            // read cannot get in.
            SocialPracticeHolding shop = Room.Shop(ServiceAvailability.Offered)
                .Practices().Held.Single(h => h.Kind == SocialPracticeKind.Commerce);
            Assert.NotEmpty(shop.Sources);
            Assert.All(shop.Sources, source => Assert.False(string.IsNullOrWhiteSpace(source)));
        }

        /// <summary>
        /// A meeting is several people answerable to one body. Two people who each belong to
        /// something different are two people in a room, and asserting a meeting from that would be
        /// the guess the identity boundary refuses.
        /// </summary>
        [Fact]
        public void SeveralOfOneBodyMakeAMeetingAndOneOfEachDoesNot()
        {
            Assert.True(Room.Meeting(sameBody: true).Practices().Holds(SocialPracticeKind.Assembly));
            Assert.False(Room.Meeting(sameBody: false).Practices().Holds(SocialPracticeKind.Assembly));
        }

        /// <summary>
        /// A household is a household because somebody who lives there is standing in it. The same
        /// Home with everybody out is the unattended case, not a weaker household.
        /// </summary>
        [Fact]
        public void AHouseholdNeedsSomebodyWhoLivesThereStandingInIt()
        {
            Assert.True(Room.Household(residentPresent: true).Practices().Holds(SocialPracticeKind.Household));
            Assert.False(Room.Household(residentPresent: false).Practices().Holds(SocialPracticeKind.Household));
        }

        // -- C. the limits -------------------------------------------------------------------

        /// <summary>
        /// A practice modulates a reaction and never invents one. A bribe passed at a wake is not
        /// something CD §16 says a wake minds, and the bystander who would not have reacted to it
        /// anywhere else does not react to it here either.
        /// </summary>
        [Fact]
        public void APracticeNeverInventsAReactionForAnEventItsNormIsSilentOn()
        {
            Room wake = Room.Mourning();
            SocialNormReading silent = wake.Practices().ReadingOf(WorldEventType.Bribed);

            Assert.True(silent.IsSilent);
            Assert.Equal(0.0, silent.Aggravation);

            int before = wake.Vanilla.GetAffinity(wake.Bystander);
            wake.Record(WorldEventType.Bribed, new[] { wake.Bystander });
            Assert.Equal(before, wake.Vanilla.GetAffinity(wake.Bystander));
        }

        /// <summary>
        /// A practice is not a verdict. What the law makes of an act stays BQ-046's answer and what
        /// a loss cost the person robbed is theirs; the company they were in changes neither.
        /// </summary>
        [Fact]
        public void APracticeChangesWhatTheRoomMakesOfItAndNotTheLawOrTheLoss()
        {
            Room wake = Room.Mourning();
            Room storeRoom = Room.Plain();

            wake.WitnessedTheft();
            storeRoom.WitnessedTheft();

            Assert.Equal(storeRoom.Vanilla.Karma, wake.Vanilla.Karma);
            Assert.Equal(storeRoom.Vanilla.Fame, wake.Vanilla.Fame);
            Assert.Equal(
                storeRoom.Vanilla.GetAffinity(storeRoom.Victim),
                wake.Vanilla.GetAffinity(wake.Victim));
        }

        /// <summary>
        /// The other direction: a place that licenses the act takes it more lightly. A shove at a
        /// contest still costs the person shoved, and costs less with the people who came to watch
        /// one - which is the only reason the norm is worth having rather than a second severity
        /// dial pointing one way.
        /// </summary>
        [Fact]
        public void APlaceThatLicensesTheActTakesItMoreLightly()
        {
            Room festival = Room.Contest();
            Room street = Room.Plain();

            int atTheContest = festival.WitnessedThreat();
            int onTheStreet = street.WitnessedThreat();

            Assert.True(onTheStreet < 0, "somebody with a stake in it did not mind on an ordinary street");
            Assert.True(atTheContest > onTheStreet,
                "the contest did not soften it: " + atTheContest + " vs " + onTheStreet);
        }

        /// <summary>
        /// The regression floor under every one of these: where no norm is in force the layer
        /// behaves exactly as it did before practices existed. Ties still carry, strangers still do
        /// not, and nothing about the place is consulted.
        /// </summary>
        [Fact]
        public void AnOrdinaryPlaceLeavesTheConsequenceLayerExactlyWhereItWas()
        {
            Room street = Room.Plain();
            Assert.True(street.Practices().IsOrdinary);
            Assert.True(street.Practices().ReadingOf(WorldEventType.Theft).IsSilent);
            Assert.Equal(0, street.WitnessedTheft());

            Room withAFriendOfTheVictim = Room.Plain();
            withAFriendOfTheVictim.TieBystanderToVictim();
            Assert.True(withAFriendOfTheVictim.WitnessedTheft() < 0,
                "a tie stopped carrying once practices existed");
        }

        /// <summary>
        /// Norms that disagree are allowed to, and the reading says who said what. This is the
        /// composition rule stated as behaviour rather than as arithmetic: a contest held where
        /// somebody is being mourned is a place at odds with itself, and both halves are named.
        /// </summary>
        [Fact]
        public void NormsThatDisagreeComposeAndBothAreNamed()
        {
            Room both = Room.Mourning();
            both.RecordContestHere();

            SocialPracticeReading practices = both.Practices();
            Assert.True(practices.Holds(SocialPracticeKind.Mourning));
            Assert.True(practices.Holds(SocialPracticeKind.Contest));

            SocialNormReading threat = practices.ReadingOf(WorldEventType.Threatened);
            Assert.Equal(2, threat.Terms.Count);
            Assert.Contains(threat.Terms, term => term.StartsWith("mourning"));
            Assert.Contains(threat.Terms, term => term.StartsWith("contest"));

            double alone = Room.Mourning().Practices().ReadingOf(WorldEventType.Threatened).Aggravation;
            Assert.True(threat.Aggravation < alone,
                "the contest did not pull against the wake: " + threat.Aggravation + " vs " + alone);
        }

        /// <summary>
        /// Nothing is read with no game attached, and nothing is guessed either. Every practice
        /// needs to know who is standing here and only the game can say.
        /// </summary>
        [Fact]
        public void WithNoGameAttachedNoPracticeIsAsserted()
        {
            Room room = Room.Mourning();

            Assert.True(SocialPractices.Read(room.World, null, Hall, room.Vanilla.Now).IsOrdinary);
            Assert.True(SocialPractices.Read(null, room.Vanilla, Hall, room.Vanilla.Now).IsOrdinary);
            Assert.True(SocialPractices.Read(room.World, room.Vanilla, EntityId.None, room.Vanilla.Now).IsOrdinary);
            Assert.True(SocialPracticeReading.Ordinary.IsOrdinary);
            Assert.True(SocialPracticeReading.Ordinary.ReadingOf(WorldEventType.Theft).IsSilent);
        }

        // -- the fixture ---------------------------------------------------------------------

        /// <summary>
        /// One room, four people, and whatever the caller says has lately happened in it.
        ///
        /// Deliberately built by hand rather than out of the theft laboratory: the point of every
        /// test here is that two rooms differ in exactly one respect, and the laboratory stages a
        /// situation whose casting would differ in others.
        /// </summary>
        private sealed class Room
        {
            public readonly NarrativeWorldState World;
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            public readonly ConsequenceEngine Consequences;
            public readonly EntityId Victim;
            public readonly EntityId Bystander;
            public readonly EntityId Mourner;

            private readonly EntityId _zone;

            private Room(EntityId zone)
            {
                _zone = zone;
                World = new NarrativeWorldState(4242);
                Vanilla.Define(Player, zone: zone);
                Vanilla.Now = GameTime.FromDays(30);

                Victim = Person("victim");
                Bystander = Person("bystander");
                Mourner = Person("mourner");

                Consequences = new ConsequenceEngine(World, Vanilla);
                Consequences.Attach();
            }

            /// <summary>A room with nothing going on in it, which is most rooms.</summary>
            public static Room Plain() => new Room(Warehouse);

            /// <summary>Somebody died here, and somebody who cared about them is standing in it.</summary>
            public static Room Mourning(int daysAgo = 0)
            {
                Room room = new Room(Hall);
                room.RecordDeathHere(daysAgo, mourned: true);
                return room;
            }

            /// <summary>A contest was judged here and the people who watched it are still about.</summary>
            public static Room Contest()
            {
                Room room = new Room(Hall);
                room.RecordContestHere();
                return room;
            }

            public static Room Shop(ServiceAvailability availability)
            {
                Room room = new Room(Hall);
                room.Vanilla.SetCharacterIdentity(room.Bystander, new CharacterIdentityBuilder(room.Bystander)
                    .WithService("shop_general", "general store", availability)
                    .Build());
                return room;
            }

            public static Room Meeting(bool sameBody)
            {
                Room room = new Room(Hall);
                Organization first = room.World.Registry.Add(
                    new Organization(EntityId.Parse("org_guild"), "the guild", "guild"));
                first.MemberIds.Add(room.Bystander);

                Organization second = sameBody
                    ? first
                    : room.World.Registry.Add(new Organization(EntityId.Parse("org_watch"), "the watch", "watch"));
                second.MemberIds.Add(room.Mourner);
                return room;
            }

            public static Room Household(bool residentPresent)
            {
                Room room = new Room(Hall);
                HomeStateBuilder home = new HomeStateBuilder(Hall, "the farmstead").WithCapacity(4);
                home.AddResident(room.Bystander, "bystander", "farmhand");
                room.Vanilla.SetHome(home.Build());
                if (!residentPresent)
                {
                    room.Vanilla.SetZone(room.Bystander, Warehouse);
                }

                return room;
            }

            public SocialPracticeReading Practices() =>
                SocialPractices.Read(World, Vanilla, _zone, Vanilla.Now);

            public void RecordDeathHere(int daysAgo, bool mourned) => RecordDeath(_zone, daysAgo, mourned);

            public void RecordDeathElsewhere(int daysAgo, bool mourned) =>
                RecordDeath(_zone == Hall ? Warehouse : Hall, daysAgo, mourned);

            public void RecordContestHere()
            {
                World.Record(
                    WorldEventType.CompetitionWon,
                    Mourner,
                    Bystander,
                    Vanilla.Now,
                    0.35,
                    _zone);
            }

            /// <summary>
            /// The bystander turns out to have been a friend of the victim's. The one input the
            /// consequence layer already had, so that a softening practice has something to soften.
            /// </summary>
            public void TieBystanderToVictim() =>
                World.Relationships.Connect(Bystander, Victim, RelationKind.Friend, 70);

            /// <summary>The player robs the victim in front of the bystander. Returns what it cost.</summary>
            public int WitnessedTheft() => WitnessedBy(WorldEventType.Theft);

            /// <summary>
            /// The player leans on the victim in front of the bystander, who has a stake in the
            /// matter but no tie to anybody in it.
            ///
            /// The stake rather than a friendship on purpose: a friend of the victim is also owed
            /// a reaction by the tie graph, which no practice touches, and measuring both at once
            /// would say nothing about either.
            /// </summary>
            public int WitnessedThreat() => WitnessedBy(WorldEventType.Threatened, StakeOfTheBystander());

            public void RecordTheft(IReadOnlyList<EntityId> witnesses) =>
                Record(WorldEventType.Theft, witnesses);

            public void Record(WorldEventType type, IReadOnlyList<EntityId> witnesses)
            {
                World.Record(type, Player, Victim, Vanilla.Now, 0.8, _zone, witnesses: witnesses);
            }

            /// <summary>
            /// A matter of the bystander's own, so the consequence layer already has a reason to
            /// let them react. Nothing about it is a relationship and nothing about it is a norm.
            /// </summary>
            private EntityId[] StakeOfTheBystander()
            {
                Fact stake = new Fact(
                    World.NewId("fact"),
                    Bystander,
                    FactPredicates.Owes,
                    Victim,
                    "a debt",
                    TruthState.True);
                World.Knowledge.AddFact(stake);
                return new[] { stake.Id };
            }

            public MemoryRecord WitnessMemory() =>
                World.Memories.MemoriesAbout(Bystander, Player).Single();

            private int WitnessedBy(WorldEventType type, IReadOnlyList<EntityId> related = null)
            {
                int before = Vanilla.GetAffinity(Bystander);
                World.Record(
                    type, Player, Victim, Vanilla.Now, 0.8, _zone, related, new[] { Bystander });
                return Vanilla.GetAffinity(Bystander) - before;
            }

            private void RecordDeath(EntityId zone, int daysAgo, bool mourned)
            {
                EntityId dead = Person("dead");
                if (mourned)
                {
                    World.Relationships.Connect(Bystander, dead, RelationKind.Friend, 60);
                }

                World.Record(
                    WorldEventType.Killed,
                    EntityId.Parse("npc_killer"),
                    dead,
                    Vanilla.Now.PlusDays(-daysAgo),
                    0.9,
                    zone);
                Vanilla.Kill(dead);
                NarrativeNpc npc = World.Registry.GetNpc(dead);
                if (npc != null)
                {
                    npc.Alive = false;
                }
            }

            private EntityId Person(string key)
            {
                EntityId id = EntityId.Parse("npc_" + key);
                World.Registry.Add(new NarrativeNpc(id, key)
                {
                    Occupation = "local",
                    Importance = NarrativeImportance.Background
                });

                Vanilla.Define(id, money: 20, zone: _zone)
                    .SetActorClass(id, NarrativeActorClass.OrdinaryCitizen)
                    .SetActorKind(id, NarrativeActorKind.Person)
                    .SetSocialAgency(id, SocialAgency.Full);

                return id;
            }
        }
    }
}
