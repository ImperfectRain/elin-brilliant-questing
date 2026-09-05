using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Where a place a matter needs was found, worst case last.
    ///
    /// The order is `LW §7.2` and `PM §14` read together: the cheapest place is the one that is
    /// already this matter's, then one the world already had, then one this mod made for a matter
    /// that is over. Generating is the fourth answer, not the first.
    /// </summary>
    public enum SiteReuseTier
    {
        /// <summary>Nothing existing could host it. A new place has to be made.</summary>
        None,

        /// <summary>A place this same matter already uses. Nothing about the world changes at all.</summary>
        Bound,

        /// <summary>
        /// A place the world already had and this mod did not make - a town, a shop, a road, a
        /// mine. Reusing one adds nothing to the map, which is why it outranks every made place.
        /// </summary>
        WorldsOwn,

        /// <summary>
        /// A place genesis made for some earlier matter, and no matter that can still surface
        /// holds it now. Recontextualising one is still cheaper than a second place next to it.
        /// </summary>
        Generated
    }

    public enum SiteReuseOutcome
    {
        /// <summary>A place that already exists can host this. Nothing needs generating.</summary>
        Reuse,

        /// <summary>Nothing that exists can host it, so the plan is genesis's after all.</summary>
        Generate
    }

    /// <summary>
    /// One existing place, weighed against what a matter needs, and every reason it was refused.
    ///
    /// Refusals are the point rather than a by-product: the step's done-when is that the inspector
    /// can say *why* a place was reused, and "why this one" is only half an answer without "and
    /// why not those".
    /// </summary>
    public sealed class SiteCandidateReading
    {
        private static readonly string[] NoRefusals = new string[0];

        internal SiteCandidateReading(NarrativeSite site, SiteReuseTier tier, IReadOnlyList<string> refusals)
        {
            Site = site;
            Tier = tier;
            Refusals = refusals ?? NoRefusals;
        }

        public NarrativeSite Site { get; }

        public EntityId SiteId => Site == null ? EntityId.None : Site.Id;

        public SiteReuseTier Tier { get; }

        public IReadOnlyList<string> Refusals { get; }

        /// <summary>Nothing stood in the way. Whether it was picked is the tier's business.</summary>
        public bool CanHost => Refusals.Count == 0;

        /// <summary>Set on the one place the choice landed on, so a trace can mark it.</summary>
        public bool Chosen { get; internal set; }
    }

    /// <summary>Which place a matter should use, and how that was decided.</summary>
    public sealed class SiteChoice
    {
        private static readonly SiteCandidateReading[] NothingConsidered = new SiteCandidateReading[0];

        internal SiteChoice(
            SiteReuseOutcome outcome,
            NarrativeSite site,
            SiteReuseTier tier,
            string reason,
            IReadOnlyList<SiteCandidateReading> considered)
        {
            Outcome = outcome;
            Site = site;
            Tier = tier;
            Reason = reason ?? string.Empty;
            Considered = considered ?? NothingConsidered;
        }

        public SiteReuseOutcome Outcome { get; }

        /// <summary>The place to use, or null when nothing existing could host the matter.</summary>
        public NarrativeSite Site { get; }

        public SiteReuseTier Tier { get; }

        /// <summary>Why this answer. Inspector-facing, never player-facing.</summary>
        public string Reason { get; }

        /// <summary>Every existing place weighed, in the order they were weighed.</summary>
        public IReadOnlyList<SiteCandidateReading> Considered { get; }

        public bool Reused => Outcome == SiteReuseOutcome.Reuse;
    }

    /// <summary>What a matter ended up with, whichever way the question went.</summary>
    public sealed class SiteProvision
    {
        internal SiteProvision(SiteChoice choice, SiteGenesisResult genesis, NarrativeSite site)
        {
            Choice = choice;
            Genesis = genesis;
            Site = site;
        }

        public SiteChoice Choice { get; }

        /// <summary>What genesis said, or null when it was never asked because a place was reused.</summary>
        public SiteGenesisResult Genesis { get; }

        /// <summary>The place the matter now uses, or null when it has none.</summary>
        public NarrativeSite Site { get; }

        public bool Reused => Choice != null && Choice.Reused && Site != null;

        public bool Generated => Genesis != null && Genesis.Created;

        /// <summary>The matter has somewhere, however it got there.</summary>
        public bool Placed => Site != null;
    }

    /// <summary>
    /// BQ-088. Asks whether a place a matter needs already exists before anything is made.
    ///
    /// The requirement it weighs is the <see cref="SitePlan"/> itself, read as requirements rather
    /// than as instructions. A caller that might have to generate has to build the plan anyway, and
    /// a second vocabulary for "what a place must be" would be one more thing to get out of step
    /// with the one genesis validates. So only the fields that describe the *place* are read - the
    /// matter it belongs to, its kind, and whether what it keeps is behind somebody's permission.
    /// Occupants, cargo and approaches describe a place being built, and reuse builds nothing.
    ///
    /// Three things a place asserts about itself decide whether it can host a matter:
    ///
    /// - **Kind.** A hideout is not a market. A plan naming no kind is not fussy.
    /// - **Reach.** <see cref="NarrativeSite.Restricted"/> is the one route property every place
    ///   carries honestly, and `D058` already made it the load-bearing half of "two ways in". A
    ///   matter that turns on getting past somebody cannot happen where there is nobody to get
    ///   past, and a matter that never planned for a lock should not be handed one.
    /// - **Whether it is spoken for.** A place genesis made exists because one matter needed it,
    ///   so handing it to a second live matter puts two invented matters in one invented room. A
    ///   place the world already had is shared infrastructure - a market row hosts as many matters
    ///   as the town has - and is never refused for being busy.
    ///
    /// What is deliberately *not* read: <see cref="NarrativeSite.Approaches"/>, because it records
    /// how a place was planned rather than which verbs work there, and the places the world already
    /// had have none - an empty list is "never described in those terms", not "no way in", and
    /// reading it as a refusal would rule out the whole first tier on an absence. Whether a route
    /// is actually available is the action library's precondition question and BQ-090's projection.
    /// <see cref="NarrativeSite.Persistence"/> is not read either: it is left at its default on
    /// every place nobody set it on, so it is not evidence about them.
    /// </summary>
    public static class SiteReuse
    {
        /// <summary>
        /// Which place this matter should use, with the reasons for every place that was passed
        /// over. Reads the world and changes nothing.
        /// </summary>
        public static SiteChoice Choose(NarrativeWorldState world, SitePlan plan)
        {
            if (world == null || plan == null)
            {
                return new SiteChoice(
                    SiteReuseOutcome.Generate, null, SiteReuseTier.None, "no world or no plan to place", null);
            }

            // A matter that does not exist gets no place. Genesis already refuses a plan with no
            // thread behind it, and a reuse that bound one anyway would put a place into the world
            // on terms genesis would have rejected - so the question is handed to the one decider
            // that owns it rather than answered twice.
            if (FindThread(world, plan.ThreadId) == null)
            {
                return new SiteChoice(
                    SiteReuseOutcome.Generate,
                    null,
                    SiteReuseTier.None,
                    "there is no matter for a place to belong to",
                    null);
            }

            List<SiteCandidateReading> considered = new List<SiteCandidateReading>();
            SiteCandidateReading best = null;

            List<NarrativeSite> candidates = SitesInOrder(world);
            for (int i = 0; i < candidates.Count; i++)
            {
                SiteCandidateReading reading = Weigh(world, plan, candidates[i]);
                considered.Add(reading);
                if (reading.CanHost && (best == null || Prefers(reading.Tier, best.Tier)))
                {
                    best = reading;
                }
            }

            if (best == null)
            {
                return new SiteChoice(
                    SiteReuseOutcome.Generate,
                    null,
                    SiteReuseTier.None,
                    considered.Count == 0
                        ? "the world knows no places yet, so there is nothing to reuse"
                        : "none of the " + considered.Count + " place(s) the world knows can host this matter",
                    considered);
            }

            best.Chosen = true;
            return new SiteChoice(SiteReuseOutcome.Reuse, best.Site, best.Tier, WhyChosen(best.Tier), considered);
        }

        /// <summary>
        /// Gives the matter somewhere: the place the choice landed on, or a new one from genesis
        /// when nothing existing could host it.
        ///
        /// Reuse is a binding and nothing else. The place is added to the matter's own list and
        /// left exactly as it is - nobody is staged into it, none of the plan's cargo is put there,
        /// and no event is written. What a reused place then holds is the situation's own business
        /// through the ordinary verbs; a reuse that staged the plan's contents would be genesis
        /// under another name, and would overwrite the history that made the place worth reusing.
        /// Appending nothing also keeps `D058` true from the other side: a matter adopting a place
        /// is bookkeeping, not something that happened to anybody.
        /// </summary>
        public static SiteProvision Provide(
            NarrativeWorldState world,
            SitePlan plan,
            ISituationStager stager,
            GameTime now)
        {
            SiteChoice choice = Choose(world, plan);
            if (choice.Reused)
            {
                Bind(world, plan.ThreadId, choice.Site);
                return new SiteProvision(choice, null, choice.Site);
            }

            SiteGenesisResult genesis = SiteGenesis.Establish(world, plan, stager, now);
            return new SiteProvision(choice, genesis, genesis.Site);
        }

        /// <summary>
        /// Whether some matter that can still surface holds this place. Latent, active and dormant
        /// matters all count - a dormant one can wake up, and its place is still spoken for -
        /// while a resolved, inherited or quarantined one can never surface again and frees it.
        /// </summary>
        public static bool SpokenFor(NarrativeWorldState world, EntityId siteId, EntityId exceptThread)
        {
            if (world == null || siteId.IsNone)
            {
                return false;
            }

            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread.Id == exceptThread || !CanStillSurface(thread))
                {
                    continue;
                }

                if (thread.SiteIds.Contains(siteId))
                {
                    return true;
                }
            }

            return false;
        }

        private static SiteCandidateReading Weigh(NarrativeWorldState world, SitePlan plan, NarrativeSite site)
        {
            SiteReuseTier tier = TierOf(world, plan, site);
            List<string> refusals = new List<string>();

            if (!string.IsNullOrEmpty(plan.SiteType)
                && !string.Equals(site.SiteType, plan.SiteType, StringComparison.OrdinalIgnoreCase))
            {
                refusals.Add("it is a " + Kind(site.SiteType) + ", and this matter needs a " + Kind(plan.SiteType));
            }

            if (site.Restricted != plan.Restricted)
            {
                refusals.Add(site.Restricted
                    ? "what it keeps is behind somebody's permission, and this matter needs a place anybody can reach"
                    : "what it keeps is open to anybody, and this matter needs a place somebody has to let you into");
            }

            // Only a place this mod made can be spoken for. The world's own places are shared, and
            // a matter is not a claim on a town.
            if (tier == SiteReuseTier.Generated && SpokenFor(world, site.Id, plan.ThreadId))
            {
                refusals.Add("another matter that can still surface is using it");
            }

            return new SiteCandidateReading(site, tier, refusals.Count == 0 ? null : refusals);
        }

        /// <summary>
        /// Whether the first tier beats the second. The tiers are declared cheapest-first, so the
        /// earlier one wins; <see cref="SiteReuseTier.None"/> never reaches here because it is what
        /// the absence of a candidate is called, not a candidate's answer.
        /// </summary>
        private static bool Prefers(SiteReuseTier candidate, SiteReuseTier incumbent)
        {
            return candidate < incumbent;
        }

        private static SiteReuseTier TierOf(NarrativeWorldState world, SitePlan plan, NarrativeSite site)
        {
            if (site.Id == plan.SiteId || Holds(world, plan.ThreadId, site.Id))
            {
                return SiteReuseTier.Bound;
            }

            return site.Established ? SiteReuseTier.Generated : SiteReuseTier.WorldsOwn;
        }

        private static string WhyChosen(SiteReuseTier tier)
        {
            switch (tier)
            {
                case SiteReuseTier.Bound:
                    return "this matter already uses it, so nothing has to change";
                case SiteReuseTier.WorldsOwn:
                    return "the world already had it, so reusing it adds nothing to the map";
                case SiteReuseTier.Generated:
                    return "this mod made it for a matter that is over, and no live matter holds it";
                default:
                    return string.Empty;
            }
        }

        private static void Bind(NarrativeWorldState world, EntityId threadId, NarrativeSite site)
        {
            NarrativeThread thread = FindThread(world, threadId);
            if (thread != null && !thread.SiteIds.Contains(site.Id))
            {
                thread.SiteIds.Add(site.Id);
            }
        }

        private static bool Holds(NarrativeWorldState world, EntityId threadId, EntityId siteId)
        {
            NarrativeThread thread = FindThread(world, threadId);
            return thread != null && thread.SiteIds.Contains(siteId);
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

        private static bool CanStillSurface(NarrativeThread thread)
        {
            return thread.State == ThreadState.Latent
                   || thread.State == ThreadState.Active
                   || thread.State == ThreadState.Dormant;
        }

        /// <summary>
        /// Every place the world knows, in one order. The registry is a dictionary, and a policy
        /// whose answer depended on insertion order would not survive a reload of the same save.
        /// </summary>
        private static List<NarrativeSite> SitesInOrder(NarrativeWorldState world)
        {
            List<NarrativeSite> sites = new List<NarrativeSite>();
            foreach (KeyValuePair<EntityId, NarrativeSite> entry in world.Registry.Sites)
            {
                if (entry.Value != null)
                {
                    sites.Add(entry.Value);
                }
            }

            sites.Sort(ById);
            return sites;
        }

        private static int ById(NarrativeSite left, NarrativeSite right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static string Kind(string siteType)
        {
            return string.IsNullOrEmpty(siteType) ? "place" : siteType;
        }
    }
}
