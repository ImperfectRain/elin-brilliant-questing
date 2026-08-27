namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Why an action is or is not on the table.
    ///
    /// The distinction the design document insists on lives here: <see cref="Impossible"/> is for
    /// things that cannot be attempted at all (you cannot reveal a secret you have never heard),
    /// while a low skill is never a reason to hide an option - a terrible liar is allowed to lie
    /// badly. Unavailable options keep their reason so the debug inspector can answer "why not?".
    /// </summary>
    public readonly struct Availability
    {
        private Availability(bool available, string reason)
        {
            IsAvailable = available;
            Reason = reason;
        }

        public bool IsAvailable { get; }

        public string Reason { get; }

        public static Availability Available(string note = null) => new Availability(true, note ?? string.Empty);

        /// <summary>Semantically impossible, or barred by a real vanilla capability limit.</summary>
        public static Availability Impossible(string reason) => new Availability(false, reason);

        /// <summary>Doesn't apply here - wrong target, nothing to act on, nothing at stake.</summary>
        public static Availability NotRelevant(string reason) => new Availability(false, reason);

        public override string ToString() => IsAvailable ? "available" : "unavailable: " + Reason;
    }
}
