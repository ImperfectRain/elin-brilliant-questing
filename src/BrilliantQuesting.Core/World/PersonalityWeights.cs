namespace BrilliantQuesting.World
{
    /// <summary>
    /// Decision weights, not RPG stats. These decide what a character *wants* to do; vanilla Elin
    /// attributes and skills decide what they can actually pull off. Keeping the two separate is
    /// what stops the mod from growing a shadow character sheet.
    ///
    /// All values are 0..1.
    /// </summary>
    public sealed class PersonalityWeights
    {
        public double Greed { get; set; } = 0.5;

        public double Mercy { get; set; } = 0.5;

        public double Courage { get; set; } = 0.5;

        public double Honesty { get; set; } = 0.5;

        public double Ambition { get; set; } = 0.5;

        public double Loyalty { get; set; } = 0.5;

        public double Sociability { get; set; } = 0.5;

        public double Curiosity { get; set; } = 0.5;

        public double Vengefulness { get; set; } = 0.5;

        public PersonalityWeights Clone()
        {
            return new PersonalityWeights
            {
                Greed = Greed,
                Mercy = Mercy,
                Courage = Courage,
                Honesty = Honesty,
                Ambition = Ambition,
                Loyalty = Loyalty,
                Sociability = Sociability,
                Curiosity = Curiosity,
                Vengefulness = Vengefulness
            };
        }
    }
}
