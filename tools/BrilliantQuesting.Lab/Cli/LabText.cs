using System;
using System.Collections.Generic;
using System.IO;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// The laboratory's reporting conventions, in one place: a fixed rule width, section headers
    /// and the empty-list singletons scenarios use for their metadata.
    /// </summary>
    public static class LabText
    {
        /// <summary>Width of the rules the laboratory has always printed around section headers.</summary>
        public const int RuleWidth = 78;

        public static readonly IReadOnlyList<string> NoStrings = new string[0];

        /// <summary>Blank line, rule, upper-case title, rule - the laboratory's section break.</summary>
        public static void Header(TextWriter output, string title)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.WriteLine();
            Rule(output);
            output.WriteLine((title ?? string.Empty).ToUpperInvariant());
            Rule(output);
        }

        public static void Rule(TextWriter output)
        {
            output.WriteLine(new string('=', RuleWidth));
        }

        /// <summary>Pads a label so a column of them lines up, without truncating a long one.</summary>
        public static string Column(string label, int width)
        {
            label = label ?? string.Empty;
            return label.Length >= width ? label + " " : label.PadRight(width);
        }
    }
}
