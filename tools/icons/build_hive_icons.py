"""Los retratos del Corredor, el abrazacaras y el huevo.

    python tools/icons/build_hive_icons.py

Los tres llevaban `iconPath: "wolf"` y en la ficha salia un lobo. Este script
hace las dos mitades del icono de un animal de vanilla (`wolf_128.png`):

  1. Blender, sin ventana, pinta cada FBX de `ASSESTS/Modelos/` con su textura
     del mod, en tres cuartos y con fondo transparente;
  2. PIL lo pone sobre el fondo oliva con el halo cian de `gm_style`, y lo deja
     en `XenomorphRunner/Data/Sprites/aldrich_icon_<id>.png` a 128 px.

Cuando corre dentro de Blender solo hace el paso 1.
"""

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
MODELS = os.path.join(REPO, "ASSESTS", "Modelos")
MOD = os.path.join(os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval",
                   "Mods", "XenomorphRunner", "Data")
RENDERS = os.path.join(REPO, "ASSESTS", "Iconos", "render")
BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

# modelo, textura, icono, giro de la camara en grados (0 = de lado)
PIECES = [
    ("aldrich_xeno_runner", "aldrich_xeno_runner", "aldrich_icon_xeno_runner", 35),
    ("aldrich_facehugger", "aldrich_facehugger", "aldrich_icon_facehugger", 30),
    ("aldrich_xeno_egg", "aldrich_xeno_egg", "aldrich_icon_xeno_egg", 20),
]


def render_all():
    import math
    import bpy
    from mathutils import Vector

    os.makedirs(RENDERS, exist_ok=True)
    for model, texture, icon, turn in PIECES:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, model + ".fbx"))
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]

        mat = bpy.data.materials.new("icon")
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        bsdf = nodes.get("Principled BSDF")
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(MOD, "Textures", texture + ".png"))
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.8
        for o in meshes:
            o.data.materials.clear()
            o.data.materials.append(mat)

        bpy.context.view_layer.update()
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        centre = (lo + hi) / 2
        radius = (hi - lo).length / 2

        scene = bpy.context.scene
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        scene.collection.objects.link(cam)
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = radius * 2.05
        a = math.radians(turn)
        # Blender es Z arriba; el FBX llega girado a Y arriba si no se horneo,
        # asi que el "arriba" se toma del eje mas alto de la caja.
        up_z = (hi.z - lo.z) >= (hi.y - lo.y) * 0.6
        if up_z:
            direction = Vector((math.cos(a), -math.sin(a), 0.55))
        else:
            direction = Vector((math.cos(a), 0.55, math.sin(a)))
        cam.location = centre + direction.normalized() * radius * 4
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Z" if up_z else "Y").to_euler()
        scene.camera = cam

        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
        scene.collection.objects.link(sun)
        sun.data.energy = 3.5
        sun.rotation_euler = cam.rotation_euler
        sun.rotation_euler.z += math.radians(25)
        world = bpy.data.worlds.new("w")
        scene.world = world
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9

        engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
        scene.render.film_transparent = True
        scene.render.resolution_x = scene.render.resolution_y = 768
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.filepath = os.path.join(RENDERS, icon + ".png")
        bpy.ops.render.render(write_still=True)
        print("[icons] rendered", icon)


def compose():
    import numpy as np
    from PIL import Image
    sys.path.insert(0, HERE)
    import gm_style as gm

    out_dir = os.path.join(MOD, "Sprites")
    os.makedirs(out_dir, exist_ok=True)
    for _, _, icon, _ in PIECES:
        n = 128 * gm.SS
        # El fondo del icono de lobo: oliva claro en el centro, casi negro en las
        # esquinas (medido en wolf_128: centro ~(101,115,76), esquina (51,53,0)).
        img = gm.radial_field(n, (112, 124, 72), (50, 52, 0), reach=1.41)
        # Y la mancha cian de detras, que en el lobo es un circulo grande
        # centrado un poco por debajo de la mitad (~(90,174,194) en su nucleo).
        y, x = np.mgrid[0:n, 0:n]
        r = np.sqrt((x - n * 0.5) ** 2 + (y - n * 0.56) ** 2) / (n * 0.36)
        a = (np.clip(1 - r, 0, 1) ** 0.8)[..., None] * 0.85
        arr = np.asarray(img, np.float32)
        arr[..., :3] = arr[..., :3] * (1 - a) + np.array((84, 178, 204), np.float32) * a
        img = Image.fromarray(arr.astype(np.uint8), "RGBA")

        art = Image.open(os.path.join(RENDERS, icon + ".png")).convert("RGBA")
        art = art.crop(art.getbbox())
        side = int(n * 0.84)
        k = side / max(art.size)
        art = art.resize((max(1, int(art.width * k)), max(1, int(art.height * k))), Image.LANCZOS)
        layer = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        layer.paste(art, ((n - art.width) // 2, (n - art.height) // 2 + int(n * 0.02)), art)

        img = gm._glow(img, np.asarray(layer, np.float32)[..., 3] / 255.0)
        img.alpha_composite(gm.light(layer))
        img.putalpha(255)
        path = os.path.join(out_dir, icon + ".png")
        gm._down(img, 128, 128).save(path)
        print("[icons]", path)


if __name__ == "__main__":
    try:
        import bpy  # noqa: F401
        render_all()
    except ImportError:
        subprocess.run([BLENDER, "--background", "--python", os.path.abspath(__file__)], check=True)
        compose()
