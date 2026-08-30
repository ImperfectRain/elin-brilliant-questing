using System;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Where the game keeps a character, and how the mod moves them.
    ///
    /// The whole of Grade B absence in the live build, and it does exactly one thing: put a Chara
    /// in a different Zone. Nothing here removes anybody, destroys anybody or spawns a stand-in.
    /// That is the safety argument for the riskiest step in the plan - the character that comes
    /// back is the character that left, because there was only ever one of them, so no citizen
    /// refresh, rebuilt zone or reloaded save can produce a second.
    ///
    /// The two vanilla members used here are the version-matched EA 23.338 Patch 2 shapes:
    /// Chara.MoveZone(Zone, ZoneTransition.EnterState) and EClass.game.spatials.Find(int). A move
    /// is still verified by asking the game where the character is afterwards rather than by
    /// trusting that the call returned.
    /// </summary>
    internal static class ElinPresence
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>The prefix <see cref="ElinVanillaState.GetZoneOf"/> mints zone ids with.</summary>
        internal const string ZonePrefix = "zone_";

        internal static EntityId IdOf(Zone zone)
        {
            return zone == null
                ? EntityId.None
                : EntityId.Parse(ZonePrefix + zone.uid.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The uid inside a zone id, or false for anything not minted as one.</summary>
        internal static bool TryReadUid(EntityId zoneId, out int uid)
        {
            uid = 0;
            string value = zoneId.Value;
            return value.StartsWith(ZonePrefix, StringComparison.Ordinal)
                   && int.TryParse(value.Substring(ZonePrefix.Length), NumberStyles.Integer,
                       CultureInfo.InvariantCulture, out uid);
        }

        /// <summary>
        /// The names this build actually has for both halves of a move, or null when it is missing
        /// either. What the capability probe asks, and it resolves without calling: unlike
        /// <c>ModCurrency(0)</c> there is no harmless version of moving a person.
        /// </summary>
        internal static string ResolvedMembers(ManualLogSource log)
        {
            MethodInfo move = ResolveMove();
            MethodInfo find = ResolveFindZone(out Type owner);
            if (move == null || find == null)
            {
                log?.LogInfo("BQ presence: tried Chara.MoveZone(Zone, EnterState)"
                             + " and spatials.Find(int); move=" + (move == null ? "UNREADABLE" : move.Name)
                             + " findZone=" + (find == null ? "UNREADABLE" : find.Name) + ".");
                return null;
            }

            return "Chara." + move.Name + "(Zone, " + move.GetParameters()[1].ParameterType.Name + ") and " + owner.Name + "." + find.Name
                   + "(int) resolved; a move is confirmed by re-reading Chara.currentZone afterwards";
        }

        /// <summary>
        /// Moves a character into a zone and reports where they are afterwards, not what the call
        /// returned. Already being there is success and costs nothing, which is what lets
        /// reconciliation run on every tick.
        /// </summary>
        internal static bool TryMove(Chara chara, EntityId zoneId, ManualLogSource log)
        {
            if (chara == null || chara.isDead || !TryReadUid(zoneId, out int uid))
            {
                return false;
            }

            if (chara.currentZone != null && chara.currentZone.uid == uid)
            {
                return true;
            }

            if (!VanillaApiReflection.LooksGlobal(chara))
            {
                log?.LogWarning("BQ presence: refused to move " + chara.uid
                                + " because vanilla off-screen MoveZone requires an existing global record.");
                return false;
            }

            try
            {
                Zone destination = FindZone(uid);
                MethodInfo move = ResolveMove();
                if (destination == null || move == null)
                {
                    log?.LogWarning("BQ presence: no route to zone " + zoneId + " on this build.");
                    return false;
                }

                object enterState = VanillaApiReflection.ResolveEnterState(move.GetParameters()[1].ParameterType);
                if (enterState == null)
                {
                    log?.LogWarning("BQ presence: no safe ZoneTransition.EnterState value exists on this build.");
                    return false;
                }

                move.Invoke(chara, new[] { destination, enterState });
            }
            catch (Exception ex)
            {
                log?.LogWarning("BQ presence: moving " + chara.uid + " to " + zoneId + " threw "
                                + ex.GetType().Name + ": " + ex.Message);
                return false;
            }

            // The game is asked, not told. A move that silently did nothing has to report false, or
            // an absence would be recorded for somebody still standing in the market.
            return chara.currentZone != null && chara.currentZone.uid == uid;
        }

        private static Zone FindZone(int uid)
        {
            MethodInfo find = ResolveFindZone(out Type _);
            object spatials = EClass.game?.spatials;
            return find == null || spatials == null
                ? null
                : find.Invoke(spatials, new object[] { uid }) as Zone;
        }

        private static MethodInfo ResolveMove()
        {
            return VanillaApiReflection.ResolveMoveZone(typeof(Chara));
        }

        private static MethodInfo ResolveFindZone(out Type owner)
        {
            owner = EClass.game?.spatials?.GetType();
            if (owner == null)
            {
                return null;
            }

            return VanillaApiReflection.ResolveSpatialFindZone(owner);
        }
    }
}
