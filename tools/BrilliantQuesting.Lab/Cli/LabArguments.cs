using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// The tokens left after the command and scenario have been identified, split into
    /// <c>--name value</c> options and bare positional arguments.
    ///
    /// Parsing is deliberately small: <c>--name value</c>, <c>--name=value</c> and <c>--flag</c>.
    /// Anything else is a positional. Bad values raise <see cref="LabArgumentException"/> so every
    /// scenario reports them the same way and with the same exit status.
    /// </summary>
    public sealed class LabArguments
    {
        private static readonly IReadOnlyList<string> NoTokens = new string[0];

        private readonly Dictionary<string, string> _options;
        private readonly List<string> _positionals;

        private LabArguments(Dictionary<string, string> options, List<string> positionals, IReadOnlyList<string> tokens)
        {
            _options = options;
            _positionals = positionals;
            Tokens = tokens;
        }

        public static LabArguments Empty => Parse(NoTokens);

        /// <summary>The tokens exactly as given, for scenarios that forward their own arguments.</summary>
        public IReadOnlyList<string> Tokens { get; }

        public IReadOnlyList<string> Positionals => _positionals;

        /// <summary>Option names present, without the leading dashes.</summary>
        public IEnumerable<string> OptionNames => _options.Keys;

        public static LabArguments Parse(IReadOnlyList<string> tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> positionals = new List<string>();

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (token == null)
                {
                    continue;
                }

                if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
                {
                    positionals.Add(token);
                    continue;
                }

                string name = token.Substring(2);
                string value = null;
                int equals = name.IndexOf('=');
                if (equals >= 0)
                {
                    value = name.Substring(equals + 1);
                    name = name.Substring(0, equals);
                }
                else if (i + 1 < tokens.Count && !IsOption(tokens[i + 1]))
                {
                    value = tokens[i + 1];
                    i++;
                }

                if (name.Length == 0)
                {
                    throw new LabArgumentException("Missing option name in '" + token + "'.");
                }

                options[name] = value;
            }

            string[] copy = new string[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                copy[i] = tokens[i];
            }

            return new LabArguments(options, positionals, copy);
        }

        public bool Has(string name) => _options.ContainsKey(name);

        /// <summary>The raw value of an option, or null when it was absent or given as a bare flag.</summary>
        public string Value(string name)
        {
            return _options.TryGetValue(name, out string value) ? value : null;
        }

        public bool Flag(string name) => _options.ContainsKey(name);

        public string String(string name, string fallback)
        {
            if (!_options.TryGetValue(name, out string value))
            {
                return fallback;
            }

            return value ?? throw new LabArgumentException("Option --" + name + " needs a value.");
        }

        public int Int(string name, int fallback)
        {
            if (!_options.TryGetValue(name, out string raw))
            {
                return fallback;
            }

            return ParseInt(name, raw);
        }

        public ulong UInt64(string name, ulong fallback)
        {
            if (!_options.ContainsKey(name))
            {
                return fallback;
            }

            return ParseUInt64(name, _options[name]);
        }

        /// <summary>
        /// An option that may also be given as a bare argument, which is how the laboratory's
        /// historic flag forms passed their one number (<c>--questline-sweep 60</c>).
        /// </summary>
        public int IntOrPositional(string name, int index, int fallback)
        {
            if (_options.ContainsKey(name))
            {
                return ParseInt(name, _options[name]);
            }

            if (index < _positionals.Count)
            {
                return ParseInt(name, _positionals[index]);
            }

            return fallback;
        }

        public ulong UInt64OrPositional(string name, int index, ulong fallback)
        {
            if (_options.ContainsKey(name))
            {
                return ParseUInt64(name, _options[name]);
            }

            if (index < _positionals.Count)
            {
                return ParseUInt64(name, _positionals[index]);
            }

            return fallback;
        }

        private static bool IsOption(string token)
        {
            return token != null && token.Length > 2 && token.StartsWith("--", StringComparison.Ordinal);
        }

        private static int ParseInt(string name, string value)
        {
            if (value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            throw new LabArgumentException("Option --" + name + " needs a whole number, got '" + (value ?? string.Empty) + "'.");
        }

        private static ulong ParseUInt64(string name, string value)
        {
            if (value != null && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed))
            {
                return parsed;
            }

            throw new LabArgumentException("Option --" + name + " needs a non-negative whole number, got '" + (value ?? string.Empty) + "'.");
        }
    }
}
