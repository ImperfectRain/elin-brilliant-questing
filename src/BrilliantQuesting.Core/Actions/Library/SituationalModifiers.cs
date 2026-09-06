using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Memory;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Turns standing world state into difficulty. These are the terms that make the same verb
    /// feel different depending on who you are and what you have already done - and because each
    /// carries a label, the debug inspector can show the player's history as arithmetic.
    ///
    /// Four of them read standing Elin keeps for the player and for nobody else: affinity is
    /// affinity *toward the player*, and Karma, Fame and a guild card are the player's own. Asked
    /// during an NPC's attempt they would not return a smaller number, they would return the
    /// player's - so an NPC would be easy to talk round exactly when the player is famous. Each
    /// therefore contributes nothing for a non-player actor and says so by name, which is the
    /// shape <see cref="Settlement"/> already used for a Home element this build never answered
    /// (BQ-093, `D017`).
    /// </summary>
    public static class SituationalModifiers
    {
        /// <summary>Someone who likes you is easier to talk round. Roughly one point of DC per 12 affinity.</summary>
        public static CheckRequestExtensions.Modifier Rapport(ActionContext context)
        {
            return Rapport(context, context.Target);
        }

        /// <summary>
        /// The same term against somebody who is not the named target.
        ///
        /// A verb that finds the person it deals with - the receiver in the alley, the officer who
        /// speaks for the hall - is dealing with them whether or not the caller named them, and
        /// their goodwill counts for exactly what a named target's would.
        /// </summary>
        public static CheckRequestExtensions.Modifier Rapport(ActionContext context, EntityId who)
        {
            if (!context.TryGetAffinityToActor(who, out int affinity))
            {
                return CheckRequestExtensions.Modifier.Unreadable("rapport unread: vanilla keeps goodwill toward the player only");
            }

            return new CheckRequestExtensions.Modifier("rapport", -(affinity / 12));
        }

        /// <summary>
        /// Fame cuts both ways. A known name makes a threat land harder and a lie land worse,
        /// because everyone already has an opinion about who you are.
        /// </summary>
        public static CheckRequestExtensions.Modifier Reputation(ActionContext context, bool helpfulWhenFamous)
        {
            if (!context.ActorIsPlayer)
            {
                return CheckRequestExtensions.Modifier.Unreadable("fame unread: vanilla keeps Fame for the player only");
            }

            int band = context.Vanilla.Fame / 500;
            if (band == 0)
            {
                return new CheckRequestExtensions.Modifier("fame", 0);
            }

            int delta = helpfulWhenFamous ? -band : band;
            return new CheckRequestExtensions.Modifier("fame", Clamp(delta, -4, 4));
        }

        /// <summary>
        /// Karma is legal standing, not morality. A wanted criminal is harder to believe and
        /// harder to cooperate with - and easier to be frightened of.
        /// </summary>
        public static CheckRequestExtensions.Modifier LegalStanding(ActionContext context, bool helpfulWhenNotorious)
        {
            if (!context.ActorIsPlayer)
            {
                return CheckRequestExtensions.Modifier.Unreadable("karma unread: vanilla keeps Karma for the player only");
            }

            int karma = context.Vanilla.Karma;
            if (karma >= 0)
            {
                return new CheckRequestExtensions.Modifier("karma", 0);
            }

            int magnitude = Clamp(-karma / 25, 0, 4);
            return new CheckRequestExtensions.Modifier("criminal record", helpfulWhenNotorious ? -magnitude : magnitude);
        }

        /// <summary>
        /// The target's own history with you. Someone who remembers being robbed by you does not
        /// need a stat check to be suspicious - they have a reason.
        /// </summary>
        public static CheckRequestExtensions.Modifier Grudge(ActionContext context)
        {
            if (context.Target.IsNone)
            {
                return new CheckRequestExtensions.Modifier("history", 0);
            }

            int delta = 0;
            foreach (MemoryRecord memory in context.World.Memories.MemoriesAbout(context.Target, context.Actor))
            {
                if (memory.Weight >= MemoryWeight.Important && memory.AffinityContribution < 0)
                {
                    delta += 2;
                }
            }

            return new CheckRequestExtensions.Modifier("bad history", Clamp(delta, 0, 6));
        }

        /// <summary>
        /// Transient affect changes how hard a person is to draw out, but never decides what is true.
        /// </summary>
        public static CheckRequestExtensions.Modifier DisclosureMood(ActionContext context)
        {
            NarrativeNpc npc = context.TargetNpc;
            if (npc == null)
            {
                return new CheckRequestExtensions.Modifier("emotional state", 0);
            }

            EmotionalStateProfile emotions = npc.Emotions;
            int guarded = Scale(emotions.Get(EmotionalState.Anger, context.Now), 6)
                          + Scale(emotions.Get(EmotionalState.Suspicion, context.Now), 4)
                          + Scale(emotions.Get(EmotionalState.Stress, context.Now), 3)
                          + Scale(emotions.Get(EmotionalState.Shame, context.Now), 2)
                          + Scale(emotions.Get(EmotionalState.Fear, context.Now), 2)
                          + Scale(emotions.Get(EmotionalState.Grief, context.Now), 1);
            int softened = Scale(emotions.Get(EmotionalState.Affection, context.Now), 3)
                           + Scale(emotions.Get(EmotionalState.Relief, context.Now), 2);
            return new CheckRequestExtensions.Modifier("emotional state", Clamp(guarded - softened, -4, 8));
        }

        /// <summary>
        /// One of the settlement's own Home Skill elements, as difficulty.
        ///
        /// A well-run, well-fed, well-watched place makes the work it is being asked for easier,
        /// and the number is Elin's own - the mod reads what the player watches on the Home board
        /// rather than scoring settlements privately. An element this build never answered
        /// contributes nothing and says so by name, so the inspector distinguishes "the Home is
        /// mediocre at this" from "nobody could read it" (decision D017).
        /// </summary>
        public static CheckRequestExtensions.Modifier Settlement(HomeState home, HomeMetric metric)
        {
            string label = metric.ToString().ToLowerInvariant();
            if (home == null || !home.TryGetMetric(metric, out int value))
            {
                return CheckRequestExtensions.Modifier.Unreadable("home " + label + " unread");
            }

            return new CheckRequestExtensions.Modifier("home " + label, -Clamp(value / 20, -4, 4));
        }

        /// <summary>
        /// Guild standing as social authority, where the guild is relevant to the ask.
        ///
        /// Both halves of what the game keeps about a member: the rank they hold and what they
        /// have put in to hold it. Rank is what the guild calls them and contribution is what it
        /// owes them, and a member who has done a great deal for it is listened to further than
        /// their title alone would carry - which is why the two are added rather than one standing
        /// in for the other.
        /// </summary>
        public static CheckRequestExtensions.Modifier GuildAuthority(ActionContext context, GuildId guild)
        {
            if (!context.ActorIsPlayer)
            {
                return CheckRequestExtensions.Modifier.Unreadable(
                    guild + " standing unread: vanilla keeps a guild card for the player only");
            }

            int rank = context.Vanilla.GetGuildRank(guild);
            int contribution = context.Vanilla.GetGuildContribution(guild);
            return new CheckRequestExtensions.Modifier(
                guild + " standing", -(Clamp(rank, 0, 5) + Clamp(contribution / 25, 0, 4)));
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static int Scale(double value, int max)
        {
            return (int)System.Math.Round(value * max);
        }
    }

    /// <summary>Small helper so modifier producers can be composed fluently onto a request.</summary>
    public static class CheckRequestExtensions
    {
        public readonly struct Modifier
        {
            public Modifier(string label, int delta)
                : this(label, delta, false)
            {
            }

            private Modifier(string label, int delta, bool unread)
            {
                Label = label;
                Delta = delta;
                Unread = unread;
            }

            public string Label { get; }

            public int Delta { get; }

            /// <summary>
            /// Whether this term contributes nothing because nobody could read the number, as
            /// opposed to because the number was zero.
            ///
            /// The difference is the whole of `D017` and it has to survive as far as the
            /// inspector: an ordinary zero is dropped, because a check listing every term that
            /// happened not to matter is unreadable, and an unread one is kept, because a reader
            /// asking why an NPC's threat landed the way it did needs to see that the actor's
            /// notoriety was never consulted rather than assume it was consulted and found
            /// wanting.
            /// </summary>
            public bool Unread { get; }

            /// <summary>A term nobody could read: no contribution, and named so in the trace.</summary>
            public static Modifier Unreadable(string label) => new Modifier(label, 0, true);
        }

        public static Checks.CheckRequest With(this Checks.CheckRequest request, Modifier modifier)
        {
            if (modifier.Unread)
            {
                return request.WithUnreadTerm(modifier.Label);
            }

            return request.WithModifier(modifier.Label, modifier.Delta);
        }
    }
}
