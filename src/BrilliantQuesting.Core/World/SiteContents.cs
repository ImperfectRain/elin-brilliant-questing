using System;
using System.Collections.Generic;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Why somebody is at a place.
    ///
    /// Three values, and each of them is something the simulation already recorded - the same
    /// admission rule <see cref="ProvenanceRole"/> keeps for objects. There is no "guard", no
    /// "lookout" and no "leader" here, because nothing in the world writes those down; a place is
    /// held by whoever took what it keeps, and the rest of them are there because they are in that
    /// person's crew.
    /// </summary>
    public enum SitePresence
    {
        /// <summary>This matter's history records them taking what the place keeps, or somebody it holds.</summary>
        Holds,

        /// <summary>They belong to an organization one of the holders belongs to.</summary>
        Group,

        /// <summary>The ledger recorded them taken in this matter, and nothing after it let them go.</summary>
        Held
    }

    /// <summary>Why an object is at a place. Never "because a place of this kind has one".</summary>
    public enum SiteKeeping
    {
        /// <summary>History records it taken in this matter, and whoever took it is here.</summary>
        Taken,

        /// <summary>A claim this matter rests on names it as evidence, and somebody here has it.</summary>
        Proof
    }

    /// <summary>One person the matter puts at the place, and what put them there.</summary>
    public sealed class SiteOccupancy
    {
        internal SiteOccupancy(
            NarrativeNpc npc,
            SitePresence presence,
            EntityId because,
            string nodeId,
            bool alreadyExists,
            string reason)
        {
            Npc = npc;
            Presence = presence;
            Because = because;
            NodeId = nodeId ?? string.Empty;
            AlreadyExists = alreadyExists;
            Reason = reason ?? string.Empty;
        }

        public NarrativeNpc Npc { get; }

        public EntityId Id => Npc == null ? EntityId.None : Npc.Id;

        public SitePresence Presence { get; }

        /// <summary>The event or organization that puts them here. Never <see cref="EntityId.None"/>.</summary>
        public EntityId Because { get; }

        /// <summary>
        /// The part of the plan they are kept in, where the plan says one. Empty for everybody who
        /// is simply in the place: an abstract plan says what a place is for, not where a body is
        /// standing, and only being held somewhere is a fact about which part.
        /// </summary>
        public string NodeId { get; }

        /// <summary>The game already has this person, so genesis binds them rather than building one.</summary>
        public bool AlreadyExists { get; }

        public string Reason { get; }

        public override string ToString() => (Npc == null ? "nobody" : Npc.Name) + ": " + Reason;
    }

    /// <summary>One object the matter leaves at the place, and what leaves it there.</summary>
    public sealed class SiteHolding
    {
        internal SiteHolding(
            ItemDescriptor item,
            EntityId holderId,
            SiteKeeping keeping,
            EntityId evidenceForFact,
            EntityId because,
            string nodeId,
            string reason)
        {
            Item = item;
            HolderId = holderId;
            Keeping = keeping;
            EvidenceForFact = evidenceForFact;
            Because = because;
            NodeId = nodeId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public ItemDescriptor Item { get; }

        /// <summary>One of the place's own occupants, read out of the live inventory it is in.</summary>
        public EntityId HolderId { get; }

        public SiteKeeping Keeping { get; }

        /// <summary>The claim it proves, where this matter rests on one.</summary>
        public EntityId EvidenceForFact { get; }

        /// <summary>The event that put it here.</summary>
        public EntityId Because { get; }

        /// <summary>The part of the plan that keeps it, empty where the plan does not say.</summary>
        public string NodeId { get; }

        public string Reason { get; }

        public override string ToString() => (Item == null ? "nothing" : Item.Name) + ": " + Reason;
    }

    /// <summary>Somebody or something the matter named that the place does not get, and why not.</summary>
    public sealed class SiteContentsOmission
    {
        internal SiteContentsOmission(EntityId id, string reason)
        {
            Id = id;
            Reason = reason ?? string.Empty;
        }

        public EntityId Id { get; }

        public string Reason { get; }

        public override string ToString() => Id.Value + ": " + Reason;
    }

    /// <summary>
    /// A part of the plan that could hold something and got nothing.
    ///
    /// Reported rather than filled. This is where "no template chest" is observable: a plan with a
    /// pen and a matter that holds nobody produces an empty pen and a line saying so, never a
    /// prisoner invented to justify the room.
    /// </summary>
    public sealed class SiteVacancy
    {
        internal SiteVacancy(string nodeId, SiteAffordance affordance, string reason)
        {
            NodeId = nodeId ?? string.Empty;
            Affordance = affordance;
            Reason = reason ?? string.Empty;
        }

        public string NodeId { get; }

        public SiteAffordance Affordance { get; }

        public string Reason { get; }

        public override string ToString() => NodeId + ": " + Reason;
    }

    /// <summary>What one matter puts in one place, and everything it does not.</summary>
    public sealed class SiteContentsReading
    {
        private static readonly SiteOccupancy[] Nobody = new SiteOccupancy[0];
        private static readonly SiteHolding[] Nothing = new SiteHolding[0];
        private static readonly SiteContentsOmission[] NoOmissions = new SiteContentsOmission[0];
        private static readonly SiteVacancy[] NoVacancies = new SiteVacancy[0];
        private static readonly string[] NoRefusals = new string[0];

        internal SiteContentsReading(
            NarrativeThread thread,
            SiteLayout layout,
            IReadOnlyList<SiteOccupancy> occupants,
            IReadOnlyList<SiteHolding> cargo,
            IReadOnlyList<SiteContentsOmission> omitted,
            IReadOnlyList<SiteVacancy> vacant,
            IReadOnlyList<string> refusals)
        {
            Thread = thread;
            Layout = layout;
            Occupants = occupants ?? Nobody;
            Cargo = cargo ?? Nothing;
            Omitted = omitted ?? NoOmissions;
            Vacant = vacant ?? NoVacancies;
            Refusals = refusals ?? NoRefusals;
        }

        /// <summary>The matter the contents were derived from, or null when there was none.</summary>
        public NarrativeThread Thread { get; }

        /// <summary>The plan they were placed into, or null when the caller had none.</summary>
        public SiteLayout Layout { get; }

        public IReadOnlyList<SiteOccupancy> Occupants { get; }

        public IReadOnlyList<SiteHolding> Cargo { get; }

        public IReadOnlyList<SiteContentsOmission> Omitted { get; }

        public IReadOnlyList<SiteVacancy> Vacant { get; }

        /// <summary>Why this matter cannot furnish a place. Empty when it can.</summary>
        public IReadOnlyList<string> Refusals { get; }

        /// <summary>The matter has enough at the place for genesis to be asked.</summary>
        public bool Furnished => Refusals.Count == 0;

        /// <summary>
        /// Puts the derived contents on a plan.
        ///
        /// Genesis stays the decider: this fills the two lists it validates and adds nothing else,
        /// so a matter that came up short is refused there with the same words as any other bad
        /// plan rather than being quietly padded here.
        /// </summary>
        public void ApplyTo(SitePlan plan)
        {
            if (plan == null)
            {
                return;
            }

            for (int i = 0; i < Occupants.Count; i++)
            {
                SiteOccupancy occupant = Occupants[i];
                string role = RoleOf(occupant.Presence);

                // Somebody the game has not built yet is built from what BQ actually knows about
                // them, which is their name: an archetype guessed here would be this layer
                // deciding what a person is, and an empty one lets the adapter use its own
                // default. Somebody the game already has is bound, never rebuilt (`D021`).
                plan.Occupants.Add(occupant.AlreadyExists
                    ? SiteOccupantPlan.AlreadyThere(occupant.Npc, role)
                    : new SiteOccupantPlan(occupant.Npc, role, new CharacterBlueprint(occupant.Npc.Name)));
            }

            for (int i = 0; i < Cargo.Count; i++)
            {
                SiteHolding holding = Cargo[i];
                plan.Cargo.Add(SiteCargoPlan.AlreadyHere(holding.Item, holding.HolderId, holding.EvidenceForFact));
            }
        }

        private static string RoleOf(SitePresence presence)
        {
            switch (presence)
            {
                case SitePresence.Holds:
                    return "holder";
                case SitePresence.Held:
                    return "held";
                default:
                    return "member";
            }
        }
    }

    /// <summary>
    /// BQ-091. What a matter actually leaves in a place, derived from what the simulation recorded
    /// rather than from what a place of that kind usually contains.
    ///
    /// <b>State causes contents (`PP §2`).</b> Nothing here is invented. An object is at the place
    /// because this matter's history says somebody took it and the live world says that person is
    /// still carrying it; a person is at the place because they took it, because they are in the
    /// crew of somebody who did, or because the ledger recorded them being taken and nothing since
    /// let them go. There is no filler, no template chest and no difficulty table: an empty store
    /// room is reported as empty (<see cref="SiteContentsReading.Vacant"/>) and a matter with too
    /// little at the place is refused, because a place furnished with things nobody did would be a
    /// second, invented history sitting next to the real one.
    ///
    /// <b>The group is the group (`LW §7.8`).</b> Enemies are the living members of the
    /// organizations the holders belong to, in the order the organization itself lists them. A
    /// member the world has killed is not there and nobody takes their place, which is exactly
    /// "do not refill a cleared group" - expressed as where the roster comes from rather than as a
    /// rule somebody has to remember not to break.
    ///
    /// <b>An object is here only where the game says somebody here has it.</b> Provenance is
    /// history's claim about an object, not proof the object exists: a stale binding is not
    /// evidence, so every piece of cargo is confirmed against a live inventory read and anything
    /// that cannot be found is omitted with the reason. That is also why nothing here is staged -
    /// the objects already exist and are already in the right hands, and genesis binds them.
    ///
    /// <b>Which acts make somebody hold a place is not a table.</b> It is derived from the
    /// contents: the actor of the theft that produced the cargo, and the actor of the capture that
    /// produced the captive. A general list of "hostile verbs" would be this layer inventing a
    /// vocabulary, and would put the two people who merely argued here in the hideout.
    ///
    /// Which candidate plan best suits the contents is BQ-092's; this reports the mismatches -
    /// somebody held with nowhere to hold them, a pen with nobody in it - and places what fits.
    /// </summary>
    public static class SiteContents
    {
        public static SiteContentsReading Derive(
            NarrativeWorldState world,
            EntityId threadId,
            SiteLayout layout,
            IVanillaState vanilla)
        {
            if (world == null)
            {
                return Refused(null, layout, "there is no world to read the matter out of");
            }

            NarrativeThread thread = world.GetThread(threadId);
            if (thread == null)
            {
                return Refused(null, layout, "there is no matter for a place to be furnished from");
            }

            // Contents are things that exist. With nothing to ask, the honest answer is that
            // nothing can be said to be at the place - the same refusal BQ-090 makes of a route
            // whose build was never asked (`D067`).
            if (vanilla == null)
            {
                return Refused(thread, layout, "nothing can be read from the world, so nothing can be said to be here");
            }

            History history = History.Of(world, thread);
            List<SiteContentsOmission> omitted = new List<SiteContentsOmission>();

            List<SiteOccupancy> occupants = Occupants(world, layout, vanilla, history, omitted);
            List<SiteHolding> cargo = Cargo(world, thread, layout, vanilla, history, occupants, omitted);
            List<SiteVacancy> vacant = Vacancies(layout, occupants, cargo);

            List<string> refusals = new List<string>();
            if (occupants.Count < SiteGenesis.MinimumOccupants)
            {
                refusals.Add("this matter puts " + occupants.Count + " person(s) at the place, and a place takes "
                             + SiteGenesis.MinimumOccupants);
            }

            if (cargo.Count == 0)
            {
                refusals.Add("this matter's history leaves nothing here for the place to keep");
            }

            return new SiteContentsReading(thread, layout, occupants, cargo, omitted, vacant, refusals);
        }

        // -- people ---------------------------------------------------------------------------

        private static List<SiteOccupancy> Occupants(
            NarrativeWorldState world,
            SiteLayout layout,
            IVanillaState vanilla,
            History history,
            List<SiteContentsOmission> omitted)
        {
            List<SiteOccupancy> placed = new List<SiteOccupancy>();
            HashSet<EntityId> taken = new HashSet<EntityId>();

            // The people who took what the place keeps, in the order history recorded them doing
            // it. They are first because they are the reason the place is anybody's.
            for (int i = 0; i < history.Holders.Count; i++)
            {
                Add(world, vanilla, placed, taken, omitted, history.Holders[i].Who, SitePresence.Holds,
                    history.Holders[i].Because, string.Empty,
                    "this matter's history records them taking what the place keeps");
            }

            // Then whoever is still held, if the plan has anywhere to hold them.
            string cell = NodeWith(layout, SiteAffordance.PrisonCell);
            for (int i = 0; i < history.Captives.Count; i++)
            {
                EntityId who = history.Captives[i].Who;
                if (layout != null && cell.Length == 0)
                {
                    omitted.Add(new SiteContentsOmission(
                        who, "this plan has nowhere to hold anybody, so they are not kept here"));
                    continue;
                }

                Add(world, vanilla, placed, taken, omitted, who, SitePresence.Held,
                    history.Captives[i].Because, cell,
                    "the ledger recorded them taken in this matter and nothing since let them go");
            }

            // Then the rest of the crews the holders belong to, in the order each organization
            // lists its own members. Nobody is added to reach a number.
            List<SiteOccupancy> crew = new List<SiteOccupancy>();
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Presence != SitePresence.Holds)
                {
                    continue;
                }

                NarrativeNpc holder = placed[i].Npc;
                for (int o = 0; o < holder.OrganizationIds.Count; o++)
                {
                    Organization organization = world.Registry.GetOrganization(holder.OrganizationIds[o]);
                    if (organization == null)
                    {
                        continue;
                    }

                    Add(world, vanilla, crew, taken, omitted, organization.LeaderId, SitePresence.Group,
                        organization.Id, string.Empty, "they lead the crew " + holder.Name + " is in");

                    for (int m = 0; m < organization.MemberIds.Count; m++)
                    {
                        Add(world, vanilla, crew, taken, omitted, organization.MemberIds[m], SitePresence.Group,
                            organization.Id, string.Empty, "they are in the crew " + holder.Name + " is in");
                    }
                }
            }

            placed.AddRange(crew);

            // A place this step is willing to make stays small (BQ-087). The ones history
            // implicates are kept and the rest are reported, rather than the list being trimmed
            // wherever it happened to end.
            if (placed.Count <= SiteGenesis.MaximumOccupants)
            {
                return placed;
            }

            for (int i = SiteGenesis.MaximumOccupants; i < placed.Count; i++)
            {
                omitted.Add(new SiteContentsOmission(
                    placed[i].Id,
                    "a place holds at most " + SiteGenesis.MaximumOccupants
                    + " and this matter's history implicates the others first"));
            }

            return placed.GetRange(0, SiteGenesis.MaximumOccupants);
        }

        private static void Add(
            NarrativeWorldState world,
            IVanillaState vanilla,
            List<SiteOccupancy> into,
            HashSet<EntityId> taken,
            List<SiteContentsOmission> omitted,
            EntityId who,
            SitePresence presence,
            EntityId because,
            string nodeId,
            string reason)
        {
            if (who.IsNone || who == vanilla.PlayerId || !taken.Add(who))
            {
                return;
            }

            NarrativeNpc npc = world.Registry.GetNpc(who);
            if (npc == null || !npc.IsCanonical)
            {
                omitted.Add(new SiteContentsOmission(who, "the world knows no actor by that name"));
                return;
            }

            // Dead is a recorded answer; unread is not. An actor the build could not answer for is
            // still somebody this matter names, and treating silence as a death would empty the
            // place on a build that simply does not answer (`D017`).
            if (!npc.Alive || vanilla.GetLifeState(who) == VanillaLifeState.Dead)
            {
                omitted.Add(new SiteContentsOmission(who, "they are dead, and nobody takes their place"));
                return;
            }

            into.Add(new SiteOccupancy(npc, presence, because, nodeId, Embodied(vanilla, npc), reason));
        }

        /// <summary>
        /// Whether the game already has this person. Read rather than assumed: an actor standing
        /// somewhere has a body, and one the mod only has a record of does not.
        /// </summary>
        private static bool Embodied(IVanillaState vanilla, NarrativeNpc npc)
        {
            return !string.IsNullOrEmpty(npc.VanillaCharaRef) || !vanilla.GetZoneOf(npc.Id).IsNone;
        }

        // -- things ---------------------------------------------------------------------------

        private static List<SiteHolding> Cargo(
            NarrativeWorldState world,
            NarrativeThread thread,
            SiteLayout layout,
            IVanillaState vanilla,
            History history,
            List<SiteOccupancy> occupants,
            List<SiteContentsOmission> omitted)
        {
            List<SiteHolding> cargo = new List<SiteHolding>();
            string cache = NodeWith(layout, SiteAffordance.EvidenceCache);
            HashSet<EntityId> counted = new HashSet<EntityId>();

            for (int i = 0; i < history.Taken.Count; i++)
            {
                TakenObject taken = history.Taken[i];
                if (!counted.Add(taken.ItemId))
                {
                    continue;
                }

                if (!Occupies(occupants, taken.Who))
                {
                    omitted.Add(new SiteContentsOmission(
                        taken.ItemId, "whoever took it is not at the place, so it is not here"));
                    continue;
                }

                ItemDescriptor item = Carrying(vanilla, taken.Who, taken.ItemId);
                if (item == null)
                {
                    // History says they took it; the game says they are not carrying it. The
                    // recorded claim is not evidence the object is here, and inventing a
                    // descriptor for it would put a second copy of a real object in the world.
                    omitted.Add(new SiteContentsOmission(
                        taken.ItemId, "the one who took it is not carrying it any more"));
                    continue;
                }

                cargo.Add(new SiteHolding(
                    item, taken.Who, SiteKeeping.Taken, ProofOf(world, thread, taken.ItemId), taken.Because, cache,
                    "this matter's history records it taken, and the one who took it has it here"));
            }

            // What the claims this matter rests on are proved by, where somebody here has it. Not
            // an inference from the claim: the object has to be in a live inventory belonging to
            // one of the place's own people, exactly as taken cargo does.
            for (int f = 0; f < thread.FactIds.Count; f++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[f]);
                if (fact == null)
                {
                    continue;
                }

                for (int e = 0; e < fact.EvidenceIds.Count; e++)
                {
                    EntityId itemId = fact.EvidenceIds[e];
                    if (!counted.Add(itemId))
                    {
                        continue;
                    }

                    EntityId holder;
                    ItemDescriptor item = HeldByOneOf(vanilla, occupants, itemId, out holder);
                    if (item == null)
                    {
                        omitted.Add(new SiteContentsOmission(
                            itemId, "it proves something this matter rests on, and nobody here has it"));
                        continue;
                    }

                    cargo.Add(new SiteHolding(
                        item, holder, SiteKeeping.Proof, fact.Id, fact.OriginEvent, cache,
                        "somebody here is carrying what one of this matter's claims rests on"));
                }
            }

            return cargo;
        }

        private static EntityId ProofOf(NarrativeWorldState world, NarrativeThread thread, EntityId itemId)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact == null)
                {
                    continue;
                }

                for (int e = 0; e < fact.EvidenceIds.Count; e++)
                {
                    if (fact.EvidenceIds[e] == itemId)
                    {
                        return fact.Id;
                    }
                }
            }

            return EntityId.None;
        }

        private static ItemDescriptor Carrying(IVanillaState vanilla, EntityId holder, EntityId itemId)
        {
            IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(holder);
            if (inventory == null)
            {
                return null;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].Id == itemId)
                {
                    return inventory[i];
                }
            }

            return null;
        }

        private static ItemDescriptor HeldByOneOf(
            IVanillaState vanilla,
            List<SiteOccupancy> occupants,
            EntityId itemId,
            out EntityId holder)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                ItemDescriptor item = Carrying(vanilla, occupants[i].Id, itemId);
                if (item != null)
                {
                    holder = occupants[i].Id;
                    return item;
                }
            }

            holder = EntityId.None;
            return null;
        }

        private static bool Occupies(List<SiteOccupancy> occupants, EntityId who)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i].Id == who)
                {
                    return true;
                }
            }

            return false;
        }

        // -- the plan -------------------------------------------------------------------------

        private static List<SiteVacancy> Vacancies(
            SiteLayout layout,
            List<SiteOccupancy> occupants,
            List<SiteHolding> cargo)
        {
            List<SiteVacancy> vacant = new List<SiteVacancy>();
            if (layout == null)
            {
                return vacant;
            }

            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                for (int a = 0; a < node.Affordances.Count; a++)
                {
                    SiteAffordance affordance = node.Affordances[a];
                    if (affordance == SiteAffordance.PrisonCell && !AnyIn(occupants, node.Id))
                    {
                        vacant.Add(new SiteVacancy(
                            node.Id, affordance, "this plan can hold somebody and this matter holds nobody"));
                    }
                    else if (affordance == SiteAffordance.EvidenceCache && !AnythingIn(cargo, node.Id))
                    {
                        vacant.Add(new SiteVacancy(
                            node.Id, affordance, "this plan keeps things and this matter left nothing here"));
                    }
                }
            }

            return vacant;
        }

        private static bool AnyIn(List<SiteOccupancy> occupants, string nodeId)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                if (string.Equals(occupants[i].NodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnythingIn(List<SiteHolding> cargo, string nodeId)
        {
            for (int i = 0; i < cargo.Count; i++)
            {
                if (string.Equals(cargo[i].NodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The first part of the plan that answers this requirement, or empty.</summary>
        private static string NodeWith(SiteLayout layout, SiteAffordance affordance)
        {
            if (layout == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                for (int a = 0; a < node.Affordances.Count; a++)
                {
                    if (node.Affordances[a] == affordance)
                    {
                        return node.Id;
                    }
                }
            }

            return string.Empty;
        }

        private static SiteContentsReading Refused(NarrativeThread thread, SiteLayout layout, string reason)
        {
            return new SiteContentsReading(thread, layout, null, null, null, null, new[] { reason });
        }

        // -- what the matter's history says ----------------------------------------------------

        private struct Actor
        {
            internal Actor(EntityId who, EntityId because)
            {
                Who = who;
                Because = because;
            }

            internal EntityId Who;

            internal EntityId Because;
        }

        private struct TakenObject
        {
            internal TakenObject(EntityId itemId, EntityId who, EntityId because)
            {
                ItemId = itemId;
                Who = who;
                Because = because;
            }

            internal EntityId ItemId;

            internal EntityId Who;

            internal EntityId Because;
        }

        /// <summary>
        /// The two things this matter's history says about a place: what was taken and never given
        /// back, and who was taken and never let go.
        ///
        /// <b>An event belongs to a matter because the ledger says so.</b> The thread id it was
        /// written with, or the thread naming it as the event it began from - the two the ledger
        /// records explicitly. The looser readings elsewhere answer "could this be brought up",
        /// which is the right question for a callback and the wrong one for putting a person into
        /// a room.
        /// </summary>
        private sealed class History
        {
            private History(List<Actor> holders, List<Actor> captives, List<TakenObject> taken)
            {
                Holders = holders;
                Captives = captives;
                Taken = taken;
            }

            internal List<Actor> Holders { get; }

            internal List<Actor> Captives { get; }

            internal List<TakenObject> Taken { get; }

            internal static History Of(NarrativeWorldState world, NarrativeThread thread)
            {
                Dictionary<EntityId, TakenObject> taken = new Dictionary<EntityId, TakenObject>();
                List<EntityId> takenOrder = new List<EntityId>();
                Dictionary<EntityId, Actor> captives = new Dictionary<EntityId, Actor>();
                List<EntityId> captiveOrder = new List<EntityId>();
                List<Actor> holders = new List<Actor>();
                HashSet<EntityId> holding = new HashSet<EntityId>();

                IReadOnlyList<WorldEvent> events = world.Ledger.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    WorldEvent worldEvent = events[i];
                    if (!Belongs(thread, worldEvent))
                    {
                        continue;
                    }

                    switch (ItemProvenance.RoleOf(worldEvent.Type))
                    {
                        case ProvenanceRole.Stolen:
                            for (int e = 0; e < worldEvent.Evidence.Count; e++)
                            {
                                EntityId itemId = worldEvent.Evidence[e];
                                if (!taken.ContainsKey(itemId))
                                {
                                    takenOrder.Add(itemId);
                                }

                                taken[itemId] = new TakenObject(itemId, worldEvent.Actor, worldEvent.Id);
                            }

                            break;

                        // Given back, handed on, or destroyed: whatever else it is now, it is not
                        // something this place still keeps.
                        case ProvenanceRole.Returned:
                        case ProvenanceRole.Given:
                        case ProvenanceRole.Destroyed:
                            for (int e = 0; e < worldEvent.Evidence.Count; e++)
                            {
                                taken.Remove(worldEvent.Evidence[e]);
                            }

                            break;
                    }

                    if (worldEvent.Type == WorldEventType.Captured && !worldEvent.Target.IsNone)
                    {
                        if (!captives.ContainsKey(worldEvent.Target))
                        {
                            captiveOrder.Add(worldEvent.Target);
                        }

                        captives[worldEvent.Target] = new Actor(worldEvent.Target, worldEvent.Id);
                    }
                    else if ((worldEvent.Type == WorldEventType.Rescued || worldEvent.Type == WorldEventType.Killed)
                             && !worldEvent.Target.IsNone)
                    {
                        captives.Remove(worldEvent.Target);
                    }
                }

                // Whoever is holding what the place keeps holds the place. Derived from the
                // contents rather than from a list of hostile verbs, so nothing but taking
                // somebody's property or their liberty puts a person in the hideout.
                List<TakenObject> takenNow = new List<TakenObject>();
                for (int i = 0; i < takenOrder.Count; i++)
                {
                    TakenObject entry;
                    if (!taken.TryGetValue(takenOrder[i], out entry))
                    {
                        continue;
                    }

                    takenNow.Add(entry);
                    if (holding.Add(entry.Who))
                    {
                        holders.Add(new Actor(entry.Who, entry.Because));
                    }
                }

                List<Actor> held = new List<Actor>();
                for (int i = 0; i < captiveOrder.Count; i++)
                {
                    Actor captive;
                    if (!captives.TryGetValue(captiveOrder[i], out captive))
                    {
                        continue;
                    }

                    held.Add(captive);
                    WorldEvent capture = Find(world, captive.Because);
                    if (capture != null && holding.Add(capture.Actor))
                    {
                        holders.Add(new Actor(capture.Actor, capture.Id));
                    }
                }

                return new History(holders, held, takenNow);
            }

            private static bool Belongs(NarrativeThread thread, WorldEvent worldEvent)
            {
                return (!worldEvent.ThreadId.IsNone && worldEvent.ThreadId == thread.Id)
                       || (!thread.OriginEventId.IsNone && thread.OriginEventId == worldEvent.Id);
            }

            private static WorldEvent Find(NarrativeWorldState world, EntityId eventId)
            {
                IReadOnlyList<WorldEvent> events = world.Ledger.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Id == eventId)
                    {
                        return events[i];
                    }
                }

                return null;
            }
        }
    }
}
