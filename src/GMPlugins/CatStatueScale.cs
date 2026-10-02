using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.BuildingComponents;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Blows the cat statue up to the size of a statue, and puts it back over
    /// the middle of its own tile afterwards.
    ///
    /// `cat_stuffed_trophy` is the mesh of an inventory item - about half a
    /// unit tall - while `foxy_statue`, which is what a statue looks like in
    /// this game, stands 3.08 units in its box collider. That gap is the whole
    /// reason the cat reads as an ornament rather than a monument.
    ///
    /// There is no JSON way to close it: a building's `transformSettingsArray`
    /// is only ever read for the pile it leaves behind when dismantled, so the
    /// scale has to be set on the view. `InitModelSettings()` runs whenever the
    /// model is prepared, including on the way out of the pool, and everything
    /// here is assigned absolutely rather than accumulated, so running twice is
    /// harmless.
    ///
    /// <para><b>Why the second half exists.</b> Scaling a transform scales the
    /// distance from its own pivot too. The trophy's pivot is not the middle of
    /// its base - it is wherever the artist left it, which at 1x is a couple of
    /// centimetres nobody notices, and at 4x is most of a tile: the statue
    /// walked off its plinth and sank into the floor. So the model is nudged
    /// back by (1 - scale) x pivot-to-anchor, which pins the footprint centre
    /// and the underside of the mesh exactly where they were before the scale,
    /// and lets the growth go upward where it belongs.</para>
    ///
    /// The measurements come off the mesh, not off the renderer: mesh bounds are
    /// author-space and constant, so they can be taken once and cached, while
    /// renderer bounds change with the very scale being applied here and would
    /// feed the correction back into itself.
    ///
    /// The factor is a BepInEx config entry, not a constant: the right number is
    /// a matter of taste, and far easier to find by nudging
    /// BepInEx/config/aldrich.gmplugins.cfg between two loads than by editing
    /// this file and recompiling.
    /// </summary>
    [HarmonyPatch]
    internal static class CatStatueScale
    {
        private const string BuildingId = "cat_statue";

        /// <summary>
        /// Author-space anchor and untouched local position of every model part
        /// this has already measured, keyed by the part's instance id. Building
        /// views are pooled, so the same objects come back around and their
        /// original placement has to survive the first scaling.
        /// </summary>
        private static readonly Dictionary<int, PartPlacement> Measured =
            new Dictionary<int, PartPlacement>();

        private struct PartPlacement
        {
            public Vector3 LocalPosition;

            /// <summary>Footprint centre and underside of the mesh, in part space.</summary>
            public Vector3 Anchor;

            public bool HasAnchor;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(BaseBuildingViewComponent), "InitModelSettings");
        }

        /// <summary>
        /// Said once per session: what the mesh actually measures, what it was
        /// multiplied by, and where the selection box ended up.
        ///
        /// Two rounds of "sigue sin estar centrada" and "la caja sigue mal"
        /// went by while nobody had read the numbers. They are read now, out of
        /// the bundle: <c>cat_stuffed_trophy</c> is 0.264 x 0.505 x 0.604, its
        /// pivot sits 0.251 <em>above</em> the base and 0.119 off centre in x,
        /// and a tile is one unit across. Four times that is a cat two and a
        /// half tiles deep, and no collider arithmetic was ever going to make
        /// that look right.
        /// </summary>
        private static bool said;

        /// <summary>
        /// How much of its own tile the statue may fill. Vanilla's one-tile
        /// statues measure 0.96 across (foxy_statue) and 1.10
        /// (statue_large_01, which overhangs a little on purpose), so a whole
        /// tile is the generous end of normal rather than a tight rule.
        /// </summary>
        private const float TileRoom = 1.0f;

        /// <summary>Said once, not once per statue per load.</summary>
        private static bool told;

        /// <summary>
        /// Whether this blueprint names a mesh of ours.
        ///
        /// Read off the variation slots rather than kept as a constant, so the
        /// day the coffin or the crypt gets its own mesh this class does not
        /// need to be told about it: the prefix is the whole test.
        /// </summary>
        private static bool OwnMesh(BaseBuildingBlueprint blueprint)
        {
            var lists = blueprint?.VariationLists;
            if (lists == null) return false;

            foreach (var list in lists)
            {
                if (list?.Variations == null) continue;

                foreach (var variation in list.Variations)
                {
                    if (variation?.Slots == null) continue;

                    foreach (var slot in variation.Slots)
                    {
                        if (slot == null || slot.Slot != "baseMesh") continue;
                        if (slot.Value != null && slot.Value.StartsWith("aldrich_")) return true;
                    }
                }
            }

            return false;
        }

        private static void Postfix(BaseBuildingViewComponent __instance)
        {
            var wanted = GMPlugin.CatStatueScale?.Value ?? 1f;
            if (__instance == null) return;

            var blueprint = GameAccess.BuildingBlueprint(__instance);
            if (blueprint == null || blueprint.GetID() != BuildingId) return;

            // Nothing to do once the statue has a mesh of its own.
            //
            // This whole class exists because `cat_stuffed_trophy` is an
            // inventory trophy - 0.264 x 0.505 x 0.604, lying on its side - and
            // a runtime scale was the only lever there was. `aldrich_cat_statue`
            // is built against `foxy_statue` instead: 0.892 x 1.653 x 0.937,
            // sitting, pivot on the floor, already the size it means to be.
            // Scaling that is how a statue ends up through the ceiling, and the
            // `.cfg` on this machine still says 2 from the trophy days - a new
            // default cannot fix that, only a rule can. See the note further
            // down about BepInEx writing the file once.
            if (OwnMesh(blueprint))
            {
                if (told) return;

                told = true;
                GMPlugin.Log?.LogInfo(
                    "[cat] the statue has a mesh of its own now - leaving its scale alone");
                return;
            }

            var scale = Fit(__instance, wanted);
            if (Mathf.Approximately(scale, 1f)) return;

            GameObject finished = null;
            var index = 0;

            foreach (var part in GameAccess.BuildingModelParts(__instance))
            {
                var slot = index++;
                if (part == null) continue;

                // BuildingModelParts hands back "finished", "blueprint" and
                // "foundation", in that order. The foundation is the flat slab
                // the game draws under a building's footprint, and it is
                // already the size of the tile: growing it four times over is
                // what put a plate of stone sticking out to one side of the
                // statue. Only the two that are the statue get scaled.
                if (slot > 1) continue;

                var placement = Placement(part);

                part.transform.localScale = Vector3.one * scale;

                if (placement.HasAnchor && (GMPlugin.CatStatueRecenter?.Value ?? true))
                {
                    // Not "back where the pivot left it at 1x" - that was the
                    // old arithmetic, and it preserved an offset that was
                    // already wrong. `cat_stuffed_trophy` is an inventory mesh:
                    // its pivot is wherever the artist needed it for a hand to
                    // hold it, so even unscaled the cat sat off the middle of
                    // its tile. Multiplying that by four is what "corrida a la
                    // izquierda" was.
                    //
                    // So the anchor - footprint centre, underside of the mesh -
                    // is put on the part's own origin instead, which is the
                    // centre of the tile at floor level. Whatever the pivot is,
                    // the cat ends up standing in the middle of its square with
                    // its feet on the ground.
                    part.transform.localPosition = -scale * placement.Anchor;
                }

                if (slot == 0) finished = part;
            }

            // The finished mesh, and nothing else. Measuring against the
            // foundation - a flat slab - is what left the selection box lying
            // in the floor at the size of the footprint, which is exactly the
            // "caja por debajo del terreno" that was reported.
            if (finished != null) FitColliders(__instance, finished);
        }

        /// <summary>
        /// Wraps the selection box around the statue that is actually there,
        /// and moves what hangs off that box with it.
        ///
        /// <b>Where the box comes from.</b> Not from any of the code above it:
        /// <c>HandleSelectionBoxCollider</c> writes it, straight out of the
        /// blueprint's <c>boxColliderSettings</c>, onto a BoxCollider that
        /// lives on the view root - and it is the <em>first</em> thing
        /// <c>InitModelSettings</c> does, before the model is even assembled.
        /// So the JSON numbers are the box, and for the cat those numbers are
        /// 0.85 wide by 0.95 tall: the trophy's own size, written when the
        /// statue was still trophy-sized. Multiply the mesh and the box stays
        /// where it was, which is a hitbox in the floor with a cat standing
        /// over it.
        ///
        /// Being measured off the mesh after the scale and the nudge are on the
        /// transform is what keeps the two in step: there is no second copy of
        /// the arithmetic to get wrong.
        ///
        /// The destruction marker and the resource indicator are parked on
        /// <c>box.center</c> by that same method, so they are moved here too.
        /// Leaving them behind is how a statue ends up with its damage numbers
        /// coming out of the ground beside it.
        /// </summary>
        private static void FitColliders(BaseBuildingViewComponent view, GameObject model)
        {
            var boxes = view.GetComponentsInChildren<BoxCollider>(true);
            var wrote = false;
            var centre = Vector3.zero;

            foreach (var box in boxes)
            {
                if (box == null || box.transform.IsChildOf(model.transform)) continue;
                if (!TryMeshBounds(model, box.transform, out var bounds)) continue;

                box.center = bounds.center;
                box.size = bounds.size;

                if (box.transform != view.transform) continue;

                wrote = true;
                centre = bounds.center;
            }

            if (!wrote) return;

            Follow(view, "destructionMarker", centre);
            Follow(view, "indicatorUI", centre);

            if (said) return;

            said = true;
            GMPlugin.Log?.LogInfo(
                $"[cat] selection box now centre {centre} size {Size(view)}");
        }

        private static Vector3 Size(BaseBuildingViewComponent view)
        {
            var box = view.GetComponent<BoxCollider>();
            return box == null ? Vector3.zero : box.size;
        }

        /// <summary>
        /// Moves one of the two things the game parks on the box centre. Both
        /// are non-public fields, and one of them is a component rather than a
        /// GameObject, so the transform is fished out of whichever it turns out
        /// to be.
        /// </summary>
        private static void Follow(BaseBuildingViewComponent view, string field, Vector3 where)
        {
            var value = AccessTools.Field(typeof(BaseBuildingViewComponent), field)?.GetValue(view);

            var transform = (value as GameObject)?.transform ?? (value as Component)?.transform;
            if (transform == null) return;

            transform.localPosition = where;
        }

        /// <summary>
        /// The largest of the asked-for scale and what actually fits the tile.
        ///
        /// <b>Why a default was not enough.</b> The scale was lowered from 4 to
        /// 2 by changing the config default - and nothing changed in game,
        /// because BepInEx writes the config file on first run and reads that
        /// file ever after. <c>aldrich.gmplugins.cfg</c> still said
        /// <c>Scale = 4</c>, so the statue was still four times over while the
        /// collider written into the JSON alongside it had been authored for
        /// two. The two disagreed, and the box ended up in the floor again.
        ///
        /// A default cannot fix a file that already exists, so the rule stops
        /// being a default: the mesh is measured, and any scale that would push
        /// the statue outside its own tile is brought back to the one that just
        /// fits. Whatever the config says, and whatever it said last week, the
        /// cat stands inside its square.
        ///
        /// Height is deliberately not capped. A statue is allowed to be tall -
        /// that is what makes it a statue - and nothing collides upwards.
        /// </summary>
        private static float Fit(BaseBuildingViewComponent view, float wanted)
        {
            if (wanted <= 1f) return wanted;

            foreach (var part in GameAccess.BuildingModelParts(view))
            {
                if (part == null) continue;
                if (!TryMeshBounds(part, part.transform, out var bounds)) continue;

                var widest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (widest <= 0.0001f) continue;

                var room = TileRoom / widest;
                if (wanted <= room) return wanted;

                if (!capped)
                {
                    capped = true;
                    GMPlugin.Log?.LogWarning(
                        $"[cat] scale {wanted:0.##} would make the statue "
                        + $"{widest * wanted:0.00} tiles across; holding it at {room:0.##}. "
                        + "Edit CatStatue/Scale in aldrich.gmplugins.cfg to choose your own.");
                }

                return room;
            }

            return wanted;
        }

        private static bool capped;

        /// <summary>
        /// Measures a part the first time it is seen and remembers it. The
        /// first sighting is the only moment the transform is still the one the
        /// prefab shipped with.
        /// </summary>
        private static PartPlacement Placement(GameObject part)
        {
            var key = part.GetInstanceID();
            if (Measured.TryGetValue(key, out var known)) return known;

            var placement = new PartPlacement
            {
                LocalPosition = part.transform.localPosition
            };

            if (TryMeshBounds(part, part.transform, out var bounds))
            {
                placement.Anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                placement.HasAnchor = true;

                GMPlugin.Log?.LogInfo(
                    $"[cat] '{part.name}' measures {bounds.size} with its pivot "
                    + $"{-bounds.min.y:0.000} above the base and {bounds.center.x:+0.000;-0.000} "
                    + "off centre in x - one tile is one unit");
            }

            Measured[key] = placement;
            return placement;
        }

        /// <summary>
        /// Every mesh under the part, expressed in the part's own space.
        ///
        /// Going through world space and back cancels the part's own transform,
        /// so the answer does not depend on the scale currently on it - which is
        /// what makes it safe to call on a pooled object that has already been
        /// through here once.
        /// </summary>
        private static bool TryMeshBounds(GameObject part, Transform space, out Bounds bounds)
        {
            bounds = default;
            var found = false;

            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var local = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);

                    var inPartSpace = space.InverseTransformPoint(
                        filter.transform.TransformPoint(point));

                    if (!found)
                    {
                        bounds = new Bounds(inPartSpace, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(inPartSpace);
                    }
                }
            }

            return found;
        }
    }
}
