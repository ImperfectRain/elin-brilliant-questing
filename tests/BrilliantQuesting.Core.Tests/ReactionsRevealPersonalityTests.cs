using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-080. One unusual event, six observers, six reactions, and nothing anywhere in the mod
    /// that was written about this event.
    ///
    /// The event is deliberately one BQ-064 can already read three ways - a ruined field crop -
    /// with an absurd premise on top of it (CD §22: an ordinary problem, one absurd premise, a
    /// real mechanical consequence). The absurdity lives entirely in the fact's own prose, which is
    /// exactly the part of it no reaction may read: <see cref="RetitlingTheEventChangesNoReaction"/>
    /// rewrites that prose and proves every reaction comes out identical.
    /// </summary>
    public class ReactionsRevealPersonalityTests
    {
        private static readonly EntityId Crop = EntityId.Parse("item_field_crop");
        private static readonly EntityId Sample = EntityId.Parse("item_crop_sample");
        private static readonly EntityId SourceFact = EntityId.Parse("fact_orderly_ruin");

        private const string Premise = "the field crop cut to one length and stacked by size";
        private const WeirdnessLevel Absurd = WeirdnessLevel.AbsurdPremiseCentral;

        [Fact]
        public void OneAbsurdEventDrawsSixReactionsAndNoneOfThemIsWrittenForIt()
        {
            NarrativeWorldState world = Town();
            IReadOnlyList<ActorReaction> reactions = ReactAll(world);

            // The done-when, stated as the thing that would be false if these were six wordings of
            // one reaction: six wording-free identities, six concerns, six next moves.
            Assert.Equal(6, reactions.Select(r => r.Signature).Distinct().Count());
            Assert.Equal(6, reactions.Select(r => r.Concern).Distinct().Count());
            Assert.Equal(6, reactions.Select(r => r.Response).Distinct().Count());

            Assert.Equal(
                new[]
                {
                    ValueConcern.Animals, ValueConcern.Wealth, ValueConcern.Law,
                    ValueConcern.Knowledge, ValueConcern.Faith, ValueConcern.Family
                },
                reactions.Select(r => r.Concern).ToArray());

            Assert.Equal(
                new[]
                {
                    ProblemSolvingStyle.Wait, ProblemSolvingStyle.PaySomeone, ProblemSolvingStyle.AskAuthority,
                    ProblemSolvingStyle.DoItSelf, ProblemSolvingStyle.SeekReligiousHelp, ProblemSolvingStyle.Flee
                },
                reactions.Select(r => r.Response).ToArray());

            // Reacting differently is not the same as reading differently, and the step needs
            // both: three of the six read the evidence through a lens of their own, and the other
            // three share a reading and still react three ways.
            Assert.Equal(3, reactions.Select(r => r.Interpretation.DerivedPredicate).Distinct().Count());
            Assert.Equal(FactPredicates.HasSoilTrouble, reactions[0].Interpretation.DerivedPredicate);
            Assert.Equal(FactPredicates.MayBeSabotaged, reactions[2].Interpretation.DerivedPredicate);
            Assert.Equal(FactPredicates.IsContaminated, reactions[3].Interpretation.DerivedPredicate);

            // CD §23's tier, read as what somebody takes in stride: the same absurd premise is the
            // whole of the event to five of them and unremarkable to the one the town already
            // finds strange.
            Assert.Equal(WeirdnessLevel.Mundane, reactions[0].Registers);
            foreach (ActorReaction reaction in reactions.Skip(1))
            {
                Assert.Equal(WeirdnessLevel.AbsurdPremiseCentral, reaction.Registers);
            }

            // Nothing a reaction carries is text of the event's, because nothing in the derivation
            // ever reads any.
            foreach (ActorReaction reaction in reactions)
            {
                Assert.Equal(Absurd, reaction.Premise);
                Assert.DoesNotContain("stacked by size", reaction.Signature);
                Assert.DoesNotContain("stacked by size", NarrativeInspector.DescribeReaction(world, reaction));
                Assert.True(reaction.Intensity > 0.0);
            }
        }

        [Fact]
        public void TheEventIsUnchangedByBeingReactedTo()
        {
            NarrativeWorldState world = Town();
            Fact before = world.Knowledge.GetFact(SourceFact);
            string shape = before.Subject + "|" + before.Predicate + "|" + before.Object + "|"
                           + before.Value + "|" + before.Truth + "|" + before.Secrecy + "|"
                           + before.DistortionOf;

            IReadOnlyList<ActorReaction> reactions = ReactAll(world);

            Fact after = world.Knowledge.GetFact(SourceFact);
            Assert.Same(before, after);
            Assert.Equal(
                shape,
                after.Subject + "|" + after.Predicate + "|" + after.Object + "|" + after.Value + "|"
                + after.Truth + "|" + after.Secrecy + "|" + after.DistortionOf);

            foreach (ActorReaction reaction in reactions)
            {
                Assert.Equal(SourceFact, reaction.SourceFactId);
                Assert.Equal(SourceFact, reaction.Interpretation.SourceFactId);
                Assert.NotEqual(SourceFact, reaction.Interpretation.DerivedFactId);
            }
        }

        [Fact]
        public void RetitlingTheEventChangesNoReaction()
        {
            // Same fact, same subject, same predicate, different prose - and the same six
            // reactions. There is nowhere in the derivation for bespoke text about an event to be
            // read, which is a stronger statement than "nobody wrote any".
            string[] first = ReactAll(Town(Premise)).Select(r => r.Signature).ToArray();
            string[] second = ReactAll(Town("the field crop wound into a single tidy spiral"))
                .Select(r => r.Signature).ToArray();

            Assert.Equal(first, second);
        }

        [Fact]
        public void RemovingWhatCarriedAReactionChangesItForThatReason()
        {
            // The devout woman's concern rests on faith being worth something to her. Take that
            // away and what is left is the reading the evidence itself put in front of her.
            NarrativeWorldState world = Town();
            NarrativeNpc devout = world.Registry.GetNpc(EntityId.Parse("npc_ovin"));
            devout.Values.Faith.Importance = 0.0;

            ActorReaction reaction = React(world, devout);
            Assert.NotEqual(ValueConcern.Faith, reaction.Concern);
            Assert.Equal(ValueConcern.Animals, reaction.Concern);

            // Her habit is hers, not her concern's: seeking religious help survives losing the
            // reason for it, because BQ-062's profile is the larger term by design.
            Assert.Equal(ProblemSolvingStyle.SeekReligiousHelp, reaction.Response);
        }

        [Fact]
        public void RemovingWhatLetSomebodyReadTheEventWeakensTheirReaction()
        {
            NarrativeWorldState world = Town();
            NarrativeNpc apothecary = world.Registry.GetNpc(EntityId.Parse("npc_mira"));
            ActorReaction before = React(world, apothecary);

            apothecary.Occupation = string.Empty;
            ActorReaction after = React(world, apothecary);

            // Take away the work that made her reading of the evidence a credible one. What she
            // cares about is untouched, so she still reaches for the same reading and the same
            // concern - but the identity weight behind both is gone, and the reaction that comes
            // out is the same reaction held far less hard.
            Assert.Equal(ValueConcern.Knowledge, before.Concern);
            Assert.Equal(ValueConcern.Knowledge, after.Concern);
            Assert.Equal(IdentityDomain.Alchemy, before.Interpretation.LensDomain);
            Assert.Equal(IdentityDomain.Alchemy, after.Interpretation.LensDomain);

            Assert.Contains("plausible knowledge alchemy (authored work 'apothecary') 0.30", before.ConcernTerms);
            Assert.Contains("plausible knowledge alchemy (no identity facet) 0.00", after.ConcernTerms);
            Assert.True(after.Interpretation.Confidence < before.Interpretation.Confidence);
            Assert.True(after.Intensity < before.Intensity);
        }

        [Fact]
        public void TransientFeelingBiasesTheResponseItBearsOn()
        {
            NarrativeWorldState world = Town();
            NarrativeNpc herder = world.Registry.GetNpc(EntityId.Parse("npc_ilfa"));
            Assert.Contains("emotion relief 0.00", React(world, herder).ResponseTerms);

            herder.Emotions.Affect(EmotionalState.Relief, 1.0, GameTime.Zero);
            Assert.Contains("emotion relief 0.10", React(world, herder).ResponseTerms);
        }

        [Fact]
        public void HowOddSomethingLandsIsTheObserversOwnTierAndNothingElse()
        {
            NarrativeWorldState world = Town();
            NarrativeNpc herder = world.Registry.GetNpc(EntityId.Parse("npc_ilfa"));
            ActorReaction unfazed = React(world, herder);

            herder.Quirk.Weirdness = CharacterWeirdnessTier.MostlyOrdinary;
            ActorReaction struck = React(world, herder);

            Assert.Equal(WeirdnessLevel.Mundane, unfazed.Registers);
            Assert.Equal(WeirdnessLevel.AbsurdPremiseCentral, struck.Registers);
            Assert.Equal(unfazed.Concern, struck.Concern);
            Assert.Equal(unfazed.Response, struck.Response);

            // A premise nobody staged is nobody's to be struck by, whatever their tier.
            Assert.Equal(WeirdnessLevel.Mundane, ReactionDerivation.RegistersAs(herder, WeirdnessLevel.Mundane));
        }

        [Fact]
        public void TheSameStateReactsTheSameWayEveryTime()
        {
            NarrativeWorldState world = Town();
            NarrativeNpc reeve = world.Registry.GetNpc(EntityId.Parse("npc_devek"));

            ActorReaction first = React(world, reeve);
            ActorReaction second = React(world, reeve);

            Assert.Equal(first.Signature, second.Signature);
            Assert.Equal(first.Intensity, second.Intensity);
            Assert.Equal(first.ConcernTerms, second.ConcernTerms);
            Assert.Equal(first.ResponseTerms, second.ResponseTerms);
            Assert.Equal(first.Interpretation.DerivedFactId, second.Interpretation.DerivedFactId);
        }

        [Fact]
        public void TheReactionIsInspectable()
        {
            NarrativeWorldState world = Town();
            NarrativeNpc reeve = world.Registry.GetNpc(EntityId.Parse("npc_devek"));
            string report = NarrativeInspector.DescribeReaction(world, React(world, reeve));

            Assert.Contains("reaction for devek", report);
            Assert.Contains("unchanged", report);
            Assert.Contains("read as: may be sabotaged", report);
            Assert.Contains("concern: Law", report);
            Assert.Contains("response: AskAuthority", report);
            Assert.Contains("premise: AbsurdPremiseCentral registers as AbsurdPremiseCentral", report);
            Assert.Contains("identity eligibility authority (authored work 'reeve') 0.20", report);
            Assert.Contains("interpretation lens public order 0.25", report);
            Assert.Contains("style preference ask authority 0.80", report);
            Assert.Contains("concern law reaches for ask authority 0.35", report);
        }

        private static ActorReaction React(NarrativeWorldState world, NarrativeNpc actor)
        {
            return ReactionDerivation.React(world, actor.Id, SourceFact, Absurd, GameTime.Zero);
        }

        private static IReadOnlyList<ActorReaction> ReactAll(NarrativeWorldState world)
        {
            return Names
                .Select(name => React(world, world.Registry.GetNpc(EntityId.Parse("npc_" + name))))
                .ToList();
        }

        private static readonly string[] Names = { "ilfa", "sarn", "devek", "mira", "ovin", "tess" };

        private static NarrativeWorldState Town(string premise = Premise)
        {
            NarrativeWorldState world = new NarrativeWorldState(64);
            Fact ruined = new Fact(SourceFact, Crop, FactPredicates.Damaged, EntityId.None, premise);
            ruined.EvidenceIds.Add(Sample);
            world.Knowledge.AddFact(ruined);

            // Six people, described by what they care about and how they handle things - never by
            // what they are. No branch anywhere below reads an occupation string; the identity
            // layer does, and only ever to say what somebody could plausibly know.
            NarrativeNpc herder = Actor("ilfa", "shepherd", ValueConcern.Animals);
            herder.Sensitivities.Animals = 0.9;
            herder.ProblemSolving.Wait = 0.8;
            herder.Personality.Patience = 0.9;
            herder.Quirk.Assigned = true;
            herder.Quirk.Weirdness = CharacterWeirdnessTier.Unforgettable;
            herder.Quirk.Kind = CharacterQuirk.GreetsDoors;

            NarrativeNpc trader = Actor("sarn", "merchant", ValueConcern.Wealth);
            trader.ProblemSolving.PaySomeone = 0.8;
            trader.Personality.Generosity = 0.6;

            NarrativeNpc reeve = Actor("devek", "reeve", ValueConcern.Law);
            reeve.Roles.Add("authority");
            reeve.Sensitivities.Theft = 0.8;
            reeve.Sensitivities.Dishonesty = 0.8;
            reeve.ProblemSolving.AskAuthority = 0.8;
            reeve.Personality.Orderliness = 0.9;

            NarrativeNpc apothecary = Actor("mira", "apothecary", ValueConcern.Knowledge);
            apothecary.ProblemSolving.DoItSelf = 0.8;
            apothecary.Personality.Curiosity = 0.9;

            NarrativeNpc devout = Actor("ovin", string.Empty, ValueConcern.Faith);
            devout.ProblemSolving.SeekReligiousHelp = 0.8;
            devout.Personality.Earnestness = 0.9;

            NarrativeNpc anxious = Actor("tess", string.Empty, ValueConcern.Family);
            anxious.Sensitivities.FamilyThreat = 0.9;
            anxious.ProblemSolving.Flee = 0.7;
            anxious.Personality.Boldness = 0.15;
            anxious.Emotions.Affect(EmotionalState.Fear, 0.8, GameTime.Zero);

            foreach (NarrativeNpc npc in new[] { herder, trader, reeve, apothecary, devout, anxious })
            {
                world.Registry.Add(npc);
            }

            return world;
        }

        /// <summary>
        /// Somebody who holds one concern above the rest. Every other value is left worth little
        /// rather than worth nothing, so a concern that wins does so by being worth more than a
        /// live alternative rather than by being the only one on the board.
        /// </summary>
        private static NarrativeNpc Actor(string key, string work, ValueConcern held)
        {
            NarrativeNpc npc = new NarrativeNpc(EntityId.Parse("npc_" + key), key)
            {
                Occupation = work
            };

            foreach (ValueConcern concern in new[]
            {
                ValueConcern.Family, ValueConcern.Wealth, ValueConcern.Law, ValueConcern.Faith,
                ValueConcern.Status, ValueConcern.Animals, ValueConcern.Knowledge, ValueConcern.Freedom
            })
            {
                npc.Values.Get(concern).Importance = concern == held ? 0.95 : 0.1;
                npc.Values.Get(concern).Flexibility = 0.2;
            }

            foreach (SensitivityTopic topic in new[]
            {
                SensitivityTopic.PublicEmbarrassment, SensitivityTopic.UnpaidDebt,
                SensitivityTopic.FamilyThreat, SensitivityTopic.Animals, SensitivityTopic.Status,
                SensitivityTopic.Theft, SensitivityTopic.Violence, SensitivityTopic.Dishonesty
            })
            {
                npc.Sensitivities.Set(topic, 0.1);
            }

            return npc;
        }
    }
}
