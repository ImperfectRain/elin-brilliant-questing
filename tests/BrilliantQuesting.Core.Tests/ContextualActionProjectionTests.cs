using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ContextualActionProjectionTests
    {
        [Fact]
        public void TheftOptionsProjectAsIntentBearingLabels()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = Focus(lab, lab.Situation.WitnessId);

            List<ActionIntentOption> options = Project(lab, context);

            Assert.Contains(options, o => o.Action.Id == "question" && o.Label == "Ask " + lab.World.Registry.NameOf(lab.Situation.WitnessId) + " what they saw");
            Assert.Contains(options, o => o.Action.Id == "search" && o.Label.Contains("the missing "));
            Assert.DoesNotContain(options, o => o.Label == "Persuade");
            Assert.DoesNotContain(options, o => o.Label == "Intimidate");
            Assert.DoesNotContain(options, o => o.Label == "Escort");
            Assert.DoesNotContain(options, o => o.Label == "Restrain");
        }

        [Fact]
        public void UnknownCulpritIsNotLeakedThroughIntentText()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = Focus(lab, lab.Situation.ThiefId);

            List<ActionIntentOption> options = Project(lab, context);
            ActionIntentOption persuade = Find(options, "persuade");
            ActionIntentOption pickpocket = Find(options, "pickpocket");

            Assert.Contains("help with", persuade.Label);
            Assert.DoesNotContain("put", persuade.Label);
            Assert.DoesNotContain("right", persuade.Label);
            Assert.Equal("Pick " + lab.World.Registry.NameOf(lab.Situation.ThiefId) + "'s pocket", pickpocket.Label);
        }

        [Fact]
        public void KnownCulpritCanBePressedWithoutExposingRawVerb()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, false);
            ActionContext context = Focus(lab, lab.Situation.ThiefId);

            List<ActionIntentOption> options = Project(lab, context);

            Assert.Contains(options, o => o.Action.Id == "persuade" && o.Label.Contains("put the missing "));
            Assert.Contains(options, o => o.Action.Id == "intimidate" && o.Label.Contains("answer for the missing "));
            Assert.DoesNotContain(options, o => o.Label == "Persuade" || o.Label == "Intimidate");
        }

        [Fact]
        public void ProjectedIntentStillCarriesBackendActionForResolution()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = Focus(lab, lab.Situation.WitnessId);

            List<ActionIntentOption> options = Project(lab, context);
            ActionIntentOption question = Find(options, "question");

            Assert.Equal("question", question.Action.Id);
            Assert.True(question.Availability.IsAvailable);
            Assert.Equal("Talk", question.Surface);
            Assert.Equal("Investigate", question.IntentFamily);
        }

        private static ActionContext Focus(TheftLaboratory lab, BrilliantQuesting.Foundation.EntityId target)
        {
            ActionContext context = lab.Context(target);
            context.SubjectFact = lab.Situation.TheftFactId;
            context.SubjectItem = lab.Situation.ItemId;
            return context;
        }

        private static List<ActionIntentOption> Project(TheftLaboratory lab, ActionContext context)
        {
            List<ActionOffer> available = new List<ActionOffer>();
            foreach (ActionOffer offer in lab.Actions.Discover(context))
            {
                available.Add(offer);
            }

            return ContextualActionProjection.Project(available, context, 7);
        }

        private static ActionIntentOption Find(List<ActionIntentOption> options, string actionId)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action.Id == actionId)
                {
                    return options[i];
                }
            }

            throw new KeyNotFoundException(actionId);
        }
    }
}
