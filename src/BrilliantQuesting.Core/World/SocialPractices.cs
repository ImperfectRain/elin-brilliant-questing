using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The contextual norms CD 16 names, as the small closed vocabulary the world can actually
    /// derive.
    ///
    /// Each member is the norm rather than the ceremony, because the ceremony is not readable and
    /// the norm is. Elin has no funeral object to ask about; it does have a death recorded in a
    /// place and people standing there who were tied to whoever died, and that is what makes theft
    /// during it land differently. Naming the derivable thing keeps the vocabulary from becoming a
    /// list of occasions nobody can detect.
    ///
    /// Closed on the same terms as <see cref="IdentityDomain"/>: a new member arrives with the
    /// consumer that needs it and the reads that establish it, never as a category somebody might
    /// want later.
    /// </summary>
    public enum SocialPracticeKind
    {
        /// <summary>CD 16 "Shop". Trade is on offer here and the person offering it controls it.</summary>
        Commerce,

        /// <summary>CD 16 "Funeral". Somebody died here lately and their people are present.</summary>
        Mourning,

        /// <summary>CD 16 "Festival". A public contest was judged here lately.</summary>
        Contest,

        /// <summary>CD 16 "Guild meeting". Several people answerable to one body are here.</summary>
        Assembly,

        /// <summary>CD 16 "Home dinner". This is a household, and people who live in it are here.</summary>
        Household
    }

    /// <summary>
    /// One practice in force at one place, how strongly, and the reads that put it there.
    ///
    /// <see cref="Strength"/> is 0..1 and means "how firmly this norm is holding here right now" -
    /// a wake on the day is not a wake three days later, and one counter open is not a market.
    /// It is never a probability that the practice exists: a holding is created only when the
    /// reads behind it actually fired, and a practice nothing supports is simply absent.
    /// </summary>
    public sealed class SocialPracticeHolding
    {
        private readonly List<string> _sources;

        internal SocialPracticeHolding(SocialPracticeKind kind, double strength, List<string> sources)
        {
            Kind = kind;
            Strength = strength < 0.0 ? 0.0 : strength > 1.0 ? 1.0 : strength;
            _sources = sources ?? new List<string>();
        }

        public SocialPracticeKind Kind { get; }

        public double Strength { get; }

        /// <summary>Never empty. A holding nothing supports is not created at all.</summary>
        public IReadOnlyList<string> Sources => _sources;

        public string Describe()
        {
            return Name(Kind) + " " + Strength.ToString("0.00") + " (" + string.Join("; ", _sources.ToArray()) + ")";
        }

        /// <summary>Spelled out, because a trace line is read by a person.</summary>
        public static string Name(SocialPracticeKind kind) => kind.ToString().ToLowerInvariant();

        public override string ToString() => Describe();
    }

    /// <summary>
    /// What the practices in force make of one kind of event, and why.
    ///
    /// <see cref="Aggravation"/> is -1..1. Positive means the room takes it harder than it would
    /// anywhere else; negative means the place licenses it and the room takes it more lightly.
    /// Zero - the answer everywhere no practice speaks to this event - leaves every consumer
    /// exactly where it was before this step existed.
    /// </summary>
    public sealed class SocialNormReading
    {
        internal static readonly SocialNormReading Silent = new SocialNormReading(0.0, new List<string>());

        private readonly List<string> _terms;

        internal SocialNormReading(double aggravation, List<string> terms)
        {
            Aggravation = aggravation < -1.0 ? -1.0 : aggravation > 1.0 ? 1.0 : aggravation;
            _terms = terms ?? new List<string>();
        }

        public double Aggravation { get; }

        /// <summary>Which holding contributed what, named. Empty when no practice speaks to the event.</summary>
        public IReadOnlyList<string> Terms => _terms;

        public bool IsSilent => _terms.Count == 0;

        public string Describe()
        {
            return _terms.Count == 0
                ? "no practice speaks to this"
                : Aggravation.ToString("+0.00;-0.00;0.00") + " (" + string.Join("; ", _terms.ToArray()) + ")";
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Every practice in force at one place, at one moment.
    ///
    /// An empty reading is the ordinary answer and a real one: most of the world most of the time
    /// is an unattended corridor, and the whole point of CD 16 is that theft there is not theft at
    /// a wake. <see cref="Ordinary"/> is what a caller gets with no zone, no game and no history,
    /// and it changes nothing downstream.
    /// </summary>
    public sealed class SocialPracticeReading
    {
        /// <summary>Nowhere in particular: no norm in force, and no effect on anything.</summary>
        public static readonly SocialPracticeReading Ordinary =
            new SocialPracticeReading(EntityId.None, new List<SocialPracticeHolding>());

        private readonly List<SocialPracticeHolding> _held;

        internal SocialPracticeReading(EntityId zoneId, List<SocialPracticeHolding> held)
        {
            ZoneId = zoneId;
            _held = held ?? new List<SocialPracticeHolding>();
        }

        public EntityId ZoneId { get; }

        /// <summary>What is in force here, strongest first and then in vocabulary order.</summary>
        public IReadOnlyList<SocialPracticeHolding> Held => _held;

        /// <summary>True where no norm is in force. Not a defect, and the commonest answer.</summary>
        public bool IsOrdinary => _held.Count == 0;

        public double StrengthOf(SocialPracticeKind kind)
        {
            for (int i = 0; i < _held.Count; i++)
            {
                if (_held[i].Kind == kind)
                {
                    return _held[i].Strength;
                }
            }

            return 0.0;
        }

        public bool Holds(SocialPracticeKind kind) => StrengthOf(kind) > 0.0;

        /// <summary>
        /// How much harder, or more lightly, this place takes that kind of event.
        ///
        /// Contributions compose and are clamped: two norms that both condemn the same act do not
        /// add up past the strongest reaction the event already carries, and a norm that licenses
        /// what another condemns is a place where the two genuinely disagree.
        /// </summary>
        public SocialNormReading ReadingOf(WorldEventType type)
        {
            if (_held.Count == 0)
            {
                return SocialNormReading.Silent;
            }

            double total = 0.0;
            List<string> terms = new List<string>();
            for (int i = 0; i < _held.Count; i++)
            {
                SocialPracticeHolding holding = _held[i];
                double bearing;
                if (!SocialPractices.Bearing(holding.Kind, type, out bearing))
                {
                    continue;
                }

                double contribution = bearing * holding.Strength;
                total += contribution;
                terms.Add(SocialPracticeHolding.Name(holding.Kind) + " "
                          + contribution.ToString("+0.00;-0.00;0.00"));
            }

            return terms.Count == 0 ? SocialNormReading.Silent : new SocialNormReading(total, terms);
        }

        /// <summary>Every holding as one line each, reads named, for the inspector and the live log.</summary>
        public IReadOnlyList<string> Explain()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < _held.Count; i++)
            {
                lines.Add("practice " + _held[i].Describe());
            }

            return lines;
        }

        public override string ToString()
        {
            if (IsOrdinary)
            {
                return "no practice in force";
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < _held.Count; i++)
            {
                lines.Add(_held[i].Describe());
            }

            return string.Join(", ", lines.ToArray());
        }
    }

    /// <summary>
    /// BQ-084. What the place an act happened in makes of it (CD 16).
    ///
    /// Versu's lightweight contextual norms, derived rather than declared. A practice is never set
    /// by a caller, never attached to a zone by hand and never named after a town: it is read, per
    /// pass, from three things the world already holds - <b>where</b> this is, <b>what has lately
    /// happened here</b>, and <b>who is standing in it</b> - and where those reads say nothing the
    /// answer is <see cref="SocialPracticeReading.Ordinary"/>, which is the honest description of
    /// most of the map and leaves every consumer exactly where it was.
    ///
    /// Four rules hold it together.
    ///
    /// <b>Who is present is the stronger input, and it is an identity read.</b> Trade is on offer
    /// here because BQ-145 says somebody present provides a service and the game says it is open;
    /// a body is meeting here because people the registry records as its members are here. Nothing
    /// in this file knows what a shopkeeper is or who counts as clergy - that vocabulary has one
    /// owner (<see cref="IdentityAffordances"/>) and this is a consumer of it.
    ///
    /// <b>An unread facet contributes nothing.</b> A build that cannot see whether a counter is
    /// open has not told anybody the shop is shut, and a practice is never asserted because BQ
    /// guessed somebody's role (D017). Every read here fails toward "no practice", never toward a
    /// default one.
    ///
    /// <b>A practice modulates a reaction; it never invents one.</b> It changes how hard the room
    /// takes something the room already reacts to. An event type nobody witnesses a reaction to
    /// stays unwitnessed however solemn the room is, because the alternative is a norm minting
    /// consequences out of context alone.
    ///
    /// <b>A practice is not a verdict.</b> Karma and fame are the law's answer and stay BQ-046's
    /// (see <c>EventTags.Observed</c>); what a loss cost the person who suffered it is theirs and
    /// does not depend on the company. What a practice changes is what the room makes of it.
    ///
    /// Nothing derived here is persisted. It is a projection of live reads and of the ledger, on
    /// the same terms as the identity affordances it consumes, and is recomputed per pass.
    /// </summary>
    public static class SocialPractices
    {
        /// <summary>How long a death keeps a place solemn. Whole days, decaying across them.</summary>
        public const int MourningDays = 3;

        /// <summary>How long a judged contest keeps a place festive.</summary>
        public const int ContestDays = 1;

        /// <summary>Two of one body in one room is a meeting; one of them is a person standing there.</summary>
        private const int AssemblyQuorum = 2;

        /// <summary>Below this a holding has decayed to nothing worth reporting.</summary>
        private const double Faintest = 0.05;

        /// <summary>
        /// What each practice makes of each kind of event, straight out of CD 16's five norms.
        ///
        /// Positive sharpens the room's reaction, negative licenses the act. The table is short on
        /// purpose: an entry earns its place by being one of the norms the design actually states,
        /// and by naming an event the consequence layer already gives the room a reaction to -
        /// anything else would be a norm with no way to be felt.
        /// </summary>
        private static readonly Dictionary<SocialPracticeKind, Dictionary<WorldEventType, double>> Bearings =
            new Dictionary<SocialPracticeKind, Dictionary<WorldEventType, double>>
            {
                // "Pay, do not steal, merchant controls trade, haggling is acceptable." Haggling is
                // absent because haggling is not an event; what the norm forbids is taking instead
                // of paying, going where custom does not, and being caught working a lie in a place
                // whose whole business is a bargain.
                {
                    SocialPracticeKind.Commerce,
                    new Dictionary<WorldEventType, double>
                    {
                        { WorldEventType.Theft, 1.0 },
                        { WorldEventType.Trespass, 0.6 },
                        { WorldEventType.DeceptionExposed, 0.5 }
                    }
                },

                // "Respect corpse, avoid obvious looting, reduced joking tolerance." The heaviest
                // of the five, and the one that gives a bystander with no stake of their own a
                // reason to mind: helping yourself where somebody is being mourned is an offence
                // against everybody standing there, not only against whoever owned the thing.
                {
                    SocialPracticeKind.Mourning,
                    new Dictionary<WorldEventType, double>
                    {
                        { WorldEventType.Theft, 1.0 },
                        { WorldEventType.Attacked, 1.0 },
                        { WorldEventType.Harmed, 0.8 },
                        { WorldEventType.Threatened, 0.8 },
                        { WorldEventType.Killed, 0.6 }
                    }
                },

                // "Boasting and competition are acceptable; minor cheating may be culturally
                // tolerated." The only practice that softens anything, and deliberately narrow:
                // bluster, a bout and a caught trick are festival business, and theft, assault and
                // killing are not excused by a crowd being in a good mood.
                {
                    SocialPracticeKind.Contest,
                    new Dictionary<WorldEventType, double>
                    {
                        { WorldEventType.Threatened, -0.6 },
                        { WorldEventType.Harmed, -0.5 },
                        { WorldEventType.DeceptionExposed, -0.4 }
                    }
                },

                // "Rank matters; outsider access may be restricted." What a meeting minds is
                // somebody being where the body did not admit them, leaning on people in front of
                // their own, and selling the body out.
                {
                    SocialPracticeKind.Assembly,
                    new Dictionary<WorldEventType, double>
                    {
                        { WorldEventType.Trespass, 0.8 },
                        { WorldEventType.Threatened, 0.6 },
                        { WorldEventType.OrganizationBetrayed, 0.6 }
                    }
                },

                // "Host obligations, food sharing, guest etiquette, residents have standing."
                // A household reads a broken word harder than a street does, because a household
                // is where the word was given over somebody's table.
                {
                    SocialPracticeKind.Household,
                    new Dictionary<WorldEventType, double>
                    {
                        { WorldEventType.Theft, 1.0 },
                        { WorldEventType.Trespass, 0.8 },
                        { WorldEventType.PromiseBroken, 0.6 }
                    }
                }
            };

        internal static bool Bearing(SocialPracticeKind kind, WorldEventType type, out double bearing)
        {
            bearing = 0.0;
            Dictionary<WorldEventType, double> table;
            return Bearings.TryGetValue(kind, out table) && table.TryGetValue(type, out bearing);
        }

        /// <summary>
        /// What norms are in force at one place, right now.
        ///
        /// Both a world and a game are required, because every practice needs presence and only
        /// the game can say who is standing anywhere. With either missing the answer is
        /// <see cref="SocialPracticeReading.Ordinary"/> - not a guess about an empty room, but the
        /// statement that nothing was read.
        /// </summary>
        public static SocialPracticeReading Read(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId zoneId,
            GameTime now)
        {
            if (world == null || vanilla == null || zoneId.IsNone)
            {
                return SocialPracticeReading.Ordinary;
            }

            List<EntityId> present = Present(world, vanilla, zoneId);
            if (present.Count == 0)
            {
                // Nobody here holds anything. An unattended place is exactly the contrast CD 16
                // draws, and it is reported as itself rather than as a weaker version of a norm.
                return new SocialPracticeReading(zoneId, new List<SocialPracticeHolding>());
            }

            List<SocialPracticeHolding> held = new List<SocialPracticeHolding>();
            Add(held, ReadCommerce(world, vanilla, present));
            Add(held, ReadMourning(world, vanilla, zoneId, present, now));
            Add(held, ReadContest(world, zoneId, present, now));
            Add(held, ReadAssembly(world, present));
            Add(held, ReadHousehold(world, vanilla, zoneId, present));

            held.Sort(delegate (SocialPracticeHolding a, SocialPracticeHolding b)
            {
                int byStrength = b.Strength.CompareTo(a.Strength);
                return byStrength != 0 ? byStrength : ((int)a.Kind).CompareTo((int)b.Kind);
            });

            return new SocialPracticeReading(zoneId, held);
        }

        private static void Add(List<SocialPracticeHolding> held, SocialPracticeHolding holding)
        {
            if (holding != null && holding.Strength >= Faintest)
            {
                held.Add(holding);
            }
        }

        /// <summary>
        /// Who is here and could hold a norm at all.
        ///
        /// The player is excluded because a practice is what the room makes of an act, and the
        /// same read of presence the settlement profile uses is reused rather than reinvented.
        /// Social agency is the gate: a norm is something a participant in ordinary social life
        /// holds, and an actor whose agency this build cannot read contributes nothing rather than
        /// being counted as a person by default.
        /// </summary>
        private static List<EntityId> Present(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            List<EntityId> present = new List<EntityId>();
            IReadOnlyList<EntityId> inZone = vanilla.GetCharactersInZone(zoneId);
            for (int i = 0; i < inZone.Count; i++)
            {
                EntityId actor = inZone[i];
                if (actor.IsNone
                    || actor == vanilla.PlayerId
                    || present.Contains(actor)
                    || !vanilla.IsAlive(actor)
                    || vanilla.GetSocialAgency(actor) != SocialAgency.Full
                    || world.Registry.GetNpc(actor) == null)
                {
                    continue;
                }

                present.Add(actor);
            }

            return present;
        }

        /// <summary>
        /// Trade on offer, as BQ-145 derives it and the game currently answers it.
        ///
        /// <c>AvailableNow</c> rather than <c>IsProvider</c> on purpose: the shopkeeper is still
        /// the shopkeeper when the shop is shut, and a shut shop is not a shop practice. An
        /// unread availability answers false to both, so a build that cannot see opening state
        /// derives no commerce here instead of assuming a counter is open.
        /// </summary>
        private static SocialPracticeHolding ReadCommerce(
            NarrativeWorldState world,
            IVanillaState vanilla,
            List<EntityId> present)
        {
            List<string> sources = new List<string>();
            for (int i = 0; i < present.Count; i++)
            {
                IdentityAffordances identity = IdentityAffordances.Of(world.Registry.GetNpc(present[i]), vanilla);
                if (!identity.Service.AvailableNow)
                {
                    continue;
                }

                sources.Add(world.Registry.NameOf(present[i]) + " has a service on offer ("
                            + identity.Service.Source.Describe() + ")");
            }

            return sources.Count == 0
                ? null
                : new SocialPracticeHolding(SocialPracticeKind.Commerce, Crowded(0.5, 0.2, 0.9, sources.Count), sources);
        }

        /// <summary>
        /// A death here lately, and people here who were tied to whoever died.
        ///
        /// Elin has no funeral to ask about, so this asks the two questions a funeral is the
        /// answer to. Both halves are required: a killing in an empty street is history and not a
        /// norm, and a room full of people is not solemn because somebody died two towns over. The
        /// tie is read off the relationship graph, so nobody is a mourner for their occupation.
        /// </summary>
        private static SocialPracticeHolding ReadMourning(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId zoneId,
            List<EntityId> present,
            GameTime now)
        {
            SocialPracticeHolding best = null;
            List<WorldEvent> deaths = RecentHere(world, zoneId, WorldEventType.Killed, now, MourningDays);
            for (int i = 0; i < deaths.Count; i++)
            {
                WorldEvent death = deaths[i];
                if (death.Target.IsNone || vanilla.IsAlive(death.Target))
                {
                    continue;
                }

                List<string> sources = new List<string>();
                for (int m = 0; m < present.Count; m++)
                {
                    RelationshipEdge tie = world.Relationships.Find(present[m], death.Target);
                    if (tie == null || tie.Sentiment <= 0)
                    {
                        continue;
                    }

                    sources.Add(world.Registry.NameOf(present[m]) + " was " + tie.Kind + " to "
                                + world.Registry.NameOf(death.Target));
                }

                if (sources.Count == 0)
                {
                    continue;
                }

                double freshness = Freshness(now, death.Time, MourningDays);
                sources.Insert(0, "a death recorded here " + now.DaysSince(death.Time) + " day(s) ago");
                SocialPracticeHolding holding = new SocialPracticeHolding(
                    SocialPracticeKind.Mourning,
                    freshness * Crowded(0.5, 0.25, 1.0, sources.Count - 1),
                    sources);

                if (best == null || holding.Strength > best.Strength)
                {
                    best = holding;
                }
            }

            return best;
        }

        /// <summary>
        /// A contest judged here lately, with a crowd still in it.
        ///
        /// The one occasion the ledger already records as itself (BQ-045's festival writes
        /// <see cref="WorldEventType.CompetitionWon"/> with the site on it), so nothing has to be
        /// inferred about what kind of gathering this is.
        /// </summary>
        private static SocialPracticeHolding ReadContest(
            NarrativeWorldState world,
            EntityId zoneId,
            List<EntityId> present,
            GameTime now)
        {
            List<WorldEvent> contests = RecentHere(world, zoneId, WorldEventType.CompetitionWon, now, ContestDays);
            if (contests.Count == 0)
            {
                return null;
            }

            WorldEvent freshest = contests[0];
            for (int i = 1; i < contests.Count; i++)
            {
                if (contests[i].Time > freshest.Time)
                {
                    freshest = contests[i];
                }
            }

            List<string> sources = new List<string>
            {
                "a contest judged here " + (now.TotalMinutes - freshest.Time.TotalMinutes) + " minute(s) ago",
                present.Count + " still here for it"
            };

            return new SocialPracticeHolding(
                SocialPracticeKind.Contest,
                Freshness(now, freshest.Time, ContestDays) * Crowded(0.5, 0.25, 1.0, present.Count),
                sources);
        }

        /// <summary>
        /// Several people answerable to one body, in one room.
        ///
        /// Read off the registry's own membership rather than off identity eligibility, because
        /// eligibility says somebody answers to <em>an</em> organised body and never to which one:
        /// two people who each belong to something are not a meeting, and asserting one from that
        /// would be exactly the guess D017 refuses.
        /// </summary>
        private static SocialPracticeHolding ReadAssembly(NarrativeWorldState world, List<EntityId> present)
        {
            SocialPracticeHolding best = null;
            string bestBody = null;
            foreach (KeyValuePair<EntityId, Organization> entry in world.Registry.Organizations)
            {
                Organization body = entry.Value;
                if (body == null)
                {
                    continue;
                }

                List<string> sources = new List<string>();
                for (int i = 0; i < present.Count; i++)
                {
                    if (body.MemberIds.Contains(present[i]))
                    {
                        sources.Add(world.Registry.NameOf(present[i]) + " answers to " + body.Name);
                    }
                }

                if (sources.Count < AssemblyQuorum)
                {
                    continue;
                }

                SocialPracticeHolding holding = new SocialPracticeHolding(
                    SocialPracticeKind.Assembly,
                    Crowded(0.5, 0.2, 0.9, sources.Count - (AssemblyQuorum - 1)),
                    sources);

                // Strongest wins, and where two bodies are equally represented the answer is
                // settled by id so that the same room does not report a different meeting on a
                // different pass.
                if (best == null
                    || holding.Strength > best.Strength
                    || (holding.Strength == best.Strength
                        && string.CompareOrdinal(entry.Key.Value, bestBody) < 0))
                {
                    best = holding;
                    bestBody = entry.Key.Value;
                }
            }

            return best;
        }

        /// <summary>
        /// A household, and somebody who lives in it standing in it.
        ///
        /// The player's Home is the only household this simulation can read, and saying so is the
        /// limit rather than a defect: an NPC's house is not a readable roll of who lives there,
        /// and inventing one would be a norm asserted from nothing. Residency is not presence
        /// (<see cref="HomeState"/>), so this asks for both - an empty Home holds no household
        /// practice, which is the unattended case CD 16 contrasts against.
        /// </summary>
        private static SocialPracticeHolding ReadHousehold(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId zoneId,
            List<EntityId> present)
        {
            HomeState home = vanilla.GetHomeState();
            if (home == null || home.ZoneId.IsNone || home.ZoneId != zoneId)
            {
                return null;
            }

            List<string> sources = new List<string> { "the player's household here" };
            for (int i = 0; i < present.Count; i++)
            {
                if (home.IsResident(present[i]))
                {
                    sources.Add(world.Registry.NameOf(present[i]) + " lives here");
                }
            }

            return sources.Count < 2
                ? null
                : new SocialPracticeHolding(
                    SocialPracticeKind.Household,
                    Crowded(0.5, 0.2, 0.9, sources.Count - 1),
                    sources);
        }

        /// <summary>
        /// Everything of one type recorded in this zone inside a window, newest-first and bounded.
        ///
        /// Walks history backwards and stops at the window, so the cost of asking what has lately
        /// happened here does not grow with the length of the save. That rests on the ledger being
        /// append-ordered in time (D005), which is what an append-only history is; an entry written
        /// out of order is not found rather than found late, and no practice is asserted from it.
        /// </summary>
        private static List<WorldEvent> RecentHere(
            NarrativeWorldState world,
            EntityId zoneId,
            WorldEventType type,
            GameTime now,
            int windowDays)
        {
            List<WorldEvent> found = new List<WorldEvent>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            long cutoff = now.TotalMinutes - ((long)windowDays * GameTime.MinutesPerDay);
            for (int i = events.Count - 1; i >= 0; i--)
            {
                WorldEvent candidate = events[i];
                if (candidate.Time.TotalMinutes < cutoff)
                {
                    break;
                }

                if (candidate.Type == type && candidate.Zone == zoneId && candidate.Time <= now)
                {
                    found.Add(candidate);
                }
            }

            return found;
        }

        /// <summary>
        /// How much of the window is left, 1.0 at the moment and 0.0 once it has run out.
        /// </summary>
        private static double Freshness(GameTime now, GameTime when, int windowDays)
        {
            long window = (long)windowDays * GameTime.MinutesPerDay;
            long elapsed = now.TotalMinutes - when.TotalMinutes;
            if (elapsed < 0 || elapsed >= window)
            {
                return 0.0;
            }

            return 1.0 - ((double)elapsed / window);
        }

        /// <summary>
        /// How firmly a norm holds given how many people put it there. One is enough to hold it;
        /// more hold it harder, up to a ceiling short of certainty.
        /// </summary>
        private static double Crowded(double first, double each, double ceiling, int count)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            double strength = first + (each * (count - 1));
            return strength > ceiling ? ceiling : strength;
        }
    }
}
