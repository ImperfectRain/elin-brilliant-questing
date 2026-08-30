using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-031. How far the mod may reach into somebody, and the proof that it cannot reach
    /// further by accident.
    ///
    /// Two halves. The classification is ordinary table-driven behaviour and is tested as such.
    /// The done-when - "every mutation call site consults a policy, and story-critical NPCs are
    /// provably unkillable and unmovable by the mod" - is not something assertions about known
    /// verbs can establish, because the risk is the write nobody remembered to check. So the
    /// structural tests here walk the seam itself: every member of it is either a read or a
    /// classified mutation, every classified mutation is gated in one place that no implementation
    /// can override, and nothing in the contract can remove or kill anybody at all.
    /// </summary>
    public class MutationPolicyTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Protected = EntityId.Parse("npc_story");
        private static readonly EntityId Ordinary = EntityId.Parse("npc_ordinary");

        /// <summary>
        /// Every read on the seam, named. Not decoration: the census below fails on a member that
        /// is in neither list, so a write added to the contract cannot slip in ungated, and a read
        /// added to it is a deliberate edit rather than an oversight.
        /// </summary>
        private static readonly string[] SeamReads =
        {
            "Now", "PlayerId", "Supports", "GetActorClass",
            "IsAlive", "GetAttribute", "GetSkill", "GetLevel", "GetAffinity",
            "Karma", "Fame", "GetInfluence", "IsGuildMember", "GetGuildRank", "GetGuildContribution",
            "GetWorshippedDeity", "GetPiety",
            "GetMoney", "GetInventory",
            "GetHomeState",
            "GetZoneOf", "GetCharactersInZone"
        };

        /// <summary>
        /// Every member allowed to skip the gate because it only undoes one of the mod's own
        /// reaches. Pinned by name for the same reason the reads are: a withdrawal is a hole in
        /// the ladder, so opening a second one has to be somebody's deliberate edit to this list
        /// rather than an attribute quietly added to a new write.
        /// </summary>
        private static readonly string[] SeamWithdrawals = { "TryBringBack" };

        // -- the ladder ----------------------------------------------------------------------

        /// <summary>
        /// A kind is permitted exactly when the actor stands on the rung of the same name. The
        /// two enums are compared numerically at runtime, so a value added to one and not the
        /// other would silently widen or narrow every permission; this is what stops that.
        /// </summary>
        [Fact]
        public void EveryMutationKindHasTheRungThatPermitsIt()
        {
            foreach (MutationKind kind in Enum.GetValues(typeof(MutationKind)).Cast<MutationKind>())
            {
                NarrativeMutationPolicy rung = (NarrativeMutationPolicy)(int)kind;
                Assert.True(Enum.IsDefined(typeof(NarrativeMutationPolicy), rung),
                    kind + " has no rung of its own on the policy ladder");
                Assert.True(MutationPolicies.Permits(rung, kind));

                if ((int)rung > 0)
                {
                    Assert.False(MutationPolicies.Permits((NarrativeMutationPolicy)((int)rung - 1), kind),
                        kind + " is permitted a rung lower than it should be");
                }
            }
        }

        [Fact]
        public void ObserveOnlyPermitsNothingAndFullyMutablePermitsEverything()
        {
            foreach (MutationKind kind in Enum.GetValues(typeof(MutationKind)).Cast<MutationKind>())
            {
                Assert.False(MutationPolicies.Permits(NarrativeMutationPolicy.ObserveOnly, kind));
                Assert.True(MutationPolicies.Permits(NarrativeMutationPolicy.FullyMutable, kind));
            }
        }

        /// <summary>The design's actor policy (LW 5.1), asserted class by class.</summary>
        [Fact]
        public void ClassificationFollowsTheDesignsActorPolicy()
        {
            // Story-critical: observed, spoken to, allowed to like or dislike the player. Nothing
            // that takes their belongings, moves them, or ends them.
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.Dialogue));
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.Social));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.Inventory));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.Relocate));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.TemporaryAbsence));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.StoryCritical, MutationKind.Death));

            // A named shopkeeper: trade with them freely, do not make them disappear until the
            // lifecycle proof BQ-032 owes exists.
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.UniqueService, MutationKind.Inventory));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.UniqueService, MutationKind.Relocate));

            // An ordinary citizen may be moved and may be absent; dying is still reserved.
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.OrdinaryCitizen, MutationKind.Relocate));
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.OrdinaryCitizen, MutationKind.TemporaryAbsence));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.OrdinaryCitizen, MutationKind.Death));

            // Somebody the mod made carries no vanilla obligations at all.
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.Generated, MutationKind.Death));

            // The player's standing and purse are the mod's business; the player's whereabouts
            // and life are not.
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.Player, MutationKind.Inventory));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.Player, MutationKind.Relocate));
        }

        /// <summary>
        /// The important one. An actor this build could not classify keeps everything reversible
        /// and loses everything that is not, so the protection does not depend on having
        /// recognised anybody.
        /// </summary>
        [Fact]
        public void AnUnclassifiedActorKeepsTheReversibleReachesAndLosesTheRest()
        {
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.Unknown, MutationKind.Social));
            Assert.True(MutationPolicies.Permits(NarrativeActorClass.Unknown, MutationKind.Inventory));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.Unknown, MutationKind.Relocate));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.Unknown, MutationKind.TemporaryAbsence));
            Assert.False(MutationPolicies.Permits(NarrativeActorClass.Unknown, MutationKind.Death));
        }

        // -- the structural proof ------------------------------------------------------------

        /// <summary>
        /// Every member of the seam is either a named read or carries the attribute that says
        /// what it changes and to whom. There is no third category, which is what makes "every
        /// mutation call site consults a policy" a property of the contract rather than a habit.
        /// </summary>
        [Fact]
        public void EverySeamMemberIsEitherAReadOrAClassifiedMutation()
        {
            foreach (MemberInfo member in SeamMembers())
            {
                bool classified = member.GetCustomAttribute<VanillaMutationAttribute>() != null;
                bool withdrawal = member.GetCustomAttribute<VanillaWithdrawalAttribute>() != null;
                bool read = SeamReads.Contains(member.Name);

                Assert.False(classified && read, member.Name + " is listed as a read and marked as a mutation");
                Assert.False(classified && withdrawal, member.Name + " is both a mutation and a withdrawal");
                Assert.True(classified || read || withdrawal,
                    member.Name + " is neither a listed read, a classified mutation nor a declared "
                    + "withdrawal: say which it is, and if it writes, give it a [VanillaMutation] "
                    + "naming the rung it needs");

                Assert.Equal(withdrawal, SeamWithdrawals.Contains(member.Name));
            }
        }

        /// <summary>
        /// Each classified mutation names real parameters as its subjects, so the gate cannot be
        /// looking up an argument that is not there.
        /// </summary>
        [Fact]
        public void EveryClassifiedMutationNamesSubjectsThatExist()
        {
            foreach (MethodInfo method in Mutations())
            {
                VanillaMutationAttribute mutation = method.GetCustomAttribute<VanillaMutationAttribute>();
                string[] parameters = method.GetParameters().Select(p => p.Name).ToArray();
                foreach (string subject in mutation.Subjects)
                {
                    Assert.Contains(subject, parameters);
                }
            }
        }

        /// <summary>
        /// The gate is in one place and cannot be routed around. Every classified mutation is
        /// implemented by <see cref="VanillaStateBase"/> itself, and non-virtually, so an
        /// implementation supplies the unguarded half or nothing at all.
        /// </summary>
        [Fact]
        public void EveryClassifiedMutationIsImplementedOnlyByTheGate()
        {
            foreach (MethodInfo method in Mutations())
            {
                MethodInfo implementation = typeof(SandboxVanillaState).GetMethod(
                    method.Name,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    method.GetParameters().Select(p => p.ParameterType).ToArray(),
                    null);

                Assert.NotNull(implementation);
                Assert.Equal(typeof(VanillaStateBase), implementation.DeclaringType);
            }

            // And every unguarded half really is separate, so an implementation has nowhere to put
            // a write that skips the check. Named by convention rather than one per write: travel
            // is one primitive with two permissions, so `TrySendAway` and `TryBringBack` share a
            // `MoveToZoneCore`, and requiring a `...Core` per member would only force that logic
            // to be written twice.
            foreach (MethodInfo unguarded in typeof(VanillaStateBase).GetMethods(
                         BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (unguarded.Name.EndsWith("Core", StringComparison.Ordinal))
                {
                    Assert.True(unguarded.IsAbstract, unguarded.Name + " is an unguarded half with a body");
                }
            }
        }

        /// <summary>
        /// And there is no implementation that sidesteps the gate by implementing the contract
        /// directly. Core is the whole of what can be checked here; the live adapter derives from
        /// the same base and is checked by the compiler rather than by this test.
        /// </summary>
        [Fact]
        public void EverySeamImplementationInheritsTheGate()
        {
            IEnumerable<Type> implementations = typeof(IVanillaState).Assembly.GetTypes()
                .Where(t => typeof(IVanillaState).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            Assert.NotEmpty(implementations);
            foreach (Type implementation in implementations)
            {
                Assert.True(typeof(VanillaStateBase).IsAssignableFrom(implementation),
                    implementation.Name + " implements the seam without inheriting the mutation gate");
            }
        }

        /// <summary>
        /// The mod cannot kill anybody, because the contract has no member that does. Together with
        /// the census above - every member is a read, a classified write or a declared withdrawal -
        /// that is the "provably unkillable" half of BQ-031: not a rule about how the verbs behave,
        /// but the absence of the capability.
        ///
        /// BQ-032 added the rung below it, and deliberately as travel rather than as removal:
        /// somebody who is away is in another zone, so the same one character exists throughout and
        /// there is still nothing here that takes a person out of the world.
        /// </summary>
        [Fact]
        public void TheSeamCannotKillAnybodyOrRemoveThemFromTheWorld()
        {
            foreach (MethodInfo method in Mutations())
            {
                MutationKind kind = method.GetCustomAttribute<VanillaMutationAttribute>().Kind;
                Assert.True(kind < MutationKind.Death,
                    method.Name + " kills somebody; that write does not exist and must not be added");

                if (kind == MutationKind.TemporaryAbsence)
                {
                    // The one rung that moves a person, and the shape it has to keep: a
                    // destination. A member at this rung taking nowhere to put them would be a
                    // removal wearing an absence's name.
                    Assert.Contains(method.GetParameters(), p => p.ParameterType == typeof(EntityId)
                                                                 && p.Name == "zone");
                }
            }
        }

        /// <summary>
        /// Driven off the attribute rather than off a list of verbs: every write the contract
        /// declares is put to a story-critical actor, and every one the policy forbids is refused
        /// by the gate, says so, and changes nothing. A write added later is covered the day it
        /// is added.
        /// </summary>
        [Fact]
        public void EveryForbiddenWriteAgainstAStoryCriticalActorIsRefusedAndLogged()
        {
            int forbidden = 0;
            foreach (MethodInfo method in Mutations())
            {
                VanillaMutationAttribute mutation = method.GetCustomAttribute<VanillaMutationAttribute>();
                if (mutation.Subjects.Length == 0
                    || MutationPolicies.Permits(NarrativeActorClass.StoryCritical, mutation.Kind))
                {
                    continue;
                }

                forbidden++;
                SandboxVanillaState vanilla = Lab();
                int moneyBefore = vanilla.GetMoney(Protected);
                int itemsBefore = vanilla.GetInventory(Protected).Count;

                object result = method.Invoke(vanilla, Arguments(method, mutation, Protected));

                if (result is bool reported)
                {
                    Assert.False(reported, method.Name + " reported success against a story-critical actor");
                }

                Assert.Single(vanilla.Refusals);
                Assert.Contains("StoryCritical", vanilla.Refusals[0]);
                Assert.Contains(Protected.ToString(), vanilla.Refusals[0]);
                Assert.Equal(moneyBefore, vanilla.GetMoney(Protected));
                Assert.Equal(itemsBefore, vanilla.GetInventory(Protected).Count);
            }

            // The loop asserting nothing would pass vacuously, which is exactly the failure this
            // whole test is meant to catch.
            Assert.True(forbidden >= 2, "the contract declares no write a story-critical actor is protected from");
        }

        /// <summary>
        /// The rung a write declares is the rung the gate actually enforces.
        ///
        /// The kind is written twice - once on the contract, where the census and any future
        /// reader can see it, and once in the gate, which cannot afford to reflect on every write
        /// - so the two can drift. This drives every declared write against every class of actor
        /// and requires the gate's own refusal log to agree with the table, which is the only
        /// thing that would notice.
        /// </summary>
        [Fact]
        public void TheGateEnforcesTheRungEachWriteDeclares()
        {
            foreach (MethodInfo method in Mutations())
            {
                VanillaMutationAttribute mutation = method.GetCustomAttribute<VanillaMutationAttribute>();
                if (mutation.Subjects.Length == 0)
                {
                    continue;
                }

                foreach (NarrativeActorClass actorClass in
                         Enum.GetValues(typeof(NarrativeActorClass)).Cast<NarrativeActorClass>())
                {
                    SandboxVanillaState vanilla = Lab();
                    vanilla.SetActorClass(Ordinary, actorClass);

                    method.Invoke(vanilla, Arguments(method, mutation, Ordinary));

                    bool permitted = MutationPolicies.Permits(actorClass, mutation.Kind);
                    Assert.Equal(permitted, vanilla.Refusals.Count == 0);
                }
            }
        }

        /// <summary>The other direction: protection that blocks everything is not protection.</summary>
        [Fact]
        public void AStoryCriticalActorStillReactsToThePlayer()
        {
            SandboxVanillaState vanilla = Lab();
            int before = vanilla.GetAffinity(Protected);

            vanilla.ChangeAffinity(Protected, 12);

            Assert.True(vanilla.GetAffinity(Protected) > before);
            Assert.Empty(vanilla.Refusals);
        }

        // -- the one relocation the mod actually has -----------------------------------------

        [Fact]
        public void AStoryCriticalActorCannotBeMovedIntoTheHome()
        {
            SandboxVanillaState vanilla = Lab();

            Assert.False(vanilla.TryAdmitResident(Protected));

            Assert.Equal(0, vanilla.GetHomeState().ResidentCount);
            Assert.Single(vanilla.Refusals);
            Assert.Contains("StoryCritical", vanilla.Refusals[0]);
            Assert.Contains("Relocate", vanilla.Refusals[0]);
        }

        /// <summary>
        /// The guarantee that matters most in a build this mod cannot read: nobody the game would
        /// not classify gets moved either.
        /// </summary>
        [Fact]
        public void AnActorTheBuildCannotClassifyCannotBeMovedIntoTheHomeEither()
        {
            SandboxVanillaState vanilla = Lab();
            vanilla.SetActorClass(Ordinary, NarrativeActorClass.Unknown);

            Assert.False(vanilla.TryAdmitResident(Ordinary));
            Assert.Equal(0, vanilla.GetHomeState().ResidentCount);
        }

        [Fact]
        public void AnOrdinaryCitizenStillMovesIn()
        {
            SandboxVanillaState vanilla = Lab();

            Assert.True(vanilla.TryAdmitResident(Ordinary));

            Assert.Equal(1, vanilla.GetHomeState().ResidentCount);
            Assert.Empty(vanilla.Refusals);
        }

        /// <summary>
        /// A refusal at either end of a transfer stops the whole thing. Half a payment would take
        /// money out of the world and leave the simulation believing it arrived.
        /// </summary>
        [Fact]
        public void MoneyIsNotHalfMovedWhenOneEndIsProtected()
        {
            SandboxVanillaState vanilla = Lab();
            int payer = vanilla.GetMoney(Player);
            int payee = vanilla.GetMoney(Protected);

            Assert.False(vanilla.TrySpendMoney(Player, Protected, 50));

            Assert.Equal(payer, vanilla.GetMoney(Player));
            Assert.Equal(payee, vanilla.GetMoney(Protected));
        }

        // -- who the reference implementation says people are --------------------------------

        [Fact]
        public void TheLaboratoryClassifiesThePlayerItselfAndWhatItStages()
        {
            SandboxVanillaState vanilla = Lab();
            SandboxStager stager = new SandboxStager(vanilla);
            EntityId staged = EntityId.Parse("npc_staged");
            stager.StageCharacter(staged, new CharacterBlueprint("Someone"), EntityId.Parse("zone_lane"));

            Assert.Equal(NarrativeActorClass.Player, vanilla.GetActorClass(Player));
            Assert.Equal(NarrativeActorClass.Generated, vanilla.GetActorClass(staged));
            Assert.Equal(NarrativeActorClass.OrdinaryCitizen, vanilla.GetActorClass(Ordinary));
            Assert.Equal(NarrativeActorClass.StoryCritical, vanilla.GetActorClass(Protected));

            // Nobody is not an actor, and asking about nobody is not a licence.
            Assert.Equal(NarrativeActorClass.Unknown, vanilla.GetActorClass(EntityId.None));
            Assert.False(vanilla.MayMutate(EntityId.None, MutationKind.Relocate));
        }

        // -- fixtures ------------------------------------------------------------------------

        private static SandboxVanillaState Lab()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, money: 500);
            vanilla.Define(Protected, money: 500);
            vanilla.Define(Ordinary, money: 20);
            vanilla.SetActorClass(Protected, NarrativeActorClass.StoryCritical);
            vanilla.GiveItem(Protected, new ItemDescriptor(EntityId.Parse("item_relic"), "relic", "treasure", 100));
            vanilla.SetHome(new HomeStateBuilder(EntityId.Parse("zone_home"), "Steading").WithCapacity(4).Build());
            return vanilla;
        }

        /// <summary>
        /// Arguments for one declared write, with the protected actor in every subject position.
        /// Anything else is filled with something plausible; it never gets that far, because the
        /// gate refuses before the implementation is reached - which is what the refusal log in
        /// the caller actually proves.
        /// </summary>
        private static object[] Arguments(
            MethodInfo method, VanillaMutationAttribute mutation, EntityId subject)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(EntityId))
                {
                    arguments[i] = mutation.Subjects.Contains(parameters[i].Name)
                        ? subject
                        : (object)Player;
                }
                else if (parameters[i].ParameterType == typeof(int))
                {
                    arguments[i] = 5;
                }
                else
                {
                    arguments[i] = null;
                }
            }

            return arguments;
        }

        private static IEnumerable<MemberInfo> SeamMembers()
        {
            foreach (MemberInfo member in typeof(IVanillaState).GetMembers())
            {
                if (member is MethodInfo method && method.IsSpecialName)
                {
                    // A property's accessor; the property itself is in the list already.
                    continue;
                }

                yield return member;
            }
        }

        private static IEnumerable<MethodInfo> Mutations()
        {
            return typeof(IVanillaState).GetMethods()
                .Where(m => m.GetCustomAttribute<VanillaMutationAttribute>() != null);
        }
    }
}
