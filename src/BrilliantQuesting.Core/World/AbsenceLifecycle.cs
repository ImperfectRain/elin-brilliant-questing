using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>What one reconciliation pass actually did. Empty is the normal answer.</summary>
    public sealed class AbsenceRound
    {
        /// <summary>Absences the game had quietly undone and that were put back.</summary>
        public int Enforced { get; internal set; }

        /// <summary>People whose time was up and who are home again.</summary>
        public int Returned { get; internal set; }

        /// <summary>Physical absences demoted to Grade A because the game would not keep them.</summary>
        public int Demoted { get; internal set; }

        /// <summary>Records dropped because the person they were about is dead.</summary>
        public int Closed { get; internal set; }

        /// <summary>
        /// People who are due home and could not be brought there yet.
        ///
        /// Counted rather than passed over in silence, because this is the one state that does not
        /// resolve itself: the record is deliberately kept - dropping it would leave somebody the
        /// mod moved with nothing left that remembers to move them back - so if the reason persists
        /// it needs to be visible in the log rather than retried forever without a word.
        /// </summary>
        public int Stuck { get; internal set; }

        public List<string> Notes { get; } = new List<string>();

        public bool DidAnything => Enforced > 0 || Returned > 0 || Demoted > 0 || Closed > 0 || Stuck > 0;

        public override string ToString()
        {
            return "enforced " + Enforced + ", returned " + Returned + ", demoted " + Demoted
                   + ", closed " + Closed + ", stuck " + Stuck;
        }
    }

    /// <summary>
    /// Who is away, kept true.
    ///
    /// The absence ledger is the mod's intent; Elin is where people actually are; and those two
    /// drift apart constantly through no fault of anybody's - a town refreshes its citizens, a
    /// zone is rebuilt when the player walks back into it, a save is loaded and the game puts
    /// everybody where it last wrote them. This class is the only thing that closes that gap, and
    /// it closes it by re-deriving the answer every time rather than by trusting a flag: nothing
    /// anywhere records "the game has been told", because that is precisely the fact a reload
    /// invalidates.
    ///
    /// Two rules make duplication impossible rather than unlikely. An absence names an
    /// <see cref="EntityId"/> that already exists and moves that one character, so nothing is ever
    /// spawned to stand in for somebody who is away; and the ledger holds one record per person,
    /// so no amount of re-entry can file a second.
    /// </summary>
    public sealed class AbsenceLifecycle
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public AbsenceLifecycle(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        /// <summary>
        /// Grade A. The person stays exactly where they are and stops doing what they do.
        ///
        /// Writes nothing into the game, which is why it is available for anybody: their vanilla
        /// shop, schedule and dialogue are untouched, and what closes is the set of procedural
        /// routes this mod runs through them - their trade, their office, the work they take.
        /// </summary>
        public bool TryWithdrawService(EntityId actor, string reason, GameTime expectedReturn)
        {
            if (!CanBegin(actor))
            {
                return false;
            }

            return Begin(new ActorAbsence(
                actor, AbsenceGrade.ServiceOnly, reason, _vanilla.Now, expectedReturn));
        }

        /// <summary>
        /// Grade B. The person leaves, and the game is told before anything is written down.
        ///
        /// Refused, rather than approximated, when the departure cannot be made real: a build that
        /// cannot move a character between zones, an actor the mutation policy protects, nowhere
        /// to go, nowhere to come back to, or a game that simply declined. That order matters - an
        /// absence recorded on the strength of a call that failed would be procedural state
        /// describing a town the player is not looking at, which is the failure this whole step
        /// exists to prevent.
        /// </summary>
        public bool TrySendAway(EntityId actor, EntityId awayZone, string reason, GameTime expectedReturn)
        {
            if (!CanBegin(actor) || !CanLeave(actor, awayZone))
            {
                return false;
            }

            EntityId homeZone = _vanilla.GetZoneOf(actor);
            if (homeZone.IsNone || homeZone == awayZone)
            {
                // Nowhere to put them back, or they are already there. Either way this is not a
                // departure, and pretending otherwise would leave nothing to reconcile against.
                return false;
            }

            if (!_vanilla.TrySendAway(actor, awayZone))
            {
                return false;
            }

            return Begin(new ActorAbsence(
                actor, AbsenceGrade.Physical, reason, _vanilla.Now, expectedReturn, awayZone, homeZone));
        }

        /// <summary>
        /// Ends an absence early, and reports whether it is actually over.
        ///
        /// A Grade B record is cleared only once the person is demonstrably home. Dropping it
        /// while the game still has them elsewhere would leave somebody the mod moved with nothing
        /// left that remembers to move them back.
        /// </summary>
        public bool TryEnd(EntityId actor, string reason)
        {
            ActorAbsence absence = _world.Absences.Of(actor);
            if (absence == null)
            {
                return false;
            }

            if (absence.Grade == AbsenceGrade.Physical && !BringHome(absence))
            {
                return false;
            }

            _world.Absences.Remove(actor);
            _world.Record(WorldEventType.Returned, actor, EntityId.None, _vanilla.Now, 0.4,
                tags: new[] { reason ?? string.Empty });
            return true;
        }

        /// <summary>
        /// Makes the game agree with the ledger, and lets whoever is due back come back.
        ///
        /// Safe to call as often as anything likes and does nothing when there is nothing to do,
        /// which is what lets it hang off every re-entry point the game has - a load, a day
        /// turning, the player walking into a zone - without any of them having to know what the
        /// others do.
        /// </summary>
        public AbsenceRound Reconcile()
        {
            AbsenceRound round = new AbsenceRound();
            if (_world.Absences.Count == 0)
            {
                return round;
            }

            // A copy, because returning somebody removes their record while we are walking it.
            List<ActorAbsence> active = new List<ActorAbsence>(_world.Absences.Active);
            for (int i = 0; i < active.Count; i++)
            {
                Reconcile(active[i], round);
            }

            return round;
        }

        private void Reconcile(ActorAbsence absence, AbsenceRound round)
        {
            EntityId actor = absence.ActorId;

            // Somebody who died while they were away is not coming back, and a record that keeps
            // trying to move a corpse home would never clear.
            if (!_vanilla.IsAlive(actor))
            {
                _world.Absences.Remove(actor);
                round.Closed++;
                round.Notes.Add(Name(actor) + " died while away; absence closed");
                return;
            }

            if (absence.IsDue(_vanilla.Now))
            {
                if (absence.Grade != AbsenceGrade.Physical || BringHome(absence))
                {
                    _world.Absences.Remove(actor);
                    _world.Record(WorldEventType.Returned, actor, EntityId.None, _vanilla.Now, 0.4,
                        tags: new[] { absence.Reason });
                    round.Returned++;
                    round.Notes.Add(Name(actor) + " is back");
                }
                else
                {
                    // Keep the record and say so. They are still out there - a build that can no
                    // longer move anybody, or a character it cannot resolve - and the next pass
                    // tries again, which is the whole reason the record must not be dropped.
                    round.Stuck++;
                    round.Notes.Add(Name(actor) + " is due home from " + _vanilla.GetZoneOf(actor)
                                    + " and could not be moved there");
                }

                return;
            }

            if (absence.Grade != AbsenceGrade.Physical)
            {
                return;
            }

            EntityId where = _vanilla.GetZoneOf(actor);
            if (where.IsNone || where == absence.AwayZoneId)
            {
                // Either the game agrees, or it cannot say where they are. An unanswered question
                // is not evidence that an absence has been undone, and acting on it would demote
                // a perfectly good absence every time the character was out of reach.
                return;
            }

            if (_vanilla.TrySendAway(actor, absence.AwayZoneId))
            {
                round.Enforced++;
                round.Notes.Add(Name(actor) + " had been put back in " + where + "; sent away again");
                return;
            }

            // The game has them, and the mod may no longer move them - a lost capability, or a
            // classification that changed underneath the absence. Say the smaller true thing
            // rather than the larger false one: their trade is still interrupted, and the
            // simulation stops claiming they are somewhere they are not.
            absence.Grade = AbsenceGrade.ServiceOnly;
            round.Demoted++;
            round.Notes.Add(Name(actor) + " could not be kept away; absence demoted to service only");
        }

        /// <summary>
        /// Puts somebody back where they left from. True when they are there afterwards, including
        /// when the game had already brought them home itself.
        /// </summary>
        private bool BringHome(ActorAbsence absence)
        {
            if (absence.HomeZoneId.IsNone)
            {
                return false;
            }

            return _vanilla.GetZoneOf(absence.ActorId) == absence.HomeZoneId
                   || _vanilla.TryBringBack(absence.ActorId, absence.HomeZoneId);
        }

        private bool Begin(ActorAbsence absence)
        {
            if (!_world.Absences.TryAdd(absence))
            {
                return false;
            }

            _world.Record(WorldEventType.WentAbsent, absence.ActorId, EntityId.None, _vanilla.Now, 0.5,
                zone: absence.HomeZoneId, tags: new[] { absence.Reason });
            return true;
        }

        /// <summary>
        /// Nobody, the player, and anybody already away are refused. The one-record rule is the
        /// ledger's, and this is the first place it is asked, so a caller learns "no" before
        /// anything has been moved.
        /// </summary>
        private bool CanBegin(EntityId actor)
        {
            return !actor.IsNone
                   && actor != _vanilla.PlayerId
                   && !_world.Absences.IsAbsent(actor);
        }

        /// <summary>
        /// Whether a physical departure is possible at all - a capability question and a policy
        /// question, both asked before anything happens, so a route that certainly cannot run is
        /// absent rather than offered and then declined.
        /// </summary>
        public bool CanLeave(EntityId actor, EntityId awayZone)
        {
            return !awayZone.IsNone
                   && _vanilla.Supports(VanillaCapability.MoveCharaBetweenZones)
                   && _vanilla.MayMutate(actor, MutationKind.TemporaryAbsence);
        }

        private string Name(EntityId actor) => _world.Registry.NameOf(actor);
    }
}
