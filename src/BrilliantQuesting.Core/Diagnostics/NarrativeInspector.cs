using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Storylets;
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

        public static string DescribeTravelingGroup(NarrativeWorldState world, EntityId groupId)
        {
            StringBuilder sb = new StringBuilder();
            if (world == null)
            {
                return "traveling group: no world\n";
            }

            TravelingGroup group = world.TravelingGroups.Of(groupId);
            if (group == null)
            {
                return "traveling group " + groupId + ": not found\n";
            }

            sb.Append("traveling group ").Append(group.Id.Value).Append('\n');
            sb.Append("  kind:        ").Append(group.Kind).Append('\n');
            sb.Append("  route:       ").Append(world.Registry.NameOf(group.OriginId))
              .Append(" -> ").Append(world.Registry.NameOf(group.DestinationId)).Append('\n');
            sb.Append("  purpose:     ").Append(group.Purpose).Append('\n');
            sb.Append("  physical:    ").Append(group.PhysicalPlan).Append('\n');
            sb.Append("  milestone:   ").Append(group.State)
              .Append(" since ").Append(group.StateChangedAt).Append('\n');
            sb.Append("  schedule:    depart ").Append(group.DepartureAt)
              .Append(", expected ").Append(group.ExpectedArrivalAt).Append('\n');
            sb.Append("  route risk:  ").Append(group.RouteRisk.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');

            if (!group.ThreadId.IsNone)
            {
                sb.Append("  thread:      ").Append(group.ThreadId.Value).Append('\n');
            }

            if (!group.InterruptionCauseId.IsNone || !group.InterruptionSiteId.IsNone)
            {
                sb.Append("  cause:       ").Append(world.Registry.NameOf(group.InterruptionCauseId))
                  .Append(" at ").Append(world.Registry.NameOf(group.InterruptionSiteId)).Append('\n');
            }

            sb.Append("  members ").Append(group.Members.Count).Append('\n');
            for (int i = 0; i < group.Members.Count; i++)
            {
                TravelingGroupMember member = group.Members[i];
                sb.Append("    ").Append(world.Registry.NameOf(member.ActorId))
                  .Append("  movement ").Append(member.Movement);
                if (!member.LastKnownZone.IsNone)
                {
                    sb.Append("  last-zone ").Append(world.Registry.NameOf(member.LastKnownZone));
                }

                if (!string.IsNullOrEmpty(member.Note))
                {
                    sb.Append("; ").Append(member.Note);
                }

                sb.Append('\n');
            }

            sb.Append("  cargo ").Append(group.CargoIds.Count).Append('\n');
            for (int i = 0; i < group.CargoIds.Count; i++)
            {
                sb.Append("    ").Append(group.CargoIds[i].Value).Append('\n');
            }

            sb.Append("  events\n");
            AppendTravelEvent(sb, world, "planned", group.PlannedEventId);
            AppendTravelEvent(sb, world, "departed", group.DepartedEventId);
            AppendTravelEvent(sb, world, "arrived", group.ArrivedEventId);
            AppendTravelEvent(sb, world, "interrupted", group.InterruptedEventId);
            AppendTravelEvent(sb, world, "failed", group.FailedEventId);
            return sb.ToString();
        }

        public static string DescribeTravelRound(TravelingGroupRound round)
        {
            if (round == null)
            {
                return "travel round: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("travel round: ").Append(round).Append('\n');
            for (int i = 0; i < round.Notes.Count; i++)
            {
                sb.Append("  ").Append(round.Notes[i]).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-070. Everything one speech act means, and the proof that it means it without words.
        ///
        /// The step's done-when condition is read off this dump: an act is produced, no text is
        /// attached to it anywhere, and the log still shows the whole of what was communicated -
        /// who spoke, to whom, about which claim, against whom, what it does to that claim, which
        /// way it moves, and what it answers. Anything a realizer (BQ-074) later chooses is absent
        /// here because it does not exist yet and is not needed to know what happened.
        /// </summary>
        public static string DescribeSpeechAct(NarrativeWorldState world, SpeechAct act)
        {
            if (act == null)
            {
                return "no speech act.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("speech act: ").Append(act.Type).Append('\n');
            sb.Append("  speaker:     ").Append(Who(world, act.Speaker)).Append('\n');
            sb.Append("  addressees:  ");
            for (int i = 0; i < act.Addressees.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(Who(world, act.Addressees[i]));
            }

            sb.Append('\n');
            sb.Append("  stance:      ").Append(act.Stance).Append("   direction: ").Append(act.Direction).Append('\n');

            sb.Append("  about:       ");
            if (act.About.IsNone)
            {
                sb.Append("no claim");
            }
            else
            {
                Fact fact = world == null ? null : world.Knowledge.GetFact(act.About);
                sb.Append(act.About.Value);
                if (fact != null)
                {
                    sb.Append("  (").Append(fact).Append(')');
                }
            }

            sb.Append('\n');
            sb.Append("  referent:    ").Append(act.Referent.IsNone ? "nobody named" : Who(world, act.Referent)).Append('\n');
            sb.Append("  content:     ").Append(DescribeContent(act)).Append('\n');

            sb.Append("  in reply to: ");
            if (act.InReplyTo == null)
            {
                sb.Append("nothing - opens the exchange");
            }
            else
            {
                sb.Append(act.InReplyTo.Type).Append(" by ").Append(Who(world, act.InReplyTo.Speaker));
            }

            sb.Append('\n');

            // Stated rather than implied by omission: the absence of wording is the step's
            // condition, not an unfinished part of the dump.
            sb.Append("  wording:     none - this layer produces meaning only\n");
            sb.Append("  signature:   ").Append(act.Signature).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// BQ-083. What one conversation has accumulated so far: every act in order, which
        /// questions are still hanging, which lies BQ-073 already caught, and every unresolved
        /// self-contradiction among the assertions made - the dump that makes "why did the NPC
        /// just say that was never asked" and "why didn't it call out the contradiction"
        /// answerable without re-running the scene.
        /// </summary>
        public static string DescribeConversation(NarrativeWorldState world, ConversationState conversation)
        {
            if (conversation == null)
            {
                return "no conversation.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("conversation: ").Append(conversation.Acts.Count).Append(" act(s)\n");

            for (int i = 0; i < conversation.Acts.Count; i++)
            {
                SpeechAct act = conversation.Acts[i];
                sb.Append("  ").Append(i).Append(". ").Append(Who(world, act.Speaker)).Append(' ').Append(act.Type);
                sb.Append(" -> ");
                for (int a = 0; a < act.Addressees.Count; a++)
                {
                    if (a > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(Who(world, act.Addressees[a]));
                }

                sb.Append("  about ").Append(act.About.IsNone ? "nothing" : act.About.Value).Append('\n');
            }

            IReadOnlyList<SpeechAct> unanswered = conversation.UnansweredQuestions;
            sb.Append("  unanswered:  ").Append(unanswered.Count).Append('\n');

            sb.Append("  lies told:   ").Append(conversation.LiesTold.Count).Append('\n');

            IReadOnlyList<DiscourseContradiction> contradictions = conversation.AllContradictions(world);
            if (contradictions.Count == 0)
            {
                sb.Append("  contradictions: none\n");
            }

            for (int i = 0; i < contradictions.Count; i++)
            {
                DiscourseContradiction found = contradictions[i];
                sb.Append("  contradiction: ").Append(Who(world, found.Later.Speaker)).Append(" - ").Append(found.Because).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-071. Why somebody said it, or why they did not.
        ///
        /// The step's condition is that disclosure is a character decision rather than a
        /// difficulty check, and a decision nobody can interrogate is indistinguishable from a
        /// roll. So this prints the whole reasoning: which claim was asked about, what the speaker
        /// actually believes about it, every pressure that applied with its sign, its size and the
        /// state it was read from, the balance they add to, and which of them settled it.
        ///
        /// BQ-072 adds two lines rather than a second dump: how deep the disclosure went, which of
        /// the three ceilings held it there, and what each of them permitted. A shallow answer
        /// from a friend is otherwise indistinguishable from a bug.
        ///
        /// Two things are stated rather than left to be inferred. A speaker who holds no belief is
        /// reported as having nothing to disclose and no pressures at all, because "would not" and
        /// "could not" are different answers. And the dump says it produced no wording, for
        /// BQ-070's reason: what is missing here is missing by design.
        /// </summary>
        public static string DescribeDisclosure(NarrativeWorldState world, DisclosureDecision decision)
        {
            if (decision == null)
            {
                return "no disclosure decision.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("disclosure: ").Append(decision.Strategy).Append('\n');
            sb.Append("  speaker:     ").Append(Who(world, decision.Speaker)).Append('\n');
            sb.Append("  asked by:    ").Append(Who(world, decision.Asker)).Append('\n');

            sb.Append("  about:       ");
            if (decision.FactId.IsNone)
            {
                sb.Append("no claim");
            }
            else
            {
                Fact fact = world == null ? null : world.Knowledge.GetFact(decision.FactId);
                sb.Append(decision.FactId.Value);
                if (fact != null)
                {
                    sb.Append("  (").Append(fact).Append(')');
                }
            }

            sb.Append('\n');

            sb.Append("  belief:      ");
            if (world != null && world.Knowledge.TryGetBelief(decision.Speaker, decision.FactId, out KnowledgeRecord belief))
            {
                sb.Append(belief.Source).Append(" at ").Append(belief.Confidence.ToString("0.00"));
                sb.Append(belief.CanProve ? ", can prove it" : ", cannot prove it");
            }
            else
            {
                sb.Append("none - they do not hold this claim");
            }

            sb.Append('\n');
            sb.Append("  discloses:   ").Append(decision.WillDisclose ? "yes" : "no");
            sb.Append(decision.WillDisclose && !decision.Committed ? " (will not stand behind it)" : string.Empty).Append('\n');

            // BQ-073. What is done instead, which the ladder deliberately does not say: "no" is
            // the same rung for somebody who declines, somebody who changes the subject and
            // somebody who says it was another man, and those are not the same event to have
            // witnessed.
            sb.Append("  instead:     ").Append(Instead(decision)).Append('\n');
            sb.Append("  balance:     ").Append(decision.Balance.ToString("+0.00;-0.00;0.00")).Append('\n');

            // BQ-072. How far in they went, and which of the three ceilings stopped them - the
            // question a shallow answer actually raises. Printed even when nothing was disclosed,
            // because "no depth, they are not saying it" is the answer in that case and leaving
            // the line out would read as an unfinished dump.
            sb.Append("  depth:       ").Append(decision.Depth).Append(" - ").Append(Held(decision.Limit)).Append('\n');
            sb.Append("  ceilings:    knows ").Append(decision.KnownDepth);
            sb.Append(", standing ").Append(decision.Standing.ToString("+0.00;-0.00;0.00"));
            sb.Append(" reaches ").Append(decision.StandingDepth).Append('\n');

            if (decision.Pressures.Count == 0)
            {
                sb.Append("  pressures:   none weighed");
                if (decision.Note.Length != 0)
                {
                    sb.Append(" - ").Append(decision.Note);
                }

                sb.Append('\n');
            }
            else
            {
                sb.Append("  pressures:\n");
                for (int i = 0; i < decision.Pressures.Count; i++)
                {
                    DisclosurePressure pressure = decision.Pressures[i];
                    sb.Append("    ").Append(pressure.Tag.PadRight(14));
                    sb.Append(pressure.Weight.ToString("+0.00;-0.00").PadRight(8));
                    sb.Append(pressure.TowardDisclosure ? "toward  " : "against ");
                    sb.Append(pressure.Because).Append('\n');
                }

                sb.Append("  decisive:    ");
                if (decision.Decisive.Count == 0)
                {
                    // Worth saying rather than leaving blank: no single pressure carried it, so
                    // whoever is tuning this should be looking at the balance and not at one knob.
                    sb.Append("no single pressure - the balance settled it");
                }
                else
                {
                    for (int i = 0; i < decision.Decisive.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(", ");
                        }

                        sb.Append(decision.Decisive[i].Tag).Append(' ').Append(decision.Decisive[i].Weight.ToString("+0.00;-0.00"));
                    }
                }

                sb.Append('\n');
            }

            AppendProhibitions(sb, decision);
            sb.Append("  wording:     none - this layer decides meaning only\n");
            return sb.ToString();
        }

        /// <summary>
        /// The personal lines that bore on this decision (BQ-077), printed apart from the pressures
        /// because they are not pressures: a line does not push the balance one way, it limits what
        /// the balance is allowed to buy, and listing the two together would invite reading a
        /// prohibition as a heavy weight.
        /// </summary>
        private static void AppendProhibitions(StringBuilder sb, DisclosureDecision decision)
        {
            if (decision.Prohibitions.Count == 0)
            {
                return;
            }

            sb.Append("  lines held:\n");
            for (int i = 0; i < decision.Prohibitions.Count; i++)
            {
                ProhibitionRuling ruling = decision.Prohibitions[i];
                sb.Append("    ").Append(ruling.Kind.ToString().PadRight(26));
                sb.Append(ruling.Broke ? "broke   " : "holds   ");
                sb.Append(ruling.Because).Append('\n');
            }

            IReadOnlyList<string> manners = decision.ForbiddenManners;
            if (manners.Count == 0)
            {
                return;
            }

            sb.Append("  wording ruled out: ");
            for (int i = 0; i < manners.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(manners[i]);
            }

            sb.Append('\n');
        }

        /// <summary>
        /// What the speaker does with the question rather than answer it, in the words it would be
        /// said in - including the case where they answer it, which is stated rather than left
        /// blank so a reader can tell a forthcoming decision from an unfinished dump.
        /// </summary>
        private static string Instead(DisclosureDecision decision)
        {
            switch (decision.Tactic)
            {
                case DisclosureTactic.Decline:
                    return "declines, and lets it be seen that they are declining";
                case DisclosureTactic.ChangeSubject:
                    return "lets the question go and puts something else in its place";
                case DisclosureTactic.AnswerElsewhere:
                    return "answers a neighbouring question truthfully instead";
                case DisclosureTactic.Falsify:
                    return "says something they do not believe - this is a lie and is recorded as one";
                default:
                    return decision.WillDisclose
                        ? (decision.HeldBack
                            ? "nothing - the claim is put forward, though not all of what they hold"
                            : "nothing - the claim is put forward")
                        : "nothing - they hold no belief to withhold";
            }
        }

        /// <summary>
        /// What an assertion amounts to against its speaker's own belief (BQ-073).
        ///
        /// The two readings are printed on separate lines and never merged, because the whole
        /// point of the layer is that they can disagree: a sincere assertion of something false is
        /// an honest mistake, and an insincere one is a lie whatever the world thinks of the
        /// claim. Anybody debugging a deception is asking which of those they are looking at.
        /// </summary>
        public static string DescribeVeracity(NarrativeWorldState world, SpeechAct act)
        {
            if (act == null)
            {
                return "no speech act.\n";
            }

            Veracity veracity = Deception.Assess(world, act);
            StringBuilder sb = new StringBuilder();
            sb.Append("veracity: ").Append(veracity.Sincerity).Append('\n');
            sb.Append("  speaker:     ").Append(Who(world, act.Speaker)).Append('\n');
            sb.Append("  put forward: ");
            if (veracity.AssertedClaim.IsNone)
            {
                sb.Append("no claim - ").Append(act.Type).Append(" asserts nothing");
            }
            else
            {
                sb.Append(veracity.Stance == SpeechActStance.Denies ? "as not so: " : "as so: ");
                sb.Append(veracity.AssertedClaim.Value);
                Fact claim = world == null ? null : world.Knowledge.GetFact(veracity.AssertedClaim);
                if (claim != null)
                {
                    sb.Append("  (").Append(claim).Append(')');
                }
            }

            sb.Append('\n');
            sb.Append("  against:     ");
            sb.Append(veracity.Contradicts.IsNone
                ? "nothing they hold"
                : veracity.Contradicts.Value + " held at " + veracity.Conviction.ToString("0.00"));
            sb.Append('\n');

            // Reported, never consulted. Stated as such on the line itself so that nobody reading
            // the dump concludes the verdict above was derived from it.
            sb.Append("  world says:  ");
            sb.Append(veracity.ClaimIsModelled ? veracity.Accuracy.ToString() : "no such claim");
            sb.Append("   (reported, not used to decide sincerity)").Append('\n');

            sb.Append("  reading:     ").Append(veracity.Because).Append('\n');
            sb.Append("  verdict:     ");
            if (veracity.IsLie)
            {
                sb.Append("a deliberate falsehood");
            }
            else if (veracity.IsHonestMistake)
            {
                sb.Append("an honest mistake - said in good faith and untrue");
            }
            else if (veracity.Sincerity == Sincerity.Unfounded)
            {
                sb.Append("asserted with nothing behind it - reckless, not dishonest");
            }
            else if (veracity.Sincerity == Sincerity.NotAsserted)
            {
                sb.Append("nothing was claimed, so nothing can be false");
            }
            else
            {
                sb.Append("said in good faith");
            }

            sb.Append('\n');
            sb.Append("  wording:     none - this layer decides meaning only\n");
            return sb.ToString();
        }

        /// <summary>Which ceiling held a disclosure where it is, in the words it would be said in.</summary>
        private static string Held(DisclosureLimit limit)
        {
            switch (limit)
            {
                case DisclosureLimit.Unspoken:
                    return "the claim is not being put forward at all";
                case DisclosureLimit.Knowledge:
                    return "that is as much as they hold";
                case DisclosureLimit.Restraint:
                    return "something other than the relationship keeps the rest back";
                case DisclosureLimit.Standing:
                    return "the tie does not reach further";
                default:
                    return "nothing held anything back";
            }
        }

        private static string DescribeContent(SpeechAct act)
        {
            StringBuilder sb = new StringBuilder();
            if (act.Content.HasProposition)
            {
                sb.Append("proposition ").Append(act.Content.PropositionFact.Value);
            }

            if (act.Content.HasItem)
            {
                Separate(sb);
                sb.Append("item ").Append(act.Content.Item.Value);
            }

            if (act.Content.HasDestination)
            {
                Separate(sb);
                sb.Append("destination ").Append(act.Content.Destination.Value);
            }

            if (!string.IsNullOrEmpty(act.Content.Purpose))
            {
                Separate(sb);
                sb.Append("purpose ").Append(act.Content.Purpose);
            }

            return sb.Length == 0 ? "carried by what it answers" : sb.ToString();
        }

        private static void Separate(StringBuilder sb)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
        }

        private static string Who(NarrativeWorldState world, EntityId id)
        {
            if (id.IsNone)
            {
                return "nobody";
            }

            string name = world == null ? null : world.Registry.NameOf(id);
            return string.IsNullOrEmpty(name) ? id.Value : name + " (" + id.Value + ")";
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

            // BQ-077. What this character will not do, listed even when the answer is nothing:
            // negative space is only recognizable as a fact about somebody if its absence is a
            // fact about everybody else.
            sb.Append("  will not: ");
            IReadOnlyList<PersonalProhibition> lines = npc.NegativeSpace.Declared;
            if (lines.Count == 0)
            {
                sb.Append("nothing declared");
            }
            else
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append("; ");
                    }

                    sb.Append(NegativeSpaceProfile.Describe(lines[i]))
                      .Append(" (firmness ").Append(npc.NegativeSpace.FirmnessOf(lines[i]).ToString("0.00"))
                      .Append(npc.NegativeSpace.IsBreakable(lines[i]) ? ", breakable)" : ", unbreakable)");
                }
            }

            sb.Append('\n');

            // BQ-145. Not the identity observation - that is BQ-144's and is a live read of the
            // game - but what BQ derives from it, with the facet behind every weight named. An
            // identity-derived number nobody can attribute to a facet is a number nobody can argue
            // with, and this is where the argument is made available.
            IdentityAffordances identity = IdentityAffordances.Of(npc, vanilla);
            sb.Append("  identity affordances:");
            IReadOnlyList<string> derived = identity.Explain();
            if (derived.Count == 0)
            {
                sb.Append(" none derived (no identity facet contributes)\n");
            }
            else
            {
                sb.Append('\n');
                for (int i = 0; i < derived.Count; i++)
                {
                    sb.Append("    - ").Append(derived[i]).Append('\n');
                }
            }

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
                sb.Append(action == trace.ChosenAction
                    ? "    [chosen] "
                    : action.Forbidden ? " [forbidden] " : "             ");
                sb.Append(action.Action).Append(" -> ").Append(action.Outcome)
                  .Append(" via ").Append(action.Style).Append(" score ")
                  .Append(action.Score.ToString("0.00")).Append('\n');

                // Which registered verb this approach would actually be attempted as, or why
                // none of them means it (BQ-093). Without this line the trace says what the
                // actor decided and not what they could do about it.
                sb.Append(action.IsAttemptable
                    ? "      = verb " + action.RegisteredActionId
                    : "      = no registered verb: " + action.UnboundBecause).Append('\n');
                for (int i = 0; i < action.ScoreTerms.Count; i++)
                {
                    sb.Append("      - ").Append(action.ScoreTerms[i]).Append('\n');
                }

                // Printed beside the score it cost, because a prohibition that is only visible as
                // an action never taken is indistinguishable from a scoring bug (BQ-077).
                if (action.Ruling.Held)
                {
                    sb.Append("      * ").Append(action.Ruling.Because).Append('\n');
                }
            }

            sb.Append("  chosen action: ").Append(trace.ChosenAction.Action)
              .Append(" -> ").Append(trace.ChosenAction.Outcome).Append('\n');
            sb.Append("  attempted as: ").Append(trace.ChosenAction.IsAttemptable
                ? trace.ChosenAction.RegisteredActionId
                : "nothing - " + trace.ChosenAction.UnboundBecause).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// One actor's intention carried all the way through the shared action system: the verb it
        /// chose, who may take that verb, what it needs from a body, whether it applied here, how
        /// it rolled and what history it wrote (BQ-093).
        ///
        /// The whole reason the step is inspectable rather than merely tested. A reader must be
        /// able to see, in one place, that an NPC's bribe went through the registry, through the
        /// verb's own availability question, through the shared resolver and into the same ledger
        /// - and, where it did not, which of those said no.
        /// </summary>
        public static string DescribeAttempt(NarrativeWorldState world, ActionAttempt attempt)
        {
            if (attempt == null)
            {
                return "attempt: none\n";
            }

            StringBuilder sb = new StringBuilder();
            ActionIntent intent = attempt.Intent;
            sb.Append("attempt by ")
              .Append(intent == null ? "nobody" : world.Registry.NameOf(intent.Actor))
              .Append('\n');

            if (intent != null)
            {
                sb.Append("  intends: ").Append(intent.ActionId);
                if (!intent.Target.IsNone)
                {
                    sb.Append(" against ").Append(world.Registry.NameOf(intent.Target));
                }

                sb.Append('\n');
                if (intent.Because.Length > 0)
                {
                    sb.Append("  because: ").Append(intent.Because).Append('\n');
                }
            }

            if (attempt.Action == null)
            {
                sb.Append("  verb: not registered\n");
                sb.Append("  nothing resolved: ").Append(attempt.Refusal).Append('\n');
                return sb.ToString();
            }

            sb.Append("  verb: ").Append(attempt.Action.Id)
              .Append(" (").Append(attempt.Action.Family).Append(")\n");
            sb.Append("  who may take it: ").Append(attempt.Action.ActorScope).Append('\n');
            sb.Append("  ").Append(attempt.Action.Embodiment.Describe()).Append('\n');
            sb.Append("  availability: ").Append(attempt.Availability).Append('\n');

            if (!attempt.Resolved)
            {
                sb.Append("  nothing resolved: ").Append(attempt.Refusal).Append('\n');
                return sb.ToString();
            }

            ActionOutcome outcome = attempt.Outcome;
            if (outcome.Check != null)
            {
                sb.Append("  ").Append(outcome.Check.Explain()).Append('\n');
            }
            else
            {
                sb.Append("  resolved without a roll\n");
            }

            for (int i = 0; i < outcome.Notes.Count; i++)
            {
                sb.Append("  - ").Append(outcome.Notes[i]).Append('\n');
            }

            if (outcome.Events.Count == 0)
            {
                sb.Append("  recorded: nothing\n");
            }

            for (int i = 0; i < outcome.Events.Count; i++)
            {
                Events.WorldEvent recorded = outcome.Events[i];
                sb.Append("  recorded ").Append(recorded.Type)
                  .Append(": ").Append(world.Registry.NameOf(recorded.Actor));
                if (!recorded.Target.IsNone)
                {
                    sb.Append(" -> ").Append(world.Registry.NameOf(recorded.Target));
                }

                sb.Append(outcome.Observation == ContextObservation.OffScreen
                    ? " (nobody was watching: witnesses unread, not absent)\n"
                    : " (" + recorded.Witnesses.Count + " witnessed)\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Why the world did or did not take a matter up on its own (BQ-094).
        ///
        /// Prints the working the pass threw away: who was weighed, what their activity read was
        /// worth, every option scored including the ones a personal line took off the table, and
        /// then the attempt itself through <see cref="DescribeAttempt"/> so an autonomous act and
        /// a player's act are read in the same words.
        /// </summary>
        public static string DescribeIntervention(NarrativeWorldState world, InterventionTrace trace)
        {
            if (trace == null)
            {
                return "intervention: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(trace.Describe(world)).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// BQ-096. Reads the party layer in the same shape as autonomous intervention: what
        /// group took the matter up, which ordinary verb its leader attempted, and which claim
        /// can carry the result back to the player.
        /// </summary>
        public static string DescribeAdventurerEcology(NarrativeWorldState world, AdventurerEcologyTrace trace)
        {
            if (trace == null)
            {
                return "adventurer ecology: none\n";
            }

            return trace.Describe(world);
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

        /// <summary>
        /// BQ-080. The reaction beside the interpretation it came out of, so a reader can see that
        /// two actors differed because of what they are rather than because somebody wrote them
        /// different lines. No text of the event appears here that the event did not already
        /// carry, and no wording of the reaction appears at all - there is none to print.
        /// </summary>
        public static string DescribeReaction(NarrativeWorldState world, ActorReaction reaction)
        {
            if (reaction == null)
            {
                return "reaction: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("reaction for ").Append(world.Registry.NameOf(reaction.ActorId)).Append('\n');
            sb.Append("  event: [").Append(reaction.SourceFactId).Append("] unchanged\n");
            if (reaction.Interpretation != null)
            {
                sb.Append("  read as: ").Append(reaction.Interpretation.DerivedPredicate.Replace('_', ' '))
                  .Append(" = ").Append(reaction.Interpretation.DerivedValue)
                  .Append(" (lens: ").Append(reaction.Interpretation.Lens).Append(")\n");
            }

            sb.Append("  concern: ").Append(reaction.Concern).Append('\n');
            sb.Append("  response: ").Append(reaction.Response).Append('\n');
            sb.Append("  premise: ").Append(reaction.Premise)
              .Append(" registers as ").Append(reaction.Registers).Append('\n');
            sb.Append("  intensity: ").Append(reaction.Intensity.ToString("0.00")).Append('\n');
            sb.Append("  concern terms:\n");
            for (int i = 0; i < reaction.ConcernTerms.Count; i++)
            {
                sb.Append("    - ").Append(reaction.ConcernTerms[i]).Append('\n');
            }

            sb.Append("  response terms:\n");
            for (int i = 0; i < reaction.ResponseTerms.Count; i++)
            {
                sb.Append("    - ").Append(reaction.ResponseTerms[i]).Append('\n');
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

        /// <summary>
        /// Why this scene is being played by these people, in two parts that must not be confused.
        ///
        /// The casting notes answer BQ-067's question - what qualified each of them for the role
        /// they hold - and the chemistry answers BQ-068's: of the groups that all qualified, why
        /// this one. A flat score is printed as such rather than omitted, because "there was
        /// nothing to choose between them" is the answer in most towns most of the time, and a
        /// report that silently prints nothing looks like a report that failed.
        /// </summary>
        public static string DescribeCasting(StoryletOpportunity opportunity)
        {
            if (opportunity == null)
            {
                return "casting: none\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("casting for ").Append(opportunity.Definition.Id).Append('\n');
            if (!opportunity.IsAvailable)
            {
                sb.Append("  uncast: ").Append(opportunity.RefusalReason).Append('\n');
                return sb.ToString();
            }

            sb.Append("  roles:\n");
            for (int i = 0; i < opportunity.CastingNotes.Count; i++)
            {
                sb.Append("    - ").Append(opportunity.CastingNotes[i]).Append('\n');
            }

            sb.Append("  chemistry: ")
              .Append(opportunity.Chemistry.Total.ToString("0.00"))
              .Append(" over ").Append(opportunity.GroupsConsidered)
              .Append(opportunity.GroupsConsidered == 1 ? " qualified group" : " qualified groups")
              .Append('\n');

            // Both bounds, said out loud. Without them the line above reads as "and these were all
            // of them", which is a claim the bounded search is not entitled to make: it weighs a
            // prefix of the qualified groups, built from a prefix of the qualified people.
            sb.Append("    search: ")
              .Append(opportunity.SearchTruncated
                  ? "truncated at the group bound; better groups may not have been weighed"
                  : "exhausted; every group these shortlists allow was weighed")
              .Append('\n');

            if (opportunity.CandidateBoundReached)
            {
                sb.Append("    shortlist: a role reached its candidate bound; people who also qualified were never grouped\n");
            }

            if (opportunity.Chemistry.IsFlat)
            {
                sb.Append("    - nothing ties these people to each other; the first qualified group in the stable order was kept\n");
                return sb.ToString();
            }

            IReadOnlyList<string> reasons = opportunity.Chemistry.Explain();
            for (int i = 0; i < reasons.Count; i++)
            {
                sb.Append("    - ").Append(reasons[i]).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// What the world is currently pressing on, and what each pressure could turn into.
        ///
        /// Deliberately printed beside nothing else: a development is not a thread, a fact or an
        /// event, and the question this answers - "what is unresolved right now, and would any of
        /// it reach a player?" - is not answerable from any of their dumps. A pressure that can
        /// reach nobody is printed exactly like one that can, because it is not a defect.
        /// </summary>
        /// <summary>
        /// A scene as it actually went (BQ-146): beat by beat, who spoke, what they weighed, what
        /// they decided, how the check came out, what was said, what history recorded and where it
        /// went next.
        ///
        /// The pipeline's own answer to "why did that happen". Every line of it is read back off
        /// what the layers already produced - the intent scores are <c>ActionIntent</c>'s trace, the
        /// check line is <c>CheckResult.Explain</c>, the words are the realizer's - so the report
        /// cannot disagree with the scene and cannot be produced when the scene was not.
        /// </summary>
        public static string DescribeStoryletPlay(NarrativeWorldState world, StoryletPlay play)
        {
            if (play == null)
            {
                return "scene: none\n";
            }

            StringBuilder sb = new StringBuilder();
            if (!play.Played)
            {
                sb.Append("scene unplayed: ").Append(play.Refusal).Append('\n');
                return sb.ToString();
            }

            sb.Append("scene ").Append(play.Firing.StoryletId).Append('\n');
            for (int i = 0; i < play.Beats.Count; i++)
            {
                PlayedBeat beat = play.Beats[i];
                sb.Append("  ").Append(i + 1).Append(". ").Append(beat.BeatId);
                if (!beat.Played)
                {
                    sb.Append("  (skipped: ").Append(beat.Skipped).Append(")\n");
                    continue;
                }

                sb.Append('\n');
                if (!beat.Speaker.IsNone)
                {
                    sb.Append("     speaker: ").Append(Who(world, beat.Speaker))
                      .Append(" -> ").Append(Who(world, beat.Listener)).Append('\n');
                }

                if (beat.Choice != null)
                {
                    sb.Append("     weighed:\n");
                    for (int j = 0; j < beat.Choice.Considered.Count; j++)
                    {
                        IntentScore score = beat.Choice.Considered[j];
                        sb.Append("       ")
                          .Append(ReferenceEquals(score, beat.Choice.Chosen) ? "* " : "  ")
                          .Append(score.Intention.Act).Append(' ');
                        if (score.IsAvailable)
                        {
                            sb.Append(score.Total.ToString("0.00")).Append("  ").Append(Terms(score));
                        }
                        else
                        {
                            sb.Append("(unavailable: ").Append(score.Refusal).Append(')');
                        }

                        sb.Append('\n');
                    }
                }

                if (beat.Decision != null)
                {
                    sb.Append("     disclosure: ").Append(beat.Decision.Strategy)
                      .Append(", ").Append(beat.Decision.Depth).Append('\n');
                }

                sb.Append("     act: ").Append(beat.Act == null ? "none - nobody spoke" : beat.Act.Signature).Append('\n');

                if (beat.Recalled != null)
                {
                    sb.Append("     recalled: ").Append(beat.Recalled.Hook.PrimaryKind)
                      .Append(" (").Append(beat.Recalled.Hook.Route).Append(")\n");
                }

                if (beat.Check != null)
                {
                    sb.Append("     ").Append(beat.Check.Explain()).Append('\n');
                }

                if (beat.Line != null)
                {
                    sb.Append("     said: ")
                      .Append(beat.Line.Rendered ? beat.Line.Text : "(unworded: " + beat.Line.Refusal + ")")
                      .Append('\n');
                }

                if (beat.Consequences.Count > 0)
                {
                    sb.Append("     recorded: ").Append(string.Join(", ", beat.Consequences)).Append('\n');
                }

                if (beat.PlayerIntersections.Count > 0)
                {
                    sb.Append("     player could: ").Append(string.Join(", ", beat.PlayerIntersections)).Append('\n');
                }

                sb.Append("     next: ").Append(beat.Route == null ? "nothing routed" : beat.Route.ToString()).Append('\n');
            }

            sb.Append("  ended: ").Append(play.Resolution.Length == 0 ? "no declared resolution" : play.Resolution).Append('\n');
            return sb.ToString();
        }

        private static string Terms(IntentScore score)
        {
            List<string> terms = new List<string>();
            for (int i = 0; i < score.Reasons.Count; i++)
            {
                terms.Add(score.Reasons[i].ToString());
            }

            return string.Join(", ", terms);
        }

        public static string DescribeDevelopments(NarrativeWorldState world)
        {
            IReadOnlyList<Development> developments = DevelopmentDetector.Detect(world);
            StringBuilder sb = new StringBuilder();
            sb.Append("developments: ").Append(developments.Count).Append('\n');

            for (int i = 0; i < developments.Count; i++)
            {
                Development development = developments[i];
                sb.Append("  ").Append(development.Id).Append('\n');
                sb.Append("    pressure:");
                for (int t = 0; t < development.PressureTags.Count; t++)
                {
                    sb.Append(' ').Append(development.PressureTags[t]);
                }

                sb.Append(" (urgency ").Append(development.Urgency).Append(")\n");

                sb.Append("    subjects:");
                for (int s = 0; s < development.SubjectIds.Count; s++)
                {
                    sb.Append(' ').Append(world.Registry.NameOf(development.SubjectIds[s]));
                }

                sb.Append('\n');

                sb.Append("    derived from: ")
                  .Append(development.OriginEventIds.Count)
                  .Append(development.OriginEventIds.Count == 1 ? " event" : " events")
                  .Append(development.ThreadId.IsNone ? ", no thread" : ", thread " + development.ThreadId)
                  .Append(development.FocusFactId.IsNone ? ", no focus fact" : ", focus " + development.FocusFactId)
                  .Append('\n');

                sb.Append("    ")
                  .Append(development.CanBeExpressedAsStorylet
                      ? "a storylet could be looked for"
                      : "no storylet can be looked for; it stays a pressure the world holds")
                  .Append('\n');
            }

            return sb.ToString();
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

            if (thread.RecoveryRoutes.Count > 0)
            {
                sb.Append("  recovery routes:\n");
                foreach (RecoveryRoute route in thread.RecoveryRoutes)
                {
                    sb.Append("    - ").Append(route.WorstOutcome)
                        .Append(" -> ").Append(route.ActionId)
                        .Append("; price: ").Append(route.Price)
                        .Append("; risk: ").Append(route.Uncertainty)
                        .Append("; restores: ").Append(route.Restores)
                        .Append('\n');
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

            // BQ-117: the trophy case rather than the flat list. It is a superset of the
            // Chronicle's own reading - the finished matters are still every line BQ-034 prints -
            // so the report gains the people and places without carrying the log twice.
            sb.Append("\n-- who the player became, and what they finished --\n");
            sb.Append(ChronicleNarrative.Export(world, vanilla.PlayerId, vanilla.Now));

            sb.Append("\n-- what the player holds that is not money or an item --\n");
            sb.Append(StandingSheet.Describe(world, vanilla));

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

        /// <summary>
        /// BQ-081. What old business one person may bring up, and why the rest of history is not
        /// theirs to bring up.
        ///
        /// Both halves matter. The listing answers "where would a callback here come from"; the
        /// tally underneath answers the question that is harder to see from the outside, which is
        /// how much of the ledger this person has no route to at all. A step whose whole content
        /// is a gate has to be able to show the gate closing.
        /// </summary>
        public static string DescribeCallbacks(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            GameTime now,
            CallbackSelection selection = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("callbacks available to ").Append(Who(world, recaller)).Append('\n');
            if (world == null || recaller.IsNone)
            {
                sb.Append("  nobody to recall anything\n");
                return sb.ToString();
            }

            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(world, vanilla, recaller, now, selection);
            if (hooks.Count == 0)
            {
                sb.Append("  nothing old enough that they could know about\n");
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                CallbackHook hook = hooks[i];
                sb.Append("  ").Append(hook.EventType.ToString().PadRight(20));
                sb.Append("day ").Append(hook.At.TotalDays).Append(" (").Append(hook.AgeInDays).Append("d ago)  ");
                sb.Append(hook.PrimaryKind.ToString().PadRight(14));
                sb.Append(hook.Route.ToString().PadRight(10));
                sb.Append("with ").Append(hook.Counterpart.IsNone ? "nobody" : world.Registry.NameOf(hook.Counterpart));
                sb.Append(" [").Append(hook.Party).Append(']');
                sb.Append("  weight ").Append(hook.Weight.ToString("0.00"));
                sb.Append(" embarrassment ").Append(hook.Embarrassment);
                sb.Append(" publicity ").Append(hook.Publicity);
                sb.Append('\n');
            }

            int material = 0;
            int noRoute = 0;
            IReadOnlyList<Events.WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (CallbackHooks.KindsOf(events[i].Type).Count == 0)
                {
                    continue;
                }

                material++;
                Continuity.CallbackRoute route;
                if (!CallbackHooks.TryRoute(world, events[i], recaller, out route))
                {
                    noRoute++;
                }
            }

            sb.Append("  ").Append(material).Append(" of ").Append(events.Count)
              .Append(" recorded events leave reusable material; ").Append(noRoute)
              .Append(" of those are not theirs to know\n");
            return sb.ToString();
        }

        /// <summary>
        /// BQ-085. What one object has been through, and how much of that the person in front of
        /// the player can place it by.
        ///
        /// The same doctrine as <see cref="DescribeCallbacks"/>: a step whose content is a gate has
        /// to be able to show the gate closing. "Why did showing her the ring do nothing" has three
        /// possible answers - history recorded nothing about the object, she has no route to the
        /// part it did record, or the matter it belongs to is over - and this separates them.
        /// </summary>
        public static string DescribeProvenance(
            NarrativeWorldState world,
            EntityId itemId,
            EntityId viewer,
            GameTime now)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("provenance of ").Append(itemId.IsNone ? "nothing" : itemId.Value);
            sb.Append(" as ").Append(Who(world, viewer)).Append(" can place it\n");
            if (world == null || itemId.IsNone)
            {
                sb.Append("  no object to trace\n");
                return sb.ToString();
            }

            IReadOnlyList<ProvenanceEntry> all = ItemProvenance.Of(world, itemId, now);
            if (all.Count == 0)
            {
                sb.Append("  history recorded nothing about it\n");
                return sb.ToString();
            }

            IReadOnlyList<ProvenanceEntry> recognized = ItemProvenance.RecognizedBy(world, itemId, viewer, now);
            for (int i = 0; i < all.Count; i++)
            {
                ProvenanceEntry entry = all[i];
                bool theirs = Recognizes(recognized, entry.EventId);
                sb.Append(theirs ? "  * " : "    ");
                sb.Append(entry.Role.ToString().PadRight(10));
                sb.Append(entry.EventType.ToString().PadRight(20));
                sb.Append("day ").Append(entry.At.TotalDays).Append(" (").Append(entry.AgeInDays).Append("d ago)  ");
                sb.Append(world.Registry.NameOf(entry.Actor));
                sb.Append(" -> ").Append(entry.Other.IsNone ? "nobody" : world.Registry.NameOf(entry.Other));
                sb.Append(theirs ? "  [" + RouteOf(recognized, entry.EventId) + "]" : "  [not theirs to know]");
                sb.Append('\n');
            }

            IReadOnlyList<NarrativeThread> matters = ItemProvenance.OpenMatters(world, recognized);
            sb.Append("  ").Append(recognized.Count).Append(" of ").Append(all.Count)
              .Append(" entries are theirs to know; ").Append(matters.Count)
              .Append(" still-open matter(s) hang on them\n");
            for (int i = 0; i < matters.Count; i++)
            {
                sb.Append("    ").Append(matters[i].ArchetypeId).Append(' ').Append(matters[i].Id.Value)
                  .Append(" [").Append(matters[i].State).Append("]\n");
            }

            return sb.ToString();
        }

        private static bool Recognizes(IReadOnlyList<ProvenanceEntry> recognized, EntityId eventId)
        {
            for (int i = 0; i < recognized.Count; i++)
            {
                if (recognized[i].EventId == eventId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string RouteOf(IReadOnlyList<ProvenanceEntry> recognized, EntityId eventId)
        {
            for (int i = 0; i < recognized.Count; i++)
            {
                if (recognized[i].EventId == eventId && recognized[i].RecognizedVia.HasValue)
                {
                    return recognized[i].RecognizedVia.Value.ToString();
                }
            }

            return "unknown";
        }

        /// <summary>
        /// BQ-081 x BQ-071. Which of one person's callbacks they would actually raise in front of
        /// another, and which claim keeps the rest of them back.
        ///
        /// The companion to <see cref="DescribeCallbacks"/>, and the same doctrine: a step whose
        /// content is a gate has to be able to show the gate closing. This one shows the second
        /// gate - the material is theirs to remember either way, and the question here is only whom
        /// they would spend it on.
        /// </summary>
        public static string DescribeCallbackPermission(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            EntityId listener,
            GameTime now,
            CallbackSelection selection = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("callbacks ").Append(Who(world, recaller)).Append(" would raise with ")
              .Append(Who(world, listener)).Append('\n');
            if (world == null || recaller.IsNone || listener.IsNone)
            {
                sb.Append("  nobody to recall anything, or nobody to recall it to\n");
                return sb.ToString();
            }

            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(world, vanilla, recaller, now, selection);
            if (hooks.Count == 0)
            {
                sb.Append("  nothing old enough that they could know about\n");
                return sb.ToString();
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                CallbackPermit permit = CallbackDisclosure.Permit(world, hooks[i], listener, now);
                sb.Append("  ").Append(hooks[i].EventType.ToString().PadRight(20));
                sb.Append(hooks[i].PrimaryKind.ToString().PadRight(14));
                sb.Append(permit.Allowed ? "would say  " : "withheld   ");
                sb.Append(permit.Because);
                if (!permit.Withheld.IsNone)
                {
                    sb.Append(" [").Append(permit.Withheld.Value).Append(' ').Append(permit.Strategy).Append(']');
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-082. Whether this recaller has continuity humour to bring to a given thread and
        /// site - old, memorable material that did not happen there - or an honest "nothing
        /// earns it" when the ledger offers nothing of the kind.
        /// </summary>
        public static string DescribeContinuityHumour(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            ContinuityContext context,
            GameTime now,
            CallbackSelection selection = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("continuity humour available to ").Append(Who(world, recaller)).Append('\n');
            if (world == null || recaller.IsNone)
            {
                sb.Append("  nobody to recall anything\n");
                return sb.ToString();
            }

            CallbackHook hook = CallbackRecurrence.Best(world, vanilla, recaller, context, now, selection);
            if (hook == null)
            {
                sb.Append("  nothing earns it: no available hook is both memorable and out of its own context\n");
                return sb.ToString();
            }

            sb.Append("  ").Append(hook.EventType.ToString().PadRight(20));
            sb.Append(hook.PrimaryKind.ToString().PadRight(14));
            sb.Append(hook.Route.ToString().PadRight(10));
            sb.Append("with ").Append(hook.Counterpart.IsNone ? "nobody" : world.Registry.NameOf(hook.Counterpart));
            sb.Append(" [").Append(hook.Party).Append(']');
            sb.Append("  weight ").Append(hook.Weight.ToString("0.00"));
            sb.Append('\n');
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

        /// <summary>
        /// BQ-088. Which place a matter was given, and why that one rather than a new one.
        ///
        /// The step's done-when is that reuse can be explained, and "why this one" is only half an
        /// explanation: the interesting half is why every other place the world knows was passed
        /// over. So every candidate is listed with its tier and, where it was refused, the reasons
        /// it was refused - which is also what makes a wrong answer diagnosable, because a policy
        /// that generated when it should not have will say which rule did it.
        /// </summary>
        public static string DescribeSiteChoice(NarrativeWorldState world, SitePlan plan)
        {
            StringBuilder sb = new StringBuilder();
            if (world == null || plan == null)
            {
                sb.Append("place for nothing: no world or no plan to place\n");
                return sb.ToString();
            }

            SiteChoice choice = SiteReuse.Choose(world, plan);
            sb.Append("place for ").Append(plan.ThreadId.IsNone ? "no matter" : plan.ThreadId.Value)
              .Append(": ").Append(choice.Reused ? "reuse " + choice.Site.Name : "generate a new one")
              .Append('\n');
            sb.Append("  needs ").Append(string.IsNullOrEmpty(plan.SiteType) ? "any kind of place" : "a " + plan.SiteType)
              .Append(plan.Restricted ? ", what it keeps behind somebody's permission" : ", what it keeps open to anybody")
              .Append('\n');
            sb.Append("  because ").Append(choice.Reason).Append('\n');

            sb.Append("  considered ").Append(choice.Considered.Count).Append(" existing place(s)\n");
            for (int i = 0; i < choice.Considered.Count; i++)
            {
                SiteCandidateReading reading = choice.Considered[i];
                sb.Append(reading.Chosen ? "    chosen  " : reading.CanHost ? "    could   " : "    refused ");
                sb.Append(reading.Site.Name).Append(" [").Append(reading.Site.SiteType).Append("]  ");
                sb.Append(reading.Tier);
                for (int j = 0; j < reading.Refusals.Count; j++)
                {
                    sb.Append("\n              ").Append(reading.Refusals[j]);
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-089. The abstract plan a place of some curated kind is made from: every part it has,
        /// every route between them, and what each of those requires.
        ///
        /// The step's done-when is that this can explain every required node and edge, so it lists
        /// them all and says of each whether every place of the kind has one - which is also the
        /// readable form of "the same kind of place, and not the same place": the required lines
        /// are identical between two plans from one grammar, and the chosen ones are not. What the
        /// kind allows and this place does not have is listed too, with the reason, because an
        /// absence a trace does not mention reads as a hole in the grammar.
        ///
        /// Nothing here is geometry, and that is not a gap in the trace: the plan is authoritative
        /// for meaning and the map is the embodiment (`PP §3`). Which affordance is real on the
        /// live build is BQ-090's evidence question, and this reports what the place *requires*.
        /// </summary>
        public static string DescribeSiteLayout(SiteLayout layout)
        {
            StringBuilder sb = new StringBuilder();
            if (layout == null)
            {
                sb.Append("site plan: no grammar composed\n");
                return sb.ToString();
            }

            sb.Append("site plan ").Append(layout.GrammarId)
              .Append(" [").Append(layout.SiteType).Append("] seed ").Append(layout.Seed).Append('\n');
            sb.Append("  what it keeps is ")
              .Append(layout.Restricted ? "behind somebody's permission" : "open to anybody")
              .Append('\n');

            sb.Append("  parts ").Append(layout.Nodes.Count).Append('\n');
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                sb.Append(node.Required ? "    every one  " : "    this one   ").Append(node.Id);
                AppendAffordances(sb, node.Affordances);
                if (node.Spec.HasSocket)
                {
                    sb.Append("; authored piece ").Append(node.Socket);
                }

                sb.Append('\n');
            }

            for (int i = 0; i < layout.Omitted.Count; i++)
            {
                SiteOmission omission = layout.Omitted[i];
                sb.Append("    not here   ").Append(omission.Id).Append("; ")
                  .Append(omission.Reason == SiteOmissionReason.NotDrawn
                      ? "another place of this kind may have one"
                      : "nothing this place has leads to it")
                  .Append('\n');
            }

            sb.Append("  routes ").Append(layout.Routes.Count).Append('\n');
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                sb.Append(route.Required ? "    every one  " : "    this one   ")
                  .Append(route.From).Append(" -> ").Append(route.To);
                if (route.ActionId.Length > 0)
                {
                    sb.Append(" by ").Append(route.ActionId);
                }

                if (route.NeedsAdmission)
                {
                    sb.Append("; waits on somebody letting you in");
                }
                else if (route.IsEntry)
                {
                    sb.Append("; goes around everybody");
                }

                AppendAffordances(sb, route.Affordances);
                sb.Append('\n');
            }

            sb.Append("  ways in ").Append(layout.Approaches.Count).Append('\n');
            for (int i = 0; i < layout.Approaches.Count; i++)
            {
                sb.Append("    ").Append(layout.Approaches[i]).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-090. Every way through a place to one part of it, and for each one either the verbs
        /// it is taken with or the leg that stopped it.
        ///
        /// The refusals are the point. A place whose plan reads perfectly well and whose physical
        /// routes cannot be offered on this build is a thing the inspector has to be able to say
        /// out loud, because the alternative is a player shown a mined wall that does nothing.
        /// </summary>
        public static string DescribeSiteRoutes(SiteRouteProjection projection)
        {
            StringBuilder sb = new StringBuilder();
            if (projection == null)
            {
                sb.Append("ways through: nothing projected\n");
                return sb.ToString();
            }

            sb.Append("ways through ")
              .Append(projection.Layout == null ? "no plan" : projection.Layout.GrammarId)
              .Append(" to ").Append(projection.Objective.Length > 0 ? projection.Objective : "nowhere named")
              .Append('\n');

            if (projection.Refusal.Length > 0)
            {
                sb.Append("  nothing to read: ").Append(projection.Refusal).Append('\n');
                return sb.ToString();
            }

            sb.Append("  ").Append(projection.Promised.Count).Append(" of ").Append(projection.Ways.Count)
              .Append(" can be offered on this build\n");

            for (int i = 0; i < projection.Ways.Count; i++)
            {
                SiteWayThrough way = projection.Ways[i];
                sb.Append(way.Promised ? "    offered  " : "    refused  ").Append(way);
                if (way.NeedsAdmission)
                {
                    sb.Append("; waits on somebody letting you in");
                }

                sb.Append('\n');

                for (int leg = 0; leg < way.Legs.Count; leg++)
                {
                    AppendLeg(sb, way.Legs[leg]);
                }
            }

            return sb.ToString();
        }

        private static void AppendLeg(StringBuilder sb, SiteRouteLeg leg)
        {
            sb.Append("      ").Append(leg.From).Append(" -> ").Append(leg.To);
            AppendAffordances(sb, leg.Route.Affordances);
            if (leg.Verbs.Count == 0)
            {
                sb.Append("; nothing to get past");
            }

            sb.Append('\n');

            for (int i = 0; i < leg.Verbs.Count; i++)
            {
                SiteRouteVerb verb = leg.Verbs[i];
                sb.Append(verb.Promised ? "        can    " : "        cannot ").Append(verb.ActionId);
                sb.Append(verb.Authored ? " (this route's own verb)" : " (answers the requirement)");
                if (verb.Claim != null)
                {
                    sb.Append("; ").Append(verb.Claim.Evidence);
                    if (verb.Claim.LeansOn.Length > 0)
                    {
                        sb.Append(", leaning on ").Append(verb.Claim.LeansOn);
                    }
                }

                if (!verb.Promised)
                {
                    sb.Append("; ").Append(verb.Refusal);
                }

                sb.Append('\n');
            }

            for (int i = 0; i < leg.Unanswered.Count; i++)
            {
                sb.Append("        nobody answers ").Append(leg.Unanswered[i]).Append('\n');
            }
        }

        /// <summary>
        /// BQ-092. Every plan drawn for one errand, what each scored, and every reason a plan was
        /// refused.
        ///
        /// The refusals are the step's done-when, so each one prints its kind and the sentence that
        /// explains it - the part that could not be reached, the way in that reaches nothing the
        /// errand wants, the verb the build would not promise. The scores print beside the counts
        /// they were worked out from, because a number nobody can check is an assertion rather than
        /// a measurement: "diversity 0.83" says nothing on its own, and "3 plays, admitted and
        /// uninvited" says what was seen.
        /// </summary>
        public static string DescribeSiteCandidates(SiteCandidateSelection selection)
        {
            StringBuilder sb = new StringBuilder();
            if (selection == null)
            {
                sb.Append("site candidates: nothing weighed\n");
                return sb.ToString();
            }

            sb.Append("site candidates ")
              .Append(selection.Grammar == null ? "no grammar" : selection.Grammar.Id)
              .Append(" from seed ").Append(selection.Seed)
              .Append(", for an errand after ").Append(selection.Objective).Append('\n');

            int usable = 0;
            for (int i = 0; i < selection.Considered.Count; i++)
            {
                if (selection.Considered[i].Usable)
                {
                    usable++;
                }
            }

            sb.Append("  ").Append(selection.Considered.Count).Append(" drawn, ").Append(usable)
              .Append(" usable\n");

            if (!selection.Selected)
            {
                sb.Append("  nothing chosen: ")
                  .Append(selection.Refusal.Length > 0 ? selection.Refusal : "no reason was recorded")
                  .Append('\n');
            }

            for (int i = 0; i < selection.Considered.Count; i++)
            {
                AppendCandidate(sb, selection.Considered[i]);
            }

            return sb.ToString();
        }

        private static void AppendCandidate(StringBuilder sb, SitePlanCandidate candidate)
        {
            sb.Append(candidate.Chosen ? "    chosen   " : candidate.Usable ? "    usable   " : "    refused  ")
              .Append("seed ").Append(candidate.Seed)
              .Append("; ").Append(candidate.ObjectiveNodeId.Length > 0
                  ? candidate.ObjectiveNodeId
                  : "nowhere answers the errand")
              .Append("; scored ").Append(Fixed(candidate.Score.Total))
              .Append('\n');

            SiteCandidateScore score = candidate.Score;
            sb.Append("      reach ").Append(Fixed(score.Reachability))
              .Append(" (").Append(score.ReachableNodes).Append(" of ").Append(score.Nodes)
              .Append(" parts can be got to)\n");
            sb.Append("      diversity ").Append(Fixed(score.RouteDiversity))
              .Append(" (").Append(score.PromisedWays).Append(" way(s), ").Append(score.DistinctPlays)
              .Append(" play(s), ")
              .Append(score.AdmittedWay && score.UninvitedWay
                  ? "admitted and uninvited"
                  : score.AdmittedWay ? "admitted only" : score.UninvitedWay ? "uninvited only" : "neither")
              .Append(")\n");
            sb.Append("      separation ").Append(Fixed(score.ObjectiveSeparation))
              .Append(" (").Append(score.ShortestWayLegs).Append(" leg(s) at the shortest)\n");
            sb.Append("      evidence ").Append(Fixed(score.EvidenceDistribution))
              .Append(" (").Append(score.ReachableKeepNodes).Append(" of ").Append(score.KeepNodes)
              .Append(" part(s) that hold something can be got to)\n");
            sb.Append("      loops ").Append(Fixed(score.LoopQuality))
              .Append(" (").Append(score.RealAlternatives).Append(" alternative(s) worth having)\n");
            sb.Append("      mechanics ").Append(Fixed(score.MechanicVocabulary))
              .Append(" (").Append(score.Mechanics).Append(" requirement(s) actually answered on the way)\n");

            for (int i = 0; i < candidate.Flaws.Count; i++)
            {
                SiteCandidateFlaw flaw = candidate.Flaws[i];
                sb.Append("      refused ").Append(flaw.Kind).Append(": ").Append(flaw.Reason).Append('\n');
            }
        }

        private static string Fixed(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void AppendAffordances(StringBuilder sb, IReadOnlyList<SiteAffordance> affordances)
        {
            if (affordances.Count == 0)
            {
                return;
            }

            sb.Append("; needs ");
            for (int i = 0; i < affordances.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(affordances[i]);
            }
        }

        /// <summary>
        /// BQ-139. One whole scenario plan, in enough detail that somebody can understand the
        /// place without anything having rendered it.
        ///
        /// That is the bar this step is held to, so the trace is the plan and not a summary of it:
        /// every region with what it requires, how deep in it is and what is anchored there; every
        /// route with what it asks and whether this build can keep it; every ring, every genuine
        /// alternative and every way that turned out to be one of them reworded; who the plan is
        /// entitled to say is where, and everybody it is not; the authored sockets it carries and
        /// fills none of; each invariant with what it was read off; and the plans that were refused
        /// so the choice can be argued with.
        ///
        /// Nothing here is geometry and nothing here is a line a player reads. A plan that had
        /// either would be answering a question BQ-140 has not asked yet.
        /// </summary>
        public static string DescribeScenarioPlan(NarrativeWorldState world, ScenarioPlan plan)
        {
            StringBuilder sb = new StringBuilder();
            if (plan == null)
            {
                sb.Append("scenario plan: nothing planned\n");
                return sb.ToString();
            }

            sb.Append("scenario plan ").Append(plan.PlanId.Length > 0 ? plan.PlanId : "unidentified").Append('\n');
            sb.Append("  ").Append(plan.GrammarId.Length > 0 ? plan.GrammarId : "no grammar")
              .Append(" [").Append(plan.SiteType).Append("] composed at seed ").Append(plan.Seed)
              .Append(", drawn from batch seed ").Append(plan.SelectionSeed).Append('\n');
            sb.Append("  for matter ").Append(plan.ThreadId.IsNone ? "nobody's" : plan.ThreadId.Value)
              .Append(", an errand after ").Append(plan.Objective)
              .Append(plan.ObjectiveRegionId.Length > 0
                  ? ", answered in " + plan.ObjectiveRegionId
                  : ", answered nowhere")
              .Append('\n');

            if (!plan.Planned)
            {
                sb.Append("  no plan: ");
                for (int i = 0; i < plan.Refusals.Count; i++)
                {
                    sb.Append(i > 0 ? "; " : string.Empty).Append(plan.Refusals[i]);
                }

                sb.Append(plan.Refusals.Count == 0 ? "no reason was recorded" : string.Empty).Append('\n');
                AppendRefusedPlans(sb, plan);
                return sb.ToString();
            }

            sb.Append("  ").Append(plan.Valid ? "valid: every invariant holds" : "not valid: an invariant is broken")
              .Append('\n');

            AppendScenarioRegions(sb, world, plan);
            AppendScenarioRoutes(sb, plan);
            AppendScenarioCycles(sb, plan);
            AppendScenarioAlternatives(sb, plan);
            AppendScenarioOccupancy(sb, world, plan);
            AppendScenarioSockets(sb, plan);
            AppendScenarioInvariants(sb, plan);
            AppendRefusedPlans(sb, plan);

            return sb.ToString();
        }

        private static void AppendScenarioRegions(StringBuilder sb, NarrativeWorldState world, ScenarioPlan plan)
        {
            sb.Append("  regions ").Append(plan.Regions.Count).Append('\n');
            for (int i = 0; i < plan.Regions.Count; i++)
            {
                ScenarioRegion region = plan.Regions[i];
                sb.Append(region.Required ? "    every one  " : "    this one   ").Append(region.Id);
                AppendAffordances(sb, region.Affordances);
                if (region.Socket.Length > 0)
                {
                    sb.Append("; authored piece ").Append(region.Socket);
                }

                sb.Append("; ").Append(region.Reachable
                    ? region.Depth + " leg(s) in at the shortest"
                    : "no way to it can be offered on this build");
                if (region.IsObjective)
                {
                    sb.Append("; this is what the errand came for");
                }

                sb.Append('\n');

                for (int a = 0; a < region.Anchors.Count; a++)
                {
                    AppendScenarioAnchor(sb, world, region.Anchors[a]);
                }
            }

            SiteLayout layout = plan.Layout;
            for (int i = 0; i < layout.Omitted.Count; i++)
            {
                SiteOmission omission = layout.Omitted[i];
                sb.Append("    not here   ").Append(omission.Id).Append("; ")
                  .Append(omission.Reason == SiteOmissionReason.NotDrawn
                      ? "another place of this kind may have one"
                      : "nothing this place has leads to it")
                  .Append('\n');
            }
        }

        private static void AppendScenarioAnchor(StringBuilder sb, NarrativeWorldState world, ScenarioAnchor anchor)
        {
            sb.Append("        anchored ").Append(anchor.Kind);
            if (!anchor.SubjectId.IsNone)
            {
                sb.Append(' ').Append(anchor.Kind == ScenarioAnchorKind.Captive
                    ? Who(world, anchor.SubjectId)
                    : anchor.SubjectId.Value);
            }

            if (!anchor.EventId.IsNone)
            {
                sb.Append("; put here by ").Append(anchor.EventId.Value);
            }

            if (!anchor.FactId.IsNone)
            {
                sb.Append("; proves ").Append(anchor.FactId.Value);
            }

            if (anchor.Reason.Length > 0)
            {
                sb.Append("; ").Append(anchor.Reason);
            }

            sb.Append('\n');
        }

        private static void AppendScenarioRoutes(StringBuilder sb, ScenarioPlan plan)
        {
            sb.Append("  routes ").Append(plan.Routes.Count).Append('\n');
            for (int i = 0; i < plan.Routes.Count; i++)
            {
                ScenarioRoute route = plan.Routes[i];
                sb.Append(route.Required ? "    every one  " : "    this one   ")
                  .Append(route.From).Append(" -> ").Append(route.To);
                if (route.ActionId.Length > 0)
                {
                    sb.Append(" by ").Append(route.ActionId);
                }

                if (route.NeedsAdmission)
                {
                    sb.Append("; waits on somebody letting you in");
                }
                else if (route.IsEntry)
                {
                    sb.Append("; goes around everybody");
                }

                AppendAffordances(sb, route.Affordances);
                if (route.Free)
                {
                    sb.Append("; nothing to get past");
                }

                sb.Append("; ").Append(route.Support);
                if (route.Support != ScenarioSupport.Supported)
                {
                    sb.Append("; ").Append(route.Refusal);
                }

                sb.Append('\n');
            }
        }

        private static void AppendScenarioCycles(StringBuilder sb, ScenarioPlan plan)
        {
            sb.Append("  cycles ").Append(plan.Cycles.Count).Append('\n');
            if (plan.Cycles.Count == 0)
            {
                sb.Append("    none: nothing in this plan comes back round to where it started\n");
            }

            for (int i = 0; i < plan.Cycles.Count; i++)
            {
                ScenarioCycle cycle = plan.Cycles[i];
                sb.Append(cycle.Kind == ScenarioCycleKind.Reentrant ? "    reentrant  " : "    internal   ")
                  .Append(cycle).Append("; ").Append(cycle.Demanding)
                  .Append(" route(s) round it ask something")
                  .Append(cycle.Cosmetic ? "; a ring nobody has to get past anything on" : string.Empty)
                  .Append('\n');
            }
        }

        private static void AppendScenarioAlternatives(StringBuilder sb, ScenarioPlan plan)
        {
            sb.Append("  alternatives ").Append(plan.Alternatives.Count).Append('\n');
            for (int i = 0; i < plan.Alternatives.Count; i++)
            {
                ScenarioAlternative alternative = plan.Alternatives[i];
                sb.Append("    to ").Append(alternative.TargetRegionId).Append("   ").Append(alternative)
                  .Append(alternative.NeedsAdmission ? "; waits on somebody letting you in" : string.Empty)
                  .Append('\n');
            }

            for (int i = 0; i < plan.CollapsedAlternatives.Count; i++)
            {
                ScenarioAlternative collapsed = plan.CollapsedAlternatives[i];
                sb.Append("    not one   ").Append(collapsed)
                  .Append("; the same play as ").Append(collapsed.CollapsedInto).Append('\n');
            }
        }

        private static void AppendScenarioOccupancy(StringBuilder sb, NarrativeWorldState world, ScenarioPlan plan)
        {
            sb.Append("  occupant regions ").Append(plan.OccupantRegions.Count).Append('\n');
            for (int i = 0; i < plan.OccupantRegions.Count; i++)
            {
                ScenarioOccupantRegion region = plan.OccupantRegions[i];
                sb.Append("    ").Append(region.Kind == ScenarioOccupancyKind.Garrison ? "garrison " : "held     ")
                  .Append(region.RegionId).Append(" (").Append(region.Affordance).Append(')');
                if (!region.OrganizationId.IsNone)
                {
                    sb.Append("; crew ").Append(Who(world, region.OrganizationId));
                }

                sb.Append("; ").Append(region.Reason).Append('\n');

                for (int o = 0; o < region.Occupants.Count; o++)
                {
                    ScenarioOccupant occupant = region.Occupants[o];
                    sb.Append("        ").Append(Who(world, occupant.NpcId))
                      .Append("; ").Append(occupant.Presence)
                      .Append("; because of ").Append(occupant.Because.Value).Append('\n');
                }
            }

            if (plan.UnplacedOccupants.Count == 0)
            {
                return;
            }

            sb.Append("  at the place and in no region ").Append(plan.UnplacedOccupants.Count)
              .Append(": nothing in the plan says where a body stands\n");
            for (int i = 0; i < plan.UnplacedOccupants.Count; i++)
            {
                ScenarioOccupant occupant = plan.UnplacedOccupants[i];
                sb.Append("        ").Append(Who(world, occupant.NpcId))
                  .Append("; ").Append(occupant.Presence)
                  .Append("; because of ").Append(occupant.Because.Value).Append('\n');
            }
        }

        private static void AppendScenarioSockets(StringBuilder sb, ScenarioPlan plan)
        {
            sb.Append("  authored-piece sockets ").Append(plan.Sockets.Count)
              .Append(", none filled: filling one needs a physical realization (BQ-140)\n");
            for (int i = 0; i < plan.Sockets.Count; i++)
            {
                ScenarioSocket socket = plan.Sockets[i];
                sb.Append(socket.Required ? "    every one  " : "    this one   ")
                  .Append(socket.Socket).Append(" fills ").Append(socket.RegionId).Append('\n');
            }
        }

        private static void AppendScenarioInvariants(StringBuilder sb, ScenarioPlan plan)
        {
            sb.Append("  invariants ").Append(plan.Validation.Findings.Count).Append('\n');
            for (int i = 0; i < plan.Validation.Findings.Count; i++)
            {
                ScenarioFinding finding = plan.Validation.Findings[i];
                sb.Append(finding.Held ? "    holds   " : "    broken  ")
                  .Append(finding.Invariant).Append(": ").Append(finding.Reason).Append('\n');
            }
        }

        private static void AppendRefusedPlans(StringBuilder sb, ScenarioPlan plan)
        {
            if (plan.Selection == null)
            {
                return;
            }

            int refused = 0;
            for (int i = 0; i < plan.Selection.Considered.Count; i++)
            {
                refused += plan.Selection.Considered[i].Usable ? 0 : 1;
            }

            sb.Append("  plans refused ").Append(refused).Append(" of ")
              .Append(plan.Selection.Considered.Count).Append(" drawn\n");

            for (int i = 0; i < plan.Selection.Considered.Count; i++)
            {
                SitePlanCandidate candidate = plan.Selection.Considered[i];
                for (int f = 0; f < candidate.Flaws.Count; f++)
                {
                    sb.Append("    seed ").Append(candidate.Seed).Append("; ")
                      .Append(candidate.Flaws[f].Kind).Append(": ")
                      .Append(candidate.Flaws[f].Reason).Append('\n');
                }
            }
        }

        /// <summary>
        /// BQ-091. What one matter leaves in one place, why each of it is there, and everything
        /// the place does not get.
        ///
        /// The omissions and the empty parts are the point. "No template chest" is not visible in
        /// a list of contents - a good derivation and a generous one look the same from the front -
        /// so the inspector shows what the matter named and did not leave here, and which part of
        /// the plan stayed empty because nothing in the world filled it.
        /// </summary>
        /// <summary>
        /// BQ-140. The place as it was actually built: which authored piece stands for each part,
        /// where it is, what joins them, what the plan asked for and did not get, and every check
        /// the built place was put through.
        ///
        /// The omissions and the failed checks are the point, in the same way BQ-090's refusals
        /// are. A site that quietly built an open drift where the plan wanted a hidden way would
        /// read perfectly well here; a site that dropped it and said so cannot be mistaken for one
        /// that had it.
        /// </summary>
        public static string DescribeSiteStructure(SiteRealizationResult realization)
        {
            StringBuilder sb = new StringBuilder();
            if (realization == null)
            {
                sb.Append("site structure: nothing built\n");
                return sb.ToString();
            }

            for (int i = 0; i < realization.Refusals.Count; i++)
            {
                sb.Append("  refused    ").Append(realization.Refusals[i]).Append('\n');
            }

            SiteStructure structure = realization.Structure;
            if (structure == null)
            {
                if (realization.Refusals.Count == 0)
                {
                    sb.Append("site structure: nothing built and no reason given\n");
                }

                return sb.ToString();
            }

            sb.Append("site structure ").Append(structure.GrammarId)
              .Append(" [").Append(structure.Family).Append("] seed ").Append(structure.Seed)
              .Append(", for an errand after ").Append(structure.Objective)
              .Append(" in ").Append(structure.ObjectiveNodeId).Append('\n');
            sb.Append("  ground ").Append(structure.Width).Append('x').Append(structure.Height).Append('\n');

            sb.Append("  pieces ").Append(structure.Placements.Count).Append('\n');
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                sb.Append("    ").Append(placement.NodeId).Append(" = ").Append(placement.Piece.Id)
                  .Append(" at ").Append(placement.X).Append(',').Append(placement.Y)
                  .Append(" (").Append(placement.Piece.Width).Append('x').Append(placement.Piece.Height)
                  .Append("), ").Append(placement.Depth).Append(" way(s) in");
                AppendAffordances(sb, placement.Piece.Provides);
                sb.Append('\n');
            }

            sb.Append("  ways ").Append(structure.Connectors.Count).Append('\n');
            for (int i = 0; i < structure.Connectors.Count; i++)
            {
                SiteConnector connector = structure.Connectors[i];
                sb.Append(connector.Passable ? "    open       " : "    shut       ")
                  .Append(connector.From).Append(" -> ").Append(connector.To)
                  .Append(" (").Append(connector.Kind).Append(')');
                if (!connector.Passable)
                {
                    sb.Append("; ").Append(connector.Refusal);
                }

                sb.Append('\n');
            }

            for (int i = 0; i < structure.Anchors.Count; i++)
            {
                SiteAnchor anchor = structure.Anchors[i];
                sb.Append("    holds      ").Append(anchor.Kind == SiteAnchorKind.Cargo ? "cargo " : "person ")
                  .Append(anchor.Id.Value).Append(" in ").Append(anchor.NodeId);
                if (anchor.Why.Length > 0)
                {
                    sb.Append("; ").Append(anchor.Why);
                }

                sb.Append('\n');
            }

            for (int i = 0; i < structure.Omitted.Count; i++)
            {
                SiteStructureOmission omission = structure.Omitted[i];
                sb.Append("    not here   ").Append(omission.What).Append("; ").Append(omission.Reason).Append('\n');
            }

            for (int i = 0; i < realization.Checks.Count; i++)
            {
                SiteStructureCheck check = realization.Checks[i];
                sb.Append(check.Held ? "    checked    " : "    FAILED     ")
                  .Append(check.What).Append("; ").Append(check.Detail).Append('\n');
            }

            SiteTopology topology = structure.Topology();
            sb.Append("  shape ").Append(topology.Signature).Append('\n');
            return sb.ToString();
        }

        public static string DescribeSiteContents(NarrativeWorldState world, SiteContentsReading contents)
        {
            StringBuilder sb = new StringBuilder();
            if (contents == null)
            {
                sb.Append("contents: nothing derived\n");
                return sb.ToString();
            }

            sb.Append("contents of the place ")
              .Append(contents.Thread == null ? "no matter" : contents.Thread.ArchetypeId)
              .Append(" needs")
              .Append(contents.Layout == null ? string.Empty : " [" + contents.Layout.GrammarId + "]")
              .Append('\n');

            for (int i = 0; i < contents.Refusals.Count; i++)
            {
                sb.Append("  cannot furnish: ").Append(contents.Refusals[i]).Append('\n');
            }

            sb.Append("  people ").Append(contents.Occupants.Count).Append('\n');
            for (int i = 0; i < contents.Occupants.Count; i++)
            {
                SiteOccupancy occupant = contents.Occupants[i];
                sb.Append("    ").Append(occupant.Presence).Append("  ")
                  .Append(world == null ? occupant.Id.Value : world.Registry.NameOf(occupant.Id));
                if (occupant.NodeId.Length > 0)
                {
                    sb.Append(" in ").Append(occupant.NodeId);
                }

                sb.Append(occupant.AlreadyExists ? " (the game has them)" : " (to be built)")
                  .Append("; ").Append(occupant.Reason).Append('\n');
            }

            sb.Append("  things ").Append(contents.Cargo.Count).Append('\n');
            for (int i = 0; i < contents.Cargo.Count; i++)
            {
                SiteHolding holding = contents.Cargo[i];
                sb.Append("    ").Append(holding.Keeping).Append("  ").Append(holding.Item.Name)
                  .Append(" held by ")
                  .Append(world == null ? holding.HolderId.Value : world.Registry.NameOf(holding.HolderId));
                if (holding.NodeId.Length > 0)
                {
                    sb.Append(" in ").Append(holding.NodeId);
                }

                if (!holding.EvidenceForFact.IsNone)
                {
                    sb.Append("; proves ").Append(holding.EvidenceForFact.Value);
                }

                sb.Append("; ").Append(holding.Reason).Append('\n');
            }

            for (int i = 0; i < contents.Omitted.Count; i++)
            {
                SiteContentsOmission omission = contents.Omitted[i];
                sb.Append("  not here  ")
                  .Append(world == null ? omission.Id.Value : world.Registry.NameOf(omission.Id))
                  .Append("; ").Append(omission.Reason).Append('\n');
            }

            for (int i = 0; i < contents.Vacant.Count; i++)
            {
                SiteVacancy vacancy = contents.Vacant[i];
                sb.Append("  empty     ").Append(vacancy.NodeId).Append(" (").Append(vacancy.Affordance)
                  .Append("); ").Append(vacancy.Reason).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// BQ-087. What a return visit found, and if something is missing, which thing.
        ///
        /// The done-when of the site proof is a comparison, so the tooling has to be able to show
        /// both halves of it: what genesis wrote down, and what is there now. It reports the ledger
        /// length beside them because "nothing regenerated" and "no historical event was
        /// redispatched" are the same claim seen twice - genesis appends nothing, so a return that
        /// moved that number moved it for some other reason and the trace should say so.
        /// </summary>
        public static string DescribeSite(NarrativeWorldState world, EntityId siteId, IVanillaState vanilla)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("site ").Append(siteId.IsNone ? "nothing" : siteId.Value).Append('\n');
            if (world == null)
            {
                sb.Append("  no world to look in\n");
                return sb.ToString();
            }

            SiteVisit visit = SiteGenesis.Visit(world, siteId, vanilla);
            if (!visit.Found)
            {
                sb.Append("  the world knows no such place\n");
                return sb.ToString();
            }

            NarrativeSite site = visit.Site;
            sb.Append("  ").Append(site.Name).Append(" [").Append(site.SiteType).Append("] ")
              .Append(site.Persistence).Append(site.Restricted ? ", restricted" : ", open").Append('\n');
            if (!string.IsNullOrEmpty(site.GrammarId))
            {
                sb.Append("  planned from ").Append(site.GrammarId)
                  .Append(" at seed ").Append(site.GenerationSeed).Append('\n');
            }

            sb.Append("  genesis: ")
              .Append(visit.Established ? "established " + site.EstablishedAt : "never generated here")
              .Append("; body: ")
              .Append(visit.Embodied ? site.VanillaZoneRef : "none bound")
              .Append('\n');

            sb.Append("  occupants ").Append(site.OccupantIds.Count - visit.MissingOccupants.Count)
              .Append('/').Append(site.OccupantIds.Count).Append('\n');
            for (int i = 0; i < site.OccupantIds.Count; i++)
            {
                EntityId who = site.OccupantIds[i];
                sb.Append(Contains(visit.MissingOccupants, who) ? "    gone    " : "    here    ")
                  .Append(world.Registry.NameOf(who)).Append('\n');
            }

            sb.Append("  cargo ").Append(site.ImportantObjectIds.Count - visit.MissingCargo.Count)
              .Append('/').Append(site.ImportantObjectIds.Count).Append('\n');
            for (int i = 0; i < site.ImportantObjectIds.Count; i++)
            {
                EntityId what = site.ImportantObjectIds[i];
                sb.Append(Contains(visit.MissingCargo, what) ? "    gone    " : "    here    ")
                  .Append(what.Value).Append('\n');
            }

            sb.Append("  ways in\n");
            for (int i = 0; i < site.Approaches.Count; i++)
            {
                sb.Append("    ").Append(site.Approaches[i]).Append('\n');
            }

            AppendSiteHistory(sb, world, site, vanilla == null ? GameTime.Zero : vanilla.Now);

            sb.Append(visit.Intact ? "  intact" : "  changed since genesis")
              .Append("; ledger holds ").Append(world.Ledger.Count).Append(" event(s)\n");
            return sb.ToString();
        }

        /// <summary>
        /// BQ-086. What the place has been through and what it is known for, as the world has it.
        ///
        /// A place is described by its history rather than by its plan, so a return visit's trace
        /// carries it: a site somebody cleared a year ago says so, in the same block that says who
        /// is still standing in it.
        /// </summary>
        private static void AppendSiteHistory(
            StringBuilder sb,
            NarrativeWorldState world,
            NarrativeSite site,
            GameTime now)
        {
            IReadOnlyList<SiteHistoryEntry> history = LocationHistory.Of(world, site.Id, now);
            sb.Append("  history ").Append(history.Count).Append(" notable event(s)\n");
            for (int i = 0; i < history.Count; i++)
            {
                SiteHistoryEntry entry = history[i];
                sb.Append("    ").Append(entry.Role.ToString().PadRight(9));
                sb.Append(entry.EventType.ToString().PadRight(20));
                sb.Append("day ").Append(entry.At.TotalDays).Append(" (").Append(entry.AgeInDays).Append("d ago)  ");
                sb.Append(world.Registry.NameOf(entry.Actor));
                if (!entry.Other.IsNone)
                {
                    sb.Append(" -> ").Append(world.Registry.NameOf(entry.Other));
                }

                sb.Append('\n');
            }

            IReadOnlyList<SiteLegend> legends = LocationHistory.Legends(history);
            if (legends.Count == 0)
            {
                sb.Append("  known for nothing yet\n");
                return;
            }

            sb.Append("  known for\n");
            for (int i = 0; i < legends.Count; i++)
            {
                SiteLegend legend = legends[i];
                sb.Append("    ").Append(legend.Subject.ToString().PadRight(14));
                sb.Append('x').Append(legend.Occurrences);
                sb.Append(legend.Repeated ? " (repeated)" : " (severity " + legend.Salience.ToString("0.00") + ")");
                sb.Append("  last day ").Append(legend.Last.TotalDays)
                  .Append(" (").Append(legend.AgeInDays).Append("d ago)\n");
            }
        }

        /// <summary>
        /// BQ-086. The same history as one person could tell it, beside the world's own.
        ///
        /// The same doctrine as <see cref="DescribeProvenance"/>: a step whose content is a gate
        /// has to be able to show the gate closing. "Why does she not know about the massacre" has
        /// two possible answers - the ledger recorded nothing of the kind here, or she has no route
        /// to what it did record - and this separates them.
        /// </summary>
        public static string DescribeSiteHistory(
            NarrativeWorldState world,
            EntityId siteId,
            EntityId viewer,
            GameTime now)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("history of ").Append(siteId.IsNone ? "nowhere" : siteId.Value);
            sb.Append(" as ").Append(Who(world, viewer)).Append(" could tell it\n");
            if (world == null || siteId.IsNone)
            {
                sb.Append("  no place to trace\n");
                return sb.ToString();
            }

            IReadOnlyList<SiteHistoryEntry> all = LocationHistory.Of(world, siteId, now);
            if (all.Count == 0)
            {
                sb.Append("  history recorded nothing here\n");
                return sb.ToString();
            }

            IReadOnlyList<SiteHistoryEntry> known = LocationHistory.KnownTo(world, siteId, viewer, now);
            for (int i = 0; i < all.Count; i++)
            {
                SiteHistoryEntry entry = all[i];
                CallbackRoute? route = RouteTo(known, entry.EventId);
                sb.Append(route.HasValue ? "  * " : "    ");
                sb.Append(entry.Role.ToString().PadRight(9));
                sb.Append(entry.EventType.ToString().PadRight(20));
                sb.Append("day ").Append(entry.At.TotalDays).Append(" (").Append(entry.AgeInDays).Append("d ago)  ");
                sb.Append(world.Registry.NameOf(entry.Actor));
                sb.Append(route.HasValue ? "  [" + route.Value + "]" : "  [not theirs to know]");
                sb.Append('\n');
            }

            IReadOnlyList<SiteLegend> legends = LocationHistory.Legends(known);
            sb.Append("  ").Append(known.Count).Append(" of ").Append(all.Count)
              .Append(" entries are theirs to know; ").Append(legends.Count)
              .Append(" legend(s) follow from them\n");
            for (int i = 0; i < legends.Count; i++)
            {
                sb.Append("    ").Append(legends[i].Subject).Append(" x").Append(legends[i].Occurrences).Append('\n');
            }

            return sb.ToString();
        }

        private static CallbackRoute? RouteTo(IReadOnlyList<SiteHistoryEntry> known, EntityId eventId)
        {
            for (int i = 0; i < known.Count; i++)
            {
                if (known[i].EventId == eventId)
                {
                    return known[i].KnownVia;
                }
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<EntityId> ids, EntityId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendTravelEvent(StringBuilder sb, NarrativeWorldState world, string label, EntityId eventId)
        {
            sb.Append("    ").Append(label.PadRight(12));
            if (eventId.IsNone)
            {
                sb.Append("not recorded\n");
                return;
            }

            WorldEvent found = null;
            for (int i = 0; i < world.Ledger.Events.Count; i++)
            {
                if (world.Ledger.Events[i].Id == eventId)
                {
                    found = world.Ledger.Events[i];
                    break;
                }
            }

            if (found == null)
            {
                sb.Append(eventId.Value).Append(" missing from ledger\n");
                return;
            }

            sb.Append(found.Type).Append(" at ").Append(found.Time)
              .Append(" in ").Append(world.Registry.NameOf(found.Zone)).Append('\n');
        }

        private static string Render(NarrativeWorldState world, Fact fact)
        {
            if (fact == null)
            {
                return "(missing fact)";
            }

            string subject = world.Registry.NameOf(fact.Subject);
            string obj = world.Registry.AllNpcs.ContainsKey(fact.Object)
                ? world.Registry.NameOf(fact.Object)
                : !string.IsNullOrEmpty(fact.Value) ? fact.Value : fact.Object.Value;
            string truth = fact.Truth == Knowledge.TruthState.True ? string.Empty : " (" + fact.Truth + "!)";
            return subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj + truth;
        }
    }
}
