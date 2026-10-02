using System.Reflection;
using HarmonyLib;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Surviving the plague makes a settler immune to it.
    ///
    /// Wounds are effectors, so when the fever finally ends we ban that effector
    /// on the creature that carried it. StatsInstance.bannedEffectors is a
    /// SerializableHashSet, so the immunity is written into the save on its own.
    ///
    /// The injected parameter must be named activeEffectorInfo: Harmony binds
    /// postfix parameters by name against the original signature
    /// EndEffector(ref ActiveEffectorInfo activeEffectorInfo, int indexOfEffector).
    /// Naming it anything else throws at PatchAll time and takes the whole
    /// plugin down with it.
    /// </summary>
    [HarmonyPatch]
    internal static class PlagueImmunity
    {
        private const string FeverEffectorId = "plague_fever";
        private const string ImmunityEffectorId = "PlagueImmunityMarker";

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(StatsInstance),
                "EndEffector",
                new[] { typeof(ActiveEffectorInfo).MakeByRefType(), typeof(int) });
        }

        private static void Postfix(StatsInstance __instance, ref ActiveEffectorInfo activeEffectorInfo)
        {
            if (__instance == null || activeEffectorInfo.Name != FeverEffectorId)
            {
                return;
            }

            __instance.BanEffector(FeverEffectorId);

            // La prohibicion es la inmunidad de verdad y no puede fallar; el
            // marcador es solo lo que el jugador ve, y StartEffector devuelve
            // false en silencio si el efector no esta en el repositorio.
            var marked = __instance.StartEffector(ImmunityEffectorId, 1f, false, -1, null);
            var who = GameAccess.Name(GameAccess.StatsOwner(__instance));

            if (marked)
            {
                GMPlugin.Log?.LogInfo($"[plague] {who} is now immune to {FeverEffectorId}");
            }
            else
            {
                GMPlugin.Log?.LogWarning(
                    $"[plague] {who} is immune, but '{ImmunityEffectorId}' did not start "
                    + "- no habra nada que lo diga en pantalla");
            }
        }
    }
}
