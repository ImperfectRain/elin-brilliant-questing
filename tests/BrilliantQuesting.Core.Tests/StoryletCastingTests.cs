using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-067. A storylet names roles, never people; who fills them is decided here, from whoever
    /// is actually in the town the scene happens in.
    ///
    /// The two regression tests in the middle are not hypothetical. Both defects were live in the
    /// shipped content before casting existed, because role binding was positional: whatever sat
    /// in a slot became the role, qualified or not.
    /// </summary>
    public class StoryletCastingTests
    {
        [Fact]
        public void TheSameStoryletCastsDifferentActorsInTwoTowns()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();

            Town first = Town.Of(lab, lab.Situation, lab.Zone);
            Town second = Town.Elsewhere(lab, seed: 991117UL);

            IReadOnlyList<StoryletOpportunity> here = engine.Find(first.Casting());
            IReadOnlyList<StoryletOpportunity> there = engine.Find(second.Casting());

            // The same definitions play in both places - however many the library has grown to.
            // Counting them would be a test of how much content exists rather than of casting.
            Assert.Equal(
                here.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal),
                there.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal));
            Assert.True(here.Count >= 5, "only " + here.Count + " scenes were available");

            foreach (StoryletOpportunity opportunity in here)
            {
                StoryletOpportunity twin = Assert.Single(there, o => o.Definition.Id == opportunity.Definition.Id);

                // Same roles, and not one person in common between the two casts.
                Assert.Equal(
                    opportunity.RoleBindings.Keys.OrderBy(k => k, StringComparer.Ordinal),
                    twin.RoleBindings.Keys.OrderBy(k => k, StringComparer.Ordinal));
                Assert.Empty(opportunity.RoleBindings.Values.Intersect(twin.RoleBindings.Values));

                first.AssertReadsCorrectly(opportunity);
                second.AssertReadsCorrectly(twin);
            }
        }

        [Fact]
        public void ARoleNeverCastsSomethingThatIsNotAPerson()
        {
            // The focus fact is "the thief stole the ring", so its object slot holds the ring.
            // Before BQ-067 this bound the ring itself as the injured party and the storylet
            // reported itself playable with an item in a speaking role.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.injured_party");
            definition.RequiredRoles.Add(new StoryletRole("injured_party", StoryletRoleSource.FactObject));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("injured_party", opportunity.RefusalReason);
            Assert.DoesNotContain(lab.Situation.ItemId, opportunity.RoleBindings.Values);
        }

        [Fact]
        public void TheInjuredPartyIsWhoeverTheWorldSaysOwnsTheThing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.owner");
            definition.RequiredRoles.Add(new StoryletRole("injured_party", StoryletRoleSource.OwnerOfFocusObject));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(lab.Situation.VictimId, opportunity.RoleBindings["injured_party"]);
        }

        [Fact]
        public void NobodyHoldsTwoRolesInOneScene()
        {
            // Two roles with the same requirement. The thief knows the theft because he did it,
            // and the witness because she saw it: two roles, two people. A third such role has
            // nobody left, and says so rather than repeating somebody.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.two_knowers");
            definition.RequiredRoles.Add(new StoryletRole("first", StoryletRoleSource.AnyoneWhoKnowsFocus));
            definition.RequiredRoles.Add(new StoryletRole("second", StoryletRoleSource.AnyoneWhoKnowsFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.NotEqual(opportunity.RoleBindings["first"], opportunity.RoleBindings["second"]);

            definition.RequiredRoles.Add(new StoryletRole("third", StoryletRoleSource.AnyoneWhoKnowsFocus));
            StoryletOpportunity crowded = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.False(crowded.IsAvailable);
            Assert.Contains("third", crowded.RefusalReason);
        }

        [Fact]
        public void ARoleTheFocusFactNamesIsNeverTakenByAnotherRole()
        {
            // The accused is the person the fact is about; a corroborating knower must be
            // somebody else, even though the accused is the one who knows it best.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.accusation");
            definition.RequiredRoles.Add(new StoryletRole("accused", StoryletRoleSource.FactSubject));
            definition.RequiredRoles.Add(new StoryletRole("knower", StoryletRoleSource.AnyParticipantWhoKnowsFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(lab.Situation.ThiefId, opportunity.RoleBindings["accused"]);
            Assert.Equal(lab.Situation.WitnessId, opportunity.RoleBindings["knower"]);
        }

        [Fact]
        public void CastingRejectsTheDeadAndThePlayer()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.knower");
            definition.RequiredRoles.Add(new StoryletRole("knower", StoryletRoleSource.AnyoneWhoKnowsFocus));

            // The player knows nothing here, and is never written into a role in any case.
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, false);
            lab.Vanilla.Kill(lab.Situation.WitnessId);

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(lab.Situation.ThiefId, opportunity.RoleBindings["knower"]);
            Assert.DoesNotContain(lab.Player, opportunity.RoleBindings.Values);
        }

        [Fact]
        public void CastingRejectsSomebodyWhoIsNotInThePlace()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.knower");
            definition.RequiredRoles.Add(new StoryletRole("knower", StoryletRoleSource.AnyoneWhoKnowsFocus));

            // Both knowers travel. The scene has nobody to play it, which is a refusal rather
            // than a conversation with somebody who is two towns away.
            EntityId elsewhere = lab.World.NewId("zone");
            lab.Vanilla.SetZone(lab.Situation.ThiefId, elsewhere);
            lab.Vanilla.SetZone(lab.Situation.WitnessId, elsewhere);

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("knower", opportunity.RefusalReason);
        }

        [Fact]
        public void StandingIsCastFromWhoActuallyHoldsIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.mediator");
            definition.RequiredRoles.Add(new StoryletRole("mediator", StoryletRoleSource.AnyoneWithStandingHere));

            Assert.False(StoryletEngine.Evaluate(definition, Casting(lab)).IsAvailable);

            lab.World.Registry.GetNpc(lab.Situation.VictimId).Roles.Add("guard");
            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(lab.Situation.VictimId, opportunity.RoleBindings["mediator"]);
        }

        [Fact]
        public void ACastSceneCanSayWhoIsInItAndWhy()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.notes");
            definition.RequiredRoles.Add(new StoryletRole("accused", StoryletRoleSource.FactSubject));
            definition.RequiredRoles.Add(new StoryletRole("accuser", StoryletRoleSource.AnyoneWhoKnowsFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

            Assert.Equal(2, opportunity.CastingNotes.Count);
            Assert.Contains(opportunity.CastingNotes, n => n.StartsWith("accused: ", StringComparison.Ordinal) && n.Contains("the fact is about them"));
            Assert.Contains(opportunity.CastingNotes, n => n.StartsWith("accuser: ", StringComparison.Ordinal) && n.Contains("knows what happened"));
            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.ThiefId), string.Join(" ", opportunity.CastingNotes));
        }

        [Fact]
        public void RolesAreNeverIdentities()
        {
            // Casting writes to the firing and to nothing else: nobody becomes an accuser.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletDefinition definition = Minimal("storylet.test.identity");
            definition.RequiredRoles.Add(new StoryletRole("accuser", StoryletRoleSource.AnyoneWhoKnowsFocus));
            StoryletEngine engine = new StoryletEngine();
            engine.Register(definition);

            StoryletOpportunity opportunity = Assert.Single(engine.Find(Casting(lab)));
            EntityId accuser = opportunity.RoleBindings["accuser"];
            engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);

            Assert.Empty(lab.World.Registry.GetNpc(accuser).Roles);
            Assert.Equal(accuser, Assert.Single(lab.Situation.Thread.StoryletFirings).RoleBindings["accuser"]);
        }

        [Fact]
        public void AFiredSceneSurvivesTheSaveBecauseEveryRoleHoldsAPerson()
        {
            // Save integrity quarantines a thread whose storylet firing names a role holder the
            // registry has never heard of. A cast that could put an item in a role was therefore
            // not only wrong on the page - it wrote a situation the next load would throw away.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();

            IReadOnlyList<StoryletOpportunity> opportunities = engine.Find(Casting(lab));
            Assert.True(opportunities.Count >= 5, "only " + opportunities.Count + " scenes were available");
            foreach (StoryletOpportunity opportunity in opportunities)
            {
                engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);
            }

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            NarrativeThread thread = Assert.Single(reloaded.Threads);

            Assert.NotEqual(ThreadState.Quarantined, thread.State);
            Assert.Equal(opportunities.Count, thread.StoryletFirings.Count);
            foreach (StoryletFiring firing in thread.StoryletFirings)
            {
                foreach (KeyValuePair<string, EntityId> role in firing.RoleBindings)
                {
                    Assert.True(
                        reloaded.Registry.Npcs.ContainsKey(role.Value),
                        firing.StoryletId + " role " + role.Key + " is not a person");
                }
            }
        }

        private static StoryletCastingContext Casting(TheftLaboratory lab)
        {
            return new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId);
        }

        private static StoryletDefinition Minimal(string id)
        {
            StoryletDefinition definition = new StoryletDefinition(id);
            definition.Beats.Add(new StoryletBeat("open"));
            return definition;
        }

        private static StoryletEngine ShippedEngine()
        {
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine engine = StoryletContent.CreateEngine(bundle.Bundle, out diagnostics);
            Assert.Empty(bundle.Diagnostics);
            Assert.Empty(diagnostics);
            return engine;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Could not locate repository root.");
            }

            return directory.FullName;
        }

        /// <summary>One settlement with its own theft, its own people, and its own stolen thing.</summary>
        private sealed class Town
        {
            private readonly TheftLaboratory _lab;
            private readonly PettyTheftSituation _situation;
            private readonly EntityId _zone;

            private Town(TheftLaboratory lab, PettyTheftSituation situation, EntityId zone)
            {
                _lab = lab;
                _situation = situation;
                _zone = zone;
            }

            public static Town Of(TheftLaboratory lab, PettyTheftSituation situation, EntityId zone)
            {
                return new Town(lab, situation, zone);
            }

            /// <summary>A second town in the same world: same archetype, nobody in common.</summary>
            public static Town Elsewhere(TheftLaboratory lab, ulong seed)
            {
                EntityId zone = lab.World.NewId("zone");
                PettyTheftSituation situation = PettyTheftSituation.Create(
                    lab.World,
                    new SandboxStager(lab.Vanilla),
                    zone,
                    lab.Vanilla.Now,
                    seed);
                return new Town(lab, situation, zone);
            }

            public StoryletCastingContext Casting()
            {
                return new StoryletCastingContext(_lab.World, _lab.Vanilla, _situation.Thread, _situation.TheftFactId);
            }

            /// <summary>
            /// Every role holds somebody this town can actually produce, and holds the right one:
            /// the accused is who the fact is about, the injured party owns what was taken, and
            /// whoever speaks knows what happened.
            /// </summary>
            public void AssertReadsCorrectly(StoryletOpportunity opportunity)
            {
                Assert.NotEmpty(opportunity.RoleBindings);
                Assert.Equal(opportunity.RoleBindings.Count, opportunity.RoleBindings.Values.Distinct().Count());

                foreach (KeyValuePair<string, EntityId> binding in opportunity.RoleBindings)
                {
                    Assert.NotNull(_lab.World.Registry.GetNpc(binding.Value));
                    Assert.True(_lab.Vanilla.IsAlive(binding.Value), binding.Key + " is not alive");
                    Assert.Equal(_zone, _lab.Vanilla.GetZoneOf(binding.Value));
                    Assert.NotEqual(_lab.Player, binding.Value);
                }

                foreach (string accused in new[] { "accused", "subject", "suspect", "confessor" })
                {
                    if (opportunity.RoleBindings.ContainsKey(accused))
                    {
                        Assert.Equal(_situation.ThiefId, opportunity.RoleBindings[accused]);
                    }
                }

                foreach (string injured in new[] { "victim", "injured_party" })
                {
                    if (opportunity.RoleBindings.ContainsKey(injured))
                    {
                        Assert.Equal(_situation.VictimId, opportunity.RoleBindings[injured]);
                    }
                }

                foreach (string speaking in new[] { "accuser", "challenger", "requester", "speaker", "listener", "knower", "witness" })
                {
                    if (opportunity.RoleBindings.ContainsKey(speaking))
                    {
                        Assert.True(
                            _lab.World.Knowledge.Knows(opportunity.RoleBindings[speaking], _situation.TheftFactId),
                            speaking + " does not know what happened");
                    }
                }
            }
        }
    }
}
