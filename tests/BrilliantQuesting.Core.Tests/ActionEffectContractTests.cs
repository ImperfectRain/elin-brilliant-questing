using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-010. What a registered verb says it could change, read off the verb rather than off a
    /// table beside it, and enough for a want to find a route without anybody switching on its
    /// name.
    /// </summary>
    public class ActionEffectContractTests
    {
        /// <summary>
        /// The five areas the step names - property, resource, information, protection and
        /// obligation - each answered by more than one verb, discovered by asking the registry
        /// what kind of change a verb could make rather than by naming verbs anywhere.
        /// </summary>
        [Fact]
        public void RepresentativeFamiliesExposeTheirEffectsThroughTheRegistry()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            Assert.Superset(
                new HashSet<string> { "return_item", "pickpocket", "carry", "fence", "smuggle" },
                Ids(registry.Advancing(SemanticEffects.PossessionTransferred)));
            Assert.Superset(
                new HashSet<string> { "buy_supplies", "provide_supplies", "cook", "repair", "escort" },
                Ids(registry.Advancing(SemanticEffects.ResourceSupplied)));
            Assert.Superset(
                new HashSet<string> { "question", "search", "eavesdrop", "read", "compare_testimony" },
                Ids(registry.Advancing(SemanticEffects.InformationLearned)));
            Assert.Superset(
                new HashSet<string> { "rescue", "shelter", "assign_protection", "escort", "restrain" },
                Ids(registry.Advancing(SemanticEffects.PersonSecured)));
            Assert.Superset(
                new HashSet<string> { "pay_debt", "call_favor", "persuade" },
                Ids(registry.Advancing(SemanticEffects.ObligationAltered)));

            // The declaration is the verb's own, not a lookup somebody could forget to update.
            foreach (NarrativeAction action in registry.Advancing(SemanticEffects.PossessionTransferred))
            {
                Assert.True(action.Effects.Advances(SemanticEffects.PossessionTransferred));
            }
        }

        /// <summary>
        /// Sufficiency for generic matching: every registered desired-condition term names the
        /// kind of change that would move it, and some registered verb makes that kind of change.
        /// A term nothing answers is a want with no route, and it is reported here rather than
        /// discovered as silence.
        /// </summary>
        [Fact]
        public void EveryRegisteredDesiredConditionHasADeclaredRouteInTheLibrary()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            IReadOnlyList<string> terms = GoalConditionRegistry.RegisteredKinds();
            Assert.NotEmpty(terms);

            foreach (string term in terms)
            {
                IReadOnlyList<string> advancing = GoalConditionRegistry.AdvancedBy(term);
                Assert.NotNull(advancing);
                Assert.NotEmpty(advancing);

                foreach (string effect in advancing)
                {
                    Assert.True(SemanticEffects.IsRegistered(effect), term + " names unregistered effect " + effect);
                    Assert.NotEmpty(registry.Advancing(effect));
                }
            }
        }

        /// <summary>
        /// Telling somebody a thing makes it more provable, not less, so disclosure must not be
        /// offered as a way to keep a claim unproven. The term names the change it wants; it does
        /// not accept everything that touches the same claim.
        /// </summary>
        [Fact]
        public void ADesiredConditionOnlyNamesChangesThatWouldActuallyMoveIt()
        {
            IReadOnlyList<string> unproven = GoalConditionRegistry.AdvancedBy(GoalConditionKinds.ClaimUnproven);

            Assert.Contains(SemanticEffects.EvidenceRemoved, unproven);
            Assert.DoesNotContain(SemanticEffects.InformationDisclosed, unproven);
            Assert.DoesNotContain(SemanticEffects.EvidenceCreated, unproven);

            // And the verb that starts a fight is never a route to somebody staying alive.
            Assert.DoesNotContain(
                SemanticEffects.PersonHarmed,
                GoalConditionRegistry.AdvancedBy(GoalConditionKinds.PersonAlive));
        }

        /// <summary>
        /// The step's central claim: a new verb using existing vocabulary joins every want that
        /// vocabulary answers, by being registered and for no other reason. Nothing here names
        /// the verb, and no goal-name switch was edited to reach it.
        /// </summary>
        [Fact]
        public void AddingAVerbWithExistingVocabularyNeedsNoCentralSwitch()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();
            int before = registry.Advancing(SemanticEffects.PossessionTransferred).Count;

            registry.Register(new RansomAction());

            List<NarrativeAction> routes = registry.Advancing(SemanticEffects.PossessionTransferred);
            Assert.Equal(before + 1, routes.Count);
            Assert.Contains("ransom", Ids(routes));

            // Reached the way a want would reach it: term, then effect kind, then verbs.
            List<string> forRecoveredProperty = new List<string>();
            foreach (string effect in GoalConditionRegistry.AdvancedBy(GoalConditionKinds.PropertyOwnedBy))
            {
                forRecoveredProperty.AddRange(Ids(registry.Advancing(effect)));
            }

            Assert.Contains("ransom", forRecoveredProperty);
        }

        /// <summary>
        /// A verb's binding requirement is its own too. The projection refuses to offer an
        /// unpointed verb without knowing anything about which verb it is.
        /// </summary>
        [Fact]
        public void SemanticSlotRequirementsAreDeclaredByTheVerbNotSwitchedOnItsId()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            RansomAction ransom = new RansomAction();

            // Nothing points this attempt at anything: no matter, no subject, no binding.
            ActionContext bare = new ActionContext(
                lab.World, lab.Vanilla, lab.Checks, lab.World.Rng, lab.Player, lab.Situation.ThiefId);

            Assert.Equal(new[] { SemanticSlots.Item }, ransom.Effects.NeedsAnyOf);
            Assert.False(ActionBinding.HasRequiredSemanticSlots(ransom, bare));

            ActionContext pointed = lab.Context(lab.Situation.ThiefId);
            pointed.Binding = new ActionBinding { Item = lab.Situation.ItemId };
            Assert.True(ActionBinding.HasRequiredSemanticSlots(ransom, pointed));

            // A verb that declares no slots is attemptable unbound, which is most of the library.
            Assert.True(ActionBinding.HasRequiredSemanticSlots(lab.Actions.Get("rapport"), bare));
        }

        /// <summary>
        /// Inspection is a read. Asking every verb what it could change, and asking the registry
        /// where it has said nothing, leaves the world byte-identical - no roll, no event, no
        /// fact and no availability side effect.
        /// </summary>
        [Fact]
        public void EffectInspectionMutatesNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            string before = WorldStateSerializer.Save(lab.World);
            int eventsBefore = lab.World.Ledger.Events.Count;

            foreach (NarrativeAction action in lab.Actions.Actions)
            {
                foreach (string kind in SemanticEffects.All)
                {
                    action.CanPotentiallyAdvance(kind, lab.Vanilla, out string _);
                    action.Effects.Advances(kind);
                }
            }

            foreach (string kind in SemanticEffects.All)
            {
                lab.Actions.Advancing(kind);
                lab.Actions.Advancing(kind, lab.Vanilla);
            }

            lab.Actions.EffectCoverage();

            Assert.Equal(before, WorldStateSerializer.Save(lab.World));
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);
        }

        /// <summary>
        /// Where the library has said nothing, it says so. An undeclared verb is a reported gap
        /// rather than a silent "this changes nothing", and no verb has invented a term.
        /// </summary>
        [Fact]
        public void MissingCoverageIsReportedRatherThanDefaultedAway()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();
            ActionEffectCoverage coverage = registry.EffectCoverage();

            Assert.Empty(coverage.UnregisteredKinds);
            Assert.Empty(coverage.UnansweredKinds);
            Assert.Equal(SemanticEffects.All.Count, coverage.DeclaredKinds.Count);

            // The gap is named, and it is exactly the verbs nobody has yet been able to describe
            // honestly - an institutional grant, a god's gift, a household place, a business
            // changing hands. Each is a later step's question, not a silent default here.
            Assert.Equal(
                new[]
                {
                    "buy_business",
                    "host",
                    "invoke_authority",
                    "invoke_blessing",
                    "make_offering",
                    "recruit_specialist",
                    "reopen_business"
                },
                coverage.Undeclared.Select(a => a.Id).OrderBy(id => id, System.StringComparer.Ordinal).ToArray());

            foreach (NarrativeAction action in coverage.Undeclared)
            {
                Assert.False(action.CanPotentiallyAdvance(SemanticEffects.PossessionTransferred, null, out string refusal));
                Assert.Contains("declared no semantic effects", refusal);
            }
        }

        /// <summary>
        /// A half vanilla has to carry is not promised on a build that cannot carry it, and the
        /// refusal names the missing capability rather than reading as "this verb does nothing".
        /// </summary>
        [Fact]
        public void DelegatedEffectsAreRefusedOnABuildThatCannotCarryThem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            NarrativeAction giveBack = lab.Actions.Get("return_item");

            Assert.True(giveBack.CanPotentiallyAdvance(SemanticEffects.PossessionTransferred, lab.Vanilla, out string _));
            Assert.Contains("return_item", Ids(lab.Actions.Advancing(SemanticEffects.PossessionTransferred, lab.Vanilla)));

            lab.Vanilla.SetCapability(VanillaCapability.TransferItems, false);

            Assert.False(giveBack.CanPotentiallyAdvance(SemanticEffects.PossessionTransferred, lab.Vanilla, out string refusal));
            Assert.Contains(VanillaCapability.TransferItems.ToString(), refusal);
            Assert.DoesNotContain("return_item", Ids(lab.Actions.Advancing(SemanticEffects.PossessionTransferred, lab.Vanilla)));

            // A build nobody has asked answers nothing, so a delegated effect stays unpromised.
            Assert.False(giveBack.CanPotentiallyAdvance(SemanticEffects.PossessionTransferred, null, out string unasked));
            Assert.Contains("no build has said", unasked);

            // The BQ-owned half of the library is untouched by any of that.
            Assert.True(lab.Actions.Get("rapport").CanPotentiallyAdvance(SemanticEffects.StandingAltered, null, out string _));
        }

        /// <summary>
        /// Metadata is capability, not prediction. A declared effect is neither an availability
        /// verdict nor a promise that taking the verb produces it, and a kind the verb does not
        /// claim is refused with a reason rather than by exception.
        /// </summary>
        [Fact]
        public void DeclaredEffectsNeitherGrantAvailabilityNorPromiseAnOutcome()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            NarrativeAction shelter = lab.Actions.Get("shelter");
            ActionContext context = lab.Context(lab.Situation.ThiefId);

            Assert.True(shelter.Effects.Advances(SemanticEffects.PersonSecured));
            Assert.False(shelter.GetAvailability(context).IsAvailable);

            Assert.False(shelter.CanPotentiallyAdvance(SemanticEffects.ResourceSupplied, lab.Vanilla, out string refusal));
            Assert.Contains("does not advance", refusal);

            // Discovery is still the question about here and now, and it is a different answer.
            Assert.DoesNotContain("shelter", Ids(lab.Actions.Discover(context).Select(o => o.Action).ToList()));
        }

        private static HashSet<string> Ids(IReadOnlyList<NarrativeAction> actions)
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < actions.Count; i++)
            {
                ids.Add(actions[i].Id);
            }

            return ids;
        }

        /// <summary>
        /// A verb this repository does not ship, declaring only vocabulary that already exists.
        /// It exists to prove that joining the library is the whole of joining goal discovery.
        /// </summary>
        private sealed class RansomAction : NarrativeAction
        {
            public RansomAction() : base("ransom", ActionFamily.Economic, "Buy it back")
            {
            }

            public override ActionEffects Effects => ActionEffects
                .Declaring(ActionEffect.Delegated(
                    SemanticEffects.PossessionTransferred,
                    "IVanillaState.TrySpendMoney",
                    VanillaCapability.SpendMoney))
                .NeedingAnyOf(SemanticSlots.Item);

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context) =>
                new ActionOutcome(Id, null, "You buy it back.");
        }
    }
}
