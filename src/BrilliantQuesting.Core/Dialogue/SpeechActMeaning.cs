using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// What a contextual interaction (BQ-134) <em>means</em>, when it means something sayable.
    ///
    /// This is the whole seam between the two layers, and it runs one way on purpose. BQ-134
    /// decides what the player may attempt and how it is shown; this reads the result and says
    /// what communicating it amounts to. Nothing here consults availability, resolves a check,
    /// spends anything or writes anything, and nothing in the action library asks this class for
    /// permission - so the speech-act vocabulary cannot become a second gameplay action system
    /// however large it grows.
    ///
    /// The mapping is deliberately many-to-one and deliberately partial:
    ///
    /// <list type="bullet">
    /// <item>Telling a neighbour and reporting to a guard are different attempts with different
    /// consequences and the same communicative act. That they collapse here is the point - the
    /// meaning layer is not a mirror of the verb registry.</item>
    /// <item>Most verbs mean nothing communicative at all. Picking a pocket says nothing, and
    /// returning nothing is the correct answer rather than a gap to be filled.</item>
    /// <item>Half the vocabulary has no player verb, because answering, denying, owning up,
    /// refusing, apologizing and passing something on are moves inside a conversation rather than
    /// options on a menu. Their composer is whoever runs the conversation - disclosure (BQ-071)
    /// and the storylet beats - not this class.</item>
    /// </list>
    /// </summary>
    public static class SpeechActMeaning
    {
        /// <summary>
        /// What a projected intent means. Takes BQ-134's own output rather than re-deriving from
        /// the registry, so there is exactly one discovery pass and this is downstream of it.
        /// </summary>
        public static SpeechAct Of(ActionIntentOption option, ActionContext context)
        {
            return option == null ? null : Of(option.Action.Id, context);
        }

        /// <summary>Null when the verb is not communicative, or when its meaning is not well formed here.</summary>
        public static SpeechAct Of(string actionId, ActionContext context)
        {
            if (context == null || context.Target.IsNone || !TryMap(actionId, out SpeechActType type))
            {
                return null;
            }

            // The same binding the projection layer infers to label the option. Speech has no
            // private notion of what an attempt is about.
            ActionBinding content = ActionBinding.Infer(context);

            switch (type)
            {
                case SpeechActType.Ask:
                case SpeechActType.Accuse:
                    return SpeechAct.Compose(type, context.Actor, context.Target, content, KnownSubject(context, content));

                default:
                    return SpeechAct.Compose(type, context.Actor, context.Target, content);
            }
        }

        /// <summary>Whether the verb communicates anything at all, without composing the act.</summary>
        public static bool IsCommunicative(string actionId) => TryMap(actionId, out _);

        /// <summary>
        /// The whole table, held once so that "does this verb say anything" and "what does it say"
        /// can never come to different answers.
        /// </summary>
        private static bool TryMap(string actionId, out SpeechActType type)
        {
            switch (actionId)
            {
                case "question":
                    type = SpeechActType.Ask;
                    return true;

                // Telling a neighbour and reporting to a guard are different attempts with
                // different consequences and one communicative act.
                case "expose":
                case "report":
                    type = SpeechActType.Accuse;
                    return true;

                case "persuade":
                case "call_favor":
                case "bribe":
                case "invoke_authority":
                    type = SpeechActType.Request;
                    return true;

                case "intimidate":
                case "extort":
                    type = SpeechActType.Threaten;
                    return true;

                default:
                    // Including "lie": a lie is not an act type but a stance held against the
                    // speaker's own belief, so which act carries one is BQ-073's decision and not
                    // a row in this table.
                    type = SpeechActType.Ask;
                    return false;
            }
        }

        /// <summary>
        /// Who the speaker can name as the claim's subject - and only if they actually hold the
        /// belief.
        ///
        /// The same rule BQ-134 applies to labels, applied to meaning, and it matters more here:
        /// a label that leaks an unknown culprit is a display bug, whereas an act that names one
        /// is the simulation asserting the player said something they had no way to say. An act
        /// that consequently cannot be formed (a charge against nobody) is refused by
        /// <see cref="SpeechAct.Compose"/> rather than filled in, which is why an accusation is
        /// never composed out of a suspicion the speaker does not hold.
        /// </summary>
        private static EntityId KnownSubject(ActionContext context, ActionBinding content)
        {
            if (!content.HasProposition || !context.World.Knowledge.Knows(context.Actor, content.PropositionFact))
            {
                return EntityId.None;
            }

            Fact fact = context.World.Knowledge.GetFact(content.PropositionFact);
            if (fact == null || fact.Subject == context.Actor)
            {
                return EntityId.None;
            }

            return context.World.Registry.GetNpc(fact.Subject) == null ? EntityId.None : fact.Subject;
        }
    }
}
