using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Consequences
{
    public enum ViolenceJudgment
    {
        None,
        Murder,
        SelfDefense,
        LawfulBounty
    }

    /// <summary>
    /// Reads the social meaning of a death without changing the death itself.
    ///
    /// BQ-014 records what Elin says happened, and BQ-016 deliberately withholds legal standing
    /// from observed violence because a raw hit does not say why it happened. This is the missing
    /// judgment layer: a witness with the right stake or office can classify the same `Killed`
    /// event without rewriting the event, the fact, or anybody's memory of seeing it.
    /// </summary>
    public static class ViolenceRecognition
    {
        public static ViolenceJudgment Recognize(NarrativeWorldState world, WorldEvent worldEvent)
        {
            if (world == null
                || worldEvent == null
                || worldEvent.Type != WorldEventType.Killed
                || !HasTag(worldEvent, EventTags.Observed)
                || worldEvent.Actor.IsNone
                || worldEvent.Target.IsNone
                || worldEvent.Witnesses.Count == 0)
            {
                return ViolenceJudgment.None;
            }

            if (WitnessSawPriorAttack(world, worldEvent))
            {
                return ViolenceJudgment.SelfDefense;
            }

            if (FightersWitnessReadsBounty(world, worldEvent))
            {
                return ViolenceJudgment.LawfulBounty;
            }

            if (AuthorityWitnessed(world, worldEvent))
            {
                return ViolenceJudgment.Murder;
            }

            return ViolenceJudgment.None;
        }

        private static bool WitnessSawPriorAttack(NarrativeWorldState world, WorldEvent killing)
        {
            foreach (WorldEvent earlier in world.Ledger.Events)
            {
                if (earlier == killing)
                {
                    break;
                }

                if (earlier.Type != WorldEventType.Attacked
                    || earlier.Actor != killing.Target
                    || earlier.Target != killing.Actor
                    || (!earlier.Zone.IsNone && !killing.Zone.IsNone && earlier.Zone != killing.Zone))
                {
                    continue;
                }

                for (int i = 0; i < killing.Witnesses.Count; i++)
                {
                    if (Contains(earlier.Witnesses, killing.Witnesses[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FightersWitnessReadsBounty(NarrativeWorldState world, WorldEvent killing)
        {
            for (int w = 0; w < killing.Witnesses.Count; w++)
            {
                NarrativeNpc witness = world.Registry.GetNpc(killing.Witnesses[w]);
                if (!GuildNetworks.BelongsTo(witness, GuildId.Fighters))
                {
                    continue;
                }

                for (int r = 0; r < killing.Related.Count; r++)
                {
                    Fact fact = world.Knowledge.GetFact(killing.Related[r]);
                    if (fact != null
                        && world.Knowledge.Knows(witness.Id, fact.Id)
                        && GuildNetworks.Reads(world, GuildId.Fighters, fact) == GuildFraming.Bounty)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool AuthorityWitnessed(NarrativeWorldState world, WorldEvent killing)
        {
            for (int i = 0; i < killing.Witnesses.Count; i++)
            {
                NarrativeNpc witness = world.Registry.GetNpc(killing.Witnesses[i]);
                if (witness != null && witness.Roles.Contains(AuthorityPolicy.GuardRole))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<EntityId> ids, EntityId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTag(WorldEvent worldEvent, string tag)
        {
            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                if (worldEvent.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
