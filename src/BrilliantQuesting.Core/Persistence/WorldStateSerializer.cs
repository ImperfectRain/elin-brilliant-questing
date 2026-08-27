using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Persistence
{
    /// <summary>
    /// Reads and writes the whole procedural database.
    ///
    /// Two rules shape the format. Nothing is keyed on a display name - only on
    /// <see cref="EntityId"/> - so renaming a character or losing a vanilla instance cannot orphan
    /// history. And events are restored without re-dispatching them: replaying a save must not
    /// re-apply fifty hours of affinity changes.
    /// </summary>
    public static class WorldStateSerializer
    {
        public static string Save(NarrativeWorldState world, bool indented = true)
        {
            return ToJson(world).ToJson(indented);
        }

        public static JsonValue ToJson(NarrativeWorldState world)
        {
            JsonValue root = JsonValue.Object()
                .Set("schemaVersion", NarrativeWorldState.CurrentSchemaVersion)
                .Set("worldSeed", world.WorldSeed.ToString())
                .Set("rngState", world.Rng.State.ToString());

            JsonValue counters = JsonValue.Object();
            foreach (KeyValuePair<string, ulong> counter in world.Ids.Counters)
            {
                counters.Set(counter.Key, counter.Value);
            }

            root.Set("idCounters", counters);
            root.Set("npcs", NpcsToJson(world));
            root.Set("organizations", OrganizationsToJson(world));
            root.Set("sites", SitesToJson(world));
            root.Set("events", EventsToJson(world));
            root.Set("facts", FactsToJson(world));
            root.Set("beliefs", BeliefsToJson(world));
            root.Set("memories", MemoriesToJson(world));
            root.Set("relationships", RelationshipsToJson(world));
            root.Set("threads", ThreadsToJson(world));
            return root;
        }

        public static NarrativeWorldState Load(string json)
        {
            JsonValue root = SaveMigrations.Migrate(JsonValue.Parse(json), NarrativeWorldState.CurrentSchemaVersion);
            return FromJson(root);
        }

        public static NarrativeWorldState FromJson(JsonValue root)
        {
            NarrativeWorldState world = new NarrativeWorldState(ulong.Parse(root.GetString("worldSeed", "0")));
            world.SchemaVersion = root.GetInt("schemaVersion", NarrativeWorldState.CurrentSchemaVersion);
            world.Rng.RestoreState(ulong.Parse(root.GetString("rngState", "0")));

            JsonValue counters = root["idCounters"];
            if (counters != null)
            {
                foreach (KeyValuePair<string, JsonValue> counter in counters.Members)
                {
                    world.Ids.Restore(counter.Key, (ulong)counter.Value.NumberValue);
                }
            }

            ReadNpcs(world, root);
            ReadOrganizations(world, root);
            ReadSites(world, root);
            ReadEvents(world, root);
            ReadFacts(world, root);
            ReadBeliefs(world, root);
            ReadMemories(world, root);
            ReadRelationships(world, root);
            ReadThreads(world, root);
            return world;
        }

        // -- write ---------------------------------------------------------------------------

        private static JsonValue NpcsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                JsonValue personality = JsonValue.Object()
                    .Set("greed", npc.Personality.Greed)
                    .Set("mercy", npc.Personality.Mercy)
                    .Set("courage", npc.Personality.Courage)
                    .Set("honesty", npc.Personality.Honesty)
                    .Set("ambition", npc.Personality.Ambition)
                    .Set("loyalty", npc.Personality.Loyalty)
                    .Set("sociability", npc.Personality.Sociability)
                    .Set("curiosity", npc.Personality.Curiosity)
                    .Set("vengefulness", npc.Personality.Vengefulness);

                JsonValue goals = JsonValue.Array();
                foreach (Goal goal in npc.Goals)
                {
                    goals.Add(JsonValue.Object()
                        .Set("kind", goal.Kind)
                        .Set("subject", goal.Subject.Value)
                        .Set("weight", goal.Weight)
                        .Set("satisfied", goal.Satisfied));
                }

                array.Add(JsonValue.Object()
                    .Set("id", npc.Id.Value)
                    .Set("name", npc.Name)
                    .Set("charaRef", npc.VanillaCharaRef)
                    .Set("occupation", npc.Occupation)
                    .Set("homeSite", npc.HomeSiteId.Value)
                    .Set("importance", (int)npc.Importance)
                    .Set("alive", npc.Alive)
                    .Set("lastSimulated", npc.LastSimulatedAt.TotalMinutes)
                    .Set("personality", personality)
                    .Set("goals", goals)
                    .Set("organizations", Ids(npc.OrganizationIds)));
            }

            return array;
        }

        private static JsonValue OrganizationsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (Organization organization in world.Registry.Organizations.Values)
            {
                array.Add(JsonValue.Object()
                    .Set("id", organization.Id.Value)
                    .Set("name", organization.Name)
                    .Set("type", organization.Type)
                    .Set("leader", organization.LeaderId.Value)
                    .Set("wealth", organization.Wealth)
                    .Set("legitimacy", organization.Legitimacy)
                    .Set("aggression", organization.Aggression)
                    .Set("members", Ids(organization.MemberIds))
                    .Set("sites", Ids(organization.SiteIds)));
            }

            return array;
        }

        private static JsonValue SitesToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (NarrativeSite site in world.Registry.Sites.Values)
            {
                array.Add(JsonValue.Object()
                    .Set("id", site.Id.Value)
                    .Set("name", site.Name)
                    .Set("siteType", site.SiteType)
                    .Set("zoneRef", site.VanillaZoneRef)
                    .Set("controller", site.ControllingOrganizationId.Value)
                    .Set("danger", site.DangerLevel)
                    .Set("persistence", (int)site.Persistence)
                    .Set("seed", site.GenerationSeed.ToString())
                    .Set("occupants", Ids(site.OccupantIds))
                    .Set("objects", Ids(site.ImportantObjectIds)));
            }

            return array;
        }

        private static JsonValue EventsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (WorldEvent worldEvent in world.Ledger.Events)
            {
                array.Add(JsonValue.Object()
                    .Set("id", worldEvent.Id.Value)
                    .Set("type", worldEvent.Type.ToString())
                    .Set("actor", worldEvent.Actor.Value)
                    .Set("target", worldEvent.Target.Value)
                    .Set("time", worldEvent.Time.TotalMinutes)
                    .Set("magnitude", worldEvent.Magnitude)
                    .Set("zone", worldEvent.Zone.Value)
                    .Set("related", Ids(worldEvent.Related))
                    .Set("witnesses", Ids(worldEvent.Witnesses))
                    .Set("evidence", Ids(worldEvent.Evidence))
                    .Set("tags", Strings(worldEvent.Tags))
                    .Set("thread", worldEvent.ThreadId.Value));
            }

            return array;
        }

        private static JsonValue FactsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                array.Add(JsonValue.Object()
                    .Set("id", fact.Id.Value)
                    .Set("subject", fact.Subject.Value)
                    .Set("predicate", fact.Predicate)
                    .Set("object", fact.Object.Value)
                    .Set("value", fact.Value)
                    .Set("truth", fact.Truth.ToString())
                    .Set("secrecy", fact.Secrecy)
                    .Set("originEvent", fact.OriginEvent.Value)
                    .Set("evidence", Ids(fact.EvidenceIds)));
            }

            return array;
        }

        private static JsonValue BeliefsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                foreach (EntityId knower in world.Knowledge.Knowers(fact.Id))
                {
                    world.Knowledge.TryGetBelief(knower, fact.Id, out KnowledgeRecord record);
                    array.Add(JsonValue.Object()
                        .Set("knower", record.Knower.Value)
                        .Set("fact", record.FactId.Value)
                        .Set("source", record.Source.ToString())
                        .Set("confidence", record.Confidence)
                        .Set("learnedAt", record.LearnedAt.TotalMinutes)
                        .Set("canProve", record.CanProve)
                        .Set("toldBy", record.ToldBy.Value));
                }
            }

            return array;
        }

        private static JsonValue MemoriesToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (KeyValuePair<EntityId, List<MemoryRecord>> pair in world.Memories.All)
            {
                foreach (MemoryRecord memory in pair.Value)
                {
                    array.Add(JsonValue.Object()
                        .Set("id", memory.Id.Value)
                        .Set("owner", memory.Owner.Value)
                        .Set("about", memory.About.Value)
                        .Set("eventType", memory.EventType.ToString())
                        .Set("weight", (int)memory.Weight)
                        .Set("when", memory.When.TotalMinutes)
                        .Set("affinity", memory.AffinityContribution)
                        .Set("tag", memory.SummaryTag)
                        .Set("occurrences", memory.Occurrences));
                }
            }

            return array;
        }

        private static JsonValue RelationshipsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (KeyValuePair<EntityId, List<RelationshipEdge>> pair in world.Relationships.All)
            {
                foreach (RelationshipEdge edge in pair.Value)
                {
                    array.Add(JsonValue.Object()
                        .Set("from", edge.From.Value)
                        .Set("to", edge.To.Value)
                        .Set("kind", edge.Kind.ToString())
                        .Set("sentiment", edge.Sentiment));
                }
            }

            return array;
        }

        private static JsonValue ThreadsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (NarrativeThread thread in world.Threads)
            {
                JsonValue steps = JsonValue.Array();
                foreach (EscalationStep step in thread.Escalation)
                {
                    steps.Add(JsonValue.Object()
                        .Set("id", step.Id)
                        .Set("dayOffset", step.DayOffset)
                        .Set("description", step.Description));
                }

                array.Add(JsonValue.Object()
                    .Set("id", thread.Id.Value)
                    .Set("archetype", thread.ArchetypeId)
                    .Set("originEvent", thread.OriginEventId.Value)
                    .Set("createdAt", thread.CreatedAt.TotalMinutes)
                    .Set("lastAdvancedAt", thread.LastAdvancedAt.TotalMinutes)
                    .Set("tension", thread.Tension)
                    .Set("importance", thread.Importance)
                    .Set("state", thread.State.ToString())
                    .Set("resolution", thread.Resolution)
                    .Set("participants", Ids(thread.ParticipantIds))
                    .Set("sites", Ids(thread.SiteIds))
                    .Set("facts", Ids(thread.FactIds))
                    .Set("openQuestions", Strings(thread.OpenQuestions))
                    .Set("escalation", steps)
                    .Set("completedSteps", Strings(thread.CompletedSteps)));
            }

            return array;
        }

        // -- read ----------------------------------------------------------------------------

        private static void ReadNpcs(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("npcs"))
            {
                NarrativeNpc npc = new NarrativeNpc(EntityId.Parse(json.GetString("id")), json.GetString("name"))
                {
                    VanillaCharaRef = json.GetString("charaRef"),
                    Occupation = json.GetString("occupation"),
                    HomeSiteId = EntityId.Parse(json.GetString("homeSite")),
                    Importance = (NarrativeImportance)json.GetInt("importance"),
                    Alive = json.GetBool("alive", true),
                    LastSimulatedAt = new GameTime(json.GetLong("lastSimulated"))
                };

                JsonValue personality = json["personality"];
                if (personality != null)
                {
                    npc.Personality.Greed = personality.GetNumber("greed", 0.5);
                    npc.Personality.Mercy = personality.GetNumber("mercy", 0.5);
                    npc.Personality.Courage = personality.GetNumber("courage", 0.5);
                    npc.Personality.Honesty = personality.GetNumber("honesty", 0.5);
                    npc.Personality.Ambition = personality.GetNumber("ambition", 0.5);
                    npc.Personality.Loyalty = personality.GetNumber("loyalty", 0.5);
                    npc.Personality.Sociability = personality.GetNumber("sociability", 0.5);
                    npc.Personality.Curiosity = personality.GetNumber("curiosity", 0.5);
                    npc.Personality.Vengefulness = personality.GetNumber("vengefulness", 0.5);
                }

                foreach (JsonValue goalJson in json.GetArray("goals"))
                {
                    npc.Goals.Add(new Goal(goalJson.GetString("kind"), EntityId.Parse(goalJson.GetString("subject")), goalJson.GetInt("weight"))
                    {
                        Satisfied = goalJson.GetBool("satisfied")
                    });
                }

                foreach (JsonValue orgJson in json.GetArray("organizations"))
                {
                    npc.OrganizationIds.Add(EntityId.Parse(orgJson.StringValue));
                }

                world.Registry.Add(npc);
            }
        }

        private static void ReadOrganizations(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("organizations"))
            {
                Organization organization = new Organization(EntityId.Parse(json.GetString("id")), json.GetString("name"), json.GetString("type"))
                {
                    LeaderId = EntityId.Parse(json.GetString("leader")),
                    Wealth = json.GetInt("wealth"),
                    Legitimacy = json.GetInt("legitimacy"),
                    Aggression = json.GetInt("aggression")
                };

                foreach (JsonValue member in json.GetArray("members"))
                {
                    organization.MemberIds.Add(EntityId.Parse(member.StringValue));
                }

                foreach (JsonValue site in json.GetArray("sites"))
                {
                    organization.SiteIds.Add(EntityId.Parse(site.StringValue));
                }

                world.Registry.Add(organization);
            }
        }

        private static void ReadSites(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("sites"))
            {
                NarrativeSite site = new NarrativeSite(EntityId.Parse(json.GetString("id")), json.GetString("name"), json.GetString("siteType"))
                {
                    VanillaZoneRef = json.GetString("zoneRef"),
                    ControllingOrganizationId = EntityId.Parse(json.GetString("controller")),
                    DangerLevel = json.GetInt("danger"),
                    Persistence = (SitePersistence)json.GetInt("persistence"),
                    GenerationSeed = ulong.Parse(json.GetString("seed", "0"))
                };

                foreach (JsonValue occupant in json.GetArray("occupants"))
                {
                    site.OccupantIds.Add(EntityId.Parse(occupant.StringValue));
                }

                foreach (JsonValue thing in json.GetArray("objects"))
                {
                    site.ImportantObjectIds.Add(EntityId.Parse(thing.StringValue));
                }

                world.Registry.Add(site);
            }
        }

        private static void ReadEvents(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("events"))
            {
                WorldEvent worldEvent = new WorldEvent(
                    EntityId.Parse(json.GetString("id")),
                    (WorldEventType)Enum.Parse(typeof(WorldEventType), json.GetString("type")),
                    EntityId.Parse(json.GetString("actor")),
                    EntityId.Parse(json.GetString("target")),
                    new GameTime(json.GetLong("time")),
                    json.GetNumber("magnitude"),
                    EntityId.Parse(json.GetString("zone")),
                    IdList(json, "related"),
                    IdList(json, "witnesses"),
                    IdList(json, "evidence"),
                    StringList(json, "tags"),
                    EntityId.Parse(json.GetString("thread")));

                // Restored, not replayed: listeners must not re-apply historical consequences.
                world.Ledger.RestoreWithoutDispatch(worldEvent);
            }
        }

        private static void ReadFacts(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("facts"))
            {
                Fact fact = new Fact(
                    EntityId.Parse(json.GetString("id")),
                    EntityId.Parse(json.GetString("subject")),
                    json.GetString("predicate"),
                    EntityId.Parse(json.GetString("object")),
                    json.GetString("value", null),
                    (TruthState)Enum.Parse(typeof(TruthState), json.GetString("truth", "True")),
                    json.GetInt("secrecy"),
                    EntityId.Parse(json.GetString("originEvent")));

                foreach (JsonValue evidence in json.GetArray("evidence"))
                {
                    fact.EvidenceIds.Add(EntityId.Parse(evidence.StringValue));
                }

                world.Knowledge.AddFact(fact);
            }
        }

        private static void ReadBeliefs(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("beliefs"))
            {
                world.Knowledge.Teach(
                    EntityId.Parse(json.GetString("knower")),
                    EntityId.Parse(json.GetString("fact")),
                    (KnowledgeSource)Enum.Parse(typeof(KnowledgeSource), json.GetString("source", "Hearsay")),
                    json.GetNumber("confidence"),
                    new GameTime(json.GetLong("learnedAt")),
                    json.GetBool("canProve"),
                    EntityId.Parse(json.GetString("toldBy")));
            }
        }

        private static void ReadMemories(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("memories"))
            {
                MemoryRecord memory = new MemoryRecord(
                    EntityId.Parse(json.GetString("id")),
                    EntityId.Parse(json.GetString("owner")),
                    EntityId.Parse(json.GetString("about")),
                    (WorldEventType)Enum.Parse(typeof(WorldEventType), json.GetString("eventType")),
                    (MemoryWeight)json.GetInt("weight"),
                    new GameTime(json.GetLong("when")),
                    json.GetInt("affinity"),
                    json.GetString("tag"))
                {
                    Occurrences = json.GetInt("occurrences", 1)
                };

                world.Memories.Add(memory);
            }
        }

        private static void ReadRelationships(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("relationships"))
            {
                world.Relationships.Connect(
                    EntityId.Parse(json.GetString("from")),
                    EntityId.Parse(json.GetString("to")),
                    (RelationKind)Enum.Parse(typeof(RelationKind), json.GetString("kind")),
                    json.GetInt("sentiment"));
            }
        }

        private static void ReadThreads(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("threads"))
            {
                NarrativeThread thread = new NarrativeThread(
                    EntityId.Parse(json.GetString("id")),
                    json.GetString("archetype"),
                    new GameTime(json.GetLong("createdAt")))
                {
                    OriginEventId = EntityId.Parse(json.GetString("originEvent")),
                    LastAdvancedAt = new GameTime(json.GetLong("lastAdvancedAt")),
                    Tension = json.GetInt("tension"),
                    Importance = json.GetInt("importance"),
                    State = (ThreadState)Enum.Parse(typeof(ThreadState), json.GetString("state", "Latent")),
                    Resolution = json.GetString("resolution", null)
                };

                foreach (JsonValue participant in json.GetArray("participants"))
                {
                    thread.ParticipantIds.Add(EntityId.Parse(participant.StringValue));
                }

                foreach (JsonValue site in json.GetArray("sites"))
                {
                    thread.SiteIds.Add(EntityId.Parse(site.StringValue));
                }

                foreach (JsonValue fact in json.GetArray("facts"))
                {
                    thread.FactIds.Add(EntityId.Parse(fact.StringValue));
                }

                foreach (JsonValue question in json.GetArray("openQuestions"))
                {
                    thread.OpenQuestions.Add(question.StringValue);
                }

                foreach (JsonValue step in json.GetArray("escalation"))
                {
                    thread.Escalation.Add(new EscalationStep(step.GetString("id"), step.GetLong("dayOffset"), step.GetString("description")));
                }

                foreach (JsonValue completed in json.GetArray("completedSteps"))
                {
                    thread.CompletedSteps.Add(completed.StringValue);
                }

                world.Threads.Add(thread);
            }
        }

        // -- helpers -------------------------------------------------------------------------

        private static JsonValue Ids(IReadOnlyList<EntityId> ids)
        {
            JsonValue array = JsonValue.Array();
            for (int i = 0; i < ids.Count; i++)
            {
                array.Add(JsonValue.String(ids[i].Value));
            }

            return array;
        }

        private static JsonValue Strings(IReadOnlyList<string> values)
        {
            JsonValue array = JsonValue.Array();
            for (int i = 0; i < values.Count; i++)
            {
                array.Add(JsonValue.String(values[i]));
            }

            return array;
        }

        private static EntityId[] IdList(JsonValue json, string name)
        {
            IReadOnlyList<JsonValue> items = json.GetArray(name);
            EntityId[] ids = new EntityId[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                ids[i] = EntityId.Parse(items[i].StringValue);
            }

            return ids;
        }

        private static string[] StringList(JsonValue json, string name)
        {
            IReadOnlyList<JsonValue> items = json.GetArray(name);
            string[] values = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                values[i] = items[i].StringValue;
            }

            return values;
        }
    }
}
