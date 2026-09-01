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
    /// A situation that begins with the player's own Home resident roll.
    ///
    /// Residency is the source, not presence in the current map. The resident is read from Elin's
    /// Home list, and pressure is derived only when the Home is at its verified food-supported
    /// capacity. `fFood` is not treated as an absolute hunger or stock threshold.
    /// </summary>
    public sealed class HomeResidentSituation
    {
        public const string ArchetypeId = "home_resident_problem";

        private static readonly ProductionSpec FoodNeed = new ProductionSpec("food", 20);

        private HomeResidentSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId ResidentId { get; private set; }

        public EntityId NeedFactId { get; private set; }

        public EntityId HomeZoneId { get; private set; }

        public static HomeResidentSituation TryGenerate(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            if (world == null || vanilla == null)
            {
                return null;
            }

            HomeState home = vanilla.GetHomeState();
            if (home == null || home.Residents.Count == 0 || home.ZoneId.IsNone)
            {
                return null;
            }

            if (!AtFoodSupportedCapacity(home, out int food))
            {
                return null;
            }

            HomeResident resident = FirstResident(home);
            if (resident == null)
            {
                return null;
            }

            NarrativeThread existing = ExistingUnresolvedResidentProblem(world, resident.Id, home.ZoneId);
            if (existing != null)
            {
                if (existing.IsLive)
                {
                    return null;
                }

                return ReactivateFoodProblem(world, home, resident, food, now, existing);
            }

            return CreateFoodProblem(world, vanilla.PlayerId, home, resident, food, now);
        }

        private static HomeResidentSituation CreateFoodProblem(
            NarrativeWorldState world,
            EntityId player,
            HomeState home,
            HomeResident resident,
            int food,
            GameTime now)
        {
            HomeResidentSituation situation = new HomeResidentSituation
            {
                ResidentId = resident.Id,
                HomeZoneId = home.ZoneId
            };

            EnsureHomeSite(world, home);
            NarrativeNpc npc = EnsureResident(world, home, resident);
            AddKeepHomeFedGoal(npc, home.ZoneId);

            Fact need = new Fact(
                world.NewId("fact"),
                resident.Id,
                FactPredicates.Needs,
                EntityId.None,
                FoodNeed.ToFactValue(),
                TruthState.True);
            world.Knowledge.AddFact(need);
            situation.NeedFactId = need.Id;
            world.Demands.AddOrUpdate(home.ZoneId, LocalDemandCategory.Food, 55 - food, now, now.PlusDays(5), need.Id);

            world.Knowledge.Teach(resident.Id, need.Id, KnowledgeSource.Participant, 1.0, now, true);
            if (!player.IsNone)
            {
                world.Knowledge.Teach(player, need.Id, KnowledgeSource.Hearsay, 0.8, now, false, resident.Id);
            }

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 20 + System.Math.Max(0, home.ResidentCount - home.Capacity + 1) * 10,
                Importance = 30,
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(resident.Id);
            thread.SiteIds.Add(home.ZoneId);
            thread.FactIds.Add(need.Id);
            thread.OpenQuestions.Add("How will " + world.Registry.NameOf(resident.Id) + " get food into " + world.Registry.NameOf(home.ZoneId) + "?");
            thread.GenerationCauses.Add(world.Registry.NameOf(resident.Id) + " is on the Home resident roll.");
            thread.GenerationCauses.Add("Home is at its food-supported capacity: " + home.ResidentCount + "/" + home.Capacity + " residents, fFood " + food + ".");
            thread.Escalation.Add(new EscalationStep("household_pressure_mounts", 5, "The household's capacity pressure worsens."));

            world.Threads.Add(thread);
            situation.Thread = thread;
            return situation;
        }

        private static bool AtFoodSupportedCapacity(HomeState home, out int food)
        {
            food = 0;
            if (home == null || !home.CapacityKnown || home.Capacity <= 0)
            {
                return false;
            }

            if (!home.TryGetMetric(HomeMetric.Food, out food))
            {
                return false;
            }

            return home.ResidentCount >= home.Capacity;
        }

        private static HomeResident FirstResident(HomeState home)
        {
            HomeResident best = null;
            for (int i = 0; i < home.Residents.Count; i++)
            {
                HomeResident resident = home.Residents[i];
                if (resident == null || resident.Id.IsNone)
                {
                    continue;
                }

                if (best == null || resident.Id.CompareTo(best.Id) < 0)
                {
                    best = resident;
                }
            }

            return best;
        }

        private static HomeResidentSituation ReactivateFoodProblem(
            NarrativeWorldState world,
            HomeState home,
            HomeResident resident,
            int food,
            GameTime now,
            NarrativeThread thread)
        {
            HomeResidentSituation situation = new HomeResidentSituation
            {
                ResidentId = resident.Id,
                HomeZoneId = home.ZoneId,
                Thread = thread,
                NeedFactId = FirstOpenNeedFact(world, thread)
            };

            EnsureHomeSite(world, home);
            NarrativeNpc npc = EnsureResident(world, home, resident);
            AddKeepHomeFedGoal(npc, home.ZoneId);

            if (!situation.NeedFactId.IsNone)
            {
                world.Demands.AddOrUpdate(
                    home.ZoneId,
                    LocalDemandCategory.Food,
                    55 - food,
                    thread.CreatedAt,
                    now.PlusDays(5),
                    situation.NeedFactId);
            }

            ThreadLifecycle.Reactivate(
                world,
                thread,
                now,
                "Home pressure still exists for " + world.Registry.NameOf(resident.Id));
            return situation;
        }

        private static NarrativeThread ExistingUnresolvedResidentProblem(NarrativeWorldState world, EntityId resident, EntityId home)
        {
            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread.ArchetypeId == ArchetypeId
                    && IsUnresolved(thread)
                    && thread.ParticipantIds.Contains(resident)
                    && thread.SiteIds.Contains(home)
                    && !FirstOpenNeedFact(world, thread).IsNone)
                {
                    return thread;
                }
            }

            return null;
        }

        private static bool IsUnresolved(NarrativeThread thread)
        {
            return thread.State == ThreadState.Latent
                   || thread.State == ThreadState.Active
                   || thread.State == ThreadState.Dormant;
        }

        private static EntityId FirstOpenNeedFact(NarrativeWorldState world, NarrativeThread thread)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    return fact.Id;
                }
            }

            return EntityId.None;
        }

        private static void EnsureHomeSite(NarrativeWorldState world, HomeState home)
        {
            if (world.Registry.GetSite(home.ZoneId) == null)
            {
                world.Registry.Add(new NarrativeSite(
                    home.ZoneId,
                    home.Name.Length == 0 ? "Home" : home.Name,
                    "home"));
            }
        }

        private static NarrativeNpc EnsureResident(NarrativeWorldState world, HomeState home, HomeResident resident)
        {
            NarrativeNpc npc = world.Registry.GetNpc(resident.Id);
            if (npc == null)
            {
                npc = world.Registry.Add(new NarrativeNpc(resident.Id, resident.Name)
                {
                    Occupation = resident.Job,
                    HomeSiteId = home.ZoneId,
                    Importance = NarrativeImportance.Known
                });
            }
            else
            {
                if (resident.Name.Length > 0)
                {
                    npc.Name = resident.Name;
                }

                if (resident.HasJob)
                {
                    npc.Occupation = resident.Job;
                }

                if (npc.HomeSiteId.IsNone)
                {
                    npc.HomeSiteId = home.ZoneId;
                }

                npc.Promote(NarrativeImportance.Known);
            }

            return npc;
        }

        private static void AddKeepHomeFedGoal(NarrativeNpc npc, EntityId home)
        {
            for (int i = 0; i < npc.Goals.Count; i++)
            {
                NpcGoal goal = npc.Goals[i];
                if (goal.Kind == "keep_home_fed" && goal.Subject == home && !goal.Satisfied)
                {
                    return;
                }
            }

            npc.Goals.Add(new NpcGoal("keep_home_fed", home, 70));
        }
    }

    /// <summary>Escalation for resident-origin Home problems.</summary>
    public sealed class HomeResidentEscalation : IThreadEscalationHandler
    {
        public void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now)
        {
            if (step.Id != "household_pressure_mounts")
            {
                return;
            }

            EntityId resident = thread.ParticipantIds.Count == 0 ? EntityId.None : thread.ParticipantIds[0];
            EntityId home = thread.SiteIds.Count == 0 ? EntityId.None : thread.SiteIds[0];
            thread.Tension += 15;
            world.Record(
                WorldEventType.Harmed,
                resident,
                EntityId.None,
                now,
                0.35,
                home,
                related: OpenDemandIds(world, thread),
                threadId: thread.Id);
        }

        private static EntityId[] OpenDemandIds(NarrativeWorldState world, NarrativeThread thread)
        {
            EntityId[] ids = new EntityId[thread.FactIds.Count];
            int count = 0;
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    ids[count++] = fact.Id;
                }
            }

            if (count == ids.Length)
            {
                return ids;
            }

            EntityId[] trimmed = new EntityId[count];
            for (int i = 0; i < count; i++)
            {
                trimmed[i] = ids[i];
            }

            return trimmed;
        }
    }
}
