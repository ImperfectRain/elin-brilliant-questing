using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// What kind of thing two people are contending for (BQa-015).
    ///
    /// Three, because the intent already says which one it is: a concrete object it names, a
    /// recorded shortage the verb would answer, or the bare chance to be the one who does this to
    /// this person or this matter. It is a label for grouping and for the inspector, never a rule -
    /// what makes a contest exclusive is <see cref="ActionContest.IsExclusive"/>, read off the
    /// verb's own declared effects.
    /// </summary>
    public enum ContestedThing
    {
        /// <summary>A named object. One purse, one pair of hands it ends up in.</summary>
        Object,

        /// <summary>A recorded shortage. Answering it twice would have to invent a second one.</summary>
        Resource,

        /// <summary>The chance to be the one who does this - to this person, about this matter.</summary>
        Opportunity
    }

    /// <summary>
    /// The one thing several actors are reaching for, and whether more than one of them can have
    /// it (BQa-015).
    ///
    /// Two questions kept apart on purpose. <em>What</em> is contended for comes from the intent,
    /// because the intent is where the caller already said which object, matter or person this
    /// attempt is about. <em>Whether it is indivisible</em> comes from the verb's BQa-010
    /// declaration, because that is where the library already says what kind of change the verb
    /// could make, and a second list of "exclusive verbs" beside it is the hand-kept copy that
    /// drifts silently (`D086`).
    ///
    /// Most contests are not exclusive and nothing here pretends otherwise. Two people can both
    /// tell the reeve, both hurt the same man, both learn the same fact; a race like that is one
    /// the world can carry, and arbitrating it would invent a scarcity that does not exist. The
    /// claim exists for the small set of changes a second completion would have to fabricate.
    /// </summary>
    public readonly struct ActionContest : IEquatable<ActionContest>
    {
        /// <summary>
        /// The effect kinds whose completion takes the contested thing out of everybody else's
        /// reach, in the vocabulary's own order.
        ///
        /// Each is here because a second actor completing the same change against the same
        /// subject would have to invent something that is not there: a second purse to lift, a
        /// second shortage to answer, a second body to take into custody, the destroyed ledger
        /// page back again, a break to mend in a thing already mended. Every other registered
        /// effect is deliberately absent - two people can both disclose, both deny, both harm,
        /// both damage, both create evidence, both alter standing or access, and none of those
        /// second deeds is incoherent.
        /// </summary>
        private static readonly string[] Indivisible =
        {
            SemanticEffects.PossessionTransferred,
            SemanticEffects.ResourceSupplied,
            SemanticEffects.EvidenceRemoved,
            SemanticEffects.PersonSecured,
            SemanticEffects.ObjectRepaired
        };

        private ActionContest(ContestedThing over, EntityId subject, bool exclusive)
        {
            Over = over;
            Subject = subject;
            IsExclusive = exclusive;
        }

        /// <summary>Nothing identifiable is being contended for. Not a contest anybody can lose.</summary>
        public static readonly ActionContest Nothing = new ActionContest(ContestedThing.Opportunity, EntityId.None, false);

        public ContestedThing Over { get; }

        /// <summary>The object, shortage, person or matter itself. <see cref="EntityId.None"/> for <see cref="Nothing"/>.</summary>
        public EntityId Subject { get; }

        /// <summary>
        /// Whether one completion ends it for everybody. False is the ordinary answer and means
        /// the contenders all proceed under their own attempts.
        /// </summary>
        public bool IsExclusive { get; }

        /// <summary>Whether this names something at all.</summary>
        public bool IsSomething => !Subject.IsNone;

        /// <summary>
        /// The grouping key, ordinal and stable across runs. Contenders are grouped by this and
        /// the groups are walked in its order, so nothing about the batch depends on the order a
        /// dictionary happens to enumerate in.
        /// </summary>
        public string Key => IsSomething
            ? Over.ToString().ToLowerInvariant() + "|" + Subject.Value
            : string.Empty;

        /// <summary>Whether that effect kind is one a second completion would have to invent.</summary>
        public static bool IsIndivisible(string effectKind)
        {
            for (int i = 0; i < Indivisible.Length; i++)
            {
                if (string.Equals(Indivisible[i], effectKind, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The indivisible effect kinds, in a stable order, for the inspector and tests.</summary>
        public static IReadOnlyList<string> IndivisibleEffects => Indivisible;

        /// <summary>An exclusive contest declared by a caller that knows better than the default.</summary>
        public static ActionContest Exclusive(ContestedThing over, EntityId subject)
        {
            return subject.IsNone ? Nothing : new ActionContest(over, subject, true);
        }

        /// <summary>A named contest several actors may finish. Grouped and reported, never refused.</summary>
        public static ActionContest Shared(ContestedThing over, EntityId subject)
        {
            return subject.IsNone ? Nothing : new ActionContest(over, subject, false);
        }

        /// <summary>
        /// What this intent is reaching for, read off the intent's own bindings and the verb's
        /// declared effects.
        ///
        /// The subject is whichever binding the caller filled, in the order the caller filled
        /// them for: a named object first, because an intent that names one is about that one;
        /// then the matter; then the other party, which is all a verb aimed at a person has.
        /// An undeclared verb (`ActionEffects.Undeclared`) yields a shared contest rather than an
        /// exclusive one - a coverage gap is not evidence of scarcity, and refusing contenders on
        /// the strength of a missing declaration would turn BQa-010's reported gap into silent
        /// behaviour.
        /// </summary>
        public static ActionContest ForIntent(NarrativeAction action, ActionIntent intent)
        {
            if (intent == null)
            {
                return Nothing;
            }

            bool exclusive = false;
            bool answersShortage = false;
            if (action != null)
            {
                IReadOnlyList<ActionEffect> effects = action.Effects.Effects;
                for (int i = 0; i < effects.Count; i++)
                {
                    if (IsIndivisible(effects[i].Kind))
                    {
                        exclusive = true;
                    }

                    if (string.Equals(effects[i].Kind, SemanticEffects.ResourceSupplied, StringComparison.Ordinal))
                    {
                        answersShortage = true;
                    }
                }
            }

            if (!intent.SubjectItem.IsNone)
            {
                return new ActionContest(ContestedThing.Object, intent.SubjectItem, exclusive);
            }

            if (!intent.SubjectFact.IsNone)
            {
                return new ActionContest(
                    answersShortage ? ContestedThing.Resource : ContestedThing.Opportunity,
                    intent.SubjectFact,
                    exclusive);
            }

            if (!intent.Target.IsNone)
            {
                return new ActionContest(ContestedThing.Opportunity, intent.Target, exclusive);
            }

            return Nothing;
        }

        public bool Equals(ActionContest other)
        {
            return Over == other.Over && Subject == other.Subject && IsExclusive == other.IsExclusive;
        }

        public override bool Equals(object obj) => obj is ActionContest other && Equals(other);

        public override int GetHashCode() => Key.GetHashCode() ^ (IsExclusive ? 1 : 0);

        public override string ToString()
        {
            if (!IsSomething)
            {
                return "nothing contested";
            }

            return (IsExclusive ? "one " : "a shared ") + Over.ToString().ToLowerInvariant() + ": " + Subject;
        }
    }
}
