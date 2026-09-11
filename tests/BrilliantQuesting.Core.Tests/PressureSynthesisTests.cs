using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-006. The detector reads more of the world than one secret and one favour, and the two
    /// properties that stop "more rules" from meaning "more trouble".
    ///
    /// A pressure is a current condition that gives one or more actors reason to act. It is not a
    /// quest, a storylet, an event or a second state store, and broadening what counts as one is
    /// only safe while three things stay true: reading twice changes nothing, two readings of one
    /// standing condition are one pressure rather than two, and a live consumer can ask about the
    /// handful of things that changed instead of the whole save.
    ///
    /// The fixture world is deliberately one small town holding several different kinds of
    /// trouble at once - a killing nobody has answered, a broken mill, a shortage, a failed shop,
    /// a guild with plans, an open debt, a secret and a lie - because the interesting failures are
    /// between families, not inside one.
    /// </summary>
    public class PressureSynthesisTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Mill = EntityId.Parse("zone_mill");
        private static readonly EntityId Killer = EntityId.Parse("npc_killer");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Neighbour = EntityId.Parse("npc_neighbour");
        private static readonly EntityId Miller = EntityId.Parse("npc_miller");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Shop = EntityId.Parse("business_shop");
        private static readonly EntityId Guild = EntityId.Parse("org_guild");

        // -- the done-when -------------------------------------------------------------------

        /// <summary>
        /// Several distinct authoritative state families each produce a generic pressure, and none
        /// of them names or selects anything dramatic.
        ///
        /// The four the step requires are here - property/crime, economic and service continuity,
        /// social obligation and belief conflict, and an organization's own stakes - and each one
        /// comes from a different store: the knowledge graph, the demand ledger, the business
        /// ledger, the obligation ledger and the organization records. That is the point of the
        /// list. A detector that read one store and inferred the rest would be inventing most of
        /// this town's troubles.
        ///
        /// And the reading stays generic. No development names a storylet, a situation or a scene,
        /// the type has nowhere to put one, and detecting authored nothing: no fact, no event, no
        /// thread. The world is exactly as full of trouble after being read as before.
        /// </summary>
        [Fact]
        public void DistinctStateFamiliesProduceGenericPressureWithoutNamingAScene()
        {
            NarrativeWorldState world = Town_();
            int facts = world.Knowledge.Facts.Count;
            int events = world.Ledger.Events.Count;
            int threads = world.Threads.Count;

            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.DamagedProperty));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.Shortage));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.ServiceInterruption));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnmetObligation));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.EvidenceConflict));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.OrganizationStake));

            // Nothing dramatic is named or chosen. Every tag is from the controlled vocabulary,
            // and the type has no field a storylet id could hide in.
            HashSet<string> vocabulary = new HashSet<string>(typeof(DevelopmentPressures)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetValue(null)));
            foreach (Development pressure in pressures)
            {
                Assert.NotEmpty(pressure.PressureTags);
                foreach (string tag in pressure.PressureTags)
                {
                    Assert.Contains(tag, vocabulary);
                }
            }

            foreach (string dramatic in new[] { "Storylet", "StoryletId", "Situation", "Scene", "Quest" })
            {
                Assert.Empty(typeof(Development).GetMember(dramatic, BindingFlags.Public | BindingFlags.Instance));
            }

            Assert.Equal(facts, world.Knowledge.Facts.Count);
            Assert.Equal(events, world.Ledger.Events.Count);
            Assert.Equal(threads, world.Threads.Count);
        }

        /// <summary>
        /// Equivalent readings of one standing condition deduplicate; distinct causes do not.
        ///
        /// The mill's shortage arrives twice over: the miller's stated need is a claim in the
        /// knowledge graph, and the town's demand ledger holds an entry that cites that very
        /// claim as its source. They are one thing the world is short of, so they derive one
        /// pressure carrying both readings - the person who is short and the place that is short -
        /// rather than two pressures that would each have to be answered separately.
        ///
        /// The same town is also short of medicine, from nothing in particular. That stays its own
        /// pressure, because a consumer that could not tell the two apart could not later say
        /// which of them anybody actually addressed.
        /// </summary>
        [Fact]
        public void EquivalentReadingsDeduplicateAndDistinctCausesStayApart()
        {
            NarrativeWorldState world = Town_();

            IReadOnlyList<Development> shortages = DevelopmentDetector.Detect(world)
                .Where(d => d.HasPressure(DevelopmentPressures.Shortage))
                .ToList();

            Assert.Equal(2, shortages.Count);

            Development need = Assert.Single(shortages, d => d.FocusFactId == NeedFactId(world));
            Assert.Equal("dev.shortage:" + NeedFactId(world).Value, need.Id);

            // Both readings landed on it: the claim named the miller, the ledger entry named the
            // town, and the merged pressure knows both without either having been copied.
            Assert.Contains(Miller, need.SubjectIds);
            Assert.Contains(Town, need.SiteIds);

            // Severity is the ledger's, not the claim's default - a condition seen twice presses
            // as hard as the harder reading of it, and never as the sum of them.
            Assert.Equal(70, need.Urgency);

            Development medicine = Assert.Single(shortages, d => d.Id != need.Id);
            Assert.Equal("dev.shortage:" + Town.Value + ":Medicine", medicine.Id);
            Assert.True(medicine.FocusFactId.IsNone);
        }

        /// <summary>
        /// Repeated detection mutates nothing, over every family at once.
        ///
        /// BQ-069 proved this for two rules. It is worth proving again for a rule set that now
        /// touches five stores, because the cheap way to write several of these rules - index
        /// something, cache a lookup on the world, mark a record as seen - would pass every other
        /// test in this file and quietly make reading the world a way of changing it.
        /// </summary>
        [Fact]
        public void RepeatedDetectionOverEveryFamilyMutatesNothing()
        {
            NarrativeWorldState world = Town_();

            string before = WorldStateSerializer.Save(world);
            IReadOnlyList<Development> first = DevelopmentDetector.Detect(world);
            IReadOnlyList<Development> second = DevelopmentDetector.Detect(world);
            string after = WorldStateSerializer.Save(world);

            Assert.Equal(before, after);
            Assert.Equal(first.Select(d => d.Id), second.Select(d => d.Id));
            Assert.Equal(first.Select(d => d.Urgency), second.Select(d => d.Urgency));

            // And the same save derives the same pressures after a round trip, which is the only
            // honest way for a derived reading to survive a reload.
            IReadOnlyList<Development> reloaded = DevelopmentDetector.Detect(WorldStateSerializer.Load(after));
            Assert.Equal(first.Select(d => d.Id), reloaded.Select(d => d.Id));
            Assert.Equal(first.Select(d => d.Urgency), reloaded.Select(d => d.Urgency));
            Assert.Equal(
                first.Select(d => string.Join(",", d.PressureTags)),
                reloaded.Select(d => string.Join(",", d.PressureTags)));
        }

        /// <summary>
        /// Production detection can be bounded to an affected work set, and a bounded reading is
        /// the same reading.
        ///
        /// This is the property that keeps BQ-107 and BQ-108's bounded off-screen work bounded. A
        /// consumer that must scan every fact, business and organization in a large save to find
        /// out what changed has handed the budget straight back, so detection enumerates what the
        /// caller says was affected instead of walking each store and filtering.
        ///
        /// Same means same: what the shop's work set derives is exactly what a whole-world pass
        /// derives for the shop, tags, urgency, places and all.
        /// </summary>
        [Fact]
        public void DetectionCanBeBoundedToAnAffectedWorkSetWithoutChangingWhatItDerives()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> everything = DevelopmentDetector.Detect(world);

            DevelopmentScope scope = DevelopmentScope.Affected().Business(Shop).Organization(Guild);
            IReadOnlyList<Development> bounded = DevelopmentDetector.Detect(world, scope);

            Assert.Equal(
                everything.Where(d => d.Id.StartsWith("dev.business_continuity:")
                                      || d.Id.StartsWith("dev.organization_stake:"))
                          .Select(Signature),
                bounded.Select(Signature));

            // Nothing from the stores the caller did not name - and that is the saving, not a
            // filter applied after the fact.
            Assert.DoesNotContain(bounded, d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.DoesNotContain(bounded, d => d.HasPressure(DevelopmentPressures.Shortage));

            // A tick in which nothing changed derives nothing, and says so cheaply.
            Assert.Empty(DevelopmentDetector.Detect(world, DevelopmentScope.Affected()));

            // A work set answers for what it names. The mill's shortage is recorded in two stores,
            // so naming both reproduces the whole-world reading exactly...
            Development whole = Assert.Single(
                everything, d => d.Id == "dev.shortage:" + NeedFactId(world).Value);
            Development both = Assert.Single(DevelopmentDetector.Detect(
                world, DevelopmentScope.Affected().Fact(NeedFactId(world)).Site(Town)),
                d => d.Id == whole.Id);
            Assert.Equal(Signature(whole), Signature(both));

            // ...and naming only the place gives the ledger's half of it under the same identity,
            // which is what lets the pass that later names the claim land on this development
            // rather than mint a second one for the same shortage.
            Development half = Assert.Single(
                DevelopmentDetector.Detect(world, DevelopmentScope.Affected().Site(Town)),
                d => d.Id == whole.Id);
            Assert.Equal(whole.Id, half.Id);
            Assert.Equal(whole.Urgency, half.Urgency);
            Assert.Contains(Town, half.SiteIds);
            Assert.DoesNotContain(Miller, half.SubjectIds);

            // The work set is inspectable: a budget has to be able to answer "what did this pass
            // consider, and why those".
            Assert.Equal(2, scope.Count);
            Assert.Contains(Shop.Value, scope.ToString());
            Assert.Contains(Guild.Value, scope.ToString());
            Assert.Equal("scope: entire world", DevelopmentScope.EntireWorld.ToString());
        }

        // -- required coverage ---------------------------------------------------------------

        /// <summary>
        /// The cycle is not only crises. An organization's plan to grow reads as an opportunity,
        /// and a shop with a new operator behind the counter reads as recovering rather than as
        /// one more thing that has gone wrong.
        ///
        /// Both are still reasons to act - a guild recruiting wants people, a replacement operator
        /// wants custom - which is what makes them pressures at all. A detector that reported only
        /// failure would be telling the truth every time while describing a world nobody
        /// recognises.
        /// </summary>
        [Fact]
        public void PositiveAndRecoveringConditionsAreDerivedToo()
        {
            NarrativeWorldState world = Town_();

            Development growth = Assert.Single(
                DevelopmentDetector.Detect(world),
                d => d.HasPressure(DevelopmentPressures.Opportunity));
            Assert.True(growth.HasPressure(DevelopmentPressures.OrganizationStake));
            Assert.Contains(Guild, growth.SubjectIds);

            new BusinessContinuity(world).TryChangeState(
                Shop, BusinessContinuityState.ReplacementOperator, GameTime.FromDays(40));

            IReadOnlyList<Development> after = DevelopmentDetector.Detect(world);
            Development recovering = Assert.Single(after, d => d.HasPressure(DevelopmentPressures.Recovering));
            Assert.Equal("dev.business_continuity:" + Shop.Value, recovering.Id);
            Assert.DoesNotContain(after, d => d.HasPressure(DevelopmentPressures.ServiceInterruption));
            Assert.True(recovering.Urgency < 80);
        }

        /// <summary>
        /// A raid is a stake, not an opportunity, and the difference is kept.
        ///
        /// "The guild wants to expand" and "the guild wants to burn a rival out" are both
        /// unsatisfied organization goals with a weight, and a consumer that could not tell them
        /// apart would stage the wrong scene for both.
        /// </summary>
        [Fact]
        public void AnAdversarialOrganizationGoalIsNotReadAsAnOpportunity()
        {
            NarrativeWorldState world = Town_();
            Organization guild = world.Registry.GetOrganization(Guild);
            guild.Goals.Add(new OrganizationGoal(OrganizationActivity.RaidOrganization, EntityId.Parse("org_rival"), 70));

            Development raid = Assert.Single(
                DevelopmentDetector.Detect(world),
                d => d.HasPressure(DevelopmentPressures.Adversarial)
                     && d.HasPressure(DevelopmentPressures.OrganizationStake));

            Assert.False(raid.HasPressure(DevelopmentPressures.Opportunity));
            Assert.Equal(70, raid.Urgency);

            // Two goals of one guild are two stakes. They are separately answerable, so they are
            // separately derived.
            Assert.Equal(
                2,
                DevelopmentDetector.Detect(world).Count(d => d.HasPressure(DevelopmentPressures.OrganizationStake)));
        }

        /// <summary>
        /// Unsupported native facts stay unknown, and a working day is not a pressure.
        ///
        /// BQ-051 keeps the live service surface and durable continuity meaning apart precisely so
        /// that a sleeping shopkeeper and an empty shelf cannot be mistaken for a business in
        /// trouble. Core cannot see Elin's stock at all, so the detector reads the ledger's
        /// recorded meaning and nothing else: the projection may quite correctly say the counter
        /// is unattended right now, and no pressure is derived from it.
        /// </summary>
        [Fact]
        public void ALiveServiceSurfaceCannotMintABusinessPressure()
        {
            NarrativeWorldState world = Town_();
            BusinessContinuity businesses = new BusinessContinuity(world);
            EntityId inn = EntityId.Parse("business_inn");
            Assert.True(businesses.TryRegister(inn, Town, Neighbour, GameTime.Zero));

            BusinessProjection asleep = businesses.Project(
                inn, new BusinessServiceSnapshot(OperatorAvailability.Sleeping, false), GameTime.FromDays(1));
            Assert.NotEqual(ServiceContinuitySurface.Available, asleep.Surface);

            Assert.DoesNotContain(
                DevelopmentDetector.Detect(world),
                d => d.Id == "dev.business_continuity:" + inn.Value);
        }

        /// <summary>
        /// A source change that never reached a thread is still detectable.
        ///
        /// Threads are how the world decides a matter is worth tracking, and that decision sits
        /// above this layer. A need nobody has opened a matter about presses exactly as much as
        /// one that has: the pressure simply carries no thread, and says so.
        /// </summary>
        [Fact]
        public void PressureWithNoThreadIsStillDerived()
        {
            NarrativeWorldState world = Town_();
            Assert.Empty(world.Threads);

            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);
            Assert.NotEmpty(pressures);
            Assert.All(pressures, d => Assert.True(d.ThreadId.IsNone));

            // Opening a matter around the mill does not create the pressure - it was already
            // derived - it only gives the existing reading somewhere to point.
            NarrativeThread matter = new NarrativeThread(world.NewId("thr"), "the broken mill", GameTime.Zero);
            matter.FactIds.Add(DamageFactId(world));
            world.Threads.Add(matter);

            Development damage = Assert.Single(
                DevelopmentDetector.Detect(world),
                d => d.HasPressure(DevelopmentPressures.DamagedProperty));
            Assert.Equal(matter.Id, damage.ThreadId);
        }

        /// <summary>
        /// A belief conflict is read objectively and teaches nobody anything.
        ///
        /// The shopkeeper sincerely holds a garbled version of the killing, one that names the
        /// wrong man. That the graph holds two claims about one matter, one of them false and
        /// believed, is an objective condition and derives a pressure. What it must not do is
        /// settle whose version is right for whom: the believer still believes the false claim
        /// afterwards, and detection has taught her nothing about the true one.
        /// </summary>
        [Fact]
        public void ABeliefConflictIsObjectiveAndCorrectsNobody()
        {
            NarrativeWorldState world = Town_();

            Development conflict = Assert.Single(
                DevelopmentDetector.Detect(world),
                d => d.HasPressure(DevelopmentPressures.EvidenceConflict));

            // It points at the matter that actually happened, and names who is caught up in it:
            // the believer, the man her version blames, and the man who actually did it.
            Assert.Equal(KillingFactId(world), conflict.FocusFactId);
            Assert.Contains(Shopkeeper, conflict.SubjectIds);
            Assert.Contains(Miller, conflict.SubjectIds);
            Assert.Contains(Killer, conflict.SubjectIds);

            Assert.True(world.Knowledge.Knows(Shopkeeper, RumourFactId(world)));
            Assert.False(world.Knowledge.Knows(Shopkeeper, KillingFactId(world)));
        }

        // -- pressure ends when its condition does --------------------------------------------

        /// <summary>
        /// Every new family stops being derived when the authoritative state behind it changes,
        /// and only then. Nothing here is resolved, closed or marked done, because there is
        /// nothing to resolve - which is the whole reason developments are not saved.
        /// </summary>
        [Fact]
        public void EachNewPressureDisappearsWithTheConditionThatProducedIt()
        {
            NarrativeWorldState world = Town_();

            // A wrong somebody has put right is no longer unanswered. An ending belongs to the
            // matter that ended, so the claim is filed on the matter, exactly where thread
            // resolution files it - and it names the man the matter was about.
            NarrativeThread matter = new NarrativeThread(world.NewId("thr"), "the killing", GameTime.Zero);
            matter.FactIds.Add(KillingFactId(world));
            world.Threads.Add(matter);

            Assert.Contains(
                DevelopmentDetector.Detect(world),
                d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));

            Fact ending = new Fact(
                world.NewId("fact"), Neighbour, FactPredicates.Settled, Killer, "blood_price_paid");
            world.Knowledge.AddFact(ending);
            matter.FactIds.Add(ending.Id);

            // The mill is repaired: the claim that it is broken is superseded, not deleted.
            world.Knowledge.GetFact(DamageFactId(world)).Truth = TruthState.Superseded;

            // The shortage is answered, which shortens it rather than completing a counter.
            world.Demands.Relieve(Town, LocalDemandCategory.Food, NeedFactId(world), 70, 5, GameTime.FromDays(10));
            world.Knowledge.GetFact(NeedFactId(world)).Truth = TruthState.Superseded;

            // The debt is settled and the guild gets what it wanted.
            world.Obligations.Records[0].Fulfill(GameTime.FromDays(10));
            world.Registry.GetOrganization(Guild).Goals[0].Satisfied = true;

            IReadOnlyList<Development> left = DevelopmentDetector.Detect(world);

            Assert.DoesNotContain(left, d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.DoesNotContain(left, d => d.HasPressure(DevelopmentPressures.DamagedProperty));
            Assert.DoesNotContain(left, d => d.Id == "dev.shortage:" + NeedFactId(world).Value);
            Assert.DoesNotContain(left, d => d.HasPressure(DevelopmentPressures.UnmetObligation));
            Assert.DoesNotContain(left, d => d.HasPressure(DevelopmentPressures.OrganizationStake));

            // The shop is still failed, the town is still short of medicine and the secret is
            // still unproven: answering one condition does not quietly answer the rest.
            Assert.Contains(left, d => d.Id == "dev.shortage:" + Town.Value + ":Medicine");
            Assert.Contains(left, d => d.HasPressure(DevelopmentPressures.ServiceInterruption));
            Assert.Contains(left, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));
        }

        /// <summary>
        /// The unbounded reading is shared, so it cannot be turned into somebody's work set by
        /// accident. Asking for a bounded pass is asking for one by name.
        /// </summary>
        [Fact]
        public void TheUnboundedScopeCannotBeMutated()
        {
            Assert.Throws<System.InvalidOperationException>(() => DevelopmentScope.EntireWorld.Fact(Town));
            Assert.True(DevelopmentScope.EntireWorld.IsEntireWorld);
            Assert.False(DevelopmentScope.Affected().IsEntireWorld);
        }

        // -- fixture ---------------------------------------------------------------------------

        private static string Signature(Development development)
        {
            return development.Id
                   + "|" + string.Join(",", development.PressureTags)
                   + "|" + development.Urgency
                   + "|" + string.Join(",", development.SubjectIds.Select(id => id.Value))
                   + "|" + string.Join(",", development.SiteIds.Select(id => id.Value));
        }

        private static EntityId KillingFactId(NarrativeWorldState world) => Find(world, FactPredicates.Killed, TruthState.True);

        private static EntityId RumourFactId(NarrativeWorldState world) => Find(world, FactPredicates.Killed, TruthState.False);

        private static EntityId DamageFactId(NarrativeWorldState world) => Find(world, FactPredicates.Damaged, null);

        private static EntityId NeedFactId(NarrativeWorldState world) => Find(world, FactPredicates.Needs, null);

        private static EntityId Find(NarrativeWorldState world, string predicate, TruthState? truth)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Predicate == predicate && (truth == null || fact.Truth == truth.Value))
                {
                    return fact.Id;
                }
            }

            return EntityId.None;
        }

        /// <summary>
        /// One small town holding several different kinds of trouble at once, each in the store
        /// that actually owns it. Nothing here is a development, and nothing here anticipates one.
        /// </summary>
        private static NarrativeWorldState Town_()
        {
            NarrativeWorldState world = new NarrativeWorldState(11);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeSite(Mill, "the mill", "workshop"));
            world.Registry.Add(new NarrativeNpc(Killer, "Garron"));
            world.Registry.Add(new NarrativeNpc(Victim, "Tovar"));
            world.Registry.Add(new NarrativeNpc(Neighbour, "Elsi"));
            world.Registry.Add(new NarrativeNpc(Miller, "Bran") { Occupation = "miller" });
            world.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira") { Occupation = "shopkeeper" });

            // Crime: a killing the world holds as true, that nobody can prove and nothing has
            // answered. Two pressures, deliberately - unresolved and unproven are different.
            Fact killing = new Fact(
                world.NewId("fact"), Killer, FactPredicates.Killed, Victim, truth: TruthState.True, secrecy: 60);
            world.Knowledge.AddFact(killing);
            world.Knowledge.Teach(Neighbour, killing.Id, KnowledgeSource.Inference, 0.6, GameTime.Zero, canProve: false);

            // Belief conflict: a sincerely held, garbled version of the same matter.
            Fact rumour = new Fact(
                world.NewId("fact"), Miller, FactPredicates.Killed, Victim, truth: TruthState.False)
            {
                DistortionOf = killing.Id
            };
            world.Knowledge.AddFact(rumour);
            world.Knowledge.Teach(Shopkeeper, rumour.Id, KnowledgeSource.Hearsay, 0.7, GameTime.Zero, canProve: false);

            // Property: the mill wheel is broken. Nobody's crime, and pressing all the same.
            world.Knowledge.AddFact(new Fact(
                world.NewId("fact"), Mill, FactPredicates.Damaged, EntityId.None, "mill_wheel"));

            // Economy: the miller's stated need, and the town-level shortage that cites it.
            Fact need = new Fact(
                world.NewId("fact"), Miller, FactPredicates.Needs, EntityId.None, "grain of ordinary quality");
            world.Knowledge.AddFact(need);
            world.Demands.AddOrUpdate(Town, LocalDemandCategory.Food, 70, GameTime.Zero, GameTime.FromDays(30), need.Id);
            world.Demands.AddOrUpdate(Town, LocalDemandCategory.Medicine, 30, GameTime.Zero, GameTime.FromDays(20), EntityId.None);

            // Service continuity: a shop that failed, recorded as durable meaning.
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Town, Shopkeeper, GameTime.Zero);
            businesses.TryChangeState(Shop, BusinessContinuityState.Failed, GameTime.FromDays(2));

            // Social obligation: an open debt between two people.
            world.Obligations.Add(new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Favor,
                Neighbour,
                Miller,
                EntityId.None,
                "owes a day's work",
                GameTime.Zero,
                EntityId.None));

            // Organization stake: a guild that wants to grow.
            Organization guild = new Organization(Guild, "the carters", "guild") { LeaderId = Shopkeeper };
            guild.SiteIds.Add(Town);
            guild.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 40));
            world.Registry.Add(guild);

            return world;
        }
    }
}
