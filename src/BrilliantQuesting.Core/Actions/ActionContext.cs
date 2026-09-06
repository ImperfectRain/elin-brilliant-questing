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

        /// <summary>The concrete proposition, object, destination or undertaking this action means.</summary>
        public ActionBinding Binding { get; set; }

        public NarrativeThread Thread { get; set; }

        /// <summary>
        /// Who is close enough to notice. The caller fills this from the real zone contents; the
        /// consequence layer propagates knowledge from here rather than telling the whole town.
        /// </summary>
        public List<EntityId> Witnesses { get; }

        public EntityId Zone => Vanilla.GetZoneOf(Actor);

        public GameTime Now => Vanilla.Now;

        /// <summary>
        /// Whether the acting character is the player (BQ-093).
        ///
        /// Not a permission and not a branch in a verb's meaning: it is the one question that
        /// decides whether the standing Elin keeps - affinity, Karma, Fame, influence, a guild
        /// card - is *about this actor at all*. Elin keeps exactly one of each, and it is the
        /// player's. Asking it of an NPC act does not return a smaller number, it returns
        /// somebody else's.
        /// </summary>
        public bool ActorIsPlayer => Actor == Vanilla.PlayerId;

        /// <summary>
        /// How <paramref name="who"/> feels about the acting character, when the game keeps such
        /// a number at all.
        ///
        /// True only for the player, because <see cref="IVanillaState.GetAffinity"/> is affinity
        /// *toward the player* and there is no second reading for anybody else. False is "the
        /// game does not keep this", which is not zero and not indifference: a caller must let it
        /// contribute nothing rather than let it read as a stranger (`D017`).
        /// </summary>
        public bool TryGetAffinityToActor(EntityId who, out int affinity)
        {
            if (who.IsNone || !ActorIsPlayer)
            {
                affinity = 0;
                return false;
            }

            affinity = Vanilla.GetAffinity(who);
            return true;
        }

        /// <summary>Whether <see cref="Affinity"/> is a reading rather than a stand-in for one.</summary>
        public bool AffinityKnown => TryGetAffinityToActor(Target, out int _);

        /// <summary>
        /// How the target feels about the acting character. Zero when the game keeps no such
        /// number for this actor - check <see cref="AffinityKnown"/> before letting it decide
        /// anything.
        /// </summary>
        public int Affinity => TryGetAffinityToActor(Target, out int affinity) ? affinity : 0;

        public NarrativeNpc TargetNpc => World.Registry.GetNpc(Target);

        public string NameOf(EntityId id) => World.Registry.NameOf(id);
    }
}
