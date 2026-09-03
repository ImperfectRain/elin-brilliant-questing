using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// A neutral firsthand witness: they saw it, they know the person asking a little, and nothing
    /// is pressing on them either way.
    ///
    /// The baseline the other presets are read against. It changes almost nothing, because the
    /// theft laboratory's own witness already is this - the one thing added is an ordinary
    /// acquaintance with the person asking, so that "no relationship at all" is not silently doing
    /// the work of "an indifferent relationship".
    /// </summary>
    internal sealed class NeutralWitnessPreset : PlaygroundPreset
    {
        public override string Id => "neutral-witness";

        public override string Summary => "somebody who saw it, asked by an ordinary acquaintance";

        public override string Description =>
            "The theft laboratory's own witness, with a plain acquaintance to the person asking so that\n"
            + "an indifferent tie is a tie rather than an absence. Read the other presets against this\n"
            + "one: everything they change is stated in their own description.";

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Acquaintance, 15, mutual: true);
        }
    }

    /// <summary>
    /// The same firsthand knowledge, and somebody they have no reason to help.
    ///
    /// Distrust, wariness and present fear, all read from state <c>Disclosure</c> already weighs:
    /// the relationship pressure turns against speaking, the fear pressure joins it, and the
    /// standing that buys particulars is gone.
    /// </summary>
    internal sealed class HostileWitnessPreset : PlaygroundPreset
    {
        public override string Id => "hostile-witness";

        public override string Summary => "the same firsthand knowledge, and no reason to give it to this listener";

        public override string Description =>
            "A witness who saw exactly what the neutral one saw, asked by somebody they distrust and\n"
            + "are afraid of. Nothing about the claim changed; what changed is the relationship, the\n"
            + "wariness and the fear, which is where the difference in the answer has to come from.";

        public override string Voice => PlaygroundVoices.PlainBlunt;

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Rival, -70);
            PlaygroundState.Tie(stage, stage.Player, stage.Witness, RelationKind.Rival, -30);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Trust = 0.15;
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Fear, 0.7);
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Suspicion, 0.6);
        }
    }

    /// <summary>
    /// Somebody who would tell this person anything, and the depth that buys.
    ///
    /// A warm tie both ways plus a settled obligation between them, which is the half of standing
    /// that is a record rather than a feeling. BQ-072's depth is the axis under test: the claim is
    /// the same claim, and what changes is how much of what they hold comes out with it.
    /// </summary>
    internal sealed class TrustedConfidantPreset : PlaygroundPreset
    {
        public override string Id => "trusted-confidant";

        public override string Summary => "a deep tie, to see how far into what they hold the same answer goes";

        public override string Description =>
            "A friend of long standing who has also been sheltered by the person asking. The claim and\n"
            + "the belief behind it are untouched; the tie and the obligation ledger are what move, so\n"
            + "any extra that comes out is depth the relationship bought rather than knowledge nobody had.";

        public override string Voice => PlaygroundVoices.WarmOpen;

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Friend, 90, mutual: true);
            PlaygroundState.Owes(
                stage,
                SocialObligationKind.Sanctuary,
                stage.Witness,
                stage.Player,
                stage.SubjectFactId,
                "took them in over the winter");

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Trust = 0.85;
            witness.Personality.Honesty = 0.7;
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Affection, 0.6);
        }
    }

    /// <summary>
    /// Somebody whose silence would itself be the answer, and who has no line against saying
    /// otherwise.
    ///
    /// The thief is family, the loyalty is heavy, and the listener is somebody they will not simply
    /// refuse. Whether that reaches <c>DisclosureTactic.Falsify</c> is BQ-073's judgement about
    /// this state and not this preset's - the pair below it holds the identical pressures against
    /// a different character.
    /// </summary>
    internal sealed class LoyalLiarPreset : PlaygroundPreset
    {
        public override string Id => "loyal-liar";

        public override string Summary => "heavy loyalty to the person the claim is about, and no line against lying";

        public override string Description =>
            "The witness is the thief's family, holds them dear, and is pressed by somebody they cannot\n"
            + "simply turn down. Read this beside principled-refuser: the two hold the identical\n"
            + "pressures and differ only in the speaker's own honesty and the line they keep.";

        public override string Voice => PlaygroundVoices.FormalCold;

        public override void Apply(PlaygroundStage stage)
        {
            Pressure(stage);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Honesty = 0.1;
        }

        /// <summary>
        /// The state the two loyalty presets share, so the pair really does differ in one thing.
        /// </summary>
        internal static void Pressure(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Thief, RelationKind.Family, 90, mutual: true);
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Acquaintance, -35);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Loyalty = 0.95;
            witness.Values.Family.Importance = 0.95;
            witness.Sensitivities.FamilyThreat = 0.9;
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Fear, 0.75);
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Stress, 0.6);
        }
    }

    /// <summary>
    /// The identical pressures, and somebody who does not lie.
    ///
    /// BQ-071's own claim, made visible: a large enough pressure must not be able to buy its way
    /// past a person's character. The line is declared unbreakable, so the pressure has somewhere
    /// to go and it is not into a falsehood.
    /// </summary>
    internal sealed class PrincipledRefuserPreset : PlaygroundPreset
    {
        public override string Id => "principled-refuser";

        public override string Summary => "the same pressures as loyal-liar, held by somebody who will not lie";

        public override string Description =>
            "Every pressure loyal-liar sets, over a speaker whose honesty is high and who holds\n"
            + "NeverLiesDirectly as an unbreakable line. The contrast scenario runs the two side by\n"
            + "side, which is the cheapest way to see that the tactic came out of the state.";

        public override string Voice => PlaygroundVoices.FormalCold;

        public override void Apply(PlaygroundStage stage)
        {
            LoyalLiarPreset.Pressure(stage);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Honesty = 0.85;
            PlaygroundState.Line(stage, stage.Witness, PersonalProhibition.NeverLiesDirectly, 0.95, breakable: false);
        }
    }

    /// <summary>
    /// Somebody who would have spoken, and does not, because of who the claim is about.
    ///
    /// BQ-077's second line. Everything else is favourable - a warm listener, an untroubled
    /// speaker - so the weighing comes out in favour of saying it, and the line is what changes the
    /// answer afterwards. It is the one preset where the interesting output is a refusal from a
    /// speaker with every reason to talk.
    /// </summary>
    internal sealed class KinLinePreset : PlaygroundPreset
    {
        public override string Id => "kin-line";

        public override string Summary => "a willing speaker who will not say this particular thing about their own family";

        public override string Description =>
            "A friendly witness with nothing pressing on them, who happens to be the thief's sister and\n"
            + "keeps a line against speaking badly of her own. The weighing comes out in favour of the\n"
            + "claim; the line is applied after it and is reported as a ruling rather than as a pressure.";

        public override string Voice => PlaygroundVoices.WarmOpen;

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Friend, 80, mutual: true);
            PlaygroundState.Tie(stage, stage.Witness, stage.Thief, RelationKind.Family, 70, mutual: true);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Honesty = 0.8;
            witness.Personality.Trust = 0.8;
            PlaygroundState.Line(stage, stage.Witness, PersonalProhibition.NeverSpeaksBadlyOfFamily, 0.9, breakable: false);
        }
    }

    /// <summary>
    /// The same claim reached by a different route: not seen, only heard.
    ///
    /// The victim knows the thing is gone and nothing more, so they are the one person on the stage
    /// who can be given a belief by a named route - <c>KnowledgeGraph.Teach</c> strengthens a
    /// belief somebody already holds rather than re-sourcing it, and the playground does not work
    /// around that. What a thinly held second-hand belief does to the answer is
    /// <c>Disclosure</c>'s to say.
    /// </summary>
    internal sealed class HearsayVictimPreset : PlaygroundPreset
    {
        public override string Id => "hearsay-victim";

        public override string Summary => "the same claim held second-hand and thinly, by the person it was taken from";

        public override string Description =>
            "The victim is told by the witness what happened, and believes it without much conviction.\n"
            + "The speaker, the listener and the relationship are ordinary; the route into the claim is\n"
            + "what differs from every other preset, which is what makes it the one to run against\n"
            + "--knowledge and --confidence.";

        public override string Speaker => PlaygroundRoles.Victim;

        public override string Voice => PlaygroundVoices.WryGuarded;

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Believes(
                stage,
                stage.Victim,
                stage.SubjectFactId,
                KnowledgeSource.Hearsay,
                0.45,
                canProve: false,
                toldBy: stage.Witness);

            PlaygroundState.Tie(stage, stage.Victim, stage.Player, RelationKind.Acquaintance, 25, mutual: true);
        }
    }

    /// <summary>
    /// Old business, and a listener the speaker would spend it on.
    ///
    /// The history is made the way the world makes history: the player leans on the witness about
    /// the theft, the action layer records what it records, and a fortnight and a half passes so
    /// the material is old enough for <c>CallbackHooks</c> to offer it unprompted. The tie is
    /// mended afterwards, which is what makes the second gate - would they raise it with this
    /// person - come out the other way from <see cref="GuardedHistoryPreset"/>'s.
    /// </summary>
    internal sealed class SettledHistoryPreset : PlaygroundPreset
    {
        public override string Id => "settled-history";

        public override string Summary => "an old, settled incident this speaker may recall and would raise with this listener";

        public override string Description =>
            "The player leaned on the witness about the theft, and twenty days have passed. The event is\n"
            + "the action layer's own, not one the preset wrote, so the callback is derived from history\n"
            + "rather than staged. The tie has since mended, so both gates - may they remember it, would\n"
            + "they say it to this person - come out open.";

        public override string Voice => PlaygroundVoices.WarmOpen;

        public override int Turns => 2;

        public override void Apply(PlaygroundStage stage)
        {
            History(stage);
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Friend, 85, mutual: true);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Trust = 0.8;
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Fear, 0.0);
        }

        /// <summary>
        /// The incident both history presets are built on. Scripted to pass so the two differ in
        /// the state around the memory rather than in whether the memory exists.
        /// </summary>
        internal static void History(PlaygroundStage stage)
        {
            PlaygroundState.Act(stage, "intimidate", stage.Witness, CheckOutcome.Pass);
            PlaygroundState.Wait(stage, 20);
        }
    }

    /// <summary>
    /// The same old business, and a listener the speaker would not spend it on.
    ///
    /// The gate this preset exists to show closing is the second one. The witness may still recall
    /// the incident - the route is untouched - and would refuse the claim it refers to, so
    /// <c>CallbackDisclosure</c> withholds the material and the playground never puts it in front
    /// of wording. Nothing here is a wording rule: a withheld callback is a decision, and the
    /// report names the claim that withheld it.
    /// </summary>
    internal sealed class GuardedHistoryPreset : PlaygroundPreset
    {
        public override string Id => "guarded-history";

        public override string Summary => "the same old business, withheld because of the claim it refers to";

        public override string Description =>
            "The identical history as settled-history, over a witness who is loyal to the thief and wary\n"
            + "of the person asking. They may still remember what happened to them; what they will not do\n"
            + "is raise it with this listener, because the claim it refers to is one they would keep.";

        public override string Voice => PlaygroundVoices.FormalCold;

        public override void Apply(PlaygroundStage stage)
        {
            SettledHistoryPreset.History(stage);
            LoyalLiarPreset.Pressure(stage);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Honesty = 0.8;
        }
    }

    /// <summary>
    /// A speaker willing enough to be asked for something, so the third exchange has somewhere to go.
    ///
    /// The promise itself is composed by the laboratory rather than chosen by the simulation, and
    /// that is stated in the run's own output: nothing in Core selects a
    /// <c>SpeechActType.Promise</c> yet. What is production, and what this preset exists to make
    /// inspectable, is <c>ConversationState.Commit</c> - which promise becomes a durable
    /// obligation, when, and what it refuses.
    /// </summary>
    internal sealed class PromiseExchangePreset : PlaygroundPreset
    {
        public override string Id => "promise-exchange";

        public override string Summary => "a third exchange where something is asked for and undertaken";

        public override string Description =>
            "A witness on good enough terms to be asked for a favour. Turn three is a request and the\n"
            + "promise that answers it, and the point of it is the promotion rule: a promise is noted\n"
            + "transiently like any other act, and becomes a durable obligation only when a caller says\n"
            + "so. Run it with --no-commit to see the same exchange leave the ledger alone.";

        public override string Voice => PlaygroundVoices.WarmOpen;

        public override int Turns => 3;

        public override void Apply(PlaygroundStage stage)
        {
            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Friend, 60, mutual: true);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Trust = 0.7;
            witness.Personality.Honesty = 0.7;
        }
    }

    /// <summary>
    /// A speaker whose observed trade reaches wording, and enough restraint that it shows.
    ///
    /// Two things at once, because neither is visible without the other. The sandbox reports work
    /// and hobby facets the way the live adapter would - which is the honest headless seam, since
    /// an unread facet stays unread rather than being guessed - and BQ-145 derives domains from
    /// them, which BQ-076 turns into vocabulary tags. The shipped flavoured fragments all attach
    /// to refusals, so the preset also arranges for the speaker to have something to keep: a
    /// vocabulary that never became eligible would be a tag nobody could see.
    ///
    /// The anti-stereotype gate is the point rather than a caveat. The trade narrows which
    /// <em>wording</em> says the point; it decides nothing about willingness, and the decision
    /// section of the report is where that can be checked.
    /// </summary>
    internal sealed class LivedTradePreset : PlaygroundPreset
    {
        public override string Id => "lived-trade";

        public override string Summary => "an observed trade reaching the words, over a speaker with something to keep";

        public override string Description =>
            "The sandbox reports the witness's work and hobby the way the live adapter would, so BQ-145\n"
            + "derives domains and BQ-076 asks for their vocabulary. The speaker is also given a reason to\n"
            + "keep the claim, because every flavoured fragment the bundle ships attaches to a refusal.\n"
            + "Identity narrows the wording and decides nothing about willingness - the decision section is\n"
            + "where that can be checked. A flavoured fragment joins the same pool a plain one already fits,\n"
            + "at the same odds, so it is drawn on some seeds and not others: --seed 6 draws one.";

        public override string Voice => PlaygroundVoices.PlainBlunt;

        public override void Apply(PlaygroundStage stage)
        {
            stage.Vanilla.SetCharacterIdentity(
                stage.Witness,
                new CharacterIdentityBuilder(stage.Witness)
                    .WithWork("farmer")
                    .AddHobby("brewer")
                    .Build());

            PlaygroundState.Tie(stage, stage.Witness, stage.Player, RelationKind.Acquaintance, -25);

            NarrativeNpc witness = stage.Npc(stage.Witness);
            witness.Personality.Trust = 0.2;
            witness.Values.Law.Importance = 0.2;
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Fear, 0.65);
            PlaygroundState.Feels(stage, stage.Witness, EmotionalState.Suspicion, 0.5);
        }
    }
}
