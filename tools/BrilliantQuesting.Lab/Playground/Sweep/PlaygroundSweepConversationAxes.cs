using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// The same question, asked until the obvious ways of answering it run out.
    ///
    /// One run, one row per exchange, and the input that changes between the rows is the only one
    /// that can: what this conversation has already said. Ten exchanges is well past what the
    /// shipped library has fresh wordings for, which is the point - the interesting behaviour is
    /// what the realizer does when a slot has nothing new left.
    ///
    /// CD §21's degrade has two halves and they must not be confused. An optional slot has always
    /// been allowed to say nothing, so it simply goes quiet. The core has to be said, so when
    /// nothing in its pool is fresh it reuses the pool it already had rather than falling through
    /// to a refusal - and through all of it the meaning is the same meaning, because repetition
    /// narrows wording and can reach nothing else.
    /// </summary>
    internal sealed class RepetitionAxis : PlaygroundSweepAxis
    {
        private const int Exchanges = 10;

        public override string Id => "repetition";

        public override string Summary => "the same question, asked until the fresh wordings run out";

        public override string Question =>
            "What goes quiet, what starts repeating, and does the meaning ever move while it does?";

        public override string Held => "the state, the pair of people, the claim, the voice and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            PlaygroundRun run = PlaygroundRun.Begin(
                new PlaygroundOptions
                {
                    Seed = seed,
                    Preset = "neutral-witness",
                    Turns = Exchanges,
                    Undertaking = false,
                    Inputs = new[] { PlaygroundInputs.Tie(RelationKind.Acquaintance, 15) }
                },
                PlaygroundPresets.Default());

            List<PlaygroundSweepRow> rows = new List<PlaygroundSweepRow>();
            for (int turn = 1; turn <= run.Exchange.Turns.Count; turn++)
            {
                rows.Add(PlaygroundSweepRow.Of(
                    "exchange " + turn,
                    turn == 1,
                    turn == 1 ? new string[0] : new[] { "repetition history: " + (turn - 1) + " exchange(s) already said" },
                    run,
                    turn));
            }

            return rows;
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "every exchange means the same thing",
                rows => VoiceAxis.OneMeaning(rows)),

            new PlaygroundSweepInvariant(
                "the required core never went silent",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    if (row.Turn?.Reply == null)
                    {
                        return null;
                    }

                    if (row.Turn.Line == null || !row.Turn.Line.Rendered)
                    {
                        return "the exchange produced no line at all";
                    }

                    return row.Turn.Line.Core.Length == 0 ? "a line was spoken with no core fragment" : null;
                }))
        };

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            Dictionary<string, int> coreUses = new Dictionary<string, int>(StringComparer.Ordinal);
            output.WriteLine();
            output.WriteLine("what each exchange chose");
            output.WriteLine("  " + LabText.Column("turn", 7) + LabText.Column("core", 30) + "the rest of the line");

            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                RealizedLine line = row.Turn?.Line;
                if (line == null || !line.Rendered)
                {
                    continue;
                }

                coreUses.TryGetValue(line.Core, out int seen);
                coreUses[line.Core] = seen + 1;

                List<string> rest = new List<string>();
                for (int f = 0; f < line.Fragments.Count; f++)
                {
                    if (line.Fragments[f] != line.Core)
                    {
                        rest.Add(line.Fragments[f]);
                    }
                }

                output.WriteLine("  " + LabText.Column(row.ReadAt.ToString(), 7)
                    + LabText.Column(line.Core + (seen >= DialogueExpressionHistory.DefaultCap ? " (stale)" : string.Empty), 30)
                    + (rest.Count == 0 ? "the core alone" : string.Join(", ", rest)));
            }

            output.WriteLine();
            output.WriteLine("what the history holds at the end");
            PlaygroundRun run = result.Rows.Count == 0 ? null : result.Rows[0].Run;
            if (run == null)
            {
                return;
            }

            foreach (KeyValuePair<string, int> use in Ordered(coreUses))
            {
                output.WriteLine("  core " + LabText.Column(use.Key, 32) + "spoken " + use.Value
                    + " time(s), the history's own count " + run.Exchange.History.UsesOf(use.Key)
                    + (use.Value > DialogueExpressionHistory.DefaultCap
                        ? " - past the freshness cap, so the core reused an exhausted pool"
                        : string.Empty));
            }

            output.WriteLine();
            output.WriteLine("  A required slot with nothing fresh reuses what it had; an optional slot is simply");
            output.WriteLine("  skipped, which is why the tail of the table is shorter than its head. The meaning");
            output.WriteLine("  column above is identical throughout, which is the half that must not degrade.");
        }

        private static IEnumerable<KeyValuePair<string, int>> Ordered(Dictionary<string, int> uses)
        {
            List<KeyValuePair<string, int>> ordered = new List<KeyValuePair<string, int>>(uses);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return ordered;
        }
    }

    /// <summary>
    /// The whole conversation rather than one exchange: what it recognises, what it catches, what
    /// it promotes, and what it refuses to keep.
    ///
    /// The rows are four conversations rather than one state changed four ways, because the thing
    /// under test is BQ-083's transient state and each property needs a differently shaped
    /// exchange. What they share is that none of them is allowed to become a second history: the
    /// world figures beside each row are the whole of what survived, and for three of the four the
    /// honest answer is nothing.
    ///
    /// The reversal row is the one that needs days to pass between exchanges. A conversation that
    /// contradicted itself out of unchanged state would mean the decision was not a function of the
    /// state, so the only honest way to reach one is to let the world move between the questions.
    /// </summary>
    internal sealed class ConversationAxis : PlaygroundSweepAxis
    {
        public override string Id => "conversation";

        public override string Summary => "several exchanges over one matter, and what conversation state keeps";

        public override string Question =>
            "What does a conversation recognise about itself, and what of it outlives the conversation?";

        public override string Held => "the claim, the pair of people and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            PlaygroundRun asked = Play(seed, "neutral-witness", 3, undertaking: false, days: 0);
            PlaygroundRun reversed = Play(seed, "hostile-witness", 2, undertaking: false, days: 3);
            PlaygroundRun promised = Play(seed, "promise-exchange", 3, undertaking: true, days: 0);
            PlaygroundRun transient = Play(seed, "promise-exchange", 3, undertaking: true, days: 0, commit: false);

            return new[]
            {
                PlaygroundSweepRow.Of("asked once", true, new string[0], asked, 1),
                PlaygroundSweepRow.Of("asked again", false, new[] { "the same question, a second time" }, asked, 2),
                PlaygroundSweepRow.Of("asked a third time", false, new[] { "the same question, a third time" }, asked, 3),
                PlaygroundSweepRow.Of(
                    "asked once, guardedly", false,
                    new[] { "a hostile witness rather than a neutral one" }, reversed, 1),
                PlaygroundSweepRow.Of(
                    "and again, three days on", false,
                    new[] { "three days pass between the two exchanges, so affect decays and threads advance" },
                    reversed, 2, against: "asked once, guardedly"),
                PlaygroundSweepRow.NotSupported(
                    "a self-contradiction",
                    "ConversationState.Contradicts needs two assertions by one speaker with opposite stances, and\n"
                    + "no state this playground can write reaches one. Disclosure is a function of state, so the\n"
                    + "only thing that can move between two exchanges here is the world - and a search over ties,\n"
                    + "sentiment, honesty, secrecy, fear and the gap in days finds reversals (Deny to Refuse, Refuse\n"
                    + "to Evade) but never Deny to Answer, because only one of those two rungs asserts anything.\n"
                    + "The check runs on every assertion below and correctly stays silent; Core's own\n"
                    + "ConversationStateTests cover the positive case."),
                PlaygroundSweepRow.Of(
                    "a promise, promoted", false,
                    new[] { "the listener asks for something, and the caller promotes what is undertaken" },
                    promised, 3),
                PlaygroundSweepRow.Of(
                    "a promise, left transient", false,
                    new[] { "the same exchange, with nothing promoted" }, transient, 3,
                    against: "a promise, promoted")
            };
        }

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("what each conversation recognised about itself");

            HashSet<PlaygroundRun> seen = new HashSet<PlaygroundRun>();
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (row.Run == null || !seen.Add(row.Run))
                {
                    continue;
                }

                ConversationState conversation = row.Run.Exchange.Conversation;
                IReadOnlyList<DiscourseContradiction> contradictions =
                    conversation.AllContradictions(row.Run.Stage.World);

                output.WriteLine();
                output.WriteLine("  " + row.Run.Preset.Id + ", " + row.Run.Exchange.Turns.Count + " exchange(s)"
                    + (row.Run.DaysBetweenTurns > 0 ? ", " + row.Run.DaysBetweenTurns + " day(s) between them" : string.Empty));
                output.WriteLine("    acts noted:        " + conversation.Acts.Count);
                output.WriteLine("    questions:         " + conversation.Questions.Count
                    + ", of which unanswered " + conversation.UnansweredQuestions.Count);
                output.WriteLine("    repeated question: " + RepeatedIn(row.Run));
                output.WriteLine("    contradictions:    " + (contradictions.Count == 0
                    ? "none - every exchange was decided from the same state and reached the same stance"
                    : Describe(contradictions)));
                output.WriteLine("    lies told:         " + conversation.LiesTold.Count);
                output.WriteLine("    promises promoted: " + PromotedIn(row.Run));

                for (int t = 0; t < row.Run.Exchange.Turns.Count; t++)
                {
                    IReadOnlyList<string> notes = row.Run.Exchange.Turns[t].Notes;
                    for (int n = 0; n < notes.Count; n++)
                    {
                        output.WriteLine("    note (exchange " + (t + 1) + "): " + notes[n]);
                    }
                }
            }

            output.WriteLine();
            output.WriteLine("content coverage");
            List<string> unworded = new List<string>();
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (row.Unrealized && row.Turn?.Reply != null)
                {
                    unworded.Add(row.Label + ": " + row.Turn.Reply.Type + " - "
                        + (row.Turn.Line == null ? "nothing was realized" : row.Turn.Line.Refusal));
                }
            }

            if (unworded.Count == 0)
            {
                output.WriteLine("  every act composed here had at least one wording.");
            }

            for (int i = 0; i < unworded.Count; i++)
            {
                output.WriteLine("  DEFECT: " + unworded[i]);
            }

            output.WriteLine();
            output.WriteLine("  Conversation state is transient and stays transient: the acts, the expression");
            output.WriteLine("  history and the weirdness budget are let go when the exchange ends, and the only");
            output.WriteLine("  doorway from any of it into the save is ConversationState.Commit.");
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "asking the same question twice is recognised as the same question",
                rows =>
                {
                    PlaygroundSweepRow again = HonestyAxis.Find(rows, "asked again");
                    if (again?.Turn == null)
                    {
                        return new[] { "the second exchange was never played" };
                    }

                    return again.Turn.AlreadyAsked
                        ? new string[0]
                        : new[] { "the second identical question was not recognised as a repeat" };
                }),

            new PlaygroundSweepInvariant(
                "a promise becomes an obligation exactly once, and only when a caller says so",
                rows =>
                {
                    List<string> broken = new List<string>();
                    PlaygroundSweepRow promoted = HonestyAxis.Find(rows, "a promise, promoted");
                    PlaygroundSweepRow left = HonestyAxis.Find(rows, "a promise, left transient");

                    if (promoted?.Turn != null && promoted.Turn.Committed == null)
                    {
                        broken.Add("the promoted promise minted nothing");
                    }

                    if (left?.Turn != null && left.Turn.Committed != null)
                    {
                        broken.Add("a promise nobody promoted still reached the ledger");
                    }

                    if (promoted?.Run != null
                        && promoted.Run.Stage.World.Obligations.Records.Count
                        != CountBefore(promoted) + 1)
                    {
                        broken.Add("promoting one promise did not leave exactly one new obligation");
                    }

                    return broken;
                })
        };

        private static int CountBefore(PlaygroundSweepRow row) => row.Run.Exchange.Before.Obligations;

        private static string RepeatedIn(PlaygroundRun run)
        {
            for (int i = 0; i < run.Exchange.Turns.Count; i++)
            {
                if (run.Exchange.Turns[i].AlreadyAsked)
                {
                    return "recognised at exchange " + run.Exchange.Turns[i].Number;
                }
            }

            return "nothing was asked twice";
        }

        private static string PromotedIn(PlaygroundRun run)
        {
            int promoted = 0;
            for (int i = 0; i < run.Exchange.Turns.Count; i++)
            {
                if (run.Exchange.Turns[i].Committed != null)
                {
                    promoted++;
                }
            }

            return promoted == 0 ? "none" : promoted.ToString();
        }

        private static string Describe(IReadOnlyList<DiscourseContradiction> contradictions)
        {
            List<string> said = new List<string>();
            for (int i = 0; i < contradictions.Count; i++)
            {
                said.Add(contradictions[i].Because);
            }

            return string.Join("; ", said);
        }

        private static PlaygroundRun Play(
            ulong seed, string preset, int turns, bool undertaking, long days, bool commit = true)
        {
            return PlaygroundRun.Begin(
                new PlaygroundOptions
                {
                    Seed = seed,
                    Preset = preset,
                    Turns = turns,
                    Undertaking = undertaking,
                    DaysBetweenTurns = days,
                    Commit = commit
                },
                PlaygroundPresets.Default());
        }
    }
}
