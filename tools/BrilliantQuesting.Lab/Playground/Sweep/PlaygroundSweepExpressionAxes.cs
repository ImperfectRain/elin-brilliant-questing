using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One semantic act, one state, and the voice it is said in moved across its axes.
    ///
    /// Four things have to be true at once and each is a column or an invariant here. Meaning never
    /// moves - a voice is a request about wording and can reach nothing else. A more specified
    /// voice never widens the pool, because a tone request is a filter and a filter that added
    /// candidates would be choosing what to say rather than how. Opposite poles do not leak, so a
    /// warm voice never speaks the curt fragment. And a neutral voice leaves the ordinary pool
    /// exactly as it was, which is what makes "no tonal constraint" an honest description rather
    /// than a hidden default.
    ///
    /// All four hold twice over as of BQ-142, because a voice now asks for two things and the
    /// second is checked separately rather than folded into the first. The last two rows request no
    /// tone at all and differ from each other only in length, cadence and figuration, so the
    /// difference between their lines has exactly one possible cause - which is the whole reason
    /// they were added rather than idiolect being bolted onto the existing four.
    ///
    /// Run over a state that refuses, because that is where the shipped library's tone tags are -
    /// a curt core, a curt closer and a warm one - and where BQ-142's migrated cross-section is
    /// thickest. An axis measured over a state with no marked fragments in its pool would measure
    /// nothing and look like a pass.
    /// </summary>
    internal sealed class VoiceAxis : PlaygroundSweepAxis
    {
        private static readonly ulong[] Draws = { 0UL, 1UL, 2UL, 3UL };

        public override string Id => "voice";

        public override string Summary => "one act, one state, and the voice it is said in moved across its axes";

        public override string Question =>
            "Does a voice ever change what is meant, and does asking for more of one ever widen the choice?";

        public override string Held => "the state, the claim, the pair of people, the act it produced and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            List<PlaygroundSweepRow> rows = new List<PlaygroundSweepRow>();
            IReadOnlyList<string> voices = PlaygroundVoices.All;

            for (int i = 0; i < voices.Count; i++)
            {
                string voice = voices[i];
                rows.Add(Row(
                    voice, seed, Preset, Inputs(),
                    options => options.Voice = voice,
                    baseline: voice == PlaygroundVoices.Neutral,
                    alsoChanged: new[] { "voice = " + voice }));
            }

            return rows;
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "every voice says the same thing",
                rows => OneMeaning(rows)),

            new PlaygroundSweepInvariant(
                "a requested tone never widens a slot's pool",
                rows =>
                {
                    List<string> broken = new List<string>();
                    PlaygroundSweepRow neutral = HonestyAxis.Find(rows, PlaygroundVoices.Neutral);
                    if (neutral?.Turn?.Eligible == null)
                    {
                        return broken;
                    }

                    for (int i = 0; i < rows.Count; i++)
                    {
                        PlaygroundSweepRow row = rows[i];
                        if (row.Turn?.Eligible == null || row.Turn.Request.Tone.Count == 0)
                        {
                            continue;
                        }

                        for (int slot = 0; slot < PlaygroundEligibility.Slots.Length; slot++)
                        {
                            FragmentPosition position = PlaygroundEligibility.Slots[slot];
                            int constrained = row.Turn.Eligible.CountAt(position);
                            int free = neutral.Turn.Eligible.CountAt(position);
                            if (constrained > free)
                            {
                                broken.Add(row.Label + " had " + constrained + " " + PlaygroundEligibility.Name(position)
                                           + "(s) where an unconstrained voice had " + free);
                            }
                        }
                    }

                    return broken;
                }),

            new PlaygroundSweepInvariant(
                "opposite tone poles do not leak through",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    if (row.Turn?.Request == null || row.Turn.Eligible == null || row.Run == null)
                    {
                        return null;
                    }

                    DialogueFragmentLibrary library = row.Run.Stage.Realizer.Library;
                    IReadOnlyList<string> tone = row.Turn.Request.Tone;

                    for (int t = 0; t < tone.Count; t++)
                    {
                        string opposite = DialogueTones.Opposite(tone[t]);
                        if (opposite == null)
                        {
                            continue;
                        }

                        for (int slot = 0; slot < PlaygroundEligibility.Slots.Length; slot++)
                        {
                            IReadOnlyList<string> ids = row.Turn.Eligible.At(PlaygroundEligibility.Slots[slot]);
                            for (int i = 0; i < ids.Count; i++)
                            {
                                if (library.TryGet(ids[i], out DialogueFragment fragment)
                                    && Asks(fragment.ToneTags, opposite))
                                {
                                    return "asked for " + tone[t] + " and left " + ids[i]
                                           + ", which is tagged " + opposite + ", eligible";
                                }
                            }
                        }
                    }

                    return null;
                })),

            // BQ-142's two, and deliberately separate checks rather than a generalisation of the
            // two above. A request now carries tone and idiolect, and a combined check would pass
            // a row whose idiolect widened a pool its tone had narrowed further - which is exactly
            // the arithmetic a sweep exists to refuse to average away.
            new PlaygroundSweepInvariant(
                "a requested idiolect never widens a slot's pool",
                rows =>
                {
                    List<string> broken = new List<string>();
                    PlaygroundSweepRow neutral = HonestyAxis.Find(rows, PlaygroundVoices.Neutral);
                    if (neutral?.Turn?.Eligible == null)
                    {
                        return broken;
                    }

                    for (int i = 0; i < rows.Count; i++)
                    {
                        PlaygroundSweepRow row = rows[i];
                        if (row.Turn?.Eligible == null || row.Turn.Request.Idiolect.Count == 0)
                        {
                            continue;
                        }

                        for (int slot = 0; slot < PlaygroundEligibility.Slots.Length; slot++)
                        {
                            FragmentPosition position = PlaygroundEligibility.Slots[slot];
                            int constrained = row.Turn.Eligible.CountAt(position);
                            int free = neutral.Turn.Eligible.CountAt(position);
                            if (constrained > free)
                            {
                                broken.Add(row.Label + " had " + constrained + " " + PlaygroundEligibility.Name(position)
                                           + "(s) where an unconstrained voice had " + free);
                            }
                        }
                    }

                    return broken;
                }),

            new PlaygroundSweepInvariant(
                "opposite idiolect poles do not leak through",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    if (row.Turn?.Request == null || row.Turn.Eligible == null || row.Run == null)
                    {
                        return null;
                    }

                    DialogueFragmentLibrary library = row.Run.Stage.Realizer.Library;
                    IReadOnlyList<string> idiolect = row.Turn.Request.Idiolect;

                    for (int t = 0; t < idiolect.Count; t++)
                    {
                        string opposite = DialogueIdiolect.Opposite(idiolect[t]);
                        if (opposite == null)
                        {
                            continue;
                        }

                        for (int slot = 0; slot < PlaygroundEligibility.Slots.Length; slot++)
                        {
                            IReadOnlyList<string> ids = row.Turn.Eligible.At(PlaygroundEligibility.Slots[slot]);
                            for (int i = 0; i < ids.Count; i++)
                            {
                                if (library.TryGet(ids[i], out DialogueFragment fragment)
                                    && Asks(fragment.IdiolectTags, opposite))
                                {
                                    return "asked for " + idiolect[t] + " and left " + ids[i]
                                           + ", which is marked " + opposite + ", eligible";
                                }
                            }
                        }
                    }

                    return null;
                }))
        };

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("what each voice actually asked for, and what it left to choose from");
            output.WriteLine("  " + LabText.Column("voice", 22) + LabText.Column("tone", 26)
                + LabText.Column("idiolect", 30) + "eligible");
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (row.Turn?.Request == null)
                {
                    continue;
                }

                output.WriteLine("  " + LabText.Column(row.Label, 22)
                    + LabText.Column(PlaygroundText.Join(row.Turn.Request.Tone, "no tonal constraint"), 26)
                    + LabText.Column(PlaygroundText.Join(row.Turn.Request.Idiolect, "no habit requested"), 30)
                    + row.EligibleBySlot);
            }

            output.WriteLine();
            output.WriteLine("several draws of the same meaning, over deterministic seeds");
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (!row.Evaluated)
                {
                    continue;
                }

                output.WriteLine("  " + row.Label);
                for (int d = 0; d < Draws.Length; d++)
                {
                    PlaygroundRun draw = Draw(row.Label, Draws[d]);
                    PlaygroundTurn turn = draw.Exchange.Turns.Count == 0 ? null : draw.Exchange.Turns[0];
                    output.WriteLine("    seed " + LabText.Column(Draws[d].ToString(), 6)
                        + (turn?.Line == null || !turn.Line.Rendered ? "nothing worded" : "\"" + turn.Line.Text + "\""));
                }
            }

            output.WriteLine();
            output.WriteLine("  The seed changes the whole world this laboratory generates, not only the draw,");
            output.WriteLine("  so the meanings above are printed with the lines and are the thing to check.");
            for (int i = 0; i < result.Rows.Count; i++)
            {
                if (result.Rows[i].Evaluated)
                {
                    output.WriteLine("  meaning at the run's own seed: " + result.Rows[i].Meaning);
                    break;
                }
            }
        }

        private static PlaygroundRun Draw(string voice, ulong seed)
        {
            return PlaygroundRun.Begin(
                new PlaygroundOptions { Seed = seed, Preset = Preset, Turns = 1, Voice = voice },
                PlaygroundPresets.Default());
        }

        private static bool Asks(IReadOnlyList<string> tone, string tag)
        {
            for (int i = 0; i < tone.Count; i++)
            {
                if (tone[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

        internal static IReadOnlyList<string> OneMeaning(IReadOnlyList<PlaygroundSweepRow> rows)
        {
            List<string> broken = new List<string>();
            string meaning = null;
            string from = null;

            for (int i = 0; i < rows.Count; i++)
            {
                PlaygroundSweepRow row = rows[i];
                if (row.Turn?.Reply == null)
                {
                    continue;
                }

                if (meaning == null)
                {
                    meaning = row.Meaning;
                    from = row.Label;
                    continue;
                }

                if (row.Meaning != meaning)
                {
                    broken.Add(from + " meant '" + meaning + "' and " + row.Label + " meant '" + row.Meaning + "'");
                }
            }

            return broken;
        }

        private const string Preset = "hostile-witness";
    }

    /// <summary>
    /// One act, one relationship, one belief, one voice - and the lived context the speaker is read
    /// as having.
    ///
    /// The rows are BQ-145's five domains plus the three answers that are not a domain: an actor
    /// nobody has read at all, one whose only facets are race and character archetype, and one
    /// whose work is spelled in a way nothing recognises. All three must produce no vocabulary and
    /// no flavoured fragment, which is the anti-stereotype gate stated as a measurement: an
    /// unrecognised trade is not a weaker trade, it is not evidence of a trade.
    ///
    /// What identity may change is which wording says the point. What it may never change is the
    /// point, the willingness or the personality - so the decision columns are expected to be
    /// identical down the whole table, and an invariant says so.
    /// </summary>
    internal sealed class VocabularyAxis : PlaygroundSweepAxis
    {
        public override string Id => "vocabulary";

        public override string Summary => "the same act and state, over identities read as different lived contexts";

        public override string Question =>
            "Where does lived context reach the words, and does it ever reach the decision?";

        public override string Held => "the state, the tie, the belief, the voice, the claim and the seed";

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            return new[]
            {
                Row("unread", seed, Preset, Inputs(PlaygroundInputs.Identity("nothing read", null)), baseline: true),
                Row("race and archetype", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "race and character archetype only",
                    builder => builder.WithRace("norland").WithCharacterArchetype("villager")))),
                Row("cultivation", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'farmer'", builder => builder.WithWork("farmer")))),
                Row("alchemy", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'alchemist'", builder => builder.WithWork("alchemist")))),
                Row("craft", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'smith'", builder => builder.WithWork("smith")))),
                Row("trade", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'merchant'", builder => builder.WithWork("merchant")))),
                Row("public order", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'guard'", builder => builder.WithWork("guard")))),
                Row("unrecognised work", seed, Preset, Inputs(PlaygroundInputs.Identity(
                    "work 'stargazer'", builder => builder.WithWork("stargazer"))))
            };
        }

        public override IReadOnlyList<PlaygroundSweepInvariant> Invariants => new[]
        {
            new PlaygroundSweepInvariant(
                "every identity says the same thing",
                rows => VoiceAxis.OneMeaning(rows)),

            new PlaygroundSweepInvariant(
                "identity never reaches the decision",
                rows =>
                {
                    List<string> broken = new List<string>();
                    PlaygroundSweepRow unread = HonestyAxis.Find(rows, "unread");
                    if (unread?.Turn?.Decision == null)
                    {
                        return broken;
                    }

                    for (int i = 0; i < rows.Count; i++)
                    {
                        PlaygroundSweepRow row = rows[i];
                        if (row.Turn?.Decision == null)
                        {
                            continue;
                        }

                        if (row.Strategy != unread.Strategy || row.Depth != unread.Depth || row.Tactic != unread.Tactic)
                        {
                            broken.Add(row.Label + " decided " + row.SemanticSignature
                                       + " where an unread actor decided " + unread.SemanticSignature);
                        }
                    }

                    return broken;
                }),

            new PlaygroundSweepInvariant(
                "an identity nothing recognises asks for no vocabulary",
                rows => PlaygroundSweepInvariant.Each(rows, row =>
                {
                    bool shouldBeSilent = row.Label == "unread"
                        || row.Label == "race and archetype"
                        || row.Label == "unrecognised work";

                    if (!shouldBeSilent || row.Turn?.Request == null)
                    {
                        return null;
                    }

                    return row.Turn.Request.Vocabulary.Count == 0
                        ? null
                        : "guessed a lived context: " + row.Vocabulary;
                }))
        };

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("which flavoured fragment each identity made eligible");
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                if (row.Turn?.Eligible == null)
                {
                    continue;
                }

                output.WriteLine("  " + LabText.Column(row.Label, 22)
                    + LabText.Column(row.Vocabulary, 18)
                    + "modifiers: " + PlaygroundText.Join(
                        row.Turn.Eligible.At(FragmentPosition.Modifier), "none"));
            }
        }

        private const string Preset = "lived-trade";
    }
}
