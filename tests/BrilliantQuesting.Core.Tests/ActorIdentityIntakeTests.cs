using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// One live physical character is one participating BQ actor.
    ///
    /// The failure this pins is a live one: a single Elin Chara uid was found registered under two
    /// BQ ids at once - an authored `npc_...` for somebody the mod staged, and a `npc_vanilla_&lt;uid&gt;`
    /// minted the next time the zone was walked. Two ids for one body is not a cosmetic duplicate.
    /// Casting can put the body in two roles of one scene; familiarity, beliefs, relationships and
    /// callbacks each accumulate against whichever half the caller happened to hold; and the id
    /// history was written under stops being the one the game answers to.
    ///
    /// The rule is narrow and has two halves that must both hold. One: a body participates once.
    /// Two: the id it used to be known by keeps resolving, because the events, beliefs and threads
    /// written under it are true and repointing them would invent a past. So a duplicate is
    /// reconciled by retiring one record onto the other, never by deleting or rewriting either.
    /// </summary>
    public class ActorIdentityIntakeTests
    {
        private static readonly EntityId Authored = EntityId.Parse("npc_miller_01");
        private static readonly EntityId Minted = EntityId.Parse("npc_vanilla_500");
        private static readonly EntityId Other = EntityId.Parse("npc_vanilla_501");

        /// <summary>The adapter's convention, stated where a headless test can use it.</summary>
        private static bool MintedFromUid(EntityId id) => id.Value.StartsWith("npc_vanilla_");

        // -- the invariant --------------------------------------------------------------------

        [Fact]
        public void TwoRecordsForOnePhysicalCharacterLeaveOneParticipatingActor()
        {
            NarrativeWorldState world = TwoIdsForOneBody();

            var retired = ActorIdentityIntake.Reconcile(world, MintedFromUid);

            // The authored id is canonical: it is the name situations and threads were written
            // against, and the minted one can be reconstructed from the character at any time.
            ActorIdentityIntake.Retirement only = Assert.Single(retired);
            Assert.Equal(Minted, only.Alias);
            Assert.Equal(Authored, only.Canonical);
            Assert.Equal("500", only.VanillaRef);

            Assert.True(world.Registry.IsActor(Authored));
            Assert.False(world.Registry.IsActor(Minted));
            Assert.Equal(Authored, world.Registry.Canonical(Minted));
            Assert.Equal(Authored, world.Registry.Canonical(Authored));
        }

        [Fact]
        public void TheRetiredRecordSurvivesAndStaysResolvable()
        {
            NarrativeWorldState world = TwoIdsForOneBody();
            ActorIdentityIntake.Reconcile(world, MintedFromUid);

            // Still a record, still named, still carrying whatever it carried. Only its
            // participation is gone.
            NarrativeNpc alias = world.Registry.GetNpc(Minted);
            Assert.NotNull(alias);
            Assert.Equal("Miller", alias.Name);
            Assert.Equal(Authored, alias.AliasOf);
            Assert.False(alias.IsCanonical);
            Assert.True(world.Registry.AllNpcs.ContainsKey(Minted));
            Assert.False(world.Registry.Npcs.ContainsKey(Minted));
        }

        [Fact]
        public void ReconcilingIsIdempotentAndDeterministic()
        {
            NarrativeWorldState world = TwoIdsForOneBody();

            Assert.Single(ActorIdentityIntake.Reconcile(world, MintedFromUid));
            Assert.Empty(ActorIdentityIntake.Reconcile(world, MintedFromUid));
            Assert.Empty(ActorIdentityIntake.Reconcile(world, MintedFromUid));
            Assert.Equal(Authored, world.Registry.Canonical(Minted));
        }

        [Fact]
        public void TwoIdsOfTheSameKindReconcileByOrdinalOrderRatherThanByLuck()
        {
            NarrativeWorldState world = new NarrativeWorldState(3);
            world.Registry.Add(new NarrativeNpc(Other, "Later") { VanillaCharaRef = "500" });
            world.Registry.Add(new NarrativeNpc(Minted, "Earlier") { VanillaCharaRef = "500" });

            ActorIdentityIntake.Retirement only =
                Assert.Single(ActorIdentityIntake.Reconcile(world, MintedFromUid));

            Assert.Equal(Minted, only.Canonical);
            Assert.Equal(Other, only.Alias);
        }

        [Fact]
        public void RecordsBoundToNothingAreNotEachOther()
        {
            // An empty external ref is "not spawned", which is the ordinary state of everybody in
            // the database who is not currently in the world - not a claim that they share a body.
            NarrativeWorldState world = new NarrativeWorldState(5);
            world.Registry.Add(new NarrativeNpc(Authored, "Miller"));
            world.Registry.Add(new NarrativeNpc(Minted, "Someone else"));

            Assert.Empty(ActorIdentityIntake.Reconcile(world, MintedFromUid));
            Assert.Equal(2, world.Registry.Npcs.Count);
        }

        [Fact]
        public void ARetirementThatWouldBeALieIsRefused()
        {
            NarrativeWorldState world = TwoIdsForOneBody();

            Assert.False(world.Registry.Retire(Authored, Authored));
            Assert.False(world.Registry.Retire(Authored, EntityId.None));
            Assert.False(world.Registry.Retire(EntityId.Parse("npc_nobody"), Authored));
            Assert.False(world.Registry.Retire(Authored, EntityId.Parse("npc_nobody")));

            // And nothing may be retired onto a record that is itself retired: a chain that ends
            // nowhere is how a canonical id stops naming anybody.
            Assert.True(world.Registry.Retire(Minted, Authored));
            Assert.False(world.Registry.Retire(Other, Minted));
            Assert.True(world.Registry.IsActor(Authored));
        }

        [Fact]
        public void AnIdNobodyKnowsCanonicalisesToItself()
        {
            // Canonicalisation resolves names; it does not decide who exists. A dead, removed or
            // never-registered id comes back unchanged and fails downstream on its own terms.
            NarrativeWorldState world = TwoIdsForOneBody();

            EntityId stranger = EntityId.Parse("npc_never_seen");
            Assert.Equal(stranger, world.Registry.Canonical(stranger));
            Assert.Equal(EntityId.None, world.Registry.Canonical(EntityId.None));
            Assert.False(world.Registry.IsActor(stranger));
            Assert.False(world.Registry.IsActor(EntityId.None));
        }

        // -- save and reload ------------------------------------------------------------------

        [Fact]
        public void ARetirementAndTheHistoryUnderItSurviveAReload()
        {
            NarrativeWorldState world = TwoIdsForOneBody();

            // History written under the id that is about to be retired. It is true, and it stays
            // true: the alias really is what this was recorded about.
            Fact seen = new Fact(
                world.NewId("fact"), Minted, FactPredicates.Possesses, EntityId.Parse("item_sack"), "a sack");
            world.Knowledge.AddFact(seen);

            ActorIdentityIntake.Reconcile(world, MintedFromUid);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));

            Assert.True(reloaded.Registry.AllNpcs.ContainsKey(Minted));
            Assert.Equal(Authored, reloaded.Registry.GetNpc(Minted).AliasOf);
            Assert.Equal(Authored, reloaded.Registry.Canonical(Minted));
            Assert.False(reloaded.Registry.IsActor(Minted));
            Assert.True(reloaded.Registry.IsActor(Authored));

            // The fact still names the alias, unrewritten, and still resolves to the actor.
            Fact after = reloaded.Knowledge.Facts[seen.Id];
            Assert.Equal(Minted, after.Subject);
            Assert.Equal(Authored, reloaded.Registry.Canonical(after.Subject));
        }

        [Fact]
        public void ASaveWrittenBeforeThisRuleLoadsWithBothRecordsAndNoRetirement()
        {
            // Backward compatibility, stated as a test rather than assumed: an old save has no
            // alias field, every record loads canonical, and the duplicate is still a duplicate
            // until something reconciles it. Nothing is lost on the way in.
            NarrativeWorldState world = TwoIdsForOneBody();
            string json = WorldStateSerializer.Save(world).Replace("\"aliasOf\"", "\"retiredField\"");

            NarrativeWorldState reloaded = WorldStateSerializer.Load(json);

            Assert.Equal(2, reloaded.Registry.AllNpcs.Count);
            Assert.Equal(2, reloaded.Registry.Npcs.Count);
            Assert.True(reloaded.Registry.GetNpc(Minted).IsCanonical);

            Assert.Single(ActorIdentityIntake.Reconcile(reloaded, MintedFromUid));
            Assert.Equal(1, reloaded.Registry.Npcs.Count);
        }

        // -- casting --------------------------------------------------------------------------

        [Fact]
        public void CastingCannotBindTwoIdsOfOneBodyIntoTwoRoles()
        {
            // The failure at its sharpest. Two roles with one requirement, and a witness who is in
            // the world twice: without the invariant the scene casts the same person as both its
            // knowers and reads as two people agreeing with each other.
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId witness = lab.Situation.WitnessId;
            EntityId alias = AliasInTheWorldFor(lab, witness, "npc_vanilla_witness");

            // Three roles, and only two bodies in the room that know the theft: the thief and the
            // witness. Before reconciliation the witness's second id fills the third role, and the
            // scene reads as three people when it is two - one of whom agrees with herself.
            StoryletDefinition definition = TwoKnowers();
            definition.RequiredRoles.Add(new StoryletRole("third", StoryletRoleSource.AnyoneWhoKnowsFocus));

            StoryletOpportunity duplicated = StoryletEngine.Evaluate(definition, Casting(lab));
            Assert.True(duplicated.IsAvailable);
            Assert.Contains(alias, duplicated.RoleBindings.Values);
            Assert.Contains(witness, duplicated.RoleBindings.Values);

            ActorIdentityIntake.Reconcile(lab.World, MintedFromUid);

            // Afterwards the third role has nobody left, which is the honest answer: there is no
            // third person here.
            StoryletOpportunity crowded = StoryletEngine.Evaluate(definition, Casting(lab));
            Assert.False(crowded.IsAvailable);
            Assert.Contains("third", crowded.RefusalReason);

            definition.RequiredRoles.RemoveAt(2);
            StoryletOpportunity cast = StoryletEngine.Evaluate(definition, Casting(lab));
            Assert.True(cast.IsAvailable, cast.RefusalReason);
            Assert.DoesNotContain(alias, cast.RoleBindings.Values);
            Assert.NotEqual(cast.RoleBindings["first"], cast.RoleBindings["second"]);
        }

        [Fact]
        public void AThreadCastUnderARetiredIdStillFindsItsActor()
        {
            // The other direction, and the reason retiring is not deleting. A thread already cast
            // under an id that later turns out to be the alias must still be about somebody - the
            // person, under the id the world now knows them by.
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId witness = lab.Situation.WitnessId;
            EntityId alias = AliasInTheWorldFor(lab, witness, "npc_vanilla_witness");

            lab.Situation.Thread.ParticipantIds.Add(alias);
            ActorIdentityIntake.Reconcile(lab.World, MintedFromUid);

            StoryletDefinition definition = TwoKnowers();
            StoryletOpportunity cast = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(cast.IsAvailable, cast.RefusalReason);
            Assert.DoesNotContain(alias, cast.RoleBindings.Values);
            Assert.Contains(witness, cast.RoleBindings.Values);
        }

        [Fact]
        public void ARetiredIdIsNotADeadActorAndIsNotCountedAsAPerson()
        {
            // Retirement is a statement about identity, not about life. The canonical actor is
            // untouched, and nothing about the alias is presented as somebody having died.
            NarrativeWorldState world = TwoIdsForOneBody();
            ActorIdentityIntake.Reconcile(world, MintedFromUid);

            Assert.True(world.Registry.GetNpc(Minted).Alive);
            Assert.True(world.Registry.GetNpc(Authored).Alive);
            Assert.Equal(1, world.Registry.Npcs.Count);
            Assert.Equal(2, world.Registry.AllNpcs.Count);
        }

        // -- fixtures -------------------------------------------------------------------------

        /// <summary>
        /// One staged character the mod authored an id for, met a second time and registered again
        /// under an id minted from the same physical uid. The live defect, in the smallest world
        /// that can hold it.
        /// </summary>
        private static NarrativeWorldState TwoIdsForOneBody()
        {
            NarrativeWorldState world = new NarrativeWorldState(11);
            world.Registry.Add(new NarrativeNpc(Authored, "Miller") { VanillaCharaRef = "500" });
            world.Registry.Add(new NarrativeNpc(Minted, "Miller") { VanillaCharaRef = "500" });
            return world;
        }

        /// <summary>
        /// A second id for somebody already in the laboratory's world, standing where they stand
        /// and knowing what they know - the shape a uid-derived duplicate has in a live game.
        /// </summary>
        private static EntityId AliasInTheWorldFor(TheftLaboratory lab, EntityId actor, string aliasId)
        {
            EntityId alias = EntityId.Parse(aliasId);
            NarrativeNpc original = lab.World.Registry.GetNpc(actor);

            original.VanillaCharaRef = "500";
            lab.World.Registry.Add(new NarrativeNpc(alias, original.Name) { VanillaCharaRef = "500" });
            lab.Vanilla.Define(alias, zone: lab.Zone);
            lab.World.Knowledge.Teach(
                alias, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, false);
            return alias;
        }

        private static StoryletDefinition TwoKnowers()
        {
            StoryletDefinition definition = new StoryletDefinition("storylet.test.two_knowers");
            definition.Beats.Add(new StoryletBeat("open"));
            definition.RequiredRoles.Add(new StoryletRole("first", StoryletRoleSource.AnyoneWhoKnowsFocus));
            definition.RequiredRoles.Add(new StoryletRole("second", StoryletRoleSource.AnyoneWhoKnowsFocus));
            return definition;
        }

        private static StoryletCastingContext Casting(TheftLaboratory lab)
        {
            return new StoryletCastingContext(
                lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId);
        }
    }
}
