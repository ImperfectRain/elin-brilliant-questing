using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// Harm travels along ties. Nothing in the mod knows that this particular shopkeeper has a
    /// brother - it knows she has a Family edge, and every one of these assertions falls out of
    /// that edge rather than out of a rule naming the pair.
    /// </summary>
    public class RelationshipHarmTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Brother = EntityId.Parse("npc_brother");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Thug = EntityId.Parse("npc_thug");
        private static readonly EntityId Zone = EntityId.Parse("zone_town");

        private sealed class Town
        {
            public NarrativeWorldState World;
            public SandboxVanillaState Vanilla;
            public ConsequenceEngine Consequences;
        }

        /// <summary>
        /// A shopkeeper, her brother, an unrelated stranger and a thug. The only thing that makes
        /// the brother a brother is one Family edge.
        /// </summary>
        private static Town Create(int brotherSentiment = 70, RelationKind brotherTie = RelationKind.Family)
        {
            NarrativeWorldState world = new NarrativeWorldState(4242);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);

            foreach (EntityId id in new[] { Player, Shopkeeper, Brother, Stranger, Thug })
            {
                vanilla.Define(id, zone: Zone);
            }

            world.Registry.Add(new NarrativeNpc(Player, "You"));
            world.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira") { Occupation = "shopkeeper" });
            world.Registry.Add(new NarrativeNpc(Brother, "Halvar") { Occupation = "guard" });
            world.Registry.Add(new NarrativeNpc(Stranger, "Ost"));
            world.Registry.Add(new NarrativeNpc(Thug, "Kip"));

            world.Relationships.ConnectMutual(Shopkeeper, Brother, brotherTie, brotherSentiment);

            Town town = new Town { World = world, Vanilla = vanilla };
            town.Consequences = new ConsequenceEngine(world, vanilla);
            town.Consequences.Attach();
            return town;
        }

        private static WorldEvent Attack(Town town, EntityId actor, EntityId target, IReadOnlyList<string> tags = null)
        {
            return town.World.Record(
                WorldEventType.Attacked, actor, target, town.Vanilla.Now, 1.0, Zone, tags: tags);
        }

        private static WorldEvent Threaten(Town town, EntityId actor, EntityId target, EntityId witness)
        {
            return town.World.Record(
                WorldEventType.Threatened, actor, target, town.Vanilla.Now, 1.0, Zone,
                witnesses: new[] { witness });
        }

        [Fact]
        public void HurtingAShopkeeperTurnsHerBrotherAgainstYou()
        {
            Town town = Create();
            int brotherBefore = town.Vanilla.GetAffinity(Brother);

            Attack(town, Player, Shopkeeper);

            int brotherAfter = town.Vanilla.GetAffinity(Brother);
            Assert.True(brotherAfter < brotherBefore, "the brother should mind: " + brotherAfter);

            // He minds less than she does. A tie carries a share of a blow; it never amplifies it.
            Assert.True(town.Vanilla.GetAffinity(Shopkeeper) < brotherAfter);
        }

        [Fact]
        public void TheBrothersMemoryNamesWhoDidItAndToWhom()
        {
            Town town = Create();

            Attack(town, Player, Shopkeeper);

            MemoryRecord memory = Assert.Single(town.World.Memories.MemoriesAbout(Brother, Player));
            Assert.Equal("kin_was_attacked", memory.SummaryTag);
            Assert.Equal(town.Vanilla.GetAffinity(Brother), memory.AffinityContribution);

            // Rule 26: the trace has to be able to say why, in terms of the tie it came through.
            Assert.Contains(town.Consequences.Trace, line => line.Contains("Halvar") && line.Contains("Family") && line.Contains("Mira"));
        }

        [Fact]
        public void NobodyWithoutATieToHerReacts()
        {
            Town town = Create();

            Attack(town, Player, Shopkeeper);

            // Ost is standing in the same town, is the same kind of character, and saw nothing.
            // The reaction is the edge, not the proximity.
            Assert.Equal(0, town.Vanilla.GetAffinity(Stranger));
            Assert.Empty(town.World.Memories.MemoriesOf(Stranger));
        }

        [Fact]
        public void UnrelatedWitnessDoesNotLoseAffinityForAThreat()
        {
            Town town = Create();

            Threaten(town, Player, Shopkeeper, Stranger);

            Assert.Equal(0, town.Vanilla.GetAffinity(Stranger));
            MemoryRecord memory = Assert.Single(town.World.Memories.MemoriesAbout(Stranger, Player));
            Assert.Equal("saw_was_threatened", memory.SummaryTag);
            Assert.Equal(0, memory.AffinityContribution);
            Assert.Contains(town.Consequences.Trace, line => line.Contains("Ost witnessed Threatened")
                                                             && line.Contains("no affinity effect"));
        }

        [Fact]
        public void ConnectedWitnessCanReactToAThreat()
        {
            Town town = Create();

            Threaten(town, Player, Shopkeeper, Brother);

            Assert.True(town.Vanilla.GetAffinity(Brother) < 0);
            MemoryRecord memory = town.World.Memories.MemoriesAbout(Brother, Player)
                .Single(m => m.SummaryTag == "saw_was_threatened");
            Assert.Equal("saw_was_threatened", memory.SummaryTag);
            Assert.True(memory.AffinityContribution < 0);
            Assert.Contains(town.Consequences.Trace, line => line.Contains("Halvar witnessed Threatened")
                                                             && line.Contains("Family tie to Mira"));
        }

        [Fact]
        public void ACloserTieCarriesMoreThanADistantOne()
        {
            int throughFamily = ReactionOfBrother(RelationKind.Family, 70);
            int throughGuild = ReactionOfBrother(RelationKind.GuildMate, 70);
            int throughAcquaintance = ReactionOfBrother(RelationKind.Acquaintance, 70);

            Assert.True(throughFamily < throughGuild, throughFamily + " vs " + throughGuild);
            Assert.True(throughGuild < throughAcquaintance, throughGuild + " vs " + throughAcquaintance);
            Assert.True(throughAcquaintance < 0);
        }

        [Fact]
        public void AWarmerTieCarriesMoreThanAColdOne()
        {
            Assert.True(ReactionOfBrother(RelationKind.Family, 90) < ReactionOfBrother(RelationKind.Family, 30));
        }

        [Fact]
        public void AnEstrangedBrotherNeitherRalliesNorGloats()
        {
            Town town = Create(brotherSentiment: -40);

            Attack(town, Player, Shopkeeper);

            // Not a rally, and deliberately not a reward either: hurting someone must never be a
            // way to buy the goodwill of the people who dislike them.
            Assert.Equal(0, town.Vanilla.GetAffinity(Brother));
            Assert.Empty(town.World.Memories.MemoriesOf(Brother));
        }

        [Fact]
        public void ASlightDoesNotTravel()
        {
            Town town = Create();

            town.World.Record(WorldEventType.Trespass, Player, Shopkeeper, town.Vanilla.Now, 1.0, Zone);

            // She minds being trespassed on. It is not the kind of thing a brother hears about.
            Assert.True(town.Vanilla.GetAffinity(Shopkeeper) < 0);
            Assert.Equal(0, town.Vanilla.GetAffinity(Brother));
        }

        [Fact]
        public void ACrimeNobodyNoticedReachesNobodysFamily()
        {
            Town town = Create();

            Attack(town, Player, Shopkeeper, new[] { EventTags.Unnoticed });

            Assert.Equal(0, town.Vanilla.GetAffinity(Shopkeeper));
            Assert.Equal(0, town.Vanilla.GetAffinity(Brother));
        }

        [Fact]
        public void HarmBetweenNpcsLandsOnTheTieGraph()
        {
            Town town = Create();

            Attack(town, Thug, Shopkeeper);

            RelationshipEdge grudge = town.World.Relationships.Find(Brother, Thug);
            Assert.NotNull(grudge);
            Assert.True(grudge.Sentiment < 0, "the brother should hold a view of Kip: " + grudge);

            // Vanilla affinity only tracks the player, and the player did nothing here.
            Assert.Equal(0, town.Vanilla.GetAffinity(Brother));
            Assert.Equal(0, town.World.Memories.MemoriesAbout(Brother, Thug).First().AffinityContribution);
        }

        [Fact]
        public void RepeatedHarmDeepensTheGrudgeWithoutRunningAway()
        {
            Town town = Create();

            for (int i = 0; i < 40; i++)
            {
                Attack(town, Thug, Shopkeeper);
            }

            // It deepens, and then it stops. Sentiment is a bounded scale, not a counter.
            RelationshipEdge grudge = town.World.Relationships.Find(Brother, Thug);
            Assert.Equal(-100, grudge.Sentiment);
        }

        [Fact]
        public void ABrotherWhoWatchedItMindsMoreThanOneWhoHeardAboutIt()
        {
            Town watched = Create();
            watched.World.Record(
                WorldEventType.Attacked, Player, Shopkeeper, watched.Vanilla.Now, 1.0, Zone,
                witnesses: new[] { Brother });

            Town heard = Create();
            Attack(heard, Player, Shopkeeper);

            Assert.True(watched.Vanilla.GetAffinity(Brother) < heard.Vanilla.GetAffinity(Brother));
        }

        [Fact]
        public void TheWorldNeverDecidesHowThePlayerFeels()
        {
            Town town = Create();
            town.World.Relationships.ConnectMutual(Shopkeeper, Player, RelationKind.Friend, 80);

            Attack(town, Thug, Shopkeeper);

            // Background simulation may move NPC opinion. The player's own is theirs (D008).
            Assert.Null(town.World.Relationships.Find(Player, Thug));
            Assert.Empty(town.World.Memories.MemoriesOf(Player));
        }

        [Fact]
        public void TheDeadDoNotTakeSides()
        {
            Town town = Create();
            town.World.Registry.GetNpc(Brother).Alive = false;

            Attack(town, Player, Shopkeeper);

            Assert.Equal(0, town.Vanilla.GetAffinity(Brother));
        }

        [Fact]
        public void AWellConnectedVictimProducesAHandfulOfReactionsNotACrowd()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            world.Registry.Add(new NarrativeNpc(Player, "You"));
            world.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira"));

            for (int i = 0; i < 30; i++)
            {
                EntityId friend = EntityId.Parse("npc_friend_" + i.ToString("00"));
                world.Registry.Add(new NarrativeNpc(friend, "Friend " + i));
                vanilla.Define(friend, zone: Zone);
                world.Relationships.Connect(friend, Shopkeeper, RelationKind.Friend, 80);
            }

            ConsequenceEngine consequences = new ConsequenceEngine(world, vanilla);
            consequences.Attach();

            world.Record(WorldEventType.Attacked, Player, Shopkeeper, vanilla.Now, 1.0, Zone);

            int reacted = Enumerable.Range(0, 30)
                .Count(i => vanilla.GetAffinity(EntityId.Parse("npc_friend_" + i.ToString("00"))) != 0);
            Assert.Equal(HarmPropagation.MaxReactors, reacted);
        }

        [Fact]
        public void TheSameHarmInTheSameWorldReactsTheSameWay()
        {
            string First = Explain(Create());
            string Second = Explain(Create());
            Assert.Equal(First, Second);
        }

        private static string Explain(Town town)
        {
            Attack(town, Player, Shopkeeper);
            return string.Join("\n", town.Consequences.Trace);
        }

        private static int ReactionOfBrother(RelationKind tie, int sentiment)
        {
            Town town = Create(sentiment, tie);
            Attack(town, Player, Shopkeeper);
            return town.Vanilla.GetAffinity(Brother);
        }
    }
}
