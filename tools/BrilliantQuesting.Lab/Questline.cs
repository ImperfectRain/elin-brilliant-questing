using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// Plays a generated situation end to end, one in-game day at a time, with the real dice.
    ///
    /// The player is not a script. Each day a policy looks at what the world currently permits and
    /// what the player currently knows, and picks the most sensible thing available - so the
    /// player's moves and the situation's own escalation interleave, and a run where the witness
    /// clams up goes somewhere different from a run where she talks.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --questline [seed]
    /// </summary>
    internal static class Questline
    {
        public static void Run(ulong seed, int days = 16)
        {
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            Player player = new Player(lab);

            Banner("THE SITUATION THE WORLD GENERATED (seed " + seed + ")");
            Console.WriteLine("  " + Name(lab, lab.Situation.ThiefId) + " stole " + ItemName(lab)
                              + " from " + Name(lab, lab.Situation.VictimId)
                              + "; " + Name(lab, lab.Situation.WitnessId) + " saw it happen.");
            Console.WriteLine("  The player knows none of that.\n");
            Console.Write(WorldInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));

            Banner("SIXTEEN DAYS");
            for (int day = 0; day <= days; day++)
            {
                if (day > 0)
                {
                    lab.AdvanceDays(1);
                    foreach (string applied in lab.Threads.LastApplied)
                    {
                        Console.WriteLine("day " + day + "  [world] " + Describe(lab, applied));
                    }
                }

                if (lab.Situation.Thread.State == ThreadState.Resolved)
                {
                    continue;
                }

                player.TakeTurn(day);
            }

            Banner("WHERE EVERYONE ENDED UP");
            foreach (EntityId id in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
            {
                Console.Write(WorldInspector.DescribeCharacter(lab.World, lab.Vanilla, id));
                Console.WriteLine();
            }

            Console.Write(WorldInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
            Console.WriteLine();
            Console.Write(WorldInspector.DescribeThread(lab.World, lab.Situation.Thread));
            Console.WriteLine("player standing: karma " + lab.Vanilla.Karma + ", fame " + lab.Vanilla.Fame
                              + ", " + lab.Vanilla.GetMoney(lab.Player) + " orens, carrying "
                              + lab.Vanilla.GetInventory(lab.Player).Count + " item(s)");

            Banner("HISTORY");
            Console.Write(WorldInspector.DescribeHistory(lab.World, 40));
        }

        /// <summary>
        /// The decision policy. Priorities read top to bottom; the first one whose action the world
        /// currently allows is what the player does that day. Nothing here knows the scenario - it
        /// reasons from the same knowledge graph the NPCs live in.
        /// </summary>
        private sealed class Player
        {
            private readonly TheftLaboratory _lab;
            private readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();

            public Player(TheftLaboratory lab)
            {
                _lab = lab;
            }

            /// <summary>Same decision, no transcript. Used by the sweep.</summary>
            public void TakeTurnQuietly()
            {
                foreach (Intent intent in Priorities())
                {
                    if (Spent(intent))
                    {
                        continue;
                    }

                    if (!_lab.Actions.Get(intent.ActionId).GetAvailability(Build(intent)).IsAvailable)
                    {
                        continue;
                    }

                    Count(intent);
                    _lab.Perform(intent.ActionId, intent.Target, ctx =>
                    {
                        ctx.SubjectFact = intent.SubjectFact;
                        ctx.SubjectItem = intent.SubjectItem;
                    });
                    return;
                }
            }

            public void TakeTurn(int day)
            {
                List<string> rejected = new List<string>();

                foreach (Intent intent in Priorities())
                {
                    if (Spent(intent))
                    {
                        rejected.Add(intent.ActionId + " " + Name(_lab, intent.Target) + " - tried that twice already");
                        continue;
                    }

                    ActionContext context = Build(intent);
                    Availability availability = _lab.Actions.Get(intent.ActionId).GetAvailability(context);
                    if (!availability.IsAvailable)
                    {
                        rejected.Add(intent.ActionId + " " + Name(_lab, intent.Target) + " - " + availability.Reason);
                        continue;
                    }

                    Count(intent);
                    Console.WriteLine("day " + day + "  > " + intent.ActionId + " " + Name(_lab, intent.Target)
                                      + "   (" + intent.Because + ")");

                    ActionOutcome outcome = _lab.Perform(intent.ActionId, intent.Target, ctx =>
                    {
                        ctx.SubjectFact = intent.SubjectFact;
                        ctx.SubjectItem = intent.SubjectItem;
                    });

                    Console.WriteLine("          " + outcome.Explain().Replace("\n", "\n          "));
                    return;
                }

                // An idle day is a finding, not a gap in the transcript: say what the player wanted
                // and what the world said about it.
                Console.WriteLine("day " + day + "  (nothing left to try)");
                for (int i = 0; i < rejected.Count && i < 3; i++)
                {
                    Console.WriteLine("          wanted: " + rejected[i]);
                }
            }

            private IEnumerable<Intent> Priorities()
            {
                PettyTheftSituation s = _lab.Situation;
                KnowledgeGraph knowledge = _lab.World.Knowledge;
                bool knowsTheft = knowledge.Knows(_lab.Player, s.TheftFactId);
                bool canProve = knowledge.CanProve(_lab.Player, s.TheftFactId);
                bool carryingIt = Carrying(s.ItemId);
                bool thiefHasIt = HasItem(s.ThiefId, s.ItemId);

                // 1. Holding the stolen property: give it back. Nothing else beats that.
                if (carryingIt)
                {
                    yield return new Intent("return_item", s.VictimId, "carrying their property");
                }

                // 2. Take the job: hear the victim out, then agree to look into it. Once there is
                //    an undertaking on the record there is nothing left to agree to.
                yield return new Intent("question", s.VictimId, "hear what the victim knows");
                if (!HasUndertakingWith(s.VictimId))
                {
                    yield return new Intent("persuade", s.VictimId, "agree to look into it");
                }

                // 3. Find out what happened. The witness is the cheapest source; if she will not
                //    talk, money and then pressure are the escalating alternatives.
                if (!knowsTheft)
                {
                    yield return new Intent("question", s.WitnessId, "she was standing right there");
                    yield return new Intent("bribe", s.WitnessId, "she would not say it for free");
                    yield return new Intent("search", s.VictimId, "look for a physical trace") { SubjectFact = s.TheftFactId };
                    yield return new Intent("intimidate", s.WitnessId, "out of gentler options");
                }

                // 4. Knowing who did it, recover the thing itself while he still carries it.
                if (knowsTheft && thiefHasIt)
                {
                    yield return new Intent("pickpocket", s.ThiefId, "he is still carrying it") { SubjectItem = s.ItemId };
                }

                // 5. It is hidden now. Proof has to come from the scene instead.
                if (knowsTheft && !canProve)
                {
                    yield return new Intent("search", s.ThiefId, "find where he put it") { SubjectFact = s.TheftFactId };
                }

                // 6. Cannot get the object. Tell the victim what you know and let them decide.
                if (knowsTheft)
                {
                    yield return new Intent("expose", s.VictimId, canProve ? "with proof in hand" : "on your word alone")
                    {
                        SubjectFact = s.TheftFactId
                    };
                }

                // 7. Out of gentle options and out of leads: lean on the man himself.
                if (knowsTheft && !canProve)
                {
                    yield return new Intent("intimidate", s.ThiefId, "he is the only one who knows where it is");
                }
            }

            private ActionContext Build(Intent intent)
            {
                ActionContext context = _lab.Context(intent.Target);
                context.SubjectFact = intent.SubjectFact;
                context.SubjectItem = intent.SubjectItem;
                return context;
            }

            /// <summary>Two goes at any one thing; after that a sensible person tries something else.</summary>
            private bool Spent(Intent intent)
            {
                _attempts.TryGetValue(intent.Key, out int count);
                return count >= 2;
            }

            private void Count(Intent intent)
            {
                _attempts.TryGetValue(intent.Key, out int count);
                _attempts[intent.Key] = count + 1;
            }

            /// <summary>Has an agreement with this person already been recorded, either way round?</summary>
            private bool HasUndertakingWith(EntityId person)
            {
                foreach (var worldEvent in _lab.World.Ledger.OfType(BrilliantQuesting.Events.WorldEventType.PromiseMade))
                {
                    bool between = (worldEvent.Actor == _lab.Player && worldEvent.Target == person)
                                   || (worldEvent.Actor == person && worldEvent.Target == _lab.Player);
                    if (between)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool Carrying(EntityId itemId) => HasItem(_lab.Player, itemId);

            private bool HasItem(EntityId owner, EntityId itemId)
            {
                var inventory = _lab.Vanilla.GetInventory(owner);
                for (int i = 0; i < inventory.Count; i++)
                {
                    if (inventory[i].Id == itemId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Intent
        {
            public Intent(string actionId, EntityId target, string because)
            {
                ActionId = actionId;
                Target = target;
                Because = because;
            }

            public string ActionId { get; }

            public EntityId Target { get; }

            /// <summary>Why the policy wanted this, printed so the run reads as decisions.</summary>
            public string Because { get; }

            public EntityId SubjectFact { get; set; }

            public EntityId SubjectItem { get; set; }

            public string Key => ActionId + ":" + Target;
        }

        /// <summary>
        /// The same policy across many seeds. One transcript shows that the machinery works; a
        /// sweep shows whether the same situation actually produces different stories.
        /// </summary>
        public static void Sweep(int count)
        {
            Dictionary<string, int> endings = new Dictionary<string, int>();
            int knewByTheEnd = 0;
            int couldProve = 0;
            int accused = 0;

            for (ulong seed = 1; seed <= (ulong)count; seed++)
            {
                TheftLaboratory lab = TheftLaboratory.Create(seed);
                Player player = new Player(lab);
                for (int day = 0; day <= 16; day++)
                {
                    if (day > 0)
                    {
                        lab.AdvanceDays(1);
                    }

                    if (lab.Situation.Thread.State != ThreadState.Resolved)
                    {
                        player.TakeTurnQuietly();
                    }
                }

                string ending = lab.Situation.Thread.Resolution ?? "unresolved";
                endings.TryGetValue(ending, out int tally);
                endings[ending] = tally + 1;

                if (lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId))
                {
                    knewByTheEnd++;
                }

                if (lab.World.Knowledge.CanProve(lab.Player, lab.Situation.TheftFactId))
                {
                    couldProve++;
                }

                foreach (var e in lab.World.Ledger.OfType(BrilliantQuesting.Events.WorldEventType.FalseAccusation))
                {
                    if (e.Actor == lab.Situation.VictimId)
                    {
                        accused++;
                        break;
                    }
                }
            }

            Banner(count + " SEEDS, SAME POLICY");
            foreach (KeyValuePair<string, int> pair in endings)
            {
                Console.WriteLine("  " + pair.Key.PadRight(20) + pair.Value + "/" + count);
            }

            Console.WriteLine();
            Console.WriteLine("  player learned who did it      " + knewByTheEnd + "/" + count);
            Console.WriteLine("  player could prove it          " + couldProve + "/" + count);
            Console.WriteLine("  victim accused without proof   " + accused + "/" + count);
        }

        private static string Describe(TheftLaboratory lab, string applied)
        {
            switch (applied)
            {
                case "petty_theft/victim_asks_around": return Name(lab, lab.Situation.VictimId) + " starts asking the neighbours.";
                case "petty_theft/thief_hides_it": return Name(lab, lab.Situation.ThiefId) + " stops carrying it.";
                case "petty_theft/witness_talks": return Name(lab, lab.Situation.WitnessId) + " lets something slip.";
                case "petty_theft/accusation": return Name(lab, lab.Situation.VictimId) + " acts on what they believe.";
                case "petty_theft/feud": return "The two households stop speaking.";
                default: return applied;
            }
        }

        private static string ItemName(TheftLaboratory lab)
        {
            return lab.World.Knowledge.GetFact(lab.Situation.TheftFactId).Value;
        }

        private static string Name(TheftLaboratory lab, EntityId id) => lab.World.Registry.NameOf(id);

        private static void Banner(string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 78));
        }
    }
}
