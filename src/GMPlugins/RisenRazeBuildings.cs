using System;
using System.Reflection;
using HarmonyLib;
using NSMedieval.CommanderAI.BehaviourTreeTasks;
using NSMedieval.CommanderAI.Utilities;
using NSMedieval.Enums;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// With nobody left to kill, the horde starts on the walls.
    ///
    /// The raid commander picks what to send its units at in
    /// <c>PickBestRaidTargetBTAction.PickBestTargetThread</c>, and it tries five
    /// things in a fixed order:
    ///
    ///   1. a worker past the ones already fighting
    ///   2. a drafted worker in immediate danger, on a direct path
    ///   3. any drafted worker on a direct path
    ///   4. <b>the most valuable building in a reachable room</b>
    ///   5. a worker in the densest region of them
    ///
    /// Step 4 is already the "there is nobody to reach" branch, so a raid that
    /// cannot get at the settlers does turn on the settlement itself. What it
    /// does *not* do is turn on much: the candidates are filtered through
    /// <c>CommanderAIFilters.BuildingTypesToSpreadAttack</c>, a static mask
    /// holding production benches, furniture, shrines, wells and siege weapons -
    /// and pointedly no walls, no doors, no floors, no fences. A besieging army
    /// wants the workshop; it has no reason to spend a morning pulling apart a
    /// wall it could walk around.
    ///
    /// The Risen have exactly that reason, because they have no other. So for
    /// the length of that one call, and only when the units being commanded are
    /// ours, the mask is opened to everything and put back afterwards. Which
    /// building actually gets picked is still the game's own answer - reachable,
    /// not already broken, not on fire, and the most valuable of what is left -
    /// so this widens what counts as a target and changes nothing else.
    ///
    /// Swapping a static field around a call is the narrowest way in, and the
    /// finaliser is what makes it safe: the mask goes back even if the pick
    /// throws, so a raid of ours cannot leave the filter open for the human raid
    /// after it. The pick runs off the main thread (PickBestTargetThread), so in
    /// theory two commanders could overlap it - in practice a village runs one
    /// raid, and the worst an overlap could do is let one human raider consider
    /// a wall for one pass.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenRazeBuildings
    {
        /// <summary>
        /// The mask is <c>static readonly</c>, so plain assignment does not
        /// compile. Harmony's static field ref emits a DynamicMethod that
        /// ignores initonly, which is the same thing the game's own serializer
        /// does to restore fields it does not own.
        /// </summary>
        private static readonly AccessTools.FieldRef<BuildingType> SpreadMask = MaskRef();

        /// <summary>
        /// <c>FirstUnit</c> is protected, and typing a patch parameter as the
        /// task would drag in ParadoxNotion - the behaviour-tree library the
        /// class inherits from, which is not one of the assemblies this project
        /// references. Both are read reflectively instead, once.
        /// </summary>
        private static readonly MethodInfo FirstUnitGetter =
            AccessTools.PropertyGetter(typeof(UnitsBTActionBase), "FirstUnit");

        private static readonly FieldInfo UnitHumanoid =
            AccessTools.Field(AccessTools.TypeByName("NSMedieval.CommanderAI.CommanderAIUnit"),
                "Humanoid");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PickBestRaidTargetBTAction),
                "PickHighestValueRoomBuilding");
        }

        private static void Prefix(object __instance, out BuildingType __state)
        {
            __state = SpreadMask != null ? SpreadMask() : BuildingType.Default;

            if (SpreadMask == null) return;
            if (!(GMPlugin.UndeadRazeEverything?.Value ?? false)) return;
            if (!IsUndeadRaid(__instance)) return;

            SpreadMask() = BuildingType.AllBuildings;
        }

        private static void Finalizer(BuildingType __state)
        {
            if (SpreadMask == null) return;

            SpreadMask() = __state;
        }

        /// <summary>
        /// Whether the squad this task is choosing a target for is one of ours.
        ///
        /// A commander task carries the units it commands, and every unit in a
        /// squad comes out of the same raid, so the first one answers for all of
        /// them. <see cref="RisenPallor.IsUndead"/> is the same test the grey
        /// skin and the horde's taste for livestock already use, so a walker
        /// that counts as undead anywhere counts here too.
        /// </summary>
        private static bool IsUndeadRaid(object task)
        {
            if (task == null || FirstUnitGetter == null || UnitHumanoid == null) return false;

            try
            {
                var unit = FirstUnitGetter.Invoke(task, null);
                if (unit == null) return false;

                return Census.IsUndead(UnitHumanoid.GetValue(unit) as HumanoidInstance);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] could not read the squad: {e.Message}");
                return false;
            }
        }

        private static AccessTools.FieldRef<BuildingType> MaskRef()
        {
            try
            {
                var field = AccessTools.Field(typeof(CommanderAIFilters),
                    nameof(CommanderAIFilters.BuildingTypesToSpreadAttack));

                return field == null ? null : AccessTools.StaticFieldRefAccess<BuildingType>(field);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] BuildingTypesToSpreadAttack unreachable: {e.Message}");
                return null;
            }
        }
    }
}
