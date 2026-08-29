using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// The gate every write into Elin passes through.
    ///
    /// The step this exists for asks that every mutation call site consult a policy. Those call
    /// sites are spread across every verb family, the situations and the consequence engine, and
    /// threading a check through each of them would mean that many places to forget. So the check
    /// does not live at the call sites: it lives at the one place they all end up. Every
    /// implementation of <see cref="IVanillaState"/> derives from this class, the public writes are declared here and
    /// nowhere else, and what an implementation supplies is the unguarded half - <c>...Core</c> -
    /// which the gate calls only after the policy has said yes.
    ///
    /// That is what makes the guarantee structural rather than a habit. A story-critical NPC is
    /// unmovable because <see cref="TryAdmitResident"/> - the mod's one relocation, the settlement
    /// roll the shelter verbs write - refuses on their class before the adapter is reached, and
    /// unkillable because the seam has no way to kill anybody and a new one could not be added
    /// without a <see cref="VanillaMutationAttribute"/> naming the rung it needs.
    ///
    /// Nothing here is a substitute for a precondition. A verb whose write will certainly be
    /// refused should not be offered at all (<see cref="MutationPolicies.MayMutate"/>); the gate
    /// is the floor under that, for the paths nobody thought about.
    /// </summary>
    public abstract class VanillaStateBase
    {
        /// <summary>Whoever the player is. The subject of every write that names no other actor.</summary>
        public abstract EntityId PlayerId { get; }

        /// <summary>
        /// What kind of actor this is, as the game answers it. Nobody is
        /// <see cref="NarrativeActorClass.Unknown"/> on purpose - it is what an unresolved
        /// character or an unreadable build reports.
        /// </summary>
        public NarrativeActorClass GetActorClass(EntityId chara)
        {
            return chara.IsNone ? NarrativeActorClass.Unknown : GetActorClassCore(chara);
        }

        // -- the gated writes ---------------------------------------------------------------
        //
        // One shape throughout: refuse and change nothing, or delegate. A void write that is
        // refused does nothing; a bool write that is refused reports false, which every caller
        // already treats as "the game would not do it" rather than as an error.

        public void ChangeAffinity(EntityId chara, int delta)
        {
            if (Allows(MutationKind.Social, "change affinity", chara))
            {
                ChangeAffinityCore(chara, delta);
            }
        }

        public void ChangeKarma(int delta)
        {
            if (Allows(MutationKind.Social, "change karma", PlayerId))
            {
                ChangeKarmaCore(delta);
            }
        }

        public void ChangeFame(int delta)
        {
            if (Allows(MutationKind.Social, "change fame", PlayerId))
            {
                ChangeFameCore(delta);
            }
        }

        public void ChangeInfluence(EntityId townId, int delta)
        {
            // The town is a place, not an actor. What moves is the player's standing in it.
            if (Allows(MutationKind.Social, "change influence", PlayerId))
            {
                ChangeInfluenceCore(townId, delta);
            }
        }

        public bool TrySpendMoney(EntityId payer, EntityId payee, int amount)
        {
            return AllowsBoth(MutationKind.Inventory, "spend money", payer, payee)
                   && TrySpendMoneyCore(payer, payee, amount);
        }

        public bool TryTransferItem(EntityId itemId, EntityId from, EntityId to)
        {
            // The item is the thing moved; the two people are who it is being moved between, and
            // both of them are being reached into.
            return AllowsBoth(MutationKind.Inventory, "transfer item", from, to)
                   && TryTransferItemCore(itemId, from, to);
        }

        public bool TryDestroyItem(EntityId itemId, EntityId holder)
        {
            return Allows(MutationKind.Inventory, "destroy item", holder)
                   && TryDestroyItemCore(itemId, holder);
        }

        public bool TryAdmitResident(EntityId chara)
        {
            // A permanent relocation, and the reason the "unmovable" half of the policy is a live
            // rule rather than a promise about code nobody has written yet: this is the write the
            // shelter verbs make, and it is the highest rung the mod currently reaches.
            return Allows(MutationKind.Relocate, "admit resident", chara)
                   && TryAdmitResidentCore(chara);
        }

        // -- what an implementation supplies ------------------------------------------------

        protected abstract NarrativeActorClass GetActorClassCore(EntityId chara);

        /// <summary>
        /// Says that a write was refused and why. A write that quietly does nothing is
        /// unfindable, which is the same rule the adapter's own refusals follow.
        /// </summary>
        protected abstract void OnMutationRefused(string message);

        protected abstract void ChangeAffinityCore(EntityId chara, int delta);

        protected abstract void ChangeKarmaCore(int delta);

        protected abstract void ChangeFameCore(int delta);

        protected abstract void ChangeInfluenceCore(EntityId townId, int delta);

        protected abstract bool TrySpendMoneyCore(EntityId payer, EntityId payee, int amount);

        protected abstract bool TryTransferItemCore(EntityId itemId, EntityId from, EntityId to);

        protected abstract bool TryDestroyItemCore(EntityId itemId, EntityId holder);

        protected abstract bool TryAdmitResidentCore(EntityId chara);

        // -- the check ----------------------------------------------------------------------

        /// <summary>
        /// Every named subject has to stand high enough. One refusal refuses the whole write:
        /// a transfer half of which is allowed would take an object off somebody and leave it
        /// nowhere.
        /// </summary>
        private bool AllowsBoth(MutationKind kind, string what, EntityId subjectA, EntityId subjectB)
        {
            return Allows(kind, what, subjectA) && Allows(kind, what, subjectB);
        }

        private bool Allows(MutationKind kind, string what, EntityId subject)
        {
            // Nobody named is not an actor to protect: an unnamed payee is a fine paid into the
            // world, and the seam's own contract already decides whether that is legal.
            if (subject.IsNone)
            {
                return true;
            }

            NarrativeActorClass actorClass = GetActorClass(subject);
            if (MutationPolicies.Permits(actorClass, kind))
            {
                return true;
            }

            OnMutationRefused("Refused to " + what + ": " + subject + " is " + actorClass
                              + ", which is " + MutationPolicies.PolicyFor(actorClass)
                              + " and does not permit " + kind + ".");
            return false;
        }
    }
}
