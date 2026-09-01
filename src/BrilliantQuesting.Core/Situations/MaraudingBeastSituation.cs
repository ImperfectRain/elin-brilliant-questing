using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The laboratory for guild authority: something is taking people on the Wick road, and a
    /// guild hall two zones away whose whole business that is.
    ///
    /// The earlier laboratories each closed the other routes by taking something out of the world.
    /// This one closes nothing. The beast can be fought, the carter can be taken in, the road can
    /// be avoided - what it isolates is a difference between two players standing in the same hall
    /// with the same news, one of whom carries the Fighters' card. `PM 62` files invoking guild
    /// authority without membership under impossible; this is the situation that shows what the
    /// membership is worth and what its absence actually costs, which is one route and not the
    /// problem.
    ///
    /// Three things make it a laboratory for the mechanic rather than a monster quest.
    ///
    /// **Nothing here is written for the Fighters.** The situation states that a man is not safe
    /// from a thing on the road, and the network's own interest table reads that as a bounty. Toma
    /// stands in the same hall for the Merchants and reads nothing in it, having been told nothing
    /// different - which is the same table refusing, not a second one.
    ///
    /// **The hall is not where the trouble is.** Wickstead is a day off; the officers never see the
    /// road and are told about it by the member who walks in. What crosses that distance is the
    /// guild, and it is the one thing in the situation that a lone adventurer does not have.
    ///
    /// **What died stays dead.** Halda was killed before the situation opens and no guild can undo
    /// it: the killing is history, and history is not a matter anybody is asked to put right. The
    /// only thing that can be answered is the condition the beast leaves behind.
    ///
    /// The beast deliberately has no body. It is registered so the claim about it can name
    /// something, and no character is staged for it, because what a guild's answer supersedes here
    /// is the claim that somebody is in danger - `D021` keeps embodiment vanilla's, and a mod that
    /// declared a live creature dealt with while it still walked around would be exactly the
    /// contradiction that rule exists to prevent.
    /// </summary>
    public sealed class MaraudingBeastSituation
    {
        public const string ArchetypeId = "marauding_beast";

        private MaraudingBeastSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>The carter who has to use the road, and is not safe on it.</summary>
        public EntityId CarterId { get; private set; }

        /// <summary>The drover it already killed. Registered so history can name her.</summary>
        public EntityId DroverId { get; private set; }

        /// <summary>Speaks for the Fighters. Knows nothing until somebody walks in and tells him.</summary>
        public EntityId FightersOfficerId { get; private set; }

        /// <summary>Speaks for the Merchants, in the same hall, and reads nothing in any of it.</summary>
        public EntityId MerchantsOfficerId { get; private set; }

        /// <summary>The thing itself. Named, never embodied.</summary>
        public EntityId BeastId { get; private set; }

        /// <summary>"Orren is not safe from the thing on the Wick road." The matter.</summary>
        public EntityId ExposureFactId { get; private set; }

        /// <summary>"The thing killed Halda." History, and not answerable.</summary>
        public EntityId KillingFactId { get; private set; }

        /// <summary>What is left of Halda's team, on the road where it happened.</summary>
        public EntityId CarcassId { get; private set; }

        public EntityId HamletZoneId { get; private set; }

        public EntityId HallZoneId { get; private set; }

        public static MaraudingBeastSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId hamlet, GameTime now)
        {
            MaraudingBeastSituation situation = new MaraudingBeastSituation
            {
                HamletZoneId = hamlet,
                HallZoneId = world.NewId("zone"),
                CarcassId = world.NewId("item")
            };

            world.Registry.Add(new NarrativeSite(hamlet, "Wickstead", "village"));
            world.Registry.Add(new NarrativeSite(situation.HallZoneId, "the guild hall in Derwen", "hall"));

            NarrativeNpc carter = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Orren")
            {
                Occupation = "carter",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc drover = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Halda")
            {
                Occupation = "drover",
                Importance = NarrativeImportance.Known
            });

            // No blueprint and no zone. The thing on the road is a subject the claims can name and
            // nothing else; the mod does not put a creature in the game and then declare it dealt
            // with.
            NarrativeNpc beast = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "the thing out of the Fenwyck barrow")
            {
                Occupation = "beast",
                Importance = NarrativeImportance.Known
            });

            NarrativeNpc fighters = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Sera")
            {
                Occupation = "guild officer",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc merchants = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Toma")
            {
                Occupation = "guild officer",
                Importance = NarrativeImportance.Known
            });

            // Two roles each, and neither is invented here: the standing that lets somebody commit
            // a guild, and the membership that says which guild it is.
            fighters.Roles.Add(AuthorityPolicy.GuildRole);
            fighters.Roles.Add(GuildNetworks.FightersRole);
            merchants.Roles.Add(AuthorityPolicy.GuildRole);
            merchants.Roles.Add(GuildNetworks.MerchantsRole);

            situation.CarterId = carter.Id;
            situation.DroverId = drover.Id;
            situation.BeastId = beast.Id;
            situation.FightersOfficerId = fighters.Id;
            situation.MerchantsOfficerId = merchants.Id;

            stager.StageCharacter(carter.Id, new CharacterBlueprint(carter.Name)
                    .With(VanillaAttribute.Endurance, 11)
                    .With(VanillaAttribute.Will, 9),
                hamlet);
            stager.StageCharacter(fighters.Id, new CharacterBlueprint(fighters.Name)
                    .With(VanillaAttribute.Will, 14)
                    .With(VanillaAttribute.Strength, 15),
                situation.HallZoneId);
            stager.StageCharacter(merchants.Id, new CharacterBlueprint(merchants.Name)
                    .With(VanillaAttribute.Charisma, 14)
                    .With(VanillaSkill.Negotiation, 13),
                situation.HallZoneId);

            // -- what is true -----------------------------------------------------------------
            Fact killing = new Fact(
                world.NewId("fact"), beast.Id, FactPredicates.Killed, drover.Id,
                "on the Wick road", TruthState.True);
            world.Knowledge.AddFact(killing);
            situation.KillingFactId = killing.Id;

            Fact exposure = new Fact(
                world.NewId("fact"), carter.Id, FactPredicates.AtRisk, beast.Id,
                "the beast on the Wick road", TruthState.True);
            exposure.EvidenceIds.Add(situation.CarcassId);
            world.Knowledge.AddFact(exposure);
            situation.ExposureFactId = exposure.Id;

            // Something real on the road, so the claim is a thing somebody looked at rather than a
            // thing the situation asserted.
            stager.StageItem(hamlet, new ItemDescriptor(
                situation.CarcassId, "what is left of Halda's team", "carcass", 0, "carcass"));

            // -- who knows what ---------------------------------------------------------------
            // Orren lives with it and the player has seen the road. The hall has heard nothing:
            // whatever the officers end up believing, a member walked in and told them.
            world.Knowledge.Teach(carter.Id, killing.Id, KnowledgeSource.Witnessed, 1.0, now, true);
            world.Knowledge.Teach(carter.Id, exposure.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(player, killing.Id, KnowledgeSource.Hearsay, 0.8, now, false);
            world.Knowledge.Teach(player, exposure.Id, KnowledgeSource.Witnessed, 1.0, now, true);

            carter.Goals.Add(new NpcGoal("get_the_carts_through", situation.HamletZoneId, 80));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 45,
                Importance = 55,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(carter.Id);
            thread.ParticipantIds.Add(beast.Id);
            thread.FactIds.Add(killing.Id);
            thread.FactIds.Add(exposure.Id);
            thread.SiteIds.Add(hamlet);
            thread.SiteIds.Add(situation.HallZoneId);
            thread.OpenQuestions.Add("What is taking people on the Wick road?");
            thread.Escalation.Add(new EscalationStep("road_closes", 5, "Nothing moves on the Wick road."));
            thread.Escalation.Add(new EscalationStep("wickstead_empties", 14, "Wickstead starts to empty."));
            ArchetypeRecoveryRoutes.AddRecognizedViolence(thread);

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
