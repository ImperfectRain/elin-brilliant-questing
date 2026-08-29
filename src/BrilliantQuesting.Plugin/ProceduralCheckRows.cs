using System;
using BepInEx.Logging;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Registers procedural checks with Elin's Check sheet so Drama can render native difficulty
    /// wording through Check.GetText. The full composite math still lives in CheckProfile and the
    /// portable resolver; these rows are the single-element projection vanilla Check understands.
    /// </summary>
    internal static class ProceduralCheckRows
    {
        internal static void Install(ManualLogSource log)
        {
            SourceCheck source = EClass.sources?.checks;
            if (source == null)
            {
                log.LogWarning("Procedural Check rows skipped: EClass.sources.checks is not loaded.");
                return;
            }

            int installed = 0;
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Deception,
                ActorSkill(VanillaSkill.Negotiation, 0.4f),
                TargetAttribute(VanillaAttribute.Perception, 0.25f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Persuasion,
                ActorSkill(VanillaSkill.Negotiation, 0.4f),
                TargetAttribute(VanillaAttribute.Will, 0.2f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Intimidation,
                ActorAttribute(VanillaAttribute.Strength, 0.3f),
                TargetAttribute(VanillaAttribute.Will, 0.35f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Interrogation,
                ActorSkill(VanillaSkill.Negotiation, 0.35f),
                TargetAttribute(VanillaAttribute.Will, 0.3f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Bribery,
                ActorSkill(VanillaSkill.Negotiation, 0.35f),
                TargetAttribute(VanillaAttribute.Will, 0.25f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Pickpocketing,
                ActorSkill(VanillaSkill.Pickpocket, 0.4f),
                TargetAttribute(VanillaAttribute.Perception, 0.35f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Investigation,
                ActorSkill(VanillaSkill.SpotHidden, 0.4f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Fabrication,
                ActorSkill(VanillaSkill.Literacy, 0.3f),
                TargetAttribute(VanillaAttribute.Perception, 0.3f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Credibility,
                ActorSkill(VanillaSkill.Negotiation, 0.3f),
                TargetAttribute(VanillaAttribute.Will, 0.2f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Forensics,
                ActorSkill(VanillaSkill.Anatomy, 0.4f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Documents,
                ActorSkill(VanillaSkill.Literacy, 0.45f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Translation,
                ActorSkill(VanillaSkill.Literacy, 0.35f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.SubstanceAnalysis,
                ActorSkill(VanillaSkill.Alchemy, 0.4f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Tracking,
                ActorSkill(VanillaSkill.SpotHidden, 0.35f),
                ElementProjection.None);
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Shadowing,
                ActorSkill(VanillaSkill.Stealth, 0.4f),
                TargetAttribute(VanillaAttribute.Perception, 0.35f));
            installed += InstallRow(
                log, source, ProceduralCheckProfiles.Corroboration,
                ActorAttribute(VanillaAttribute.Learning, 0.35f),
                ElementProjection.None);

            log.LogInfo("Installed " + installed + " procedural Check row(s) for native difficulty text.");
        }

        private static int InstallRow(
            ManualLogSource log,
            SourceCheck source,
            CheckProfile profile,
            ElementProjection actor,
            ElementProjection target)
        {
            if (!actor.TryResolve(out int actorElement))
            {
                log.LogWarning("Procedural Check row '" + profile.Id + "' skipped: actor element is unavailable.");
                return 0;
            }

            int targetElement = 0;
            if (target.Kind != ProjectionKind.None && !target.TryResolve(out targetElement))
            {
                log.LogWarning("Procedural Check row '" + profile.Id + "' skipped: target element is unavailable.");
                return 0;
            }

            try
            {
                SourceCheck.Row row = source.CreateRow();
                row.id = profile.Id;
                row.baseDC = profile.BaseDifficulty;
                row.dice = profile.Dice;
                row.critRange = profile.CritRange;
                row.fumbleRange = profile.FumbleRange;
                row.element = actorElement;
                row.subFactor = actor.Factor;
                row.targetElement = targetElement;
                row.targetSubFactor = target.Factor;
                row.lvMod = (float)profile.TargetLevelWeight;
                source.SetRow(row);

                Check check = Check.Get(profile.Id, 0);
                if (check == null)
                {
                    log.LogWarning("Procedural Check row '" + profile.Id + "' was inserted but Check.Get could not read it.");
                    return 0;
                }

                log.LogInfo("Procedural Check row '" + profile.Id + "' available through Check.Get.");
                return 1;
            }
            catch (Exception ex)
            {
                log.LogWarning("Procedural Check row '" + profile.Id + "' failed: " + ex.GetType().Name + ": " + ex.Message);
                return 0;
            }
        }

        private static ElementProjection ActorSkill(VanillaSkill skill, float factor)
        {
            return new ElementProjection(ProjectionKind.Skill, skill, default, factor);
        }

        private static ElementProjection ActorAttribute(VanillaAttribute attribute, float factor)
        {
            return new ElementProjection(ProjectionKind.Attribute, default, attribute, factor);
        }

        private static ElementProjection TargetAttribute(VanillaAttribute attribute, float factor)
        {
            return new ElementProjection(ProjectionKind.Attribute, default, attribute, factor);
        }

        private readonly struct ElementProjection
        {
            internal static readonly ElementProjection None = new ElementProjection(ProjectionKind.None, default, default, 0f);

            internal ElementProjection(ProjectionKind kind, VanillaSkill skill, VanillaAttribute attribute, float factor)
            {
                Kind = kind;
                Skill = skill;
                Attribute = attribute;
                Factor = factor;
            }

            internal ProjectionKind Kind { get; }

            internal VanillaSkill Skill { get; }

            internal VanillaAttribute Attribute { get; }

            internal float Factor { get; }

            internal bool TryResolve(out int elementId)
            {
                if (Kind == ProjectionKind.Skill)
                {
                    return ElementAliases.TryGet(Skill, out elementId);
                }

                if (Kind == ProjectionKind.Attribute)
                {
                    return ElementAliases.TryGet(Attribute, out elementId);
                }

                elementId = 0;
                return true;
            }
        }

        private enum ProjectionKind
        {
            None,
            Skill,
            Attribute
        }
    }
}
