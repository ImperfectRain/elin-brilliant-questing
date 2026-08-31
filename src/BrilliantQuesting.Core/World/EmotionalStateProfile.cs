using System;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    public enum EmotionalState
    {
        Anger,
        Fear,
        Shame,
        Grief,
        Relief,
        Suspicion,
        Affection,
        Stress
    }

    /// <summary>
    /// Transient affect that biases decisions and disclosure without becoming personality.
    /// </summary>
    public sealed class EmotionalStateProfile
    {
        public const long FullDecayMinutes = GameTime.MinutesPerHour * 12;

        public double Anger { get; set; }

        public double Fear { get; set; }

        public double Shame { get; set; }

        public double Grief { get; set; }

        public double Relief { get; set; }

        public double Suspicion { get; set; }

        public double Affection { get; set; }

        public double Stress { get; set; }

        public GameTime LastUpdatedAt { get; set; } = GameTime.Zero;

        public void Affect(EmotionalState emotion, double intensity, GameTime now)
        {
            DecayTo(now);
            Set(emotion, Math.Max(Get(emotion), Clamp01(intensity)));
            LastUpdatedAt = now;
        }

        public double Get(EmotionalState emotion, GameTime now)
        {
            long elapsed = Math.Max(0, now.TotalMinutes - LastUpdatedAt.TotalMinutes);
            if (elapsed >= FullDecayMinutes)
            {
                return 0.0;
            }

            double remaining = 1.0 - (elapsed / (double)FullDecayMinutes);
            return Get(emotion) * remaining;
        }

        public double Get(EmotionalState emotion)
        {
            switch (emotion)
            {
                case EmotionalState.Anger:
                    return Anger;
                case EmotionalState.Fear:
                    return Fear;
                case EmotionalState.Shame:
                    return Shame;
                case EmotionalState.Grief:
                    return Grief;
                case EmotionalState.Relief:
                    return Relief;
                case EmotionalState.Suspicion:
                    return Suspicion;
                case EmotionalState.Affection:
                    return Affection;
                case EmotionalState.Stress:
                    return Stress;
                default:
                    throw new ArgumentOutOfRangeException(nameof(emotion), emotion, "Unknown emotional state.");
            }
        }

        public void Set(EmotionalState emotion, double value)
        {
            double clamped = Clamp01(value);
            switch (emotion)
            {
                case EmotionalState.Anger:
                    Anger = clamped;
                    break;
                case EmotionalState.Fear:
                    Fear = clamped;
                    break;
                case EmotionalState.Shame:
                    Shame = clamped;
                    break;
                case EmotionalState.Grief:
                    Grief = clamped;
                    break;
                case EmotionalState.Relief:
                    Relief = clamped;
                    break;
                case EmotionalState.Suspicion:
                    Suspicion = clamped;
                    break;
                case EmotionalState.Affection:
                    Affection = clamped;
                    break;
                case EmotionalState.Stress:
                    Stress = clamped;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(emotion), emotion, "Unknown emotional state.");
            }
        }

        public void DecayTo(GameTime now)
        {
            long elapsed = Math.Max(0, now.TotalMinutes - LastUpdatedAt.TotalMinutes);
            if (elapsed == 0)
            {
                return;
            }

            double remaining = elapsed >= FullDecayMinutes
                ? 0.0
                : 1.0 - (elapsed / (double)FullDecayMinutes);

            Anger *= remaining;
            Fear *= remaining;
            Shame *= remaining;
            Grief *= remaining;
            Relief *= remaining;
            Suspicion *= remaining;
            Affection *= remaining;
            Stress *= remaining;
            LastUpdatedAt = now;
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }
}
