using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// How honestly a system can be exercised with no game attached.
    ///
    /// Deliberately not <see cref="HarnessCoverageState"/>, which the integration harness uses for
    /// a different question: that enum answers "what did this run touch", so its <c>Available</c>
    /// and <c>Exercised</c> are facts about one pass. This one answers "what can be shown at all
    /// headlessly", which is a standing property of the system. The one word the two share is the
    /// one that matters to a reader - a system that needs the game prints as <c>PLUGIN ONLY</c> in
    /// both places, because there is no version of that answer that should depend on which
    /// laboratory command you ran.
    /// </summary>
    public enum PlaygroundSupport
    {
        /// <summary>Its own Core API runs here, over state this world actually holds.</summary>
        Production = 0,

        /// <summary>
        /// Production logic, fed through the headless seam the architecture already uses -
        /// <c>SandboxVanillaState</c> answering what the game would answer. Honest because the
        /// sandbox reports a facet as unread rather than guessing one, which is the same thing the
        /// live adapter does when the game will not say.
        /// </summary>
        SyntheticInput = 1,

        /// <summary>
        /// No production authority chooses this yet, so the laboratory chooses it and says so. Not
        /// a gap in the playground: a gap in the simulation, named where somebody can see it.
        /// </summary>
        LaboratoryAuthored = 2,

        /// <summary>Needs a running Elin. Not simulated here, not mocked here.</summary>
        RuntimeRequired = 3
    }

    /// <summary>One system, and what running it headlessly actually amounts to.</summary>
    public sealed class PlaygroundSystem
    {
        public PlaygroundSystem(string name, string step, PlaygroundSupport support, string note)
        {
            Name = name;
            Step = step;
            Support = support;
            Note = note;
        }

        public string Name { get; }

        /// <summary>The BQ step that owns it, for looking it up in the roadmap.</summary>
        public string Step { get; }

        public PlaygroundSupport Support { get; }

        /// <summary>Why it is in this column. Diagnostic; nothing branches on it.</summary>
        public string Note { get; }
    }

    /// <summary>
    /// What the playground exercises, and what it refuses to pretend to.
    ///
    /// The list exists because a headless demonstration is only useful if it is honest about its
    /// own edges. Three of them matter and each has its own column above: a system whose input the
    /// sandbox supplies is not the same as one whose decision the laboratory made up, and neither
    /// is the same as one that cannot run at all without the game. Nothing here is inferred at run
    /// time - this is an authored ledger, kept beside the code it describes, and a step that
    /// changes any of these three answers is expected to change this table in the same commit.
    ///
    /// <b>The headline.</b> Nothing in BQ-070 through BQ-083 is consumed by the plugin. The live
    /// half projects procedural verbs into Elin's dialogue and reads the game's own state; no part
    /// of it takes a <c>SpeechAct</c>, a <c>DisclosureDecision</c> or a <c>RealizedLine</c> and puts
    /// it in front of a player. So the last thing this playground can honestly show is a realized
    /// line and its meaning, not a line anybody read.
    /// </summary>
    public static class PlaygroundAvailability
    {
        private static readonly PlaygroundSystem[] Systems =
        {
            new PlaygroundSystem("storylet eligibility", "BQ-067",
                PlaygroundSupport.Production, "StoryletEngine.Find over the compiled bundle"),
            new PlaygroundSystem("cast chemistry", "BQ-068",
                PlaygroundSupport.Production, "StoryletChemistry, with its bounds reported by DescribeCasting"),
            new PlaygroundSystem("identity affordances", "BQ-145",
                PlaygroundSupport.Production, "IdentityAffordances.Of, over whatever facets the sandbox reports"),
            new PlaygroundSystem("personality, values, sensitivities, emotions", "BQ-011, BQ-062",
                PlaygroundSupport.Production, "the profiles on NarrativeNpc, read by Disclosure and ReactionDerivation"),
            new PlaygroundSystem("relationships and standing", "BQ-013",
                PlaygroundSupport.Production, "RelationshipGraph plus the obligation ledger"),
            new PlaygroundSystem("knowledge and belief", "BQ-063",
                PlaygroundSupport.Production, "KnowledgeGraph: nothing unbelieved is ever disclosed"),
            new PlaygroundSystem("world facts, events and history", "BQ-005",
                PlaygroundSupport.Production, "the event ledger every callback is derived from"),
            new PlaygroundSystem("semantic speech acts", "BQ-070",
                PlaygroundSupport.Production, "SpeechAct.Compose, which refuses rather than repairs"),
            new PlaygroundSystem("state-driven disclosure", "BQ-071",
                PlaygroundSupport.Production, "Disclosure.Decide, with its pressures and decisive terms"),
            new PlaygroundSystem("relationship-dependent depth", "BQ-072",
                PlaygroundSupport.Production, "DisclosureDepth and the limit that bound it"),
            new PlaygroundSystem("lying and evasion", "BQ-073",
                PlaygroundSupport.Production, "DisclosureTactic, Deception.Assess and Deception.Record"),
            new PlaygroundSystem("fragment realization", "BQ-074",
                PlaygroundSupport.Production, "DialogueRealizer over the shipped fragment library"),
            new PlaygroundSystem("occupational vocabulary", "BQ-076",
                PlaygroundSupport.Production, "OccupationalVocabulary.RequestedVocabulary"),
            new PlaygroundSystem("negative-space prohibitions", "BQ-077",
                PlaygroundSupport.Production, "NegativeSpaceProfile rulings, taken where the decision is taken"),
            new PlaygroundSystem("repetition control", "BQ-078",
                PlaygroundSupport.Production, "DialogueExpressionHistory, carried across the turns of one exchange"),
            new PlaygroundSystem("weirdness budget", "BQ-079",
                PlaygroundSupport.Production, "WeirdnessBudget at a fixed ceiling, so a contrast is not a dice roll"),
            new PlaygroundSystem("personality-revealing reactions", "BQ-080",
                PlaygroundSupport.Production, "ReactionDerivation.React over the actor's own interpretation"),
            new PlaygroundSystem("callback hooks", "BQ-081",
                PlaygroundSupport.Production, "CallbackHooks derived from the ledger, with the route gate intact"),
            new PlaygroundSystem("callback disclosure", "BQ-081 x BQ-071",
                PlaygroundSupport.Production, "CallbackDisclosure.Best: recall permission is never spent as telling permission"),
            new PlaygroundSystem("callback recurrence and context", "BQ-082",
                PlaygroundSupport.Production, "CallbackRecurrence against the scene's own thread and site"),
            new PlaygroundSystem("conversation state", "BQ-083",
                PlaygroundSupport.Production, "ConversationState: repeated questions, unanswered questions, contradictions"),
            new PlaygroundSystem("commitment promotion", "BQ-083",
                PlaygroundSupport.Production, "ConversationState.Commit, the one doorway into the obligation ledger"),

            new PlaygroundSystem("character identity facets", "BQ-144",
                PlaygroundSupport.SyntheticInput,
                "SandboxVanillaState answers the identity read; an unread facet stays unread rather than guessed"),
            new PlaygroundSystem("vanilla attributes, skills, affinity", "BQ-002",
                PlaygroundSupport.SyntheticInput, "the same IVanillaState contract the live adapter implements"),
            new PlaygroundSystem("check resolution", "BQ-008",
                PlaygroundSupport.SyntheticInput,
                "a preset that needs history scripts the outcome; no exchange resolves a check"),

            new PlaygroundSystem("voice profiles and idiolect", "BQ-075, BQ-142",
                PlaygroundSupport.LaboratoryAuthored,
                "nothing in Core assigns a VoiceProfile; --voice names one, and deriving it from a job or a race is the stereotype BQ-145 refuses"),
            new PlaygroundSystem("choosing to promise", "BQ-083",
                PlaygroundSupport.LaboratoryAuthored,
                "no Core system selects a Promise act or decides one is worth promoting; the third exchange composes it and Commit judges it"),

            new PlaygroundSystem("Elin Drama / dialogue projection", "BQ-036, BQ-088",
                PlaygroundSupport.RuntimeRequired,
                "the plugin projects procedural verbs into talk; no part of it consumes a SpeechAct or a RealizedLine"),
            new PlaygroundSystem("barks and overheard lines", "BQ-035",
                PlaygroundSupport.RuntimeRequired, "presentation decides a line was heard; Core stops at what would be said"),
            new PlaygroundSystem("native journal surface", "BQ-033",
                PlaygroundSupport.RuntimeRequired, "an Elin UI surface, patched at runtime"),
            new PlaygroundSystem("live identity intake", "BQ-144",
                PlaygroundSupport.RuntimeRequired, "reading a real Chara's source sheet and trait subclasses"),
            new PlaygroundSystem("vanilla check rows and dice", "BQ-008",
                PlaygroundSupport.RuntimeRequired, "ElinCheckResolver against the game's own tables")
        };

        public static IReadOnlyList<PlaygroundSystem> All => Systems;

        public static void Write(TextWriter output)
        {
            LabText.Header(output, "systems this playground exercises");
            output.WriteLine("Nothing in BQ-070 through BQ-083 is consumed by the plugin: the live half projects");
            output.WriteLine("procedural verbs and reads game state, and no part of it takes a speech act, a");
            output.WriteLine("disclosure decision or a realized line and puts it in front of a player. The last");
            output.WriteLine("thing this playground can honestly show is a line and its meaning.");

            Column(output, PlaygroundSupport.Production,
                "production logic, over state this world holds");
            Column(output, PlaygroundSupport.SyntheticInput,
                "production logic, fed by the headless sandbox seam");
            Column(output, PlaygroundSupport.LaboratoryAuthored,
                "no production authority yet - the laboratory chooses, and says so");
            Column(output, PlaygroundSupport.RuntimeRequired,
                "requires live Elin - not simulated, not mocked");
        }

        private static void Column(TextWriter output, PlaygroundSupport support, string heading)
        {
            output.WriteLine();
            output.WriteLine(Label(support) + " - " + heading);

            for (int i = 0; i < Systems.Length; i++)
            {
                if (Systems[i].Support != support)
                {
                    continue;
                }

                output.WriteLine("  " + LabText.Column(Systems[i].Name, 42)
                    + LabText.Column(Systems[i].Step, 14) + Systems[i].Note);
            }
        }

        public static string Label(PlaygroundSupport support)
        {
            switch (support)
            {
                case PlaygroundSupport.Production:
                    return "PRODUCTION";
                case PlaygroundSupport.SyntheticInput:
                    return "SYNTHETIC INPUT";
                case PlaygroundSupport.LaboratoryAuthored:
                    return "LABORATORY-AUTHORED";
                default:
                    return "PLUGIN ONLY";
            }
        }

        public static IReadOnlyList<PlaygroundSystem> WithSupport(PlaygroundSupport support)
        {
            List<PlaygroundSystem> found = new List<PlaygroundSystem>();
            for (int i = 0; i < Systems.Length; i++)
            {
                if (Systems[i].Support == support)
                {
                    found.Add(Systems[i]);
                }
            }

            return found;
        }
    }
}
