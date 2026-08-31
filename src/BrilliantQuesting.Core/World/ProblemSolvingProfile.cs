using System;

namespace BrilliantQuesting.World
{
    public enum ProblemSolvingStyle
    {
        Confront,
        Avoid,
        AskAuthority,
        AskFriends,
        PaySomeone,
        DoItSelf,
        Manipulate,
        UseViolence,
        SeekGuild,
        SeekReligiousHelp,
        Wait,
        Flee,
        Publicize,
        Conceal
    }

    /// <summary>
    /// Durable preferences for how an actor tends to turn a problem into action. These are
    /// personality expression, not capability checks; vanilla skills still decide whether the
    /// chosen approach can work.
    /// </summary>
    public sealed class ProblemSolvingProfile
    {
        public double Confront { get; set; } = 0.5;

        public double Avoid { get; set; } = 0.5;

        public double AskAuthority { get; set; } = 0.5;

        public double AskFriends { get; set; } = 0.5;

        public double PaySomeone { get; set; } = 0.5;

        public double DoItSelf { get; set; } = 0.5;

        public double Manipulate { get; set; } = 0.5;

        public double UseViolence { get; set; } = 0.5;

        public double SeekGuild { get; set; } = 0.5;

        public double SeekReligiousHelp { get; set; } = 0.5;

        public double Wait { get; set; } = 0.5;

        public double Flee { get; set; } = 0.5;

        public double Publicize { get; set; } = 0.5;

        public double Conceal { get; set; } = 0.5;

        public double Get(ProblemSolvingStyle style)
        {
            switch (style)
            {
                case ProblemSolvingStyle.Confront:
                    return Confront;
                case ProblemSolvingStyle.Avoid:
                    return Avoid;
                case ProblemSolvingStyle.AskAuthority:
                    return AskAuthority;
                case ProblemSolvingStyle.AskFriends:
                    return AskFriends;
                case ProblemSolvingStyle.PaySomeone:
                    return PaySomeone;
                case ProblemSolvingStyle.DoItSelf:
                    return DoItSelf;
                case ProblemSolvingStyle.Manipulate:
                    return Manipulate;
                case ProblemSolvingStyle.UseViolence:
                    return UseViolence;
                case ProblemSolvingStyle.SeekGuild:
                    return SeekGuild;
                case ProblemSolvingStyle.SeekReligiousHelp:
                    return SeekReligiousHelp;
                case ProblemSolvingStyle.Wait:
                    return Wait;
                case ProblemSolvingStyle.Flee:
                    return Flee;
                case ProblemSolvingStyle.Publicize:
                    return Publicize;
                case ProblemSolvingStyle.Conceal:
                    return Conceal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown problem-solving style.");
            }
        }

        public void Set(ProblemSolvingStyle style, double value)
        {
            switch (style)
            {
                case ProblemSolvingStyle.Confront:
                    Confront = value;
                    break;
                case ProblemSolvingStyle.Avoid:
                    Avoid = value;
                    break;
                case ProblemSolvingStyle.AskAuthority:
                    AskAuthority = value;
                    break;
                case ProblemSolvingStyle.AskFriends:
                    AskFriends = value;
                    break;
                case ProblemSolvingStyle.PaySomeone:
                    PaySomeone = value;
                    break;
                case ProblemSolvingStyle.DoItSelf:
                    DoItSelf = value;
                    break;
                case ProblemSolvingStyle.Manipulate:
                    Manipulate = value;
                    break;
                case ProblemSolvingStyle.UseViolence:
                    UseViolence = value;
                    break;
                case ProblemSolvingStyle.SeekGuild:
                    SeekGuild = value;
                    break;
                case ProblemSolvingStyle.SeekReligiousHelp:
                    SeekReligiousHelp = value;
                    break;
                case ProblemSolvingStyle.Wait:
                    Wait = value;
                    break;
                case ProblemSolvingStyle.Flee:
                    Flee = value;
                    break;
                case ProblemSolvingStyle.Publicize:
                    Publicize = value;
                    break;
                case ProblemSolvingStyle.Conceal:
                    Conceal = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown problem-solving style.");
            }
        }
    }
}
