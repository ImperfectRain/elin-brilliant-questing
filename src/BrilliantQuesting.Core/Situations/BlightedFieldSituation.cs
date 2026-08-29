using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The laboratory for faith: a hamlet whose sown field has gone under, and a shrine at the
    /// edge of it.
    ///
    /// The earlier laboratories each closed the other routes by taking something out of the world
    /// - nobody knew anything, there was nothing to find, the goods did not exist. This one closes
    /// them by what the trouble *is*. A blight is in the land, and land is not a thing anybody can
    /// pick up, carry, appraise, mend or hand over: no examination verb has an object to be
    /// pointed at, no craft has stock to work from and no demand is stated that goods could
    /// answer. Which is exactly why it is a matter for a god rather than a carpenter, and it is
    /// deliberately narrow in the same way its predecessors are - a laboratory for one solution
    /// family, not an archetype. Route plurality is what `PM 70` asks of generated archetypes, and
    /// that is Stage S5's job rather than this step's.
    ///
    /// What the step turns on is that the gate is identity and not odds. The blight is in
    /// Kumiromi's gift and says so, so a worshipper of his has a route through Ashfen and a
    /// follower of any other god has none - not worse odds on the same one, no route, refused by
    /// name. The two builds are otherwise the same person with the same pack.
    ///
    /// Three things have to be true before the dice come out, and each is a real piece of Elin
    /// state rather than a quest flag: you follow him, he knows you (piety), and you have laid
    /// something real on his ground. The first is who you are, the second is how you have played,
    /// and the third costs an object permanently.
    ///
    /// Whose matter the blight is has to be *learned*. Wyn keeps the shrine and knows; the player
    /// walks in knowing only that the field has failed, which they can see. So the faith route has
    /// a door into it from the information family, and a devout player who never asks anybody
    /// anything does not get it for free.
    /// </summary>
    public sealed class BlightedFieldSituation
    {
        public const string ArchetypeId = "blighted_field";

        private BlightedFieldSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>Holds the land, and stands to lose the year.</summary>
        public EntityId StewardId { get; private set; }

        /// <summary>Keeps the field shrine. Knows whose matter the blight is; nobody else says so.</summary>
        public EntityId ShrineKeeperId { get; private set; }

        /// <summary>The sown ground itself. Deliberately not an object in anybody's keeping.</summary>
        public EntityId FieldId { get; private set; }

        /// <summary>"The blight is in the seed corn." Subject is the field.</summary>
        public EntityId BlightId { get; private set; }

        /// <summary>"Lifting the blight is in Kumiromi's gift, and this is what he asks."</summary>
        public EntityId SacredMatterId { get; private set; }

        /// <summary>"The shrine is Kumiromi's ground." What makes an offering possible there.</summary>
        public EntityId SacredGroundId { get; private set; }

        public EntityId HamletZoneId { get; private set; }

        public EntityId ShrineZoneId { get; private set; }

        /// <summary>The god of the harvest, as the situation names him.</summary>
        public const string Harvest = "Kumiromi";

        /// <summary>
        /// What Kumiromi asks of whoever asks him: his own worship, piety 20, and 15 orens' worth
        /// on his ground. All three are refusals rather than penalties.
        /// </summary>
        public static readonly DevotionSpec Blight = new DevotionSpec(Harvest, 20, 15);

        public static BlightedFieldSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId hamlet, GameTime now)
        {
            BlightedFieldSituation situation = new BlightedFieldSituation
            {
                HamletZoneId = hamlet,
                ShrineZoneId = world.NewId("zone"),
                FieldId = world.NewId("place")
            };

            world.Registry.Add(new NarrativeSite(hamlet, "Ashfen", "village"));
            world.Registry.Add(new NarrativeSite(situation.ShrineZoneId, "the field shrine", "shrine"));

            NarrativeNpc steward = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Odren")
            {
                Occupation = "steward",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc keeper = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Wyn")
            {
                Occupation = "shrine keeper",
                Importance = NarrativeImportance.Known
            });

            situation.StewardId = steward.Id;
            situation.ShrineKeeperId = keeper.Id;

            // Nobody is hiding anything here either. What the player lacks is not a secret being
            // kept from them, it is a thing they have not thought to ask about.
            steward.Personality.Honesty = 0.9;
            keeper.Personality.Honesty = 0.95;

            stager.StageCharacter(steward.Id, new CharacterBlueprint(steward.Name)
                    .With(VanillaAttribute.Will, 11).With(VanillaAttribute.Strength, 12),
                hamlet);
            stager.StageCharacter(keeper.Id, new CharacterBlueprint(keeper.Name)
                    .With(VanillaAttribute.Learning, 12).With(VanillaSkill.Faith, 14),
                situation.ShrineZoneId);

            // -- what is true -----------------------------------------------------------------
            // The field is Odren's, so the blessing credits the man whose year it is rather than
            // whoever happened to be standing at the shrine.
            world.Knowledge.AddFact(new Fact(
                world.NewId("fact"), steward.Id, FactPredicates.Possesses, situation.FieldId,
                "the sown field", TruthState.True));

            Fact blight = new Fact(
                world.NewId("fact"), situation.FieldId, FactPredicates.Damaged, EntityId.None,
                "the blight in Ashfen's field", TruthState.True);
            world.Knowledge.AddFact(blight);
            situation.BlightId = blight.Id;

            Fact sacredMatter = new Fact(
                world.NewId("fact"), situation.FieldId, FactPredicates.SacredTo, EntityId.None,
                Blight.ToFactValue(), TruthState.True);
            world.Knowledge.AddFact(sacredMatter);
            situation.SacredMatterId = sacredMatter.Id;

            // The ground asks for nothing of its own. An altar is a place you can give at, not a
            // second set of conditions on top of the matter's.
            Fact sacredGround = new Fact(
                world.NewId("fact"), situation.ShrineZoneId, FactPredicates.SacredTo, EntityId.None,
                new DevotionSpec(Harvest).ToFactValue(), TruthState.True);
            world.Knowledge.AddFact(sacredGround);
            situation.SacredGroundId = sacredGround.Id;

            // -- who knows what ---------------------------------------------------------------
            // A failed field is not news anybody has to be told; the player can see it. Whose
            // matter it is, is Wyn's to say.
            world.Knowledge.Teach(steward.Id, blight.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(keeper.Id, blight.Id, KnowledgeSource.Witnessed, 1.0, now, true);
            world.Knowledge.Teach(player, blight.Id, KnowledgeSource.Witnessed, 1.0, now, true);
            world.Knowledge.Teach(keeper.Id, sacredMatter.Id, KnowledgeSource.Participant, 1.0, now, true);

            steward.Goals.Add(new NpcGoal("bring_in_a_harvest", situation.FieldId, 85));
            keeper.Goals.Add(new NpcGoal("keep_the_shrine", situation.ShrineZoneId, 60));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 30,
                Importance = 50,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(steward.Id);
            thread.ParticipantIds.Add(keeper.Id);
            thread.FactIds.Add(blight.Id);
            thread.FactIds.Add(sacredMatter.Id);
            thread.FactIds.Add(sacredGround.Id);
            thread.SiteIds.Add(hamlet);
            thread.SiteIds.Add(situation.ShrineZoneId);
            thread.OpenQuestions.Add("Whose matter is the blight in Ashfen's field?");

            thread.Escalation.Add(new EscalationStep("sowing_closes", 6, "The sowing season closes on Ashfen."));
            thread.Escalation.Add(new EscalationStep("hamlet_leaves", 14, "Ashfen starts to empty."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        /// <summary>
        /// What the player is carrying: three things worth three different amounts, so that what
        /// an offering is worth is something a test can vary without changing anything else.
        ///
        /// Staged apart from the hamlet because it is the player's state rather than the world's,
        /// and because the tests need to take pieces of it away.
        /// </summary>
        public void StockThePlayer(NarrativeWorldState world, ISituationStager stager, EntityId player)
        {
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a copper charm", "trinket", 4, "charm"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a handful of seed corn", "seed", 12, "seed"));
            stager.StageItem(player, new ItemDescriptor(world.NewId("item"), "a basket of first fruits", "food", 40, "fruit"));
        }
    }
}
