using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Maps the mod's small stat vocabulary onto Elin's element aliases, and resolves those
    /// aliases to element ids once at startup.
    ///
    /// UNVERIFIED. The alias strings below are the best reading available from metadata; the
    /// authoritative list lives in the game's Element source sheet. Anything that fails to resolve
    /// is logged by name and its capability is switched off, so a wrong guess degrades to a
    /// missing route rather than a silent zero that quietly makes every check trivial.
    ///
    /// Run the plugin once and read the log, or call <see cref="DumpKnownAliases"/>, then correct
    /// this table. It is deliberately the only place in the mod where these strings appear.
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
                { VanillaSkill.SpotHidden, "eye" },
                { VanillaSkill.Literacy, "literacy" },
                { VanillaSkill.Appraising, "identify" },
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

        private static bool TryResolveAlias(string alias, out int elementId)
        {
            elementId = 0;
            SourceElement.Row row = EClass.sources.elements.GetRow(alias);
            if (row == null)
            {
                return false;
            }

            elementId = row.id;
            return elementId != 0;
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
