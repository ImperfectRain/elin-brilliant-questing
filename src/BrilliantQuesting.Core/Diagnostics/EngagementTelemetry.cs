using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>How one generated matter stopped being something the player could still act on.</summary>
    public enum EngagementEnding
    {
        /// <summary>It has not stopped: latent, active, or dormant and able to wake again.</summary>
        None,

        /// <summary>Resolved, and the closing event names the player as who ended it.</summary>
        Player,

        /// <summary>Resolved, and the closing event names somebody else.</summary>
        Others,

        /// <summary>
        /// Resolved, and history does not say by whom - no closing event, or one carrying no actor.
        /// Distinct from <see cref="Others"/> on purpose: "somebody else dealt with it" is the one
        /// number the engagement test reads most, and an unattributed ending counted there would
        /// inflate it with endings nobody performed (`D017`).
        /// </summary>
        Unattributed,

        /// <summary>
        /// Inherited or quarantined: it stopped being playable without anybody resolving it. Not
        /// an ending somebody earned, and not an opportunity the player still has.
        /// </summary>
        Withdrawn
    }

    /// <summary>One generated matter, and how far it got towards the player.</summary>
    public sealed class EngagementEntry
    {
        public EngagementEntry(
            EntityId matterId,
            string archetypeId,
            ThreadState state,
            bool encountered,
            int playerActs,
            EngagementEnding ending,
            EntityId endedBy,
            string evidence)
        {
            MatterId = matterId;
            ArchetypeId = archetypeId ?? string.Empty;
            State = state;
            Encountered = encountered;
            PlayerActs = playerActs;
            Ending = ending;
            EndedBy = endedBy;
            Evidence = evidence ?? string.Empty;
        }

        public EntityId MatterId { get; }

        public string ArchetypeId { get; }

        /// <summary>The thread's own lifecycle state, read rather than restated.</summary>
        public ThreadState State { get; }

        /// <summary>
        /// Whether the player has a route to this matter at all: they hold one of its claims, or a
        /// consequence of it arrived somewhere they were.
        /// </summary>
        public bool Encountered { get; }

        /// <summary>
        /// How many acts in history the matter itself attributes to the player, through
        /// <see cref="NarrativeThread.IsNamedBy"/> - never because the player was standing nearby.
        /// </summary>
        public int PlayerActs { get; }

        public EngagementEnding Ending { get; }

        /// <summary>Whoever the closing event names, or none when nothing closed it.</summary>
        public EntityId EndedBy { get; }

        /// <summary>One line saying what decided the reading. Debug text, never player wording.</summary>
        public string Evidence { get; }

        public bool Engaged => PlayerActs > 0;

        /// <summary>Still something the player could act on, including a dormant matter.</summary>
        public bool Open => Ending == EngagementEnding.None;

        /// <summary>
        /// The player was shown it, never acted, and it is over. Deliberately not "surfaced minus
        /// engaged": a matter put in front of the player an hour ago that is still open is being
        /// declined, which is what `engagement §6` question 6 says the player is entitled to do,
        /// and counting it as ignored would report an opportunity lost while it is still standing.
        /// </summary>
        public bool Ignored => Encountered && PlayerActs == 0 && !Open;
    }

    /// <summary>The whole engagement lifecycle of one save, counted.</summary>
    public sealed class EngagementProfile
    {
        public EngagementProfile(IReadOnlyList<EngagementEntry> matters, bool playerKnown)
        {
            Matters = matters ?? new List<EngagementEntry>();
            PlayerKnown = playerKnown;
            for (int i = 0; i < Matters.Count; i++)
            {
                EngagementEntry entry = Matters[i];
                Generated++;
                if (entry.Encountered) Surfaced++;
                else Unseen++;
                if (entry.Engaged) Engaged++;
                if (entry.Ignored) Ignored++;
                if (entry.Encountered && !entry.Engaged && entry.Open) Awaiting++;
                if (entry.Open) Open++;
                switch (entry.Ending)
                {
                    case EngagementEnding.Player: ResolvedByPlayer++; break;
                    case EngagementEnding.Others: ResolvedByOthers++; break;
                    case EngagementEnding.Unattributed: ResolvedUnattributed++; break;
                    case EngagementEnding.Withdrawn: Withdrawn++; break;
                }
            }
        }

        public IReadOnlyList<EngagementEntry> Matters { get; }

        /// <summary>
        /// Whether the adapter could name a player at all. False makes every per-player count zero
        /// for want of a subject, which is not the same claim as "nothing reached them".
        /// </summary>
        public bool PlayerKnown { get; }

        /// <summary>Every matter the world has generated and still carries.</summary>
        public int Generated { get; private set; }

        /// <summary>Of those, the ones the player has any route to.</summary>
        public int Surfaced { get; private set; }

        /// <summary>Of those, the ones history attributes an act of the player's to.</summary>
        public int Engaged { get; private set; }

        /// <summary>Encountered, never acted on, and over.</summary>
        public int Ignored { get; private set; }

        /// <summary>Encountered, not acted on, still open. The player's standing choice.</summary>
        public int Awaiting { get; private set; }

        /// <summary>Generated and never within the player's reach.</summary>
        public int Unseen { get; private set; }

        public int ResolvedByPlayer { get; private set; }

        public int ResolvedByOthers { get; private set; }

        public int ResolvedUnattributed { get; private set; }

        public int Withdrawn { get; private set; }

        public int Open { get; private set; }
    }

    /// <summary>
    /// BQ-119: how much of what the world generated ever reached the player, and what became of it.
    ///
    /// `engagement §6` asks seven questions a build has to answer yes to, and says the first one -
    /// can a player who wants nothing but a better town find this useful - is the one that matters.
    /// None of them can be answered by opinion, and the shape of the answer is the same for all of
    /// them: of everything the simulation made, how much the player ever saw, how much they chose
    /// to touch, how much went by without them, and how much somebody else dealt with.
    ///
    /// **Derived, never stored** (`D022`). Every number here is read from state the save already
    /// carries - the threads, the ledger, and the player's own beliefs - so the profile survives a
    /// reload for the same reason history does and cannot drift from it. There is no counter to
    /// increment, nothing to migrate, and nothing to reset: a corrected event corrects the profile.
    /// The living-world plan is explicit that this must not become an authority the world reads
    /// back, and derivation is what makes that structurally true rather than a promise - there is
    /// no stored datum here for anything to consume.
    ///
    /// **Debug only, local, and never transmitted.** It reaches a session through the `why?`
    /// inspector, which is off unless a developer turns it on, and writes where the rest of that
    /// report writes. Nothing here opens a file, a socket or a player-facing surface, and because
    /// it deliberately counts matters the player has no route to, it is not one: `D008` is a rule
    /// about what may be shown to a player, and printing "three situations you never heard of" in
    /// a journal would break it. This is a number for whoever is tuning the director.
    ///
    /// **It reports the save, not a session.** Nothing records when a session began, and inventing
    /// a durable session boundary to make this read prettier would be exactly the new saved datum
    /// the plan says not to add. So the counts are cumulative over the save's whole history, which
    /// is also the honest answer to "has this player been ignoring everything".
    ///
    /// Every question it asks is answered by an existing authority rather than a rule of its own:
    /// which acts belong to a matter is `NarrativeThread.IsNamedBy`, the same reading the autonomy
    /// pass uses to decide whether the player has a matter in hand; whether the player has a route
    /// to a matter is their own belief in one of its claims, the encounter rule BQ-101 and BQ-102
    /// already read, plus the arrival surfaces the attention budget already spends attention on;
    /// who ended a matter is the actor on the `ThreadResolved` event, because `D022` makes the
    /// ending an event and the thread's fields a projection of it.
    /// </summary>
    public static class EngagementTelemetry
    {
        public static EngagementProfile Read(NarrativeWorldState world, IVanillaState vanilla)
        {
            List<EngagementEntry> matters = new List<EngagementEntry>();
            if (world == null)
            {
                return new EngagementProfile(matters, false);
            }

            EntityId player = vanilla == null ? EntityId.None : vanilla.PlayerId;
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;

            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread == null)
                {
                    continue;
                }

                string encounter = EncounterRoute(world, player, thread, events);
                int acts = PlayerActs(player, thread, events);
                WorldEvent closing = ClosingEvent(thread, events);
                EngagementEnding ending = EndingOf(thread, closing, player);

                // Only an ending history actually attributes to somebody carries an author. An
                // unattributed or withdrawn matter has none, and naming the last person who
                // happened to be on a closing event would invent one.
                EntityId endedBy = ending == EngagementEnding.Player || ending == EngagementEnding.Others
                    ? closing.Actor
                    : EntityId.None;

                matters.Add(new EngagementEntry(
                    thread.Id,
                    thread.ArchetypeId,
                    thread.State,
                    encounter != null,
                    acts,
                    ending,
                    endedBy,
                    Evidence(world, player, encounter, acts, ending, endedBy)));
            }

            return new EngagementProfile(matters, !player.IsNone);
        }

        public static string Describe(NarrativeWorldState world, IVanillaState vanilla)
        {
            EngagementProfile profile = Read(world, vanilla);
            StringBuilder sb = new StringBuilder();
            sb.Append("Brilliant Questing engagement profile\n");
            sb.Append("  debug only: derived from saved history, stored nowhere, transmitted nowhere.\n");
            sb.Append("  counts cover this save's whole history; nothing records a session boundary.\n");

            if (!profile.PlayerKnown)
            {
                sb.Append("  no player is bound, so nothing can be said to have reached one.\n");
            }

            if (profile.Generated == 0)
            {
                sb.Append("  no situations generated yet\n");
                return sb.ToString();
            }

            sb.Append("  generated ").Append(profile.Generated)
              .Append("; surfaced ").Append(profile.Surfaced)
              .Append("; engaged ").Append(profile.Engaged)
              .Append("; ignored ").Append(profile.Ignored)
              .Append("; awaiting the player ").Append(profile.Awaiting)
              .Append("; never surfaced ").Append(profile.Unseen).Append('\n');
            sb.Append("  endings: resolved by the player ").Append(profile.ResolvedByPlayer)
              .Append("; resolved by others ").Append(profile.ResolvedByOthers)
              .Append("; resolved, author unrecorded ").Append(profile.ResolvedUnattributed)
              .Append("; withdrawn without a resolution ").Append(profile.Withdrawn)
              .Append("; still open ").Append(profile.Open).Append('\n');

            sb.Append("  matters:\n");
            for (int i = 0; i < profile.Matters.Count; i++)
            {
                EngagementEntry entry = profile.Matters[i];
                sb.Append("    ").Append(entry.ArchetypeId)
                  .Append(' ').Append(entry.MatterId.Value)
                  .Append(" [").Append(entry.State).Append("] ")
                  .Append(Reach(entry)).Append('\n');
                sb.Append("      ").Append(entry.Evidence).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>The furthest this matter got towards the player, in words.</summary>
        private static string Reach(EngagementEntry entry)
        {
            if (entry.Engaged)
            {
                return "engaged, " + entry.PlayerActs + (entry.PlayerActs == 1 ? " act" : " acts");
            }

            if (entry.Ignored)
            {
                return "ignored";
            }

            return entry.Encountered ? "surfaced, awaiting the player" : "never surfaced";
        }

        /// <summary>
        /// How the matter came within the player's reach, or null if it never did.
        ///
        /// Two routes, in the order a reader cares about them. The first is the player's own
        /// belief in one of the matter's claims - the encounter rule the repetition and niche
        /// policies already read, which is what makes "surfaced" here mean the same thing it means
        /// to the director. The second is a consequence that came to them: an arrival reaches a
        /// player who learned nothing from it, and the attention budget already treats it as
        /// exposure, so a matter that walked into the room counts as having reached them.
        ///
        /// A consequence sent to an empty Home does not count, which is why the surface tags are
        /// read rather than the arrival alone: being visited while away is not being shown
        /// something.
        /// </summary>
        private static string EncounterRoute(
            NarrativeWorldState world,
            EntityId player,
            NarrativeThread thread,
            IReadOnlyList<WorldEvent> events)
        {
            if (player.IsNone)
            {
                return null;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (world.Knowledge.Knows(player, thread.FactIds[i]))
                {
                    return "encountered: the player believes " + thread.FactIds[i].Value;
                }
            }

            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent entry = events[i];
                if (entry.ThreadId != thread.Id
                    || !HasTag(entry, ConsequenceArrivals.ArrivalTag)
                    || (!HasTag(entry, ConsequenceArrivals.PlayerSurfaceTag)
                        && !HasTag(entry, ConsequenceArrivals.PlayerPresentTag)))
                {
                    continue;
                }

                return "encountered: a consequence arrived where the player was (" + entry.Id.Value + ")";
            }

            return null;
        }

        /// <summary>
        /// Acts the matter itself attributes to the player.
        ///
        /// The thread's own attribution rule, because an act counts when the verb that recorded it
        /// said which matter it belonged to. Proximity is not engagement: a player who happened to
        /// be in the room is not somebody who did something about it.
        /// </summary>
        private static int PlayerActs(EntityId player, NarrativeThread thread, IReadOnlyList<WorldEvent> events)
        {
            if (player.IsNone)
            {
                return 0;
            }

            int acts = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Actor == player && thread.IsNamedBy(events[i]))
                {
                    acts++;
                }
            }

            return acts;
        }

        /// <summary>
        /// The closing entry of the matter's current ending, or null if history carries none.
        ///
        /// The last one wins: a thread can be resolved, reopened and resolved again (BQ-052), and
        /// history is appended to rather than rewritten, so the latest closing event is the ending
        /// the matter stands on now.
        /// </summary>
        private static WorldEvent ClosingEvent(NarrativeThread thread, IReadOnlyList<WorldEvent> events)
        {
            WorldEvent closing = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == WorldEventType.ThreadResolved && events[i].ThreadId == thread.Id)
                {
                    closing = events[i];
                }
            }

            return closing;
        }

        private static EngagementEnding EndingOf(NarrativeThread thread, WorldEvent closing, EntityId player)
        {
            if (thread.State == ThreadState.Inherited || thread.State == ThreadState.Quarantined)
            {
                return EngagementEnding.Withdrawn;
            }

            if (thread.State != ThreadState.Resolved)
            {
                return EngagementEnding.None;
            }

            if (closing == null || closing.Actor.IsNone)
            {
                return EngagementEnding.Unattributed;
            }

            return closing.Actor == player && !player.IsNone
                ? EngagementEnding.Player
                : EngagementEnding.Others;
        }

        private static string Evidence(
            NarrativeWorldState world,
            EntityId player,
            string encounter,
            int acts,
            EngagementEnding ending,
            EntityId endedBy)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(encounter ?? (player.IsNone
                ? "not surfaced: no player to surface it to"
                : "not surfaced: the player holds none of its claims and nothing arrived where they were"));
            sb.Append("; acts attributed to the player: ").Append(acts);

            switch (ending)
            {
                case EngagementEnding.None:
                    sb.Append("; not ended");
                    break;
                case EngagementEnding.Player:
                    sb.Append("; ended by the player");
                    break;
                case EngagementEnding.Others:
                    sb.Append("; ended by ").Append(world.Registry.NameOf(endedBy));
                    break;
                case EngagementEnding.Unattributed:
                    sb.Append("; resolved with no author on the record");
                    break;
                case EngagementEnding.Withdrawn:
                    sb.Append("; withdrawn without a resolution");
                    break;
            }

            return sb.ToString();
        }

        private static bool HasTag(WorldEvent entry, string tag)
        {
            for (int i = 0; i < entry.Tags.Count; i++)
            {
                if (entry.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
