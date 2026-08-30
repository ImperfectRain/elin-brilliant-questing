using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// One thing somebody is about to say out loud, and what the listener is left believing if
    /// they do.
    ///
    /// It carries no tag, no confidence bar and no thread name, because it is a line of speech
    /// rather than a report. What the player makes of it is what the journal is for.
    /// </summary>
    public sealed class SpokenRemark
    {
        internal SpokenRemark(EntityId speaker, string speakerName, EntityId factId, string line, double salience)
        {
            Speaker = speaker;
            SpeakerName = speakerName;
            FactId = factId;
            Line = line;
            Salience = salience;
        }

        /// <summary>Who says it. Never the listener, and never the person the claim is about.</summary>
        public EntityId Speaker { get; }

        /// <summary>Their name, so a presentation layer that has lost the binding can still attribute it.</summary>
        public string SpeakerName { get; }

        /// <summary>What they are talking about. The listener does not hold it yet.</summary>
        public EntityId FactId { get; }

        /// <summary>The words, hedged to match how sure the speaker actually is.</summary>
        public string Line { get; }

        /// <summary>
        /// How much news is in it, as the speaker's own world scores it. Internal because it is
        /// the ordering, not something the player is ever shown a number for.
        /// </summary>
        internal double Salience { get; }
    }

    /// <summary>
    /// How forthcoming somebody is being.
    ///
    /// Two knobs, and they are what separates the ways a claim can reach a listener without
    /// anybody rolling for it: what falls out of walking past a conversation is a higher bar than
    /// what a person will say when they are actually asked. Everything else about the two routes
    /// is the same, and is <see cref="TalkRepertoire"/>'s.
    /// </summary>
    public readonly struct TalkRules
    {
        public TalkRules(double speakerFloor, int secrecyCeiling)
        {
            SpeakerFloor = speakerFloor;
            SecrecyCeiling = secrecyCeiling;
        }

        /// <summary>Confidence a speaker needs in a claim before they will put it into words.</summary>
        public double SpeakerFloor { get; }

        /// <summary>Above this secrecy, it is not something said where it can be repeated.</summary>
        public int SecrecyCeiling { get; }
    }

    /// <summary>
    /// What one person would tell another right now, in the order they would bring it up.
    ///
    /// This is the one place that decides what somebody is willing to say, so that overhearing a
    /// remark in the market (<see cref="AmbientTalk"/>) and asking somebody what has been going on
    /// (<see cref="TownNews"/>) are the same act at different volumes rather than two systems that
    /// have to be kept in agreement by hand.
    ///
    /// The rules it holds:
    ///
    /// **They may only repeat what they were told.** First-hand knowledge is testimony, and
    /// testimony is asked for at the cost of a check - that is what the `question` and `eavesdrop`
    /// verbs are. A witness who watched the theft says nothing about it either in the street or
    /// when a stranger asks what has been happening; the neighbour who heard about it secondhand
    /// is the one who mentions it. Without that line, free talk would quietly become the cheapest
    /// route to the evidence the investigation verbs exist to earn.
    ///
    /// **Never about themselves.** <see cref="RumorCirculation"/>'s subject clause, for
    /// circulation's reason: the one person who can never be relied on to bring a matter up is
    /// the person it is about.
    ///
    /// **Only what would take.** <see cref="RumorSystem.CanTell"/> is asked before the words are
    /// chosen, so a deterministic picker cannot fix on a retelling that will never land and starve
    /// everything behind it. It is also the real floor under <see cref="TalkRules.SpeakerFloor"/>:
    /// a retelling that would arrive below the gossip floor is not said at all.
    ///
    /// **Nothing is learned by being read.** Building the repertoire touches nothing. Whoever
    /// renders a remark decides whether it reached the listener, and only then may it teach them
    /// anything - a belief that arrived because a line failed to render is the omniscient journal
    /// standing rule 22 forbids. What the listener ends up with then is hearsay at the speaker's
    /// own conviction minus the usual cost of a retelling, with no proof attached: a lead rather
    /// than a case.
    /// </summary>
    public sealed class TalkRepertoire
    {
        private readonly RumorSystem _rumors;

        public TalkRepertoire(RumorSystem rumors)
        {
            _rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
        }

        /// <summary>
        /// The best <paramref name="limit"/> things this speaker would say to this listener, most
        /// newsworthy first. Empty when they have nothing to offer, which is the only honest
        /// reason to hide a way of asking.
        /// </summary>
        public List<SpokenRemark> Of(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId speaker,
            EntityId listener,
            TalkRules rules,
            int limit)
        {
            List<SpokenRemark> remarks = new List<SpokenRemark>();
            if (world == null || vanilla == null || limit <= 0 || !CanSpeak(world, vanilla, speaker, listener))
            {
                return remarks;
            }

            List<Candidate> candidates = new List<Candidate>();
            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(speaker))
            {
                Fact fact = world.Knowledge.GetFact(belief.FactId);
                if (IsWorthMentioning(world, speaker, listener, fact, belief, rules))
                {
                    candidates.Add(new Candidate(fact, belief, Score(world, fact, belief)));
                }
            }

            candidates.Sort(Candidate.Compare);

            // Wording happens last, for the few that are actually said. The ambient route asks
            // every person in the zone for their best line on every check, and phrasing a whole
            // town's beliefs to throw all but one away is the sort of cost that only shows up in
            // a long save.
            string speakerName = world.Registry.NameOf(speaker);
            int said = candidates.Count < limit ? candidates.Count : limit;
            for (int i = 0; i < said; i++)
            {
                remarks.Add(new SpokenRemark(
                    speaker,
                    speakerName,
                    candidates[i].Fact.Id,
                    Words(world, candidates[i].Fact, candidates[i].Belief),
                    candidates[i].Salience));
            }

            return remarks;
        }

        /// <summary>One thing the speaker could bring up, before anybody has put it into words.</summary>
        private readonly struct Candidate
        {
            internal Candidate(Fact fact, KnowledgeRecord belief, double salience)
            {
                Fact = fact;
                Belief = belief;
                Salience = salience;
            }

            internal Fact Fact { get; }

            internal KnowledgeRecord Belief { get; }

            internal double Salience { get; }

            /// <summary>
            /// Most news first, ties broken on fact id so the same world always produces the same
            /// answer. Enumeration order over a dictionary is not something a save may depend on.
            /// </summary>
            internal static int Compare(Candidate a, Candidate b)
            {
                if (a.Salience != b.Salience)
                {
                    return a.Salience > b.Salience ? -1 : 1;
                }

                return string.CompareOrdinal(a.Fact.Id.Value, b.Fact.Id.Value);
            }
        }

        /// <summary>
        /// Somebody who could say something: alive, known to the simulation, and not the person
        /// being spoken to.
        ///
        /// No mutation policy is consulted, and none applies. Speech is the one thing every class
        /// of actor permits by construction (<see cref="MutationKind.Speech"/> is the bottom rung),
        /// because putting a line in somebody's mouth changes nothing about them - it does not move
        /// their affinity, their inventory or where they stand. What it changes is what the
        /// listener knows, and that is guarded here rather than there.
        /// </summary>
        private static bool CanSpeak(NarrativeWorldState world, IVanillaState vanilla, EntityId speaker, EntityId listener)
        {
            return !speaker.IsNone
                   && speaker != listener
                   && world.Registry.GetNpc(speaker) != null
                   && vanilla.IsAlive(speaker);
        }

        /// <summary>
        /// Whether this belief is something that speaker would say to this listener.
        ///
        /// The hearsay and subject clauses are the class rules above. The rest is ordinary - it
        /// has to be news, they have to believe it firmly enough to repeat, it must not be
        /// something being actively kept quiet, and it must be something the listener does not
        /// already hold.
        /// </summary>
        private bool IsWorthMentioning(
            NarrativeWorldState world,
            EntityId speaker,
            EntityId listener,
            Fact fact,
            KnowledgeRecord belief,
            TalkRules rules)
        {
            return fact != null
                   && belief.Source == KnowledgeSource.Hearsay
                   && fact.Subject != speaker
                   && fact.Truth != TruthState.Superseded
                   && FactPredicates.IsNewsworthy(fact.Predicate)
                   && fact.Secrecy <= rules.SecrecyCeiling
                   && belief.Confidence >= rules.SpeakerFloor
                   && !world.Knowledge.Knows(listener, fact.Id)
                   && _rumors.CanTell(speaker, listener, fact.Id);
        }

        /// <summary>
        /// What people bring up first: how sure they are, whether it is part of something still
        /// going on, and how far it is from the sort of thing said in the open.
        /// </summary>
        private static double Score(NarrativeWorldState world, Fact fact, KnowledgeRecord belief)
        {
            return belief.Confidence
                   + (BelongsToLiveThread(world, fact.Id) ? 1.0 : 0.0)
                   - fact.Secrecy / 100.0;
        }

        /// <summary>
        /// Whether one person's best line beats another's, on the same order the repertoire is
        /// sorted by. Used where several people are in earshot and only one of them speaks.
        /// </summary>
        internal static bool Beats(SpokenRemark candidate, SpokenRemark best)
        {
            return best == null
                   || candidate.Salience > best.Salience
                   || (candidate.Salience == best.Salience
                       && string.CompareOrdinal(candidate.FactId.Value, best.FactId.Value) < 0);
        }

        /// <summary>
        /// The claim, hedged the way a person hedges rather than scored the way a database scores.
        ///
        /// The speaker's confidence is in here and it is not printed. Somebody near the start of
        /// the chain repeats it as settled; somebody at the end of a long one says the thing people
        /// say when they are passing on something they only half have. That wording is the
        /// listener's only handle on how good the lead is, which is the point of `LW §3.1`.
        /// </summary>
        private static string Words(NarrativeWorldState world, Fact fact, KnowledgeRecord belief)
        {
            string claim = FactPhrasing.Claim(world.Registry, fact);

            if (belief.Confidence >= 0.7)
            {
                return "Everyone knows it by now: " + claim + ".";
            }

            if (belief.Confidence >= 0.5)
            {
                return "Word going round is " + claim + ".";
            }

            return "Somebody was saying " + claim + ". Make of that what you like.";
        }

        private static bool BelongsToLiveThread(NarrativeWorldState world, EntityId factId)
        {
            for (int i = 0; i < world.Threads.Count; i++)
            {
                if (world.Threads[i].IsLive && world.Threads[i].FactIds.Contains(factId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
