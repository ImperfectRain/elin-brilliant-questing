using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The laboratory for the investigation route: a death nobody will explain.
    ///
    /// Its whole design constraint is that the only person who knows what happened is the person
    /// who did it, and they are not going to say. There is no witness to question, no rumour in
    /// circulation and nothing anybody is willing to volunteer, so every social verb in the
    /// library is a dead end here by construction. What there is instead is a body, a shop's
    /// sales ledger, a vial, and a room that still shows who was in it.
    ///
    /// That makes it the honest test of the action library's information half: if the case can be
    /// closed, it was closed by reading objects and places. Nothing was handed over.
    ///
    /// Three zones, because reaching evidence is half of investigating. The body is where it fell,
    /// the ledger is on the apothecary's shelf, and the vial is still in the poisoner's pocket at
    /// home - and no verb reaches through the world to fetch any of them.
    /// </summary>
    public sealed class UnexplainedDeathSituation
    {
        public const string ArchetypeId = "unexplained_death";

        private UnexplainedDeathSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        /// <summary>The dead. The body is still on them, which is what makes forensics possible.</summary>
        public EntityId VictimId { get; private set; }

        /// <summary>Who did it, at home, still carrying what they did it with.</summary>
        public EntityId PoisonerId { get; private set; }

        /// <summary>Sold the poison, wrote it down, and has no idea what it was for.</summary>
        public EntityId ApothecaryId { get; private set; }

        /// <summary>Standing to act on a proven case, and none at all on a suspicion.</summary>
        public EntityId GuardId { get; private set; }

        public EntityId CorpseId { get; private set; }

        public EntityId VialId { get; private set; }

        public EntityId LedgerId { get; private set; }

        /// <summary>"The victim is dead." Public, obvious, and useless on its own.</summary>
        public EntityId DeathFactId { get; private set; }

        /// <summary>"The victim was killed by nightshade." Only the body says so.</summary>
        public EntityId CauseFactId { get; private set; }

        /// <summary>"The poisoner has a vial of nightshade." The ledger says so; so does the vial.</summary>
        public EntityId SupplyFactId { get; private set; }

        /// <summary>"The poisoner killed the victim." True from the start. Nobody can show it yet.</summary>
        public EntityId KillFactId { get; private set; }

        /// <summary>Where the body is.</summary>
        public EntityId SceneZoneId { get; private set; }

        public EntityId ShopZoneId { get; private set; }

        public EntityId HomeZoneId { get; private set; }

        public static UnexplainedDeathSituation Create(
            NarrativeWorldState world, ISituationStager stager, EntityId player, EntityId scene, GameTime now)
        {
            UnexplainedDeathSituation situation = new UnexplainedDeathSituation
            {
                SceneZoneId = scene,
                ShopZoneId = world.NewId("zone"),
                HomeZoneId = world.NewId("zone")
            };

            world.Registry.Add(new NarrativeSite(scene, "the back room", "lodging"));
            world.Registry.Add(new NarrativeSite(situation.ShopZoneId, "the apothecary's shop", "shop"));
            world.Registry.Add(new NarrativeSite(situation.HomeZoneId, "the weaver's house", "house"));

            NarrativeNpc victim = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Sabren")
            {
                Occupation = "porter",
                Importance = NarrativeImportance.Known,
                Alive = false
            });
            NarrativeNpc poisoner = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Yeleth")
            {
                Occupation = "weaver",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc apothecary = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Nils")
            {
                Occupation = "apothecary",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc guard = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Rook")
            {
                Occupation = "guard",
                Importance = NarrativeImportance.Known
            });
            guard.Roles.Add(AuthorityPolicy.GuardRole);

            situation.VictimId = victim.Id;
            situation.PoisonerId = poisoner.Id;
            situation.ApothecaryId = apothecary.Id;
            situation.GuardId = guard.Id;

            // Nothing about this person invites a conversation, and that is the scenario.
            poisoner.Personality.Honesty = 0.1;
            poisoner.Personality.Courage = 0.6;
            apothecary.Personality.Sociability = 0.3;

            situation.CorpseId = world.NewId("item");
            situation.VialId = world.NewId("item");
            situation.LedgerId = world.NewId("item");

            ItemDescriptor corpse = new ItemDescriptor(situation.CorpseId, victim.Name + "'s corpse", "corpse", 0, "corpse");
            ItemDescriptor vial = new ItemDescriptor(situation.VialId, "small unlabelled vial", "drink", 90, "potion");
            ItemDescriptor ledger = new ItemDescriptor(situation.LedgerId, "sales ledger", "book", 20, "book");

            stager.StageCharacter(victim.Id, new CharacterBlueprint(victim.Name)
                    .With(VanillaAttribute.Endurance, 9)
                    .Carrying(corpse),
                scene);

            stager.StageCharacter(guard.Id, new CharacterBlueprint(guard.Name)
                    .With(VanillaAttribute.Will, 12).With(VanillaAttribute.Perception, 11),
                scene);

            stager.StageCharacter(apothecary.Id, new CharacterBlueprint(apothecary.Name)
                    .With(VanillaAttribute.Learning, 13).With(VanillaSkill.Alchemy, 9)
                    .Carrying(ledger),
                situation.ShopZoneId);

            stager.StageCharacter(poisoner.Id, new CharacterBlueprint(poisoner.Name)
                    .With(VanillaAttribute.Will, 13).With(VanillaAttribute.Perception, 12)
                    .Carrying(vial),
                situation.HomeZoneId);

            // -- what happened -------------------------------------------------------------
            // Recorded with no witnesses at all. The room is the only thing that saw it, which is
            // exactly what `track` reads.
            WorldEvent killing = world.Record(
                WorldEventType.Killed,
                poisoner.Id,
                victim.Id,
                now,
                magnitude: 0.9,
                zone: scene,
                related: new[] { situation.VialId },
                evidence: new[] { situation.VialId });

            Fact death = new Fact(world.NewId("fact"), victim.Id, FactPredicates.IsDead, EntityId.None, "dead", TruthState.True, secrecy: 0, originEvent: killing.Id);
            death.EvidenceIds.Add(situation.CorpseId);
            world.Knowledge.AddFact(death);
            situation.DeathFactId = death.Id;

            Fact cause = new Fact(world.NewId("fact"), victim.Id, FactPredicates.KilledBy, EntityId.None, "nightshade", TruthState.True, secrecy: 50, originEvent: killing.Id);
            cause.EvidenceIds.Add(situation.CorpseId);
            world.Knowledge.AddFact(cause);
            situation.CauseFactId = cause.Id;

            Fact supply = new Fact(world.NewId("fact"), poisoner.Id, FactPredicates.Possesses, situation.VialId, "a vial of nightshade", TruthState.True, secrecy: 30);
            supply.EvidenceIds.Add(situation.LedgerId);
            supply.EvidenceIds.Add(situation.VialId);
            world.Knowledge.AddFact(supply);
            situation.SupplyFactId = supply.Id;

            Fact killed = new Fact(world.NewId("fact"), poisoner.Id, FactPredicates.Killed, victim.Id, null, TruthState.True, secrecy: 80, originEvent: killing.Id);
            killed.EvidenceIds.Add(situation.VialId);
            world.Knowledge.AddFact(killed);
            situation.KillFactId = killed.Id;

            // -- who knows what ------------------------------------------------------------
            // The poisoner knows everything and will say none of it. The apothecary knows only
            // that they sold somebody a bottle. The guard knows a man is dead. And the player
            // knows what anybody who walks in would know: there is a body, and that is all.
            world.Knowledge.Teach(poisoner.Id, killed.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(poisoner.Id, supply.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(poisoner.Id, death.Id, KnowledgeSource.Participant, 1.0, now, false);
            world.Knowledge.Teach(apothecary.Id, supply.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(guard.Id, death.Id, KnowledgeSource.Witnessed, 1.0, now, false);
            world.Knowledge.Teach(player, death.Id, KnowledgeSource.Witnessed, 1.0, now, false);

            poisoner.Goals.Add(new NpcGoal("avoid_exposure", killed.Id, 95));
            guard.Goals.Add(new NpcGoal("close_the_case", victim.Id, 60));

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 40,
                Importance = 55,
                State = ThreadState.Active,
                OriginEventId = killing.Id
            };
            thread.ParticipantIds.Add(victim.Id);
            thread.ParticipantIds.Add(poisoner.Id);
            thread.ParticipantIds.Add(apothecary.Id);
            thread.ParticipantIds.Add(guard.Id);
            thread.FactIds.Add(death.Id);
            thread.FactIds.Add(cause.Id);
            thread.FactIds.Add(supply.Id);
            thread.FactIds.Add(killed.Id);
            thread.SiteIds.Add(scene);
            thread.SiteIds.Add(situation.ShopZoneId);
            thread.SiteIds.Add(situation.HomeZoneId);
            thread.OpenQuestions.Add("What killed " + victim.Name + "?");
            thread.OpenQuestions.Add("Who can be shown to have done it?");

            thread.Escalation.Add(new EscalationStep("body_removed", 3, "The body is taken away and buried."));
            thread.Escalation.Add(new EscalationStep("case_filed", 8, "The guard stops treating it as anything but a bad heart."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }
    }
}
