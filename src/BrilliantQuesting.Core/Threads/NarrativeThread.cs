using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Threads
{
    public enum ThreadState
    {
        /// <summary>Exists in the world but nothing has surfaced it to anyone yet.</summary>
        Latent,

        /// <summary>Live: escalation steps are due and the director may expose it.</summary>
        Active,

        /// <summary>Nothing pending, but it can wake up if the world touches it again.</summary>
        Dormant,

        /// <summary>Closed as a live matter because another thread now carries it.</summary>
        Inherited,

        /// <summary>Preserved for inspection, but no longer allowed to advance or surface.</summary>
        Quarantined,

        Resolved
    }

    /// <summary>
    /// An unresolved causal chain. Quests, rumours and encounters are projections of a thread;
    /// the thread itself keeps running whether or not the player ever accepted anything, which is
    /// the difference between a world that continues and a quest log that waits.
    /// </summary>
    public sealed class NarrativeThread
    {
        public NarrativeThread(EntityId id, string archetypeId, GameTime createdAt)
        {
            Id = id;
            ArchetypeId = archetypeId;
            CreatedAt = createdAt;
            LastAdvancedAt = createdAt;
            ParticipantIds = new List<EntityId>();
            SiteIds = new List<EntityId>();
            FactIds = new List<EntityId>();
            OpenQuestions = new List<string>();
            GenerationCauses = new List<string>();
            Escalation = new List<EscalationStep>();
            CompletedSteps = new List<string>();
            StoryletFirings = new List<StoryletFiring>();
            State = ThreadState.Latent;
        }

        public EntityId Id { get; }

        public string ArchetypeId { get; }

        public EntityId OriginEventId { get; set; }

        public EntityId ParentThreadId { get; set; }

        public EntityId SuccessorThreadId { get; set; }

        public GameTime CreatedAt { get; }

        public GameTime LastAdvancedAt { get; set; }

        public List<EntityId> ParticipantIds { get; }

        public List<EntityId> SiteIds { get; }

        public List<EntityId> FactIds { get; }

        /// <summary>
        /// What the player still does not know, phrased as questions. The journal shows these
        /// rather than the hidden truth - the log is a record of the investigation, not a spoiler.
        /// </summary>
        public List<string> OpenQuestions { get; }

        /// <summary>
        /// Inspector-only provenance for why generation selected this matter. These lines are not
        /// player knowledge and must not be projected as journal questions.
        /// </summary>
        public List<string> GenerationCauses { get; }

        /// <summary>Scheduled deterioration. Milestones, not a countdown to failure.</summary>
        public List<EscalationStep> Escalation { get; }

        public List<string> CompletedSteps { get; }

        /// <summary>
        /// Durable record of social/dramatic presentations that happened for this thread. The
        /// content that defined the storylet is not saved; only the stable ids and bindings that
        /// became history are.
        /// </summary>
        public List<StoryletFiring> StoryletFirings { get; }

        /// <summary>0..100. How badly this wants to become someone's problem.</summary>
        public int Tension { get; set; }

        public int Importance { get; set; }

        public ThreadState State { get; set; }

        /// <summary>Set when resolved, so consequences can say which ending actually happened.</summary>
        public string Resolution { get; set; }

        /// <summary>Inspector-facing lifecycle note: inherited, merged or quarantined and why.</summary>
        public string LifecycleReason { get; set; }

        public bool IsLive => State == ThreadState.Latent || State == ThreadState.Active;

        public EscalationStep NextStep(GameTime now)
        {
            for (int i = 0; i < Escalation.Count; i++)
            {
                EscalationStep step = Escalation[i];
                if (CompletedSteps.Contains(step.Id))
                {
                    continue;
                }

                if (now.DaysSince(CreatedAt) >= step.DayOffset)
                {
                    return step;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return ArchetypeId + " [" + State + ", tension " + Tension + ", " + CompletedSteps.Count + "/" + Escalation.Count + " steps]";
        }
    }

    /// <summary>
    /// One scheduled development: "day 4, the captors relocate if pressure is rising". Data, so a
    /// thread can be saved and resumed; the archetype's handler decides what it actually does.
    /// </summary>
    public sealed class EscalationStep
    {
        public EscalationStep(string id, long dayOffset, string description)
        {
            Id = id;
            DayOffset = dayOffset;
            Description = description;
        }

        public string Id { get; }

        public long DayOffset { get; }

        public string Description { get; }

        public override string ToString() => "day +" + DayOffset + ": " + Id;
    }

    public sealed class StoryletFiring
    {
        public StoryletFiring(string storyletId, EntityId focusFactId, GameTime firedAt)
        {
            StoryletId = storyletId;
            FocusFactId = focusFactId;
            FiredAt = firedAt;
            RoleBindings = new Dictionary<string, EntityId>();
            BeatIds = new List<string>();
            ConsequenceHookIds = new List<string>();
        }

        public string StoryletId { get; }

        public EntityId FocusFactId { get; }

        public GameTime FiredAt { get; }

        public Dictionary<string, EntityId> RoleBindings { get; }

        public List<string> BeatIds { get; }

        public List<string> ConsequenceHookIds { get; }
    }
}
