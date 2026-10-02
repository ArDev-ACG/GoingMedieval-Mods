"""The cat statue, built the way the game builds its own animal statue.

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
        --python tools/models/build_cat_statue.py

Writes ASSESTS/Modelos/aldrich_cat_statue.fbx (and .obj, for looking at it
without Unity) plus the palette texture, straight into the mod.

Why this exists. The cat statue has been `cat_stuffed_trophy` wearing a repaint
since the day it was added, and the screenshot of it standing next to
`foxy_statue` is the whole argument: the fox is a chunky voxel animal in one
saturated colour on a faceted stone drum, taller than a settler; the cat is a
grey lump the size of a boot, lying on its side, because a stuffed trophy is an
inventory item and no scale turns one into the other.

The measurements are the game's own, out of tools/models/extract_reference.py:

    foxy_statue          0.965 x 1.577 x 1.208, pivot on the floor
    cat_stuffed_trophy   0.264 x 0.505 x 0.604, pivot 0.251 up (a trophy)
    statue_large_01      1.095 x 3.014 x 1.095

One tile is one unit, so the footprint has to stay under about 0.95 - and the
fox's 1.208 of depth says vanilla is willing to overhang a little for a tail.

What it is made of. Cubes, on purpose and all the way through. Foxy Voxel's
statue is not a smooth sculpt scaled down: it is readable blocks, and the
blockiness is the style rather than a budget. So the cat is laid out as a voxel
figure on a grid of `V`, and every piece is an axis-aligned box of whole
voxels. A sitting cat is the one pose that reads as a cat from any angle at
forty pixels: haunches, straight front legs, a tail curled round the base, and
ears.

Materials are three flat colours on a 2x2 palette, the same trick the throne
uses - see `palette_uv` there for why a real unwrap would be wasted here.
"""

import os
import sys

import bpy
from mathutils import Vector

# --------------------------------------------------------------------------
# The envelope, in game units.
# --------------------------------------------------------------------------

V = 0.080           # one voxel. The cat is fifteen of these tall.

DRUM_R = 0.430      # the stone drum, kept inside the tile
DRUM_H = 0.320
BASE_H = 0.075      # the wider slab under it

# The square plinth the whole thing now stands on.
#
# Asked for in as many words - "debajo de los pies o donde empieza la estatua
# pon un bloque gris mas grande del ancho de la estatua" - and it earns its
# place: a round slab on a square tile reads as a prop dropped on the floor,
# while a square block reads as something that was built there. 0.960 is the
# widest it can be: the tile is 1.000 and nothing of ours may ask for more than
# about 0.95 of it, or the build preview clips the neighbour.
PLINTH_W = 0.960
PLINTH_H = 0.070

# The fox's gold. It has to be this saturated: a pale version reads as unpainted
# stone next to it, which is the difference between "a statue" and "the statue
# they have not finished yet".
FUR = (0.78, 0.50, 0.05, 1.0)
STONE = (0.44, 0.46, 0.50, 1.0)
DARK = (0.13, 0.12, 0.14, 1.0)

# The fourth and last slot the 2x2 palette has. It exists for the eyes and for
# nothing else, which is the whole reason the cat now has a face at forty
# pixels instead of two dark dents.
WHITE = (0.92, 0.92, 0.90, 1.0)


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.55

    return mat


def voxel_box(name, cells, at):
    """A box measured in voxels and placed by its own bottom-centre.

    `cells` is (wide, deep, tall) in voxels and `at` is (x, y, z) in game
    units, with z the floor of the box rather than its middle. Every number
    below is therefore a count of blocks and a height off the pedestal, which
    is the only way a figure made of forty boxes stays editable.
    """
    size = Vector((cells[0] * V, cells[1] * V, cells[2] * V))

    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    obj.location = Vector((at[0], at[1], at[2] + size.z / 2.0))
    return obj


def slab(name, width, height, z):
    """A square block, placed by its own bottom face, centred on the tile.

    The pedestal's own shape is a ten-sided drum, which is right for the
    statue and wrong for what holds it up: a square tile wants a square base
    under it, and the contrast between the two is most of what makes the drum
    read as turned stone.
    """
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = Vector((width, width, height))
    obj.location = Vector((0.0, 0.0, z + height / 2.0))
    return obj


