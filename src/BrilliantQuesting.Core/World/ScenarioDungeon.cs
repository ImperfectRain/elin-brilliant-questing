using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>Everything one attempt at a scenario dungeon produced, whether or not it made one.</summary>
    public sealed class ScenarioDungeonResult
    {
        private static readonly string[] Nothing = new string[0];

        internal ScenarioDungeonResult(
            SiteCandidateSelection selection,
            SiteContentsReading contents,
            SiteRealizationResult realization,
            SiteGenesisResult genesis,
            IReadOnlyList<string> refusals)
        {
            Selection = selection;
            Contents = contents;
            Realization = realization;
            Genesis = genesis;
            Refusals = refusals ?? Nothing;
        }

        /// <summary>The plans that were drawn and the one that was chosen (BQ-092).</summary>
        public SiteCandidateSelection Selection { get; }

        /// <summary>What the matter leaves in it (BQ-091), or null where it never got that far.</summary>
        public SiteContentsReading Contents { get; }

        /// <summary>The body the plan was given, or the reasons it could not be given one.</summary>
        public SiteRealizationResult Realization { get; }

        /// <summary>What genesis did, or null where nothing was asked of it.</summary>
        public SiteGenesisResult Genesis { get; }

        public SiteStructure Structure => Realization == null ? null : Realization.Structure;

        public NarrativeSite Site => Genesis == null ? null : Genesis.Site;

        /// <summary>Every reason there is no place. Empty when there is one.</summary>
        public IReadOnlyList<string> Refusals { get; }

        /// <summary>A place was made by this call. False on a place that already existed.</summary>
        public bool Created => Genesis != null && Genesis.Outcome == SiteGenesisOutcome.Established;

        /// <summary>Genesis had already run for this place, so nothing was built and nothing staged.</summary>
        public bool AlreadyThere => Genesis != null && Genesis.Outcome == SiteGenesisOutcome.AlreadyEstablished;
    }

    /// <summary>
    /// BQ-140. The one procedural scenario-dungeon family this repository has: worked-out mines
    /// somebody is holding, built from a semantic plan and authored pieces.
    ///
    /// This is the join, and it owns no vocabulary of its own. The plan is BQ-089's composition
    /// chosen by BQ-092 against the errand; what is in it is BQ-091's derivation from the matter's
    /// own history; the body is <see cref="SiteRealization"/>'s assembly of authored pieces; and
    /// the place is made by <see cref="SiteGenesis"/>, which already refuses to build a second one
    /// over a place the world knows (`BQ-087`). Nothing here re-decides any of those.
    ///
    /// <b>One family, deliberately.</b> `site.collapsed_mine` and the `mine` pieces, because the
    /// standing rule is not to build a general dungeon engine (`LW §7`, `PP`) and because a second
    /// family proves extensibility rather than the thing itself. Another kind of site is another
    /// grammar file and another set of piece files - content, and the evidence to back them - not
    /// another branch in here.
    ///
    /// <b>Nothing physical is written down.</b> A site records the grammar, the seed and the errand
    /// it was made for; <see cref="StructureOf"/> rebuilds the same shape from those three whenever
    /// anybody asks. Elin owns the map it made, this owns the plan it was made from, and neither
    /// keeps a copy of the other's (`PP §6`).
    /// </summary>
    public static class ScenarioDungeon
    {
        /// <summary>The one family of pieces this step ships.</summary>
        public const string Family = "mine";

        /// <summary>The one grammar those pieces build.</summary>
        public const string GrammarId = "site.collapsed_mine";

        public static ScenarioDungeonResult Establish(
            NarrativeWorldState world,
            EntityId siteId,
            string name,
            EntityId threadId,
            SiteAffordance objective,
            ulong seed,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            IVanillaState vanilla,
            ISituationStager stager,
            GameTime now,
            int batch = SiteCandidates.DefaultBatch)
        {
            if (world == null || stager == null)
            {
                return Refused("a scenario dungeon needs a world and a stager");
            }

            // A place the world already established is handed straight back, before a plan is
            // drawn and before anything is built. Returning to a mine must not cost a batch of
            // candidates, let alone a second body over the one the player has walked through.
            NarrativeSite existing = world.Registry.GetSite(siteId);
            if (existing != null && existing.Established)
            {
                return new ScenarioDungeonResult(
                    null,
                    null,
                    null,
                    new SiteGenesisResult(SiteGenesisOutcome.AlreadyEstablished, existing, null),
                    null);
            }

            SiteGrammar grammar = grammars == null ? null : grammars.Get(GrammarId);
            if (grammar == null)
            {
                return Refused("the bundle has no " + GrammarId + " to plan a scenario dungeon from");
            }

            SiteCandidateSelection selection = SiteCandidates.Select(
                grammar, objective, seed, batch, actions, vanilla);
            if (!selection.Selected)
            {
                return new ScenarioDungeonResult(selection, null, null, null, new[] { selection.Refusal });
            }

            SiteContentsReading contents = SiteContents.Derive(world, threadId, selection.Layout, vanilla);
            if (!contents.Furnished)
            {
                return new ScenarioDungeonResult(selection, contents, null, null, contents.Refusals);
            }

            SiteRealizationResult realization = SiteRealization.Realize(
                Family, selection.Layout, objective, pieces, contents, actions, vanilla);
            if (!realization.Built)
            {
                return new ScenarioDungeonResult(selection, contents, realization, null, realization.Refusals);
            }

            SitePlan plan = selection.NewPlan(siteId, name, threadId);
            contents.ApplyTo(plan);
            plan.Objective = objective.ToString();
            plan.Structure = realization.Structure;

            SiteGenesisResult genesis = SiteGenesis.Establish(world, plan, stager, now);
            return new ScenarioDungeonResult(
                selection,
                contents,
                realization,
                genesis,
                genesis.Outcome == SiteGenesisOutcome.Established ? null : genesis.Reasons);
        }

        /// <summary>
        /// The shape of a place that already exists, rebuilt from what the place records.
        ///
        /// A read, and only a read: it stages nothing, changes nothing and is the same answer
        /// before and after a reload, because the grammar, the seed and the errand are all the site
        /// carries and all this needs. That is what makes "the site is not regenerated on a return
        /// visit" true of the physical shape as well as of the occupants - there is no second act
        /// of building to avoid, only the same derivation run again.
        /// </summary>
        public static SiteRealizationResult StructureOf(
            NarrativeSite site,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            if (site == null)
            {
                return new SiteRealizationResult(null, null, new[] { "there is no place to read" });
            }

            SiteLayout layout = grammars == null ? null : grammars.LayoutOf(site);
            if (layout == null)
            {
                return new SiteRealizationResult(
                    null, null, new[] { site.Name + " was not planned from a grammar this bundle has" });
            }

            SiteAffordance objective;
            if (!SiteGrammarContent.TryParseAffordance(site.Objective, out objective))
            {
                return new SiteRealizationResult(
                    null, null, new[] { site.Name + " does not record what it was made for" });
            }

            return SiteRealization.Realize(Family, layout, objective, pieces, null, actions, vanilla);
        }

        /// <summary>
        /// The shapes a batch of seeds would produce, before anything is made.
        ///
        /// BQ-140's done-when is that a handful of authored pieces produce meaningfully different
        /// navigation and problem structures across seeds, which is a claim about the plans rather
        /// than about a save - so it is answerable, and answered, without a world.
        /// </summary>
        public static IReadOnlyList<SiteStructure> Draw(
            IEnumerable<ulong> seeds,
            SiteAffordance objective,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            IVanillaState vanilla,
            int batch = SiteCandidates.DefaultBatch)
        {
            List<SiteStructure> structures = new List<SiteStructure>();
            SiteGrammar grammar = grammars == null ? null : grammars.Get(GrammarId);
            if (grammar == null || seeds == null)
            {
                return structures.AsReadOnly();
            }

            foreach (ulong seed in seeds)
            {
                SiteCandidateSelection selection = SiteCandidates.Select(
                    grammar, objective, seed, batch, actions, vanilla);
                if (!selection.Selected)
                {
                    continue;
                }

                SiteRealizationResult realization = SiteRealization.Realize(
                    Family, selection.Layout, objective, pieces, null, actions, vanilla);
                if (realization.Built)
                {
                    structures.Add(realization.Structure);
                }
            }

            return structures.AsReadOnly();
        }

        private static ScenarioDungeonResult Refused(string reason)
        {
            return new ScenarioDungeonResult(null, null, null, null, new[] { reason });
        }
    }
}
