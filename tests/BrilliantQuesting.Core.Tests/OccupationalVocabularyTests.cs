using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-076. Lived work, hobby and service context subtly influencing vocabulary - a fragment
    /// pool narrowed the same way a caller-supplied tone already narrows it (BQ-074), from a
    /// source this simulation is not allowed to invent: <see cref="IdentityAffordances"/>, BQ-145's
    /// own anti-stereotype derivation.
    ///
    /// <list type="bullet">
    /// <item><see cref="OccupationalVocabulary.RequestedVocabulary"/> is a pure reading of
    /// identity affordances and nothing else;</item>
    /// <item>an identity nobody could read, or one described only by race and character
    /// archetype, requests nothing;</item>
    /// <item>a fragment tagged with a domain is eligible only when that domain was requested, so
    /// an unread identity never lets a flavoured line through;</item>
    /// <item>the identical act, decision and personality render recognizably differently through
    /// two different lived contexts; and</item>
    /// <item>every one of those renderings still carries the identical meaning.</item>
    /// </list>
    /// </summary>
    public class OccupationalVocabularyTests
    {
        private static readonly EntityId Nobody = EntityId.Parse("npc_nobody");

        // -- RequestedVocabulary is a pure reading of BQ-145's own derivation ------------------------

        [Fact]
        public void AnUnreadIdentityRequestsNoVocabulary()
        {
            Assert.Empty(OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Nothing));
            Assert.Empty(OccupationalVocabulary.RequestedVocabulary(
                IdentityAffordances.Derive(CharacterIdentity.UnknownFor(Nobody))));
        }

        [Fact]
        public void ANullIdentityRequestsNoVocabulary()
        {
            Assert.Empty(OccupationalVocabulary.RequestedVocabulary(null));
        }

        /// <summary>
        /// Race and character archetype are the two facets BQ-145 derives nothing at all from, on
        /// purpose - the two a stereotype would arrive through. This step reads only what BQ-145
        /// derived, so it inherits that refusal for free rather than re-implementing it.
        /// </summary>
        [Fact]
        public void RaceAndCharacterArchetypeAloneRequestNoVocabulary()
        {
            IdentityAffordances punk = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithCharacterArchetype("punk", "Punk")
                    .WithRace("fairy", "Fairy")
                    .WithHobbiesRead()
                    .WithInstitutionsRead()
                    .Build());

            Assert.Empty(OccupationalVocabulary.RequestedVocabulary(punk));
        }

        [Fact]
        public void ObservedWorkRequestsItsOwnDomain()
        {
            IdentityAffordances farmer = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).WithWork("farmer", "Farmer").Build());

            Assert.True(farmer.PlausibleKnowledgeOf(IdentityDomain.Cultivation) > 0.0);
            Assert.Equal(
                new[] { DialogueVocabulary.Cultivation },
                OccupationalVocabulary.RequestedVocabulary(farmer).ToArray());
        }

        [Fact]
        public void HobbyAloneStillRequestsItsDomain()
        {
            IdentityAffordances gardener = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).AddHobby("gardening").Build());

            Assert.Contains(DialogueVocabulary.Cultivation, OccupationalVocabulary.RequestedVocabulary(gardener));
        }

        [Fact]
        public void ServiceContributesTradeWhenNoWorkWasRead()
        {
            IdentityAffordances shopkeeper = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithService("TraitShopGeneral", null, ServiceAvailability.Offered)
                    .Build());

            Assert.Contains(DialogueVocabulary.Trade, OccupationalVocabulary.RequestedVocabulary(shopkeeper));
        }

        [Fact]
        public void RequestedVocabularyIsDeterministic()
        {
            IdentityAffordances brewer = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithWork("brewer", "Brewer")
                    .AddHobby("gardening")
                    .Build());

            Assert.Equal(
                OccupationalVocabulary.RequestedVocabulary(brewer),
                OccupationalVocabulary.RequestedVocabulary(brewer));
        }

        // -- FitsVocabulary is the only place either list is read ------------------------------------

        [Fact]
        public void AnUnmarkedFragmentFitsAnyVocabularyIncludingNone()
        {
            DialogueFragment plain = Fragment(Array.Empty<string>());

            Assert.True(plain.FitsVocabulary(Array.Empty<string>()));
            Assert.True(plain.FitsVocabulary(new[] { DialogueVocabulary.Cultivation }));
            Assert.True(plain.FitsVocabulary(null));
        }

        /// <summary>
        /// The one place this step's behaviour departs from tone's: asking for nothing excludes a
        /// vocabulary-tagged fragment rather than admitting it, because letting it through for an
        /// unread identity would be the guessed vocabulary BQ-145 already refuses to derive.
        /// </summary>
        [Fact]
        public void AVocabularyTaggedFragmentIsExcludedWhenNothingIsRequested()
        {
            DialogueFragment farmerFlavoured = Fragment(new[] { DialogueVocabulary.Cultivation });

            Assert.False(farmerFlavoured.FitsVocabulary(Array.Empty<string>()));
            Assert.False(farmerFlavoured.FitsVocabulary(null));
        }

        [Fact]
        public void AVocabularyTaggedFragmentFitsOnlyItsOwnDomain()
        {
            DialogueFragment farmerFlavoured = Fragment(new[] { DialogueVocabulary.Cultivation });

            Assert.True(farmerFlavoured.FitsVocabulary(new[] { DialogueVocabulary.Cultivation }));
            Assert.False(farmerFlavoured.FitsVocabulary(new[] { DialogueVocabulary.Trade }));
        }

        /// <summary>
        /// A tag outside this closed vocabulary - the free tags BQ-077 is expected to add its own
        /// meaning to - carries no occupational opinion and is left alone.
        /// </summary>
        [Fact]
        public void AnUnrecognisedTagHasNoVocabularyOpinion()
        {
            DialogueFragment other = Fragment(new[] { "never_begs" });

            Assert.True(other.FitsVocabulary(Array.Empty<string>()));
            Assert.True(other.FitsVocabulary(new[] { DialogueVocabulary.Cultivation }));
        }

        private static DialogueFragment Fragment(IReadOnlyList<string> tags)
        {
            return new DialogueFragment(
                "test.fragment",
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: null,
                tags: tags,
                repetitionGroup: null,
                slots: null);
        }

        // -- the done-when, said out loud --------------------------------------------------------

        /// <summary>
        /// The step's condition, in this codebase's terms: the identical refusal - same act, same
        /// disclosure decision, same speaker personality throughout - said with two different
        /// lived contexts comes out recognizably different, and every rendering still means the
        /// refusal it started as.
        /// </summary>
        [Fact]
        public void TheIdenticalRefusalSoundsDifferentThroughTwoLivedContexts()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            string meaning = request.Act.Signature;

            IReadOnlyList<string> farmer = OccupationalVocabulary.RequestedVocabulary(
                IdentityAffordances.Derive(new CharacterIdentityBuilder(Nobody).WithWork("farmer", "Farmer").Build()));
            IReadOnlyList<string> guard = OccupationalVocabulary.RequestedVocabulary(
                IdentityAffordances.Derive(new CharacterIdentityBuilder(Nobody)
                    .AddInstitution("city_of_yowyn", "TraitGuard").Build()));

            HashSet<string> farmerLines = Rendered(scene, request, farmer, meaning);
            HashSet<string> guardLines = Rendered(scene, request, guard, meaning);

            Assert.NotEmpty(farmerLines);
            Assert.NotEmpty(guardLines);
            Assert.True(
                farmerLines.Except(guardLines).Any() || guardLines.Except(farmerLines).Any(),
                "the two lived contexts produced the identical set of lines: " + string.Join(" / ", farmerLines));
        }

        /// <summary>
        /// The other half: an identity nobody could read never introduces a flavoured line, even
        /// though the same flavoured fragments exist in the library and would be reachable for a
        /// farmer saying the identical refusal.
        /// </summary>
        [Fact]
        public void AnUnreadIdentityNeverIntroducesInventedVocabulary()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();

            IReadOnlyList<string> nothing = OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Nothing);
            Assert.Empty(nothing);

            request.Vocabulary = nothing;
            IReadOnlyList<DialogueFragment> modifiers = scene.Realizer.Candidates(FragmentPosition.Modifier, request);

            foreach (DialogueFragment fragment in modifiers)
            {
                Assert.All(fragment.Tags, tag => Assert.False(DialogueVocabulary.IsVocabulary(tag)));
            }
        }

        /// <summary>
        /// Race and character archetype alone must not imply occupational vocabulary or
        /// personality: a character described only as a Punk and a fairy renders the identical
        /// candidate pool an unread identity would.
        /// </summary>
        [Fact]
        public void RaceOrArchetypeAloneNeverUnlocksAFlavouredFragment()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();

            IdentityAffordances punk = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody)
                    .WithCharacterArchetype("punk", "Punk")
                    .WithRace("fairy", "Fairy")
                    .Build());

            request.Vocabulary = OccupationalVocabulary.RequestedVocabulary(punk);
            IReadOnlyList<DialogueFragment> modifiers = scene.Realizer.Candidates(FragmentPosition.Modifier, request);

            foreach (DialogueFragment fragment in modifiers)
            {
                Assert.All(fragment.Tags, tag => Assert.False(DialogueVocabulary.IsVocabulary(tag)));
            }
        }

        /// <summary>
        /// The other side of the same coin: a farmer's own lived context does make the cultivation
        /// modifier reachable for the identical refusal.
        /// </summary>
        [Fact]
        public void AFarmersOwnContextUnlocksTheCultivationModifier()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            IdentityAffordances farmer = IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).WithWork("farmer", "Farmer").Build());
            request.Vocabulary = OccupationalVocabulary.RequestedVocabulary(farmer);

            IReadOnlyList<DialogueFragment> modifiers = scene.Realizer.Candidates(FragmentPosition.Modifier, request);

            Assert.Contains(modifiers, fragment => fragment.Id == "mod.refuse.cultivation");
            Assert.DoesNotContain(modifiers, fragment => fragment.Id == "mod.refuse.trade");
        }

        [Fact]
        public void EveryRenderingUnderEveryLivedContextCarriesTheUnchangedMeaning()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            string meaning = request.Act.Signature;

            IReadOnlyList<string>[] contexts =
            {
                OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Nothing),
                OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Derive(
                    new CharacterIdentityBuilder(Nobody).WithWork("farmer", "Farmer").Build())),
                OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Derive(
                    new CharacterIdentityBuilder(Nobody).AddInstitution("city_of_yowyn", "TraitGuard").Build())),
            };

            foreach (IReadOnlyList<string> vocabulary in contexts)
            {
                request.Vocabulary = vocabulary;
                foreach (RealizedLine line in scene.Renderings(request, 10))
                {
                    Assert.True(line.Rendered, line.Refusal);
                    Assert.Equal(meaning, line.Meaning);
                }
            }
        }

        /// <summary>
        /// A voice and a vocabulary narrow the same pool independently and without reading each
        /// other - the same act, said with a lived context, still respects a tonal request.
        /// </summary>
        [Fact]
        public void VocabularyAndToneNarrowIndependently()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Vocabulary = OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Derive(
                new CharacterIdentityBuilder(Nobody).WithWork("farmer", "Farmer").Build()));
            request.Tone = new[] { DialogueTones.Curt };

            foreach (RealizedLine line in scene.Renderings(request, 20))
            {
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
            }
        }

        private static HashSet<string> Rendered(
            FragmentRealizationTests.Scene scene,
            RealizationRequest request,
            IReadOnlyList<string> vocabulary,
            string expectedMeaning)
        {
            request.Vocabulary = vocabulary;
            HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(expectedMeaning, line.Meaning);
                lines.Add(line.Text);
            }

            return lines;
        }
    }
}
