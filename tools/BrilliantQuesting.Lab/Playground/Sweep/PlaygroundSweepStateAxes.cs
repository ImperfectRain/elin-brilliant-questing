using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One claim, one pair of people, one personality - and the tie between them walked from
    /// hostile to kin.
    ///
    /// The deliverable is not a threshold. Where exactly a refusal becomes a deflection is an
    /// implementation figure that should be free to move; what a reader needs is that there
    /// <em>is</em> a transition, which rung it lands on, and which of BQ-072's three ceilings was
    /// binding on either side of it. The last row adds the record half of standing - an obligation
    /// - to the warmest tie, because a graph edge and a debt are different evidence of the same
    /// thing and only one of them is a feeling.
    /// </summary>
    internal sealed class RelationshipAxis : PlaygroundSweepAxis
    {
        public override string Id => "relationship";

        public override string Summary => "the same knowledge, walked from hostile to kin";

        public override string Question =>
            "Where does standing change what comes out - and which ceiling was binding at each rung?";

        public override string Held =>
            "the claim, the speaker's firsthand belief, their personality, their emotions, the listener and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            return new[]
            {
                Row("as-written", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Acquaintance, 15)), baseline: true),
                Row("hostile", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Rival, -80))),
                Row("distant", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Acquaintance, -40))),
                Row("indifferent", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Acquaintance, 0))),
                Row("familiar", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Friend, 45))),
                Row("trusted", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Friend, 90))),
                Row("kin", seed, Preset, Inputs(PlaygroundInputs.Tie(RelationKind.Family, 90))),
                Row("trusted, and owed", seed, Preset, Inputs(
                    PlaygroundInputs.Tie(RelationKind.Friend, 90),
                    PlaygroundInputs.Owes(SocialObligationKind.Sanctuary, "took them in over the winter")),
                    against: "trusted")
            };
        }

        /// <summary>
        /// BQ-149, from the end the voice family cannot reach. Every row here is the same speaker
        /// with the same voice - the playground's default, which asks for nothing - and only the
        /// tie moves, so a line reserved for a particular way of speaking turning up at any rung
        /// would be a relationship handing somebody a temperament nobody gave them.
        /// </summary>
        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "a tie never hands the speaker a temperament",
                rows => VoiceAxis.NoUnaskedTemperament(rows))
        };

        private const string Preset = "neutral-witness";
    }

    /// <summary>
    /// The same tie, the same person, and the claim reached by different routes and held at
    /// different strengths.
    ///
    /// Run over the victim rather than the witness, and for a reason the knowledge graph itself
    /// imposes: <c>KnowledgeGraph.Teach</c> strengthens a belief somebody already holds rather than
    /// re-sourcing it, so provenance is only an input for somebody who starts with none. The victim
    /// knows the thing is gone and nothing more, which makes her the one person on this stage whose
    /// route into the claim a sweep may actually choose.
    ///
    /// The baseline is the interesting row. She has an identity the sandbox reads as trade, which
    /// BQ-145 turns into plausible knowledge - and plausible knowledge is not knowledge. A speaker
    /// who holds no belief must reach <c>NothingToDisclose</c> and compose no act at all, however
    /// plausible it would be for her to know.
    /// </summary>
    internal sealed class KnowledgeAxis : PlaygroundSweepAxis
    {
        public override string Id => "knowledge";

        public override string Summary => "one speaker, one tie, and the claim reached by different routes";

        public override string Question =>
            "What does provenance buy, what does confidence buy, and does plausibility ever become knowledge?";

        public override string Held =>
            "the claim, the speaker (the victim, who starts holding no belief), an acquaintance tie, and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            IReadOnlyList<PlaygroundInput> held = Inputs(PlaygroundInputs.Tie(RelationKind.Acquaintance, 25));

            return new[]
            {
                Row("no belief", seed, Preset, held, Victim, baseline: true),
                Row("faint inference", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Inference, 0.20)), Victim),
                Row("thin hearsay", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Hearsay, 0.45)), Victim),
                Row("confident hearsay", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Hearsay, 0.85)), Victim),
                Row("read it", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Document, 0.70)), Victim),
                Row("told by the thief", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Admission, 0.90)), Victim),
                Row("saw it, can prove it", seed, Preset,
                    With(held, PlaygroundInputs.Believes(KnowledgeSource.Witnessed, 1.00, canProve: true)), Victim),
                PlaygroundSweepRow.NotSupported(
                    "a distorted rival claim",
                    "a garbled version of the claim is a second Fact with DistortionOf set, and minting a fact is a "
                    + "write no input here makes. RumorDistortion is the production authority that mints one, and a "
                    + "sweep that hand-wrote a rival fact would be authoring the story rather than varying state.")
            };
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "wording never names a particular the speaker does not hold",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    DisclosureDecision decision = row.Turn?.Decision;
                    if (decision == null || row.Turn.Line == null || !row.Turn.Line.Rendered)
                    {
                        return null;
                    }

                    // The fragment that names what was taken declares depth detail or deeper, so a
                    // gist-deep answer reaching it would be wording adding a particular.
                    bool namesTheMatter = Contains(row.Turn.Line.Fragments, "core.answer.theft.matter");
                    return namesTheMatter && decision.Depth < DisclosureDepth.Detail
                        ? "a detail-only fragment was spoken at depth " + decision.Depth
                        : null;
                }))
        };

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private const string Preset = "neutral-witness";

        private static void Victim(PlaygroundOptions options)
        {
            options.Speaker = PlaygroundRoles.Victim;
        }
    }

    /// <summary>
    /// One set of pressures, and the character holding them changed a slope at a time.
    ///
    /// The pressures come from the <c>loyal-liar</c> preset and never move: heavy family loyalty to
    /// the person the claim is about, a listener who cannot simply be turned down, fear and stress.
    /// What moves is <c>PersonalityWeights.Honesty</c>, which is the slope, and - in the last two
    /// rows - <c>PersonalProhibition.NeverLiesDirectly</c>, which is not.
    ///
    /// That pairing is the point. If a large enough pressure could buy its way past a person's
    /// character then a line would be a redundant way of writing a high honesty, and the two rows
    /// that hold the line at the <em>liar's</em> honesty are what distinguish them: the slope says
    /// they would, and the line says they do not.
    ///
    /// The last row moves the truth of the claim instead. BQ-073 decides sincerity against what the
    /// speaker believes and reports world truth without consulting it, so moving it must move the
    /// report and leave the act, the pool and the words exactly where they were.
    /// </summary>
    internal sealed class HonestyAxis : PlaygroundSweepAxis
    {
        public override string Id => "honesty";

        public override string Summary => "one set of pressures, and the character under them changed a slope at a time";

        public override string Question =>
            "Where does a slope stop somebody lying, what does a line do that a slope cannot, "
            + "and is the wording layer ever told which of them is lying?";

        public override string Held =>
            "the loyalty, the fear, the tie to the thief, the tie to the listener, the claim and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            List<PlaygroundSweepRow> rows = new List<PlaygroundSweepRow>
            {
                Row("honesty 0.10", seed, Preset, Inputs(PlaygroundInputs.Personality("honesty", 0.10)), baseline: true)
            };

            double[] slopes = { 0.25, 0.40, 0.55, 0.70, 0.85, 0.95 };
            for (int i = 0; i < slopes.Length; i++)
            {
                rows.Add(Row(
                    "honesty " + slopes[i].ToString("0.00"), seed, Preset,
                    Inputs(PlaygroundInputs.Personality("honesty", slopes[i]))));
            }

            rows.Add(Row("0.10, will not lie", seed, Preset, Inputs(
                PlaygroundInputs.Personality("honesty", 0.10),
                PlaygroundInputs.Line(PersonalProhibition.NeverLiesDirectly, 0.95, breakable: false))));

            rows.Add(Row("0.85, will not lie", seed, Preset, Inputs(
                    PlaygroundInputs.Personality("honesty", 0.85),
                    PlaygroundInputs.Line(PersonalProhibition.NeverLiesDirectly, 0.95, breakable: false)),
                against: "honesty 0.85"));

            rows.Add(Row("0.10, and the claim is untrue", seed, Preset, Inputs(
                PlaygroundInputs.Personality("honesty", 0.10),
                PlaygroundInputs.Truth(TruthState.False))));

            return rows;
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "moving the truth of the claim moves the veracity report and nothing else",
                rows =>
                {
                    PlaygroundSweepRow believed = Find(rows, "honesty 0.10");
                    PlaygroundSweepRow untrue = Find(rows, "0.10, and the claim is untrue");
                    List<string> broken = new List<string>();
                    if (believed?.Turn == null || untrue?.Turn == null)
                    {
                        return broken;
                    }

                    if (believed.Meaning != untrue.Meaning)
                    {
                        broken.Add("the act changed: " + believed.Meaning + " vs " + untrue.Meaning);
                    }

                    if (believed.EligibleBySlot != untrue.EligibleBySlot)
                    {
                        broken.Add("the eligible pool changed: " + believed.EligibleBySlot
                                   + " vs " + untrue.EligibleBySlot);
                    }

                    if (believed.Line != untrue.Line)
                    {
                        broken.Add("the wording changed: " + believed.Line + " vs " + untrue.Line);
                    }

                    return broken;
                })
        };

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            PlaygroundSweepRow believed = Find(result.Rows, "honesty 0.10");
            PlaygroundSweepRow untrue = Find(result.Rows, "0.10, and the claim is untrue");
            if (believed?.Turn == null || untrue?.Turn == null)
            {
                return;
            }

            output.WriteLine();
            output.WriteLine("is the wording layer told who is lying?");
            output.WriteLine("  the two rows below hold identical state but for the truth of the claim itself.");
            output.WriteLine("  'reads' is the accuracy of what was put forward, so a denial inverts it:");
            output.WriteLine("  denying a claim the world holds false is an accurate thing to have said.");
            output.WriteLine("  veracity  " + believed.Veracity + "   |   " + untrue.Veracity);
            output.WriteLine("  act       " + believed.Meaning);
            output.WriteLine("            " + untrue.Meaning);
            output.WriteLine("  pool      " + believed.EligibleBySlot);
            output.WriteLine("            " + untrue.EligibleBySlot);
            output.WriteLine("  line      " + believed.Line);
            output.WriteLine("            " + untrue.Line);
            output.WriteLine();
            output.WriteLine("  A sincere denial has no production path to compare against: Disclosure composes");
            output.WriteLine("  Deny only from the Falsify tactic, and Falsify requires a belief to contradict.");
            output.WriteLine("  So 'the honest denier' is a semantic state this model does not reach, and the");
            output.WriteLine("  strongest available proof that wording is veracity-blind is the pair above.");
        }

        internal static PlaygroundSweepRow Find(IReadOnlyList<PlaygroundSweepRow> rows, string label)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Label == label)
                {
                    return rows[i];
                }
            }

            return null;
        }

        private const string Preset = "loyal-liar";
    }
}
