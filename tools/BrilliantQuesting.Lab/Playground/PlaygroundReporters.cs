using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// One section of a playground report.
    ///
    /// Reporters read a finished <see cref="PlaygroundRun"/> and write; none of them may run a
    /// system, take a decision or touch the world, which is what makes the report replayable and
    /// what stops "print it nicely" from quietly becoming a second place the simulation happens.
    /// Adding a view is a subclass plus one line in <see cref="PlaygroundReporters.Default"/>.
    /// </summary>
    public abstract class PlaygroundReporter
    {
        public abstract string Id { get; }

        public abstract string Summary { get; }

        public abstract void Write(TextWriter output, PlaygroundRun run);
    }

    /// <summary>The ordered set of sections a playground run prints.</summary>
    public sealed class PlaygroundReporters
    {
        private readonly List<PlaygroundReporter> _reporters;

        public PlaygroundReporters(IEnumerable<PlaygroundReporter> reporters)
        {
            _reporters = new List<PlaygroundReporter>(reporters ?? throw new ArgumentNullException(nameof(reporters)));
        }

        public static PlaygroundReporters Default()
        {
            return new PlaygroundReporters(new PlaygroundReporter[]
            {
                new SituationReporter(),
                new AuthoritativeStateReporter(),
                new CastingReporter(),
                new ExchangeReporter(),
                new ConversationReporter()
            });
        }

        public IReadOnlyList<PlaygroundReporter> All => _reporters;

        public void Write(TextWriter output, PlaygroundRun run)
        {
            for (int i = 0; i < _reporters.Count; i++)
            {
                _reporters[i].Write(output, run);
            }
        }
    }

    /// <summary>Who is talking, about what, and which knobs the caller turned.</summary>
    internal sealed class SituationReporter : PlaygroundReporter
    {
        public override string Id => "situation";

        public override string Summary => "the preset, the seed, the two people and the claim at issue";

        public override void Write(TextWriter output, PlaygroundRun run)
        {
            PlaygroundStage stage = run.Stage;
            LabText.Header(output, "conversation playground");

            output.WriteLine(Field("preset") + run.Preset.Id + " - " + run.Preset.Summary);
            output.WriteLine(Field("seed") + stage.Seed);
            output.WriteLine(Field("speaker") + stage.Describe(run.Speaker)
                + Occupation(stage.Npc(run.Speaker)));
            output.WriteLine(Field("listener") + stage.Describe(run.Listener)
                + Occupation(stage.Npc(run.Listener)));
            output.WriteLine(Field("subject") + PlaygroundText.Claim(stage, stage.SubjectFactId));
            output.WriteLine(Field("voice") + run.VoiceName + " -> "
                + PlaygroundVoices.Describe(run.Voice) + "   (laboratory-authored: nothing in Core assigns one)");

            output.WriteLine(Field("overrides") + (run.Overrides.Count == 0 ? "none" : run.Overrides[0]));
            for (int i = 1; i < run.Overrides.Count; i++)
            {
                output.WriteLine(Field(string.Empty) + run.Overrides[i]);
            }
        }

        private static string Occupation(NarrativeNpc npc)
        {
            return npc == null || string.IsNullOrEmpty(npc.Occupation) ? string.Empty : ", " + npc.Occupation;
        }

        private static string Field(string name) => LabText.Column(name.Length == 0 ? string.Empty : name + ":", 12);
    }

    /// <summary>
    /// The state the answer has to come out of.
    ///
    /// The speaker's own sheet is <see cref="NarrativeInspector.DescribeCharacter"/> verbatim -
    /// there is no reason for the laboratory to hold a second opinion about how to print a
    /// character - and what is added around it is only the part that is about the <em>pair</em>:
    /// the ties each way, what stands between them in the ledger, and what the speaker holds about
    /// the claim being asked after.
    /// </summary>
    internal sealed class AuthoritativeStateReporter : PlaygroundReporter
    {
        public override string Id => "state";

        public override string Summary => "the beliefs, ties, pressures and lines the decision is taken from";

        public override void Write(TextWriter output, PlaygroundRun run)
        {
            PlaygroundStage stage = run.Stage;
            LabText.Header(output, "authoritative state");

            output.WriteLine("what the speaker holds about the claim");
            output.WriteLine("  " + PlaygroundText.Belief(stage, run.Speaker, stage.SubjectFactId));
            output.WriteLine("  listener: " + PlaygroundText.Belief(stage, run.Listener, stage.SubjectFactId));

            output.WriteLine();
            output.WriteLine("between these two");
            output.WriteLine("  " + PlaygroundText.Tie(stage, run.Speaker, run.Listener));
            output.WriteLine("  " + PlaygroundText.Tie(stage, run.Listener, run.Speaker));
            output.WriteLine("  " + PlaygroundText.Tie(stage, run.Speaker, stage.Subject?.Subject ?? EntityId.None));
            WriteObligations(output, run);

            output.WriteLine();
            output.WriteLine("lived context reaching wording");
            IdentityAffordances identity = IdentityAffordances.Of(stage.Npc(run.Speaker), stage.Vanilla);
            output.WriteLine("  vocabulary: " + PlaygroundText.Join(
                OccupationalVocabulary.RequestedVocabulary(identity), "none - no facet implies a domain"));
            output.WriteLine("  tone:       " + PlaygroundVoices.Describe(run.Voice));

            output.WriteLine();
            output.WriteLine("old business this speaker may recall about the listener");
            output.Write(Indent(NarrativeInspector.DescribeCallbacks(
                stage.World, stage.Vanilla, run.Speaker, stage.Now,
                new CallbackSelection { About = run.Listener })));

            output.WriteLine();
            output.WriteLine("the speaker's own sheet");
            output.Write(Indent(NarrativeInspector.DescribeCharacter(stage.World, stage.Vanilla, run.Speaker)));
        }

        private static void WriteObligations(TextWriter output, PlaygroundRun run)
        {
            IReadOnlyList<SocialObligation> records = run.Stage.World.Obligations.Records;
            bool any = false;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                bool between = (obligation.Debtor == run.Speaker && obligation.Creditor == run.Listener)
                    || (obligation.Debtor == run.Listener && obligation.Creditor == run.Speaker);
                if (!between)
                {
                    continue;
                }

                any = true;
                output.WriteLine("  " + run.Stage.NameOf(obligation.Debtor) + " owes "
                    + run.Stage.NameOf(obligation.Creditor) + ": " + obligation.Kind + " (" + obligation.Status
                    + (obligation.Purpose.Length == 0 ? ")" : ", " + obligation.Purpose + ")"));
            }

            if (!any)
            {
                output.WriteLine("  nothing stands between them in the obligation ledger");
            }
        }

        private static string Indent(string block) => PlaygroundText.Indent(block, "  ");
    }

    /// <summary>
    /// Which scenes this situation supports and who they cast.
    ///
    /// <see cref="NarrativeInspector.DescribeCasting"/> already accounts for the whole score - what
    /// qualified each person and why these people rather than the others who also qualified - so
    /// this reporter only chooses which opportunities to print.
    /// </summary>
    internal sealed class CastingReporter : PlaygroundReporter
    {
        public override string Id => "casting";

        public override string Summary => "storylet eligibility and chemistry over the same claim";

        public override void Write(TextWriter output, PlaygroundRun run)
        {
            PlaygroundStage stage = run.Stage;
            LabText.Header(output, "scenes this situation supports");

            IReadOnlyList<StoryletOpportunity> opportunities = stage.Storylets.Find(
                new StoryletCastingContext(stage.World, stage.Vanilla, stage.Situation.Thread, stage.SubjectFactId));

            if (opportunities.Count == 0)
            {
                output.WriteLine("no storylet in the bundle applies to this claim.");
                return;
            }

            for (int i = 0; i < opportunities.Count; i++)
            {
                output.Write(NarrativeInspector.DescribeCasting(opportunities[i]));
                output.WriteLine();
            }
        }
    }

    /// <summary>
    /// The pipeline, one exchange at a time: what was put to the speaker, what they decided and
    /// why, what old business was cleared or withheld, what constrained the wording, what came out,
    /// and what any of it changed.
    /// </summary>
    internal sealed class ExchangeReporter : PlaygroundReporter
    {
        public override string Id => "turns";

        public override string Summary => "decision, reasons, disclosure, callback, wording and consequences per exchange";

        public override void Write(TextWriter output, PlaygroundRun run)
        {
            IReadOnlyList<PlaygroundTurn> turns = run.Exchange.Turns;
            for (int i = 0; i < turns.Count; i++)
            {
                WriteTurn(output, run, turns[i]);
            }
        }

        private static void WriteTurn(TextWriter output, PlaygroundRun run, PlaygroundTurn turn)
        {
            PlaygroundStage stage = run.Stage;
            LabText.Header(output, "exchange " + turn.Number + " - " + turn.Kind);

            if (turn.Prompt == null)
            {
                WriteNotes(output, turn);
                return;
            }

            output.WriteLine("put to the speaker");
            output.Write(PlaygroundText.Indent(
                NarrativeInspector.DescribeSpeechAct(stage.World, turn.Prompt), "  "));
            output.WriteLine("  already asked in this conversation: " + (turn.AlreadyAsked ? "yes" : "no"));

            if (turn.Reaction != null)
            {
                output.WriteLine();
                output.WriteLine("how the speaker reads the matter");
                output.Write(PlaygroundText.Indent(
                    NarrativeInspector.DescribeReaction(stage.World, turn.Reaction), "  "));
            }

            if (turn.Decision != null)
            {
                output.WriteLine();
                output.WriteLine("semantic decision, and why");
                output.Write(PlaygroundText.Indent(
                    NarrativeInspector.DescribeDisclosure(stage.World, turn.Decision), "  "));
            }

            output.WriteLine();
            output.WriteLine("what the speaker said, semantically");
            if (turn.Reply == null)
            {
                output.WriteLine("  nothing: the decision amounted to no act");
            }
            else
            {
                output.Write(PlaygroundText.Indent(
                    NarrativeInspector.DescribeSpeechAct(stage.World, turn.Reply), "  "));
                output.Write(PlaygroundText.Indent(
                    NarrativeInspector.DescribeVeracity(stage.World, turn.Reply), "  "));
            }

            WriteCallback(output, run, turn);
            WriteWording(output, run, turn);
            WriteEffects(output, run, turn);
            WriteNotes(output, turn);
        }

        private static void WriteCallback(TextWriter output, PlaygroundRun run, PlaygroundTurn turn)
        {
            output.WriteLine();
            output.WriteLine("old business");
            if (turn.Callback != null)
            {
                output.WriteLine("  cleared:   " + PlaygroundText.Permit(run.Stage, turn.Callback));
            }
            else if (turn.WithheldCallback != null)
            {
                output.WriteLine("  withheld:  " + PlaygroundText.Permit(run.Stage, turn.WithheldCallback));
                output.WriteLine("             nothing about it reached wording, which is the gate closing");
            }
            else
            {
                output.WriteLine("  none:      no material old enough that this speaker has a route to");
            }

            output.WriteLine("  recurrence: " + (turn.Recurrence == null
                ? Recurrence(run, turn)
                : PlaygroundText.Permit(run.Stage, turn.Recurrence)));
        }

        /// <summary>
        /// Why nothing earned a retelling, asked of BQ-082's own predicates rather than guessed.
        ///
        /// The narrow Lab adapter this report needs and production does not have: the inspector can
        /// say a recurrence is available, and <c>CallbackRecurrence.Best</c> returns null when one
        /// is not, but "memorable, and it happened right here" and "not the kind of history that
        /// recurs" are different answers and a null cannot tell them apart. Both halves are read
        /// off the public predicates; nothing is decided here.
        /// </summary>
        private static string Recurrence(PlaygroundRun run, PlaygroundTurn turn)
        {
            CallbackHook hook = turn.Callback?.Hook ?? turn.WithheldCallback?.Hook;
            if (hook == null)
            {
                return "no material to weigh";
            }

            ContinuityContext context = new ContinuityContext(
                run.Stage.Situation.Thread?.Id ?? EntityId.None, run.Stage.Zone);

            if (!CallbackRecurrence.IsMemorable(hook))
            {
                return "their material is reusable but not the kind that recurs (" + hook.PrimaryKind + ")";
            }

            return CallbackRecurrence.IsUnrelatedContext(hook, context)
                ? "memorable and unrelated to here, but this speaker would not raise it with this listener"
                : "memorable, but it happened in this very thread or place - a recurrence has to come from elsewhere";
        }

        private static void WriteWording(TextWriter output, PlaygroundRun run, PlaygroundTurn turn)
        {
            output.WriteLine();
            output.WriteLine("expression constraints");
            if (turn.Request == null)
            {
                output.WriteLine("  no request was made, so nothing was worded");
                return;
            }

            output.WriteLine("  tone:       " + PlaygroundText.Join(turn.Request.Tone, "none requested"));
            output.WriteLine("  vocabulary: " + PlaygroundText.Join(turn.Request.Vocabulary, "none requested"));
            output.WriteLine("  forbidden:  " + PlaygroundText.Join(turn.Request.Forbidden, "nothing ruled out"));
            output.WriteLine("  weirdness:  ceiling " + run.Exchange.Budget.Ceiling
                + ", spent " + run.Exchange.Budget.Spent
                + ", premise " + (run.Exchange.Budget.AdmittedPremise ?? "none"));
            output.WriteLine("  repetition: history carried from the earlier exchanges of this conversation");

            output.WriteLine();
            output.WriteLine("realization");
            if (turn.Line == null)
            {
                output.WriteLine("  nothing was realized");
                return;
            }

            if (!turn.Line.Rendered)
            {
                output.WriteLine("  unrealized: " + turn.Line.Refusal);
            }
            else
            {
                output.WriteLine("  \"" + turn.Line.Text + "\"");
                output.WriteLine("  core:      " + turn.Line.Core);
                output.WriteLine("  fragments: " + PlaygroundText.Join(turn.Line.Fragments, "none"));
            }

            output.WriteLine("  meaning:   " + turn.Line.Meaning);
            output.WriteLine("             (the act's own signature, unchanged by wording)");
        }

        private static void WriteEffects(TextWriter output, PlaygroundRun run, PlaygroundTurn turn)
        {
            output.WriteLine();
            output.WriteLine("what this exchange changed");
            output.WriteLine("  conversation: " + turn.ActsNoted + " act(s) noted, "
                + turn.Unanswered + " question(s) still unanswered");

            if (turn.Contradiction != null)
            {
                output.WriteLine("  contradiction: " + turn.Contradiction.Value.Because);
            }

            if (turn.RecordedDeception != null)
            {
                output.WriteLine("  durable: a deception was recorded as event " + turn.RecordedDeception.Id);
            }

            if (turn.Committed != null)
            {
                output.WriteLine("  durable: the promise was promoted, event " + turn.Committed.Id
                    + " and one obligation");
            }

            output.WriteLine("  ledger: " + turn.LedgerBefore + " -> " + turn.LedgerAfter
                + " event(s), " + turn.ObligationsBefore + " -> " + turn.ObligationsAfter + " obligation(s)"
                + (turn.WroteToTheLedger ? string.Empty : "   (nothing durable)"));
        }

        private static void WriteNotes(TextWriter output, PlaygroundTurn turn)
        {
            for (int i = 0; i < turn.Notes.Count; i++)
            {
                output.WriteLine("  note: " + turn.Notes[i]);
            }
        }
    }

    /// <summary>The whole exchange as conversation state holds it, once everything has been said.</summary>
    internal sealed class ConversationReporter : PlaygroundReporter
    {
        public override string Id => "conversation";

        public override string Summary => "the transcript conversation state kept, and what survived it";

        public override void Write(TextWriter output, PlaygroundRun run)
        {
            LabText.Header(output, "the conversation, as it is remembered");
            output.Write(NarrativeInspector.DescribeConversation(run.Stage.World, run.Exchange.Conversation));

            output.WriteLine();
            output.WriteLine("what outlives it");
            IReadOnlyList<SocialObligation> obligations = run.Stage.World.Obligations.Records;
            if (obligations.Count == 0)
            {
                output.WriteLine("  no obligations in the ledger at all");
            }

            for (int i = 0; i < obligations.Count; i++)
            {
                output.WriteLine("  " + run.Stage.NameOf(obligations[i].Debtor) + " owes "
                    + run.Stage.NameOf(obligations[i].Creditor) + ": " + obligations[i].Kind
                    + " (" + obligations[i].Status + ")");
            }

            output.WriteLine("  transient and discarded: the acts above, the expression history and the weirdness budget");
        }
    }

    /// <summary>Shared formatting. No system runs from in here.</summary>
    internal static class PlaygroundText
    {
        public static string Join(IReadOnlyList<string> values, string empty)
        {
            if (values == null || values.Count == 0)
            {
                return empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(values[i]);
            }

            return sb.ToString();
        }

        public static string Indent(string block, string prefix)
        {
            if (string.IsNullOrEmpty(block))
            {
                return block ?? string.Empty;
            }

            string[] lines = block.TrimEnd('\n').Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(prefix).Append(lines[i]).Append('\n');
            }

            return sb.ToString();
        }

        public static string Claim(PlaygroundStage stage, EntityId factId)
        {
            Fact fact = stage.World.Knowledge.GetFact(factId);
            if (fact == null)
            {
                return factId.Value + " (no such claim)";
            }

            return "[" + fact.Id.Value + "] " + stage.NameOf(fact.Subject) + " " + fact.Predicate
                + " " + (string.IsNullOrEmpty(fact.Value) ? fact.Object.Value : fact.Value)
                + "   (truth " + fact.Truth + ", secrecy " + fact.Secrecy + ")";
        }

        public static string Belief(PlaygroundStage stage, EntityId who, EntityId factId)
        {
            if (!stage.World.Knowledge.TryGetBelief(who, factId, out KnowledgeRecord belief))
            {
                return stage.Describe(who) + ": holds no belief about it";
            }

            return stage.Describe(who) + ": " + belief.Source + ", confidence "
                + belief.Confidence.ToString("0.00")
                + (belief.CanProve ? ", can prove" : ", cannot prove");
        }

        public static string Tie(PlaygroundStage stage, EntityId from, EntityId to)
        {
            if (from.IsNone || to.IsNone || from == to)
            {
                return "no pair to describe";
            }

            RelationshipEdge edge = stage.World.Relationships.Find(from, to);
            string pair = stage.NameOf(from) + " -> " + stage.NameOf(to) + ": ";
            return edge == null
                ? pair + "no tie at all"
                : pair + edge.Kind + " at sentiment " + edge.Sentiment;
        }

        public static string Permit(PlaygroundStage stage, CallbackPermit permit)
        {
            if (permit == null || permit.Hook == null)
            {
                return "none";
            }

            CallbackHook hook = permit.Hook;
            string material = hook.EventType + " " + hook.AgeInDays + "d ago, " + hook.PrimaryKind
                + " via " + hook.Route + ", other party " + stage.NameOf(hook.Counterpart)
                + " (" + hook.Party + ")";

            if (permit.Allowed)
            {
                return material + " - allowed: " + permit.Because;
            }

            return material + " - withheld: " + permit.Because
                + (permit.Withheld.IsNone ? string.Empty : ", claim " + permit.Withheld.Value
                    + " they would " + permit.Strategy);
        }
    }
}
