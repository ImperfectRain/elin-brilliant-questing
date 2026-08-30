using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// Reads a local settlement as affordances for situations: who is present, what they carry,
    /// what they can plausibly do, and who has enough to become a target.
    /// </summary>
    public sealed class LocalAffordanceProfile
    {
        private readonly List<EntityId> _residents = new List<EntityId>();
        private readonly List<string> _features = new List<string>();

        private LocalAffordanceProfile(EntityId zoneId)
        {
            ZoneId = zoneId;
        }

        public EntityId ZoneId { get; }

        public IReadOnlyList<EntityId> Residents => _residents;

        public IReadOnlyList<string> Features => _features;

        public static LocalAffordanceProfile Read(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            LocalAffordanceProfile profile = new LocalAffordanceProfile(zoneId);
            if (world == null || vanilla == null || zoneId.IsNone)
            {
                return profile;
            }

            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zoneId);
            for (int i = 0; i < present.Count; i++)
            {
                EntityId actor = present[i];
                if (actor.IsNone || actor == vanilla.PlayerId || !vanilla.IsAlive(actor))
                {
                    continue;
                }

                NarrativeNpc npc = world.Registry.GetNpc(actor);
                if (npc == null || vanilla.GetActorClass(actor) == NarrativeActorClass.Unknown)
                {
                    continue;
                }

                profile._residents.Add(actor);
            }

            int valuables = 0;
            int hardship = 0;
            int illicitMeans = 0;
            for (int i = 0; i < profile._residents.Count; i++)
            {
                EntityId resident = profile._residents[i];
                if (vanilla.GetMoney(resident) < 80)
                {
                    hardship++;
                }

                if (vanilla.GetSkill(resident, VanillaSkill.Pickpocket) >= 5
                    || vanilla.GetSkill(resident, VanillaSkill.Stealth) >= 5)
                {
                    illicitMeans++;
                }

                IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(resident);
                for (int item = 0; item < inventory.Count; item++)
                {
                    if (inventory[item].Value >= 250)
                    {
                        valuables++;
                    }
                }
            }

            profile._features.Add("locals present: " + profile._residents.Count);
            profile._features.Add("valuable carried objects: " + valuables);
            profile._features.Add("cash-poor locals: " + hardship);
            profile._features.Add("locals with theft means: " + illicitMeans);
            return profile;
        }
    }

    public sealed class SituationCandidate
    {
        private readonly List<string> _causes = new List<string>();

        internal SituationCandidate(
            string archetypeId,
            int score,
            EntityId actorId,
            EntityId targetId,
            EntityId witnessId,
            ItemDescriptor item)
        {
            ArchetypeId = archetypeId;
            Score = score;
            ActorId = actorId;
            TargetId = targetId;
            WitnessId = witnessId;
            Item = item;
        }

        public string ArchetypeId { get; }

        public int Score { get; }

        public EntityId ActorId { get; }

        public EntityId TargetId { get; }

        public EntityId WitnessId { get; }

        public ItemDescriptor Item { get; }

        public IReadOnlyList<string> Causes => _causes;

        internal void AddCause(string cause)
        {
            if (!string.IsNullOrEmpty(cause))
            {
                _causes.Add(cause);
            }
        }
    }

    public sealed class SettlementSituationPlan
    {
        private readonly List<SituationCandidate> _candidates;

        internal SettlementSituationPlan(LocalAffordanceProfile profile, List<SituationCandidate> candidates)
        {
            Profile = profile;
            _candidates = candidates;
        }

        public LocalAffordanceProfile Profile { get; }

        public IReadOnlyList<SituationCandidate> Candidates => _candidates;

        public SituationCandidate BestCandidate => _candidates.Count == 0 ? null : _candidates[0];
    }

    public sealed class SettlementSituationGenerator
    {
        private const int MinimumTheftScore = 70;

        public SettlementSituationPlan Evaluate(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            LocalAffordanceProfile profile = LocalAffordanceProfile.Read(world, vanilla, zoneId);
            List<SituationCandidate> candidates = new List<SituationCandidate>();

            for (int actorIndex = 0; actorIndex < profile.Residents.Count; actorIndex++)
            {
                EntityId actor = profile.Residents[actorIndex];
                for (int targetIndex = 0; targetIndex < profile.Residents.Count; targetIndex++)
                {
                    EntityId target = profile.Residents[targetIndex];
                    if (actor == target)
                    {
                        continue;
                    }

                    ItemDescriptor item = BestTargetItem(vanilla.GetInventory(target));
                    if (item == null)
                    {
                        continue;
                    }

                    EntityId witness = PickWitness(profile, actor, target);
                    if (witness.IsNone)
                    {
                        continue;
                    }

                    SituationCandidate candidate = TheftCandidate(world, vanilla, actor, target, witness, item);
                    if (candidate.Score >= MinimumTheftScore)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            candidates.Sort(CompareCandidates);
            return new SettlementSituationPlan(profile, candidates);
        }

        public PettyTheftSituation TryGenerate(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId, GameTime now)
        {
            SettlementSituationPlan plan = Evaluate(world, vanilla, zoneId);
            for (int i = 0; i < plan.Candidates.Count; i++)
            {
                SituationCandidate candidate = plan.Candidates[i];
                if (vanilla.TryTransferItem(candidate.Item.Id, candidate.TargetId, candidate.ActorId))
                {
                    return PettyTheftSituation.FromLocalAffordance(world, candidate, zoneId, now);
                }
            }

            return null;
        }

        private static SituationCandidate TheftCandidate(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId actor,
            EntityId target,
            EntityId witness,
            ItemDescriptor item)
        {
            NarrativeNpc actorNpc = world.Registry.GetNpc(actor);
            NarrativeNpc targetNpc = world.Registry.GetNpc(target);

            int actorMoney = vanilla.GetMoney(actor);
            int targetMoney = vanilla.GetMoney(target);
            int pickpocket = vanilla.GetSkill(actor, VanillaSkill.Pickpocket);
            int stealth = vanilla.GetSkill(actor, VanillaSkill.Stealth);
            int dexterity = vanilla.GetAttribute(actor, VanillaAttribute.Dexterity);

            int motive = Math.Max(0, 35 - actorMoney / 4) + (int)((actorNpc?.Personality.Greed ?? 0.5) * 20.0);
            int means = Math.Min(30, pickpocket * 3 + stealth * 2 + dexterity);
            int targetPressure = Math.Min(35, item.Value / 40 + targetMoney / 80);
            int opportunity = 12;
            if (targetNpc != null && IsCommercial(targetNpc))
            {
                targetPressure += 10;
            }

            int score = motive + means + targetPressure + opportunity;
            SituationCandidate candidate = new SituationCandidate(
                PettyTheftSituation.ArchetypeId,
                score,
                actor,
                target,
                witness,
                item);

            candidate.AddCause(world.Registry.NameOf(actor) + " has motive: " + actorMoney + " orens and greed "
                               + (actorNpc?.Personality.Greed ?? 0.5).ToString("0.00"));
            candidate.AddCause(world.Registry.NameOf(actor) + " has means: pickpocket " + pickpocket
                               + ", stealth " + stealth + ", dexterity " + dexterity);
            candidate.AddCause(world.Registry.NameOf(target) + " is a target: carrying " + item.Name
                               + " worth " + item.Value + " orens");
            candidate.AddCause(world.Registry.NameOf(witness) + " is present as a possible witness");
            return candidate;
        }

        private static bool IsCommercial(NarrativeNpc npc)
        {
            string occupation = npc.Occupation ?? string.Empty;
            return occupation.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0
                   || occupation.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0
                   || occupation.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0
                   || npc.Roles.Contains("merchant")
                   || npc.Roles.Contains("shopkeeper");
        }

        private static ItemDescriptor BestTargetItem(IReadOnlyList<ItemDescriptor> inventory)
        {
            ItemDescriptor best = null;
            for (int i = 0; i < inventory.Count; i++)
            {
                ItemDescriptor item = inventory[i];
                if (item == null || item.Value < 250)
                {
                    continue;
                }

                if (best == null || item.Value > best.Value
                    || (item.Value == best.Value && string.CompareOrdinal(item.Id.Value, best.Id.Value) < 0))
                {
                    best = item;
                }
            }

            return best;
        }

        private static EntityId PickWitness(LocalAffordanceProfile profile, EntityId actor, EntityId target)
        {
            for (int i = 0; i < profile.Residents.Count; i++)
            {
                EntityId resident = profile.Residents[i];
                if (resident != actor && resident != target)
                {
                    return resident;
                }
            }

            return EntityId.None;
        }

        private static int CompareCandidates(SituationCandidate a, SituationCandidate b)
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0)
            {
                return score;
            }

            int actor = string.CompareOrdinal(a.ActorId.Value, b.ActorId.Value);
            if (actor != 0)
            {
                return actor;
            }

            int target = string.CompareOrdinal(a.TargetId.Value, b.TargetId.Value);
            if (target != 0)
            {
                return target;
            }

            return string.CompareOrdinal(a.Item.Id.Value, b.Item.Id.Value);
        }
    }
}
