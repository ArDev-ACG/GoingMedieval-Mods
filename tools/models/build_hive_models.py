"""El huevo y el abrazacaras: de los FBX descargados a los dos que publica el mod.

Se corre sin abrir Blender:

    blender -b --python tools/models/build_hive_models.py

Y despues, como siempre, Unity los mete en el bundle:

    Unity.exe -batchmode -quit -nographics -projectPath tools/unity/AldrichBundles
        -executeMethod Aldrich.BuildModBundles.Run -logFile -

<b>Estos dos no son el Corredor.</b> El Corredor venia como 89 piezas rigidas
sin skin y hubo que fabricarle un esqueleto; estos dos vienen ya como una malla
con su UV, asi que aqui no se rigea nada. Lo que si hay que hacer es lo que este
juego necesita de cualquier malla nuestra:

  * **una sola malla y sin materiales**, porque un renderer del juego lleva un
    material y las submallas de mas sencillamente no se dibujan - fue lo que
    saco la estatua de gato naranja plana -;
  * **de pie y con la base en 0**, que es donde el juego apoya un agente;
  * **la textura en PNG al lado**, que es lo que el plugin le pone encima, no el
    material del FBX: los materiales de un bundle nuestro el juego no los usa.

<b>El tamano no se decide aqui.</b> Como con el Corredor, la escala final la
pone el plugin en partida midiendo al animal portador; lo que este script
garantiza es que la malla mide lo que dice `tall` y se apoya en 0.
"""

import io as _io
import os
import shutil
import tempfile
import zipfile

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
GLOBAL = os.path.abspath(os.path.join(REPO, "..", "Assets globales", "Modelo 3d"))
MODELS = os.path.join(REPO, "ASSESTS", "Modelos")
TEXTURES = os.path.join(
    os.path.expanduser("~"), "Documents", "Foxy Voxel", "Going Medieval", "Mods",
    "XenomorphRunner", "Data", "Textures")

# zip de fuera, zip de dentro (o None), fbx, textura, nombre nuestro, tamano en
# metros, por que medida se ajusta, y cuantos vertices tiene que tener un objeto
# para no ser basura de la vitrina.
PIECES = [
    dict(
        zip="alien-egg.zip",
        inner=None,
        fbx="source/aliengeggFULL.fbx",
        texture="textures/alienegg_baseColor.png",
        name="aldrich_xeno_egg",
        size=0.9,
        by="tall",
        least=100,
    ),
    dict(
        zip="facehugger-ps1.zip",
        inner="source/praetorian facehugger.zip",
        fbx="praetorian facehugger/Praetorian Facehugger.fbx",
        texture="praetorian facehugger/Textures/0F76C208.png",
        name="aldrich_facehugger",
        size=0.6,
        by="long",
        least=0,
    ),
]

# La textura de un animal en este juego es pequena - la del Corredor son 128 -,
# y la del huevo viene a 4 MB. Se baja a esto, que es lo que se ve a la
# distancia a la que se juega.
TEXTURE_SIDE = 256


