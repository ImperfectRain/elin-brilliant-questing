using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Everything an action needs to decide whether it applies and what happens when it does.
    /// Actions are stateless; all state arrives here.
    /// </summary>
    public sealed class ActionContext
    {
        public ActionContext(NarrativeWorldState world, IVanillaState vanilla, ICheckResolver checks, DeterministicRng rng, EntityId actor, EntityId target)
        {
            World = world;
            Vanilla = vanilla;
            Checks = checks;
            Rng = rng;
            Actor = actor;
            Target = target;
            Witnesses = new List<EntityId>();
        }

        public NarrativeWorldState World { get; }

        public IVanillaState Vanilla { get; }

        public ICheckResolver Checks { get; }

        public DeterministicRng Rng { get; }

        /// <summary>Whoever is acting. Usually the player, but NPCs use the same library.</summary>
        public EntityId Actor { get; }

        public EntityId Target { get; }

        /// <summary>The fact being revealed, denied, traded or used as leverage.</summary>
        public EntityId SubjectFact { get; set; }

        /// <summary>The real Elin item being stolen, returned, offered or planted.</summary>
        public EntityId SubjectItem { get; set; }

        /// <summary>A third party - the person being accused, framed or vouched for.</summary>
        public EntityId ThirdParty { get; set; }

        public NarrativeThread Thread { get; set; }

        /// <summary>
        /// Who is close enough to notice. The caller fills this from the real zone contents; the
        /// consequence layer propagates knowledge from here rather than telling the whole town.
        /// </summary>
        public List<EntityId> Witnesses { get; }

        public EntityId Zone => Vanilla.GetZoneOf(Actor);

        public GameTime Now => Vanilla.Now;

        public int Affinity => Target.IsNone ? 0 : Vanilla.GetAffinity(Target);

        public NarrativeNpc TargetNpc => World.Registry.GetNpc(Target);

        public string NameOf(EntityId id) => World.Registry.NameOf(id);
    }
}
