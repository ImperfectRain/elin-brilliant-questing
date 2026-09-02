using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SettlementSituationGeneratorTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Market = EntityId.Parse("zone_market");
        private static readonly EntityId Hamlet = EntityId.Parse("zone_hamlet");
        private static readonly EntityId Elsewhere = EntityId.Parse("zone_a_completely_different_name");

        // -- A. a quiet world stays quiet -------------------------------------------------------

        [Fact]
        public void QuietSettlementDoesNotGenerateBecauseAQuestIsNeeded()
        {
            Lab lab = new Lab(Market);
            lab.Local("baker", "Baker", money: 300, greed: 0.5);
            lab.Local("neighbour", "Neighbour", money: 220, greed: 0.5);
            lab.Local("porter", "Porter", money: 180, greed: 0.5);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.Empty(plan.Candidates);
            Assert.Empty(plan.Suppressed);
            Assert.Equal(3, plan.Profile.Actors.Count);
            Assert.Contains("carried value here: 0", plan.Profile.Features);
            Assert.Null(new SettlementSituationGenerator().TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now));
        }

        // -- B. generation from unstaged world state --------------------------------------------

        [Fact]
        public void FreshSaveGeneratesTheftFromLocalPressure()
        {
            Lab lab = PressuredMarket();

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Single(lab.World.Threads);
            Assert.Equal(ThreadState.Active, situation.Thread.State);
            Assert.NotEmpty(situation.Thread.GenerationCauses);
            Assert.DoesNotContain(situation.Thread.OpenQuestions, q => q.StartsWith("Cause: "));
            Assert.False(lab.World.Knowledge.Knows(Player, situation.TheftFactId));
        }

        // -- C. vanilla mutation is authoritative -----------------------------------------------

        [Fact]
        public void TheItemActuallyMovesBeforeTheTheftIsRecorded()
        {
            Lab lab = PressuredMarket();

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Contains(situation.ItemId, lab.Vanilla.GetInventory(situation.ThiefId).Select(i => i.Id));
            Assert.DoesNotContain(situation.ItemId, lab.Vanilla.GetInventory(situation.VictimId).Select(i => i.Id));
        }

        [Fact]
        public void ARefusedTransferRecordsNoTheftAtAll()
        {
            Lab lab = PressuredMarket();
            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            SettlementSituationPlan plan = generator.Evaluate(lab.World, lab.Vanilla, Market);
            Assert.NotEmpty(plan.Candidates);

            // The plan was read, and then the game moved on: the ring is gone before anything was
            // committed. Vanilla owns the outcome, so its refusal has to end the whole attempt.
            Assert.True(lab.Vanilla.TryDestroyItem(lab.Item, lab.Victim));

            Assert.Null(generator.TryGenerate(lab.World, lab.Vanilla, plan, Market, lab.Vanilla.Now));
            Assert.Empty(lab.World.Threads);
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Type == WorldEventType.Theft);
            Assert.Empty(lab.World.Knowledge.Facts);
        }

        // -- D. an unwitnessed theft is an ordinary theft ----------------------------------------

        [Fact]
        public void TwoPeopleAloneProduceAnUnwitnessedTheft()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.True(situation.WitnessId.IsNone);
            Assert.Equal(2, situation.Thread.ParticipantIds.Count);
            Assert.DoesNotContain(situation.Thread.Escalation, step => step.Id == "witness_talks");
            Assert.Contains(situation.Thread.GenerationCauses, c => c.Contains("nobody is placed to see it"));

            // Nobody learns a theft they could not have seen - not the player, not the victim.
            WorldEvent theft = lab.World.Ledger.Events.Single(e => e.Type == WorldEventType.Theft);
            Assert.Empty(theft.Witnesses);
            Assert.False(lab.World.Knowledge.Knows(Player, situation.TheftFactId));
            Assert.False(lab.World.Knowledge.Knows(situation.VictimId, situation.TheftFactId));
        }

        // -- E. a witnessed theft still binds and teaches a witness ------------------------------

        [Fact]
        public void AWitnessedTheftBindsTheMostLikelyObserver()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);

            // Registered first, but half asleep. Chosen by attention, so the later, sharper local wins.
            lab.Local("dozer", "Dozer", money: 140, greed: 0.4, perception: 1);
            EntityId sharp = lab.Local("clerk", "Clerk", money: 140, greed: 0.4, perception: 12, spotHidden: 4);

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Equal(sharp, situation.WitnessId);
            Assert.True(lab.World.Knowledge.TryGetBelief(sharp, situation.TheftFactId, out KnowledgeRecord seen));
            Assert.Equal(KnowledgeSource.Witnessed, seen.Source);
            Assert.Contains(situation.Thread.Escalation, step => step.Id == "witness_talks");
            Assert.False(lab.World.Knowledge.Knows(Player, situation.TheftFactId));
        }

        [Fact]
        public void OrdinarySocialActorsRemainEligibleForTheftRoles()
        {
            Lab lab = PressuredMarket();

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.NotEmpty(plan.Candidates);
            SituationCandidate best = plan.Candidates[0];
            Assert.Equal(lab.Thief, best.ActorIn(SituationRoles.Actor));
            Assert.Equal(lab.Victim, best.ActorIn(SituationRoles.Target));
            Assert.Equal(NarrativeActorKind.Person, plan.Profile.Of(lab.Thief).ActorKind);
            Assert.Equal(SocialAgency.Full, plan.Profile.Of(lab.Thief).SocialAgency);
        }

        [Fact]
        public void LivestockDoesNotBecomePettyTheftPerpetratorBecauseItHasDexterityAndInventory()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900);
            EntityId cow = lab.Local(
                "cow",
                "Dairy Cow",
                money: 0,
                greed: 1.0,
                pickpocket: 20,
                stealth: 20,
                dexterity: 40,
                actorKind: NarrativeActorKind.Animal,
                socialAgency: SocialAgency.None);
            lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.NotNull(plan.Profile.Of(cow));
            Assert.Equal(NarrativeActorKind.Animal, plan.Profile.Of(cow).ActorKind);
            Assert.DoesNotContain(plan.Candidates, c => c.ActorIn(SituationRoles.Actor) == cow);
        }

        [Fact]
        public void AnimalsStayInTheLocalAffordanceProfile()
        {
            Lab lab = PressuredMarket();
            EntityId goat = lab.Local(
                "goat",
                "Goat",
                money: 0,
                greed: 0.2,
                actorKind: NarrativeActorKind.Animal,
                socialAgency: SocialAgency.None);

            LocalAffordanceProfile profile = LocalAffordanceProfile.Read(lab.World, lab.Vanilla, Market);

            Assert.NotNull(profile.Of(goat));
            Assert.Equal(3, profile.SocialActorCount);
            Assert.Equal(1, profile.OtherLivingActorCount);
            Assert.Contains("other living locals: 1", profile.Features);
        }

        [Fact]
        public void MutationPolicyIsIndependentOfActorKind()
        {
            Lab lab = PressuredMarket();
            lab.Vanilla.SetActorKind(lab.Thief, NarrativeActorKind.Animal);
            lab.Vanilla.SetSocialAgency(lab.Thief, SocialAgency.Full);
            lab.Vanilla.SetActorClass(lab.Victim, NarrativeActorClass.StoryCritical);

            Assert.True(MutationPolicies.Permits(lab.Vanilla.GetActorClass(lab.Thief), MutationKind.Inventory));
            Assert.False(MutationPolicies.Permits(lab.Vanilla.GetActorClass(lab.Victim), MutationKind.Inventory));
            Assert.Equal(NarrativeActorKind.Animal, lab.Vanilla.GetActorKind(lab.Thief));
        }

        [Fact]
        public void UnknownSocialAgencyFailsSafelyForTheftRoles()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900);
            lab.Thief = lab.Local(
                "stranger",
                "Unreadable Stranger",
                money: 15,
                greed: 0.8,
                pickpocket: 8,
                stealth: 6,
                actorKind: NarrativeActorKind.Unknown,
                socialAgency: SocialAgency.Unknown);
            lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.NotNull(plan.Profile.Of(lab.Thief));
            Assert.DoesNotContain(plan.Candidates, c => c.ActorIn(SituationRoles.Actor) == lab.Thief);
        }

        [Fact]
        public void StoryCriticalActorsAreNotSelectedForInventoryMutatingTheftRoles()
        {
            Lab lab = new Lab(Market);
            EntityId storyVictim = lab.Local(
                "duke",
                "Duke",
                money: 1000,
                greed: 0.2,
                carriedValue: 2000,
                actorClass: NarrativeActorClass.StoryCritical);
            EntityId storyThief = lab.Local(
                "oracle",
                "Oracle",
                money: 0,
                greed: 1.0,
                pickpocket: 20,
                stealth: 20,
                actorClass: NarrativeActorClass.StoryCritical);
            lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900);
            lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            Assert.DoesNotContain(plan.Candidates, c => c.ActorIn(SituationRoles.Actor) == storyThief);
            Assert.DoesNotContain(plan.Candidates, c => c.ActorIn(SituationRoles.Target) == storyVictim);
            Assert.False(lab.Vanilla.TryTransferItem(EntityId.Parse("item_duke_valuable"), storyVictim, storyThief));
            Assert.Contains(lab.Vanilla.Refusals, r => r.Contains("StoryCritical") && r.Contains("Inventory"));
        }

        [Fact]
        public void TheftOpportunityCountsSocialBystandersSeparatelyFromOtherLivingActors()
        {
            Lab lab = PressuredMarket();
            lab.Local("goat", "Goat", money: 0, greed: 0.2, actorKind: NarrativeActorKind.Animal, socialAgency: SocialAgency.None);
            lab.Local("cow", "Cow", money: 0, greed: 0.2, actorKind: NarrativeActorKind.Animal, socialAgency: SocialAgency.None);

            SituationCandidate best = BestOf(lab);

            Assert.Contains(best.Causes, c => c.Contains("1 socially capable local(s) nearby"));
            Assert.Contains(best.Causes, c => c.Contains("2 other living local(s) also present"));
        }

        // -- F. motive dimensions are independent ------------------------------------------------

        [Fact]
        public void PovertyAndGreedMoveMotiveIndependently()
        {
            int poorAndPlain = MotiveOf(money: 10, greed: 0.2);
            int poorAndGreedy = MotiveOf(money: 10, greed: 0.9);
            int comfortableAndPlain = MotiveOf(money: 400, greed: 0.2);
            int comfortableAndGreedy = MotiveOf(money: 400, greed: 0.9);

            // Greed moves motive at a fixed purse.
            Assert.True(poorAndGreedy > poorAndPlain);
            Assert.True(comfortableAndGreedy > comfortableAndPlain);

            // Poverty moves motive at a fixed disposition.
            Assert.True(poorAndPlain > comfortableAndPlain);
            Assert.True(poorAndGreedy > comfortableAndGreedy);

            // And neither is derivable from the other: a comfortable greedy person and a destitute
            // scrupulous one are different candidates, not the same one counted twice.
            Assert.NotEqual(poorAndPlain, comfortableAndGreedy);
        }

        // -- G. opportunity is derived, not a constant -------------------------------------------

        [Fact]
        public void OpportunityRespondsToWhoElseIsThereAndHowAlertTheyAre()
        {
            SituationCandidate alone = BestOf(WithBystanders());
            SituationCandidate watchedByADozer = BestOf(WithBystanders(("dozer", 1, 0)));
            SituationCandidate watchedByAWatchman = BestOf(WithBystanders(("watchman", 14, 6)));
            SituationCandidate watchedByACrowd = BestOf(WithBystanders(
                ("dozer", 1, 0), ("hauler", 1, 0), ("child", 1, 0)));

            int Opportunity(SituationCandidate c) => c.Pressure(PettyTheftPressure.Opportunity);

            // An empty street affords the most; each bystander and each alert eye takes some away.
            Assert.True(Opportunity(alone) > Opportunity(watchedByADozer));
            Assert.True(Opportunity(watchedByADozer) > Opportunity(watchedByAWatchman));
            Assert.True(Opportunity(watchedByADozer) > Opportunity(watchedByACrowd));

            // The other three pressures are untouched, so the difference is opportunity and nothing
            // else - which is what makes it a derived term rather than a constant.
            Assert.Equal(alone.Pressure(PettyTheftPressure.Motive), watchedByAWatchman.Pressure(PettyTheftPressure.Motive));
            Assert.Equal(alone.Pressure(PettyTheftPressure.Means), watchedByAWatchman.Pressure(PettyTheftPressure.Means));
            Assert.True(alone.Score > watchedByAWatchman.Score);
            Assert.Contains(watchedByAWatchman.Causes, c => c.Contains("Watchman is present and attentive"));
        }

        // -- H. two generative settlements, materially different ---------------------------------

        [Fact]
        public void DifferentSettlementStructuresYieldDifferentCandidateDistributions()
        {
            // A market: one conspicuously rich trader among modest locals, and a specialist thief.
            Lab market = new Lab(Market);
            market.Local("merchant", "Merchant", money: 900, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            market.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            market.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);
            market.Local("porter", "Porter", money: 120, greed: 0.3);

            // A hamlet: no trade, no specialist, flat wealth, but two heirlooms and hungry
            // neighbours with nimble hands. It generates - differently.
            Lab hamlet = new Lab(Hamlet);
            hamlet.Local("farmer", "Farmer", money: 60, greed: 0.7, carriedValue: 600, dexterity: 14);
            hamlet.Local("herbalist", "Herbalist", money: 55, greed: 0.75, carriedValue: 520, dexterity: 13);
            hamlet.Local("miner", "Miner", money: 50, greed: 0.8, dexterity: 15);

            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            SettlementSituationPlan marketPlan = generator.Evaluate(market.World, market.Vanilla, Market);
            SettlementSituationPlan hamletPlan = generator.Evaluate(hamlet.World, hamlet.Vanilla, Hamlet);

            // Both places are generative. This is not "one has a valuable and the other does not".
            Assert.NotEmpty(marketPlan.Candidates);
            Assert.NotEmpty(hamletPlan.Candidates);
            Assert.True(hamletPlan.Profile.TotalCarriedValue > 0);

            // The market concentrates: one specialist, one mark, roles that do not overlap. The
            // hamlet spreads across several pairings, and the same people appear on both sides of
            // them - neighbours who could each rob the other.
            Assert.Single(marketPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Actor)).Distinct());
            Assert.Single(marketPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Target)).Distinct());
            Assert.True(hamletPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Actor)).Distinct().Count() > 1);
            Assert.True(hamletPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Target)).Distinct().Count() > 1);

            HashSet<EntityId> marketThieves = marketPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Actor)).ToHashSet();
            Assert.DoesNotContain(marketPlan.Candidates, c => marketThieves.Contains(c.ActorIn(SituationRoles.Target)));
            HashSet<EntityId> hamletThieves = hamletPlan.Candidates.Select(c => c.ActorIn(SituationRoles.Actor)).ToHashSet();
            Assert.Contains(hamletPlan.Candidates, c => hamletThieves.Contains(c.ActorIn(SituationRoles.Target)));

            // And they say different things about why. The market's story is a conspicuous trader:
            // wealth read against a much higher local middle, and trade on top of it.
            Assert.True(marketPlan.Profile.MedianMoney > hamletPlan.Profile.MedianMoney);
            Assert.Contains(marketPlan.Candidates[0].Causes, c => c.Contains("trades for a living"));
            Assert.DoesNotContain(hamletPlan.Candidates[0].Causes, c => c.Contains("trades for a living"));
            Assert.True(
                marketPlan.Candidates[0].Pressure(PettyTheftPressure.TargetWorth)
                > hamletPlan.Candidates[0].Pressure(PettyTheftPressure.TargetWorth));

            // The hamlet's is need among equals: a larger share of its score is motive, even though
            // the market's specialist is the poorer person in absolute terms. Cross-multiplied
            // rather than divided so the comparison stays exact.
            Assert.True(
                hamletPlan.Candidates[0].Pressure(PettyTheftPressure.Motive) * marketPlan.Candidates[0].Score
                > marketPlan.Candidates[0].Pressure(PettyTheftPressure.Motive) * hamletPlan.Candidates[0].Score);
        }

        // -- I. the place's name is not an input --------------------------------------------------

        [Fact]
        public void EquivalentAffordancesGenerateEquivalentlyUnderAnyZoneName()
        {
            SettlementSituationPlan here = BestPlanFor(Market);
            SettlementSituationPlan there = BestPlanFor(Elsewhere);

            Assert.Equal(here.Candidates.Count, there.Candidates.Count);
            for (int i = 0; i < here.Candidates.Count; i++)
            {
                Assert.Equal(here.Candidates[i].Score, there.Candidates[i].Score);
                Assert.Equal(here.Candidates[i].ArchetypeId, there.Candidates[i].ArchetypeId);
                Assert.Equal(here.Candidates[i].ActorIn(SituationRoles.Actor), there.Candidates[i].ActorIn(SituationRoles.Actor));
                Assert.Equal(here.Candidates[i].ActorIn(SituationRoles.Target), there.Candidates[i].ActorIn(SituationRoles.Target));
                Assert.Equal(here.Candidates[i].ActorIn(SituationRoles.Witness), there.Candidates[i].ActorIn(SituationRoles.Witness));
                Assert.Equal(here.Candidates[i].Causes, there.Candidates[i].Causes);
            }

            // The only thing that differs is the place each is bound to.
            Assert.Equal(Market, here.Candidates[0].SiteIn(SituationRoles.Place));
            Assert.Equal(Elsewhere, there.Candidates[0].SiteIn(SituationRoles.Place));
        }

        // -- J. family and dependents are weighted when the graph offers them --------------------

        [Fact]
        public void FamilyAtRiskOutranksAMerelyBusinessTarget()
        {
            Lab lab = FamilyAndBusinessPressure(seed: 42);

            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);

            SituationCandidate best = plan.BestCandidate;
            Assert.NotNull(best);
            Assert.Contains(
                best.ActorIn(SituationRoles.Target),
                new[] { EntityId.Parse("npc_sibling"), EntityId.Parse("npc_spouse") });
            Assert.True(best.Pressure(PettyTheftPressure.PersonAtRisk) >= 40);
            Assert.Contains(best.Causes, c => c.Contains(" is at personal risk through "));

            SituationCandidate business = plan.Candidates.Single(c =>
                c.ActorIn(SituationRoles.Target) == EntityId.Parse("npc_merchant"));
            Assert.Equal(0, business.Pressure(PettyTheftPressure.PersonAtRisk));
        }

        [Fact]
        public void FamilyOrSpouseTargetsWinAClearMajorityWhenTheGraphOffersAChoice()
        {
            int domesticTargets = 0;
            const int runs = 100;

            for (int i = 0; i < runs; i++)
            {
                Lab lab = FamilyAndBusinessPressure((ulong)(1000 + i));
                PettyTheftSituation situation = new SettlementSituationGenerator()
                    .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

                Assert.NotNull(situation);
                if (situation.VictimId == EntityId.Parse("npc_sibling")
                    || situation.VictimId == EntityId.Parse("npc_spouse"))
                {
                    domesticTargets++;
                }
            }

            Assert.True(domesticTargets >= 75, domesticTargets + " domestic targets out of " + runs);
        }

        // -- K. generated premises name Irva rather than a parallel mythology -------------------

        [Fact]
        public void GeneratedPremisesNameVanillaIrvaInAClearMajority()
        {
            int anchored = 0;
            const int runs = 100;

            for (int i = 0; i < runs; i++)
            {
                Lab lab = IrvaAnchoredMarket((ulong)(1260 + i));
                SettlementSituationGenerator generator = new SettlementSituationGenerator();
                SettlementSituationPlan plan = generator.Evaluate(lab.World, lab.Vanilla, Market);

                Assert.NotEmpty(plan.Candidates);
                Assert.NotEmpty(plan.BestCandidate.SettingReferences);

                PettyTheftSituation situation = generator.TryGenerate(
                    lab.World,
                    lab.Vanilla,
                    plan,
                    Market,
                    lab.Vanilla.Now);

                Assert.NotNull(situation);
                if (situation.Thread.GenerationCauses.Any(c => c.StartsWith("setting: ")))
                {
                    anchored++;
                }
            }

            Assert.True(anchored >= 75, anchored + " Irva-anchored generated premises out of " + runs);
        }

        [Fact]
        public void SettingReferencesSurviveIntoTheThreadInspectorCauses()
        {
            Lab lab = IrvaAnchoredMarket(126);

            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            Assert.NotNull(situation);
            Assert.Contains(situation.Thread.GenerationCauses, c => c.Contains("Noyel"));
            Assert.Contains(situation.Thread.GenerationCauses, c => c.Contains("Merchants Guild"));
        }

        // -- L. the world does not tell the same story twice --------------------------------------

        [Fact]
        public void TheSameCausalTheftIsNotGeneratedAgainButADistinctOneRemainsEligible()
        {
            Lab lab = PressuredMarket();

            // A second mark, so there is a genuinely different story available afterwards.
            EntityId jeweller = lab.Local("jeweller", "Jeweller", money: 700, greed: 0.3, carriedValue: 800, occupation: "trader");

            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            PettyTheftSituation first = generator.TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);
            Assert.NotNull(first);

            // Give the original victim something new to lose: the pressure that produced the first
            // theft reads exactly the same afterwards, so only suppression stops a repeat.
            lab.Vanilla.GiveItem(first.VictimId, new ItemDescriptor(
                EntityId.Parse("item_merchant_second"), "silver chain", "jewelry", 900, "ring"));

            SettlementSituationPlan again = generator.Evaluate(lab.World, lab.Vanilla, Market);

            Assert.DoesNotContain(again.Candidates, c =>
                c.ActorIn(SituationRoles.Actor) == first.ThiefId
                && c.ActorIn(SituationRoles.Target) == first.VictimId);
            Assert.Contains(again.Suppressed, s =>
                s.Candidate.ActorIn(SituationRoles.Actor) == first.ThiefId
                && s.Candidate.ActorIn(SituationRoles.Target) == first.VictimId);
            Assert.Contains(again.Suppressed, s => s.Reason.Contains("already exists"));

            // The same thief moving on to a different mark is a different story, and stays eligible.
            Assert.Contains(again.Candidates, c => c.ActorIn(SituationRoles.Target) == jeweller);
        }

        [Fact]
        public void ARecentTheftInTheLedgerSuppressesARepeatEvenWithoutALiveThread()
        {
            Lab lab = PressuredMarket();
            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            PettyTheftSituation first = generator.TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);
            Assert.NotNull(first);

            // The thread is done with, but the ledger still remembers what happened last week.
            first.Thread.State = ThreadState.Resolved;
            lab.Vanilla.AdvanceDays(7);
            lab.Vanilla.GiveItem(first.VictimId, new ItemDescriptor(
                EntityId.Parse("item_merchant_second"), "silver chain", "jewelry", 900, "ring"));

            SettlementSituationPlan soon = generator.Evaluate(lab.World, lab.Vanilla, Market);
            Assert.Contains(soon.Suppressed, s => s.Reason.Contains("was already recorded stealing from"));
            Assert.DoesNotContain(soon.Candidates, c =>
                c.ActorIn(SituationRoles.Actor) == first.ThiefId
                && c.ActorIn(SituationRoles.Target) == first.VictimId);
        }

        [Fact]
        public void TheRepetitionWindowExpires()
        {
            Lab lab = PressuredMarket();
            SettlementSituationGenerator generator = new SettlementSituationGenerator();
            PettyTheftSituation first = generator.TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);
            Assert.NotNull(first);
            first.Thread.State = ThreadState.Resolved;
            lab.Vanilla.GiveItem(first.VictimId, new ItemDescriptor(
                EntityId.Parse("item_merchant_second"), "silver chain", "jewelry", 900, "ring"));

            // Suppression is a memory, not a permanent bar. Measured against the clock rather than
            // against whenever the ledger last recorded anything, so a quiet world still forgets.
            lab.Vanilla.AdvanceDays(SettlementSituationGenerator.RepetitionWindowDays + 1);

            SettlementSituationPlan later = generator.Evaluate(lab.World, lab.Vanilla, Market);
            Assert.Contains(later.Candidates, c =>
                c.ActorIn(SituationRoles.Actor) == first.ThiefId
                && c.ActorIn(SituationRoles.Target) == first.VictimId);
        }

        [Fact]
        public void WitnessSelectionDoesNotDependOnRegistrationOrder()
        {
            // Two bystanders the world cannot tell apart. Whichever is chosen, it must be the same
            // one whichever order they were registered in - the old behaviour took the first in the
            // collection, which made the witness an artefact of how the zone was enumerated.
            EntityId first = WitnessWhenBystandersAre("alma", "bruno");
            EntityId reversed = WitnessWhenBystandersAre("bruno", "alma");

            Assert.False(first.IsNone);
            Assert.Equal(first, reversed);
        }

        private static EntityId WitnessWhenBystandersAre(string firstKey, string secondKey)
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("mark", "Mark", money: 800, greed: 0.3, carriedValue: 900);
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Local(firstKey, firstKey, money: 140, greed: 0.3, perception: 7);
            lab.Local(secondKey, secondKey, money: 140, greed: 0.3, perception: 7);

            return BestOf(lab).ActorIn(SituationRoles.Witness);
        }

        // -- M. persistence ------------------------------------------------------------------------

        [Fact]
        public void GeneratedSituationSurvivesSaveReloadWithoutRedispatch()
        {
            Lab lab = PressuredMarket();
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Single(reloaded.Threads);
            NarrativeThread thread = reloaded.Threads[0];
            Assert.Equal(PettyTheftSituation.ArchetypeId, thread.ArchetypeId);
            Assert.Equal(situation.Thread.OriginEventId, thread.OriginEventId);
            Assert.Equal(situation.Thread.GenerationCauses, thread.GenerationCauses);
            Assert.Single(reloaded.Ledger.Events, e => e.Type == WorldEventType.Theft);
        }

        // -- N. the inspector can account for the whole of it -------------------------------------

        [Fact]
        public void InspectorNamesEveryPressureBehindAGeneratedSituation()
        {
            Lab lab = PressuredMarket();
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            string report = Explain(lab, situation);

            Assert.Contains("generated from world state", report);
            Assert.Contains("Cutpurse has motive", report);
            Assert.Contains("Cutpurse has means", report);
            Assert.Contains("Merchant is a target", report);
            Assert.Contains("opportunity:", report);
            Assert.Contains("Clerk is present and attentive", report);
        }

        [Fact]
        public void InspectorSaysWhenNobodySawIt()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            PettyTheftSituation situation = new SettlementSituationGenerator()
                .TryGenerate(lab.World, lab.Vanilla, Market, lab.Vanilla.Now);

            string report = Explain(lab, situation);

            Assert.Contains("nobody is placed to see it", report);

            // Diagnostics are not disclosure: the hidden truth stays out of what the player can read.
            Assert.DoesNotContain(situation.Thread.OpenQuestions, q => q.Contains("motive"));
            Assert.False(lab.World.Knowledge.Knows(Player, situation.TheftFactId));
        }

        // -- helpers --------------------------------------------------------------------------------

        private static string Explain(Lab lab, PettyTheftSituation situation)
        {
            ActionContext context = new ActionContext(
                lab.World,
                lab.Vanilla,
                new FixedCheckResolver(CheckOutcome.Pass),
                lab.World.Rng,
                Player,
                situation.VictimId);

            return NarrativeInspector.Explain(
                lab.World,
                lab.Vanilla,
                StandardActions.CreateRegistry(),
                context,
                situation.Thread);
        }

        private static Lab PressuredMarket()
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Witness = lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);
            lab.Item = EntityId.Parse("item_merchant_valuable");
            return lab;
        }

        private static Lab FamilyAndBusinessPressure(ulong seed)
        {
            Lab lab = new Lab(Market, seed);
            EntityId thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            EntityId sibling = lab.Local("sibling", "Sibling", money: 260, greed: 0.3, carriedValue: 520);
            EntityId spouse = lab.Local("spouse", "Spouse", money: 250, greed: 0.3, carriedValue: 500);
            EntityId merchant = lab.Local("merchant", "Merchant", money: 900, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            EntityId clerk = lab.Local("clerk", "Clerk", money: 120, greed: 0.3, perception: 8);

            lab.Thief = thief;
            lab.Victim = sibling;
            lab.Witness = clerk;

            lab.World.Relationships.ConnectMutual(clerk, sibling, RelationKind.Family, 80);
            lab.World.Relationships.ConnectMutual(clerk, spouse, RelationKind.Spouse, 80);
            lab.World.Relationships.Connect(merchant, clerk, RelationKind.Employer, 70);
            lab.World.Relationships.Connect(clerk, merchant, RelationKind.Employee, 70);
            return lab;
        }

        private static Lab IrvaAnchoredMarket(ulong seed)
        {
            Lab lab = new Lab(Market, seed);
            lab.Victim = lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Witness = lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);
            lab.Item = EntityId.Parse("item_merchant_valuable");
            lab.World.Registry.Add(new NarrativeSite(Market, "Noyel market", "town"));
            lab.World.Registry.GetNpc(lab.Victim).Roles.Add("Merchants Guild");
            lab.World.Registry.GetNpc(lab.Thief).Occupation = "Derphy pickpocket";
            return lab;
        }

        private static SettlementSituationPlan BestPlanFor(EntityId zone)
        {
            Lab lab = new Lab(zone);
            lab.Local("merchant", "Merchant", money: 800, greed: 0.3, carriedValue: 900, occupation: "shopkeeper");
            lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            lab.Local("clerk", "Clerk", money: 140, greed: 0.3, perception: 10);
            return new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, zone);
        }

        /// <summary>One market, one thief, one mark, and whichever bystanders the case needs.</summary>
        private static Lab WithBystanders(params (string Key, int Perception, int SpotHidden)[] bystanders)
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("mark", "Mark", money: 800, greed: 0.3, carriedValue: 900);
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: 15, greed: 0.8, pickpocket: 8, stealth: 6);
            foreach ((string key, int perception, int spotHidden) in bystanders)
            {
                lab.Local(
                    key,
                    char.ToUpperInvariant(key[0]) + key.Substring(1),
                    money: 140,
                    greed: 0.3,
                    perception: perception,
                    spotHidden: spotHidden);
            }

            return lab;
        }

        private static SituationCandidate BestOf(Lab lab)
        {
            SettlementSituationPlan plan = new SettlementSituationGenerator().Evaluate(lab.World, lab.Vanilla, Market);
            return plan.Candidates.Single(c => c.ActorIn(SituationRoles.Actor) == lab.Thief
                                               && c.ActorIn(SituationRoles.Target) == lab.Victim);
        }

        /// <summary>The motive pressure for one thief, holding everything else fixed.</summary>
        private static int MotiveOf(int money, double greed)
        {
            Lab lab = new Lab(Market);
            lab.Victim = lab.Local("mark", "Mark", money: 500, greed: 0.3, carriedValue: 900);
            lab.Thief = lab.Local("cutpurse", "Cutpurse", money: money, greed: greed, pickpocket: 8, stealth: 6);

            LocalAffordanceProfile profile = LocalAffordanceProfile.Read(lab.World, lab.Vanilla, Market);
            SituationCandidate candidate = new PettyTheftPressure().Evaluate(
                lab.World,
                profile,
                profile.Of(lab.Thief),
                profile.Of(lab.Victim),
                witness: null);

            return candidate.Pressure(PettyTheftPressure.Motive);
        }

        private sealed class Lab
        {
            public readonly NarrativeWorldState World;
            public readonly SandboxVanillaState Vanilla = new SandboxVanillaState(Player);
            private readonly EntityId _zone;

            public EntityId Victim;
            public EntityId Thief;
            public EntityId Witness;
            public EntityId Item;

            public Lab(EntityId zone, ulong seed = 42)
            {
                World = new NarrativeWorldState(seed);
                _zone = zone;
                Vanilla.Define(Player, zone: zone);
            }

            /// <summary>
            /// Defines one local. Greed is passed in rather than derived from money on purpose: a
            /// fixture that made the poor greedy could never show that need and disposition are two
            /// inputs, because every case would move both at once.
            /// </summary>
            public EntityId Local(
                string key,
                string name,
                int money,
                double greed,
                int carriedValue = 0,
                int pickpocket = 0,
                int stealth = 0,
                int dexterity = -1,
                int perception = 4,
                int spotHidden = 0,
                string occupation = "local",
                NarrativeActorClass actorClass = NarrativeActorClass.OrdinaryCitizen,
                NarrativeActorKind actorKind = NarrativeActorKind.Person,
                SocialAgency socialAgency = SocialAgency.Full)
            {
                EntityId id = EntityId.Parse("npc_" + key);
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(id, name)
                {
                    Occupation = occupation,
                    Importance = NarrativeImportance.Background
                });
                npc.Personality.Greed = greed;

                Vanilla.Define(id, money: money, zone: _zone)
                    .SetActorClass(id, actorClass)
                    .SetActorKind(id, actorKind)
                    .SetSocialAgency(id, socialAgency)
                    .SetSkill(id, VanillaSkill.Pickpocket, pickpocket)
                    .SetSkill(id, VanillaSkill.Stealth, stealth)
                    .SetSkill(id, VanillaSkill.SpotHidden, spotHidden)
                    .SetAttribute(id, VanillaAttribute.Dexterity, dexterity < 0 ? pickpocket + stealth : dexterity)
                    .SetAttribute(id, VanillaAttribute.Perception, perception);

                if (carriedValue > 0)
                {
                    EntityId item = EntityId.Parse("item_" + key + "_valuable");
                    Vanilla.GiveItem(id, new ItemDescriptor(item, name.ToLowerInvariant() + " heirloom", "jewelry", carriedValue, "ring"));
                    if (Item.IsNone)
                    {
                        Item = item;
                    }
                }

                return id;
            }
        }
    }
}
