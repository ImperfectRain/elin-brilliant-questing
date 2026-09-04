using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// The core communicative vocabulary (CD §17.1, §38 Phase B).
    ///
    /// Sixteen acts, deliberately: the design archive lists three dozen, and the ones missing here
    /// are missing because nothing consumes them yet. A vocabulary grows when a consumer needs a
    /// distinction, not when somebody thinks of a verb - which is why thanking, praising, teasing,
    /// comforting and flattering are still absent although an authoring corpus is full of them.
    /// Each of those is a way of saying one of the acts below, or a modifier on it, and none of
    /// them is a distinction anything in the simulation currently branches on.
    ///
    /// <see cref="Evade"/> is the addition BQ-070 named in advance and left to BQ-073: a
    /// deflection had no act, so a speaker who let a question go produced either nothing or a
    /// <see cref="Refuse"/> they never made. There is still no <c>Lie</c>, and there will not be
    /// one - a falsehood is a stance held against the speaker's own belief, so it is a property of
    /// an assertion rather than a separate way of speaking, and <c>Deception</c> reads it off the
    /// act and the belief graph together.
    ///
    /// <see cref="Promise"/> is BQ-083's addition, for the same reason Evade was BQ-073's: a
    /// conversation cannot hold somebody to their word if nothing they said was ever a commitment
    /// rather than a claim. It takes no stance on any proposition - a promise is not true or
    /// false, it is kept or broken, and which of those it becomes is decided later by what its
    /// speaker does, not by anything readable at the moment of speaking.
    ///
    /// <see cref="Inform"/>, <see cref="Warn"/>, <see cref="Offer"/> and <see cref="Forgive"/> are
    /// BQ-146's four, and each arrives with the consumer that could not be written without it.
    /// Routed storylet beats let an actor decide what to communicate from their own state
    /// (<c>ActorIntent</c>), and the four moves that decision most wanted to be able to reach were:
    /// telling somebody something nobody asked for, cautioning them about a danger the speaker is
    /// not the source of, putting terms on the table without yet being bound by them, and letting
    /// go of what is owed. Every one of those was previously either impossible or expressible only
    /// as an act that meant something materially different, and the last two are what make a
    /// merciful actor and a vindictive actor route a restitution scene apart.
    /// </summary>
    public enum SpeechActType
    {
        Ask,
        Answer,
        Accuse,
        Deny,
        Admit,
        Request,
        Refuse,
        Threaten,
        Apologize,
        Gossip,

        /// <summary>
        /// The question is let go of rather than answered or turned down.
        ///
        /// It asserts nothing, which is the whole of the difference between it and a lie, and it
        /// declines nothing, which is the whole of the difference between it and a
        /// <see cref="Refuse"/>. Both distinctions have to survive into the record, because "she
        /// would not say", "she changed the subject" and "she told me it was somebody else" are
        /// three different things to have learned about a person.
        /// </summary>
        Evade,

        /// <summary>
        /// The speaker takes on a future doing, rather than asking for one or being asked for one.
        ///
        /// Distinct from <see cref="Request"/> (which seeks the addressee's action) and from
        /// <see cref="Threaten"/> (which seeks it under pressure): a promise moves the obligation
        /// onto the speaker themself. Whether it survives the conversation - whether it is worth
        /// entering into the durable obligation ledger at all - is a judgement conversation state
        /// makes, not a fact carried on the act.
        /// </summary>
        Promise,

        /// <summary>
        /// A claim put forward that nobody asked for.
        ///
        /// BQ-146's addition, and the hole it closes is the one routed storylets fell into first:
        /// before it, an actor could only assert something in reply to a question
        /// (<see cref="Answer"/>) or behind somebody's back (<see cref="Gossip"/>), so an NPC who
        /// wanted to tell the person in front of them what had happened had no act to do it with.
        /// A storylet in which nobody has asked anything yet could therefore never start with
        /// somebody speaking, which made the player's question the only way a scene could begin.
        ///
        /// It is not a weaker <see cref="Answer"/>: an answer is owed to an antecedent and an
        /// informing is volunteered, and the difference is exactly what a listener learns about
        /// somebody who brings a thing up unprompted. It may not answer an
        /// <see cref="Ask"/> - that act already exists - and <see cref="SpeechActProfile"/> says
        /// so by deriving its permitted antecedents from the vocabulary minus that one member,
        /// rather than by a list that would go stale.
        /// </summary>
        Inform,

        /// <summary>
        /// A caution about something the speaker is not themself threatening to do.
        ///
        /// The distinction from <see cref="Threaten"/> is the whole reason it exists, and it is a
        /// distinction consequences already care about: being threatened is something done *to*
        /// somebody and moves standing accordingly, while being warned is a kindness or at worst
        /// an inconvenience. A layer that had to word both as <see cref="Threaten"/> would file
        /// "do not go down that road alone" in the ledger beside extortion.
        ///
        /// Stance is <see cref="SpeechActStance.None"/> for the same reason a threat's is: a
        /// warning is about what may happen, not about whether a claim already holds, so nothing
        /// that reads stance can score one as an assertion and nobody can be caught lying by
        /// warning.
        /// </summary>
        Warn,

        /// <summary>
        /// Terms put on the table: something the speaker will do, or give, if.
        ///
        /// Between <see cref="Request"/> (which seeks the addressee's action and commits the
        /// speaker to nothing) and <see cref="Promise"/> (which binds the speaker outright). An
        /// offer is neither: it is a proposal that is still open, which is why
        /// <c>ConversationState.Commit</c> takes promises and not these - an offer nobody has
        /// taken up is not an obligation, and recording it as one would fill the ledger with debts
        /// nobody agreed to.
        ///
        /// It is what restitution, bargaining, hire and bribery all are before anybody says yes.
        /// </summary>
        Offer,

        /// <summary>
        /// What is owed is released.
        ///
        /// Repairs, like <see cref="Apologize"/>, and the other half of it: an apology is offered
        /// by whoever did the thing, and this is granted by whoever it was done to. The act
        /// cannot be about the speaker themself, because releasing your own debt to yourself is
        /// not something said to anybody.
        ///
        /// Saying it is not the same as clearing the ledger. Whether a <c>SocialObligation</c>
        /// actually closes is the obligation layer's to decide from what the speaker then does,
        /// exactly as a promise's keeping is - this act carries the meaning and never the write.
        /// </summary>
        Forgive
    }

    /// <summary>
    /// One thing somebody communicates, with no words in it and none implied.
    ///
    /// The distinction the layer exists to keep (CD §17.1): the simulation decides what is
    /// *meant*, and a realizer decides much later how it is *said*. Nothing on this type is text,
    /// nothing on it is a fragment, and nothing on it is tone. Two speakers of opposite temperament
    /// saying the same thing produce the identical act; the same act rendered three ways is still
    /// one act; and every consumer that has to reason about what happened - disclosure (BQ-071),
    /// lying (BQ-073), conversation state (BQ-083) - reads this rather than a sentence.
    ///
    /// Three boundaries hold it in place.
    ///
    /// <b>It is a meaning, never an authority.</b> A speech act decides nothing: not whether a
    /// choice is offered, not whether an attempt succeeds, not what it costs, not what history
    /// records. Those stay with <c>NarrativeAction</c> and the registry, which is why this type has
    /// no availability, no check, no outcome and no <c>Perform</c>. The mod has one gameplay action
    /// system and this is not a second one.
    ///
    /// <b>Its content is the action layer's content.</b> The matter an act is about is an
    /// <see cref="ActionBinding"/> - the same "concrete proposition, object, destination or
    /// undertaking" that BQ-134 already infers to project a contextual intent. Speech has no
    /// private model of what things are about, so nothing here can disagree with what the player
    /// was offered.
    ///
    /// <b>It is transient and never persisted.</b> An act is what somebody said in a conversation
    /// that is happening; the durable record of having said it is an event, a belief, a memory or
    /// an obligation, and those layers already own it. There is no save entry, because a stored
    /// act would be a second history racing the ledger.
    /// </summary>
    public sealed class SpeechAct
    {
        private static readonly EntityId[] NoAddressees = new EntityId[0];

        private SpeechAct(
            SpeechActType type,
            SpeechActProfile profile,
            EntityId speaker,
            IReadOnlyList<EntityId> addressees,
            ActionBinding content,
            EntityId referent,
            SpeechAct inReplyTo)
        {
            Type = type;
            Profile = profile;
            Speaker = speaker;
            Addressees = addressees ?? NoAddressees;
            Content = content ?? ActionBinding.Empty;
            Referent = referent;
            InReplyTo = inReplyTo;
        }

        public SpeechActType Type { get; }

        public SpeechActProfile Profile { get; }

        public EntityId Speaker { get; }

        /// <summary>
        /// Everybody the act is addressed to, in stable id order. Ordering is dropped on purpose:
        /// which of an audience was looked at first is staging, and staging is not meaning.
        /// </summary>
        public IReadOnlyList<EntityId> Addressees { get; }

        /// <summary>
        /// What the act is about, as the action layer already describes such things. Never null;
        /// <see cref="ActionBinding.Empty"/> for the acts whose matter is carried by what they
        /// answer.
        /// </summary>
        public ActionBinding Content { get; }

        /// <summary>
        /// The person the content is about: who is accused, who is being owned up for, who is
        /// being talked about behind their back. Distinct from <see cref="Speaker"/> and from
        /// <see cref="Addressees"/> because the interesting acts are exactly the ones where those
        /// three come apart.
        /// </summary>
        public EntityId Referent { get; }

        /// <summary>
        /// The act this one responds to, or null. Held rather than copied: an answer means "to
        /// that question", and a chain lives and dies with the conversation holding it.
        /// </summary>
        public SpeechAct InReplyTo { get; }

        /// <summary>The claim at issue, when the act is about a claim at all.</summary>
        public EntityId About => Content.PropositionFact;

        public SpeechActStance Stance => Profile.Stance;

        public SpeechActDirection Direction => Profile.Direction;

        public bool IsAddressedTo(EntityId id)
        {
            for (int i = 0; i < Addressees.Count; i++)
            {
                if (Addressees[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A wording-free identity for the meaning: same meaning, same string; different meaning,
        /// different string. It is what lets a test prove that realization changed nothing, and
        /// what lets a later conversation layer notice the same thing being said twice.
        /// </summary>
        public string Signature
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Type);
                sb.Append('|').Append(Speaker.Value);
                sb.Append('|');
                for (int i = 0; i < Addressees.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(Addressees[i].Value);
                }

                sb.Append('|').Append(Content.PropositionFact.Value);
                sb.Append('|').Append(Content.Item.Value);
                sb.Append('|').Append(Content.Destination.Value);
                sb.Append('|').Append(Content.Purpose ?? string.Empty);
                sb.Append('|').Append(Referent.Value);
                sb.Append('|').Append(InReplyTo == null ? string.Empty : InReplyTo.Type.ToString());
                return sb.ToString();
            }
        }

        /// <summary>Ids and act type only. Never a line, and never the beginning of one.</summary>
        public override string ToString() => Signature;

        public static SpeechAct Compose(
            SpeechActType type,
            EntityId speaker,
            EntityId addressee,
            ActionBinding content,
            EntityId referent = default,
            SpeechAct inReplyTo = null)
        {
            return Compose(type, speaker, new[] { addressee }, content, referent, inReplyTo);
        }

        /// <summary>
        /// The only way to obtain an act, and it refuses rather than repairs.
        ///
        /// An act missing what its type requires is not a weaker act, it is a different one or
        /// none: an accusation naming nobody, an answer to nothing, gossip told to its own
        /// subject. Returning null keeps those out of the world instead of letting a realizer
        /// invent the missing half in words later. <see cref="WhyNot"/> says which rule refused.
        /// </summary>
        public static SpeechAct Compose(
            SpeechActType type,
            EntityId speaker,
            IReadOnlyList<EntityId> addressees,
            ActionBinding content,
            EntityId referent = default,
            SpeechAct inReplyTo = null)
        {
            SpeechActProfile profile = SpeechActProfile.Of(type);
            if (profile == null)
            {
                return null;
            }

            EntityId resolvedReferent = referent.IsNone && profile.ReferentDefaultsToSpeaker ? speaker : referent;
            List<EntityId> audience = StableAudience(addressees);

            if (WhyNot(type, speaker, audience, content, resolvedReferent, inReplyTo).Length != 0)
            {
                return null;
            }

            return new SpeechAct(type, profile, speaker, audience, Copy(content), resolvedReferent, inReplyTo);
        }

        /// <summary>
        /// Why an act of this shape would not be well formed, or an empty string when it would be.
        /// Diagnostic only: nothing in the mod branches on the wording of these.
        /// </summary>
        public static string WhyNot(
            SpeechActType type,
            EntityId speaker,
            IReadOnlyList<EntityId> addressees,
            ActionBinding content,
            EntityId referent,
            SpeechAct inReplyTo)
        {
            SpeechActProfile profile = SpeechActProfile.Of(type);
            if (profile == null)
            {
                return "no profile for " + type;
            }

            if (speaker.IsNone)
            {
                return "an act with no speaker is nobody's";
            }

            // The same defaulting Compose applies, so the two never disagree about whether a
            // given shape is well formed.
            if (referent.IsNone && profile.ReferentDefaultsToSpeaker)
            {
                referent = speaker;
            }

            if (addressees == null || addressees.Count == 0)
            {
                return "an act addressed to nobody was not communicated";
            }

            for (int i = 0; i < addressees.Count; i++)
            {
                if (addressees[i].IsNone)
                {
                    return "an unresolved addressee is not an addressee";
                }

                if (addressees[i] == speaker)
                {
                    return "the speaker cannot be their own audience";
                }

                for (int j = i + 1; j < addressees.Count; j++)
                {
                    if (addressees[i] == addressees[j])
                    {
                        return "the same person was addressed twice";
                    }
                }
            }

            ActionBinding matter = content ?? ActionBinding.Empty;
            if (profile.Content == SpeechActContentRule.PropositionRequired && !matter.HasProposition)
            {
                return type + " is about a claim and no claim was named";
            }

            if (profile.Content == SpeechActContentRule.Required && !matter.HasPurpose)
            {
                return type + " needs a matter and none was named";
            }

            string referentRefusal = WhyNotReferent(profile, speaker, addressees, referent);
            if (referentRefusal.Length != 0)
            {
                return referentRefusal;
            }

            return WhyNotAntecedent(profile, speaker, addressees, inReplyTo);
        }

        private static string WhyNotReferent(
            SpeechActProfile profile,
            EntityId speaker,
            IReadOnlyList<EntityId> addressees,
            EntityId referent)
        {
            switch (profile.Referent)
            {
                case SpeechActReferentRule.MustBeSpeaker:
                    if (referent.IsNone)
                    {
                        return profile.Type + " is about the speaker and named nobody";
                    }

                    return referent == speaker
                        ? string.Empty
                        : profile.Type + " may only be about the speaker themself";

                case SpeechActReferentRule.MustNotBeSpeaker:
                    if (referent.IsNone)
                    {
                        return profile.Type + " must name who the claim is against";
                    }

                    return referent == speaker
                        ? profile.Type + " against the speaker themself is an admission, not a charge"
                        : string.Empty;

                case SpeechActReferentRule.MustBeAbsentThirdParty:
                    if (referent.IsNone)
                    {
                        return profile.Type + " must name who is being talked about";
                    }

                    if (referent == speaker)
                    {
                        return profile.Type + " about the speaker themself is not " + profile.Type;
                    }

                    for (int i = 0; i < addressees.Count; i++)
                    {
                        if (addressees[i] == referent)
                        {
                            return profile.Type + " told to its own subject is something said to their face";
                        }
                    }

                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static string WhyNotAntecedent(
            SpeechActProfile profile,
            EntityId speaker,
            IReadOnlyList<EntityId> addressees,
            SpeechAct inReplyTo)
        {
            if (inReplyTo == null)
            {
                return profile.AntecedentRequired
                    ? profile.Type + " responds to something and nothing was named"
                    : string.Empty;
            }

            if (!profile.MayRespondTo(inReplyTo.Type))
            {
                return profile.Type + " does not respond to " + inReplyTo.Type;
            }

            if (inReplyTo.Speaker == speaker)
            {
                return "an act does not respond to the speaker's own act";
            }

            // Responding to somebody means responding *to them*. A bystander may speak up about a
            // question that was not put to them, but the act that does so is still addressed to
            // whoever asked - otherwise it is a remark that happens to be on the same subject, and
            // a conversation layer that treated the two alike would pair the wrong moves.
            for (int i = 0; i < addressees.Count; i++)
            {
                if (addressees[i] == inReplyTo.Speaker)
                {
                    return string.Empty;
                }
            }

            return profile.Type + " does not address whoever it claims to respond to";
        }

        private static List<EntityId> StableAudience(IReadOnlyList<EntityId> addressees)
        {
            List<EntityId> audience = new List<EntityId>();
            if (addressees == null)
            {
                return audience;
            }

            for (int i = 0; i < addressees.Count; i++)
            {
                audience.Add(addressees[i]);
            }

            audience.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return audience;
        }

        /// <summary>
        /// The binding is copied in, not held. <see cref="ActionBinding"/> is a mutable carrier the
        /// action layer fills per attempt, and an act whose matter could change after it was
        /// composed would be a record of something nobody said.
        /// </summary>
        private static ActionBinding Copy(ActionBinding content)
        {
            if (content == null)
            {
                return ActionBinding.Empty;
            }

            return new ActionBinding
            {
                PropositionFact = content.PropositionFact,
                Item = content.Item,
                Destination = content.Destination,
                Purpose = content.Purpose
            };
        }
    }
}
