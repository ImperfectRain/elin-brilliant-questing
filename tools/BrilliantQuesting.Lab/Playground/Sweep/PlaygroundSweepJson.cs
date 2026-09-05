using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// The same rows, as JSON, so a later run can be diffed against an earlier one without a human
    /// reading two tables.
    ///
    /// Hand-written and deliberately tiny: the repository targets netstandard2.0 in Core and has no
    /// serialization dependency, and taking one on for a diagnostic side channel would be a larger
    /// decision than this step is. What it emits is exactly what the console report prints - the
    /// same readings off the same finished rows - so the two cannot disagree about what happened.
    ///
    /// It is an output, never an input. Nothing reads this back; a regression check compares two
    /// files.
    /// </summary>
    public static class PlaygroundSweepJson
    {
        public static void Write(TextWriter output, IReadOnlyList<PlaygroundSweepResult> results, ulong seed)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.WriteLine("{");
            output.WriteLine("  \"seed\": " + seed + ",");
            output.WriteLine("  \"families\": [");

            for (int i = 0; i < results.Count; i++)
            {
                WriteFamily(output, results[i], i == results.Count - 1);
            }

            output.WriteLine("  ]");
            output.WriteLine("}");
        }

        private static void WriteFamily(TextWriter output, PlaygroundSweepResult result, bool last)
        {
            output.WriteLine("    {");
            output.WriteLine("      \"axis\": " + Text(result.Axis.Id) + ",");
            output.WriteLine("      \"rowsEvaluated\": " + result.Evaluated + ",");
            output.WriteLine("      \"unsupported\": " + result.Unsupported + ",");
            output.WriteLine("      \"distinctSemantics\": " + result.DistinctSemantics + ",");
            output.WriteLine("      \"distinctLines\": " + result.DistinctLines + ",");
            output.WriteLine("      \"unrealized\": " + result.Unrealized + ",");
            output.WriteLine("      \"noEffectInputs\": " + result.NoEffect.Count + ",");
            output.WriteLine("      \"weighedButUnchanged\": " + result.WeighedButUnchanged.Count + ",");
            output.WriteLine("      \"worldMutations\": " + result.Mutating + ",");
            WriteDiversity(output, result.Diversity);
            output.WriteLine("      \"violations\": [");
            for (int i = 0; i < result.Violations.Count; i++)
            {
                output.WriteLine("        {\"invariant\": " + Text(result.Violations[i].Invariant)
                    + ", \"detail\": " + Text(result.Violations[i].Detail) + "}"
                    + (i == result.Violations.Count - 1 ? string.Empty : ","));
            }

            output.WriteLine("      ],");
            output.WriteLine("      \"rows\": [");

            for (int i = 0; i < result.Rows.Count; i++)
            {
                WriteRow(output, result.Rows[i], i == result.Rows.Count - 1);
            }

            output.WriteLine("      ]");
            output.WriteLine("    }" + (last ? string.Empty : ","));
        }

        /// <summary>
        /// The same figures <see cref="PlaygroundSweepReport"/> prints under "dialogue diversity",
        /// with the formatted strings kept alongside the raw counts so a regression check can
        /// compare either without recomputing anything.
        /// </summary>
        private static void WriteDiversity(TextWriter output, DialogueDiversityReport diversity)
        {
            output.WriteLine("      \"diversity\": {");
            output.WriteLine("        \"samples\": " + diversity.Samples + ",");
            output.WriteLine("        \"realized\": " + diversity.Realized + ",");
            output.WriteLine("        \"unrealizedRate\": " + Text(diversity.UnrealizedSummary) + ",");
            output.WriteLine("        \"distinctCores\": " + diversity.DistinctCores + ",");
            output.WriteLine("        \"distinctCoreRate\": " + Text(diversity.CoreSummary) + ",");
            output.WriteLine("        \"distinctFragmentsUsed\": " + diversity.DistinctFragmentsUsed + ",");
            output.WriteLine("        \"fragmentsSharedAcrossProfiles\": " + diversity.FragmentsSharedAcrossProfiles + ",");
            output.WriteLine("        \"fragmentOverlap\": " + Text(diversity.OverlapSummary) + ",");
            output.WriteLine("        \"memorableFragmentUses\": " + diversity.MemorableFragmentUses + ",");
            output.WriteLine("        \"reusedMemorableFragments\": " + Texts(diversity.ReusedMemorableFragments) + ",");
            output.WriteLine("        \"reusedStructuralGroups\": " + Texts(diversity.ReusedStructuralGroups) + ",");
            output.WriteLine("        \"textualOverlap\": " + Text(diversity.TextualOverlapSummary) + ",");
            output.WriteLine("        \"lineLength\": " + Text(diversity.LineLengthSummary));
            output.WriteLine("      },");
        }

        private static void WriteRow(TextWriter output, PlaygroundSweepRow row, bool last)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("        {");
            sb.Append("\"label\": ").Append(Text(row.Label));
            sb.Append(", \"baseline\": ").Append(row.IsBaseline ? "true" : "false");
            sb.Append(", \"input\": ").Append(Texts(row.Changed));

            if (!row.Evaluated)
            {
                sb.Append(", \"unsupported\": ").Append(Text(row.Unsupported));
                sb.Append('}');
                output.WriteLine(sb + (last ? string.Empty : ","));
                return;
            }

            sb.Append(", \"readAt\": ").Append(row.ReadAt);
            sb.Append(", \"strategy\": ").Append(Text(row.Strategy));
            sb.Append(", \"depth\": ").Append(Text(row.Depth));
            sb.Append(", \"tactic\": ").Append(Text(row.Tactic));
            sb.Append(", \"act\": ").Append(Text(row.Act));
            sb.Append(", \"balance\": ").Append(Text(row.Balance));
            sb.Append(", \"weighing\": ").Append(Text(row.WeighingSignature));
            sb.Append(", \"decisive\": ").Append(Text(row.Decisive));
            sb.Append(", \"rulings\": ").Append(Text(row.Rulings));
            sb.Append(", \"callback\": ").Append(Text(row.Callback));
            sb.Append(", \"recurrence\": ").Append(Text(row.Recurrence));
            sb.Append(", \"veracity\": ").Append(Text(row.Veracity));
            sb.Append(", \"tone\": ").Append(Text(row.Tone));
            sb.Append(", \"idiolect\": ").Append(Text(row.Idiolect));
            sb.Append(", \"vocabulary\": ").Append(Text(row.Vocabulary));
            sb.Append(", \"forbidden\": ").Append(Text(row.Forbidden));
            sb.Append(", \"eligible\": ").Append(Text(row.EligibleBySlot));
            sb.Append(", \"line\": ").Append(Text(row.Line));
            sb.Append(", \"fragments\": ").Append(Text(row.Fragments));
            sb.Append(", \"meaning\": ").Append(Text(row.Meaning));
            sb.Append(", \"conversation\": ").Append(Text(row.Conversation));
            sb.Append(", \"world\": ").Append(Texts(row.WorldMoved));
            sb.Append('}');

            output.WriteLine(sb + (last ? string.Empty : ","));
        }

        private static string Texts(IReadOnlyList<string> values)
        {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(Text(values[i]));
            }

            return sb.Append(']').ToString();
        }

        /// <summary>A JSON string. Escapes what JSON requires and nothing else.</summary>
        private static string Text(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder sb = new StringBuilder("\"");
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
