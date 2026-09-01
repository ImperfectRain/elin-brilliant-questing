using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Content;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ContentBundleLoaderTests
    {
        [Fact]
        public void LoadsFlatVersionedBundleByStableId()
        {
            ContentBundleLoadResult result = ContentBundleLoader.LoadText(
                "{\"format\":\"brilliant-questing-content\",\"version\":1,\"records\":["
                + "{\"id\":\"storylet.public_accusation\",\"kind\":\"storylet\",\"payload\":{\"role\":\"accuser\"}},"
                + "{\"id\":\"act.accuse\",\"kind\":\"speechAct\",\"payload\":{}}"
                + "]}");

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.Bundle.Records.Count);
            Assert.True(result.Bundle.TryGet("storylet.public_accusation", out ContentRecord record));
            Assert.Equal("storylet", record.Kind);
            Assert.Equal("accuser", record.Payload.GetString("role"));
        }

        [Fact]
        public void TruncatedBundleDisablesContentWithDiagnostic()
        {
            ContentBundleLoadResult result = ContentBundleLoader.LoadText(
                "{\"format\":\"brilliant-questing-content\",\"version\":1,\"records\":[");

            Assert.Empty(result.Bundle.Records);
            ContentDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("content.bundle.malformed", diagnostic.Code);
            Assert.Contains("malformed", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UnknownVersionDisablesContentWithDiagnostic()
        {
            ContentBundleLoadResult result = ContentBundleLoader.LoadText(
                "{\"format\":\"brilliant-questing-content\",\"version\":99,\"records\":[]}");

            Assert.Empty(result.Bundle.Records);
            ContentDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("content.bundle.version", diagnostic.Code);
            Assert.Contains("99", diagnostic.Message);
        }

        [Fact]
        public void MissingFileDisablesContentWithDiagnostic()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "content.bqc");

            ContentBundleLoadResult result = ContentBundleLoader.LoadFile(path);

            Assert.Empty(result.Bundle.Records);
            ContentDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("content.bundle.missing", diagnostic.Code);
            Assert.Contains(path, diagnostic.Message);
        }

        [Fact]
        public void MalformedRecordsAreDisabledWithoutDroppingUsableContent()
        {
            ContentBundleLoadResult result = ContentBundleLoader.LoadText(
                "{\"format\":\"brilliant-questing-content\",\"version\":1,\"records\":["
                + "{\"id\":\"act.accuse\",\"kind\":\"speechAct\",\"payload\":{}},"
                + "{\"id\":\"act.accuse\",\"kind\":\"speechAct\",\"payload\":{}},"
                + "{\"id\":\"fragment.bad\",\"kind\":\"fragment\",\"payload\":null}"
                + "]}");

            Assert.Single(result.Bundle.Records);
            Assert.Equal(2, result.Diagnostics.Count);
            Assert.All(result.Diagnostics, d => Assert.Equal("content.record.invalid", d.Code));
            Assert.True(result.Bundle.TryGet("act.accuse", out _));
        }

        [Fact]
        public void ShippedCoreAssemblyReferencesNoSerializationPackage()
        {
            AssemblyName[] references = typeof(ContentBundleLoader).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(references, r => r.Name.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, r => r.Name.IndexOf("Yaml", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, r => r.Name.IndexOf("Newtonsoft", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, r => r.Name.IndexOf("System.Text.Json", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
