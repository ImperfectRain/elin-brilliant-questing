using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// Decides when a story changes in the retelling, and what it changes into.
    ///
    /// A rumour that only ever loses confidence is a rumour that is still true. What makes gossip
    /// worth simulating is that it gets things wrong - and specifically that it gets the *person*
    /// wrong, because that is the mutation with teeth: an accusation, a feud and a bounty all hang
    /// off who the town thinks did it, and none of them care whether the object was a ring or a
    /// necklace.
    ///
    /// So one mutation is modelled: **substitution of the subject**. Somebody who half-remembers
    /// a story remembers that a thing was taken and misremembers who took it. The garbled version
    /// becomes its own `Fact`, marked untrue and linked back to what it is a version of; nothing
    /// touches the true one. The world knows exactly what happened for as long as anybody is
    /// wrong about it, which is the only reason a false accusation is ever correctable.
    ///
    /// Two people who mishear the same story the same way get the *same* false fact, not one
    /// each. Otherwise "the town believes Kel did it" is unaskable - there would be eleven
    /// separate beliefs that happen to name Kel - and the fact store would grow with every
    /// retelling.
    ///
    /// The player is never the one a story gets pinned on. Waking up to a town that has decided
    /// you are a thief, with no cause and no way to have seen it coming, is a situation the mod
    /// owes the player a proper entrance to; that is the false-accusation archetype (BQ-044) and
    /// its decline surface, not a side effect of the gossip scheduler.
    /// </summary>
    public sealed class RumorDistortion
    {
        /// <summary>
        /// Above this, the speaker still has the story straight. Garbling is what happens at the
        /// far end of a chain, not at the start of one.
        /// </summary>
        public double ClarityCeiling { get; set; } = 0.5;

        /// <summary>Odds a weak retelling names the wrong person.</summary>
        public double Chance { get; set; } = 0.25;

        /// <summary>
        /// What the listener should end up believing, which is usually the truth.
        ///
        /// Returns <paramref name="original"/> unchanged when the story holds together, and a
        /// false version of it when it does not.
        /// </summary>
        public Fact Retell(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Fact original,
            EntityId speaker,
            EntityId listener,
            double transmitted,
            DeterministicRng rng)
        {
            if (world == null || original == null || original.IsUntrue
                || transmitted > ClarityCeiling
                || !rng.Chance(Chance))
            {
                return original;
            }

            Fact garbled = Blame(world, vanilla, original, speaker, listener, rng);
            return garbled ?? original;
        }

        /// <summary>
        /// The same false version, produced deliberately: no clarity test and no dice on whether
        /// it happens, because somebody has decided to say it.
        ///
        /// Sharing the machinery is the point. A lie and a misunderstanding should be the same
        /// claim in the world - the town does not hold two different beliefs about who took the
        /// ring depending on how the idea got there - and everything that can later correct one
        /// works on the other for free.
        /// </summary>
        public Fact Blame(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Fact original,
            EntityId liar,
            EntityId listener,
            DeterministicRng rng)
        {
            if (world == null || original == null || original.IsUntrue)
            {
                return null;
            }

            EntityId blamed = PickSomeoneElse(world, vanilla, original, liar, listener, rng);
            if (blamed.IsNone)
            {
                return null;
            }

            return ExistingVersion(world, original, blamed) ?? Invent(world, original, blamed);
        }

        /// <summary>
        /// Somebody the story could plausibly be pinned on: a person the world model knows, alive,
        /// not the one who actually did it, not the one telling it, not the player, and above all
        /// not the person being told.
        ///
        /// That last exclusion looks fussy and is not. Without it the thief told the victim that
        /// the victim had robbed himself, the victim believed it, and two days later the ledger
        /// recorded him accusing himself of the theft. A person is the one witness to their own
        /// innocence, and nobody hears "it was you" and thinks well, perhaps.
        /// </summary>
        private static EntityId PickSomeoneElse(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Fact original,
            EntityId speaker,
            EntityId listener,
            DeterministicRng rng)
        {
            List<EntityId> candidates = new List<EntityId>();
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                if (npc.Id == original.Subject
                    || npc.Id == original.Object
                    || npc.Id == speaker
                    || npc.Id == listener
                    || npc.Id == vanilla.PlayerId
                    || !vanilla.IsAlive(npc.Id))
                {
                    continue;
                }

                candidates.Add(npc.Id);
            }

            if (candidates.Count == 0)
            {
                return EntityId.None;
            }

            // The registry is a dictionary, so sort before drawing: the same save must garble the
            // same way twice or none of this is reproducible.
            candidates.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return candidates[rng.NextInt(candidates.Count)];
        }

        private static Fact ExistingVersion(NarrativeWorldState world, Fact original, EntityId blamed)
        {
            foreach (Fact candidate in world.Knowledge.Facts.Values)
            {
                if (candidate.DistortionOf == original.Id && candidate.Subject == blamed)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The garbled version. It carries no evidence: the ring is still in the real thief's
        /// pocket, so there is nothing in the world that could substantiate this, and nobody who
        /// believes it will ever be able to prove it.
        /// </summary>
        private static Fact Invent(NarrativeWorldState world, Fact original, EntityId blamed)
        {
            Fact garbled = new Fact(
                world.NewId("fact"),
                blamed,
                original.Predicate,
                original.Object,
                original.Value,
                TruthState.False,
                original.Secrecy,
                original.OriginEvent)
            {
                DistortionOf = original.Id
            };

            world.Knowledge.AddFact(garbled);
            return garbled;
        }
    }
}
