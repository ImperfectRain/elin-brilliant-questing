using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>What kind of standing one line of the sheet reports.</summary>
    public enum StandingKind
    {
        /// <summary>Somebody is in the player's debt and it has not been settled.</summary>
        OwedToYou,

        /// <summary>The same records read from the other side: the player is in somebody's.</summary>
        YouOwe,

        /// <summary>A door that is open to the player because they earned it.</summary>
        Access,

        /// <summary>A generated organization that counts the player a member.</summary>
        Membership,

        /// <summary>
        /// A standing number the game itself keeps. Read live, never copied: vanilla owns the
        /// value and this only says it out loud next to the rest of what was earned.
        /// </summary>
        VanillaStanding
    }

    /// <summary>One thing the player holds that is neither money nor an item.</summary>
    public sealed class StandingEntry
    {
        public StandingEntry(
            StandingKind kind,
            EntityId subject,
            EntityId recordId,
            string title,
            string detail,
            bool callable,
            GameTime since)
        {
            Kind = kind;
            Subject = subject;
            RecordId = recordId;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Callable = callable;
            Since = since;
        }

        public StandingKind Kind { get; }

        /// <summary>The person, site or organization this is standing with, or none.</summary>
        public EntityId Subject { get; }

        /// <summary>The obligation this line reads, where there is one. None otherwise.</summary>
        public EntityId RecordId { get; }

        public string Title { get; }

        public string Detail { get; }

        /// <summary>
        /// True only for something the player can still spend. An obligation owed to them that
        /// is open and whose debtor the game has not reported dead; false for everything else,
        /// including what they owe. This is deliberately not the same as being listed: a favour
        /// from somebody who died is still part of the history and is still shown, but saying it
        /// is callable would be a promise the world cannot keep.
        /// </summary>
        public bool Callable { get; }

        public GameTime Since { get; }
    }

    /// <summary>
    /// BQ-118: everything the player has earned that is not money or an item, in one place.
    ///
    /// The engagement track's argument is that access-as-reward only motivates while the player
    /// can watch it accumulate. BQ-112 established the vocabulary - access, a relationship,
    /// standing, information, property, a favour owed - and BQ-113 made the strongest of those
    /// spendable, but a favour is only a stored option if the player knows they are holding one.
    /// Before this, the single place an open favour was visible was the dialogue node of the
    /// person who owed it, which means the player had to already be standing in front of them,
    /// already suspecting, to find out.
    ///
    /// Derived, not stored, for the reason D022 gives: every line here is read from state that is
    /// already in the save - the obligation ledger, a site's admitted list, an organization's
    /// membership, and the game's own standing numbers - so it survives a reload for the same
    /// reason they do and cannot drift from what is true. Nothing is written back.
    ///
    /// **It reports what is held, never a replay of what happened.** Finished business belongs to
    /// the Chronicle. A fulfilled favour, a promise that was kept, an ask that was granted and is
    /// long over: those are history, and listing them here would turn a sheet the player checks
    /// for what they can spend into a log they have to read past. So the obligation lines are
    /// open records only, and there are no event lines at all.
    ///
    /// **It obeys D008 like every other player surface.** A record is shown only where the player
    /// was a party to the event that created it. Today that gate changes nothing, because the one
    /// thing minting obligations is BQ-113's accrual off the player's own `Helped` event - but the
    /// ledger's model already carries grudges and sponsorships, and once background simulation
    /// writes those, a sheet that listed every record naming the player would quietly hand them
    /// somebody else's private reckoning. A record whose source the ledger cannot vouch for is
    /// withheld rather than shown, which is D017's rule about an unanswered datum applied to a
    /// surface: absent, not assumed.
    ///
    /// Two things the engagement material names are not here, because nothing in the game or the
    /// simulation answers them yet and inventing an answer would be worse than an honest gap: a
    /// **discount** is not a thing any landed system records, and **Influence** is per-town and
    /// Core has no way to enumerate towns. Both belong to whatever step builds them.
    /// </summary>
    public static class StandingSheet
    {
        public static IReadOnlyList<StandingEntry> Entries(NarrativeWorldState world, IVanillaState vanilla)
        {
            List<StandingEntry> entries = new List<StandingEntry>();
            if (world == null || vanilla == null)
            {
                return entries;
            }

            EntityId player = vanilla.PlayerId;
            if (player.IsNone)
            {
                return entries;
            }

            AddObligations(world, vanilla, player, entries);
            AddAccess(world, player, entries);
            AddMemberships(world, player, entries);
            AddVanillaStanding(vanilla, entries);
            return entries;
        }

        public static string Describe(NarrativeWorldState world, IVanillaState vanilla)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Brilliant Questing standing\n");

            IReadOnlyList<StandingEntry> entries = Entries(world, vanilla);
            if (entries.Count == 0)
            {
                sb.Append("  nothing earned yet\n");
                return sb.ToString();
            }

            AppendGroup(sb, entries, StandingKind.OwedToYou, "owed to you");
            AppendGroup(sb, entries, StandingKind.YouOwe, "you owe");
            AppendGroup(sb, entries, StandingKind.Access, "doors open to you");
            AppendGroup(sb, entries, StandingKind.Membership, "you belong to");
            AppendGroup(sb, entries, StandingKind.VanillaStanding, "standing");
            return sb.ToString();
        }

        private static void AppendGroup(
            StringBuilder sb,
            IReadOnlyList<StandingEntry> entries,
            StandingKind kind,
            string heading)
        {
            bool any = false;
            for (int i = 0; i < entries.Count; i++)
            {
                StandingEntry entry = entries[i];
                if (entry.Kind != kind)
                {
                    continue;
                }

                if (!any)
                {
                    sb.Append("  ").Append(heading).Append(":\n");
                    any = true;
                }

                sb.Append("    ").Append(entry.Title);
                if (entry.Detail.Length > 0)
                {
                    sb.Append(" - ").Append(entry.Detail);
                }

                sb.Append('\n');
            }
        }

        /// <summary>
        /// The obligation ledger, both directions, open records only.
        ///
        /// The player is told who owes them and what they owe, and for the first of those whether
        /// it can still be called on. A debtor the game reports dead is the one case where the
        /// record outlives the option: it stays listed, because it is part of what the player
        /// did, and it is marked so the sheet is never read as an offer. `Unknown` is not dead -
        /// an actor the adapter cannot resolve right now is off-screen, not gone, and treating
        /// the two the same would blank the sheet every time the player left town.
        /// </summary>
        private static void AddObligations(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId player,
            List<StandingEntry> entries)
        {
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                if (!obligation.IsOpen)
                {
                    continue;
                }

                bool owedToPlayer = obligation.Creditor == player;
                bool playerOwes = obligation.Debtor == player;
                if (owedToPlayer == playerOwes)
                {
                    // Not the player's, or a record they hold against themselves.
                    continue;
                }

                if (!PlayerWasPartyTo(world, player, obligation.SourceEventId))
                {
                    continue;
                }

                EntityId other = owedToPlayer ? obligation.Debtor : obligation.Creditor;
                string who = world.Registry.NameOf(other);
                string what = Words(obligation.Kind.ToString());
                bool lost = owedToPlayer && vanilla.GetLifeState(other) == VanillaLifeState.Dead;

                entries.Add(new StandingEntry(
                    owedToPlayer ? StandingKind.OwedToYou : StandingKind.YouOwe,
                    other,
                    obligation.Id,
                    owedToPlayer ? who + " owes you a " + what : "you owe " + who + " a " + what,
                    lost
                        ? "since day " + obligation.CreatedAt.TotalDays + ", and " + who + " is dead"
                        : "since day " + obligation.CreatedAt.TotalDays,
                    owedToPlayer && !lost,
                    obligation.CreatedAt));
            }
        }

        /// <summary>
        /// Places that will let the player in and would not let in a stranger.
        ///
        /// Read off the site's own admitted list rather than off the event that opened it, because
        /// the list is what the world actually consults - and because the verb that talks a player
        /// past a door records the admission as an outcome note and nothing else, so there is no
        /// event to read. An unrestricted site is not an achievement and is not listed.
        /// </summary>
        private static void AddAccess(NarrativeWorldState world, EntityId player, List<StandingEntry> entries)
        {
            foreach (NarrativeSite site in world.Registry.Sites.Values)
            {
                if (site == null || !site.Restricted || !site.AdmittedIds.Contains(player))
                {
                    continue;
                }

                entries.Add(new StandingEntry(
                    StandingKind.Access,
                    site.Id,
                    EntityId.None,
                    site.Name + " admits you",
                    Words(site.SiteType),
                    false,
                    GameTime.Zero));
            }
        }

        private static void AddMemberships(NarrativeWorldState world, EntityId player, List<StandingEntry> entries)
        {
            foreach (Organization organization in world.Registry.Organizations.Values)
            {
                if (organization == null || !organization.MemberIds.Contains(player))
                {
                    continue;
                }

                entries.Add(new StandingEntry(
                    StandingKind.Membership,
                    organization.Id,
                    EntityId.None,
                    organization.Name + " counts you a member",
                    Words(organization.Type),
                    false,
                    GameTime.Zero));
            }
        }

        /// <summary>
        /// The standing the game keeps, said out loud beside the standing the mod keeps.
        ///
        /// `engagement §3` counts Karma, fame and guild contribution as rewards in the same
        /// vocabulary as a favour, and a sheet that answered "everything you have earned" while
        /// silently omitting them would be answering a narrower question than the player asked.
        /// It is a live read of vanilla's own number on every call, never a stored copy, so there
        /// is nothing here that can fall out of step with the game.
        ///
        /// Capability-gated per D017: a build that cannot report Karma gets no Karma line rather
        /// than a zero, because a zero is a claim and an unread number is not one.
        ///
        /// A number that is genuinely zero is left out too, for a different reason: this sheet
        /// answers what the player has earned, and nothing earned is not an entry. Karma is the
        /// case that makes the rule read oddly and is still right - notoriety is earned, so a
        /// negative Karma is listed, and only untouched neutrality is silent.
        /// </summary>
        private static void AddVanillaStanding(IVanillaState vanilla, List<StandingEntry> entries)
        {
            if (vanilla.Supports(VanillaCapability.ReadWriteKarma) && vanilla.Karma != 0)
            {
                entries.Add(new StandingEntry(
                    StandingKind.VanillaStanding,
                    EntityId.None,
                    EntityId.None,
                    "karma " + vanilla.Karma,
                    string.Empty,
                    false,
                    GameTime.Zero));
            }

            if (vanilla.Supports(VanillaCapability.ReadWriteFame) && vanilla.Fame != 0)
            {
                entries.Add(new StandingEntry(
                    StandingKind.VanillaStanding,
                    EntityId.None,
                    EntityId.None,
                    "fame " + vanilla.Fame,
                    string.Empty,
                    false,
                    GameTime.Zero));
            }

            if (!vanilla.Supports(VanillaCapability.ReadGuildRank))
            {
                return;
            }

            IReadOnlyList<GuildId> guilds = GuildNetworks.All;
            for (int i = 0; i < guilds.Count; i++)
            {
                GuildId guild = guilds[i];
                if (!vanilla.IsGuildMember(guild))
                {
                    continue;
                }

                entries.Add(new StandingEntry(
                    StandingKind.VanillaStanding,
                    EntityId.None,
                    EntityId.None,
                    guild + " guild, rank " + vanilla.GetGuildRank(guild),
                    "contribution " + vanilla.GetGuildContribution(guild),
                    false,
                    GameTime.Zero));
            }
        }

        /// <summary>
        /// Whether history says the player lived through the moment this record was created.
        ///
        /// The ledger is append-oriented (D005) and nothing prunes it, so an event that is not
        /// found is one that was never written rather than one that expired - which is why a
        /// missing source withholds the record instead of waving it through.
        /// </summary>
        private static bool PlayerWasPartyTo(NarrativeWorldState world, EntityId player, EntityId sourceEventId)
        {
            if (sourceEventId.IsNone)
            {
                return false;
            }

            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.Id != sourceEventId)
                {
                    continue;
                }

                return worldEvent.Actor == player || worldEvent.Target == player;
            }

            return false;
        }

        /// <summary>"Favor" and "criminal_crew" both become readable without a phrasebook.</summary>
        private static string Words(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }

                if (char.IsUpper(c) && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }
}
