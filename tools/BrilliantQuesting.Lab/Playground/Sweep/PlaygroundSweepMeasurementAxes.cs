using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// How many different ways the shipped content can say the same thing.
    ///
    /// A measurement rather than a comparison, and it is printed as a distribution rather than as a
    /// row per point: a table of sixty single draws says less than the four counts underneath them.
    ///
    /// <b>The seed is not a wording knob.</b> It seeds the whole laboratory - the situation, the
    /// people, their ties, the action stream - and only then the wording draw. So the distinct
    /// semantic outcomes are printed beside the distinct lines: a state whose semantics stayed
    /// constant across the sweep is one whose line count really is expressive variety, and one
    /// whose semantics moved is telling you the sample is not comparable.
    ///
    /// A selector with one wording is reported as a content-coverage defect, because that is what
    /// it is. It is not repaired here: writing fragments to make a measurement look better would be
    /// authoring content to fit an instrument.
    /// </summary>
    internal sealed class SeedAxis : PlaygroundSweepAxis
    {
        private const int Draws = 12;

        private static readonly string[] States =
        {
            "neutral-witness", "hostile-witness", "trusted-confidant", "loyal-liar", "lived-trade"
        };

        public override string Id => "seeds";

        public override string Summary => "how many ways the shipped content can say the same thing";

        public override string Question =>
            "For each state the playground reaches, how much expressive variety is actually shipped?";

        public override string Held => "the preset's own state; only the seed moves";

        public override bool PrintsRowTable => false;

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed)
        {
            List<PlaygroundSweepRow> rows = new List<PlaygroundSweepRow>();
            for (int s = 0; s < States.Length; s++)
            {
                for (int d = 0; d < Draws; d++)
                {
                    ulong drawn = seed + (ulong)d;
                    rows.Add(PlaygroundSweepRow.Of(
                        States[s] + " @ " + drawn,
                        s == 0 && d == 0,
                        d == 0 ? new string[0] : new[] { "seed = " + drawn },
                        PlaygroundRun.Begin(
                            new PlaygroundOptions { Seed = drawn, Preset = States[s], Turns = 1 },
                            PlaygroundPresets.Default())));
                }
            }

            return rows;
        }

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine(Draws + " seeds per state. 'semantics' is how many different answers the state gave -");
            output.WriteLine("anything above one means the seed moved the world, not only the wording.");
            output.WriteLine();
            output.WriteLine("  " + LabText.Column("state", 20) + LabText.Column("semantics", 11)
                + LabText.Column("lines", 7) + LabText.Column("cores", 7) + "most-drawn fragment");

            List<string> defects = new List<string>();
            List<string> thin = new List<string>();

            for (int s = 0; s < States.Length; s++)
            {
                HashSet<string> semantics = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> cores = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, int> fragments = new Dictionary<string, int>(StringComparer.Ordinal);
                Dictionary<FragmentPosition, HashSet<string>> bySlot =
                    new Dictionary<FragmentPosition, HashSet<string>>();
                Dictionary<FragmentPosition, int> widest = new Dictionary<FragmentPosition, int>();
                int worded = 0;

                for (int i = 0; i < result.Rows.Count; i++)
                {
                    PlaygroundSweepRow row = result.Rows[i];
                    if (!row.Label.StartsWith(States[s] + " @", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    semantics.Add(row.SemanticSignature);
                    Widen(widest, row);

                    RealizedLine line = row.Turn?.Line;
                    if (line == null || !line.Rendered)
                    {
                        continue;
                    }

                    worded++;
                    lines.Add(line.Text);
                    cores.Add(line.Core);
                    for (int f = 0; f < line.Fragments.Count; f++)
                    {
                        fragments.TryGetValue(line.Fragments[f], out int seen);
                        fragments[line.Fragments[f]] = seen + 1;
                        Slot(bySlot, row.Run.Stage.Realizer.Library, line.Fragments[f]);
                    }
                }

                output.WriteLine("  " + LabText.Column(States[s], 20)
                    + LabText.Column(semantics.Count.ToString(), 11)
                    + LabText.Column(lines.Count.ToString(), 7)
                    + LabText.Column(cores.Count.ToString(), 7)
                    + Dominant(fragments, worded));

                output.WriteLine("    drawn by slot:    " + BySlot(bySlot));
                output.WriteLine("    widest pool seen: " + Widest(widest));

                if (worded > 0 && cores.Count < 2)
                {
                    defects.Add(States[s] + ": " + worded + " worded draw(s) produced " + cores.Count
                                + " distinct core fragment(s)");
                }

                // A slot the state genuinely reaches, and for which the library ships exactly one
                // legal wording, is a selector with no choice at all - a content-coverage fact
                // rather than a wording bug, and one no amount of seeding can improve.
                for (int i = 0; i < PlaygroundEligibility.Slots.Length; i++)
                {
                    FragmentPosition slot = PlaygroundEligibility.Slots[i];
                    if (!widest.TryGetValue(slot, out int pool) || pool != 1)
                    {
                        continue;
                    }

                    string note = States[s] + ": the " + PlaygroundEligibility.Name(slot)
                                  + " slot was reachable and the library ships exactly one legal wording for it";

                    // A required slot with one wording is a defect: every line this state ever
                    // produces says the same thing the same way. An optional slot with one wording
                    // is thinner than it should be and still has "say nothing" as its other option.
                    if (slot == FragmentPosition.Core)
                    {
                        defects.Add(note);
                    }
                    else
                    {
                        thin.Add(note);
                    }
                }
            }

            output.WriteLine();
            output.WriteLine("content coverage");
            if (defects.Count == 0)
            {
                output.WriteLine("  no required slot is down to a single wording: every state that worded anything");
                output.WriteLine("  had more than one way of saying it.");
            }

            for (int i = 0; i < defects.Count; i++)
            {
                output.WriteLine("  DEFECT: " + defects[i]);
            }

            for (int i = 0; i < thin.Count; i++)
            {
                output.WriteLine("  thin:   " + thin[i]);
            }
        }

        private static void Widen(Dictionary<FragmentPosition, int> widest, PlaygroundSweepRow row)
        {
            if (row.Turn?.Eligible == null)
            {
                return;
            }

            for (int i = 0; i < PlaygroundEligibility.Slots.Length; i++)
            {
                FragmentPosition slot = PlaygroundEligibility.Slots[i];
                int count = row.Turn.Eligible.CountAt(slot);
                if (!widest.TryGetValue(slot, out int seen) || count > seen)
                {
                    widest[slot] = count;
                }
            }
        }

        private static string Widest(Dictionary<FragmentPosition, int> widest)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < PlaygroundEligibility.Slots.Length; i++)
            {
                FragmentPosition slot = PlaygroundEligibility.Slots[i];
                parts.Add(PlaygroundEligibility.Name(slot) + " "
                    + (widest.TryGetValue(slot, out int pool) ? pool : 0));
            }

            return string.Join(" ", parts);
        }

        private static void Slot(
            Dictionary<FragmentPosition, HashSet<string>> bySlot, DialogueFragmentLibrary library, string id)
        {
            if (!library.TryGet(id, out DialogueFragment fragment))
            {
                return;
            }

            if (!bySlot.TryGetValue(fragment.Position, out HashSet<string> ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                bySlot[fragment.Position] = ids;
            }

            ids.Add(id);
        }

        private static string BySlot(Dictionary<FragmentPosition, HashSet<string>> bySlot)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < PlaygroundEligibility.Slots.Length; i++)
            {
                FragmentPosition slot = PlaygroundEligibility.Slots[i];
                int count = bySlot.TryGetValue(slot, out HashSet<string> ids) ? ids.Count : 0;
                parts.Add(PlaygroundEligibility.Name(slot) + " " + count);
            }

            return string.Join(" ", parts);
        }

        private static string Dominant(Dictionary<string, int> fragments, int worded)
        {
            string top = null;
            int most = 0;
            foreach (KeyValuePair<string, int> use in fragments)
            {
                if (use.Value > most || (use.Value == most && top != null && string.CompareOrdinal(use.Key, top) < 0))
                {
                    top = use.Key;
                    most = use.Value;
                }
            }

            if (top == null || worded == 0)
            {
                return "nothing was worded";
            }

            return top + " in " + most + " of " + worded;
        }
    }

    /// <summary>
    /// Which layer owns each answer the sweep prints.
    ///
    /// The ledger <c>playground-systems</c> already keeps, narrowed to the inputs a sweep can move
    /// and read the other way round: not "what does the playground exercise" but "when a row moves,
    /// who moved it". The distinction that matters is the one a table makes easy to lose - a voice
    /// the laboratory handed the speaker and a relationship the graph actually holds look identical
    /// as a column, and only one of them is the simulation talking.
    ///
    /// Authored rather than inferred, for the same reason <see cref="PlaygroundAvailability"/> is:
    /// a step that changes one of these answers is expected to change this table in the same commit.
    /// </summary>
    internal sealed class OwnershipAxis : PlaygroundSweepAxis
    {
        private static readonly string[][] Inputs =
        {
            new[] { "relationship kind and sentiment", "RelationshipGraph", "Production" },
            new[] { "obligations between the pair", "ObligationLedger", "Production" },
            new[] { "belief presence, route and confidence", "KnowledgeGraph", "Production" },
            new[] { "claim truth and secrecy", "Fact", "Production" },
            new[] { "personality weights", "PersonalityWeights", "Production" },
            new[] { "present emotion", "EmotionalStateProfile", "Production" },
            new[] { "personal prohibitions", "NegativeSpaceProfile", "Production" },
            new[] { "callback history in the ledger", "EventLedger, via actions", "Production" },
            new[] { "number of exchanges, and days between them", "the laboratory's scene", "LaboratoryAuthored" },
            new[] { "identity facets: work, hobby, race, archetype", "SandboxVanillaState", "SyntheticInput" },
            new[] { "the check outcome a scripted history needs", "PlaygroundFixedChecks", "SyntheticInput" },
            new[] { "VoiceProfile", "PlaygroundVoices", "LaboratoryAuthored" },
            new[] { "that somebody undertook something at all", "the laboratory composes it", "LaboratoryAuthored" },
            new[] { "the weirdness ceiling", "fixed at DistinctlyElin", "LaboratoryAuthored" },
            new[] { "the seed", "the whole laboratory", "LaboratoryAuthored" }
        };

        public override string Id => "ownership";

        public override string Summary => "which layer owns each input the sweep can move, and each answer it reads";

        public override string Question => "When a row moves, who moved it - and which of these is the laboratory talking?";

        public override string Held => "nothing: this family runs no conversation";

        public override bool PrintsRowTable => false;

        public override IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed) => new PlaygroundSweepRow[0];

        public override void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("inputs a sweep may move, and who owns the state behind each");
            output.WriteLine("  " + LabText.Column("input", 46) + LabText.Column("owner", 26) + "kind");
            for (int i = 0; i < Inputs.Length; i++)
            {
                output.WriteLine("  " + LabText.Column(Inputs[i][0], 46)
                    + LabText.Column(Inputs[i][1], 26)
                    + PlaygroundAvailability.Label(Support(Inputs[i][2])));
            }

            output.WriteLine();
            output.WriteLine("systems whose answers a sweep row reports, from the playground's own ledger");
            Column(output, PlaygroundSupport.Production);
            Column(output, PlaygroundSupport.SyntheticInput);
            Column(output, PlaygroundSupport.LaboratoryAuthored);
            Column(output, PlaygroundSupport.RuntimeRequired);

            output.WriteLine();
            output.WriteLine("  No row in any family turns a laboratory-chosen voice or a laboratory-composed");
            output.WriteLine("  promise into something the simulation decided. Run 'playground-systems' for the");
            output.WriteLine("  full ledger, including the systems no headless run can reach at all.");
        }

        private static void Column(TextWriter output, PlaygroundSupport support)
        {
            IReadOnlyList<PlaygroundSystem> systems = PlaygroundAvailability.WithSupport(support);
            output.WriteLine();
            output.WriteLine("  " + PlaygroundAvailability.Label(support) + " (" + systems.Count + ")");
            for (int i = 0; i < systems.Count; i++)
            {
                output.WriteLine("    " + LabText.Column(systems[i].Name, 44) + systems[i].Step);
            }
        }

        private static PlaygroundSupport Support(string named)
        {
            switch (named)
            {
                case "Production":
                    return PlaygroundSupport.Production;
                case "SyntheticInput":
                    return PlaygroundSupport.SyntheticInput;
                case "LaboratoryAuthored":
                    return PlaygroundSupport.LaboratoryAuthored;
                default:
                    return PlaygroundSupport.RuntimeRequired;
            }
        }
    }
}
