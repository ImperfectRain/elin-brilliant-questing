using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// One live physical character is one participating BQ actor.
    ///
    /// The invariant is narrow on purpose. It says nothing about who exists, who is alive, or who
    /// may be cast - only that two BQ identities must never both act on behalf of the same body.
    /// Casting, familiarity, beliefs, callbacks, relationships and history all assume that an
    /// <see cref="EntityId"/> names one person; where one Elin character was registered twice, all
    /// six of them are quietly wrong at once, and the failures look like different bugs.
    ///
    /// Duplicates are prevented at intake - the adapter asks for the id a character already has
    /// before it mints one - and this is the other half: a save written before that was true still
    /// carries the pair, and the pair has to be reconciled on load rather than left to be found by
    /// six consumers separately.
    ///
    /// <b>Reconciliation never rewrites history.</b> Both records survive with everything in them.
    /// One is named canonical and keeps participating; the other is retired onto it
    /// (<see cref="EntityRegistry.Retire"/>) and keeps being resolvable, so the events, beliefs
    /// and threads written under its id still read correctly. Repointing them would be inventing a
    /// past in which somebody else did those things, which is a worse failure than the duplicate.
    /// </summary>
    public static class ActorIdentityIntake
    {
        /// <summary>
        /// One duplicate that was reconciled, for the log and for a test to assert against.
        /// </summary>
        public sealed class Retirement
        {
            internal Retirement(EntityId alias, EntityId canonical, string vanillaRef)
            {
                Alias = alias;
                Canonical = canonical;
                VanillaRef = vanillaRef ?? string.Empty;
            }

            public EntityId Alias { get; }

            public EntityId Canonical { get; }

            /// <summary>The external reference the two records shared - the physical character.</summary>
            public string VanillaRef { get; }

            public override string ToString()
            {
                return Alias + " retired onto " + Canonical + " (one character, external ref " + VanillaRef + ")";
            }
        }

        private static readonly IReadOnlyList<Retirement> None = new List<Retirement>();

        /// <summary>
        /// Finds every set of person records claiming one physical character and retires all but
        /// one of each.
        ///
        /// <paramref name="mintedFromExternalRef"/> is how the caller says which of its own ids are
        /// derived labels rather than names: the adapter mints an id out of a live character's uid
        /// for anybody it meets, and BQ authors ids for the people it stages. Where a physical
        /// character carries both, the authored one is canonical - it is the name situations,
        /// threads and organizations were written against, and the derived one can be reconstructed
        /// from the character at any time while the authored one cannot. Core does not know either
        /// convention and must not; it asks.
        ///
        /// Deterministic in every other case: among ids of the same kind the ordinally first wins,
        /// so two loads of one save reconcile identically. Idempotent, so running it on every load
        /// costs nothing once the save is clean.
        /// </summary>
        public static IReadOnlyList<Retirement> Reconcile(
            NarrativeWorldState world,
            Func<EntityId, bool> mintedFromExternalRef)
        {
            if (world == null)
            {
                return None;
            }

            Dictionary<string, List<NarrativeNpc>> byRef = new Dictionary<string, List<NarrativeNpc>>(StringComparer.Ordinal);
            foreach (KeyValuePair<EntityId, NarrativeNpc> pair in world.Registry.AllNpcs)
            {
                NarrativeNpc npc = pair.Value;

                // A record already retired is already reconciled, and a record bound to nothing
                // shares no body with anybody - an empty external ref is "not spawned", which is a
                // normal state and not a claim that two of them are the same person.
                if (!npc.IsCanonical || string.IsNullOrEmpty(npc.VanillaCharaRef))
                {
                    continue;
                }

                if (!byRef.TryGetValue(npc.VanillaCharaRef, out List<NarrativeNpc> sharing))
                {
                    sharing = new List<NarrativeNpc>();
                    byRef[npc.VanillaCharaRef] = sharing;
                }

                sharing.Add(npc);
            }

            List<Retirement> retired = null;
            foreach (KeyValuePair<string, List<NarrativeNpc>> pair in byRef)
            {
                List<NarrativeNpc> sharing = pair.Value;
                if (sharing.Count < 2)
                {
                    continue;
                }

                sharing.Sort(delegate(NarrativeNpc left, NarrativeNpc right)
                {
                    bool leftMinted = mintedFromExternalRef != null && mintedFromExternalRef(left.Id);
                    bool rightMinted = mintedFromExternalRef != null && mintedFromExternalRef(right.Id);
                    if (leftMinted != rightMinted)
                    {
                        return leftMinted ? 1 : -1;
                    }

                    return string.CompareOrdinal(left.Id.Value, right.Id.Value);
                });

                NarrativeNpc canonical = sharing[0];
                for (int i = 1; i < sharing.Count; i++)
                {
                    if (!world.Registry.Retire(sharing[i].Id, canonical.Id))
                    {
                        continue;
                    }

                    if (retired == null)
                    {
                        retired = new List<Retirement>();
                    }

                    retired.Add(new Retirement(sharing[i].Id, canonical.Id, pair.Key));
                }
            }

            if (retired == null)
            {
                return None;
            }

            retired.Sort(delegate(Retirement left, Retirement right)
            {
                return string.CompareOrdinal(left.Alias.Value, right.Alias.Value);
            });

            return retired;
        }
    }
}
