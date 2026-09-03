using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>The four people a playground run may put in a chair, by the name a caller types.</summary>
    public static class PlaygroundRoles
    {
        public const string Player = "player";
        public const string Witness = "witness";
        public const string Thief = "thief";
        public const string Victim = "victim";

        public static IReadOnlyList<string> All { get; } = new[] { Player, Witness, Thief, Victim };
    }

    /// <summary>
    /// One deterministic world for the conversation playground, with nothing invented in it.
    ///
    /// The situation is <see cref="TheftLaboratory"/>'s - the same three-NPC theft every other
    /// laboratory scenario runs over - and the storylets and wordings are the compiled bundle the
    /// mod ships. A stage therefore holds no content of its own: it resolves the four people a
    /// caller can name, hands out the production engines, and is otherwise a seed and a clock.
    ///
    /// <b>It builds state; it never decides an outcome.</b> Nothing on this type produces a speech
    /// act, a disclosure decision, a permit or a line. Those all come from Core, through
    /// <see cref="PlaygroundExchange"/>, which is what keeps the playground from becoming a second
    /// dialogue engine sitting beside the one it is meant to make legible.
    /// </summary>
    public sealed class PlaygroundStage
    {
        private readonly TheftLaboratory _lab;
        private DialogueCast _cast;

        private PlaygroundStage(
            TheftLaboratory lab,
            StoryletEngine storylets,
            DialogueRealizer realizer,
            ulong seed)
        {
            _lab = lab;
            Storylets = storylets;
            Realizer = realizer;
            Seed = seed;
        }

        public ulong Seed { get; }

        public StoryletEngine Storylets { get; }

        public DialogueRealizer Realizer { get; }

        public NarrativeWorldState World => _lab.World;

        public SandboxVanillaState Vanilla => _lab.Vanilla;

        public TheftLaboratory Lab => _lab;

        public GameTime Now => _lab.Vanilla.Now;

        public EntityId Player => _lab.Player;

        public EntityId Zone => _lab.Zone;

        public EntityId Witness => _lab.Situation.WitnessId;

        public EntityId Thief => _lab.Situation.ThiefId;

        public EntityId Victim => _lab.Situation.VictimId;

        /// <summary>The claim every preset is about, so two presets differ in state and not in subject.</summary>
        public EntityId SubjectFactId => _lab.Situation.TheftFactId;

        public Fact Subject => World.Knowledge.GetFact(SubjectFactId);

        public PettyTheftSituation Situation => _lab.Situation;

        /// <summary>
        /// The names wording may use, taken from the registry once. Rebuilt on demand rather than
        /// cached at construction because a preset may still be adding people when it runs.
        /// </summary>
        public DialogueCast Cast => _cast ?? (_cast = DialogueCast.From(World, Player, Witness, Thief, Victim));

        /// <summary>
        /// The whole stack around one theft, plus the shipped bundle.
        ///
        /// A bundle that will not load, or a storylet or fragment the compiler would have rejected,
        /// is a failure rather than a quieter run: a playground that silently lost half the fragment
        /// library would report "nothing in the library says this" about a content bug.
        /// </summary>
        public static PlaygroundStage Open(ulong seed)
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(BundlePath());
            Require(loaded.Diagnostics, "content bundle");

            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine storylets = StoryletContent.CreateEngine(loaded.Bundle, out diagnostics);
            Require(diagnostics, "storylets");

            DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(loaded.Bundle, out diagnostics);
            Require(diagnostics, "dialogue fragments");

            return new PlaygroundStage(
                TheftLaboratory.Create(seed), storylets, new DialogueRealizer(library), seed);
        }

        public NarrativeNpc Npc(EntityId id) => World.Registry.GetNpc(id);

        public string NameOf(EntityId id)
        {
            string name = World.Registry.NameOf(id);
            return string.IsNullOrEmpty(name) ? id.Value : name;
        }

        /// <summary>The id behind a role name, or <see cref="EntityId.None"/> for a name nobody holds.</summary>
        public EntityId Resolve(string role)
        {
            if (role == null)
            {
                return EntityId.None;
            }

            switch (role.Trim().ToLowerInvariant())
            {
                case PlaygroundRoles.Player:
                    return Player;
                case PlaygroundRoles.Witness:
                    return Witness;
                case PlaygroundRoles.Thief:
                    return Thief;
                case PlaygroundRoles.Victim:
                    return Victim;
                default:
                    return EntityId.None;
            }
        }

        /// <summary>The role name for somebody on this stage, or an empty string for anybody else.</summary>
        public string RoleOf(EntityId id)
        {
            if (id == Player)
            {
                return PlaygroundRoles.Player;
            }

            if (id == Witness)
            {
                return PlaygroundRoles.Witness;
            }

            if (id == Thief)
            {
                return PlaygroundRoles.Thief;
            }

            return id == Victim ? PlaygroundRoles.Victim : string.Empty;
        }

        /// <summary>Who this person is and what role they hold, for a report line.</summary>
        public string Describe(EntityId id)
        {
            string role = RoleOf(id);
            return role.Length == 0 ? NameOf(id) : NameOf(id) + " (" + role + ")";
        }

        private static void Require(IReadOnlyList<ContentDiagnostic> diagnostics, string what)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "The playground cannot run against broken " + what + ": " + diagnostics[0]);
        }

        /// <summary>
        /// The compiled bundle beside the solution file. Walked for rather than taken relative to
        /// the working directory so the playground runs the same from the repository root, from the
        /// project directory and from a test binary's output folder.
        /// </summary>
        private static string BundlePath()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                DirectoryInfo directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                    {
                        return Path.Combine(directory.FullName, "Package", "content.bqc");
                    }

                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException(
                "Could not find Package/content.bqc: no ElinBrilliantQuesting.sln above the working directory.");
        }
    }
}
