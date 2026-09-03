using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-145: what an identity makes plausible, derived once, and unable to decide anything it is
    /// not entitled to decide.
    ///
    /// The step's done-when is the anti-stereotype gate, and the gate is a pair of tests that have
    /// to fail in opposite directions. Two characters the game describes identically must still be
    /// able to act differently, because identity is not personality. Two characters with the same
    /// personality must differ in what is plausible, who they are eligible to be and what they
    /// stand to lose - and in nothing else - because identity is not decoration either. Everything
    /// else here guards the ways a derivation like this usually goes wrong: quietly minting
    /// knowledge, quietly defaulting an unread facet, quietly leaking into personality or mutation
    /// policy, and quietly producing weights nobody can attribute to anything.
    /// </summary>
    public class IdentityAffordanceTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Brewer = EntityId.Parse("npc_brewer");
        private static readonly EntityId Watchman = EntityId.Parse("npc_watchman");
        private static readonly EntityId Nobody = EntityId.Parse("npc_nobody");

        // -- the anti-stereotype gate ----------------------------------------------------------

        /// <summary>
        /// Identity is not personality. The game says the same thing about both of these people;
        /// they meet the same loss in opposite ways, because what they are like was generated
        /// without ever consulting what they do.
        /// </summary>
        [Fact]
        public void SameIdentityWithOppositePersonalitiesChoosesDifferentActions()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeNpc patient = Neutral(EntityId.Parse("npc_patient"));
            NarrativeNpc schemer = Neutral(EntityId.Parse("npc_schemer"));

            vanilla.SetCharacterIdentity(patient.Id, BrewerIdentity(patient.Id));
            vanilla.SetCharacterIdentity(schemer.Id, BrewerIdentity(schemer.Id));

            // Identical identity, down to the derived affordances.
            Assert.Equal(
                Describe(IdentityAffordances.Of(patient, vanilla)),
                Describe(IdentityAffordances.Of(schemer, vanilla)));

            patient.Personality.Patience = 1.0;
            patient.Personality.Boldness = 0.0;
            patient.ProblemSolving.Set(ProblemSolvingStyle.Wait, 1.0);

            schemer.Personality.Honesty = 0.0;
            schemer.Personality.Humility = 0.0;
            schemer.Sensitivities.Set(SensitivityTopic.Status, 1.0);
            schemer.ProblemSolving.Set(ProblemSolvingStyle.Manipulate, 1.0);

            MissingGoatResponse patientChoice = MissingGoatProblemSolver.Choose(patient).Response;
            MissingGoatResponse schemerChoice = MissingGoatProblemSolver.Choose(schemer).Response;

            Assert.NotEqual(patientChoice, schemerChoice);
            Assert.Equal(MissingGoatResponse.ComplainAndWait, patientChoice);
            Assert.Equal(MissingGoatResponse.AccuseRival, schemerChoice);
        }

        /// <summary>
        /// The other half of the gate. Same personality, different identity: what changes is what
        /// is plausible, what they are eligible for and what is at stake. What does not change is
        /// anything about who they are, and the proof is that the chosen action is the same one.
        /// </summary>
        [Fact]
        public void SamePersonalityWithDifferentIdentityChangesOnlyPlausibilityEligibilityAndStakes()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeNpc brewer = Neutral(Brewer);
            NarrativeNpc watchman = Neutral(Watchman);

            vanilla.SetCharacterIdentity(Brewer, BrewerIdentity(Brewer));
            vanilla.SetCharacterIdentity(Watchman, WatchmanIdentity(Watchman));

            IdentityAffordances brewing = IdentityAffordances.Of(brewer, vanilla);
            IdentityAffordances watching = IdentityAffordances.Of(watchman, vanilla);

            // Plausibility differs.
            Assert.True(brewing.PlausibleKnowledgeOf(IdentityDomain.Craft) > 0.0);
            Assert.Equal(0.0, brewing.PlausibleKnowledgeOf(IdentityDomain.PublicOrder));
            Assert.True(watching.PlausibleKnowledgeOf(IdentityDomain.PublicOrder) > 0.0);
            Assert.Equal(0.0, watching.PlausibleKnowledgeOf(IdentityDomain.Craft));

            // Eligibility differs.
            Assert.True(brewing.IsEligibleFor(IdentityRole.ServiceOperator));
            Assert.False(brewing.IsEligibleFor(IdentityRole.Authority));
            Assert.True(watching.IsEligibleFor(IdentityRole.Authority));
            Assert.False(watching.IsEligibleFor(IdentityRole.ServiceOperator));

            // Stakes differ.
            Assert.True(brewing.ExposureTo(IdentityStakeKind.Business) > 0.0);
            Assert.Equal(0.0, brewing.ExposureTo(IdentityStakeKind.Standing));
            Assert.True(watching.ExposureTo(IdentityStakeKind.Standing) > 0.0);
            Assert.Equal(0.0, watching.ExposureTo(IdentityStakeKind.Business));

            // Nothing about who they are moved, and neither did what they decide to do.
            Assert.Equal(DescribePersonality(brewer), DescribePersonality(watchman));
            Assert.Equal(
                MissingGoatProblemSolver.Choose(brewer).Response,
                MissingGoatProblemSolver.Choose(watchman).Response);
        }

        /// <summary>
        /// The facets a stereotype would arrive through. A character who is only ever described as
        /// a Punk, or only ever as a fairy, derives nothing: species and presentation say nothing
        /// about what somebody can do, is entitled to, or would lose.
        /// </summary>
        [Fact]
        public void RaceAndCharacterArchetypeDeriveNothing()
        {
            IdentityAffordances punk = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithCharacterArchetype("punk", "Punk")
                    .WithRace("fairy", "Fairy")
                    .WithHobbiesRead()
                    .WithInstitutionsRead()
                    .Build());

            Assert.True(punk.IsEmpty);
            Assert.Empty(punk.RoleEligibility);
            Assert.Empty(punk.Stakes);
            Assert.Empty(punk.ContributingFacets);
        }

        // -- the work column is not an occupation ----------------------------------------------

        /// <summary>
        /// The BQ-144 runtime finding, pinned as a rule.
        ///
        /// Elin's work column is a build column before it is a trade: live diagnostics have it
        /// answering `predator` for shopkeeper-like NPCs and for horses, and `tourist` for nuns.
        /// A work id nothing recognises as a lived trade is carried through as the observation it
        /// is and derives nothing - not a weaker livelihood, not a livelihood with a caveat. BQ
        /// asserting that a horse has a trade to lose is the same stereotype failure as asserting
        /// a Punk is aggressive, arriving through the one facet that was still ungated.
        /// </summary>
        [Theory]
        [InlineData("predator")]
        [InlineData("tourist")]
        [InlineData("berserker")]
        public void AMechanicalWorkIdIsObservedAndDerivesNoLivelihood(string workId)
        {
            CharacterIdentity observed = new CharacterIdentityBuilder(Nobody)
                .WithWork(workId)
                .WithHobbiesRead()
                .WithInstitutionsRead()
                .Build();

            // The observation keeps it verbatim. BQ-144 stays honest whatever BQ-145 makes of it.
            Assert.True(observed.Work.IsKnown);
            Assert.Equal(workId, observed.Work.VanillaId);

            IdentityAffordances derived = IdentityAffordances.Derive(observed);

            Assert.Equal(0.0, derived.ExposureTo(IdentityStakeKind.Livelihood));
            Assert.Empty(derived.Stakes);
            Assert.Empty(derived.RoleEligibility);
            Assert.False(derived.Service.IsProvider);
            Assert.DoesNotContain(IdentityFacetKind.Work, derived.ContributingFacets);
            Assert.True(derived.IsEmpty);
        }

        /// <summary>
        /// The other direction, so the gate cannot be satisfied by deriving nothing from anybody.
        /// A work id that reads as a lived trade still stakes a livelihood, and still names the
        /// facet that did it.
        /// </summary>
        [Theory]
        [InlineData("farmer")]
        [InlineData("brewer")]
        [InlineData("merchant")]
        [InlineData("guard")]
        public void RecognisedLivedWorkStillStakesALivelihood(string workId)
        {
            IdentityAffordances derived = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).WithWork(workId).Build());

            Assert.True(derived.ExposureTo(IdentityStakeKind.Livelihood) > 0.0);
            Assert.Contains(IdentityFacetKind.Work, derived.ContributingFacets);
            Assert.Contains(derived.Explain(), line => line.StartsWith("at stake livelihood"));
        }

        /// <summary>
        /// An unrecognised work id costs the livelihood and nothing else. Observed service is its
        /// own evidence: a shopkeeper whose work column says `predator` still runs a business,
        /// because the shop is read from the trait and not from the job template.
        /// </summary>
        [Fact]
        public void ObservedServiceSurvivesAMechanicalWorkId()
        {
            IdentityAffordances derived = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithWork("predator")
                    .WithService("TraitShopGeneral", null, ServiceAvailability.Offered)
                    .Build());

            Assert.True(derived.Service.IsProvider);
            Assert.True(derived.Service.AvailableNow);
            Assert.True(derived.IsEligibleFor(IdentityRole.ServiceOperator));
            Assert.True(derived.ExposureTo(IdentityStakeKind.Business) > 0.0);

            // The business is the shop's, not the job template's.
            Assert.Equal(0.0, derived.ExposureTo(IdentityStakeKind.Livelihood));
            Assert.Equal(
                new[] { IdentityFacetKind.Service },
                derived.ContributingFacets.ToArray());
        }

        /// <summary>
        /// An office is still an office. A guard whose work column reads as a combat template
        /// keeps the standing the institutional facet grants, because the two facets fail
        /// separately and always did.
        /// </summary>
        [Fact]
        public void AnOfficeSurvivesAMechanicalWorkId()
        {
            IdentityAffordances derived = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithWork("predator")
                    .AddInstitution("city_of_yowyn", "TraitGuard")
                    .Build());

            Assert.True(derived.IsEligibleFor(IdentityRole.Authority));
            Assert.True(derived.ExposureTo(IdentityStakeKind.Standing) > 0.0);
            Assert.Equal(0.0, derived.ExposureTo(IdentityStakeKind.Livelihood));
        }

        // -- unknown contributes nothing -------------------------------------------------------

        [Fact]
        public void AFullyUnreadIdentityDerivesNothingRatherThanADefault()
        {
            IdentityAffordances nothing = IdentityAffordances.Derive(CharacterIdentity.UnknownFor(Nobody));

            Assert.True(nothing.IsEmpty);
            Assert.Empty(nothing.PlausibleKnowledge);
            Assert.Empty(nothing.PlausibleInterests);
            Assert.Empty(nothing.RoleEligibility);
            Assert.Empty(nothing.Stakes);
            Assert.False(nothing.Service.IsProvider);

            foreach (IdentityDomain domain in Enum.GetValues(typeof(IdentityDomain)).Cast<IdentityDomain>())
            {
                Assert.Equal(0.0, nothing.PlausibleKnowledgeOf(domain));
                Assert.Equal(0.0, nothing.PlausibleInterestIn(domain));
            }
        }

        /// <summary>
        /// One unread facet costs its own affordances and no others - the same independence the
        /// observation itself guarantees, carried through the derivation.
        /// </summary>
        [Fact]
        public void AnUnreadFacetCostsOnlyItsOwnAffordances()
        {
            IdentityAffordances shopOnly = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithService("TraitShopGeneral", null, ServiceAvailability.Offered)
                    .Build());

            Assert.True(shopOnly.Service.IsProvider);
            Assert.True(shopOnly.IsEligibleFor(IdentityRole.ServiceOperator));
            Assert.True(shopOnly.ExposureTo(IdentityStakeKind.Business) > 0.0);

            // Work went unread, so there is no livelihood and no authority - not a weaker version
            // of either.
            Assert.Equal(0.0, shopOnly.ExposureTo(IdentityStakeKind.Livelihood));
            Assert.False(shopOnly.IsEligibleFor(IdentityRole.Authority));
            Assert.DoesNotContain(IdentityFacetKind.Work, shopOnly.ContributingFacets);
        }

        /// <summary>
        /// Unknown availability is not a closed shop. The provider stands; only the claim about
        /// right now is withheld.
        /// </summary>
        [Fact]
        public void UnreadServiceAvailabilityClaimsNeitherOpenNorShut()
        {
            IdentityAffordances unread = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).WithService("TraitShopBar").Build());

            Assert.True(unread.Service.IsProvider);
            Assert.False(unread.Service.AvailableNow);
            Assert.False(unread.Service.KnownUnavailable);

            IdentityAffordances shut = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithService("TraitShopBar", null, ServiceAvailability.NotOffered)
                    .Build());

            Assert.True(shut.Service.IsProvider);
            Assert.False(shut.Service.AvailableNow);
            Assert.True(shut.Service.KnownUnavailable);
        }

        // -- plausible is not actual -----------------------------------------------------------

        /// <summary>
        /// The line the whole step exists to hold. A brewer plausibly knows who buys ale here;
        /// deriving that must not put a single fact in the graph or a single belief in their head.
        /// </summary>
        [Fact]
        public void DerivingAffordancesAddsNoFactAndTeachesNobodyAnything()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeWorldState world = new NarrativeWorldState(7);
            NarrativeNpc brewer = Neutral(Brewer);
            world.Registry.Add(brewer);
            vanilla.SetCharacterIdentity(Brewer, BrewerIdentity(Brewer));

            int factsBefore = world.Knowledge.Facts.Count;

            IdentityAffordances affordances = IdentityAffordances.Of(brewer, vanilla);

            Assert.True(affordances.PlausibleKnowledgeOf(IdentityDomain.Trade) > 0.0);
            Assert.Equal(factsBefore, world.Knowledge.Facts.Count);
            Assert.Empty(world.Knowledge.BeliefsOf(Brewer));
        }

        // -- explainability --------------------------------------------------------------------

        [Fact]
        public void EveryDerivedAffordanceNamesTheFacetBehindIt()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeNpc watchman = Neutral(Watchman);
            vanilla.SetCharacterIdentity(Watchman, WatchmanIdentity(Watchman));

            IdentityAffordances affordances = IdentityAffordances.Of(watchman, vanilla);

            foreach (IdentityDomainAffordance domain in affordances.PlausibleKnowledge)
            {
                Assert.NotEmpty(domain.Sources);
            }

            foreach (IdentityDomainAffordance domain in affordances.PlausibleInterests)
            {
                Assert.NotEmpty(domain.Sources);
            }

            foreach (IdentityRoleEligibility role in affordances.RoleEligibility)
            {
                Assert.NotNull(role.Source);
            }

            foreach (IdentityStake stake in affordances.Stakes)
            {
                Assert.NotNull(stake.Source);
            }

            // A weight that did not fire is as attributable as one that did.
            Assert.Equal(
                "plausible knowledge alchemy (no identity facet)",
                affordances.ExplainKnowledge(IdentityDomain.Alchemy));
            Assert.Equal(
                "plausible knowledge public order (institution 'TraitGuard')",
                affordances.ExplainKnowledge(IdentityDomain.PublicOrder));
        }

        [Fact]
        public void TheInspectorNamesTheFacetBehindEveryIdentityDerivedWeight()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeWorldState world = new NarrativeWorldState(11);
            NarrativeNpc brewer = Neutral(Brewer);
            world.Registry.Add(brewer);
            vanilla.SetCharacterIdentity(Brewer, BrewerIdentity(Brewer));

            string report = NarrativeInspector.DescribeCharacter(world, vanilla, Brewer);

            Assert.Contains("identity affordances:", report);
            Assert.Contains("plausible knowledge craft 1.00 (work 'brewer')", report);
            Assert.Contains("plausible interest cultivation 0.80 (hobby 'gardening')", report);
            Assert.Contains("eligible for service operator (service 'TraitShopBar')", report);
            Assert.Contains("at stake livelihood 0.60 (work 'brewer')", report);
            Assert.Contains("at stake business 0.75 (service 'TraitShopBar')", report);
        }

        [Fact]
        public void AnIdentityThatImpliesNothingSaysSoInTheInspector()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeWorldState world = new NarrativeWorldState(13);
            NarrativeNpc nobody = Neutral(Nobody);
            world.Registry.Add(nobody);

            string report = NarrativeInspector.DescribeCharacter(world, vanilla, Nobody);

            Assert.Contains("identity affordances: none derived (no identity facet contributes)", report);
        }

        // -- one derivation, and one only ------------------------------------------------------

        /// <summary>
        /// The observation wins where it answered. What BQ authored fills only a facet the game
        /// declined to describe, and says so when it does - so a report never passes this
        /// simulation's own authorship off as Elin's answer.
        /// </summary>
        [Fact]
        public void AnObservedFacetOverridesWhatThisSimulationAuthored()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeNpc npc = Neutral(Brewer);
            npc.Occupation = "shopkeeper";

            Assert.Equal(
                "plausible knowledge trade (authored work 'shopkeeper')",
                IdentityAffordances.Of(npc, vanilla).ExplainKnowledge(IdentityDomain.Trade));

            vanilla.SetCharacterIdentity(Brewer, new CharacterIdentityBuilder(Brewer)
                .WithWork("guard", "Guard")
                .Build());

            IdentityAffordances observed = IdentityAffordances.Of(npc, vanilla);
            Assert.Equal(0.0, observed.PlausibleKnowledgeOf(IdentityDomain.Trade));
            Assert.Equal(
                "plausible knowledge public order (work 'guard')",
                observed.ExplainKnowledge(IdentityDomain.PublicOrder));
        }

        /// <summary>
        /// The authority intake reads the derivation rather than the observation, so which office
        /// counts as which standing is decided in exactly one place.
        /// </summary>
        [Fact]
        public void AuthorityStandingComesFromTheOneDerivation()
        {
            CharacterIdentity guard = new CharacterIdentityBuilder(Watchman)
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build();
            CharacterIdentity clerk = new CharacterIdentityBuilder(Watchman)
                .AddInstitution("mages_guild", "TraitGuildPersonnel")
                .Build();

            Assert.Equal(
                new[] { AuthorityPolicy.GuardRole },
                AuthorityPolicy.RoleWordsFor(IdentityAffordances.Derive(guard)).ToArray());
            Assert.Equal(
                new[] { AuthorityPolicy.GuildRole },
                AuthorityPolicy.RoleWordsFor(IdentityAffordances.Derive(clerk)).ToArray());

            // An office this build spells in a way nothing recognises grants no standing, and an
            // unread facet grants none either.
            Assert.Empty(AuthorityPolicy.RoleWordsFor(IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Watchman).AddInstitution("somewhere", "TraitOstler").Build())));
            Assert.Empty(AuthorityPolicy.RoleWordsFor(
                IdentityAffordances.Derive(CharacterIdentity.UnknownFor(Watchman))));
        }

        /// <summary>
        /// Interpretation consumes the derivation rather than a private occupation table, and the
        /// score terms it produces carry the facet through to the inspector.
        /// </summary>
        [Fact]
        public void InterpretationWeighsTheDerivedAffordancesAndNamesTheFacet()
        {
            SandboxVanillaState vanilla = Town1();
            NarrativeWorldState world = new NarrativeWorldState(17);
            EntityId crop = EntityId.Parse("item_crop");
            EntityId sourceFact = EntityId.Parse("fact_crop_damage");
            Fact damaged = new Fact(sourceFact, crop, FactPredicates.Damaged, EntityId.None, "blighted crop");
            world.Knowledge.AddFact(damaged);

            NarrativeNpc watchman = Neutral(Watchman);
            world.Registry.Add(watchman);
            vanilla.SetCharacterIdentity(Watchman, WatchmanIdentity(Watchman));

            ActorInterpretationTrace trace = ActorLocalInterpreter.Interpret(
                world, Watchman, sourceFact, GameTime.Zero, vanilla);

            Assert.Equal(FactPredicates.MayBeSabotaged, trace.DerivedPredicate);
            Assert.Contains(
                trace.ScoreTerms,
                term => term.StartsWith("plausible knowledge public order (institution 'TraitGuard')"));
            Assert.Contains(
                trace.ScoreTerms,
                term => term.StartsWith("identity eligibility authority (institution 'TraitGuard')"));
        }

        // -- what identity may never touch -----------------------------------------------------

        /// <summary>
        /// BQ-056 ... BQ-060 and the BQ-031 mutation policy take no identity input, and this is a
        /// structural proof rather than a behavioural one: a leak would arrive as somebody adding a
        /// parameter, a field or a property, and assertions about today's behaviour would not
        /// notice. A Punk is not aggressive because they are a Punk, and a costume is not a
        /// permission.
        /// </summary>
        [Fact]
        public void PersonalityGenerationAndMutationPolicyTakeNoIdentityInput()
        {
            Type[] mustNotSeeIdentity =
            {
                // BQ-056 ... BQ-060: dimensions, problem-solving style, sensitivities,
                // contradictions, quirks.
                typeof(PersonalityWeights),
                typeof(ProblemSolvingProfile),
                typeof(SensitivityProfile),
                typeof(ContradictionProfile),
                typeof(CharacterQuirkProfile),
                typeof(CharacterQuirkAssignment),

                // BQ-031: how far the mod may reach into somebody.
                typeof(NarrativeMutationPolicy),
                typeof(MutationPolicies)
            };

            Type[] identityTypes =
            {
                typeof(CharacterIdentity),
                typeof(IdentityFacet),
                typeof(IdentityFacetKind),
                typeof(ServiceRole),
                typeof(InstitutionalRole),
                typeof(IdentityAffordances),
                typeof(IdentityDomain),
                typeof(IdentityRole),
                typeof(IdentityStakeKind),
                typeof(IdentityStake),
                typeof(IdentityRoleEligibility),
                typeof(IdentityDomainAffordance),
                typeof(IdentityServiceCapability),
                typeof(IdentityFacetReference)
            };

            HashSet<Type> forbidden = new HashSet<Type>(identityTypes);
            const BindingFlags Everything = BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.Instance | BindingFlags.Static
                                            | BindingFlags.DeclaredOnly;

            foreach (Type type in mustNotSeeIdentity)
            {
                foreach (MethodBase method in type.GetMethods(Everything).Cast<MethodBase>()
                             .Concat(type.GetConstructors(Everything)))
                {
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        Assert.False(
                            forbidden.Contains(parameter.ParameterType),
                            type.Name + "." + method.Name + " takes identity input via "
                            + parameter.ParameterType.Name);
                    }
                }

                foreach (FieldInfo field in type.GetFields(Everything))
                {
                    Assert.False(
                        forbidden.Contains(field.FieldType),
                        type.Name + "." + field.Name + " holds identity via " + field.FieldType.Name);
                }

                foreach (PropertyInfo property in type.GetProperties(Everything))
                {
                    Assert.False(
                        forbidden.Contains(property.PropertyType),
                        type.Name + "." + property.Name + " exposes identity via "
                        + property.PropertyType.Name);
                }
            }
        }

        // -- fixtures ----------------------------------------------------------------------------

        private static SandboxVanillaState Town1()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Town);
            vanilla.Define(Brewer, zone: Town);
            vanilla.Define(Watchman, zone: Town);
            vanilla.Define(Nobody, zone: Town);
            return vanilla;
        }

        /// <summary>Somebody with a trade, a shop, and something to do on their own time.</summary>
        private static CharacterIdentity BrewerIdentity(EntityId actor)
        {
            return new CharacterIdentityBuilder(actor)
                .WithCharacterArchetype("punk", "Punk")
                .WithRace("fairy", "Fairy")
                .WithWork("brewer", "Brewer")
                .AddHobby("gardening")
                .WithService("TraitShopBar", null, ServiceAvailability.Offered)
                .WithInstitutionsRead()
                .Build();
        }

        /// <summary>Somebody holding an office, with no trade and no shop the build could read.</summary>
        private static CharacterIdentity WatchmanIdentity(EntityId actor)
        {
            return new CharacterIdentityBuilder(actor)
                .WithCharacterArchetype("punk", "Punk")
                .WithRace("fairy", "Fairy")
                .WithHobbiesRead()
                .AddInstitution("city_of_yowyn", "TraitGuard")
                .Build();
        }

        /// <summary>
        /// A character with nothing distinctive about them, so that anything two of them do
        /// differently is the thing under test rather than the generator's spread.
        /// </summary>
        private static NarrativeNpc Neutral(EntityId id)
        {
            NarrativeNpc npc = new NarrativeNpc(id, id.Value);
            foreach (ProblemSolvingStyle style in Enum.GetValues(typeof(ProblemSolvingStyle)).Cast<ProblemSolvingStyle>())
            {
                npc.ProblemSolving.Set(style, 0.5);
            }

            foreach (SensitivityTopic topic in Enum.GetValues(typeof(SensitivityTopic)).Cast<SensitivityTopic>())
            {
                npc.Sensitivities.Set(topic, 0.0);
            }

            return npc;
        }

        private static string Describe(IdentityAffordances affordances) =>
            string.Join("|", affordances.Explain().ToArray());

        private static string DescribePersonality(NarrativeNpc npc)
        {
            List<string> parts = new List<string>();
            foreach (PropertyInfo property in typeof(PersonalityWeights)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(double))
                {
                    parts.Add(property.Name + "=" + ((double)property.GetValue(npc.Personality)).ToString("0.000"));
                }
            }

            foreach (ProblemSolvingStyle style in Enum.GetValues(typeof(ProblemSolvingStyle)).Cast<ProblemSolvingStyle>())
            {
                parts.Add(style + "=" + npc.ProblemSolving.Get(style).ToString("0.000"));
            }

            foreach (SensitivityTopic topic in Enum.GetValues(typeof(SensitivityTopic)).Cast<SensitivityTopic>())
            {
                parts.Add(topic + "=" + npc.Sensitivities.Get(topic).ToString("0.000"));
            }

            return string.Join("|", parts.ToArray());
        }
    }
}
