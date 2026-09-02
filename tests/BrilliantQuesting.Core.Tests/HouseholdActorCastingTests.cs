using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-123. The player's own pets, residents and companions are people a scene may be about.
    ///
    /// Two halves. The first is admission: a chicken the player keeps could not previously be cast
    /// into anything, because social agency was a filter on the whole pool rather than a
    /// requirement of the roles that need somebody to speak. The second, and the larger one, is
    /// what happens afterwards - the pet is sold, the resident marries out of the settlement, the
    /// companion is killed - and the rule the whole step rests on is that household membership was
    /// never a fact this mod wrote down, so there is nothing to go stale.
    /// </summary>
    public class HouseholdActorCastingTests
    {
        // -- A. who the household is ---------------------------------------------------------

        [Fact]
        public void TheHouseholdIsTheHomeRollAndThePartyTogether()
        {
            Household house = Household.Create();

            PlayerHousehold read = PlayerHousehold.Read(house.World, house.Vanilla);

            Assert.Equal(HouseholdBond.Resident, read.BondOf(house.Resident));
            Assert.Equal(HouseholdBond.Companion, read.BondOf(house.Pet));
            Assert.Equal(HouseholdBond.None, read.BondOf(house.Lab.Situation.WitnessId));
            Assert.Equal(HouseholdBond.None, read.BondOf(house.Lab.Player));
            Assert.True(read.ResidentsRead);
            Assert.True(read.CompanionsRead);
            Assert.False(read.IsUnread);
        }

        [Fact]
        public void SomebodyWhoBothLivesHereAndTravelsWithThePlayerIsOneMemberAndAResident()
        {
            Household house = Household.Create();
            house.MoveOntoTheRoll(house.Pet, "Kettle");

            PlayerHousehold read = PlayerHousehold.Read(house.World, house.Vanilla);

            Assert.Equal(2, read.Count);
            HouseholdMember pet = read.Find(house.Pet);
            Assert.Equal(HouseholdBond.Resident, pet.Bond);
            Assert.Contains("lives on the player's own land", pet.Because);
            Assert.Contains("keeps the player company", pet.Because);
        }

        [Fact]
        public void AHouseholdNobodyCouldReadSaysSoRatherThanReportingNobody()
        {
            // A player with no Home and a build that cannot list a party is not a player who lives
            // alone. The difference is the whole of D017 at this scale: the first is a silence and
            // the second is a measurement, and only the second may be acted on.
            Household house = Household.Create();
            house.Vanilla.SetHome(null);
            house.Vanilla.SetCapability(VanillaCapability.ReadPlayerCompanions, false);

            PlayerHousehold read = PlayerHousehold.Read(house.World, house.Vanilla);

            Assert.True(read.IsUnread);
            Assert.Empty(read.Members);
            Assert.Contains("household unread", read.Describe());

            // And a Home with nobody on it, read successfully, is the measurement.
            house.Vanilla.SetHome(new HomeStateBuilder(Household.HomeZone, "Home").WithCapacity(4).Build());
            PlayerHousehold empty = PlayerHousehold.Read(house.World, house.Vanilla);
            Assert.False(empty.IsUnread);
            Assert.Empty(empty.Members);
        }

        [Fact]
        public void AHouseholdIsReadFromTheGameAndNeverFromTheSave()
        {
            // The save carries who was cast, never who was household. A world reloaded beside a
            // game that has since sold the pet reports a household without it, with no migration,
            // no cleanup pass and nothing to forget.
            Household house = Household.Create();
            Assert.True(PlayerHousehold.Read(house.World, house.Vanilla).Includes(house.Pet));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(house.World));
            house.Sell(house.Pet);

            Assert.False(PlayerHousehold.Read(reloaded, house.Vanilla).Includes(house.Pet));
            Assert.True(reloaded.Registry.Npcs.ContainsKey(house.Pet));
        }

        [Fact]
        public void KeepingThePlayerCompanyIsAGroundOfFamiliarityLikeLivingOnTheirLand()
        {
            // BQ-114 already weighted the household; BQ-123 is what puts a companion in it. The
            // weight is the same one, not a second scale beside it.
            Household house = Household.Create();

            FamiliarityReading reading = PlayerFamiliarity.Read(house.World, house.Vanilla).Of(house.Pet);

            Assert.True(reading.IsKnown);
            Assert.Contains("keeps the player company", reading.Because);
            Assert.Equal(PlayerFamiliarity.HouseholdWeight, reading.Score);
        }

        // -- B. admission to casting ---------------------------------------------------------

        [Fact]
        public void ASceneCanBeAboutThePlayersOwnPet()
        {
            // The done-when's first half. Nothing about this cast is different from any other -
            // the role states a requirement, the pet meets it, and the note says why.
            Household house = Household.Create();
            StoryletDefinition definition = Minimal("storylet.test.household_subject");
            definition.RequiredRoles.Add(new StoryletRole("at_risk", StoryletRoleSource.HouseholdMemberHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(house.Pet, opportunity.RoleBindings["at_risk"]);
            Assert.Contains(
                opportunity.CastingNotes,
                note => note.Contains("Kettle") && note.Contains("belongs to the player's household"));
        }

        [Fact]
        public void APetIsNeverCastIntoARoleThatHasToSpeak()
        {
            // The pet saw the whole thing. It is still not the accuser, and the scene refuses
            // rather than casting an animal into testimony: social agency did not stop being a
            // requirement, it stopped being a filter on the pool.
            Household house = Household.Create();
            house.SendTheTownAway();
            house.World.Knowledge.Teach(
                house.Pet,
                house.Lab.Situation.TheftFactId,
                KnowledgeSource.Witnessed,
                1.0,
                house.Vanilla.Now,
                false);

            StoryletDefinition definition = Minimal("storylet.test.accuser");
            definition.RequiredRoles.Add(new StoryletRole("accuser", StoryletRoleSource.AnyoneWhoKnowsFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("accuser", opportunity.RefusalReason);
            Assert.DoesNotContain(house.Pet, opportunity.RoleBindings.Values);
        }

        [Fact]
        public void AResidentSpeaksLikeAnybodyElseAndIsRecognisedBeforeAStranger()
        {
            // The other side of the same rule: a resident is a person, so nothing about the
            // household admits or excludes them from a speaking role. What being of the household
            // does is put them first in the order the role searches (BQ-114), which is a
            // preference and not a qualification.
            Household house = Household.Create();
            house.SendTheTownAway();
            EntityId stranger = house.Townsperson("stranger", "Halden");
            foreach (EntityId knower in new[] { house.Resident, stranger })
            {
                house.World.Knowledge.Teach(
                    knower,
                    house.Lab.Situation.TheftFactId,
                    KnowledgeSource.Witnessed,
                    1.0,
                    house.Vanilla.Now,
                    false);
            }

            StoryletDefinition definition = Minimal("storylet.test.witness");
            definition.RequiredRoles.Add(new StoryletRole("witness", StoryletRoleSource.AnyoneWhoKnowsFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(house.Resident, opportunity.RoleBindings["witness"]);
        }

        [Fact]
        public void SomebodyElsesAnimalIsNotOfThePlayersHousehold()
        {
            // Admission is membership, not species. A stray in the same square is the same kind of
            // actor as the player's pet and is nobody's to lose.
            Household house = Household.Create();
            EntityId stray = house.Animal("stray", "Stray Dog", companion: false);
            house.Sell(house.Pet);

            StoryletDefinition definition = Minimal("storylet.test.household_subject");
            definition.RequiredRoles.Add(new StoryletRole("at_risk", StoryletRoleSource.HouseholdMemberHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(house.Resident, opportunity.RoleBindings["at_risk"]);
            Assert.DoesNotContain(stray, opportunity.RoleBindings.Values);
        }

        [Fact]
        public void AHouseholdRoleRefusesWhenNobodyOfTheHouseholdIsHere()
        {
            Household house = Household.Create();
            EntityId elsewhere = house.World.NewId("zone");
            house.Vanilla.SetZone(house.Pet, elsewhere);
            house.Vanilla.SetZone(house.Resident, elsewhere);

            StoryletDefinition definition = Minimal("storylet.test.household_subject");
            definition.RequiredRoles.Add(new StoryletRole("at_risk", StoryletRoleSource.HouseholdMemberHere));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("at_risk", opportunity.RefusalReason);

            // Away is not gone: they are still of this household, and the scene simply is not here.
            Assert.True(PlayerHousehold.Read(house.World, house.Vanilla).Includes(house.Pet));
        }

        [Fact]
        public void NobodyBecomesAHouseholdActorByBeingCastAsOne()
        {
            // D026 unchanged. Casting writes to the firing and to nothing else, so the next scene
            // asks the game again rather than reading back what this one decided.
            Household house = Household.Create();
            StoryletDefinition definition = Minimal("storylet.test.household_subject");
            definition.RequiredRoles.Add(new StoryletRole("at_risk", StoryletRoleSource.HouseholdMemberHere));
            StoryletEngine engine = new StoryletEngine();
            engine.Register(definition);

            StoryletOpportunity opportunity = Assert.Single(engine.Find(house.Casting()));
            engine.Fire(opportunity, house.Lab.Situation.Thread, house.Vanilla.Now);

            NarrativeNpc pet = house.World.Registry.GetNpc(house.Pet);
            Assert.Empty(pet.Roles);
            Assert.Empty(pet.Goals);
            Assert.Equal(string.Empty, pet.Occupation ?? string.Empty);
        }

        // -- C. surviving the lifecycle ------------------------------------------------------

        [Fact]
        public void APetThatIsSoldLeavesTheCastAndTheSceneItPlayedStillReads()
        {
            // The done-when's second half, in the shape the game actually produces it: the pet is
            // gone from the party and the adapter can no longer resolve it at all. What must not
            // happen is a scene the player already played turning into a broken reference.
            Household house = Household.Create();
            StoryletFiring firing = house.PlayAHouseholdScene();
            Assert.Equal(house.Pet, firing.RoleBindings["at_risk"]);

            house.Sell(house.Pet);

            Assert.False(PlayerHousehold.Read(house.World, house.Vanilla).Includes(house.Pet));
            Assert.False(house.CanCastAHouseholdSceneOn(house.Pet));

            NarrativeThread reloaded = ReloadTheOnlyThread(house.World);
            Assert.NotEqual(ThreadState.Quarantined, reloaded.State);
            Assert.Equal(house.Pet, Assert.Single(reloaded.StoryletFirings).RoleBindings["at_risk"]);
        }

        [Fact]
        public void AResidentMarriedOffLeavesTheHouseholdWithoutLeavingTheWorld()
        {
            // Married into somebody else's household: off the player's roll, still alive, still
            // standing in the same square, still a person a scene may be about for other reasons.
            Household house = Household.Create();
            house.MarryOff(house.Resident);

            PlayerHousehold read = PlayerHousehold.Read(house.World, house.Vanilla);
            Assert.False(read.Includes(house.Resident));
            Assert.False(read.IsUnread);
            Assert.False(house.CanCastAHouseholdSceneOn(house.Resident));

            // And they are not a ghost: an ordinary speaking role still finds them.
            house.SendTheTownAway();
            house.World.Knowledge.Teach(
                house.Resident,
                house.Lab.Situation.TheftFactId,
                KnowledgeSource.Witnessed,
                1.0,
                house.Vanilla.Now,
                false);

            StoryletDefinition definition = Minimal("storylet.test.witness");
            definition.RequiredRoles.Add(new StoryletRole("witness", StoryletRoleSource.AnyoneWhoKnowsFocus));
            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, house.Casting());

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(house.Resident, opportunity.RoleBindings["witness"]);
        }

        [Fact]
        public void AKilledCompanionIsNoLongerCastAndTheSceneTheyPlayedSurvives()
        {
            Household house = Household.Create();
            StoryletFiring firing = house.PlayAHouseholdScene();
            Assert.Equal(house.Pet, firing.RoleBindings["at_risk"]);

            house.Vanilla.Kill(house.Pet);

            Assert.False(PlayerHousehold.Read(house.World, house.Vanilla).Includes(house.Pet));
            Assert.False(house.CanCastAHouseholdSceneOn(house.Pet));

            NarrativeThread reloaded = ReloadTheOnlyThread(house.World);
            Assert.NotEqual(ThreadState.Quarantined, reloaded.State);
            Assert.Equal(house.Pet, Assert.Single(reloaded.StoryletFirings).RoleBindings["at_risk"]);
        }

        [Fact]
        public void AResidentTheGameNoLongerAnswersForIsNotReportedAsLivingHere()
        {
            // Removal rather than death: the Home roll still lists them and the character is gone.
            // The mod must not go on describing somebody it cannot resolve as part of the player's
            // home, and "unknown" is not "alive" (D017).
            Household house = Household.Create();
            house.Vanilla.Forget(house.Resident);

            Assert.Equal(VanillaLifeState.Unknown, house.Vanilla.GetLifeState(house.Resident));
            PlayerHousehold read = PlayerHousehold.Read(house.World, house.Vanilla);
            Assert.False(read.Includes(house.Resident));
            Assert.True(read.Includes(house.Pet));
        }

        [Fact]
        public void EveryHouseholdLifecycleLeavesEveryRoleHoldingSomebodyTheSaveKnows()
        {
            // The realistic run: a scene is played with the household in it, then the household
            // turns over completely - one sold, one married off, one killed - and the save is
            // reloaded. Save integrity quarantines a thread whose firing names a role holder the
            // registry has never heard of, so this is the test that a lifecycle change cannot cost
            // the player a situation they were in the middle of.
            Household house = Household.Create();
            EntityId goat = house.Animal("goat", "Nettle", companion: true);

            StoryletFiring firing = house.PlayAHouseholdScene("bereaved", "at_risk", "also_at_risk");
            Assert.Equal(3, firing.RoleBindings.Count);
            Assert.Equal(
                new[] { goat, house.Pet, house.Resident }.OrderBy(id => id.Value).ToArray(),
                firing.RoleBindings.Values.OrderBy(id => id.Value).ToArray());

            house.Sell(house.Pet);
            house.MarryOff(house.Resident);
            house.Vanilla.Kill(goat);

            PlayerHousehold after = PlayerHousehold.Read(house.World, house.Vanilla);
            Assert.Empty(after.Members);
            Assert.False(after.IsUnread);
            Assert.False(house.CanCastAHouseholdScene());

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(house.World));
            NarrativeThread thread = Assert.Single(reloaded.Threads);
            Assert.NotEqual(ThreadState.Quarantined, thread.State);
            foreach (StoryletFiring played in thread.StoryletFirings)
            {
                foreach (KeyValuePair<string, EntityId> role in played.RoleBindings)
                {
                    Assert.True(
                        reloaded.Registry.Npcs.ContainsKey(role.Value),
                        played.StoryletId + " role " + role.Key + " is not an actor the save knows");
                }
            }
        }

        // -- helpers -------------------------------------------------------------------------

        private static StoryletDefinition Minimal(string id)
        {
            StoryletDefinition definition = new StoryletDefinition(id);
            definition.Beats.Add(new StoryletBeat("open"));
            return definition;
        }

        private static NarrativeThread ReloadTheOnlyThread(NarrativeWorldState world)
        {
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            return Assert.Single(reloaded.Threads);
        }

        /// <summary>
        /// One player, one Home, one resident on its roll and one chicken in the party, all
        /// standing in the same square as a theft that has already happened.
        ///
        /// Built on <see cref="TheftLaboratory"/> so the scene the household is cast into is a real
        /// one with a real focus fact, rather than a thread invented to make the assertion pass.
        /// </summary>
        private sealed class Household
        {
            internal static readonly EntityId HomeZone = EntityId.Parse("zone_home");

            private readonly List<HomeResident> _roll = new List<HomeResident>();

            private Household(TheftLaboratory lab)
            {
                Lab = lab;
            }

            public TheftLaboratory Lab { get; }

            public NarrativeWorldState World => Lab.World;

            public SandboxVanillaState Vanilla => Lab.Vanilla;

            /// <summary>A person on the player's Home roll.</summary>
            public EntityId Resident { get; private set; }

            /// <summary>An animal in the player's party: a race and nothing else, which is correct.</summary>
            public EntityId Pet { get; private set; }

            public static Household Create()
            {
                Household house = new Household(TheftLaboratory.Create());
                house.Resident = house.Townsperson("resident", "Nell");
                house.MoveOntoTheRoll(house.Resident, "Nell");
                house.Pet = house.Animal("pet", "Kettle", companion: true);
                return house;
            }

            public StoryletCastingContext Casting()
            {
                return new StoryletCastingContext(World, Vanilla, Lab.Situation.Thread, Lab.Situation.TheftFactId);
            }

            /// <summary>An ordinary person of the town, standing where the scene is.</summary>
            public EntityId Townsperson(string tag, string name)
            {
                EntityId id = EntityId.Parse("npc_" + tag);
                Vanilla.Define(id, zone: Lab.Zone);
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }

            /// <summary>
            /// A pet: the game says what species it is and nothing else, which is a complete
            /// answer for an animal rather than a gap. It cannot testify, and
            /// <see cref="NarrativeActorClass"/> is untouched - a companion is not more or less
            /// protected for being a chicken.
            /// </summary>
            public EntityId Animal(string tag, string name, bool companion)
            {
                EntityId id = Townsperson(tag, name);
                Vanilla.SetActorKind(id, NarrativeActorKind.Animal);
                Vanilla.SetSocialAgency(id, SocialAgency.None);
                Vanilla.SetCharacterIdentity(id, new CharacterIdentityBuilder(id)
                    .WithRace("chicken", "Chicken")
                    .WithCharacterArchetype("pet", "Pet")
                    .Build());
                if (companion)
                {
                    Vanilla.SetCompanion(id);
                }

                return id;
            }

            public void MoveOntoTheRoll(EntityId chara, string name)
            {
                _roll.Add(new HomeResident(chara, name));
                RebuildHome();
            }

            /// <summary>Off the player's roll, into somebody else's household. Still alive.</summary>
            public void MarryOff(EntityId chara)
            {
                _roll.RemoveAll(resident => resident.Id == chara);
                RebuildHome();
            }

            /// <summary>
            /// Out of the party and out of the game's reach - which is what selling a pet looks
            /// like from this side of the seam, and is the same shape as removal.
            /// </summary>
            public void Sell(EntityId chara)
            {
                Vanilla.SetCompanion(chara, false);
                _roll.RemoveAll(resident => resident.Id == chara);
                RebuildHome();
                Vanilla.Forget(chara);
            }

            /// <summary>
            /// Everybody the theft is about goes elsewhere, so the pool the roles search is the
            /// household and the town rather than the thread's own participants.
            /// </summary>
            public void SendTheTownAway()
            {
                EntityId elsewhere = World.NewId("zone");
                for (int i = 0; i < Lab.Situation.Thread.ParticipantIds.Count; i++)
                {
                    Vanilla.SetZone(Lab.Situation.Thread.ParticipantIds[i], elsewhere);
                }
            }

            /// <summary>
            /// Casts and fires one household scene, and hands back what it recorded. One role per
            /// name given, all of them asking for a member of the player's household.
            /// </summary>
            public StoryletFiring PlayAHouseholdScene(params string[] roleIds)
            {
                StoryletDefinition definition = HouseholdStorylet(
                    "storylet.test.household_scene",
                    roleIds.Length == 0 ? new[] { "at_risk" } : roleIds);
                StoryletEngine engine = new StoryletEngine();
                engine.Register(definition);

                StoryletOpportunity opportunity = Assert.Single(engine.Find(Casting()));
                return engine.Fire(opportunity, Lab.Situation.Thread, Vanilla.Now);
            }

            /// <summary>Whether a household role would still find this particular actor.</summary>
            public bool CanCastAHouseholdSceneOn(EntityId actor)
            {
                StoryletOpportunity opportunity = StoryletEngine.Evaluate(
                    HouseholdStorylet("storylet.test.household_probe", "at_risk"),
                    Casting());
                return opportunity.IsAvailable && opportunity.RoleBindings.Values.Contains(actor);
            }

            /// <summary>Whether a household role would find anybody at all.</summary>
            public bool CanCastAHouseholdScene()
            {
                return StoryletEngine
                    .Evaluate(HouseholdStorylet("storylet.test.household_probe", "at_risk"), Casting())
                    .IsAvailable;
            }

            private static StoryletDefinition HouseholdStorylet(string id, params string[] roleIds)
            {
                StoryletDefinition definition = Minimal(id);
                for (int i = 0; i < roleIds.Length; i++)
                {
                    definition.RequiredRoles.Add(
                        new StoryletRole(roleIds[i], StoryletRoleSource.HouseholdMemberHere));
                }

                return definition;
            }

            private void RebuildHome()
            {
                HomeStateBuilder home = new HomeStateBuilder(HomeZone, "Home").WithCapacity(6);
                for (int i = 0; i < _roll.Count; i++)
                {
                    home.AddResident(_roll[i].Id, _roll[i].Name, _roll[i].Job);
                }

                Vanilla.SetHome(home.Build());
            }
        }
    }
}
