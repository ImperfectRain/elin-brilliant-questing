using System;

namespace BrilliantQuesting.World
{
    public enum ValueConcern
    {
        Family,
        Wealth,
        Law,
        Faith,
        Status,
        Animals,
        Knowledge,
        Freedom
    }

    public enum NarrativeNeed
    {
        Safety,
        Belonging,
        DebtRelief,
        Status,
        Loyalty,
        Justice,
        Secrecy,
        Revenge,
        Protection,
        MaterialShortage,
        Obligation
    }

    public sealed class ValueProfile
    {
        public ValueProfile()
        {
            Family = new ValueConcernProfile();
            Wealth = new ValueConcernProfile();
            Law = new ValueConcernProfile();
            Faith = new ValueConcernProfile();
            Status = new ValueConcernProfile();
            Animals = new ValueConcernProfile();
            Knowledge = new ValueConcernProfile();
            Freedom = new ValueConcernProfile();
        }

        public ValueConcernProfile Family { get; }

        public ValueConcernProfile Wealth { get; }

        public ValueConcernProfile Law { get; }

        public ValueConcernProfile Faith { get; }

        public ValueConcernProfile Status { get; }

        public ValueConcernProfile Animals { get; }

        public ValueConcernProfile Knowledge { get; }

        public ValueConcernProfile Freedom { get; }

        public ValueConcernProfile Get(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Family:
                    return Family;
                case ValueConcern.Wealth:
                    return Wealth;
                case ValueConcern.Law:
                    return Law;
                case ValueConcern.Faith:
                    return Faith;
                case ValueConcern.Status:
                    return Status;
                case ValueConcern.Animals:
                    return Animals;
                case ValueConcern.Knowledge:
                    return Knowledge;
                case ValueConcern.Freedom:
                    return Freedom;
                default:
                    throw new ArgumentOutOfRangeException(nameof(concern), concern, "Unknown value concern.");
            }
        }
    }

    public sealed class ValueConcernProfile
    {
        public double Importance { get; set; } = 0.5;

        public double Flexibility { get; set; } = 0.5;
    }

    public sealed class NarrativeNeedProfile
    {
        public double Safety { get; set; }

        public double Belonging { get; set; }

        public double DebtRelief { get; set; }

        public double Status { get; set; }

        public double Loyalty { get; set; }

        public double Justice { get; set; }

        public double Secrecy { get; set; }

        public double Revenge { get; set; }

        public double Protection { get; set; }

        public double MaterialShortage { get; set; }

        public double Obligation { get; set; }

        public double Get(NarrativeNeed need)
        {
            switch (need)
            {
                case NarrativeNeed.Safety:
                    return Safety;
                case NarrativeNeed.Belonging:
                    return Belonging;
                case NarrativeNeed.DebtRelief:
                    return DebtRelief;
                case NarrativeNeed.Status:
                    return Status;
                case NarrativeNeed.Loyalty:
                    return Loyalty;
                case NarrativeNeed.Justice:
                    return Justice;
                case NarrativeNeed.Secrecy:
                    return Secrecy;
                case NarrativeNeed.Revenge:
                    return Revenge;
                case NarrativeNeed.Protection:
                    return Protection;
                case NarrativeNeed.MaterialShortage:
                    return MaterialShortage;
                case NarrativeNeed.Obligation:
                    return Obligation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(need), need, "Unknown narrative need.");
            }
        }

        public void Set(NarrativeNeed need, double value)
        {
            switch (need)
            {
                case NarrativeNeed.Safety:
                    Safety = value;
                    break;
                case NarrativeNeed.Belonging:
                    Belonging = value;
                    break;
                case NarrativeNeed.DebtRelief:
                    DebtRelief = value;
                    break;
                case NarrativeNeed.Status:
                    Status = value;
                    break;
                case NarrativeNeed.Loyalty:
                    Loyalty = value;
                    break;
                case NarrativeNeed.Justice:
                    Justice = value;
                    break;
                case NarrativeNeed.Secrecy:
                    Secrecy = value;
                    break;
                case NarrativeNeed.Revenge:
                    Revenge = value;
                    break;
                case NarrativeNeed.Protection:
                    Protection = value;
                    break;
                case NarrativeNeed.MaterialShortage:
                    MaterialShortage = value;
                    break;
                case NarrativeNeed.Obligation:
                    Obligation = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(need), need, "Unknown narrative need.");
            }
        }
    }
}
