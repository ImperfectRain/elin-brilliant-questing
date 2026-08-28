using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Stages the three-NPC theft into the live game and plays it, so the laboratory's Gate B can
    /// be re-run against real Charas, real inventories and real affinity.
    ///
    /// Everything it makes is an ordinary Elin object and every consequence goes through the same
    /// adapter the rest of the mod uses. If this produces the same shape of story the headless lab
    /// produces, then the simulation and the game agree, which is the only claim worth making
    /// before building a dialogue layer on top.
    ///
    /// The dialogue layer now presents the verbs interactively. This class only stages the
    /// canonical laboratory situation and reports the available routes.
    /// </summary>
    internal sealed class ProceduralQuestTest
    {
        private readonly NarrativeWorldState _world;
        private readonly ElinVanillaState _vanilla;
        private readonly ElinSituationStager _stager;
        private readonly ICheckResolver _checks;
        private readonly ActionRegistry _actions;
        private readonly ManualLogSource _log;
        private readonly DeterministicRng _rng;

        internal ProceduralQuestTest(
            NarrativeWorldState world,
            ElinVanillaState vanilla,
            ElinSituationStager stager,
            ICheckResolver checks,
            ActionRegistry actions,
            ManualLogSource log)
        {
            _world = world;
            _vanilla = vanilla;
            _stager = stager;
            _checks = checks;
            _actions = actions;
            _log = log;
            _rng = world.Rng.Fork("in-game-test");
        }

        internal void Run()
        {
            EntityId player = _vanilla.PlayerId;
            EntityId zone = _vanilla.GetZoneOf(player);

            _log.LogInfo("=== staging the three-NPC theft into zone " + zone + " ===");

            PettyTheftSituation situation = PettyTheftSituation.Create(
                _world, _stager, zone, _vanilla.Now, _world.WorldSeed);

            NarrativeNpc victim = _world.Registry.GetNpc(situation.VictimId);
            NarrativeNpc thief = _world.Registry.GetNpc(situation.ThiefId);
            NarrativeNpc witness = _world.Registry.GetNpc(situation.WitnessId);

            _log.LogInfo("truth: " + thief.Name + " stole from " + victim.Name
                         + "; " + witness.Name + " saw it. The player knows neither.");

            ReportBindings(situation);
            ReportOptions(situation);
            _log.LogInfo("Talk to any staged NPC to choose a Brilliant Questing verb through Drama.");
        }

        /// <summary>
        /// Proves the generated people are real Charas the adapter can read back - the step that
        /// separates "spawned something" from "the simulation and the game share an identity".
        /// </summary>
        private void ReportBindings(PettyTheftSituation situation)
        {
            foreach (EntityId id in new[] { situation.VictimId, situation.ThiefId, situation.WitnessId })
            {
                NarrativeNpc npc = _world.Registry.GetNpc(id);
                _log.LogInfo("  " + npc.Name.PadRight(10)
                             + " alive " + _vanilla.IsAlive(id)
                             + "  level " + _vanilla.GetLevel(id)
                             + "  affinity " + _vanilla.GetAffinity(id)
                             + "  PER " + _vanilla.GetAttribute(id, VanillaAttribute.Perception)
                             + "  WIL " + _vanilla.GetAttribute(id, VanillaAttribute.Will)
                             + "  carrying " + _vanilla.GetInventory(id).Count);
            }
        }

        /// <summary>Every verb the world currently permits, and the reason for each it does not.</summary>
        private void ReportOptions(PettyTheftSituation situation)
        {
            foreach (EntityId target in new[] { situation.VictimId, situation.ThiefId, situation.WitnessId })
            {
                ActionContext context = Context(situation, target);
                context.SubjectFact = situation.TheftFactId;

                HashSet<ActionFamily> families = new HashSet<ActionFamily>();
                List<string> available = new List<string>();
                List<string> blocked = new List<string>();

                foreach (ActionOffer offer in _actions.Discover(context, includeUnavailable: true))
                {
                    if (offer.Availability.IsAvailable)
                    {
                        available.Add(offer.Action.Id);
                        families.Add(offer.Action.Family);
                    }
                    else
                    {
                        blocked.Add(offer.Action.Id + " (" + offer.Availability.Reason + ")");
                    }
                }

                _log.LogInfo("options vs " + _world.Registry.NameOf(target) + ": " + string.Join(", ", available.ToArray()));
                _log.LogInfo("   blocked: " + string.Join("; ", blocked.ToArray()));
                _log.LogInfo("   solution families open: " + families.Count);
            }
        }

        /// <summary>Witnesses come from whoever is actually standing in the zone.</summary>
        private ActionContext Context(PettyTheftSituation situation, EntityId target)
        {
            ActionContext context = new ActionContext(_world, _vanilla, _checks, _rng, _vanilla.PlayerId, target)
            {
                Thread = situation.Thread
            };

            IReadOnlyList<EntityId> present = _vanilla.GetCharactersInZone(situation.ZoneId);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i] != _vanilla.PlayerId && present[i] != target)
                {
                    context.Witnesses.Add(present[i]);
                }
            }

            return context;
        }
    }
}
