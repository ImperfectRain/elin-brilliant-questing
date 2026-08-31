using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-046: the same observed death remains the same physical history, but its legal and
    /// social reading depends on who was actually in a position to recognize it.
    /// </summary>
    public class RecognizedViolenceTests
    {
        [Fact]
        public void AGuardWitnessReadsTheDeathAsMurder()
        {
            ViolenceLab lab = ViolenceLab.Create();
            lab.Witness.Roles.Add(AuthorityPolicy.GuardRole);
            int karma = lab.Vanilla.Karma;

            WorldEvent killing = lab.KillInFrontOfWitness();

            Assert.Equal(WorldEventType.Killed, killing.Type);
            Assert.True(lab.Vanilla.Karma < karma);
            Assert.Contains("standing judged: murder", lab.Engine.Trace);
        }

        [Fact]
        public void ASharedWitnessToTheFirstAttackReadsTheDeathAsSelfDefense()
        {
            ViolenceLab lab = ViolenceLab.Create();
            lab.Witness.Roles.Add(AuthorityPolicy.GuardRole);

            lab.VanillaActionRecorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Attacked,
                lab.VictimId,
                lab.PlayerId,
                EntityId.None,
                "",
                lab.ZoneId,
                "ActMelee",
                new[] { lab.WitnessId }));
            int karma = lab.Vanilla.Karma;

            lab.KillInFrontOfWitness();

            Assert.Equal(karma, lab.Vanilla.Karma);
            Assert.Contains("standing judged: self-defense", lab.Engine.Trace);
        }

        [Fact]
        public void AFightersWitnessReadsTheDeathAsALawfulBounty()
        {
            ViolenceLab lab = ViolenceLab.Create();
            lab.Witness.Roles.Add(GuildNetworks.FightersRole);
            int karma = lab.Vanilla.Karma;
            int fame = lab.Vanilla.Fame;

            lab.KillInFrontOfWitness();

            Assert.True(lab.Vanilla.Karma > karma);
            Assert.True(lab.Vanilla.Fame > fame);
            Assert.Contains("standing judged: lawful bounty", lab.Engine.Trace);
        }

        [Fact]
        public void AnOrdinaryWitnessStillDoesNotCreateALegalVerdict()
        {
            ViolenceLab lab = ViolenceLab.Create();
            int karma = lab.Vanilla.Karma;
            int fame = lab.Vanilla.Fame;

            lab.KillInFrontOfWitness();

            Assert.Equal(karma, lab.Vanilla.Karma);
            Assert.Equal(fame, lab.Vanilla.Fame);
            Assert.Contains("standing unchanged: Killed was observed, not judged", lab.Engine.Trace);
        }

        private sealed class ViolenceLab
        {
            private ViolenceLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ConsequenceEngine Engine { get; private set; }

            public VanillaActionRecorder VanillaActionRecorder { get; private set; }

            public EntityId PlayerId { get; private set; }

            public EntityId VictimId { get; private set; }

            public EntityId WitnessId { get; private set; }

            public EntityId ZoneId { get; private set; }

            public NarrativeNpc Witness => World.Registry.GetNpc(WitnessId);

            public static ViolenceLab Create()
            {
                ViolenceLab lab = new ViolenceLab();
                NarrativeWorldState world = new NarrativeWorldState(46046);
                EntityId player = world.NewId("npc");
                EntityId victim = world.NewId("npc");
                EntityId witness = world.NewId("npc");
                EntityId zone = world.NewId("zone");

                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });
                world.Registry.Add(new NarrativeNpc(victim, "Rasan") { Importance = NarrativeImportance.Known });
                world.Registry.Add(new NarrativeNpc(witness, "Ovel") { Importance = NarrativeImportance.Known });

                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, zone: zone);
                vanilla.Define(victim, zone: zone);
                vanilla.Define(witness, zone: zone);

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.PlayerId = player;
                lab.VictimId = victim;
                lab.WitnessId = witness;
                lab.ZoneId = zone;
                lab.Engine = new ConsequenceEngine(world, vanilla);
                lab.Engine.Attach();
                lab.VanillaActionRecorder = new VanillaActionRecorder(world, vanilla);
                return lab;
            }

            public WorldEvent KillInFrontOfWitness()
            {
                return VanillaActionRecorder.Record(new ObservedVanillaAction(
                    ObservedVanillaActionKind.Killed,
                    PlayerId,
                    VictimId,
                    EntityId.None,
                    "",
                    ZoneId,
                    "ActMelee",
                    new[] { WitnessId }));
            }
        }
    }
}
