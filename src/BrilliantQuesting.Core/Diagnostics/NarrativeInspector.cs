using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>
    /// The "why?" tooling the design document treats as non-negotiable.
    ///
    /// A procedural system that cannot explain itself cannot be debugged, tuned or trusted. Every
    /// question a developer will actually ask - why is this option here, why did that NPC react
    /// like that, why does this thread exist - should be answerable from these dumps without
    /// re-running the world.
    /// </summary>
    /// <remarks>
    /// Named `NarrativeInspector`, not `WorldInspector`: Elin ships a global-namespace
    /// `WorldInspector`, and the game's type wins at any call site inside the plugin. This is the
    /// same collision that already renamed `Goal` to `NpcGoal` and `Scene` to `NarrativeScene`,
    /// and the standing resolution is that Core avoids the game's generic names rather than
    /// qualifying every use.
    /// </remarks>
    public static class NarrativeInspector
    {
        /// <summary>Every option the registry considered, including the ones it rejected and why.</summary>
        public static string DescribeOptions(ActionRegistry registry, ActionContext context)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("options against ").Append(context.NameOf(context.Target)).Append('\n');

            List<ActionOffer> offers = registry.Discover(context, includeUnavailable: true);
            foreach (ActionOffer offer in offers)
            {
                sb.Append(offer.Availability.IsAvailable ? "  [x] " : "  [ ] ");
                // Wide enough for the longest verb id; a column that runs into the next one makes
                // the one surface that has to explain a procedural decision harder to read.
                sb.Append(offer.Action.Id.PadRight(20));
                sb.Append(offer.Action.Family.ToString().PadRight(14));
                CheckProfile profile = ProceduralCheckProfiles.ForAction(offer.Action.Id);
                sb.Append((profile == null ? "no check" : profile.Id + " dc" + profile.BaseDifficulty).PadRight(26));
                if (!string.IsNullOrEmpty(offer.Availability.Reason))
                {
                    sb.Append("- ").Append(offer.Availability.Reason);
                }

                sb.Append('\n');
            }

            HashSet<ActionFamily> families = registry.AvailableFamilies(context);
            sb.Append("  solution families open: ").Append(families.Count).Append('\n');
            return sb.ToString();
        }

        public static string DescribeCharacter(NarrativeWorldState world, IVanillaState vanilla, EntityId id)
        {
            NarrativeNpc npc = world.Registry.GetNpc(id);
            if (npc == null)
            {
                return id + " is not a known character.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(npc.Name).Append(" (").Append(npc.Occupation).Append(", ").Append(npc.Importance).Append(")\n");
            sb.Append("  alive: ").Append(vanilla.IsAlive(id)).Append("   affinity to player: ").Append(vanilla.GetAffinity(id)).Append('\n');
            sb.Append("  quirk: ");
            if (!npc.Quirk.Assigned)
            {
                sb.Append("unassigned");
            }
            else if (!npc.Quirk.HasQuirk)
            {
                sb.Append(npc.Quirk.Weirdness);
            }
            else
            {
                sb.Append(npc.Quirk.Weirdness).Append(' ').Append(npc.Quirk.Kind);
            }

            sb.Append('\n');

            sb.Append("  values:");
            AppendValue(sb, "family", npc.Values.Family);
            AppendValue(sb, "wealth", npc.Values.Wealth);
            AppendValue(sb, "law", npc.Values.Law);
            AppendValue(sb, "faith", npc.Values.Faith);
            AppendValue(sb, "status", npc.Values.Status);
            AppendValue(sb, "animals", npc.Values.Animals);
            AppendValue(sb, "knowledge", npc.Values.Knowledge);
            AppendValue(sb, "freedom", npc.Values.Freedom);
            sb.Append('\n');

            sb.Append("  narrative needs:");
            AppendNeed(sb, "safety", npc.Needs.Safety);
            AppendNeed(sb, "belonging", npc.Needs.Belonging);
            AppendNeed(sb, "debt_relief", npc.Needs.DebtRelief);
            AppendNeed(sb, "status", npc.Needs.Status);
            AppendNeed(sb, "loyalty", npc.Needs.Loyalty);
            AppendNeed(sb, "justice", npc.Needs.Justice);
            AppendNeed(sb, "secrecy", npc.Needs.Secrecy);
            AppendNeed(sb, "revenge", npc.Needs.Revenge);
            AppendNeed(sb, "protection", npc.Needs.Protection);
            AppendNeed(sb, "material_shortage", npc.Needs.MaterialShortage);
            AppendNeed(sb, "obligation", npc.Needs.Obligation);
            sb.Append('\n');

            sb.Append("  emotions:");
            AppendEmotion(sb, "anger", npc.Emotions.Anger);
            AppendEmotion(sb, "fear", npc.Emotions.Fear);
            AppendEmotion(sb, "shame", npc.Emotions.Shame);
            AppendEmotion(sb, "grief", npc.Emotions.Grief);
            AppendEmotion(sb, "relief", npc.Emotions.Relief);
            AppendEmotion(sb, "suspicion", npc.Emotions.Suspicion);
            AppendEmotion(sb, "affection", npc.Emotions.Affection);
            AppendEmotion(sb, "stress", npc.Emotions.Stress);
            sb.Append(" updated ").Append(npc.Emotions.LastUpdatedAt).Append('\n');

            sb.Append("  goals:");
            if (npc.Goals.Count == 0)
            {
                sb.Append(" none");
            }

            foreach (NpcGoal goal in npc.Goals)
            {
                sb.Append(' ').Append(goal);
            }

            sb.Append('\n');

            sb.Append("  believes:\n");
            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(id))
            {
                Fact fact = world.Knowledge.GetFact(belief.FactId);
                sb.Append("    ").Append(Render(world, fact));
                sb.Append("  [").Append(belief.Source).Append(", confidence ").Append(belief.Confidence.ToString("0.00"));
                sb.Append(belief.CanProve ? ", can prove]" : ", cannot prove]").Append('\n');
            }

            sb.Append("  remembers about the player:\n");
            foreach (MemoryRecord memory in world.Memories.MemoriesAbout(id, vanilla.PlayerId))
            {
                sb.Append("    ").Append(memory.SummaryTag);
                if (memory.Occurrences > 1)
                {
                    sb.Append(" x").Append(memory.Occurrences);
                }

                sb.Append(" (").Append(memory.Weight).Append(", ").Append(memory.AffinityContribution).Append(" affinity)\n");
            }

            IReadOnlyList<RelationshipEdge> edges = world.Relationships.EdgesOf(id);
            if (edges.Count > 0)
            {
                sb.Append("  ties:");
                for (int i = 0; i < edges.Count; i++)
                {
                    sb.Append(' ').Append(world.Registry.NameOf(edges[i].To)).Append('(').Append(edges[i].Kind).Append(' ').Append(edges[i].Sentiment).Append(')');
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static string DescribeGoalFormation(NarrativeWorldState world, GoalFormationTrace trace)
        {
            if (trace == null)
            {
                return "goal formation: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("goal formation for ").Append(world.Registry.NameOf(trace.ActorId)).Append('\n');
            sb.Append("  world state: ").Append(trace.Problem).Append('\n');
            sb.Append("  need: ").Append(trace.Need).Append(" pressure ")
              .Append(trace.NeedPressure.ToString("0.00")).Append('\n');
            sb.Append("  values and sensitivities: value ").Append(trace.ValueConcern)
              .Append(" drives desire; action scores include sensitivity terms below\n");
            sb.Append("  desire: ").Append(trace.Desire).Append('\n');
            sb.Append("  candidate goal: ").Append(trace.CandidateGoal).Append('\n');
            sb.Append("  candidate actions:\n");
            foreach (GoalActionTrace action in trace.CandidateActions)
            {
                sb.Append(action == trace.ChosenAction ? "    [chosen] " : "             ");
                sb.Append(action.Action).Append(" -> ").Append(action.Outcome)
                  .Append(" via ").Append(action.Style).Append(" score ")
                  .Append(action.Score.ToString("0.00")).Append('\n');
                for (int i = 0; i < action.ScoreTerms.Count; i++)
                {
                    sb.Append("      - ").Append(action.ScoreTerms[i]).Append('\n');
                }
            }

            sb.Append("  chosen action: ").Append(trace.ChosenAction.Action)
              .Append(" -> ").Append(trace.ChosenAction.Outcome).Append('\n');
            return sb.ToString();
        }

        public static string DescribeInterpretation(NarrativeWorldState world, ActorInterpretationTrace trace)
        {
            if (trace == null)
            {
                return "interpretation: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("interpretation for ").Append(world.Registry.NameOf(trace.ActorId)).Append('\n');
            sb.Append("  source: ").Append(trace.Source).Append(" [").Append(trace.SourceFactId).Append("]\n");
            sb.Append("  lens: ").Append(trace.Lens).Append('\n');
            sb.Append("  derived fact: ").Append(trace.DerivedPredicate.Replace('_', ' '))
              .Append(" = ").Append(trace.DerivedValue).Append(" [")
              .Append(trace.DerivedFactId).Append("]\n");
            sb.Append("  confidence: ").Append(trace.Confidence.ToString("0.00"))
              .Append(" via inference\n");
            sb.Append("  score terms:\n");
            for (int i = 0; i < trace.ScoreTerms.Count; i++)
            {
                sb.Append("    - ").Append(trace.ScoreTerms[i]).Append('\n');
            }

            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, string name, ValueConcernProfile value)
        {
            sb.Append(' ').Append(name).Append("(i ")
              .Append(value.Importance.ToString("0.00")).Append(", f ")
              .Append(value.Flexibility.ToString("0.00")).Append(')');
        }

        private static void AppendNeed(StringBuilder sb, string name, double pressure)
        {
            if (pressure > 0.0)
            {
                sb.Append(' ').Append(name).Append(' ').Append(pressure.ToString("0.00"));
            }
        }

        private static void AppendEmotion(StringBuilder sb, string name, double intensity)
        {
            if (intensity > 0.0)
            {
                sb.Append(' ').Append(name).Append(' ').Append(intensity.ToString("0.00"));
            }
        }

        public static string DescribeThread(NarrativeWorldState world, NarrativeThread thread)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("thread ").Append(thread.ArchetypeId).Append(" [").Append(thread.State).Append(", tension ").Append(thread.Tension).Append("]\n");
            if (!string.IsNullOrEmpty(thread.Resolution))
            {
                sb.Append("  resolution: ").Append(thread.Resolution).Append('\n');
            }

            sb.Append("  participants:");
            foreach (EntityId participant in thread.ParticipantIds)
            {
                sb.Append(' ').Append(world.Registry.NameOf(participant));
            }

            sb.Append('\n');

            sb.Append("  open questions:\n");
            foreach (string question in thread.OpenQuestions)
            {
                sb.Append("    - ").Append(question).Append('\n');
            }

            if (thread.GenerationCauses.Count > 0)
            {
                sb.Append("  generated from world state:\n");
                foreach (string cause in thread.GenerationCauses)
                {
                    sb.Append("    - ").Append(cause).Append('\n');
                }
            }

            sb.Append("  escalation:\n");
            foreach (EscalationStep step in thread.Escalation)
            {
                sb.Append(thread.CompletedSteps.Contains(step.Id) ? "    [done] " : "    [    ] ");
                sb.Append("day +").Append(step.DayOffset).Append("  ").Append(step.Description).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Who currently knows a fact, which of them could demonstrate it, and who told them.
        ///
        /// The last part matters once gossip circulates on its own (BQ-019): the interesting
        /// question about a rumour is not who believes it but how it got to them, and without the
        /// chain a spread that went wrong cannot be traced back to the retelling that did it.
        /// </summary>
        public static string DescribeFactSpread(NarrativeWorldState world, EntityId factId)
        {
            Fact fact = world.Knowledge.GetFact(factId);
            StringBuilder sb = new StringBuilder();
            sb.Append("fact: ").Append(Render(world, fact)).Append('\n');
            foreach (EntityId knower in world.Knowledge.Knowers(factId))
            {
                world.Knowledge.TryGetBelief(knower, factId, out KnowledgeRecord belief);
                sb.Append("  ").Append(world.Registry.NameOf(knower).PadRight(12));
                sb.Append(belief.Source.ToString().PadRight(12));
                sb.Append("confidence ").Append(belief.Confidence.ToString("0.00"));
                sb.Append(belief.CanProve ? "  (can prove)" : "  (cannot prove)");
                if (!belief.ToldBy.IsNone)
                {
                    sb.Append("  heard from ").Append(world.Registry.NameOf(belief.ToldBy));
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static string DescribeHistory(NarrativeWorldState world, int limit = 20)
        {
            StringBuilder sb = new StringBuilder();
            IReadOnlyList<Events.WorldEvent> events = world.Ledger.Events;
            int start = events.Count > limit ? events.Count - limit : 0;
            for (int i = start; i < events.Count; i++)
            {
                Events.WorldEvent worldEvent = events[i];
                sb.Append(worldEvent.Time).Append("  ").Append(worldEvent.Type.ToString().PadRight(18));
                sb.Append(world.Registry.NameOf(worldEvent.Actor));
                if (!worldEvent.Target.IsNone)
                {
                    sb.Append(" -> ").Append(world.Registry.NameOf(worldEvent.Target));
                }

                if (worldEvent.Witnesses.Count > 0)
                {
                    sb.Append("  (seen by");
                    for (int w = 0; w < worldEvent.Witnesses.Count; w++)
                    {
                        sb.Append(' ').Append(world.Registry.NameOf(worldEvent.Witnesses[w]));
                    }

                    sb.Append(')');
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// One report that walks every question in `living-world-priorities.md` §12, in order.
        ///
        /// The order matters more than the formatting: a developer reading this from the top
        /// should never have to open the source to answer any of the twelve. Where a question
        /// belongs to a system that does not exist yet, the report says so and names the step it
        /// arrives at, because "not built" is a real answer and a silent omission is not.
        /// </summary>
        public static string Explain(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ActionRegistry registry,
            ActionContext context,
            NarrativeThread thread)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("=== Brilliant Questing: why? ===\n");

            sb.Append("\n-- why does this situation exist, and which event caused it --\n");
            if (thread == null)
            {
                sb.Append("  no thread in front of the player.\n");
            }
            else
            {
                sb.Append(DescribeThread(world, thread));
                sb.Append("  created ").Append(thread.CreatedAt).Append(", last advanced ")
                  .Append(thread.LastAdvancedAt).Append('\n');
                sb.Append("  origin event: ").Append(DescribeEvent(world, thread.OriginEventId)).Append('\n');
            }

            sb.Append("\n-- why is this person involved, and what do they know or falsely believe --\n");
            sb.Append(DescribeCharacter(world, vanilla, context.Target));
            if (thread != null)
            {
                sb.Append("  in this thread as: ")
                  .Append(thread.ParticipantIds.Contains(context.Target) ? "a participant" : "not a participant")
                  .Append('\n');
            }

            sb.Append("\n-- why is each action available or unavailable, and what check runs --\n");
            sb.Append(DescribeOptions(registry, context));

            sb.Append("\n-- what the player knows --\n");
            sb.Append(NarrativeJournal.Describe(world, vanilla.PlayerId));

            sb.Append("\n-- what the player has finished --\n");
            sb.Append(Chronicle.Describe(world, vanilla.PlayerId));

            sb.Append("\n-- what somebody standing here would say out loud --\n");
            sb.Append(DescribeAmbientTalk(world, vanilla));

            sb.Append("\n-- what this person would say if the player asked --\n");
            sb.Append(DescribeNews(world, vanilla, context.Target));

            sb.Append("\n-- who witnessed what, and what consequences were emitted --\n");
            sb.Append(DescribeHistory(world));

            if (!context.SubjectFact.IsNone)
            {
                sb.Append("\n-- why a claim spread the way it did --\n");
                sb.Append(DescribeFactSpread(world, context.SubjectFact));
            }

            sb.Append("\n-- questions whose systems do not exist yet --\n");
            sb.Append("  why did an autonomous NPC embody an action? not simulated; NPC autonomy arrives at BQ-093.\n");
            sb.Append("  why did a shop close or NPC vanish?   not simulated; continuity arrives at BQ-051, BQ-032.\n");
            sb.Append("  why was a site selected or generated? not simulated; sites arrive at BQ-087 onward.\n");
            sb.Append("  why did this person choose to tell you? not decided; disclosure arrives at BQ-071.\n");

            return sb.ToString();
        }

        /// <summary>
        /// Whether anybody near the player is about to mention something, and if not, why not.
        ///
        /// Reading only: <see cref="AmbientTalk.Next"/> touches nothing, and the throwaway
        /// <see cref="RumorSystem"/> built here is used for the question "would that retelling
        /// take" and never to make one happen. Silence has two quite different causes and a debug
        /// report that ran them together would be useless - a town with nothing new to say and a
        /// town that said something twenty minutes ago look identical from the outside.
        /// </summary>
        public static string DescribeAmbientTalk(NarrativeWorldState world, IVanillaState vanilla)
        {
            AmbientTalk talk = new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            SpokenRemark remark = talk.Next(world, vanilla, vanilla.Now);
            if (remark != null)
            {
                return "  " + remark.SpeakerName + ": \"" + remark.Line + "\"  [" + remark.FactId + "]\n";
            }

            long last = world.LastAmbientRemarkMinute;
            if (last != NarrativeWorldState.NothingSaidYet
                && vanilla.Now.TotalMinutes - last < talk.MinutesBetweenRemarks)
            {
                return "  nothing yet: somebody spoke " + (vanilla.Now.TotalMinutes - last)
                       + " minute(s) ago, and the next remark is due after "
                       + talk.MinutesBetweenRemarks + ".\n";
            }

            return "  nothing: nobody in this zone is repeating anything the player has not already"
                   + " heard. First-hand knowledge is not repeated here - it is asked for.\n";
        }

        /// <summary>
        /// The answer this person would give to "what's been happening?", or why the topic is not
        /// on their conversation.
        ///
        /// Reading only, on the same terms as <see cref="DescribeAmbientTalk"/>: nothing is taught
        /// by looking, and the throwaway <see cref="RumorSystem"/> is only ever asked whether a
        /// retelling would take.
        /// </summary>
        public static string DescribeNews(NarrativeWorldState world, IVanillaState vanilla, EntityId speaker)
        {
            if (speaker.IsNone)
            {
                return "  nobody is being talked to.\n";
            }

            TownNews news = new TownNews(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            IReadOnlyList<SpokenRemark> answer = news.Ask(world, vanilla, speaker);
            if (answer.Count == 0)
            {
                return "  nothing: " + world.Registry.NameOf(speaker) + " has heard nothing the player"
                       + " has not, so the topic is not offered. What they saw or did themselves is"
                       + " testimony, and is asked for by name.\n";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < answer.Count; i++)
            {
                sb.Append("  " + answer[i].SpeakerName + ": \"" + answer[i].Line + "\"  ["
                          + answer[i].FactId + "]\n");
            }

            return sb.ToString();
        }

        /// <summary>One line for the event a thread grew out of, or an honest blank.</summary>
        private static string DescribeEvent(NarrativeWorldState world, EntityId eventId)
        {
            if (eventId.IsNone)
            {
                return "none recorded";
            }

            IReadOnlyList<Events.WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Id != eventId)
                {
                    continue;
                }

                Events.WorldEvent found = events[i];
                string line = found.Time + " " + found.Type + " by " + world.Registry.NameOf(found.Actor);
                return found.Target.IsNone ? line : line + " -> " + world.Registry.NameOf(found.Target);
            }

            return eventId + " (no longer in the ledger)";
        }

        private static string Render(NarrativeWorldState world, Fact fact)
        {
            if (fact == null)
            {
                return "(missing fact)";
            }

            string subject = world.Registry.NameOf(fact.Subject);
            string obj = world.Registry.Npcs.ContainsKey(fact.Object)
                ? world.Registry.NameOf(fact.Object)
                : !string.IsNullOrEmpty(fact.Value) ? fact.Value : fact.Object.Value;
            string truth = fact.Truth == Knowledge.TruthState.True ? string.Empty : " (" + fact.Truth + "!)";
            return subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj + truth;
        }
    }
}
