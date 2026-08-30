using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-037: guilds as information networks. The step's own condition is that one caravan
    /// robbery reads as a bounty to one guild and as stock to another, and the rest of these pin
    /// what makes that a network rather than four authored views: it carries only what its
    /// interest table reads, it crosses the distance the street cannot, it leaves the player out
    /// of the background entirely, and it hands over word without ever handing over proof.
    /// </summary>
    public class GuildNetworkTests
    {
        /// <summary>
        /// One town, one robbery on the road out of it, and the people who might hear about it.
        ///
        /// Built directly rather than on the theft laboratory: what is being measured is which
        /// claims cross which network, and a scenario that comes with a theft already in it would
        /// be competing for the same day's few slots.
        /// </summary>
        private sealed class Robbery
        {
            internal NarrativeWorldState World { get; private set; }

            internal SandboxVanillaState Vanilla { get; private set; }

            internal RumorSystem Rumors { get; private set; }

            internal RumorCirculation Circulation { get; private set; }

            internal TownNews News { get; private set; }

            internal EntityId Player { get; private set; }

            internal EntityId Town { get; private set; }

            /// <summary>Somewhere else entirely: a guild hall a day's travel away.</summary>
            internal EntityId Hall { get; private set; }

            /// <summary>The bandit who took the shipment and killed the guard escorting it.</summary>
            internal EntityId Rurik { get; private set; }

            internal EntityId Killing { get; private set; }

            internal EntityId Theft { get; private set; }

            internal EntityId Shortage { get; private set; }

            internal static Robbery Create()
            {
                Robbery bench = new Robbery();
                NarrativeWorldState world = new NarrativeWorldState(20260830UL);
                EntityId player = world.NewId("npc");
                EntityId town = world.NewId("zone");
                EntityId hall = world.NewId("zone");

                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, zone: town);
                world.Registry.Add(new NarrativeNpc(player, "You"));

                bench.World = world;
                bench.Vanilla = vanilla;
                bench.Player = player;
                bench.Town = town;
                bench.Hall = hall;
                bench.Rumors = new RumorSystem(world.Knowledge, world.Ledger, world.Ids);

                // The gossip half is switched off in most of these: the street is BQ-035's and
                // would otherwise be a second way a claim could arrive, which is exactly what
                // these tests have to be able to tell apart.
                bench.Circulation = new RumorCirculation(bench.Rumors) { MaxFactsPerDay = 0, Distortion = null };
                bench.News = new TownNews(bench.Rumors);

                EntityId guard = bench.Person("Ceren, the caravan guard", town);
                EntityId cargo = world.NewId("item");
                EntityId tavernkeeper = bench.Person("Ilsa the tavernkeeper", town);
                bench.Rurik = bench.Person("Rurik", town);

                // One event, stated as what actually happened: somebody was killed, goods were
                // taken, and the town is now short of what was in the cart.
                bench.Killing = bench.Claim(bench.Rurik, FactPredicates.Killed, guard);
                bench.Theft = bench.Claim(bench.Rurik, FactPredicates.Stole, cargo, "the wine shipment");
                bench.Shortage = bench.Claim(tavernkeeper, FactPredicates.Needs, EntityId.None, "alcohol, any quality");

                return bench;
            }

            internal EntityId Person(string name, EntityId zone)
            {
                EntityId id = World.NewId("npc");
                World.Registry.Add(new NarrativeNpc(id, name));
                Vanilla.Define(id, level: 3, zone: zone);
                return id;
            }

            /// <summary>Somebody in a guild, standing wherever their work puts them.</summary>
            internal EntityId Member(string name, GuildId guild, EntityId zone)
            {
                EntityId id = Person(name, zone);
                World.Registry.GetNpc(id).Roles.Add(GuildNetworks.MembershipRole(guild));
                return id;
            }

            internal EntityId Claim(EntityId subject, string predicate, EntityId about, string value = null)
            {
                EntityId id = World.NewId("fact");
                World.Knowledge.AddFact(new Fact(id, subject, predicate, about, value));
                return id;
            }

            /// <summary>Teaches somebody a claim the way hearing it in the market teaches them.</summary>
            internal void Told(EntityId who, EntityId factId, double confidence = 0.8)
            {
                World.Knowledge.Teach(who, factId, KnowledgeSource.Hearsay, confidence, Vanilla.Now, false);
            }

            /// <summary>A day passes and every channel the world has runs once.</summary>
            internal RumorRound Day()
            {
                Circulation.Run(World, Vanilla, Vanilla.Now);
                Vanilla.AdvanceDays(1);
                return Circulation.Run(World, Vanilla, Vanilla.Now);
            }

            internal SpokenRemark Ask(EntityId speaker, EntityId factId)
            {
                IReadOnlyList<SpokenRemark> answer = News.Ask(World, Vanilla, speaker);
                for (int i = 0; i < answer.Count; i++)
                {
                    if (answer[i].FactId == factId)
                    {
                        return answer[i];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// The step's completion test. One robbery, two guilds, two different things to do about
        /// it - and neither reading was written for this scenario: the Fighters hear the killing
        /// because their network carries force, and the Thieves hear the theft because theirs
        /// carries property.
        /// </summary>
        [Fact]
        public void OneRobberyIsABountyToTheFightersAndStockToTheThieves()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            EntityId sable = bench.Member("Sable", GuildId.Thieves, bench.Town);

            // The player carries both cards, which Elin allows and this mod reads rather than
            // keeps: membership is a faction relation the game already owns.
            bench.Vanilla.SetGuildRank(GuildId.Fighters, 1).SetGuildRank(GuildId.Thieves, 1);

            bench.Told(harn, bench.Killing);
            bench.Told(harn, bench.Theft);
            bench.Told(sable, bench.Killing);
            bench.Told(sable, bench.Theft);

            SpokenRemark bounty = bench.Ask(harn, bench.Killing);
            SpokenRemark stock = bench.Ask(sable, bench.Theft);

            Assert.NotNull(bounty);
            Assert.Equal(GuildFraming.Bounty, bounty.Framing);
            Assert.Equal(GuildId.Fighters, bounty.Network);
            Assert.Contains("guild business", bounty.Line);

            Assert.NotNull(stock);
            Assert.Equal(GuildFraming.Fence, stock.Framing);
            Assert.Equal(GuildId.Thieves, stock.Network);
            Assert.Contains("buyer who does not ask", stock.Line);

            // Same event, and each contact reads their own half of it rather than the other's.
            Assert.Equal(GuildFraming.None, bench.Ask(harn, bench.Theft).Framing);
            Assert.Equal(GuildFraming.None, bench.Ask(sable, bench.Killing).Framing);
        }

        /// <summary>
        /// The routing half of the same claim: a network carries what it reads and nothing else,
        /// and the guild it is no business of hears none of it.
        /// </summary>
        [Fact]
        public void EachNetworkCarriesItsOwnHalfOfTheRobberyAndTheMagesHearNoneOfIt()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            EntityId bram = bench.Member("Bram", GuildId.Fighters, bench.Hall);
            EntityId sable = bench.Member("Sable", GuildId.Thieves, bench.Town);
            EntityId nix = bench.Member("Nix", GuildId.Thieves, bench.Hall);
            EntityId weiss = bench.Member("Weiss", GuildId.Mages, bench.Town);
            EntityId ivo = bench.Member("Ivo", GuildId.Mages, bench.Hall);

            foreach (EntityId knower in new[] { harn, sable, weiss })
            {
                bench.Told(knower, bench.Killing);
                bench.Told(knower, bench.Theft);
            }

            bench.Day();

            Assert.True(bench.World.Knowledge.Knows(bram, bench.Killing));
            Assert.False(bench.World.Knowledge.Knows(bram, bench.Theft));

            Assert.True(bench.World.Knowledge.Knows(nix, bench.Theft));
            Assert.False(bench.World.Knowledge.Knows(nix, bench.Killing));

            // A network with no interest in either half is silent about both, even though one of
            // its own members knows the lot.
            Assert.False(bench.World.Knowledge.Knows(ivo, bench.Killing));
            Assert.False(bench.World.Knowledge.Knows(ivo, bench.Theft));
        }

        /// <summary>
        /// What a guild is actually for: the member who hears it is nowhere near the member who
        /// needs it, and the street has no way to bridge that.
        /// </summary>
        [Fact]
        public void WordCrossesTheDistanceTheStreetCannot()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            EntityId bram = bench.Member("Bram", GuildId.Fighters, bench.Hall);
            EntityId bystander = bench.Person("Otto", bench.Hall);

            bench.Told(harn, bench.Killing);
            RumorRound round = bench.Day();

            Assert.NotEqual(bench.Vanilla.GetZoneOf(harn), bench.Vanilla.GetZoneOf(bram));
            Assert.True(bench.World.Knowledge.Knows(bram, bench.Killing));
            Assert.Equal(1, round.Routed);

            // Standing in the same hall is not membership. The claim reached the guild, not the
            // room it was said in.
            Assert.False(bench.World.Knowledge.Knows(bystander, bench.Killing));
        }

        /// <summary>
        /// The player is not on the network in the background, card or no card. A guild reaches
        /// them through somebody who says something (D008, standing rule 22).
        /// </summary>
        [Fact]
        public void TheNetworkNeverPutsAnythingInThePlayersHead()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            bench.Member("Bram", GuildId.Fighters, bench.Hall);

            bench.Vanilla.SetGuildRank(GuildId.Fighters, 3);
            bench.World.Registry.GetNpc(bench.Player).Roles.Add(GuildNetworks.FightersRole);
            bench.Told(harn, bench.Killing);

            bench.Day();

            Assert.False(bench.World.Knowledge.Knows(bench.Player, bench.Killing));
        }

        /// <summary>
        /// Nobody files a report on themselves. The subject clause circulation holds in the street
        /// binds harder inside a guild, which is the last place a member wants their own name
        /// raised.
        /// </summary>
        [Fact]
        public void AMemberNeverReportsAMatterHeIsTheSubjectOf()
        {
            Robbery bench = Robbery.Create();
            bench.World.Registry.GetNpc(bench.Rurik).Roles.Add(GuildNetworks.ThievesRole);
            EntityId nix = bench.Member("Nix", GuildId.Thieves, bench.Hall);

            bench.World.Knowledge.Teach(bench.Rurik, bench.Theft, KnowledgeSource.Participant, 1.0, bench.Vanilla.Now, false);

            RumorRound round = bench.Day();

            Assert.Equal(0, round.Routed);
            Assert.False(bench.World.Knowledge.Knows(nix, bench.Theft));
        }

        /// <summary>
        /// A network moves word, never evidence. A guild can be certain of something and still
        /// have nothing to put in front of a guard, which is what keeps BQ-012's proof layer
        /// meaningful once information starts travelling for free.
        /// </summary>
        [Fact]
        public void ProofDoesNotTravelWithTheReport()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            EntityId bram = bench.Member("Bram", GuildId.Fighters, bench.Hall);

            EntityId blade = bench.World.NewId("item");
            bench.World.Knowledge.GetFact(bench.Killing).EvidenceIds.Add(blade);
            bench.World.Knowledge.Teach(
                harn,
                bench.Killing,
                KnowledgeSource.Witnessed,
                1.0,
                bench.Vanilla.Now,
                true,
                new List<ProofLink> { new ProofLink(ProofKind.PhysicalEvidence, blade) });

            bench.Day();

            Assert.True(bench.World.Knowledge.Knows(bram, bench.Killing));
            Assert.False(bench.World.Knowledge.CanProve(bram, bench.Killing));
            Assert.True(bench.World.Knowledge.CanProve(harn, bench.Killing));
        }

        /// <summary>
        /// Membership buys the reading, not the news. A stranger is told the same thing happened,
        /// hedged the same way, and is left believing it just as firmly - what they do not get is
        /// somebody telling them what it means (D012, `LW §3.4`).
        /// </summary>
        [Fact]
        public void AnOutsiderHearsTheSameClaimWithoutTheGuildsReadingOfIt()
        {
            Robbery bench = Robbery.Create();
            EntityId sable = bench.Member("Sable", GuildId.Thieves, bench.Town);
            bench.Told(sable, bench.Theft);

            SpokenRemark toOutsider = bench.Ask(sable, bench.Theft);
            Assert.NotNull(toOutsider);
            Assert.Equal(GuildFraming.None, toOutsider.Framing);
            Assert.DoesNotContain("buyer who does not ask", toOutsider.Line);
            Assert.Contains("the wine shipment", toOutsider.Line);

            Assert.True(bench.News.Deliver(bench.World, bench.Vanilla, toOutsider, bench.Vanilla.Now));
            Assert.True(bench.World.Knowledge.Knows(bench.Player, bench.Theft));

            // The same speaker, the same claim, once the player carries the card: one sentence
            // longer, and the claim itself unchanged.
            Robbery second = Robbery.Create();
            EntityId fence = second.Member("Sable", GuildId.Thieves, second.Town);
            second.Vanilla.SetGuildRank(GuildId.Thieves, 1);
            second.Told(fence, second.Theft);

            SpokenRemark toMember = second.Ask(fence, second.Theft);
            Assert.NotNull(toMember);
            Assert.Equal(GuildFraming.Fence, toMember.Framing);
            Assert.StartsWith(toOutsider.Line, toMember.Line);
        }

        /// <summary>
        /// A contact leads with guild business. The ordering is the whole of what a network does
        /// to attention (`PM §9`) - and it reorders without hiding anything, so the matter a
        /// stranger would have been told first is still in the answer.
        /// </summary>
        [Fact]
        public void AContactLeadsWithWhatTheirNetworkCarries()
        {
            Robbery bench = Robbery.Create();
            EntityId sable = bench.Member("Sable", GuildId.Thieves, bench.Town);
            bench.Vanilla.SetGuildRank(GuildId.Thieves, 1);

            // The debt is the more confidently held of the two, so it leads for anybody outside
            // the network.
            EntityId debt = bench.Claim(bench.Rurik, FactPredicates.Owes, bench.Player, "80 orens");
            bench.Told(sable, debt, 0.9);
            bench.Told(sable, bench.Theft, 0.6);

            IReadOnlyList<SpokenRemark> answer = bench.News.Ask(bench.World, bench.Vanilla, sable);
            Assert.Equal(bench.Theft, answer[0].FactId);
            Assert.Equal(debt, answer[1].FactId);
        }

        /// <summary>
        /// Reload safety, inherited rather than reimplemented: routing runs inside the round the
        /// world's own day counter governs, so opening the same save twice does not double what
        /// the guilds know.
        /// </summary>
        [Fact]
        public void RoutingRunsOncePerDayHoweverOftenItIsCalled()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            bench.Member("Bram", GuildId.Fighters, bench.Hall);
            EntityId ivar = bench.Member("Ivar", GuildId.Fighters, bench.Hall);
            bench.Told(harn, bench.Killing);

            RumorRound first = bench.Day();
            RumorRound again = bench.Circulation.Run(bench.World, bench.Vanilla, bench.Vanilla.Now);

            Assert.True(first.Routed > 0);
            Assert.Equal(0, again.Routed);

            // Everybody who was going to hear it has heard it, so tomorrow the network has
            // nothing to say about it either.
            Assert.True(bench.World.Knowledge.Knows(ivar, bench.Killing));
            Assert.Equal(0, bench.Day().Routed);
        }

        /// <summary>
        /// A claim the world has replaced is nobody's business: passing it on would be a network
        /// circulating a version of events that is no longer the one the world holds.
        /// </summary>
        [Fact]
        public void ASupersededClaimIsCarriedByNobody()
        {
            Robbery bench = Robbery.Create();
            EntityId harn = bench.Member("Harn", GuildId.Fighters, bench.Town);
            EntityId bram = bench.Member("Bram", GuildId.Fighters, bench.Hall);

            bench.Told(harn, bench.Killing);
            bench.World.Knowledge.GetFact(bench.Killing).Truth = TruthState.Superseded;

            Assert.Equal(0, bench.Day().Routed);
            Assert.False(bench.World.Knowledge.Knows(bram, bench.Killing));
        }

        /// <summary>
        /// The interest table itself, at the one place it has to distinguish a thing from a
        /// person: where a shipment ended up is stock, and where a person ended up is somebody's
        /// whereabouts that this network has no more claim on than any other.
        /// </summary>
        [Fact]
        public void WhereAThingIsReadsAsStockAndWhereAPersonIsDoesNot()
        {
            Robbery bench = Robbery.Create();
            EntityId mine = bench.World.NewId("zone");
            EntityId cargo = bench.World.NewId("item");
            EntityId hidden = bench.Person("Ceren's brother", bench.Hall);

            Fact goods = bench.World.Knowledge.GetFact(bench.Claim(cargo, FactPredicates.LocatedAt, mine, "the old mine"));
            Fact person = bench.World.Knowledge.GetFact(bench.Claim(hidden, FactPredicates.LocatedAt, mine, "the old mine"));

            Assert.Equal(GuildFraming.Fence, GuildNetworks.Reads(bench.World, GuildId.Thieves, goods));
            Assert.Equal(GuildFraming.None, GuildNetworks.Reads(bench.World, GuildId.Thieves, person));
        }

        /// <summary>
        /// The merchants hear about the robbery as the hole it leaves rather than as the crime,
        /// which is what makes the shortage chain of `PM §52` route without anything knowing what
        /// a caravan is.
        /// </summary>
        [Fact]
        public void TheMerchantsHearTheShortageRatherThanTheCrime()
        {
            Robbery bench = Robbery.Create();
            EntityId guilda = bench.Member("Guilda", GuildId.Merchants, bench.Town);
            EntityId tam = bench.Member("Tam", GuildId.Merchants, bench.Hall);

            bench.Told(guilda, bench.Killing);
            bench.Told(guilda, bench.Theft);
            bench.Told(guilda, bench.Shortage);

            bench.Day();

            Assert.True(bench.World.Knowledge.Knows(tam, bench.Shortage));
            Assert.False(bench.World.Knowledge.Knows(tam, bench.Killing));
            Assert.False(bench.World.Knowledge.Knows(tam, bench.Theft));
        }
    }
}
