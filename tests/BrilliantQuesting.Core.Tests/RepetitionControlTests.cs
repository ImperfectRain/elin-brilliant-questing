using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-078. Repetition control adds no new way of choosing a fragment - it only narrows the pool
    /// <see cref="DialogueFragment.Fits"/> and its neighbours already built, away from what a
    /// conversation has said too often. Every test here is really one of two claims: the narrowing
    /// never lets an invalid fragment through, and it never blocks the last valid one either.
    /// </summary>
    public class RepetitionControlTests
    {
        /// <summary>
        /// The step's own done-when: a 100-line synthetic conversation, mixing every kind of act
        /// this simulation already produces, contains no opener fragment more than twice.
        /// </summary>
        [Fact]
        public void OneHundredLineConversationRepeatsNoOpenerMoreThanTwice()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            DialogueExpressionHistory history = new DialogueExpressionHistory();
            Func<RealizationRequest>[] kinds =
            {
                scene.WitnessAnswers, scene.WitnessEvades, scene.ThiefRefuses, scene.ThiefDenies, scene.PlayerAsks
            };
            RealizationRequest[] requests = kinds.Select(kind => kind()).ToArray();

            Dictionary<string, int> openerUses = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int line = 0; line < 100; line++)
            {
                RealizationRequest request = requests[line % requests.Length];
                request.Rng = new DeterministicRng((ulong)(line + 1));
                request.History = history;

                RealizedLine realized = scene.Realizer.Realize(request);
                Assert.True(realized.Rendered, realized.Refusal);

                foreach (string fragmentId in realized.Fragments)
                {
                    if (scene.Realizer.Library.TryGet(fragmentId, out DialogueFragment fragment)
                        && fragment.Position == FragmentPosition.Opener)
                    {
                        openerUses.TryGetValue(fragmentId, out int count);
                        openerUses[fragmentId] = count + 1;
                    }
                }
            }

            foreach (KeyValuePair<string, int> use in openerUses)
            {
                Assert.True(use.Value <= 2, use.Key + " opened " + use.Value + " times in 100 lines");
            }
        }

        /// <summary>
        /// Semantic correctness wins. A speaker whose only refusal candidates all belong to the
        /// same overworked repetition group keeps refusing correctly - the same core, over and over
        /// - rather than ever falling through to an unrealized line or a wording that answers.
        /// </summary>
        [Fact]
        public void ExhaustingEveryValidCoreStillRendersCorrectlyRatherThanRefusing()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            DialogueExpressionHistory history = new DialogueExpressionHistory(cap: 1);
            request.History = history;

            HashSet<string> coresUsed = new HashSet<string>(StringComparer.Ordinal);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);

                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
                Assert.Contains(line.Core, scene.Realizer.Candidates(FragmentPosition.Core, request).Select(f => f.Id));
                coresUsed.Add(line.Core);
            }

            // The "refuse" group has three members; a cap of one exhausts all of them within the
            // first three lines, and every line after that is the reuse this step's degrade path
            // asks for - never a refusal, never a wording for a different act.
            Assert.True(coresUsed.Count >= 2, "repetition avoidance never diversified the refusal at all");
        }

        /// <summary>
        /// The mirror image: repetition avoidance is only ever a narrowing of what
        /// <see cref="DialogueRealizer.Candidates"/> already allowed. Every fragment a history-aware
        /// realization actually says is one the semantic-only view would have allowed too.
        /// </summary>
        [Fact]
        public void RepetitionAvoidanceNeverSaysAFragmentSemanticEligibilityWouldRefuse()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            request.History = new DialogueExpressionHistory();

            for (ulong seed = 1; seed <= 30; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);
                Assert.True(line.Rendered, line.Refusal);

                foreach (string fragmentId in line.Fragments)
                {
                    bool semanticallyEligible = false;
                    foreach (FragmentPosition position in Enum.GetValues(typeof(FragmentPosition)))
                    {
                        if (scene.Realizer.Candidates(position, request).Any(f => f.Id == fragmentId))
                        {
                            semanticallyEligible = true;
                            break;
                        }
                    }

                    Assert.True(semanticallyEligible, fragmentId + " was said without being semantically eligible");
                }
            }
        }

        /// <summary>
        /// A conversation is deterministic in its whole sequence, not merely in one call: replaying
        /// the same lines with the same seeds against a fresh history reproduces the same words.
        /// </summary>
        [Fact]
        public void TheSameConversationSequenceAlwaysSaysTheSameWords()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            Func<RealizationRequest>[] kinds =
            {
                scene.WitnessAnswers, scene.WitnessEvades, scene.ThiefRefuses, scene.ThiefDenies, scene.PlayerAsks
            };

            List<string> first = Speak(scene, kinds, new DialogueExpressionHistory());
            List<string> second = Speak(scene, kinds, new DialogueExpressionHistory());

            Assert.Equal(first, second);
        }

        /// <summary>
        /// Repetition history is bookkeeping about words already spoken, not a fact, a belief or a
        /// memory a character holds. Tracking it changes no saved state, the same way saying a line
        /// three ways never did before this step.
        /// </summary>
        [Fact]
        public void TrackingRepetitionWritesNothingToTheWorld()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();

            // Building the requests is what sets each speaker's honesty and mood, exactly as it
            // does in FragmentRealizationTests - that is world setup, not realization. The
            // snapshot is taken once every request already exists, so what is measured is
            // repetition-aware realization alone.
            Func<RealizationRequest>[] kinds =
            {
                scene.WitnessAnswers, scene.WitnessEvades, scene.ThiefRefuses, scene.ThiefDenies, scene.PlayerAsks
            };
            RealizationRequest[] requests = kinds.Select(kind => kind()).ToArray();
            string before = WorldStateSerializer.Save(scene.World);

            DialogueExpressionHistory history = new DialogueExpressionHistory();
            for (int line = 0; line < 40; line++)
            {
                RealizationRequest request = requests[line % requests.Length];
                request.Rng = new DeterministicRng((ulong)(line + 1));
                request.History = history;
                scene.Realizer.Realize(request);
            }

            Assert.Equal(before, WorldStateSerializer.Save(scene.World));
        }

        /// <summary>
        /// No history at all is the seam BQ-074 left: repetition control opts in per request and
        /// changes nothing about a caller that never mentions it.
        /// </summary>
        [Fact]
        public void ARequestWithNoHistoryBehavesExactlyAsBeforeThisStep()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            Assert.Null(request.History);

            for (ulong seed = 1; seed <= 10; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
            }
        }

        private static List<string> Speak(
            FragmentRealizationTests.Scene scene,
            Func<RealizationRequest>[] kinds,
            DialogueExpressionHistory history)
        {
            RealizationRequest[] requests = kinds.Select(kind => kind()).ToArray();
            List<string> spoken = new List<string>();
            for (int line = 0; line < 40; line++)
            {
                RealizationRequest request = requests[line % requests.Length];
                request.Rng = new DeterministicRng((ulong)(line + 1));
                request.History = history;
                spoken.Add(scene.Realizer.Realize(request).Text);
            }

            return spoken;
        }
    }
}
