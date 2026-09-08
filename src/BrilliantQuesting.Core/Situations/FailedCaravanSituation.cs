using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    public sealed class FailedCaravanResult
    {
        internal FailedCaravanResult(ScenarioPlan plan, SiteGenesisResult genesis, params string[] refusals)
        {
            Plan = plan;
            Genesis = genesis;
            Refusals = refusals;
        }

        public ScenarioPlan Plan { get; }
        public SiteGenesisResult Genesis { get; }
        public NarrativeSite Site => Genesis?.Site;
        public IReadOnlyList<string> Refusals { get; }
        public bool Established => Genesis != null && Refusals.Count == 0;
    }

    /// <summary>
    /// BQ-043: a failed journey's existing matter acquires a findable place. This consumes travel,
    /// history and observed possessions; it creates no journey, attack, actor, cargo or second thread.
    /// The caller supplies a supported kind of place, never an inferred biome or a default camp.
    /// </summary>
    public static class FailedCaravanSituation
    {
        public const string ArchetypeId = "failed_caravan";

        public static FailedCaravanResult Establish(
            NarrativeWorldState world, EntityId groupId, string siteName,
            SiteGrammar grammar, string pieceFamily, SitePieceCatalogue pieces, ulong seed,
            ActionRegistry actions, IVanillaState vanilla, ISituationStager stager, GameTime now)
        {
            if (world == null || vanilla == null || stager == null)
                return Refused("a failed caravan needs a world, an observation seam and a stager");

            TravelingGroup group = world.TravelingGroups.Of(groupId);
            if (group == null || group.Kind != "caravan" || group.State != TravelingGroupState.Failed)
                return Refused("there is no failed caravan; lateness is not failure");

            NarrativeThread thread = world.GetThread(group.ThreadId);
            WorldEvent failure = Find(world, group.FailedEventId);
            WorldEvent departure = Find(world, group.DepartedEventId);
            WorldEvent cause = Find(world, group.InterruptionCauseId);
            if (thread == null || failure == null || departure == null || cause == null
                || failure.Type != WorldEventType.TravelFailed || failure.Target != group.Id
                || departure.Type != WorldEventType.TravelDeparted || departure.Target != group.Id
                || failure.ThreadId != thread.Id || departure.ThreadId != thread.Id
                || (cause.ThreadId != thread.Id && cause.Id != thread.OriginEventId)
                || cause.Time < departure.Time || cause.Time > failure.Time
                || !Contains(failure.Related, cause.Id))
                return Refused("the departure, failure and cause must belong to this journey's recorded matter");

            EntityId siteId = group.InterruptionSiteId;
            if (siteId.IsNone || failure.Zone != siteId || cause.Zone != siteId)
                return Refused("the failure and its cause do not establish the same known site");

            // An unrelated incident in a broad thread must not furnish a caravan wreck.
            bool implicated = group.Member(cause.Target) != null || cause.Target == group.Id;
            foreach (EntityId cargo in group.CargoIds)
                implicated |= Contains(cause.Evidence, cargo);
            if (!implicated)
                return Refused("the recorded cause names neither this caravan's members nor its cargo");

            NarrativeSite existing = world.Registry.GetSite(siteId);
            if (existing != null)
            {
                if (!existing.Established || !thread.SiteIds.Contains(siteId)
                    || SiteGenesis.ZoneOf(existing) != siteId)
                    return Refused("the failure site already exists outside this matter's established site binding");
                // Returning does not refill cargo, reteach facts or rebuild terrain. Current
                // whereabouts and missing contents remain SiteGenesis.Visit's question.
                return new FailedCaravanResult(null,
                    new SiteGenesisResult(SiteGenesisOutcome.AlreadyEstablished, existing, null));
            }

            if (thread.State == ThreadState.Resolved || thread.State == ThreadState.Inherited
                || thread.State == ThreadState.Quarantined)
                return Refused("the caravan's matter is closed; no new site may be generated");

            ScenarioPlan scenario = ScenarioPlanner.Plan(world, thread.Id, grammar,
                SiteAffordance.EvidenceCache, seed, SiteCandidates.DefaultBatch, actions, vanilla);
            if (!scenario.Valid)
                return Refused("the causal scenario plan is invalid: "
                    + string.Join("; ", scenario.Refusals), scenario);

            List<SiteHolding> caravanCargo = new List<SiteHolding>();
            foreach (SiteHolding holding in scenario.Contents.Cargo)
                if (group.CargoIds.Contains(holding.Item.Id))
                    caravanCargo.Add(holding);
            if (caravanCargo.Count == 0)
                return Refused("no original caravan cargo remains in the observed inventories", scenario);

            // SiteContents establishes narrative presence, not physical whereabouts (D068).
            // Binding its existing people elsewhere would claim a wreck with absent contents.
            foreach (SiteOccupancy occupant in scenario.Contents.Occupants)
                if (vanilla.GetZoneOf(occupant.Id) != siteId)
                    return Refused("an occupant's whereabouts are unknown or differ from the failure site: "
                        + occupant.Id, scenario);

            SiteRealizationResult realization = SiteRealization.Realize(pieceFamily, scenario.Layout,
                SiteAffordance.EvidenceCache, pieces, scenario.Contents, actions, vanilla);
            if (!realization.Built)
                return Refused(string.Join("; ", realization.Refusals), scenario);

            SitePlan plan = scenario.NewSitePlan(siteId, siteName);
            plan.RequiredZoneId = siteId;
            plan.Objective = SiteAffordance.EvidenceCache.ToString();
            plan.Structure = realization.Structure;
            SiteGenesisResult genesis = SiteGenesis.Establish(world, plan, stager, now);
            if (genesis.Outcome != SiteGenesisOutcome.Established)
                return new FailedCaravanResult(scenario, genesis, string.Join("; ", genesis.Reasons));

            // A location lead is ordinary knowledge about the cargo's observed keeper, initially
            // known only to that person. It can reach the player through existing inquiry/rumour
            // surfaces. No off-screen eyewitness proof or omniscient journal entry is granted.
            foreach (SiteHolding holding in caravanCargo)
            {
                Fact location = Whereabouts.Record(world, holding.HolderId, siteId, siteName, failure.Id);
                if (!thread.FactIds.Contains(location.Id)) thread.FactIds.Add(location.Id);
                world.Knowledge.Teach(holding.HolderId, location.Id, KnowledgeSource.Participant,
                    1.0, now, false);
            }
            return new FailedCaravanResult(scenario, genesis);
        }

        private static FailedCaravanResult Refused(string reason, ScenarioPlan plan = null)
            => new FailedCaravanResult(plan, null, reason);

        private static WorldEvent Find(NarrativeWorldState world, EntityId id)
        {
            foreach (WorldEvent entry in world.Ledger.Events)
                if (entry.Id == id) return entry;
            return null;
        }

        private static bool Contains(IReadOnlyList<EntityId> ids, EntityId id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;
            return false;
        }
    }
}
