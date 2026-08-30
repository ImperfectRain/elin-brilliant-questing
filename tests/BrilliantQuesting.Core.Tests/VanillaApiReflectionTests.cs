using System.Reflection;
using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class VanillaApiReflectionTests
    {
        [Fact]
        public void HomeCapacityReadsMaxPopulationOnly()
        {
            HomeBranchStub branch = new HomeBranchStub { MaxPopulation = 9, Capacity = 99 };

            Assert.True(VanillaApiReflection.TryReadInt(branch, "MaxPopulation", out int capacity));
            Assert.Equal(9, capacity);
            Assert.Null(VanillaApiReflection.ResolveReadableMember(typeof(HomeBranchStub), "maxResident"));
        }

        [Fact]
        public void HomeAdmissionResolvesMisspelledAddMemeber()
        {
            MethodInfo method = VanillaApiReflection.ResolveHomeAdmission(typeof(HomeBranchStub), typeof(Chara));

            Assert.NotNull(method);
            Assert.Equal("AddMemeber", method.Name);
            Assert.Null(typeof(HomeBranchStub).GetMethod("AddMember", BindingFlags.Public | BindingFlags.Instance));
        }

        [Fact]
        public void HomeAdmissionIsUnsupportedWhenMisspelledVanillaMethodIsAbsent()
        {
            Assert.Null(VanillaApiReflection.ResolveHomeAdmission(typeof(StaleHomeBranchStub), typeof(Chara)));
        }

        [Fact]
        public void MovementResolvesTwoArgumentMoveZoneWithEnterState()
        {
            MethodInfo method = VanillaApiReflection.ResolveMoveZone(typeof(Chara));

            Assert.NotNull(method);
            Assert.Equal("MoveZone", method.Name);
            Assert.Equal(typeof(Zone), method.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(ZoneTransition.EnterState), method.GetParameters()[1].ParameterType);
            Assert.Equal(ZoneTransition.EnterState.RandomVisit,
                VanillaApiReflection.ResolveEnterState(typeof(ZoneTransition.EnterState)));
        }

        [Fact]
        public void MovementEnterStateFallsBackToExistingEnumValue()
        {
            Assert.Equal(AlternateEnterState.Somewhere,
                VanillaApiReflection.ResolveEnterState(typeof(AlternateEnterState)));
        }

        [Fact]
        public void MovementIgnoresOneArgumentAndStaleZoneNames()
        {
            Assert.Null(VanillaApiReflection.ResolveMoveZone(typeof(StaleChara)));
            Assert.Null(VanillaApiReflection.ResolveSpatialFindZone(typeof(StaleSpatialManager)));
        }

        [Fact]
        public void SpatialLookupResolvesFindIntReturningZone()
        {
            MethodInfo method = VanillaApiReflection.ResolveSpatialFindZone(typeof(SpatialManager));

            Assert.NotNull(method);
            Assert.Equal("Find", method.Name);
        }

        [Fact]
        public void MovementGlobalPreconditionRequiresExistingGlobalRecord()
        {
            Assert.False(VanillaApiReflection.LooksGlobal(new Chara()));
            Assert.True(VanillaApiReflection.LooksGlobal(new Chara { global = new object() }));
            Assert.True(VanillaApiReflection.LooksGlobal(new Chara { IsGlobal = true }));
        }

        [Fact]
        public void RawSpeechResolvesInheritedCardRoute()
        {
            MethodInfo method = VanillaApiReflection.ResolveRawSpeech(typeof(Chara));

            Assert.NotNull(method);
            Assert.Equal("SayRaw", method.Name);
            Assert.Equal(typeof(Card), method.DeclaringType);
        }

        [Fact]
        public void RawSpeechCanResolveTalkRawWhenSayRawIsAbsent()
        {
            MethodInfo method = VanillaApiReflection.ResolveRawSpeech(typeof(TalkOnlyChara));
            object[] arguments = VanillaApiReflection.RawSpeechArguments(method, "line");

            Assert.NotNull(method);
            Assert.Equal("TalkRaw", method.Name);
            Assert.Equal(new object[] { "line", "", "", true }, arguments);
        }

        [Fact]
        public void RawSpeechIsUnsupportedWhenOnlyLocalizationKeyRouteExists()
        {
            Assert.Null(VanillaApiReflection.ResolveRawSpeech(typeof(StaleBarkChara)));
        }

        [Fact]
        public void QualityUsesTotalQualityOrQualityButNeverRarity()
        {
            QualityThing thing = new QualityThing { Quality = 4, rarity = 99 };

            Assert.True(VanillaApiReflection.TryReadQuality(thing, out int quality, out string source));
            Assert.Equal(14, quality);
            Assert.Equal("GetTotalQuality", source);

            QualityOnlyThing fallback = new QualityOnlyThing { Quality = 7, rarity = 100 };
            Assert.True(VanillaApiReflection.TryReadQuality(fallback, out quality, out source));
            Assert.Equal(7, quality);
            Assert.Equal("Quality", source);

            RarityOnlyThing unreadable = new RarityOnlyThing { rarity = 100 };
            Assert.False(VanillaApiReflection.TryReadQuality(unreadable, out quality, out source));
            Assert.Equal(0, quality);
        }

        [Fact]
        public void GuildRankReadsRelationRankOnlyForMembers()
        {
            Guild guild = new Guild { IsMember = true, relation = new FactionRelation { rank = 6 } };
            Guild nonmember = new Guild { IsMember = false, relation = new FactionRelation { rank = 6 } };

            Assert.True(VanillaApiReflection.TryReadGuildRank(guild, out int rank));
            Assert.Equal(6, rank);
            Assert.True(VanillaApiReflection.TryReadGuildRank(nonmember, out rank));
            Assert.Equal(0, rank);
        }

        [Fact]
        public void GuildRankFailsClosedWhenRelationShapeIsUnreadable()
        {
            Guild missingRelation = new Guild { IsMember = true };
            StaleGuild stale = new StaleGuild { IsMember = true, rank = 9 };

            Assert.False(VanillaApiReflection.TryReadGuildRank(missingRelation, out int rank));
            Assert.Equal(0, rank);
            Assert.False(VanillaApiReflection.TryReadGuildRank(stale, out rank));
            Assert.Equal(0, rank);
        }

        [Fact]
        public void KnownFieldLookupFindsStaticAndInheritedActFields()
        {
            Chara actor = new Chara();
            Thing tool = new Thing();
            Act.CC = actor;
            DerivedTargetAct act = new DerivedTargetAct { target = tool };

            Assert.Same(actor, VanillaApiReflection.GetKnownField<Chara>(act, "CC"));
            Assert.Same(tool, VanillaApiReflection.GetKnownField<Thing>(act, "target"));
            Assert.Null(VanillaApiReflection.GetKnownField<Thing>(act, "madeProduct"));
        }

        private sealed class HomeBranchStub
        {
            public int MaxPopulation { get; set; }
            public int Capacity { get; set; }
            public void AddMemeber(Chara chara) { }
        }

        private sealed class StaleHomeBranchStub
        {
            public void AddMember(Chara chara) { }
            public void AddResident(Chara chara) { }
            public void AddChara(Chara chara) { }
        }

        private class Card
        {
            public void SayRaw(string text, string ref1, string ref2) { }
        }

        private class TalkOnlyCard
        {
            public void TalkRaw(string text, string ref1, string ref2, bool log) { }
        }

        private sealed class TalkOnlyChara : TalkOnlyCard
        {
        }

        private sealed class StaleBarkChara
        {
            public void Say(string localizationKey) { }
            public void Talk(string localizationKey) { }
        }

        private class Chara : Card
        {
            public object global = null;
            public bool IsGlobal = false;
            public void MoveZone(Zone zone, ZoneTransition.EnterState state) { }
            public void MoveZone(Zone zone, ZoneTransition transition) { }
        }

        private sealed class StaleChara
        {
            public void MoveZone(Zone zone) { }
            public void SetZone(Zone zone) { }
            public void ChangeZone(Zone zone) { }
        }

        private sealed class Zone
        {
        }

        private sealed class ZoneTransition
        {
            public enum EnterState
            {
                Auto,
                RandomVisit
            }
        }

        private enum AlternateEnterState
        {
            Somewhere
        }

        private sealed class SpatialManager
        {
            public Zone Find(int uid) => new Zone();
        }

        private sealed class StaleSpatialManager
        {
            public Zone FindZone(int uid) => new Zone();
            public Zone GetZone(int uid) => new Zone();
        }

        private sealed class QualityThing
        {
            public int Quality { get; set; }
            public int rarity;
            public int GetTotalQuality(bool includeQuality) => includeQuality ? 14 : 3;
        }

        private sealed class QualityOnlyThing
        {
            public int Quality { get; set; }
            public int rarity;
        }

        private sealed class RarityOnlyThing
        {
            public int rarity;
        }

        private sealed class Guild
        {
            public bool IsMember { get; set; }
            public FactionRelation relation;
        }

        private sealed class StaleGuild
        {
            public bool IsMember { get; set; }
            public int rank;
        }

        private sealed class FactionRelation
        {
            public int rank;
        }

        private class Act
        {
            public static Chara CC;
        }

        private sealed class DerivedTargetAct : Act
        {
            public Thing target;
        }

        private sealed class Thing
        {
        }
    }
}
