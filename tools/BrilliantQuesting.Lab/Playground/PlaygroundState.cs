using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// The small set of authoritative writes the presets share.
    ///
    /// Every one of them is a plain call into the store that owns the state - the relationship
    /// graph, the knowledge graph, the obligation ledger, the character's own profiles - so a
    /// preset is a list of facts about the world rather than a private format this class has to
    /// interpret. There is deliberately no builder for a decision, an act, a permit or a line:
    /// those are outcomes, and a preset that could set one would be authoring the answer the
    /// playground exists to derive.
    /// </summary>
    public static class PlaygroundState
    {
        /// <summary>A directed tie, replacing whatever was there. Both directions when asked.</summary>
        public static void Tie(
            PlaygroundStage stage,
            EntityId from,
            EntityId to,
            RelationKind kind,
            int sentiment,
            bool mutual = false)
        {
            if (mutual)
            {
                stage.World.Relationships.ConnectMutual(from, to, kind, sentiment);
                return;
            }

            stage.World.Relationships.Connect(from, to, kind, sentiment);
        }

        /// <summary>
        /// Present affect at the current time. Set rather than accumulated, because a preset is
        /// describing the state a scene starts in and not an event that just moved somebody.
        /// </summary>
        public static void Feels(PlaygroundStage stage, EntityId who, EmotionalState emotion, double intensity)
        {
            NarrativeNpc npc = stage.Npc(who);
            if (npc == null)
            {
                return;
            }

            npc.Emotions.LastUpdatedAt = stage.Now;
            npc.Emotions.Set(emotion, intensity);
        }

        /// <summary>
        /// Teaches a belief by a named route.
        ///
        /// <see cref="KnowledgeGraph.Teach"/>'s own rule stands and is not worked around: a belief
        /// somebody already holds is strengthened rather than re-sourced, so this establishes a
        /// route only for somebody who held none. A preset that wants a different route asks it of
        /// somebody who has not already learned the claim - the victim, who knows only that the
        /// thing is gone - and <see cref="PlaygroundReport"/> prints the route that actually
        /// resulted rather than the one that was asked for.
        /// </summary>
        public static KnowledgeRecord Believes(
            PlaygroundStage stage,
            EntityId who,
            EntityId factId,
            KnowledgeSource source,
            double confidence,
            bool canProve = false,
            EntityId toldBy = default)
        {
            return stage.World.Knowledge.Teach(who, factId, source, confidence, stage.Now, canProve, toldBy);
        }

        /// <summary>
        /// An obligation standing between two people - the record half of a relationship, which
        /// <c>Disclosure</c> reads as standing and BQ-077 reads as a line.
        /// </summary>
        public static SocialObligation Owes(
            PlaygroundStage stage,
            SocialObligationKind kind,
            EntityId debtor,
            EntityId creditor,
            EntityId subject,
            string purpose)
        {
            return stage.World.Obligations.Add(new SocialObligation(
                stage.World.NewId("obl"), kind, debtor, creditor, subject, purpose, stage.Now, EntityId.None));
        }

        /// <summary>One of BQ-077's personal lines, held at a firmness.</summary>
        public static void Line(
            PlaygroundStage stage,
            EntityId who,
            PersonalProhibition kind,
            double firmness,
            bool breakable = true)
        {
            stage.Npc(who)?.NegativeSpace.Declare(kind, firmness, breakable);
        }

        /// <summary>
        /// Runs a production action to completion against a scripted resolver, so a preset that
        /// wants history gets the events the action layer actually records rather than events this
        /// class invented. The resolver is restored afterwards, so nothing outside the preset
        /// inherits scripted dice.
        /// </summary>
        public static void Act(PlaygroundStage stage, string actionId, EntityId target, CheckOutcome outcome)
        {
            ICheckResolver previous = stage.Lab.Checks;
            stage.Lab.Checks = new PlaygroundFixedChecks(outcome);
            try
            {
                stage.Lab.Perform(actionId, target);
            }
            finally
            {
                stage.Lab.Checks = previous;
            }
        }

        /// <summary>Moves the clock and lets the live threads catch up, exactly as the laboratory does.</summary>
        public static void Wait(PlaygroundStage stage, long days) => stage.Lab.AdvanceDays(days);
    }

    /// <summary>
    /// A resolver that returns one outcome, so a preset that needs a particular history gets it
    /// without the run depending on dice. Only ever used while a preset is building state: no
    /// exchange resolves a check.
    /// </summary>
    internal sealed class PlaygroundFixedChecks : ICheckResolver
    {
        private static readonly CheckTerm[] NoTerms = new CheckTerm[0];

        private readonly CheckOutcome _outcome;

        public PlaygroundFixedChecks(CheckOutcome outcome)
        {
            _outcome = outcome;
        }

        public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
        {
            int roll = _outcome == CheckOutcome.CriticalPass ? 20
                : _outcome == CheckOutcome.Pass ? 15
                : _outcome == CheckOutcome.CriticalFail ? 1 : 5;

            return new CheckResult(
                request.Profile.Id, request.Profile.BaseDifficulty, NoTerms, 10, roll, _outcome);
        }
    }
}
