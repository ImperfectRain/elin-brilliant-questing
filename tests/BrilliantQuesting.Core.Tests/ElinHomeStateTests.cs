using System;
using System.Collections.Generic;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Plugin;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public sealed class ElinHomeStateTests : IDisposable
    {
        public void Dispose() { EClass.Branch = null; }

        [Fact]
        public void RealReaderUsesOwnerZoneAndKeepsEntireLivingMembershipWithoutBinding()
        {
            var branch = new HomeBranchDouble();
            for (int i = 1; i <= 22; i++) branch.members.Add(new Chara { uid = i, Name = "Member" });
            branch.members.Add(new Chara { uid = 23, Name = "Dead", isDead = true });
            EClass.Branch = branch;
            var bindings = new ElinBindings();
            bindings.Bind(1, EntityId.Parse("staged_resident"));
            var home = ElinHomeState.Read(bindings, EntityId.Parse("player"), null);
            Assert.Equal(EntityId.Parse("zone_7"), home.ZoneId);
            Assert.Equal("Meadow", home.Name);
            Assert.Equal(22, home.ResidentCount);
            Assert.Equal(24, home.Capacity);
            Assert.Equal(1, bindings.Writes);
            Assert.Equal(EntityId.Parse("staged_resident"), home.Residents[0].Id);
            Assert.Equal(EntityId.Parse("npc_vanilla_22"), home.Residents[21].Id);
        }

        [Fact]
        public void MissingOwnerCannotBorrowBranchUidOrStaleUidZone()
        {
            EClass.Branch = new HomeBranchDouble { owner = null };
            var home = ElinHomeState.Read(null, EntityId.Parse("player"), null);
            Assert.True(home.ZoneId.IsNone);
            Assert.Equal(string.Empty, home.Name);
            EClass.Branch = new ThrowingOwnerDouble();
            Assert.True(ElinHomeState.Read(null, EntityId.Parse("player"), null).ZoneId.IsNone);
            EClass.Branch = null;
            Assert.Null(ElinHomeState.Read(null, EntityId.Parse("player"), null));
        }

        [Fact]
        public void ReaderSnapshotEnablesActiveResidentClockReconciliationAndReloadDoesNotReplay()
        {
            var branch = new HomeBranchDouble();
            branch.members.Add(new Chara { uid = 1, Name = "Resident" });
            EClass.Branch = branch;
            var player = EntityId.Parse("player");
            var home = ElinHomeState.Read(null, player, null);
            var id = home.Residents[0].Id;
            var world = new NarrativeWorldState(108);
            world.Registry.Add(new NarrativeNpc(id, "Resident")); // existing intake, before reconcile
            var vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, zone: home.ZoneId);
            vanilla.Define(id, zone: home.ZoneId);
            vanilla.SetActorActivity(id, new ActorActivityBuilder(id).WithPresence(PhysicalPresence.InActiveZone).Build());
            vanilla.SetHome(home);
            var now = GameTime.FromDays(20);
            var schemes = new OffScreenSchemes();
            schemes.ReconcileZone(world, vanilla, now);
            Assert.Equal(now, world.Registry.GetNpc(id).LastSimulatedAt);
            var restored = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            schemes.ReconcileZone(restored, vanilla, now);
            Assert.Equal(now, restored.Registry.GetNpc(id).LastSimulatedAt);
            Assert.Equal(0, restored.Ledger.Count);
            Assert.Same(home, vanilla.GetHomeState());
        }

        private sealed class HomeBranchDouble
        {
            public object owner = new HomeZoneDouble();
            public int uid = 999;
            public int uidZone = 888;
            public string Name => "Wrong branch name";
            public int MaxPopulation => 24;
            public List<Chara> members = new List<Chara>();
        }
        private sealed class HomeZoneDouble { public int uid = 7; public string Name => "Meadow"; }
        private sealed class ThrowingOwnerDouble { public object owner => throw new InvalidOperationException(); }
    }
}

// Minimal native shapes for the linked production Home reader, not an emulation of Elin.
internal static class EClass { internal static object Branch { get; set; } }
internal sealed class Chara { public int uid; public string Name; public bool isDead; }
namespace BrilliantQuesting.Plugin
{
    internal sealed class ElinBindings
    {
        private readonly Dictionary<int, EntityId> _ids = new Dictionary<int, EntityId>();
        public int Writes { get; private set; }
        internal void Bind(int uid, EntityId id) { _ids[uid] = id; Writes++; }
        internal EntityId IdOf(Chara c, EntityId player) => _ids.TryGetValue(c.uid, out var id) ? id : MintCharaId(c, player);
        internal static EntityId MintCharaId(Chara c, EntityId player) => EntityId.Parse("npc_vanilla_" + c.uid);
        internal Chara ResolveChara(EntityId id) => null;
    }
    internal static class ElementAliases
    {
        internal static bool TryGet(HomeMetric metric, out int id) { id = 0; return false; }
    }
}
