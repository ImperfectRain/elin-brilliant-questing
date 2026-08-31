namespace BrilliantQuesting.World
{
    public enum PersonalityContradiction
    {
        None,
        CowardlyButProtective,
        HonestExceptAboutFamily,
        GreedyButRefusesMedicineProfit,
        ViolentButHatesTheft,
        CriminalWhoRespectsContracts
    }

    /// <summary>
    /// A durable exception to an actor's ordinary personality read. Contradictions are narrow
    /// decision modifiers: they do not create goals, and they only matter when the current problem
    /// presents the protected topic.
    /// </summary>
    public sealed class ContradictionProfile
    {
        public PersonalityContradiction Kind { get; set; } = PersonalityContradiction.None;

        public double Strength { get; set; } = 1.0;

        public bool HasContradiction => Kind != PersonalityContradiction.None;
    }
}
