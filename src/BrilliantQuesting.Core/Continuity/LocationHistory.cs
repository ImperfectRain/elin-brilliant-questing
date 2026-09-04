using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Continuity
{
    /// <summary>
    /// What one recorded event was, as far as a place is concerned (PM §40).
    ///
    /// Three members, and each of them has a recorder. <see cref="Found"/> and <see cref="Cleared"/>
    /// are the two event types that name a place rather than a person - the only two the ledger
    /// has - and <see cref="Incident"/> is everything else that left material somebody could bring
    /// up afterwards. The design's richer vocabulary - occupied, ruined, repurposed, forgotten
    /// (`LW §7.7`) - is absent for the same reason <see cref="ProvenanceRole"/> has no "inherited
    /// by": nothing in the simulation records an occupancy change yet, and deriving one from who
    /// happens to be standing there would be this layer inventing the history it then reports. The
    /// vocabulary grows when a recorder for one of them exists.
    /// </summary>
    public enum SiteHistoryRole
    {
        /// <summary>Somebody found the place.</summary>
        Found,

        /// <summary>Somebody got past whatever was holding the place shut.</summary>
        Cleared,

        /// <summary>Something happened here that people could still bring up afterwards.</summary>
        Incident
    }

    /// <summary>
    /// One recorded event in one place's history.
    ///
    /// <b>A reference, never a copy.</b> Everything here is an id the save already holds or a
    /// reading of that event's own recorded fields, on the terms <c>D039</c> and <c>D057</c> set:
    /// there is no history store on a site, no index and no save entry, so a place's past survives
    /// a reload because the ledger does, and a corrected event corrects every entry that reads it.
    /// Nothing here is prose, and nothing here is geometry.
    /// </summary>
    public sealed class SiteHistoryEntry
    {
        internal SiteHistoryEntry(
            EntityId siteId,
            WorldEvent worldEvent,
            SiteHistoryRole role,
            IReadOnlyList<CallbackKind> kinds,
            long ageInDays,
            EntityId knower,
            CallbackRoute? knownVia)
        {
            SiteId = siteId;
            EventId = worldEvent.Id;
            EventType = worldEvent.Type;
            Role = role;
            Kinds = kinds;
            Actor = worldEvent.Actor;

            // A place-naming event carries the place itself as its target, so there is nobody on
            // the other side of it. Reporting the site id as a second party would hand every
            // consumer a place dressed as a person.
            Other = role == SiteHistoryRole.Incident ? worldEvent.Target : EntityId.None;
            ThreadId = worldEvent.ThreadId;
            At = worldEvent.Time;
            AgeInDays = ageInDays;
            Weight = Clamp01(worldEvent.Magnitude);
            Knower = knower;
            KnownVia = knownVia;
        }

        /// <summary>The place this is about.</summary>
        public EntityId SiteId { get; }

        /// <summary>The event in the ledger. The only source of what happened.</summary>
        public EntityId EventId { get; }

        public WorldEventType EventType { get; }

        public SiteHistoryRole Role { get; }

        /// <summary>
        /// What sort of material this left, as <see cref="CallbackHooks.KindsOf"/> already reads it.
        /// Empty for <see cref="SiteHistoryRole.Found"/> and <see cref="SiteHistoryRole.Cleared"/>,
        /// which are facts about the place's own standing rather than stories about somebody.
        /// </summary>
        public IReadOnlyList<CallbackKind> Kinds { get; }

        /// <summary>Whoever did it, as history recorded them.</summary>
        public EntityId Actor { get; }

        /// <summary>Whoever it was done to, or nobody.</summary>
        public EntityId Other { get; }

        /// <summary>The matter the verb that wrote it recorded, when it recorded one.</summary>
        public EntityId ThreadId { get; }

        public GameTime At { get; }

        /// <summary>Whole in-game days between the event and the moment this was derived at.</summary>
        public long AgeInDays { get; }

        /// <summary>How much of a thing it was, 0..1: the event's own recorded magnitude.</summary>
        public double Weight { get; }

        /// <summary>
        /// Whose reading of the place's history this is, or <see cref="EntityId.None"/> for the
        /// world's own - see <see cref="LocationHistory.Of"/>.
        /// </summary>
        public EntityId Knower { get; }

        /// <summary>
        /// How <see cref="Knower"/> comes to know this happened, and null when nobody is asking.
        /// It is <see cref="CallbackRoute"/> rather than a second enum for the reason
        /// <see cref="ProvenanceEntry.RecognizedVia"/> is: whether somebody knows what happened in
        /// a place is the same knowledge question BQ-081 already answers, and answering it twice is
        /// how the two answers start to disagree.
        /// </summary>
        public CallbackRoute? KnownVia { get; }

        public override string ToString() =>
            Role + " " + SiteId + " @" + EventId + (KnownVia.HasValue ? " (" + Knower + " " + KnownVia.Value + ")" : string.Empty);

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }

    /// <summary>
    /// What a place is known for: one kind of thing that kept happening there, or one that was bad
    /// enough on its own (PM §41).
    ///
    /// <b>Compressed history, not new history.</b> A legend holds the entries it compresses and
    /// nothing else - no summary sentence, no name, no invented incident. It is a grouping of the
    /// ledger, so it cannot outlive, contradict or drift from what it groups, and there is no
    /// legend store to migrate.
    ///
    /// <b>Its subject is a <see cref="CallbackKind"/>.</b> That vocabulary already says what sort
    /// of story an event leaves, and it already groups the distinctions a legend has to group -
    /// three different deaths at one mine are one thing a place is known for, not three. Minting a
    /// second taxonomy of legend motifs here would mean maintaining two answers to "what kind of
    /// thing was that".
    /// </summary>
    public sealed class SiteLegend
    {
        internal SiteLegend(EntityId siteId, CallbackKind subject, IReadOnlyList<SiteHistoryEntry> entries)
        {
            SiteId = siteId;
            Subject = subject;
            Entries = entries;

            GameTime first = entries[0].At;
            GameTime last = entries[0].At;
            long youngest = entries[0].AgeInDays;
            double heaviest = entries[0].Weight;
            for (int i = 1; i < entries.Count; i++)
            {
                SiteHistoryEntry entry = entries[i];
                if (entry.At < first)
                {
                    first = entry.At;
                }

                if (entry.At > last)
                {
                    last = entry.At;
                    youngest = entry.AgeInDays;
                }

                if (entry.Weight > heaviest)
                {
                    heaviest = entry.Weight;
                }
            }

            First = first;
            Last = last;
            AgeInDays = youngest;
            Salience = heaviest;
        }

        public EntityId SiteId { get; }

        /// <summary>What the place is known for.</summary>
        public CallbackKind Subject { get; }

        /// <summary>The history this compresses, oldest first. Never empty.</summary>
        public IReadOnlyList<SiteHistoryEntry> Entries { get; }

        public int Occurrences => Entries.Count;

        public GameTime First { get; }

        public GameTime Last { get; }

        /// <summary>Days since the most recent of the compressed events.</summary>
        public long AgeInDays { get; }

        /// <summary>The heaviest single entry, 0..1. Not a sum: a legend is not made bigger by
        /// arithmetic, and two figures a caller can weigh itself beat one nobody can explain.</summary>
        public double Salience { get; }

        /// <summary>Whether this is a legend because it kept happening, rather than because one of
        /// them was severe.</summary>
        public bool Repeated => Occurrences >= LocationHistory.MinimumOccurrences;

        public override string ToString() => Subject + " x" + Occurrences + " @" + SiteId;
    }

    /// <summary>
    /// What has happened in a place, and what the place is known for (BQ-086).
    ///
    /// Derived from the event ledger on demand, never stored - the reasoning of <c>D005</c>,
    /// <c>D039</c> and <c>D057</c>, applied to places instead of to people or things. A site
    /// already persists; its history does not need to, because the events do.
    ///
    /// <b>"Track only notable events" needs no notable flag.</b> Every event records a zone, so
    /// unlike an object's provenance a place's history cannot be defined by the field being
    /// populated at all - some rule has to say which of the things that happened here are the
    /// place's history rather than its traffic. The rule is that the event either names the place
    /// (somebody found it, somebody cleared it) or left material somebody could bring up
    /// afterwards, which is exactly <see cref="CallbackHooks.KindsOf"/>. So meeting somebody in a
    /// mine, talking there and the thread engine's own bookkeeping are not the mine's history,
    /// and no per-site budget, pruning pass or salience flag is needed to keep them out.
    ///
    /// <b>Knowing what happened somewhere is the same gate as being able to bring it up.</b>
    /// <see cref="KnownTo"/> filters on <see cref="CallbackHooks.TryRoute"/> rather than on a
    /// second rule of its own, so background simulation cannot hand somebody - or the player - a
    /// past they were never part of.
    ///
    /// <b>It stops before wording, maps and generation.</b> This is semantic input: what a place's
    /// history makes of its description, of whether it should be reused rather than generated
    /// (BQ-088), or of what is built into it (BQ-089 … BQ-092) is those steps' question. Nothing
    /// here writes a map, mutates a place or authors a sentence.
    /// </summary>
    public static class LocationHistory
    {
        /// <summary>Twice is a pattern. The design's own examples are two adventurers and three
        /// caravans, so the bar is the smallest number that can be called repetition at all.</summary>
        public const int MinimumOccurrences = 2;

        /// <summary>
        /// The weight one event has to carry to be a legend on its own: the top fifth of the
        /// ledger's 0..1 scale, above every event recorded at the default magnitude. It is what
        /// makes a massacre a legend where a single scuffle in the same room is only history.
        /// </summary>
        public const double HighSalience = 0.8;

        private static readonly SiteHistoryEntry[] NoEntries = new SiteHistoryEntry[0];
        private static readonly SiteLegend[] NoLegends = new SiteLegend[0];
        private static readonly CallbackKind[] NoKinds = new CallbackKind[0];

        /// <summary>
        /// Everything history recorded here that the place will be remembered by, oldest first,
        /// with nobody's knowledge consulted. The world's own answer: for what one person could
        /// tell you about the place, ask <see cref="KnownTo"/>.
        /// </summary>
        public static IReadOnlyList<SiteHistoryEntry> Of(NarrativeWorldState world, EntityId siteId, GameTime now)
        {
            return Derive(world, siteId, EntityId.None, now);
        }

        /// <summary>
        /// The part of a place's history this person has a route to, oldest first.
        ///
        /// Empty for somebody who was never near any of it, which is the whole of "a place's past
        /// is not common knowledge" - expressed as which entries exist rather than as a rule every
        /// consumer has to remember.
        /// </summary>
        public static IReadOnlyList<SiteHistoryEntry> KnownTo(
            NarrativeWorldState world,
            EntityId siteId,
            EntityId who,
            GameTime now)
        {
            return who.IsNone ? NoEntries : Derive(world, siteId, who, now);
        }

        /// <summary>
        /// What these entries make the place known for, most-told first.
        ///
        /// It takes entries rather than a world so that the same compression answers both
        /// questions: hand it <see cref="Of"/> and it says what the place is; hand it
        /// <see cref="KnownTo"/> and it says what that person could tell you the place is. A
        /// legend derived from what one settlement actually knows is what that settlement tells,
        /// and there is no second implementation to keep in step - the same seam
        /// <see cref="ItemProvenance.OpenMatters"/> uses.
        ///
        /// Ordered by how often, then by how heavy, then by subject, so nothing depends on ledger
        /// walk order and a reload offers the same legends in the same order.
        /// </summary>
        public static IReadOnlyList<SiteLegend> Legends(IReadOnlyList<SiteHistoryEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return NoLegends;
            }

            List<CallbackKind> subjects = new List<CallbackKind>();
            List<List<SiteHistoryEntry>> grouped = new List<List<SiteHistoryEntry>>();
            for (int i = 0; i < entries.Count; i++)
            {
                SiteHistoryEntry entry = entries[i];
                IReadOnlyList<CallbackKind> kinds = entry.Kinds;
                for (int k = 0; k < kinds.Count; k++)
                {
                    int at = subjects.IndexOf(kinds[k]);
                    if (at < 0)
                    {
                        subjects.Add(kinds[k]);
                        grouped.Add(new List<SiteHistoryEntry>());
                        at = subjects.Count - 1;
                    }

                    grouped[at].Add(entry);
                }
            }

            List<SiteLegend> legends = new List<SiteLegend>();
            for (int i = 0; i < subjects.Count; i++)
            {
                SiteLegend legend = new SiteLegend(grouped[i][0].SiteId, subjects[i], grouped[i].ToArray());
                if (legend.Repeated || legend.Salience >= HighSalience)
                {
                    legends.Add(legend);
                }
            }

            if (legends.Count == 0)
            {
                return NoLegends;
            }

            legends.Sort(Compare);
            return legends;
        }

        /// <summary>
        /// Which recorded act an event was as far as a place is concerned, and whether it is the
        /// place's history at all.
        ///
        /// Public because "why is that not in the mine's history" is a question the inspector has
        /// to be able to answer, and because the admission rule is the interesting half of the
        /// derivation.
        /// </summary>
        public static bool TryRoleOf(WorldEventType type, out SiteHistoryRole role)
        {
            switch (type)
            {
                case WorldEventType.SiteDiscovered:
                    role = SiteHistoryRole.Found;
                    return true;

                case WorldEventType.SiteCleared:
                    role = SiteHistoryRole.Cleared;
                    return true;

                default:
                    role = SiteHistoryRole.Incident;
                    return CallbackHooks.KindsOf(type).Count > 0;
            }
        }

        /// <summary>
        /// Whether this event belongs to this place's history.
        ///
        /// Two recorded ways, because the ledger writes the two differently. Most events say where
        /// they happened, and the zone a site's contents live under is
        /// <see cref="SiteGenesis.ZoneOf"/> - the same key every other read of a place is on. The
        /// two place-naming events instead carry the site as their target and record whatever zone
        /// surrounds it, so clearing a cache under a boathouse is the cache's history even though
        /// the zone on it is the town's. Nothing is matched on name, type or time: a coincidence is
        /// not a connection.
        /// </summary>
        public static bool Happened(WorldEvent worldEvent, NarrativeSite site)
        {
            if (worldEvent == null || site == null)
            {
                return false;
            }

            EntityId zone = SiteGenesis.ZoneOf(site);
            if (!zone.IsNone && worldEvent.Zone == zone)
            {
                return true;
            }

            SiteHistoryRole role;
            return TryRoleOf(worldEvent.Type, out role)
                   && role != SiteHistoryRole.Incident
                   && worldEvent.Target == site.Id;
        }

        private static IReadOnlyList<SiteHistoryEntry> Derive(
            NarrativeWorldState world,
            EntityId siteId,
            EntityId who,
            GameTime now)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            NarrativeSite site = siteId.IsNone ? null : world.Registry.GetSite(siteId);
            if (site == null)
            {
                return NoEntries;
            }

            List<SiteHistoryEntry> entries = new List<SiteHistoryEntry>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                SiteHistoryRole role;
                if (!TryRoleOf(worldEvent.Type, out role) || !Happened(worldEvent, site))
                {
                    continue;
                }

                CallbackRoute? via = null;
                if (!who.IsNone)
                {
                    CallbackRoute route;
                    if (!CallbackHooks.TryRoute(world, worldEvent, who, out route))
                    {
                        continue;
                    }

                    via = route;
                }

                entries.Add(new SiteHistoryEntry(
                    site.Id,
                    worldEvent,
                    role,
                    role == SiteHistoryRole.Incident ? CallbackHooks.KindsOf(worldEvent.Type) : NoKinds,
                    now.DaysSince(worldEvent.Time),
                    who,
                    via));
            }

            return entries.Count == 0 ? NoEntries : entries;
        }

        private static int Compare(SiteLegend a, SiteLegend b)
        {
            if (a.Occurrences != b.Occurrences)
            {
                return a.Occurrences > b.Occurrences ? -1 : 1;
            }

            if (a.Salience != b.Salience)
            {
                return a.Salience > b.Salience ? -1 : 1;
            }

            return a.Subject.CompareTo(b.Subject);
        }
    }
}
