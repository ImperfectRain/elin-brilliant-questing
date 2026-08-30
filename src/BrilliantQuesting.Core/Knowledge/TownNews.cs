using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// "What's been happening?" - the asked half of <see cref="AmbientTalk"/>.
    ///
    /// Standing in a market and waiting is one way to find out that something is going on. This is
    /// the other: the player picks somebody and asks, and gets the handful of developments that
    /// person has actually heard about, the loudest first. Two people in the same town answer differently
    /// because they were told different things, which is the whole reason knowledge is held per
    /// character rather than as a flag on the world (`PM §37`, `LW §3.3`).
    ///
    /// What separates it from <see cref="Actions.Library.QuestionAction"/> - the other verb that
    /// gets somebody talking - is what it can reach and what it costs. This asks for the news,
    /// which anybody will give for free and which is by construction the town's gossip:
    /// <see cref="TalkRepertoire"/> only ever hands out what a speaker was told themselves. What
    /// somebody saw, did or read is testimony, and testimony is asked for by name, against a
    /// check, with a critical failure that tells the subject somebody is asking. Keeping the free
    /// topic on the gossip side is what stops it becoming the cheap route to the evidence the
    /// investigation verbs exist to earn.
    ///
    /// Three consequences of that fall out rather than being arranged:
    ///
    /// - it needs no check, so it has no four-outcome shape to preserve - nobody is being
    ///   persuaded, pressed or deceived, and there is nothing here to fail at;
    /// - it is self-limiting, because a claim the listener already holds is not news, so asking
    ///   the same person twice gets whatever the town has learned since and otherwise nothing;
    /// - it writes no event of its own. The retelling <see cref="RumorSystem.Tell"/> records is
    ///   the history that happened; that the player asked for it rather than overhearing it
    ///   changes nothing about anybody.
    /// </summary>
    public sealed class TownNews
    {
        private readonly RumorSystem _rumors;
        private readonly TalkRepertoire _repertoire;

        public TownNews(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
            _repertoire = new TalkRepertoire(_rumors);
        }

        /// <summary>
        /// How much somebody will run through in one answer. Three developments is a person
        /// catching you up, not a briefing.
        /// </summary>
        public int MostAtOnce { get; set; } = 3;

        /// <summary>
        /// Confidence a speaker needs before passing something on when they have been asked
        /// directly. Below <see cref="AmbientTalk.SpeakerFloor"/> on purpose: a person will
        /// mention a half-remembered thing to somebody who asked and would not have brought it up
        /// unprompted. In practice <see cref="RumorSystem.CanTell"/> binds first - it refuses a
        /// retelling that would arrive below the gossip floor, which puts the effective floor a
        /// little above this one - and that is the right shape: how weak a claim can be and still
        /// travel belongs to the rumour layer rather than to this knob.
        /// </summary>
        public double SpeakerFloor { get; set; } = 0.2;

        /// <summary>
        /// The same ceiling the street has. Being asked does not make somebody discreet about a
        /// matter people are keeping quiet; that stays behind the verbs that cost something.
        /// </summary>
        public int SecrecyCeiling { get; set; } = 60;

        /// <summary>
        /// What this person would tell the player about, best news first. Empty when they have
        /// nothing the player does not already have, which is the only honest reason to leave the
        /// topic off a conversation.
        ///
        /// Pure, like <see cref="AmbientTalk.Next"/> and for the same reason: reading somebody's
        /// answer is not the same as their having given it. Nothing is learned until
        /// <see cref="Deliver"/> is called for a line the player actually saw.
        /// </summary>
        public IReadOnlyList<SpokenRemark> Ask(NarrativeWorldState world, IVanillaState vanilla, EntityId speaker)
        {
            if (world == null || vanilla == null)
            {
                return new List<SpokenRemark>();
            }

            EntityId player = vanilla.PlayerId;
            if (player.IsNone || !vanilla.IsAlive(player))
            {
                return new List<SpokenRemark>();
            }

            return _repertoire.Of(
                world,
                vanilla,
                speaker,
                player,
                new TalkRules(SpeakerFloor, SecrecyCeiling),
                Math.Max(0, MostAtOnce));
        }

        /// <summary>
        /// One line of the answer reached the player. Returns whether they are left believing it.
        ///
        /// No cooldown is spent and none is checked. Asking is something the player chose to do,
        /// so there is nothing to pace: the street's cooldown exists because remarks arrive
        /// without being asked for, and spending it here would let a conversation silence the town
        /// - or worse, let a quiet street silence the person standing in front of the player.
        /// </summary>
        public bool Deliver(NarrativeWorldState world, IVanillaState vanilla, SpokenRemark remark, GameTime now)
        {
            if (world == null || vanilla == null || remark == null)
            {
                return false;
            }

            return _rumors.Tell(remark.Speaker, vanilla.PlayerId, remark.FactId, now);
        }
    }
}
