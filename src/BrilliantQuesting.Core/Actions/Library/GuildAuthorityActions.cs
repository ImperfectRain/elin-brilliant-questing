using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// One matter a network would take on, and the person who can commit it to that.
    ///
    /// Never authored. The guild is whichever network the officer standing here belongs to, the
    /// matter is whichever standing trouble that network's interest table reads, and the framing
    /// is what it reads it as - so a beast on the road is the Fighters' business and a failed
    /// field is the Merchants' without either being written down anywhere as a guild's quest.
    /// </summary>
    internal sealed class GuildCommission
    {
        public GuildCommission(EntityId officer, GuildId guild, GuildFraming framing, Fact matter)
        {
            Officer = officer;
            Guild = guild;
            Framing = framing;
            Matter = matter;
        }

        /// <summary>Who speaks for the guild here.</summary>
        public EntityId Officer { get; }

        public GuildId Guild { get; }

        /// <summary>What the network makes of it, and therefore why it is theirs at all.</summary>
        public GuildFraming Framing { get; }

        /// <summary>The claim the guild would be answering. Always a live condition, never history.</summary>
        public Fact Matter { get; }
    }

    /// <summary>
    /// The third thing a guild does: it acts on what it reads.
    ///
    /// `BQ-037` gave the networks a channel and a reading (decision `D024`). Neither of those
    /// changes anything in the world - a member hears sooner and hears what a matter means, and
    /// that is all. What `MD 13.5` describes and this adds is the rest of it: a guild is also a
    /// body with people in it, and a member with standing can get those people put on a matter the
    /// guild already considers its own. That is the authority a rank actually confers, and it is
    /// the reason rank is not a cosmetic dialogue tag.
    ///
    /// Four rules keep it from becoming a guild-shaped wish.
    ///
    /// **The guild answers what its own interest table already reads.** Nothing here decides that
    /// beasts are for the Fighters; <see cref="GuildNetworks.Reads"/> does, off the predicate
    /// ontology, exactly as it does for what a network carries. So the same verb commits the
    /// Fighters to somebody who is not safe and the Merchants to a town that is short, and commits
    /// nobody to anything the Mages would only have gossiped about.
    ///
    /// **It answers conditions, never history.** A killing stays true for ever and no guild can
    /// undo it. What can be put right is a standing trouble
    /// (<see cref="FactPredicates.IsStandingTrouble"/>), which is the same set the shelter, repair,
    /// clearing and production routes already supersede when they answer one.
    ///
    /// **Membership is the gate, and it is a refusal rather than odds.** `PM 62` files invoking
    /// guild authority without membership under impossible, alongside blackmail without leverage,
    /// and this is that entry implemented. It stays inside `D012` because the guild is the party
    /// doing the work: a hall that does not know you is a contact who will not deal with you, the
    /// same shape as a receiver who will not fence for a stranger or a god who does not answer a
    /// follower of somebody else. Nothing a non-member could do with their own hands is closed by
    /// it - the beast can still be fought, the road still walked, the exposed man still taken in.
    ///
    /// **What it spends is the guild's willingness, and that is finite.** A hall that has turned a
    /// matter down will not be asked about it again by the same member, and a botched asking puts
    /// the matter out of that guild's hands for everybody. Nothing is written into vanilla: rank
    /// and contribution are read and never moved, because vanilla owns what a guild thinks of its
    /// members and a mod that quietly promoted people would be a second progression disagreeing
    /// with the one the player can see.
    ///
    /// What the guild's answer touches is the claim, not a body. It supersedes "somebody is not
    /// safe from that thing" and records that the guild took it on; it does not reach into Elin to
    /// remove a creature, and nothing here moves, schedules or dispatches an actor (`D021`).
    /// Whoever states such a claim owns whether a live Chara stands behind it.
    /// </summary>
    internal static class GuildAuthorityPolicy
    {
        /// <summary>Tag naming the guild that turned a matter down, on the officer who did.</summary>
        public const string DeclinedTag = "guild_declined:";

        /// <summary>Tag naming a guild that will not hear a matter again, from anybody.</summary>
        public const string ClosedTag = "guild_closed:";

        /// <summary>
        /// How much of a matter one rank is worth.
        ///
        /// Thread importance runs 0..100, so a hall will put its people on ordinary trouble for
        /// anybody who carries its card, and wants an officer of some standing before it commits
        /// them to something the whole district is about to feel. The scale of vanilla's own rank
        /// numbers has not been observed on a running game, which is recorded rather than guessed
        /// at: what the arithmetic must not do is grant the route on an unread number, and a rank
        /// that cannot be read is zero and refuses everything.
        /// </summary>
        public const int ImportancePerRank = 40;

        /// <summary>The rank a hall wants before it will commit its people to a matter this size.</summary>
        public static int RequiredRank(NarrativeThread thread)
        {
            int importance = thread == null || thread.Importance < 0 ? 0 : thread.Importance;
            return 1 + (importance / ImportancePerRank);
        }

        /// <summary>What people call the network, for a line the player reads.</summary>
        public static string Name(GuildId guild) => guild + " Guild";

        /// <summary>
        /// The matter a guild here would take on, or null.
        ///
        /// Preference is for a network the actor is actually in, so a member of one guild standing
        /// in a hall that houses two is never handed the refusal that belongs to the other. When
        /// nothing is available to a member, the first commission found is still returned, so the
        /// verb can refuse by name - "you have no standing in the Fighters Guild" is the answer
        /// this step exists to produce, and a silent absence would not be it.
        ///
        /// <paramref name="blocked"/> is filled only when everything that could have been asked
        /// has already been turned down, so the inspector can tell an exhausted hall from an
        /// uninterested one.
        /// </summary>
        public static GuildCommission Find(ActionContext context, out string blocked)
        {
            blocked = null;
            if (context.Thread == null)
            {
                return null;
            }

            List<EntityId> officers = Officers(context);
            if (officers.Count == 0)
            {
                return null;
            }

            GuildCommission first = null;
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                GuildCommission match = Match(context, officers, named, ref first, ref blocked);
                if (match != null)
                {
                    return match;
                }
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                GuildCommission match = Match(context, officers, fact, ref first, ref blocked);
                if (match != null)
                {
                    return match;
                }
            }

            if (first != null)
            {
                blocked = null;
            }

            return first;
        }

        /// <summary>
        /// Why this actor cannot ask for this commission, or null when they can.
        ///
        /// Three refusals, in the order the player would meet them: you are not in that guild, the
        /// guild wants somebody of more standing for a matter this size, and you have nothing to
        /// bring them. All three are preconditions rather than penalties - none of them is a hard
        /// attempt that might come off - and each says its own numbers so the inspector can show
        /// the arithmetic.
        /// </summary>
        public static string Shortfall(ActionContext context, GuildCommission commission)
        {
            if (!context.Vanilla.IsGuildMember(commission.Guild))
            {
                return "you have no standing in the " + Name(commission.Guild);
            }

            int rank = context.Vanilla.GetGuildRank(commission.Guild);
            int wanted = RequiredRank(context.Thread);
            if (rank < wanted)
            {
                return "the " + Name(commission.Guild) + " does not put its people on a matter this size"
                       + " for rank " + rank + "; that asks rank " + wanted;
            }

            return context.World.Knowledge.BelievesConfidently(context.Actor, commission.Matter.Id)
                ? null
                : "you have nothing to bring them: you do not believe it yourself";
        }

        /// <summary>Whether this guild will still hear this matter from this actor at all.</summary>
        public static bool AlreadyRefused(ActionContext context, EntityId officer, GuildId guild, EntityId matter)
        {
            foreach (WorldEvent past in context.World.Ledger.OfType(WorldEventType.RequestDeclined))
            {
                if (!Mentions(past.Related, matter))
                {
                    continue;
                }

                // A hall that threw the matter out is closed to everybody, which is what makes a
                // botched asking cost more than a failed one. An ordinary refusal is between the
                // officer who gave it and the member who asked.
                if (HasTag(past, ClosedTag + guild))
                {
                    return true;
                }

                if (past.Target == officer && past.Actor == context.Actor && HasTag(past, DeclinedTag + guild))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether anything in the thread is still a condition somebody could put right.
        ///
        /// The resolution test, and deliberately not "is this network still interested": the
        /// Fighters read a killing for ever, and a thread that could never close because somebody
        /// once died in it would be a situation with no ending.
        /// </summary>
        public static bool AnyStandingTroubleIn(NarrativeThread thread, KnowledgeGraph knowledge)
        {
            if (thread == null)
            {
                return false;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = knowledge.GetFact(thread.FactIds[i]);
                if (fact != null
                    && fact.Truth == TruthState.True
                    && FactPredicates.IsStandingTrouble(fact.Predicate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everybody here who speaks for a guild: the authority role says they can commit one, and
        /// a membership role says which.
        ///
        /// Both are needed and neither is invented for this verb - the same standing that decides
        /// what a guild does with an accusation, and the same membership that decides what it
        /// carries. A named target wins outright when they hold both, so a player who walked up to
        /// one officer is not answered by another standing behind them.
        /// </summary>
        private static List<EntityId> Officers(ActionContext context)
        {
            List<EntityId> officers = new List<EntityId>();
            if (Speaks(context, context.Target))
            {
                officers.Add(context.Target);
                return officers;
            }

            if (!context.Target.IsNone)
            {
                return officers;
            }

            IReadOnlyList<EntityId> present = context.Vanilla.GetCharactersInZone(context.Zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i] != context.Actor && Speaks(context, present[i]))
                {
                    officers.Add(present[i]);
                }
            }

            return officers;
        }

        private static bool Speaks(ActionContext context, EntityId who)
        {
            if (AuthorityPolicy.RoleOf(context, who) != AuthorityRole.Guild)
            {
                return false;
            }

            NarrativeNpc npc = context.World.Registry.GetNpc(who);
            for (int i = 0; i < GuildNetworks.All.Count; i++)
            {
                if (GuildNetworks.BelongsTo(npc, GuildNetworks.All[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The commission this fact makes for one of these officers, if any.
        ///
        /// Returns a match only when the actor is in the guild; anything else it finds is kept in
        /// <paramref name="first"/> so the caller can still refuse by name. Guild order inside an
        /// officer is <see cref="GuildNetworks.All"/>'s, so somebody who speaks for two networks
        /// answers with the same one every time.
        /// </summary>
        private static GuildCommission Match(
            ActionContext context, List<EntityId> officers, Fact fact, ref GuildCommission first, ref string blocked)
        {
            if (fact == null
                || fact.Truth != TruthState.True
                || !FactPredicates.IsStandingTrouble(fact.Predicate))
            {
                return null;
            }

            for (int i = 0; i < officers.Count; i++)
            {
                NarrativeNpc npc = context.World.Registry.GetNpc(officers[i]);
                for (int g = 0; g < GuildNetworks.All.Count; g++)
                {
                    GuildId guild = GuildNetworks.All[g];
                    if (!GuildNetworks.BelongsTo(npc, guild))
                    {
                        continue;
                    }

                    GuildFraming framing = GuildNetworks.Reads(context.World, guild, fact);
                    if (framing == GuildFraming.None)
                    {
                        continue;
                    }

                    if (AlreadyRefused(context, officers[i], guild, fact.Id))
                    {
                        blocked = blocked ?? ("the " + Name(guild) + " has already turned that down");
                        continue;
                    }

                    GuildCommission commission = new GuildCommission(officers[i], guild, framing, fact);
                    if (context.Vanilla.IsGuildMember(guild))
                    {
                        return commission;
                    }

                    first = first ?? commission;
                }
            }

            return null;
        }

        private static bool Mentions(IReadOnlyList<EntityId> ids, EntityId wanted)
        {
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                if (ids[i] == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTag(WorldEvent worldEvent, string tag)
        {
            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                if (worldEvent.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Committing your guild to a matter it already counts as its own.
    ///
    /// The route `BQ-038` is measured by, and the difference between two builds standing in the
    /// same hall with the same news is not that one rolls better. A member of the guild whose
    /// business this is has a way to end the matter; anybody else is told, by name, that they have
    /// no standing here - and still has every other route the situation offers, because what
    /// membership opened was a body of people, not a solution.
    ///
    /// Being taken on is not free and is not repeatable at leisure. A hall that says no will not
    /// hear the same matter from the same member again, and an asking that goes badly wrong puts
    /// it beyond that guild altogether - the same shape as a botched petition taking a matter out
    /// of a god's gift, and for the same reason: an institution's patience is a real resource and a
    /// route that could be tried until it worked would not be a decision.
    ///
    /// What ordinary success buys is the matter that was asked about. A critical asking commits
    /// enough of the hall to finish the job, so everything in the situation that this network reads
    /// is answered at once - which is a difference in what the guild does, not in what it feels.
    /// </summary>
    public sealed class InvokeGuildAuthorityAction : NarrativeAction
    {
        public InvokeGuildAuthorityAction() : base("invoke_authority", ActionFamily.Social, "Put it to the guild")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            // A build that cannot report standing loses the route rather than opening it to
            // everybody: the safe direction for an unread number, exactly as quality zero and an
            // unreadable deity are handled.
            if (!context.Vanilla.Supports(VanillaCapability.ReadGuildRank))
            {
                return Availability.Impossible("this build cannot report guild standing");
            }

            GuildCommission commission = GuildAuthorityPolicy.Find(context, out string blocked);
            if (commission == null)
            {
                return Availability.NotRelevant(blocked ?? "no guild here has business of its own in this");
            }

            string shortfall = GuildAuthorityPolicy.Shortfall(context, commission);
            return shortfall != null
                ? Availability.Impossible(shortfall)
                : Availability.Available("commits the " + GuildAuthorityPolicy.Name(commission.Guild) + " to "
                                         + ActionSupport.Describe(context, commission.Matter.Id));
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            // A projected choice can outlive the state it was drawn against: the officer may have
            // gone off duty, the matter may have been answered another way, and the hall may
            // already have refused it since the option was offered.
            string blocked = null;
            GuildCommission commission = context.Vanilla.Supports(VanillaCapability.ReadGuildRank)
                ? GuildAuthorityPolicy.Find(context, out blocked)
                : null;

            if (commission == null)
            {
                ActionOutcome nobody = new ActionOutcome(Id, null, blocked == null
                    ? "There is nobody here to put it to."
                    : "They have heard this from you already, and their answer has not changed.");
                nobody.Notes.Add(blocked ?? "no officer present who speaks for a guild with business of its own here");
                return nobody;
            }

            string shortfall = GuildAuthorityPolicy.Shortfall(context, commission);
            if (shortfall != null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "They hear you out and go back to what they were doing.");
                refused.Notes.Add(shortfall);
                return refused;
            }

            // The guild is told before it decides. Word reaching the hall is what the network is
            // for, and it is not conditional on the hall agreeing to act - a claim carried this
            // way is BQ-037's business and travels from here on its own.
            Inform(context, commission);

            CheckRequest request = new CheckRequest(
                    ProceduralCheckProfiles.GuildStanding, context.Actor, commission.Officer)
                .With(SituationalModifiers.GuildAuthority(context, commission.Guild))
                .With(SituationalModifiers.Rapport(context, commission.Officer));
            request.WithModifier("how much you are asking", context.Thread.Importance / 10);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            return check.Outcome.IsSuccess()
                ? TakenOn(context, commission, check)
                : TurnedDown(context, commission, check);
        }

        /// <summary>
        /// The guild takes it on, and the claim it was about stops being true.
        ///
        /// Coarse on purpose. What is recorded is that the matter was answered and who brought it;
        /// nothing here says where anybody stood, and nothing here touches a Chara.
        /// </summary>
        private ActionOutcome TakenOn(ActionContext context, GuildCommission commission, CheckResult check)
        {
            bool wholeHall = check.Outcome == CheckOutcome.CriticalPass;
            List<Fact> answered = new List<Fact> { commission.Matter };
            if (wholeHall)
            {
                Gather(context, commission, answered);
            }

            EntityId helped = ForWhom(context, commission.Matter);
            ActionOutcome outcome = new ActionOutcome(Id, check, wholeHall
                ? "The hall does not do it by halves. Everything of theirs in this is spoken for before you leave."
                : "The officer hears you out, writes it up as the guild's own, and puts people on it.");

            for (int i = 0; i < answered.Count; i++)
            {
                answered[i].Truth = TruthState.Superseded;
                outcome.Notes.Add("the " + GuildAuthorityPolicy.Name(commission.Guild) + " answers: "
                                  + ActionSupport.Describe(context, answered[i].Id));
                ActionSupport.CloseDemandsOn(context, answered[i].Subject, outcome);
            }

            outcome.Notes.Add("read as " + commission.Framing + " by the " + GuildAuthorityPolicy.Name(commission.Guild));
            outcome.Notes.Add("nobody is dispatched and no creature is removed; what ends is the claim");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                helped,
                context.Now,
                wholeHall ? 0.9 : 0.7,
                context.Zone,
                related: new[] { commission.Matter.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));

            Settle(context, outcome);
            return outcome;
        }

        /// <summary>
        /// The hall will not commit. An ordinary no is this officer's; a botched one is the
        /// guild's, and closes the matter to every officer in it.
        /// </summary>
        private ActionOutcome TurnedDown(ActionContext context, GuildCommission commission, CheckResult check)
        {
            bool closed = check.Outcome == CheckOutcome.CriticalFail;
            ActionOutcome outcome = new ActionOutcome(Id, check, closed
                ? "It goes badly. The matter is written off as none of the guild's business, and that is the end of it here."
                : "The officer hears it, and will not put anybody on it on your word.");

            outcome.Notes.Add(closed
                ? "the " + GuildAuthorityPolicy.Name(commission.Guild) + " will not hear this matter again from anybody"
                : context.NameOf(commission.Officer) + " will not hear this matter again from you");
            outcome.Notes.Add("the claim itself stands, and the guild has it now");
            outcome.Events.Add(context.World.Record(
                WorldEventType.RequestDeclined,
                context.Actor,
                commission.Officer,
                context.Now,
                0.2,
                context.Zone,
                related: new[] { commission.Matter.Id },
                tags: new[] { (closed ? GuildAuthorityPolicy.ClosedTag : GuildAuthorityPolicy.DeclinedTag) + commission.Guild },
                threadId: context.Thread?.Id ?? EntityId.None));

            return outcome;
        }

        /// <summary>
        /// Everything else in the situation this network reads as its own, for the asking that
        /// commits the whole hall.
        /// </summary>
        private static void Gather(ActionContext context, GuildCommission commission, List<Fact> answered)
        {
            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact == null
                    || fact.Id == commission.Matter.Id
                    || fact.Truth != TruthState.True
                    || !FactPredicates.IsStandingTrouble(fact.Predicate)
                    || GuildNetworks.Reads(context.World, commission.Guild, fact) == GuildFraming.None)
                {
                    continue;
                }

                answered.Add(fact);
            }
        }

        /// <summary>
        /// Tells the officer what the member brought, at the confidence the member holds it.
        ///
        /// Never with proof attached. A guild acts on its members' word, which is the whole reason
        /// this route exists where an accusation to a guard would rebound - and proof does not
        /// travel through a network, so a hall can be certain and still have nothing to show
        /// anybody.
        /// </summary>
        private static void Inform(ActionContext context, GuildCommission commission)
        {
            context.World.Knowledge.TryGetBelief(context.Actor, commission.Matter.Id, out KnowledgeRecord held);
            context.World.Knowledge.Teach(
                commission.Officer,
                commission.Matter.Id,
                KnowledgeSource.Hearsay,
                held == null ? 0.6 : held.Confidence,
                context.Now,
                false,
                null,
                context.Actor);
        }

        /// <summary>Whoever the trouble was about: the person in it, or whoever holds the thing.</summary>
        private static EntityId ForWhom(ActionContext context, Fact matter)
        {
            if (context.World.Registry.GetNpc(matter.Subject) != null)
            {
                return matter.Subject;
            }

            EntityId owner = Ownership.OwnerOf(context, matter.Subject);
            return owner.IsNone ? context.Target : owner;
        }

        /// <summary>The situation ends when nothing in it is a live condition any more.</summary>
        private static void Settle(ActionContext context, ActionOutcome outcome)
        {
            if (GuildAuthorityPolicy.AnyStandingTroubleIn(context.Thread, context.World.Knowledge))
            {
                return;
            }

            ActionSupport.Resolve(context, outcome, "guild_answered", 0.7);
        }
    }
}
