using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Reads the player's Home out of Elin, and moves one thing in it.
    ///
    /// Reading is the bulk of the file and the reason it exists. The one write is
    /// <see cref="TryAdmit"/>, which puts a person on the settlement's resident roll and stops
    /// there: what that resident then does, and what it does to the six Home Skill elements, is
    /// Elin's own arithmetic and is read back rather than set (decision D018).
    ///
    /// The entry point is `EClass.Branch`, the player's own settlement branch. Its members are the
    /// residents, the Home Skill elements (`fSafety`, `fMoral`, `fFood`, `fSoil`, `fPromo`,
    /// `fAdmin`) live in its element container, and their element ids come from the same verified
    /// alias table every attribute and skill is resolved through.
    ///
    /// Everything below the branch object is read by name against a candidate list rather than
    /// compiled against a member, because unlike `Chara.elements` or `Player.karma` none of these
    /// members has been read off a running game. That has one deliberate consequence: a name this
    /// build does not have makes the datum *absent* - "?" in the log, `TryGetMetric` false, and no
    /// capacity - instead of a zero that would read as a measurement. A Home that reported
    /// capacity zero would look permanently full and quietly close every shelter route; one that
    /// reported safety zero would look like a slum. The member each datum came from is named in
    /// the log once, so a live run says what this build actually calls things.
    /// </summary>
    internal static class ElinHomeState
    {
        /// <summary>The residents. A list of Chara on the branch.</summary>
        private static readonly string[] MemberListNames = { "members", "Members", "charas" };

        /// <summary>
        /// How many people the settlement may hold. Deliberately no "worker" name here: a cap on
        /// how many residents can be put to work is a different quantity, and reporting it as room
        /// for people would be a wrong measurement rather than an absent one.
        /// </summary>
        private static readonly string[] CapacityNames =
        {
            "maxResident", "MaxResident", "maxMember", "MaxMember", "capacity", "Capacity"
        };

        private static readonly string[] BranchNameNames = { "Name", "name" };

        /// <summary>
        /// Only the zone's own uid. A branch's own uid would mint a `zone_` id for something that
        /// is not that zone, and the collision would be invisible; no id at all is the better
        /// answer.
        /// </summary>
        private static readonly string[] ZoneUidNames = { "uidZone" };

        private static readonly string[] ElementContainerNames = { "elements", "Elements" };

        /// <summary>What a resident does at Home.</summary>
        private static readonly string[] JobNames = { "job", "idJob", "Job", "hobby", "work" };

        /// <summary>
        /// The call that puts somebody on the settlement's roll. Resolved by name against the
        /// branch, like everything else below <c>EClass.Branch</c>, and like everything else here
        /// it is unconfirmed on a running game - so a build with none of these names loses the
        /// residency write and says so, rather than reporting a move that never happened.
        ///
        /// Deliberately no bare "Add": every other candidate list here guesses at a *read*, where
        /// a wrong guess costs a datum, and this one calls a method that changes a save. A branch
        /// with some unrelated `Add(Chara)` on it would be handed a person on the strength of a
        /// name, so the write would rather not exist than be wrong.
        /// </summary>
        private static readonly string[] AdmitNames = { "AddMember", "AddResident", "AddChara" };

        private static bool _reportedNoAdmit;

        private static bool _reportedShape;

        /// <summary>
        /// The Home as the game has it now, or null when the player has none and when the branch
        /// cannot be read at all. Null is the contract's "no answer" - never an empty Home.
        /// </summary>
        internal static HomeState Read(ElinBindings bindings, EntityId playerId, ManualLogSource log)
        {
            object branch = Branch(log);
            if (branch == null)
            {
                return null;
            }

            EntityId zoneId = TryReadInt(branch, ZoneUidNames, out int zoneUid) && zoneUid != 0
                ? EntityId.Parse("zone_" + zoneUid)
                : EntityId.None;

            HomeStateBuilder builder = new HomeStateBuilder(
                zoneId,
                TryRead(branch, BranchNameNames, out object name) ? AsText(name) : string.Empty);

            if (TryReadInt(branch, CapacityNames, out int capacity))
            {
                builder.WithCapacity(capacity);
            }

            bool residentsListed = ReadResidents(branch, bindings, playerId, builder);
            ReadMetrics(branch, builder, log);

            HomeState home = builder.Build();
            ReportShapeOnce(branch, home, residentsListed, log);
            return home;
        }

        /// <summary>
        /// Moves somebody into the player's Home, and reports whether the game actually took them.
        ///
        /// The branch is told, and then asked. Trusting the call would be the same mistake a
        /// destroy that never happened would be: a `sheltered_by` fact written over a settlement
        /// that never took anybody is exactly the stale binding the evidence rules exist to stop,
        /// and it would be invisible until the player walked home and found nobody there.
        ///
        /// Nothing here sets a job. A resident's work, and what it does to Public Safety, Food
        /// Supply and the rest, is the game's own arithmetic; writing either would put a second
        /// settlement economy beside the one on the player's Home board (decision D018).
        /// </summary>
        internal static bool TryAdmit(ElinBindings bindings, EntityId playerId, EntityId chara, ManualLogSource log)
        {
            Chara person = bindings?.ResolveChara(chara);
            if (person == null || person.isDead)
            {
                return false;
            }

            object branch = Branch(log);
            if (branch == null)
            {
                return false;
            }

            MethodInfo add = ResolveAdmit(branch.GetType());
            if (add == null)
            {
                if (!_reportedNoAdmit)
                {
                    _reportedNoAdmit = true;
                    log?.LogWarning("No member of " + branch.GetType().Name + " matched "
                                    + string.Join("/", AdmitNames) + ", so nobody can be moved into the Home on this build.");
                }

                return false;
            }

            try
            {
                add.Invoke(branch, new object[] { person });
            }
            catch (Exception ex)
            {
                log?.LogWarning("Moving " + person.Name + " into the Home through " + add.Name
                                + " failed (" + ex.GetType().Name + ").");
                return false;
            }

            HomeState after = Read(bindings, playerId, log);
            bool moved = after != null && after.IsResident(chara);
            if (!moved)
            {
                log?.LogWarning(person.Name + " is not on the Home's roll after " + add.Name + ".");
            }

            return moved;
        }

        /// <summary>
        /// What this build calls the residency write, or null when it has none. Read for the
        /// capability probe, which must not actually move anybody to find out.
        /// </summary>
        internal static string AdmitMemberName(ManualLogSource log)
        {
            object branch = Branch(log);
            return branch == null ? null : ResolveAdmit(branch.GetType())?.Name;
        }

        /// <summary>
        /// The player's settlement branch, or null when there is none and when it cannot be
        /// reached. Typed as object on purpose: the branch class is not otherwise mentioned in
        /// this assembly, so an Early Access rename of the type costs nothing here.
        /// </summary>
        private static object Branch(ManualLogSource log)
        {
            try
            {
                return EClass.Branch;
            }
            catch (Exception ex)
            {
                log?.LogWarning("Could not reach EClass.Branch (" + ex.GetType().Name + ").");
                return null;
            }
        }

        /// <summary>
        /// A one-argument method on the branch that will take a <c>Chara</c>. Kept apart from
        /// <see cref="Resolve"/>, which deliberately only ever finds parameterless members: a read
        /// that accidentally resolved to something taking arguments would be a bug, and a write
        /// that has to take a person cannot use that rule.
        /// </summary>
        private static MethodInfo ResolveAdmit(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            for (int i = 0; i < AdmitNames.Length; i++)
            {
                MethodInfo method = type.GetMethod(AdmitNames[i], Flags, null, new[] { typeof(Chara) }, null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        /// <summary>Fills in the residents, and reports whether the game listed them at all.</summary>
        private static bool ReadResidents(object branch, ElinBindings bindings, EntityId playerId, HomeStateBuilder builder)
        {
            if (!TryRead(branch, MemberListNames, out object members) || !(members is IEnumerable list))
            {
                return false;
            }

            foreach (object entry in list)
            {
                Chara chara = entry as Chara;
                if (chara == null || chara.isDead)
                {
                    continue;
                }

                // Derived, never registered. Reading who lives at Home must not enrol anybody in
                // the world model or write a binding: the id is computed the same way the observer
                // computes it, so a resident who later does something the mod watches keeps the
                // identity they were listed under here.
                EntityId id = bindings != null && bindings.TryGetEntity(chara.uid, out EntityId bound)
                    ? bound
                    : ElinBindings.MintCharaId(chara, playerId);

                builder.AddResident(id, chara.Name, JobOf(chara));
            }

            return true;
        }

        private static void ReadMetrics(object branch, HomeStateBuilder builder, ManualLogSource log)
        {
            if (!TryRead(branch, ElementContainerNames, out object elements) || elements == null)
            {
                return;
            }

            MethodInfo value = elements.GetType().GetMethod(
                "Value",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int) },
                null);
            if (value == null)
            {
                log?.LogWarning("The Home element container has no Value(int); Home Skill elements read as unavailable.");
                return;
            }

            foreach (HomeMetric metric in (HomeMetric[])Enum.GetValues(typeof(HomeMetric)))
            {
                if (!ElementAliases.TryGet(metric, out int elementId))
                {
                    continue;
                }

                try
                {
                    object read = value.Invoke(elements, new object[] { elementId });
                    if (read != null)
                    {
                        builder.WithMetric(metric, Convert.ToInt32(read, CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception)
                {
                    // Absent rather than zero. One unreadable element must not make the Home look
                    // like a slum, and the "?" in the log says which one did not answer.
                }
            }
        }

        private static string JobOf(Chara chara)
        {
            return TryRead(chara, JobNames, out object job) ? AsText(job) : string.Empty;
        }

        /// <summary>The member a candidate list actually resolved to on this build, or null.</summary>
        private static string NameOf(Type type, string[] candidates)
        {
            MemberInfo member = Resolve(type, candidates);
            return member?.Name;
        }

        /// <summary>
        /// Says once what this build actually answered with, so a live log distinguishes "the
        /// player has a small quiet Home" from "half of these member names are wrong".
        /// </summary>
        private static void ReportShapeOnce(object branch, HomeState home, bool residentsListed, ManualLogSource log)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            Type type = branch.GetType();
            log.LogInfo("Home branch is " + type.Name
                        + "; residents from " + (NameOf(type, MemberListNames) ?? "-")
                        + ", capacity from " + (NameOf(type, CapacityNames) ?? "-")
                        + ", elements from " + (NameOf(type, ElementContainerNames) ?? "-") + ".");

            List<string> unread = new List<string>();
            if (!home.CapacityKnown)
            {
                unread.Add("capacity (tried " + string.Join("/", CapacityNames) + ")");
            }

            if (!residentsListed)
            {
                unread.Add("residents (tried " + string.Join("/", MemberListNames) + ")");
            }

            foreach (HomeMetric metric in (HomeMetric[])Enum.GetValues(typeof(HomeMetric)))
            {
                if (!home.KnowsMetric(metric))
                {
                    unread.Add(metric.ToString());
                }
            }

            if (unread.Count > 0)
            {
                log.LogWarning("Unread Home data: " + string.Join(", ", unread.ToArray())
                               + ". Those read as absent, not as zero.");
            }
        }

        // -- member lookup ---------------------------------------------------------------------

        private static readonly Dictionary<string, MemberInfo> Resolved = new Dictionary<string, MemberInfo>();

        private static bool TryRead(object target, string[] candidates, out object value)
        {
            value = null;
            if (target == null)
            {
                return false;
            }

            MemberInfo member = Resolve(target.GetType(), candidates);
            if (member == null)
            {
                return false;
            }

            try
            {
                if (member is PropertyInfo property)
                {
                    value = property.GetValue(target, null);
                }
                else if (member is FieldInfo field)
                {
                    value = field.GetValue(target);
                }
                else
                {
                    value = ((MethodInfo)member).Invoke(target, null);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadInt(object target, string[] candidates, out int value)
        {
            value = 0;
            if (!TryRead(target, candidates, out object read) || read == null || read is string)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(read, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static MemberInfo Resolve(Type type, string[] candidates)
        {
            string key = type.FullName + "|" + candidates[0];
            if (Resolved.TryGetValue(key, out MemberInfo cached))
            {
                return cached;
            }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            MemberInfo found = null;
            for (int i = 0; i < candidates.Length && found == null; i++)
            {
                found = (MemberInfo)type.GetProperty(candidates[i], Flags)
                        ?? (MemberInfo)type.GetField(candidates[i], Flags)
                        ?? type.GetMethod(candidates[i], Flags, null, Type.EmptyTypes, null);
            }

            Resolved[key] = found;
            return found;
        }

        /// <summary>
        /// A readable word for a value that may be a string, an id-carrying row, or anything else.
        /// </summary>
        private static string AsText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string text)
            {
                return text;
            }

            if (TryRead(value, new[] { "id" }, out object id) && id is string idText)
            {
                return idText;
            }

            // A bare number is not a word for what somebody does. Reported as unread rather than
            // printed as a job called "3".
            if (value is IConvertible && value.GetType().IsPrimitive)
            {
                return string.Empty;
            }

            return value.ToString();
        }
    }
}
