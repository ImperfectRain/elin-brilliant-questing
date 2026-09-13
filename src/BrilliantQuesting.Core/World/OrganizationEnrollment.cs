using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>What one enrollment pass raised, admitted and refused. Derived, transient.</summary>
    public sealed class OrganizationEnrollmentPass
    {
        private static readonly IReadOnlyList<EntityId> Nobody = new EntityId[0];

        internal OrganizationEnrollmentPass()
        {
            Roster = Nobody;
        }

        /// <summary>The bodies production may give a turn, in the order it should take them.</summary>
        public IReadOnlyList<EntityId> Roster { get; internal set; }

        /// <summary>Bodies this pass raised from observation for the first time.</summary>
        public List<EntityId> Raised { get; } = new List<EntityId>();

        /// <summary>Bodies left out of the roster, and why. The visible half of "not an institution".</summary>
        public List<string> Refusals { get; } = new List<string>();

        public override string ToString()
        {
            return Roster.Count + " enrolled, " + Raised.Count + " raised, " + Refusals.Count + " refused";
        }
    }

    /// <summary>
    /// How production comes by organizations at all, and which of them it may act on (BQa-020).
    ///
    /// Before this there was no answer. Organizations reached the simulation through a save, a
    /// fixture or a Lab scenario, which is why organization activity was only ever exercised where
    /// somebody had authored a crew first - and why the Plugin, which authors none, ran no
    /// institutional pass at all. This owner is the seam that was missing, and it does exactly two
    /// things.
    ///
    /// <b>It raises a body from offices the game says people hold.</b> The only route into
    /// <see cref="NarrativeNpc.Roles"/> for a live character is
    /// <see cref="AuthorityPolicy.Reconcile"/>, which takes an observed institutional facet and
    /// nothing else - not a trade, not a job token, not a name. So people holding a watch office in
    /// one settlement are the watch of that settlement, and people holding guild standing there are
    /// its guild. That is a grouping of observations, not an inference about the game's own
    /// factions: the body is keyed on <see cref="Organization.ExternalRef"/> and raised once, so a
    /// pass every morning refreshes one roll rather than founding a new watch every day.
    ///
    /// <b>It says which bodies are institutions at all.</b> A body with people, or ground it keeps,
    /// is one. A registry row with neither is a name, and is refused by name rather than quietly
    /// skipped - which is what stops production depending on whatever a harness happened to add.
    ///
    /// Two things it deliberately does not do. A raised body gets <b>no holding</b>: the settlement
    /// its people were observed in is not ground the watch keeps, and recording it as one would let
    /// BQa-019 collect reserves from a town nobody owns. And it gets <b>no wealth</b>, because a
    /// vanilla guild is not a BQ treasury. What it has is a roll and an empty receipt ledger, and
    /// until somebody files something through BQa-018's channels it notices nothing - which is the
    /// correct answer rather than a gap in this owner.
    ///
    /// Headless. <see cref="IVanillaState"/> is asked only who the player is.
    ///
    /// It reads the population once per pass, which is the cost profile the organization owners
    /// already have - BQa-019 walks it to find a reachable recruit, and rumour circulation walks it
    /// daily. Sampling it instead would be worse than the saving: a body observed only on the passes
    /// its people happened to be in the sample would be raised and emptied at random.
    /// </summary>
    public static class OrganizationEnrollment
    {
        /// <summary>An observed watch office makes a body of BQa-018's `watch` charter.</summary>
        public const string WatchType = "watch";

        /// <summary>Observed guild standing makes a body of BQa-018's `guild` charter.</summary>
        public const string GuildType = "guild";

        /// <summary>Nobody holds it any more. Used to empty a raised body's roll.</summary>
        private static readonly List<EntityId> NoHolders = new List<EntityId>();

        /// <summary>
        /// The offices this owner recognises, and what kind of body each makes.
        ///
        /// Deliberately the role vocabulary <see cref="AuthorityPolicy"/> already owns rather than a
        /// second one: which observed offices amount to authority and which to guild standing is
        /// decided once, in <see cref="IdentityAffordances"/>, and an office this build does not
        /// recognise grants no role and therefore raises no body.
        /// </summary>
        private static readonly string[][] Offices =
        {
            new[] { AuthorityPolicy.GuardRole, WatchType },
            new[] { AuthorityPolicy.GuildRole, GuildType }
        };

        /// <summary>
        /// One bounded enrollment pass: raise what is newly observed, then answer whose turn it is.
        ///
        /// <paramref name="most"/> bounds both halves, because both are counts of work and a town
        /// the player has never visited must not cost a pass more than a town they live in.
        /// </summary>
        public static OrganizationEnrollmentPass Advance(
            NarrativeWorldState world,
            IVanillaState vanilla,
            GameTime now,
            int most)
        {
            OrganizationEnrollmentPass pass = new OrganizationEnrollmentPass();
            if (world == null)
            {
                return pass;
            }

            int bound = most < 1 ? 1 : most;
            Raise(world, vanilla, pass, bound);
            pass.Roster = Admit(world, pass, bound);
            return pass;
        }

        // -- raising a body from what was observed ------------------------------------------------

        /// <summary>
        /// Groups the observed offices by the institution they belong to, then raises or refreshes
        /// one body per group.
        ///
        /// Ordered throughout - people by id, groups by key - so the same save enrolls the same way
        /// on two loads, and so which body gets an id is not decided by dictionary enumeration.
        /// </summary>
        private static void Raise(
            NarrativeWorldState world,
            IVanillaState vanilla,
            OrganizationEnrollmentPass pass,
            int most)
        {
            EntityId player = vanilla == null ? EntityId.None : vanilla.PlayerId;

            List<NarrativeNpc> people = new List<NarrativeNpc>();
            foreach (KeyValuePair<EntityId, NarrativeNpc> pair in world.Registry.Npcs)
            {
                NarrativeNpc npc = pair.Value;

                // The player's own standing is theirs. An institution the player is enrolled into
                // would be a body acting through the player's hands, which is not what a body is.
                if (npc == null || !npc.IsCanonical || !npc.Alive || npc.Id == player)
                {
                    continue;
                }

                // Nowhere to belong. An office held by somebody the simulation cannot place is a
                // real office and not evidence of a local institution.
                if (npc.HomeSiteId.IsNone || npc.Roles.Count == 0)
                {
                    continue;
                }

                people.Add(npc);
            }

            people.Sort(ById);

            SortedDictionary<string, List<EntityId>> observed =
                new SortedDictionary<string, List<EntityId>>(StringComparer.Ordinal);
            Dictionary<string, string> types = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, EntityId> places = new Dictionary<string, EntityId>();

            for (int i = 0; i < people.Count; i++)
            {
                NarrativeNpc npc = people[i];
                for (int o = 0; o < Offices.Length; o++)
                {
                    if (!npc.Roles.Contains(Offices[o][0]))
                    {
                        continue;
                    }

                    string type = Offices[o][1];
                    string key = type + "@" + npc.HomeSiteId.Value;
                    if (!observed.TryGetValue(key, out List<EntityId> holders))
                    {
                        holders = new List<EntityId>();
                        observed[key] = holders;
                        types[key] = type;
                        places[key] = npc.HomeSiteId;
                    }

                    holders.Add(npc.Id);
                }
            }

            Dictionary<string, Organization> known = Raised(world);

            // A body whose office nobody holds any more. The record stays - history was written
            // under it - but its roll empties, and a body with nobody on it and no ground is not
            // one Admit gives a turn to. Without this, an office that went away would leave a
            // watch acting through men who are no longer the watch.
            foreach (KeyValuePair<string, Organization> pair in known)
            {
                if (!observed.ContainsKey(pair.Key))
                {
                    Reconcile(world, pair.Value, NoHolders);
                }
            }

            if (observed.Count == 0)
            {
                return;
            }

            int raised = 0;
            foreach (KeyValuePair<string, List<EntityId>> group in observed)
            {
                if (!known.TryGetValue(group.Key, out Organization body))
                {
                    if (raised >= most)
                    {
                        // Late rather than lost: the group is still observed tomorrow, and the
                        // bodies already raised keep their turn order.
                        continue;
                    }

                    body = world.Registry.Add(new Organization(
                        world.NewId("org"), NameFor(world, types[group.Key], places[group.Key]), types[group.Key])
                    {
                        Source = OrganizationSource.ObservedMembership,
                        ExternalRef = group.Key,

                        // No holding and no reserves. See the type comment: a settlement its people
                        // were seen in is not ground it keeps, and a body raised with a purse would
                        // be BQ paying out what a vanilla guild is worth.
                        Wealth = 0
                    });

                    pass.Raised.Add(body.Id);
                    raised++;
                }

                Reconcile(world, body, group.Value);
            }
        }

        /// <summary>Bodies this owner has already raised, by what it raised them on.</summary>
        private static Dictionary<string, Organization> Raised(NarrativeWorldState world)
        {
            Dictionary<string, Organization> known = new Dictionary<string, Organization>(StringComparer.Ordinal);
            foreach (KeyValuePair<EntityId, Organization> pair in world.Registry.Organizations)
            {
                Organization body = pair.Value;
                if (body == null
                    || body.Source != OrganizationSource.ObservedMembership
                    || string.IsNullOrEmpty(body.ExternalRef))
                {
                    continue;
                }

                // First by id wins, so a save that somehow carries two bodies on one reference
                // resolves the same way on every load instead of alternating.
                if (!known.TryGetValue(body.ExternalRef, out Organization held)
                    || string.CompareOrdinal(body.Id.Value, held.Id.Value) < 0)
                {
                    known[body.ExternalRef] = body;
                }
            }

            return known;
        }

        /// <summary>
        /// Brings a raised body's roll into line with who still holds the office.
        ///
        /// Both directions, because the roll is the observation rather than a record of it: somebody
        /// dismissed, dead or no longer read as holding the office is no longer one of the watch,
        /// and leaving them on it would have a body sending a man who is not theirs. Only bodies
        /// this owner raised are touched; a crew somebody established keeps its own membership.
        /// </summary>
        private static void Reconcile(NarrativeWorldState world, Organization body, List<EntityId> holders)
        {
            if (body.Source != OrganizationSource.ObservedMembership)
            {
                return;
            }

            for (int i = body.MemberIds.Count - 1; i >= 0; i--)
            {
                EntityId member = body.MemberIds[i];
                if (holders.Contains(member))
                {
                    continue;
                }

                body.MemberIds.RemoveAt(i);
                NarrativeNpc left = world.Registry.GetNpc(member);
                if (left != null)
                {
                    left.OrganizationIds.Remove(body.Id);
                }
            }

            for (int i = 0; i < holders.Count; i++)
            {
                EntityId holder = holders[i];
                if (!body.MemberIds.Contains(holder))
                {
                    body.MemberIds.Add(holder);
                }

                NarrativeNpc npc = world.Registry.GetNpc(holder);
                if (npc != null && !npc.OrganizationIds.Contains(body.Id))
                {
                    npc.OrganizationIds.Add(body.Id);
                }
            }

            // No leader is named. Which guard commands the watch is not something the game says,
            // and BQa-019 treats a leader as a preference among eligible members rather than a
            // requirement - so guessing one would be inventing a chain of command.
        }

        private static string NameFor(NarrativeWorldState world, string type, EntityId place)
        {
            NarrativeSite site = world.Registry.GetSite(place);
            string where = site == null || string.IsNullOrEmpty(site.Name) ? null : site.Name;
            string what = string.Equals(type, WatchType, StringComparison.Ordinal) ? "the watch" : "the guild";
            return where == null ? what : what + " of " + where;
        }

        // -- whose turn it is ---------------------------------------------------------------------

        /// <summary>
        /// Every body with evidence that it is an institution, least recently acted first.
        ///
        /// The same order <see cref="OrganizationActivity"/> uses, for the same reason: with one
        /// shared pool of people between them, the order two bodies are reached in would otherwise
        /// decide which of them got anybody.
        /// </summary>
        private static IReadOnlyList<EntityId> Admit(
            NarrativeWorldState world,
            OrganizationEnrollmentPass pass,
            int most)
        {
            HashSet<string> controls = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<EntityId, NarrativeSite> pair in world.Registry.Sites)
            {
                NarrativeSite site = pair.Value;
                if (site != null && !site.ControllingOrganizationId.IsNone)
                {
                    controls.Add(site.ControllingOrganizationId.Value);
                }
            }

            List<Organization> due = new List<Organization>();
            List<string> refused = new List<string>();
            foreach (KeyValuePair<EntityId, Organization> pair in world.Registry.Organizations)
            {
                Organization body = pair.Value;
                if (body == null)
                {
                    continue;
                }

                string why = WhyNot(world, body, controls);
                if (why == null)
                {
                    due.Add(body);
                }
                else
                {
                    refused.Add(body.Id.Value + ": " + why);
                }
            }

            refused.Sort(StringComparer.Ordinal);
            pass.Refusals.AddRange(refused);

            due.Sort(ByTurn);

            List<EntityId> roster = new List<EntityId>();
            for (int i = 0; i < due.Count && roster.Count < most; i++)
            {
                roster.Add(due[i].Id);
            }

            return roster;
        }

        /// <summary>Why this body is not an institution production may act on, or null when it is.</summary>
        private static string WhyNot(NarrativeWorldState world, Organization body, HashSet<string> controls)
        {
            if (HasSomebody(world, body))
            {
                return null;
            }

            if (controls.Contains(body.Id.Value))
            {
                return null;
            }

            for (int i = 0; i < body.SiteIds.Count; i++)
            {
                NarrativeSite site = world.Registry.GetSite(body.SiteIds[i]);
                if (site != null
                    && (site.ControllingOrganizationId.IsNone || site.ControllingOrganizationId == body.Id))
                {
                    return null;
                }
            }

            return "no living member and no holding on record";
        }

        private static bool HasSomebody(NarrativeWorldState world, Organization body)
        {
            if (Participates(world, body.LeaderId))
            {
                return true;
            }

            for (int i = 0; i < body.MemberIds.Count; i++)
            {
                if (Participates(world, body.MemberIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// BQ's own record of whether somebody is still a person who acts.
        ///
        /// Deliberately not <see cref="IVanillaState.IsAlive"/>: whether a body is an institution
        /// is a fact about the simulation's records, and a member the current build cannot resolve
        /// is not the game saying the guild disbanded. Whether that member can carry a deed today
        /// stays BQa-019's question, asked where the deed is planned.
        /// </summary>
        private static bool Participates(NarrativeWorldState world, EntityId who)
        {
            NarrativeNpc npc = who.IsNone ? null : world.Registry.GetNpc(who);
            return npc != null && npc.IsCanonical && npc.Alive;
        }

        private static int ById(NarrativeNpc a, NarrativeNpc b)
        {
            return string.CompareOrdinal(a.Id.Value, b.Id.Value);
        }

        private static int ByTurn(Organization a, Organization b)
        {
            int turn = a.LastActedAt.CompareTo(b.LastActedAt);
            return turn != 0 ? turn : string.CompareOrdinal(a.Id.Value, b.Id.Value);
        }
    }
}
