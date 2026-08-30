using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Memory;

namespace BrilliantQuesting.Consequences
{
    /// <summary>
    /// What one kind of event does to the people it touches. Data rather than code so that the
    /// consequence rules are visible in one place and can be tuned without hunting through verbs.
    /// </summary>
    public sealed class ConsequenceProfile
    {
        public ConsequenceProfile(string summaryTag, MemoryWeight weight, int targetAffinity, int witnessAffinity = 0, int karma = 0, int fame = 0)
        {
            SummaryTag = summaryTag;
            Weight = weight;
            TargetAffinity = targetAffinity;
            WitnessAffinity = witnessAffinity;
            Karma = karma;
            Fame = fame;
        }

        public string SummaryTag { get; }

        public MemoryWeight Weight { get; }

        /// <summary>Affinity swing for the person it happened to, scaled by event magnitude.</summary>
        public int TargetAffinity { get; }

        /// <summary>Smaller swing for anyone who merely saw it.</summary>
        public int WitnessAffinity { get; }

        /// <summary>Applied only when the player is the actor. Karma is lawfulness, not morality.</summary>
        public int Karma { get; }

        public int Fame { get; }
    }

    /// <summary>
    /// The default event-to-consequence table. Values are deliberately modest: affinity is a
    /// vanilla currency and the procedural layer should nudge it, not flood it.
    /// </summary>
    public static class ConsequenceProfiles
    {
        private static readonly Dictionary<WorldEventType, ConsequenceProfile> Table =
            new Dictionary<WorldEventType, ConsequenceProfile>
            {
                { WorldEventType.Met, new ConsequenceProfile("met", MemoryWeight.Trivial, 0) },
                { WorldEventType.Conversed, new ConsequenceProfile("spoke_with", MemoryWeight.Routine, 1) },
                { WorldEventType.Helped, new ConsequenceProfile("was_helped", MemoryWeight.Notable, 12) },
                { WorldEventType.Harmed, new ConsequenceProfile("was_harmed", MemoryWeight.Important, -20, -4) },
                { WorldEventType.Threatened, new ConsequenceProfile("was_threatened", MemoryWeight.Important, -18, -6) },
                { WorldEventType.Bribed, new ConsequenceProfile("took_a_bribe", MemoryWeight.Notable, 6, -2) },
                { WorldEventType.Deceived, new ConsequenceProfile("was_lied_to", MemoryWeight.Routine, 0) },
                { WorldEventType.DeceptionExposed, new ConsequenceProfile("caught_me_lying", MemoryWeight.Important, -15, -5) },
                { WorldEventType.PromiseMade, new ConsequenceProfile("was_promised", MemoryWeight.Notable, 2) },
                { WorldEventType.PromiseBroken, new ConsequenceProfile("was_let_down", MemoryWeight.Important, -22, -3) },
                { WorldEventType.Theft, new ConsequenceProfile("was_robbed", MemoryWeight.Important, -25, -8, karma: -3) },
                { WorldEventType.ItemReturned, new ConsequenceProfile("got_property_back", MemoryWeight.Important, 20, 3, karma: 1) },
                { WorldEventType.ItemGiven, new ConsequenceProfile("received_a_gift", MemoryWeight.Notable, 8) },
                { WorldEventType.Trespass, new ConsequenceProfile("caught_me_trespassing", MemoryWeight.Notable, -10, -4, karma: -1) },
                { WorldEventType.Attacked, new ConsequenceProfile("was_attacked", MemoryWeight.Defining, -45, -12, karma: -5) },
                { WorldEventType.Killed, new ConsequenceProfile("killed_someone", MemoryWeight.Defining, -80, -30, karma: -12, fame: 2) },
                { WorldEventType.Rescued, new ConsequenceProfile("was_rescued", MemoryWeight.Defining, 35, 8, karma: 3, fame: 3) },
                { WorldEventType.Captured, new ConsequenceProfile("was_captured", MemoryWeight.Defining, -40, -5) },
                { WorldEventType.DebtCreated, new ConsequenceProfile("debt_created", MemoryWeight.Important, -6, 0) },
                { WorldEventType.DebtPaid, new ConsequenceProfile("debt_settled", MemoryWeight.Important, 18, 0, karma: 1) },
                { WorldEventType.SecretLearned, new ConsequenceProfile("learned_a_secret", MemoryWeight.Routine, 0) },
                { WorldEventType.SecretRevealed, new ConsequenceProfile("exposed_a_secret", MemoryWeight.Important, -30, 0) },
                { WorldEventType.FalseAccusation, new ConsequenceProfile("was_falsely_accused", MemoryWeight.Defining, -35, -6, karma: -4) },

                // Being accused truthfully but unprovably still stings; it is not being lied about.
                { WorldEventType.AccusationMade, new ConsequenceProfile("was_accused", MemoryWeight.Important, -12) },
                { WorldEventType.AccusationRejected, new ConsequenceProfile("accusation_dismissed", MemoryWeight.Routine, 0) },
                { WorldEventType.InquiryOpened, new ConsequenceProfile("under_inquiry", MemoryWeight.Important, -8) },
                { WorldEventType.EvidenceCreated, new ConsequenceProfile("created_evidence", MemoryWeight.Routine, 0) },
                { WorldEventType.EvidenceDestroyed, new ConsequenceProfile("destroyed_evidence", MemoryWeight.Important, -12, -3, karma: -1) },
                // Making something is ordinary life. It is on the record because provenance
                // matters later, not because anybody's opinion of anybody moves.
                { WorldEventType.GoodsProduced, new ConsequenceProfile("made_something", MemoryWeight.Trivial, 0) },
                // Devotion is ordinary life in Elin and nobody's opinion of anybody moves for
                // it. It is on the record because a petition later spends it, and because what
                // somebody gave up is worth being able to look back at.
                { WorldEventType.OfferingMade, new ConsequenceProfile("made_an_offering", MemoryWeight.Routine, 0) },
                // Being taken into somebody's house is one of the largest things that can happen
                // to a person, and it is public: the neighbours see who is suddenly living at the
                // player's Home, and the player's name travels for it. Karma stays zero because
                // the ledger does not know whether the household just did a kindness or harboured
                // a fugitive - that is a judgment, and judgments wait for BQ-046.
                { WorldEventType.TakenIn, new ConsequenceProfile("was_taken_in", MemoryWeight.Defining, 28, 3, fame: 2) },
                { WorldEventType.CrimeWitnessed, new ConsequenceProfile("witnessed_a_crime", MemoryWeight.Notable, 0) },
                { WorldEventType.CrimeReported, new ConsequenceProfile("reported_to_authorities", MemoryWeight.Important, -25, 0, karma: 2) },
                { WorldEventType.RumorSpread, new ConsequenceProfile("heard_a_rumor", MemoryWeight.Routine, 0) },

                // Notable rather than routine: hearing the wrong name is the moment a person
                // acquires a belief that will make them act against somebody who did nothing, and
                // a memory they can later be shown to have been wrong about. No standing changes
                // hands - nobody has done anything to anybody yet.
                { WorldEventType.RumorDistorted, new ConsequenceProfile("heard_it_wrong", MemoryWeight.Notable, 0) },
                // Somebody leaving, and somebody coming back. Nobody's standing moves: the ledger
                // does not know whether they fled, were sent for, or shut the counter to go to a
                // funeral, and a departure is not something done *to* anybody. What earns them a
                // place in history is that the town noticed - a shop that is shut is the reason a
                // player is looking for another route, and a person who came back is the answer
                // to where they had gone.
                { WorldEventType.WentAbsent, new ConsequenceProfile("went_away", MemoryWeight.Notable, 0) },
                { WorldEventType.Returned, new ConsequenceProfile("came_back", MemoryWeight.Notable, 0) },
                { WorldEventType.Recruited, new ConsequenceProfile("joined_me", MemoryWeight.Defining, 10) },
                { WorldEventType.OrganizationJoined, new ConsequenceProfile("joined_organization", MemoryWeight.Notable, 4, 1) },
                { WorldEventType.OrganizationBetrayed, new ConsequenceProfile("betrayed_organization", MemoryWeight.Defining, -50, -12, karma: -2) },
                { WorldEventType.SiteDiscovered, new ConsequenceProfile("discovered_site", MemoryWeight.Routine, 0, 0, fame: 1) },
                { WorldEventType.SiteCleared, new ConsequenceProfile("cleared_site", MemoryWeight.Important, 8, 3, fame: 2) },
                { WorldEventType.ThreadEscalated, new ConsequenceProfile("thread_escalated", MemoryWeight.Routine, 0) },
                { WorldEventType.ThreadResolved, new ConsequenceProfile("thread_resolved", MemoryWeight.Notable, 4, 1) },

                // Being turned down costs the asker nothing with the person who turned them down.
                // A guild officer who will not commit his people has not been wronged and has not
                // wronged anybody, and moving affinity here would make an honest no into a slight.
                { WorldEventType.RequestDeclined, new ConsequenceProfile("turned_down_a_request", MemoryWeight.Routine, 0) }
            };

        private static readonly HashSet<WorldEventType> ProfileExemptions = new HashSet<WorldEventType>();

        public static ConsequenceProfile For(WorldEventType type)
        {
            Table.TryGetValue(type, out ConsequenceProfile profile);
            return profile;
        }

        public static bool HasProfileOrExemption(WorldEventType type)
        {
            return Table.ContainsKey(type) || ProfileExemptions.Contains(type);
        }

        public static IReadOnlyCollection<WorldEventType> Exemptions => ProfileExemptions;

        public static IEnumerable<WorldEventType> MissingProfiles()
        {
            foreach (WorldEventType type in Enum.GetValues(typeof(WorldEventType)))
            {
                if (!HasProfileOrExemption(type))
                {
                    yield return type;
                }
            }
        }
    }
}
