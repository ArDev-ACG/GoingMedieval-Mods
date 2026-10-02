using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Model;
using NSMedieval.StorageUniversal;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Keeps one bad resource group from freezing the loading screen forever.
    ///
    /// <para><b>The failure.</b> <c>InitializeStorableResourceGroupsNew()</c>
    /// builds a shelf's category list in two passes. The first walks every
    /// usable resource and does
    /// <c>resourceGroups.GetByID(resource.NewSortingGroup)</c>, then adds the
    /// answer to <c>storableResourceGroupsList</c> <em>without checking it for
    /// null</em>. The second pass dequeues that same list and asks each entry
    /// for its id to find its parent. A group id that does not resolve
    /// therefore does not fail where it is read - it fails one loop later, as a
    /// NullReferenceException inside a LINQ predicate, with nothing in the
    /// message naming the group, the resource, or the mod that shipped it.</para>
    ///
    /// <para><b>Why that hangs the game rather than logging a warning.</b>
    /// <c>LoadingScreenFake.OnLoadingFinished()</c> ends on
    /// <c>TaskController.WaitUntil(() =&gt; AlmanacPanelManager.InitDone)</c>,
    /// and the almanac sets that flag from a background thread that walks every
    /// buildable through <c>BuildingUtils.GetInfoLines</c> - which asks each
    /// shelf for exactly these categories. The exception kills that thread, the
    /// flag stays false, and the wait never ends: the bar sits full, the
    /// Continue button is never shown, and the game is alive but has finished
    /// loading nothing. The same call also runs per shelf during
    /// <c>ShelfComponentInstance.SetupAfterLoading</c>, so a saved game hits it
    /// once per chest on top of that.</para>
    ///
    /// <para><b>What this does.</b> Drops the nulls out of the list and eats the
    /// exception, which leaves the same list the method would have produced had
    /// the missing group never been named. The shelf loses the categories it
    /// could not resolve - which do not exist, so nothing that exists can be
    /// stored under them - and the almanac thread finishes. It also says once,
    /// per blueprint, which storage it was and which groups it asked for,
    /// because the stock exception says none of that and finding it by hand
    /// costs an evening of disabling mods two at a time.</para>
    ///
    /// A finalizer rather than a rewrite of the method: the loop above is the
    /// game's, it changes between patches, and nothing here needs it to behave
    /// differently - only to not take the loading screen down with it.
    /// </summary>
    [HarmonyPatch]
    internal static class StorageGroupNullGuard
    {
        /// <summary>Blueprint ids already reported, so a mapful of shelves says it once.</summary>
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private static readonly FieldInfo GroupsListField =
            AccessTools.Field(typeof(UniversalStorageBlueprint), "storableResourceGroupsList");

        private static readonly FieldInfo GroupNamesField =
            AccessTools.Field(typeof(UniversalStorageBlueprint), "storableResourceGroups");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(UniversalStorageBlueprint),
                "InitializeStorableResourceGroupsNew");
        }

        private static Exception Finalizer(Exception __exception, UniversalStorageBlueprint __instance)
        {
            int dropped = Clean(__instance);

            if (__exception == null)
            {
                return null;
            }

            Report(__instance, dropped, __exception);
            return null;
        }

        /// <summary>
        /// Removes the unresolved groups from the list the method was filling.
        /// Runs even when nothing threw: the first pass can leave a null behind
        /// that only bites later, when something else reads the list.
        /// </summary>
        private static int Clean(UniversalStorageBlueprint blueprint)
        {
            if (blueprint == null || GroupsListField == null)
            {
                return 0;
            }

            var groups = GroupsListField.GetValue(blueprint) as List<ResourceGroups>;
            if (groups == null)
            {
                return 0;
            }

            // ResourceGroups derives from Model, whose == is overloaded; the
            // reference check goes first so a destroyed-but-not-null entry still goes.
            return groups.RemoveAll(g => ReferenceEquals(g, null) || g == null);
        }

        private static void Report(UniversalStorageBlueprint blueprint, int dropped, Exception e)
        {
            string id = SafeId(blueprint);
            if (!Reported.Add(id))
            {
                return;
            }

            string asked = "?";
            if (GroupNamesField != null)
            {
                var names = GroupNamesField.GetValue(blueprint) as IEnumerable;
                if (names != null)
                {
                    asked = string.Join(", ", names.Cast<object>()
                        .Select(n => n == null ? "<null>" : n.ToString())
                        .ToArray());
                }
            }

            GMPlugin.Log.LogWarning(
                $"[StorageGroupNullGuard] storage '{id}' names a resource group that "
                + $"does not exist; dropped {dropped} unresolved group(s) so the almanac "
                + $"thread can finish. storableResourceGroups = [{asked}]. "
                + $"Original: {e.GetType().Name}");
        }

        private static string SafeId(UniversalStorageBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return "<null blueprint>";
            }

            try
            {
                string id = blueprint.GetID();
                return string.IsNullOrEmpty(id) ? "<no id>" : id;
            }
            catch
            {
                return "<id threw>";
            }
        }
    }
}
