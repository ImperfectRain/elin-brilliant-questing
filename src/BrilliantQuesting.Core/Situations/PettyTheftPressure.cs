using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// What a settlement's affordances mean for petty theft.
    ///
    /// The archetype-specific half of BQ-039, and the only place that holds theft's opinions.
    /// <see cref="LocalAffordanceProfile"/> reports that somebody has fifteen orens and a
    /// Pickpocket of eight; this decides that makes them a plausible cutpurse. Keeping the two
    /// apart is what lets BQ-041's shortage read the same profile without inheriting a theft
    /// threshold, and what makes these numbers replaceable by data later without touching the
    /// generic layer.
    ///
    /// Every weight below is approximate and meant to be tuned. What matters at this step is that
    /// each is named, commented, counted once, and derived from something the world actually says.
    /// </summary>
    public sealed class PettyTheftPressure
    {
        // -- pressure names ---------------------------------------------------------------------
        // The four terms of "an actor with a motive, means, opportunity and target". A candidate's
        // score is their sum and nothing else, so no part of it is unaccounted for.

        public const string Motive = "motive";
        public const string Means = "means";
        public const string TargetWorth = "target";
        public const string Opportunity = "opportunity";
        public const string PersonAtRisk = "person_at_risk";

        // -- motive -----------------------------------------------------------------------------

        /// <summary>The most an empty purse alone can push somebody toward taking something.</summary>
        private const int DestitutionCeiling = 35;

        /// <summary>Orens per point of relief from that pressure. A full purse removes the motive.</summary>
        private const int DestitutionRelief = 4;

        /// <summary>
        /// The most a disposition alone contributes. Held well under the destitution ceiling because
        /// need is the stronger story: a greedy person with money is a worse candidate than a
        /// scrupulous one with none.
        /// </summary>
        private const int GreedWeight = 20;

        // -- means ------------------------------------------------------------------------------

        /// <summary>Ceiling on capability, so a master thief cannot carry a candidate on skill alone.</summary>
        private const int MeansCeiling = 30;

        private const int PickpocketWeight = 3;
        private const int StealthWeight = 2;
        private const int DexterityWeight = 1;

        // -- target -----------------------------------------------------------------------------

        /// <summary>Ceiling on how much a mark's worth can contribute.</summary>
        private const int TargetWorthCeiling = 35;

        /// <summary>Orens of carried value per point. A trinket is not a reason; an heirloom is.</summary>
        private const int StakeValuePerPoint = 40;

        /// <summary>
        /// Orens *above this settlement's median purse* per point of conspicuousness.
        ///
        /// Read against the local middle rather than a fixed figure on purpose: eight hundred orens
        /// marks somebody out in a subsistence hamlet and passes unnoticed in a merchant quarter.
        /// This is the term that makes two settlements produce different candidates without either
        /// being named anywhere in this file.
        /// </summary>
        private const int ConspicuousWealthPerPoint = 80;

        /// <summary>Somebody who visibly handles money for a living is a known mark.</summary>
        private const int CommercialTargetWorth = 10;

        /// <summary>A spouse or close family member at risk should outrank a merely useful mark.</summary>
        private const int SpouseAtRiskWeight = 45;
        private const int FamilyAtRiskWeight = 40;

        // -- opportunity ------------------------------------------------------------------------

        /// <summary>Ceiling on circumstance, so a deserted street cannot by itself make a thief.</summary>
        private const int OpportunityCeiling = 25;

        /// <summary>
        /// The opening a pair alone in a place affords. Every other term below reduces it, which is
        /// the intended shape: opportunity is what the world has not taken away.
        /// </summary>
        private const int UnwatchedOpening = 18;

        /// <summary>Each additional local standing about is one more pair of eyes.</summary>
        private const int CrowdEyesPenalty = 3;

        /// <summary>
        /// How far an actual witness's attention closes the opening. Divides the sum of their
        /// Perception and SpotHidden, so an alert bystander suppresses a theft that a distracted one
        /// does not - the difference between two otherwise identical settlements.
        /// </summary>
        private const int WitnessAttentionRelief = 2;

        /// <summary>A trader busy with customers and coin is looking at the transaction, not their purse.</summary>
        private const int CommercialDistraction = 6;

        // -- eligibility ------------------------------------------------------------------------

        /// <summary>
        /// Below this the world is not under enough pressure to have produced a theft by itself.
        /// The one number that decides whether a settlement is quiet.
        /// </summary>
        public const int MinimumScore = 70;

        /// <summary>
        /// Nothing under this is worth the risk. Deliberately theft's own threshold rather than a
        /// generic "valuable" flag on the profile, because what counts as worth stealing is exactly
        /// the kind of judgement the generic layer must not make.
        /// </summary>
        public const int MinimumStakeValue = 250;

        /// <summary>
        /// Scores one actor taking one thing from another, in front of whoever is there to see it.
        ///
        /// Returns null when the pair cannot support a theft at all - nothing worth taking - rather
        /// than a zero-scored candidate, so the caller never has to distinguish "impossible" from
        /// "implausible".
        /// </summary>
        public SituationCandidate Evaluate(
            NarrativeWorldState world,
            LocalAffordanceProfile profile,
            ActorAffordances thief,
            ActorAffordances victim,
            ActorAffordances witness)
        {
            if (!CanFillSocialTheftRole(thief)
                || !CanFillSocialTheftRole(victim)
                || !CanMutateInventory(thief)
                || !CanMutateInventory(victim))
            {
                return null;
            }

            if (witness != null && !CanFillSocialTheftRole(witness))
            {
                witness = null;
            }

            ItemDescriptor stake = WorthTaking(victim);
            if (stake == null)
            {
                return null;
            }

            SituationCandidateBuilder builder = new SituationCandidateBuilder(PettyTheftSituation.ArchetypeId)
                .Bind(SituationRoles.Actor, thief.ActorId)
                .Bind(SituationRoles.Target, victim.ActorId)
                .BindItem(SituationRoles.Stake, stake)
                .BindSite(SituationRoles.Place, profile.ZoneId);

            double greed = world.Registry.GetNpc(thief.ActorId)?.Personality.Greed ?? 0.5;
            int destitution = Math.Max(0, DestitutionCeiling - thief.Money / DestitutionRelief);
            int disposition = (int)(greed * GreedWeight);
            builder.Pressure(
                Motive,
                destitution + disposition,
                thief.Name + " has motive: " + thief.Money + " orens in hand (need " + destitution
                + ") and greed " + greed.ToString("0.00") + " (disposition " + disposition + ")");

            int pickpocket = thief.Skill(VanillaSkill.Pickpocket);
            int stealth = thief.Skill(VanillaSkill.Stealth);
            int dexterity = thief.Attribute(VanillaAttribute.Dexterity);
            builder.Pressure(
                Means,
                Math.Min(
                    MeansCeiling,
                    pickpocket * PickpocketWeight + stealth * StealthWeight + dexterity * DexterityWeight),
                thief.Name + " has means: pickpocket " + pickpocket + ", stealth " + stealth
                + ", dexterity " + dexterity);

            int conspicuous = Math.Max(0, victim.Money - profile.MedianMoney) / ConspicuousWealthPerPoint;
            int worth = stake.Value / StakeValuePerPoint + conspicuous;
            string commercialNote = string.Empty;
            if (victim.IsCommercial)
            {
                worth += CommercialTargetWorth;
                commercialNote = ", and trades for a living";
            }

            builder.Pressure(
                TargetWorth,
                Math.Min(TargetWorthCeiling, worth),
                victim.Name + " is a target: carrying " + stake.Name + " worth " + stake.Value
                + " orens, holding " + victim.Money + " orens against a local median of "
                + profile.MedianMoney + commercialNote);

            // ScoreOpportunity records its own causes as it goes, because opportunity is a sum of
            // several observations rather than one; the pressure is then recorded with no further
            // sentence of its own.
            int opportunity = ScoreOpportunity(profile, thief, victim, witness, builder);
            builder.Pressure(Opportunity, opportunity, null);

            int personalStake = ScorePersonAtRisk(world, victim.ActorId, out string personalCause);
            if (personalStake > 0)
            {
                builder.Pressure(PersonAtRisk, personalStake, personalCause);
            }

            if (witness != null)
            {
                builder.Bind(SituationRoles.Witness, witness.ActorId);
            }

            return builder.Build();
        }

        private static int ScorePersonAtRisk(NarrativeWorldState world, EntityId victim, out string cause)
        {
            cause = null;
            RelationshipEdge best = null;
            int bestScore = 0;

            foreach (RelationshipEdge edge in world.Relationships.EdgesTo(victim))
            {
                int score = PersonAtRiskScore(edge);
                if (score <= bestScore)
                {
                    continue;
                }

                best = edge;
                bestScore = score;
            }

            if (best == null)
            {
                return 0;
            }

            cause = world.Registry.NameOf(victim) + " is at personal risk through "
                    + best.Kind + " tie to " + world.Registry.NameOf(best.From);
            return bestScore;
        }

        private static int PersonAtRiskScore(RelationshipEdge edge)
        {
            if (edge == null || edge.Sentiment <= 0)
            {
                return 0;
            }

            switch (edge.Kind)
            {
                case RelationKind.Spouse:
                    return SpouseAtRiskWeight;
                case RelationKind.Family:
                    return FamilyAtRiskWeight;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// What the moment affords, derived from who else is standing here and how alert they are.
        ///
        /// Every input is something the game can be asked for today. There is deliberately no
        /// schedule, room visibility or current-AI-goal term: BQ cannot read those reliably yet, and
        /// a constant standing in for them - which is what the first cut of this had - is a number
        /// that claims to describe the world without consulting it.
        /// </summary>
        internal static bool CanFillSocialTheftRole(ActorAffordances actor)
        {
            return actor != null && actor.SocialAgency == SocialAgency.Full;
        }

        internal static bool CanMutateInventory(ActorAffordances actor)
        {
            return actor != null && MutationPolicies.Permits(actor.ActorClass, MutationKind.Inventory);
        }

        private static int ScoreOpportunity(
            LocalAffordanceProfile profile,
            ActorAffordances thief,
            ActorAffordances victim,
            ActorAffordances witness,
            SituationCandidateBuilder builder)
        {
            int socialBystanders = 0;
            int otherLiving = 0;
            for (int i = 0; i < profile.Actors.Count; i++)
            {
                ActorAffordances local = profile.Actors[i];
                if (local.ActorId == thief.ActorId || local.ActorId == victim.ActorId)
                {
                    continue;
                }

                if (CanFillSocialTheftRole(local))
                {
                    socialBystanders++;
                }
                else
                {
                    otherLiving++;
                }
            }

            int opportunity = UnwatchedOpening - socialBystanders * CrowdEyesPenalty;
            builder.Cause(socialBystanders == 0
                ? "opportunity: " + thief.Name + " and " + victim.Name + " are the only socially capable locals here"
                : "opportunity: " + socialBystanders + " socially capable local(s) nearby");
            if (otherLiving > 0)
            {
                builder.Cause("opportunity: " + otherLiving + " other living local(s) also present");
            }

            if (witness != null)
            {
                int attention = witness.Attribute(VanillaAttribute.Perception)
                                + witness.Skill(VanillaSkill.SpotHidden);
                opportunity -= attention / WitnessAttentionRelief;
                builder.Cause("opportunity: " + witness.Name + " is present and attentive (perception "
                              + witness.Attribute(VanillaAttribute.Perception) + ", spot hidden "
                              + witness.Skill(VanillaSkill.SpotHidden) + ")");
            }
            else
            {
                builder.Cause("opportunity: nobody is placed to see it");
            }

            if (victim.IsCommercial)
            {
                opportunity += CommercialDistraction;
                builder.Cause("opportunity: " + victim.Name + " is occupied with trade");
            }

            return Math.Max(0, Math.Min(OpportunityCeiling, opportunity));
        }

        /// <summary>
        /// The one thing on somebody worth the risk of taking, or null. By value and then by id, so
        /// the answer does not depend on the order an inventory came back in.
        /// </summary>
        public static ItemDescriptor WorthTaking(ActorAffordances actor)
        {
            ItemDescriptor best = null;
            IReadOnlyList<ItemDescriptor> carried = actor.Carried;
            for (int i = 0; i < carried.Count; i++)
            {
                ItemDescriptor item = carried[i];
                if (item.Value < MinimumStakeValue)
                {
                    continue;
                }

                if (best == null
                    || item.Value > best.Value
                    || (item.Value == best.Value && string.CompareOrdinal(item.Id.Value, best.Id.Value) < 0))
                {
                    best = item;
                }
            }

            return best;
        }
    }
}
