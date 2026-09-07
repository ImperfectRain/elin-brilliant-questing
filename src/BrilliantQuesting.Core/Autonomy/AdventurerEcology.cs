using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Autonomy
{
    /// <summary>
    /// Adventuring parties taking up rescue matters the player left alone (BQ-096).
    ///
    /// A party is the existing <see cref="Organization"/> record with type
    /// <see cref="PartyType"/>. Its leader makes the actual attempt through the same
    /// <see cref="ActionAttempt"/> path as every other actor; the organization record says whose
    /// banner the attempt happened under. This deliberately adds no travel, pathfinding, party
    /// AI, or private rescue resolver.
    /// </summary>
    public sealed class AdventurerEcology
    {
        public const string PartyType = "adventuring_party";
        public const string RescueGoal = "rescue";
        public const string RescueAttemptTag = "adventurer_rescue_attempt";
        public const string PartyTag = "adventuring_party";

        public long Patience { get; set; } = 2;

        public int MostAttemptsPerPass { get; set; } = 1;

        public List<AdventurerEcologyTrace> LastPass { get; } = new List<AdventurerEcologyTrace>();

        public int Advance(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            GameTime now)
        {
            LastPass.Clear();
            if (world == null || vanilla == null || checks == null || registry == null)
            {
                return 0;
            }

            int acted = 0;
            EntityId player = vanilla.PlayerId;
            for (int i = 0; i < world.Threads.Count && acted < MostAttemptsPerPass; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread == null || !thread.IsLive || now.DaysSince(thread.CreatedAt) < Patience)
                {
                    continue;
                }

                if (PlayerHasActed(world, player, thread))
                {
                    continue;
                }

                AdventurerEcologyTrace trace = Consider(world, vanilla, checks, registry, thread, now);
                if (trace == null)
                {
                    continue;
                }

                LastPass.Add(trace);
                if (trace.Acted)
                {
                    acted++;
                }
            }

            return acted;
        }

        private static AdventurerEcologyTrace Consider(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry,
            NarrativeThread thread,
            GameTime now)
        {
            Fact matter = ActionBinding.StandingTrouble(world, thread);
            if (matter == null || matter.Predicate != FactPredicates.AtRisk)
            {
                return null;
            }

            AdventurerEcologyTrace trace = new AdventurerEcologyTrace(thread.Id, thread.ArchetypeId);
            List<Organization> parties = Parties(world);
            for (int i = 0; i < parties.Count; i++)
            {
                Organization party = parties[i];
                OrganizationGoal goal = RescueGoalFor(party, thread, matter);
                if (goal == null)
                {
                    continue;
                }

                trace.PartyId = party.Id;
                trace.Actor = party.LeaderId;
                trace.Target = matter.Subject;
                if (!CanAttempt(world, vanilla, party, goal, thread, matter, now, out string refusal))
                {
                    trace.Refusal = refusal;
                    return trace;
                }

                if (registry.Get("rescue") == null)
                {
                    trace.Refusal = "the rescue verb is not registered";
                    return trace;
                }

                if (!ActorContexts.TryBuildOffScreen(
                        world,
                        vanilla,
                        checks,
                        world.Rng,
                        party.LeaderId,
                        matter.Subject,
                        out ActionContext context,
                        out string contextRefusal))
                {
                    trace.Refusal = contextRefusal;
                    return trace;
                }

                context.Thread = thread;
                context.SubjectFact = matter.Id;
                context.Binding = new ActionBinding
                {
                    PropositionFact = matter.Id,
                    Purpose = "rescue " + world.Registry.NameOf(matter.Subject)
                };

                ActionIntent intent = new ActionIntent(
                    party.LeaderId,
                    "rescue",
                    matter.Subject,
                    party.Name + " heard of " + thread.ArchetypeId)
                {
                    Thread = thread,
                    SubjectFact = matter.Id
                };

                trace.Attempt = ActionAttempt.Run(registry, intent, context);
                trace.OrganizationEventId = RecordPartyAttempt(world, party, thread, matter, trace, now);
                trace.AttemptFactId = RecordAttemptClaim(world, party, thread, matter, trace, now);
                if (trace.Attempt?.Outcome != null && trace.Attempt.Outcome.Succeeded)
                {
                    goal.Satisfied = true;
                    trace.SettledFactId = RecordSettledClaim(world, party, thread, matter, trace, now);
                }

                party.LastActedAt = now;
                return trace;
            }

            trace.Refusal = "no adventuring party has heard enough and chosen this rescue";
            return trace;
        }

        private static bool CanAttempt(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization party,
            OrganizationGoal goal,
            NarrativeThread thread,
            Fact matter,
            GameTime now,
            out string refusal)
        {
            if (now.TotalDays <= party.LastActedAt.TotalDays)
            {
                refusal = "the party has already acted today";
                return false;
            }

            if (party.LeaderId.IsNone || !party.MemberIds.Contains(party.LeaderId))
            {
                refusal = "the party has no participating leader";
                return false;
            }

            NarrativeNpc leader = world.Registry.GetNpc(party.LeaderId);
            if (leader == null || !leader.IsCanonical || !leader.Alive || !vanilla.IsAlive(party.LeaderId))
            {
                refusal = "the party leader is not available as an actor";
                return false;
            }

            if (!PartyKnows(world, vanilla, party, thread))
            {
                refusal = "nobody in the party knows this matter";
                return false;
            }

            if (matter.Subject.IsNone || !vanilla.IsAlive(matter.Subject))
            {
                refusal = "the rescue target is not somebody the game can answer for";
                return false;
            }

            if (goal.Satisfied)
            {
                refusal = "the party already considers this rescue answered";
                return false;
            }

            refusal = string.Empty;
            return true;
        }

        private static EntityId RecordPartyAttempt(
            NarrativeWorldState world,
            Organization party,
            NarrativeThread thread,
            Fact matter,
            AdventurerEcologyTrace trace,
            GameTime now)
        {
            WorldEvent recorded = world.Record(
                WorldEventType.OrganizationActed,
                party.LeaderId,
                party.Id,
                now,
                trace.Attempt?.Outcome != null && trace.Attempt.Outcome.Succeeded ? 0.6 : 0.35,
                EntityId.None,
                related: new[] { thread.Id, matter.Id, matter.Subject },
                tags: new[] { PartyTag, RescueAttemptTag, trace.OutcomeName });
            return recorded.Id;
        }

        private static EntityId RecordAttemptClaim(
            NarrativeWorldState world,
            Organization party,
            NarrativeThread thread,
            Fact matter,
            AdventurerEcologyTrace trace,
            GameTime now)
        {
            Fact claim = new Fact(
                world.NewId("fact"),
                party.Id,
                FactPredicates.AttemptedRescue,
                matter.Subject,
                trace.OutcomeName,
                originEvent: trace.OrganizationEventId);

            world.Knowledge.AddFact(claim);
            TeachParty(world, party, claim.Id, now);
            thread.FactIds.Add(claim.Id);
            return claim.Id;
        }

        private static EntityId RecordSettledClaim(
            NarrativeWorldState world,
            Organization party,
            NarrativeThread thread,
            Fact matter,
            AdventurerEcologyTrace trace,
            GameTime now)
        {
            WorldEvent ending = LatestEnding(world, thread);
            if (ending == null)
            {
                return EntityId.None;
            }

            Fact settled = new Fact(
                world.NewId("fact"),
                party.Id,
                FactPredicates.Settled,
                matter.Subject,
                ThreadResolution.OutcomeOf(ending),
                originEvent: ending.Id);

            world.Knowledge.AddFact(settled);
            TeachParty(world, party, settled.Id, now);
            thread.FactIds.Add(settled.Id);
            trace.Resolved = true;
            trace.Resolution = settled.Value;
            return settled.Id;
        }

        private static WorldEvent LatestEnding(NarrativeWorldState world, NarrativeThread thread)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Type == WorldEventType.ThreadResolved && events[i].ThreadId == thread.Id)
                {
                    return events[i];
                }
            }

            return null;
        }

        private static void TeachParty(NarrativeWorldState world, Organization party, EntityId factId, GameTime now)
        {
            for (int i = 0; i < party.MemberIds.Count; i++)
            {
                EntityId member = party.MemberIds[i];
                if (!member.IsNone && world.Registry.IsActor(member))
                {
                    world.Knowledge.Teach(member, factId, KnowledgeSource.Participant, 1.0, now, canProve: false);
                }
            }
        }

        private static List<Organization> Parties(NarrativeWorldState world)
        {
            List<Organization> parties = new List<Organization>();
            foreach (Organization organization in world.Registry.Organizations.Values)
            {
                if (organization != null && string.Equals(organization.Type, PartyType, StringComparison.Ordinal))
                {
                    parties.Add(organization);
                }
            }

            parties.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            return parties;
        }

        private static OrganizationGoal RescueGoalFor(Organization party, NarrativeThread thread, Fact matter)
        {
            for (int i = 0; i < party.Goals.Count; i++)
            {
                OrganizationGoal goal = party.Goals[i];
                if (goal == null || goal.Satisfied || !Has(goal.Kind, RescueGoal))
                {
                    continue;
                }

                if (goal.Subject == thread.Id || goal.Subject == matter.Id || goal.Subject == matter.Subject)
                {
                    return goal;
                }
            }

            return null;
        }

        private static bool PartyKnows(NarrativeWorldState world, IVanillaState vanilla, Organization party, NarrativeThread thread)
        {
            for (int m = 0; m < party.MemberIds.Count; m++)
            {
                EntityId member = party.MemberIds[m];
                if (!vanilla.IsAlive(member))
                {
                    continue;
                }

                for (int f = 0; f < thread.FactIds.Count; f++)
                {
                    if (world.Knowledge.Knows(member, thread.FactIds[f]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PlayerHasActed(NarrativeWorldState world, EntityId player, NarrativeThread thread)
        {
            if (player.IsNone)
            {
                return false;
            }

            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Actor == player && thread.IsNamedBy(events[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Has(string text, string word)
        {
            return text != null && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class AdventurerEcologyTrace
    {
        public AdventurerEcologyTrace(EntityId threadId, string archetypeId)
        {
            ThreadId = threadId;
            ArchetypeId = archetypeId ?? string.Empty;
        }

        public EntityId ThreadId { get; }

        public string ArchetypeId { get; }

        public EntityId PartyId { get; set; }

        public EntityId Actor { get; set; }

        public EntityId Target { get; set; }

        public string ActionId => "rescue";

        public ActionAttempt Attempt { get; set; }

        public string Refusal { get; set; } = string.Empty;

        public EntityId OrganizationEventId { get; set; }

        public EntityId AttemptFactId { get; set; }

        public EntityId SettledFactId { get; set; }

        public bool Resolved { get; set; }

        public string Resolution { get; set; } = string.Empty;

        public bool Acted => Attempt != null;

        public string OutcomeName
        {
            get
            {
                if (Attempt?.Outcome == null)
                {
                    return "rescue_refused";
                }

                if (Attempt.Outcome.Succeeded)
                {
                    return "rescued";
                }

                return Attempt.Outcome.Outcome == CheckOutcome.CriticalFail
                    ? "rescue_disaster"
                    : "rescue_failed";
            }
        }

        public string Describe(NarrativeWorldState world)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("adventurer ecology: ").Append(ArchetypeId).Append('\n');
            if (!PartyId.IsNone)
            {
                sb.Append("  party: ").Append(world == null ? PartyId.Value : world.Registry.NameOf(PartyId)).Append('\n');
            }

            if (!Actor.IsNone)
            {
                sb.Append("  actor: ").Append(world == null ? Actor.Value : world.Registry.NameOf(Actor)).Append('\n');
            }

            if (!Target.IsNone)
            {
                sb.Append("  rescue target: ").Append(world == null ? Target.Value : world.Registry.NameOf(Target)).Append('\n');
            }

            if (!Acted)
            {
                sb.Append("  nobody went: ").Append(Refusal.Length == 0 ? "no reason recorded" : Refusal).Append('\n');
                return sb.ToString();
            }

            sb.Append("  ").Append(Attempt.Explain().Replace("\n", "\n  ")).Append('\n');
            sb.Append("  party claim: ").Append(OutcomeName).Append(" [").Append(AttemptFactId).Append("]\n");
            if (Resolved)
            {
                sb.Append("  settled claim: ").Append(Resolution).Append(" [").Append(SettledFactId).Append("]\n");
            }

            return sb.ToString();
        }
    }
}
