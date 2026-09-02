using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Rewards
{
    public enum ResolutionRewardKind
    {
        Access,
        Relationship,
        Standing,
        Information,
        Property,
        FavorOwed
    }

    public sealed class ResolutionRewardFinding
    {
        public ResolutionRewardFinding(EntityId threadId, EntityId itemId, string reason)
        {
            ThreadId = threadId;
            ItemId = itemId;
            Reason = reason ?? string.Empty;
        }

        public EntityId ThreadId { get; }

        public EntityId ItemId { get; }

        public string Reason { get; }
    }

    public sealed class ResolutionRewardReport
    {
        internal ResolutionRewardReport(
            HashSet<ResolutionRewardKind> kinds,
            List<ResolutionRewardFinding> forbiddenItemPayouts)
        {
            Kinds = kinds;
            ForbiddenItemPayouts = forbiddenItemPayouts;
        }

        public IReadOnlyCollection<ResolutionRewardKind> Kinds { get; }

        public IReadOnlyList<ResolutionRewardFinding> ForbiddenItemPayouts { get; }

        public bool Passed => ForbiddenItemPayouts.Count == 0;
    }

    /// <summary>
    /// BQ-112's reward vocabulary as a ledger audit. Rewards are consequences that change what
    /// the player can do next; a loose item is allowed only when it was already real property,
    /// evidence, cargo or an object the situation put in the world.
    /// </summary>
    public sealed class ResolutionRewardAudit
    {
        private readonly NarrativeWorldState _world;
        private readonly EntityId _player;

        public ResolutionRewardAudit(NarrativeWorldState world, EntityId player)
        {
            _world = world;
            _player = player;
        }

        public ResolutionRewardReport AuditResolvedThreads()
        {
            HashSet<ResolutionRewardKind> kinds = new HashSet<ResolutionRewardKind>();
            List<ResolutionRewardFinding> forbidden = new List<ResolutionRewardFinding>();
            HashSet<EntityId> provenance = new HashSet<EntityId>();

            IReadOnlyList<WorldEvent> events = _world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                LearnProvenance(worldEvent, provenance);
                AddKind(worldEvent, kinds);

                if (worldEvent.Type == WorldEventType.ThreadResolved && !worldEvent.ThreadId.IsNone)
                {
                    AuditThread(events, i, worldEvent.ThreadId, provenance, forbidden);
                }
            }

            AddKnowledgeKinds(kinds);
            AddStandingKinds(kinds);
            return new ResolutionRewardReport(kinds, forbidden);
        }

        private void AuditThread(
            IReadOnlyList<WorldEvent> events,
            int resolutionIndex,
            EntityId threadId,
            HashSet<EntityId> provenance,
            List<ResolutionRewardFinding> forbidden)
        {
            for (int i = 0; i <= resolutionIndex; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.ThreadId != threadId || worldEvent.Type != WorldEventType.ItemGiven)
                {
                    continue;
                }

                for (int item = 0; item < worldEvent.Evidence.Count; item++)
                {
                    EntityId itemId = worldEvent.Evidence[item];
                    if (ItemMovedToPlayer(worldEvent) && !provenance.Contains(itemId))
                    {
                        forbidden.Add(new ResolutionRewardFinding(
                            threadId,
                            itemId,
                            "resolved thread gave the player an item with no prior provenance"));
                    }
                }
            }
        }

        private bool ItemMovedToPlayer(WorldEvent worldEvent)
        {
            return worldEvent.Target == _player || (worldEvent.Actor == _player && worldEvent.Target == _player);
        }

        private static void LearnProvenance(WorldEvent worldEvent, HashSet<EntityId> provenance)
        {
            if (worldEvent.Type == WorldEventType.ItemGiven)
            {
                return;
            }

            AddAll(worldEvent.Evidence, provenance);
            AddAll(worldEvent.Related, provenance);
        }

        private static void AddKind(WorldEvent worldEvent, HashSet<ResolutionRewardKind> kinds)
        {
            switch (worldEvent.Type)
            {
                case WorldEventType.SiteDiscovered:
                case WorldEventType.SiteCleared:
                    kinds.Add(ResolutionRewardKind.Access);
                    break;
                case WorldEventType.Helped:
                case WorldEventType.TakenIn:
                case WorldEventType.PromiseMade:
                case WorldEventType.Recruited:
                    kinds.Add(ResolutionRewardKind.Relationship);
                    break;
                case WorldEventType.CompetitionWon:
                case WorldEventType.OrganizationJoined:
                case WorldEventType.OrganizationActed:
                case WorldEventType.BusinessStateChanged:
                    kinds.Add(ResolutionRewardKind.Standing);
                    break;
                case WorldEventType.SecretLearned:
                case WorldEventType.SecretRevealed:
                case WorldEventType.InquiryOpened:
                case WorldEventType.CrimeReported:
                    kinds.Add(ResolutionRewardKind.Information);
                    break;
                case WorldEventType.ItemReturned:
                case WorldEventType.DebtPaid:
                    kinds.Add(ResolutionRewardKind.Property);
                    break;
                case WorldEventType.FavorOwed:
                    kinds.Add(ResolutionRewardKind.FavorOwed);
                    break;
            }
        }

        private void AddKnowledgeKinds(HashSet<ResolutionRewardKind> kinds)
        {
            foreach (Fact fact in _world.Knowledge.Facts.Values)
            {
                if (fact == null || fact.Truth != TruthState.True)
                {
                    continue;
                }

                if (fact.Predicate == FactPredicates.ShelteredBy)
                {
                    kinds.Add(ResolutionRewardKind.Relationship);
                }
                else if (fact.Predicate == FactPredicates.WonCompetition)
                {
                    kinds.Add(ResolutionRewardKind.Standing);
                }
                else if (fact.Predicate == FactPredicates.Possesses && fact.Subject == _player)
                {
                    kinds.Add(ResolutionRewardKind.Property);
                }
            }
        }

        /// <summary>
        /// The rewards that are records rather than events.
        ///
        /// `FavorOwed` was reachable here only through a `FavorOwed` event, and nothing in the
        /// mod has ever recorded one - BQ-113 mints the debt straight into the obligation ledger,
        /// which is the authoritative record and the thing `call_favor` and the standing sheet
        /// both read. So this audit reported the vocabulary as narrower than play actually
        /// delivers, and it under-reported precisely the reward `engagement §3` calls the
        /// strongest in it. Read from the ledger for the same reason
        /// <see cref="AddKnowledgeKinds"/> reads the knowledge graph: what was granted is a state
        /// the save carries, not only an event that went past.
        ///
        /// Status is deliberately not filtered. A favour that has been called in was still a
        /// favour the world granted, and this audit answers what the reward vocabulary contains -
        /// what the player is still holding is the standing sheet's question, not this one's.
        /// </summary>
        private void AddStandingKinds(HashSet<ResolutionRewardKind> kinds)
        {
            IReadOnlyList<SocialObligation> obligations = _world.Obligations.Records;
            for (int i = 0; i < obligations.Count; i++)
            {
                if (obligations[i].Kind == SocialObligationKind.Favor && obligations[i].Creditor == _player)
                {
                    kinds.Add(ResolutionRewardKind.FavorOwed);
                    return;
                }
            }
        }

        private static void AddAll(IReadOnlyList<EntityId> ids, HashSet<EntityId> target)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (!ids[i].IsNone && ids[i].Kind == "item")
                {
                    target.Add(ids[i]);
                }
            }
        }
    }
}
