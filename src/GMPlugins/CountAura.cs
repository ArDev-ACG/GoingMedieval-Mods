using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Gives the Count a presence the settlement can feel.
    ///
    /// The game already has this mechanic, but it hangs off perks rather than
    /// roles: <c>HumanoidProximityBehaviour</c> walks the perks of whoever is
    /// standing there and fires each <c>proximityEffector</c> on everyone
    /// nearby. Roles have no equivalent field.
    ///
    /// Two earlier shapes and why they went:
    ///
    /// The first bridged the gap with three hidden perks handed out by
    /// <c>TryAddNewPerk</c>. It never worked, because <c>hideInGame: true</c>
    /// does not mean "do not show it" - <c>Repository.Deserialize</c> ends with
    /// <c>RemoveAll(m =&gt; m.HideInGame)</c> and the perks were simply deleted
    /// from the repository. The log said otherwise only because TryAddNewPerk
    /// returns void and the success line was printed unconditionally.
    ///
    /// The second borrowed the game's own proximity hooks. That worked, and the
    /// reach was the problem: <c>CreatureBase.TryInitProximitySpheres</c>
    /// builds <b>one static sphere of radius 6</b> shared by every creature in
    /// the world, so widening the Count's aura would have widened messy eaters
    /// and rotting wounds along with it.
    ///
    /// So the sweep is our own. Once a quarter hour the Count looks over every
    /// settler in the village and hands the mood to the ones inside
    /// <see cref="GMPlugin.CountAuraRadius"/> tiles, taking it back from the
    /// ones who have walked out. That is the same cadence vanilla uses for its
    /// proximity effectors, and the radius is now a number we own.
    /// </summary>
    [HarmonyPatch]
    internal static class CountAura
    {
        private const string CountRole = "count";

        /// <summary>Aura effector per rank; index 0 is "no rank" and holds nothing.</summary>
        private static readonly string[] AuraByRank =
        {
            null, "CountAuraLvl1", "CountAuraLvl2", "CountAuraLvl3", "CountAuraLvl4",
        };

        /// <summary>
        /// The quarter-hour tick, plus every moment the rank can change, so the
        /// aura appears the instant the role is handed over instead of at the
        /// next quarter.
        /// </summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[]
                     {
                         "HandleQuarterHourChange", "OnRoleAssign", "OnSetupRole",
                         "LevelUpRole", "LevelDownRole", "RetractRole",
                     })
            {
                var method = AccessTools.Method(typeof(HumanoidRoleOwner), name);
                if (method != null) yield return method;
                else GMPlugin.Log?.LogError($"[aura] HumanoidRoleOwner.{name} not found");
            }
        }

        private static void Postfix(HumanoidRoleOwner __instance)
        {
            var count = GameAccess.RoleOwnerHumanoid(__instance);
            if (count == null || count.HasDisposed || count.HasDied) return;

            var wanted = AuraFor(__instance);
            var radius = GMPlugin.CountAuraRadius?.Value ?? 12;
            var reach = radius * radius;

            if (!MonoSingleton<WorkerManager>.IsInstantiated()) return;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDied) continue;
                if (ReferenceEquals(settler, count)) continue;

                var inReach = wanted != null && DistanceSquared(count, settler) <= reach;
                Sync(settler, inReach ? wanted : null, count);
            }
        }

        /// <summary>
        /// Leaves the settler carrying exactly <paramref name="wanted"/> and
        /// nothing else from the set - which is also what takes the old aura
        /// off someone the moment the Count is promoted or deposed.
        /// </summary>
        private static void Sync(HumanoidInstance settler, string wanted, HumanoidInstance count)
        {
            var stats = GameAccess.Stats(settler);
            if (stats == null) return;

            for (var i = 1; i < AuraByRank.Length; i++)
            {
                var effectorId = AuraByRank[i];
                var active = stats.IsEffectorActive(effectorId);

                if (effectorId != wanted)
                {
                    if (active) stats.EndEffector(effectorId);
                    continue;
                }

                if (active) continue;

                if (stats.StartEffector(effectorId, 1f, false, count.UniqueId, null))
                {
                    GMPlugin.Log?.LogInfo(
                        $"[aura] {GameAccess.Name(settler)} feels {effectorId} "
                        + $"from {GameAccess.Name(count)}");
                }
                else
                {
                    GMPlugin.Log?.LogWarning(
                        $"[aura] {effectorId} refused on {GameAccess.Name(settler)} "
                        + "- banned by a perk, or missing from Effectors.json");
                }
            }
        }

        /// <summary>
        /// In tiles, squared, on the grid the game itself measures proximity
        /// with - height included, so the Count does not reach three floors up.
        /// </summary>
        private static int DistanceSquared(CreatureBase a, CreatureBase b)
        {
            var pa = a.GetGridPosition();
            var pb = b.GetGridPosition();

            var dx = pa.x - pb.x;
            var dy = pa.y - pb.y;
            var dz = pa.z - pb.z;

            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// The aura this settler radiates right now, or null for everyone who
        /// is not the Count.
        /// </summary>
        private static string AuraFor(HumanoidRoleOwner roles)
        {
            if (roles == null || !roles.HasRole(CountRole)) return null;

            var rank = roles.RoleLevel;
            return rank > 0 && rank < AuraByRank.Length ? AuraByRank[rank] : null;
        }
    }
}
