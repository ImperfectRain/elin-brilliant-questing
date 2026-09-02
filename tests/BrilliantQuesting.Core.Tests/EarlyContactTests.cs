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
    /// BQ-115. A save that has just started has no history in it, so BQ-114 reads every face in
    /// town as a stranger and the first situation lands on whoever the pressure happened to pick.
    /// These prove the save elects a handful of faces before that happens, that electing them
    /// invents nothing, and that it never becomes a reason for a situation to exist.
    /// </summary>
    public class EarlyContactTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Market = EntityId.Parse("zone_market");
        private static readonly EntityId Home = EntityId.Parse("zone_home");

        private static readonly string[] Marks = { "alder", "bram", "cass", "dell" };

        // -- A. the done-when ---------------------------------------------------------------

        /// <summary>
        /// The done-when, on a save with nothing in it. Four equally pressured marks, no ledger, no
        /// relationships, no affinity - the state a save is actually in when the mod attaches - and
        /// the first situation it produces is about a face it elected before producing anything.
        ///
        /// This is the fixture BQ-114 could not have: its own done-when writes an affinity of 70 by
        /// hand, because when it landed nothing in the mod produced an acquaintance. Measured on
        /// this one before BQ-115 existed, the elected mark was cast in 25 runs of 100 - which is
        /// one mark in four, i.e. chance, i.e. the preference was inert.
        /// </summary>
        [Fact]
        public void AFreshSaveElectsRecognisableFacesBeforeItProducesItsFirstSituation()
        {
            int recognisedFaces = 0;
            const int runs = 100;

            for (int run = 0; run < runs; run++)
            {
                Lab lab = FreshMarket((ulong)(7000 + run));

                // The first-hours pass, in the order the plugin and the harness run it: elect, and
                // only then let the settlement produce anything.
                EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
                Assert.True(cast.Count > 0, "a populated market elected nobody");
                Assert.Empty(lab.World.Threads);

                PettyTheftSituation situation = new SettlementSituationGenerator()
                    .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

                Assert.NotNull(situation);
                if (cast.Includes(situation.VictimId) || cast.Includes(situation.ThiefId))
                {
                    recognisedFaces++;
                }
            }

            Assert.True(recognisedFaces >= 75, recognisedFaces + " recognisable faces cast out of " + runs);
        }

        /// <summary>
        /// The other half of "recognisable recurring faces": the same save keeps naming the same
        /// people. Election is a reading of the settlement rather than a stored roster, so the
        /// answer survives a reload without anything being written to the save to make it.
        /// </summary>
        [Fact]
        public void TheSameSaveKeepsBringingBackTheSameFaces()
        {
            Lab lab = FreshMarket(seed: 4242);

            EarlyContactCast first = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            EarlyContactCast again = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            EarlyContactCast afterReload = EarlyContacts.Elect(RoundTrip(lab.World), lab.Vanilla, Market);

            Assert.NotEmpty(first.Contacts);
            Assert.Equal(Ids(first), Ids(again));
            Assert.Equal(Ids(first), Ids(afterReload));
        }

        /// <summary>
        /// The ladder rung that used to be unreachable before a crisis. `PM §19` wants importance
        /// to be emergent, and it still is - this only says that being somebody the save keeps
        /// bringing back is itself a ground, and one that does not require being robbed first.
        /// </summary>
        [Fact]
        public void ElectedFacesReachTheRecurringRungBeforeAnythingHappensToThem()
        {
            Lab lab = FreshMarket(seed: 11);

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);

            Assert.NotEmpty(cast.Contacts);
            foreach (EarlyContact contact in cast.Contacts)
            {
                Assert.True(
                    lab.World.Registry.GetNpc(contact.Actor).Importance >= NarrativeImportance.Recurring,
                    contact.Actor + " was elected but never reached the recurring rung");
            }

            Assert.Empty(lab.World.Ledger.Events);
        }

        // -- B. what electing must never do -------------------------------------------------

        /// <summary>
        /// Rule 5. The mod may decide who a story is about; it may never record that the player did
        /// something they did not do. Nothing is written to the ledger, no relationship is minted,
        /// and Elin's own affinity is untouched - so BQ-114 still reads a stranger as a stranger.
        /// </summary>
        [Fact]
        public void ElectingInventsNoHistoryTheSaveDidNotHave()
        {
            Lab lab = FreshMarket(seed: 77);

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            PlayerFamiliarity familiarity = PlayerFamiliarity.Read(lab.World, lab.Vanilla);

            Assert.NotEmpty(cast.Contacts);
            Assert.Empty(lab.World.Ledger.Events);
            foreach (EarlyContact contact in cast.Contacts)
            {
                Assert.False(familiarity.Knows(contact.Actor), "electing a face claimed the player knows them");
                Assert.Equal(0, lab.Vanilla.GetAffinity(contact.Actor));
                Assert.Null(lab.World.Relationships.Find(Player, contact.Actor));
                Assert.Null(lab.World.Relationships.Find(contact.Actor, Player));
            }
        }

        /// <summary>
        /// `D027`, unchanged. Familiarity was never allowed to be the reason a situation exists and
        /// neither is this: a settlement with no pressure in it stays quiet however many faces the
        /// save would like to keep bringing back.
        /// </summary>
        [Fact]
        public void ElectingEverybodyStillDoesNotProduceASituation()
        {
            Lab lab = new Lab(Market, seed: 5);
            foreach (string key in Marks)
            {
                // Comfortable, unhurried, carrying nothing worth taking: no motive, no means.
                lab.Local(key, money: 500, greed: 0.05, occupation: "shopkeeper");
            }

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotEmpty(cast.Contacts);
            Assert.Null(situation);
        }

        /// <summary>
        /// Real pressure still decides. An elected face is worth less than a genuine acquaintance
        /// and far less than the world's own reasons, which is what keeps a casting preference from
        /// quietly becoming a director.
        /// </summary>
        [Fact]
        public void RealPressureStillOutranksAnElectedFace()
        {
            Lab lab = new Lab(Market, seed: 31);
            lab.Local("cutpurse", money: 15, greed: 0.9, pickpocket: 9, stealth: 7);
            lab.Local("elected", money: 700, greed: 0.3, carriedValue: 60, occupation: "shopkeeper");
            lab.Local("richer", money: 700, greed: 0.3, carriedValue: 4000);

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.True(cast.Includes(Npc("elected")));
            Assert.NotNull(situation);
            Assert.Equal(Npc("richer"), situation.VictimId);
        }

        // -- C. the grounds -----------------------------------------------------------------

        /// <summary>
        /// The two faces `engagement §4` names by hand, chosen from the people already here rather
        /// than spawned: the neighbour who lives on the player's land, and the shopkeeper they will
        /// end up standing in front of anyway. Household outranks trade, because it is the stronger
        /// tie the game itself already models.
        /// </summary>
        [Fact]
        public void TheNeighbourAndTheShopkeeperAreTheFacesItPicks()
        {
            Lab lab = new Lab(Market, seed: 9);
            EntityId keeper = lab.Local("keeper", money: 400, greed: 0.3, occupation: "shopkeeper");
            EntityId passerby = lab.Local("passerby", money: 40, greed: 0.3);
            EntityId neighbour = lab.Resident("mira");

            EarlyContactCast cast = EarlyContacts.Elect(lab.World, lab.Vanilla, Market);

            Assert.Equal(EarlyContactKind.Neighbour, cast.Of(neighbour).Kind);
            Assert.Equal(EarlyContactKind.Shopkeeper, cast.Of(keeper).Kind);
            Assert.Equal(EarlyContactKind.Regular, cast.Of(passerby).Kind);
            Assert.Equal(neighbour, cast.Contacts[0].Actor);
            Assert.True(cast.WeightOf(neighbour) > cast.WeightOf(keeper));
            Assert.True(cast.WeightOf(keeper) > cast.WeightOf(passerby));
        }

        /// <summary>
        /// A face is elected for the settlement casting is about to happen in. Somebody who lives
        /// on the player's land but is not here cannot make a story told here land on a familiar
        /// face, and BQ-114 already reads them as the strongest tie the game has - so spending a
        /// slot on them would buy nothing and cost the settlement one of its three.
        /// </summary>
        [Fact]
        public void SomebodyElsewhereDoesNotTakeUpASettlementsSlot()
        {
            Lab lab = new Lab(Market, seed: 17);
            EntityId away = lab.Resident("mira");
            EntityId here = lab.Local("keeper", money: 400, greed: 0.3, occupation: "shopkeeper");
            lab.Vanilla.Define(away, zone: Home);

            EarlyContactCast cast = EarlyContacts.Elect(lab.World, lab.Vanilla, Market);

            Assert.False(cast.Includes(away));
            Assert.True(cast.Includes(here));

            // And BQ-114 still holds them as the strongest reading it has, unchanged.
            Assert.True(
                PlayerFamiliarity.Read(lab.World, lab.Vanilla).ScoreOf(away)
                >= PlayerFamiliarity.HouseholdWeight);
        }

        /// <summary>
        /// A handful, not a census. The point of recognising these people is that they are not
        /// everybody, so a crowded market elects the same small number a quiet one does.
        /// </summary>
        [Fact]
        public void OnlyAHandfulOfFacesAreEverElected()
        {
            Lab lab = new Lab(Market, seed: 12);
            for (int i = 0; i < 20; i++)
            {
                lab.Local("local" + i, money: 100, greed: 0.3);
            }

            EarlyContactCast cast = EarlyContacts.Elect(lab.World, lab.Vanilla, Market);

            Assert.Equal(EarlyContacts.Handful, cast.Count);
        }

        /// <summary>
        /// Rule 7: existing actor before new actor. Electing never creates anybody, and an empty
        /// settlement is an ordinary answer rather than a reason to invent a face for it.
        /// </summary>
        [Fact]
        public void AnEmptySettlementElectsNobodyRatherThanInventingSomeone()
        {
            Lab lab = new Lab(Market, seed: 3);
            int before = lab.World.Registry.Npcs.Count;

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);

            Assert.Equal(0, cast.Count);
            Assert.Equal(before, lab.World.Registry.Npcs.Count);
        }

        /// <summary>
        /// The mod may only reach into people the mutation policy already lets it reach into, and
        /// only people who can hold up a social role. A pet is not a shopkeeper who remembers you.
        /// </summary>
        [Fact]
        public void OnlyPeopleTheModMayCastAreElected()
        {
            Lab lab = new Lab(Market, seed: 8);
            EntityId person = lab.Local("keeper", money: 400, greed: 0.3, occupation: "shopkeeper");
            EntityId animal = lab.Local("goat", money: 0, greed: 0.1);
            lab.Vanilla.SetActorKind(animal, NarrativeActorKind.Creature);
            EntityId mute = lab.Local("statue", money: 0, greed: 0.1);
            lab.Vanilla.SetSocialAgency(mute, SocialAgency.None);

            EarlyContactCast cast = EarlyContacts.Elect(lab.World, lab.Vanilla, Market);

            Assert.True(cast.Includes(person));
            Assert.False(cast.Includes(animal));
            Assert.False(cast.Includes(mute));
        }

        /// <summary>Rule 26. Every elected face can say why it was elected.</summary>
        [Fact]
        public void TheReasonAFaceWasElectedReachesTheInspector()
        {
            Lab lab = new Lab(Market, seed: 21);
            lab.Local("keeper", money: 400, greed: 0.3, occupation: "shopkeeper");

            EarlyContactCast cast = EarlyContacts.Elect(lab.World, lab.Vanilla, Market);
            string description = EarlyContacts.Describe(cast);

            Assert.Contains("keeper", description);
            Assert.Contains("sells to strangers here", description);
            Assert.All(cast.Contacts, contact => Assert.NotEqual(string.Empty, contact.Because));
        }

        // -- D. the repair to what the skip left behind --------------------------------------

        /// <summary>
        /// The skip-related damage, held down. BQ-114 is the ground that matters when there is any
        /// history, and an elected face must never displace it: somebody the player actually deals
        /// with outranks somebody the save merely decided to keep, and the pressure the candidate
        /// records says which of the two carried the decision.
        /// </summary>
        [Fact]
        public void RealHistoryOutranksAnElectedFaceAndSaysSo()
        {
            // Two marks the world is equally pressured toward - same purse, same thing worth
            // taking, same trade - and both of them elected, so the only thing telling them apart
            // is that the player has actually dealt with one of them.
            Lab lab = new Lab(Market, seed: 63);
            lab.Local("cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            EntityId elected = lab.Local("elected", money: 700, greed: 0.3, carriedValue: 800);
            EntityId known = lab.Local("known", money: 700, greed: 0.3, carriedValue: 800);
            lab.Vanilla.SetAffinity(known, 70);

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, Market);
            Assert.True(cast.Includes(elected));
            Assert.True(cast.Includes(known));

            SettlementSituationPlan plan = new SettlementSituationGenerator()
                .Evaluate(lab.World, lab.Vanilla, Market);
            SituationCandidate best = plan.BestCandidate;

            Assert.NotNull(best);
            Assert.Contains(known, best.ActorsIn(SituationRoles.Target));
            Assert.True(best.Pressure(SituationPressures.PlayerFamiliarity) > 0);
            Assert.Equal(0, best.Pressure(SituationPressures.RecurringContact));
        }

        /// <summary>
        /// And the other way round: with no history to read, the elected face is what the candidate
        /// records, so a fresh save's inspector says why this story is about these people rather
        /// than reporting no reason at all.
        /// </summary>
        [Fact]
        public void AFreshSaveRecordsTheElectedFaceAsTheReason()
        {
            Lab lab = FreshMarket(seed: 64);

            SettlementSituationPlan plan = new SettlementSituationGenerator()
                .Evaluate(lab.World, lab.Vanilla, Market);
            SituationCandidate best = plan.BestCandidate;

            Assert.NotNull(best);
            Assert.True(best.Pressure(SituationPressures.RecurringContact) > 0);
            Assert.Equal(0, best.Pressure(SituationPressures.PlayerFamiliarity));
            Assert.Contains(best.Causes, cause => cause.Contains("is a face this save keeps"));
        }

        /// <summary>
        /// The second casting surface BQ-114 named. A storylet searching for somebody in a save too
        /// new to hold any history used to fall back to id order; it now finds an elected face
        /// first, without any role's requirement being relaxed (`D026`).
        /// </summary>
        [Fact]
        public void AStoryletSearchingInAFreshSaveFindsAnElectedFaceFirst()
        {
            // Two people who equally meet the requirement and neither of whom the player has ever
            // dealt with - no affinity, no ledger, which is what a new save actually looks like.
            // "z" sorts last by id, so finding them first cannot be an artefact of ordering.
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId stranger = Bystander(lab, "npc_a_stranger", "A Stranger", "local");
            EntityId keeper = Bystander(lab, "npc_z_keeper", "Z Keeper", "shopkeeper");

            EarlyContactCast cast = EarlyContacts.Establish(lab.World, lab.Vanilla, lab.Zone);

            StoryletDefinition definition = new StoryletDefinition("storylet.test.elected");
            definition.Beats.Add(new StoryletBeat("open"));
            definition.RequiredRoles.Add(new StoryletRole("mediator", StoryletRoleSource.AnyoneWithStandingHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                definition,
                new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId));

            Assert.Equal(EarlyContactKind.Shopkeeper, cast.Of(keeper).Kind);
            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(keeper, opportunity.RoleBindings["mediator"]);
            Assert.NotEqual(stranger, opportunity.RoleBindings["mediator"]);
        }

        // -- helpers ------------------------------------------------------------------------

        private static EntityId Npc(string key) => EntityId.Parse("npc_" + key);

        /// <summary>
        /// Somebody standing in the theft laboratory's town who is not part of the matter, holds
        /// standing so the role's requirement is met, and does one job or another for a living.
        /// </summary>
        private static EntityId Bystander(TheftLaboratory lab, string id, string name, string occupation)
        {
            EntityId actor = EntityId.Parse(id);
            NarrativeNpc npc = lab.World.Registry.Add(new NarrativeNpc(actor, name) { Occupation = occupation });
            npc.Roles.Add("guard");
            lab.Vanilla.Define(actor, money: 100, zone: lab.Zone);
            return actor;
        }

        private static List<EntityId> Ids(EarlyContactCast cast) =>
            cast.Contacts.Select(contact => contact.Actor).ToList();

        /// <summary>
        /// A world put through the serializer, to prove election is re-derived rather than stored.
        /// </summary>
        private static NarrativeWorldState RoundTrip(NarrativeWorldState world)
        {
            return Persistence.WorldStateSerializer.Load(
                Persistence.WorldStateSerializer.Save(world));
        }

        /// <summary>
        /// One thief and four equally pressured marks, and nothing else: no ledger, no
        /// relationships, no affinity, no residents. What a save looks like the moment the mod
        /// attaches to it.
        /// </summary>
        private static Lab FreshMarket(ulong seed)
        {
            Lab lab = new Lab(Market, seed);
            lab.Local("cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            foreach (string key in Marks)
            {
                lab.Local(key, money: 700, greed: 0.3, carriedValue: 800, occupation: "shopkeeper");
            }

            return lab;
        }

        private sealed class Lab
        {
            public readonly NarrativeWorldState World;
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            private readonly EntityId _zone;
            private readonly HomeStateBuilder _home = new HomeStateBuilder(Home, "Home").WithCapacity(4);
            private bool _anyResidents;

            public Lab(EntityId zone, ulong seed = 42)
            {
                World = new NarrativeWorldState(seed);
                _zone = zone;
                Vanilla.Define(Player, zone: zone);
            }

            /// <summary>
            /// Somebody living on the player's own land, standing where the lab is reading unless
            /// a caller moves them. A resident who walked into town is still a resident.
            /// </summary>
            public EntityId Resident(string key)
            {
                EntityId id = Define(key, money: 50, greed: 0.2, occupation: "farmhand", zone: _zone);
                _home.AddResident(id, key, "farmhand");
                _anyResidents = true;
                Vanilla.SetHome(_home.Build());
                return id;
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
                EntityId id = Define(key, money, greed, occupation, _zone, pickpocket, stealth, perception);
                if (carriedValue > 0)
                {
                    Vanilla.GiveItem(id, new ItemDescriptor(
                        EntityId.Parse("item_" + key + "_valuable"),
                        key + " heirloom",
                        "jewelry",
                        carriedValue,
                        "ring"));
                }

                if (!_anyResidents)
                {
                    // An empty Home is the ordinary state of a new save, and must read as "nobody
                    // lives here" rather than as an unreadable one.
                    Vanilla.SetHome(_home.Build());
                }

                return id;
            }

            private EntityId Define(
                string key,
                int money,
                double greed,
                string occupation,
                EntityId zone,
                int pickpocket = 0,
                int stealth = 0,
                int perception = 4)
            {
                EntityId id = Npc(key);
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(id, key)
                {
                    Occupation = occupation,
                    Importance = NarrativeImportance.Background
                });
                npc.Personality.Greed = greed;

                Vanilla.Define(id, money: money, zone: zone)
                    .SetActorClass(id, NarrativeActorClass.OrdinaryCitizen)
                    .SetActorKind(id, NarrativeActorKind.Person)
                    .SetSocialAgency(id, SocialAgency.Full)
                    .SetSkill(id, VanillaSkill.Pickpocket, pickpocket)
                    .SetSkill(id, VanillaSkill.Stealth, stealth)
                    .SetAttribute(id, VanillaAttribute.Dexterity, pickpocket + stealth)
                    .SetAttribute(id, VanillaAttribute.Perception, perception);

                return id;
            }
        }
    }
}
