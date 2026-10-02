"""La careta de pico del medico de la peste.

    blender -b --python tools/models/build_plague_mask.py

Y despues el bundle, como con cualquier malla nuestra (BuildModBundles).

<b>Por que modelada y no sacada del modelo de referencia.</b> Se intento: el
`um-medico-da-peste-musculoso.zip` es una sola malla de 9.372 vertices, y
separar la careta por el color de su textura dio esquirlas sueltas de 54
vertices, porque la malla es de escaneo y la careta no es una pieza. La
referencia sirve para la forma - placa de cuero, pico largo y curvado hacia
abajo, dos lentes redondas con aro y una correa -, y eso se construye limpio
con las primitivas de `gm_model`.

<b>Donde va.</b> En el espacio de la malla `mouthpiece` de vanilla, que es la
que llevaba hasta hoy y cuya colocacion en la cara ya funciona en partida: x a
lo ancho, y arriba y la cara hacia -z (medido en su OBJ: el centro del pano
esta mas adelantado que sus bordes; la boca queda hacia y=0, los ojos hacia
+0.07). `gm_model.export` lleva el -Y de Blender a ese -Z. Asi los
`EquippedTransformSettings` del mouthpiece valen tal cual; si en partida sale
corrida, se ajustan esos numeros en Resources.json, sin volver aqui.

<b>Color</b> por paleta 2x2, como el resto de mallas nuestras, escrita encima de
`aldrich_plague_mask_albedo.png`.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gm_model as gm  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
MODELS = os.path.join(REPO, "ASSESTS", "Modelos")
TEXTURE = os.path.join(os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval",
                       "Mods", "CarrionAndPlague", "Data", "Textures", "aldrich_plague_mask_albedo.png")
NAME = "aldrich_plague_mask"

# Cuero encerado, pico de cuero crudo, cristal ahumado ambar y laton.
LEATHER = (0.11, 0.095, 0.085, 1.0)
BEAK = (0.60, 0.52, 0.40, 1.0)
GLASS = (0.30, 0.13, 0.06, 1.0)
BRASS = (0.56, 0.43, 0.18, 1.0)

FACE_Y = -0.085       # la superficie de la cara (el frente del mouthpiece)
EYES_Z = 0.07
MOUTH_Z = 0.0


def cone(name, r1, r2, length, base, direction, sides=10):
    """Un tronco de cono que nace en `base` y crece hacia `direction`."""
    d = Vector(direction).normalized()
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r1, radius2=r2, depth=length,
                                    location=Vector(base) + d * (length / 2.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return obj


def lens(name, at, radius, depth):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=depth, location=at,
                                        rotation=(math.radians(90), 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    return obj


def rim(name, at, radius, thick):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=thick, major_segments=12,
                                     minor_segments=4, location=at, rotation=(math.radians(90), 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    return obj


def main():
    gm.clear()
    leather = gm.material("leather", LEATHER)
    beak_mat = gm.material("beak", BEAK)
    glass = gm.material("glass", GLASS)
    brass = gm.material("brass", BRASS)

    # La placa: media cascara que tapa de la barbilla a la frente.
    plate = gm.ball("plate", 1.0, (0, FACE_Y + 0.012, 0.035), segments=14, rings=8)
    plate.scale = Vector((0.092, 0.045, 0.098))
    # La correa alrededor de la cabeza, a la altura de los ojos.
    strap = gm.ring("strap", 0.098, 0.006, 0.014, (0, -0.005, EYES_Z + 0.01), sides=16)
    strap.scale = Vector((1.0, 1.12, strap.scale.z))
    leather_group = gm.paint([plate, strap], "leather_group", leather)

    # El pico: nace sobre la boca, sale hacia delante y cae un poco.
    root = Vector((0, FACE_Y - 0.025, MOUTH_Z + 0.03))
    ahead = Vector((0, -1, -0.28))
    beak = cone("beak", 0.036, 0.004, 0.19, root, ahead, sides=10)
    # Un poco aplastado de lado, como un pico y no como un cucurucho.
    bpy.ops.object.select_all(action="DESELECT")
    beak.select_set(True)
    bpy.context.view_layer.objects.active = beak
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for v in beak.data.vertices:
        v.co.x *= 0.78
    # Y el lomo de arriba, que se lee a distancia.
    ridge = cone("ridge", 0.010, 0.002, 0.15, root + Vector((0, 0.0, 0.030)),
                 ahead + Vector((0, 0, -0.06)), sides=6)
    beak_group = gm.paint([beak, ridge], "beak_group", beak_mat)

    # Las lentes y sus aros.
    eyes = []
    rims = []
    for side in (-1, 1):
        at = (side * 0.042, FACE_Y - 0.036, EYES_Z)
        eyes.append(lens(f"lens{side}", at, 0.024, 0.012))
        rims.append(rim(f"rim{side}", (at[0], at[1] - 0.004, at[2]), 0.026, 0.0055))
    glass_group = gm.paint(eyes, "glass_group", glass)
    brass_group = gm.paint(rims, "brass_group", brass)

    mask = gm.join([leather_group, beak_group, glass_group, brass_group], NAME)
    mask.data.name = NAME

    # Medido en el bundle, no supuesto: construida mirando a -Y, el pico salia
    # hacia +z en Unity (punta en z=+0.29), y la cara del mouthpiece mira a -z.
    # Media vuelta sobre la vertical, que no cambia la mano de la malla.
    mask.rotation_euler = (0.0, 0.0, math.pi)
    bpy.ops.object.select_all(action="DESELECT")
    mask.select_set(True)
    bpy.context.view_layer.objects.active = mask
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    bpy.context.view_layer.objects.active = mask
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0004)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    gm.palette_uv(mask)
    gm.one_submesh(mask)
    bpy.ops.object.shade_flat()

    pts = [v.co for v in mask.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    print(f"[mask] {len(pts)} vertices; ancho {hi.x - lo.x:.3f}, alto {hi.z - lo.z:.3f}, "
          f"de la nuca a la punta {hi.y - lo.y:.3f}")

    gm.bake_albedo(NAME, [LEATHER, BEAK, GLASS, BRASS], TEXTURE)
    bpy.ops.object.select_all(action="DESELECT")
    mask.select_set(True)
    fbx, _ = gm.export(mask, MODELS, NAME)
    print(f"[mask] -> {fbx}")
    print(f"[mask] -> {TEXTURE}")


main()
