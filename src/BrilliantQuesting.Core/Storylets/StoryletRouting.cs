using System;
using System.Collections.Generic;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// What a beat's act is about, as a reference to the scene rather than as a matter of its own
    /// (BQ-146).
    ///
    /// Three values, and the shortness is the argument. A storylet already names one focus fact
    /// and its casting already names the people; a beat that could name a *fourth* thing would be
    /// content inventing a matter, which is the one thing storylets are forbidden to do. So a beat
    /// says which of the things the scene already holds its act is about, and nothing else.
    ///
    /// There is deliberately no free-text purpose. <c>ActionBinding.Purpose</c> is a sentence, and
    /// a sentence in a storylet is authored prose in the one file that must not contain any -
    /// every act whose profile needs content gets it from the focus, which is a proposition and
    /// therefore already satisfies the rule.
    /// </summary>
    public enum BeatContentSource
    {
        /// <summary>The focus fact, and the object it turns on. The ordinary case.</summary>
        Focus = 0,

        /// <summary>Only the object the focus fact is about - the ring rather than the theft.</summary>
        FocusObject = 1,

        /// <summary>Nothing. For the acts whose matter is carried by what they answer.</summary>
        None = 2
    }

    /// <summary>
    /// One thing the actor holding this beat's speaking role might be trying to communicate.
    ///
    /// A beat lists several of these and <see cref="ActorIntent"/> chooses between them from the
    /// speaker's own state. That is the whole shape of "storylets decide what is happening,
    /// characters decide what they are trying to say": the content names the moves that would make
    /// sense here, and never which one is made.
    ///
    /// It carries a meaning and no wording. The act is a <see cref="SpeechActType"/>, the referent
    /// is a role in this scene, and the matter is a reference to the focus - so a beat can no more
    /// contain a sentence than a <see cref="SpeechAct"/> can.
    /// </summary>
    public sealed class BeatIntention
    {
        public BeatIntention(SpeechActType act, string referentRole = null, BeatContentSource content = BeatContentSource.Focus)
        {
            Act = act;
            ReferentRole = referentRole ?? string.Empty;
            Content = content;
        }

        public SpeechActType Act { get; }

        /// <summary>
        /// Which role the content is about, or empty to let the act's own profile decide - which
        /// for an admission or an apology means the speaker.
        /// </summary>
        public string ReferentRole { get; }

        public BeatContentSource Content { get; }

        public override string ToString()
        {
            return Act + (ReferentRole.Length == 0 ? string.Empty : " about " + ReferentRole);
        }
    }

    /// <summary>
    /// An uncertainty the beat wants settled, expressed as an existing check profile.
    ///
    /// The rule the roadmap states and this type enforces structurally: a check answers a
    /// question. <see cref="Question"/> is required and is a slug rather than a sentence, so a
    /// beat cannot roll dice for atmosphere - somebody has to have been able to name what is in
    /// doubt. The profile is one of <c>ProceduralCheckProfiles</c>' and is validated at compile
    /// time, so a storylet cannot mint a parallel skill system by naming a check nobody built.
    /// </summary>
    public sealed class BeatCheck
    {
        public BeatCheck(string profileId, string actorRole, string targetRole, string question)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                throw new ArgumentException("Beat check needs a profile.", nameof(profileId));
            }

            if (string.IsNullOrEmpty(actorRole))
            {
                throw new ArgumentException("Beat check needs somebody attempting it.", nameof(actorRole));
            }

            if (string.IsNullOrEmpty(question))
            {
                throw new ArgumentException("Beat check needs the uncertainty it settles.", nameof(question));
            }

            ProfileId = profileId;
            ActorRole = actorRole;
            TargetRole = targetRole ?? string.Empty;
            Question = question;
        }

        public string ProfileId { get; }

        public string ActorRole { get; }

        /// <summary>Who resists, or empty for a check nobody is on the other side of.</summary>
        public string TargetRole { get; }

        /// <summary>What is actually in doubt, as a slug. Inspector-facing; nothing branches on it.</summary>
        public string Question { get; }
    }

    /// <summary>
    /// What has to have happened for a route to be taken.
    ///
    /// Closed, small, and every member is a fact the beat itself produced - what the actor decided
    /// to do, and how the check went. Nothing here reads world state: a route that could ask
    /// arbitrary questions about the world would be the beginning of a scripting language, and the
    /// place to ask those questions is a beat's own <c>requires</c>, which speaks the storylet
    /// precondition vocabulary that already exists.
    /// </summary>
    public enum BeatTrigger
    {
        /// <summary>Whatever happened. The fallback every beat should end with.</summary>
        Always = 0,

        /// <summary>The actor decided to say something and it was well formed.</summary>
        Spoke = 1,

        /// <summary>Nobody spoke: no intention survived, or the act could not be composed.</summary>
        Silent = 2,

        CheckPass = 3,
        CheckFail = 4,
        CheckCriticalPass = 5,
        CheckCriticalFail = 6
    }

    /// <summary>
    /// Where the scene goes next, and why.
    ///
    /// Routes are tried in authored order and the first whose trigger holds wins, so a beat reads
    /// top to bottom like the decision it is. A route either names the next beat or names a
    /// resolution the storylet declared; naming neither ends the scene without a resolution, which
    /// content validation refuses because a scene that can stop nowhere in particular is a scene
    /// nobody can write a consequence for.
    /// </summary>
    public sealed class BeatRoute
    {
        public BeatRoute(BeatTrigger when, SpeechActType? act, string to, string ends)
        {
            When = when;
            Act = act;
            To = to ?? string.Empty;
            Ends = ends ?? string.Empty;
        }

        public BeatTrigger When { get; }

        /// <summary>
        /// Narrows the trigger to one chosen act, or null for any. This is how a beat routes on
        /// what the character decided rather than on what the author hoped they would decide - the
        /// same beat sends a denial one way and an admission another.
        /// </summary>
        public SpeechActType? Act { get; }

        public string To { get; }

        public string Ends { get; }

        public bool IsTerminal => To.Length == 0;

        public override string ToString()
        {
            string trigger = Act.HasValue ? When + "/" + Act.Value : When.ToString();
            return trigger + " -> " + (IsTerminal ? "ends " + Ends : To);
        }
    }

    /// <summary>
    /// One authoritative change a beat asks the world to record.
    ///
    /// Two shapes, and the difference is whether anything happens. A hook with no
    /// <see cref="Event"/> is a marker: it is written onto the firing and read by whoever cares,
    /// exactly as every storylet hook has been since BQ-065. A hook that names a
    /// <see cref="WorldEventType"/> is applied - and applied the only way anything is applied in
    /// this codebase, by appending to the event ledger so that <c>ConsequenceEngine</c> does what
    /// it already does with affinity, memory, knowledge propagation and thread tension.
    ///
    /// <b>Nothing here is a second consequence system.</b> The vocabulary is
    /// <see cref="WorldEventType"/>'s, the arithmetic is <c>ConsequenceProfiles</c>', and the
    /// participants are roles this scene already cast. A storylet can say "an accusation was made
    /// here, by this role, against that one"; it cannot say what an accusation costs, and it
    /// cannot invent an effect that is not an event.
    /// </summary>
    public sealed class BeatConsequence
    {
        public BeatConsequence(string hookId, WorldEventType? eventType, string actorRole, string targetRole, double magnitude)
        {
            if (string.IsNullOrEmpty(hookId))
            {
                throw new ArgumentException("Consequence hook id is required.", nameof(hookId));
            }

            HookId = hookId;
            Event = eventType;
            ActorRole = actorRole ?? string.Empty;
            TargetRole = targetRole ?? string.Empty;
            Magnitude = magnitude;
        }

        public string HookId { get; }

        /// <summary>What history should record, or null for a marker that records nothing.</summary>
        public WorldEventType? Event { get; }

        public string ActorRole { get; }

        public string TargetRole { get; }

        /// <summary>0..1 severity, handed to the ledger unchanged.</summary>
        public double Magnitude { get; }

        public override string ToString()
        {
            return HookId + (Event.HasValue ? " (" + Event.Value + ")" : string.Empty);
        }
    }
}
