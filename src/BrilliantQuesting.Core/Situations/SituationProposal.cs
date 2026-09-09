using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Situations
{
    /// <summary>Intended casting, not an actor allocation or proof of native existence.</summary>
    public sealed class SituationActorRequirement
    {
        internal SituationActorRequirement(string role, EntityId existingActor, string creationKey)
        {
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("A role is required.", nameof(role));
            Role = role;
            ExistingActor = existingActor;
            CreationKey = creationKey;
        }

        public string Role { get; }
        public EntityId ExistingActor { get; }
        /// <summary>Proposal-local identity shared across roles; never a reserved EntityId.</summary>
        public string CreationKey { get; }
        public bool RequiresCreation => CreationKey != null;
    }

    /// <summary>
    /// Transient, owner-produced alternative. Key must be unique within a selection and stable on
    /// replay. Candidate retains the owner's bindings, pressures and causes; no commit callback.
    /// </summary>
    public sealed class SituationProposal
    {
        public SituationProposal(string key, SituationCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A stable key is required.", nameof(key));
            Key = key;
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        }

        public string Key { get; }
        public SituationCandidate Candidate { get; }

        /// <summary>Inspector-only requirements; does not assert they can be fulfilled.</summary>
        public string Explain()
        {
            var text = new StringBuilder(Key).Append("; archetype=").Append(Candidate.ArchetypeId)
                .Append("; quality=").Append(Candidate.Score.ToString(CultureInfo.InvariantCulture));
            foreach (SituationActorRequirement actor in Candidate.ActorRequirements)
                text.Append("; ").Append(actor.Role).Append(actor.RequiresCreation ? " requires new actor " : " reuses actor ")
                    .Append(actor.RequiresCreation ? actor.CreationKey : actor.ExistingActor.Value);
            return text.ToString();
        }
    }

    /// <summary>BQ-152: quality only, then ordinal key. No creation costs, world access or mutation.</summary>
    public static class SituationProposalSelection
    {
        public static IReadOnlyList<SituationProposal> Rank(IEnumerable<SituationProposal> proposals)
        {
            if (proposals == null) throw new ArgumentNullException(nameof(proposals));
            var result = new List<SituationProposal>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (SituationProposal proposal in proposals)
            {
                if (proposal == null) throw new ArgumentException("Null proposal.", nameof(proposals));
                if (!keys.Add(proposal.Key)) throw new ArgumentException("Duplicate proposal key: " + proposal.Key, nameof(proposals));
                result.Add(proposal);
            }
            result.Sort((a, b) =>
            {
                int quality = b.Candidate.Score.CompareTo(a.Candidate.Score);
                return quality != 0 ? quality : string.CompareOrdinal(a.Key, b.Key);
            });
            return result.AsReadOnly();
        }
    }
}
