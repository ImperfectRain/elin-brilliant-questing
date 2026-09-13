using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-021. The world can be asked for more than one kind of trouble, and asking still changes
    /// nothing.
    ///
    /// Before this step generic production was one settlement's theft arithmetic: a town holding a
    /// broken mill, a failed shop, a shortage, an open debt and a guild with plans proposed exactly
    /// nothing, because the only producer in the world knew how to read a pocket. The seam BQ-103
    /// built already compared reuse against hypothetical creation; it had one producer to compare.
    ///
    /// So the fixture is deliberately a town with several different kinds of trouble in it at once,
    /// each in the store that actually owns it, and the tests are organised around the three things
    /// that keep "more producers" from meaning "a quest generator":
    ///
    /// <list type="bullet">
    /// <item>production is a read - nothing is committed, minted, transferred or opened;</item>
    /// <item>production binds causes the world already held - a producer cannot commit the theft
    /// that would make its own proposal worth making;</item>
    /// <item>a requirement is a description until somebody else fulfils it, and identity is stable
    /// enough that the same trouble read twice is one proposal rather than two.</item>
    /// </list>
    /// </summary>
    public class SituationProposalEcologyTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Mill = EntityId.Parse("zone_mill");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Neighbour = EntityId.Parse("npc_neighbour");
        private static readonly EntityId Miller = EntityId.Parse("npc_miller");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Shop = EntityId.Parse("business_shop");
        private static readonly EntityId Guild = EntityId.Parse("org_guild");
        private static readonly EntityId Watch = EntityId.Parse("org_watch");

        // -- the done-when ---------------------------------------------------------------------

        /// <summary>
        /// The step's condition: several distinct families propose from one world pass, ranked
        /// through the existing authority, and the pass can be inspected without the world moving.
        ///
        /// "Distinct families" is checked by name rather than by count, because the interesting
        /// failure is a producer set that has grown in size while still reading one store. Ranking
        /// is checked by handing the same proposals to <see cref="SituationProposalSelection"/>
        /// directly: the ecology must not hold a second opinion about order.
        ///
        /// And the world is proved unchanged the strongest way available - the whole save before
        /// and the whole save after - because a producer that quietly taught somebody a fact or
        /// minted an id would otherwise pass every other assertion here.
        /// </summary>
        [Fact]
        public void SeveralFamiliesProposeFromOneWorldPassAndTheWorldDoesNotMove()
        {
            NarrativeWorldState world = Town_();
            string before = WorldStateSerializer.Save(world);

            SituationProposalPass pass = SituationProposalEcology.Standard().Read(world);

            Assert.Equal(
                new[]
                {
                    SituationProposalFamilies.Business,
                    SituationProposalFamilies.Institution,
                    SituationProposalFamilies.Property
                },
                pass.Families);

            // Each of the enumerated producers, over the store that owns its condition.
            Assert.Contains(pass.Offers, o => o.ProducerId == "property/unresolved_crime");
            Assert.Contains(pass.Offers, o => o.ProducerId == "property/damaged");
            Assert.Contains(pass.Offers, o => o.ProducerId == "business/service_continuity");
            Assert.Contains(pass.Offers, o => o.ProducerId == "business/local_supply");
            Assert.Contains(pass.Offers, o => o.ProducerId == "institution/open_obligation");
            Assert.Contains(pass.Offers, o => o.ProducerId == "institution/mandate");

            // Order is the existing ranking authority's, not a second one kept here.
            Assert.Equal(
                SituationProposalSelection.Rank(pass.Offers.Select(o => o.Proposal)).Select(p => p.Key),
                pass.Offers.Select(o => o.Key));

            // Threadless conditions are the ordinary case, not an edge one: none of this town's
            // trouble is anybody's matter yet, and all of it still proposes.
            Assert.All(pass.Offers, o => Assert.True(o.Cause.Threadless));
            Assert.Empty(pass.Suppressed);

            // The producer set is enumerated rather than universal: the town's unproven secret and
            // its evidence conflict are real conditions that nothing here claims to make anything
            // of, and a pressure reaching no producer is a perfectly ordinary outcome.
            Assert.Contains(pass.Conditions, c => c.HasPressure(DevelopmentPressures.UnprovenKnowledge));
            Assert.DoesNotContain(
                pass.Offers,
                o => o.Cause.PressureTags.Contains(DevelopmentPressures.UnprovenKnowledge));

            // Inspecting is reading. So is doing it twice.
            string dump = NarrativeInspector.DescribeSituationProposals(world);
            Assert.Contains("situation proposals: " + pass.Offers.Count, dump);
            Assert.Equal(dump, NarrativeInspector.DescribeSituationProposals(world));
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        // -- recognition is not creation ---------------------------------------------------------

        /// <summary>
        /// A producer binds a wrong somebody already committed, and has no way to commit one.
        ///
        /// The founding-incident boundary in one pair of reads: with the theft claim in the world
        /// the property producer proposes and names the thief and the man he took from, exactly as
        /// the claim records them; with the claim superseded - the matter put right, nothing
        /// deleted - the same producer over the same town proposes nothing at all. It cannot make
        /// the incident it needs, which is the difference between recognising a theft and
        /// performing one in order to have something to recognise.
        /// </summary>
        [Fact]
        public void AProducerBindsAnIncidentTheWorldAlreadyHeldAndCannotMintOne()
        {
            NarrativeWorldState world = Town_();

            SituationProposalOffer crime = Assert.Single(
                SituationProposalEcology.Standard().Read(world).Offers,
                o => o.ProducerId == "property/unresolved_crime");

            Assert.Equal(UnresolvedCrimeProducer.Archetype, crime.Proposal.Candidate.ArchetypeId);
            Assert.Equal(Thief, crime.Proposal.Candidate.ActorIn(SituationRoles.Actor));
            Assert.Equal(Victim, crime.Proposal.Candidate.ActorIn(SituationRoles.Target));
            Assert.Equal(TheftFactId(world), crime.Cause.FocusFactId);
            Assert.False(crime.RequiresCreation);

            // The town is otherwise untouched, and the theft is put right.
            string before = WorldStateSerializer.Save(world);
            world.Knowledge.GetFact(TheftFactId(world)).Truth = TruthState.Superseded;

            SituationProposalPass after = SituationProposalEcology.Standard().Read(world);
            Assert.DoesNotContain(after.Offers, o => o.ProducerId == "property/unresolved_crime");
            Assert.DoesNotContain(after.Suppressed, s => s.Offer.ProducerId == "property/unresolved_crime");

            // The rest of the town still proposes: answering one condition answered one condition.
            Assert.Contains(after.Offers, o => o.Family == SituationProposalFamilies.Business);
            Assert.Contains(after.Offers, o => o.Family == SituationProposalFamilies.Institution);
            Assert.NotEqual(before, WorldStateSerializer.Save(world));
        }

        // -- requirements stay descriptions ------------------------------------------------------

        /// <summary>
        /// A proposal may say what it would still need, and saying it creates nothing.
        ///
        /// The watch has a goal on its own records and nobody on its roll to carry it, so the
        /// mandate producer declares a hand. The requirement is priced by BQ-103's existing weights
        /// and carried into the ranking as a cost, and that is the entire extent of it: no actor
        /// exists, no id was minted, the key is proposal-local rather than anything the registry
        /// would recognise, and the body is exactly as short-handed after the pass as before.
        /// </summary>
        [Fact]
        public void AHypotheticalRequirementIsPricedAndStaysADescription()
        {
            NarrativeWorldState world = Town_();
            string before = WorldStateSerializer.Save(world);

            SituationProposalOffer mandate = Assert.Single(
                SituationProposalEcology.Standard().Read(world).Offers,
                o => o.ProducerId == "institution/mandate" && o.Cause.DevelopmentId.Contains(Watch.Value));

            Assert.True(mandate.RequiresCreation);
            SituationActorRequirement hand = Assert.Single(
                mandate.Proposal.Candidate.ActorRequirements, a => a.RequiresCreation);
            Assert.Equal(SituationProposalRoles.Party, hand.Role);
            Assert.Equal("mandate/" + Watch.Value + "/hand", hand.CreationKey);
            Assert.Equal(EntityId.None, hand.ExistingActor);

            // Priced by the existing policy, and the cost is the only thing the requirement does.
            Assert.Equal(4, mandate.Proposal.CreationCost);
            Assert.Equal(mandate.Proposal.Candidate.Score - 4, mandate.Proposal.RankingScore);
            Assert.Contains("requires new actor mandate/" + Watch.Value + "/hand", mandate.Proposal.Explain());

            // Nothing was created, reserved or enrolled by describing it.
            Assert.Null(world.Registry.GetNpc(EntityId.Parse(hand.CreationKey)));
            Assert.Empty(world.Registry.GetOrganization(Watch).MemberIds);
            Assert.Equal(before, WorldStateSerializer.Save(world));

            // The body that does have a hand asks for nothing.
            SituationProposalOffer guild = Assert.Single(
                SituationProposalEcology.Standard().Read(world).Offers,
                o => o.ProducerId == "institution/mandate" && o.Cause.DevelopmentId.Contains(Guild.Value));
            Assert.False(guild.RequiresCreation);
        }

        // -- the existing authorities still decide -----------------------------------------------

        /// <summary>
        /// A matter somebody is already telling is not proposed again, and the director's admission
        /// gate refuses the whole pass rather than being re-derived here.
        ///
        /// Both refusals keep their proposal and their reason, so an empty offer list can be told
        /// apart from a quiet world - the same distinction the settlement owner already draws, for
        /// the same reason.
        /// </summary>
        [Fact]
        public void AnExistingMatterAndTheAdmissionGateBothSuppressWithTheirReasons()
        {
            NarrativeWorldState world = Town_();
            NarrativeThread matter = new NarrativeThread(world.NewId("thr"), "the theft", GameTime.Zero);
            matter.FactIds.Add(TheftFactId(world));
            world.Threads.Add(matter);

            SituationProposalPass carried = SituationProposalEcology.Standard().Read(world);
            SuppressedProposal already = Assert.Single(
                carried.Suppressed, s => s.Offer.ProducerId == "property/unresolved_crime");
            Assert.Contains("an existing matter already carries", already.Reason);
            Assert.DoesNotContain(carried.Offers, o => o.ProducerId == "property/unresolved_crime");

            // The rest of the town is threadless and still proposes.
            Assert.Contains(carried.Offers, o => o.ProducerId == "business/service_continuity");

            world.AttentionBudget.MaximumLiveThreads = 0;
            SituationProposalPass full = SituationProposalEcology.Standard().Read(world);
            Assert.Empty(full.Offers);
            Assert.Empty(full.Families);
            Assert.Null(full.Best);
            Assert.Contains(full.Suppressed, s => s.Reason == "live-thread budget is full");
        }

        // -- identity and revalidation -----------------------------------------------------------

        /// <summary>
        /// The same trouble read twice is one proposal, across a pass and across a save, and a
        /// cause can be asked later whether it still holds.
        ///
        /// That is the whole of what BQa-022 will need before it fulfils anything: a key it can
        /// route back to an owner, a binding it can compare, and an honest answer to "is this still
        /// true?" - which has three different wrong answers, each checked here. The condition has
        /// gone; something the proposal bound has gone; somebody else's matter took it over while
        /// the proposal sat.
        /// </summary>
        [Fact]
        public void ACauseKeepsItsIdentityAcrossASaveAndCanBeAskedAgainWhetherItStillHolds()
        {
            NarrativeWorldState world = Town_();
            SituationProposalPass pass = SituationProposalEcology.Standard().Read(world);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            SituationProposalPass again = SituationProposalEcology.Standard().Read(reloaded);

            Assert.Equal(pass.Offers.Select(o => o.Key), again.Offers.Select(o => o.Key));
            Assert.Equal(pass.Offers.Select(o => o.BindingKey), again.Offers.Select(o => o.BindingKey));
            Assert.Equal(pass.Explain(), again.Explain());

            IReadOnlyList<Development> current = DevelopmentDetector.Detect(world);
            Assert.All(pass.Offers, o => Assert.Null(o.Cause.RevalidationRefusal(world, current)));
            Assert.NotNull(pass.Find(pass.Best.Key));
            Assert.Null(pass.Find("no such proposal"));

            // The condition is gone: the guild got what it wanted.
            SituationProposalOffer mandate = Assert.Single(
                pass.Offers, o => o.ProducerId == "institution/mandate" && o.Cause.DevelopmentId.Contains(Guild.Value));
            world.Registry.GetOrganization(Guild).Goals[0].Satisfied = true;
            Assert.Contains(
                "no longer holds",
                mandate.Cause.RevalidationRefusal(world, DevelopmentDetector.Detect(world)));

            // Something it bound is gone: the shopkeeper has left the registry.
            SituationProposalOffer shop = Assert.Single(
                pass.Offers, o => o.ProducerId == "business/service_continuity");
            NarrativeWorldState departed = Town_(omit: Shopkeeper);
            Assert.Contains(
                "no longer in the registry",
                shop.Cause.RevalidationRefusal(departed, DevelopmentDetector.Detect(departed)));

            // Somebody else's matter took it over: the proposal was made about a threadless
            // condition, which is exactly the one that can be overtaken while it waits.
            NarrativeWorldState overtaken = Town_();
            NarrativeThread matter = new NarrativeThread(overtaken.NewId("thr"), "the theft", GameTime.Zero);
            matter.FactIds.Add(TheftFactId(overtaken));
            overtaken.Threads.Add(matter);
            SituationProposalOffer crime = Assert.Single(
                pass.Offers, o => o.ProducerId == "property/unresolved_crime");
            Assert.True(crime.Cause.Threadless);
            Assert.Contains(
                "an existing matter now carries",
                crime.Cause.RevalidationRefusal(overtaken, DevelopmentDetector.Detect(overtaken)));
        }

        // -- the producer set --------------------------------------------------------------------

        /// <summary>
        /// The set is enumerated and each member owns one stable name, because the key that routes
        /// a selected proposal back to its owner is half producer id.
        /// </summary>
        [Fact]
        public void ProducersAreNamedOnceAndTheSetRefusesAmbiguity()
        {
            SituationProposalEcology standard = SituationProposalEcology.Standard();
            Assert.Equal(6, standard.Producers.Count);
            Assert.Equal(
                standard.Producers.Count,
                standard.Producers.Select(p => p.ProducerId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(
                standard.Producers,
                p => Assert.Contains(
                    p.Family,
                    new[]
                    {
                        SituationProposalFamilies.Property,
                        SituationProposalFamilies.Business,
                        SituationProposalFamilies.Institution
                    }));

            Assert.Throws<ArgumentNullException>(() => new SituationProposalEcology(null));
            Assert.Throws<ArgumentException>(() => new SituationProposalEcology(
                new ISituationProposalProducer[] { new LocalSupplyProducer(), new LocalSupplyProducer() }));
            Assert.Throws<ArgumentException>(() => new SituationProposalEcology(
                new ISituationProposalProducer[] { null }));

            // An empty world is quiet rather than broken, and so is a null one.
            Assert.Empty(standard.Read(null).Offers);
            Assert.Empty(standard.Read(new NarrativeWorldState(3)).Offers);
            Assert.Equal("situation proposals: no world\n", NarrativeInspector.DescribeSituationProposals(null));
        }

        // -- fixture -------------------------------------------------------------------------------

        private static EntityId TheftFactId(NarrativeWorldState world)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.Stole) return fact.Id;
            }

            return EntityId.None;
        }

        /// <summary>
        /// One small town holding several different kinds of trouble at once, each in the store
        /// that owns it, and none of it anybody's matter yet. Nothing here is a proposal.
        ///
        /// <paramref name="omit"/> leaves one person out of the registry, which is the same town
        /// as far as every store is concerned and a stale binding as far as a proposal is.
        /// </summary>
        private static NarrativeWorldState Town_(EntityId omit = default(EntityId))
        {
            NarrativeWorldState world = new NarrativeWorldState(17);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeSite(Mill, "the mill", "workshop"));
            if (omit != Thief) world.Registry.Add(new NarrativeNpc(Thief, "Garron"));
            if (omit != Victim) world.Registry.Add(new NarrativeNpc(Victim, "Tovar"));
            if (omit != Neighbour) world.Registry.Add(new NarrativeNpc(Neighbour, "Elsi"));
            if (omit != Miller) world.Registry.Add(new NarrativeNpc(Miller, "Bran") { Occupation = "miller" });
            if (omit != Shopkeeper)
                world.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira") { Occupation = "shopkeeper" });

            // Property: a theft the world holds as true that nothing has answered, and a secret
            // about it that somebody believes and cannot demonstrate.
            Fact theft = new Fact(
                world.NewId("fact"), Thief, FactPredicates.Stole, Victim, truth: TruthState.True, secrecy: 60);
            world.Knowledge.AddFact(theft);
            world.Knowledge.Teach(Neighbour, theft.Id, KnowledgeSource.Inference, 0.6, GameTime.Zero, canProve: false);

            // Property: the mill wheel is broken. Nobody's crime, and pressing all the same.
            world.Knowledge.AddFact(new Fact(
                world.NewId("fact"), Mill, FactPredicates.Damaged, EntityId.None, "mill_wheel"));

            // Business: the miller's stated need, and the town shortage that cites it.
            Fact need = new Fact(
                world.NewId("fact"), Miller, FactPredicates.Needs, EntityId.None, "grain of ordinary quality");
            world.Knowledge.AddFact(need);
            world.Demands.AddOrUpdate(Town, LocalDemandCategory.Food, 70, GameTime.Zero, GameTime.FromDays(30), need.Id);

            // Business: a shop that failed, recorded as durable continuity meaning.
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Town, Shopkeeper, GameTime.Zero);
            businesses.TryChangeState(Shop, BusinessContinuityState.Failed, GameTime.FromDays(2));

            // Social: an open debt between two people.
            world.Obligations.Add(new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Favor,
                Neighbour,
                Miller,
                EntityId.None,
                "owes a day's work",
                GameTime.Zero,
                EntityId.None));

            // Institutional: a guild with a hand to carry its plans, and a watch with none.
            Organization guild = new Organization(Guild, "the carters", "guild") { LeaderId = Shopkeeper };
            guild.SiteIds.Add(Town);
            guild.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 40));
            world.Registry.Add(guild);

            Organization watch = new Organization(Watch, "the ford watch", "watch");
            watch.SiteIds.Add(Town);
            watch.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, EntityId.None, 30));
            world.Registry.Add(watch);

            return world;
        }
    }
}
