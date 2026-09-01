using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class StoryletContentTests
    {
        private static readonly string[] FirstFive =
        {
            "storylet.public_accusation",
            "storylet.private_confrontation",
            "storylet.request_for_help",
            "storylet.confession",
            "storylet.gossip"
        };

        [Fact]
        public void ShippedContentDefinesTheFirstFiveStoryletsAsBundleDefinitions()
        {
            string root = RepositoryRoot();
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(Path.Combine(root, "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);

            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<StoryletDefinition> definitions = StoryletContent.LoadDefinitions(bundle.Bundle, out diagnostics);

            Assert.Empty(diagnostics);
            Assert.Equal(FirstFive.Length, definitions.Count(d => FirstFive.Contains(d.Id)));
            foreach (string id in FirstFive)
            {
                StoryletDefinition definition = Assert.Single(definitions, d => d.Id == id);
                Assert.NotEmpty(definition.RequiredRoles);
                Assert.NotEmpty(definition.Beats);
            }

            string storyletRoot = Path.Combine(root, "content", "storylets");
            string[] authoredFiles = Directory.GetFiles(storyletRoot, "*.yaml");
            Assert.True(authoredFiles.Length >= FirstFive.Length);
        }

        [Fact]
        public void TheFirstFiveAuthoredStoryletsCanFireOnTheExistingTheft()
        {
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine engine = StoryletContent.CreateEngine(bundle.Bundle, out diagnostics);
            TheftLaboratory lab = TheftLaboratory.Create();

            IReadOnlyList<StoryletOpportunity> opportunities = engine.Find(
                lab.World,
                lab.Vanilla,
                lab.Situation.Thread,
                lab.Situation.WitnessId,
                lab.Situation.ThiefId,
                lab.Situation.TheftFactId);

            Assert.Empty(bundle.Diagnostics);
            Assert.Empty(diagnostics);
            Assert.Equal(FirstFive.OrderBy(id => id), opportunities.Select(o => o.Definition.Id).OrderBy(id => id));
        }

        [Fact]
        public void ASixthStoryletCompilesAndLoadsWithoutCodeChanges()
        {
            string temp = NewTempDirectory();
            string content = Path.Combine(temp, "content");
            string storylets = Path.Combine(content, "storylets");
            Directory.CreateDirectory(storylets);
            File.WriteAllText(Path.Combine(storylets, "sixth.yaml"),
                "id: storylet.sixth\r\n"
                + "kind: storylet\r\n"
                + "payload:\r\n"
                + "  requiredRoles:\r\n"
                + "    - id: speaker\r\n"
                + "      source: Actor\r\n"
                + "  preconditions:\r\n"
                + "    - kind: RoleAlive\r\n"
                + "      role: speaker\r\n"
                + "  beats:\r\n"
                + "    - open\r\n");

            string output = Path.Combine(temp, "content.bqc");
            CompilerRun run = RunCompiler(content, output);
            Assert.Equal(0, run.ExitCode);

            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(output);
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<StoryletDefinition> definitions = StoryletContent.LoadDefinitions(bundle.Bundle, out diagnostics);

            Assert.Empty(bundle.Diagnostics);
            Assert.Empty(diagnostics);
            StoryletDefinition sixth = Assert.Single(definitions);
            Assert.Equal("storylet.sixth", sixth.Id);
            Assert.Equal("speaker", Assert.Single(sixth.RequiredRoles).Id);
            Assert.Equal("open", Assert.Single(sixth.Beats).Id);
        }

        [Fact]
        public void StoryletReferencingAnUndefinedRoleFailsTheCompiler()
        {
            string temp = NewTempDirectory();
            string content = Path.Combine(temp, "content");
            string storylets = Path.Combine(content, "storylets");
            Directory.CreateDirectory(storylets);
            File.WriteAllText(Path.Combine(storylets, "bad.yaml"),
                "id: storylet.bad\r\n"
                + "kind: storylet\r\n"
                + "payload:\r\n"
                + "  requiredRoles:\r\n"
                + "    - id: speaker\r\n"
                + "      source: Actor\r\n"
                + "  preconditions:\r\n"
                + "    - kind: RoleAlive\r\n"
                + "      role: ghost\r\n"
                + "  beats:\r\n"
                + "    - open\r\n");

            CompilerRun run = RunCompiler(content, Path.Combine(temp, "content.bqc"));

            Assert.NotEqual(0, run.ExitCode);
            Assert.Contains("content.storylet.invalid", run.Error);
            Assert.Contains("undefined role: ghost", run.Error);
        }

        private static CompilerRun RunCompiler(string content, string output)
        {
            string root = RepositoryRoot();
            ProcessStartInfo start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("--project");
            start.ArgumentList.Add(Path.Combine(root, "tools", "ContentCompiler", "ContentCompiler.csproj"));
            start.ArgumentList.Add("--");
            start.ArgumentList.Add("--content");
            start.ArgumentList.Add(content);
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(output);

            using (Process process = Process.Start(start))
            {
                string outputText = process.StandardOutput.ReadToEnd();
                string errorText = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new CompilerRun(process.ExitCode, outputText, errorText);
            }
        }

        private static string NewTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "bq-storylet-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }

        private sealed class CompilerRun
        {
            public CompilerRun(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; }

            public string Output { get; }

            public string Error { get; }
        }
    }
}
