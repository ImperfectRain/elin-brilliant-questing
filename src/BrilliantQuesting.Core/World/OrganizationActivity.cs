using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Deterministic off-screen action for generated organizations. This mutates only BQ-owned
    /// organization records; vanilla guild rank, membership and economy remain read-only inputs for
    /// their own systems.
    /// </summary>
    public sealed class OrganizationActivity
    {
        public const string ExpandMembership = "expand_membership";
        public const string BuildReserves = "build_reserves";
        public const string ProtectHolding = "protect_holding";
        public const string RaidOrganization = "raid_organization";

        private readonly NarrativeWorldState _world;

        public OrganizationActivity(NarrativeWorldState world)
        {
            _world = world;
        }

        public int Advance(GameTime now)
        {
            int acted = 0;
            foreach (Organization organization in _world.Registry.Organizations.Values)
            {
                if (CanAct(organization, now) && Act(organization, now))
                {
                    acted++;
                }
            }

            return acted;
        }

        private static bool CanAct(Organization organization, GameTime now)
        {
            return organization != null
                   && !organization.LeaderId.IsNone
                   && now.TotalDays > organization.LastActedAt.TotalDays
                   && organization.Goals.Count > 0;
        }

        private bool Act(Organization organization, GameTime now)
        {
            OrganizationGoal goal = ChooseGoal(organization);
            if (goal == null)
            {
                organization.LastActedAt = now;
                return false;
            }

            bool changed;
            if (goal.Kind == ExpandMembership)
            {
                changed = Expand(organization, goal, now);
            }
            else if (goal.Kind == ProtectHolding)
            {
                changed = Fortify(organization, goal, now);
            }
            else if (goal.Kind == RaidOrganization)
            {
                changed = Raid(organization, goal, now);
            }
            else
            {
                changed = BuildWealth(organization, goal, now);
            }

            organization.LastActedAt = now;
            return changed;
        }

        private static OrganizationGoal ChooseGoal(Organization organization)
        {
            OrganizationGoal best = null;
            for (int i = 0; i < organization.Goals.Count; i++)
            {
                OrganizationGoal goal = organization.Goals[i];
                if (goal.Satisfied)
                {
                    continue;
                }

                if (best == null || goal.Weight > best.Weight)
                {
                    best = goal;
                }
            }

            return best;
        }

        private bool Expand(Organization organization, OrganizationGoal goal, GameTime now)
        {
            EntityId recruit = FindRecruit(organization);
            if (recruit.IsNone)
            {
                return BuildWealth(organization, goal, now);
            }

            organization.Wealth = LocalDemandPressure.Clamp(organization.Wealth - 5, 0, 100);
            organization.MemberIds.Add(recruit);

            NarrativeNpc npc = _world.Registry.GetNpc(recruit);
            if (npc != null && !npc.OrganizationIds.Contains(organization.Id))
            {
                npc.OrganizationIds.Add(organization.Id);
            }

            goal.Progress += 50;
            goal.Satisfied = goal.Progress >= 100;

            _world.Record(
                WorldEventType.OrganizationActed,
                organization.LeaderId,
                organization.Id,
                now,
                0.4,
                PrimarySite(organization),
                new[] { recruit },
                tags: new[] { ExpandMembership, "member_recruited" });
            return true;
        }

        private EntityId FindRecruit(Organization organization)
        {
            EntityId preferredSite = PrimarySite(organization);
            foreach (KeyValuePair<EntityId, NarrativeNpc> pair in _world.Registry.Npcs)
            {
                NarrativeNpc npc = pair.Value;
                if (!npc.Alive
                    || organization.MemberIds.Contains(npc.Id)
                    || npc.OrganizationIds.Count > 0)
                {
                    continue;
                }

                if (preferredSite.IsNone || npc.HomeSiteId.IsNone || npc.HomeSiteId == preferredSite)
                {
                    return npc.Id;
                }
            }

            return EntityId.None;
        }

        private bool BuildWealth(Organization organization, OrganizationGoal goal, GameTime now)
        {
            int gain = 3 + organization.MemberIds.Count;
            organization.Wealth = LocalDemandPressure.Clamp(organization.Wealth + gain, 0, 100);
            goal.Progress += gain;
            goal.Satisfied = goal.Progress >= 100 || organization.Wealth >= 100;

            _world.Record(
                WorldEventType.OrganizationActed,
                organization.LeaderId,
                organization.Id,
                now,
                0.3,
                PrimarySite(organization),
                tags: new[] { BuildReserves, "wealth_changed" });
            return true;
        }

        private bool Fortify(Organization organization, OrganizationGoal goal, GameTime now)
        {
            EntityId siteId = goal.Subject.IsNone ? PrimarySite(organization) : goal.Subject;
            NarrativeSite site = _world.Registry.GetSite(siteId);
            if (site == null)
            {
                return BuildWealth(organization, goal, now);
            }

            site.DangerLevel = LocalDemandPressure.Clamp(site.DangerLevel + 1 + organization.Aggression / 50, 0, 100);
            goal.Progress += 25;
            goal.Satisfied = goal.Progress >= 100;

            _world.Record(
                WorldEventType.OrganizationActed,
                organization.LeaderId,
                organization.Id,
                now,
                0.35,
                site.Id,
                new[] { site.Id },
                tags: new[] { ProtectHolding, "site_fortified" });
            return true;
        }

        private bool Raid(Organization organization, OrganizationGoal goal, GameTime now)
        {
            Organization target = _world.Registry.GetOrganization(goal.Subject);
            if (target == null || target.Id == organization.Id)
            {
                return BuildWealth(organization, goal, now);
            }

            int damage = 2 + organization.Aggression / 25;
            int cost = 1 + (100 - organization.Legitimacy) / 50;
            target.Wealth = LocalDemandPressure.Clamp(target.Wealth - damage, 0, 100);
            organization.Wealth = LocalDemandPressure.Clamp(organization.Wealth - cost, 0, 100);
            goal.Progress += 25 + organization.Aggression / 10;
            goal.Satisfied = goal.Progress >= 100;

            RelationshipEdge targetStanding = _world.Relationships.Find(target.Id, organization.Id)
                                              ?? _world.Relationships.Connect(target.Id, organization.Id, RelationKind.Rival, 0);
            targetStanding.Sentiment = LocalDemandPressure.Clamp(targetStanding.Sentiment - 20, -100, 100);
            targetStanding.Kind = targetStanding.Sentiment <= -40 ? RelationKind.Enemy : RelationKind.Rival;

            RelationshipEdge raiderStanding = _world.Relationships.Find(organization.Id, target.Id)
                                              ?? _world.Relationships.Connect(organization.Id, target.Id, RelationKind.Rival, 0);
            raiderStanding.Sentiment = LocalDemandPressure.Clamp(raiderStanding.Sentiment - 8, -100, 100);
            raiderStanding.Kind = raiderStanding.Sentiment <= -60 ? RelationKind.Enemy : RelationKind.Rival;

            _world.Record(
                WorldEventType.OrganizationActed,
                organization.LeaderId,
                target.Id,
                now,
                0.5,
                PrimarySite(target),
                new[] { organization.Id, target.Id },
                tags: new[] { RaidOrganization, "standing_changed", "wealth_damaged" });
            return true;
        }

        private static EntityId PrimarySite(Organization organization)
        {
            return organization.SiteIds.Count == 0 ? EntityId.None : organization.SiteIds[0];
        }
    }
}
