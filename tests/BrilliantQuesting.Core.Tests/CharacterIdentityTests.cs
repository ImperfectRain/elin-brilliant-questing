using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-144: the game can be asked who somebody is, and what it did not answer says so.
    ///
    /// These drive <see cref="SandboxVanillaState"/>, the reference implementation of the seam.
    /// The live adapter cannot be exercised without a running game, so the rules pinned here are
    /// the rules it is written to honour: six facets that fail independently, an unread facet that
    /// is unknown rather than a plausible default, and an observation that grants nothing and
    /// registers nobody.
    /// </summary>
    public class CharacterIdentityTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Town = EntityId.Parse("zone_town");

        private static SandboxVanillaState Town1()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Town);
            vanilla.Define(Shopkeeper, zone: Town);
            vanilla.Define(Guard, zone: Town);

            vanilla.SetCharacterIdentity(Shopkeeper, new CharacterIdentityBuilder(Shopkeeper)
                .WithCharacterArchetype("bunny", "Bunny")
                .WithRace("fairy", "Fairy")
                .WithWork("shopkeeper", "Shopkeeper")
                .AddHobby("cooking")
                .AddHobby("gaming")
                .WithService("TraitShopGeneral", null, ServiceAvailability.Offered)
                .WithInstitutionsRead()
                .Build());

            vanilla.SetCharacterIdentity(Guard, new CharacterIdentityBuilder(Guard)
                .WithCharacterArchetype("guard", "Town Guard")
                .WithRace("norland", "Norlander")
                .WithWork("soldier")
                .WithHobbiesRead()
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build());

            return vanilla;
        }

        // -- the six facets --------------------------------------------------------------------

        [Fact]
        public void EachFacetIsItsOwnFieldCarryingTheGamesOwnId()
        {
            CharacterIdentity identity = Town1().GetCharacterIdentity(Shopkeeper);

            Assert.Equal(Shopkeeper, identity.Actor);
            Assert.Equal("bunny", identity.CharacterArchetype.VanillaId);
            Assert.Equal("Bunny", identity.CharacterArchetype.DisplayName);
            Assert.Equal("fairy", identity.Race.VanillaId);
            Assert.Equal("shopkeeper", identity.Work.VanillaId);
            Assert.Equal(2, identity.Hobbies.Count);
            Assert.Equal("cooking", identity.Hobbies[0].VanillaId);
            Assert.Equal("TraitShopGeneral", identity.Service.Kind.VanillaId);
            Assert.Equal(ServiceAvailability.Offered, identity.Service.Availability);
            Assert.Empty(identity.UnreadFacets);
        }

        /// <summary>
        /// The facets are separate fields, not a merged tag list: a shopkeeper who staffs a guild
        /// has to stay distinguishable from somebody whose hobby is shopping.
        /// </summary>
        [Fact]
        public void WorkServiceAndInstitutionAreDistinguishable()
        {
            CharacterIdentity identity = Town1().GetCharacterIdentity(Guard);

            Assert.Equal("soldier", identity.Work.VanillaId);
            Assert.False(identity.Service.IsKnown);
            Assert.Single(identity.Institutions);
            Assert.Equal("TraitGuard", identity.Institutions[0].Role.VanillaId);
            Assert.Equal("city_of_yowyn", identity.Institutions[0].Body.VanillaId);
            Assert.False(identity.Institutions[0].RankKnown);
        }

        /// <summary>A rank nobody read is not a rank of zero.</summary>
        [Fact]
        public void AnUnreadRankIsNotZero()
        {
            InstitutionalRole unread = new InstitutionalRole(
                IdentityFacet.FromVanilla("fighters_guild"),
                IdentityFacet.FromVanilla("TraitGuildPersonnel"));
            InstitutionalRole read = new InstitutionalRole(
                IdentityFacet.FromVanilla("fighters_guild"),
                IdentityFacet.FromVanilla("TraitGuildPersonnel"),
                0,
                true);

            Assert.False(unread.RankKnown);
            Assert.True(read.RankKnown);
            Assert.Equal(0, read.Rank);
            Assert.Contains("rank 0", read.Describe());
            Assert.DoesNotContain("rank", unread.Describe());
        }

        // -- what unknown means ----------------------------------------------------------------

        [Fact]
        public void AnUnreadFacetIsUnknownRatherThanAnEmptyStringOrADefaultJob()
        {
            CharacterIdentity identity = new CharacterIdentityBuilder(Stranger)
                .WithRace("fairy")
                .Build();

            Assert.False(identity.Work.IsKnown);
            Assert.Equal(string.Empty, identity.Work.VanillaId);
            Assert.Equal("?", identity.Work.Describe());
            Assert.False(identity.IsKnown(IdentityFacetKind.Work));
            Assert.DoesNotContain("local", identity.Describe());
        }

        /// <summary>
        /// A build that answers a facet with nothing has not answered it. There is no way to spell
        /// "unread" that reads like a measurement.
        /// </summary>
        [Fact]
        public void AnEmptyIdIsNeverAKnownFacet()
        {
            Assert.False(IdentityFacet.FromVanilla(string.Empty).IsKnown);
            Assert.False(IdentityFacet.FromVanilla(null).IsKnown);
            Assert.False(new CharacterIdentityBuilder(Stranger).WithWork(string.Empty).Build().Work.IsKnown);
            Assert.False(new ServiceRole(IdentityFacet.Unknown, ServiceAvailability.Offered).IsKnown);
        }

        /// <summary>An unknown service kind cannot be reported as a service that is on offer.</summary>
        [Fact]
        public void AnUnknownServiceIsNotAnOfferedOne()
        {
            ServiceRole service = new ServiceRole(IdentityFacet.Unknown, ServiceAvailability.Offered);

            Assert.Equal(ServiceAvailability.Unknown, service.Availability);
        }

        /// <summary>
        /// An empty hobby list is two different answers, and only one of them is a fact: the sheet
        /// listing none, and this build not having the column at all.
        /// </summary>
        [Fact]
        public void NoHobbiesListedIsNotTheSameAsNoHobbyColumn()
        {
            CharacterIdentity read = new CharacterIdentityBuilder(Stranger).WithHobbiesRead().Build();
            CharacterIdentity unread = new CharacterIdentityBuilder(Stranger).Build();

            Assert.True(read.IsKnown(IdentityFacetKind.Hobby));
            Assert.Empty(read.Hobbies);
            Assert.False(unread.IsKnown(IdentityFacetKind.Hobby));
            Assert.Contains(IdentityFacetKind.Hobby, unread.UnreadFacets);
        }

        // -- degradation -----------------------------------------------------------------------

        [Fact]
        public void AnUnavailableFacetDegradesOnlyItself()
        {
            CharacterIdentity identity = new CharacterIdentityBuilder(Shopkeeper)
                .WithCharacterArchetype("bunny")
                .WithWork("shopkeeper")
                .AddHobby("cooking")
                .WithService("TraitShopGeneral")
                .AddInstitution("merchants_guild", "TraitGuildPersonnel")
                .Build();

            Assert.False(identity.Race.IsKnown);
            Assert.Equal(new[] { IdentityFacetKind.Race }, identity.UnreadFacets);
            Assert.True(identity.CharacterArchetype.IsKnown);
            Assert.True(identity.Work.IsKnown);
            Assert.True(identity.Service.IsKnown);
            Assert.True(identity.InstitutionsRead);
            Assert.Single(identity.Hobbies);
        }

        [Fact]
        public void AnActorTheBuildCannotResolveIsUnknownOnEveryFacetAndStillAnObservation()
        {
            CharacterIdentity identity = Town1().GetCharacterIdentity(Stranger);

            Assert.Equal(Stranger, identity.Actor);
            Assert.True(identity.IsFullyUnknown);
            Assert.Equal(6, identity.UnreadFacets.Count);
            Assert.Equal(
                "character archetype ?, race ?, work ?, hobby ?, service ?, institution ?",
                identity.Describe());
        }

        [Fact]
        public void NobodyInParticularIsAnObservationRatherThanACrash()
        {
            CharacterIdentity identity = Town1().GetCharacterIdentity(EntityId.None);

            Assert.True(identity.IsFullyUnknown);
        }

        /// <summary>
        /// A build that cannot read identity at all loses identity and nothing else - and loses it
        /// for everybody, rather than reporting the facets it happened to cache.
        /// </summary>
        [Fact]
        public void AnUnsupportedBuildKnowsNothingAboutAnybody()
        {
            SandboxVanillaState vanilla = Town1();
            vanilla.SetCapability(VanillaCapability.ReadCharacterIdentity, false);

            Assert.True(vanilla.GetCharacterIdentity(Shopkeeper).IsFullyUnknown);
            Assert.True(vanilla.GetCharacterIdentity(Guard).IsFullyUnknown);

            vanilla.SetCapability(VanillaCapability.ReadCharacterIdentity, true);
            Assert.False(vanilla.GetCharacterIdentity(Shopkeeper).IsFullyUnknown);
        }

        /// <summary>
        /// The read observes and does nothing else. Asking about somebody the world has never
        /// heard of must not bring them into existence - the live adapter registers nobody either,
        /// and this is the implementation where that can be seen.
        /// </summary>
        [Fact]
        public void ReadingAnIdentityRegistersNobody()
        {
            SandboxVanillaState vanilla = Town1();

            vanilla.GetCharacterIdentity(Stranger);

            Assert.DoesNotContain(Stranger, vanilla.GetCharactersInZone(EntityId.None));
            Assert.DoesNotContain(Stranger, vanilla.GetCharactersInZone(Town));
        }

        /// <summary>
        /// Identity is a costume and never a permission. Somebody the game will not let the mod
        /// touch stays untouchable however their facets read, and somebody whose facets are all
        /// unknown is still reachable on the strength of their class.
        /// </summary>
        [Fact]
        public void IdentityDoesNotDecideWhatTheModMayDoToSomebody()
        {
            SandboxVanillaState vanilla = Town1();
            vanilla.SetActorClass(Shopkeeper, NarrativeActorClass.StoryCritical);
            vanilla.SetActorClass(Stranger, NarrativeActorClass.OrdinaryCitizen);

            Assert.False(vanilla.TryAdmitResident(Shopkeeper));
            Assert.True(vanilla.GetCharacterIdentity(Stranger).IsFullyUnknown);
            Assert.True(MutationPolicies.Permits(vanilla.GetActorClass(Stranger), MutationKind.Relocate));
        }

        // -- the intake it replaces ------------------------------------------------------------

        /// <summary>
        /// The institutional facet is the only part of the observation that lands in the world
        /// model, and it lands through one path: no second write into
        /// <see cref="NarrativeNpc.Roles"/> for a live character.
        /// </summary>
        [Fact]
        public void ObservedStandingBecomesAuthorityAndIsWithdrawnWhenItStops()
        {
            NarrativeNpc npc = new NarrativeNpc(Guard, "Warden");

            Assert.True(AuthorityPolicy.Reconcile(npc, new[] { AuthorityPolicy.GuardRole }, true));
            Assert.Equal(AuthorityRole.Guard, AuthorityPolicy.RoleOf(npc));

            Assert.True(AuthorityPolicy.Reconcile(npc, new string[0], true));
            Assert.Equal(AuthorityRole.None, AuthorityPolicy.RoleOf(npc));
        }

        /// <summary>
        /// Unknown withdraws nothing. Not being able to read the institutional facet - an actor
        /// off-map, a build that stopped answering - is not the game saying somebody was
        /// dismissed.
        /// </summary>
        [Fact]
        public void AnUnreadInstitutionalFacetChangesNothing()
        {
            NarrativeNpc npc = new NarrativeNpc(Guard, "Warden");
            AuthorityPolicy.Reconcile(npc, new[] { AuthorityPolicy.GuardRole }, true);

            Assert.False(AuthorityPolicy.Reconcile(npc, new string[0], false));
            Assert.Equal(AuthorityRole.Guard, AuthorityPolicy.RoleOf(npc));
        }

        /// <summary>Standing a situation or an organization granted is not the adapter's to take.</summary>
        [Fact]
        public void ReconcilingLeavesRolesThisPolicyDoesNotOwnAlone()
        {
            NarrativeNpc npc = new NarrativeNpc(Shopkeeper, "Rina");
            npc.Roles.Add("fence");

            AuthorityPolicy.Reconcile(npc, new string[0], true);

            Assert.Contains("fence", npc.Roles);
        }
    }
}
