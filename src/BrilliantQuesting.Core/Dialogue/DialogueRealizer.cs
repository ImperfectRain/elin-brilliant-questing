using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// One line, and everything needed to check that it is only a line.
    ///
    /// <see cref="Meaning"/> is the act's own signature, carried through untouched. It is the
    /// whole assurance the type offers and it is worth more than the text: a caller, a test or an
    /// inspector can hold the meaning beside the words and see that three renderings of one act
    /// are three renderings of one act.
    /// </summary>
    public sealed class RealizedLine
    {
        private static readonly string[] NoFragments = new string[0];

        private RealizedLine(SpeechAct act, string text, IReadOnlyList<string> fragments, string core, string refusal)
        {
            Act = act;
            Text = text ?? string.Empty;
            Fragments = fragments ?? NoFragments;
            Core = core ?? string.Empty;
            Refusal = refusal ?? string.Empty;
        }

        /// <summary>The act, as it was given. Realization does not copy it and cannot alter it.</summary>
        public SpeechAct Act { get; }

        /// <summary>Whether there were words for it. False lines carry no text, not a fallback one.</summary>
        public bool Rendered => Refusal.Length == 0;

        public string Text { get; }

        /// <summary>The act's wording-free identity, or empty when nothing was said.</summary>
        public string Meaning => Act == null ? string.Empty : Act.Signature;

        /// <summary>Which fragments were used, in the order they were spoken.</summary>
        public IReadOnlyList<string> Fragments { get; }

        /// <summary>The one fragment that carried the point.</summary>
        public string Core { get; }

        /// <summary>Why there was no line, in words nothing branches on.</summary>
        public string Refusal { get; }

        public override string ToString() => Rendered ? Text : "(unrealized: " + Refusal + ")";

        internal static RealizedLine Said(SpeechAct act, string text, IReadOnlyList<string> fragments, string core)
        {
            return new RealizedLine(act, text, fragments, core, string.Empty);
        }

        internal static RealizedLine Unsaid(SpeechAct act, string refusal)
        {
            return new RealizedLine(act, string.Empty, NoFragments, string.Empty, refusal);
        }
    }

    /// <summary>
    /// Turns a meaning into words, and is incapable of doing anything else (CD §18, §38 Phase C).
    ///
    /// The layer above the semantic machinery of BQ-070 through BQ-073, and the first one in the
    /// mod that produces English. Everything about its shape is chosen to keep that from
    /// mattering:
    ///
    /// <b>It has no world.</b> Not a read-only one, not a scoped one - none. The constructor takes
    /// a fragment library and <see cref="Realize"/> takes a request, and neither carries anything
    /// that can be written to. "Realization writes no world state" is therefore a fact about the
    /// signature rather than a discipline about the body, and no later change to the assembly can
    /// quietly weaken it without changing the type.
    ///
    /// <b>It chooses; it does not compose.</b> Every phrase it can emit was authored in content
    /// and every condition it can choose on is a <see cref="DialogueReadings"/> reading of state
    /// that already existed. There is no grammar, no inflection, no template that fills itself in
    /// from the world, and no runtime model anywhere near it - the mod's rule that no LLM decides
    /// authoritative state is not weakened by a wording layer that never generates prose.
    ///
    /// <b>It refuses rather than repairs.</b> An act nothing in the library has words for produces
    /// an unrealized line with a reason, never a vaguer line assembled from openers and closers.
    /// A line that says less than it should is a bug in content; a line that says something the
    /// simulation did not decide is a bug in the world, and only one of those is recoverable.
    ///
    /// <b>It is deterministic.</b> The same semantic state and the same seed give the same line,
    /// whatever else was realized in between: choices are drawn from streams forked off the
    /// caller's, so nothing depends on call order.
    /// </summary>
    public sealed class DialogueRealizer
    {
        /// <summary>
        /// The order the slots are spoken in, and the only grammar in the system: opener, core,
        /// modifier, callback, context, closer (CD §18).
        /// </summary>
        private static readonly FragmentPosition[] Line =
        {
            FragmentPosition.Opener,
            FragmentPosition.Core,
            FragmentPosition.Modifier,
            FragmentPosition.Callback,
            FragmentPosition.Context,
            FragmentPosition.Closer
        };

        private static readonly DeterministicRng Unseeded = new DeterministicRng(0UL);

        public DialogueRealizer(DialogueFragmentLibrary library)
        {
            Library = library ?? new DialogueFragmentLibrary();
        }

        public DialogueFragmentLibrary Library { get; }

        /// <summary>
        /// The words for one act, or a refusal saying why there are none.
        ///
        /// Exactly one core fragment and up to one of each other slot. The optional slots are drawn
        /// against an implicit "say nothing", which is why not every line has every part - a
        /// speaker who opened, qualified, called back, apologised for the company and signed off
        /// every single time would be a machine talking.
        /// </summary>
        public RealizedLine Realize(RealizationRequest request)
        {
            if (request == null)
            {
                return RealizedLine.Unsaid(null, "there is no request to realize");
            }

            string refusal = request.WhyNot();
            if (refusal.Length != 0)
            {
                return RealizedLine.Unsaid(request.Act, refusal);
            }

            SpeechAct act = request.Act;
            RealizationReading reading = RealizationReading.Of(act, request.Decision, request.Claim, request.Cast, request.Recalled);
            DeterministicRng rng = request.Rng ?? Unseeded;

            List<DialogueFragment> cores = Candidates(FragmentPosition.Core, request, reading);
            if (cores.Count == 0)
            {
                return RealizedLine.Unsaid(act, "nothing in the fragment library says " + reading.Value(DialogueReadings.Act)
                    + " under these conditions");
            }

            DialogueExpressionHistory history = request.History;
            history?.NoteAct(reading.Value(DialogueReadings.Act));

            DialogueFragment reserved = ReserveCore(cores, act, rng, history, request.WeirdnessBudget);

            StringBuilder text = new StringBuilder();
            List<string> used = new List<string>();
            string core = string.Empty;

            for (int i = 0; i < Line.Length; i++)
            {
                FragmentPosition position = Line[i];
                bool required = position == FragmentPosition.Core;
                DialogueFragment fragment;
                if (required)
                {
                    fragment = reserved;
                }
                else
                {
                    List<DialogueFragment> candidates = AvoidRepetition(Candidates(position, request, reading), false, history);
                    if (candidates.Count == 0)
                    {
                        continue;
                    }

                    DeterministicRng stream = rng.Fork("bq074|" + position + "|" + act.Signature);
                    int pick = stream.NextInt(candidates.Count + 1);
                    if (pick == candidates.Count)
                    {
                        continue;
                    }

                    fragment = candidates[pick];
                }

                string phrase = Fill(fragment, reading);
                if (phrase.Length == 0)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                text.Append(phrase);
                used.Add(fragment.Id);
                history?.Note(fragment);
                if (required)
                {
                    core = fragment.Id;
                }
                else
                {
                    request.WeirdnessBudget?.Note(fragment);
                }
            }

            return RealizedLine.Said(act, text.ToString(), used.ToArray(), core);
        }

        /// <summary>
        /// Every fragment that could fill this slot for this request, in a stable order.
        ///
        /// Exposed because "which ways of saying this were available" is the question worth asking
        /// of a wording layer - a line that surprises somebody is usually a pool that was smaller
        /// or larger than they thought, and answering that should not require rerunning the
        /// selection.
        /// </summary>
        public IReadOnlyList<DialogueFragment> Candidates(FragmentPosition position, RealizationRequest request)
        {
            if (request == null || request.Act == null)
            {
                return new DialogueFragment[0];
            }

            return Candidates(
                position,
                request,
                RealizationReading.Of(request.Act, request.Decision, request.Claim, request.Cast, request.Recalled));
        }

        private List<DialogueFragment> Candidates(FragmentPosition position, RealizationRequest request, RealizationReading reading)
        {
            List<DialogueFragment> eligible = new List<DialogueFragment>();
            IReadOnlyList<DialogueFragment> all = Library.At(position);
            for (int i = 0; i < all.Count; i++)
            {
                DialogueFragment fragment = all[i];
                if (fragment.Fits(reading) && fragment.FitsTone(request.Tone)
                    && fragment.FitsVocabulary(request.Vocabulary) && fragment.FitsManner(request.Forbidden)
                    && fragment.FitsWeirdness(request.WeirdnessBudget)
                    && Resolves(fragment, reading))
                {
                    eligible.Add(fragment);
                }
            }

            return eligible;
        }

        /// <summary>
        /// Whether everything the fragment names can be named from this act.
        ///
        /// A fragment that would have said a name nobody supplied is not eligible. It does not
        /// fall back to a pronoun, a role or "someone": referring to a person the caller did not
        /// put on stage would be the wording layer deciding who is in the conversation.
        /// </summary>
        private static bool Resolves(DialogueFragment fragment, RealizationReading reading)
        {
            for (int i = 0; i < fragment.Slots.Count; i++)
            {
                if (reading.Slot(fragment.Slots[i]) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Chooses the core, and spends the scene's weirdness on it, before any optional slot gets
        /// to run.
        ///
        /// The core is the one fragment that has to be said, so it is the one entitled to the
        /// scene's single absurd premise (BQ-079). Selecting it inside the spoken order left it
        /// choosing from a pool computed before the loop began, while an opener spoken ahead of it
        /// had already been able to <see cref="WeirdnessBudget.Note"/> a premise of its own - so a
        /// line could open on one absurd premise and then make its actual point on a second,
        /// against the very invariant the budget exists to hold. Deciding the core first inverts
        /// that: whatever premise the core commits to is already noted by the time any optional
        /// slot is filtered, and an optional fragment can never price out the fragment carrying the
        /// point.
        ///
        /// It changes no output on its own. The pick is drawn from the same
        /// <see cref="DeterministicRng.Fork"/> key over the same candidate list, and the fragment
        /// is still spoken - and still noted into <see cref="RealizationRequest.History"/> - in the
        /// core's own place in the line.
        /// </summary>
        private static DialogueFragment ReserveCore(
            List<DialogueFragment> cores,
            SpeechAct act,
            DeterministicRng rng,
            DialogueExpressionHistory history,
            WeirdnessBudget budget)
        {
            List<DialogueFragment> candidates = AvoidRepetition(cores, true, history);
            DeterministicRng stream = rng.Fork("bq074|" + FragmentPosition.Core + "|" + act.Signature);
            DialogueFragment core = candidates[stream.NextInt(candidates.Count)];
            budget?.Note(core);
            return core;
        }

        /// <summary>
        /// Narrows an already-eligible pool away from what this conversation has said too often
        /// (BQ-078). Never adds a candidate <see cref="Candidates"/> did not already admit, and
        /// never removes the last one a required slot has to speak from: a core with nothing fresh
        /// left reuses the pool it already had rather than falling through to a refusal, which is
        /// exactly the degrade CD §21 asks for and BQ-074's "wording is never told to invent"
        /// forbids working around any other way. An optional slot has always been allowed to say
        /// nothing, so narrowing it to empty needs no separate fallback - the position is simply
        /// skipped, the same way a slot with zero eligible candidates always has been.
        /// </summary>
        private static List<DialogueFragment> AvoidRepetition(List<DialogueFragment> candidates, bool required, DialogueExpressionHistory history)
        {
            if (history == null || candidates.Count == 0)
            {
                return candidates;
            }

            List<DialogueFragment> fresh = new List<DialogueFragment>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (history.IsFresh(candidates[i]))
                {
                    fresh.Add(candidates[i]);
                }
            }

            if (fresh.Count > 0 || !required)
            {
                return fresh;
            }

            return candidates;
        }

        private static string Fill(DialogueFragment fragment, RealizationReading reading)
        {
            string text = fragment.Text;
            for (int i = 0; i < fragment.Slots.Count; i++)
            {
                string slot = fragment.Slots[i];
                text = text.Replace("{" + slot + "}", reading.Slot(slot));
            }

            return text.Trim();
        }
    }
}
