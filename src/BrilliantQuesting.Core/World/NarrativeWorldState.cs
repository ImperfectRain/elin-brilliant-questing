using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The authoritative procedural database.
    ///
    /// Vanilla objects stay authoritative for vanilla facts (real stats, real inventory, real shop
    /// level). This owns identity, causal history, belief, relationship context and narrative
    /// state - the things Elin has no place to put.
    /// </summary>
    public sealed class NarrativeWorldState
    {
        /// <summary>Bumped whenever the persisted shape changes; drives save migration.</summary>
        public const int CurrentSchemaVersion = 6;

        public NarrativeWorldState(ulong worldSeed)
        {
            WorldSeed = worldSeed;
            SchemaVersion = CurrentSchemaVersion;
            Ids = new IdMinter();
            Registry = new EntityRegistry();
            Ledger = new EventLedger();
            Knowledge = new KnowledgeGraph();
            Memories = new MemoryLedger();
            Relationships = new RelationshipGraph();
            Obligations = new SocialObligationLedger();
            Threads = new List<NarrativeThread>();
            Absences = new AbsenceLedger();
            Demands = new LocalDemandLedger();
            Businesses = new BusinessLedger();
            Rng = new DeterministicRng(worldSeed);
        }

        public int SchemaVersion { get; set; }

        public ulong WorldSeed { get; }

        public IdMinter Ids { get; }

        public EntityRegistry Registry { get; }

        public EventLedger Ledger { get; }

        public KnowledgeGraph Knowledge { get; }

        public MemoryLedger Memories { get; }

        public RelationshipGraph Relationships { get; }

        /// <summary>
        /// Concrete social debts: favors, promises, sanctuary, sponsorships, grudges and debts.
        /// These are scoped records with sources in history, not a second affinity meter.
        /// </summary>
        public SocialObligationLedger Obligations { get; }

        public List<NarrativeThread> Threads { get; }

        /// <summary>
        /// Who is away and why. Procedural state in the strictest sense: for a Grade A absence
        /// nothing in Elin has changed at all, and for a Grade B one this is the only thing that
        /// knows the departure was deliberate rather than a character who wandered off.
        ///
        /// It lives on the world, and therefore in the save, because an absence the game keeps and
        /// the simulation forgets is somebody stranded wherever the mod left them.
        /// <see cref="AbsenceLifecycle"/> is what puts records in and takes them out.
        /// </summary>
        public AbsenceLedger Absences { get; }

        /// <summary>
        /// Coarse local pressures such as Food, Medicine and Safety. These are persistent
        /// narrative pressures over places, not a commodity simulator and not a quest counter.
        /// </summary>
        public LocalDemandLedger Demands { get; }

        /// <summary>
        /// Persistent business continuity. Elin still owns the live shop surface: the operator's
        /// sleep, hobby, shift state and stock are projection inputs, not saved procedural facts.
        /// </summary>
        public BusinessLedger Businesses { get; }

        /// <summary>World-level stream. Subsystems should Fork() rather than draw from this.</summary>
        public DeterministicRng Rng { get; }

        /// <summary>
        /// The adapter's handle for an entity, keyed by the id the simulation uses.
        ///
        /// Opaque to Core: it is a string because what is on the other side is not Core's business.
        /// It exists because identity has to outlive a session. `NarrativeNpc.VanillaCharaRef`
        /// already did this for people, and nothing did it for anything else - so after a reload
        /// the adapter could not recognise the very object a situation was about, and a stolen
        /// ring came back as a different ring.
        /// </summary>
        public Dictionary<EntityId, string> ExternalRefs { get; } = new Dictionary<EntityId, string>();

        /// <summary>No round has run in this world yet. Days are never negative, so -1 is free.</summary>
        public const long RumorsNeverCirculated = -1;

        /// <summary>
        /// The in-game day the last round of gossip belongs to.
        ///
        /// Lives on the world rather than in the adapter because it has to survive a reload. If
        /// the scheduler kept its own counter, loading the same save would circulate again, and a
        /// player who did not like what the town started saying could reroll it from the load
        /// screen. Additive and optional in the save: an older one has no node, reads back as
        /// never-circulated, and quietly starts from the day it is opened.
        /// </summary>
        public long LastRumorDay { get; set; } = RumorsNeverCirculated;

        /// <summary>Nobody near the player has said anything yet. Minutes are never negative.</summary>
        public const long NothingSaidYet = -1;

        /// <summary>
        /// The in-game minute somebody last mentioned something within the player's hearing.
        ///
        /// On the world for the same reason <see cref="LastRumorDay"/> is: it paces something the
        /// player cannot be allowed to pump. Ambient talk is the one route by which the player
        /// gains knowledge simply by being present, so a counter the adapter kept would reset every
        /// load and turn a save-and-reload into a way to empty the town's news into the journal in
        /// one standing. Additive and optional in the save: an older one has no node and reads back
        /// as nothing said yet.
        /// </summary>
        public long LastAmbientRemarkMinute { get; set; } = NothingSaidYet;

        public EntityId NewId(string kind) => Ids.Next(kind);

        /// <summary>
        /// Appends an event and dispatches it to every listener. All consequence handling hangs
        /// off this one call, which is what keeps causality inspectable.
        /// </summary>
        public WorldEvent Record(
            WorldEventType type,
            EntityId actor,
            EntityId target,
            GameTime now,
            double magnitude = 0.5,
            EntityId zone = default,
            IReadOnlyList<EntityId> related = null,
            IReadOnlyList<EntityId> witnesses = null,
            IReadOnlyList<EntityId> evidence = null,
            IReadOnlyList<string> tags = null,
            EntityId threadId = default)
        {
            WorldEvent worldEvent = new WorldEvent(
                NewId("evt"), type, actor, target, now, magnitude, zone, related, witnesses, evidence, tags, threadId);
            Ledger.Append(worldEvent);
            return worldEvent;
        }

        public NarrativeThread GetThread(EntityId id)
        {
            for (int i = 0; i < Threads.Count; i++)
            {
                if (Threads[i].Id == id)
                {
                    return Threads[i];
                }
            }

            return null;
        }
    }
}
