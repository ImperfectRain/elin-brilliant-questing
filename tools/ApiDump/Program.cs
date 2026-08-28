using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BrilliantQuesting.ApiDump
{
    /// <summary>
    /// Prints the public surface of the game assemblies without executing a byte of them.
    ///
    /// The adapter has to be written against what Elin actually exposes on this build, not against
    /// what a wiki said last year. MetadataLoadContext reads the metadata only, so Unity assemblies
    /// that could never load on a Linux box are still fully inspectable here.
    ///
    ///     dotnet run --project tools/ApiDump -- --assemblies
    ///     dotnet run --project tools/ApiDump -- --find Chara Check Karma
    ///     dotnet run --project tools/ApiDump -- --type Chara --type Check
    /// </summary>
    public static class Program
    {
        private const string LibDirectory = "lib/Elin";

        /// <summary>Extra folders searched only to resolve references, never dumped.</summary>
        private static readonly string[] SupportDirectories =
        {
            "lib/BepInEx/BepInEx/core",
            "lib/Package/_ModdingKit"
        };

        public static int Main(string[] args)
        {
            string[] assemblyPaths = Directory.Exists(LibDirectory)
                ? Directory.GetFiles(LibDirectory, "*.dll")
                : Array.Empty<string>();

            if (assemblyPaths.Length == 0)
            {
                Console.Error.WriteLine("No assemblies in " + LibDirectory);
                return 1;
            }

            // The game ships its own mscorlib. It has to win over the host runtime's, or the
            // context refuses to load both under the same name - and it is the correct BCL to
            // resolve Unity-era signatures against anyway.
            Dictionary<string, string> searchPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in RuntimeAssemblies())
            {
                searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
            }

            foreach (string directory in SupportDirectories)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string path in Directory.GetFiles(directory, "*.dll"))
                {
                    searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }

            foreach (string path in assemblyPaths)
            {
                searchPaths[Path.GetFileNameWithoutExtension(path)] = path;
            }

            bool gameCoreLib = assemblyPaths.Any(p => Path.GetFileNameWithoutExtension(p) == "mscorlib");
            PathAssemblyResolver resolver = new PathAssemblyResolver(searchPaths.Values.ToArray());

            using MetadataLoadContext context = new MetadataLoadContext(
                resolver, gameCoreLib ? "mscorlib" : "System.Private.CoreLib");
            List<Assembly> loaded = new List<Assembly>();
            foreach (string path in assemblyPaths)
            {
                try
                {
                    loaded.Add(context.LoadFromAssemblyPath(path));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("could not load " + Path.GetFileName(path) + ": " + ex.Message);
                }
            }

            string mode = args.Length > 0 ? args[0] : "--assemblies";
            string[] rest = args.Skip(1).ToArray();

            switch (mode)
            {
                case "--assemblies": return Overview(loaded);
                case "--find": return Find(loaded, rest);
                case "--type": return Types(loaded, args.Where((a, i) => i > 0 && args[i - 1] == "--type").ToArray());
                case "--members": return Members(loaded, rest);
                default:
                    Console.Error.WriteLine("modes: --assemblies | --find <substring...> | --type <name> | --members <type> <substring>");
                    return 1;
            }
        }

        /// <summary>What is in the folder, how big its surface is, and what it depends on.</summary>
        private static int Overview(List<Assembly> loaded)
        {
            foreach (Assembly assembly in loaded)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                Console.WriteLine(assembly.GetName().Name + "  v" + assembly.GetName().Version
                                  + "  types: " + types.Length);

                foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(r => r.Name))
                {
                    Console.WriteLine("    -> " + reference.Name + " " + reference.Version);
                }

                if (types.Length > 0 && types.Length <= 40)
                {
                    foreach (Type type in types.OrderBy(t => t.FullName))
                    {
                        Console.WriteLine("    type " + type.FullName);
                    }
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
                    foreach (string needle in needles)
                    {
                        if (type.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Console.WriteLine(assembly.GetName().Name + "  " + type.FullName + BaseTypeSuffix(type));
                            break;
                        }
                    }
                }
            }

            return 0;
        }

        private static int Types(List<Assembly> loaded, string[] names)
        {
            foreach (string name in names)
            {
                Type type = Find(loaded, name);
                if (type == null)
                {
                    Console.WriteLine("### " + name + " - not found");
                    continue;
                }

                Console.WriteLine(Describe(type));
            }

            return 0;
        }

        /// <summary>Members of a type filtered by substring - for hunting one property in a huge class.</summary>
        private static int Members(List<Assembly> loaded, string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("--members <type> [substring...]");
                return 1;
            }

            Type type = Find(loaded, args[0]);
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

        /// <summary>
        /// Resolves a type name, preferring the game's own assembly. Several game types collide
        /// with BCL ones - Zone is both an Elin class and System.Security.Policy.Zone - and the
        /// BCL match is never the one anybody asking about Elin wants.
        /// </summary>
        private static Type Find(List<Assembly> loaded, string name)
        {
            List<Assembly> ordered = loaded
                .OrderBy(a => a.GetName().Name == "mscorlib" ? 2 : a.GetName().Name.StartsWith("UnityEngine") ? 1 : 0)
                .ToList();

            foreach (Assembly assembly in ordered)
            {
                Type match = SafeTypes(assembly).FirstOrDefault(t => t.FullName == name || t.Name == name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string Describe(Type type)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("### ").Append(type.FullName);
            sb.Append(BaseTypeSuffix(type));
            sb.Append('\n');
            foreach (string line in MemberLines(type))
            {
                sb.Append("  ").Append(line).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Members, with every signature decoded defensively.
        ///
        /// Not every assembly the game references is in lib/, and a signature that touches a
        /// missing one throws when it is read. That is worth one "(unresolved)" line, not a
        /// crashed dump: the other ninety members are still exactly what the adapter needs.
        /// </summary>
        private static IEnumerable<string> MemberLines(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (string line in Safely(type, t => t.GetFields(Flags).OrderBy(f => f.Name).Cast<MemberInfo>()))
            {
                yield return line;
            }

            foreach (string line in Safely(type, t => t.GetProperties(Flags).OrderBy(p => p.Name).Cast<MemberInfo>()))
            {
                yield return line;
            }

            foreach (string line in Safely(type, t => t.GetMethods(Flags).Where(m => !m.IsSpecialName).OrderBy(m => m.Name).Cast<MemberInfo>()))
            {
                yield return line;
            }
        }

        private static List<string> Safely(Type type, Func<Type, IEnumerable<MemberInfo>> select)
        {
            List<string> lines = new List<string>();
            IEnumerable<MemberInfo> members;
            try
            {
                members = select(type).ToList();
            }
            catch (Exception ex)
            {
                lines.Add("(unresolved member group: " + ex.GetType().Name + ")");
                return lines;
            }

            foreach (MemberInfo member in members)
            {
                try
                {
                    lines.Add(Render(member));
                }
                catch (Exception)
                {
                    lines.Add("(unresolved) " + member.MemberType.ToString().ToLowerInvariant() + " " + member.Name);
                }
            }

            return lines;
        }

        private static string Render(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo field:
                    return (field.IsStatic ? "static field " : "field  ") + Short(field.FieldType) + " " + field.Name;

                case PropertyInfo property:
                    string accessors = (property.CanRead ? "get;" : string.Empty) + (property.CanWrite ? "set;" : string.Empty);
                    return "prop   " + Short(property.PropertyType) + " " + property.Name + " { " + accessors + " }";

                case MethodInfo method:
                    string parameters = string.Join(", ", method.GetParameters().Select(p => Short(p.ParameterType) + " " + p.Name));
                    return (method.IsStatic ? "static " : string.Empty) + "method " + Short(method.ReturnType)
                           + " " + method.Name + "(" + parameters + ")";

                default:
                    return member.MemberType + " " + member.Name;
            }
        }

        private static string BaseTypeSuffix(Type type)
        {
            try
            {
                return type.BaseType != null && type.BaseType.Name != "Object" ? " : " + type.BaseType.Name : string.Empty;
            }
            catch (Exception)
            {
                return " : (unresolved base)";
            }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        private static string Short(Type type)
        {
            if (type == null)
            {
                return "?";
            }

            string name = type.Name;
            if (type.IsGenericType)
            {
                int tick = name.IndexOf('`');
                if (tick > 0)
                {
                    name = name.Substring(0, tick);
                }

                return name + "<" + string.Join(", ", type.GetGenericArguments().Select(Short)) + ">";
            }

            return name;
        }

        /// <summary>The BCL has to be resolvable or every signature comes back as a broken reference.</summary>
        private static IEnumerable<string> RuntimeAssemblies()
        {
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
            return runtime == null ? Array.Empty<string>() : Directory.GetFiles(runtime, "*.dll");
        }
    }
}