def drum(name, radius, height, at, sides=10):
    """The faceted stone cylinder the fox stands on."""
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=height,
        location=(at[0], at[1], at[2] + height / 2.0))
    obj = bpy.context.active_object
    obj.name = name
    return obj


def join(parts, name):
    """Welds a list of objects into one standing at the world origin.

    Blender's join hands the result the transform of the *first* object in the
    list and rewrites everyone else to suit, so the transform is applied right
    afterwards: from here on local space and world space are the same thing,
    and a height test means what it says. The throne script learned this the
    expensive way - see the note in tools/models/build_count_throne.py.
    """
    bpy.ops.object.select_all(action="DESELECT")

    for part in parts:
        part.select_set(True)

    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    joined = bpy.context.active_object
    joined.name = name

    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return joined


def paint(parts, name, material):
    group = join(parts, name)

    group.data.materials.clear()
    group.data.materials.append(material)

    for poly in group.data.polygons:
        poly.material_index = 0

    return group


def build():
    clear()

    materials = [material("fur", FUR), material("stone", STONE),
                 material("dark", DARK), material("white", WHITE)]

    fur = []
    stone = []
    dark = []
    white = []

    # ---- the pedestal -----------------------------------------------------
    #
    # A slab, a drum, and a dark band where the two meet. The band is the only
    # reason the base does not read as one grey cylinder at play distance.
    # 1.09, not 1.16: at 1.16 the slab measures 0.998 across and the build
    # preview clips the neighbouring tile. The tile is 1.000 and nothing of
    # ours is allowed to need more than about 0.95 of it.
    stone.append(slab("plinth", PLINTH_W, PLINTH_H, 0.0))
    dark.append(slab("plinth_lip", PLINTH_W * 0.98, 0.022, PLINTH_H))

    foot = PLINTH_H + 0.022
    stone.append(drum("base", DRUM_R * 1.09, BASE_H, (0, 0, foot), sides=10))
    dark.append(drum("base_band", DRUM_R * 1.04, 0.030, (0, 0, foot + BASE_H)))
    stone.append(drum("drum", DRUM_R, DRUM_H, (0, 0, foot + BASE_H + 0.030), sides=10))
    dark.append(drum("drum_lip", DRUM_R * 1.06, 0.028,
                     (0, 0, foot + BASE_H + 0.030 + DRUM_H)))

    top = foot + BASE_H + 0.030 + DRUM_H + 0.028      # where the cat sits

    # ---- the cat, sitting, facing -Y --------------------------------------
    #
    # Read from the ground up. Every box is whole voxels, and the widths step
    # inward as they rise, which is what makes a stack of cubes read as an
    # animal rather than as a tower.

    # It narrows all the way up, without exception. The first pass had the
    # shoulders wider than the chest below them - anatomically true of a real
    # cat, and at forty pixels it turned the whole animal into a stepped tower.
    # A silhouette that only ever gets smaller is what reads as a seated
    # animal.

    # Haunches: the widest thing on the statue, and what a sitting cat is
    # mostly made of. Set back, because the mass of a sitting cat is behind it.
    fur.append(voxel_box("haunches", (6, 5, 4), (0, 0.075, top)))
    fur.append(voxel_box("rump", (4, 3, 2), (0, 0.150, top + 4 * V)))

    # Front legs, straight down to the pedestal, with paws in front of them.
    # Straight front legs are half of what makes the pose read as sitting.
    for side in (-1, 1):
        x = side * 1.5 * V
        fur.append(voxel_box(f"foreleg_{side}", (1, 2, 4), (x, -0.105, top)))
        fur.append(voxel_box(f"paw_{side}", (1, 1, 1), (x, -0.175, top)))

    # Belly and chest, each a step narrower than what is under it.
    fur.append(voxel_box("belly", (5, 4, 3), (0, -0.010, top + 4 * V)))
    fur.append(voxel_box("chest", (4, 3, 2), (0, -0.045, top + 7 * V)))

    # A neck, and a real one: two voxels of nothing between chest and head is
    # what stops the head from being read as the next storey of the body.
    fur.append(voxel_box("neck", (2, 2, 1), (0, -0.045, top + 9 * V)))

    # Head. Narrower than the chest, pushed forward over the paws.
    head_z = top + 10 * V
    fur.append(voxel_box("head", (4, 3, 3), (0, -0.070, head_z)))
    fur.append(voxel_box("muzzle", (2, 1, 1), (0, -0.170, head_z)))

    # Ears: two voxels each, standing on the outer top corners of the head and
    # tapering. Short and wide reads as a cat; tall and thin reads as a hare,
    # which is what the first pass produced.
    for side in (-1, 1):
        x = side * 1.5 * V
        fur.append(voxel_box(f"ear_{side}", (1, 1, 1), (x, -0.070, head_z + 3 * V)))
        fur.append(voxel_box(f"ear_tip_{side}", (1, 1, 1),
                             (x + side * 0.5 * V, -0.070, head_z + 4 * V)))

    # The face.
    #
    # It used to be two dark voxels and a dark voxel, all three the same
    # colour as the shadow under the chin, which at play distance is a cat with
    # no face at all. Now each eye is a white block with a smaller dark one set
    # into the front of it - that pair is what the eye actually reads as, and
    # the small one is what gives the animal a direction to be looking in - and
    # the nose is a flat black patch on the muzzle rather than a cube stuck
    # onto the end of it.
    for side in (-1, 1):
        x = side * 1.0 * V
        white.append(voxel_box(f"eye_{side}", (1, 1, 1), (x, -0.135, head_z + 2 * V)))
        dark.append(voxel_box(f"pupil_{side}", (0.5, 0.35, 0.5),
                              (x, -0.152, head_z + 2.25 * V)))

    dark.append(voxel_box("nose", (1, 0.35, 1), (0, -0.216, head_z + 0.2 * V)))

    # Tail, curled round the right of the pedestal and coming back up. Four
    # boxes, because a curve made of forty would be a smooth tail on a blocky
    # cat.
    fur.append(voxel_box("tail_root", (1, 3, 1), (2.5 * V, 0.190, top)))
    fur.append(voxel_box("tail_side", (3, 1, 1), (0.100, 0.020, top)))
    fur.append(voxel_box("tail_bend", (1, 2, 1), (3.5 * V, -0.090, top)))
    fur.append(voxel_box("tail_tip", (1, 1, 2), (3.5 * V, -0.130, top)))

    # ---- one object, three material slots ---------------------------------
    statue = join([paint(fur, "fur_group", materials[0]),
                   paint(stone, "stone_group", materials[1]),
                   paint(dark, "dark_group", materials[2]),
                   paint(white, "white_group", materials[3])],
                  "aldrich_cat_statue")

    finish(statue)
    return statue