def unpack(piece):
    """El zip, y si dentro hay otro zip, ese. Devuelve la carpeta."""
    folder = tempfile.mkdtemp(prefix="hive_")

    with zipfile.ZipFile(os.path.join(GLOBAL, piece["zip"])) as z:
        if piece["inner"] is None:
            z.extractall(folder)
        else:
            with zipfile.ZipFile(_io.BytesIO(z.read(piece["inner"]))) as inner:
                inner.extractall(folder)

    return folder


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def one_mesh(name, least=0):
    """
    Todo junto, sin materiales y con el nombre que el bundle va a publicar.

    <b>`least` es el suelo de la vitrina.</b> El FBX del huevo trae quince
    objetos y solo siete son el huevo: los otros ocho tienen tres, cuatro o
    nueve vertices y son el plano del suelo y los ayudantes de ZBrush con que
    se presento el modelo - por eso el zip trae texturas llamadas `piso` -.
    Pegados al huevo, la malla medía 2,23 de ancho por 1,00 de alto, que es una
    peana, no un huevo. Se tiran por cuenta de vertices, que es lo unico que los
    distingue: los nombres son `Group44292` y no dicen nada.
    """
    pieces = [o for o in bpy.context.scene.objects
              if o.type == "MESH" and len(o.data.vertices) >= least]
    if not pieces:
        raise RuntimeError("el FBX no trajo ninguna malla")

    for spare in [o for o in bpy.context.scene.objects
                  if o.type == "MESH" and o not in pieces]:
        bpy.data.objects.remove(spare, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]

    if len(pieces) > 1:
        bpy.ops.object.join()

    body = bpy.context.view_layer.objects.active
    body.name = name
    body.data.name = name
    body.data.materials.clear()

    # Lo que el FBX traiga de rotacion y escala, cocido en los vertices: el
    # juego saca una malla del bundle por nombre, no un nodo con su
    # transformacion.
    body.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return body


def bounds(body):
    box = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    low = Vector((min(v.x for v in box), min(v.y for v in box), min(v.z for v in box)))
    high = Vector((max(v.x for v in box), max(v.y for v in box), max(v.z for v in box)))
    return low, high


def stand(body, size_wanted, by):
    """
    Centrado en X e Y, con la base en Z=0, y a tamano.

    `by` dice que medida es la que manda. Para el huevo es el **alto**, que es
    lo que uno mira de un huevo. Para el abrazacaras es el **largo**: el bicho
    va tumbado y mide 3,78 de largo por 0,65 de alto, asi que ajustarlo por el
    alto lo dejaba de casi cuatro metros de envergadura.
    """
    low, high = bounds(body)
    size = high - low
    if max(size.x, size.y, size.z) <= 0.0001:
        raise RuntimeError("malla sin volumen")

    reference = size.z if by == "tall" else max(size.x, size.y, size.z)
    scale = size_wanted / reference
    body.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(scale=True)

    low, high = bounds(body)
    body.location -= Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    bpy.ops.object.transform_apply(location=True)


def texture(folder, piece):
    source = os.path.join(folder, piece["texture"].replace("/", os.sep))
    if not os.path.exists(source):
        print("SIN TEXTURA", source)
        return

    image = bpy.data.images.load(source)
    if max(image.size) > TEXTURE_SIDE:
        image.scale(TEXTURE_SIDE, TEXTURE_SIDE)

    os.makedirs(TEXTURES, exist_ok=True)
    out = os.path.join(TEXTURES, piece["name"] + ".png")
    image.filepath_raw = out
    image.file_format = "PNG"
    image.save()
    print("TEXTURE", out, tuple(image.size))


def build(piece):
    folder = unpack(piece)
    try:
        clear()
        bpy.ops.import_scene.fbx(filepath=os.path.join(
            folder, piece["fbx"].replace("/", os.sep)))

        body = one_mesh(piece["name"], piece.get("least", 0))
        stand(body, piece["size"], piece["by"])
        texture(folder, piece)

        os.makedirs(MODELS, exist_ok=True)
        out = os.path.join(MODELS, piece["name"] + ".fbx")

        bpy.ops.object.select_all(action="DESELECT")
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.export_scene.fbx(
            filepath=out, use_selection=True, object_types={"MESH"},
            add_leaf_bones=False, bake_anim=False, path_mode="STRIP",
            bake_space_transform=False)

        low, high = bounds(body)
        print(f"EXPORTED {out} verts {len(body.data.vertices)} "
              f"size {high.x - low.x:.2f} x {high.z - low.z:.2f} x {high.y - low.y:.2f}")
    finally:
        shutil.rmtree(folder, ignore_errors=True)


def main():
    for piece in PIECES:
        print("==", piece["name"])
        build(piece)


main()
