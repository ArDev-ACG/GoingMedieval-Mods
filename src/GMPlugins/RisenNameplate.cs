using System;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.FloatingOverlaySystem;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.View;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The name over a settler who turned.
    ///
    /// "el infectado murio y se convirtio en zombie pero no tiene nombre encima
    /// de su personaje como los demas": in the same screenshot two walkers of
    /// the horde carry their names in red and the turned settler carries
    /// nothing at all. The two halves of the game that draw that label do not
    /// know about each other:
    ///
    ///   - <c>NPCView.InstantiateNameElement</c> reads the behaviour, sees
    ///     <c>EnemyBehaviour</c>, and asks the factory for an
    ///     <c>EnemyName</c> element. That is the red name.
    ///   - <c>WorkerView.GenerateWorkerNameGuiOverlayElements</c> makes a
    ///     <c>WorkerName</c> element instead, and it does it exactly once, in
    ///     <c>Setup</c>.
    ///
    /// A turned settler is drawn by the WorkerView it was born with -
    /// <see cref="RisenRoster"/> keeps that view on purpose, because disposing
    /// it would leave a walker nothing would ever draw - so it is on the wrong
    /// side of that fork: it never reaches InstantiateNameElement, and the
    /// worker label it does have goes down with the rest of the settler's
    /// overlay when the death it was rescued from runs its course.
    ///
    /// So the label is made here, by hand, the way NPCView would have made it:
    /// same element type, same holder, same hook transform, same text. What
    /// comes out reads like the rest of the horde, which is the whole of the
    /// request.
    ///
    /// The view has to be taken <b>before</b> the eviction, because the
    /// eviction is what takes it out of <c>WorkerManager</c>'s dictionary and
    /// there is no other index from a humanoid to its view.
    /// </summary>
    internal static class RisenNameplate
    {
        /// <summary>
        /// The WorkerView drawing this body, or null. Only answers while the
        /// humanoid is still registered, which is why it is asked first.
        /// </summary>
        internal static WorkerView ViewOf(HumanoidInstance humanoid)
        {
            if (humanoid == null) return null;
            if (!MonoSingleton<WorkerManager>.IsInstantiated()) return null;

            try
            {
                return WorkerManager.Instance?.GetView(humanoid);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError("[risen] could not find the view for "
                                       + $"{GameAccess.Name(humanoid)}: {e}");
                return null;
            }
        }

        /// <summary>
        /// Swaps the settler's white name for the horde's red one.
        ///
        /// The old element is disposed rather than restyled: the factory pools
        /// a different prefab per <c>OverlayTextElementType</c>, so a
        /// WorkerName element told to call itself an EnemyName would keep the
        /// colonist's styling and only the bookkeeping would change.
        ///
        /// The new one is written back into <c>workerNameElement</c> so the
        /// view's own <c>DestroyWorkerNameGuiOverlayElement</c> still finds it:
        /// a label the game cannot clean up outlives the body it names.
        /// </summary>
        internal static bool Apply(HumanoidInstance humanoid, WorkerView view)
        {
            if (humanoid == null || view == null) return false;
            if (NameElementRef == null) return false;

            try
            {
                var old = NameElementRef(view);
                if (old != null) old.Dispose();

                var element = FloatingElementFactory.ProduceTextElement(
                    OverlayTextElementType.EnemyName, FloatingElementHolderType.Default,
                    view.GetGuiOverlayHookTransform(), Label(humanoid));

                NameElementRef(view) = element;
                return element != null;
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError("[risen] could not put a name over "
                                       + $"{GameAccess.Name(humanoid)}: {e}");
                return false;
            }
        }

        /// <summary>
        /// What the label says.
        ///
        /// Vanilla asks <c>ActiveBehaviour.HumanoidRoleOwner
        /// .GetDefaultDisplayNameRole()</c>, which is "Denys (Marauder)" for a
        /// raider. A turned settler is on EnemyBehaviour by now, so the same
        /// call is the right one to try - but a body that was never an NPC can
        /// come back from it empty, and an empty label is the bug being fixed.
        /// The settler's own name is the fallback, because a walker the player
        /// used to feed is worth naming.
        /// </summary>
        private static string Label(HumanoidInstance humanoid)
        {
            try
            {
                var display = humanoid.ActiveBehaviour?.HumanoidRoleOwner?
                    .GetDefaultDisplayNameRole();
                if (!string.IsNullOrWhiteSpace(display)) return display;
            }
            catch (Exception)
            {
                // A role owner that is not there is not an error here: it is
                // the ordinary shape of a settler who has changed sides.
            }

            return GameAccess.Name(humanoid);
        }

        private static readonly AccessTools.FieldRef<WorkerView, TextFloatingElement>
            NameElementRef = Element();

        private static AccessTools.FieldRef<WorkerView, TextFloatingElement> Element()
        {
            try
            {
                return AccessTools.FieldRefAccess<WorkerView, TextFloatingElement>(
                    "workerNameElement");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] workerNameElement unreachable: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Lets a turned settler's body be cleaned up.
    ///
    /// In the log of the 8th, three walkers and three copies of the same line:
    /// <c>InvalidCastException</c> in <c>WorkerBehaviour.Dispose</c> at line
    /// 443, inside <c>HumanoidInstance.FinalizeDispose</c>. That line is
    /// <c>(IFormCaravanGoapAgent)GoapAgent</c>, asked so the game can cancel a
    /// caravan the settler was about to leave with.
    ///
    /// A settler's agent implements that interface. A walker's does not - the
    /// horde does not form caravans - and the body still carries the
    /// WorkerBehaviour it was born with, so when the walker is finally put down
    /// and the game disposes it, the cast throws. Everything
    /// <c>FinalizeDispose</c> had left to do after that point never runs, which
    /// is a body half torn down and a save carrying it.
    ///
    /// A finalizer, not a prefix: the rest of <c>Dispose</c> - the base call,
    /// the pets - is work that should still happen, and only what comes after
    /// the cast is lost. What is lost is caravan bookkeeping for a creature
    /// that cannot be in a caravan.
    /// </summary>
    [HarmonyPatch(typeof(WorkerBehaviour), nameof(WorkerBehaviour.Dispose))]
    internal static class RisenDisposeGuard
    {
        private static Exception Finalizer(Exception __exception, WorkerBehaviour __instance)
        {
            if (__exception == null) return null;
            if (!(__exception is InvalidCastException)) return __exception;

            GMPlugin.Log?.LogInfo(
                "[risen] swallowed the caravan cast while disposing "
                + $"{GameAccess.Name(__instance?.Humanoid)} - a walker cannot be in a caravan");

            return null;
        }
    }
}
