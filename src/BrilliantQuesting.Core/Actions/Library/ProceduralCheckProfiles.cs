using BrilliantQuesting.Checks;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// The semantic checks the verb library uses, each composed out of vanilla Elin values.
    ///
    /// Note what is absent: there is no Deception skill, no Investigation skill, no Intimidation
    /// stat. Elin already has Negotiation, Charisma, Perception, Spot Hidden, Literacy and Anatomy,
    /// so a "deception check" is a particular way of reading those - not a parallel character
    /// sheet the player has to level separately.
    /// </summary>
    public static class ProceduralCheckProfiles
    {
        /// <summary>Lying, bluffing, denying. Spotted by a perceptive or strong-willed target.</summary>
        public static readonly CheckProfile Deception = new CheckProfile("proc_deception", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.4)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.25)
            .WithTargetAttribute(VanillaAttribute.Perception, 0.25)
            .WithTargetAttribute(VanillaAttribute.Will, 0.1);

        /// <summary>Asking for cooperation on the level.</summary>
        public static readonly CheckProfile Persuasion = new CheckProfile("proc_persuasion", 11)
            .WithActorSkill(VanillaSkill.Negotiation, 0.4)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.3)
            .WithTargetAttribute(VanillaAttribute.Will, 0.2);

        /// <summary>
        /// Leaning on someone. Strength counts as well as Charisma, so a mute bruiser has a social
        /// route that an eloquent weakling does not.
        /// </summary>
        public static readonly CheckProfile Intimidation = new CheckProfile("proc_intimidation", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.2)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.15)
            .WithActorAttribute(VanillaAttribute.Strength, 0.3)
            .WithTargetAttribute(VanillaAttribute.Will, 0.35)
            .WithTargetLevel(0.3);

        /// <summary>Pressing someone for what they know.</summary>
        public static readonly CheckProfile Interrogation = new CheckProfile("proc_interrogation", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.35)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.2)
            .WithActorAttribute(VanillaAttribute.Will, 0.15)
            .WithTargetAttribute(VanillaAttribute.Will, 0.3);

        /// <summary>Buying compliance. Wealth opens the door; Negotiation sets the price.</summary>
        public static readonly CheckProfile Bribery = new CheckProfile("proc_bribery", 10)
            .WithActorSkill(VanillaSkill.Negotiation, 0.35)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.15)
            .WithTargetAttribute(VanillaAttribute.Will, 0.25);

        /// <summary>Vanilla pickpocketing already contests Dexterity against target Perception.</summary>
        public static readonly CheckProfile Pickpocketing = new CheckProfile("proc_pickpocket", 13)
            .WithActorSkill(VanillaSkill.Pickpocket, 0.4)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.3)
            .WithTargetAttribute(VanillaAttribute.Perception, 0.35);

        /// <summary>Turning over a scene for what it can tell you.</summary>
        public static readonly CheckProfile Investigation = new CheckProfile("proc_investigation", 12)
            .WithActorSkill(VanillaSkill.SpotHidden, 0.4)
            .WithActorAttribute(VanillaAttribute.Perception, 0.3)
            .WithActorAttribute(VanillaAttribute.Learning, 0.1);

        /// <summary>Making a false thing look true, and the target's Literacy arguing back.</summary>
        public static readonly CheckProfile Fabrication = new CheckProfile("proc_fabrication", 14)
            .WithActorSkill(VanillaSkill.Literacy, 0.3)
            .WithActorSkill(VanillaSkill.Stealth, 0.2)
            .WithActorAttribute(VanillaAttribute.Learning, 0.2)
            .WithTargetAttribute(VanillaAttribute.Perception, 0.3);

        /// <summary>Being believed when you make a public claim.</summary>
        public static readonly CheckProfile Credibility = new CheckProfile("proc_credibility", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.3)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.25)
            .WithTargetAttribute(VanillaAttribute.Will, 0.2);
    }
}
