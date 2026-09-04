using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// One named write of authoritative or actor-local state, applied to a run after its preset.
    ///
    /// The unit the sweep is built out of, and deliberately the same kind of thing a preset is: a
    /// statement about the world, made through the store that owns it. What separates an input
    /// from a preset is only that a sweep composes several of them and reports which one it moved,
    /// so a row can say <em>exactly</em> what changed rather than "a different preset".
    ///
    /// <b>An input can only ever be an input.</b> Every factory below writes through
    /// <see cref="PlaygroundState"/>, the relationship graph, the knowledge graph, the obligation
    /// ledger, the event ledger or a character's own profiles. None of them can reach a strategy,
    /// a depth, a tactic, a speech act, a permit, a fragment or a line, because nothing on
    /// <see cref="PlaygroundStage"/> exposes one - which is the same structural guarantee the
    /// presets already rely on, inherited rather than restated.
    /// </summary>
    public sealed class PlaygroundInput
    {
        private readonly Action<PlaygroundStage, EntityId, EntityId> _write;

        public PlaygroundInput(string because, Action<PlaygroundStage, EntityId, EntityId> write)
        {
            if (string.IsNullOrWhiteSpace(because))
            {
                throw new ArgumentException("An input has to say what it changed.", nameof(because));
            }

            Because = because;
            _write = write ?? throw new ArgumentNullException(nameof(write));
        }

        /// <summary>What this changed, in the words the row's input-difference column prints.</summary>
        public string Because { get; }

        /// <summary>Applies the write. The two ids are the run's resolved speaker and listener.</summary>
        public void Apply(PlaygroundStage stage, EntityId speaker, EntityId listener)
        {
            _write(stage, speaker, listener);
        }
    }

    /// <summary>
    /// The writes the sweep families are assembled from.
    ///
    /// One factory per piece of state the model already holds, named for the state rather than for
    /// the effect it is hoped to have: <see cref="Tie"/> and not "make them hostile",
    /// <see cref="Believes"/> and not "make them sure". A factory named for an effect would be an
    /// outcome smuggled in as an input, and a row whose input column read "make them refuse" would
    /// be measuring nothing.
    /// </summary>
    public static class PlaygroundInputs
    {
        /// <summary>The speaker's directed tie to the listener, replacing whatever stood there.</summary>
        public static PlaygroundInput Tie(RelationKind kind, int sentiment)
        {
            return new PlaygroundInput(
                "tie speaker->listener = " + kind + " at sentiment " + sentiment,
                (stage, speaker, listener) => PlaygroundState.Tie(stage, speaker, listener, kind, sentiment));
        }

        /// <summary>A directed tie to somebody else on the stage, by role.</summary>
        public static PlaygroundInput TieTo(string role, RelationKind kind, int sentiment, bool mutual = false)
        {
            return new PlaygroundInput(
                "tie speaker->" + role + " = " + kind + " at sentiment " + sentiment,
                (stage, speaker, listener) =>
                {
                    EntityId other = stage.Resolve(role);
                    if (!other.IsNone && other != speaker)
                    {
                        PlaygroundState.Tie(stage, speaker, other, kind, sentiment, mutual);
                    }
                });
        }

        /// <summary>
        /// A belief the speaker did not already hold, by a named route.
        ///
        /// <see cref="KnowledgeGraph.Teach"/>'s own rule stands: somebody who already believes the
        /// claim is strengthened rather than re-sourced. A knowledge sweep therefore runs over a
        /// speaker who starts with no belief at all, and the row reports the record the graph
        /// actually holds afterwards rather than the one that was asked for.
        /// </summary>
        public static PlaygroundInput Believes(KnowledgeSource source, double confidence, bool canProve = false)
        {
            return new PlaygroundInput(
                "belief = " + source + " at " + confidence.ToString("0.00") + (canProve ? ", provable" : string.Empty),
                (stage, speaker, listener) => PlaygroundState.Believes(
                    stage, speaker, stage.SubjectFactId, source, confidence, canProve, listener));
        }

        /// <summary>How hidden the claim is. Read by BQ-071 as the privacy pressure.</summary>
        public static PlaygroundInput Secrecy(int secrecy)
        {
            return new PlaygroundInput(
                "claim secrecy = " + secrecy,
                (stage, speaker, listener) =>
                {
                    Fact fact = stage.Subject;
                    if (fact != null)
                    {
                        fact.Secrecy = secrecy;
                    }
                });
        }

        /// <summary>
        /// Whether the claim is actually so.
        ///
        /// An input rather than an outcome, and the one the veracity sweep turns on: BQ-073 decides
        /// sincerity against what the speaker believes and reports world truth without using it, so
        /// moving this must move the report and nothing else.
        /// </summary>
        public static PlaygroundInput Truth(TruthState truth)
        {
            return new PlaygroundInput(
                "claim truth = " + truth,
                (stage, speaker, listener) =>
                {
                    Fact fact = stage.Subject;
                    if (fact != null)
                    {
                        fact.Truth = truth;
                    }
                });
        }

        /// <summary>One personality weight on the speaker, by name, so a row can say which axis moved.</summary>
        public static PlaygroundInput Personality(string axis, double value)
        {
            return new PlaygroundInput(
                "personality." + axis + " = " + value.ToString("0.00"),
                (stage, speaker, listener) => Weigh(stage.Npc(speaker), axis, value));
        }

        /// <summary>Present affect on the speaker, set at the current time.</summary>
        public static PlaygroundInput Feels(EmotionalState emotion, double intensity)
        {
            return new PlaygroundInput(
                "emotion." + emotion + " = " + intensity.ToString("0.00"),
                (stage, speaker, listener) => PlaygroundState.Feels(stage, speaker, emotion, intensity));
        }

        /// <summary>One of BQ-077's lines, declared onto the speaker.</summary>
        public static PlaygroundInput Line(PersonalProhibition kind, double firmness, bool breakable)
        {
            return new PlaygroundInput(
                "line " + kind + " at firmness " + firmness.ToString("0.00")
                + (breakable ? ", breakable" : ", unbreakable"),
                (stage, speaker, listener) => PlaygroundState.Line(stage, speaker, kind, firmness, breakable));
        }

        /// <summary>An obligation between the two people talking - the record half of standing.</summary>
        public static PlaygroundInput Owes(SocialObligationKind kind, string purpose)
        {
            return new PlaygroundInput(
                "obligation: speaker owes listener " + kind,
                (stage, speaker, listener) => PlaygroundState.Owes(
                    stage, kind, speaker, listener, stage.SubjectFactId, purpose));
        }

        /// <summary>
        /// The identity facets the sandbox reports for the speaker, replacing whatever it held.
        ///
        /// Nothing here interprets them: BQ-145 reads the facets and decides which domains they
        /// make plausible, and an id it does not recognise stays unrecognised rather than being
        /// mapped onto the nearest familiar trade. An empty build is a legitimate row - it is the
        /// unread actor, and what it proves is that nothing is guessed for one.
        /// </summary>
        public static PlaygroundInput Identity(string label, Action<CharacterIdentityBuilder> build)
        {
            return new PlaygroundInput(
                "identity facets = " + label,
                (stage, speaker, listener) =>
                {
                    CharacterIdentityBuilder builder = new CharacterIdentityBuilder(speaker);
                    build?.Invoke(builder);
                    stage.Vanilla.SetCharacterIdentity(speaker, builder.Build());
                });
        }

        /// <summary>
        /// History made the way the world makes history: a production action, resolved to a
        /// scripted outcome, followed by enough days for the material to settle.
        /// </summary>
        public static PlaygroundInput History(string actionId, string targetRole, long days)
        {
            return new PlaygroundInput(
                "history: the player " + actionId + "s the " + targetRole + ", then " + days + " days pass",
                (stage, speaker, listener) =>
                {
                    EntityId target = stage.Resolve(targetRole);
                    if (!target.IsNone)
                    {
                        PlaygroundState.Act(stage, actionId, target, BrilliantQuesting.Checks.CheckOutcome.Pass);
                    }

                    PlaygroundState.Wait(stage, days);
                });
        }

        /// <summary>
        /// One recorded event, somewhere else, and the days for it to settle.
        ///
        /// The one input that writes to the ledger directly rather than through an action, and it
        /// is here because BQ-082's recurrence gate turns on <em>where</em> something happened: the
        /// laboratory's action layer only ever acts inside this situation's own thread and zone, so
        /// history in a second context cannot be made any other way. It is still only history - a
        /// recorded event with an actor, a target, a place and a time - and every reading of it,
        /// including whether this speaker has a route to it at all, stays BQ-081's.
        /// </summary>
        public static PlaygroundInput Elsewhere(
            WorldEventType type,
            string actorRole,
            string targetRole,
            bool witnessedBySpeaker,
            bool sameContext,
            long days)
        {
            return new PlaygroundInput(
                "history: " + type + " by the " + actorRole + " upon the " + targetRole
                + (witnessedBySpeaker ? ", speaker on the witness list" : string.Empty)
                + (sameContext ? ", in this thread and zone" : ", in another zone and no thread")
                + ", then " + days + " days pass",
                (stage, speaker, listener) =>
                {
                    EntityId actor = stage.Resolve(actorRole);
                    EntityId target = stage.Resolve(targetRole);
                    List<EntityId> witnesses = new List<EntityId>();
                    if (witnessedBySpeaker)
                    {
                        witnesses.Add(speaker);
                    }

                    stage.World.Record(
                        type,
                        actor,
                        target,
                        stage.Now,
                        0.6,
                        sameContext ? stage.Zone : stage.World.NewId("zone"),
                        null,
                        witnesses,
                        null,
                        null,
                        sameContext ? (stage.Situation.Thread?.Id ?? EntityId.None) : EntityId.None);

                    PlaygroundState.Wait(stage, days);
                });
        }

        /// <summary>Days passing, and every live thread catching up, before anybody is asked anything.</summary>
        public static PlaygroundInput Wait(long days)
        {
            return new PlaygroundInput(
                days + " day(s) pass",
                (stage, speaker, listener) => PlaygroundState.Wait(stage, days));
        }

        /// <summary>
        /// The personality axes a row may name.
        ///
        /// A closed switch rather than reflection, so a sweep cannot quietly start moving an axis
        /// nobody looked at, and so a mistyped axis is a build error rather than a row that
        /// silently changed nothing.
        /// </summary>
        private static void Weigh(NarrativeNpc npc, string axis, double value)
        {
            if (npc == null)
            {
                return;
            }

            switch (axis)
            {
                case "honesty":
                    npc.Personality.Honesty = value;
                    return;
                case "trust":
                    npc.Personality.Trust = value;
                    return;
                case "loyalty":
                    npc.Personality.Loyalty = value;
                    return;
                case "warmth":
                    npc.Personality.Warmth = value;
                    return;
                case "orderliness":
                    npc.Personality.Orderliness = value;
                    return;
                default:
                    throw new ArgumentException("The sweep does not sweep personality." + axis + ".", nameof(axis));
            }
        }
    }
}
