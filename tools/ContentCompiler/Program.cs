using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Storylets;

namespace BrilliantQuesting.ContentCompiler
{
    internal static class Program
    {
        private const string Format = "brilliant-questing-content";

        public static int Main(string[] args)
        {
            CompilerOptions options = CompilerOptions.Parse(args);
            if (!string.IsNullOrEmpty(options.Error))
            {
                Console.Error.WriteLine(options.Error);
                return 2;
            }

            try
            {
                CompileResult result = Compile(options.ContentRoot);
                string bundleText = result.Bundle.ToJson(indented: true) + Environment.NewLine;

                ContentBundleLoadResult loaded = ContentBundleLoader.LoadText(bundleText);
                if (loaded.Diagnostics.Count > 0)
                {
                    foreach (ContentDiagnostic diagnostic in loaded.Diagnostics)
                    {
                        Console.Error.WriteLine(diagnostic);
                    }

                    return 1;
                }

                // Fragment ids are unique across the whole library rather than within a file, so
                // the collision is only visible once every file is in one bundle.
                IReadOnlyList<ContentDiagnostic> fragmentDiagnostics;
                DialogueFragmentContent.LoadFragments(loaded.Bundle, out fragmentDiagnostics);
                if (fragmentDiagnostics.Count > 0)
                {
                    foreach (ContentDiagnostic diagnostic in fragmentDiagnostics)
                    {
                        Console.Error.WriteLine(diagnostic);
                    }

                    return 1;
                }

                if (options.Check)
                {
                    if (!File.Exists(options.OutputPath))
                    {
                        Console.Error.WriteLine("content.bundle.stale: " + options.OutputPath + " is missing.");
                        return 1;
                    }

                    string existing = File.ReadAllText(options.OutputPath);
                    if (!string.Equals(existing, bundleText, StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine("content.bundle.stale: " + options.OutputPath + " does not match content/.");
                        return 1;
                    }

                    Console.WriteLine("Content bundle is current: " + options.OutputPath);
                    return 0;
                }

                string directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(options.OutputPath, bundleText);
                Console.WriteLine("Wrote " + result.RecordCount + " content record(s) to " + options.OutputPath + ".");
                return 0;
            }
            catch (CompilerException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static CompileResult Compile(string contentRoot)
        {
            if (!Directory.Exists(contentRoot))
            {
                throw new CompilerException("content.root.missing: " + contentRoot + " does not exist.");
            }

            string[] files = Directory.GetFiles(contentRoot, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(contentRoot, "*.yml", SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            JsonValue records = JsonValue.Array();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in files)
            {
                SourceRecord source = SourceRecord.Read(file);
                if (!ids.Add(source.Id))
                {
                    throw new CompilerException(source.Location("content.id.duplicate: " + source.Id + " is already defined."));
                }

                ValidateSource(source);
                records.Add(JsonValue.Object()
                    .Set("id", source.Id)
                    .Set("kind", source.Kind)
                    .Set("payload", source.Payload));
            }

            JsonValue bundle = JsonValue.Object()
                .Set("format", Format)
                .Set("version", ContentBundle.CurrentVersion)
                .Set("records", records);

            return new CompileResult(bundle, files.Length);
        }

        /// <summary>
        /// A record whose kind has a reader is compiled through that reader, so a bad file fails
        /// here with a path rather than at load with a diagnostic nobody is watching.
        /// </summary>
        private static void ValidateSource(SourceRecord source)
        {
            ContentBundle bundle = new ContentBundle(
                ContentBundle.CurrentVersion,
                new[] { new ContentRecord(source.Id, source.Kind, source.Payload) });
            IReadOnlyList<ContentDiagnostic> diagnostics;
            if (string.Equals(source.Kind, "storylet", StringComparison.Ordinal))
            {
                StoryletContent.LoadDefinitions(bundle, out diagnostics);
            }
            else if (string.Equals(source.Kind, DialogueFragmentContent.Kind, StringComparison.Ordinal))
            {
                DialogueFragmentContent.LoadFragments(bundle, out diagnostics);
            }
            else
            {
                return;
            }

            if (diagnostics.Count > 0)
            {
                throw new CompilerException(source.Location(diagnostics[0].Code + ": " + diagnostics[0].Message));
            }
        }

        private sealed class CompileResult
        {
            public CompileResult(JsonValue bundle, int recordCount)
            {
                Bundle = bundle;
                RecordCount = recordCount;
            }

            public JsonValue Bundle { get; }

            public int RecordCount { get; }
        }

        private sealed class SourceRecord
        {
            private SourceRecord(string path, JsonValue root)
            {
                Path = path;
                Id = RequiredString(path, root, "id");
                Kind = RequiredString(path, root, "kind");
                Payload = root["payload"] ?? JsonValue.Object();
                if (Payload.Kind != JsonKind.Object)
                {
                    throw new CompilerException(Location("content.payload.invalid: payload must be a map."));
                }
            }

            public string Path { get; }

            public string Id { get; }

            public string Kind { get; }

            public JsonValue Payload { get; }

            public static SourceRecord Read(string path)
            {
                JsonValue root = SimpleYaml.ParseFile(path);
                if (root.Kind != JsonKind.Object)
                {
                    throw new CompilerException(path + ":1: content.record.invalid: record root must be a map.");
                }

                return new SourceRecord(path, root);
            }

            public string Location(string message)
            {
                return Path + ":1: " + message;
            }

            private static string RequiredString(string path, JsonValue root, string name)
            {
                string value = root.GetString(name, null);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new CompilerException(path + ":1: content." + name + ".missing: " + name + " is required.");
                }

                return value;
            }
        }

        private sealed class CompilerOptions
        {
            private CompilerOptions()
            {
                ContentRoot = "content";
                OutputPath = Path.Combine("Package", "content.bqc");
            }

            public string ContentRoot { get; private set; }

            public string OutputPath { get; private set; }

            public bool Check { get; private set; }

            public string Error { get; private set; }

            public static CompilerOptions Parse(string[] args)
            {
                CompilerOptions options = new CompilerOptions();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    switch (arg)
                    {
                        case "--content":
                            options.ContentRoot = RequireValue(args, ref i, arg, options);
                            break;
                        case "--output":
                            options.OutputPath = RequireValue(args, ref i, arg, options);
                            break;
                        case "--check":
                            options.Check = true;
                            break;
                        case "--help":
                            options.Error = "Usage: ContentCompiler [--content content] [--output Package/content.bqc] [--check]";
                            return options;
                        default:
                            options.Error = "Unknown option: " + arg;
                            return options;
                    }
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index, string option, CompilerOptions options)
            {
                if (index + 1 >= args.Length)
                {
                    options.Error = option + " requires a value.";
                    return "";
                }

                index++;
                return args[index];
            }
        }
    }

    internal sealed class CompilerException : Exception
    {
        public CompilerException(string message) : base(message)
        {
        }
    }

    internal static class SimpleYaml
    {
        public static JsonValue ParseFile(string path)
        {
            List<YamlLine> lines = File.ReadAllLines(path)
                .Select((text, index) => YamlLine.From(path, index + 1, text))
                .ToList();

            int indexRef = 0;
            SkipIgnored(lines, ref indexRef);
            if (indexRef >= lines.Count)
            {
                throw new CompilerException(path + ":1: yaml.empty: file is empty.");
            }

            JsonValue value = ParseBlock(lines, ref indexRef, lines[indexRef].Indent);
            SkipIgnored(lines, ref indexRef);
            if (indexRef < lines.Count)
            {
                throw new CompilerException(lines[indexRef].Location("yaml.indent.invalid: unexpected indentation."));
            }

            return value;
        }

        private static JsonValue ParseBlock(List<YamlLine> lines, ref int index, int indent)
        {
            SkipIgnored(lines, ref index);
            if (index >= lines.Count)
            {
                return JsonValue.Object();
            }

            return lines[index].Text.StartsWith("- ", StringComparison.Ordinal)
                ? ParseArray(lines, ref index, indent)
                : ParseObject(lines, ref index, indent);
        }

        private static JsonValue ParseObject(List<YamlLine> lines, ref int index, int indent)
        {
            JsonValue obj = JsonValue.Object();
            while (index < lines.Count)
            {
                SkipIgnored(lines, ref index);
                if (index >= lines.Count)
                {
                    break;
                }

                YamlLine line = lines[index];
                if (line.Indent < indent)
                {
                    break;
                }

                if (line.Indent > indent)
                {
                    throw new CompilerException(line.Location("yaml.indent.invalid: unexpected nested line."));
                }

                if (line.Text.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                KeyValue keyValue = SplitKeyValue(line);
                index++;
                obj.Set(keyValue.Key, ReadValue(lines, ref index, line, keyValue.Value));
            }

            return obj;
        }

        private static JsonValue ParseArray(List<YamlLine> lines, ref int index, int indent)
        {
            JsonValue array = JsonValue.Array();
            while (index < lines.Count)
            {
                SkipIgnored(lines, ref index);
                if (index >= lines.Count)
                {
                    break;
                }

                YamlLine line = lines[index];
                if (line.Indent < indent)
                {
                    break;
                }

                if (line.Indent != indent || !line.Text.StartsWith("- ", StringComparison.Ordinal))
                {
                    throw new CompilerException(line.Location("yaml.array.invalid: expected list item."));
                }

                string rest = line.Text.Substring(2).Trim();
                index++;
                if (rest.Length == 0)
                {
                    array.Add(ParseNested(lines, ref index, line));
                }
                else if (LooksLikeKeyValue(rest))
                {
                    JsonValue obj = JsonValue.Object();
                    KeyValue keyValue = SplitKeyValue(line.WithText(rest));
                    obj.Set(keyValue.Key, ReadValue(lines, ref index, line, keyValue.Value));

                    while (index < lines.Count && lines[index].Indent > indent)
                    {
                        if (lines[index].Indent != indent + 2)
                        {
                            throw new CompilerException(lines[index].Location("yaml.indent.invalid: list item map members must indent two spaces."));
                        }

                        KeyValue nested = SplitKeyValue(lines[index]);
                        YamlLine nestedLine = lines[index];
                        index++;
                        obj.Set(nested.Key, ReadValue(lines, ref index, nestedLine, nested.Value));
                    }

                    array.Add(obj);
                }
                else
                {
                    array.Add(ParseScalar(line, rest));
                }
            }

            return array;
        }

        private static JsonValue ReadValue(List<YamlLine> lines, ref int index, YamlLine line, string text)
        {
            if (string.Equals(text, "|", StringComparison.Ordinal))
            {
                return ReadBlockText(lines, ref index, line);
            }

            if (text.Length > 0)
            {
                return ParseScalar(line, text);
            }

            return ParseNested(lines, ref index, line);
        }

        private static JsonValue ParseNested(List<YamlLine> lines, ref int index, YamlLine parent)
        {
            SkipIgnored(lines, ref index);
            if (index >= lines.Count || lines[index].Indent <= parent.Indent)
            {
                return JsonValue.Object();
            }

            if (lines[index].Indent != parent.Indent + 2)
            {
                throw new CompilerException(lines[index].Location("yaml.indent.invalid: nested blocks must indent two spaces."));
            }

            return ParseBlock(lines, ref index, lines[index].Indent);
        }

        private static JsonValue ReadBlockText(List<YamlLine> lines, ref int index, YamlLine parent)
        {
            if (index >= lines.Count || lines[index].Indent <= parent.Indent)
            {
                return JsonValue.String("");
            }

            int blockIndent = lines[index].Indent;
            List<string> parts = new List<string>();
            while (index < lines.Count && (lines[index].Raw.Length == 0 || lines[index].Indent >= blockIndent))
            {
                YamlLine line = lines[index];
                parts.Add(line.Raw.Length >= blockIndent ? line.Raw.Substring(blockIndent) : "");
                index++;
            }

            return JsonValue.String(string.Join("\n", parts));
        }

        private static void SkipIgnored(List<YamlLine> lines, ref int index)
        {
            while (index < lines.Count && lines[index].IsIgnored)
            {
                index++;
            }
        }

        private static JsonValue ParseScalar(YamlLine line, string text)
        {
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                return JsonValue.String(Unquote(line, text.Substring(1, text.Length - 2)));
            }

            if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
            {
                return JsonValue.String(text.Substring(1, text.Length - 2).Replace("''", "'"));
            }

            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                return JsonValue.Bool(true);
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                return JsonValue.Bool(false);
            }

            if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
            {
                return JsonValue.Null();
            }

            double number;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return JsonValue.Number(number);
            }

            return JsonValue.String(text);
        }

        private static string Unquote(YamlLine line, string text)
        {
            return text
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private static KeyValue SplitKeyValue(YamlLine line)
        {
            int colon = line.Text.IndexOf(':');
            if (colon <= 0)
            {
                throw new CompilerException(line.Location("yaml.map.invalid: expected key: value."));
            }

            string key = line.Text.Substring(0, colon).Trim();
            if (key.Length == 0)
            {
                throw new CompilerException(line.Location("yaml.key.invalid: key is empty."));
            }

            return new KeyValue(key, line.Text.Substring(colon + 1).Trim());
        }

        private static bool LooksLikeKeyValue(string text)
        {
            int colon = text.IndexOf(':');
            return colon > 0 && text.Substring(0, colon).IndexOf(' ') < 0;
        }

        private sealed class KeyValue
        {
            public KeyValue(string key, string value)
            {
                Key = key;
                Value = value;
            }

            public string Key { get; }

            public string Value { get; }
        }

        private sealed class YamlLine
        {
            private YamlLine(string path, int number, int indent, string raw, string text)
            {
                Path = path;
                Number = number;
                Indent = indent;
                Raw = raw;
                Text = text;
            }

            public string Path { get; }

            public int Number { get; }

            public int Indent { get; }

            public string Raw { get; }

            public string Text { get; }

            public bool IsIgnored => Text.Length == 0 || Text.StartsWith("#", StringComparison.Ordinal);

            public static YamlLine From(string path, int number, string raw)
            {
                string trimmedRaw = raw.TrimEnd();
                string withoutComment = StripComment(trimmedRaw);

                int indent = 0;
                while (indent < trimmedRaw.Length && trimmedRaw[indent] == ' ')
                {
                    indent++;
                }

                if (indent < trimmedRaw.Length && trimmedRaw[indent] == '\t')
                {
                    throw new CompilerException(path + ":" + number + ": yaml.indent.invalid: tabs are not supported.");
                }

                if (indent % 2 != 0)
                {
                    throw new CompilerException(path + ":" + number + ": yaml.indent.invalid: indentation must use two spaces.");
                }

                string text = withoutComment.Length >= indent ? withoutComment.Substring(indent).TrimEnd() : "";
                return new YamlLine(path, number, indent, trimmedRaw, text);
            }

            public YamlLine WithText(string text)
            {
                return new YamlLine(Path, Number, Indent, Raw, text);
            }

            public string Location(string message)
            {
                return Path + ":" + Number + ": " + message;
            }

            private static string StripComment(string raw)
            {
                bool inSingle = false;
                bool inDouble = false;
                for (int i = 0; i < raw.Length; i++)
                {
                    char c = raw[i];
                    if (c == '"' && !inSingle)
                    {
                        inDouble = !inDouble;
                    }
                    else if (c == '\'' && !inDouble)
                    {
                        inSingle = !inSingle;
                    }
                    else if (c == '#' && !inSingle && !inDouble)
                    {
                        return raw.Substring(0, i).TrimEnd();
                    }
                }

                return raw.TrimEnd();
            }
        }
    }
}
