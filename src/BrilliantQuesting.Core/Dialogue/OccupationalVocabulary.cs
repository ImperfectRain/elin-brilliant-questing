using System;
using System.Collections.Generic;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// The closed vocabulary of lived-context flavour a fragment may declare, and the tags
    /// <see cref="RealizationRequest.Vocabulary"/> asks for (BQ-076).
    ///
    /// One tag per <see cref="IdentityDomain"/> BQ-145 already derives - cultivation, alchemy,
    /// craft, trade, public order - spelled once here so a fragment and a derivation never
    /// disagree about what a domain is called. Widening this list means widening
    /// <see cref="IdentityDomain"/> first, in BQ-145, where the anti-stereotype gate lives; this
    /// step is not a second place to invent an occupation.
    /// </summary>
    public static class DialogueVocabulary
    {
        public const string Cultivation = "cultivation";
        public const string Alchemy = "alchemy";
        public const string Craft = "craft";
        public const string Trade = "trade";
        public const string PublicOrder = "public_order";

        public static IReadOnlyList<string> Vocabulary { get; } = new[]
        {
            Cultivation, Alchemy, Craft, Trade, PublicOrder
        };

        public static bool IsVocabulary(string tag)
        {
            for (int i = 0; i < Vocabulary.Count; i++)
            {
                if (string.Equals(Vocabulary[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The one spelling for a domain, everywhere it reaches wording.</summary>
        internal static string TagFor(IdentityDomain domain)
        {
            switch (domain)
            {
                case IdentityDomain.Cultivation:
                    return Cultivation;
                case IdentityDomain.Alchemy:
                    return Alchemy;
                case IdentityDomain.Craft:
                    return Craft;
                case IdentityDomain.Trade:
                    return Trade;
                case IdentityDomain.PublicOrder:
                    return PublicOrder;
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// BQ-076. Lived work, hobby and service context narrowing which fragment says a point - never
    /// which point is said, and never a fact about personality, disclosure or eligibility.
    ///
    /// <see cref="RequestedVocabulary"/> is this step's whole contribution, and it is deliberately
    /// small: a pure reading of <see cref="IdentityAffordances"/> - BQ-145's own derivation and the
    /// only place identity is allowed to be interpreted - into the tags
    /// <see cref="DialogueFragment.FitsVocabulary"/> already knows how to narrow selection on. It
    /// mirrors <see cref="VoiceProfile.RequestedTone"/> in shape and differs from it in exactly the
    /// one way BQ-075 called out as its own boundary: a voice is given to whoever is speaking, and
    /// is not built from a race, an archetype, an occupation or a hobby; an occupational vocabulary
    /// is nothing *but* that reading, because reading lived context into wording, subtly and only
    /// where BQ-145 already found it plausible, is the whole of what this step is for.
    ///
    /// <b>A domain reaches wording because BQ-145 said it was plausible knowledge or interest for
    /// this identity - never because this method looked at a race, an archetype, or a raw vanilla
    /// id itself.</b> There is no second reading of <see cref="World.CharacterIdentity"/> here and
    /// no substring table of occupations: every tag this method can ever produce is one
    /// <see cref="IdentityAffordances"/> already attributed to a work, hobby, service or
    /// institutional facet, so a fragment tagged with it can be explained the same way any other
    /// identity-derived weight can (BQ-145's own requirement, inherited rather than re-proven).
    ///
    /// <b>Presence, not magnitude, is what gets requested.</b> Every domain BQ-145 derived at all
    /// is requested, whatever its plausibility. The subtlety this step promises comes entirely from
    /// <see cref="DialogueFragment.FitsVocabulary"/>: a flavoured fragment only ever joins the same
    /// pool a plain one already fits, at the same odds every other candidate in that pool has,
    /// never by replacing the plain wording or by winning a comparison against it. A vocabulary
    /// that fired on every line because a weight cleared some threshold would be the stereotype
    /// failure BQ-145 already refuses arriving through wording instead.
    ///
    /// <b>An identity nobody could read requests nothing.</b> <see cref="IdentityAffordances.Nothing"/>
    /// - an unread actor, or one whose only facets are race and character archetype, which BQ-145
    /// derives nothing at all from - has no domains, so nothing is requested and no flavoured
    /// fragment becomes eligible. The ordinary, unflavoured pool is the whole answer, not a guessed
    /// default standing in for the one the build declined to give.
    /// </summary>
    public static class OccupationalVocabulary
    {
        private static readonly string[] None = new string[0];

        /// <summary>
        /// The vocabulary tags this identity makes it plausible to reach for, in domain order.
        /// Empty for an identity that implies nothing - never a default.
        /// </summary>
        public static IReadOnlyList<string> RequestedVocabulary(IdentityAffordances identity)
        {
            if (identity == null)
            {
                return None;
            }

            List<string> tags = new List<string>();
            AddDomains(identity.PlausibleKnowledge, tags);
            AddDomains(identity.PlausibleInterests, tags);
            return tags;
        }

        private static void AddDomains(IReadOnlyList<IdentityDomainAffordance> from, List<string> into)
        {
            for (int i = 0; i < from.Count; i++)
            {
                string tag = DialogueVocabulary.TagFor(from[i].Domain);
                if (tag.Length != 0 && !into.Contains(tag))
                {
                    into.Add(tag);
                }
            }
        }
    }
}
