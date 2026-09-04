using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Obligations;
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

            // The adapter's identity map. Additive and optional: a save written before this
            // existed simply has no node, and loads with an empty map - which is exactly the
            // state it was already in - so no schema bump is owed.
            JsonValue refs = JsonValue.Object();
            foreach (KeyValuePair<EntityId, string> pair in world.ExternalRefs)
            {
                refs.Set(pair.Key.Value, pair.Value);
            }

            root.Set("externalRefs", refs);
            root.Set("lastRumorDay", world.LastRumorDay);
            root.Set("lastAmbientRemarkMinute", world.LastAmbientRemarkMinute);
            root.Set("npcs", NpcsToJson(world));
            root.Set("organizations", OrganizationsToJson(world));
            root.Set("sites", SitesToJson(world));
            root.Set("events", EventsToJson(world));
            root.Set("facts", FactsToJson(world));
            root.Set("beliefs", BeliefsToJson(world));
            root.Set("memories", MemoriesToJson(world));
            root.Set("relationships", RelationshipsToJson(world));
            root.Set("obligations", ObligationsToJson(world));
            root.Set("threads", ThreadsToJson(world));
            root.Set("absences", AbsencesToJson(world));
            root.Set("demands", DemandsToJson(world));
            root.Set("businesses", BusinessesToJson(world));
            return root;
        }

        public static NarrativeWorldState Load(string json)
        {
            return LoadWithDiagnostics(json).World;
        }

        public static WorldStateLoadResult LoadWithDiagnostics(string json)
        {
            JsonValue root = SaveMigrations.Migrate(JsonValue.Parse(json), NarrativeWorldState.CurrentSchemaVersion);
            return FromJsonWithDiagnostics(root);
        }

        public static NarrativeWorldState FromJson(JsonValue root)
        {
            return FromJsonWithDiagnostics(root).World;
        }

        public static WorldStateLoadResult FromJsonWithDiagnostics(JsonValue root)
        {
            NarrativeWorldState world = new NarrativeWorldState(ulong.Parse(root.GetString("worldSeed", "0")));
            List<SaveLoadDiagnostic> diagnostics = new List<SaveLoadDiagnostic>();
            world.SchemaVersion = root.GetInt("schemaVersion", NarrativeWorldState.CurrentSchemaVersion);
            world.Rng.RestoreState(ulong.Parse(root.GetString("rngState", "0")));

            world.LastRumorDay = root.GetLong("lastRumorDay", NarrativeWorldState.RumorsNeverCirculated);
            world.LastAmbientRemarkMinute = root.GetLong("lastAmbientRemarkMinute", NarrativeWorldState.NothingSaidYet);

            JsonValue refs = root["externalRefs"];
            if (refs != null)
            {
                foreach (KeyValuePair<string, JsonValue> pair in refs.Members)
                {
                    world.ExternalRefs[EntityId.Parse(pair.Key)] = refs.GetString(pair.Key);
                }
            }

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
            ReadObligations(world, root);
            ReadThreads(world, root, diagnostics);
            ReadAbsences(world, root);
            ReadDemands(world, root);
            ReadBusinesses(world, root);
            return new WorldStateLoadResult(world, diagnostics);
        }

        // -- write ---------------------------------------------------------------------------

        private static JsonValue NpcsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();

            // Every record, retired aliases included: an alias dropped from the save would take
            // the history written under its id with it on the next load.
            foreach (NarrativeNpc npc in world.Registry.AllNpcs.Values)
            {
                JsonValue personality = PersonalityToJson(npc.Personality);
                JsonValue problemSolving = ProblemSolvingToJson(npc.ProblemSolving);
                JsonValue sensitivities = SensitivitiesToJson(npc.Sensitivities);
                JsonValue contradiction = ContradictionToJson(npc.Contradiction);
                JsonValue quirk = QuirkToJson(npc.Quirk);
                JsonValue negativeSpace = NegativeSpaceToJson(npc.NegativeSpace);
                JsonValue values = ValuesToJson(npc.Values);
                JsonValue needs = NeedsToJson(npc.Needs);
                JsonValue emotions = EmotionsToJson(npc.Emotions);

                JsonValue goals = JsonValue.Array();
                foreach (NpcGoal goal in npc.Goals)
                {
                    goals.Add(JsonValue.Object()
                        .Set("kind", goal.Kind)
                        .Set("subject", goal.Subject.Value)
                        .Set("weight", goal.Weight)
                        .Set("reason", goal.Reason)
                        .Set("satisfied", goal.Satisfied));
                }

                array.Add(JsonValue.Object()
                    .Set("id", npc.Id.Value)
                    .Set("name", npc.Name)
                    .Set("charaRef", npc.VanillaCharaRef)
                    .Set("aliasOf", npc.AliasOf.Value)
                    .Set("occupation", npc.Occupation)
                    .Set("roles", Strings(npc.Roles))
                    .Set("homeSite", npc.HomeSiteId.Value)
                    .Set("importance", (int)npc.Importance)
                    .Set("alive", npc.Alive)
                    .Set("lastSimulated", npc.LastSimulatedAt.TotalMinutes)
                    .Set("personality", personality)
                    .Set("problemSolving", problemSolving)
                    .Set("sensitivities", sensitivities)
                    .Set("contradiction", contradiction)
                    .Set("quirk", quirk)
                    .Set("negativeSpace", negativeSpace)
                    .Set("values", values)
                    .Set("needs", needs)
                    .Set("emotions", emotions)
                    .Set("goals", goals)
                    .Set("organizations", Ids(npc.OrganizationIds)));
            }

            return array;
        }

        /// <summary>
        /// Who is away. Additive and optional, like the identity map above: a save written before
        /// absences existed has no node and loads with nobody away, which is the state it was in.
        ///
        /// What is deliberately *not* written is whether Elin currently agrees. That is a fact
        /// about a session, not about the world, and a save that carried it would come back
        /// insisting an absence had already been applied to a game that has since put everybody
        /// back where it last wrote them. Reconciliation re-derives it on load instead.
        /// </summary>
        private static JsonValue AbsencesToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (ActorAbsence absence in world.Absences.Active)
            {
                array.Add(JsonValue.Object()
                    .Set("actor", absence.ActorId.Value)
                    .Set("grade", (int)absence.Grade)
                    .Set("reason", absence.Reason)
                    .Set("began", absence.BeganAt.TotalMinutes)
                    .Set("expectedReturn", absence.ExpectedReturn.TotalMinutes)
                    .Set("awayZone", absence.AwayZoneId.Value)
                    .Set("homeZone", absence.HomeZoneId.Value));
            }

            return array;
        }

        private static JsonValue DemandsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (LocalDemandPressure pressure in world.Demands.Pressures)
            {
                array.Add(JsonValue.Object()
                    .Set("place", pressure.PlaceId.Value)
                    .Set("category", pressure.Category)
                    .Set("severity", pressure.Severity)
                    .Set("began", pressure.BeganAt.TotalMinutes)
                    .Set("expectedRelief", pressure.ExpectedReliefAt.TotalMinutes)
                    .Set("sourceFact", pressure.SourceFactId.Value));
            }

            return array;
        }

        private static JsonValue BusinessesToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (BusinessRecord business in world.Businesses.Records)
            {
                array.Add(JsonValue.Object()
                    .Set("id", business.BusinessId.Value)
                    .Set("place", business.PlaceId.Value)
                    .Set("operator", business.OperatorId.Value)
                    .Set("state", (int)business.State)
                    .Set("began", business.BeganAt.TotalMinutes)
                    .Set("lastChanged", business.LastChangedAt.TotalMinutes)
                    .Set("causeFact", business.CauseFactId.Value)
                    .Set("replacementOperator", business.ReplacementOperatorId.Value)
                    .Set("inheritedBy", business.InheritedById.Value));
            }

            return array;
        }

        private static JsonValue OrganizationsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (Organization organization in world.Registry.Organizations.Values)
            {
                JsonValue goals = JsonValue.Array();
                foreach (OrganizationGoal goal in organization.Goals)
                {
                    goals.Add(JsonValue.Object()
                        .Set("kind", goal.Kind)
                        .Set("subject", goal.Subject.Value)
                        .Set("weight", goal.Weight)
                        .Set("progress", goal.Progress)
                        .Set("satisfied", goal.Satisfied));
                }

                array.Add(JsonValue.Object()
                    .Set("id", organization.Id.Value)
                    .Set("name", organization.Name)
                    .Set("type", organization.Type)
                    .Set("leader", organization.LeaderId.Value)
                    .Set("wealth", organization.Wealth)
                    .Set("legitimacy", organization.Legitimacy)
                    .Set("aggression", organization.Aggression)
                    .Set("lastActed", organization.LastActedAt.TotalMinutes)
                    .Set("goals", goals)
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
                    .Set("restricted", site.Restricted)
                    .Set("established", site.Established)
                    .Set("establishedAt", site.EstablishedAt.TotalMinutes)
                    .Set("approaches", ApproachesToJson(site))
                    .Set("occupants", Ids(site.OccupantIds))
                    .Set("objects", Ids(site.ImportantObjectIds))
                    .Set("admitted", Ids(site.AdmittedIds)));
            }

            return array;
        }

        private static JsonValue ApproachesToJson(NarrativeSite site)
        {
            JsonValue array = JsonValue.Array();
            foreach (SiteApproach approach in site.Approaches)
            {
                array.Add(JsonValue.Object()
                    .Set("action", approach.ActionId)
                    .Set("admitted", approach.NeedsAdmission));
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
                    .Set("distortionOf", fact.DistortionOf.Value)
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
                        .Set("proofs", Proofs(record.Proofs))
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

        private static JsonValue ObligationsToJson(NarrativeWorldState world)
        {
            JsonValue array = JsonValue.Array();
            foreach (SocialObligation obligation in world.Obligations.Records)
            {
                array.Add(JsonValue.Object()
                    .Set("id", obligation.Id.Value)
                    .Set("kind", obligation.Kind.ToString())
                    .Set("debtor", obligation.Debtor.Value)
                    .Set("creditor", obligation.Creditor.Value)
                    .Set("subject", obligation.Subject.Value)
                    .Set("purpose", obligation.Purpose)
                    .Set("createdAt", obligation.CreatedAt.TotalMinutes)
                    .Set("sourceEvent", obligation.SourceEventId.Value)
                    .Set("strength", obligation.Strength)
                    .Set("status", obligation.Status.ToString())
                    .Set("resolvedAt", obligation.ResolvedAt.TotalMinutes));
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

                JsonValue storyletFirings = JsonValue.Array();
                foreach (StoryletFiring firing in thread.StoryletFirings)
                {
                    JsonValue roles = JsonValue.Object();
                    foreach (KeyValuePair<string, EntityId> role in firing.RoleBindings)
                    {
                        roles.Set(role.Key, role.Value.Value);
                    }

                    storyletFirings.Add(JsonValue.Object()
                        .Set("storylet", firing.StoryletId)
                        .Set("focusFact", firing.FocusFactId.Value)
                        .Set("firedAt", firing.FiredAt.TotalMinutes)
                        .Set("roles", roles)
                        .Set("beats", Strings(firing.BeatIds))
                        .Set("consequenceHooks", Strings(firing.ConsequenceHookIds)));
                }

                JsonValue recoveryRoutes = JsonValue.Array();
                foreach (RecoveryRoute route in thread.RecoveryRoutes)
                {
                    recoveryRoutes.Add(JsonValue.Object()
                        .Set("worstOutcome", route.WorstOutcome)
                        .Set("action", route.ActionId)
                        .Set("price", route.Price)
                        .Set("uncertainty", route.Uncertainty)
                        .Set("restores", route.Restores));
                }

                array.Add(JsonValue.Object()
                    .Set("id", thread.Id.Value)
                    .Set("archetype", thread.ArchetypeId)
                    .Set("originEvent", thread.OriginEventId.Value)
                    .Set("parentThread", thread.ParentThreadId.Value)
                    .Set("successorThread", thread.SuccessorThreadId.Value)
                    .Set("createdAt", thread.CreatedAt.TotalMinutes)
                    .Set("lastAdvancedAt", thread.LastAdvancedAt.TotalMinutes)
                    .Set("tension", thread.Tension)
                    .Set("importance", thread.Importance)
                    .Set("state", thread.State.ToString())
                    .Set("resolution", thread.Resolution)
                    .Set("lifecycleReason", thread.LifecycleReason)
                    .Set("participants", Ids(thread.ParticipantIds))
                    .Set("sites", Ids(thread.SiteIds))
                    .Set("facts", Ids(thread.FactIds))
                    .Set("openQuestions", Strings(thread.OpenQuestions))
                    .Set("generationCauses", Strings(thread.GenerationCauses))
                    .Set("recoveryRoutes", recoveryRoutes)
                    .Set("escalation", steps)
                    .Set("completedSteps", Strings(thread.CompletedSteps))
                    .Set("storyletFirings", storyletFirings));
            }

            return array;
        }

        // -- read ----------------------------------------------------------------------------

        /// <summary>
        /// The retired placeholder occupation, dropped on load.
        ///
        /// Every vanilla townsperson BQ registered before BQ-144 was written into the save as
        /// doing this for a living. It was never something the game said - it was the mod having
        /// nowhere to put "we did not ask" - and identity is a live read now, so the saved claim
        /// degrades to unknown and is re-read rather than kept in sync (VS 4.4). Anybody whose
        /// occupation a situation actually authored keeps it.
        /// </summary>
        private const string RetiredPlaceholderOccupation = "local";

        private static string Occupation(string saved)
        {
            return saved == RetiredPlaceholderOccupation ? string.Empty : saved;
        }

        private static void ReadNpcs(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("npcs"))
            {
                NarrativeNpc npc = new NarrativeNpc(EntityId.Parse(json.GetString("id")), json.GetString("name"))
                {
                    VanillaCharaRef = json.GetString("charaRef"),
                    AliasOf = EntityId.Parse(json.GetString("aliasOf")),
                    Occupation = Occupation(json.GetString("occupation")),
                    HomeSiteId = EntityId.Parse(json.GetString("homeSite")),
                    Importance = (NarrativeImportance)json.GetInt("importance"),
                    Alive = json.GetBool("alive", true),
                    LastSimulatedAt = new GameTime(json.GetLong("lastSimulated"))
                };

                foreach (string role in StringList(json, "roles"))
                {
                    npc.Roles.Add(role);
                }

                JsonValue personality = json["personality"];
                if (personality != null)
                {
                    ReadPersonality(npc.Personality, personality);
                }

                JsonValue problemSolving = json["problemSolving"];
                if (problemSolving != null)
                {
                    ReadProblemSolving(npc.ProblemSolving, problemSolving);
                }

                JsonValue sensitivities = json["sensitivities"];
                if (sensitivities != null)
                {
                    ReadSensitivities(npc.Sensitivities, sensitivities);
                }

                JsonValue contradiction = json["contradiction"];
                if (contradiction != null)
                {
                    ReadContradiction(npc.Contradiction, contradiction);
                }

                JsonValue quirk = json["quirk"];
                if (quirk != null)
                {
                    ReadQuirk(npc.Quirk, quirk);
                }

                JsonValue negativeSpace = json["negativeSpace"];
                if (negativeSpace != null)
                {
                    ReadNegativeSpace(npc.NegativeSpace, negativeSpace);
                }

                JsonValue values = json["values"];
                if (values != null)
                {
                    ReadValues(npc.Values, values);
                }

                JsonValue needs = json["needs"];
                if (needs != null)
                {
                    ReadNeeds(npc.Needs, needs);
                }

                JsonValue emotions = json["emotions"];
                if (emotions != null)
                {
                    ReadEmotions(npc.Emotions, emotions);
                }

                foreach (JsonValue goalJson in json.GetArray("goals"))
                {
                    npc.Goals.Add(new NpcGoal(
                        goalJson.GetString("kind"),
                        EntityId.Parse(goalJson.GetString("subject")),
                        goalJson.GetInt("weight"),
                        goalJson.GetString("reason"))
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

        private static void ReadAbsences(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("absences"))
            {
                world.Absences.Restore(new ActorAbsence(
                    EntityId.Parse(json.GetString("actor")),
                    (AbsenceGrade)json.GetInt("grade"),
                    json.GetString("reason"),
                    new GameTime(json.GetLong("began")),
                    new GameTime(json.GetLong("expectedReturn", ActorAbsence.NoScheduledReturn.TotalMinutes)),
                    EntityId.Parse(json.GetString("awayZone")),
                    EntityId.Parse(json.GetString("homeZone"))));
            }
        }

        private static void ReadDemands(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("demands"))
            {
                world.Demands.Restore(new LocalDemandPressure(
                    EntityId.Parse(json.GetString("place")),
                    json.GetString("category"),
                    json.GetInt("severity"),
                    new GameTime(json.GetLong("began")),
                    new GameTime(json.GetLong("expectedRelief")),
                    EntityId.Parse(json.GetString("sourceFact"))));
            }
        }

        private static void ReadBusinesses(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("businesses"))
            {
                world.Businesses.Restore(new BusinessRecord(
                    EntityId.Parse(json.GetString("id")),
                    EntityId.Parse(json.GetString("place")),
                    EntityId.Parse(json.GetString("operator")),
                    (BusinessContinuityState)json.GetInt("state"),
                    new GameTime(json.GetLong("began")),
                    new GameTime(json.GetLong("lastChanged")),
                    EntityId.Parse(json.GetString("causeFact")),
                    EntityId.Parse(json.GetString("replacementOperator")),
                    EntityId.Parse(json.GetString("inheritedBy"))));
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
                    Aggression = json.GetInt("aggression"),
                    LastActedAt = new GameTime(json.GetLong("lastActed"))
                };

                foreach (JsonValue goalJson in json.GetArray("goals"))
                {
                    organization.Goals.Add(new OrganizationGoal(
                        goalJson.GetString("kind"),
                        EntityId.Parse(goalJson.GetString("subject")),
                        goalJson.GetInt("weight"))
                    {
                        Progress = goalJson.GetInt("progress"),
                        Satisfied = goalJson.GetBool("satisfied")
                    });
                }

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
                    GenerationSeed = ulong.Parse(json.GetString("seed", "0")),

                    // Additive and optional: a save written before locked places existed has no
                    // node here, reads back as an open site, and behaves exactly as it did.
                    Restricted = json.GetBool("restricted"),

                    // Likewise for genesis (BQ-087). A site an archetype wrote down directly was
                    // never generated, so reading back as not-established is the truth about it,
                    // and the only thing the flag gates is generating a second place over one.
                    Established = json.GetBool("established"),
                    EstablishedAt = new GameTime(json.GetLong("establishedAt"))
                };

                foreach (JsonValue approach in json.GetArray("approaches"))
                {
                    site.Approaches.Add(new SiteApproach(
                        approach.GetString("action"),
                        approach.GetBool("admitted")));
                }

                foreach (JsonValue occupant in json.GetArray("occupants"))
                {
                    site.OccupantIds.Add(EntityId.Parse(occupant.StringValue));
                }

                foreach (JsonValue thing in json.GetArray("objects"))
                {
                    site.ImportantObjectIds.Add(EntityId.Parse(thing.StringValue));
                }

                foreach (JsonValue admitted in json.GetArray("admitted"))
                {
                    site.Admit(EntityId.Parse(admitted.StringValue));
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
                    EntityId.Parse(json.GetString("originEvent")))
                {
                    DistortionOf = EntityId.Parse(json.GetString("distortionOf"))
                };

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
                    ProofList(json, "proofs"),
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

        private static void ReadObligations(NarrativeWorldState world, JsonValue root)
        {
            foreach (JsonValue json in root.GetArray("obligations"))
            {
                SocialObligation obligation = new SocialObligation(
                    EntityId.Parse(json.GetString("id")),
                    (SocialObligationKind)Enum.Parse(typeof(SocialObligationKind), json.GetString("kind", "Favor")),
                    EntityId.Parse(json.GetString("debtor")),
                    EntityId.Parse(json.GetString("creditor")),
                    EntityId.Parse(json.GetString("subject")),
                    json.GetString("purpose"),
                    new GameTime(json.GetLong("createdAt")),
                    EntityId.Parse(json.GetString("sourceEvent")),
                    json.GetInt("strength", 1));

                obligation.Restore(
                    (SocialObligationStatus)Enum.Parse(typeof(SocialObligationStatus), json.GetString("status", "Open")),
                    new GameTime(json.GetLong("resolvedAt")));
                world.Obligations.Restore(obligation);
            }
        }

        private static void ReadThreads(NarrativeWorldState world, JsonValue root, List<SaveLoadDiagnostic> diagnostics)
        {
            int index = 0;
            foreach (JsonValue json in root.GetArray("threads"))
            {
                try
                {
                    NarrativeThread thread = ReadThread(json);
                    string quarantineReason = ThreadQuarantineReason(world, thread);
                    if (quarantineReason != null)
                    {
                        thread.State = ThreadState.Quarantined;
                        thread.LifecycleReason = quarantineReason;
                        diagnostics.Add(new SaveLoadDiagnostic(
                            "save.thread.quarantined",
                            "threads[" + index + "]",
                            quarantineReason));
                    }

                    world.Threads.Add(thread);
                }
                catch (Exception ex)
                {
                    NarrativeThread thread = QuarantinedThread(json, index, ex.Message);
                    world.Threads.Add(thread);
                    diagnostics.Add(new SaveLoadDiagnostic(
                        "save.thread.quarantined",
                        "threads[" + index + "]",
                        thread.LifecycleReason));
                }

                index++;
            }
        }

        private static NarrativeThread ReadThread(JsonValue json)
        {
            NarrativeThread thread = new NarrativeThread(
                EntityId.Parse(json.GetString("id")),
                json.GetString("archetype"),
                new GameTime(json.GetLong("createdAt")))
                {
                    OriginEventId = EntityId.Parse(json.GetString("originEvent")),
                    ParentThreadId = EntityId.Parse(json.GetString("parentThread")),
                    SuccessorThreadId = EntityId.Parse(json.GetString("successorThread")),
                    LastAdvancedAt = new GameTime(json.GetLong("lastAdvancedAt")),
                    Tension = json.GetInt("tension"),
                    Importance = json.GetInt("importance"),
                    State = (ThreadState)Enum.Parse(typeof(ThreadState), json.GetString("state", "Latent")),
                    Resolution = json.GetString("resolution", null),
                    LifecycleReason = json.GetString("lifecycleReason", null)
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

            foreach (JsonValue cause in json.GetArray("generationCauses"))
            {
                thread.GenerationCauses.Add(cause.StringValue);
            }

            foreach (JsonValue route in json.GetArray("recoveryRoutes"))
            {
                thread.RecoveryRoutes.Add(new RecoveryRoute(
                    route.GetString("worstOutcome"),
                    route.GetString("action"),
                    route.GetString("price"),
                    route.GetString("uncertainty"),
                    route.GetString("restores")));
            }

            foreach (JsonValue step in json.GetArray("escalation"))
            {
                thread.Escalation.Add(new EscalationStep(step.GetString("id"), step.GetLong("dayOffset"), step.GetString("description")));
            }

            foreach (JsonValue completed in json.GetArray("completedSteps"))
            {
                thread.CompletedSteps.Add(completed.StringValue);
            }

            foreach (JsonValue firingJson in json.GetArray("storyletFirings"))
            {
                StoryletFiring firing = new StoryletFiring(
                    firingJson.GetString("storylet"),
                    EntityId.Parse(firingJson.GetString("focusFact")),
                    new GameTime(firingJson.GetLong("firedAt")));

                JsonValue roles = firingJson["roles"];
                if (roles != null)
                {
                    foreach (KeyValuePair<string, JsonValue> role in roles.Members)
                    {
                        firing.RoleBindings[role.Key] = EntityId.Parse(role.Value.StringValue);
                    }
                }

                foreach (JsonValue beat in firingJson.GetArray("beats"))
                {
                    firing.BeatIds.Add(beat.StringValue);
                }

                foreach (JsonValue hook in firingJson.GetArray("consequenceHooks"))
                {
                    firing.ConsequenceHookIds.Add(hook.StringValue);
                }

                thread.StoryletFirings.Add(firing);
            }

            return thread;
        }

        private static NarrativeThread QuarantinedThread(JsonValue json, int index, string reason)
        {
            EntityId id = EntityId.Parse(json.GetString("id"));
            if (id.IsNone)
            {
                id = EntityId.Parse("thread_quarantined_" + index.ToString("x8"));
            }

            NarrativeThread thread = new NarrativeThread(
                id,
                json.GetString("archetype", "quarantined"),
                new GameTime(json.GetLong("createdAt")))
            {
                State = ThreadState.Quarantined,
                LifecycleReason = "quarantined during save load: " + reason
            };
            return thread;
        }

        private static string ThreadQuarantineReason(NarrativeWorldState world, NarrativeThread thread)
        {
            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                EntityId participant = thread.ParticipantIds[i];
                if (!participant.IsNone && !world.Registry.AllNpcs.ContainsKey(participant))
                {
                    return "quarantined during save load: missing participant " + participant.Value;
                }
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                EntityId fact = thread.FactIds[i];
                if (!fact.IsNone && !world.Knowledge.Facts.ContainsKey(fact))
                {
                    return "quarantined during save load: missing fact " + fact.Value;
                }
            }

            for (int i = 0; i < thread.StoryletFirings.Count; i++)
            {
                StoryletFiring firing = thread.StoryletFirings[i];
                if (!firing.FocusFactId.IsNone && !world.Knowledge.Facts.ContainsKey(firing.FocusFactId))
                {
                    return "quarantined during save load: storylet " + firing.StoryletId
                        + " references missing focus fact " + firing.FocusFactId.Value;
                }

                foreach (KeyValuePair<string, EntityId> role in firing.RoleBindings)
                {
                    if (!role.Value.IsNone && !world.Registry.AllNpcs.ContainsKey(role.Value))
                    {
                        return "quarantined during save load: storylet " + firing.StoryletId
                            + " role " + role.Key + " references missing actor " + role.Value.Value;
                    }
                }
            }

            return null;
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

        private static JsonValue PersonalityToJson(PersonalityWeights personality)
        {
            return JsonValue.Object()
                .Set("boldness", personality.Boldness)
                .Set("patience", personality.Patience)
                .Set("warmth", personality.Warmth)
                .Set("earnestness", personality.Earnestness)
                .Set("optimism", personality.Optimism)
                .Set("orderliness", personality.Orderliness)
                .Set("mercy", personality.Mercy)
                .Set("honesty", personality.Honesty)
                .Set("generosity", personality.Generosity)
                .Set("loyalty", personality.Loyalty)
                .Set("trust", personality.Trust)
                .Set("humility", personality.Humility)
                .Set("curiosity", personality.Curiosity)
                .Set("conventionality", personality.Conventionality)
                .Set("statusBlindness", personality.StatusBlindness);
        }

        private static void ReadPersonality(PersonalityWeights target, JsonValue personality)
        {
            target.Boldness = personality.GetNumber("boldness", 0.5);
            target.Patience = personality.GetNumber("patience", 0.5);
            target.Warmth = personality.GetNumber("warmth", 0.5);
            target.Earnestness = personality.GetNumber("earnestness", 0.5);
            target.Optimism = personality.GetNumber("optimism", 0.5);
            target.Orderliness = personality.GetNumber("orderliness", 0.5);
            target.Mercy = personality.GetNumber("mercy", 0.5);
            target.Honesty = personality.GetNumber("honesty", 0.5);
            target.Generosity = personality.GetNumber("generosity", 0.5);
            target.Loyalty = personality.GetNumber("loyalty", 0.5);
            target.Trust = personality.GetNumber("trust", 0.5);
            target.Humility = personality.GetNumber("humility", 0.5);
            target.Curiosity = personality.GetNumber("curiosity", 0.5);
            target.Conventionality = personality.GetNumber("conventionality", 0.5);
            target.StatusBlindness = personality.GetNumber("statusBlindness", 0.5);
        }

        private static JsonValue ProblemSolvingToJson(ProblemSolvingProfile profile)
        {
            return JsonValue.Object()
                .Set("confront", profile.Confront)
                .Set("avoid", profile.Avoid)
                .Set("askAuthority", profile.AskAuthority)
                .Set("askFriends", profile.AskFriends)
                .Set("paySomeone", profile.PaySomeone)
                .Set("doItSelf", profile.DoItSelf)
                .Set("manipulate", profile.Manipulate)
                .Set("useViolence", profile.UseViolence)
                .Set("seekGuild", profile.SeekGuild)
                .Set("seekReligiousHelp", profile.SeekReligiousHelp)
                .Set("wait", profile.Wait)
                .Set("flee", profile.Flee)
                .Set("publicize", profile.Publicize)
                .Set("conceal", profile.Conceal);
        }

        private static void ReadProblemSolving(ProblemSolvingProfile target, JsonValue profile)
        {
            target.Confront = profile.GetNumber("confront", 0.5);
            target.Avoid = profile.GetNumber("avoid", 0.5);
            target.AskAuthority = profile.GetNumber("askAuthority", 0.5);
            target.AskFriends = profile.GetNumber("askFriends", 0.5);
            target.PaySomeone = profile.GetNumber("paySomeone", 0.5);
            target.DoItSelf = profile.GetNumber("doItSelf", 0.5);
            target.Manipulate = profile.GetNumber("manipulate", 0.5);
            target.UseViolence = profile.GetNumber("useViolence", 0.5);
            target.SeekGuild = profile.GetNumber("seekGuild", 0.5);
            target.SeekReligiousHelp = profile.GetNumber("seekReligiousHelp", 0.5);
            target.Wait = profile.GetNumber("wait", 0.5);
            target.Flee = profile.GetNumber("flee", 0.5);
            target.Publicize = profile.GetNumber("publicize", 0.5);
            target.Conceal = profile.GetNumber("conceal", 0.5);
        }

        private static JsonValue SensitivitiesToJson(SensitivityProfile profile)
        {
            return JsonValue.Object()
                .Set("publicEmbarrassment", profile.PublicEmbarrassment)
                .Set("unpaidDebt", profile.UnpaidDebt)
                .Set("familyThreat", profile.FamilyThreat)
                .Set("animals", profile.Animals)
                .Set("status", profile.Status)
                .Set("theft", profile.Theft)
                .Set("violence", profile.Violence)
                .Set("dishonesty", profile.Dishonesty);
        }

        private static void ReadSensitivities(SensitivityProfile target, JsonValue profile)
        {
            target.PublicEmbarrassment = profile.GetNumber("publicEmbarrassment", 0.5);
            target.UnpaidDebt = profile.GetNumber("unpaidDebt", 0.5);
            target.FamilyThreat = profile.GetNumber("familyThreat", 0.5);
            target.Animals = profile.GetNumber("animals", 0.5);
            target.Status = profile.GetNumber("status", 0.5);
            target.Theft = profile.GetNumber("theft", 0.5);
            target.Violence = profile.GetNumber("violence", 0.5);
            target.Dishonesty = profile.GetNumber("dishonesty", 0.5);
        }

        private static JsonValue ContradictionToJson(ContradictionProfile profile)
        {
            return JsonValue.Object()
                .Set("kind", profile.Kind.ToString())
                .Set("strength", profile.Strength);
        }

        private static void ReadContradiction(ContradictionProfile target, JsonValue profile)
        {
            string kind = profile.GetString("kind", "None");
            PersonalityContradiction parsed;
            if (!System.Enum.TryParse(kind, out parsed))
            {
                parsed = PersonalityContradiction.None;
            }

            target.Kind = parsed;
            target.Strength = profile.GetNumber("strength", 1.0);
        }

        private static JsonValue QuirkToJson(CharacterQuirkProfile profile)
        {
            return JsonValue.Object()
                .Set("assigned", profile.Assigned)
                .Set("weirdness", profile.Weirdness.ToString())
                .Set("kind", profile.Kind.ToString());
        }

        private static void ReadQuirk(CharacterQuirkProfile target, JsonValue profile)
        {
            CharacterWeirdnessTier weirdness;
            if (!Enum.TryParse(profile.GetString("weirdness", "MostlyOrdinary"), out weirdness))
            {
                weirdness = CharacterWeirdnessTier.MostlyOrdinary;
            }

            CharacterQuirk kind;
            if (!Enum.TryParse(profile.GetString("kind", "None"), out kind))
            {
                kind = CharacterQuirk.None;
            }

            target.Assigned = profile.GetBool("assigned");
            target.Weirdness = weirdness;
            target.Kind = kind;
        }

        /// <summary>
        /// The lines this character holds (BQ-077), as an array rather than a fixed object so a
        /// save carries only what was declared. An actor with no lines writes an empty array, and
        /// a kind this build does not know is dropped on load rather than failing the save: the
        /// vocabulary is closed, but a save written by a later build must still open.
        /// </summary>
        private static JsonValue NegativeSpaceToJson(NegativeSpaceProfile profile)
        {
            JsonValue array = JsonValue.Array();
            IReadOnlyList<PersonalProhibition> declared = profile.Declared;
            for (int i = 0; i < declared.Count; i++)
            {
                PersonalProhibition kind = declared[i];
                array.Add(JsonValue.Object()
                    .Set("kind", kind.ToString())
                    .Set("firmness", profile.FirmnessOf(kind))
                    .Set("breakable", profile.IsBreakable(kind)));
            }

            return array;
        }

        private static void ReadNegativeSpace(NegativeSpaceProfile target, JsonValue profile)
        {
            if (profile.Kind != JsonKind.Array)
            {
                return;
            }

            for (int i = 0; i < profile.Items.Count; i++)
            {
                JsonValue entry = profile.Items[i];
                PersonalProhibition kind;
                if (!Enum.TryParse(entry.GetString("kind", string.Empty), out kind)
                    || !Enum.IsDefined(typeof(PersonalProhibition), kind))
                {
                    continue;
                }

                target.Declare(kind, entry.GetNumber("firmness", 1.0), entry.GetBool("breakable"));
            }
        }

        private static JsonValue ValuesToJson(ValueProfile profile)
        {
            return JsonValue.Object()
                .Set("family", ValueConcernToJson(profile.Family))
                .Set("wealth", ValueConcernToJson(profile.Wealth))
                .Set("law", ValueConcernToJson(profile.Law))
                .Set("faith", ValueConcernToJson(profile.Faith))
                .Set("status", ValueConcernToJson(profile.Status))
                .Set("animals", ValueConcernToJson(profile.Animals))
                .Set("knowledge", ValueConcernToJson(profile.Knowledge))
                .Set("freedom", ValueConcernToJson(profile.Freedom));
        }

        private static JsonValue ValueConcernToJson(ValueConcernProfile profile)
        {
            return JsonValue.Object()
                .Set("importance", profile.Importance)
                .Set("flexibility", profile.Flexibility);
        }

        private static void ReadValues(ValueProfile target, JsonValue values)
        {
            ReadValueConcern(target.Family, values["family"]);
            ReadValueConcern(target.Wealth, values["wealth"]);
            ReadValueConcern(target.Law, values["law"]);
            ReadValueConcern(target.Faith, values["faith"]);
            ReadValueConcern(target.Status, values["status"]);
            ReadValueConcern(target.Animals, values["animals"]);
            ReadValueConcern(target.Knowledge, values["knowledge"]);
            ReadValueConcern(target.Freedom, values["freedom"]);
        }

        private static void ReadValueConcern(ValueConcernProfile target, JsonValue value)
        {
            if (value == null)
            {
                return;
            }

            target.Importance = value.GetNumber("importance", 0.5);
            target.Flexibility = value.GetNumber("flexibility", 0.5);
        }

        private static JsonValue NeedsToJson(NarrativeNeedProfile profile)
        {
            return JsonValue.Object()
                .Set("safety", profile.Safety)
                .Set("belonging", profile.Belonging)
                .Set("debtRelief", profile.DebtRelief)
                .Set("status", profile.Status)
                .Set("loyalty", profile.Loyalty)
                .Set("justice", profile.Justice)
                .Set("secrecy", profile.Secrecy)
                .Set("revenge", profile.Revenge)
                .Set("protection", profile.Protection)
                .Set("materialShortage", profile.MaterialShortage)
                .Set("obligation", profile.Obligation);
        }

        private static void ReadNeeds(NarrativeNeedProfile target, JsonValue needs)
        {
            target.Safety = needs.GetNumber("safety");
            target.Belonging = needs.GetNumber("belonging");
            target.DebtRelief = needs.GetNumber("debtRelief");
            target.Status = needs.GetNumber("status");
            target.Loyalty = needs.GetNumber("loyalty");
            target.Justice = needs.GetNumber("justice");
            target.Secrecy = needs.GetNumber("secrecy");
            target.Revenge = needs.GetNumber("revenge");
            target.Protection = needs.GetNumber("protection");
            target.MaterialShortage = needs.GetNumber("materialShortage");
            target.Obligation = needs.GetNumber("obligation");
        }

        private static JsonValue EmotionsToJson(EmotionalStateProfile profile)
        {
            return JsonValue.Object()
                .Set("anger", profile.Anger)
                .Set("fear", profile.Fear)
                .Set("shame", profile.Shame)
                .Set("grief", profile.Grief)
                .Set("relief", profile.Relief)
                .Set("suspicion", profile.Suspicion)
                .Set("affection", profile.Affection)
                .Set("stress", profile.Stress)
                .Set("lastUpdated", profile.LastUpdatedAt.TotalMinutes);
        }

        private static void ReadEmotions(EmotionalStateProfile target, JsonValue emotions)
        {
            target.Anger = emotions.GetNumber("anger");
            target.Fear = emotions.GetNumber("fear");
            target.Shame = emotions.GetNumber("shame");
            target.Grief = emotions.GetNumber("grief");
            target.Relief = emotions.GetNumber("relief");
            target.Suspicion = emotions.GetNumber("suspicion");
            target.Affection = emotions.GetNumber("affection");
            target.Stress = emotions.GetNumber("stress");
            target.LastUpdatedAt = new GameTime(emotions.GetLong("lastUpdated"));
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

        private static JsonValue Proofs(IReadOnlyList<ProofLink> proofs)
        {
            JsonValue array = JsonValue.Array();
            for (int i = 0; i < proofs.Count; i++)
            {
                array.Add(JsonValue.Object()
                    .Set("kind", proofs[i].Kind.ToString())
                    .Set("entity", proofs[i].Entity.Value));
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

        private static JsonValue Strings(IEnumerable<string> values)
        {
            JsonValue array = JsonValue.Array();
            foreach (string value in values)
            {
                array.Add(JsonValue.String(value));
            }

            return array;
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

        private static ProofLink[] ProofList(JsonValue json, string name)
        {
            IReadOnlyList<JsonValue> items = json.GetArray(name);
            ProofLink[] proofs = new ProofLink[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                JsonValue proof = items[i];
                proofs[i] = new ProofLink(
                    (ProofKind)Enum.Parse(typeof(ProofKind), proof.GetString("kind")),
                    EntityId.Parse(proof.GetString("entity")));
            }

            return proofs;
        }
    }

    public sealed class WorldStateLoadResult
    {
        public WorldStateLoadResult(NarrativeWorldState world, IEnumerable<SaveLoadDiagnostic> diagnostics)
        {
            World = world;
            Diagnostics = new List<SaveLoadDiagnostic>(diagnostics ?? new SaveLoadDiagnostic[0]).AsReadOnly();
        }

        public NarrativeWorldState World { get; }

        public IReadOnlyList<SaveLoadDiagnostic> Diagnostics { get; }
    }

    public sealed class SaveLoadDiagnostic
    {
        public SaveLoadDiagnostic(string code, string location, string message)
        {
            Code = code;
            Location = location;
            Message = message;
        }

        public string Code { get; }

        public string Location { get; }

        public string Message { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Location)
                ? Code + ": " + Message
                : Code + " at " + Location + ": " + Message;
        }
    }
}
