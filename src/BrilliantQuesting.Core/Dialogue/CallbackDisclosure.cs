using System.Collections.Generic;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// One speaker's clearance to bring one piece of their own history up in front of one listener,
    /// right now.
    ///
    /// <b>It is the second gate, and it is a different question from the first.</b> BQ-081's
    /// <c>CallbackRoute</c> settles whether somebody may <em>remember</em> an event; this settles
    /// whether they would <em>say</em> it to the person opposite. Those have different answers all
    /// the time - the whole of BQ-071 is that knowing and telling come apart - and a hook alone
    /// answers only the first.
    ///
    /// <b>It cannot be forged.</b> The constructor is internal, so the only thing that produces one
    /// is <see cref="CallbackDisclosure"/>, which produces it by asking <see cref="Disclosure"/>.
    /// <c>RealizationRequest</c> takes a permit and not a hook, so there is no way to word a
    /// callback that was never cleared - the same shape BQ-081 used to make the knowledge gate
    /// structural, applied to the gate BQ-081 left to convention.
    /// </summary>
    public sealed class CallbackPermit
    {
        internal CallbackPermit(
            CallbackHook hook,
            EntityId listener,
            bool allowed,
            EntityId withheld,
            DisclosureStrategy strategy,
            string because)
        {
            Hook = hook;
            Listener = listener;
            Allowed = allowed;
            Withheld = withheld;
            Strategy = strategy;
            Because = because ?? string.Empty;
        }

        /// <summary>The material this is a clearance for. Never null on a permit that exists.</summary>
        public CallbackHook Hook { get; }

        /// <summary>Who it was cleared for. A permit is about one listener and no other.</summary>
        public EntityId Listener { get; }

        /// <summary>Whether the speaker would actually bring this up with them.</summary>
        public bool Allowed { get; }

        /// <summary>
        /// The claim that refused it, or <see cref="EntityId.None"/> when nothing did. Diagnostic:
        /// the point of naming it is that "why did nobody mention the theft" has an answer in the
        /// knowledge graph rather than in this class.
        /// </summary>
        public EntityId Withheld { get; }

        /// <summary>
        /// What the speaker would do about <see cref="Withheld"/> if asked outright, or
        /// <see cref="DisclosureStrategy.NothingToDisclose"/> when no claim was weighed. Read from
        /// the disclosure decision that bound this, never decided here.
        /// </summary>
        public DisclosureStrategy Strategy { get; }

        /// <summary>Why, in words, for the inspector. Nothing branches on it.</summary>
        public string Because { get; }

        public override string ToString()
        {
            return (Hook == null ? "no hook" : Hook.Signature) + " -> " + Listener.Value
                + (Allowed ? ": allowed" : ": withheld (" + Because + ")");
        }
    }

    /// <summary>
    /// Whether a speaker who is entitled to remember something is willing to raise it with the
    /// person in front of them (BQ-081 x BQ-071/072/073).
    ///
    /// <b>Why this exists.</b> BQ-081 derives a hook per recaller, which makes "they could not
    /// possibly know that" structurally impossible. It says nothing about the listener, and the
    /// gap that leaves is real: a confidently-believed claim with secrecy 100 that its holder would
    /// refuse to state if asked outright could still be handed to <c>DialogueRealizer</c> as a
    /// callback and come out as "I know what {recalled} is said to have done". Recall permission
    /// was being spent as disclosure permission.
    ///
    /// <b>It adds no authority of its own.</b> Every answer here comes from
    /// <see cref="Disclosure.Decide"/>, over the claims the recalled event already named
    /// (<see cref="CallbackHook.Claims"/>) and the beliefs the speaker already holds. There is no
    /// callback-specific willingness, no second secrecy model and no fact minted to stand for "what
    /// the callback is about" - which would be exactly the second fact system a hook exists not to
    /// be. If a claim would not be disclosed to this listener, the callback that names it is not
    /// either; if the claim would come out, referring to it certainly may.
    ///
    /// <b>What it does not reach, and why that is not a hole.</b> An event that named no claim has
    /// nothing for disclosure to weigh, and such a callback is always permitted. That is honest
    /// rather than convenient: the only route to an event whose claims are absent and whose notice
    /// was suppressed is <see cref="CallbackRoute.FirstHand"/> - <c>unnoticed</c> closes
    /// <c>Involved</c> and <c>Witnessed</c>, and <c>Heard</c> requires a claim to have been
    /// believed - so the speaker is the one it happened by, talking about themselves. There is no
    /// third party whose secret could leak through a gap of that shape.
    ///
    /// <b>It stops before wording, like everything else on this seam.</b> A permit is carried into
    /// <c>RealizationRequest</c> and honoured there; nothing about the listener reaches the
    /// fragments, and realization still reads no world state.
    /// </summary>
    public static class CallbackDisclosure
    {
        /// <summary>
        /// Whether this speaker would bring this piece of history up with this listener, and what
        /// stopped them if they would not. Never null: a refusal is an answer and a caller handed
        /// null could not tell it from a missing hook.
        /// </summary>
        public static CallbackPermit Permit(
            NarrativeWorldState world,
            CallbackHook hook,
            EntityId listener,
            GameTime now)
        {
            if (hook == null)
            {
                return new CallbackPermit(null, listener, false, EntityId.None, DisclosureStrategy.NothingToDisclose,
                    "there is no material to clear");
            }

            if (world == null || listener.IsNone)
            {
                return Withhold(hook, listener, EntityId.None, DisclosureStrategy.NothingToDisclose,
                    "there is nobody to clear it for");
            }

            // Nobody discloses anything to themself, which is `Disclosure`'s own rule rather than a
            // new one; saying it here keeps the answer a refusal instead of an empty weighing.
            if (listener == hook.Recaller)
            {
                return Withhold(hook, listener, EntityId.None, DisclosureStrategy.NothingToDisclose,
                    "the speaker and the listener are the same person");
            }

            IReadOnlyList<EntityId> claims = hook.Claims;
            for (int i = 0; i < claims.Count; i++)
            {
                DisclosureDecision decision = Disclosure.Decide(world, hook.Recaller, listener, claims[i], now);

                // `NothingToDisclose` is not a refusal - it means the speaker holds no belief about
                // this particular claim, which is the ordinary case for a witness who saw an event
                // without forming a view about the claims filed against it. Only somebody who holds
                // a claim and would keep it withholds the callback that names it.
                if (decision.Strategy == DisclosureStrategy.NothingToDisclose || decision.WillDisclose)
                {
                    continue;
                }

                return Withhold(hook, listener, claims[i], decision.Strategy,
                    "they would " + Word(decision.Strategy) + " the claim this refers to");
            }

            return new CallbackPermit(hook, listener, true, EntityId.None, DisclosureStrategy.NothingToDisclose,
                claims.Count == 0
                    ? "the event named no claim to keep"
                    : "they would put every claim this refers to forward");
        }

        /// <summary>
        /// The most salient piece of old business this speaker would actually raise with this
        /// listener, or null when nothing survives both gates.
        ///
        /// The safe form of <c>CallbackHooks.Best</c>, and the reason it exists is that the unsafe
        /// form is a trap: taking the best hook and then discovering it is withheld loses every
        /// perfectly sayable callback behind it. Order is <c>CallbackHooks</c>' own, so what comes
        /// back is still the most striking thing they are willing to say and nothing is reordered
        /// by willingness.
        /// </summary>
        public static CallbackPermit Best(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            EntityId listener,
            GameTime now,
            CallbackSelection selection = null)
        {
            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(world, vanilla, recaller, now, selection);
            for (int i = 0; i < hooks.Count; i++)
            {
                CallbackPermit permit = Permit(world, hooks[i], listener, now);
                if (permit.Allowed)
                {
                    return permit;
                }
            }

            return null;
        }

        /// <summary>
        /// The same, for BQ-082's narrower question: the one old scandal or embarrassment this
        /// speaker would raise here, in front of this person, that did not happen here.
        ///
        /// Both gates, in the order they belong in - <see cref="CallbackRecurrence"/> decides
        /// whether the material earns a recurrence at all, and this decides whether the speaker
        /// would spend it on this listener. Neither can substitute for the other: a scandal worth
        /// retelling is still not one you tell the subject's brother.
        /// </summary>
        public static CallbackPermit BestRecurrence(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            EntityId listener,
            ContinuityContext context,
            GameTime now,
            CallbackSelection selection = null)
        {
            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(world, vanilla, recaller, now, selection);
            for (int i = 0; i < hooks.Count; i++)
            {
                if (!CallbackRecurrence.IsContinuityHumour(hooks[i], context))
                {
                    continue;
                }

                CallbackPermit permit = Permit(world, hooks[i], listener, now);
                if (permit.Allowed)
                {
                    return permit;
                }
            }

            return null;
        }

        private static CallbackPermit Withhold(
            CallbackHook hook,
            EntityId listener,
            EntityId withheld,
            DisclosureStrategy strategy,
            string because)
        {
            return new CallbackPermit(hook, listener, false, withheld, strategy, because);
        }

        private static string Word(DisclosureStrategy strategy)
        {
            return strategy == DisclosureStrategy.Refuse ? "refuse" : "deflect";
        }
    }
}
