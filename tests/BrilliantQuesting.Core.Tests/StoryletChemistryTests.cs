using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-068. Everybody here is already qualified; this is about which of them make a scene.
    ///
    /// The fixture is deliberately flat: three people who know exactly the same thing, in the same
    /// place, with the same standing and no history, so that one dimension can be switched on at a
    /// time and the score has nowhere else to come from. The first candidate in the pool is always
    /// the one BQ-067 would have cast, which makes every test below also a test that chemistry
    /// changed the answer for a reason it can name - or did not change it at all.
    /// </summary>
    public class StoryletChemistryTests
    {
        // -- the done-when ---------------------------------------------------------------------

        /// <summary>
        /// A proud debtor and a proud former friend both outscore an indifferent pairing, and when
        /// all three are qualified for the same role the indifferent one is not who gets cast.
        /// </summary>
        [Fact]
        public void ADebtorAndAFormerFriendBothOutscoreAnIndifferentPairing()
        {
            double indifferent = Scene.WithAccuser(delegate(Scene scene) { scene.AddKnower("stranger"); });

            double debtor = Scene.WithAccuser(delegate(Scene scene)
            {
                EntityId who = scene.AddKnower("debtor");
                scene.Owes(who, scene.Accused, sentiment: -45);
            });

            double formerFriend = Scene.WithAccuser(delegate(Scene scene)
            {
                EntityId who = scene.AddKnower("former_friend");
                scene.Soured(who, scene.Accused);
            });

            Assert.Equal(0.0, indifferent);
            Assert.True(debtor > indifferent, "a debtor is not worth more than a stranger");
            Assert.True(formerFriend > indifferent, "a soured friendship is not worth more than a stranger");

            // Put all three in front of the same role, with the stranger first in the order the
            // unscored engine searches, and the stranger is not who plays the scene.
            Scene together = Scene.Create();
            EntityId stranger = together.AddKnower("stranger");
            EntityId owes = together.AddKnower("debtor");
            together.Owes(owes, together.Accused, sentiment: -45);
            EntityId soured = together.AddKnower("former_friend");
            together.Soured(soured, together.Accused);

            StoryletOpportunity opportunity = together.Cast();
            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(3, opportunity.GroupsConsidered);
            Assert.NotEqual(stranger, opportunity.RoleBindings["accuser"]);
            Assert.Contains(opportunity.RoleBindings["accuser"], new[] { owes, soured });
        }

        // -- one dimension at a time -----------------------------------------------------------

        /// <summary>Two people who want things that cannot both happen.</summary>
        [Fact]
        public void GoalConflictAloneDecidesTheCast()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("plain");
            EntityId charged = scene.AddKnower("charged");
            scene.World.Registry.GetNpc(charged).Goals.Add(
                new NpcGoal("recover_property", scene.Accused, 80, "he still has my ring"));

            scene.AssertOnly(ChemistryDimension.GoalConflict, charged, "wants recover_property of");
        }

        /// <summary>They already know each other, and it is not neutral.</summary>
        [Fact]
        public void SharedHistoryAloneDecidesTheCast()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("plain");
            EntityId charged = scene.AddKnower("charged");
            scene.World.Relationships.ConnectMutual(charged, scene.Accused, RelationKind.Rival, -50);

            scene.AssertOnly(ChemistryDimension.SharedHistory, charged, "rival");
        }

        /// <summary>One of them can prove what the other can only say.</summary>
        [Fact]
        public void KnowledgeAsymmetryAloneDecidesTheCast()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("plain");
            EntityId charged = scene.AddKnower("charged");
            scene.World.Knowledge.Teach(
                charged,
                scene.Focus.Id,
                KnowledgeSource.Witnessed,
                Scene.Confidence,
                GameTime.Zero,
                canProve: true,
                proofs: new[] { new ProofLink(ProofKind.WitnessTestimony, charged) });

            scene.AssertOnly(ChemistryDimension.KnowledgeAsymmetry, charged, "can prove it");
        }

        /// <summary>
        /// One of them holds an office and the other does not - read from the derived identity
        /// affordances of BQ-145, and only ever as the difference between the two.
        /// </summary>
        [Fact]
        public void PowerAsymmetryAloneDecidesTheCast()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("plain");
            EntityId charged = scene.AddKnower("charged");
            scene.Vanilla.SetCharacterIdentity(charged, new CharacterIdentityBuilder(charged)
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build());

            scene.AssertOnly(ChemistryDimension.PowerAsymmetry, charged, "institution 'TraitGuard'");
        }

        // -- what chemistry is not allowed to be -------------------------------------------------

        /// <summary>
        /// Eligibility is BQ-067's and runs first. The best group in the town is not a group if one
        /// of its members does not meet the requirement, and no score can put them in the role.
        /// </summary>
        [Fact]
        public void ChemistryCannotCastSomebodyWhoDoesNotQualify()
        {
            Scene scene = Scene.Create();
            EntityId plain = scene.AddKnower("plain");

            // Everything chemistry could want, and no knowledge of what the scene is about.
            EntityId ignorant = scene.AddPerson("ignorant");
            scene.World.Relationships.ConnectMutual(ignorant, scene.Accused, RelationKind.Enemy, -90);
            scene.World.Registry.GetNpc(ignorant).Goals.Add(
                new NpcGoal("ruin", scene.Accused, 100, "he took everything"));
            scene.Vanilla.SetCharacterIdentity(ignorant, new CharacterIdentityBuilder(ignorant)
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build());

            StoryletOpportunity opportunity = scene.Cast();

            Assert.Equal(plain, opportunity.RoleBindings["accuser"]);
            Assert.DoesNotContain(ignorant, opportunity.RoleBindings.Values);
            Assert.Equal(1, opportunity.GroupsConsidered);
            Assert.True(opportunity.Chemistry.IsFlat);
        }

        /// <summary>
        /// And a role nobody here meets stays uncast, however much chemistry the room holds.
        /// </summary>
        [Fact]
        public void ARoleNobodyQualifiesForStaysUncastHoweverGoodTheGroupWouldBe()
        {
            Scene scene = Scene.Create();
            EntityId enemy = scene.AddKnower("enemy");
            scene.World.Relationships.ConnectMutual(enemy, scene.Accused, RelationKind.Enemy, -100);

            StoryletDefinition definition = Scene.Definition();
            definition.RequiredRoles.Add(new StoryletRole("prover", StoryletRoleSource.AnyoneWhoCanProveFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, scene.Casting());

            Assert.False(opportunity.IsAvailable);
            Assert.Contains("prover", opportunity.RefusalReason);
        }

        /// <summary>
        /// Forming whole groups is what makes chemistry possible, and it buys one thing on its own:
        /// where taking the obvious person for the first role left the second role with nobody, the
        /// search tries the next qualified person instead of reporting the scene uncastable.
        ///
        /// Nobody enters a role they do not meet by this route - the corroborator here is still the
        /// only person who can prove anything, and the accuser is still somebody who knows.
        /// </summary>
        [Fact]
        public void GroupFormationBacktracksRatherThanReportingACastableSceneUncastable()
        {
            Scene scene = Scene.Create();
            EntityId onlyProver = scene.AddProver("only_prover");
            EntityId knowerOnly = scene.AddKnower("knower_only");

            StoryletDefinition definition = Scene.Definition();
            definition.RequiredRoles.Add(new StoryletRole("corroborator", StoryletRoleSource.AnyoneWhoCanProveFocus));

            StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, scene.Casting());

            // The unscored engine would have given the accuser role to the only person who can
            // prove anything, because they are first in the pool, and then had nobody left.
            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(knowerOnly, opportunity.RoleBindings["accuser"]);
            Assert.Equal(onlyProver, opportunity.RoleBindings["corroborator"]);
            Assert.True(scene.World.Knowledge.CanProve(onlyProver, scene.Focus.Id));
            Assert.False(scene.World.Knowledge.CanProve(knowerOnly, scene.Focus.Id));
        }

        /// <summary>
        /// A character archetype and a race are not chemistry. They derive nothing at BQ-145, so
        /// they can reach no weight here at any size: nobody is a better accuser for being a Punk.
        /// </summary>
        [Fact]
        public void ArchetypeAndRaceAreNotChemistry()
        {
            Scene scene = Scene.Create();
            EntityId plain = scene.AddKnower("plain");
            EntityId punk = scene.AddKnower("punk");
            scene.Vanilla.SetCharacterIdentity(punk, new CharacterIdentityBuilder(punk)
                .WithCharacterArchetype("Punk", "Punk")
                .WithRace("fairy", "Fairy")
                .Build());

            StoryletOpportunity opportunity = scene.Cast();

            Assert.True(opportunity.Chemistry.IsFlat);
            Assert.Equal(plain, opportunity.RoleBindings["accuser"]);
        }

        /// <summary>
        /// Standing is chemistry only where it is unequal. Two people who hold the same office
        /// score exactly what two people who hold none score, and which of the pair holds it makes
        /// no difference to the total - it is a relation, not a ranking of who is worth casting.
        /// </summary>
        [Fact]
        public void StandingScoresTheDifferenceAndNotThePerson()
        {
            double neither = Scene.WithAccuser(delegate(Scene scene) { scene.AddKnower("plain"); });

            double bothGuards = Scene.WithAccuser(delegate(Scene scene)
            {
                EntityId guard = scene.AddKnower("guard");
                scene.MakeGuard(guard);
                scene.MakeGuard(scene.Accused);
            });

            double accuserIsTheGuard = Scene.WithAccuser(delegate(Scene scene)
            {
                scene.MakeGuard(scene.AddKnower("guard"));
            });

            double accusedIsTheGuard = Scene.WithAccuser(delegate(Scene scene)
            {
                scene.AddKnower("plain");
                scene.MakeGuard(scene.Accused);
            });

            Assert.Equal(neither, bothGuards);
            Assert.True(accuserIsTheGuard > neither);
            Assert.Equal(accuserIsTheGuard, accusedIsTheGuard);
        }

        /// <summary>
        /// Chemistry is a property of the group. A candidate whose whole history is with somebody
        /// who is not in the scene brings nothing to it, which is what stops this from becoming a
        /// per-person interestingness score wearing a group's clothes.
        /// </summary>
        [Fact]
        public void HistoryWithSomebodyOutsideTheCastIsNotChemistry()
        {
            Scene scene = Scene.Create();
            EntityId plain = scene.AddKnower("plain");
            EntityId connected = scene.AddKnower("connected");
            EntityId bystander = scene.AddPerson("bystander");
            scene.World.Relationships.ConnectMutual(connected, bystander, RelationKind.Enemy, -100);
            scene.World.Registry.GetNpc(connected).Goals.Add(
                new NpcGoal("ruin", bystander, 100, "an old quarrel with somebody else"));

            StoryletOpportunity opportunity = scene.Cast();

            Assert.True(opportunity.Chemistry.IsFlat);
            Assert.Equal(plain, opportunity.RoleBindings["accuser"]);
        }

        // -- determinism -------------------------------------------------------------------------

        /// <summary>
        /// Identical authoritative state casts identically, and two groups nothing distinguishes
        /// fall back to the order the unscored engine searched in - so a tie is not a coin toss.
        /// </summary>
        [Fact]
        public void TiesAreBrokenByTheUnscoredOrderAndTheAnswerIsStable()
        {
            Scene scene = Scene.Create();
            EntityId first = scene.AddKnower("first");
            EntityId second = scene.AddKnower("second");
            scene.World.Relationships.ConnectMutual(first, scene.Accused, RelationKind.Rival, -50);
            scene.World.Relationships.ConnectMutual(second, scene.Accused, RelationKind.Rival, -50);

            StoryletOpportunity opportunity = scene.Cast();
            Assert.Equal(first, opportunity.RoleBindings["accuser"]);
            Assert.NotEqual(second, opportunity.RoleBindings["accuser"]);

            for (int i = 0; i < 8; i++)
            {
                StoryletOpportunity again = scene.Cast();
                Assert.Equal(opportunity.RoleBindings, again.RoleBindings);
                Assert.Equal(opportunity.Chemistry.Total, again.Chemistry.Total);
                Assert.Equal(opportunity.Chemistry.Explain(), again.Chemistry.Explain());
            }
        }

        /// <summary>Two worlds built the same way score the same, sentence for sentence.</summary>
        [Fact]
        public void TwoIdenticalWorldsProduceTheSameSelectionAndTheSameReasons()
        {
            StoryletOpportunity left = Contested().Cast();
            StoryletOpportunity right = Contested().Cast();

            Assert.Equal(left.RoleBindings, right.RoleBindings);
            Assert.Equal(left.GroupsConsidered, right.GroupsConsidered);
            Assert.Equal(left.Chemistry.Total, right.Chemistry.Total);
            Assert.Equal(left.Chemistry.Explain(), right.Chemistry.Explain());
        }

        // -- explainability ----------------------------------------------------------------------

        /// <summary>
        /// The inspector answers both questions separately: what qualified each of them, and why
        /// these people rather than the others who also qualified.
        /// </summary>
        [Fact]
        public void TheInspectorNamesWhyThisGroupWasPreferred()
        {
            string report = NarrativeInspector.DescribeCasting(Contested().Cast());

            Assert.Contains("casting for storylet.test.chemistry", report);
            Assert.Contains("accuser: ", report);
            Assert.Contains("knows what happened", report);
            Assert.Contains("qualified groups", report);
            Assert.Contains("shared history accused/accuser", report);
            Assert.Contains("creditor", report);
            Assert.Contains("power asymmetry", report);
        }

        /// <summary>
        /// A flat score is reported as one. Most towns most of the time have nothing to choose
        /// between, and a report that printed nothing there would read as a report that failed.
        /// </summary>
        [Fact]
        public void AFlatSceneSaysSoRatherThanPrintingNothing()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("plain");

            string report = NarrativeInspector.DescribeCasting(scene.Cast());

            Assert.Contains("chemistry: 0.00 over 1 qualified group", report);
            Assert.Contains("nothing ties these people to each other", report);
        }

        /// <summary>
        /// Every reason names two roles and a dimension, and the reasons account for the whole of
        /// the total - there is no unattributed remainder for anybody to argue with.
        /// </summary>
        [Fact]
        public void TheReasonsAccountForTheWholeScore()
        {
            StoryletOpportunity opportunity = Contested().Cast();

            Assert.NotEmpty(opportunity.Chemistry.Reasons);
            Assert.Equal(
                opportunity.Chemistry.Total,
                opportunity.Chemistry.Reasons.Sum(r => r.Weight),
                10);

            foreach (ChemistryReason reason in opportunity.Chemistry.Reasons)
            {
                Assert.NotEqual(reason.LeftRole, reason.RightRole);
                Assert.NotEmpty(reason.LeftRole);
                Assert.NotEmpty(reason.RightRole);
                Assert.NotEmpty(reason.Detail);
                Assert.True(reason.Weight > 0.0, reason.Describe());
            }
        }

        /// <summary>A creditor with an office: two dimensions, both named, over three groups.</summary>
        private static Scene Contested()
        {
            Scene scene = Scene.Create();
            scene.AddKnower("stranger");
            EntityId creditor = scene.AddKnower("creditor");
            scene.Owes(scene.Accused, creditor, sentiment: -40);
            scene.MakeGuard(creditor);
            scene.AddKnower("another_stranger");
            return scene;
        }

        /// <summary>
        /// One theft, one accused, and however many equally qualified accusers a test wants.
        ///
        /// Everybody added knows the focus fact from the same source at the same confidence with
        /// no proof, holds no standing and no ties, and stands in the same zone - so the only
        /// thing that can move the score is what the test puts there.
        /// </summary>
        private sealed class Scene
        {
            public const double Confidence = 0.9;

            private int _added;

            private Scene(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId zone, EntityId player)
            {
                World = world;
                Vanilla = vanilla;
                Zone = zone;
                Player = player;
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public EntityId Zone { get; }

            public EntityId Player { get; }

            public EntityId Accused { get; private set; }

            public NarrativeThread Thread { get; private set; }

            public Fact Focus { get; private set; }

            public static Scene Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(20260902UL);
                EntityId player = EntityId.Parse("npc_player");
                EntityId zone = EntityId.Parse("zone_town");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, zone: zone);
                world.Registry.Add(new NarrativeNpc(player, "You"));

                Scene scene = new Scene(world, vanilla, zone, player);
                scene.Accused = scene.AddPerson("accused");

                EntityId ring = EntityId.Parse("item_ring");
                Fact focus = new Fact(
                    EntityId.Parse("fact_theft"), scene.Accused, FactPredicates.Stole, ring, "a silver ring");
                world.Knowledge.AddFact(focus);
                scene.Focus = focus;

                NarrativeThread thread = new NarrativeThread(
                    EntityId.Parse("thread_theft"), "petty_theft", GameTime.Zero);
                thread.SiteIds.Add(zone);
                thread.FactIds.Add(focus.Id);
                thread.ParticipantIds.Add(scene.Accused);
                scene.Thread = thread;

                // The accused knows what they did, on the same terms as everybody else, so that
                // knowledge asymmetry starts at nothing rather than at "the accuser knows".
                world.Knowledge.Teach(
                    scene.Accused, focus.Id, KnowledgeSource.Witnessed, Confidence, GameTime.Zero, canProve: false);
                return scene;
            }

            /// <summary>Somebody standing here who knows nothing about the theft.</summary>
            public EntityId AddPerson(string name)
            {
                EntityId id = EntityId.Parse("npc_" + name);
                Vanilla.Define(id, zone: Zone);
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }

            /// <summary>
            /// Somebody who qualifies for the accuser role, added to the pool after everybody
            /// already there - so the first one a test adds is the one BQ-067 would have cast.
            /// </summary>
            public EntityId AddKnower(string name)
            {
                EntityId id = AddPerson(name);
                World.Knowledge.Teach(
                    id, Focus.Id, KnowledgeSource.Witnessed, Confidence, GameTime.Zero, canProve: false);
                Thread.ParticipantIds.Add(id);
                _added++;
                return id;
            }

            /// <summary>A knower who can also prove it.</summary>
            public EntityId AddProver(string name)
            {
                EntityId id = AddKnower(name);
                World.Knowledge.Teach(
                    id,
                    Focus.Id,
                    KnowledgeSource.Witnessed,
                    Confidence,
                    GameTime.Zero,
                    canProve: true,
                    proofs: new[] { new ProofLink(ProofKind.WitnessTestimony, id) });
                return id;
            }

            public void Owes(EntityId debtor, EntityId creditor, int sentiment)
            {
                World.Relationships.Connect(debtor, creditor, RelationKind.Debtor, sentiment);
                World.Relationships.Connect(creditor, debtor, RelationKind.Creditor, sentiment);
            }

            /// <summary>A friendship one of the two has stopped meaning.</summary>
            public void Soured(EntityId who, EntityId about)
            {
                World.Relationships.Connect(about, who, RelationKind.Friend, 50);
                World.Relationships.Connect(who, about, RelationKind.Friend, -60);
            }

            public void MakeGuard(EntityId who)
            {
                Vanilla.SetCharacterIdentity(who, new CharacterIdentityBuilder(who)
                    .AddInstitution("city_of_yowyn", "TraitGuard")
                    .Build());
            }

            public static StoryletDefinition Definition()
            {
                StoryletDefinition definition = new StoryletDefinition("storylet.test.chemistry");
                definition.RequiredRoles.Add(new StoryletRole("accused", StoryletRoleSource.FactSubject));
                definition.RequiredRoles.Add(new StoryletRole("accuser", StoryletRoleSource.AnyoneWhoKnowsFocus));
                definition.Beats.Add(new StoryletBeat("open"));
                return definition;
            }

            public StoryletCastingContext Casting()
            {
                return new StoryletCastingContext(World, Vanilla, Thread, Focus.Id);
            }

            public StoryletOpportunity Cast()
            {
                StoryletOpportunity opportunity = StoryletEngine.Evaluate(Definition(), Casting());
                Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
                return opportunity;
            }

            /// <summary>What one candidate is worth to the accused, with nothing else in the room.</summary>
            public static double WithAccuser(Action<Scene> arrange)
            {
                Scene scene = Create();
                arrange(scene);
                return scene.Cast().Chemistry.Total;
            }

            /// <summary>
            /// The charged candidate was preferred to the plain one, the named dimension is the
            /// whole of why, and the sentence a reader gets says what in the world supports it.
            /// </summary>
            public void AssertOnly(ChemistryDimension dimension, EntityId expected, string detail)
            {
                StoryletOpportunity opportunity = Cast();

                Assert.Equal(_added, opportunity.GroupsConsidered);
                Assert.Equal(expected, opportunity.RoleBindings["accuser"]);
                Assert.True(opportunity.Chemistry.TotalFor(dimension) > 0.0, dimension + " did not fire");

                foreach (ChemistryDimension other in Enum.GetValues(typeof(ChemistryDimension)).Cast<ChemistryDimension>())
                {
                    if (other != dimension)
                    {
                        Assert.Equal(0.0, opportunity.Chemistry.TotalFor(other));
                    }
                }

                Assert.Contains(
                    opportunity.Chemistry.Reasons,
                    r => r.Dimension == dimension && r.Detail.Contains(detail));
            }
        }
    }
}
