using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Model;
using NSMedieval.Repository;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Gives a neutral quality base to any resource that says it has quality
    /// and has none, so that walking over a pile of one stops being an
    /// exception.
    ///
    /// <b>The report.</b> "Salio un error mientras un zombie golpeaba a una
    /// colona". The log has 3305 copies of one NullReferenceException in a
    /// single session, and the fight is a coincidence: the top of the stack is
    /// <c>Resource.BeautyInputInside</c>, reached from
    /// <c>MapNode.ForceRefreshBeautyInput</c> by way of
    /// <c>CreatureBase.OnGridSpaceChanged</c>. Something <em>steps onto a
    /// tile</em>, the tile recounts its beauty, and a pile on it answers with an
    /// exception. A melee is only when bodies cross that tile most often.
    ///
    /// <b>The cause.</b> That getter reads
    /// <c>hasQuality ? productQualityBase.BeautyInputInsideAdd : 0</c>, and
    /// <c>productQualityBase</c> is a plain serialized field - nothing fills it
    /// in at runtime. The game's own quality items get theirs from
    /// <c>CacheEquipmentQualityItems</c>, which builds one resource per material
    /// and quality out of each proto. A proto with no <c>materials</c> -
    /// <c>undead_claws</c>, the horde's hands - produces no variants, so the
    /// proto itself is what the walker carries, drops and leaves lying there,
    /// with the field still null.
    ///
    /// <b>Why not just turn <c>hasQuality</c> off.</b> Because it is load
    /// bearing the other way: <c>EquipmentRepository</c> only looks for
    /// equipment among <c>ProtoItems</c>, and that list is exactly
    /// <c>AllItems.Where(r =&gt; r.HasQuality)</c>. Off, and the claws are never
    /// registered as equipment at all - the horde spawns bare handed.
    /// <c>tools/validate/check_refs.py</c> refuses the change in as many words,
    /// and it refuses it because that is how the rule was learned the first
    /// time.
    ///
    /// So the field is filled instead, once, with a quality base that changes
    /// nothing: multipliers of one, adds of zero. The beauty sum gets its zero,
    /// hitpoints and wealth stay where they were, and nothing pays for it per
    /// call - this runs after the repository builds its quality cache, and
    /// never again.
    /// </summary>
    [HarmonyPatch]
    internal static class QualityBaseGuard
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ResourceRepository), "CacheEquipmentQualityItems");
        }

        /// <summary>
        /// One instance for all of them. A quality base carries no identity
        /// beyond its numbers, and these numbers are the identity element.
        /// </summary>
        private static ProductQualityBase neutral;

        private static void Postfix(ResourceRepository __instance)
        {
            var items = Items(__instance);
            if (items == null) return;

            var scanned = 0;
            var filled = new List<string>();

            foreach (var resource in items)
            {
                if (resource == null || !resource.HasQuality) continue;

                scanned++;
                if (resource.ProductQualityBase != null) continue;

                if (neutral == null && !Build()) return;
                if (!Write(resource, "productQualityBase", neutral)) continue;

                filled.Add(resource.GetID());
            }

            // A guard that repairs nothing and says nothing is
            // indistinguishable from a guard that is not running, and that is
            // exactly how this bug came back: the sweep was reading a list the
            // broken resource was never in, and the only sign of it was the
            // silence. Every run now leaves a line saying what was looked at.
            if (scanned == 0)
            {
                GMPlugin.Log?.LogWarning(
                    "[quality] no resource with quality was reachable from the repository - "
                    + "the guard swept nothing and a pile with no quality base can still throw");
                return;
            }

            if (filled.Count == 0)
            {
                GMPlugin.Log?.LogInfo(
                    "[quality] " + scanned + " resource(s) with quality, all of them already "
                    + "carrying a quality base; nothing to fill in");
                return;
            }

            // Named, not counted: the whole difficulty was that nobody could
            // tell which pile on which tile was the one answering.
            GMPlugin.Log?.LogInfo(
                "[quality] " + filled.Count + " of " + scanned + " resource(s) claimed quality and "
                + "had no quality base; gave them a neutral one so a pile of them can be walked "
                + "over: " + string.Join(", ", filled.ToArray()));
        }

        /// <summary>
        /// Builds the one neutral base. Every field is private and serialized,
        /// like the rest of this game's models, so they are written by
        /// reflection - and a failure to write either multiplier is a reason
        /// not to install it at all: a base with a zero hitpoints multiplier
        /// would be worse than the null it replaces.
        /// </summary>
        private static bool Build()
        {
            var built = new ProductQualityBase();

            if (!Write(built, "hitpointsMultiplier", 1f)) return false;
            if (!Write(built, "wealthPointsMultiplier", 1f)) return false;

            Write(built, "beautyInputAdd", 0f);
            Write(built, "beautyInputEquippedAdd", 0f);
            Write(built, "beautyInputOnShelfAdd", 0f);
            Write(built, "beautyInputInsideAdd", 0f);

            neutral = built;
            return true;
        }

        /// <summary>
        /// Writes one serialized field, and says so once if the name is not
        /// there any more. A rename in a game update would otherwise look
        /// exactly like the bug being back.
        /// </summary>
        private static bool Write(object target, string field, object value)
        {
            if (target == null) return false;

            var info = AccessTools.Field(target.GetType(), field);
            if (info == null)
            {
                if (missing.Add(field))
                {
                    GMPlugin.Log?.LogWarning(
                        "[quality] " + target.GetType().Name + " has no field '" + field
                        + "' any more - the guard cannot fill it in");
                }

                return false;
            }

            info.SetValue(target, value);
            return true;
        }

        private static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>
        /// Every resource the repository holds, protos included.
        ///
        /// <b>Why both lists, and why that is the whole bug.</b>
        /// <c>GetAllItems</c> fills <c>allItems</c> with
        /// <c>regularItems ∪ qualityItems ∪ structurePilesCache</c> - and
        /// <c>protoItems</c> is not one of the three. A proto with no
        /// <c>materials</c> produces no quality variants, so it appears in
        /// <em>none</em> of those three either: it is only ever itself, in
        /// <c>protoItems</c>, and it is exactly the kind of resource whose
        /// <c>productQualityBase</c> is null.
        ///
        /// Reading <c>allItems</c> and treating the protos as a fallback for
        /// when it came back empty therefore skipped the only resources that
        /// needed the repair, every single run, without a word. The two lists
        /// are swept together now; a resource in both is repaired once,
        /// because the second visit finds the field already written.
        ///
        /// <c>allItems</c> is a backing list rather than a property, so it
        /// still comes out by reflection.
        /// </summary>
        private static IEnumerable<Resource> Items(ResourceRepository repository)
        {
            if (repository == null) return null;

            var list = new List<Resource>();

            var field = AccessTools.Field(typeof(ResourceRepository), "allItems");
            var all = field?.GetValue(repository) as IEnumerable;

            if (all == null && reportedAllItems == false)
            {
                reportedAllItems = true;
                GMPlugin.Log?.LogWarning(
                    "[quality] ResourceRepository has no readable 'allItems' any more - "
                    + "the sweep is running on the protos alone");
            }

            if (all != null)
            {
                foreach (var entry in all)
                {
                    if (entry is Resource resource) list.Add(resource);
                }
            }

            var protos = repository.ProtoItems;
            if (protos != null) list.AddRange(protos);

            return list;
        }

        private static bool reportedAllItems;
    }
}
