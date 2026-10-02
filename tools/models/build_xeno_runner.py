"""El Corredor: del glTF de piezas rigidas a una malla con esqueleto.

Se corre sin abrir Blender:

    blender -b --python tools/models/build_xeno_runner.py

**Por que no se exporta tal cual.** `ASSESTS/Modelos/xenomorph-runner.zip` trae
89 piezas sueltas animadas cada una por su nodo, sin skin. FBX guarda eso como
una toma por pieza y por animacion ("head|idle", "body|idle"...), y Unity lo
importaria como cientos de clips que mueven una pieza cada uno. Aqui cada nodo
se vuelve un hueso, cada pieza se pega con peso 1 a su hueso, y las cinco
animaciones se hornean sobre el esqueleto: sale **una** malla con **un**
esqueleto y **cinco** tomas - idle, idle2, running, sprinting, roar -, que es lo
que el importador legacy de Unity convierte en cinco clips.

**Sin material.** El juego no usa los materiales de un bundle nuestro; el
plugin (`XenoRunnerModel`) le pone al Corredor una copia del material del lobo
con la textura que este script deja en la carpeta del mod.

**Sin `bake_space_transform`.** A diferencia de las mallas de edificio, aqui
viaja el prefab entero con su jerarquia, asi que la correccion de ejes que
Unity pone en el nodo raiz si se respeta. Y esa bandera rompe animaciones.
La orientacion y el tamano los ajusta el plugin en partida midiendo al lobo.
"""

import os
import shutil
import tempfile
import zipfile

import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SOURCE = os.path.join(REPO, "ASSESTS", "Modelos", "xenomorph-runner.zip")
OUT_FBX = os.path.join(REPO, "ASSESTS", "Modelos", "aldrich_xeno_runner.fbx")
OUT_TEX = os.path.join(
    os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval", "Mods",
    "XenomorphRunner", "Data", "Textures", "aldrich_xeno_runner.png")

RIG = "xeno"
CLIPS = ["idle", "idle2", "running", "sprinting", "roar"]


def unpack():
    folder = tempfile.mkdtemp(prefix="xeno_")
    with zipfile.ZipFile(SOURCE) as z:
        z.extractall(folder)
    return folder


def descendants(root):
    out = [root]
    for child in root.children:
        out.extend(descendants(child))
    return out


BASE = {}


def remember_base(objs):
    """Each node's own transform as the glTF declares it, before any clip
    has been evaluated over it."""
    for o in objs:
        BASE[o.name] = (o.location.copy(), o.rotation_mode,
                        o.rotation_quaternion.copy(), o.rotation_euler.copy(),
                        o.scale.copy())


def assign(objs, action):
    """Every node plays `action`, each on the slot named after it.

    Every node goes back to its base transform first. A channel the clip
    does not key - a node whose rotation is animated and its location not,
    or a node the clip does not touch at all - otherwise keeps whatever the
    previous clip last wrote into it, which is how the first bake of roar
    came out 8 cm off and idle2's feet went through the floor.
    """
    for o in objs:
        ad = o.animation_data or o.animation_data_create()
        ad.action = None
        loc, mode, quat, euler, scale = BASE[o.name]
        o.location, o.rotation_mode = loc, mode
        o.rotation_quaternion, o.rotation_euler, o.scale = quat, euler, scale
        for s in getattr(action, "slots", []):
            if s.name_display == o.name or s.identifier == "OB" + o.name:
                ad.action = action
                ad.action_slot = s
                break


def unscaled(m):
    loc, rot, _ = m.decompose()
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4()


