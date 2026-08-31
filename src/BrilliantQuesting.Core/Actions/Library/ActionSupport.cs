using System;
using System.Collections.Generic;
using System.Globalization;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Shared plumbing for the verb library. No gameplay rules live here.</summary>
    internal static class ActionSupport
    {
        private static readonly EntityId[] NoWitnesses = new EntityId[0];

        /// <summary>
        /// Whether there is a person here to act on at all.
        ///
        /// The one predicate behind every "nobody to steal from", "nobody to pay", "nobody here to
        /// squeeze" in the library. It used to be written out at each of those verbs as "somebody
        /// is named and they are alive", which was true until people could be away: a Grade B
        /// absence means the character is in another zone entirely, and a verb that only asked
        /// whether they were breathing would offer to pick the pocket of somebody who left town.
        /// </summary>
        public static bool Present(ActionContext context, EntityId who)
        {
            return !who.IsNone
                   && context.Vanilla.IsAlive(who)
                   && !context.World.Absences.IsPhysicallyAbsent(who);
        }

        /// <summary>
        /// Whether this person is doing what they do - the trade, the office, the work they take.
        ///
        /// The rung above <see cref="Present"/>, and what separates the two absence grades in
        /// practice. Grade A leaves somebody standing exactly where they were and closes their
        /// counter: they can still be talked to, lied to, robbed and reported to, and what stops
        /// is the fence taking work, the guard taking a statement, the specialist taking a
        /// commission.
        /// </summary>
        public static bool OnDuty(ActionContext context, EntityId who)
        {
            return Present(context, who) && !context.World.Absences.IsAbsent(who);
        }

        /// <summary>The place the actor is standing in, as the simulation understands places.</summary>
        public static NarrativeSite SiteHere(ActionContext context)
        {
            return context.World.Registry.GetSite(context.Zone);
        }

        /// <summary>
        /// One object out of somebody's keeping.
        ///
        /// An explicitly named <see cref="ActionContext.SubjectItem"/> wins outright when it is
        /// there and acceptable, and is never silently replaced with something else: a player who
        /// pointed at the ledger did not mean "or whatever else is in the bag". With nothing named,
        /// the first acceptable object in carry order is taken, so the same pack answers the same
        /// way twice.
        /// </summary>
        public static ItemDescriptor FindItem(ActionContext context, EntityId owner, Func<ItemDescriptor, bool> accepts = null)
        {
            if (owner.IsNone)
            {
                return null;
            }

            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(owner);
            for (int i = 0; i < inventory.Count; i++)
            {
                ItemDescriptor item = inventory[i];
                if (item == null || (accepts != null && !accepts(item)))
                {
                    continue;
                }

                if (context.SubjectItem.IsNone)
                {
                    return item;
                }

                if (item.Id == context.SubjectItem)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Something the target knows that the actor does not. This is what makes a conversation
        /// worth having: if there is nothing to be learned, the option is not offered.
        /// </summary>
        public static EntityId FindTeachableFact(ActionContext context)
        {
            KnowledgeGraph knowledge = context.World.Knowledge;

            if (!context.SubjectFact.IsNone)
            {
                bool targetKnows = knowledge.TryGetBelief(context.Target, context.SubjectFact, out KnowledgeRecord record);
                bool actorKnows = knowledge.Knows(context.Actor, context.SubjectFact);
                if (targetKnows && IsSelfIncriminatingDisclosure(context, context.SubjectFact, record))
                {
                    return EntityId.None;
                }

                return targetKnows && !actorKnows ? context.SubjectFact : EntityId.None;
            }

            EntityId best = EntityId.None;
            double bestConfidence = 0.0;
            foreach (KnowledgeRecord record in knowledge.BeliefsOf(context.Target))
            {
                if (knowledge.Knows(context.Actor, record.FactId))
                {
                    continue;
                }

                if (IsSelfIncriminatingDisclosure(context, record.FactId, record))
                {
                    continue;
                }

                if (record.Confidence > bestConfidence)
                {
                    bestConfidence = record.Confidence;
                    best = record.FactId;
                }
            }

            return best;
        }

        public static KnowledgeSource DisclosureSource(ActionContext context, EntityId factId)
        {
            if (context.World.Knowledge.TryGetBelief(context.Target, factId, out KnowledgeRecord record)
                && record.Source == KnowledgeSource.Participant)
            {
                Fact fact = context.World.Knowledge.GetFact(factId);
                if (fact != null && fact.Subject == context.Target)
                {
                    return KnowledgeSource.Admission;
                }
            }

            return KnowledgeSource.Hearsay;
        }

        public static bool IsSelfIncriminatingDisclosure(ActionContext context, EntityId factId)
        {
            return context.World.Knowledge.TryGetBelief(context.Target, factId, out KnowledgeRecord record)
                   && IsSelfIncriminatingDisclosure(context, factId, record);
        }

        private static bool IsSelfIncriminatingDisclosure(ActionContext context, EntityId factId, KnowledgeRecord record)
        {
            if (record == null || record.Source != KnowledgeSource.Participant)
            {
                return false;
            }

            Fact fact = context.World.Knowledge.GetFact(factId);
            if (fact == null || fact.Subject != context.Target)
            {
                return false;
            }

            return fact.Predicate == FactPredicates.Stole
                   || fact.Predicate == FactPredicates.Killed
                   || fact.Predicate == FactPredicates.Extorted
                   || fact.Predicate == FactPredicates.Forged;
        }

        /// <summary>
        /// Ends the situation this action was performed inside, if there is one.
        ///
        /// Every verb that can close a thread goes through here so the ending is written the same
        /// way each time: the thread's state, the outcome name, and the ledger entry the Chronicle
        /// reads. Silent when there is no thread, and silent on a thread that is already resolved,
        /// so a verb that closes the last of several open demands cannot post two endings.
        /// </summary>
        public static void Resolve(ActionContext context, ActionOutcome outcome, string resolution, double magnitude = 0.5)
        {
            WorldEvent resolved = ThreadResolution.Resolve(
                context.World, context.Thread, resolution, context.Actor, context.Now, magnitude, context.Zone);

            if (resolved == null)
            {
                return;
            }

            outcome?.Events.Add(resolved);
            outcome?.Notes.Add("thread resolved: " + resolution);
        }

        /// <summary>
        /// Whoever the caller says is close enough to notice. Actions decide, per outcome, whether
        /// these people actually saw anything - a clean theft has no witnesses even in a crowd.
        /// </summary>
        public static IReadOnlyList<EntityId> Bystanders(ActionContext context, bool noticed)
        {
            if (!noticed || context.Witnesses.Count == 0)
            {
                return NoWitnesses;
            }

            List<EntityId> seen = new List<EntityId>();
            for (int i = 0; i < context.Witnesses.Count; i++)
            {
                EntityId witness = context.Witnesses[i];
                if (witness != context.Actor)
                {
                    seen.Add(witness);
                }
            }

            return seen;
        }

        /// <summary>
        /// Records that the actor is looking into somebody, and lets that somebody find out.
        ///
        /// The risk that makes investigation a real choice rather than free information, and it
        /// arrives from three different directions - a question that went badly, an accusation
        /// that rebounded, a tail that was noticed - so it lives once. The claim is keyed to who
        /// is being looked into as well as who is doing it: reusing one "investigating" fact per
        /// actor told the second suspect that the player was after the first.
        /// </summary>
        public static Fact WarnUnderInvestigation(
            ActionContext context, EntityId accused, EntityId toldBy, ActionOutcome outcome, double confidence = 0.8, string note = null)
        {
            if (accused.IsNone || accused == context.Actor || context.World.Registry.GetNpc(accused) == null)
            {
                return null;
            }

            Fact investigating = FindInvestigation(context, accused);
            if (investigating == null)
            {
                investigating = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Investigating, accused);
                context.World.Knowledge.AddFact(investigating);
            }

            context.World.Knowledge.Teach(accused, investigating.Id, KnowledgeSource.Hearsay, confidence, context.Now, false, toldBy);
            outcome?.Notes.Add(note ?? context.NameOf(accused) + " now knows someone is asking about them");
            return investigating;
        }

        private static Fact FindInvestigation(ActionContext context, EntityId accused)
        {
            foreach (Fact fact in context.World.Knowledge.Facts.Values)
            {
                if (fact.Subject == context.Actor
                    && fact.Predicate == FactPredicates.Investigating
                    && fact.Object == accused)
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether an object reads as one of a list of kinds.
        ///
        /// Elin's own category id is the first answer and the object's name is the fallback,
        /// because the shipped category vocabulary has not been verified against a live build and
        /// a verb that silently disappears over a tag this project has never heard of is worse
        /// than one that occasionally offers itself where it is useless.
        /// </summary>
        public static bool LooksLike(ItemDescriptor item, string[] words)
        {
            return item != null && (LooksLike(item.CategoryTag, words) || LooksLike(item.Name, words));
        }

        /// <summary>The same reading, for a kind named on its own rather than carried by an object.</summary>
        public static bool LooksLike(string text, string[] words)
        {
            if (words == null)
            {
                return false;
            }

            for (int i = 0; i < words.Length; i++)
            {
                if (Contains(text, words[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                   && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Reads a "&lt;key&gt; &lt;number&gt;" pair out of a specification value, tolerantly.
        ///
        /// Every constraint the situation layer states lives in a fact's free value - what goods
        /// have to be, what a god asks of whoever asks him - and they are all read back the same
        /// way. Anything unparseable is worth zero rather than throwing, because a malformed
        /// specification should cost its thresholds, not the whole route.
        /// </summary>
        public static int ReadNumber(string[] words, string key)
        {
            if (words == null)
            {
                return 0;
            }

            for (int i = 1; i < words.Length - 1; i++)
            {
                if (words[i] == key
                    && int.TryParse(words[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    return value;
                }
            }

            return 0;
        }

        /// <summary>
        /// Closes whatever this thing's failing was causing, and reports how many.
        ///
        /// The shared half of "answer the cause rather than the symptom". A shortage that names a
        /// broken mill wheel and a hamlet that names a blighted field are the same shape: the
        /// demand is a consequence of the object, so removing the object's trouble removes the
        /// demand, once, rather than leaving it to be filled again next month. Shared because a
        /// second copy of it would be a second answer to the question of what a cause closes.
        /// </summary>
        public static int CloseDemandsOn(ActionContext context, EntityId cause, ActionOutcome outcome)
        {
            if (context.Thread == null || cause.IsNone)
            {
                return 0;
            }

            int closed = 0;
            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null
                    && fact.Predicate == FactPredicates.Needs
                    && fact.Truth == TruthState.True
                    && fact.Object == cause)
                {
                    fact.Truth = TruthState.Superseded;
                    RelieveDemand(context, fact, null, outcome, 45, 30);
                    outcome?.Notes.Add("no longer wanted: " + Describe(context, fact.Id));
                    closed++;
                }
            }

            return closed;
        }

        public static void RelieveDemand(
            ActionContext context,
            Fact demand,
            ProductionSpec spec,
            ActionOutcome outcome,
            int amount,
            long days)
        {
            if (context == null || demand == null)
            {
                return;
            }

            string category = spec == null ? demand.Value : spec.CategoryTag;
            EntityId place = context.Thread != null && context.Thread.SiteIds.Count > 0
                ? context.Thread.SiteIds[0]
                : context.Zone;

            if (context.World.Demands.Relieve(place, category, demand.Id, amount, days, context.Now))
            {
                LocalDemandPressure pressure = context.World.Demands.Get(place, category, demand.Id);
                outcome?.Notes.Add("local " + pressure.Category + " pressure now " + pressure.Severity
                    + ", expected relief " + pressure.ExpectedReliefAt);
            }
        }

        public static string Describe(ActionContext context, EntityId factId)
        {
            Fact fact = context.World.Knowledge.GetFact(factId);
            if (fact == null)
            {
                return "something";
            }

            string subject = context.NameOf(fact.Subject);
            // Prefer the human label an item carries over its id; ids are for the database.
            string obj = !string.IsNullOrEmpty(fact.Value) ? fact.Value
                : fact.Object.IsNone ? string.Empty : context.NameOf(fact.Object);
            return (subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj).Trim();
        }
    }
}
