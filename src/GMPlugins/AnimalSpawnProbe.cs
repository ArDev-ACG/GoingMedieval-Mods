using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Read-only probe. Logs every animal the spawner places, so the real
    /// corpse-driven rat scaling can be written against verified call sites.
    /// The type and method names come from the crash stack trace produced by
    /// the Dire Wolves mod, so they are known to exist in this build.
    /// </summary>
    [HarmonyPatch]
    internal static class AnimalSpawnProbe
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                "NSMedieval.Map.AnimalSpawner:PlaceAnimal",
                new[] { typeof(string) });
        }

        private static void Postfix(string animalId)
        {
            GMPlugin.Log?.LogInfo("[probe] AnimalSpawner.PlaceAnimal(" + animalId + ")");
        }
    }
}
