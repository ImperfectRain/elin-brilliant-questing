using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Maps the mod's small stat vocabulary onto Elin's element aliases, and resolves those
    /// aliases to element ids once at startup.
    ///
    /// VERIFIED against a live game on 28 Aug 2026 - every entry below resolved. The full table
    /// of all 1099 element aliases is recorded in docs/elin-element-aliases.md.
    ///
    /// The safety behaviour stays: anything that fails to resolve is logged by name and its
    /// capability is switched off, so a future rename degrades to a missing route rather than a
    /// silent zero that would quietly make every check trivial. This remains the only place in the
    /// mod where these strings appear.
    /// </summary>
    internal static class ElementAliases
    {
        private static readonly Dictionary<VanillaAttribute, string> AttributeAliases =
            new Dictionary<VanillaAttribute, string>
            {
                { VanillaAttribute.Strength, "STR" },
                { VanillaAttribute.Endurance, "END" },
                { VanillaAttribute.Dexterity, "DEX" },
                { VanillaAttribute.Perception, "PER" },
                { VanillaAttribute.Learning, "LER" },
                { VanillaAttribute.Will, "WIL" },
                { VanillaAttribute.Magic, "MAG" },
                { VanillaAttribute.Charisma, "CHA" }
            };

        private static readonly Dictionary<VanillaSkill, string> SkillAliases =
            new Dictionary<VanillaSkill, string>
            {
                { VanillaSkill.Negotiation, "negotiation" },
                { VanillaSkill.Investing, "investing" },
                { VanillaSkill.Pickpocket, "stealing" },
                { VanillaSkill.Stealth, "stealth" },
                { VanillaSkill.Lockpicking, "lockpicking" },
                { VanillaSkill.DisarmTrap, "disarmTrap" },
                { VanillaSkill.SpotHidden, "spotting" },
                { VanillaSkill.Literacy, "reading" },
                { VanillaSkill.Appraising, "appraising" },
                { VanillaSkill.Anatomy, "anatomy" },
                { VanillaSkill.Alchemy, "alchemy" },
                { VanillaSkill.Cooking, "cooking" },
                { VanillaSkill.Faith, "faith" },
                { VanillaSkill.Travel, "travel" },
                { VanillaSkill.Mining, "mining" }
            };

        private static readonly Dictionary<VanillaAttribute, int> ResolvedAttributes = new Dictionary<VanillaAttribute, int>();
        private static readonly Dictionary<VanillaSkill, int> ResolvedSkills = new Dictionary<VanillaSkill, int>();

        /// <summary>True once resolution has run and at least the attributes were found.</summary>
        internal static bool AttributesResolved { get; private set; }

        internal static bool SkillsResolved { get; private set; }

        /// <summary>
        /// Resolves every alias against the live source sheet. Called once, after sources import.
        /// </summary>
        internal static void Resolve(ManualLogSource log)
        {
            ResolvedAttributes.Clear();
            ResolvedSkills.Clear();

            List<string> missing = new List<string>();

            foreach (KeyValuePair<VanillaAttribute, string> pair in AttributeAliases)
            {
                if (TryResolveAlias(pair.Value, out int id))
                {
                    ResolvedAttributes[pair.Key] = id;
                }
                else
                {
                    missing.Add(pair.Key + " (\"" + pair.Value + "\")");
                }
            }

            foreach (KeyValuePair<VanillaSkill, string> pair in SkillAliases)
            {
                if (TryResolveAlias(pair.Value, out int id))
                {
                    ResolvedSkills[pair.Key] = id;
                }
                else
                {
                    missing.Add(pair.Key + " (\"" + pair.Value + "\")");
                }
            }

            AttributesResolved = ResolvedAttributes.Count == AttributeAliases.Count;
            SkillsResolved = ResolvedSkills.Count == SkillAliases.Count;

            if (missing.Count > 0)
            {
                log.LogWarning("Unresolved element aliases (" + missing.Count + "): " + string.Join(", ", missing.ToArray()));
                log.LogWarning("Those stats read as unavailable. Correct ElementAliases against the Element source sheet.");

                // Say whether the sheet was empty or the names were wrong. Those need opposite
                // fixes, and one line here beats another round of guessing.
                DumpDiagnostics(log);
            }
            else
            {
                log.LogInfo("Resolved all " + (ResolvedAttributes.Count + ResolvedSkills.Count) + " element aliases.");
            }
        }

        internal static bool TryGet(VanillaAttribute attribute, out int elementId)
        {
            return ResolvedAttributes.TryGetValue(attribute, out elementId);
        }

        internal static bool TryGet(VanillaSkill skill, out int elementId)
        {
            return ResolvedSkills.TryGetValue(skill, out elementId);
        }

        /// <summary>
        /// Resolves an alias to an element id.
        ///
        /// The lookup that matters is <c>SourceData.alias</c>, the dictionary the game builds from
        /// the Element sheet's alias column. An earlier version used <c>GetRow(string)</c>, which
        /// keys on the row id rather than the alias and therefore missed every single entry - the
        /// failure mode that produces "all 23 unresolved" rather than a few.
        ///
        /// Falls back to a case-insensitive scan of the rows, because an alias table is exactly
        /// the sort of thing whose capitalisation is not worth being defeated by.
        /// </summary>
        private static bool TryResolveAlias(string alias, out int elementId)
        {
            elementId = 0;
            SourceElement source = EClass.sources?.elements;
            if (source == null)
            {
                return false;
            }

            if (source.alias != null && source.alias.TryGetValue(alias, out SourceElement.Row row) && row != null)
            {
                elementId = row.id;
                return elementId != 0;
            }

            if (source.rows != null)
            {
                foreach (SourceElement.Row candidate in source.rows)
                {
                    if (candidate != null && string.Equals(candidate.alias, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        elementId = candidate.id;
                        return elementId != 0;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reports whether the sheet is loaded and what it actually calls things. An empty sheet
        /// and a wrong alias table look identical from the outside and need opposite fixes.
        /// </summary>
        private static void DumpDiagnostics(ManualLogSource log)
        {
            SourceElement source = EClass.sources?.elements;
            if (source == null)
            {
                log.LogWarning("EClass.sources.elements is null - the sheet is not loaded at this point in startup.");
                return;
            }

            int rowCount = source.rows?.Count ?? 0;
            int aliasCount = source.alias?.Count ?? 0;
            log.LogWarning("Element sheet: " + rowCount + " rows, " + aliasCount + " aliases indexed.");

            if (rowCount == 0)
            {
                log.LogWarning("No rows: this is a timing problem, not a naming one.");
                return;
            }

            // The whole table, chunked so no single line is unreadable. It only prints when
            // something failed to resolve, and it prints everything: a partial dump costs another
            // launch every time the missing name happens to fall outside the sample.
            List<string> line = new List<string>();
            int printed = 0;
            foreach (SourceElement.Row row in source.rows)
            {
                if (row == null || string.IsNullOrEmpty(row.alias))
                {
                    continue;
                }

                line.Add(row.id + ":" + row.alias);
                printed++;

                if (line.Count >= 40)
                {
                    log.LogWarning("aliases | " + string.Join(", ", line.ToArray()));
                    line.Clear();
                }
            }

            if (line.Count > 0)
            {
                log.LogWarning("aliases | " + string.Join(", ", line.ToArray()));
            }

            log.LogWarning("Dumped " + printed + " aliases. Copy the ones this mod needs into ElementAliases.");
        }

        /// <summary>
        /// Writes every element alias the game knows to the log. One launch with this turns the
        /// guesses above into facts.
        /// </summary>
        internal static void DumpKnownAliases(ManualLogSource log)
        {
            List<SourceElement.Row> rows = EClass.sources.elements.rows;
            log.LogInfo("--- element aliases (" + rows.Count + ") ---");
            foreach (SourceElement.Row row in rows)
            {
                if (!string.IsNullOrEmpty(row.alias))
                {
                    log.LogInfo(row.id + "\t" + row.alias);
                }
            }
        }
    }
}
