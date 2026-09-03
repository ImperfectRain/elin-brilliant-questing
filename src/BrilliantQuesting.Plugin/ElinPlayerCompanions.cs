using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Reads who keeps the player company out of Elin: the party, pets and hired companions
    /// alike.
    ///
    /// The other half of the player's household (BQ-123), and read here rather than beside the
    /// Home roll in <see cref="ElinHomeState"/> because the two come from opposite ends of the
    /// game - the roll off the settlement branch, the party off `EClass.pc` - and a build can lose
    /// one without losing the other.
    ///
    /// A pure read, with no write anywhere in this file, and there must not be one. Whether
    /// somebody is in the player's party is the player's business and the game's; what the mod
    /// takes from it is who a scene is allowed to be about. The list is minted into
    /// <see cref="EntityId"/>s exactly the way the observer and the Home roll mint theirs, so a
    /// pet that is cast in a scene, later put on the Home roll and later still sold is one actor
    /// throughout and the firing that named it keeps reading correctly.
    ///
    /// Like everything below `EClass.pc` that has not been read off a running game, the members
    /// are resolved by name against a candidate list. A build with none of these names reports the
    /// party as *unreadable* - <see cref="VanillaCapability.ReadPlayerCompanions"/> off, and
    /// <see cref="BrilliantQuesting.Relationships.PlayerHousehold.CompanionsRead"/> false - rather
    /// than as an empty party, because "this player travels alone" is a claim and this build
    /// cannot make it.
    /// </summary>
    internal static class ElinPlayerCompanions
    {
        /// <summary>The party object hanging off the player.</summary>
        private static readonly string[] PartyNames = { "party" };

        /// <summary>The characters in it. `Party.members` is the shape every candidate expects.</summary>
        private static readonly string[] MemberListNames = { "members", "Members", "charas" };

        private static bool _reportedShape;

        private static readonly IReadOnlyList<EntityId> Nobody = new List<EntityId>();

        /// <summary>
        /// Who is with the player now, or an empty list when this build cannot say.
        ///
        /// Re-read on every call, like the Home snapshot and for the same reason: a party changes
        /// between one zone and the next, and a companion the player has sold must stop being one
        /// the moment the game says so rather than the next time something remembers to ask.
        ///
        /// The player is never in their own party as far as this is concerned, and the dead are
        /// dropped here as well as in <c>PlayerHousehold</c> - the seam should not hand back a
        /// corpse as company.
        /// </summary>
        internal static IReadOnlyList<EntityId> Read(ElinBindings bindings, EntityId playerId, ManualLogSource log)
        {
            IEnumerable members = Members(log);
            if (members == null)
            {
                return Nobody;
            }

            List<EntityId> companions = new List<EntityId>();
            HashSet<EntityId> seen = new HashSet<EntityId>();
            foreach (object entry in members)
            {
                Chara chara = entry as Chara;
                if (chara == null || chara.isDead || chara.IsPC)
                {
                    continue;
                }

                // Derived, never registered, exactly as the Home roll derives its ids: reading who
                // is with the player must not enrol anybody in the world model or write a binding.
                // Same derivation both sides, so a companion who is also a resident is one actor
                // rather than two records of one animal.
                EntityId id = bindings != null
                    ? bindings.IdOf(chara, playerId)
                    : ElinBindings.MintCharaId(chara, playerId);

                if (!id.IsNone && id != playerId && seen.Add(id))
                {
                    companions.Add(id);
                }
            }

            return companions;
        }

        /// <summary>
        /// Whether this build exposes a party at all, for the capability probe. Answered by
        /// resolving the members rather than by counting them: a player who happens to be alone
        /// right now must not make the mod decide it cannot read parties.
        /// </summary>
        internal static string ResolvedMembers(ManualLogSource log)
        {
            object party = Party(log);
            if (party == null)
            {
                return null;
            }

            string member = NameOf(party.GetType(), MemberListNames);
            return member == null
                ? null
                : "EClass.pc." + (NameOf(PlayerType(), PartyNames) ?? PartyNames[0]) + "." + member;
        }

        private static IEnumerable Members(ManualLogSource log)
        {
            object party = Party(log);
            if (party == null)
            {
                return null;
            }

            ReportShapeOnce(party, log);
            return TryRead(party, MemberListNames, out object members) ? members as IEnumerable : null;
        }

        /// <summary>
        /// The player's party, or null when there is no player and when this build does not name
        /// one. Typed as object so an Early Access rename of the party class costs nothing here.
        /// </summary>
        private static object Party(ManualLogSource log)
        {
            try
            {
                Chara pc = EClass.pc;
                if (pc == null)
                {
                    return null;
                }

                return TryRead(pc, PartyNames, out object party) ? party : null;
            }
            catch (Exception ex)
            {
                log?.LogWarning("Could not reach the player's party (" + ex.GetType().Name + ").");
                return null;
            }
        }

        private static Type PlayerType()
        {
            try
            {
                return EClass.pc?.GetType();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Says once what this build actually answered with, so a live log distinguishes "the
        /// player travels alone" from "the party member name is wrong on this build".
        /// </summary>
        private static void ReportShapeOnce(object party, ManualLogSource log)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            string member = NameOf(party.GetType(), MemberListNames);
            if (member == null)
            {
                log.LogWarning("The player's party is a " + party.GetType().Name + " with no member matching "
                               + string.Join("/", MemberListNames)
                               + "; companions read as unavailable, not as none.");
                return;
            }

            log.LogInfo("Player companions from " + party.GetType().Name + "." + member + ".");
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

                return value != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string NameOf(Type type, string[] candidates)
        {
            return type == null ? null : Resolve(type, candidates)?.Name;
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
    }
}
