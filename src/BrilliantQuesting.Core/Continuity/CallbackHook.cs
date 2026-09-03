using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Continuity
{
    /// <summary>
    /// The kinds of reusable material an event can leave behind (CD §24).
    ///
    /// Every one of them is a distinction history already records, and that is the whole of the
    /// admission rule. The design's list also names <c>nickname</c> and <c>weird incident</c>;
    /// neither is here, because nothing in the simulation mints a nickname and weirdness is a
    /// property of a scene's premise that the caller states (BQ-079), not something the ledger
    /// writes down. Deriving either would mean this layer inventing the fact it then referred
    /// back to, which is exactly what a callback must never do. The vocabulary grows when a
    /// recorder for one of them exists, the same way <c>SpeechActType</c>'s does.
    ///
    /// An event usually leaves more than one kind - a theft is a scandal and a missing object at
    /// once - so a hook carries a set. <see cref="CallbackHook.PrimaryKind"/> is the one wording
    /// may select on.
    /// </summary>
    public enum CallbackKind
    {
        /// <summary>An undertaking, a debt, a favour, or the failure to honour one.</summary>
        Promise,

        /// <summary>Somebody was done well by: helped, pulled out, given back what was theirs, taken in.</summary>
        Kindness,

        /// <summary>Somebody was hurt, seized or leaned on.</summary>
        Injury,

        /// <summary>Being caught out, turned down or shown up in front of people.</summary>
        Embarrassment,

        /// <summary>Something that costs standing when it is repeated.</summary>
        Scandal,

        /// <summary>A thing that went out of somebody's hands and did not come back.</summary>
        LostObject
    }

    /// <summary>
    /// How the person making the callback comes to know the event happened.
    ///
    /// The whole of BQ-081's knowledge gate, and the reason a hook is derived per recaller rather
    /// than per event: there is no such thing as a callback the world holds, only one that
    /// somebody is entitled to make. An event nobody has a route to yields no hook at all, which
    /// is how history that is true but private stays unsayable.
    /// </summary>
    public enum CallbackRoute
    {
        /// <summary>They did it.</summary>
        FirstHand,

        /// <summary>It was done to them, and it was not done unnoticed.</summary>
        Involved,

        /// <summary>They are on the event's own witness list.</summary>
        Witnessed,

        /// <summary>
        /// They confidently believe a claim this event is the origin of - somebody told them, or
        /// they worked it out. A garbled version does not count: a belief that is a distortion of
        /// another claim is knowledge of a story, not of what happened.
        /// </summary>
        Heard
    }

    /// <summary>
    /// What the world can still produce of the person on the other side of a recalled event.
    ///
    /// The same honesty <c>SceneStatus</c> keeps for a live situation, kept here for a dead one: a
    /// callback that speaks of somebody as though they were still standing there is the failure
    /// BQ-008 exists to prevent, and history going on being true about them is not a licence to
    /// imply they are around.
    /// </summary>
    public enum CallbackParty
    {
        /// <summary>The event names nobody on the other side. Not a degradation.</summary>
        None,

        /// <summary>Alive, known to the simulation, and not away.</summary>
        Present,

        /// <summary>Alive and known, but absent from where they usually are (BQ-020).</summary>
        Away,

        /// <summary>Known to the simulation and no longer alive.</summary>
        Gone,

        /// <summary>Not a person the registry can produce - never was one, or no longer is.</summary>
        Unknown
    }

    /// <summary>
    /// Reusable narrative material from one recorded event, as one person is entitled to recall it
    /// (CD §24).
    ///
    /// <b>It is a reference, never a copy.</b> Everything on it is either the id of something the
    /// save already holds - <see cref="EventId"/>, <see cref="Participants"/>,
    /// <see cref="Objects"/>, <see cref="Place"/>, <see cref="ThreadId"/> - or a reading computed
    /// from that event's own recorded fields. There is no prose on this type, no summary sentence
    /// and no second copy of what happened, which is what keeps the ledger the single history.
    /// Rewriting an event changes every hook to it; nothing here can go on asserting the old
    /// version.
    ///
    /// <b>It belongs to somebody.</b> <see cref="Recaller"/> and <see cref="Route"/> say who may
    /// make this callback and how they know. A hook is never derived for a person with no route to
    /// the event, so "do not leak private history" is a fact about which hooks exist rather than a
    /// rule consumers have to remember.
    ///
    /// <b>It is derived, never stored.</b> Hooks are read off the ledger on demand, the way
    /// <c>Chronicle</c> is, so they survive a reload for the reason the ledger does and add no save
    /// schema of their own. See decision <c>D039</c>.
    ///
    /// <b>It stops before wording and before judgement.</b> Whether now is the moment to bring
    /// this up, and what humour it is worth, is BQ-082's; whether it may be said to this listener
    /// is disclosure's (BQ-071 through BQ-073). This type only says the material is there and what
    /// it is made of.
    /// </summary>
    public sealed class CallbackHook
    {
        private static readonly EntityId[] NoIds = new EntityId[0];

        internal CallbackHook(
            EntityId eventId,
            WorldEventType eventType,
            EntityId recaller,
            CallbackRoute route,
            IReadOnlyList<CallbackKind> kinds,
            EntityId counterpart,
            CallbackParty party,
            IReadOnlyList<EntityId> participants,
            IReadOnlyList<EntityId> objects,
            EntityId place,
            EntityId threadId,
            GameTime at,
            long ageInDays,
            double weight,
            int embarrassment,
            int publicity)
        {
            EventId = eventId;
            EventType = eventType;
            Recaller = recaller;
            Route = route;
            Kinds = kinds;
            Counterpart = counterpart;
            Party = party;
            Participants = participants ?? NoIds;
            Objects = objects ?? NoIds;
            Place = place;
            ThreadId = threadId;
            At = at;
            AgeInDays = ageInDays;
            Weight = weight;
            Embarrassment = embarrassment;
            Publicity = publicity;
        }

        /// <summary>The event in the ledger this refers to. The only source of what happened.</summary>
        public EntityId EventId { get; }

        /// <summary>Its recorded type, carried so a consumer need not re-read the ledger to sort hooks.</summary>
        public WorldEventType EventType { get; }

        /// <summary>Whose callback this is. Never blank.</summary>
        public EntityId Recaller { get; }

        public CallbackRoute Route { get; }

        /// <summary>Every kind of material this event left, in <see cref="CallbackKind"/> order.</summary>
        public IReadOnlyList<CallbackKind> Kinds { get; }

        /// <summary>
        /// The one kind wording may be chosen on. First in <see cref="CallbackKind"/>'s own order,
        /// which runs from the kinds that name something between two people to the diffuse ones,
        /// so a broken promise reads as a promise rather than as a generic scandal.
        /// </summary>
        public CallbackKind PrimaryKind => Kinds[0];

        /// <summary>
        /// The other principal, from the recaller's side: who they did it to, who did it to them,
        /// or who did it while they watched. <see cref="EntityId.None"/> when the event names
        /// nobody else.
        /// </summary>
        public EntityId Counterpart { get; }

        /// <summary>What the world can still produce of <see cref="Counterpart"/>.</summary>
        public CallbackParty Party { get; }

        /// <summary>Actor and target as history recorded them, stable ids and nothing else.</summary>
        public IReadOnlyList<EntityId> Participants { get; }

        /// <summary>The objects the event turned on - its recorded evidence.</summary>
        public IReadOnlyList<EntityId> Objects { get; }

        /// <summary>Where it happened, as the event recorded it.</summary>
        public EntityId Place { get; }

        /// <summary>The matter it belonged to, when the verb that wrote it recorded one.</summary>
        public EntityId ThreadId { get; }

        public GameTime At { get; }

        /// <summary>Whole in-game days between the event and the moment it was derived at.</summary>
        public long AgeInDays { get; }

        /// <summary>
        /// How much of a thing it was, 0..1: the event's own recorded magnitude and nothing else.
        ///
        /// Deliberately not the impression it left on <see cref="Recaller"/>, which is what
        /// <c>MemoryRecord</c> holds. A memory carries no id of the event it came from, so tying
        /// the two together would mean matching on type and timestamp - and consolidation moves a
        /// memory's timestamp. A guess dressed as provenance is worse than the plain figure.
        /// </summary>
        public double Weight { get; }

        /// <summary>
        /// 0..100, and the recaller's own exposure in it - never anybody else's. Zero unless they
        /// are the one the event showed up, and scaled by <see cref="Publicity"/>, because being
        /// caught out where nobody saw is not embarrassment.
        ///
        /// Whoever is on the other side has their own hooks with their own figure; asking for
        /// theirs is how a scene finds out what it would cost <em>them</em>.
        /// </summary>
        public int Embarrassment { get; }

        /// <summary>
        /// 0..100: how far this is already out, from the event's own witness list and the secrecy
        /// of the claims it names. Zero for anything recorded unnoticed.
        /// </summary>
        public int Publicity { get; }

        /// <summary>
        /// A wording-free identity, in the sense <c>SpeechAct.Signature</c> is one: same hook,
        /// same string. Age is left out on purpose - a hook does not become a different hook by
        /// being recalled a day later.
        /// </summary>
        public string Signature =>
            Recaller.Value + "|" + EventId.Value + "|" + PrimaryKind + "|" + Route + "|" + Party;

        public override string ToString() => Signature;
    }
}
