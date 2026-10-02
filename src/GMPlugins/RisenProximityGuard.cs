using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Stops a turned settler from throwing every time somebody walks past it.
    ///
    /// Thirty-nine of these in one session, all identical:
    ///
    ///   [ERR] [CreatureBase] Update proximity exception: NullReferenceException
    ///     at LabourerProximity.IsInteractionWithTimeout (target) [0x00002]
    ///     at LabourerProximity.TryInteract (creature) [0x00016]
    ///     at HumanoidInstance.OnCreatureEnterProximity (creature)
    ///     at CreatureBase.UpdateProximityObjectsAndCreatures (oldNode, newNode)
    ///
    /// Offset 0x00002 is the first thing the method does: read
    /// <c>allowedInteractionTypes</c> and ask it for an enumerator. The field is
    /// null, so the throw is on the first instruction and nothing about the
    /// target matters.
    ///
    /// <b>Whose field is null.</b> The same fact behind the missing nameplate
    /// and the half destroyed body: a colonist who turns keeps its
    /// <c>WorkerBehaviour</c>, and with it a <c>LabourerProximity</c>
    /// that the settler side of the game fills in on its own schedule. The
    /// horde's side never fills it, and then every colonist who walks within six
    /// tiles of that body tries to chat with it.
    ///
    /// The guard is the smallest one that ends it: with no list of allowed
    /// interactions there is no interaction to have, so the answer to "is this
    /// one on timeout" is yes, and <c>TryInteract</c> returns at its next
    /// branch - which is what vanilla does for a settler who greeted somebody a
    /// minute ago.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenProximityGuard
    {
        private static readonly FieldInfo Allowed =
            AccessTools.Field(typeof(LabourerProximity), "allowedInteractionTypes");

        private static readonly FieldInfo Timeouts =
            AccessTools.Field(typeof(LabourerProximity), "proximityHourTimeouts");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LabourerProximity), "IsInteractionWithTimeout");
        }

        /// <summary>Said once, because thirty-nine copies of it said nothing new.</summary>
        private static bool told;

        private static bool Prefix(LabourerProximity __instance, ref bool __result)
        {
            if (__instance == null) return true;
            if (Allowed == null || Timeouts == null) return true;

            var allowed = Allowed.GetValue(__instance) as IEnumerable<ProximityInteractionType>;
            var timeouts = Timeouts.GetValue(__instance)
                as Dictionary<ProximityInteractionType, float>;

            if (allowed != null && timeouts != null) return true;

            if (!told)
            {
                told = true;
                GMPlugin.Log?.LogInfo(
                    "[risen] a body with no proximity set up was asked to socialise - "
                    + "answering 'on timeout' from here on");
            }

            __result = true;   // nothing to say to anybody
            return false;
        }
    }
}
