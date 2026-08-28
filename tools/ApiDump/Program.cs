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

            PathAssemblyResolver resolver = new PathAssemblyResolver(
                assemblyPaths.Concat(RuntimeAssemblies()).ToArray());

            using MetadataLoadContext context = new MetadataLoadContext(resolver);
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
                            Console.WriteLine(assembly.GetName().Name + "  " + type.FullName
                                              + (type.BaseType != null ? " : " + type.BaseType.Name : string.Empty));
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
                Type type = loaded.SelectMany(SafeTypes).FirstOrDefault(t => t.Name == name || t.FullName == name);
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

            Type type = loaded.SelectMany(SafeTypes).FirstOrDefault(t => t.Name == args[0] || t.FullName == args[0]);
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

        private static string Describe(Type type)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("### ").Append(type.FullName);
            if (type.BaseType != null && type.BaseType.Name != "Object")
            {
                sb.Append(" : ").Append(type.BaseType.Name);
            }

            sb.Append('\n');
            foreach (string line in MemberLines(type))
            {
                sb.Append("  ").Append(line).Append('\n');
            }

            return sb.ToString();
        }

        private static IEnumerable<string> MemberLines(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (FieldInfo field in type.GetFields(Flags).OrderBy(f => f.Name))
            {
                yield return (field.IsStatic ? "static field " : "field  ") + Short(field.FieldType) + " " + field.Name;
            }

            foreach (PropertyInfo property in type.GetProperties(Flags).OrderBy(p => p.Name))
            {
                string accessors = (property.CanRead ? "get;" : string.Empty) + (property.CanWrite ? "set;" : string.Empty);
                yield return "prop   " + Short(property.PropertyType) + " " + property.Name + " { " + accessors + " }";
            }

            foreach (MethodInfo method in type.GetMethods(Flags).OrderBy(m => m.Name))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                string parameters = string.Join(", ", method.GetParameters().Select(p => Short(p.ParameterType) + " " + p.Name));
                yield return (method.IsStatic ? "static " : string.Empty) + "method " + Short(method.ReturnType)
                             + " " + method.Name + "(" + parameters + ")";
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
