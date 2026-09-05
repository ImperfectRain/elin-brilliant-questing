using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>
    /// Somebody the player's own history keeps returning to.
    ///
    /// A person, never a role and never a verdict: the fields say how often the two of them have
    /// dealt with each other, what sort of material those dealings left, and what tie the world
    /// records - and the reader draws the conclusion. Nothing here decides that a run of
    /// <see cref="CallbackKind.Injury"/> beside an <see cref="RelationKind.Enemy"/> edge is a
    /// feud; that word is the reader's, and a field holding it would be this layer inventing a
    /// judgement the simulation never made.
    /// </summary>
    public sealed class ChronicleFigure
    {
        public ChronicleFigure(
            EntityId actor,
            string name,
            int dealings,
            IReadOnlyList<CallbackKind> kinds,
            double weight,
            RelationshipEdge tie,
            GameTime first,
            GameTime last)
        {
            Actor = actor;
            Name = name ?? string.Empty;
            Dealings = dealings;
            Kinds = kinds;
            Weight = weight;
            Tie = tie;
            First = first;
            Last = last;
        }

        public EntityId Actor { get; }

        public string Name { get; }

        /// <summary>How many recorded events between the two of them left something rememberable.</summary>
        public int Dealings { get; }

        /// <summary>
        /// What sort of material their shared history left, in <see cref="CallbackKind"/> order.
        /// Read from <see cref="CallbackHooks.KindsOf"/> and nothing else, so there is one table
        /// saying what an event type is worth remembering for rather than two.
        /// </summary>
        public IReadOnlyList<CallbackKind> Kinds { get; }

        /// <summary>The heaviest single event between them, 0..1, on the ledger's own scale.</summary>
        public double Weight { get; }

        /// <summary>
        /// How this person holds the player, as the relationship graph records it, or null where
        /// it records nothing. Their direction rather than the player's: a chronicle is a reading
        /// of who the player became to other people.
        /// </summary>
        public RelationshipEdge Tie { get; }

        public GameTime First { get; }

        public GameTime Last { get; }
    }

    /// <summary>
    /// A place that carries the player's name: somewhere their own acts are part of what the
    /// place is.
    /// </summary>
    public sealed class ChroniclePlace
    {
        public ChroniclePlace(
            EntityId siteId,
            string name,
            string siteType,
            IReadOnlyList<SiteHistoryEntry> marks,
            IReadOnlyList<SiteLegend> legends,
            bool found,
            bool cleared)
        {
            SiteId = siteId;
            Name = name ?? string.Empty;
            SiteType = siteType ?? string.Empty;
            Marks = marks;
            Legends = legends;
            Found = found;
            Cleared = cleared;
        }

        public EntityId SiteId { get; }

        public string Name { get; }

        public string SiteType { get; }

        /// <summary>The place's history that the player themselves made, oldest first.</summary>
        public IReadOnlyList<SiteHistoryEntry> Marks { get; }

        /// <summary>What those marks alone make the place known for, BQ-086's own compression.</summary>
        public IReadOnlyList<SiteLegend> Legends { get; }

        /// <summary>The player found the place.</summary>
        public bool Found { get; }

        /// <summary>The player got past whatever was holding the place shut.</summary>
        public bool Cleared { get; }

        public GameTime First => Marks.Count == 0 ? GameTime.Zero : Marks[0].At;

        public GameTime Last => Marks.Count == 0 ? GameTime.Zero : Marks[Marks.Count - 1].At;
    }

    /// <summary>
    /// A business whose standing the player changed - the shop they rescued, bought, or left
    /// under somebody's thumb.
    ///
    /// It reports the change the player made and what the business is today, and those are two
    /// different readings on purpose: a shop the player put back on its feet and which has since
    /// failed again is both of those things, and collapsing them into one would let the trophy
    /// case claim a rescue the world no longer holds.
    /// </summary>
    public sealed class ChronicleWork
    {
        public ChronicleWork(
            EntityId businessId,
            EntityId placeId,
            EntityId operatorId,
            BusinessContinuityState left,
            BusinessContinuityState? now,
            GameTime at)
        {
            BusinessId = businessId;
            PlaceId = placeId;
            OperatorId = operatorId;
            Left = left;
            Now = now;
            At = at;
        }

        public EntityId BusinessId { get; }

        /// <summary>Where it trades, or none where the ledger no longer holds a record.</summary>
        public EntityId PlaceId { get; }

        /// <summary>Whoever is behind the counter, or none.</summary>
        public EntityId OperatorId { get; }

        /// <summary>The state the player's own last change to it recorded.</summary>
        public BusinessContinuityState Left { get; }

        /// <summary>What the business ledger says today, or null where it holds no record.</summary>
        public BusinessContinuityState? Now { get; }

        public GameTime At { get; }

        /// <summary>Whether the change the player made is still the business's standing.</summary>
        public bool Holds => Now.HasValue && Now.Value == Left;
    }

    /// <summary>The whole trophy case: one life, read out of the ledger.</summary>
    public sealed class ChronicleLife
    {
        public ChronicleLife(
            EntityId player,
            string name,
            IReadOnlyList<ChronicleEntry> matters,
            IReadOnlyList<ChronicleFigure> figures,
            IReadOnlyList<ChroniclePlace> places,
            IReadOnlyList<ChronicleWork> works)
        {
            Player = player;
            Name = name ?? string.Empty;
            Matters = matters;
            Figures = figures;
            Places = places;
            Works = works;
        }

        public EntityId Player { get; }

        public string Name { get; }

        /// <summary>What is finished, exactly as BQ-034 already reads it. Not a second copy.</summary>
        public IReadOnlyList<ChronicleEntry> Matters { get; }

        public IReadOnlyList<ChronicleFigure> Figures { get; }

        public IReadOnlyList<ChroniclePlace> Places { get; }

        public IReadOnlyList<ChronicleWork> Works { get; }

        public bool IsEmpty =>
            Matters.Count == 0 && Figures.Count == 0 && Places.Count == 0 && Works.Count == 0;
    }

    /// <summary>
    /// BQ-117: the Chronicle as a trophy case rather than a memory aid.
    ///
    /// `engagement §3 Tier 3` argues that legible, retrievable history is itself a reward, and a
    /// shareable one - Dwarf Fortress players generate worlds purely to read the history back.
    /// BQ-034 already records what happened, matter by matter, in the order it ended. That is a
    /// log. What a player retells is not a log: it is the handful of people their character kept
    /// dealing with, the places that carry their name, and the businesses they changed the
    /// standing of. This reads those out of exactly the same history.
    ///
    /// <b>A fourth reading of the ledger, not a fourth record of it.</b> On the terms `D022`,
    /// `D039` and `D057` set, and beside <c>CallbackHooks</c>, <c>ItemProvenance</c> and
    /// <c>LocationHistory</c>: nothing here is stored, indexed or saved, so a life survives a
    /// reload because the events do, and a corrected event corrects every reading of it. There is
    /// no chronicle store to drift from the truth, and nothing to migrate.
    ///
    /// <b>Nothing here is a second taxonomy.</b> What sort of story an event leaves is
    /// <see cref="CallbackHooks.KindsOf"/>, unchanged. Whether the player may recall it at all is
    /// <see cref="CallbackHooks.TryRoute"/>, unchanged - which is how `D008` holds without a rule
    /// of this layer's own, and why something done to the player unnoticed is history the world
    /// has and their chronicle does not. What a place is known for is
    /// <see cref="LocationHistory.Legends"/>, unchanged. What a tie is called is
    /// <see cref="RelationKind"/>, unchanged. This step contributes one thing the others do not
    /// have: the question "which of it was <em>this person's</em> doing".
    ///
    /// <b>The bar for being worth retelling is BQ-086's bar.</b> A figure earns a place the same
    /// way a legend does - repeated (<see cref="LocationHistory.MinimumOccurrences"/>: twice is a
    /// pattern) or heavy enough on its own (<see cref="LocationHistory.HighSalience"/>). Reusing
    /// the constants rather than choosing new ones is deliberate: "what is worth remembering" is
    /// one question, and two answers to it would drift apart. A place is admitted on a different
    /// and simpler ground, because the ledger has two verbs whose subject is a place rather than a
    /// person: finding somewhere or getting past what held it shut is carrying your name into it
    /// however ordinary the roll was, so <see cref="SiteHistoryRole.Found"/> and
    /// <see cref="SiteHistoryRole.Cleared"/> admit on their own and everything else has to make a
    /// legend.
    ///
    /// <b>It characterises by reporting, never by judging.</b> `engagement §3` names feuds,
    /// rescues and saved shops, and no field here holds any of those words: a feud is a reader
    /// looking at three <see cref="CallbackKind.Injury"/> dealings beside an
    /// <see cref="RelationKind.Enemy"/> edge. Naming it in Core would be this layer deciding what
    /// a history meant, which is the one thing a record of what happened must not do.
    ///
    /// <b>What it deliberately does not hold.</b> Open standing - favours owed, doors, membership -
    /// is BQ-118's sheet and stays there; this is what is finished, and listing both in one place
    /// is how a trophy case turns back into a to-do list. Legends about people and groups as the
    /// world tells them (`PM §41`) are still absent for BQ-086's reason: this reads one player's
    /// own history, and a legend the town tells needs a teller the simulation does not have yet.
    /// </summary>
    public static class ChronicleNarrative
    {
        private static readonly ChronicleFigure[] NoFigures = new ChronicleFigure[0];
        private static readonly ChroniclePlace[] NoPlaces = new ChroniclePlace[0];
        private static readonly ChronicleWork[] NoWorks = new ChronicleWork[0];
        private static readonly ChronicleEntry[] NoMatters = new ChronicleEntry[0];

        public static ChronicleLife Read(NarrativeWorldState world, EntityId player, GameTime now)
        {
            if (world == null || player.IsNone)
            {
                return new ChronicleLife(player, string.Empty, NoMatters, NoFigures, NoPlaces, NoWorks);
            }

            return new ChronicleLife(
                player,
                TryName(world, player, out string name) ? name : string.Empty,
                Chronicle.Entries(world, player),
                Figures(world, player),
                Places(world, player, now),
                Works(world, player));
        }

        /// <summary>
        /// The whole life as one self-contained piece of text: BQ-117's "exportable as text".
        ///
        /// Self-contained is the requirement that shapes it. Everything a reader needs is on the
        /// page - people and places by the names the world gave them, days rather than raw
        /// timestamps, event and outcome names spelled out - so somebody with the file and no save
        /// can still read it. Ids appear only where the world never named the thing.
        ///
        /// Plain on purpose, in the same register as the journal and the standing sheet. Wording
        /// with variants and voice belongs to the content pipeline, and a phrasebook here would be
        /// a second narrator to keep in step with the first.
        /// </summary>
        public static string Export(NarrativeWorldState world, EntityId player, GameTime now)
        {
            ChronicleLife life = Read(world, player, now);
            StringBuilder sb = new StringBuilder();

            sb.Append("Brilliant Questing chronicle\n");
            if (life.Name.Length > 0)
            {
                sb.Append(life.Name).Append(", day ").Append(now.TotalDays).Append('\n');
            }

            if (life.IsEmpty)
            {
                sb.Append("\n  Nothing to tell yet.\n");
                return sb.ToString();
            }

            AppendFigures(sb, life);
            AppendPlaces(sb, life);
            AppendWorks(sb, world, life);
            AppendMatters(sb, world, life);
            return sb.ToString();
        }

        /// <summary>
        /// The people the player's own history keeps returning to.
        ///
        /// Only events the player is a party to, and only the ones a recall route reaches: their
        /// own acts come first-hand, and something done to them counts once it was noticed, which
        /// is <see cref="CallbackHooks.TryRoute"/>'s existing answer rather than a rule invented
        /// here. An event type that leaves no material - <c>Met</c>, <c>Conversed</c>, the thread
        /// engine's bookkeeping - contributes nothing, so ordinary traffic never becomes a figure
        /// for the same reason it never becomes a place's history.
        ///
        /// The other party has to be somebody the registry knows as an actor. A verb whose target
        /// is an item, a site or a business is still the player's history, and it belongs to the
        /// readings that are about those things; listing it here would hand the reader a place
        /// dressed as a person.
        /// </summary>
        private static IReadOnlyList<ChronicleFigure> Figures(NarrativeWorldState world, EntityId player)
        {
            Dictionary<EntityId, Tally> byActor = new Dictionary<EntityId, Tally>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                EntityId other = Counterpart(world, worldEvent, player);
                if (other.IsNone)
                {
                    continue;
                }

                IReadOnlyList<CallbackKind> kinds = CallbackHooks.KindsOf(worldEvent.Type);
                if (kinds.Count == 0)
                {
                    continue;
                }

                if (!CallbackHooks.TryRoute(world, worldEvent, player, out CallbackRoute _))
                {
                    continue;
                }

                if (!byActor.TryGetValue(other, out Tally tally))
                {
                    tally = new Tally(worldEvent.Time);
                    byActor[other] = tally;
                }

                tally.Add(worldEvent, kinds);
            }

            List<ChronicleFigure> figures = new List<ChronicleFigure>();
            foreach (KeyValuePair<EntityId, Tally> pair in byActor)
            {
                Tally tally = pair.Value;
                if (tally.Dealings < LocationHistory.MinimumOccurrences && tally.Weight < LocationHistory.HighSalience)
                {
                    continue;
                }

                figures.Add(new ChronicleFigure(
                    pair.Key,
                    world.Registry.NameOf(pair.Key),
                    tally.Dealings,
                    tally.Kinds(),
                    tally.Weight,
                    world.Relationships.Find(pair.Key, player),
                    tally.First,
                    tally.Last));
            }

            if (figures.Count == 0)
            {
                return NoFigures;
            }

            figures.Sort(CompareFigures);
            return figures;
        }

        /// <summary>
        /// Whoever was on the other side of this event from the player, or none.
        ///
        /// None when the player is not a party at all, when the event is between the player and
        /// themselves, and when the other side is not somebody. <see cref="EntityRegistry.Canonical"/>
        /// is applied so a person the intake superseded is one figure rather than two (`D046`).
        /// </summary>
        private static EntityId Counterpart(NarrativeWorldState world, WorldEvent worldEvent, EntityId player)
        {
            EntityId other;
            if (worldEvent.Actor == player)
            {
                other = worldEvent.Target;
            }
            else if (worldEvent.Target == player)
            {
                other = worldEvent.Actor;
            }
            else
            {
                return EntityId.None;
            }

            other = world.Registry.Canonical(other);
            return other == player || !world.Registry.IsActor(other) ? EntityId.None : other;
        }

        /// <summary>
        /// Places the player left a mark on, oldest mark first inside each.
        ///
        /// Read through <see cref="LocationHistory.KnownTo"/> rather than
        /// <see cref="LocationHistory.Of"/> so the knowledge gate is the one BQ-086 already
        /// applies, then narrowed to the entries the player is the actor of: a place is in their
        /// chronicle because they made that history, not because they were standing there when
        /// somebody else did. Widening it to everything they merely witnessed would turn the
        /// trophy case back into the ledger it is a reading of.
        /// </summary>
        private static IReadOnlyList<ChroniclePlace> Places(NarrativeWorldState world, EntityId player, GameTime now)
        {
            List<ChroniclePlace> places = new List<ChroniclePlace>();
            foreach (NarrativeSite site in world.Registry.Sites.Values)
            {
                if (site == null)
                {
                    continue;
                }

                IReadOnlyList<SiteHistoryEntry> known = LocationHistory.KnownTo(world, site.Id, player, now);
                List<SiteHistoryEntry> mine = new List<SiteHistoryEntry>();
                bool found = false;
                bool cleared = false;
                for (int i = 0; i < known.Count; i++)
                {
                    SiteHistoryEntry entry = known[i];
                    if (entry.Actor != player)
                    {
                        continue;
                    }

                    mine.Add(entry);
                    found |= entry.Role == SiteHistoryRole.Found;
                    cleared |= entry.Role == SiteHistoryRole.Cleared;
                }

                if (mine.Count == 0)
                {
                    continue;
                }

                IReadOnlyList<SiteLegend> legends = LocationHistory.Legends(mine);
                if (!found && !cleared && legends.Count == 0)
                {
                    continue;
                }

                places.Add(new ChroniclePlace(site.Id, site.Name, site.SiteType, mine, legends, found, cleared));
            }

            if (places.Count == 0)
            {
                return NoPlaces;
            }

            places.Sort(ComparePlaces);
            return places;
        }

        /// <summary>
        /// Businesses whose standing the player themselves changed.
        ///
        /// <c>BusinessStateChanged</c> records an actor and tags the state it moved to, so who
        /// rescued a shop is history rather than an inference from who happens to be standing in
        /// it. The business ledger is asked separately for what the shop is today. The player's
        /// own last change to a business is the one reported: earlier ones are steps on the way
        /// and are already in the matter that contains them.
        /// </summary>
        private static IReadOnlyList<ChronicleWork> Works(NarrativeWorldState world, EntityId player)
        {
            Dictionary<EntityId, WorldEvent> latest = new Dictionary<EntityId, WorldEvent>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.Type != WorldEventType.BusinessStateChanged
                    || worldEvent.Actor != player
                    || worldEvent.Target.IsNone
                    || !TryState(worldEvent, out BusinessContinuityState _))
                {
                    continue;
                }

                if (!latest.TryGetValue(worldEvent.Target, out WorldEvent held) || worldEvent.Time >= held.Time)
                {
                    latest[worldEvent.Target] = worldEvent;
                }
            }

            List<ChronicleWork> works = new List<ChronicleWork>();
            foreach (KeyValuePair<EntityId, WorldEvent> pair in latest)
            {
                TryState(pair.Value, out BusinessContinuityState left);
                BusinessRecord record = world.Businesses.Of(pair.Key);
                works.Add(new ChronicleWork(
                    pair.Key,
                    record == null ? EntityId.None : record.PlaceId,
                    record == null ? EntityId.None : record.OperatorId,
                    left,
                    record == null ? (BusinessContinuityState?)null : record.State,
                    pair.Value.Time));
            }

            if (works.Count == 0)
            {
                return NoWorks;
            }

            works.Sort(CompareWorks);
            return works;
        }

        /// <summary>
        /// The state a business-state event moved the shop to, read off the tag the recorder
        /// wrote. An event whose tag no build understands yields nothing rather than a guessed
        /// state (`D017`): an unread tag is not <c>Normal</c>.
        /// </summary>
        private static bool TryState(WorldEvent worldEvent, out BusinessContinuityState state)
        {
            IReadOnlyList<string> tags = worldEvent.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (Enum.TryParse(tags[i], false, out state))
                {
                    return true;
                }
            }

            state = default;
            return false;
        }

        private static void AppendFigures(StringBuilder sb, ChronicleLife life)
        {
            if (life.Figures.Count == 0)
            {
                return;
            }

            sb.Append("\nWho you dealt with\n");
            for (int i = 0; i < life.Figures.Count; i++)
            {
                ChronicleFigure figure = life.Figures[i];
                sb.Append("  ").Append(figure.Name).Append(" - ").Append(figure.Dealings)
                  .Append(figure.Dealings == 1 ? " dealing, " : " dealings, ")
                  .Append(Kinds(figure.Kinds))
                  .Append(", ").Append(Span(figure.First, figure.Last));

                if (figure.Tie != null)
                {
                    sb.Append("; they hold you ").Append(Chronicle.Words(figure.Tie.Kind.ToString()))
                      .Append(" (").Append(figure.Tie.Sentiment).Append(')');
                }

                sb.Append('\n');
            }
        }

        private static void AppendPlaces(StringBuilder sb, ChronicleLife life)
        {
            if (life.Places.Count == 0)
            {
                return;
            }

            sb.Append("\nPlaces that carry your name\n");
            for (int i = 0; i < life.Places.Count; i++)
            {
                ChroniclePlace place = life.Places[i];
                sb.Append("  ").Append(place.Name);
                if (place.SiteType.Length > 0)
                {
                    sb.Append(" (").Append(Chronicle.Words(place.SiteType)).Append(')');
                }

                sb.Append('\n');
                for (int m = 0; m < place.Marks.Count; m++)
                {
                    SiteHistoryEntry mark = place.Marks[m];
                    sb.Append("    day ").Append(mark.At.TotalDays).Append(", ")
                      .Append(Chronicle.Words(mark.EventType.ToString())).Append('\n');
                }

                for (int l = 0; l < place.Legends.Count; l++)
                {
                    SiteLegend legend = place.Legends[l];
                    sb.Append("    known for ").Append(Chronicle.Words(legend.Subject.ToString()))
                      .Append(legend.Occurrences == 1 ? "\n" : ", " + legend.Occurrences + " times\n");
                }
            }
        }

        private static void AppendWorks(StringBuilder sb, NarrativeWorldState world, ChronicleLife life)
        {
            if (life.Works.Count == 0)
            {
                return;
            }

            sb.Append("\nBusinesses you changed\n");
            for (int i = 0; i < life.Works.Count; i++)
            {
                ChronicleWork work = life.Works[i];
                sb.Append("  ").Append(Business(world, work)).Append(" - you left it ")
                  .Append(Chronicle.Words(work.Left.ToString()))
                  .Append(" on day ").Append(work.At.TotalDays);

                if (work.Now.HasValue && !work.Holds)
                {
                    sb.Append("; today it is ").Append(Chronicle.Words(work.Now.Value.ToString()));
                }

                sb.Append('\n');
            }
        }

        private static void AppendMatters(StringBuilder sb, NarrativeWorldState world, ChronicleLife life)
        {
            if (life.Matters.Count == 0)
            {
                return;
            }

            sb.Append("\nWhat you finished\n");
            for (int i = 0; i < life.Matters.Count; i++)
            {
                ChronicleEntry entry = life.Matters[i];
                sb.Append("  ").Append(Chronicle.Words(entry.ArchetypeId)).Append(" - ")
                  .Append(Chronicle.Words(entry.Outcome))
                  .Append(" (day ").Append(entry.ResolvedAt.TotalDays).Append(")\n");

                for (int k = 0; k < entry.WhatWasKnown.Count; k++)
                {
                    JournalEntry known = entry.WhatWasKnown[k];
                    sb.Append("    what you knew: [").Append(known.Tag).Append("] ").Append(known.Text).Append('\n');
                }

                for (int a = 0; a < entry.WhatThePlayerDid.Count; a++)
                {
                    ChronicleAct act = entry.WhatThePlayerDid[a];
                    sb.Append("    what you did: day ").Append(act.At.TotalDays).Append(", ")
                      .Append(Chronicle.Words(act.Type.ToString()));
                    if (TryName(world, act.Towards, out string towards))
                    {
                        sb.Append(" - ").Append(towards);
                    }

                    sb.Append('\n');
                }
            }
        }

        /// <summary>
        /// A business by the two things the world actually named: whoever is behind the counter
        /// and where it trades. The business id itself is minted rather than registered, so it is
        /// a handle, never a name, and it appears only when there is nothing else to say.
        /// </summary>
        private static string Business(NarrativeWorldState world, ChronicleWork work)
        {
            bool named = TryName(world, work.OperatorId, out string keeper);
            bool placed = TryName(world, work.PlaceId, out string where);
            if (named && placed)
            {
                return keeper + "'s business in " + where;
            }

            if (named)
            {
                return keeper + "'s business";
            }

            return placed ? "the business in " + where : "a business";
        }

        /// <summary>
        /// The name the world gave something, or nothing at all.
        ///
        /// <see cref="EntityRegistry.NameOf"/> falls back to the id, which is the right answer for
        /// a log and the wrong one for an export: a minted handle is unreadable to somebody
        /// holding the text and not the save, and printing it claims a name the world never gave.
        /// So an unnamed thing is described by what is known about it or left out - `D017`'s rule
        /// about an unanswered datum, applied to wording.
        /// </summary>
        private static bool TryName(NarrativeWorldState world, EntityId id, out string name)
        {
            name = string.Empty;
            if (id.IsNone)
            {
                return false;
            }

            EntityRegistry registry = world.Registry;
            if (registry.GetNpc(id) == null && registry.GetOrganization(id) == null && registry.GetSite(id) == null)
            {
                return false;
            }

            name = registry.NameOf(id);
            return name.Length > 0;
        }

        /// <summary>A run of days, or the one day it all happened on.</summary>
        private static string Span(GameTime first, GameTime last)
        {
            return first.TotalDays == last.TotalDays
                ? "day " + first.TotalDays
                : "day " + first.TotalDays + " to day " + last.TotalDays;
        }

        private static string Kinds(IReadOnlyList<CallbackKind> kinds)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < kinds.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(i == kinds.Count - 1 ? " and " : ", ");
                }

                sb.Append(Chronicle.Words(kinds[i].ToString()));
            }

            return sb.ToString();
        }

        /// <summary>Most dealt with, then heaviest, then most recent, then by id so a reload orders alike.</summary>
        private static int CompareFigures(ChronicleFigure a, ChronicleFigure b)
        {
            int byDealings = b.Dealings.CompareTo(a.Dealings);
            if (byDealings != 0)
            {
                return byDealings;
            }

            int byWeight = b.Weight.CompareTo(a.Weight);
            if (byWeight != 0)
            {
                return byWeight;
            }

            int byLast = b.Last.CompareTo(a.Last);
            return byLast != 0 ? byLast : a.Actor.CompareTo(b.Actor);
        }

        private static int ComparePlaces(ChroniclePlace a, ChroniclePlace b)
        {
            int byLast = b.Last.CompareTo(a.Last);
            return byLast != 0 ? byLast : a.SiteId.CompareTo(b.SiteId);
        }

        private static int CompareWorks(ChronicleWork a, ChronicleWork b)
        {
            int byAt = b.At.CompareTo(a.At);
            return byAt != 0 ? byAt : a.BusinessId.CompareTo(b.BusinessId);
        }

        /// <summary>One person's running total, kept only while the reading is being built.</summary>
        private sealed class Tally
        {
            private readonly List<CallbackKind> _kinds = new List<CallbackKind>();

            internal Tally(GameTime first)
            {
                First = first;
                Last = first;
            }

            internal int Dealings { get; private set; }

            internal double Weight { get; private set; }

            internal GameTime First { get; private set; }

            internal GameTime Last { get; private set; }

            internal void Add(WorldEvent worldEvent, IReadOnlyList<CallbackKind> kinds)
            {
                Dealings++;
                if (worldEvent.Magnitude > Weight)
                {
                    Weight = worldEvent.Magnitude > 1.0 ? 1.0 : worldEvent.Magnitude;
                }

                if (worldEvent.Time < First)
                {
                    First = worldEvent.Time;
                }

                if (worldEvent.Time > Last)
                {
                    Last = worldEvent.Time;
                }

                for (int i = 0; i < kinds.Count; i++)
                {
                    if (!_kinds.Contains(kinds[i]))
                    {
                        _kinds.Add(kinds[i]);
                    }
                }
            }

            /// <summary>Distinct, in <see cref="CallbackKind"/> order rather than ledger order.</summary>
            internal IReadOnlyList<CallbackKind> Kinds()
            {
                _kinds.Sort();
                return _kinds;
            }
        }
    }
}
