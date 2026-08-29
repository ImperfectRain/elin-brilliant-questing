using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    public enum AuthorityRole
    {
        None,
        Guard,
        Guild,
        Court
    }

    public enum AuthorityEvidenceLevel
    {
        None,
        Rumor,
        BelievedUnprovable,
        WitnessedAndProvable,
        PhysicalProof
    }

    public enum AuthorityResponse
    {
        CannotAct,
        RejectsRumor,
        Rebounds,
        OpensInquiry,
        Acts
    }

    public sealed class AuthorityDecision
    {
        public AuthorityDecision(AuthorityRole role, AuthorityEvidenceLevel evidence, AuthorityResponse response)
        {
            Role = role;
            Evidence = evidence;
            Response = response;
        }

        public AuthorityRole Role { get; }

        public AuthorityEvidenceLevel Evidence { get; }

        public AuthorityResponse Response { get; }
    }

    /// <summary>What an authority will do with an accusation at each proof level.</summary>
    public static class AuthorityPolicy
    {
        /// <summary>Role names anybody - adapter, situation, organization - can grant.</summary>
        public const string GuardRole = "guard";
        public const string GuildRole = "guild";
        public const string CourtRole = "court";

        /// <summary>
        /// What standing this character holds.
        ///
        /// Read from <see cref="NarrativeNpc.Roles"/>, not from their occupation. Authority lived
        /// in the occupation string briefly and that conflated two different things: a brewer can
        /// be a guild officer, and a guard who is dismissed still has a job. It also made the
        /// answer impossible to withdraw, because there was nowhere to say "no longer" without
        /// erasing what the person does for a living.
        /// </summary>
        public static AuthorityRole RoleOf(NarrativeNpc npc)
        {
            if (npc == null || npc.Roles.Count == 0)
            {
                return AuthorityRole.None;
            }

            if (npc.Roles.Contains(GuardRole))
            {
                return AuthorityRole.Guard;
            }

            if (npc.Roles.Contains(GuildRole))
            {
                return AuthorityRole.Guild;
            }

            if (npc.Roles.Contains(CourtRole))
            {
                return AuthorityRole.Court;
            }

            return AuthorityRole.None;
        }

        /// <summary>
        /// What standing this character holds *right now*.
        ///
        /// The same table as the overload above, with one extra question: somebody whose office is
        /// interrupted holds no office. That is the whole of what a Grade A absence does to an
        /// authority - the guard is still standing there and can still be talked to, and what has
        /// stopped is their willingness to take a statement - and it lives here rather than in the
        /// report verb so that every route through an authority closes together.
        /// </summary>
        public static AuthorityRole RoleOf(ActionContext context, EntityId who)
        {
            return ActionSupport.OnDuty(context, who)
                ? RoleOf(context.World.Registry.GetNpc(who))
                : AuthorityRole.None;
        }

        /// <summary>Every role this policy recognises, so a refresh knows what it may withdraw.</summary>
        public static IReadOnlyList<string> AuthorityRoles { get; } =
            new List<string> { GuardRole, GuildRole, CourtRole };

        public static AuthorityDecision Evaluate(ActionContext context)
        {
            AuthorityRole role = RoleOf(context, context.Target);
            if (role == AuthorityRole.None
                || context.SubjectFact.IsNone
                || !context.World.Knowledge.TryGetBelief(context.Actor, context.SubjectFact, out KnowledgeRecord belief))
            {
                return new AuthorityDecision(role, AuthorityEvidenceLevel.None, AuthorityResponse.CannotAct);
            }

            AuthorityEvidenceLevel evidence = EvidenceLevel(belief);
            AuthorityResponse response = ResponseFor(role, evidence);
            return new AuthorityDecision(role, evidence, response);
        }

        private static AuthorityEvidenceLevel EvidenceLevel(KnowledgeRecord belief)
        {
            if (belief.CanProve)
            {
                for (int i = 0; i < belief.Proofs.Count; i++)
                {
                    if (belief.Proofs[i].Kind == ProofKind.PhysicalEvidence)
                    {
                        return AuthorityEvidenceLevel.PhysicalProof;
                    }
                }

                return AuthorityEvidenceLevel.WitnessedAndProvable;
            }

            return belief.Confidence >= 0.5
                ? AuthorityEvidenceLevel.BelievedUnprovable
                : AuthorityEvidenceLevel.Rumor;
        }

        private static AuthorityResponse ResponseFor(AuthorityRole role, AuthorityEvidenceLevel evidence)
        {
            switch (role)
            {
                case AuthorityRole.Guard:
                    return evidence == AuthorityEvidenceLevel.PhysicalProof
                           || evidence == AuthorityEvidenceLevel.WitnessedAndProvable
                        ? AuthorityResponse.Acts
                        : AuthorityResponse.Rebounds;

                case AuthorityRole.Guild:
                    if (evidence == AuthorityEvidenceLevel.PhysicalProof
                        || evidence == AuthorityEvidenceLevel.WitnessedAndProvable)
                    {
                        return AuthorityResponse.Acts;
                    }

                    return evidence == AuthorityEvidenceLevel.BelievedUnprovable
                        ? AuthorityResponse.OpensInquiry
                        : AuthorityResponse.RejectsRumor;

                case AuthorityRole.Court:
                    if (evidence == AuthorityEvidenceLevel.PhysicalProof)
                    {
                        return AuthorityResponse.Acts;
                    }

                    return evidence == AuthorityEvidenceLevel.WitnessedAndProvable
                        ? AuthorityResponse.OpensInquiry
                        : AuthorityResponse.Rebounds;

                default:
                    return AuthorityResponse.CannotAct;
            }
        }

    }
}
