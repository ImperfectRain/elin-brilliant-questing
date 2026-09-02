using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-114. A threat to a stranger is an errand; the same threat to the shopkeeper the player
    /// buys from is a story. These prove that casting notices the difference, and that noticing it
    /// never becomes a reason for something to happen.
    /// </summary>
    public class PlayerFamiliarityTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Market = EntityId.Parse("zone_market");

        private static readonly string[] Marks = { "alder", "bram", "cass", "dell" };

        // -- A. the done-when ---------------------------------------------------------------

        [Fact]
        public void TheFirstSituationInAFreshSaveCastsSomebodyThePlayerAlreadyKnows()
        {
            // Four marks the world is equally pressured toward, and one of them is a face the
            // player has actually dealt with. Which one rotates, so a run cannot be won by the
            // order ids happen to sort in - before familiarity was read, the same id won every
            // time and the known face was cast in a quarter of the runs.
            int knownFaces = 0;
            const int runs = 100;

            for (int run = 0; run < runs; run++)
            {
                string acquaintance = Marks[run % Marks.Length];
                Lab lab = EquallyPressuredMarket((ulong)(7000 + run), acquaintance);

                PettyTheftSituation situation = new SettlementSituationGenerator()
                    .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

                Assert.NotNull(situation);
                if (situation.VictimId == Npc(acquaintance) || situation.ThiefId == Npc(acquaintance))
                {
                    knownFaces++;
                }
            }

            Assert.True(knownFaces >= 75, knownFaces + " familiar faces cast out of " + runs);
        }

        [Fact]
        public void TheReasonThePlayerKnowsThemReachesTheInspector()
        {
            Lab lab = EquallyPressuredMarket(seed: 91, acquaintance: "cass");

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Contains(
                situation.Thread.GenerationCauses,
                cause => cause.Contains("is known to the player: "));
        }

        // -- B. a preference, never a cause -------------------------------------------------

        [Fact]
        public void KnowingEverybodyInTownDoesNotProduceASituation()
        {
            // A quiet settlement whose every resident lives on the player's own land, has traded
            // with them and is well liked. Nobody is carrying anything worth taking, so nothing
            // happens - familiarity decides which story is told, never that there is one.
            Lab lab = new Lab(Market);
            HomeStateBuilder home = new HomeStateBuilder(EntityId.Parse("zone_home"), "Home");
            foreach (string key in Marks)
            {
                EntityId id = lab.Local(key, money: 300, greed: 0.5);
                home.AddResident(id, key);
                lab.Vanilla.SetAffinity(id, 200);
                lab.World.Record(WorldEventType.Helped, Player, id, lab.Vanilla.Now, 0.6, Market);
            }

            lab.Vanilla.SetHome(home.Build());

            SettlementSituationPlan plan = new SettlementSituationGenerator()
                .Evaluate(lab.World, lab.Vanilla, Market);

            Assert.Empty(plan.Candidates);
            Assert.Empty(plan.Suppressed);
            Assert.Equal(4, plan.Profile.Actors.Count);
            Assert.Contains("locals the player knows: 4", plan.Profile.Features);
        }

        [Fact]
        public void RealPressureStillOutranksAPassingAcquaintance()
        {
            // The stranger is the far better mark. Recognising somebody is worth a nudge, not a
            // rewrite of what the settlement is actually under pressure about.
            Lab lab = new Lab(Market);
            lab.Local("cutpurse", money: 15, greed: 0.9, pickpocket: 9, stealth: 7);
            EntityId stranger = lab.Local("stranger", money: 2400, greed: 0.3, carriedValue: 2600, occupation: "shopkeeper");
            EntityId acquaintance = lab.Local("acquaintance", money: 260, greed: 0.3, carriedValue: 420);
            lab.Vanilla.SetAffinity(acquaintance, 60);

            SettlementSituationPlan plan = new SettlementSituationGenerator()
                .Evaluate(lab.World, lab.Vanilla, Market);

            Assert.Equal(stranger, plan.BestCandidate.ActorIn(SituationRoles.Target));
            SituationCandidate known = plan.Candidates.Single(c =>
                c.ActorIn(SituationRoles.Target) == acquaintance);
            Assert.True(known.Pressure(SituationPressures.PlayerFamiliarity) > 0);
        }

        [Fact]
        public void ACastOfStrangersRecordsNoFamiliarityTermAtAll()
        {
            // An absence of history is not a measurement of zero, and the inspector should not
            // report a term the world never contributed.
            Lab lab = PressuredMarket();

            SituationCandidate best = new SettlementSituationGenerator()
                .Evaluate(lab.World, lab.Vanilla, Market)
                .BestCandidate;

            Assert.NotNull(best);
            Assert.False(best.Pressures.ContainsKey(SituationPressures.PlayerFamiliarity));
            Assert.DoesNotContain(best.Causes, c => c.Contains("is known to the player"));
        }

        // -- C. what counts as knowing somebody ---------------------------------------------

        [Fact]
        public void EachGroundOfFamiliarityStandsOnItsOwnAndSaysSo()
        {
            Lab household = new Lab(Market);
            EntityId resident = household.Local("resident", money: 200, greed: 0.4);
            household.Vanilla.SetHome(new HomeStateBuilder(EntityId.Parse("zone_home"), "Home")
                .AddResident(resident, "Resident")
                .Build());
            AssertKnown(household, resident, "lives on the player's own land");

            Lab dealings = new Lab(Market);
            EntityId helper = dealings.Local("helper", money: 200, greed: 0.4);
            dealings.World.Record(WorldEventType.ItemGiven, Player, helper, dealings.Vanilla.Now, 0.5, Market);
            AssertKnown(dealings, helper, "has dealt with the player 1 time(s)");

            Lab crossed = new Lab(Market);
            EntityId robber = crossed.Local("robber", money: 200, greed: 0.4);
            crossed.World.Record(WorldEventType.Theft, robber, Player, crossed.Vanilla.Now, 0.6, Market);
            AssertKnown(crossed, robber, "has crossed the player 1 time(s)");

            Lab tied = new Lab(Market);
            EntityId sibling = tied.Local("sibling", money: 200, greed: 0.4);
            tied.World.Relationships.Connect(Player, sibling, RelationKind.Family, 70);
            AssertKnown(tied, sibling, "stands to the player as Family");

            Lab liked = new Lab(Market);
            EntityId regular = liked.Local("regular", money: 200, greed: 0.4);
            liked.Vanilla.SetAffinity(regular, 45);
            AssertKnown(liked, regular, "the game records affinity 45 toward the player");
        }

        [Fact]
        public void SomebodyThePlayerWrongedIsNotAStranger()
        {
            // Familiarity is recognition, never affection: a person the player robbed is exactly
            // the person a situation about them should be cast from.
            Lab lab = new Lab(Market);
            EntityId victim = lab.Local("victim", money: 200, greed: 0.4);
            lab.Vanilla.SetAffinity(victim, -80);
            lab.World.Record(WorldEventType.Theft, Player, victim, lab.Vanilla.Now, 0.6, Market);

            FamiliarityReading reading = PlayerFamiliarity.Read(lab.World, lab.Vanilla).Of(victim);

            Assert.True(reading.IsKnown);
            Assert.Contains("affinity -80", reading.Because);
        }

        [Fact]
        public void FamiliarityIsCappedSoOneFaceCannotOutweighTheWorld()
        {
            Lab lab = new Lab(Market);
            EntityId spouse = lab.Local("spouse", money: 200, greed: 0.4);
            lab.Vanilla.SetHome(new HomeStateBuilder(EntityId.Parse("zone_home"), "Home")
                .AddResident(spouse, "Spouse")
                .Build());
            lab.Vanilla.SetAffinity(spouse, 900);
            lab.World.Relationships.Connect(Player, spouse, RelationKind.Spouse, 100);
            for (int i = 0; i < 40; i++)
            {
                lab.World.Record(WorldEventType.Helped, Player, spouse, lab.Vanilla.Now, 0.5, Market);
            }

            Assert.Equal(PlayerFamiliarity.Ceiling, PlayerFamiliarity.Read(lab.World, lab.Vanilla).ScoreOf(spouse));
        }

        [Fact]
        public void ThePlayerIsNeverTheirOwnAcquaintance()
        {
            Lab lab = PressuredMarket();
            lab.World.Registry.Add(new NarrativeNpc(Player, "You"));
            lab.World.Record(WorldEventType.Conversed, Player, Player, lab.Vanilla.Now, 0.2, Market);

            Assert.False(PlayerFamiliarity.Read(lab.World, lab.Vanilla).Knows(Player));
        }

        [Fact]
        public void AnUnreadableAffinityLeavesAStrangerRatherThanAnEnemy()
        {
            // D017. A build that cannot answer has not answered zero, and the other grounds are
            // still worth reading on their own.
            Lab lab = new Lab(Market);
            EntityId regular = lab.Local("regular", money: 200, greed: 0.4);
            lab.Vanilla.SetAffinity(regular, 120);
            lab.Vanilla.SetCapability(VanillaCapability.ReadWriteAffinity, false);

            PlayerFamiliarity familiarity = PlayerFamiliarity.Read(lab.World, lab.Vanilla);
            Assert.False(familiarity.Knows(regular));

            lab.World.Record(WorldEventType.Helped, Player, regular, lab.Vanilla.Now, 0.5, Market);
            Assert.True(PlayerFamiliarity.Read(lab.World, lab.Vanilla).Knows(regular));
        }

        [Fact]
        public void ReadingTheSameWorldTwiceGivesTheSameAnswer()
        {
            Lab lab = EquallyPressuredMarket(seed: 4242, acquaintance: "dell");

            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            SettlementSituationPlan first = generator.Evaluate(lab.World, lab.Vanilla, Market);
            SettlementSituationPlan second = generator.Evaluate(lab.World, lab.Vanilla, Market);

            Assert.Equal(
                first.Candidates.Select(c => c.ActorIn(SituationRoles.Actor) + "->" + c.ActorIn(SituationRoles.Target) + "@" + c.Score),
                second.Candidates.Select(c => c.ActorIn(SituationRoles.Actor) + "->" + c.ActorIn(SituationRoles.Target) + "@" + c.Score));
        }

        // -- D. the other casting surface ---------------------------------------------------

        [Fact]
        public void AStoryletSearchingForSomebodyPrefersAFaceThePlayerKnows()
        {
            // Two people who equally meet the requirement, neither of them already in the matter.
            // Before BQ-114 the pool was ordered by id alone, so the scene took whichever id
            // sorted first and the player had never heard of them half the time.
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId stranger = Bystander(lab, "npc_a_stranger", "A Stranger");
            EntityId neighbour = Bystander(lab, "npc_z_neighbour", "Z Neighbour");
            lab.Vanilla.SetAffinity(neighbour, 90);

            StoryletDefinition definition = new StoryletDefinition("storylet.test.familiar");
            definition.Beats.Add(new StoryletBeat("open"));
            definition.RequiredRoles.Add(new StoryletRole("mediator", StoryletRoleSource.AnyoneWithStandingHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                definition,
                new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(neighbour, opportunity.RoleBindings["mediator"]);
            Assert.NotEqual(stranger, opportunity.RoleBindings["mediator"]);
        }

        [Fact]
        public void CastingStillRefusesSomebodyWhoDoesNotQualifyHoweverWellTheyAreKnown()
        {
            // Familiarity orders the search; it never satisfies the requirement (D026).
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId friend = Bystander(lab, "npc_a_friend", "A Friend", standing: false);
            lab.Vanilla.SetAffinity(friend, 400);

            StoryletDefinition definition = new StoryletDefinition("storylet.test.standing_only");
            definition.Beats.Add(new StoryletBeat("open"));
            definition.RequiredRoles.Add(new StoryletRole("mediator", StoryletRoleSource.AnyoneWithStandingHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                definition,
                new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId));

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("mediator", opportunity.RefusalReason);
        }

        // -- fixtures -----------------------------------------------------------------------

        private static void AssertKnown(Lab lab, EntityId actor, string ground)
        {
            FamiliarityReading reading = PlayerFamiliarity.Read(lab.World, lab.Vanilla).Of(actor);

            Assert.True(reading.IsKnown, actor + " reads as a stranger");
            Assert.Contains(ground, reading.Because);
            Assert.Equal(reading.Score, LocalAffordanceProfile
                .Read(lab.World, lab.Vanilla, Market)
                .Of(actor)
                .Familiarity
                .Score);
        }

        private static EntityId Npc(string key) => EntityId.Parse("npc_" + key);

        /// <summary>Somebody standing in the theft laboratory's town who is not part of the matter.</summary>
        private static EntityId Bystander(TheftLaboratory lab, string id, string name, bool standing = true)
        {
            EntityId actor = EntityId.Parse(id);
            NarrativeNpc npc = lab.World.Registry.Add(new NarrativeNpc(actor, name));
            if (standing)
            {
                npc.Roles.Add("guard");
            }

            lab.Vanilla.Define(actor, money: 100, zone: lab.Zone);
            return actor;
        }

        /// <summary>One thief and four marks the world has no reason to choose between.</summary>
        private static Lab EquallyPressuredMarket(ulong seed, string acquaintance)
        {
            Lab lab = new Lab(Market, seed);
            lab.Local("cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            foreach (string key in Marks)
            {
                lab.Local(key, money: 700, greed: 0.3, carriedValue: 800, occupation: "shopkeeper");
            }

            // The one the player buys from: Elin's own record of their dealings, which is the only
            // history that exists in a save the mod has only just attached to.
            lab.Vanilla.SetAffinity(Npc(acquaintance), 70);
            return lab;
        }

        private static Lab PressuredMarket()
        {
            Lab lab = new Lab(Market);
            lab.Local("merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Local("cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Local("clerk", money: 140, greed: 0.3, perception: 10);
            return lab;
        }

        private sealed class Lab
        {
            public readonly NarrativeWorldState World;
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            private readonly EntityId _zone;

            public Lab(EntityId zone, ulong seed = 42)
            {
                World = new NarrativeWorldState(seed);
                _zone = zone;
                Vanilla.Define(Player, zone: zone);
            }

            public EntityId Local(
                string key,
                int money,
                double greed,
                int carriedValue = 0,
                int pickpocket = 0,
                int stealth = 0,
                int perception = 4,
                string occupation = "local")
            {
                EntityId id = Npc(key);
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(id, key)
                {
                    Occupation = occupation,
                    Importance = NarrativeImportance.Background
                });
                npc.Personality.Greed = greed;

                Vanilla.Define(id, money: money, zone: _zone)
                    .SetActorClass(id, NarrativeActorClass.OrdinaryCitizen)
                    .SetActorKind(id, NarrativeActorKind.Person)
                    .SetSocialAgency(id, SocialAgency.Full)
                    .SetSkill(id, VanillaSkill.Pickpocket, pickpocket)
                    .SetSkill(id, VanillaSkill.Stealth, stealth)
                    .SetAttribute(id, VanillaAttribute.Dexterity, pickpocket + stealth)
                    .SetAttribute(id, VanillaAttribute.Perception, perception);

                if (carriedValue > 0)
                {
                    Vanilla.GiveItem(id, new ItemDescriptor(
                        EntityId.Parse("item_" + key + "_valuable"),
                        key + " heirloom",
                        "jewelry",
                        carriedValue,
                        "ring"));
                }

                return id;
            }
        }
    }
}
