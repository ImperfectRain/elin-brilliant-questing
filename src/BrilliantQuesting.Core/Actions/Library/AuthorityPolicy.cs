using System;
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
        public static AuthorityRole RoleOf(NarrativeNpc npc)
        {
            if (npc == null || string.IsNullOrEmpty(npc.Occupation))
            {
                return AuthorityRole.None;
            }

            string occupation = npc.Occupation;
            if (Contains(occupation, "guard") || Contains(occupation, "sheriff") || Contains(occupation, "constable"))
            {
                return AuthorityRole.Guard;
            }

            if (Contains(occupation, "guild"))
            {
                return AuthorityRole.Guild;
            }

            if (Contains(occupation, "judge") || Contains(occupation, "court") || Contains(occupation, "magistrate"))
            {
                return AuthorityRole.Court;
            }

            return AuthorityRole.None;
        }

        public static AuthorityDecision Evaluate(ActionContext context)
        {
            AuthorityRole role = RoleOf(context.TargetNpc);
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

        private static bool Contains(string text, string value)
        {
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
