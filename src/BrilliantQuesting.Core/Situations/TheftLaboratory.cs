using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The whole stack wired together around one small scenario, with no game attached.
    ///
    /// This is the harness the design document's Gate B is measured against: run the three-NPC
    /// theft, act on it however you like, let ten days pass, and check that the world still makes
    /// sense. Because it is deterministic given a seed, a scenario that went strangely can be
    /// replayed exactly.
    /// </summary>
    public sealed class TheftLaboratory
    {
        private readonly DeterministicRng _actionRng;

        private TheftLaboratory(ulong seed)
        {
            _actionRng = new DeterministicRng(seed).Fork("actions");
        }

        public NarrativeWorldState World { get; private set; }

        public SandboxVanillaState Vanilla { get; private set; }

        public ActionRegistry Actions { get; private set; }

        public ThreadEngine Threads { get; private set; }

        public ConsequenceEngine Consequences { get; private set; }

        public RumorSystem Rumors { get; private set; }

        public PettyTheftSituation Situation { get; private set; }

        /// <summary>
        /// Settable so a test can substitute a scripted resolver and assert on consequences
        /// instead of on dice.
        /// </summary>
        public ICheckResolver Checks { get; set; }

        public EntityId Player { get; private set; }

        public EntityId Zone { get; private set; }

        public static TheftLaboratory Create(ulong seed = 20260827UL)
        {
            TheftLaboratory lab = new TheftLaboratory(seed);
            NarrativeWorldState world = new NarrativeWorldState(seed);

            EntityId player = world.NewId("npc");
            EntityId zone = world.NewId("zone");

            SandboxVanillaState vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, level: 5, money: 2000, zone: zone);
            ApplyDefaultPlayerBuild(vanilla, player);

            // The player is a character in the graph like anyone else - they can be lied to,
            // remembered, and have facts believed about them.
            world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

            lab.World = world;
            lab.Vanilla = vanilla;
            lab.Player = player;
            lab.Zone = zone;
            lab.Checks = new VanillaStyleCheckResolver(vanilla);
            lab.Actions = StandardActions.CreateRegistry();
            lab.Rumors = new RumorSystem(world.Knowledge, world.Ledger, world.Ids);
            lab.Consequences = new ConsequenceEngine(world, vanilla);
            lab.Consequences.Attach();

            lab.Situation = PettyTheftSituation.Create(world, new SandboxStager(vanilla), zone, vanilla.Now, seed);

            lab.Threads = new ThreadEngine();
            lab.Threads.Register(PettyTheftSituation.ArchetypeId, new PettyTheftEscalation(vanilla, lab.Rumors));
            return lab;
        }

        /// <summary>A deliberately unremarkable adventurer: competent, not a specialist in anything.</summary>
        private static void ApplyDefaultPlayerBuild(SandboxVanillaState vanilla, EntityId player)
        {
            vanilla.SetAttribute(player, VanillaAttribute.Strength, 12);
            vanilla.SetAttribute(player, VanillaAttribute.Endurance, 12);
            vanilla.SetAttribute(player, VanillaAttribute.Dexterity, 11);
            vanilla.SetAttribute(player, VanillaAttribute.Perception, 12);
            vanilla.SetAttribute(player, VanillaAttribute.Learning, 10);
            vanilla.SetAttribute(player, VanillaAttribute.Will, 11);
            vanilla.SetAttribute(player, VanillaAttribute.Magic, 8);
            vanilla.SetAttribute(player, VanillaAttribute.Charisma, 11);
            vanilla.SetSkill(player, VanillaSkill.Negotiation, 6);
            vanilla.SetSkill(player, VanillaSkill.SpotHidden, 6);
            vanilla.SetSkill(player, VanillaSkill.Pickpocket, 4);
            vanilla.SetSkill(player, VanillaSkill.Stealth, 5);
        }

        /// <summary>
        /// Builds the context for an attempt, filling the witness list from whoever is actually
        /// standing in the zone. Actions decide per outcome whether those people noticed anything.
        /// </summary>
        public ActionContext Context(EntityId target)
        {
            ActionContext context = new ActionContext(World, Vanilla, Checks, _actionRng, Player, target)
            {
                Thread = Situation.Thread
            };

            IReadOnlyList<EntityId> present = Vanilla.GetCharactersInZone(Zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i] != Player && present[i] != target)
                {
                    context.Witnesses.Add(present[i]);
                }
            }

            return context;
        }

        public ActionOutcome Perform(string actionId, EntityId target, Action<ActionContext> configure = null)
        {
            NarrativeAction action = Actions.Get(actionId);
            if (action == null)
            {
                throw new ArgumentException("No such action: " + actionId, nameof(actionId));
            }

            ActionContext context = Context(target);
            configure?.Invoke(context);

            Availability availability = action.GetAvailability(context);
            if (!availability.IsAvailable)
            {
                ActionOutcome refused = new ActionOutcome(actionId, null, "You cannot: " + availability.Reason);
                refused.Notes.Add("blocked before any roll: " + availability.Reason);
                return refused;
            }

            return action.Perform(context);
        }

        /// <summary>Moves the clock and lets every live thread catch up.</summary>
        public int AdvanceDays(long days)
        {
            Vanilla.AdvanceDays(days);
            return Threads.Advance(World, Vanilla.Now);
        }
    }
}
