using System;
using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// One actor's standing intention, as it entered the batch (BQa-015).
    ///
    /// Immutable, and holding no context. A candidate that carried a built
    /// <see cref="ActionContext"/> would be carrying a reading of the world from whenever
    /// selection happened, and the whole point of revalidating a winner is that the world may
    /// have moved since - most obviously because an earlier contender in the same batch just
    /// moved it.
    ///
    /// <see cref="Motive"/> is the caller's own weight, not a second opinion about it: goal
    /// ranking stays with the goal owner (`D087`), and arbitration only needs the contenders
    /// ordered against each other.
    /// </summary>
    public sealed class ActionCandidate
    {
        public ActionCandidate(ActionIntent intent, ActionContest contest, double motive, GameTime readyAt)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            Contest = contest;
            Motive = motive < 0.0 ? 0.0 : motive;
            ReadyAt = readyAt;
        }

        public ActionIntent Intent { get; }

        /// <summary>What this intention is reaching for, and whether anybody else can have it too.</summary>
        public ActionContest Contest { get; }

        public EntityId Actor => Intent.Actor;

        /// <summary>How much its owner wants it, as the goal owner already weighed it.</summary>
        public double Motive { get; }

        /// <summary>The soonest this actor could act. Earlier settles a dead-level contest.</summary>
        public GameTime ReadyAt { get; }

        /// <summary>The contest read off the verb when the caller did not name one itself.</summary>
        public static ActionCandidate For(
            ActionRegistry registry,
            ActionIntent intent,
            double motive,
            GameTime readyAt)
        {
            NarrativeAction action = registry == null || intent == null ? null : registry.Get(intent.ActionId);
            return new ActionCandidate(intent, ActionContest.ForIntent(action, intent), motive, readyAt);
        }

        public override string ToString()
        {
            return Actor + " would " + Intent.ActionId + " (" + Contest + ")";
        }
    }

    /// <summary>
    /// A hold on one indivisible contest, for as long as one attempt takes (BQa-015).
    ///
    /// Deliberately the smallest thing that prevents the failure it exists for: a coarse
    /// scheduler walking a list and letting two actors both finish taking the same purse. It is
    /// not a booking, not a plan and not a promise - it says only that this attempt is the one
    /// currently running against this contest, and it is released the moment that attempt is
    /// over, however it ended.
    ///
    /// Nothing about it is ever written to a save. A reservation in a save file is a claim about
    /// the world that no event ever justified; history gets the committed outcome and nothing
    /// else. <see cref="ReleasedBecause"/> exists so the release is visible rather than inferred.
    /// </summary>
    public sealed class TransientClaim
    {
        internal TransientClaim(string contestKey, EntityId holder)
        {
            ContestKey = contestKey ?? string.Empty;
            Holder = holder;
            IsHeld = true;
            ReleasedBecause = string.Empty;
        }

        public string ContestKey { get; }

        public EntityId Holder { get; }

        public bool IsHeld { get; private set; }

        /// <summary>Empty while held; afterwards the reason in as many words.</summary>
        public string ReleasedBecause { get; private set; }

        internal void Release(string because)
        {
            if (!IsHeld)
            {
                return;
            }

            IsHeld = false;
            ReleasedBecause = string.IsNullOrEmpty(because) ? "the batch ended" : because;
        }

        public override string ToString()
        {
            return Holder + " on " + ContestKey + (IsHeld ? " (held)" : " (released: " + ReleasedBecause + ")");
        }
    }

    /// <summary>What became of one candidate in the batch.</summary>
    public enum ArbitrationVerdict
    {
        /// <summary>It reached execution: the world was asked again and the verb was performed.</summary>
        Attempted,

        /// <summary>The world or the verb refused it when it was asked again. Nothing ran.</summary>
        Refused,

        /// <summary>Somebody else finished the indivisible thing first. Nothing ran.</summary>
        Yielded,

        /// <summary>The batch was stopped before its turn. Nothing ran, and nothing is held.</summary>
        Cancelled,

        /// <summary>Performing it threw. The claim was released and the contest stayed open.</summary>
        Faulted
    }

    /// <summary>One candidate's place in its contest and what came of it.</summary>
    public sealed class ArbitrationDecision
    {
        internal ArbitrationDecision(ActionCandidate candidate, double standing, ulong tieKey)
        {
            Candidate = candidate;
            Standing = standing;
            TieKey = tieKey;
            Verdict = ArbitrationVerdict.Yielded;
            Why = string.Empty;
        }

        public ActionCandidate Candidate { get; }

        public EntityId Actor => Candidate.Actor;

        public ActionContest Contest => Candidate.Contest;

        /// <summary>Motive as its owner weighed it, scaled by what the place and the hour allowed.</summary>
        public double Standing { get; }

        /// <summary>The keyed draw that settles a dead-level contest. Seeded, never enumerated.</summary>
        public ulong TieKey { get; }

        /// <summary>Position within the contest once ranked. Zero is first refusal at it.</summary>
        public int Rank { get; internal set; }

        public ArbitrationVerdict Verdict { get; internal set; }

        /// <summary>Why it ended that way, in the words of whoever refused it.</summary>
        public string Why { get; internal set; }

        /// <summary>The attempt, for <see cref="ArbitrationVerdict.Attempted"/>; null otherwise.</summary>
        public ActionAttempt Attempt { get; internal set; }

        /// <summary>Whether this attempt is the one that finished the contested thing.</summary>
        public bool Committed => Attempt != null && Attempt.Outcome != null && Attempt.Outcome.Succeeded;

        public override string ToString()
        {
            string head = "#" + Rank + " " + Actor + " " + Candidate.Intent.ActionId
                          + " standing " + Standing.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                          + ": " + Verdict.ToString().ToLowerInvariant();
            return Why.Length > 0 ? head + " - " + Why : head;
        }
    }

    /// <summary>Everything the batch decided, and the claims it took and gave back.</summary>
    public sealed class ArbitrationResult
    {
        internal ArbitrationResult(
            string batchKey,
            IReadOnlyList<ArbitrationDecision> decisions,
            IReadOnlyList<TransientClaim> claims)
        {
            BatchKey = batchKey ?? string.Empty;
            Decisions = decisions;
            Claims = claims;
        }

        public string BatchKey { get; }

        /// <summary>By contest key, then by rank. Never in the order the candidates arrived.</summary>
        public IReadOnlyList<ArbitrationDecision> Decisions { get; }

        /// <summary>Every claim the batch took. All of them released by the time this exists.</summary>
        public IReadOnlyList<TransientClaim> Claims { get; }

        /// <summary>
        /// Claims still held after the batch. Always zero, and asserted rather than assumed: a
        /// held claim outliving its batch is the one state this contract must never produce.
        /// </summary>
        public int OutstandingClaims
        {
            get
            {
                int held = 0;
                for (int i = 0; i < Claims.Count; i++)
                {
                    if (Claims[i].IsHeld)
                    {
                        held++;
                    }
                }

                return held;
            }
        }

        /// <summary>The decisions that actually changed the contested thing.</summary>
        public IReadOnlyList<ArbitrationDecision> Committed
        {
            get
            {
                List<ArbitrationDecision> committed = new List<ArbitrationDecision>();
                for (int i = 0; i < Decisions.Count; i++)
                {
                    if (Decisions[i].Committed)
                    {
                        committed.Add(Decisions[i]);
                    }
                }

                return committed;
            }
        }

        public ArbitrationDecision For(EntityId actor)
        {
            for (int i = 0; i < Decisions.Count; i++)
            {
                if (Decisions[i].Actor == actor)
                {
                    return Decisions[i];
                }
            }

            return null;
        }

        /// <summary>The whole batch as text, for the inspector.</summary>
        public string Explain()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("batch ").Append(BatchKey).Append(": ").Append(Decisions.Count).Append(" intention(s)");
            string contest = null;
            for (int i = 0; i < Decisions.Count; i++)
            {
                ArbitrationDecision decision = Decisions[i];
                string key = decision.Contest.Key;
                if (key != contest)
                {
                    contest = key;
                    sb.Append("\n  ").Append(decision.Contest);
                }

                sb.Append("\n    ").Append(decision);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Asked, before each execution, whether the batch may still run (BQa-015).
    ///
    /// The seam a work budget plugs into. BQa-016 owns per-pass budgets and fairness; this only
    /// needs somewhere to ask, and the guarantee that stopping releases whatever is held rather
    /// than stranding it.
    /// </summary>
    public interface IBatchCancellation
    {
        bool IsCancelled { get; }
    }

    /// <summary>
    /// Opens a context for one intention against the world <em>as it is now</em> (BQa-015).
    ///
    /// The reason arbitration takes this rather than a list of contexts. A context read during
    /// selection is a reading of an older world, and in a contested batch the older world is
    /// precisely the one where nobody had taken the purse yet. Asking again immediately before
    /// execution is what makes "revalidate each winner against current state" a fact about the
    /// code rather than a claim in a document.
    /// </summary>
    public interface IAttemptEnvironment
    {
        bool TryOpen(ActionIntent intent, out ActionContext context, out string refusal);
    }

    /// <summary>
    /// The production environment: <see cref="ActorContexts"/> and nothing else.
    ///
    /// It adds no construction of its own - both evidence modes already exist and both already
    /// fail closed - and copies onto the context only what the intent itself carries. A verb that
    /// needs a binding the intent has no room for is a verb whose caller supplies its own
    /// environment; inventing one here would be inventing the attempt's subject.
    /// </summary>
    public sealed class ActorContextEnvironment : IAttemptEnvironment
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;
        private readonly ICheckResolver _checks;
        private readonly DeterministicRng _rng;
        private readonly ContextObservation _mode;

        public ActorContextEnvironment(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            DeterministicRng rng,
            ContextObservation mode)
        {
            _world = world;
            _vanilla = vanilla;
            _checks = checks;
            _rng = rng;
            _mode = mode;
        }

        public bool TryOpen(ActionIntent intent, out ActionContext context, out string refusal)
        {
            context = null;
            refusal = string.Empty;
            if (intent == null)
            {
                refusal = "nothing to attempt";
                return false;
            }

            bool built = _mode == ContextObservation.OffScreen
                ? ActorContexts.TryBuildOffScreen(_world, _vanilla, _checks, _rng, intent.Actor, intent.Target, out context, out refusal)
                : ActorContexts.TryBuild(_world, _vanilla, _checks, _rng, intent.Actor, intent.Target, out context, out refusal);

            if (!built)
            {
                return false;
            }

            context.SubjectFact = intent.SubjectFact;
            context.SubjectItem = intent.SubjectItem;
            context.Thread = intent.Thread;
            return true;
        }
    }

    /// <summary>
    /// One immutable set of standing intentions, arbitrated together (BQa-015).
    ///
    /// <b>Why a batch at all.</b> Every off-screen owner today walks its own candidates and runs
    /// them as it meets them, so which of two thieves lifts the purse is decided by which of them
    /// the registry's work queue reached first. That is a stable iteration order deciding an
    /// outcome, and it is the one thing this step exists to remove. Gathering copies the
    /// candidates at the moment they are gathered, so a caller that keeps adding to its own list
    /// cannot change a decision that is already being made.
    ///
    /// <b>How a contest is settled.</b> Contenders are grouped by
    /// <see cref="ActionContest.Key"/>, the groups are walked in ordinal key order, and inside a
    /// group the order is: standing first (the motive its owner gave it, scaled by what
    /// <see cref="ActionOpportunity"/> said the place and the hour allowed), then whoever is
    /// ready sooner, then a keyed draw. The draw is forked from the world stream by batch,
    /// contest and contender, so it varies with the seed and with the batch - which is to say
    /// with time - and never with the order anything was enumerated in. The final discriminator
    /// is the contender's own id, so the order is total even if two draws collide.
    ///
    /// <b>What a claim does and does not buy.</b> Only an exclusive contest takes one, and it
    /// buys exactly one thing: nobody else executes against that contest while it is held. It is
    /// released on refusal, on a fault, on cancellation, when the attempt commits, and at the end
    /// of the batch - and the only one of those that also closes the contest is a commit. An
    /// attempt that was made and failed leaves the purse where it was, so the next contender gets
    /// their turn; that is the difference between "somebody got there first" and "somebody tried".
    ///
    /// <b>The player is a contender.</b> There is no player branch here and there must never be
    /// one. The player enters the same batch, is ranked by the same three keys, and loses to an
    /// NPC who wants it more, is readier, or wins the draw.
    ///
    /// Nothing here is saved. The result, the claims, the rankings and the traces are all
    /// transient; what survives the batch is whatever the performed verbs recorded through their
    /// own owners, exactly as if they had been run one at a time.
    /// </summary>
    public sealed class ArbitrationBatch
    {
        /// <summary>
        /// How close two standings have to be to count as level. Small enough that a genuine
        /// difference in motive or opportunity always decides, wide enough that arithmetic noise
        /// does not quietly become a ranking.
        /// </summary>
        private const double Level = 1e-9;

        private readonly ActionCandidate[] _candidates;

        private ArbitrationBatch(string key, ActionCandidate[] candidates)
        {
            Key = key;
            _candidates = candidates;
        }

        /// <summary>Which batch this is. Part of every tie-break key, so ties move with it.</summary>
        public string Key { get; }

        /// <summary>The gathered intentions, in the order they were gathered. Read-only.</summary>
        public IReadOnlyList<ActionCandidate> Candidates => _candidates;

        /// <summary>
        /// Takes the batch's one input, copying it. A null candidate is a caller's bug rather
        /// than an empty intention and says so here instead of at execution.
        /// </summary>
        public static ArbitrationBatch Gather(string key, IEnumerable<ActionCandidate> candidates)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(
                    "A batch is keyed, because the key is what makes a tie-break vary with time rather than with enumeration order.",
                    nameof(key));
            }

            List<ActionCandidate> copy = new List<ActionCandidate>();
            if (candidates != null)
            {
                foreach (ActionCandidate candidate in candidates)
                {
                    if (candidate == null)
                    {
                        throw new ArgumentException("A batch cannot hold a null candidate.", nameof(candidates));
                    }

                    copy.Add(candidate);
                }
            }

            return new ArbitrationBatch(key, copy.ToArray());
        }

        /// <summary>
        /// Ranks every contest, then executes the winners, revalidating each against the world as
        /// it is at that moment.
        /// </summary>
        public ArbitrationResult Resolve(
            ActionRegistry registry,
            IAttemptEnvironment environment,
            DeterministicRng world,
            IBatchCancellation cancellation = null)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            List<TransientClaim> claims = new List<TransientClaim>();
            List<ArbitrationDecision> ordered = new List<ArbitrationDecision>();

            List<string> contests = new List<string>();
            Dictionary<string, List<ArbitrationDecision>> grouped = new Dictionary<string, List<ArbitrationDecision>>(StringComparer.Ordinal);

            for (int i = 0; i < _candidates.Length; i++)
            {
                ActionCandidate candidate = _candidates[i];
                ArbitrationDecision decision = new ArbitrationDecision(
                    candidate,
                    Standing(registry, environment, candidate),
                    TieKey(world, candidate));

                // An uncontested intention is its own group, keyed by the contender, so that it
                // is ranked, walked and reported exactly like a contested one instead of running
                // down a second path nobody tests.
                string key = candidate.Contest.IsSomething
                    ? candidate.Contest.Key
                    : "uncontested|" + candidate.Actor.Value + "|" + candidate.Intent.ActionId + "|" + i;

                List<ArbitrationDecision> group;
                if (!grouped.TryGetValue(key, out group))
                {
                    group = new List<ArbitrationDecision>();
                    grouped.Add(key, group);
                    contests.Add(key);
                }

                group.Add(decision);
            }

            // Ordinal, so the walk cannot inherit the dictionary's enumeration order - nor the
            // order the candidates arrived in, which is the same defect wearing a different hat.
            contests.Sort(StringComparer.Ordinal);

            for (int c = 0; c < contests.Count; c++)
            {
                List<ArbitrationDecision> group = grouped[contests[c]];
                Rank(group);
                Settle(contests[c], group, registry, environment, claims, cancellation);
                ordered.AddRange(group);
            }

            for (int i = 0; i < claims.Count; i++)
            {
                claims[i].Release("the batch ended");
            }

            return new ArbitrationResult(Key, ordered, claims);
        }

        /// <summary>
        /// The snapshot reading a contender is ranked on: their own motive, scaled by what the
        /// world allowed. A reading the world refuses scores zero rather than being dropped, so
        /// it is still reported and still gets its turn if everyone above it falls away.
        ///
        /// Side-effect free, like everything it calls. Ranking must not move the RNG or the
        /// world, or classifying the losers would change what the winner rolls.
        /// </summary>
        private static double Standing(ActionRegistry registry, IAttemptEnvironment environment, ActionCandidate candidate)
        {
            NarrativeAction action = registry.Get(candidate.Intent.ActionId);
            if (action == null)
            {
                return 0.0;
            }

            ActionContext context;
            string refusal;
            if (!environment.TryOpen(candidate.Intent, out context, out refusal))
            {
                return 0.0;
            }

            AttemptFeasibility feasibility = AttemptFeasibility.Classify(action, context);
            if (!feasibility.IsPossible)
            {
                return 0.0;
            }

            double plausibility = feasibility.Opportunity == null ? 1.0 : feasibility.Opportunity.Plausibility;
            return candidate.Motive * plausibility;
        }

        /// <summary>
        /// The draw that settles a dead-level contest, forked rather than drawn: a fork derives
        /// from the seed without advancing the parent, so ranking a contest - however many
        /// contenders it has - cannot move a single check anybody is about to roll.
        /// </summary>
        private ulong TieKey(DeterministicRng world, ActionCandidate candidate)
        {
            return RngStreams
                .TieBreak(world, Key, candidate.Contest.Key, candidate.Actor)
                .NextUInt64();
        }

        private static void Rank(List<ArbitrationDecision> group)
        {
            group.Sort(Compare);
            for (int i = 0; i < group.Count; i++)
            {
                group[i].Rank = i;
            }
        }

        /// <summary>
        /// Wants it more, then readier, then the draw, then the id. Total and enumeration-free:
        /// every key is a property of the contender, so the same four contenders rank the same
        /// way whatever order they were handed over in.
        /// </summary>
        private static int Compare(ArbitrationDecision a, ArbitrationDecision b)
        {
            if (Math.Abs(a.Standing - b.Standing) > Level)
            {
                return a.Standing > b.Standing ? -1 : 1;
            }

            int timing = a.Candidate.ReadyAt.CompareTo(b.Candidate.ReadyAt);
            if (timing != 0)
            {
                return timing;
            }

            if (a.TieKey != b.TieKey)
            {
                return a.TieKey < b.TieKey ? -1 : 1;
            }

            return string.CompareOrdinal(a.Actor.Value, b.Actor.Value);
        }

        private static void Settle(
            string contestKey,
            List<ArbitrationDecision> group,
            ActionRegistry registry,
            IAttemptEnvironment environment,
            List<TransientClaim> claims,
            IBatchCancellation cancellation)
        {
            bool exclusive = group[0].Contest.IsExclusive;
            bool taken = false;

            for (int i = 0; i < group.Count; i++)
            {
                ArbitrationDecision decision = group[i];

                if (taken)
                {
                    decision.Verdict = ArbitrationVerdict.Yielded;
                    decision.Why = "somebody else finished it first";
                    continue;
                }

                if (cancellation != null && cancellation.IsCancelled)
                {
                    decision.Verdict = ArbitrationVerdict.Cancelled;
                    decision.Why = "the batch was stopped before their turn";
                    continue;
                }

                // Asked again, now. Between ranking and here an earlier contender may have taken
                // the object, moved the other party, or been carried out of the zone.
                ActionContext context;
                string refusal;
                if (!environment.TryOpen(decision.Candidate.Intent, out context, out refusal))
                {
                    decision.Verdict = ArbitrationVerdict.Refused;
                    decision.Why = refusal;
                    continue;
                }

                TransientClaim claim = null;
                if (exclusive)
                {
                    claim = new TransientClaim(contestKey, decision.Actor);
                    claims.Add(claim);
                }

                try
                {
                    ActionAttempt attempt = ActionAttempt.Run(registry, decision.Candidate.Intent, context);
                    decision.Attempt = attempt;

                    if (!attempt.Resolved)
                    {
                        decision.Verdict = ArbitrationVerdict.Refused;
                        decision.Why = attempt.Refusal;
                        Release(claim, "the attempt was refused");
                        continue;
                    }

                    decision.Verdict = ArbitrationVerdict.Attempted;
                    decision.Why = attempt.Outcome.Resolution.ToString().ToLowerInvariant();

                    if (decision.Committed)
                    {
                        taken = exclusive;
                        Release(claim, "the attempt committed");
                    }
                    else
                    {
                        // Tried and changed nothing. The contested thing is exactly where it was,
                        // so the next contender is owed their turn - a failed lift is not the
                        // same event as somebody else's successful one.
                        Release(claim, "the attempt changed nothing");
                    }
                }
                catch (Exception error)
                {
                    // A verb that threw has not taken anything, and must not leave the contest
                    // held against everybody else on its way out.
                    decision.Verdict = ArbitrationVerdict.Faulted;
                    decision.Why = error.Message;
                    Release(claim, "the attempt threw");
                }
            }
        }

        private static void Release(TransientClaim claim, string because)
        {
            if (claim != null)
            {
                claim.Release(because);
            }
        }
    }
}
