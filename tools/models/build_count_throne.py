"""The Count's throne, built out of bones, in Blender with no window open.

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
        --python tools/models/build_count_throne.py

Writes ASSESTS/Modelos/aldrich_count_throne.fbx (and .obj, for looking at it
without Unity). Nothing is copied into a mod: the FBX is what Unity imports in
the next step of the queue.

Why procedural and not a sculpt. Going Medieval's furniture is faceted, chunky
and small - quality_chair is 1.484 tall and a few hundred triangles - and the
throne has to sit next to it without looking like it wandered in from another
game. Geometry that size is quicker to write than to model by hand, and
rebuilding it after "make the back taller" is one number rather than an
afternoon.

The measurements are not invented. They come out of
tools/models/extract_reference.py, which reads the real meshes from the game's
own bundles:

    quality_chair    0.749 x 0.901 x 1.484, pivot on the floor, centred
    foxy_statue      0.965 x 1.208 x 1.577
    statue_large_01  1.095 x 1.095 x 3.014

One tile is one unit, so anything wider than about 0.95 hangs out of its own
square - which is exactly the mistake the cat statue spent two test rounds on.
This is 0.86 wide, 0.92 deep and 1.86 tall: taller than the chair it replaces,
because a throne has to read as a throne from across the room, and still
inside its tile.

What it is made of. Castlevania sits Dracula in front of a high arched back
with a skull at the crown; the RimWorld vampire mods build their furniture out
of femurs and ribs with gold at the ends. Both agree on the silhouette that
reads as "vampire lord" at a glance and at low resolution - a tall narrow
back, a crest above head height, bone verticals, and metal catching the light
at the tips. So:

  - a stepped stone plinth, so it stands on something;
  - a seat slab with a bone rail along the front edge;
  - four femurs for legs;
  - arm rests of one long bone each, with a skull at the front of both;
  - a back of seven rib-shaped uprights either side of a spine column;
  - a crowning skull between two gold finials.

Three material slots - bone, stone, gold - assigned per part rather than
guessed at from position, and the albedo is a 2x2 palette every face points
into. See `paint` and `palette_uv`.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

# --------------------------------------------------------------------------
# The envelope, in game units. Everything below is derived from these.
# --------------------------------------------------------------------------

WIDTH = 0.94        # quality_chair is 0.749; the tile is 1.0 and this fills it
DEPTH = 0.96        # quality_chair is 0.901
# The board's own height. The spikes stand on top of it, so what the piece
# actually measures is about 0.18 more than this - see the print at the end.
# Kept where it is on purpose: statue_large_01 proves the game will draw a
# 3.014 prop, but a chair is furniture and goes indoors, and a floor here is
# one unit. Big enough to be a throne, short enough to sit under a roof.
HEIGHT = 2.05       # quality_chair is 1.484, foxy_statue 1.577

# A seat is a seat whatever it is made of: the throne got taller and wider, and
# the one measurement that must not follow is the one a person sits on.
SEAT_Z = 0.46
PLINTH_Z = 0.13
ARM_Z = 0.74

BONE = (0.86, 0.83, 0.74, 1.0)
STONE = (0.34, 0.33, 0.32, 1.0)
GOLD = (0.83, 0.66, 0.24, 1.0)


def clear():
    """An empty file, whatever Blender started with."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.3 if name == "gold" else 0.55
        if name == "gold" and "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 1.0

    return mat


