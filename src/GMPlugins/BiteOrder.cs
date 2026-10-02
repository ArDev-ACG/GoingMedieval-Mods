using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval;
using NSMedieval.Goap;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Teaches the game that BiteGoal and BiteMenuItem exist.
    ///
    /// Both maps are the same shape: a static Dictionary&lt;string,
    /// ConstructorInfo&gt; behind a lazy getter that fills it once, on first
    /// read, and is never rebuilt afterwards. Neither map is exposed for
    /// writing, but a postfix on the getter sees the dictionary the game is
    /// about to use, so adding a key there is as good as being in the original
    /// list - and it survives the domain reload that resets both to null.
    /// </summary>
    [HarmonyPatch]
    internal static class BiteGoalRegistration
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(GoalsMap), "Constuctors");
        }

        private static void Postfix(Dictionary<string, ConstructorInfo> __result)
        {
            if (__result == null || __result.ContainsKey(BiteGoal.GoalId)) return;

            __result[BiteGoal.GoalId] = typeof(BiteGoal).GetConstructors()[0];
            GMPlugin.Log?.LogInfo("[bite] BiteGoal registered in GoalsMap");
        }
    }

    /// <summary>
    /// Puts BiteGoal into the goal pool of the settler who was ordered to bite.
    ///
    /// Being in GoalsMap is not enough, and that is why the button did nothing.
    /// The chain behind the menu item is
    /// <c>ForceGoal -> WorkerGoapAgent.ForceNextGoalExclusive -> Agent.ForceNextGoal(string)</c>,
    /// and that last one does not build a goal from GoalsMap: it asks
    /// <c>GoalScheduler.GetFromPool(id)</c> and <b>returns null without a word</b>
    /// if the id is not already in the pool. Nothing is logged, nothing is
    /// warned, the colonist simply carries on doing whatever it was doing.
    ///
    /// A worker's pool is filled by <c>WorkerGoapAgent.RefreshHourGoals</c>, and
    /// only from the goal ids listed in the schedule data for the current hour.
    /// BiteGoal is in no schedule - it is never chosen on its own, only ordered -
    /// so it never entered any pool.
    ///
    /// The game has the same problem with its own BanishGoal and solves it the
    /// same way: <c>WorkerGoalExecutionManager.GetFromPool</c> sees the null,
    /// builds the goal and adds it to the pool. This is that, one level lower,
    /// so it also covers the scheduler being asked directly.
    ///
    /// The goal is added <b>disabled</b>: the pool's enabled half is what
    /// GatherStartableGoals picks from, and a bite must never happen except by
    /// order. Forcing a goal ignores the enabled flag, so the order still works.
    /// </summary>
    [HarmonyPatch]
    internal static class BiteGoalPool
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GoalScheduler), "GetFromPool", new[] { typeof(string) });
        }

        private static void Postfix(GoalScheduler __instance, string id, ref Goal __result)
        {
            if (__result != null || id != BiteGoal.GoalId) return;

            var owner = GameAccess.SchedulerAgent(__instance) as HumanoidInstance;
            if (owner == null || owner.WorkerBehaviour == null) return;

            var agent = owner.GetGoapAgent();
            if (agent == null) return;

            __result = new BiteGoal(agent);
            __instance.AddToPool(__result, false);

            GMPlugin.Log?.LogInfo($"[bite] {BiteGoal.GoalId} added to {GameAccess.Name(owner)}'s pool");
        }
    }

    /// <summary>
    /// The right-click menu half of the same trick.
    /// </summary>
    [HarmonyPatch]
    internal static class BiteMenuRegistration
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(AdditionalMenuItemMap), "Constuctors");
        }

        private static void Postfix(Dictionary<string, ConstructorInfo> __result)
        {
            if (__result == null || __result.ContainsKey(BiteMenuItem.ItemId)) return;

            __result[BiteMenuItem.ItemId] = typeof(BiteMenuItem).GetConstructors()[0];
            GMPlugin.Log?.LogInfo("[bite] BiteMenuItem registered in AdditionalMenuItemMap");
        }
    }
}
