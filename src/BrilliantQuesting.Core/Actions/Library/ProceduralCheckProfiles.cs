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

        /// <summary>
        /// Asking a god for something in their gift.
        ///
        /// Uncontested, and deliberately so: a god is not a person you roll against, and whether
        /// this one will hear you at all is settled by who you follow and what you have laid on
        /// their ground before the dice come out. What is left for the check is how well the
        /// asking goes, which is Elin's own Faith skill, the Will to ask for something large, and
        /// the Magic that a granted power runs through.
        /// </summary>
        public static readonly CheckProfile Devotion = new CheckProfile("proc_devotion", 13)
            .WithActorSkill(VanillaSkill.Faith, 0.45)
            .WithActorAttribute(VanillaAttribute.Will, 0.2)
            .WithActorAttribute(VanillaAttribute.Magic, 0.15);

        /// <summary>
        /// Putting a matter to a body you belong to and getting it taken on.
        ///
        /// Uncontested for the same reason the devotional profile is: a guild officer is not
        /// somebody the member is rolling against, and whether the guild will hear the ask at all
        /// is settled by membership, rank and the size of what is being asked before the dice come
        /// out. What is left for the check is how the asking goes - putting a case to people who
        /// already know you, which is Elin's own Negotiation, the Charisma under it, and the Will
        /// to press a hall that would rather be doing something else.
        /// </summary>
        public static readonly CheckProfile GuildStanding = new CheckProfile("proc_guild_standing", 12)
            .WithActorSkill(VanillaSkill.Negotiation, 0.4)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.2)
            .WithActorAttribute(VanillaAttribute.Will, 0.15);

        /// <summary>Moving a physical obstruction by strength and stamina.</summary>
        public static readonly CheckProfile Clearing = new CheckProfile("proc_clearing", 12)
            .WithActorAttribute(VanillaAttribute.Strength, 0.35)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.3)
            .WithActorSkill(VanillaSkill.Mining, 0.15);

        /// <summary>Making a way around stone with Elin's own mining skill.</summary>
        public static readonly CheckProfile MiningBypass = new CheckProfile("proc_mine_bypass", 13)
            .WithActorSkill(VanillaSkill.Mining, 0.45)
            .WithActorAttribute(VanillaAttribute.Strength, 0.2)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.15);

        /// <summary>Forcing a barrier where subtle access is not the problem.</summary>
        public static readonly CheckProfile Breaking = new CheckProfile("proc_breaking", 13)
            .WithActorAttribute(VanillaAttribute.Strength, 0.45)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.2);

        /// <summary>Getting a heavy or awkward thing under control.</summary>
        public static readonly CheckProfile Carrying = new CheckProfile("proc_carrying", 11)
            .WithActorAttribute(VanillaAttribute.Strength, 0.35)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.25);

        /// <summary>Moving something through the world without losing or ruining it.</summary>
        public static readonly CheckProfile Transport = new CheckProfile("proc_transport", 11)
            .WithActorSkill(VanillaSkill.Travel, 0.3)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.25)
            .WithActorAttribute(VanillaAttribute.Strength, 0.15);

        /// <summary>Getting someone out of danger by reaching and moving them.</summary>
        public static readonly CheckProfile Rescue = new CheckProfile("proc_rescue", 12)
            .WithActorAttribute(VanillaAttribute.Strength, 0.25)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.25)
            .WithActorSkill(VanillaSkill.Travel, 0.15);

        /// <summary>Keeping someone moving with you through a bad route.</summary>
        public static readonly CheckProfile Escort = new CheckProfile("proc_escort", 11)
            .WithActorSkill(VanillaSkill.Travel, 0.3)
            .WithActorAttribute(VanillaAttribute.Endurance, 0.2)
            .WithActorAttribute(VanillaAttribute.Will, 0.15);

        /// <summary>Taking control of a resisting person.</summary>
        public static readonly CheckProfile Capture = new CheckProfile("proc_capture", 13)
            .WithActorAttribute(VanillaAttribute.Strength, 0.3)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.2)
            .WithTargetAttribute(VanillaAttribute.Dexterity, 0.25)
            .WithTargetLevel(0.25);

        /// <summary>Holding somebody in place once close enough.</summary>
        public static readonly CheckProfile Restrain = new CheckProfile("proc_restrain", 12)
            .WithActorAttribute(VanillaAttribute.Strength, 0.25)
            .WithActorAttribute(VanillaAttribute.Dexterity, 0.25)
            .WithTargetAttribute(VanillaAttribute.Strength, 0.25);

        /// <summary>
        /// Getting somebody to come under your roof, and getting your household to have them.
        ///
        /// Contested by the target's Will because the person being offered a bed is the one who
        /// decides: a frightened witness is not an object to be placed, and a specialist with a
        /// trade has somewhere else to be. Charisma and Negotiation are Elin's own reading of
        /// whether a stranger's word is worth trusting.
        /// </summary>
        public static readonly CheckProfile Hospitality = new CheckProfile("proc_hospitality", 11)
            .WithActorAttribute(VanillaAttribute.Charisma, 0.35)
            .WithActorSkill(VanillaSkill.Negotiation, 0.25)
            .WithActorAttribute(VanillaAttribute.Will, 0.1)
            .WithTargetAttribute(VanillaAttribute.Will, 0.25);

        /// <summary>
        /// Standing a watch over somebody, using the settlement rather than your own arms.
        ///
        /// Uncontested: the person being guarded is not resisting, and whoever they are hiding
        /// from is not in the room. What decides it is whether the watch is organised and whether
        /// the place is the kind of place that can keep one - the second of which arrives as the
        /// Home's own Public Safety, not as a term on this row.
        /// </summary>
        public static readonly CheckProfile Vigilance = new CheckProfile("proc_vigilance", 12)
            .WithActorAttribute(VanillaAttribute.Will, 0.3)
            .WithActorAttribute(VanillaAttribute.Perception, 0.2)
            .WithActorSkill(VanillaSkill.Negotiation, 0.2);

        /// <summary>
        /// Getting a settlement's own stores where somebody needs them, in time.
        ///
        /// Not a craft: nothing is made, and the difficulty is entirely in the standard demanded
        /// and in how well the place is run. Learning and Negotiation are what a person organising
        /// a shipment reads off, and Travel is the road it has to cover.
        /// </summary>
        public static readonly CheckProfile Logistics = new CheckProfile("proc_logistics", 11)
            .WithActorAttribute(VanillaAttribute.Learning, 0.25)
            .WithActorSkill(VanillaSkill.Negotiation, 0.3)
            .WithActorSkill(VanillaSkill.Travel, 0.15);

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

                // Asking is the only half of the faith family that can go wrong. Laying goods on
                // an altar is not a skill test - either you have something to give and the god is
                // yours, or you do not - so `make_offering` deliberately has no profile at all.
                case "invoke_blessing": return Devotion;

                // The other petition. Whether the hall will hear it is settled before the dice,
                // exactly as the god's is; what the roll decides is how the asking goes.
                case "invoke_authority": return GuildStanding;

                case "clear_obstruction": return Clearing;
                case "mine_bypass": return MiningBypass;
                case "break_barrier": return Breaking;
                case "carry": return Carrying;
                case "transport": return Transport;
                case "rescue": return Rescue;
                case "escort": return Escort;
                case "capture": return Capture;
                case "restrain": return Restrain;

                case "shelter": return Hospitality;
                case "recruit_specialist": return Hospitality;
                case "host": return Hospitality;
                case "assign_protection": return Vigilance;
                case "provide_supplies": return Logistics;
                case "buy_supplies": return null;
                case "invest_in_supplier": return null;

                // Putting something into your own household's keeping is not a skill test. Either
                // there is a Home with somebody in it to hold the thing, or there is not, and both
                // of those are settled before any roll would happen - the same reason
                // `make_offering` has no profile.

                // Passing yourself off as somebody else is not a separate craft from lying; it is
                // lying with a prop, and it is read off the same vanilla values.
                case "impersonate": return Deception;
                default: return null;
            }
        }
    }
}