def box(name, size, at, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = Vector(size)
    return obj


def cyl(name, radius, length, at, rot=(0, 0, 0), sides=8):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=length, location=at, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def ball(name, radius, at, segments=10, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, radius=radius, location=at)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def spike(name, radius, length, at, rot=(0, 0, 0), sides=6):
    """A tapered point, planted at `at` with its tip at +length.

    `primitive_cone_add` centres the cone on its own middle, so every caller
    would otherwise have to remember to lift each spike by half its length or
    watch it sink into whatever it is standing on. It is done here instead,
    once: `at` is where the spike is planted, not where its middle falls.

    Six sides, because everything else on this throne is faceted and a smooth
    cone next to a six-sided bone reads as a different model.
    """
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=0.0, depth=length,
        location=(at[0], at[1], at[2] + length / 2.0), rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def femur(name, length, at, rot=(0, 0, 0), thick=0.040):
    """A long bone: a shaft with a knuckle at each end.

    Two spheres and a cylinder is the whole of the shape at this size. The
    silhouette carries it, and a real femur's detail would be three pixels
    wide in play.

    <b>The thickness is absolute, not a fraction of the length.</b> It was a
    fraction in the first pass, and the result is in the first render: the
    legs are the shortest bones on the throne, so they came out the thinnest -
    a monument sitting on four wires. A bone is about as thick as a bone
    whether it is a femur or a finger, so the caller says how thick and the
    default is a leg.
    """
    parts = [cyl(name + "_shaft", thick, length * 0.86, (0, 0, 0), sides=7)]

    for end in (1, -1):
        parts.append(ball(name + "_knuckle", thick * 1.75,
                          (0, 0, end * length * 0.43), segments=7, rings=5))

    bone = join(parts, name)
    bone.rotation_euler = rot
    bone.location = at
    return bone


def skull(name, size, at, rot=(0, 0, 0)):
    """A skull the size of a knuckle: cranium, jaw, two sockets.

    The sockets are cut as real holes rather than painted dark, because at
    this scale a hole survives the lighting and a painted dot does not.
    """
    cranium = ball(name + "_cranium", size, (0, 0, 0), segments=10, rings=8)
    cranium.scale = (0.92, 1.0, 0.86)

    jaw = box(name + "_jaw", (size * 1.25, size * 0.95, size * 0.7),
              (0, -size * 0.72, -size * 0.42))

    head = join([cranium, jaw], name)

    for side in (-1, 1):
        socket = ball(name + "_socket", size * 0.30,
                      (side * size * 0.42, -size * 0.62, size * 0.10),
                      segments=8, rings=6)
        cut(head, socket)

    head.rotation_euler = rot
    head.location = at
    return head


def rib(name, span, rise, at, thick=0.028, steps=5):
    """One rib: a bone laid across the back, bowed forward at the middle.

    <b>Across, not up.</b> The first pass stood them on end, and the render
    shows what that gets you - a row of thin verticals reading as chain, not
    as a rib cage, because a rib cage is horizontal and the eye knows it.
    Laid across and stacked up the back, seven of them are unmistakable.

    Built as a chain of short segments whose ends meet, rather than as a curve
    modifier: the faceting is the point, and the segments are placed at the
    midpoint of each span with the angle between its own two endpoints, so
    they actually touch. The first version placed them on the arc and angled
    them by a derivative, which is why they came out as a dashed line.
    """
    parts = []
    half = span / 2.0
    home = Vector(at)

    def curve(t):
        # t from -1 to 1 across the rib; the bow is a cosine so the ends sit
        # flat against the posts and the middle stands proud.
        #
        # Placed in world space, not around the origin, because join() gives
        # the joined object the *first* part's transform: setting .location
        # afterwards then moves the whole rib by however far that first
        # segment happened to start from zero. The first pass did exactly
        # that, and every rib came out shifted a quarter of a tile to the
        # right - which is the whole of "wider than its own tile".
        return home + Vector((t * half, -math.cos(t * math.pi / 2) * rise, 0))

    for i in range(steps):
        a = curve(-1 + 2.0 * i / steps)
        b = curve(-1 + 2.0 * (i + 1) / steps)

        along = b - a

        parts.append(cyl(f"{name}_{i}", thick, along.length * 1.12, (a + b) / 2,
                         rot=(0, math.radians(90), math.atan2(along.y, along.x)),
                         sides=6))

    for end in (-1, 1):
        parts.append(ball(f"{name}_end{end}", thick * 1.5, curve(end),
                          segments=6, rings=4))

    return join(parts, name)


def join(parts, name):
    """Welds a list of objects into one, standing at the world origin.

    <b>The transform is applied, and that is the whole point.</b> Blender's
    join gives the result the transform of the <em>first</em> object in the
    list, and rewrites everyone else's vertices to suit. So a joined object's
    local space is wherever its first part happened to be standing - which cost
    this file two separate bugs before it was written down:

      - ribs placed correctly and then shifted a quarter of a tile sideways by
        a later `.location`, because the assignment moved the whole rib rather
        than positioning it;
      - and gold that never appeared at all, because `assign_materials` decides
        by height, in local space, and local space had quietly slid down by
        however far the first leg was from the origin.

    Applying the transform makes local space and world space the same thing
    from here on, so a height test means what it says.
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


def cut(target, tool):
    """Boolean difference, and then the cutter is gone."""
    modifier = target.modifiers.new("cut", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.object = tool
    # Blender 5 renamed the solvers: FAST/EXACT became FLOAT/EXACT/MANIFOLD.
    # Whichever this build offers, take the cheapest it has - the cutters here
    # are spheres against a sphere, and nothing about that needs the exact
    # solver's cost.
    for solver in ("FLOAT", "FAST", "EXACT"):
        try:
            modifier.solver = solver
            break
        except TypeError:
            continue

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier="cut")

    bpy.data.objects.remove(tool, do_unlink=True)


def build():
    clear()

    materials = [material("bone", BONE), material("stone", STONE), material("gold", GOLD)]

    stone_parts = []
    bone_parts = []
    gold_parts = []

    # ---- the plinth it stands on -----------------------------------------
    stone_parts.append(box("plinth", (WIDTH, DEPTH, PLINTH_Z), (0, 0, PLINTH_Z / 2)))
    stone_parts.append(box("plinth_top", (WIDTH * 0.9, DEPTH * 0.9, PLINTH_Z * 0.5),
                           (0, 0, PLINTH_Z * 1.25)))

    # A gold fillet in the step between the two stones. It is one thin box and
    # it is the only metal below knee height, which is what stops the bottom
    # third of the throne from reading as a grey crate.
    gold_parts.append(box("plinth_fillet", (WIDTH * 0.93, DEPTH * 0.93, 0.018),
                          (0, 0, PLINTH_Z + 0.009)))

    # ---- legs -------------------------------------------------------------
    leg = SEAT_Z - PLINTH_Z * 1.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            bone_parts.append(femur(
                f"leg_{sx}_{sy}", leg + 0.06,
                (sx * WIDTH * 0.34, sy * DEPTH * 0.32, PLINTH_Z * 1.5 + leg / 2),
                thick=0.046))

    # ---- the seat ---------------------------------------------------------
    bone_parts.append(box("seat", (WIDTH * 0.90, DEPTH * 0.82, 0.085), (0, -0.02, SEAT_Z)))
    bone_parts.append(femur("seat_rail", WIDTH * 0.90,
                            (0, -DEPTH * 0.40, SEAT_Z + 0.03),
                            rot=(0, math.radians(90), 0), thick=0.034))

    # Gold along the front lip of the seat, at eye level for the isometric
    # camera - the one edge of this piece the player looks straight at.
    gold_parts.append(box("seat_lip", (WIDTH * 0.90, 0.030, 0.026),
                          (0, -DEPTH * 0.42, SEAT_Z - 0.035)))

    # ---- the back ---------------------------------------------------------
    #
    # A backboard first, and the bones on top of it. Without the board the
    # first render came out a cage of loose sticks - you could see straight
    # through the chair, which is the one thing a throne must never be. The
    # board is what makes it furniture; the ribs on it are what make it ours.
    back_top = HEIGHT - 0.20
    spine = back_top - SEAT_Z
    back_y = DEPTH * 0.34

    bone_parts.append(box("backboard", (WIDTH * 0.72, 0.055, spine),
                          (0, back_y + 0.03, SEAT_Z + spine / 2)))

    bone_parts.append(femur("spine", spine * 0.98, (0, back_y - 0.035, SEAT_Z + spine / 2),
                            thick=0.042))

    for side in (-1, 1):
        bone_parts.append(femur(f"back_post_{side}", spine, (side * WIDTH * 0.40, back_y,
                                                             SEAT_Z + spine / 2), thick=0.048))

        gold_parts.append(ball(f"finial_{side}", 0.058,
                               (side * WIDTH * 0.40, back_y, SEAT_Z + spine + 0.045),
                               segments=10, rings=7))

        # And a long spike straight out of each finial. These two are the
        # tallest thing on the throne and they are what gives the silhouette
        # its teeth from across the room.
        gold_parts.append(spike(f"post_spike_{side}", 0.042, 0.30,
                                (side * WIDTH * 0.40, back_y, SEAT_Z + spine + 0.075)))

    # ---- the crest of spikes ---------------------------------------------
    #
    # "como si tuviera picos": a comb of seven along the top edge of the
    # backboard, tallest in the middle and falling away to the posts, each one
    # bone with a gold tip. Seven because the ribs are seven and the two rows
    # line up; the arch shape because a flat row of equal spikes reads as a
    # fence and an arch reads as a crown.
    crest_z = SEAT_Z + spine
    for i in range(7):
        # The middle one is left out: the crowning skull stands there, and the
        # first render had a spike growing straight out of its forehead with
        # the skull unreadable behind it. The gap is what makes the comb an
        # arch around the skull rather than a fence in front of it.
        if i == 3:
            continue

        t = (i - 3) / 3.0                       # -1 at the ends, 0 in the middle
        tall = 0.34 - 0.20 * abs(t)
        x = t * WIDTH * 0.33

        bone_parts.append(spike(f"crest_spike_{i}", 0.036, tall,
                                (x, back_y + 0.03, crest_z - 0.02)))
        gold_parts.append(spike(f"crest_tip_{i}", 0.020, tall * 0.30,
                                (x, back_y + 0.03, crest_z - 0.02 + tall * 0.70)))

    # Seven ribs across the back, widest at the bottom, narrowing towards the
    # crest - the shape of an actual rib cage, and the reason it reads as one.
    for i in range(7):
        t = i / 6.0
        bone_parts.append(rib(
            f"rib_{i}", WIDTH * (0.76 - 0.20 * t), 0.055 - 0.02 * t,
            (0, back_y - 0.05, SEAT_Z + 0.14 + t * (spine - 0.30))))

    # ---- arms, each ending in a skull -------------------------------------
    for side in (-1, 1):
        bone_parts.append(femur(f"arm_{side}", DEPTH * 0.70,
                                (side * WIDTH * 0.40, -0.03, ARM_Z),
                                rot=(math.radians(90), 0, 0), thick=0.038))

        bone_parts.append(femur(f"arm_post_{side}", ARM_Z - SEAT_Z,
                                (side * WIDTH * 0.40, -DEPTH * 0.26,
                                 (ARM_Z + SEAT_Z) / 2 + 0.02), thick=0.036))

        bone_parts.append(skull(f"arm_skull_{side}", 0.082,
                                (side * WIDTH * 0.40, -DEPTH * 0.38, ARM_Z + 0.05)))

        # Three small spikes along each arm, so the teeth are not only up top.
        # Whoever sits here is not meant to be comfortable.
        for i in range(3):
            gold_parts.append(spike(f"arm_spike_{side}_{i}", 0.022, 0.085,
                                    (side * WIDTH * 0.40,
                                     -DEPTH * 0.18 + i * DEPTH * 0.17,
                                     ARM_Z + 0.030)))

        # A gold sleeve where each arm meets its post: the cheapest kind of
        # glamour, and the piece of metal that actually catches the light at
        # the height the camera looks from.
        gold_parts.append(cyl(f"arm_collar_{side}", 0.050, 0.055,
                              (side * WIDTH * 0.40, -DEPTH * 0.30, ARM_Z),
                              rot=(math.radians(90), 0, 0), sides=8))

    # The crowning skull, which is what says whose chair this is. Bigger than
    # the armrest pair on purpose: at the size this is drawn on screen, the
    # crest is the only piece anybody will actually recognise.
    crest_r = 0.150
    crest_y = back_y - 0.11
    bone_parts.append(skull("crest", crest_r, (0, crest_y, back_top + 0.085)))

    # The circlet sits *on* the skull, not above it. In an early render it
    # floated a finger's width clear and read as a separate object hanging in
    # the air - the crest has a radius, so its crown is at +radius and the band
    # has to overlap that, not start from it.
    # Low enough on the skull that the dome comes up through the ring. Sat
    # higher, the circlet showed the player its own empty top face - a gold
    # washer balanced on a head - because this game is drawn from above and a
    # crown is the one object whose inside is never meant to be seen.
    band = back_top + 0.085 + crest_r * 0.45

    # A ring, not a plank. It was a flat box with two beads on it, and at the
    # angle this game is drawn from the box presented its own dark top face -
    # so the throne wore a black slab where the crown should be. A short
    # cylinder reads as a circlet from every side, which is the only thing a
    # crown has to do.
    gold_parts.append(cyl("crown", crest_r * 0.80, 0.045, (0, crest_y, band), sides=10))

    # Five points around it, tallest at the front, so the circlet has a
    # silhouette of its own against the spikes behind it.
    for i in range(5):
        angle = math.radians(-90 + (i - 2) * 34)
        gold_parts.append(spike(
            f"crown_point_{i}", 0.026, 0.10 - 0.022 * abs(i - 2),
            (math.cos(angle) * crest_r * 0.72,
             crest_y + math.sin(angle) * crest_r * 0.72,
             band + 0.020)))

    # ---- one object, three material slots ---------------------------------
    #
    # Each group is painted while it is still its own object, and Blender's
    # join merges the three material lists and remaps the indices for us.
    #
    # <b>Not by position, which is what this replaced.</b> The first version
    # decided the material from each face's height and distance from the
    # centre - stone low down, gold up top - which is guesswork dressed up as
    # a rule, and it guessed wrong twice: it read local coordinates that had
    # silently shifted (see `join`), and once that was fixed it painted most of
    # the crowning skull gold, because a skull at the top of the throne is at
    # the same height as the circlet sitting on it. The parts already know what
    # they are made of. Asking them is exact, and it cannot drift when a piece
    # moves.
    throne = join([paint(bone_parts, "bone_group", materials[0]),
                   paint(stone_parts, "stone_group", materials[1]),
                   paint(gold_parts, "gold_group", materials[2])],
                  "aldrich_count_throne")

    finish(throne)
    return throne


def paint(parts, name, material):
    """Joins one group of parts and gives the lot a single material."""
    group = join(parts, name)

    group.data.materials.clear()
    group.data.materials.append(material)

    for poly in group.data.polygons:
        poly.material_index = 0

    return group


def finish(throne):
    """Weld, face the normals outward, flat-shade, and lay out a UV sheet."""
    bpy.context.view_layer.objects.active = throne
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0008)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    palette_uv(throne)
    one_submesh(throne)

    bpy.ops.object.shade_flat()

    # The pivot goes on the floor at the middle of the tile, which is where
    # every reference mesh has it: quality_chair, statue_large_01 and
    # foxy_statue all measure a base of 0.000. Anywhere else and the plugin
    # has to nudge it at runtime, which is the whole cat statue story.
    ground(throne)


def ground(obj):
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


# How many swatches wide the palette sheet is. Three materials fit in a 2x2.
PALETTE = 2


def palette_uv(throne):
    """Points every face at the middle of its own material's swatch.

    <b>Why not the unwrap.</b> `smart_project` gives a perfectly good UV layout
    for painting on, and the first version of this baked flat colour into it -
    which produced a 256x256 sheet that was 95% bone with the stone and the
    gold scattered across it as specks a few pixels wide. Correct, and useless:
    the islands of a 4000-triangle model are tiny, so most of what defines the
    throne's look was riding on a handful of pixels and whatever the mipmap
    chain did to them.

    A model whose materials are three flat colours does not need texture space
    per face. It needs three colours, and every face pointing at the right one.
    So the sheet is a 2x2 palette and every corner of every face lands dead in
    the centre of its own square: the colour is exact, it survives any
    filtering and any mip level, and the whole albedo is a few hundred bytes.
    It is the standard way low-poly palette models are textured, and it is what
    this game's faceted, flat-shaded furniture is already doing by eye.

    <b>What it costs.</b> Detail painted per-face - wear along the bone,
    tarnish in the gold's crevices - is off the table until somebody wants a
    real unwrap back. That is a fair trade for a piece that is 40 pixels tall
    on screen, and the unwrap is one line away when it stops being one.
    """
    mesh = throne.data
    uvs = mesh.uv_layers.active or mesh.uv_layers.new(name="palette")

    for poly in mesh.polygons:
        slot = min(poly.material_index, PALETTE * PALETTE - 1)

        # Centre of the swatch, so bilinear filtering never reaches a
        # neighbour: half a swatch away in every direction.
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

def bake_albedo(throne, path, size=64):
    """Writes the palette sheet the UVs above point into.

    <b>Why this exists at all.</b> The throne already had a texture, painted by
    hand over Going Medieval's own `quality_chair` UV. The mesh here is not
    that chair, so that texture would have arrived in game scrambled - bone
    where the plinth is, stone across the seat - and the first look at the new
    throne would have been spent on the smear rather than on the model.

    64 pixels is four times more than four flat squares need, and small enough
    that the file is measured in hundreds of bytes.
    """
    import numpy

    colours = [BONE, STONE, GOLD, BONE]     # the fourth square is spare
    step = size // PALETTE

    flat = numpy.ones((size, size, 4), dtype=numpy.float32)

    for slot, colour in enumerate(colours):
        col = slot % PALETTE
        row = slot // PALETTE

        flat[row * step:(row + 1) * step, col * step:(col + 1) * step, :3] = colour[:3]

    image = bpy.data.images.new("aldrich_count_throne_albedo", size, size, alpha=True)
    image.pixels = flat.reshape(-1)

    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()

    return path


def export(throne, folder):
    """Writes the FBX with the axis change baked into the vertices.

    <b>`bake_space_transform` is the whole of "el trono esta mal orientado".</b>
    Blender is Z-up and the game is Y-up, and `axis_up="Y"` on its own does not
    move a single vertex: it writes the ninety-degree correction into the
    exported object's <em>node transform</em>, and leaves the mesh data in
    Blender's space. Unity's importer honours that node, so the throne stands
    up in the Unity editor and everything looks fine.

    Except that nothing downstream of here ever sees that node. The game loads
    a <c>Mesh</c> out of the bundle by name and puts it on its own prefab, so
    the correction is dropped on the floor and the mesh arrives exactly as
    Blender had it: lying on its back, which is a rotation of -90 about X and
    is precisely what the screenshot shows.

    With the flag on, the rotation goes into the vertices and the node is
    identity, so the mesh alone is already right. The check that it worked is
    the height: `extract_reference.py` reads the shipped meshes in the game's
    own units, and there the vanilla chair measures 0.749 x <b>1.484</b> x
    0.901 - tall in <b>y</b>. Anything of ours that comes back tall in z is
    still wrong.
    """
    os.makedirs(folder, exist_ok=True)

    fbx = os.path.join(folder, "aldrich_count_throne.fbx")
    obj = os.path.join(folder, "aldrich_count_throne.obj")

    bpy.ops.object.select_all(action="DESELECT")
    throne.select_set(True)
    bpy.context.view_layer.objects.active = throne

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
    throne = build()

    size, base = measure(throne)
    tris = sum(len(p.vertices) - 2 for p in throne.data.polygons)

    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    fbx, obj = export(throne, os.path.join(here, "ASSESTS", "Modelos"))

    # Straight into the mod, because the texture pipeline already ends there:
    # ModTextures reads Data/Textures at load and registers whatever it finds,
    # so an albedo that matches the mesh is one file copy and no build step.
    albedo = bake_albedo(throne, os.path.join(
        os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval", "Mods",
        "VampireCourt", "Data", "Textures", "aldrich_count_throne_albedo.png"))

    print("")
    print("=== aldrich_count_throne ===")
    print(f"  size      {size[0]:.3f} x {size[1]:.3f} x {size[2]:.3f}   (the tile is 1.000)")
    print(f"  base at   {base:+.4f}   (0 means the pivot is on the floor)")
    print(f"  triangles {tris}")
    print("  quality_chair, for comparison: 0.749 x 0.901 x 1.484")
    print(f"  -> {fbx}")
    print(f"  -> {obj}")
    print(f"  -> {albedo}")

    if size[0] > 0.98 or size[1] > 0.98:
        print("  WARNING: wider than its own tile", file=sys.stderr)


main()
