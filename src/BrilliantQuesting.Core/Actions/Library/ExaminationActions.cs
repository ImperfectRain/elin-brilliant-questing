using System;
using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Reading a real object for what it can tell you.
    ///
    /// Every verb below is the same act performed by a different discipline: the player has a
    /// thing, the graph knows which facts that thing could substantiate, and a check decides
    /// whether they get it out of it. Sharing the machinery is deliberate. A corpse, a ledger and
    /// a vial are not three subsystems - they are one question ("what does this object prove?")
    /// asked by three different skills, and the moment they stop sharing an implementation they
    /// start disagreeing about what proof is.
    ///
    /// Two rules hold for all of them.
    ///
    /// You examine what you are carrying. That is not a limitation to work around, it is the
    /// point: it makes searching a scene, lifting a pocket and looting a body the things that
    /// *precede* forensics, and it keeps physical proof attached to a physical object the player
    /// can lose, sell or have taken off them.
    ///
    /// A wrong reading is a real outcome. On a critical failure the examiner does not merely
    /// learn nothing; they reach a confident conclusion about the wrong person, recorded as its
    /// own false fact standing beside the true one. That is the same shape a garbled rumour takes,
    /// so everything that can later correct one corrects the other.
    /// </summary>
    public abstract class ExaminationAction : NarrativeAction
    {
        protected ExaminationAction(string id, string label, CheckProfile profile)
            : base(id, ActionFamily.Information, label)
        {
            Profile = profile;
        }

        protected CheckProfile Profile { get; }

        /// <summary>What to say when there is nothing this discipline can be pointed at.</summary>
        protected abstract string NothingToRead { get; }

        /// <summary>Whether this discipline can read this object for this fact at all.</summary>
        protected abstract bool Reads(ItemDescriptor item, Fact fact);

        /// <summary>
        /// Whether a reading at this outcome is something the examiner could show a third party.
        /// A specialist who succeeds can point at the thing and say why; see
        /// <see cref="InspectAction"/> for the generalist who usually cannot.
        /// </summary>
        protected virtual bool ProvesOn(CheckOutcome outcome) => outcome.IsSuccess();

        protected virtual double ConfidenceOn(CheckOutcome outcome)
        {
            return outcome == CheckOutcome.CriticalPass ? 1.0 : 0.85;
        }

        /// <summary>Where the objects this discipline may be pointed at are, right now.</summary>
        protected virtual IReadOnlyList<ItemDescriptor> Reachable(ActionContext context)
        {
            return context.Vanilla.GetInventory(context.Actor);
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.ReadInventory))
            {
                return Availability.Impossible("you cannot go through what is here on this build");
            }

            return TryFindReading(context, out ItemDescriptor _, out Fact _)
                ? Availability.Available()
                : Availability.NotRelevant(NothingToRead);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            if (!TryFindReading(context, out ItemDescriptor item, out Fact fact))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here for you to go over.");
                nothing.Notes.Add(NothingToRead);
                return nothing;
            }

            CheckRequest request = new CheckRequest(Profile, context.Actor, EntityId.None);

            // The same term `search` uses. Something somebody worked to keep quiet is harder to
            // read off the thing it left behind.
            request.WithModifier("how well it was hidden", fact.Secrecy / 20);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                    return Concluded(context, check, item, fact);

                case CheckOutcome.Fail:
                {
                    ActionOutcome missed = new ActionOutcome(Id, check, "You go over " + item.Name + " and come away with nothing.");
                    missed.Notes.Add("nothing learned; the object is unchanged and can be examined again");
                    return missed;
                }

                default:
                    return Misread(context, check, item, fact);
            }
        }

        private ActionOutcome Concluded(ActionContext context, CheckResult check, ItemDescriptor item, Fact fact)
        {
            bool proves = ProvesOn(check.Outcome);
            IReadOnlyList<ProofLink> proofs = proves
                ? new[] { new ProofLink(ProofKind.PhysicalEvidence, item.Id) }
                : null;

            context.World.Knowledge.Teach(
                context.Actor,
                fact.Id,
                KnowledgeSource.Document,
                ConfidenceOn(check.Outcome),
                context.Now,
                proves,
                proofs);

            ActionOutcome outcome = new ActionOutcome(Id, check, item.Name + " tells you: " + ActionSupport.Describe(context, fact.Id) + ".");
            outcome.Notes.Add("learned: " + ActionSupport.Describe(context, fact.Id) + (proves ? " (provable)" : " (unprovable)"));
            if (!proves)
            {
                outcome.Notes.Add("you can say what you saw; you could not walk somebody through it");
            }

            outcome.Events.Add(context.World.Record(
                WorldEventType.SecretLearned,
                context.Actor,
                fact.Subject,
                context.Now,
                0.4,
                context.Zone,
                new[] { fact.Id },
                threadId: context.Thread?.Id ?? EntityId.None));
            return outcome;
        }

        /// <summary>
        /// The confident wrong answer.
        ///
        /// Standing rule 13 asks a critical failure to create a new problem rather than to refuse
        /// the player, and for an investigator the problem that has teeth is not "you found
        /// nothing" - it is "you found the wrong person, and you are certain". The mistaken
        /// version is minted the same way a garbled retelling is, so it is false, it is linked to
        /// the truth it displaced, and it can be argued back down later.
        /// </summary>
        private ActionOutcome Misread(ActionContext context, CheckResult check, ItemDescriptor item, Fact fact)
        {
            Fact wrong = new RumorDistortion().Blame(
                context.World, context.Vanilla, fact, context.Actor, context.Actor, context.Rng);

            if (wrong == null || wrong.Id == fact.Id)
            {
                ActionOutcome plain = new ActionOutcome(Id, check, "You make a hash of " + item.Name + " and learn nothing from it.");
                plain.Notes.Add("no plausible wrong conclusion was available; the reading simply failed");
                return plain;
            }

            context.World.Knowledge.Teach(context.Actor, wrong.Id, KnowledgeSource.Inference, 0.75, context.Now, false);

            ActionOutcome outcome = new ActionOutcome(Id, check, "You read " + item.Name + " with complete confidence, and read it wrong.");
            outcome.Notes.Add("false conclusion held: " + ActionSupport.Describe(context, wrong.Id));
            outcome.Notes.Add("the true version is untouched and still in the graph");
            outcome.Events.Add(context.World.Record(
                WorldEventType.RumorDistorted,
                context.Actor,
                fact.Subject,
                context.Now,
                0.5,
                context.Zone,
                new[] { wrong.Id, fact.Id },
                threadId: context.Thread?.Id ?? EntityId.None));
            return outcome;
        }

        /// <summary>
        /// The object within reach that this discipline can read for something the examiner cannot
        /// already prove, taking them in the order they are carried.
        ///
        /// Already knowing a fact is not a reason to skip the object - proof is the thing being
        /// looked for, and a belief you cannot demonstrate is why authorities turn people away.
        ///
        /// The fact store is walked once and the winner chosen afterwards rather than returning
        /// whatever turned up first: the store is a dictionary, so first-found is not a stable
        /// answer, and the same pack in the same world has to offer the same reading twice.
        /// </summary>
        private bool TryFindReading(ActionContext context, out ItemDescriptor item, out Fact fact)
        {
            item = null;
            fact = null;

            IReadOnlyList<ItemDescriptor> reachable = Reachable(context);
            Dictionary<EntityId, int> order = new Dictionary<EntityId, int>();
            for (int i = 0; i < reachable.Count; i++)
            {
                if (reachable[i] != null && !order.ContainsKey(reachable[i].Id))
                {
                    order[reachable[i].Id] = i;
                }
            }

            int bestItem = int.MaxValue;
            foreach (Fact evidenced in context.World.Knowledge.FactsEvidencedBy(order.Keys))
            {
                if (evidenced.Truth == TruthState.Superseded
                    || context.World.Knowledge.CanProve(context.Actor, evidenced.Id))
                {
                    continue;
                }

                for (int i = 0; i < evidenced.EvidenceIds.Count; i++)
                {
                    if (!order.TryGetValue(evidenced.EvidenceIds[i], out int index)
                        || index > bestItem
                        || !Reads(reachable[index], evidenced))
                    {
                        continue;
                    }

                    if (index < bestItem || fact == null || string.CompareOrdinal(evidenced.Id.Value, fact.Id.Value) < 0)
                    {
                        bestItem = index;
                        item = reachable[index];
                        fact = evidenced;
                    }
                }
            }

            return fact != null;
        }
    }

    /// <summary>
    /// What kind of thing an object is, as far as an investigator is concerned.
    ///
    /// Elin's own category id is the first answer and the object's name is the fallback. That
    /// belt-and-braces reading is deliberate: the shipped category vocabulary has not been
    /// verified against a live build, and a specialist verb that silently disappears because a
    /// corpse is filed under a tag this list has never heard of is worse than one that
    /// occasionally offers itself where it is useless. Nothing depends on getting it right -
    /// <see cref="InspectAction"/> reads anything, so a misfiled object still has a route.
    /// </summary>
    internal static class TraceMaterial
    {
        private static readonly string[] RemainsWords = { "corpse", "body", "remains", "carcass", "bone", "skull" };
        private static readonly string[] DocumentWords = { "book", "scroll", "note", "letter", "ledger", "record", "map", "tablet", "document" };
        private static readonly string[] SubstanceWords = { "potion", "drink", "powder", "poison", "dust", "vial", "flask", "reagent", "herb", "seed", "oil" };

        public static bool IsRemains(ItemDescriptor item) => ActionSupport.LooksLike(item, RemainsWords);

        public static bool IsDocument(ItemDescriptor item) => ActionSupport.LooksLike(item, DocumentWords);

        public static bool IsSubstance(ItemDescriptor item) => ActionSupport.LooksLike(item, SubstanceWords);
    }

    /// <summary>
    /// Look at a thing properly, whatever it is.
    ///
    /// The generalist route, and the one that must never be missing: any object can be turned over
    /// by anybody. What it cannot usually do is produce proof. Noticing that a vial smells wrong
    /// is not the same as being able to stand in front of a guard and say what was in it, and the
    /// gap between those two is exactly what the specialist disciplines are for.
    /// </summary>
    public sealed class InspectAction : ExaminationAction
    {
        public InspectAction() : base("inspect", "Look it over", ProceduralCheckProfiles.Investigation)
        {
        }

        protected override string NothingToRead => "nothing you are carrying has anything to say";

        protected override bool Reads(ItemDescriptor item, Fact fact) => true;

        protected override bool ProvesOn(CheckOutcome outcome) => outcome == CheckOutcome.CriticalPass;

        protected override double ConfidenceOn(CheckOutcome outcome)
        {
            return outcome == CheckOutcome.CriticalPass ? 0.9 : 0.6;
        }
    }

    /// <summary>Read a body for what killed it. Anatomy, and a strong stomach.</summary>
    public sealed class ExamineCorpseAction : ExaminationAction
    {
        public ExamineCorpseAction() : base("examine_corpse", "Examine the body", ProceduralCheckProfiles.Forensics)
        {
        }

        protected override string NothingToRead => "you are not carrying remains worth reading";

        protected override bool Reads(ItemDescriptor item, Fact fact) => TraceMaterial.IsRemains(item);
    }

    /// <summary>
    /// Read a document that is written to be read.
    ///
    /// The split from <see cref="TranslateAction"/> is the secrecy of what the document records,
    /// not a property stored on the object. Nothing the adapter can see distinguishes a plain
    /// ledger from a coded one, and inventing an item field the live game has nothing to fill
    /// would be a second, private description of a thing Elin already owns. How hard somebody
    /// worked to keep a fact from being read is already in the graph, and a document carrying an
    /// actively hidden fact is the one that was written not to be understood.
    /// </summary>
    public sealed class ReadDocumentAction : ExaminationAction
    {
        /// <summary>At or above this, the writing is a problem in itself rather than just writing.</summary>
        public const int ObscuredAt = 60;

        public ReadDocumentAction() : base("read", "Read it", ProceduralCheckProfiles.Documents)
        {
        }

        protected override string NothingToRead => "you are carrying nothing written that you have not already read";

        protected override bool Reads(ItemDescriptor item, Fact fact)
        {
            return TraceMaterial.IsDocument(item) && fact.Secrecy < ObscuredAt;
        }
    }

    /// <summary>Get a coded, foreign or dead-script document to give up what it says.</summary>
    public sealed class TranslateDocumentAction : ExaminationAction
    {
        public TranslateDocumentAction() : base("translate", "Work out what it says", ProceduralCheckProfiles.Translation)
        {
        }

        protected override string NothingToRead => "nothing you carry is written in anything you cannot already read";

        protected override bool Reads(ItemDescriptor item, Fact fact)
        {
            return TraceMaterial.IsDocument(item) && fact.Secrecy >= ReadDocumentAction.ObscuredAt;
        }
    }

    /// <summary>Work out what a substance is, and therefore what it was used for.</summary>
    public sealed class IdentifySubstanceAction : ExaminationAction
    {
        public IdentifySubstanceAction() : base("identify_substance", "Work out what it is", ProceduralCheckProfiles.SubstanceAnalysis)
        {
        }

        protected override string NothingToRead => "you are carrying nothing worth testing";

        protected override bool Reads(ItemDescriptor item, Fact fact) => TraceMaterial.IsSubstance(item);
    }

    /// <summary>
    /// Go through somebody else's papers.
    ///
    /// Mechanically this is <see cref="ReadDocumentAction"/> pointed at a shelf that is not yours,
    /// and that difference is the whole verb: an archive, a shop's ledger and a guild's rolls hold
    /// the things nobody would tell you, and reading them is possible, unowned and risky rather
    /// than impossible. What it cannot do is hand you proof. The book stays where it was, so the
    /// reader leaves knowing something they cannot show anybody - which is precisely the state
    /// that sends a player back out after the object itself.
    /// </summary>
    public sealed class SearchRecordsAction : ExaminationAction
    {
        public SearchRecordsAction() : base("search_records", "Go through their records", ProceduralCheckProfiles.Documents)
        {
        }

        protected override string NothingToRead => "there are no records here you have not already been through";

        protected override bool Reads(ItemDescriptor item, Fact fact) => TraceMaterial.IsDocument(item);

        /// <summary>Nothing leaves with you, so nothing can be shown to anybody afterwards.</summary>
        protected override bool ProvesOn(CheckOutcome outcome) => false;

        protected override double ConfidenceOn(CheckOutcome outcome)
        {
            return outcome == CheckOutcome.CriticalPass ? 0.95 : 0.8;
        }

        protected override IReadOnlyList<ItemDescriptor> Reachable(ActionContext context)
        {
            return !ActionSupport.Present(context, context.Target)
                ? EmptyShelf
                : context.Vanilla.GetInventory(context.Target);
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here keeps records");
            }

            return base.GetAvailability(context);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ActionOutcome outcome = base.Perform(context);

            // Being caught with your hands in somebody's papers is the risk that makes this
            // different from reading your own. The reading itself already happened above; this is
            // only what the room made of it.
            if (outcome.Outcome == CheckOutcome.CriticalFail && outcome.Check != null)
            {
                outcome.Events.Add(context.World.Record(
                    WorldEventType.Trespass,
                    context.Actor,
                    context.Target,
                    context.Now,
                    0.4,
                    context.Zone,
                    witnesses: ActionSupport.Bystanders(context, true),
                    threadId: context.Thread?.Id ?? EntityId.None));
                outcome.Notes.Add("caught going through " + context.NameOf(context.Target) + "'s records");
            }

            return outcome;
        }

        private static readonly ItemDescriptor[] EmptyShelf = new ItemDescriptor[0];
    }
}