def finish(statue):
    """Weld, face the normals outward, flat-shade, and lay the palette on."""
    bpy.context.view_layer.objects.active = statue
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0008)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    palette_uv(statue)
    one_submesh(statue)
    bpy.ops.object.shade_flat()

    ground(statue)


def ground(obj):
    """Centre of the footprint and base of the mesh onto the tile's origin.

    Every reference mesh in the game has its pivot there - quality_chair,
    statue_large_01 and foxy_statue all measure a base of 0.000 - and anywhere
    else means the plugin has to nudge it at runtime, which is the entire cat
    statue story up to now.
    """
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    shift = Vector((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))

    for vertex in obj.data.vertices:
        vertex.co += shift


def measure(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)), min(zs)


PALETTE = 2


def palette_uv(statue):
    """Points every face at the centre of its own material's swatch.

    Three flat colours do not need texture space per face, they need three
    colours and every face pointing at the right one. Landing dead in the
    middle of a 2x2 swatch means the colour survives any filtering and any mip
    level, and the whole albedo is a few hundred bytes.
    """
    mesh = statue.data
    uvs = mesh.uv_layers.active or mesh.uv_layers.new(name="palette")

    for poly in mesh.polygons:
        slot = min(poly.material_index, PALETTE * PALETTE - 1)

        u = (slot % PALETTE + 0.5) / PALETTE
        v = (slot // PALETTE + 0.5) / PALETTE

        for loop in poly.loop_indices:
            uvs.data[loop].uv = (u, v)



def one_submesh(obj):
    """Collapses the three material slots into one, after the UVs are laid.

    <b>This is "los colores del trono".</b> The throne came out of the game a
    single flat bone-white and the cat statue a single flat orange - in both
    cases the colour of the <em>first</em> square of their own palette - and
    every check that can be made outside the game said it should not: the mesh
    stands up at the right size, it has its UVs in TEXCOORD0 exactly where
    `quality_chair` keeps its own, those UVs land on distinct swatch centres,
    the albedo PNG has its four colours, and the log says the texture reached
    the repository.

    What none of those checks looked at was the <em>submesh count</em>. Painting
    each group with its own material gives the joined object three material
    slots, and Unity turns one material slot into one submesh - so the mesh in
    the bundle arrived with three. Every mesh this game ships has exactly one
    (`quality_chair`, `wood_chair`, `cat_stuffed_trophy`, `foxy_statue`,
    `limestone_sarcophagus`: all 1), because the building renderers carry a
    single material - the game's own log says so, `materials=1` on the finished
    renderer. Unity draws `min(subMeshCount, materials.Length)` submeshes, so
    submesh 0 was drawn and the other two were silently dropped: the stone and
    the gold were never on screen at all, and what was left was the bone group,
    one flat colour, exactly as reported.

    The material slots are not carrying the colour here - the palette UVs are -
    so there is nothing to lose by folding them into one. This runs after
    `palette_uv`, which is the only thing that still needs to know which group a
    face belonged to.
    """
    mesh = obj.data

    for poly in mesh.polygons:
        poly.material_index = 0

    keep = mesh.materials[0] if len(mesh.materials) else None
    mesh.materials.clear()

    if keep is not None:
        mesh.materials.append(keep)

def bake_albedo(path, size=64):
    """Writes the 2x2 palette sheet the UVs above point into."""
    import numpy

    colours = [FUR, STONE, DARK, WHITE]
    step = size // PALETTE

    flat = numpy.ones((size, size, 4), dtype=numpy.float32)

    for slot, colour in enumerate(colours):
        col = slot % PALETTE
        row = slot // PALETTE
        flat[row * step:(row + 1) * step, col * step:(col + 1) * step, :3] = colour[:3]

    image = bpy.data.images.new("aldrich_cat_statue_albedo", size, size, alpha=True)
    image.pixels = flat.reshape(-1)

    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()

    return path


def export(statue, folder):
    """Writes the FBX with the axis change baked into the vertices.

    See the long note in tools/models/build_count_throne.py: `axis_up="Y"` on
    its own moves no vertices, it writes the correction into the exported
    object's node transform, and the game reads only the Mesh. So
    `bake_space_transform` is on - and because that flag also bakes Blender's
    unit conversion, `global_scale` has to undo the hundredfold it brings.

    The check is the height: everything the game ships is tall in **y**.
    """
    os.makedirs(folder, exist_ok=True)

    fbx = os.path.join(folder, "aldrich_cat_statue.fbx")
    obj = os.path.join(folder, "aldrich_cat_statue.obj")

    bpy.ops.object.select_all(action="DESELECT")
    statue.select_set(True)
    bpy.context.view_layer.objects.active = statue

    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, apply_unit_scale=True,
        global_scale=0.01, apply_scale_options="FBX_SCALE_NONE",
        object_types={"MESH"}, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, bake_space_transform=True,
        axis_forward="-Z", axis_up="Y")

    bpy.ops.wm.obj_export(filepath=obj, export_selected_objects=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y")

    return fbx, obj


def main():
    statue = build()

    size, base = measure(statue)
    tris = sum(len(p.vertices) - 2 for p in statue.data.polygons)

    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    fbx, obj = export(statue, os.path.join(here, "ASSESTS", "Modelos"))

    albedo = bake_albedo(os.path.join(
        os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval", "Mods",
        "CarrionAndPlague", "Data", "Textures", "aldrich_cat_statue_albedo.png"))

    print("")
    print("=== aldrich_cat_statue ===")
    print(f"  size      {size[0]:.3f} x {size[1]:.3f} x {size[2]:.3f}   (the tile is 1.000)")
    print(f"  base at   {base:+.4f}   (0 means the pivot is on the floor)")
    print(f"  triangles {tris}")
    print("  foxy_statue, for comparison: 0.965 wide, 1.208 deep, 1.577 tall")
    print(f"  -> {fbx}")
    print(f"  -> {obj}")
    print(f"  -> {albedo}")

    if size[0] > 0.98 or size[1] > 0.98:
        print("  WARNING: wider than its own tile", file=sys.stderr)


main()
