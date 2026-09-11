using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-007. One objective pressure, several people, and what each of them can legitimately be
    /// under pressure about.
    ///
    /// The detector is omniscient by construction - it reads authoritative state, and authoritative
    /// state is what is actually so. The property under test here is that nothing downstream may
    /// inherit that omniscience: a true condition nobody has a route to presses on nobody, a claim
    /// that is false presses on whoever sincerely holds it, and what somebody stands to lose never
    /// becomes a way of finding out what they stand to lose it over.
    ///
    /// The fixture is one town where each of those cases is live at the same time, because the
    /// interesting failures are between people rather than inside one reading.
    /// </summary>
    public class ActorPressureViewTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Mill = EntityId.Parse("zone_mill");
        private static readonly EntityId Killer = EntityId.Parse("npc_killer");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Reeve = EntityId.Parse("npc_reeve");
        private static readonly EntityId Kin = EntityId.Parse("npc_kin");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Miller = EntityId.Parse("npc_miller");
        private static readonly EntityId Shop = EntityId.Parse("business_shop");
        private static readonly EntityId Guild = EntityId.Parse("org_guild");

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// One pressure, four people, four different answers - and the differences are about
        /// position, not temperament.
        ///
        /// The killing is a single development. The witness holds the claim and cannot demonstrate
        /// it; the reeve holds it with proof and is answerable for it by office; the victim's kin
        /// hold nothing, care enormously, and get no reading at all. Three readings of one pressure
        /// and one silence is the whole shape of the step.
        /// </summary>
        [Fact]
        public void OneObjectivePressureReadsDifferentlyForDifferentPeople()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);
            Development killing = Crime(pressures);

            ActorLocalPressure witness = Only(world, Witness, killing);
            ActorLocalPressure reeve = Only(world, Reeve, killing);

            Assert.Equal(ActorPressureCertainty.Suspected, witness.Certainty);
            Assert.Equal(ActorPressureCertainty.Proven, reeve.Certainty);
            Assert.NotEqual(witness.Urgency, reeve.Urgency);

            // The reeve can prove it, so the reeve can place the people it names. The witness
            // cannot, so for the witness who did it is still an open question.
            Assert.Contains(Killer, reeve.SubjectIds);
            Assert.DoesNotContain(Killer, witness.SubjectIds);
            Assert.Contains("implicated party", witness.Unknown);
            Assert.Empty(reeve.Unknown);

            // Office is a stake, not a route: it raises what the matter costs the reeve and it is
            // read off the identity owner rather than from a second opinion about occupations.
            Assert.True(reeve.HasStake(ActorStakeKind.Office));
            Assert.False(witness.HasStake(ActorStakeKind.Office));

            // The victim's kin want this answered more than anybody. They have no route to it -
            // though they are of course still under the pressures their own village is plainly in.
            Assert.Empty(Matching(world, Kin, pressures, killing));
        }

        /// <summary>
        /// A true fact nobody has a route to presses on nobody, however much they would care.
        ///
        /// The stranger is given everything except a way of knowing: the strongest possible tie to
        /// the victim, maximal sensitivity to violence, a value profile that cares about nothing
        /// else, and a home in the same town. The correct answer is still nothing, and it has to be
        /// nothing rather than a faint reading - "you are not in this" and "you are in this and do
        /// not care" are different answers, and a consumer handed the second for the first will
        /// eventually let somebody act on a matter they never heard of.
        /// </summary>
        [Fact]
        public void AHiddenTrueFactCreatesNoPressureForSomebodyWithNoRouteToIt()
        {
            NarrativeWorldState world = Town_();
            NarrativeNpc stranger = world.Registry.GetNpc(Stranger);
            stranger.HomeSiteId = Town;
            stranger.Sensitivities.Violence = 1.0;
            stranger.Sensitivities.Theft = 1.0;
            stranger.Values.Law.Importance = 1.0;
            world.Relationships.ConnectMutual(Stranger, Victim, RelationKind.Family, 90);

            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);
            Development killing = Crime(pressures);

            Assert.Contains(Victim, killing.SubjectIds);
            Assert.Empty(Matching(world, Stranger, pressures, killing));
        }

        /// <summary>
        /// The required false-belief path: a sincere error with no objectively true condition under
        /// it is still a real reason to act.
        ///
        /// The miller believes somebody stole from him. Nobody did - the claim is false, there is no
        /// matching true fact anywhere in the graph, and the detector emits nothing about it at all,
        /// because its knowledge rules test for truth and return. So the reading cannot be recovered
        /// by filtering the detector's output, and it is not: the miller's own beliefs are read.
        ///
        /// He is not told he is wrong. <c>SincerelyMistaken</c> records the disagreement for a test
        /// and an inspector; nothing the actor consumes is different because of it.
        /// </summary>
        [Fact]
        public void ASincerelyHeldFalsehoodIsARealActorLocalPressure()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            // Nothing objective corresponds to it, from either direction.
            Assert.DoesNotContain(pressures, d => d.FocusFactId == SuspicionFactId(world));
            Assert.DoesNotContain(
                world.Knowledge.Facts.Values,
                f => f.Predicate == FactPredicates.Stole && f.Truth == TruthState.True);

            ActorLocalPressure believed = Assert.Single(
                ActorPressureView.Of(world, Miller, pressures),
                p => p.FocusFactId == SuspicionFactId(world));

            Assert.True(believed.SincerelyMistaken);
            Assert.False(believed.HasObjectiveCause);
            Assert.Empty(believed.DevelopmentId);
            Assert.True(believed.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.True(believed.Urgency > 0);
            Assert.Equal(ActorPressureCertainty.Believed, believed.Certainty);
        }

        /// <summary>
        /// Reading it twice gives the same answer, and reading it at all changes nothing.
        ///
        /// Both halves matter. Determinism is not free here: beliefs are stored per knower in a
        /// dictionary whose enumeration order is not a contract, so the sort is load-bearing rather
        /// than tidy. And purity is the line between a derived view and a second authority - this
        /// projects belief, it never teaches it, which is exactly where <c>ActorLocalInterpreter</c>
        /// differs and why the two are not the same component.
        /// </summary>
        [Fact]
        public void RepeatedInterpretationIsDeterministicAndMutatesNothing()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            int facts = world.Knowledge.Facts.Count;
            int events = world.Ledger.Events.Count;
            int threads = world.Threads.Count;
            int beliefs = world.Knowledge.BeliefsOf(Witness).Count() + world.Knowledge.BeliefsOf(Reeve).Count();

            foreach (EntityId actor in new[] { Witness, Reeve, Miller, Shopkeeper, Stranger })
            {
                string first = Render(ActorPressureView.Of(world, actor, pressures));
                string second = Render(ActorPressureView.Of(world, actor, pressures));
                Assert.Equal(first, second);
            }

            Assert.Equal(facts, world.Knowledge.Facts.Count);
            Assert.Equal(events, world.Ledger.Events.Count);
            Assert.Equal(threads, world.Threads.Count);
            Assert.Equal(beliefs, world.Knowledge.BeliefsOf(Witness).Count() + world.Knowledge.BeliefsOf(Reeve).Count());
        }

        /// <summary>
        /// Being in the matter yourself does not place anybody else in it.
        ///
        /// The regression this guards is an accounting one and it hides a real leak. The miller is
        /// named in his own suspicion, so he is placed for free; a check that compared the placed
        /// list's length against the number of people named would see one and one, call the matter
        /// fully placed, and stop reporting that the person he suspects is exactly the thing he
        /// cannot demonstrate. Both sides are therefore counted excluding him.
        /// </summary>
        [Fact]
        public void PlacingYourselfDoesNotPlaceWhoeverElseTheMatterNames()
        {
            NarrativeWorldState world = Town_();

            ActorLocalPressure suspicion = Assert.Single(
                ActorPressureView.Of(world, Miller, new Development[0]),
                p => p.FocusFactId == SuspicionFactId(world));

            Assert.Contains(Miller, suspicion.SubjectIds);
            Assert.DoesNotContain(Stranger, suspicion.SubjectIds);
            Assert.Contains("implicated party", suspicion.Unknown);
            Assert.True(suspicion.HasStake(ActorStakeKind.Personal));
        }

        // -- the refusals ------------------------------------------------------------------------

        /// <summary>
        /// Being party to the record is a route; being named in somebody else's pressure is not.
        ///
        /// Three records, three people who are answerable for what those records say: the shopkeeper
        /// whose shop failed, the neighbour who owes a day's work, and a member of the guild that
        /// wants to grow. None of them was taught anything, and none of them needed to be - you know
        /// your own shop shut, your own debt and your own guild's plans.
        /// </summary>
        [Fact]
        public void PartyToTheRecordReachesItsOwnPressureWithoutBeingTaught()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            IReadOnlyList<ActorLocalPressure> shopkeeper = ActorPressureView.Of(world, Shopkeeper, pressures);
            Assert.Contains(shopkeeper, p => p.HasPressure(DevelopmentPressures.ServiceInterruption)
                                             && p.HasStake(ActorStakeKind.Property));

            IReadOnlyList<ActorLocalPressure> witness = ActorPressureView.Of(world, Witness, pressures);
            Assert.Contains(witness, p => p.HasPressure(DevelopmentPressures.UnmetObligation)
                                          && p.HasStake(ActorStakeKind.Obligation));

            IReadOnlyList<ActorLocalPressure> member = ActorPressureView.Of(world, Miller, pressures);
            Assert.Contains(member, p => p.HasPressure(DevelopmentPressures.OrganizationStake)
                                         && p.HasStake(ActorStakeKind.Organization));

            // And the same guild stake is nothing at all to somebody who is not in the guild.
            Assert.Empty(ActorPressureView.Of(world, Stranger, pressures)
                .Where(p => p.HasPressure(DevelopmentPressures.OrganizationStake)));
        }

        /// <summary>
        /// What is plainly there where you live is a route; what is hidden there is not.
        ///
        /// The town is short of grain and the miller lives in it, so he is under that pressure
        /// without anybody telling him. A secret claim recorded against the same place stays
        /// invisible from the street - which is why this route is gated on the claim's secrecy
        /// rather than on the place alone, and why crime and unproven belief are excluded from it
        /// outright rather than trusted to be secret.
        /// </summary>
        [Fact]
        public void AnOpenConditionWhereTheyLiveIsARouteAndASecretOneIsNot()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            Assert.Contains(
                ActorPressureView.Of(world, Miller, pressures),
                p => p.HasPressure(DevelopmentPressures.Shortage) && p.HasStake(ActorStakeKind.Local));

            Development hiddenDamage = pressures.Single(
                d => d.HasPressure(DevelopmentPressures.DamagedProperty) && d.SiteIds.Contains(Mill));
            NarrativeNpc miller = world.Registry.GetNpc(Miller);
            miller.HomeSiteId = Mill;

            Assert.Empty(Matching(world, Miller, pressures, hiddenDamage));

            // Make the same damage public and the same person is standing in front of it.
            world.Knowledge.GetFact(hiddenDamage.FocusFactId).Secrecy = 0;
            Assert.Single(Matching(world, Miller, DevelopmentDetector.Detect(world), hiddenDamage));
        }

        /// <summary>
        /// Two people hold two versions of one killing, and each is only in the dispute they are
        /// actually holding.
        ///
        /// The shopkeeper believes a garbled version and nothing else; from where she stands there
        /// is no contradiction, because a contradiction needs two claims and she has one. The
        /// witness holds both, so the dispute is his. The world's own evidence conflict says only
        /// that the contradiction exists - which version anybody is wrong about is this reading's,
        /// and it never arrives by being told.
        /// </summary>
        [Fact]
        public void ADisputeBelongsToWhoeverIsHoldingBothHalvesOfIt()
        {
            NarrativeWorldState world = Town_();
            EntityId rumour = RumourFactId(world);
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            ActorLocalPressure hers = Assert.Single(
                ActorPressureView.Of(world, Shopkeeper, pressures), p => p.FocusFactId == rumour);
            Assert.False(hers.Disputed);
            Assert.True(hers.SincerelyMistaken);

            world.Knowledge.Teach(Shopkeeper, KillingFactId(world), KnowledgeSource.Hearsay, 0.6, GameTime.Zero, false);
            Assert.Contains(ActorPressureView.Of(world, Shopkeeper, DevelopmentDetector.Detect(world)),
                p => p.Disputed && p.HasPressure(DevelopmentPressures.EvidenceConflict));
        }

        /// <summary>
        /// Somebody who has heard that the wrong was put right stops being under pressure about it;
        /// somebody who has not, does not.
        ///
        /// Settlement is read off their beliefs rather than off the matter, on purpose. The world
        /// knowing a debt was paid is not the same as the neighbours knowing, and a reading that
        /// quietly consulted the thread would be correcting an actor with a fact nobody gave them.
        /// </summary>
        [Fact]
        public void SettlementIsReadOffWhatTheyHeardRatherThanOffTheMatter()
        {
            NarrativeWorldState world = Town_();
            EntityId suspicion = SuspicionFactId(world);
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            Assert.Contains(ActorPressureView.Of(world, Miller, pressures), p => p.FocusFactId == suspicion);

            Fact settled = new Fact(
                world.NewId("fact"), Stranger, FactPredicates.Settled, Stranger, "made good", TruthState.True);
            world.Knowledge.AddFact(settled);
            world.Knowledge.Teach(Miller, settled.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, false);

            IReadOnlyList<ActorLocalPressure> after = ActorPressureView.Of(world, Miller, DevelopmentDetector.Detect(world));
            Assert.DoesNotContain(after, p => p.FocusFactId == suspicion);

            // The witness never heard, and is not corrected by the world on the strength of it.
            world.Knowledge.Teach(Witness, suspicion, KnowledgeSource.Hearsay, 0.7, GameTime.Zero, false);
            Assert.Contains(
                ActorPressureView.Of(world, Witness, DevelopmentDetector.Detect(world)),
                p => p.FocusFactId == suspicion);
        }

        /// <summary>
        /// What somebody believes does not depend on what the detector was asked to look at.
        ///
        /// A bounded pass is a statement about work, not about people: handing this an empty reading
        /// means "the world's stores report nothing changed", and the miller is no less convinced he
        /// was robbed. The objective half goes quiet and the belief half does not.
        /// </summary>
        [Fact]
        public void AnEmptyObjectiveReadingStillFindsWhatTheyBelieve()
        {
            NarrativeWorldState world = Town_();

            IReadOnlyList<ActorLocalPressure> local = ActorPressureView.Of(world, Miller, new Development[0]);

            Assert.Contains(local, p => p.FocusFactId == SuspicionFactId(world));
            Assert.All(local, p => Assert.False(p.HasObjectiveCause));
        }

        /// <summary>
        /// An unknown actor and a null world are questions with no answer, not an empty world.
        /// </summary>
        [Fact]
        public void AnUnknownActorReadsNothing()
        {
            NarrativeWorldState world = Town_();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(world);

            Assert.Empty(ActorPressureView.Of(world, EntityId.Parse("npc_nobody"), pressures));
            Assert.Empty(ActorPressureView.Of(null, Witness, pressures));
        }

        // -- fixture -----------------------------------------------------------------------------

        private static Development Crime(IReadOnlyList<Development> pressures)
        {
            return pressures.Single(d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
        }

        private static IEnumerable<ActorLocalPressure> Matching(
            NarrativeWorldState world,
            EntityId actor,
            IReadOnlyList<Development> pressures,
            Development development)
        {
            return ActorPressureView.Of(world, actor, pressures).Where(p => p.DevelopmentId == development.Id);
        }

        private static ActorLocalPressure Only(
            NarrativeWorldState world,
            EntityId actor,
            Development development)
        {
            return Assert.Single(ActorPressureView.Of(world, actor, new[] { development }));
        }

        private static string Render(IReadOnlyList<ActorLocalPressure> readings)
        {
            return string.Join(
                "\n",
                readings.Select(p => p.Id
                                     + "|" + p.DevelopmentId
                                     + "|" + string.Join(",", p.PressureTags)
                                     + "|" + p.Certainty
                                     + "|" + p.Urgency
                                     + "|" + p.Disputed
                                     + "|" + p.SincerelyMistaken
                                     + "|" + string.Join(",", p.SubjectIds.Select(id => id.Value))
                                     + "|" + string.Join(",", p.Unknown)
                                     + "|" + string.Join(",", p.Stakes.Select(s => s.ToString()))
                                     + "|" + string.Join(",", p.Terms)));
        }

        private static EntityId KillingFactId(NarrativeWorldState world) => Find(world, FactPredicates.Killed, TruthState.True);

        private static EntityId RumourFactId(NarrativeWorldState world) => Find(world, FactPredicates.Killed, TruthState.False);

        private static EntityId SuspicionFactId(NarrativeWorldState world) => Find(world, FactPredicates.Stole, TruthState.False);

        private static EntityId Find(NarrativeWorldState world, string predicate, TruthState truth)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Predicate == predicate && fact.Truth == truth)
                {
                    return fact.Id;
                }
            }

            return EntityId.None;
        }

        /// <summary>
        /// One town, holding at the same time: a killing two people hold different versions of, a
        /// theft that never happened, a broken mill nobody has mentioned, a shortage, a failed shop,
        /// an open debt and a guild with plans. Every piece lives in the store that owns it, and
        /// nothing here is an actor-local reading.
        /// </summary>
        private static NarrativeWorldState Town_()
        {
            NarrativeWorldState world = new NarrativeWorldState(19);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeSite(Mill, "the mill", "workshop"));
            world.Registry.Add(new NarrativeNpc(Killer, "Garron"));
            world.Registry.Add(new NarrativeNpc(Victim, "Tovar"));
            world.Registry.Add(new NarrativeNpc(Witness, "Elsi") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Reeve, "Hald") { Occupation = "reeve", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Kin, "Sera") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Stranger, "Orvo"));
            world.Registry.Add(new NarrativeNpc(Miller, "Bran") { Occupation = "miller", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira") { Occupation = "shopkeeper", HomeSiteId = Town });

            // The victim's kin care more than anybody and are told nothing.
            world.Relationships.ConnectMutual(Kin, Victim, RelationKind.Family, 90);
            world.Registry.GetNpc(Kin).Sensitivities.Violence = 1.0;
            world.Registry.GetNpc(Kin).Values.Law.Importance = 1.0;

            // Crime: true, secret, unanswered. The witness suspects; the reeve can demonstrate it.
            Fact killing = new Fact(
                world.NewId("fact"), Killer, FactPredicates.Killed, Victim, truth: TruthState.True, secrecy: 60);
            world.Knowledge.AddFact(killing);
            world.Knowledge.Teach(Witness, killing.Id, KnowledgeSource.Inference, 0.4, GameTime.Zero, canProve: false);
            world.Knowledge.Teach(Reeve, killing.Id, KnowledgeSource.Witnessed, 0.9, GameTime.Zero, canProve: true);

            // A garbled version of the same matter, sincerely held by somebody else.
            Fact rumour = new Fact(
                world.NewId("fact"), Miller, FactPredicates.Killed, Victim, truth: TruthState.False)
            {
                DistortionOf = killing.Id
            };
            world.Knowledge.AddFact(rumour);
            world.Knowledge.Teach(Shopkeeper, rumour.Id, KnowledgeSource.Hearsay, 0.7, GameTime.Zero, canProve: false);

            // A theft that never happened, believed by the man it did not happen to.
            Fact suspicion = new Fact(
                world.NewId("fact"), Stranger, FactPredicates.Stole, Miller, "a sack of flour", TruthState.False);
            world.Knowledge.AddFact(suspicion);
            world.Knowledge.Teach(Miller, suspicion.Id, KnowledgeSource.Inference, 0.65, GameTime.Zero, canProve: false);

            // Property: the mill wheel is broken, and nobody has said so out loud.
            world.Knowledge.AddFact(new Fact(
                world.NewId("fact"), Mill, FactPredicates.Damaged, EntityId.None, "mill_wheel", secrecy: 40));

            // Economy: a town-level shortage anyone living there can see.
            world.Demands.AddOrUpdate(Town, LocalDemandCategory.Food, 70, GameTime.Zero, GameTime.FromDays(30), EntityId.None);

            // Service continuity: a shop that failed, recorded as durable meaning.
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Town, Shopkeeper, GameTime.Zero);
            businesses.TryChangeState(Shop, BusinessContinuityState.Failed, GameTime.FromDays(2));

            // Social obligation: an open debt the witness owes the miller.
            world.Obligations.Add(new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Favor,
                Witness,
                Miller,
                EntityId.None,
                "owes a day's work",
                GameTime.Zero,
                EntityId.None));

            // Organization stake: a guild the miller belongs to, that wants to grow.
            Organization guild = new Organization(Guild, "the carters", "guild") { LeaderId = Shopkeeper };
            guild.SiteIds.Add(Town);
            guild.MemberIds.Add(Miller);
            guild.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 40));
            world.Registry.Add(guild);
            world.Registry.GetNpc(Miller).OrganizationIds.Add(Guild);

            return world;
        }
    }
}
