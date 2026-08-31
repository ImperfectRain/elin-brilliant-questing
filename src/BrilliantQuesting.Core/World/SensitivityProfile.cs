using System;

namespace BrilliantQuesting.World
{
    public enum SensitivityTopic
    {
        PublicEmbarrassment,
        UnpaidDebt,
        FamilyThreat,
        Animals,
        Status,
        Theft,
        Violence,
        Dishonesty
    }

    /// <summary>
    /// Durable triggers that make an actor react more strongly to some facts than another actor
    /// with the same personality would. These are interpretation/action biases, not motives by
    /// themselves; BQ-061 owns values and needs.
    /// </summary>
    public sealed class SensitivityProfile
    {
        public double PublicEmbarrassment { get; set; } = 0.5;

        public double UnpaidDebt { get; set; } = 0.5;

        public double FamilyThreat { get; set; } = 0.5;

        public double Animals { get; set; } = 0.5;

        public double Status { get; set; } = 0.5;

        public double Theft { get; set; } = 0.5;

        public double Violence { get; set; } = 0.5;

        public double Dishonesty { get; set; } = 0.5;

        public double Get(SensitivityTopic topic)
        {
            switch (topic)
            {
                case SensitivityTopic.PublicEmbarrassment:
                    return PublicEmbarrassment;
                case SensitivityTopic.UnpaidDebt:
                    return UnpaidDebt;
                case SensitivityTopic.FamilyThreat:
                    return FamilyThreat;
                case SensitivityTopic.Animals:
                    return Animals;
                case SensitivityTopic.Status:
                    return Status;
                case SensitivityTopic.Theft:
                    return Theft;
                case SensitivityTopic.Violence:
                    return Violence;
                case SensitivityTopic.Dishonesty:
                    return Dishonesty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(topic), topic, "Unknown sensitivity topic.");
            }
        }

        public void Set(SensitivityTopic topic, double value)
        {
            switch (topic)
            {
                case SensitivityTopic.PublicEmbarrassment:
                    PublicEmbarrassment = value;
                    break;
                case SensitivityTopic.UnpaidDebt:
                    UnpaidDebt = value;
                    break;
                case SensitivityTopic.FamilyThreat:
                    FamilyThreat = value;
                    break;
                case SensitivityTopic.Animals:
                    Animals = value;
                    break;
                case SensitivityTopic.Status:
                    Status = value;
                    break;
                case SensitivityTopic.Theft:
                    Theft = value;
                    break;
                case SensitivityTopic.Violence:
                    Violence = value;
                    break;
                case SensitivityTopic.Dishonesty:
                    Dishonesty = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(topic), topic, "Unknown sensitivity topic.");
            }
        }
    }
}
