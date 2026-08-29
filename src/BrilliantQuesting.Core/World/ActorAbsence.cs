using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// How far somebody has gone. The design's two shippable grades (LW 5.2), in the order of how
    /// much of the game they touch.
    ///
    /// Grade C - removing a unique or story NPC outright - is deliberately not here. It is not a
    /// third value this enum is missing; it is a reach the mutation policy does not grant to
    /// anybody, and adding a name for it would invite a caller to ask for it.
    /// </summary>
    public enum AbsenceGrade
    {
        /// <summary>Nobody is away. The value a lookup returns when there is no record.</summary>
        None = 0,

        /// <summary>
        /// Grade A. The person is exactly where they were; what has stopped is what they do.
        ///
        /// Nothing is written into the game at all: their vanilla shop, their dialogue and their
        /// schedule are untouched, and what closes is the set of procedural routes this mod would
        /// have run through them. That is the whole reason it is the safe grade - a build that
        /// can express nothing else can still express "the fence is not taking work this week".
        /// </summary>
        ServiceOnly = 1,

        /// <summary>
        /// Grade B. The person is not where they were, and procedural state is what knows where
        /// they are.
        ///
        /// The dangerous one, and the reason this step exists: an absence that the game quietly
        /// undoes - a citizen refresh, a zone rebuilt on entry, a save reloaded - leaves the
        /// simulation describing a town the player is not looking at. It is enforced by
        /// reconciliation rather than by trusting the write, and it always names where they went.
        /// </summary>
        Physical = 2
    }

    /// <summary>
    /// One person who is away, and everything needed to put them back.
    ///
    /// Two things are deliberately *not* on this record. There is no "the game has been told"
    /// flag: whether Elin currently agrees is a fact about this session, it is re-derived on every
    /// reconciliation, and persisting it is exactly how a reload would leave somebody standing in
    /// the market while the simulation insisted they had left town. And there is no second copy of
    /// the person - an absence refers to an <see cref="EntityId"/> that already exists, which is
    /// what makes "no duplication" a property of the shape rather than a rule to remember.
    /// </summary>
    public sealed class ActorAbsence
    {
        /// <summary>No date has been set for coming back. Times are never negative, so -1 is free.</summary>
        public static readonly GameTime NoScheduledReturn = new GameTime(-1);

        public ActorAbsence(
            EntityId actorId,
            AbsenceGrade grade,
            string reason,
            GameTime beganAt,
            GameTime expectedReturn,
            EntityId awayZoneId = default,
            EntityId homeZoneId = default)
        {
            ActorId = actorId;
            Grade = grade;
            Reason = reason ?? string.Empty;
            BeganAt = beganAt;
            ExpectedReturn = expectedReturn;
            AwayZoneId = awayZoneId;
            HomeZoneId = homeZoneId;
        }

        public EntityId ActorId { get; }

        /// <summary>
        /// Settable because a Grade B absence the mod can no longer enforce is demoted to Grade A
        /// rather than left claiming somebody is elsewhere. Nothing else moves it.
        /// </summary>
        public AbsenceGrade Grade { get; internal set; }

        /// <summary>Ontology term - "gone to ground", "travelling", "shut up shop". Never prose.</summary>
        public string Reason { get; }

        public GameTime BeganAt { get; }

        /// <summary>When they are due back, or <see cref="NoScheduledReturn"/>.</summary>
        public GameTime ExpectedReturn { get; }

        /// <summary>Where the game is to keep them while they are away. Nobody, for Grade A.</summary>
        public EntityId AwayZoneId { get; }

        /// <summary>Where they were standing when they left, and where they are put back.</summary>
        public EntityId HomeZoneId { get; }

        public bool ReturnsOnSchedule => ExpectedReturn.TotalMinutes >= 0;

        public bool IsDue(GameTime now) => ReturnsOnSchedule && now >= ExpectedReturn;

        public override string ToString()
        {
            return ActorId + " " + Grade + " (" + Reason + ")"
                   + (ReturnsOnSchedule ? " until " + ExpectedReturn : " indefinitely");
        }
    }

    /// <summary>
    /// Who is away, keyed by who they are.
    ///
    /// A dictionary rather than a list, and that is the anti-duplication argument in one line: an
    /// actor has one absence or none, so no sequence of situations, reloads or reconciliations can
    /// arrange for a person to be away twice, and no code has to remember to look for a second
    /// record before writing one.
    ///
    /// Plain state. <see cref="AbsenceLifecycle"/> is the way in for anything that should also be
    /// history or should also move somebody in the game; this type is what the save reads and
    /// writes, and what a precondition asks.
    /// </summary>
    public sealed class AbsenceLedger
    {
        private readonly Dictionary<EntityId, ActorAbsence> _byActor = new Dictionary<EntityId, ActorAbsence>();

        public int Count => _byActor.Count;

        public IEnumerable<ActorAbsence> Active => _byActor.Values;

        public ActorAbsence Of(EntityId actor)
        {
            _byActor.TryGetValue(actor, out ActorAbsence absence);
            return absence;
        }

        public AbsenceGrade GradeOf(EntityId actor)
        {
            ActorAbsence absence = Of(actor);
            return absence == null ? AbsenceGrade.None : absence.Grade;
        }

        /// <summary>Away in any sense: their trade is shut, whether or not they are on the map.</summary>
        public bool IsAbsent(EntityId actor) => GradeOf(actor) != AbsenceGrade.None;

        /// <summary>Away in the flesh. What a verb that needs somebody standing there asks.</summary>
        public bool IsPhysicallyAbsent(EntityId actor) => GradeOf(actor) == AbsenceGrade.Physical;

        /// <summary>
        /// Files an absence, or refuses because this person already has one. Never merges two:
        /// the second caller is told no and has to decide what it wanted.
        /// </summary>
        public bool TryAdd(ActorAbsence absence)
        {
            if (absence == null || absence.ActorId.IsNone
                || absence.Grade == AbsenceGrade.None
                || _byActor.ContainsKey(absence.ActorId))
            {
                return false;
            }

            _byActor[absence.ActorId] = absence;
            return true;
        }

        public bool Remove(EntityId actor) => _byActor.Remove(actor);

        /// <summary>
        /// The load path. Same rule as <see cref="TryAdd"/> - a save carrying two records for one
        /// person keeps the first and drops the rest, rather than deciding between them.
        /// </summary>
        public void Restore(ActorAbsence absence) => TryAdd(absence);
    }
}
