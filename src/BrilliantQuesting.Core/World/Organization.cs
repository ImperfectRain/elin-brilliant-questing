using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// A generated group - a smuggler crew, a farming family, a merchant association. These
    /// overlay vanilla guilds and factions rather than replacing them: the Thieves Guild is still
    /// the Thieves Guild, but the four people who actually fence goods in this town are ours.
    /// </summary>
    public sealed class Organization
    {
        public Organization(EntityId id, string name, string type)
        {
            Id = id;
            Name = name;
            Type = type;
            Goals = new List<OrganizationGoal>();
            MemberIds = new List<EntityId>();
            SiteIds = new List<EntityId>();
        }

        public EntityId Id { get; }

        public string Name { get; set; }

        /// <summary>Ontology term: "criminal_crew", "merchant_association", "family", "cult".</summary>
        public string Type { get; }

        public EntityId LeaderId { get; set; }

        public List<OrganizationGoal> Goals { get; }

        public List<EntityId> MemberIds { get; }

        public List<EntityId> SiteIds { get; }

        /// <summary>Coarse band rather than a modelled treasury; see the economy scope limit.</summary>
        public int Wealth { get; set; }

        /// <summary>0..100. How openly it can operate, and whether authorities will act for it.</summary>
        public int Legitimacy { get; set; } = 50;

        /// <summary>0..100. How readily it answers a problem with violence.</summary>
        public int Aggression { get; set; } = 30;

        /// <summary>Last day this organization took an autonomous off-screen action.</summary>
        public GameTime LastActedAt { get; set; }

        public override string ToString() => Name + " [" + Type + ", " + MemberIds.Count + " members]";
    }

    /// <summary>Something an organization is trying to accomplish with its own resources.</summary>
    public sealed class OrganizationGoal
    {
        public OrganizationGoal(string kind, EntityId subject, int weight)
        {
            Kind = kind;
            Subject = subject;
            Weight = weight;
        }

        public string Kind { get; }

        public EntityId Subject { get; }

        public int Weight { get; set; }

        public int Progress { get; set; }

        public bool Satisfied { get; set; }

        public override string ToString() => Kind + "(" + Subject + ") w" + Weight + " p" + Progress
            + (Satisfied ? " [done]" : string.Empty);
    }
}
