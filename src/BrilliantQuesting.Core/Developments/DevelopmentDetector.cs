using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// Step 7 of the expression pipeline (CD §37): read the world as it stands and say what is
    /// still pressing.
    ///
    /// This is a pure function of authoritative state. It appends no event, writes no fact, opens
    /// no thread and touches nothing it reads - running it twice returns the same developments,
    /// and running it never would leave the world identical. That is what keeps a derived reading
    /// from turning into a second authority; it is also why nothing needs to be saved.
    ///
    /// Developments are keyed by the <em>pressure</em>, so many events can feed one development
    /// and most events feed none - which is what stops this from becoming a wrapper around the
    /// ledger - and they do not correspond one-to-one with threads, since one thread can hold
    /// several pressures, a settled thread can still hold one, and a pressure can exist with no
    /// thread at all - which is what stops it from becoming a second thread system.
    ///
    /// <b>Two contracts hold the broadened rule set together (BQa-006).</b>
    ///
    /// <b>Aggregation.</b> Rules do not each emit a development. They emit *readings*, and
    /// readings that name the same standing condition are merged onto one id - so a shortage
    /// recorded in the demand ledger and the need claim that ledger entry came from are one
    /// pressure with two sources, not two pressures that happen to rhyme. Distinct causes keep
    /// distinct ids: two categories short in one town, or two separate needs, stay separately
    /// answerable, because collapsing them would make the later question "which of these did the
    /// actor actually address?" unanswerable.
    ///
    /// <b>Bounded intake.</b> Detection enumerates a <see cref="DevelopmentScope"/>, so a live
    /// consumer reads the handful of facts, obligations, places, businesses and organizations that
    /// changed rather than every store in the save. The unbounded reading stays available under
    /// its own name for the inspector, the Lab and fixture worlds.
    ///
    /// The two contracts meet at one edge worth stating plainly. A work set answers for the
    /// entities in it: a condition whose sources sit in two stores is read completely only when
    /// the work set names both, and naming one yields that store's contribution under the same
    /// identity rather than a different pressure. That is the correct behaviour for "tell me about
    /// what changed" and the reason identity is keyed on the condition rather than on the rule -
    /// a later pass naming the other source lands on the same development.
    ///
    /// Every rule states three things, because a pressure family that cannot say them is a guess:
    /// its <b>source</b> (the authoritative store it reads), its <b>default</b> (what it derives
    /// when that store says nothing), and its <b>refusal</b> (what it will not infer). The
    /// refusals matter most. Nothing here reads live vanilla stock, a shopkeeper's nap, or an
    /// actor's beliefs about whether any of this is so: unsupported native facts stay unknown, and
    /// unknown is not a pressure.
    /// </summary>
    public static class DevelopmentDetector
    {
        private static readonly IReadOnlyList<Development> Nothing = new Development[0];

        private static readonly IReadOnlyList<EntityId> NoIds = new EntityId[0];

        /// <summary>
        /// Every unresolved pressure the world currently holds, in stable id order.
        ///
        /// The unbounded reading: correct, deterministic, and O(world). Deterministic across a
        /// reload, because everything is read from persisted state and every list is ordered by id
        /// rather than by the enumeration order of a dictionary.
        /// </summary>
        public static IReadOnlyList<Development> Detect(NarrativeWorldState world)
        {
            return Detect(world, DevelopmentScope.EntireWorld);
        }

        /// <summary>
        /// Every unresolved pressure derivable from the given work set, in stable id order.
        ///
        /// A bounded scope changes *what was looked at*, never what a pressure means: every rule
        /// that applies to an entity in the work set runs on it exactly as it would in a
        /// whole-world pass, with the same identity, tags and urgency. What a bounded pass cannot
        /// do is answer for entities nobody named - so a condition with sources in two stores is
        /// complete when the work set names both, which is what an invalidation seam supplies.
        /// </summary>
        public static IReadOnlyList<Development> Detect(NarrativeWorldState world, DevelopmentScope scope)
        {
            if (world == null)
            {
                return Nothing;
            }

            Aggregation aggregation = new Aggregation(world);
            DevelopmentScope work = scope ?? DevelopmentScope.EntireWorld;

            IReadOnlyList<EntityId> factIds = work.IsEntireWorld ? SortedFactIds(world) : work.FactIds;
            for (int i = 0; i < factIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(factIds[i]);
                if (fact == null)
                {
                    continue;
                }

                ReadUnprovenKnowledge(world, fact, aggregation);
                ReadUnresolvedCrime(world, fact, aggregation);
                ReadDamagedProperty(world, fact, aggregation);
                ReadEvidenceConflict(world, fact, aggregation);
                ReadNeed(world, fact, aggregation);
            }

            IReadOnlyList<EntityId> obligationIds = work.IsEntireWorld ? SortedObligationIds(world) : work.ObligationIds;
            for (int i = 0; i < obligationIds.Count; i++)
            {
                ReadUnmetObligation(world, world.Obligations.Find(obligationIds[i]), aggregation);
            }

            IReadOnlyList<EntityId> siteIds = work.IsEntireWorld ? SortedDemandPlaceIds(world) : work.SiteIds;
            for (int i = 0; i < siteIds.Count; i++)
            {
                ReadShortage(world, siteIds[i], aggregation);
            }

            IReadOnlyList<EntityId> businessIds = work.IsEntireWorld ? SortedBusinessIds(world) : work.BusinessIds;
            for (int i = 0; i < businessIds.Count; i++)
            {
                ReadServiceContinuity(world, world.Businesses.Of(businessIds[i]), aggregation);
            }

            IReadOnlyList<EntityId> organizationIds = work.IsEntireWorld ? SortedOrganizationIds(world) : work.OrganizationIds;
            for (int i = 0; i < organizationIds.Count; i++)
            {
                ReadOrganizationStake(world.Registry.GetOrganization(organizationIds[i]), aggregation);
            }

            return aggregation.Build();
        }

        // -- knowledge and belief ------------------------------------------------------------

        /// <summary>
        /// Somebody believes something true about somebody else and cannot demonstrate it.
        ///
        /// Source: the knowledge graph's facts and belief records. Default: nothing - a public
        /// fact is no pressure, and neither is a fact only its own subject believes, because a
        /// thief knowing what they did is not a matter waiting on anybody. Refusal: an untrue or
        /// uncertain claim produces nothing here; a sincere believer of a false claim is an
        /// actor-local reading, not an objective one.
        ///
        /// One development per fact rather than per believer: two people who both saw the same
        /// theft are one pressure with two names on it, not two pressures.
        /// </summary>
        private static void ReadUnprovenKnowledge(NarrativeWorldState world, Fact fact, Aggregation into)
        {
            if (fact.Truth != TruthState.True || fact.Secrecy <= 0)
            {
                return;
            }

            List<EntityId> believers = new List<EntityId>();
            foreach (EntityId knower in world.Knowledge.Knowers(fact.Id))
            {
                if (knower != fact.Subject && !world.Knowledge.CanProve(knower, fact.Id))
                {
                    believers.Add(knower);
                }
            }

            if (believers.Count == 0)
            {
                return;
            }

            believers.Sort();

            Reading reading = into.For("dev.unproven_knowledge:" + fact.Id.Value);
            reading.Tag(DevelopmentPressures.UnprovenKnowledge);
            if (!fact.Subject.IsNone && world.Knowledge.Knows(fact.Subject, fact.Id))
            {
                // The person the belief is about knows it is believed: they have something to lose
                // and know it, which is a different matter from one only the accuser is aware of.
                reading.Tag(DevelopmentPressures.Contested);
            }

            reading.Subjects(believers);
            reading.Subject(fact.Subject);
            reading.Origin(fact.OriginEvent);
            reading.Focus(fact.Id);
            reading.CarrierAndPlaces(into.ThreadCarrying(fact.Id));

            // A secret nobody can prove presses harder the more mouths it is in: each believer
            // past the first is one more person who could say it out loud.
            reading.Press(fact.Secrecy + (10 * (believers.Count - 1)));
        }

        /// <summary>
        /// Two claims about one matter stand in the graph, one of them false, and somebody holds
        /// the false one.
        ///
        /// Source: <see cref="Fact.DistortionOf"/>, which already records that a garbled or
        /// invented version is about the same thing as the true claim beside it. Default: nothing
        /// - a false claim nobody believes is an unread entry, and a distortion whose original has
        /// gone is not a conflict the world can still be wrong about. Refusal: this says the
        /// contradiction exists, never who is right for whom. Correcting a believer belongs to the
        /// knowledge and inference owners, and which actor is staked in the error is BQa-007's
        /// reading.
        /// </summary>
        private static void ReadEvidenceConflict(NarrativeWorldState world, Fact fact, Aggregation into)
        {
            if (!fact.IsUntrue || fact.DistortionOf.IsNone)
            {
                return;
            }

            Fact original = world.Knowledge.GetFact(fact.DistortionOf);
            if (original == null || original.Truth != TruthState.True)
            {
                return;
            }

            List<EntityId> believers = new List<EntityId>();
            foreach (EntityId knower in world.Knowledge.Knowers(fact.Id))
            {
                believers.Add(knower);
            }

            if (believers.Count == 0)
            {
                return;
            }

            believers.Sort();

            Reading reading = into.For("dev.evidence_conflict:" + fact.Id.Value);
            reading.Tag(DevelopmentPressures.EvidenceConflict);
            reading.Subjects(believers);
            reading.Subject(fact.Subject);
            reading.Subject(original.Subject);
            reading.Origin(fact.OriginEvent);
            reading.Origin(original.OriginEvent);

            // The focus is the true claim, not the distortion: a scene built around this is about
            // the matter being got wrong, and the matter is the thing that actually happened.
            reading.Focus(original.Id);
            reading.CarrierAndPlaces(into.ThreadCarrying(original.Id));
            reading.Press(50 + (10 * (believers.Count - 1)));
        }

        // -- property and crime --------------------------------------------------------------

        /// <summary>How hard a kind of wrong presses while it stands unanswered. A property of the
        /// vocabulary, like whether a predicate is news: it is the wrong that is grave, not the
        /// particular telling of it.</summary>
        private static int CrimeWeight(string predicate)
        {
            switch (predicate)
            {
                case FactPredicates.Killed: return 90;
                case FactPredicates.Extorted: return 70;
                case FactPredicates.Stole: return 60;
                case FactPredicates.Forged: return 50;
                default: return 0;
            }
        }

        /// <summary>
        /// A wrong the world holds as true, with nothing recording it as ended.
        ///
        /// Source: the crime predicates in the fact vocabulary, and the <see cref="FactPredicates.Settled"/>
        /// claims that say a matter was put right. Default: an unsettled true crime presses at its
        /// kind's weight. Refusal: a claim that is false, uncertain or superseded produces nothing
        /// - restitution supersedes the claim and the pressure stops being derived with it - and
        /// no native crime, guard or bounty state is consulted, because none is evidenced.
        ///
        /// Separate from <see cref="ReadUnprovenKnowledge"/> on purpose, and the two come apart in
        /// both directions: a theft the whole town watched is unresolved with nothing to prove,
        /// and a theft already made good on can still be a secret somebody cannot demonstrate.
        /// </summary>
        private static void ReadUnresolvedCrime(NarrativeWorldState world, Fact fact, Aggregation into)
        {
            int weight = CrimeWeight(fact.Predicate);
            if (weight == 0 || fact.Truth != TruthState.True)
            {
                return;
            }

            NarrativeThread matter = into.ThreadCarrying(fact.Id);
            if (IsSettled(world, matter, fact))
            {
                return;
            }

            Reading reading = into.For("dev.unresolved_crime:" + fact.Id.Value);
            reading.Tag(DevelopmentPressures.UnresolvedCrime);
            reading.Person(world, fact.Subject);

            // Only where the wrong was done to somebody. A killing names its victim; a theft names
            // the ring, and a ring is not implicated in anything.
            reading.Person(world, fact.Object);
            reading.Origin(fact.OriginEvent);
            reading.Focus(fact.Id);
            reading.CarrierAndPlaces(matter);
            reading.Press(weight);
        }

        /// <summary>
        /// Whether the matter carrying this wrong records it as put right.
        ///
        /// A <see cref="FactPredicates.Settled"/> claim is made when a matter ends, and both
        /// producers file it on the matter that ended; it names whoever or whatever the matter was
        /// about, which for a wrong is the person who did it. So settlement is read off the thread
        /// rather than by hunting the knowledge graph for a claim that might name this crime -
        /// which would be a walk of every fact in the save to answer a question about one of them.
        ///
        /// A wrong no matter was ever opened about therefore has nothing that could have ended it,
        /// and keeps pressing. That is the honest answer rather than a gap: an ending is a matter's
        /// ending, and an untrue claim of one does not close anything here, because this reading is
        /// of the world rather than of what anybody says about it.
        /// </summary>
        private static bool IsSettled(NarrativeWorldState world, NarrativeThread matter, Fact crime)
        {
            if (matter == null)
            {
                return false;
            }

            for (int i = 0; i < matter.FactIds.Count; i++)
            {
                Fact claim = world.Knowledge.GetFact(matter.FactIds[i]);
                if (claim != null
                    && string.Equals(claim.Predicate, FactPredicates.Settled, StringComparison.Ordinal)
                    && claim.Truth == TruthState.True
                    && claim.Object == crime.Subject)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Something is broken, blighted or spoiled and has not been put right.
        ///
        /// Source: true <see cref="FactPredicates.Damaged"/> claims. Default: damage presses
        /// moderately on whoever depends on the thing. Refusal: most damage is nobody's crime, so
        /// this never implies one; a repair supersedes the claim and the reading disappears with
        /// it; and the local readings damage invites - contamination, sabotage, soil trouble - are
        /// separate claims made by people, not something to be assumed here.
        /// </summary>
        private static void ReadDamagedProperty(NarrativeWorldState world, Fact fact, Aggregation into)
        {
            if (!string.Equals(fact.Predicate, FactPredicates.Damaged, StringComparison.Ordinal)
                || fact.Truth != TruthState.True)
            {
                return;
            }

            Reading reading = into.For("dev.damaged_property:" + fact.Id.Value);
            reading.Tag(DevelopmentPressures.DamagedProperty);

            // Nobody is named unless the claim names them. Who depends on a broken mill wheel is a
            // question about ownership and work, and answering it here would be this layer
            // deciding who cares - which is the one thing it must leave to the actors.
            reading.Person(world, fact.Object);
            reading.Place(world, fact.Subject);
            reading.Origin(fact.OriginEvent);
            reading.Focus(fact.Id);
            reading.CarrierAndPlaces(into.ThreadCarrying(fact.Id));
            reading.Press(40);
        }

        // -- economy and service continuity --------------------------------------------------

        /// <summary>
        /// Somebody wants goods they cannot get.
        ///
        /// Source: true <see cref="FactPredicates.Needs"/> claims, whose value states the demand
        /// as a property constraint rather than a named item. Default: a need with no ledger entry
        /// behind it still presses, moderately - a source change that never reached a thread or a
        /// town-level record is exactly the kind of pressure this layer exists to keep visible.
        /// Refusal: no inventory, shop stock or vanilla economy is read; the category is whatever
        /// BQ-050's coarse buckets can honestly make of the stated demand, and nothing is invented
        /// when they can make nothing of it.
        ///
        /// Keyed by fact so that a ledger entry citing this need merges onto it rather than
        /// standing beside it as a second shortage.
        /// </summary>
        private static void ReadNeed(NarrativeWorldState world, Fact fact, Aggregation into)
        {
            if (!string.Equals(fact.Predicate, FactPredicates.Needs, StringComparison.Ordinal)
                || fact.Truth != TruthState.True)
            {
                return;
            }

            Reading reading = into.For("dev.shortage:" + fact.Id.Value);
            reading.Tag(DevelopmentPressures.Shortage);
            reading.Person(world, fact.Subject);
            reading.Origin(fact.OriginEvent);
            reading.Focus(fact.Id);
            reading.CarrierAndPlaces(into.ThreadCarrying(fact.Id));
            reading.Press(40);
        }

        /// <summary>
        /// A place is short of something, in the coarse categories BQ-050 tracks.
        ///
        /// Source: the local demand ledger, read by place. Default: the record's own severity, the
        /// authoritative number that already exists; a relieved pressure is not active and derives
        /// nothing, which is how delivering goods makes the reading go away rather than completing
        /// a counter. Refusal: routine consumption is not modelled and is never inferred - a town
        /// that eats breakfast is not a town under pressure.
        ///
        /// Shares an id with <see cref="ReadNeed"/> when the ledger entry names the need it came
        /// from: one standing condition, two authoritative sources, one development carrying both.
        /// </summary>
        private static void ReadShortage(NarrativeWorldState world, EntityId placeId, Aggregation into)
        {
            IReadOnlyList<LocalDemandPressure> pressures = world.Demands.At(placeId);
            for (int i = 0; i < pressures.Count; i++)
            {
                LocalDemandPressure pressure = pressures[i];
                if (!pressure.Active)
                {
                    continue;
                }

                Reading reading = into.For("dev.shortage:" + (pressure.SourceFactId.IsNone
                    ? pressure.PlaceId.Value + ":" + pressure.Category
                    : pressure.SourceFactId.Value));
                reading.Tag(DevelopmentPressures.Shortage);
                reading.Site(pressure.PlaceId);
                reading.Focus(pressure.SourceFactId);
                reading.CarrierAndPlaces(into.ThreadCarrying(pressure.SourceFactId));
                reading.Press(pressure.Severity);
            }
        }

        /// <summary>
        /// A tracked business is not serving as it should, or is coming back.
        ///
        /// Source: BQ-051's business ledger - the durable continuity meaning only. Default: a
        /// business in Normal state derives nothing. Refusal: the live service surface is not read
        /// here at all. An operator asleep, at a hobby or off-shift is the working day, and native
        /// stock is not evidenced from Core, so neither can mint a pressure; a shortage of stock
        /// presses only where the ledger records that continuity state as meaning.
        ///
        /// The states with an answer already in place read as <see cref="DevelopmentPressures.Recovering"/>
        /// rather than as interruption. They are still reasons to act - a replacement operator
        /// needs custom, an inherited counter needs standing - and a detector that only ever
        /// reported failure would be telling the truth every time while describing a world nobody
        /// recognises.
        /// </summary>
        private static void ReadServiceContinuity(NarrativeWorldState world, BusinessRecord record, Aggregation into)
        {
            if (record == null)
            {
                return;
            }

            string tag;
            int press;
            switch (record.State)
            {
                case BusinessContinuityState.Failed:
                    tag = DevelopmentPressures.ServiceInterruption;
                    press = 80;
                    break;
                case BusinessContinuityState.TemporarilyClosed:
                case BusinessContinuityState.OwnerAbsent:
                case BusinessContinuityState.Extorted:
                    tag = DevelopmentPressures.ServiceInterruption;
                    press = 60;
                    break;
                case BusinessContinuityState.Struggling:
                case BusinessContinuityState.ShortOnStock:
                    tag = DevelopmentPressures.ServiceInterruption;
                    press = 40;
                    break;
                case BusinessContinuityState.Recovered:
                case BusinessContinuityState.ReplacementOperator:
                case BusinessContinuityState.Inherited:
                case BusinessContinuityState.BoughtOut:
                    tag = DevelopmentPressures.Recovering;
                    press = 20;
                    break;
                default:
                    return;
            }

            Reading reading = into.For("dev.business_continuity:" + record.BusinessId.Value);
            reading.Tag(tag);
            reading.Subject(record.OperatorId);
            reading.Subject(record.ReplacementOperatorId);
            reading.Subject(record.InheritedById);
            reading.Site(record.PlaceId);
            reading.Focus(record.CauseFactId);
            reading.CarrierAndPlaces(into.ThreadCarrying(record.CauseFactId));
            reading.Press(press);
        }

        // -- organizations -------------------------------------------------------------------

        /// <summary>
        /// An organization is carrying a goal of its own that nothing has satisfied.
        ///
        /// Source: the organization records BQ owns and their goals. Default: the goal's own
        /// weight, which is the authoritative number for how much the organization cares; a
        /// satisfied or weightless goal derives nothing. Refusal: vanilla guild rank, membership
        /// and economy stay read-only inputs to their own systems and are not turned into stakes
        /// here, and nothing is minted for an organization with no goal recorded.
        ///
        /// Growth and reserves read as <see cref="DevelopmentPressures.Opportunity"/> and a raid
        /// as <see cref="DevelopmentPressures.Adversarial"/>, because "the guild wants to expand"
        /// and "the guild wants to burn a rival out" are not the same reason to act and a consumer
        /// that could not tell them apart would stage the wrong scene for both.
        /// </summary>
        private static void ReadOrganizationStake(Organization organization, Aggregation into)
        {
            if (organization == null)
            {
                return;
            }

            for (int i = 0; i < organization.Goals.Count; i++)
            {
                OrganizationGoal goal = organization.Goals[i];
                if (goal == null || goal.Satisfied || goal.Weight <= 0)
                {
                    continue;
                }

                Reading reading = into.For(
                    "dev.organization_stake:" + organization.Id.Value + ":" + goal.Kind + ":" + goal.Subject.Value);
                reading.Tag(DevelopmentPressures.OrganizationStake);
                if (string.Equals(goal.Kind, OrganizationActivity.RaidOrganization, StringComparison.Ordinal))
                {
                    reading.Tag(DevelopmentPressures.Adversarial);
                }
                else if (string.Equals(goal.Kind, OrganizationActivity.ExpandMembership, StringComparison.Ordinal)
                         || string.Equals(goal.Kind, OrganizationActivity.BuildReserves, StringComparison.Ordinal))
                {
                    reading.Tag(DevelopmentPressures.Opportunity);
                }

                reading.Subject(organization.Id);
                reading.Subject(organization.LeaderId);
                reading.Subject(goal.Subject);
                for (int s = 0; s < organization.SiteIds.Count; s++)
                {
                    reading.Site(organization.SiteIds[s]);
                }

                reading.Press(goal.Weight);
            }
        }

        // -- obligations ---------------------------------------------------------------------

        /// <summary>
        /// A social debt that is still open.
        ///
        /// Source: the obligation ledger, plus the source event it records for where and what
        /// matter the debt belongs to. Default: strength, scaled; a fulfilled, forgiven or broken
        /// debt is not open and derives nothing. Refusal: pressure between two people rather than
        /// about a claim, so it has no focus fact - and therefore nothing for a storylet to build
        /// roles around, however much of a matter it is.
        /// </summary>
        private static void ReadUnmetObligation(NarrativeWorldState world, SocialObligation obligation, Aggregation into)
        {
            if (obligation == null || !obligation.IsOpen)
            {
                return;
            }

            Reading reading = into.For("dev.unmet_obligation:" + obligation.Id.Value);
            reading.Tag(DevelopmentPressures.UnmetObligation);
            if (obligation.Kind == SocialObligationKind.Grudge)
            {
                // A grudge runs against its subject rather than toward them.
                reading.Tag(DevelopmentPressures.Adversarial);
            }

            reading.Subject(obligation.Debtor);
            reading.Subject(obligation.Creditor);
            reading.Origin(obligation.SourceEventId);

            // The obligation itself records only its source event, so where and what matter it
            // belongs to are read back off that event rather than stored twice.
            WorldEvent source = world.Ledger.Find(obligation.SourceEventId);
            if (source != null)
            {
                reading.Carrier(world.GetThread(source.ThreadId));
                reading.Site(source.Zone);
            }

            reading.Press(obligation.Strength * 20);
        }

        // -- work sets -----------------------------------------------------------------------

        private static IReadOnlyList<EntityId> SortedFactIds(NarrativeWorldState world)
        {
            List<EntityId> ids = new List<EntityId>(world.Knowledge.Facts.Keys);
            ids.Sort();
            return ids;
        }

        private static IReadOnlyList<EntityId> SortedObligationIds(NarrativeWorldState world)
        {
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            if (records.Count == 0)
            {
                return NoIds;
            }

            List<EntityId> ids = new List<EntityId>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                ids.Add(records[i].Id);
            }

            ids.Sort();
            return ids;
        }

        private static IReadOnlyList<EntityId> SortedDemandPlaceIds(NarrativeWorldState world)
        {
            IReadOnlyList<LocalDemandPressure> pressures = world.Demands.Pressures;
            if (pressures.Count == 0)
            {
                return NoIds;
            }

            List<EntityId> ids = new List<EntityId>();
            for (int i = 0; i < pressures.Count; i++)
            {
                if (!ids.Contains(pressures[i].PlaceId))
                {
                    ids.Add(pressures[i].PlaceId);
                }
            }

            ids.Sort();
            return ids;
        }

        private static IReadOnlyList<EntityId> SortedBusinessIds(NarrativeWorldState world)
        {
            if (world.Businesses.Count == 0)
            {
                return NoIds;
            }

            List<EntityId> ids = new List<EntityId>(world.Businesses.Count);
            foreach (BusinessRecord record in world.Businesses.Records)
            {
                ids.Add(record.BusinessId);
            }

            ids.Sort();
            return ids;
        }

        private static IReadOnlyList<EntityId> SortedOrganizationIds(NarrativeWorldState world)
        {
            if (world.Registry.Organizations.Count == 0)
            {
                return NoIds;
            }

            List<EntityId> ids = new List<EntityId>(world.Registry.Organizations.Keys);
            ids.Sort();
            return ids;
        }

        // -- aggregation ---------------------------------------------------------------------

        /// <summary>
        /// One pressure under construction. Rules contribute to it rather than returning it, so
        /// two rules reading the same standing condition write into one reading instead of racing
        /// to produce the better copy of it.
        /// </summary>
        private sealed class Reading
        {
            internal Reading(string id)
            {
                Id = id;
            }

            internal string Id { get; }

            internal List<string> Tags { get; } = new List<string>();

            internal List<EntityId> OriginEventIds { get; } = new List<EntityId>();

            internal List<EntityId> SubjectIds { get; } = new List<EntityId>();

            internal List<EntityId> SiteIds { get; } = new List<EntityId>();

            internal EntityId ThreadId { get; private set; }

            internal EntityId FocusFactId { get; private set; }

            /// <summary>Highest of what the contributing rules read. A condition two rules can see
            /// presses as hard as the harder reading of it, never as the sum: urgency describes the
            /// condition, and being noticed twice does not make a thing worse.</summary>
            internal int Urgency { get; private set; }

            internal void Tag(string tag)
            {
                if (!Tags.Contains(tag))
                {
                    Tags.Add(tag);
                }
            }

            internal void Subject(EntityId id) => Include(SubjectIds, id);

            internal void Subjects(List<EntityId> ids)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    Include(SubjectIds, ids[i]);
                }
            }

            internal void Site(EntityId id) => Include(SiteIds, id);

            /// <summary>Name an id only if the registry says it is an actor. Subjects are who is
            /// implicated, and a thing that was stolen or broken is what the pressure is about.</summary>
            internal void Person(NarrativeWorldState world, EntityId id)
            {
                if (world.Registry.IsActor(id))
                {
                    Include(SubjectIds, id);
                }
            }

            /// <summary>Name an id only if the registry says it is a place.</summary>
            internal void Place(NarrativeWorldState world, EntityId id)
            {
                if (!id.IsNone && world.Registry.GetSite(id) != null)
                {
                    Include(SiteIds, id);
                }
            }

            internal void Origin(EntityId id) => Include(OriginEventIds, id);

            /// <summary>First rule to name one wins; a later rule naming a different thread or
            /// focus does not get to overwrite it, because that would make the answer depend on
            /// which store happened to be enumerated first.</summary>
            internal void Focus(EntityId id)
            {
                if (FocusFactId.IsNone)
                {
                    FocusFactId = id;
                }
            }

            internal void Carrier(NarrativeThread thread)
            {
                if (thread != null && ThreadId.IsNone)
                {
                    ThreadId = thread.Id;
                }
            }

            /// <summary>The matter carrying this pressure, and the places that matter is set in.
            /// A debt reads its place off the occasion it was incurred instead, which is a
            /// narrower and more accurate answer than everywhere its matter has ever been.</summary>
            internal void CarrierAndPlaces(NarrativeThread thread)
            {
                Carrier(thread);
                if (thread == null)
                {
                    return;
                }

                for (int i = 0; i < thread.SiteIds.Count; i++)
                {
                    Include(SiteIds, thread.SiteIds[i]);
                }
            }

            internal void Press(int urgency)
            {
                if (urgency > Urgency)
                {
                    Urgency = urgency;
                }
            }

            private static void Include(List<EntityId> into, EntityId id)
            {
                if (!id.IsNone && !into.Contains(id))
                {
                    into.Add(id);
                }
            }
        }

        /// <summary>
        /// The readings of one pass, and the few per-pass lookups the rules share.
        ///
        /// The one index is built at most once and only when a rule actually asks, so a bounded
        /// pass that touches no facts never walks the threads. It is the reverse lookup nothing
        /// else keeps - which matter carries a fact - and it is bounded by live matters rather
        /// than by history; it is rebuilt per pass rather than cached on the world, because a
        /// cached index is a second authority that can go stale. Everything else a rule needs by
        /// id is asked of the store that owns it.
        /// </summary>
        private sealed class Aggregation
        {
            private readonly NarrativeWorldState _world;
            private readonly List<Reading> _order = new List<Reading>();
            private readonly Dictionary<string, Reading> _byId = new Dictionary<string, Reading>(StringComparer.Ordinal);
            private Dictionary<EntityId, NarrativeThread> _carriers;

            internal Aggregation(NarrativeWorldState world)
            {
                _world = world;
            }

            internal Reading For(string id)
            {
                Reading reading;
                if (!_byId.TryGetValue(id, out reading))
                {
                    reading = new Reading(id);
                    _byId[id] = reading;
                    _order.Add(reading);
                }

                return reading;
            }

            /// <summary>
            /// The thread that holds this fact, whatever state it is in. A settled thread does not
            /// unmake an unproven secret; whether the matter can still be <em>played</em> is a
            /// scene question, answered by <c>SceneStatus</c> when something tries.
            /// </summary>
            internal NarrativeThread ThreadCarrying(EntityId factId)
            {
                if (factId.IsNone)
                {
                    return null;
                }

                if (_carriers == null)
                {
                    _carriers = new Dictionary<EntityId, NarrativeThread>();
                    for (int i = 0; i < _world.Threads.Count; i++)
                    {
                        NarrativeThread thread = _world.Threads[i];
                        for (int f = 0; f < thread.FactIds.Count; f++)
                        {
                            if (!_carriers.ContainsKey(thread.FactIds[f]))
                            {
                                _carriers[thread.FactIds[f]] = thread;
                            }
                        }
                    }
                }

                NarrativeThread carrier;
                _carriers.TryGetValue(factId, out carrier);
                return carrier;
            }

            internal IReadOnlyList<Development> Build()
            {
                if (_order.Count == 0)
                {
                    return Nothing;
                }

                List<Development> developments = new List<Development>(_order.Count);
                for (int i = 0; i < _order.Count; i++)
                {
                    Reading reading = _order[i];
                    reading.SubjectIds.Sort();
                    developments.Add(new Development(
                        reading.Id,
                        reading.Tags,
                        reading.ThreadId,
                        reading.FocusFactId,
                        reading.OriginEventIds.Count == 0 ? null : reading.OriginEventIds,
                        reading.SubjectIds,
                        reading.SiteIds.Count == 0 ? null : reading.SiteIds,
                        reading.Urgency));
                }

                developments.Sort(ById);
                return developments;
            }
        }

        private static int ById(Development left, Development right)
        {
            return string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
