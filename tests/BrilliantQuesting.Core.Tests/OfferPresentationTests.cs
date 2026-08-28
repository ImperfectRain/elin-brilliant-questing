using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// A presentation surface with a choice limit must never drop a route that ends the situation
    /// in favour of one that only stirs it. These tests pin that, and pin it against the real
    /// registry so that adding a verb cannot quietly push a resolution off the end again.
    /// </summary>
    public class OfferPresentationTests
    {
        private static List<ActionOffer> AllAvailable()
        {
            List<ActionOffer> offers = new List<ActionOffer>();
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                offers.Add(new ActionOffer(action, Availability.Available()));
            }

            return offers;
        }

        [Fact]
        public void EveryStandardVerbIsRanked()
        {
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                Assert.True(
                    OfferPresentation.Rank(action.Id) < OfferPresentation.UnrankedTier,
                    action.Id + " has no display rank, so it would be dropped first by any capped surface.");
            }
        }

        /// <summary>
        /// The regression this exists for: the resolutions are registered last, so a plain
        /// "first seven available" cap hides all of them the moment the earlier verbs open up.
        /// </summary>
        [Fact]
        public void ResolutionsSurviveTheCapThatRegistrationOrderWouldHaveHidden()
        {
            List<ActionOffer> all = AllAvailable();
            Assert.True(all.Count > 7, "the cap is only interesting when there is more than it can show");

            List<ActionOffer> shown = OfferPresentation.TakeForDisplay(all, 7);
            List<string> ids = shown.ConvertAll(offer => offer.Action.Id);

            Assert.Equal(7, shown.Count);
            Assert.Contains("return_item", ids);
            Assert.Contains("keep_item", ids);
            Assert.Contains("expose", ids);
        }

        [Fact]
        public void InformationVerbsComeBeforePressure()
        {
            List<string> ids = OfferPresentation.TakeForDisplay(AllAvailable(), 7)
                .ConvertAll(offer => offer.Action.Id);

            Assert.True(ids.IndexOf("question") < ids.IndexOf("intimidate"));
            Assert.True(ids.IndexOf("search") < ids.IndexOf("intimidate"));
        }

        [Fact]
        public void OrderIsStableWithinARank()
        {
            List<ActionOffer> all = AllAvailable();

            List<string> first = OfferPresentation.TakeForDisplay(all, 7).ConvertAll(o => o.Action.Id);
            List<string> second = OfferPresentation.TakeForDisplay(all, 7).ConvertAll(o => o.Action.Id);

            Assert.Equal(first, second);
        }

        [Fact]
        public void NothingIsDroppedWhenEverythingFits()
        {
            List<ActionOffer> all = AllAvailable();

            Assert.Equal(all.Count, OfferPresentation.TakeForDisplay(all, all.Count).Count);
        }

        [Fact]
        public void EmptyAndDegenerateInputsAreSafe()
        {
            Assert.Empty(OfferPresentation.TakeForDisplay(null, 7));
            Assert.Empty(OfferPresentation.TakeForDisplay(AllAvailable(), 0));
            Assert.Empty(OfferPresentation.TakeForDisplay(new List<ActionOffer>(), 7));
        }
    }
}
