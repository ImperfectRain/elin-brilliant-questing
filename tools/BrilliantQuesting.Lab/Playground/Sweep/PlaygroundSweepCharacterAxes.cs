using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// Each of BQ-077's lines, run three ways: without it, with it holding, and with it thin enough
    /// for the pressure already in the room to carry it.
    ///
    /// Two situations, because a line only produces a ruling where it could have cost something and
    /// the two lines cost something in different rooms. The lying pair sits on <c>loyal-liar</c>'s
    /// pressures, where the weighing reaches a falsehood; the kinship pair sits on a friendly
    /// speaker who would otherwise say it and happens to be the thief's sister.
    ///
    /// The last two rows are the control the vocabulary is sized by: two lines that bear on moves
    /// this conversation never selects. A prohibition with nothing to forbid should produce no
    /// ruling and change nothing, and a model in which every declared line moved something would be
    /// a model in which prohibitions had quietly become personality weights.
    /// </summary>
    internal sealed class NegativeSpaceAxis : PlaygroundSweepAxis
    {
        public override string Id => "negative-space";

        public override string Summary => "each personal line, absent, holding, and under enough pressure to break";

        public override string Question =>
            "Which move does a line take off the table, what happens instead, and does the score move with it?";

        public override string Held =>
            "within each pair: the pressures, the ties, the belief, the personality, the listener and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            // The kinship situation, built from inputs rather than borrowed from kin-line, so that
            // the control row genuinely is the same state minus the line.
            IReadOnlyList<PlaygroundInput> kin = Inputs(
                PlaygroundInputs.Tie(RelationKind.Friend, 80),
                PlaygroundInputs.TieTo(PlaygroundRoles.Thief, RelationKind.Family, 70, mutual: true),
                PlaygroundInputs.Personality("honesty", 0.80),
                PlaygroundInputs.Personality("trust", 0.80));

            IReadOnlyList<PlaygroundInput> liar = Inputs(PlaygroundInputs.Personality("honesty", 0.10));

            return new[]
            {
                Row("lying: no line", seed, Liar, liar, baseline: true),
                Row("lying: line holds", seed, Liar,
                    With(liar, PlaygroundInputs.Line(PersonalProhibition.NeverLiesDirectly, 0.95, breakable: false)),
                    against: "lying: no line"),
                Row("lying: thin line", seed, Liar,
                    With(liar, PlaygroundInputs.Line(PersonalProhibition.NeverLiesDirectly, 0.05, breakable: true)),
                    against: "lying: no line"),

                Row("kin: no line", seed, Neutral, kin, against: "kin: no line"),
                Row("kin: line holds", seed, Neutral,
                    With(kin, PlaygroundInputs.Line(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.90, breakable: false)),
                    against: "kin: no line"),
                Row("kin: thin line", seed, Neutral,
                    With(kin, PlaygroundInputs.Line(PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.05, breakable: true)),
                    against: "kin: no line"),

                Row("a line about begging", seed, Liar,
                    With(liar, PlaygroundInputs.Line(PersonalProhibition.NeverBegs, 0.95, breakable: false)),
                    against: "lying: no line"),
                Row("a line about authority", seed, Liar,
                    With(liar, PlaygroundInputs.Line(PersonalProhibition.NeverInvolvesAuthority, 0.95, breakable: false)),
                    against: "lying: no line")
            };
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "a line removes a move without rewriting the balance it was weighed on",
                rows =>
                {
                    List<string> broken = new List<string>();
                    CompareBalance(rows, "lying: no line", "lying: line holds", broken);
                    CompareBalance(rows, "kin: no line", "kin: line holds", broken);
                    return broken;
                }),

            new PlaygroundSweepInvariant(
                "a line that bears on no move in this conversation produces no ruling",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    if (!row.Label.StartsWith("a line about", System.StringComparison.Ordinal))
                    {
                        return null;
                    }

                    return row.Rulings == "no line bore on this"
                        ? null
                        : "a line that forbids no move here produced a ruling: " + row.Rulings;
                }))
        };

        private static void CompareBalance(
            IReadOnlyList<PlaygroundSweepRow> rows, string control, string ruled, List<string> broken)
        {
            PlaygroundSweepRow before = HonestyAxis.Find(rows, control);
            PlaygroundSweepRow after = HonestyAxis.Find(rows, ruled);
            if (before?.Turn?.Decision == null || after?.Turn?.Decision == null)
            {
                return;
            }

            if (before.Turn.Decision.Balance != after.Turn.Decision.Balance)
            {
                broken.Add(control + " weighed " + before.Turn.Decision.Balance.ToString("0.0000")
                           + " and " + ruled + " weighed " + after.Turn.Decision.Balance.ToString("0.0000")
                           + " - a prohibition is not a pressure and must not move the sum");
            }
        }

        private const string Liar = "loyal-liar";
        private const string Neutral = "neutral-witness";
    }

    /// <summary>
    /// One emotion at a time, at the same strength, over a state that is otherwise untouched.
    ///
    /// A row that changes nothing is the useful half of this family. BQ-071 weighs fear, stress and
    /// suspicion as one pressure and reads nothing else; grief, relief and affection are modelled
    /// affect that this particular decision has no use for, and a sweep that produced a difference
    /// for all eight would be evidence that something had started reading affect it has no business
    /// reading.
    /// </summary>
    internal sealed class EmotionAxis : PlaygroundSweepAxis
    {
        public override string Id => "emotion";

        public override string Summary => "one emotional state at a time, over an otherwise untouched speaker";

        public override string Question => "Which present affect reaches a disclosure, and which is inert here?";

        public override string Held => "the tie, the belief, the personality, the claim, the listener and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            IReadOnlyList<PlaygroundInput> held = Inputs(PlaygroundInputs.Tie(RelationKind.Acquaintance, 15));

            List<PlaygroundSweepRow> rows = new List<PlaygroundSweepRow>
            {
                Row("unmoved", seed, Preset, held, baseline: true)
            };

            EmotionalState[] emotions =
            {
                EmotionalState.Anger, EmotionalState.Fear, EmotionalState.Shame, EmotionalState.Grief,
                EmotionalState.Relief, EmotionalState.Suspicion, EmotionalState.Affection, EmotionalState.Stress
            };

            for (int i = 0; i < emotions.Length; i++)
            {
                rows.Add(Row(
                    emotions[i].ToString().ToLowerInvariant(), seed, Preset,
                    With(held, PlaygroundInputs.Feels(emotions[i], 0.80))));
            }

            return rows;
        }

        private const string Preset = "neutral-witness";
    }

    /// <summary>
    /// The same speaker, and old business they come by in every way the model supports.
    ///
    /// The three questions BQ-081 and BQ-082 keep apart are printed apart here, because collapsing
    /// them is the failure the whole area exists to prevent: whether this person may recall the
    /// event at all, whether they would raise it with the person opposite, and whether it is the
    /// kind of history that gains by being told again somewhere else. A row can pass the first and
    /// fail the second, and a row that failed the second must never leave a trace in the words.
    ///
    /// The last two rows differ only in where the material happened. BQ-082 will not call something
    /// a recurrence in the very thread or place it belongs to, so the same event, twice, in two
    /// contexts, is the cheapest demonstration that the gate reads context rather than content.
    /// </summary>
    internal sealed class CallbackAxis : PlaygroundSweepAxis
    {
        public override string Id => "callbacks";

        public override string Summary => "the same speaker, and history reached by every route the model has";

        public override string Question =>
            "May they recall it, would they say it here, does it recur - and does a withheld memory ever reach words?";

        public override string Held => "the claim, the pair of people, the settled-material age and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            IReadOnlyList<PlaygroundInput> open = Inputs(PlaygroundInputs.Tie(RelationKind.Friend, 85));

            return new[]
            {
                Row("no history", seed, Neutral, With(open, PlaygroundInputs.Wait(20)), baseline: true),

                Row("it was done to them", seed, Neutral,
                    With(open, PlaygroundInputs.History("intimidate", PlaygroundRoles.Witness, 20))),

                Row("done to them, guarded", seed, Guarded, Inputs(PlaygroundInputs.Wait(0)),
                    against: "it was done to them"),

                Row("they watched it happen", seed, Neutral,
                    With(open, PlaygroundInputs.History("intimidate", PlaygroundRoles.Thief, 20))),

                Row("they were told of it", seed, Neutral,
                    Inputs(
                        PlaygroundInputs.Tie(RelationKind.Friend, 85),
                        PlaygroundInputs.Believes(BrilliantQuesting.Knowledge.KnowledgeSource.Hearsay, 0.85),
                        PlaygroundInputs.Wait(20)),
                    options => options.Speaker = PlaygroundRoles.Victim),

                Row("a scandal, elsewhere", seed, Neutral,
                    With(open, PlaygroundInputs.Elsewhere(
                        WorldEventType.AccusationMade, PlaygroundRoles.Player, PlaygroundRoles.Thief,
                        witnessedBySpeaker: true, sameContext: false, days: 20))),

                Row("a scandal, right here", seed, Neutral,
                    With(open, PlaygroundInputs.Elsewhere(
                        WorldEventType.AccusationMade, PlaygroundRoles.Player, PlaygroundRoles.Thief,
                        witnessedBySpeaker: true, sameContext: true, days: 20)),
                    against: "a scandal, elsewhere"),

                PlaygroundSweepRow.NotSupported(
                    "the Heard route",
                    "CallbackHooks reads the Heard route off WorldEvent.Related - a claim the event itself lists -\n"
                    + "and no history this stage can produce puts one there. The founding Theft event relates the\n"
                    + "stolen item, and the claim about it is minted afterwards carrying Fact.OriginEvent, so the\n"
                    + "link exists in the other direction only. The route is exercised directly by Core's own\n"
                    + "CallbackHookTests; what this sweep can honestly say is that it is unreachable from here.")
            };
        }

        /// <summary>
        /// The four gates, one column each, because the whole point is that they are four.
        ///
        /// <em>Recall</em> is every hook this speaker has a route to at all, asked of
        /// <c>CallbackHooks.For</c> with no selection at all. <em>About the listener</em> is the
        /// same question under the selection the playground actually makes, which is where material
        /// about somebody else falls out - a hook can be perfectly recallable and simply not be
        /// about the person in the room. <em>Permission</em> is <c>CallbackDisclosure</c>'s answer.
        /// And <em>eligible</em> is whether the shipped library has any wording for the material
        /// that survived all three, which is the gate nothing upstream can see.
        /// </summary>
        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("recall, selection, permission, recurrence and wording, kept apart");
            output.WriteLine("  " + LabColumn("row", 24) + LabColumn("recall (any)", 30)
                + LabColumn("about listener", 16) + LabColumn("permission", 22)
                + LabColumn("wordings", 10) + "in the words");

            List<string> unwordable = new List<string>();

            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (!row.Evaluated || row.Run == null)
                {
                    continue;
                }

                IReadOnlyList<CallbackHook> any = CallbackHooks.For(
                    row.Run.Stage.World, row.Run.Stage.Vanilla, row.Run.Speaker, row.Run.Stage.Now,
                    new CallbackSelection());

                IReadOnlyList<CallbackHook> aimed = CallbackHooks.For(
                    row.Run.Stage.World, row.Run.Stage.Vanilla, row.Run.Speaker, row.Run.Stage.Now,
                    new CallbackSelection { About = row.Run.Listener });

                int wordings = History(row);

                output.WriteLine("  " + LabColumn(row.Label, 24)
                    + LabColumn(Routes(any), 30)
                    + LabColumn(aimed.Count.ToString(), 16)
                    + LabColumn(Permission(row), 22)
                    + LabColumn(wordings.ToString(), 10)
                    + Spoken(row));

                if (row.Turn?.Callback != null && wordings == 0)
                {
                    unwordable.Add(row.Label + ": " + row.Turn.Callback.Hook.PrimaryKind
                        + " via " + row.Turn.Callback.Hook.Route
                        + ", other party " + row.Turn.Callback.Hook.Party);
                }
            }

            output.WriteLine();
            output.WriteLine("content coverage");
            if (unwordable.Count == 0)
            {
                output.WriteLine("  every cleared callback had at least one wording in the library.");
            }

            for (int i = 0; i < unwordable.Count; i++)
            {
                output.WriteLine("  DEFECT: material cleared for this listener that nothing in the library says -");
                output.WriteLine("          " + unwordable[i]);
            }

            output.WriteLine();
            output.WriteLine("  'recall (any)' is every route this speaker has to settled material; 'about listener'");
            output.WriteLine("  is what survives the selection an exchange actually makes. A hook can be recallable");
            output.WriteLine("  and simply not be about the person in the room, which is a third answer distinct");
            output.WriteLine("  from having no route and from being refused permission.");
        }

        /// <summary>How many callback-slot fragments about recalled history the wording layer had.</summary>
        private static int History(PlaygroundSweepRow row)
        {
            if (row.Turn?.Eligible == null)
            {
                return 0;
            }

            IReadOnlyList<string> ids = row.Turn.Eligible.At(FragmentPosition.Callback);
            int found = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i].StartsWith("call.history.", System.StringComparison.Ordinal))
                {
                    found++;
                }
            }

            return found;
        }

        private static string Routes(IReadOnlyList<CallbackHook> hooks)
        {
            if (hooks.Count == 0)
            {
                return "none";
            }

            List<string> said = new List<string>();
            for (int i = 0; i < hooks.Count && i < 3; i++)
            {
                said.Add(hooks[i].PrimaryKind + "/" + hooks[i].Route);
            }

            return hooks.Count + ": " + string.Join(" ", said);
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "material the speaker would not raise leaves nothing in the words",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    if (row.Turn?.WithheldCallback == null || row.Turn.Line == null || !row.Turn.Line.Rendered)
                    {
                        return null;
                    }

                    IReadOnlyList<string> fragments = row.Turn.Line.Fragments;
                    for (int i = 0; i < fragments.Count; i++)
                    {
                        if (fragments[i].StartsWith("call.history.", System.StringComparison.Ordinal))
                        {
                            return "a withheld memory reached the words as " + fragments[i];
                        }
                    }

                    return null;
                })),

            new PlaygroundSweepInvariant(
                "a recurrence is never taken from the thread or place it happened in",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    CallbackPermit recurrence = row.Turn?.Recurrence;
                    if (recurrence?.Hook == null || row.Run == null)
                    {
                        return null;
                    }

                    ContinuityContext here = new ContinuityContext(
                        row.Run.Stage.Situation.Thread?.Id ?? BrilliantQuesting.Foundation.EntityId.None,
                        row.Run.Stage.Zone);

                    return CallbackRecurrence.IsUnrelatedContext(recurrence.Hook, here)
                        ? null
                        : "a recurrence was offered from this very thread or place";
                }))
        };

        private static string Permission(PlaygroundSweepRow row)
        {
            if (row.Turn?.Callback != null)
            {
                return "cleared";
            }

            return row.Turn?.WithheldCallback == null ? "-" : "withheld: " + row.Turn.WithheldCallback.Strategy;
        }

        private static string Spoken(PlaygroundSweepRow row)
        {
            IReadOnlyList<string> fragments = row.Turn?.Line?.Fragments;
            if (fragments == null)
            {
                return "nothing worded";
            }

            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].StartsWith("call.", System.StringComparison.Ordinal))
                {
                    return fragments[i];
                }
            }

            return "no callback fragment";
        }

        private static string LabColumn(string label, int width) => Cli.LabText.Column(label, width);

        private const string Neutral = "neutral-witness";
        private const string Guarded = "guarded-history";
    }
}
