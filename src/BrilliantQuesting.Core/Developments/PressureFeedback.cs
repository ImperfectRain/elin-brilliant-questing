using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// One pass's worth of "what changed, and who is in it".
    ///
    /// The scope is what <see cref="DevelopmentDetector.Detect(NarrativeWorldState, DevelopmentScope)"/>
    /// should read next; the two name lists are who should be asked what it means to them, which is
    /// a different question with a different owner. Neither is a decision: nothing here says an
    /// actor wants anything, and nothing here says a scene should happen.
    ///
    /// Derived and transient in the strictest sense. It is built from records that are already
    /// authoritative, it is consumed once, and nothing about it is saved - a pass that was never
    /// taken leaves the world exactly as it was.
    /// </summary>
    public sealed class PressurePass
    {
        private static readonly IReadOnlyList<EntityId> Nobody = new EntityId[0];

        internal PressurePass(
            DevelopmentScope scope,
            IReadOnlyList<EntityId> actors,
            IReadOnlyList<EntityId> organizations)
        {
            Scope = scope;
            Actors = actors ?? Nobody;
            Organizations = organizations ?? Nobody;
        }

        /// <summary>The records whose pressure readings this pass should re-derive.</summary>
        public DevelopmentScope Scope { get; }

        /// <summary>
        /// People a changed record names, in stable id order.
        ///
        /// Woken whether or not they currently hold a goal, because the alternative is that only
        /// somebody already busy can notice their shop burned down. Being woken is not being
        /// informed: what any of them can legitimately make of the change stays
        /// <see cref="ActorPressureView"/>'s answer, and for most of a town the answer is nothing.
        /// </summary>
        public IReadOnlyList<EntityId> Actors { get; }

        /// <summary>Bodies a changed record names, in stable id order.</summary>
        public IReadOnlyList<EntityId> Organizations { get; }

        public bool IsEmpty => Scope.Count == 0 && Actors.Count == 0 && Organizations.Count == 0;

        public override string ToString()
        {
            return Scope + "; woken actors=" + Actors.Count + " organizations=" + Organizations.Count;
        }
    }

    /// <summary>
    /// The seam that closes the loop: real changes to authoritative state become the work set the
    /// next pressure reading is bounded to (BQa-012).
    ///
    /// BQa-006 made detection cheap by letting a caller say what changed. Nothing said it. That is
    /// the gap this fills, and it is deliberately the whole of what it fills: this collects
    /// changes, it does not interpret them. It reads no rule, forms no goal, opens no matter,
    /// writes no fact and records no event - running a pass over a world and throwing the result
    /// away leaves the world identical, which is the same promise the detector makes and for the
    /// same reason. Consequence ownership stays with `ConsequenceEngine`; facts, beliefs,
    /// businesses, demand, obligations, travel and relationships keep mutating themselves.
    ///
    /// <b>Three intakes, because changes arrive three ways.</b>
    ///
    /// <b>History announces most of them.</b> <see cref="Attach"/> subscribes to the ledger, and an
    /// appended event names the claims it is about, the place it happened in, and the people in it.
    /// Attach after loading, exactly as the consequence engine does: a listener added afterwards
    /// sees new events only, so a reload is not a replay.
    ///
    /// <b>Some changes no event announces.</b> A demand entry relieved, a claim superseded by the
    /// verb that repaired the thing, an obligation fulfilled: these mutate a store and append
    /// nothing, so a pure event listener would keep deriving a shortage that has been answered.
    /// <see cref="Changed"/> is the immediate route for whoever made such a change, and it resolves
    /// the id against the owning stores rather than trusting a caller to know which store it is in.
    ///
    /// <b>And some changes nobody announces at all.</b> Nothing appends an event when a business
    /// has now been failed for thirty days, and nothing will report a change that a caller simply
    /// forgot to mention. <see cref="Inspect"/> is the catch-all: a bounded rotation over the
    /// records the detector reads - never over actors, and never over history - that takes the next
    /// few each time it is called and comes round to everything eventually. It is the reason the
    /// other two intakes are allowed to be incomplete.
    ///
    /// <b>Waking.</b> A record that enters the work set wakes the people it names, so somebody with
    /// no goal at all is still considered when their world changes. It is bounded to the parties
    /// the record itself records - an operator, a debtor, a claim's subject, whoever the event was
    /// between - and never a sweep of the town for anybody who might care. Waking somebody is not
    /// telling them anything: <see cref="ActorPressureView"/> still decides what, if anything, they
    /// have a legitimate route to, and an unrelated actor is neither woken nor informed.
    /// </summary>
    public sealed class PressureFeedback
    {
        private readonly NarrativeWorldState _world;
        private readonly List<EntityId> _facts = new List<EntityId>();
        private readonly List<EntityId> _obligations = new List<EntityId>();
        private readonly List<EntityId> _sites = new List<EntityId>();
        private readonly List<EntityId> _businesses = new List<EntityId>();
        private readonly List<EntityId> _organizations = new List<EntityId>();
        private readonly List<EntityId> _actors = new List<EntityId>();
        private readonly List<EntityId> _bodies = new List<EntityId>();

        private List<Entry> _rotation;
        private int _cursor;
        private bool _attached;

        public PressureFeedback(NarrativeWorldState world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            _world = world;
        }

        /// <summary>
        /// How many records one pass will carry at most.
        ///
        /// A cap rather than an unbounded queue because the point of the work set is that the next
        /// reading is affordable, and a cascade of reactions between two passes must not be able to
        /// hand a whole save back to the detector one id at a time. Changes past the cap are not
        /// lost, they are late: <see cref="Inspect"/> reaches every record eventually.
        /// </summary>
        public int MostRecordsPerPass { get; set; } = 256;

        /// <summary>How many people one pass will name at most, for the same reason.</summary>
        public int MostWokenPerPass { get; set; } = 64;

        /// <summary>
        /// Listen to history.
        ///
        /// Idempotent, so a host that reattaches after a load does not double-collect. Call it
        /// after restoring a save: restored events are not dispatched, so nothing historical enters
        /// the work set and no consequence is applied twice.
        /// </summary>
        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;
            _world.Ledger.Subscribe(Observe);
        }

        /// <summary>How many records are waiting to be read.</summary>
        public int PendingCount =>
            _facts.Count + _obligations.Count + _sites.Count + _businesses.Count + _organizations.Count;

        /// <summary>How many people and bodies are waiting to be asked.</summary>
        public int WokenCount => _actors.Count + _bodies.Count;

        /// <summary>
        /// Something changed about this record, and no event said so.
        ///
        /// The id is resolved against the owning stores - claim, debt, business, body, place - so
        /// the caller does not have to know which work set it belongs in, and an id that belongs to
        /// none of them is refused rather than guessed at.
        ///
        /// Returns whether the id named a record this reads - not whether it was newly added. A
        /// change to something already in this pass is still a change to something real, and a
        /// caller told "no" for it would reasonably conclude it had the wrong id.
        /// </summary>
        public bool Changed(EntityId id)
        {
            if (id.IsNone)
            {
                return false;
            }

            Fact fact = _world.Knowledge.GetFact(id);
            if (fact != null)
            {
                return TakeFact(fact);
            }

            SocialObligation debt = _world.Obligations.Find(id);
            if (debt != null)
            {
                return TakeObligation(debt);
            }

            BusinessRecord business = _world.Businesses.Of(id);
            if (business != null)
            {
                return TakeBusiness(business);
            }

            Organization body = _world.Registry.GetOrganization(id);
            if (body != null)
            {
                return TakeOrganization(body);
            }

            // A place is the one work set with no record of its own: the demand ledger is keyed by
            // where, and a site the registry has never heard of can still be short of grain.
            if (_world.Registry.GetSite(id) != null || _world.Demands.At(id).Count > 0)
            {
                return TakePlace(id);
            }

            return false;
        }

        /// <summary>
        /// Take the next <paramref name="budget"/> records off a rotation over everything the
        /// detector reads, and report how many were taken.
        ///
        /// The safety net, and the only intake that does not need anybody to have noticed anything.
        /// It covers the two cases the other two cannot: a condition that changes with time rather
        /// than with an event, and an invalidation that was simply missed. Rotating rather than
        /// scanning is what keeps it affordable - one call costs its budget, not the size of the
        /// save - and it walks records rather than actors, so a world with ten thousand people in
        /// its history pays nothing for them here.
        ///
        /// The ordering is rebuilt when the rotation comes round, which is also when records added
        /// since the last time round join it. A record that has since gone is skipped and still
        /// costs its place in the budget, because a pass whose cost depended on how much of the
        /// world had been deleted would not be a budget.
        /// </summary>
        public int Inspect(int budget)
        {
            if (budget <= 0)
            {
                return 0;
            }

            if (_rotation == null || _cursor >= _rotation.Count)
            {
                _rotation = BuildRotation();
                _cursor = 0;
            }

            int taken = 0;
            while (taken < budget && _cursor < _rotation.Count)
            {
                Entry entry = _rotation[_cursor];
                _cursor++;
                taken++;

                switch (entry.Store)
                {
                    case Store.Fact:
                        Fact fact = _world.Knowledge.GetFact(entry.Id);
                        if (fact != null)
                        {
                            TakeFact(fact);
                        }

                        break;
                    case Store.Obligation:
                        SocialObligation debt = _world.Obligations.Find(entry.Id);
                        if (debt != null)
                        {
                            TakeObligation(debt);
                        }

                        break;
                    case Store.Site:
                        TakePlace(entry.Id);
                        break;
                    case Store.Business:
                        BusinessRecord business = _world.Businesses.Of(entry.Id);
                        if (business != null)
                        {
                            TakeBusiness(business);
                        }

                        break;
                    case Store.Organization:
                        Organization body = _world.Registry.GetOrganization(entry.Id);
                        if (body != null)
                        {
                            TakeOrganization(body);
                        }

                        break;
                }
            }

            return taken;
        }

        /// <summary>
        /// Everything collected since the last time, as a work set and the people it touches.
        ///
        /// Draining rather than reading: a change is answered once. Taking an empty pass is
        /// meaningful and cheap - it is what a tick in which nothing happened looks like, and
        /// detection over an empty work set derives nothing without reading a store.
        /// </summary>
        public PressurePass Take()
        {
            DevelopmentScope scope = DevelopmentScope.Affected();
            Drain(_facts, scope.Fact);
            Drain(_obligations, scope.Obligation);
            Drain(_sites, scope.Site);
            Drain(_businesses, scope.Business);
            Drain(_organizations, scope.Organization);

            List<EntityId> actors = new List<EntityId>(_actors);
            List<EntityId> bodies = new List<EntityId>(_bodies);
            _actors.Clear();
            _bodies.Clear();

            return new PressurePass(scope, actors, bodies);
        }

        private static void Drain(List<EntityId> from, Func<EntityId, DevelopmentScope> into)
        {
            for (int i = 0; i < from.Count; i++)
            {
                into(from[i]);
            }

            from.Clear();
        }

        // -- history ---------------------------------------------------------------------------

        /// <summary>
        /// What one appended event changed.
        ///
        /// Read off the event's own references rather than from its type: an event says which
        /// claims it is about, where it happened and who was in it, and every one of those is a
        /// record whose pressure reading may now be different. Nothing is inferred from the kind of
        /// event it was, which is what keeps this from becoming a second consequence table that
        /// would eventually disagree with the first.
        /// </summary>
        private void Observe(WorldEvent worldEvent)
        {
            if (worldEvent == null)
            {
                return;
            }

            // A target that is a person is somebody in the event, not a record to re-read, and
            // asking every store about them would walk the debt ledger once per event to learn
            // nothing. Whether they are in it is answered below.
            if (!_world.Registry.IsActor(worldEvent.Target))
            {
                Changed(worldEvent.Target);
            }

            TakeAll(worldEvent.Related);
            TakeAll(worldEvent.Evidence);

            if (!worldEvent.Zone.IsNone)
            {
                TakePlace(worldEvent.Zone);
            }

            Wake(worldEvent.Actor);
            Wake(worldEvent.Target);
            for (int i = 0; i < worldEvent.Witnesses.Count; i++)
            {
                Wake(worldEvent.Witnesses[i]);
            }
        }

        private void TakeAll(IReadOnlyList<EntityId> ids)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                Changed(ids[i]);
            }
        }

        // -- collecting ------------------------------------------------------------------------

        /// <summary>
        /// A claim, and the people the claim itself is between.
        ///
        /// Its subject, whoever or whatever it is about, and everyone who holds it - because the
        /// last of those is how somebody who has just been told something gets looked at again.
        /// A report, a rumour reaching one more ear or a witness finally saying what they saw
        /// changes nothing about the world and everything about who is now in a position to do
        /// something, and a seam that only woke the parties named in the claim would let that
        /// change pass unremarked.
        ///
        /// Still not a sweep: the knowers of one claim are the people who actually hold it, which
        /// is the same set the detector reads for that claim anyway.
        /// </summary>
        private bool TakeFact(Fact fact)
        {
            Add(_facts, fact.Id);
            Wake(fact.Subject);
            Wake(fact.Object);
            foreach (EntityId knower in _world.Knowledge.Knowers(fact.Id))
            {
                Wake(knower);
            }

            return true;
        }

        private bool TakeObligation(SocialObligation debt)
        {
            Add(_obligations, debt.Id);
            Wake(debt.Debtor);
            Wake(debt.Creditor);
            return true;
        }

        private bool TakeBusiness(BusinessRecord business)
        {
            Add(_businesses, business.BusinessId);
            Wake(business.OperatorId);
            Wake(business.ReplacementOperatorId);
            Wake(business.InheritedById);
            return true;
        }

        private bool TakeOrganization(Organization body)
        {
            Add(_organizations, body.Id);
            if (!_bodies.Contains(body.Id) && _bodies.Count + _actors.Count < MostWokenPerPass)
            {
                _bodies.Add(body.Id);
                _bodies.Sort();
            }

            Wake(body.LeaderId);
            return true;
        }

        /// <summary>
        /// A place, and whoever the pressures recorded there are already about.
        ///
        /// Not everybody who lives there. A town short of grain is a condition of the place, and
        /// who in it is troubled enough to do something is exactly the question this layer must not
        /// answer - so the people woken are the ones the demand entries themselves name through the
        /// claims they cite, and the rest of the town is reached, if at all, by the pass that comes
        /// to them on their own terms.
        /// </summary>
        private bool TakePlace(EntityId placeId)
        {
            if (placeId.IsNone)
            {
                return false;
            }

            Add(_sites, placeId);

            IReadOnlyList<LocalDemandPressure> pressures = _world.Demands.At(placeId);
            for (int i = 0; i < pressures.Count; i++)
            {
                Fact source = _world.Knowledge.GetFact(pressures[i].SourceFactId);
                if (source != null)
                {
                    Wake(source.Subject);
                }
            }

            return true;
        }

        /// <summary>
        /// Put this id in its work set, if it is not already there and the pass has room.
        ///
        /// Deliberately not the gate on waking. A record can change twice before anybody reads it,
        /// and the second change is frequently the one that brings a new person into it - somebody
        /// told about a claim that was already going to be re-read. Skipping the wake because the
        /// id was already listed would lose exactly the change that mattered, and the work set does
        /// not need the id twice to answer for it.
        /// </summary>
        private bool Add(List<EntityId> into, EntityId id)
        {
            if (id.IsNone || into.Contains(id) || PendingCount >= MostRecordsPerPass)
            {
                return false;
            }

            into.Add(id);
            into.Sort();
            return true;
        }

        /// <summary>
        /// Consider this person next pass, whatever they are currently up to.
        ///
        /// Only somebody the registry holds as an actor: a retired alias is a name history uses,
        /// not a second person to wake, and an item id named as an event's target is not anybody at
        /// all.
        /// </summary>
        private void Wake(EntityId id)
        {
            if (id.IsNone
                || !_world.Registry.IsActor(id)
                || _actors.Contains(id)
                || _actors.Count + _bodies.Count >= MostWokenPerPass)
            {
                return;
            }

            _actors.Add(id);
            _actors.Sort();
        }

        // -- the rotation ----------------------------------------------------------------------

        private enum Store
        {
            Fact,
            Obligation,
            Site,
            Business,
            Organization
        }

        private struct Entry
        {
            internal Entry(Store store, EntityId id)
            {
                Store = store;
                Id = id;
            }

            internal Store Store { get; }

            internal EntityId Id { get; }
        }

        /// <summary>
        /// Every record the detector can read, in one stable order.
        ///
        /// Sorted per store and concatenated in a fixed store order, so the rotation visits the
        /// same records in the same sequence in two identical worlds and across a reload. Built
        /// once per turn of the rotation rather than per call: it is the one O(records) cost here,
        /// and it is paid when the cursor comes round rather than every inspection.
        /// </summary>
        private List<Entry> BuildRotation()
        {
            List<Entry> rotation = new List<Entry>();
            Append(rotation, Store.Fact, Sorted(_world.Knowledge.Facts.Keys));

            List<EntityId> debts = new List<EntityId>();
            IReadOnlyList<SocialObligation> records = _world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                debts.Add(records[i].Id);
            }

            Append(rotation, Store.Obligation, Sorted(debts));

            List<EntityId> places = new List<EntityId>();
            IReadOnlyList<LocalDemandPressure> pressures = _world.Demands.Pressures;
            for (int i = 0; i < pressures.Count; i++)
            {
                if (!places.Contains(pressures[i].PlaceId))
                {
                    places.Add(pressures[i].PlaceId);
                }
            }

            Append(rotation, Store.Site, Sorted(places));

            List<EntityId> businesses = new List<EntityId>();
            foreach (BusinessRecord business in _world.Businesses.Records)
            {
                businesses.Add(business.BusinessId);
            }

            Append(rotation, Store.Business, Sorted(businesses));
            Append(rotation, Store.Organization, Sorted(_world.Registry.Organizations.Keys));
            return rotation;
        }

        private static List<EntityId> Sorted(IEnumerable<EntityId> ids)
        {
            List<EntityId> sorted = new List<EntityId>(ids);
            sorted.Sort();
            return sorted;
        }

        private static void Append(List<Entry> rotation, Store store, List<EntityId> ids)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                rotation.Add(new Entry(store, ids[i]));
            }
        }
    }
}
