using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NSEipix.Repository;
using NSMedieval.Controllers;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.UI.Utils;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Makes the Risen answer to a name instead of to their id.
    ///
    /// The combat log and the enemy panel print a raider as "Denys
    /// (general_basic_easy)" - except that in vanilla the part in brackets is
    /// not an id being leaked, it is a <b>localisation term</b>: I2.Loc has an
    /// entry called `general_basic_easy` whose English value is "Marauder", so
    /// the player reads "Denys (Marauder)". The `locKeys` a mod writes in
    /// NPCs.json are not consulted here at all; the id is handed straight to
    /// the localisation table.
    ///
    /// Mods cannot add terms to that table - it lives inside the game's own
    /// asset bundle - so `undead_horde_walker_easy` misses, and
    /// LocalizationController.GetText hands back the key it was given. That is
    /// how a raw id ended up in the middle of a Spanish combat line.
    ///
    /// The miss is the hook. Every GetText overload funnels through the one
    /// that takes only a key, and that one returns <b>the key itself</b> when
    /// I2.Loc has nothing, so "the result still equals the key" is an exact
    /// test for "not translated". When that happens and the key names one of
    /// our NPCs, its own locKeys - which do speak Spanish - answer instead.
    ///
    /// The repository is only touched on a miss, which is rare, so the hot path
    /// of a method the UI calls constantly stays a single string comparison.
    /// </summary>
    [HarmonyPatch]
    internal static class ModNpcNames
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // All of them: which overload the combat log reaches for is an
            // implementation detail, and they all end in the same miss.
            return AccessTools.GetDeclaredMethods(typeof(LocalizationController))
                .Where(m => m.Name == nameof(LocalizationController.GetText)
                            && m.ReturnType == typeof(string)
                            && m.GetParameters().Length > 0
                            && m.GetParameters()[0].ParameterType == typeof(string))
                .Cast<MethodBase>();
        }

        private static void Postfix(string key, ref string __result)
        {
            if (string.IsNullOrEmpty(key) || __result != key) return;

            var npc = Repository<NPCRepository, NPC>.Instance?.GetByID(key);
            if (npc == null) return;

            var name = LocKeyUtils.GetName(npc.GetLocKeys());
            if (!string.IsNullOrEmpty(name) && name != key) __result = name;
        }
    }
}
