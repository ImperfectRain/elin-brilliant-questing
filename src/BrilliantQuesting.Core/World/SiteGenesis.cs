using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// One way in to what a place keeps.
    ///
    /// Two fields, because only one distinction is load-bearing here: whether the route goes
    /// through somebody. <see cref="NarrativeSite.Restricted"/> and <see cref="NarrativeSite.Admits"/>
    /// already model that difference - an owner admits people, a burglar admits themselves - and an
    /// approach names which of the two it is plus the verb it is taken with. Scoring how different
    /// two routes really are is BQ-092's question over generated candidates; this is the floor
    /// underneath it, and it exists so a plan cannot describe a place with one way in.
    /// </summary>
    public sealed class SiteApproach
    {
        public SiteApproach(string actionId, bool needsAdmission)
        {
            ActionId = actionId ?? string.Empty;
            NeedsAdmission = needsAdmission;
        }

        /// <summary>The registered verb this route is taken with.</summary>
        public string ActionId { get; }

        /// <summary>Whether somebody has to let you in first.</summary>
        public bool NeedsAdmission { get; }

        public override string ToString()
        {
            return ActionId + (NeedsAdmission ? " (admitted)" : " (uninvited)");
        }
    }

    /// <summary>Somebody the place is populated with, and what the game has to build them from.</summary>
    public sealed class SiteOccupantPlan
    {
        public SiteOccupantPlan(NarrativeNpc npc, string role, CharacterBlueprint blueprint)
        {
            Npc = npc;
            Role = role ?? string.Empty;
            Blueprint = blueprint;
        }

        public NarrativeNpc Npc { get; }

        /// <summary>What they are here for - "keeper", "guard", "captive". Free text by design.</summary>
        public string Role { get; }

        public CharacterBlueprint Blueprint { get; }
    }

    /// <summary>
    /// Something the place actually keeps, and who is holding it.
    ///
    /// Cargo has a holder rather than a floor position because that is the only placement the seam
    /// can currently make real: <see cref="ISituationStager.StageItem"/> gives a thing to somebody.
    /// Loose objects on a site's floor wait on the arbitrary-zone inventory gap recorded as
    /// ELIN-Q-0008.
    /// </summary>
    public sealed class SiteCargoPlan
    {
        public SiteCargoPlan(ItemDescriptor item, EntityId holderId, EntityId evidenceForFact = default)
        {
            Item = item;
            HolderId = holderId;
            EvidenceForFact = evidenceForFact;
        }

        public ItemDescriptor Item { get; }

        /// <summary>One of the site's own occupants. Cargo nobody at the place holds is not cargo.</summary>
        public EntityId HolderId { get; }

        /// <summary>
        /// The fact this object proves, where it proves one. Attached to the fact rather than
        /// copied into the site, because physical proof stays on the physical object (`D011`).
        /// </summary>
        public EntityId EvidenceForFact { get; }
    }

    /// <summary>
    /// Why a place exists, who is in it, what it keeps and how it can be reached.
    ///
    /// The plan is authoritative for meaning; the map is the embodiment (`PP §3`). Nothing here
    /// describes geometry, and nothing here is wording the content pipeline owns - the name is
    /// whatever the caller already calls the place.
    /// </summary>
    public sealed class SitePlan
    {
        public SitePlan(EntityId siteId, string name, string siteType, EntityId threadId)
        {
            SiteId = siteId;
            Name = name ?? string.Empty;
            SiteType = siteType ?? string.Empty;
            ThreadId = threadId;
            Occupants = new List<SiteOccupantPlan>();
            Cargo = new List<SiteCargoPlan>();
            Approaches = new List<SiteApproach>();
        }

        public EntityId SiteId { get; }

        public string Name { get; }

        public string SiteType { get; }

        /// <summary>The one matter this place belongs to. A site with no thread is scenery.</summary>
        public EntityId ThreadId { get; }

        /// <summary>
        /// The curated kind of place this was planned from, where one was (BQ-089). Empty on a
        /// plan somebody wrote by hand, which is the truth about it rather than a gap.
        /// </summary>
        public string GrammarId { get; set; } = string.Empty;

        public List<SiteOccupantPlan> Occupants { get; }

        public List<SiteCargoPlan> Cargo { get; }

        public List<SiteApproach> Approaches { get; }

        public bool Restricted { get; set; } = true;

        public SitePersistence Persistence { get; set; } = SitePersistence.Persistent;

        public int DangerLevel { get; set; }

        public ulong Seed { get; set; }
    }

    public enum SiteGenesisOutcome
    {
        /// <summary>The place now exists, is populated, holds its cargo and is bound to a zone.</summary>
        Established,

        /// <summary>Genesis had already run for this place. Nothing was staged and nothing changed.</summary>
        AlreadyEstablished,

        /// <summary>The plan did not describe a place this step is willing to make.</summary>
        PlanRejected,

        /// <summary>The adapter could not give the place a body on this build. Nothing was created.</summary>
        NotEmbodied
    }

    public sealed class SiteGenesisResult
    {
        private static readonly string[] NoReasons = new string[0];

        internal SiteGenesisResult(SiteGenesisOutcome outcome, NarrativeSite site, IReadOnlyList<string> reasons)
        {
            Outcome = outcome;
            Site = site;
            Reasons = reasons ?? NoReasons;
        }

        public SiteGenesisOutcome Outcome { get; }

        /// <summary>The place, for both <see cref="SiteGenesisOutcome.Established"/> and
        /// <see cref="SiteGenesisOutcome.AlreadyEstablished"/>; null otherwise.</summary>
        public NarrativeSite Site { get; }

        /// <summary>Every reason the plan was refused, so the inspector never has to guess at one.</summary>
        public IReadOnlyList<string> Reasons { get; }

        public bool Created => Outcome == SiteGenesisOutcome.Established;
    }

    /// <summary>What a return visit found.</summary>
    public sealed class SiteVisit
    {
        private static readonly EntityId[] Nothing = new EntityId[0];

        internal SiteVisit(
            NarrativeSite site,
            bool embodied,
            IReadOnlyList<EntityId> missingOccupants,
            IReadOnlyList<EntityId> missingCargo)
        {
            Site = site;
            Embodied = embodied;
            MissingOccupants = missingOccupants ?? Nothing;
            MissingCargo = missingCargo ?? Nothing;
        }

        public NarrativeSite Site { get; }

        public bool Found => Site != null;

        public bool Established => Site != null && Site.Established;

        /// <summary>The place still carries the adapter handle genesis bound it to.</summary>
        public bool Embodied { get; }

        public IReadOnlyList<EntityId> MissingOccupants { get; }

        public IReadOnlyList<EntityId> MissingCargo { get; }

        /// <summary>Same site, same actors, same cargo.</summary>
        public bool Intact =>
            Found && Established && Embodied && MissingOccupants.Count == 0 && MissingCargo.Count == 0;
    }

    /// <summary>
    /// BQ-087. Makes one BQ-owned place exist, once, and answers what a return visit finds.
    ///
    /// Genesis and development are separate, and a visited place is never destructively
    /// regenerated (`PP §6`). That is enforced here rather than trusted: a plan whose site id the
    /// world already knows is refused, and a place that has been established says so on itself and
    /// is handed straight back. So returning cannot re-stage anybody, and there is no second copy
    /// of the manifest to disagree with the first - <see cref="NarrativeSite.OccupantIds"/> and
    /// <see cref="NarrativeSite.ImportantObjectIds"/> are the manifest, they already persist, and
    /// <see cref="Visit"/> is a read over them.
    ///
    /// Genesis writes nothing to the event ledger. A place coming into existence is bookkeeping,
    /// not something that happened to anybody: the in-world events about a site are somebody
    /// finding it and somebody clearing it, and both already exist. That is also what makes the
    /// done-when checkable - a return visit provably redispatches nothing, because genesis
    /// dispatched nothing to begin with. See `D058`.
    /// </summary>
    public static class SiteGenesis
    {
        /// <summary>A place with fewer people in it than this is a prop, not a site.</summary>
        public const int MinimumOccupants = 3;

        /// <summary>The first proof stays small on purpose. Larger populations wait on BQ-092.</summary>
        public const int MaximumOccupants = 5;

        public const int MinimumApproaches = 2;

        public static SiteGenesisResult Establish(
            NarrativeWorldState world,
            SitePlan plan,
            ISituationStager stager,
            GameTime now)
        {
            if (world == null || plan == null || stager == null)
            {
                return Rejected("genesis needs a world, a plan and a stager");
            }

            if (plan.SiteId.IsNone)
            {
                return Rejected("the plan names no place");
            }

            NarrativeSite existing = world.Registry.GetSite(plan.SiteId);
            if (existing != null)
            {
                return existing.Established
                    ? new SiteGenesisResult(SiteGenesisOutcome.AlreadyEstablished, existing, null)
                    : Rejected("a place the world already knows cannot be generated over");
            }

            List<string> refusals = Validate(world, plan);
            if (refusals.Count > 0)
            {
                return new SiteGenesisResult(SiteGenesisOutcome.PlanRejected, null, refusals);
            }

            SiteBlueprint blueprint = new SiteBlueprint(plan.SiteId, plan.Name, plan.SiteType)
            {
                Persistent = plan.Persistence == SitePersistence.Persistent,
                Restricted = plan.Restricted,
                DangerLevel = plan.DangerLevel,
                Seed = plan.Seed
            };

            string zoneRef = stager.StageSite(blueprint);
            if (string.IsNullOrEmpty(zoneRef))
            {
                // Fail closed. A place with no body would still be in the save, would still be
                // named by its thread, and would answer questions about a location nobody can walk
                // into. Nothing has been staged at this point, so refusing costs nothing.
                return new SiteGenesisResult(
                    SiteGenesisOutcome.NotEmbodied,
                    null,
                    new[] { "the adapter could not give " + plan.Name + " a place on this build" });
            }

            NarrativeSite site = new NarrativeSite(plan.SiteId, plan.Name, plan.SiteType)
            {
                VanillaZoneRef = zoneRef,
                Restricted = plan.Restricted,
                Persistence = plan.Persistence,
                DangerLevel = plan.DangerLevel,
                GenerationSeed = plan.Seed,
                GrammarId = plan.GrammarId,
                Established = true,
                EstablishedAt = now
            };

            world.Registry.Add(site);
            world.ExternalRefs[plan.SiteId] = zoneRef;

            EntityId zone = ZoneOf(site);
            for (int i = 0; i < plan.Occupants.Count; i++)
            {
                SiteOccupantPlan occupant = plan.Occupants[i];
                if (world.Registry.GetNpc(occupant.Npc.Id) == null)
                {
                    world.Registry.Add(occupant.Npc);
                }

                site.OccupantIds.Add(occupant.Npc.Id);
                stager.StageCharacter(occupant.Npc.Id, occupant.Blueprint, zone);
            }

            for (int i = 0; i < plan.Cargo.Count; i++)
            {
                SiteCargoPlan cargo = plan.Cargo[i];
                stager.StageItem(cargo.HolderId, cargo.Item);
                site.ImportantObjectIds.Add(cargo.Item.Id);

                if (cargo.EvidenceForFact.IsNone)
                {
                    continue;
                }

                Fact fact = world.Knowledge.GetFact(cargo.EvidenceForFact);
                if (fact != null && !fact.EvidenceIds.Contains(cargo.Item.Id))
                {
                    fact.EvidenceIds.Add(cargo.Item.Id);
                }
            }

            for (int i = 0; i < plan.Approaches.Count; i++)
            {
                site.Approaches.Add(plan.Approaches[i]);
            }

            NarrativeThread thread = FindThread(world, plan.ThreadId);
            if (thread != null && !thread.SiteIds.Contains(site.Id))
            {
                thread.SiteIds.Add(site.Id);
            }

            return new SiteGenesisResult(SiteGenesisOutcome.Established, site, null);
        }

        /// <summary>
        /// What is here now, read against what genesis wrote down. Changes nothing and appends
        /// nothing: coming back to a place is not an event, and a reconciliation that recorded one
        /// would make every visit history.
        /// </summary>
        public static SiteVisit Visit(NarrativeWorldState world, EntityId siteId, IVanillaState vanilla)
        {
            NarrativeSite site = world?.Registry.GetSite(siteId);
            if (site == null)
            {
                return new SiteVisit(null, false, null, null);
            }

            bool embodied = !string.IsNullOrEmpty(site.VanillaZoneRef);
            if (vanilla == null)
            {
                return new SiteVisit(site, embodied, null, null);
            }

            EntityId zone = ZoneOf(site);
            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zone);

            List<EntityId> missingOccupants = new List<EntityId>();
            for (int i = 0; i < site.OccupantIds.Count; i++)
            {
                if (!Contains(present, site.OccupantIds[i]))
                {
                    missingOccupants.Add(site.OccupantIds[i]);
                }
            }

            // Cargo is "still here" when somebody who is still here is holding it. Which of the
            // occupants that is deliberately does not matter: a keeper handing the strongbox key
            // to the guard has not lost the strongbox, and a manifest that insisted on the original
            // holder would report drift for an ordinary afternoon.
            List<EntityId> missingCargo = new List<EntityId>();
            for (int i = 0; i < site.ImportantObjectIds.Count; i++)
            {
                if (!HeldByAnyOf(vanilla, present, site.ImportantObjectIds[i]))
                {
                    missingCargo.Add(site.ImportantObjectIds[i]);
                }
            }

            return new SiteVisit(site, embodied, missingOccupants, missingCargo);
        }

        /// <summary>
        /// The zone id the place's contents live under: the adapter's handle where it parses as an
        /// id, and the site's own id otherwise. The two are the same headless; on a live build the
        /// handle is the zone Elin already minted, which is what every other read is keyed on.
        /// </summary>
        public static EntityId ZoneOf(NarrativeSite site)
        {
            if (site == null)
            {
                return EntityId.None;
            }

            if (string.IsNullOrEmpty(site.VanillaZoneRef))
            {
                return site.Id;
            }

            EntityId bound = EntityId.Parse(site.VanillaZoneRef);
            return bound.IsNone ? site.Id : bound;
        }

        /// <summary>Every reason this plan would be refused, empty when it would be accepted.</summary>
        public static IReadOnlyList<string> Refusals(NarrativeWorldState world, SitePlan plan)
        {
            return world == null || plan == null
                ? new[] { "genesis needs a world and a plan" }
                : (IReadOnlyList<string>)Validate(world, plan);
        }

        private static List<string> Validate(NarrativeWorldState world, SitePlan plan)
        {
            List<string> refusals = new List<string>();

            if (FindThread(world, plan.ThreadId) == null)
            {
                refusals.Add("no thread " + plan.ThreadId.Value + " for the place to belong to");
            }

            if (plan.Occupants.Count < MinimumOccupants || plan.Occupants.Count > MaximumOccupants)
            {
                refusals.Add("a site takes " + MinimumOccupants + " to " + MaximumOccupants
                             + " actors, not " + plan.Occupants.Count);
            }

            HashSet<EntityId> occupantIds = new HashSet<EntityId>();
            for (int i = 0; i < plan.Occupants.Count; i++)
            {
                SiteOccupantPlan occupant = plan.Occupants[i];
                if (occupant?.Npc == null || occupant.Npc.Id.IsNone || occupant.Blueprint == null)
                {
                    refusals.Add("an occupant with no identity or nothing to build them from");
                    continue;
                }

                if (!occupantIds.Add(occupant.Npc.Id))
                {
                    refusals.Add("occupant " + occupant.Npc.Id.Value + " is listed twice");
                }
            }

            if (plan.Cargo.Count == 0)
            {
                refusals.Add("a site keeps something; this one keeps nothing");
            }

            HashSet<EntityId> cargoIds = new HashSet<EntityId>();
            for (int i = 0; i < plan.Cargo.Count; i++)
            {
                SiteCargoPlan cargo = plan.Cargo[i];
                if (cargo?.Item == null || cargo.Item.Id.IsNone)
                {
                    refusals.Add("cargo with no object behind it");
                    continue;
                }

                if (!cargoIds.Add(cargo.Item.Id))
                {
                    refusals.Add("cargo " + cargo.Item.Id.Value + " is listed twice");
                }

                if (!occupantIds.Contains(cargo.HolderId))
                {
                    refusals.Add("nobody at the place holds " + cargo.Item.Name);
                }

                if (!cargo.EvidenceForFact.IsNone && world.Knowledge.GetFact(cargo.EvidenceForFact) == null)
                {
                    refusals.Add(cargo.Item.Name + " is proof of a fact the world does not have");
                }
            }

            AddApproachRefusals(plan, refusals);
            return refusals;
        }

        private static void AddApproachRefusals(SitePlan plan, List<string> refusals)
        {
            if (plan.Approaches.Count < MinimumApproaches)
            {
                refusals.Add("a site needs " + MinimumApproaches + " ways in, not " + plan.Approaches.Count);
            }

            HashSet<string> verbs = new HashSet<string>();
            bool admitted = false;
            bool uninvited = false;
            for (int i = 0; i < plan.Approaches.Count; i++)
            {
                SiteApproach approach = plan.Approaches[i];
                if (approach == null || string.IsNullOrEmpty(approach.ActionId))
                {
                    refusals.Add("an approach that names no verb");
                    continue;
                }

                if (!verbs.Add(approach.ActionId))
                {
                    refusals.Add("approach " + approach.ActionId + " is listed twice");
                }

                if (approach.NeedsAdmission)
                {
                    admitted = true;
                }
                else
                {
                    uninvited = true;
                }
            }

            // Two verbs that both wait on the same person's permission are one approach with two
            // spellings. What makes them meaningfully different is that one of them does not need
            // anybody to agree.
            if (plan.Approaches.Count >= MinimumApproaches && !(admitted && uninvited))
            {
                refusals.Add("every way in "
                             + (admitted ? "waits on somebody letting you in" : "goes around everybody")
                             + "; that is one approach, not two");
            }
        }

        private static NarrativeThread FindThread(NarrativeWorldState world, EntityId threadId)
        {
            if (threadId.IsNone)
            {
                return null;
            }

            for (int i = 0; i < world.Threads.Count; i++)
            {
                if (world.Threads[i].Id == threadId)
                {
                    return world.Threads[i];
                }
            }

            return null;
        }

        private static bool HeldByAnyOf(IVanillaState vanilla, IReadOnlyList<EntityId> holders, EntityId itemId)
        {
            for (int i = 0; i < holders.Count; i++)
            {
                IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(holders[i]);
                if (inventory == null)
                {
                    continue;
                }

                for (int j = 0; j < inventory.Count; j++)
                {
                    if (inventory[j] != null && inventory[j].Id == itemId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<EntityId> ids, EntityId id)
        {
            if (ids == null)
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static SiteGenesisResult Rejected(string reason)
        {
            return new SiteGenesisResult(SiteGenesisOutcome.PlanRejected, null, new[] { reason });
        }
    }
}
