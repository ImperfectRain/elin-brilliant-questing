using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;

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

        private static EntityId PrimarySite(Organization organization)
        {
            return organization.SiteIds.Count == 0 ? EntityId.None : organization.SiteIds[0];
        }
    }
}
