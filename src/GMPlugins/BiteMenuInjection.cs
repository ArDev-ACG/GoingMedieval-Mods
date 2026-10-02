using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Model;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Puts "Morder" into the right-click menus of everything with blood.
    ///
    /// The first attempt did this from JSON, by shipping a VampireCourt copy of
    /// AdditionalMenuData.json with `menuItems#APPEND`. The game refused it:
    ///
    ///     [ERR] [RepositoryManager] No Add action registered for AdditionalMenuData.json
    ///
    /// That repository is simply not one of the ones mods are allowed to extend
    /// - the same wall the Rich Merchant mod hits with GameDifficulty.json - and
    /// no amount of JSON gets past it. The file loaded, the error was logged,
    /// and nothing appeared in any menu.
    ///
    /// So the list is extended where it is read instead. AdditionalMenuManager
    /// asks the model for its MenuItems on every right click, and a postfix on
    /// that getter hands back the vanilla array with one more entry on the end.
    /// The extended arrays are built once per menu id and reused, so a right
    /// click does not allocate.
    ///
    /// Whether the entry is actually drawn is still BiteMenuItem's decision:
    /// its constructor leaves Text empty for anyone who is not a vampire, and
    /// the manager skips items with no text.
    /// </summary>
    [HarmonyPatch]
    internal static class BiteMenuInjection
    {
        /// <summary>
        /// The menus a vampire could plausibly be pointed at. "worker" and
        /// "prisoner" are the settlement's own; the rest are visitors, raiders
        /// and livestock.
        /// </summary>
        private static readonly HashSet<string> BloodMenus = new HashSet<string>
        {
            "worker", "prisoner", "enemy", "trader", "negotiator", "beggar", "animal",
        };

        private static readonly Dictionary<string, string[]> Extended =
            new Dictionary<string, string[]>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(AdditionalMenuItemData), "MenuItems");
        }

        private static void Postfix(AdditionalMenuItemData __instance, ref string[] __result)
        {
            if (__instance == null || __result == null) return;

            var id = __instance.GetID();
            if (id == null || !BloodMenus.Contains(id)) return;
            if (Array.IndexOf(__result, BiteMenuItem.ItemId) >= 0) return;

            if (!Extended.TryGetValue(id, out var extended))
            {
                extended = new string[__result.Length + 1];
                Array.Copy(__result, extended, __result.Length);
                extended[__result.Length] = BiteMenuItem.ItemId;
                Extended[id] = extended;

                GMPlugin.Log?.LogInfo($"[bite] menu '{id}' now offers {BiteMenuItem.ItemId}");
            }

            __result = extended;
        }
    }
}
