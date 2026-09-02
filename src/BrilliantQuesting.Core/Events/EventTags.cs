namespace BrilliantQuesting.Events
{
    /// <summary>Controlled tags that change how the consequence layer reads an event.</summary>
    public static class EventTags
    {
        /// <summary>
        /// It happened, it is in history, and nobody in the world noticed.
        ///
        /// This matters more than it looks: a clean theft must not move the victim's affinity,
        /// because affinity moving is itself information. Without this tag, perfect stealth would
        /// quietly tell the target they had been robbed.
        /// </summary>
        public const string Unnoticed = "unnoticed";

        /// <summary>
        /// Seen happening in the world, with no judgement attached about what it meant.
        ///
        /// The observer can tell that A struck B. It cannot tell murder from self-defence, a
        /// lawful bounty from an assault, a duel from a mugging, or clearing a dungeon from
        /// killing a shopkeeper - and the first live run had a yeek attacking the player and a
        /// guard shooting a gangster arrive through exactly the same door as a crime would.
        ///
        /// Consequences that encode a social or legal verdict - karma, fame - are withheld from
        /// events carrying this tag until something can actually classify them, which is BQ-046.
        /// The physical event is still recorded in full: what is deferred is the meaning, not the
        /// fact. Affinity is not withheld, because being hit is a reason to like somebody less
        /// whatever the law thinks of it.
        /// </summary>
        public const string Observed = "observed_vanilla";

        /// <summary>The target explicitly admitted the related claim.</summary>
        public const string Admission = "admission";

        /// <summary>The target was pressed about the related claim and chose not to give it up.</summary>
        public const string Withheld = "withheld";

        /// <summary>
        /// The speaker put the related claim forward as so (BQ-073).
        ///
        /// Stated rather than left to be inferred from the absence of <see cref="Denied"/>,
        /// because the pair is what makes a recorded statement readable at all: "he said Kip took
        /// it" and "he said Kip did not take it" are opposite claims about the same fact, and a
        /// ledger that recorded only which fact was mentioned could not tell a later system which
        /// of the two it was looking at.
        ///
        /// An event carrying neither tag is a deception recorded before this vocabulary existed,
        /// or by a layer that does not know the speaker's stance. Those are left alone rather than
        /// guessed at.
        /// </summary>
        public const string Affirmed = "affirmed";

        /// <summary>The speaker put the related claim forward as not so (BQ-073).</summary>
        public const string Denied = "denied";
    }
}
