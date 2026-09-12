using System;

namespace BrilliantQuesting.Foundation
{
    /// <summary>
    /// Which stream decided what, and what a repeat of the same attempt is entitled to (BQa-002).
    ///
    /// <see cref="DeterministicRng.Fork"/> already derives a private stream from a label without
    /// advancing its parent, so two consumers holding forks of the same stream cannot perturb each
    /// other however many numbers either draws. What was missing was not another RNG: it was one
    /// place saying which label belongs to which family, and what makes a second genuine attempt a
    /// second attempt rather than a replay of the first.
    ///
    /// <b>Two families.</b> A <em>simulation</em> stream decides something the world is then held
    /// to - a check, an actor's intent, a tie-break. An <em>expression</em> stream decides only how
    /// something already decided is worded. Every label lives here so the boundary can be read in
    /// one file rather than reconstructed from call sites, and so a new consumer picks a family
    /// deliberately. Expression may be off entirely, may draw a different number of times as
    /// content changes, and may allocate delivery identities of its own; none of that is allowed to
    /// move a simulation draw, which is exactly what forking from the parent - rather than drawing
    /// from it - buys.
    ///
    /// <b>Two occurrence policies, both already persisted.</b> An attempt made through the action
    /// library draws from the world stream itself, whose state is saved: each attempt continues the
    /// sequence, and a reload continues it rather than repeating it. A scene forks by stable label
    /// instead, which is what makes a scene replay identically from a seed - and would also make a
    /// second genuine firing of the same storylet repeat the first one's rolls exactly. <see
    /// cref="Occurrence"/> is the answer: the scene's stream is keyed on how many times that
    /// storylet has already fired on that thread for that focus, which is history the thread
    /// already keeps and the save already carries. Nothing new is persisted for it.
    ///
    /// Inspecting a scene is not firing one. A play that applies no consequences records no firing,
    /// so reopening a surface - however many times - draws from the same occurrence stream and
    /// gains no roll. The first occurrence keeps the scene stream unchanged, so the keys that were
    /// stable before this contract stay stable under it.
    /// </summary>
    public static class RngStreams
    {
        /// <summary>
        /// The stream for one occurrence of a storylet: the scene stream itself for the first
        /// firing, and a stable derivation of it for each later one.
        /// </summary>
        /// <param name="scene">The stream the scene is played from.</param>
        /// <param name="storyletId">The definition being played.</param>
        /// <param name="focus">The fact it is about, so two matters do not share an occurrence.</param>
        /// <param name="index">How many times this storylet has already fired for that focus.</param>
        public static DeterministicRng Occurrence(DeterministicRng scene, string storyletId, EntityId focus, int index)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "An occurrence cannot have happened a negative number of times.");
            }

            return index == 0
                ? scene
                : scene.Fork("bqa002|occurrence|" + storyletId + "|" + focus.Value + "|" + index);
        }

        /// <summary>How a beat's declared uncertainty resolves. Simulation.</summary>
        public static DeterministicRng Check(DeterministicRng scene, string beatId, string question)
        {
            return Simulation(scene, "bq146|check|" + beatId + "|" + question);
        }

        /// <summary>The coin an actor's own weighing turns on. Simulation.</summary>
        public static DeterministicRng Intent(DeterministicRng scene, EntityId speaker, string beatId, string act)
        {
            return Simulation(scene, "bq146|intent|" + speaker.Value + "|" + beatId + "|" + act);
        }

        /// <summary>
        /// Which of two level contenders takes an indivisible opportunity (BQa-015). Simulation.
        ///
        /// Keyed on the batch as well as the contest, so a contest that comes level again in a
        /// later batch is not settled the same way twice - ties move with the seed and with the
        /// batch, which is to say with time, and never with the order a collection enumerated in.
        /// A fork rather than a draw, so ranking a contest cannot move the check its winner is
        /// about to roll.
        /// </summary>
        public static DeterministicRng TieBreak(DeterministicRng world, string batchKey, string contestKey, EntityId contender)
        {
            return Simulation(world, "bqa015|tiebreak|" + batchKey + "|" + contestKey + "|" + contender.Value);
        }

        /// <summary>The wording of one beat, when anybody is rendering it. Expression.</summary>
        public static DeterministicRng Line(DeterministicRng scene, string beatId)
        {
            return Expression(scene, "bq146|line|" + beatId);
        }

        /// <summary>Which eligible fragment says it. Expression.</summary>
        public static DeterministicRng Fragment(DeterministicRng line, string position, string signature)
        {
            return Expression(line, "bq074|" + position + "|" + signature);
        }

        /// <summary>
        /// A stream whose draws the world is held to. Named rather than inlined so that adding one
        /// is a deliberate act: a simulation label is a decision key, and changing it changes what
        /// a saved world does next.
        /// </summary>
        public static DeterministicRng Simulation(DeterministicRng parent, string label)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            return parent.Fork(label);
        }

        /// <summary>
        /// A stream that only decides wording. Always a fork, never the parent itself: an
        /// expression consumer handed the stream a check draws from could move the check by
        /// drawing at all, and that is the one thing this boundary exists to prevent.
        /// </summary>
        public static DeterministicRng Expression(DeterministicRng parent, string label)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            return parent.Fork(label);
        }
    }
}
