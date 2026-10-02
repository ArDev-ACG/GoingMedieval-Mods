using System.Reflection;
using HarmonyLib;
using NSMedieval.BuildingComponents;
using NSMedieval.State;
using NSMedieval.Types;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lets a grave hold more than one body.
    ///
    /// GraveComponentsRepository.json already exposes graveStorage.capacity, but
    /// the game never gets that far: GraveComponentInstance.CanStore() opens with
    ///
    ///     if (HasBody()) return false;
    ///
    /// so the very first corpse locks every grave, whatever its capacity. The
    /// postfix below re-runs the rest of the vanilla test (free space + the
    /// player's body filter) and flips the result back to true, but only for
    /// graves whose blueprint actually asks for more than one slot. Vanilla
    /// graves and sarcophagi keep their capacity of 1 and are untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class MassGraveCapacity
    {
        private static bool IsMultiBodyGrave(GraveComponentInstance instance)
        {
            // StorageBase is a struct, so the blueprint is the only nullable hop.
            var blueprint = instance?.Blueprint;
            return blueprint != null && blueprint.GraveStorage.Capacity > 1;
        }

        [HarmonyPatch(typeof(GraveComponentInstance), nameof(GraveComponentInstance.CanStore),
            new[] { typeof(CarcassResourceInstance) })]
        [HarmonyPostfix]
        private static void CanStoreHuman(
            GraveComponentInstance __instance,
            CarcassResourceInstance carcassResourceInstance,
            ref bool __result)
        {
            if (__result || carcassResourceInstance == null) return;
            if (!IsMultiBodyGrave(__instance)) return;
            if (!__instance.HasFreeSpace()) return;

            __result = __instance.AllowedBodies.Contains(carcassResourceInstance.BodyType);
        }

        [HarmonyPatch(typeof(GraveComponentInstance), nameof(GraveComponentInstance.CanStore),
            new[] { typeof(AnimalCarcassResourceInstance) })]
        [HarmonyPostfix]
        private static void CanStoreAnimal(
            GraveComponentInstance __instance,
            AnimalCarcassResourceInstance carcassResourceInstance,
            ref bool __result)
        {
            if (__result || carcassResourceInstance == null) return;
            if (!IsMultiBodyGrave(__instance)) return;
            if (!__instance.HasFreeSpace()) return;

            __result = __instance.AllowedBodies.Contains("pet")
                       && carcassResourceInstance.AnimalType == AnimalType.Pet;
        }
    }

    /// <summary>
    /// Keeps a multi-body grave visually open until it is full.
    ///
    /// GraveViewComponent.OnAddBody() unconditionally calls CloseGrave(), which
    /// swaps the open mesh for the closed one. With the capacity patch above a
    /// mass grave still accepts corpses after that, so the mound would look
    /// sealed while settlers kept walking bodies into it.
    /// </summary>
    [HarmonyPatch]
    internal static class MassGraveStaysOpen
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GraveViewComponent), "OnAddBody");
        }

        private static bool Prefix(GraveViewComponent __instance)
        {
            var instance = __instance?.ComponentInstance;
            if (instance == null) return true;

            var blueprint = instance.Blueprint;
            if (blueprint == null || blueprint.GraveStorage.Capacity <= 1) return true;

            // Still room for another body: skip CloseGrave() this time.
            return !instance.HasFreeSpace();
        }
    }
}
