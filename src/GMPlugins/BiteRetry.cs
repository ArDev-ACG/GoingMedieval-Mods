using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.AdditionalMenuItems;
using NSMedieval.Goap;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Gives the order again when the victim walked out from under it.
    ///
    /// A bite only ever worked on someone standing still, and the reason is one
    /// line deep in the vanilla chase action. GoToCreatureTarget re-paths to the
    /// target every 0.2 s, and hands the pathfinder a status callback that does
    /// this and nothing else:
    ///
    ///     if (!success) action.Complete(ActionCompletionStatus.Fail);
    ///
    /// and GoapAction.Complete ends the whole goal on a Fail. Against a
    /// stationary target the path is built once and never fails. Against a
    /// moving one the destination tile changes several times a second, and the
    /// tile a creature stands on is not walkable, so one build eventually comes
    /// back empty - a trader turning a corner, a goat stepping onto a slope -
    /// and the order dies there. Not a rare case: the more the victim moved, the
    /// more certain it became.
    ///
    /// Widening the reach helps (a bite finishes before the chase runs out of
    /// patience) but cannot fix a failed path build, so the order is simply
    /// given again, up to <see cref="GMPlugin.BiteRetries"/> times. That is the
    /// same three calls the right-click menu makes - park the victim on the
    /// ReservationManager, abort whatever the vampire is doing, force the goal -
    /// so a retry is indistinguishable from the player clicking Bite again, only
    /// faster.
    ///
    /// The attempt count is keyed on the vampire, not the victim: what runs out
    /// is one settler's patience with one order, and it is cleared the moment a
    /// bite lands or the player orders a new one.
    /// </summary>
    internal static class BiteRetry
    {
        /// <summary>
        /// One frame is not enough - the goal that just failed is still being
        /// torn down - and a long wait reads as the vampire losing interest.
        /// </summary>
        private const float RetryDelaySeconds = 0.35f;

        private static readonly Dictionary<int, int> AttemptsLeft = new Dictionary<int, int>();

        /// <summary>
        /// Bumped every time this vampire's orders change. A retry that was
        /// already scheduled checks the number it was booked under and drops
        /// itself if it has moved on.
        ///
        /// Without it there is a third of a second after every failed bite in
        /// which the player can give the settler a different job and have the
        /// retry drag them straight back to the victim - which would read as
        /// the mod ignoring the order, and would be the sort of thing that gets
        /// reported as "el colono no me hace caso".
        /// </summary>
        private static readonly Dictionary<int, int> Orders = new Dictionary<int, int>();

        /// <summary>
        /// Called when a bite lands, and whenever the player gives this settler
        /// any order at all: both mean the run of failures is over, and any
        /// retry still in the air is stale.
        /// </summary>
        internal static void Clear(HumanoidInstance biter)
        {
            if (biter == null) return;

            AttemptsLeft.Remove(biter.UniqueId);
            Orders[biter.UniqueId] = Order(biter) + 1;
        }

        private static int Order(HumanoidInstance biter)
        {
            return Orders.TryGetValue(biter.UniqueId, out var order) ? order : 0;
        }

        /// <summary>
        /// The goal ended without anyone being drunk from. Walk back over unless
        /// this vampire has already tried enough times.
        /// </summary>
        internal static void Failed(HumanoidInstance biter, CreatureBase victim)
        {
            if (biter == null) return;

            if (!VampireBite.CanBeDrunkFrom(victim, biter))
            {
                Clear(biter);
                return;
            }

            if (!AttemptsLeft.TryGetValue(biter.UniqueId, out var left))
            {
                left = GMPlugin.BiteRetries?.Value ?? 0;
            }

            if (left <= 0)
            {
                Clear(biter);
                GMPlugin.Log?.LogInfo(
                    $"[bite] {GameAccess.Name(victim)} got away from {GameAccess.Name(biter)}");
                BiteFeedback.Bubble(biter, BiteFeedback.MissedIcon);
                return;
            }

            AttemptsLeft[biter.UniqueId] = left - 1;
            GMPlugin.Log?.LogInfo(
                $"[bite] {GameAccess.Name(biter)} lost {GameAccess.Name(victim)} "
                + $"and is going again ({left} left)");

            var order = Order(biter);
            GMPlugin.RunAfter(RetryDelaySeconds, () => Reissue(biter, victim, order));
        }

        /// <summary>
        /// AdditionalMenuPrioritiseItem.ForceGoal, rewritten where a goal can
        /// reach it. The order of the three steps is the game's own and matters:
        /// the goal's first init step reads the victim back off the
        /// ReservationManager, so it has to be parked there before the agent is
        /// told to start.
        /// </summary>
        private static void Reissue(HumanoidInstance biter, CreatureBase victim, int order)
        {
            if (biter == null || biter.HasDisposed) return;
            if (Order(biter) != order) return;   // the player has given a newer order

            if (!Send(biter, victim)) Clear(biter);
        }

        /// <summary>
        /// Sends a vampire to bite somebody, exactly as the right-click menu
        /// would.
        ///
        /// The order of the three steps is the game's own and it matters: the
        /// goal's first init step reads the victim back off the
        /// ReservationManager, so the victim has to be parked there before the
        /// agent is told to start.
        ///
        /// Split out of <see cref="Reissue"/> so the thirst can use it. A
        /// starving vampire going for the nearest living thing wants precisely
        /// the machinery a player's order already has - the walk, the reach,
        /// the retries, the feeding - and writing any of that again beside it
        /// would be a second bite with its own bugs.
        /// </summary>
        internal static bool Send(HumanoidInstance biter, CreatureBase victim)
        {
            if (biter == null || biter.HasDisposed) return false;
            if (!VampireBite.CanBeDrunkFrom(victim, biter)) return false;

            var agent = biter.GetGoapAgent() as WorkerGoapAgent;
            if (agent == null) return false;

            var reservable = victim as IReservable;
            if (reservable != null && MonoSingleton<ReservationManager>.IsInstantiated())
            {
                var reservations = MonoSingleton<ReservationManager>.Instance;
                reservations.SetPreferredReservable(biter, reservable);
                reservations.TryToExclusiveReservation(reservable, biter, 1f);
            }

            agent.Abort();
            agent.ForceNextGoalExclusive(BiteGoal.GoalId);
            return true;
        }
    }

    /// <summary>
    /// Any order the player gives cancels a bite that was about to be retried.
    ///
    /// <see cref="BiteMenuItem"/> clears its own, but every other entry on the
    /// right-click menu - haul this, build that, go here - goes through the same
    /// <c>ForceGoal</c>, and a retry already in the air would pull the settler
    /// straight back off it. One postfix on the shared method covers all of them
    /// and needs no cooperation from the items themselves.
    /// </summary>
    [HarmonyPatch]
    internal static class BiteRetryCancel
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AdditionalMenuPrioritiseItem), "ForceGoal");
        }

        // The parameter is called `goalExecutor` in 1.1.19, not
        // `humanoidOverride`, and Harmony binds a postfix parameter by name:
        // the wrong one is "FAILED BiteRetryCancel: IL Compile Error" at
        // startup and the class silently unpatched, which is what the log of
        // the 6 sep run says on line 62. `dotnet run -- members
        // AdditionalMenuPrioritiseItem` is where the real name comes from.
        private static void Postfix(AdditionalMenuPrioritiseItem __instance,
                                    HumanoidInstance goalExecutor)
        {
            BiteRetry.Clear(goalExecutor ?? SelectedWorker(__instance));
        }

        /// <summary>
        /// ForceGoal falls back to the selected settler when it is handed no
        /// override, and that getter is protected, so the fallback is read the
        /// same way it is written - just from outside.
        /// </summary>
        private static HumanoidInstance SelectedWorker(object item)
        {
            var method = AccessTools.Method(typeof(AdditionalMenuItemBase), "GetSelectedWorker");
            if (item == null || method == null) return null;

            try
            {
                // Tiene un parametro opcional (includeMercenaries); por reflexion
                // hay que pasarlo, o lanza "Number of parameters ... does not match".
                return method.Invoke(item, new object[] { true }) as HumanoidInstance;
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[bite] could not read the selected worker: {e.Message}");
                return null;
            }
        }
    }
}
