using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NSMedieval.GameEventSystem;
using NSMedieval.GameEventSystem.Events;
using Utils;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Mas incursiones de la Horda, sin mas incursiones.
    ///
    /// <c>RaidEvent.InitMapPlaces</c> elige al atacante con <c>PickRandom</c>
    /// entre todas las facciones hostiles, todas con el mismo peso: con cuatro
    /// hostiles, la Horda sale una de cada cuatro, y tener mas aldeas suyas en
    /// el mapa no cambia nada porque se elige la faccion y luego la aldea. Asi
    /// que despues de esa eleccion, si no ha salido la Horda y la Horda podia
    /// salir (es hostil, esta en el mapa y el evento no la excluye), con la
    /// probabilidad <c>Undead.HordeRaidBoost</c> pasa a ser ella. El ritmo de
    /// incursiones es el del juego; solo cambia quien viene.
    ///
    /// Solo cuando el evento elige por primera vez: al cargar una partida
    /// <c>raiderFactionId</c> ya viene escrito y no se toca.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenRaidShare
    {
        private static readonly Random Dice = new Random();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(RaidEvent), "InitMapPlaces");
            yield return AccessTools.Method(typeof(MultiRaidEvent), "InitMapPlaces");
        }

        private static void Prefix(GameEventInstance __instance, out bool __state)
        {
            __state = string.IsNullOrEmpty(Traverse.Create(__instance).Field("raiderFactionId").GetValue<string>());
        }

        private static void Postfix(GameEventInstance __instance, bool __state)
        {
            if (!__state) return;

            try
            {
                var boost = GMPlugin.HordeRaidBoost?.Value ?? 0f;
                if (boost <= 0f) return;

                var raid = Traverse.Create(__instance);
                var chosen = raid.Field("raiderFactionId").GetValue<string>();
                if (chosen == Census.UndeadFactionId) return;
                if (Dice.NextDouble() >= boost) return;

                var blueprint = __instance.Blueprint;
                var horde = FactionUtil.GetFactionsByFriendliness(blueprint.Friendliness, blueprint.ExcludeFactions, true)
                    .FirstOrDefault(f => f != null && f.BlueprintId == Census.UndeadFactionId);
                if (horde == null) return;

                var village = FactionUtil.GetRandomVillagePlace(horde);
                if (village == null) return;

                raid.Field("raiderFactionId").SetValue(horde.BlueprintId);
                raid.Field("raiderOriginVillageRef").SetValue(village);
                raid.Field("raiderFactionInstance").SetValue(horde);
                GMPlugin.Log?.LogInfo($"[horde] la incursion de '{chosen}' la trae la Horda (HordeRaidBoost {boost:0.00})");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[horde] could not hand the raid to the Horde: {e}");
            }
        }
    }
}