def main():
    folder = unpack()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(folder, "source", "model.gltf"))
    scene = bpy.context.scene

    root = [o for o in bpy.data.objects if o.parent is None][0]
    nodes = descendants(root)
    names = {o: "b%03d" % i for i, o in enumerate(nodes)}
    remember_base(nodes)
    actions = {a.name: a for a in bpy.data.actions}
    missing = [c for c in CLIPS if c not in actions]
    if missing:
        raise SystemExit("clips missing from the glTF: %s" % missing)

    # Bind pose: first frame of idle. Any pose would do - every vertex is
    # rigid to one bone - as long as mesh and bones are captured together.
    assign(nodes, actions["idle"])
    scene.frame_set(0)
    bind = {o: o.matrix_world.copy() for o in nodes}

    # Skeleton: one bone per node, same parent chain.
    arm_data = bpy.data.armatures.new(RIG)
    arm = bpy.data.objects.new(RIG, arm_data)
    scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    edit = {}
    for o in nodes:
        b = arm_data.edit_bones.new(names[o])
        b.head = (0, 0, 0)
        b.tail = (0, 0.05, 0)
        b.matrix = unscaled(bind[o])
        edit[o] = b
    for o in nodes:
        if o.parent in edit:
            edit[o].parent = edit[o.parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    rest = {o: arm_data.bones[names[o]].matrix_local.copy() for o in nodes}

    # One mesh, every piece weighted fully to its own bone.
    pieces = []
    for o in nodes:
        if o.type != "MESH":
            continue
        data = o.data.copy()
        data.transform(bind[o])
        piece = bpy.data.objects.new("piece_" + o.name, data)
        scene.collection.objects.link(piece)
        group = piece.vertex_groups.new(name=names[o])
        group.add([v.index for v in data.vertices], 1.0, "REPLACE")
        pieces.append(piece)

    # Bake each clip onto the bones.
    arm.animation_data_create()
    for clip in CLIPS:
        action = actions[clip]
        assign(nodes, action)
        start, end = (int(round(x)) for x in action.frame_range)

        baked = bpy.data.actions.new(clip + "_baked")
        baked.use_fake_user = True
        arm.animation_data.action = baked

        for f in range(start, end + 1):
            scene.frame_set(f)
            pose = {o: o.matrix_world.copy() for o in nodes}
            for o in nodes:
                pb = arm.pose.bones[names[o]]
                if o.parent in pose:
                    rel_rest = rest[o.parent].inverted() @ rest[o]
                    rel_pose = pose[o.parent].inverted() @ pose[o]
                else:
                    rel_rest = rest[o]
                    rel_pose = pose[o]
                pb.rotation_mode = "QUATERNION"
                pb.matrix_basis = rel_rest.inverted() @ rel_pose
                frame = f - start
                pb.keyframe_insert("location", frame=frame, group=pb.name)
                pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
                pb.keyframe_insert("scale", frame=frame, group=pb.name)
        print("BAKED", clip, end - start + 1, "frames")

    for img in bpy.data.images:
        if img.size[0] > 0:
            os.makedirs(os.path.dirname(OUT_TEX), exist_ok=True)
            img.filepath_raw = OUT_TEX
            img.file_format = "PNG"
            img.save()
            print("TEXTURE", OUT_TEX, tuple(img.size))
            break

    # The source nodes and their actions out of the way, so only the rig's
    # five clips reach the FBX.
    for o in nodes:
        bpy.data.objects.remove(o, do_unlink=True)
    for name in CLIPS:
        bpy.data.actions.remove(actions[name])
    for name in CLIPS:
        bpy.data.actions[name + "_baked"].name = name

    bpy.ops.object.select_all(action="DESELECT")
    for p in pieces:
        p.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "xeno_body"
    body.data.name = "xeno_body"
    body.data.materials.clear()
    body.parent = arm
    mod = body.modifiers.new("rig", "ARMATURE")
    mod.object = arm

    arm.animation_data.action = bpy.data.actions["idle"]
    scene.frame_set(0)

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, path_mode="STRIP", use_armature_deform_only=False)
    print("EXPORTED", OUT_FBX, "verts", len(body.data.vertices), "bones", len(arm_data.bones))
    shutil.rmtree(folder, ignore_errors=True)


main()
