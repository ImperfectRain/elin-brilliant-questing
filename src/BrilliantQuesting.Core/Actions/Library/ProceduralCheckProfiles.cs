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

        /// <summary>
        /// Reading a body. `MD 10.2` names this one outright: Anatomy plus Learning plus
        /// Perception, with the state of the remains as the situational term.
        /// </summary>
        public static readonly CheckProfile Forensics = new CheckProfile("proc_forensics", 13)
            .WithActorSkill(VanillaSkill.Anatomy, 0.4)
            .WithActorAttribute(VanillaAttribute.Learning, 0.2)
            .WithActorAttribute(VanillaAttribute.Perception, 0.2);

        /// <summary>Getting a document to give up what it says.</summary>
        public static readonly CheckProfile Documents = new CheckProfile("proc_documents", 11)
            .WithActorSkill(VanillaSkill.Literacy, 0.45)
            .WithActorAttribute(VanillaAttribute.Learning, 0.25)
            .WithActorAttribute(VanillaAttribute.Perception, 0.1);

        /// <summary>
        /// Getting a document to give up what it says when it was written not to. Harder than
        /// <see cref="Documents"/> and leans on Learning rather than Perception, because a cipher
        /// or a dead script is a thing you work out, not a thing you notice.
        /// </summary>
        public static readonly CheckProfile Translation = new CheckProfile("proc_translation", 15)
            .WithActorSkill(VanillaSkill.Literacy, 0.35)
            .WithActorAttribute(VanillaAttribute.Learning, 0.35);

        /// <summary>Working out what a substance is, and therefore what it did.</summary>
        public static readonly CheckProfile SubstanceAnalysis = new CheckProfile("proc_substance", 13)
            .WithActorSkill(VanillaSkill.Alchemy, 0.4)
            .WithActorAttribute(VanillaAttribute.Learning, 0.2)
            .WithActorAttribute(VanillaAttribute.Perception, 0.15);

        /// <summary>Reading what a place still shows of what happened in it. `MD 10.2` tracking.</summary>
        public static readonly CheckProfile Tracking = new CheckProfile("proc_tracking", 12)
            .WithActorSkill(VanillaSkill.SpotHidden, 0.35)
            .WithActorSkill(VanillaSkill.Travel, 0.2)
            .WithActorAttribute(VanillaAttribute.Perception, 0.3);

        /// <summary>
        /// Staying with someone, or near them, without being the thing they notice. The one
        /// investigation profile that is contested, because the other side is a person.
        /// </summary>
        public static readonly CheckProfile Shadowing = new CheckProfile("proc_shadowing", 12)
            .WithActorSkill(VanillaSkill.Stealth, 0.4)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.2)
            .WithTargetAttribute(VanillaAttribute.Perception, 0.35);

        /// <summary>Holding two accounts side by side until one of them stops fitting.</summary>
        public static readonly CheckProfile Corroboration = new CheckProfile("proc_corroboration", 12)
            .WithActorSkill(VanillaSkill.Literacy, 0.15)
            .WithActorAttribute(VanillaAttribute.Learning, 0.35)
            .WithActorAttribute(VanillaAttribute.Perception, 0.25);

        /// <summary>Making a false thing look true, and the target's Literacy arguing back.</summary>
        public static readonly CheckProfile Fabrication = new CheckProfile("proc_fabrication", 14)
            .WithActorSkill(VanillaSkill.Literacy, 0.3)
            .WithActorSkill(VanillaSkill.Stealth, 0.2)
            .WithActorAttribute(VanillaAttribute.Learning, 0.2)
            .WithTargetAttribute(VanillaAttribute.Perception, 0.3);

        /// <summary>
        /// Getting past a lock, a shutter or a back door that is not yours.
        ///
        /// Uncontested, because a strongbox is not a person. The difficulty is the thing itself
        /// and whoever happens to be standing about, and both arrive as situational terms.
        /// </summary>
        public static readonly CheckProfile Burglary = new CheckProfile("proc_burglary", 13)
            .WithActorSkill(VanillaSkill.Lockpicking, 0.35)
            .WithActorSkill(VanillaSkill.Stealth, 0.25)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.2);

        /// <summary>
        /// Getting rid of something without leaving the getting-rid-of behind.
        ///
        /// Easier than <see cref="Sabotage"/>, because destroying a paper takes no craft at all -
        /// what takes some is being nowhere near it when anybody thinks to look.
        /// </summary>
        public static readonly CheckProfile CoveringTracks = new CheckProfile("proc_covering_tracks", 10)
            .WithActorSkill(VanillaSkill.Stealth, 0.4)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.2);

        /// <summary>Breaking the thing somebody depends on, in a way that is not obviously breaking.</summary>
        public static readonly CheckProfile Sabotage = new CheckProfile("proc_sabotage", 13)
            .WithActorSkill(VanillaSkill.Stealth, 0.25)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.3)
            .WithActorAttribute(VanillaAttribute.Learning, 0.15);

        /// <summary>
        /// Naming your price for staying quiet. Leans on the target's Will and their standing,
        /// because squeezing somebody with a great deal to lose is a different job to squeezing
        /// somebody with nothing.
        /// </summary>
        public static readonly CheckProfile Extortion = new CheckProfile("proc_extortion", 13)
            .WithActorSkill(VanillaSkill.Negotiation, 0.25)
            .WithActorAttribute(VanillaAttribute.Will, 0.2)
            .WithTargetAttribute(VanillaAttribute.Will, 0.4)
            .WithTargetLevel(0.2);

        /// <summary>
        /// Agreeing a price for something that cannot be sold over a counter. Appraising earns its
        /// place here: knowing what a thing is worth is the whole of not being cheated.
        /// </summary>
        public static readonly CheckProfile Fencing = new CheckProfile("proc_fencing", 11)
            .WithActorSkill(VanillaSkill.Negotiation, 0.3)
            .WithActorSkill(VanillaSkill.Appraising, 0.3)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.1);

        /// <summary>Putting a thing on a road nobody watches.</summary>
        public static readonly CheckProfile Smuggling = new CheckProfile("proc_smuggling", 12)
            .WithActorSkill(VanillaSkill.Stealth, 0.3)
            .WithActorSkill(VanillaSkill.Travel, 0.2)
            .WithActorSkill(VanillaSkill.Negotiation, 0.15);

        /// <summary>
        /// Making food out of what is to hand, to somebody else's standard.
        ///
        /// Elin already has Cooking, and this is the first thing in the library that reads it. The
        /// difficulty is what is being asked for, which arrives as a situational term, so cooking
        /// something ordinary and cooking something a physician would accept are the same craft at
        /// two difficulties rather than two crafts.
        /// </summary>
        public static readonly CheckProfile Cookery = new CheckProfile("proc_cookery", 10)
            .WithActorSkill(VanillaSkill.Cooking, 0.45)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.2)
            .WithActorAttribute(VanillaAttribute.Learning, 0.1);

        /// <summary>
        /// Fermenting and distilling. Elin has no brewing skill, so this is where its two
        /// neighbours meet: the kitchen work is Cooking and the chemistry is Alchemy, and reading
        /// both is nearer the truth than inventing a third.
        /// </summary>
        public static readonly CheckProfile Brewing = new CheckProfile("proc_brewing", 11)
            .WithActorSkill(VanillaSkill.Cooking, 0.25)
            .WithActorSkill(VanillaSkill.Alchemy, 0.25)
            .WithActorAttribute(VanillaAttribute.Learning, 0.2);

        /// <summary>
        /// Compounding a remedy to a standard somebody will be judged against.
        ///
        /// The making counterpart to <see cref="SubstanceAnalysis"/>, and harder: telling what is
        /// in a bottle is not the same job as getting a bottle to be worth drinking.
        /// </summary>
        public static readonly CheckProfile Compounding = new CheckProfile("proc_compounding", 13)
            .WithActorSkill(VanillaSkill.Alchemy, 0.45)
            .WithActorAttribute(VanillaAttribute.Learning, 0.25)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.1);

        /// <summary>Raising something that has to stand up. Elin's own Building skill.</summary>
        public static readonly CheckProfile Construction = new CheckProfile("proc_construction", 12)
            .WithActorSkill(VanillaSkill.Building, 0.4)
            .WithActorSkill(VanillaSkill.Carpentry, 0.2)
            .WithActorAttribute(VanillaAttribute.Strength, 0.15);

        /// <summary>
        /// Putting a broken thing back into service, which is a different skill from building one
        /// - working out what went wrong is most of it, so Learning weighs as heavily as the hands.
        /// </summary>
        public static readonly CheckProfile Repairs = new CheckProfile("proc_repairs", 12)
            .WithActorSkill(VanillaSkill.Carpentry, 0.35)
            .WithActorAttribute(VanillaAttribute.Learning, 0.25)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.15);

        /// <summary>
        /// Making a thing to somebody's specification when no named craft covers it. Elin's own
        /// Handicraft, which is exactly the generalist's skill.
        /// </summary>
        public static readonly CheckProfile Craftsmanship = new CheckProfile("proc_craftsmanship", 12)
            .WithActorSkill(VanillaSkill.Handicraft, 0.4)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.25)
            .WithActorAttribute(VanillaAttribute.Learning, 0.15);

        /// <summary>Being believed when you make a public claim.</summary>
        public static readonly CheckProfile Credibility = new CheckProfile("proc_credibility", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.3)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.25)
            .WithTargetAttribute(VanillaAttribute.Will, 0.2);

        /// <summary>
        /// The check a verb rolls, or null where it rolls none.
        ///
        /// The mapping lives here rather than beside each presentation surface so that the
        /// dialogue label, the debug inspector and the source-sheet rows all name the same check.
        /// A verb with no profile resolves without a roll and says so, which is a real answer to
        /// "what check runs?" rather than a gap.
        /// </summary>
        public static CheckProfile ForAction(string actionId)
        {
            switch (actionId)
            {
                case "question": return Interrogation;
                case "persuade": return Persuasion;
                case "lie": return Deception;
                case "intimidate": return Intimidation;
                case "bribe": return Bribery;
                case "search": return Investigation;
                case "inspect": return Investigation;
                case "examine_corpse": return Forensics;
                case "read": return Documents;
                case "search_records": return Documents;
                case "translate": return Translation;
                case "identify_substance": return SubstanceAnalysis;
                case "track": return Tracking;
                case "follow": return Shadowing;
                case "eavesdrop": return Shadowing;
                case "compare_testimony": return Corroboration;
                case "expose": return Credibility;
                case "pickpocket": return Pickpocketing;
                case "frame": return Fabrication;
                case "forge": return Fabrication;
                case "trespass": return Burglary;
                case "destroy_evidence": return CoveringTracks;
                case "sabotage": return Sabotage;
                case "extort": return Extortion;
                case "fence": return Fencing;
                case "smuggle": return Smuggling;

                case "cook": return Cookery;
                case "brew": return Brewing;
                case "alchemy": return Compounding;
                case "build": return Construction;
                case "repair": return Repairs;

                // The generalist. Not a fallback for the named crafts - a route in its own right,
                // for a demand no kitchen or building site covers, read off the skill Elin already
                // gives a jack-of-all-trades maker.
                case "craft_to_property": return Craftsmanship;

                // Passing yourself off as somebody else is not a separate craft from lying; it is
                // lying with a prop, and it is read off the same vanilla values.
                case "impersonate": return Deception;
                default: return null;
            }
        }
    }
}
