using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-151. A memorable line is an alternative; it is never the only way a situation can be
    /// worded.
    ///
    /// The failure these guard against is not a missing line - the library had wording for every
    /// tie, every mood, every room and every kind of recalled history, and the wording was good.
    /// It is that in the slots which fire in <em>every line of every act</em>, the only wording for
    /// eight of thirteen ties, for grief, and for most of the context slot was above utility. A
    /// signature line said every time its situation arises is a catchphrase nobody authored, and
    /// the optional slots are where that costs the most, because a core file is drawn once per act
    /// and these are drawn once per line.
    ///
    /// The invariants are structural rather than aesthetic. Nothing here reads the English, scores
    /// a line or asserts that some particular sentence was chosen; they say only that the plain way
    /// of expressing a situation exists and is reachable, which is a property of the corpus and of
    /// eligibility (`dialogue-writing-inspiration-research.md` §11, §19; CD §18, §19).
    /// </summary>
    public class MundaneWordingTests
    {
        /// <summary>
        /// The slot-and-reading pairs whose wording is chosen on something other than the act, and
        /// therefore fires alongside every act rather than for one of them.
        /// </summary>
        private static IEnumerable<KeyValuePair<FragmentPosition, string>> AlwaysOn()
        {
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Modifier, DialogueReadings.Relationship);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Modifier, DialogueReadings.Emotion);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Context, DialogueReadings.Audience);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Callback, DialogueReadings.Callback);
            yield return new KeyValuePair<FragmentPosition, string>(
                FragmentPosition.Callback, DialogueReadings.CallbackRoute);
        }

        /// <summary>
        /// The corpus-side floor, and the one this step exists for. Any value of an always-on
        /// reading that some authored line is written for keeps a line written for the same value
        /// that asks for no repetition protection beyond the ordinary.
        ///
        /// Written-for rather than eligible-for, deliberately: a modifier with no opinion about the
        /// tie is available to a friend but does not express having one, so counting it would
        /// answer a different question and would answer it reassuringly.
        /// </summary>
        [Fact]
        public void EverySituationAnAlwaysOnSlotWordsAtAllIsAlsoWordedPlainly()
        {
            List<string> onlyMemorable = new List<string>();

            foreach (KeyValuePair<FragmentPosition, string> family in AlwaysOn())
            {
                foreach (string value in DialogueReadings.ValuesOf(family.Value))
                {
                    List<DialogueFragment> written = Declaring(family.Key, family.Value, value);
                    if (written.Count == 0)
                    {
                        continue;
                    }

                    if (!written.Any(Plain))
                    {
                        onlyMemorable.Add(family.Key + "/" + family.Value + "=" + value);
                    }
                }
            }

            Assert.Empty(onlyMemorable);
        }

        /// <summary>
        /// Recognition never depends on who the other side of the old business turned out to be.
        ///
        /// Every kind of recalled material, reached by every route the model derives, has plain
        /// wording that names nobody - so a hook whose other party the world can no longer produce,
        /// or never had one, is still sayable. Before this, `promise`, `kindness` and
        /// `embarrassment` had no party-free wording at all, and `scandal` had it only for the
        /// route it was told by, which left a speaker who watched the thing happen falling through
        /// to the two kind-agnostic lines - one of them a signature.
        /// </summary>
        [Fact]
        public void EveryKindAndRouteAHookArrivesByHasPlainWordingThatNamesNobody()
        {
            List<string> unworded = new List<string>();

            foreach (CallbackKind kind in Enum.GetValues(typeof(CallbackKind)))
            {
                foreach (CallbackRoute route in Enum.GetValues(typeof(CallbackRoute)))
                {
                    bool worded = Shipped().Any(fragment =>
                        fragment.Position == FragmentPosition.Callback
                        && Plain(fragment)
                        && Names(fragment, DialogueReadings.Callback, Slug(kind.ToString()))
                        && Admits(fragment, DialogueReadings.CallbackRoute, Slug(route.ToString()))
                        && !Declared(fragment, DialogueReadings.CallbackParty).Any());

                    if (!worded)
                    {
                        unworded.Add(kind + "/" + route);
                    }
                }
            }

            Assert.Empty(unworded);
        }

        /// <summary>
        /// The realizer's half of the same guarantee: a speaker nobody described in any particular
        /// way still reaches an ordinary way of expressing every tie the world can hand them,
        /// stranger included.
        ///
        /// A corpus check alone would pass a build where the plain line existed and a narrowing
        /// kept it out, so this walks every tie through <see cref="DialogueRealizer.Candidates"/>
        /// under <see cref="VoiceProfile.Neutral"/> and looks for a utility modifier actually
        /// written for that tie. Two acts, because a tie's plain wording is written for the acts
        /// its memorable sibling covers rather than for all sixteen, and answering and refusing are
        /// the two every tie in the corpus has something plain to say in.
        /// </summary>
        [Fact]
        public void EveryTieReachesAPlainModifierForAVoiceNobodyDescribed()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            List<SpeakerTie> ties = new List<SpeakerTie> { SpeakerTie.Stranger(scene.Player) };
            foreach (RelationKind kind in Enum.GetValues(typeof(RelationKind)))
            {
                ties.Add(SpeakerTie.Tied(kind, scene.Player));
            }

            List<string> starved = new List<string>();

            foreach (SpeakerTie tie in ties)
            {
                string value = tie.IsTied ? Slug(tie.Kind.ToString()) : "none";
                bool plain = false;

                foreach (RealizationRequest request in new[] { scene.WitnessAnswers(), scene.ThiefRefuses() })
                {
                    request.Tie = tie;
                    request.Tone = VoiceProfile.Neutral.RequestedTone();
                    request.Idiolect = VoiceProfile.Neutral.RequestedIdiolect();

                    plain = plain || scene.Realizer
                        .Candidates(FragmentPosition.Modifier, request)
                        .Any(fragment =>
                            Plain(fragment)
                            && Names(fragment, DialogueReadings.Relationship, value));
                }

                if (!plain)
                {
                    starved.Add(value);
                }
            }

            Assert.Empty(starved);
        }

        /// <summary>
        /// A mark that cannot exclude anybody is not a narrowing (`D034`). <see cref="DialogueTones.Wry"/>
        /// has no opposite pole, so <see cref="DialogueFragment.FitsTone"/> never refuses a wry line
        /// to a voice that simply took no position on sarcasm - which is how the wryest callback in
        /// the library reached five of the sweep's seven contrasting voices and was reported reused
        /// across profiles at every seed.
        ///
        /// Optional slots only. A core may not carry a demand at all (it would turn a temperament
        /// into a refused act), so a wry core is the vocabulary gap `D034` describes rather than a
        /// line that could have asked and did not.
        /// </summary>
        [Fact]
        public void AWryOptionalFragmentAsksForTheTemperamentItAssumes()
        {
            List<string> unreserved = Shipped()
                .Where(fragment => fragment.Position != FragmentPosition.Core)
                .Where(fragment => fragment.ToneTags.Contains(DialogueTones.Wry))
                .Where(fragment => !fragment.VoiceDemands.Contains(DialogueTones.Wry))
                .Select(fragment => fragment.Id)
                .ToList();

            Assert.Empty(unreserved);
        }

        /// <summary>
        /// A guard on the direction of travel rather than on a number worth defending. The slots
        /// that fire in every line are the ones a player hears most, so most of what fills them
        /// should be wording nobody notices repeating; a later pass that adds memorable material
        /// faster than plain material to these three positions is the thing to catch, and it is
        /// caught here rather than in review.
        /// </summary>
        [Fact]
        public void TheAlwaysOnSlotsStayMostlyPlain()
        {
            IReadOnlyList<DialogueFragment> always = Shipped()
                .Where(fragment =>
                    fragment.Position == FragmentPosition.Modifier
                    || fragment.Position == FragmentPosition.Context
                    || fragment.Position == FragmentPosition.Callback)
                .ToList();

            Assert.InRange(always.Count(Plain), (always.Count / 2) + 1, always.Count);
        }

        // -- reading the corpus -------------------------------------------------------------------

        private static bool Plain(DialogueFragment fragment)
        {
            return string.Equals(fragment.Memorability, DialogueMemorability.Utility, StringComparison.Ordinal);
        }

        private static List<DialogueFragment> Declaring(FragmentPosition position, string key, string value)
        {
            return Shipped()
                .Where(fragment => fragment.Position == position && Names(fragment, key, value))
                .ToList();
        }

        /// <summary>Whether a fragment names this value in its own conditions. Silence is not a declaration.</summary>
        private static bool Names(DialogueFragment fragment, string key, string value)
        {
            IReadOnlyList<string> declared = Declared(fragment, key);
            return declared.Any(candidate => string.Equals(candidate, value, StringComparison.Ordinal));
        }

        /// <summary>Whether a fragment is eligible for this value: it names it, or has no opinion.</summary>
        private static bool Admits(DialogueFragment fragment, string key, string value)
        {
            IReadOnlyList<string> declared = Declared(fragment, key);
            return declared.Count == 0 || Names(fragment, key, value);
        }

        private static IReadOnlyList<string> Declared(DialogueFragment fragment, string key)
        {
            for (int i = 0; i < fragment.Requires.Count; i++)
            {
                if (string.Equals(fragment.Requires[i].Key, key, StringComparison.Ordinal))
                {
                    return fragment.Requires[i].Values;
                }
            }

            return new string[0];
        }

        private static string Slug(string name)
        {
            System.Text.StringBuilder slug = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    slug.Append('_');
                }

                slug.Append(char.ToLowerInvariant(name[i]));
            }

            return slug.ToString();
        }

        private static IReadOnlyList<DialogueFragment> _shipped;

        private static IReadOnlyList<DialogueFragment> Shipped()
        {
            if (_shipped != null)
            {
                return _shipped;
            }

            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                Path.Combine(directory.FullName, "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);

            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments =
                DialogueFragmentContent.LoadFragments(bundle.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            Assert.NotEmpty(fragments);
            _shipped = fragments;
            return fragments;
        }
    }
}
