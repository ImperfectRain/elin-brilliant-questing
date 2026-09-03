using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Continuity
{
    /// <summary>
    /// How much of the other side of an event a caller needs the world to be able to produce.
    ///
    /// The distinction the single "available" flag used to collapse. Being referable and being
    /// present are different properties, they fail for different reasons, and a callback needs the
    /// first far more often than the second: most of what a town says about somebody is said when
    /// they are not in the room, and a good deal of it after they are dead.
    /// </summary>
    public enum CallbackParties
    {
        /// <summary>
        /// Anybody history can still name - present, away or dead alike. The default, and the
        /// honest reading of what a callback is: a reference to something that happened, not a
        /// claim about who is standing here now. Somebody the registry cannot produce at all is
        /// still excluded, because there is no name to say and nothing to describe.
        /// </summary>
        Referable = 0,

        /// <summary>
        /// Only somebody the world can put in front of a scene right now. What a caller asks for
        /// when the callback is a step toward staging the person rather than toward mentioning
        /// them - and never the default, because most references are only ever mentions.
        /// </summary>
        Stageable = 1,

        /// <summary>
        /// Everything, unavailable and unidentifiable parties included. For the inspector and for
        /// a caller that has its own reason to see the whole of what the ledger left.
        /// </summary>
        Any = 2
    }

    /// <summary>What a caller wants out of the ledger, and what it will not accept.</summary>
    public sealed class CallbackSelection
    {
        /// <summary>
        /// How old the material has to be, in whole in-game days. Defaults to
        /// <see cref="CallbackHooks.SettledDays"/>; a caller that wants everything sets it to zero.
        /// </summary>
        public long MinimumAgeInDays { get; set; } = CallbackHooks.SettledDays;

        /// <summary>
        /// Somebody the event has to have involved, or <see cref="EntityId.None"/> for no such
        /// restriction. This is how a scene asks for the history it shares with the person in
        /// front of it rather than for the recaller's whole life.
        /// </summary>
        public EntityId About { get; set; }

        /// <summary>
        /// Which other parties this caller will accept. Defaults to
        /// <see cref="CallbackParties.Referable"/> - everybody history can still name, the dead and
        /// the departed included, because remembering somebody is not claiming they are here. A
        /// caller that needs the other party actually in front of somebody asks for
        /// <see cref="CallbackParties.Stageable"/>; see <see cref="CallbackParty"/>.
        /// </summary>
        public CallbackParties Parties { get; set; } = CallbackParties.Referable;

        /// <summary>How many to return at most. Zero or less means no limit.</summary>
        public int Limit { get; set; } = 8;
    }

    /// <summary>
    /// BQ-081. Reusable narrative material, read off the one history that already exists (CD §24).
    ///
    /// <b>There is no callback store.</b> No second ledger, no persisted index, no kept list of
    /// hooks. They are derived from <c>EventLedger</c> on demand exactly as <c>Chronicle</c>
    /// derives what is finished, so they cannot drift from history, cannot outlive a retracted event, and cannot
    /// become a second thing to migrate. That also settles persistence: nothing here is saved,
    /// because everything here is already in the save. See decision <c>D039</c>.
    ///
    /// <b>Nothing is derived for somebody who could not know it.</b> The routes in
    /// <see cref="CallbackRoute"/> are the whole gate, and they are the same ones the rest of the
    /// simulation already trusts - the event's own witness list, the <c>unnoticed</c> tag that
    /// tells <c>ConsequenceEngine</c> nobody reacted, and confident belief in a claim the event is
    /// the origin of. A globally true event with no route is simply not material, which is why a
    /// clean theft never becomes a remark by the person it was taken from.
    ///
    /// <b>It selects; it does not speak.</b> The result is ids and readings. Which one to use and
    /// when to stop is the consumer's, wording is <c>DialogueRealizer</c>'s, and whether a known
    /// thing may be said to this listener is disclosure's (BQ-071 through BQ-073). Recurrence and
    /// the humour it earns are BQ-082's, and nothing here decides them.
    ///
    /// <b>A route is permission to remember, never permission to tell.</b> Everything on this class
    /// answers one question - may this person hold this history - and that question has no listener
    /// in it. Whether they would bring it up in front of a particular person is
    /// <c>CallbackDisclosure</c>'s, which asks the same <c>Disclosure</c> that decides it for every
    /// other claim; a hook that reaches wording without having been asked is refused by
    /// <c>RealizationRequest.WhyNot</c> rather than quietly spoken.
    ///
    /// <b>Being remembered is not being present.</b> Selection admits everybody history can still
    /// name, the dead and the departed included, because that is what a callback refers to. What
    /// the world can still <em>produce</em> is reported separately as <see cref="CallbackParty"/>
    /// and asked for separately as <see cref="CallbackParties.Stageable"/>.
    ///
    /// <b>It is deterministic.</b> Ordering is salience descending with ties broken on event id,
    /// the convention <c>TalkRepertoire</c> already uses, so the same world gives the same answer
    /// however the ledger was walked.
    ///
    /// <b>Retrieval is a scan, as <c>Chronicle</c>'s is.</b> If a long save ever makes that cost
    /// worth removing, what may be added is an index over the ledger that a hook is still derived
    /// through - never a kept list of hooks, which would be the second history this whole shape
    /// exists to avoid.
    /// </summary>
    public static class CallbackHooks
    {
        /// <summary>
        /// The age at which history has settled enough to be worth remarking on unprompted, in
        /// whole in-game days, and BQ-081's own done-when threshold.
        /// </summary>
        public const long SettledDays = 10;

        /// <summary>Below this, a hearsay belief is not knowledge of anything (matches <c>Knows</c>' own bar).</summary>
        private const double HeardConfidenceFloor = 0.5;

        private static readonly CallbackKind[] NoKinds = new CallbackKind[0];
        private static readonly CallbackKind[] JustPromise = { CallbackKind.Promise };
        private static readonly CallbackKind[] JustKindness = { CallbackKind.Kindness };
        private static readonly CallbackKind[] JustEmbarrassment = { CallbackKind.Embarrassment };
        private static readonly CallbackKind[] JustScandal = { CallbackKind.Scandal };
        private static readonly CallbackKind[] BrokenUndertaking = { CallbackKind.Promise, CallbackKind.Embarrassment, CallbackKind.Scandal };
        private static readonly CallbackKind[] GivenBack = { CallbackKind.Kindness, CallbackKind.LostObject };
        private static readonly CallbackKind[] PulledOut = { CallbackKind.Kindness, CallbackKind.Injury };
        private static readonly CallbackKind[] Violence = { CallbackKind.Injury, CallbackKind.Scandal };
        private static readonly CallbackKind[] ShownUp = { CallbackKind.Embarrassment, CallbackKind.Scandal };
        private static readonly CallbackKind[] TakenAway = { CallbackKind.Scandal, CallbackKind.LostObject };
        private static readonly CallbackHook[] NoHooks = new CallbackHook[0];
        private static readonly EntityId[] NoIdList = new EntityId[0];
        private static readonly CallbackSelection Default = new CallbackSelection();

        /// <summary>
        /// What kind of material this sort of event leaves, in <see cref="CallbackKind"/> order.
        ///
        /// One table, read from the event's recorded type and nothing else. It never reads the
        /// event's prose - there is none on a <c>WorldEvent</c> - and it never reads a fact's
        /// wording, so retitling anything changes no hook. An event type absent from it leaves
        /// nothing reusable and produces no hook at all; that is the honest answer for
        /// <c>Met</c>, <c>Conversed</c> and the bookkeeping the thread engine writes.
        /// </summary>
        public static IReadOnlyList<CallbackKind> KindsOf(WorldEventType type)
        {
            switch (type)
            {
                case WorldEventType.PromiseMade:
                case WorldEventType.FavorOwed:
                case WorldEventType.DebtCreated:
                case WorldEventType.DebtPaid:
                case WorldEventType.FavorRedeemed:
                    return JustPromise;

                case WorldEventType.PromiseBroken:
                    return BrokenUndertaking;

                // Not a promise. Somebody with the standing to say yes said no, and an answer is
                // not an undertaking - filing it as one would let a fragment written for "what
                // stands between us" be chosen for a refusal.
                case WorldEventType.RequestDeclined:
                    return JustEmbarrassment;

                case WorldEventType.Helped:
                case WorldEventType.TakenIn:
                case WorldEventType.ItemGiven:
                    return JustKindness;

                case WorldEventType.ItemReturned:
                    return GivenBack;

                case WorldEventType.Rescued:
                    return PulledOut;

                case WorldEventType.Harmed:
                case WorldEventType.Attacked:
                case WorldEventType.Killed:
                case WorldEventType.Captured:
                case WorldEventType.Threatened:
                    return Violence;

                case WorldEventType.Deceived:
                case WorldEventType.DeceptionExposed:
                case WorldEventType.FalseAccusation:
                case WorldEventType.AccusationRejected:
                    return ShownUp;

                case WorldEventType.Theft:
                case WorldEventType.EvidenceDestroyed:
                    return TakenAway;

                case WorldEventType.Trespass:
                case WorldEventType.Bribed:
                case WorldEventType.SecretRevealed:
                case WorldEventType.AccusationMade:
                case WorldEventType.CrimeReported:
                case WorldEventType.OrganizationBetrayed:
                    return JustScandal;

                default:
                    return NoKinds;
            }
        }

        /// <summary>
        /// Whether this person may recall this event at all, and how they come to it.
        ///
        /// Public because "why is there no callback here" is a question the inspector has to be
        /// able to answer, and because the gate is the interesting half of the step.
        /// </summary>
        public static bool TryRoute(NarrativeWorldState world, WorldEvent worldEvent, EntityId recaller, out CallbackRoute route)
        {
            route = CallbackRoute.FirstHand;
            if (world == null || worldEvent == null || recaller.IsNone)
            {
                return false;
            }

            if (worldEvent.Actor == recaller)
            {
                route = CallbackRoute.FirstHand;
                return true;
            }

            // Nobody noticed, so nobody but the person who did it has anything to remember. This
            // is the same tag that stops `ConsequenceEngine` moving a robbed shopkeeper's affinity,
            // read here for the same reason: a reaction to an unnoticed act is itself information.
            bool noticed = !HasTag(worldEvent, EventTags.Unnoticed);

            if (noticed && worldEvent.Target == recaller)
            {
                route = CallbackRoute.Involved;
                return true;
            }

            if (noticed && Contains(worldEvent.Witnesses, recaller))
            {
                route = CallbackRoute.Witnessed;
                return true;
            }

            if (BelievesSomethingThisEventBegan(world, worldEvent, recaller))
            {
                route = CallbackRoute.Heard;
                return true;
            }

            return false;
        }

        /// <summary>
        /// This one event as this one person may recall it, or null when there is no material in
        /// it or no route to it. Age is not consulted: that is <see cref="For"/>'s filter, and a
        /// caller asking about a named event has already decided it is the one it wants.
        /// </summary>
        public static CallbackHook Of(
            NarrativeWorldState world,
            IVanillaState vanilla,
            WorldEvent worldEvent,
            EntityId recaller,
            GameTime now)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (worldEvent == null || recaller.IsNone)
            {
                return null;
            }

            IReadOnlyList<CallbackKind> kinds = KindsOf(worldEvent.Type);
            if (kinds.Count == 0)
            {
                return null;
            }

            CallbackRoute route;
            if (!TryRoute(world, worldEvent, recaller, out route))
            {
                return null;
            }

            EntityId counterpart = CounterpartOf(worldEvent, recaller);
            int publicity = PublicityOf(world, worldEvent);

            return new CallbackHook(
                worldEvent.Id,
                worldEvent.Type,
                recaller,
                route,
                kinds,
                counterpart,
                PartyOf(world, vanilla, counterpart),
                Principals(worldEvent),
                worldEvent.Evidence,
                ClaimsOf(world, worldEvent),
                worldEvent.Zone,
                worldEvent.ThreadId,
                worldEvent.Time,
                now.DaysSince(worldEvent.Time),
                Clamp01(worldEvent.Magnitude),
                EmbarrassmentOf(worldEvent, recaller, publicity),
                publicity);
        }

        /// <summary>
        /// Everything this person may bring up unprompted, most striking first.
        ///
        /// "Unprompted" is a property of the call: nothing in the arguments names an event, so what
        /// comes back is what the world offers rather than what somebody asked after.
        /// </summary>
        public static IReadOnlyList<CallbackHook> For(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            GameTime now,
            CallbackSelection selection = null)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (recaller.IsNone)
            {
                return NoHooks;
            }

            CallbackSelection rules = selection ?? Default;
            List<CallbackHook> hooks = new List<CallbackHook>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;

            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (now.DaysSince(worldEvent.Time) < rules.MinimumAgeInDays)
                {
                    continue;
                }

                if (!rules.About.IsNone && !Involves(worldEvent, rules.About))
                {
                    continue;
                }

                CallbackHook hook = Of(world, vanilla, worldEvent, recaller, now);
                if (hook == null)
                {
                    continue;
                }

                if (!Admits(rules.Parties, hook.Party))
                {
                    continue;
                }

                hooks.Add(hook);
            }

            hooks.Sort(Compare);

            if (rules.Limit > 0 && hooks.Count > rules.Limit)
            {
                hooks.RemoveRange(rules.Limit, hooks.Count - rules.Limit);
            }

            return hooks;
        }

        /// <summary>The one a scene would reach for, or null when there is nothing to reach for.</summary>
        public static CallbackHook Best(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            GameTime now,
            CallbackSelection selection = null)
        {
            IReadOnlyList<CallbackHook> hooks = For(world, vanilla, recaller, now, selection);
            return hooks.Count == 0 ? null : hooks[0];
        }

        /// <summary>
        /// How much a hook asks to be used, and the only ordering there is.
        ///
        /// The event's own weight, discounted by how far the recaller stands from it: what somebody
        /// did themselves or had done to them outranks what they watched, which outranks what they
        /// were told. Nothing in it is random, and nothing in it reads the clock, so a save reloaded
        /// mid-conversation offers the same material in the same order.
        /// </summary>
        public static double SalienceOf(CallbackHook hook)
        {
            if (hook == null)
            {
                return 0.0;
            }

            double distance;
            switch (hook.Route)
            {
                case CallbackRoute.FirstHand:
                case CallbackRoute.Involved:
                    distance = 1.0;
                    break;
                case CallbackRoute.Witnessed:
                    distance = 0.8;
                    break;
                default:
                    distance = 0.6;
                    break;
            }

            return hook.Weight * distance;
        }

        /// <summary>Most striking first, ties broken on event id so nothing depends on ledger walk order.</summary>
        private static int Compare(CallbackHook a, CallbackHook b)
        {
            double left = SalienceOf(a);
            double right = SalienceOf(b);
            if (left != right)
            {
                return left > right ? -1 : 1;
            }

            return string.CompareOrdinal(a.EventId.Value, b.EventId.Value);
        }

        /// <summary>
        /// The other principal from the recaller's side. Whoever watched or was told is looking at
        /// the person who acted; whoever acted is looking at whom they acted on.
        /// </summary>
        private static EntityId CounterpartOf(WorldEvent worldEvent, EntityId recaller)
        {
            if (worldEvent.Actor == recaller)
            {
                return worldEvent.Target;
            }

            return worldEvent.Actor.IsNone ? worldEvent.Target : worldEvent.Actor;
        }

        private static CallbackParty PartyOf(NarrativeWorldState world, IVanillaState vanilla, EntityId counterpart)
        {
            if (counterpart.IsNone)
            {
                return CallbackParty.None;
            }

            if (world.Registry.GetNpc(counterpart) == null)
            {
                return CallbackParty.Unknown;
            }

            if (vanilla != null && !vanilla.IsAlive(counterpart))
            {
                return CallbackParty.Gone;
            }

            return world.Absences.IsPhysicallyAbsent(counterpart) ? CallbackParty.Away : CallbackParty.Present;
        }

        /// <summary>
        /// Whether history can still name this party: everybody except somebody the registry cannot
        /// produce at all.
        ///
        /// The dead pass. That is the point of the split: an event's other side going into the
        /// ground does not make the event stop having happened, and a settlement that could no
        /// longer say "after what your father did for me" would have lost the most durable callback
        /// there is. What being dead costs is <em>staging</em>, not reference, and
        /// <see cref="IsStageable"/> is where that is charged.
        /// </summary>
        public static bool IsReferable(CallbackParty party)
        {
            return party != CallbackParty.Unknown;
        }

        /// <summary>
        /// Whether the world could put this party in front of somebody now. The narrower question,
        /// asked by a caller whose use of the hook needs a live person rather than a memory of one.
        /// </summary>
        public static bool IsStageable(CallbackParty party)
        {
            return party == CallbackParty.None || party == CallbackParty.Present || party == CallbackParty.Away;
        }

        private static bool Admits(CallbackParties wanted, CallbackParty party)
        {
            switch (wanted)
            {
                case CallbackParties.Stageable:
                    return IsStageable(party);
                case CallbackParties.Any:
                    return true;
                default:
                    return IsReferable(party);
            }
        }

        /// <summary>
        /// How far out this already is, 0..100.
        ///
        /// Its two inputs are both recorded: who the event says could see it, and how hidden the
        /// claims it names are. An unnoticed act is at zero however large it was, which is what
        /// keeps a perfect theft from reading as a talking point.
        /// </summary>
        private static int PublicityOf(NarrativeWorldState world, WorldEvent worldEvent)
        {
            if (HasTag(worldEvent, EventTags.Unnoticed))
            {
                return 0;
            }

            int seen = worldEvent.Witnesses.Count;
            if (seen > 3)
            {
                seen = 3;
            }

            int reach = 25 + (25 * seen);
            int secrecy = 0;
            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(worldEvent.Related[i]);
                if (fact != null && fact.Secrecy > secrecy)
                {
                    secrecy = fact.Secrecy;
                }
            }

            int publicity = reach - secrecy;
            return publicity < 0 ? 0 : publicity;
        }

        /// <summary>
        /// The recaller's own exposure, 0..100, and nobody else's.
        ///
        /// Zero unless the event is one that shows somebody up and they are the one it showed up.
        /// Scaled by publicity, because being caught out where nobody was looking costs nothing.
        /// </summary>
        private static int EmbarrassmentOf(WorldEvent worldEvent, EntityId recaller, int publicity)
        {
            EntityId shown;
            switch (worldEvent.Type)
            {
                case WorldEventType.PromiseBroken:
                case WorldEventType.DeceptionExposed:
                case WorldEventType.FalseAccusation:
                case WorldEventType.AccusationRejected:
                    shown = worldEvent.Actor;
                    break;

                // The one turned down is the one who has to walk back out, not the one who said no.
                case WorldEventType.RequestDeclined:
                case WorldEventType.Deceived:
                case WorldEventType.Captured:
                    shown = worldEvent.Target;
                    break;

                default:
                    return 0;
            }

            if (shown != recaller)
            {
                return 0;
            }

            return (int)Math.Round(100.0 * Clamp01(worldEvent.Magnitude) * (publicity / 100.0));
        }

        /// <summary>
        /// A belief in something this event is the origin of, held firmly enough to be knowledge.
        ///
        /// A distorted version does not count. Somebody repeating a garbled story knows a story;
        /// letting that stand as a route would let a callback speak with the authority of history
        /// about a version history never recorded.
        /// </summary>
        private static bool BelievesSomethingThisEventBegan(NarrativeWorldState world, WorldEvent worldEvent, EntityId recaller)
        {
            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(worldEvent.Related[i]);
                if (fact == null || fact.OriginEvent != worldEvent.Id || !fact.DistortionOf.IsNone)
                {
                    continue;
                }

                if (world.Knowledge.BelievesConfidently(recaller, fact.Id, HeardConfidenceFloor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The claims the event named, filtered to the ones the knowledge graph can resolve.
        ///
        /// <c>WorldEvent.Related</c> is a general list of ids, so this keeps only what is actually
        /// a <c>Fact</c> - the same read <see cref="PublicityOf"/> already makes of it. Nothing is
        /// minted here: an event that named no claim leaves an empty list, and no claim is invented
        /// to stand for what it was about.
        /// </summary>
        private static IReadOnlyList<EntityId> ClaimsOf(NarrativeWorldState world, WorldEvent worldEvent)
        {
            List<EntityId> claims = null;
            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                if (world.Knowledge.GetFact(worldEvent.Related[i]) == null)
                {
                    continue;
                }

                if (claims == null)
                {
                    claims = new List<EntityId>();
                }

                claims.Add(worldEvent.Related[i]);
            }

            return claims == null ? (IReadOnlyList<EntityId>)NoIdList : claims.ToArray();
        }

        private static IReadOnlyList<EntityId> Principals(WorldEvent worldEvent)
        {
            List<EntityId> principals = new List<EntityId>();
            if (!worldEvent.Actor.IsNone)
            {
                principals.Add(worldEvent.Actor);
            }

            if (!worldEvent.Target.IsNone && worldEvent.Target != worldEvent.Actor)
            {
                principals.Add(worldEvent.Target);
            }

            return principals.ToArray();
        }

        private static bool Involves(WorldEvent worldEvent, EntityId who)
        {
            return worldEvent.Actor == who
                   || worldEvent.Target == who
                   || Contains(worldEvent.Witnesses, who)
                   || Contains(worldEvent.Related, who);
        }

        private static bool Contains(IReadOnlyList<EntityId> list, EntityId id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTag(WorldEvent worldEvent, string tag)
        {
            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                if (string.Equals(worldEvent.Tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }
}
