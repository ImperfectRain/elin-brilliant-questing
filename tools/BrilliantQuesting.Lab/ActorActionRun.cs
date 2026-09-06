using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-093 with no game attached: Nessa's goat is gone, and Nessa does something about it.
    ///
    /// The run exists to be read rather than to be passed. It walks one NPC from a need to a
    /// recorded consequence and prints every joint on the way - the goal her values and
    /// sensitivities formed, the candidate approaches she scored, which registered verb the
    /// chosen approach binds to, whether that verb was available to her, how it rolled, and what
    /// went into the ledger - and then runs the *same* verb for the player against the same
    /// person so the two can be compared side by side.
    ///
    /// Nothing here is autonomous. The intention is asked for once, by this run, because BQ-093
    /// proves the shared execution path and BQ-094 is what makes anybody act unbidden.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run actor-action [seed]
    /// </summary>
    internal static class ActorActionRun
    {
        public const ulong DefaultSeed = 11UL;

        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Nessa = EntityId.Parse("npc_nessa");
        private static readonly EntityId Haron = EntityId.Parse("npc_haron");
        private static readonly EntityId Orla = EntityId.Parse("npc_orla");
        private static readonly EntityId Sera = EntityId.Parse("npc_sera");
        private static readonly EntityId Town = EntityId.Parse("zone_derwen");

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);

            Banner(output, "NESSA'S GOAT IS GONE (seed " + seed + ")");
            output.WriteLine("  Nessa keeps goats and Haron sold one of them. Nessa does not know that;");
            output.WriteLine("  she knows the animal is missing, she minds about animals, and she is the");
            output.WriteLine("  sort who asks the people around her before she asks anybody official.");
            output.WriteLine("  Nobody has told her to do anything. Nothing here ticks.\n");

            // 1. Selection. Needs, values, sensitivities, problem-solving style and personal
            //    prohibitions - none of which the action library knows or should know about.
            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(
                bench.World.Registry.GetNpc(Nessa), MissingGoatProblem.OrdinaryLoss, EntityId.None);

            Banner(output, "1. WHAT SHE WANTS");
            output.Write(Indent(NarrativeInspector.DescribeGoalFormation(bench.World, trace)));

            // 2. The join. An approach is more abstract than a verb; this is where it becomes one,
            //    or honestly becomes nothing.
            ActionIntent intent = ActionIntent.FromGoalChoice(trace, Haron);
            Banner(output, "2. WHAT THAT IS, AS A VERB");
            if (intent == null)
            {
                output.WriteLine("  Her chosen approach binds to no registered verb, so there is nothing to");
                output.WriteLine("  attempt and nothing is invented to carry it:");
                output.WriteLine("    " + trace.ChosenAction.UnboundBecause);
                return;
            }

            output.WriteLine("  approach '" + trace.ChosenAction.Action + "' is attempted as the registered verb '"
                             + intent.ActionId + "'.");
            output.WriteLine("  It is the same object the player's own surface would reach: "
                             + Same(bench.Actions.Get(intent.ActionId), bench.Actions.Get(intent.ActionId)) + ".\n");

            // 3. Context. The work of "NPCs use the same resolver" is here rather than in a second
            //    resolver: an NPC has to be placed, and the people who would see them found.
            Banner(output, "3. WHERE SHE IS STANDING");
            if (!ActorContexts.TryBuild(
                    bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                    out ActionContext context, out string refusal))
            {
                output.WriteLine("  No context could be built: " + refusal);
                return;
            }

            output.WriteLine("  zone: " + bench.World.Registry.NameOf(context.Zone));
            output.WriteLine("  within earshot: " + Names(bench, context.Witnesses));
            output.WriteLine("  vanilla travel: " + bench.Vanilla.GetActorActivity(Nessa).VanillaMovementState()
                             + " (unread is never 'not travelling')\n");

            // 4. Resolution. Registry, the verb's own availability question, the shared resolver,
            //    the shared ledger. No branch anywhere below knows who is acting.
            ActionAttempt attempt = ActionAttempt.Run(bench.Actions, intent, context);
            Banner(output, "4. WHAT HAPPENED");
            output.Write(Indent(NarrativeInspector.DescribeAttempt(bench.World, attempt)));

            Banner(output, "5. WHAT IT COST, AND WHOSE");
            output.WriteLine("  Nessa believes it now:      " + bench.World.Knowledge.Knows(Nessa, bench.Rumour));
            output.WriteLine("  the player believes it:     " + bench.World.Knowledge.Knows(Player, bench.Rumour)
                             + "   (background acts grant the player nothing)");
            output.WriteLine("  player karma / fame:        " + bench.Vanilla.Karma + " / " + bench.Vanilla.Fame
                             + "   (unchanged: this was not the player's act)");
            output.WriteLine("  Haron's affinity to player: " + bench.Vanilla.GetAffinity(Haron)
                             + "   (unchanged, for the same reason)");
            output.WriteLine("  ledger:");
            foreach (WorldEvent recorded in bench.World.Ledger.Events)
            {
                output.WriteLine("    " + recorded.Type + " by " + bench.World.Registry.NameOf(recorded.Actor));
            }

            // 6. The same verb, the other actor. This is the claim, run rather than asserted.
            Banner(output, "6. THE SAME VERB, THE PLAYER ASKING");
            if (!ActorContexts.TryBuild(
                    bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Player, Haron,
                    out ActionContext playerContext, out string playerRefusal))
            {
                output.WriteLine("  No context could be built: " + playerRefusal);
            }
            else
            {
                ActionAttempt asPlayer = ActionAttempt.Run(
                    bench.Actions,
                    new ActionIntent(Player, intent.ActionId, Haron, "the player asks the same thing"),
                    playerContext);

                output.Write(Indent(NarrativeInspector.DescribeAttempt(bench.World, asPlayer)));
                output.WriteLine("  same registered verb object: "
                                 + Same(attempt.Action, asPlayer.Action));
            }

            // 7. A verb she may not take. The refusal is the classification's own words, and it
            //    arrives before any roll rather than after a branch written for the player.
            Banner(output, "7. SOMETHING SHE CANNOT DO");
            foreach (string verb in new[] { "shelter", "invoke_authority", "fence" })
            {
                NarrativeAction refused = bench.Actions.Get(verb);
                output.WriteLine("  " + verb.PadRight(17) + refused.ActorScope);
                output.WriteLine("  " + string.Empty.PadRight(17)
                                 + "-> " + refused.GetAvailability(context));
            }

            Banner(output, "8. WHAT NOBODY CLAIMED");
            output.WriteLine("  " + bench.Actions.Get("rescue").Embodiment.Describe());
            output.WriteLine("  " + bench.Actions.Get("carry").Embodiment.Describe());
            output.WriteLine("  Core moved nobody. It has no pathfinder, no timetable and no");
            output.WriteLine("  movement controller, and this step did not give it one.");
        }

        private static string Same(NarrativeAction left, NarrativeAction right)
        {
            return ReferenceEquals(left, right) ? "yes, the same instance" : "NO - two objects";
        }

        private static string Names(Bench bench, IReadOnlyList<EntityId> ids)
        {
            if (ids.Count == 0)
            {
                return "nobody";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                names.Add(bench.World.Registry.NameOf(ids[i]));
            }

            return string.Join(", ", names);
        }

        private static string Indent(string block)
        {
            return "  " + block.TrimEnd('\n').Replace("\n", "\n  ") + "\n";
        }

        private static void Banner(TextWriter output, string title)
        {
            output.WriteLine();
            output.WriteLine("== " + title + " " + new string('=', Math.Max(3, 68 - title.Length)));
        }

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId rumour)
            {
                World = world;
                Vanilla = vanilla;
                Rumour = rumour;
                Checks = new VanillaStyleCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public EntityId Rumour { get; }

            public static Bench Create(ulong seed)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeNpc(Player, "the player"));

                NarrativeNpc nessa = world.Registry.Add(new NarrativeNpc(Nessa, "Nessa") { Occupation = "goatherd" });
                nessa.Values.Animals.Importance = 0.95;
                nessa.Values.Animals.Flexibility = 0.1;
                nessa.Sensitivities.Animals = 0.9;
                nessa.Personality.Warmth = 0.8;
                nessa.Personality.Trust = 0.7;
                nessa.ProblemSolving.AskFriends = 0.9;
                nessa.ProblemSolving.AskAuthority = 0.4;

                world.Registry.Add(new NarrativeNpc(Haron, "Haron") { Occupation = "farmer" });
                world.Registry.Add(new NarrativeNpc(Orla, "Orla") { Occupation = "baker" });
                NarrativeNpc sera = world.Registry.Add(new NarrativeNpc(Sera, "Sera") { Occupation = "guard" });
                sera.Roles.Add("authority");

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Player, Nessa, Haron, Orla, Sera })
                {
                    vanilla.Define(who, zone: Town);
                }

                vanilla.SetAffinity(Haron, 40);
                vanilla.SetSkill(Nessa, VanillaSkill.Negotiation, 4);
                vanilla.SetSkill(Player, VanillaSkill.Negotiation, 4);
                vanilla.SetAttribute(Nessa, VanillaAttribute.Charisma, 12);
                vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 12);

                Fact sale = new Fact(world.NewId("fact"), Haron, FactPredicates.Stole, EntityId.None, "the goat");
                world.Knowledge.AddFact(sale);
                world.Knowledge.Teach(Haron, sale.Id, KnowledgeSource.Witnessed, 1.0, vanilla.Now, canProve: false);

                ConsequenceEngine consequences = new ConsequenceEngine(world, vanilla);
                consequences.Attach();

                return new Bench(world, vanilla, sale.Id);
            }
        }
    }
}
