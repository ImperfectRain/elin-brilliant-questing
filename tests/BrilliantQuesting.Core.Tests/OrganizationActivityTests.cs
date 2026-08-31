using System.Linq;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>BQ-053. Generated organizations can pursue goals without player involvement.</summary>
    public class OrganizationActivityTests
    {
        private static readonly EntityId Hall = EntityId.Parse("site_hall");
        private static readonly EntityId Crew = EntityId.Parse("org_crew");
        private static readonly EntityId Leader = EntityId.Parse("npc_leader");
        private static readonly EntityId Recruit = EntityId.Parse("npc_recruit");

        [Fact]
        public void AnOrganizationCanChangeMembershipBecauseItActedOffScreen()
        {
            NarrativeWorldState world = World();
            Organization organization = CrewRecord(world);
            organization.Wealth = 20;
            organization.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, Hall, 80));

            int acted = new OrganizationActivity(world).Advance(GameTime.FromDays(1));

            Assert.Equal(1, acted);
            Assert.Contains(Recruit, organization.MemberIds);
            Assert.Contains(Crew, world.Registry.GetNpc(Recruit).OrganizationIds);
            Assert.Equal(15, organization.Wealth);

            WorldEvent action = Assert.Single(world.Ledger.Events, e => e.Type == WorldEventType.OrganizationActed);
            Assert.Equal(Leader, action.Actor);
            Assert.Equal(Crew, action.Target);
            Assert.Contains(Recruit, action.Related);
            Assert.Contains(OrganizationActivity.ExpandMembership, action.Tags);
        }

        [Fact]
        public void AnOrganizationCanChangeWealthBecauseItActedOffScreen()
        {
            NarrativeWorldState world = World();
            Organization organization = CrewRecord(world);
            organization.Wealth = 8;
            organization.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, Crew, 50));

            new OrganizationActivity(world).Advance(GameTime.FromDays(2));

            Assert.Equal(12, organization.Wealth);
            WorldEvent action = Assert.Single(world.Ledger.Events, e => e.Type == WorldEventType.OrganizationActed);
            Assert.Contains("wealth_changed", action.Tags);
        }

        [Fact]
        public void TheActionClockAndGoalsSurviveSaveLoadWithoutRepeatingTheSameDay()
        {
            NarrativeWorldState world = World();
            Organization organization = CrewRecord(world);
            organization.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, Crew, 90));

            OrganizationActivity activity = new OrganizationActivity(world);
            activity.Advance(GameTime.FromDays(3));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            OrganizationActivity afterLoad = new OrganizationActivity(reloaded);
            int sameDay = afterLoad.Advance(GameTime.FromDays(3));
            int nextDay = afterLoad.Advance(GameTime.FromDays(4));

            Organization restored = reloaded.Registry.GetOrganization(Crew);
            Assert.Equal(0, sameDay);
            Assert.Equal(1, nextDay);
            Assert.Equal(GameTime.FromDays(4), restored.LastActedAt);
            Assert.Equal(2, reloaded.Ledger.Events.Count(e => e.Type == WorldEventType.OrganizationActed));
            Assert.Single(restored.Goals);
            Assert.True(restored.Goals[0].Progress > 0);
        }

        [Fact]
        public void OlderSavesWithoutOrganizationGoalsStillLoad()
        {
            NarrativeWorldState world = World();
            CrewRecord(world);
            string old = WorldStateSerializer.Save(world).Replace("\"goals\":[],", string.Empty);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(old);

            Organization restored = reloaded.Registry.GetOrganization(Crew);
            Assert.Empty(restored.Goals);
            Assert.Equal(GameTime.Zero, restored.LastActedAt);
        }

        private static NarrativeWorldState World()
        {
            NarrativeWorldState world = new NarrativeWorldState(53);
            world.Registry.Add(new NarrativeSite(Hall, "Candle Hall", "guild_hall"));
            world.Registry.Add(new NarrativeNpc(Leader, "Mara") { HomeSiteId = Hall });
            world.Registry.Add(new NarrativeNpc(Recruit, "Tovin") { HomeSiteId = Hall });
            return world;
        }

        private static Organization CrewRecord(NarrativeWorldState world)
        {
            Organization organization = new Organization(Crew, "Candle Crew", "merchant_association")
            {
                LeaderId = Leader
            };

            organization.MemberIds.Add(Leader);
            organization.SiteIds.Add(Hall);
            world.Registry.GetNpc(Leader).OrganizationIds.Add(Crew);
            world.Registry.Add(organization);
            return organization;
        }
    }
}
