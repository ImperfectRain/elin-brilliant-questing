namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// What kind of uncertainty a check represents (BQa-003).
    ///
    /// A profile has to say this out loud rather than have it read off whichever terms happen to
    /// be on the row, because the two are not the same question. "Nobody is resisting this" and
    /// "somebody is resisting this and the row forgot to say who" produce identical arithmetic
    /// today and want opposite treatment the moment difficulty stops being a flat sum. Classifying
    /// by inspection would quietly answer the second with the first.
    ///
    /// Declaring it also makes the ambiguous row a build-time argument rather than a silent one:
    /// an absolute profile may not take a target term at all, and an opposed profile that declares
    /// no opposition is a contradiction the classification test names.
    /// </summary>
    public enum CheckFamily
    {
        /// <summary>
        /// Actor capability against an opposing actor or state. Somebody on the other side is
        /// trying not to be lied to, spotted following them, or held still.
        /// </summary>
        Opposed = 0,

        /// <summary>
        /// Actor capability against a fixed challenge in the world. A lock, a cipher, a standard
        /// a dish is judged against. It does not rise because the actor did.
        /// </summary>
        Absolute = 1,

        /// <summary>
        /// No roll: the outcome is impossible, or the uncertainty is not semantically present.
        /// Laying goods on an altar is not a skill test; either the god is yours and you have
        /// something to give, or you do not.
        ///
        /// No <see cref="CheckProfile"/> carries this - a profile is a roll, and a thing with no
        /// uncertainty in it has no profile. It is the answer
        /// <see cref="Actions.Library.ProceduralCheckProfiles.FamilyForAction"/> gives for a verb
        /// that rolls nothing, so that "which check runs?" has an answer there instead of a gap.
        /// </summary>
        Certain = 2
    }
}
