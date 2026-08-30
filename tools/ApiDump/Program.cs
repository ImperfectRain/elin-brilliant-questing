using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace BrilliantQuesting.ApiDump
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Options options = Options.Parse(args);
            if (options.Help)
            {
                Usage();
                return 0;
            }

            if (options.Mode == "--source-index")
            {
                return SourceIndex(options);
            }

            return AssemblyMode(options);
        }

        private static int AssemblyMode(Options options)
        {
            List<string> assemblyPaths = ResolveAssemblyPaths(options);
            if (assemblyPaths.Count == 0)
            {
                Console.Error.WriteLine("No assemblies found. Pass --game-root, --managed, or --assembly.");
                return 1;
            }

            Dictionary<string, string> searchPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in RuntimeAssemblies())
            {
                searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
            }

            foreach (string directory in options.SupportDirectories.Where(Directory.Exists))
            {
                foreach (string path in Directory.GetFiles(directory, "*.dll"))
                {
                    searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }

            foreach (string path in assemblyPaths)
            {
                searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
            }

            bool gameCoreLib = assemblyPaths.Any(p => Path.GetFileNameWithoutExtension(p).Equals("mscorlib", StringComparison.OrdinalIgnoreCase));
            PathAssemblyResolver resolver = new PathAssemblyResolver(searchPaths.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            using MetadataLoadContext context = new MetadataLoadContext(resolver, gameCoreLib ? "mscorlib" : "System.Private.CoreLib");

            List<Assembly> loaded = new List<Assembly>();
            foreach (string path in assemblyPaths)
            {
                try
                {
                    loaded.Add(context.LoadFromAssemblyPath(path));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("could not load " + path + ": " + ex.Message);
                }
            }

            switch (options.Mode)
            {
                case "--assemblies":
                    return Overview(loaded);
                case "--find":
                    return Find(loaded, options.Values);
                case "--type":
                    return Types(loaded, options.Values);
                case "--members":
                    return Members(loaded, options.Values);
                case "--index":
                    return MetadataIndex(loaded, options);
                default:
                    Console.Error.WriteLine("Unknown mode " + options.Mode);
                    Usage();
                    return 1;
            }
        }

        private static List<string> ResolveAssemblyPaths(Options options)
        {
            List<string> paths = new List<string>();
            foreach (string path in options.Assemblies)
            {
                if (File.Exists(path))
                {
                    paths.Add(Path.GetFullPath(path));
                }
            }

            foreach (string directory in options.AssemblyDirectories.Where(Directory.Exists))
            {
                paths.AddRange(Directory.GetFiles(directory, "*.dll").Select(Path.GetFullPath));
            }

            if (paths.Count == 0 && Directory.Exists(options.GameRoot))
            {
                string managed = Path.Combine(options.GameRoot, "Elin_Data", "Managed");
                if (Directory.Exists(managed))
                {
                    paths.AddRange(Directory.GetFiles(managed, "*.dll").Select(Path.GetFullPath));
                }

                options.SupportDirectories.Add(Path.Combine(options.GameRoot, "BepInEx", "core"));
                options.SupportDirectories.Add(Path.Combine(options.GameRoot, "Package", "_ModdingKit"));
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int Overview(List<Assembly> loaded)
        {
            foreach (Assembly assembly in loaded.OrderBy(a => a.GetName().Name))
            {
                Type[] types = SafeTypes(assembly).ToArray();
                Console.WriteLine(assembly.GetName().Name + "  v" + assembly.GetName().Version + "  types: " + types.Length);
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(r => r.Name))
                {
                    Console.WriteLine("    -> " + reference.Name + " " + reference.Version);
                }
                Console.WriteLine();
            }

            return 0;
        }

        private static int Find(List<Assembly> loaded, string[] needles)
        {
            foreach (Assembly assembly in loaded)
            {
                foreach (Type type in SafeTypes(assembly).OrderBy(t => t.FullName))
                {
                    if (needles.Length == 0 || needles.Any(n => type.FullName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Console.WriteLine(assembly.GetName().Name + "  " + type.FullName + BaseTypeSuffix(type));
                    }
                }
            }

            return 0;
        }

        private static int Types(List<Assembly> loaded, string[] names)
        {
            foreach (string name in names)
            {
                Type type = FindType(loaded, name);
                Console.WriteLine(type == null ? "### " + name + " - not found" : Describe(type));
            }

            return 0;
        }

        private static int Members(List<Assembly> loaded, string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("--members <type> [substring...]");
                return 1;
            }

            Type type = FindType(loaded, args[0]);
            if (type == null)
            {
                Console.WriteLine("### " + args[0] + " - not found");
                return 1;
            }

            string[] needles = args.Skip(1).ToArray();
            Console.WriteLine("### " + type.FullName);
            foreach (string line in MemberLines(type))
            {
                if (needles.Length == 0 || needles.Any(n => line.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Console.WriteLine("  " + line);
                }
            }

            return 0;
        }

        private static int MetadataIndex(List<Assembly> loaded, Options options)
        {
            MetadataDocument document = new MetadataDocument
            {
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Assemblies = loaded.OrderBy(a => a.GetName().Name).Select(AssemblyRecord.From).ToList()
            };

            foreach (Assembly assembly in loaded.OrderBy(a => a.GetName().Name))
            {
                foreach (Type type in SafeTypes(assembly).OrderBy(t => t.FullName))
                {
                    document.Types.Add(TypeRecord.From(assembly, type));
                }
            }

            WriteJson(options.JsonOutput, document);
            if (!string.IsNullOrEmpty(options.TextOutput))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.TextOutput)) ?? ".");
                File.WriteAllLines(options.TextOutput, document.Types.Select(t => t.SearchLine()));
            }

            Console.WriteLine("Indexed " + document.Types.Count + " types from " + document.Assemblies.Count + " assemblies.");
            return 0;
        }

        private static int SourceIndex(Options options)
        {
            string sourceRoot = options.SourceRoot;
            if (string.IsNullOrEmpty(sourceRoot) && Directory.Exists(options.GameRoot))
            {
                string exportRoot = Path.Combine(options.GameRoot, "SourceExport");
                sourceRoot = Directory.Exists(exportRoot)
                    ? Directory.GetDirectories(exportRoot).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault()
                    : null;
            }

            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                Console.Error.WriteLine("No SourceExport directory found. Pass --source-root.");
                return 1;
            }

            SourceDocument document = new SourceDocument
            {
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                SourceRoot = Path.GetFullPath(sourceRoot),
                SourceVersion = Path.GetFileName(sourceRoot)
            };

            string[] wanted = options.Values.Length == 0
                ? Directory.GetFiles(sourceRoot, "*.csv").Select(Path.GetFileNameWithoutExtension).ToArray()
                : options.Values;

            HashSet<string> wantedSet = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(sourceRoot, "*.csv").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string sheet = Path.GetFileNameWithoutExtension(file);
                if (!wantedSet.Contains(sheet))
                {
                    continue;
                }

                SheetRecord record = SheetRecord.Read(file, document.SourceVersion);
                document.Sheets.Add(record);
            }

            WriteJson(options.JsonOutput, document);
            if (!string.IsNullOrEmpty(options.TextOutput))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.TextOutput)) ?? ".");
                using StreamWriter writer = new StreamWriter(options.TextOutput, false, Encoding.UTF8);
                foreach (SheetRecord sheet in document.Sheets)
                {
                    writer.WriteLine("# " + sheet.Name + " (" + sheet.RowCount + " rows)");
                    foreach (Dictionary<string, string> row in sheet.Rows.Take(options.TextLimit))
                    {
                        writer.WriteLine(sheet.SearchLine(row));
                    }
                    writer.WriteLine();
                }
            }

            Console.WriteLine("Indexed " + document.Sheets.Count + " SourceData sheet(s) from " + sourceRoot + ".");
            return 0;
        }

        private static void WriteJson<T>(string path, T value)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string Describe(Type type)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("### ").Append(type.FullName).Append(BaseTypeSuffix(type)).Append('\n');
            foreach (string line in MemberLines(type))
            {
                sb.Append("  ").Append(line).Append('\n');
            }
            return sb.ToString();
        }

        private static IEnumerable<string> MemberLines(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (MemberInfo member in SafeMembers(type, t => t.GetFields(Flags).OrderBy(m => m.Name).Cast<MemberInfo>()))
            {
                yield return Render(member);
            }
            foreach (MemberInfo member in SafeMembers(type, t => t.GetProperties(Flags).OrderBy(m => m.Name).Cast<MemberInfo>()))
            {
                yield return Render(member);
            }
            foreach (MemberInfo member in SafeMembers(type, t => t.GetMethods(Flags).Where(m => !m.IsSpecialName).OrderBy(m => m.Name).Cast<MemberInfo>()))
            {
                yield return Render(member);
            }
        }

        private static IEnumerable<MemberInfo> SafeMembers(Type type, Func<Type, IEnumerable<MemberInfo>> select)
        {
            try
            {
                return select(type).ToList();
            }
            catch
            {
                return Array.Empty<MemberInfo>();
            }
        }

        private static string Render(MemberInfo member)
        {
            try
            {
                switch (member)
                {
                    case FieldInfo field:
                        return Visibility(field) + (field.IsStatic ? " static" : " instance") + " field " + Short(field.FieldType) + " " + field.Name + Attributes(field);
                    case PropertyInfo property:
                        MethodInfo getter = property.GetMethod;
                        MethodInfo setter = property.SetMethod;
                        MethodInfo accessor = getter ?? setter;
                        return Visibility(accessor) + (accessor != null && accessor.IsStatic ? " static" : " instance") + " property " + Short(property.PropertyType) + " " + property.Name + " { " + (getter != null ? "get; " : "") + (setter != null ? "set; " : "") + "}" + Attributes(property);
                    case MethodInfo method:
                        string parameters = string.Join(", ", method.GetParameters().Select(p => Short(p.ParameterType) + " " + p.Name));
                        return Visibility(method) + (method.IsStatic ? " static" : " instance") + " method " + Short(method.ReturnType) + " " + method.Name + "(" + parameters + ")" + Attributes(method);
                    default:
                        return member.MemberType + " " + member.Name;
                }
            }
            catch
            {
                return "(unresolved) " + member.MemberType.ToString().ToLowerInvariant() + " " + member.Name;
            }
        }

        private static Type FindType(List<Assembly> loaded, string name)
        {
            return loaded
                .OrderBy(a => a.GetName().Name == "mscorlib" ? 2 : a.GetName().Name.StartsWith("UnityEngine") ? 1 : 0)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == name || t.Name == name);
        }

        private static string BaseTypeSuffix(Type type)
        {
            try { return type.BaseType != null && type.BaseType.Name != "Object" ? " : " + type.BaseType.Name : string.Empty; }
            catch { return " : (unresolved base)"; }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        private static string Short(Type type)
        {
            if (type == null) return "?";
            if (type.IsByRef) return Short(type.GetElementType()) + "&";
            if (type.IsArray) return Short(type.GetElementType()) + "[]";
            string name = type.Name;
            if (type.IsGenericType)
            {
                int tick = name.IndexOf('`');
                if (tick > 0) name = name.Substring(0, tick);
                return name + "<" + string.Join(", ", type.GetGenericArguments().Select(Short)) + ">";
            }
            return name;
        }

        private static string Visibility(MethodBase method)
        {
            if (method == null) return "unknown";
            if (method.IsPublic) return "public";
            if (method.IsFamily) return "protected";
            if (method.IsAssembly) return "internal";
            if (method.IsPrivate) return "private";
            return "nonpublic";
        }

        private static string Visibility(FieldInfo field)
        {
            if (field.IsPublic) return "public";
            if (field.IsFamily) return "protected";
            if (field.IsAssembly) return "internal";
            if (field.IsPrivate) return "private";
            return "nonpublic";
        }

        private static string Attributes(MemberInfo member)
        {
            try
            {
                string[] names = member.GetCustomAttributesData().Select(a => a.AttributeType.Name).OrderBy(n => n).ToArray();
                return names.Length == 0 ? string.Empty : " [" + string.Join(", ", names) + "]";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IEnumerable<string> RuntimeAssemblies()
        {
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
            return runtime == null ? Array.Empty<string>() : Directory.GetFiles(runtime, "*.dll");
        }

        private static void Usage()
        {
            Console.WriteLine("ApiDump --game-root <Elin> --index --json <file> --text <file>");
            Console.WriteLine("ApiDump --source-index --source-root <SourceExport/version> --json <file> --text <file> [SourceElement SourceThing ...]");
            Console.WriteLine("ApiDump --assemblies|--find|--type|--members retain the original text modes.");
        }

        private sealed class Options
        {
            public string Mode = "--assemblies";
            public string GameRoot;
            public string SourceRoot;
            public string JsonOutput;
            public string TextOutput;
            public int TextLimit = 200;
            public bool Help;
            public readonly List<string> Assemblies = new List<string>();
            public readonly List<string> AssemblyDirectories = new List<string>();
            public readonly List<string> SupportDirectories = new List<string>();
            public string[] Values = Array.Empty<string>();

            public static Options Parse(string[] args)
            {
                Options options = new Options();
                List<string> values = new List<string>();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    switch (arg)
                    {
                        case "-h":
                        case "--help": options.Help = true; break;
                        case "--assemblies":
                        case "--find":
                        case "--type":
                        case "--members":
                        case "--index":
                        case "--source-index": options.Mode = arg; break;
                        case "--game-root": options.GameRoot = args[++i]; break;
                        case "--source-root": options.SourceRoot = args[++i]; break;
                        case "--json": options.JsonOutput = args[++i]; break;
                        case "--text": options.TextOutput = args[++i]; break;
                        case "--text-limit": options.TextLimit = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--assembly": options.Assemblies.Add(args[++i]); break;
                        case "--assembly-dir": options.AssemblyDirectories.Add(args[++i]); break;
                        case "--support-dir": options.SupportDirectories.Add(args[++i]); break;
                        default: values.Add(arg); break;
                    }
                }
                options.Values = values.ToArray();
                return options;
            }
        }

        private sealed class MetadataDocument
        {
            public string GeneratedUtc { get; set; }
            public List<AssemblyRecord> Assemblies { get; set; } = new List<AssemblyRecord>();
            public List<TypeRecord> Types { get; set; } = new List<TypeRecord>();
        }

        private sealed class AssemblyRecord
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public int TypeCount { get; set; }
            public List<string> References { get; set; }
            public static AssemblyRecord From(Assembly assembly)
            {
                return new AssemblyRecord
                {
                    Name = assembly.GetName().Name,
                    Version = assembly.GetName().Version?.ToString(),
                    TypeCount = SafeTypes(assembly).Count(),
                    References = assembly.GetReferencedAssemblies().Select(a => a.Name + " " + a.Version).OrderBy(s => s).ToList()
                };
            }
        }

        private sealed class TypeRecord
        {
            public string Assembly { get; set; }
            public string Namespace { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public string BaseType { get; set; }
            public List<string> Interfaces { get; set; } = new List<string>();
            public List<string> Attributes { get; set; } = new List<string>();
            public List<MemberRecord> Members { get; set; } = new List<MemberRecord>();

            public static TypeRecord From(Assembly assembly, Type type)
            {
                TypeRecord record = new TypeRecord
                {
                    Assembly = assembly.GetName().Name,
                    Namespace = type.Namespace ?? string.Empty,
                    Name = type.Name,
                    FullName = type.FullName ?? type.Name,
                    BaseType = Safe(() => type.BaseType?.FullName),
                    Interfaces = Safe(() => type.GetInterfaces().Select(i => i.FullName ?? i.Name).OrderBy(s => s).ToList()) ?? new List<string>(),
                    Attributes = Safe(() => type.GetCustomAttributesData().Select(a => a.AttributeType.Name).OrderBy(s => s).ToList()) ?? new List<string>()
                };
                const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                record.Members.AddRange(SafeMembers(type, t => t.GetFields(Flags).OrderBy(m => m.Name).Cast<MemberInfo>()).Select(MemberRecord.From));
                record.Members.AddRange(SafeMembers(type, t => t.GetProperties(Flags).OrderBy(m => m.Name).Cast<MemberInfo>()).Select(MemberRecord.From));
                record.Members.AddRange(SafeMembers(type, t => t.GetMethods(Flags).Where(m => !m.IsSpecialName).OrderBy(m => m.Name).Cast<MemberInfo>()).Select(MemberRecord.From));
                return record;
            }

            public string SearchLine()
            {
                return FullName + " : " + BaseType + " [" + Assembly + "] :: " + string.Join(" | ", Members.Select(m => m.SearchLine()).Take(50));
            }
        }

        private sealed class MemberRecord
        {
            public string Kind { get; set; }
            public string Name { get; set; }
            public string Visibility { get; set; }
            public bool Static { get; set; }
            public string Type { get; set; }
            public string ReturnType { get; set; }
            public List<string> Parameters { get; set; } = new List<string>();
            public List<string> Attributes { get; set; } = new List<string>();

            public static MemberRecord From(MemberInfo member)
            {
                try
                {
                    if (member is FieldInfo field)
                    {
                        return new MemberRecord { Kind = "field", Name = field.Name, Visibility = Visibility(field), Static = field.IsStatic, Type = Short(field.FieldType), Attributes = AttributeNames(field) };
                    }
                    if (member is PropertyInfo property)
                    {
                        MethodInfo accessor = property.GetMethod ?? property.SetMethod;
                        return new MemberRecord { Kind = "property", Name = property.Name, Visibility = Visibility(accessor), Static = accessor != null && accessor.IsStatic, Type = Short(property.PropertyType), Attributes = AttributeNames(property) };
                    }
                    MethodInfo method = (MethodInfo)member;
                    return new MemberRecord
                    {
                        Kind = "method",
                        Name = method.Name,
                        Visibility = Visibility(method),
                        Static = method.IsStatic,
                        ReturnType = Short(method.ReturnType),
                        Parameters = method.GetParameters().Select(p => Short(p.ParameterType) + " " + p.Name).ToList(),
                        Attributes = AttributeNames(method)
                    };
                }
                catch
                {
                    return new MemberRecord { Kind = member.MemberType.ToString(), Name = member.Name };
                }
            }

            public string SearchLine()
            {
                return Kind + " " + (Type ?? ReturnType) + " " + Name + "(" + string.Join(", ", Parameters) + ")";
            }
        }

        private sealed class SourceDocument
        {
            public string GeneratedUtc { get; set; }
            public string SourceRoot { get; set; }
            public string SourceVersion { get; set; }
            public List<SheetRecord> Sheets { get; set; } = new List<SheetRecord>();
        }

        private sealed class SheetRecord
        {
            public string Name { get; set; }
            public string SourceVersion { get; set; }
            public int RowCount { get; set; }
            public List<string> Columns { get; set; } = new List<string>();
            public List<Dictionary<string, string>> Rows { get; set; } = new List<Dictionary<string, string>>();

            public static SheetRecord Read(string file, string sourceVersion)
            {
                List<string[]> parsed = Csv.Read(file).ToList();
                string[] columns = parsed.Count == 0 ? Array.Empty<string>() : parsed[0];
                SheetRecord sheet = new SheetRecord
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    SourceVersion = sourceVersion,
                    Columns = columns.ToList()
                };
                foreach (string[] values in parsed.Skip(1))
                {
                    Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < columns.Length; i++)
                    {
                        row[columns[i]] = i < values.Length ? values[i] : string.Empty;
                    }
                    sheet.Rows.Add(row);
                }
                sheet.RowCount = sheet.Rows.Count;
                return sheet;
            }

            public string SearchLine(Dictionary<string, string> row)
            {
                string id = First(row, "id", "idMain", "alias", "name");
                string name = First(row, "name", "text", "alias", "id");
                string tags = First(row, "tag", "tags", "category", "type", "race", "job", "hobby", "faith");
                return Name + " row " + id + " | " + name + " | " + tags;
            }

            private static string First(Dictionary<string, string> row, params string[] names)
            {
                foreach (string name in names)
                {
                    if (row.TryGetValue(name, out string value) && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
                return string.Empty;
            }
        }

        private static class Csv
        {
            public static IEnumerable<string[]> Read(string path)
            {
                using StreamReader reader = new StreamReader(path, Encoding.UTF8, true);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return Parse(line).ToArray();
                }
            }

            private static List<string> Parse(string line)
            {
                List<string> fields = new List<string>();
                StringBuilder current = new StringBuilder();
                bool quote = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (quote)
                    {
                        if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else if (c == '"')
                        {
                            quote = false;
                        }
                        else
                        {
                            current.Append(c);
                        }
                    }
                    else if (c == ',')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else if (c == '"')
                    {
                        quote = true;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                fields.Add(current.ToString());
                return fields;
            }
        }

        private static T Safe<T>(Func<T> func)
        {
            try { return func(); }
            catch { return default; }
        }

        private static List<string> AttributeNames(MemberInfo member)
        {
            return Safe(() => member.GetCustomAttributesData().Select(a => a.AttributeType.Name).OrderBy(n => n).ToList()) ?? new List<string>();
        }
    }
}
