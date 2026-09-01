using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public sealed class CapturedWorldSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string Source { get; set; } = "captured";

        public string WorldJson { get; set; }

        public ulong WorldSeed { get; set; } = 42UL;

        public string PlayerId { get; set; } = "npc_player";

        public string PrimaryZoneId { get; set; }

        public long NowMinutes { get; set; }

        public int Karma { get; set; }

        public int Fame { get; set; }

        public Dictionary<string, int> Influence { get; set; } = new Dictionary<string, int>();

        public Dictionary<string, int> GuildRanks { get; set; } = new Dictionary<string, int>();

        public Dictionary<string, int> GuildContribution { get; set; } = new Dictionary<string, int>();

        public List<string> Capabilities { get; set; } = new List<string>();

        public List<CapturedActor> Actors { get; set; } = new List<CapturedActor>();

        public List<CapturedSite> Sites { get; set; } = new List<CapturedSite>();

        public CapturedHome Home { get; set; }

        public static CapturedWorldSnapshot Load(string path)
        {
            JsonValue root = JsonValue.Parse(File.ReadAllText(path));
            int schema = root.GetInt("schemaVersion");
            if (schema <= 0)
            {
                throw new InvalidOperationException("Snapshot is missing schemaVersion.");
            }

            if (schema > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "Snapshot schema " + schema + " is newer than this Lab reader (" + CurrentSchemaVersion + ").");
            }

            CapturedWorldSnapshot snapshot = new CapturedWorldSnapshot
            {
                SchemaVersion = schema,
                Source = root.GetString("source", "captured"),
                WorldJson = root.GetString("worldJson", null),
                WorldSeed = ParseUlong(root.GetString("worldSeed", "42")),
                PlayerId = root.GetString("playerId", "npc_player"),
                PrimaryZoneId = root.GetString("primaryZoneId"),
                NowMinutes = root.GetLong("nowMinutes"),
                Karma = root.GetInt("karma"),
                Fame = root.GetInt("fame")
            };

            ReadStringIntMap(root["influence"], snapshot.Influence);
            ReadStringIntMap(root["guildRanks"], snapshot.GuildRanks);
            ReadStringIntMap(root["guildContribution"], snapshot.GuildContribution);
            foreach (JsonValue value in root.GetArray("capabilities"))
            {
                snapshot.Capabilities.Add(value.StringValue);
            }

            foreach (JsonValue actor in root.GetArray("actors"))
            {
                snapshot.Actors.Add(CapturedActor.FromJson(actor));
            }

            foreach (JsonValue site in root.GetArray("sites"))
            {
                snapshot.Sites.Add(CapturedSite.FromJson(site));
            }

            JsonValue home = root["home"];
            if (home != null && home.Kind == JsonKind.Object)
            {
                snapshot.Home = CapturedHome.FromJson(home);
            }

            return snapshot;
        }

        public static CapturedWorldSnapshot Capture(HarnessState state)
        {
            CapturedWorldSnapshot snapshot = new CapturedWorldSnapshot
            {
                Source = state.Source,
                WorldJson = WorldStateSerializer.Save(state.World, indented: false),
                WorldSeed = state.World.WorldSeed,
                PlayerId = state.PlayerId.Value,
                PrimaryZoneId = state.PrimaryZoneId.Value,
                NowMinutes = state.Vanilla.Now.TotalMinutes,
                Karma = state.Vanilla.Karma,
                Fame = state.Vanilla.Fame
            };

            foreach (VanillaCapability capability in (VanillaCapability[])Enum.GetValues(typeof(VanillaCapability)))
            {
                if (state.Vanilla.Supports(capability))
                {
                    snapshot.Capabilities.Add(capability.ToString());
                }
            }

            foreach (EntityId actorId in state.ActorIds)
            {
                NarrativeNpc npc = state.World.Registry.GetNpc(actorId);
                CapturedActor actor = new CapturedActor
                {
                    Id = actorId.Value,
                    Name = npc?.Name ?? actorId.Value,
                    Occupation = npc?.Occupation,
                    HomeSiteId = npc?.HomeSiteId.Value,
                    ZoneId = state.Vanilla.GetZoneOf(actorId).Value,
                    Level = state.Vanilla.GetLevel(actorId),
                    Money = state.Vanilla.GetMoney(actorId),
                    Affinity = state.Vanilla.GetAffinity(actorId),
                    Life = state.Vanilla.GetLifeState(actorId).ToString(),
                    ActorClass = state.Vanilla.GetActorClass(actorId).ToString(),
                    ActorKind = state.Vanilla.GetActorKind(actorId).ToString(),
                    SocialAgency = state.Vanilla.GetSocialAgency(actorId).ToString(),
                    Deity = state.Vanilla.GetWorshippedDeity(actorId),
                    Piety = state.Vanilla.GetPiety(actorId)
                };

                foreach (VanillaAttribute attribute in (VanillaAttribute[])Enum.GetValues(typeof(VanillaAttribute)))
                {
                    actor.Attributes[attribute.ToString()] = state.Vanilla.GetAttribute(actorId, attribute);
                }

                foreach (VanillaSkill skill in (VanillaSkill[])Enum.GetValues(typeof(VanillaSkill)))
                {
                    actor.Skills[skill.ToString()] = state.Vanilla.GetSkill(actorId, skill);
                }

                foreach (ItemDescriptor item in state.Vanilla.GetInventory(actorId))
                {
                    actor.Inventory.Add(CapturedItem.FromItem(item));
                }

                snapshot.Actors.Add(actor);
            }

            foreach (NarrativeSite site in state.World.Registry.Sites.Values)
            {
                snapshot.Sites.Add(new CapturedSite
                {
                    Id = site.Id.Value,
                    Name = site.Name,
                    SiteType = site.SiteType,
                    ZoneRef = site.VanillaZoneRef
                });
            }

            HomeState home = state.Vanilla.GetHomeState();
            if (home != null)
            {
                snapshot.Home = CapturedHome.FromHome(home);
            }

            return snapshot;
        }

        public string ToJson(bool indented = true) => ToJsonValue().ToJson(indented);

        public JsonValue ToJsonValue()
        {
            JsonValue root = JsonValue.Object()
                .Set("schemaVersion", SchemaVersion)
                .Set("source", Source)
                .Set("worldSeed", WorldSeed.ToString())
                .Set("playerId", PlayerId)
                .Set("primaryZoneId", PrimaryZoneId)
                .Set("nowMinutes", NowMinutes)
                .Set("karma", Karma)
                .Set("fame", Fame);

            if (!string.IsNullOrEmpty(WorldJson))
            {
                root.Set("worldJson", WorldJson);
            }

            root.Set("capabilities", Strings(Capabilities));
            root.Set("influence", StringIntMap(Influence));
            root.Set("guildRanks", StringIntMap(GuildRanks));
            root.Set("guildContribution", StringIntMap(GuildContribution));

            JsonValue actors = JsonValue.Array();
            foreach (CapturedActor actor in Actors)
            {
                actors.Add(actor.ToJson());
            }

            JsonValue sites = JsonValue.Array();
            foreach (CapturedSite site in Sites)
            {
                sites.Add(site.ToJson());
            }

            root.Set("actors", actors);
            root.Set("sites", sites);
            if (Home != null)
            {
                root.Set("home", Home.ToJson());
            }

            return root;
        }

        public HarnessState Hydrate() => Hydrate(null);

        public HarnessState Hydrate(NarrativeWorldState existingWorld)
        {
            NarrativeWorldState world = existingWorld
                ?? (!string.IsNullOrEmpty(WorldJson)
                    ? WorldStateSerializer.Load(WorldJson)
                    : new NarrativeWorldState(WorldSeed));

            EntityId player = EntityId.Parse(PlayerId);
            EntityId primaryZone = EntityId.Parse(PrimaryZoneId);
            SandboxVanillaState vanilla = new SandboxVanillaState(player)
            {
                Now = new GameTime(NowMinutes)
            };

            if (Capabilities.Count > 0)
            {
                foreach (VanillaCapability capability in (VanillaCapability[])Enum.GetValues(typeof(VanillaCapability)))
                {
                    vanilla.SetCapability(capability, false);
                }

                foreach (string capability in Capabilities)
                {
                    if (Enum.TryParse(capability, out VanillaCapability parsed))
                    {
                        vanilla.SetCapability(parsed, true);
                    }
                }
            }

            HarnessState state = new HarnessState(world, vanilla, player, primaryZone, Source);
            foreach (CapturedSite site in Sites)
            {
                EntityId id = EntityId.Parse(site.Id);
                if (id.IsNone || world.Registry.GetSite(id) != null)
                {
                    continue;
                }

                world.Registry.Add(new NarrativeSite(id, site.Name ?? id.Value, site.SiteType ?? "site")
                {
                    VanillaZoneRef = site.ZoneRef
                });
            }

            foreach (CapturedActor actor in Actors)
            {
                EntityId id = EntityId.Parse(actor.Id);
                if (id.IsNone)
                {
                    continue;
                }

                NarrativeNpc npc = world.Registry.GetNpc(id);
                if (npc == null)
                {
                    npc = world.Registry.Add(new NarrativeNpc(id, string.IsNullOrEmpty(actor.Name) ? id.Value : actor.Name));
                }
                else if (!string.IsNullOrEmpty(actor.Name))
                {
                    npc.Name = actor.Name;
                }

                npc.Occupation = actor.Occupation ?? npc.Occupation;
                npc.HomeSiteId = EntityId.Parse(actor.HomeSiteId);
                vanilla.Define(id, actor.Level <= 0 ? 1 : actor.Level, actor.Money, EntityId.Parse(actor.ZoneId));
                vanilla.SetAffinity(id, actor.Affinity);
                vanilla.SetActorClass(id, ParseEnum(actor.ActorClass, NarrativeActorClass.Unknown));
                vanilla.SetActorKind(id, ParseEnum(actor.ActorKind, NarrativeActorKind.Unknown));
                vanilla.SetSocialAgency(id, ParseEnum(actor.SocialAgency, SocialAgency.Unknown));
                vanilla.SetFaith(id, actor.Deity ?? string.Empty, actor.Piety);
                if (ParseEnum(actor.Life, VanillaLifeState.Unknown) == VanillaLifeState.Dead)
                {
                    vanilla.Kill(id);
                    npc.Alive = false;
                }

                foreach (KeyValuePair<string, int> pair in actor.Attributes)
                {
                    if (Enum.TryParse(pair.Key, out VanillaAttribute attribute))
                    {
                        vanilla.SetAttribute(id, attribute, pair.Value);
                    }
                }

                foreach (KeyValuePair<string, int> pair in actor.Skills)
                {
                    if (Enum.TryParse(pair.Key, out VanillaSkill skill))
                    {
                        vanilla.SetSkill(id, skill, pair.Value);
                    }
                }

                foreach (CapturedItem item in actor.Inventory)
                {
                    vanilla.GiveItem(id, item.ToItem());
                }

                state.RememberActor(id);
                state.RememberInventoryOwner(id);
            }

            if (Home != null)
            {
                vanilla.SetHome(Home.ToHome());
            }

            foreach (KeyValuePair<string, int> pair in GuildRanks)
            {
                if (Enum.TryParse(pair.Key, out GuildId guild))
                {
                    vanilla.SetGuildRank(guild, pair.Value);
                }
            }

            foreach (KeyValuePair<string, int> pair in GuildContribution)
            {
                if (Enum.TryParse(pair.Key, out GuildId guild))
                {
                    vanilla.SetGuildContribution(guild, pair.Value);
                }
            }

            return state;
        }

        private static void ReadStringIntMap(JsonValue json, Dictionary<string, int> target)
        {
            if (json == null || json.Kind != JsonKind.Object)
            {
                return;
            }

            foreach (KeyValuePair<string, JsonValue> pair in json.Members)
            {
                target[pair.Key] = (int)pair.Value.NumberValue;
            }
        }

        private static JsonValue StringIntMap(Dictionary<string, int> values)
        {
            JsonValue json = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in values)
            {
                json.Set(pair.Key, pair.Value);
            }

            return json;
        }

        private static JsonValue Strings(IEnumerable<string> values)
        {
            JsonValue json = JsonValue.Array();
            foreach (string value in values)
            {
                json.Add(JsonValue.String(value));
            }

            return json;
        }

        private static ulong ParseUlong(string value)
        {
            return ulong.TryParse(value, out ulong parsed) ? parsed : 0UL;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }
    }

    public sealed class CapturedActor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Occupation { get; set; }
        public string HomeSiteId { get; set; }
        public string ZoneId { get; set; }
        public int Level { get; set; } = 1;
        public int Money { get; set; }
        public int Affinity { get; set; }
        public string Life { get; set; } = "Unknown";
        public string ActorClass { get; set; } = "Unknown";
        public string ActorKind { get; set; } = "Unknown";
        public string SocialAgency { get; set; } = "Unknown";
        public string Deity { get; set; }
        public int Piety { get; set; }
        public Dictionary<string, int> Attributes { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
        public List<CapturedItem> Inventory { get; set; } = new List<CapturedItem>();

        internal static CapturedActor FromJson(JsonValue json)
        {
            CapturedActor actor = new CapturedActor
            {
                Id = json.GetString("id"),
                Name = json.GetString("name"),
                Occupation = json.GetString("occupation", null),
                HomeSiteId = json.GetString("homeSiteId"),
                ZoneId = json.GetString("zoneId"),
                Level = json.GetInt("level", 1),
                Money = json.GetInt("money"),
                Affinity = json.GetInt("affinity"),
                Life = json.GetString("life", "Unknown"),
                ActorClass = json.GetString("actorClass", "Unknown"),
                ActorKind = json.GetString("actorKind", "Unknown"),
                SocialAgency = json.GetString("socialAgency", "Unknown"),
                Deity = json.GetString("deity", null),
                Piety = json.GetInt("piety")
            };

            ReadMap(json["attributes"], actor.Attributes);
            ReadMap(json["skills"], actor.Skills);
            foreach (JsonValue item in json.GetArray("inventory"))
            {
                actor.Inventory.Add(CapturedItem.FromJson(item));
            }

            return actor;
        }

        internal JsonValue ToJson()
        {
            JsonValue inventory = JsonValue.Array();
            foreach (CapturedItem item in Inventory)
            {
                inventory.Add(item.ToJson());
            }

            return JsonValue.Object()
                .Set("id", Id)
                .Set("name", Name)
                .Set("occupation", Occupation)
                .Set("homeSiteId", HomeSiteId)
                .Set("zoneId", ZoneId)
                .Set("level", Level)
                .Set("money", Money)
                .Set("affinity", Affinity)
                .Set("life", Life)
                .Set("actorClass", ActorClass)
                .Set("actorKind", ActorKind)
                .Set("socialAgency", SocialAgency)
                .Set("deity", Deity)
                .Set("piety", Piety)
                .Set("attributes", Map(Attributes))
                .Set("skills", Map(Skills))
                .Set("inventory", inventory);
        }

        private static void ReadMap(JsonValue json, Dictionary<string, int> target)
        {
            if (json == null || json.Kind != JsonKind.Object)
            {
                return;
            }

            foreach (KeyValuePair<string, JsonValue> pair in json.Members)
            {
                target[pair.Key] = (int)pair.Value.NumberValue;
            }
        }

        private static JsonValue Map(Dictionary<string, int> values)
        {
            JsonValue json = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in values)
            {
                json.Set(pair.Key, pair.Value);
            }

            return json;
        }
    }

    public sealed class CapturedItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CategoryTag { get; set; }
        public int Value { get; set; }
        public string SourceId { get; set; }
        public int Quality { get; set; }

        public static CapturedItem FromItem(ItemDescriptor item)
        {
            return new CapturedItem
            {
                Id = item.Id.Value,
                Name = item.Name,
                CategoryTag = item.CategoryTag,
                Value = item.Value,
                SourceId = item.SourceId,
                Quality = item.Quality
            };
        }

        internal static CapturedItem FromJson(JsonValue json)
        {
            return new CapturedItem
            {
                Id = json.GetString("id"),
                Name = json.GetString("name"),
                CategoryTag = json.GetString("categoryTag"),
                Value = json.GetInt("value"),
                SourceId = json.GetString("sourceId", null),
                Quality = json.GetInt("quality")
            };
        }

        internal ItemDescriptor ToItem()
        {
            return new ItemDescriptor(EntityId.Parse(Id), Name, CategoryTag, Value, SourceId, Quality);
        }

        internal JsonValue ToJson()
        {
            return JsonValue.Object()
                .Set("id", Id)
                .Set("name", Name)
                .Set("categoryTag", CategoryTag)
                .Set("value", Value)
                .Set("sourceId", SourceId)
                .Set("quality", Quality);
        }
    }

    public sealed class CapturedSite
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SiteType { get; set; }
        public string ZoneRef { get; set; }

        internal static CapturedSite FromJson(JsonValue json)
        {
            return new CapturedSite
            {
                Id = json.GetString("id"),
                Name = json.GetString("name"),
                SiteType = json.GetString("siteType", "site"),
                ZoneRef = json.GetString("zoneRef", null)
            };
        }

        internal JsonValue ToJson()
        {
            return JsonValue.Object()
                .Set("id", Id)
                .Set("name", Name)
                .Set("siteType", SiteType)
                .Set("zoneRef", ZoneRef);
        }
    }

    public sealed class CapturedHome
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public int? Capacity { get; set; }
        public List<CapturedHomeResident> Residents { get; set; } = new List<CapturedHomeResident>();
        public Dictionary<string, int> Metrics { get; set; } = new Dictionary<string, int>();

        public static CapturedHome FromHome(HomeState home)
        {
            CapturedHome captured = new CapturedHome
            {
                ZoneId = home.ZoneId.Value,
                Name = home.Name,
                Capacity = home.CapacityKnown ? home.Capacity : (int?)null
            };

            foreach (HomeResident resident in home.Residents)
            {
                captured.Residents.Add(new CapturedHomeResident
                {
                    Id = resident.Id.Value,
                    Name = resident.Name,
                    Job = resident.Job
                });
            }

            foreach (HomeMetric metric in (HomeMetric[])Enum.GetValues(typeof(HomeMetric)))
            {
                if (home.TryGetMetric(metric, out int value))
                {
                    captured.Metrics[metric.ToString()] = value;
                }
            }

            return captured;
        }

        internal static CapturedHome FromJson(JsonValue json)
        {
            CapturedHome home = new CapturedHome
            {
                ZoneId = json.GetString("zoneId"),
                Name = json.GetString("name")
            };

            JsonValue capacity = json["capacity"];
            if (capacity != null && capacity.Kind == JsonKind.Number)
            {
                home.Capacity = (int)capacity.NumberValue;
            }

            foreach (JsonValue resident in json.GetArray("residents"))
            {
                home.Residents.Add(CapturedHomeResident.FromJson(resident));
            }

            if (json["metrics"] != null && json["metrics"].Kind == JsonKind.Object)
            {
                foreach (KeyValuePair<string, JsonValue> pair in json["metrics"].Members)
                {
                    home.Metrics[pair.Key] = (int)pair.Value.NumberValue;
                }
            }

            return home;
        }

        internal HomeState ToHome()
        {
            HomeStateBuilder builder = new HomeStateBuilder(EntityId.Parse(ZoneId), Name);
            if (Capacity.HasValue)
            {
                builder.WithCapacity(Capacity.Value);
            }

            foreach (CapturedHomeResident resident in Residents)
            {
                builder.AddResident(EntityId.Parse(resident.Id), resident.Name, resident.Job);
            }

            foreach (KeyValuePair<string, int> pair in Metrics)
            {
                if (Enum.TryParse(pair.Key, out HomeMetric metric))
                {
                    builder.WithMetric(metric, pair.Value);
                }
            }

            return builder.Build();
        }

        internal JsonValue ToJson()
        {
            JsonValue residents = JsonValue.Array();
            foreach (CapturedHomeResident resident in Residents)
            {
                residents.Add(resident.ToJson());
            }

            JsonValue metrics = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in Metrics)
            {
                metrics.Set(pair.Key, pair.Value);
            }

            JsonValue json = JsonValue.Object()
                .Set("zoneId", ZoneId)
                .Set("name", Name)
                .Set("residents", residents)
                .Set("metrics", metrics);
            json.Set("capacity", Capacity.HasValue ? JsonValue.Number(Capacity.Value) : JsonValue.Null());
            return json;
        }
    }

    public sealed class CapturedHomeResident
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Job { get; set; }

        internal static CapturedHomeResident FromJson(JsonValue json)
        {
            return new CapturedHomeResident
            {
                Id = json.GetString("id"),
                Name = json.GetString("name"),
                Job = json.GetString("job", null)
            };
        }

        internal JsonValue ToJson()
        {
            return JsonValue.Object()
                .Set("id", Id)
                .Set("name", Name)
                .Set("job", Job);
        }
    }
}
