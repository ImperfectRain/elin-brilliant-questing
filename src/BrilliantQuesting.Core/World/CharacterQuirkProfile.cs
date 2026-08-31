using System;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    public enum CharacterWeirdnessTier
    {
        MostlyOrdinary = 0,
        Distinctive = 1,
        Weird = 2,
        Unforgettable = 3
    }

    public enum CharacterQuirk
    {
        None = 0,
        ComparesPeopleToFish,
        NeverDiscussesMoneyIndoors,
        RefusesNames,
        PoliteThreats,
        ApologizesToCorpses,
        CollectsSpoiledFood,
        GreetsDoors,
        DistrustsCircularTables,
        FurnitureRemembersInsults,
        YoungerSistersBringLuck
    }

    /// <summary>
    /// Durable identity texture. Quirks are assigned once and then treated as part of the actor,
    /// not re-rolled at interaction time.
    /// </summary>
    public sealed class CharacterQuirkProfile
    {
        public bool Assigned { get; set; }

        public CharacterWeirdnessTier Weirdness { get; set; } = CharacterWeirdnessTier.MostlyOrdinary;

        public CharacterQuirk Kind { get; set; } = CharacterQuirk.None;

        public bool HasQuirk => Assigned && Kind != CharacterQuirk.None;
    }

    public static class CharacterQuirkAssignment
    {
        private static readonly CharacterQuirk[] Distinctive =
        {
            CharacterQuirk.ComparesPeopleToFish,
            CharacterQuirk.NeverDiscussesMoneyIndoors,
            CharacterQuirk.RefusesNames,
            CharacterQuirk.PoliteThreats
        };

        private static readonly CharacterQuirk[] Weird =
        {
            CharacterQuirk.ApologizesToCorpses,
            CharacterQuirk.CollectsSpoiledFood,
            CharacterQuirk.GreetsDoors,
            CharacterQuirk.DistrustsCircularTables
        };

        private static readonly CharacterQuirk[] Unforgettable =
        {
            CharacterQuirk.FurnitureRemembersInsults,
            CharacterQuirk.YoungerSistersBringLuck
        };

        public static bool AssignIfMissing(NarrativeNpc npc, DeterministicRng rng)
        {
            if (npc == null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (npc.Quirk.Assigned)
            {
                return false;
            }

            int roll = rng.NextInt(100);
            if (roll < 55)
            {
                Assign(npc, CharacterWeirdnessTier.MostlyOrdinary, CharacterQuirk.None);
            }
            else if (roll < 80)
            {
                Assign(npc, CharacterWeirdnessTier.Distinctive, Pick(Distinctive, rng));
            }
            else if (roll < 95)
            {
                Assign(npc, CharacterWeirdnessTier.Weird, Pick(Weird, rng));
            }
            else
            {
                Assign(npc, CharacterWeirdnessTier.Unforgettable, Pick(Unforgettable, rng));
            }

            return true;
        }

        private static void Assign(NarrativeNpc npc, CharacterWeirdnessTier tier, CharacterQuirk kind)
        {
            npc.Quirk.Assigned = true;
            npc.Quirk.Weirdness = tier;
            npc.Quirk.Kind = kind;
        }

        private static CharacterQuirk Pick(CharacterQuirk[] options, DeterministicRng rng)
        {
            return options[rng.NextInt(options.Length)];
        }
    }
}
