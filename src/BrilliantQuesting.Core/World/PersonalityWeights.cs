namespace BrilliantQuesting.World
{
    /// <summary>
    /// Private behavioural dimensions, not RPG stats. These decide what a character *wants* to
    /// do; vanilla Elin attributes and skills decide what they can actually pull off. Keeping the
    /// two separate is what stops the mod from growing a shadow character sheet.
    ///
    /// All values are 0..1, with higher values meaning the first pole of the named pair.
    /// </summary>
    public sealed class PersonalityWeights
    {
        public double Boldness { get; set; } = 0.5;

        public double Patience { get; set; } = 0.5;

        public double Warmth { get; set; } = 0.5;

        public double Earnestness { get; set; } = 0.5;

        public double Optimism { get; set; } = 0.5;

        public double Orderliness { get; set; } = 0.5;

        public double Mercy { get; set; } = 0.5;

        public double Honesty { get; set; } = 0.5;

        public double Generosity { get; set; } = 0.5;

        public double Loyalty { get; set; } = 0.5;

        public double Trust { get; set; } = 0.5;

        public double Humility { get; set; } = 0.5;

        public double Curiosity { get; set; } = 0.5;

        public double Conventionality { get; set; } = 0.5;

        public double StatusBlindness { get; set; } = 0.5;

        public double Greed
        {
            get => 1.0 - Generosity;
            set => Generosity = Invert(value);
        }

        public double Courage
        {
            get => Boldness;
            set => Boldness = value;
        }

        public double Ambition
        {
            get => ((1.0 - StatusBlindness) + (1.0 - Humility)) / 2.0;
            set
            {
                StatusBlindness = Invert(value);
                Humility = Invert(value);
            }
        }

        public double Sociability
        {
            get => Warmth;
            set => Warmth = value;
        }

        public double Vengefulness
        {
            get => 1.0 - Mercy;
            set => Mercy = Invert(value);
        }

        public PersonalityWeights Clone()
        {
            return new PersonalityWeights
            {
                Boldness = Boldness,
                Patience = Patience,
                Warmth = Warmth,
                Earnestness = Earnestness,
                Optimism = Optimism,
                Orderliness = Orderliness,
                Mercy = Mercy,
                Honesty = Honesty,
                Generosity = Generosity,
                Loyalty = Loyalty,
                Trust = Trust,
                Humility = Humility,
                Curiosity = Curiosity,
                Conventionality = Conventionality,
                StatusBlindness = StatusBlindness
            };
        }

        private static double Invert(double value) => 1.0 - value;
    }
}
