using System;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Events
{
    /// <summary>
    /// An event identity handed out before the event exists.
    ///
    /// Needed because the order is sometimes backwards: a recorder has to build the fact, the
    /// obligation or the matter that an event produces *before* it can record the event, and each
    /// of those has to name the occurrence it came from. Without this the only ways to write that
    /// reference are to predict the next id or to patch the record afterwards, and BQa-001 rules
    /// out both - a predicted id is wrong the moment anything else mints one in between, and a
    /// patched record is history rewritten.
    ///
    /// Single use. Recording twice against one reservation would put two events in the ledger
    /// under one id, which is the one failure this type exists to make impossible, so it throws
    /// rather than quietly minting a second.
    ///
    /// Reserving and then not recording is fine: the id is spent and never reissued, exactly as
    /// <see cref="IdMinter"/> already guarantees. Read-only inspection reserves nothing.
    /// </summary>
    public sealed class EventReservation
    {
        internal EventReservation(EntityId id)
        {
            Id = id;
        }

        /// <summary>The id the event will have. Allocated by the world's minter, not guessed.</summary>
        public EntityId Id { get; }

        public bool IsConsumed { get; private set; }

        internal void Consume()
        {
            if (IsConsumed)
            {
                throw new InvalidOperationException(
                    "Event identity " + Id + " has already been recorded; reserve a new one rather than " +
                    "recording a second event under the same id.");
            }

            IsConsumed = true;
        }

        public override string ToString() => Id + (IsConsumed ? " (recorded)" : " (reserved)");
    }
}
