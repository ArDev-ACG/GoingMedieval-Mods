"""Lo que todos nuestros modelos de Blender hacen igual.

`build_count_throne.py` y `build_cat_statue.py` nacieron cada uno con su propia
copia de las primitivas, del desdoblado de paleta y del exportador, y las dos
copias ya se desincronizaron una vez: el arreglo de las submallas hubo que
escribirlo dos veces. Trece piezas mas por delante son trece copias mas, asi que
lo comun vive aqui y cada pieza solo dice de que esta hecha.

Lo que este modulo da por sentado, y esta pagado con errores:

  - **Z arriba en Blender, Y arriba en el juego.** `export()` exporta con
    `bake_space_transform=True`, que mete la rotacion en los vertices en vez de
    en el nodo - el juego saca un `Mesh` del bundle por nombre y nunca ve el
    nodo. Con la bandera puesta se cuela tambien la conversion a centimetros,
    de ahi `global_scale=0.01`.
  - **Una sola submalla.** Una ranura de material en Blender es una submalla en
    Unity, y los renderers de edificio llevan un material: todo lo que pase de
    la submalla 0 no se dibuja. El color va en las UV de una paleta 2x2 y las
    ranuras se juntan al final con `one_submesh()`.
  - **El pivote donde lo tiene vanilla.** Casi todo se apoya en el suelo
    (`base_y` 0) y `ground()` es para eso; las piezas de pared no, y por eso
    `ground()` acepta una altura de base distinta de cero.

Las medidas de referencia salen de `tools/models/extract_reference.py`, que lee
las mallas del juego. No se adivina ninguna.
"""

import os
import sys

import bpy
from mathutils import Vector


# Cuadros de lado de la hoja de paleta. Tres o cuatro colores planos por pieza
# entran de sobra en 2x2, y 64 px es cuatro veces mas de lo que cuatro cuadros
# necesitan.
PALETTE = 2


# --- escena ----------------------------------------------------------------

def clear():
    """Un fichero vacio, empiece Blender con lo que empiece."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour, metallic=0.0, roughness=0.55):
    """Un material plano.

    Solo se usa para mirar la pieza dentro de Blender y para decidir en que
    cuadro de la paleta cae cada cara: el juego no importa materiales - ver
    `BuildModBundles.Configure` - y construye el suyo desde el JSON.
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic

    return mat


# --- primitivas ------------------------------------------------------------

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
    """Una punta plantada en `at`, con el pico a +length.

    `primitive_cone_add` centra el cono en su mitad, asi que cada llamada
    tendria que acordarse de subirlo la mitad de su largo o verlo hundirse en
    lo que sea que lo sostiene. Se hace aqui, una vez.
    """
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=0.0, depth=length,
        location=(at[0], at[1], at[2] + length / 2.0), rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def taper(name, bottom, top, length, at, rot=(0, 0, 0), sides=8):
    """Un tronco de cono: brocales, patas y cirios."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=bottom, radius2=top, depth=length,
        location=(at[0], at[1], at[2] + length / 2.0), rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def ring(name, radius, thick, height, at, sides=12):
    """Un anillo hueco, hecho con un toro achatado.

    Un cilindro con un booleano dentro cuesta un solver y deja caras interiores
    que nadie ve; un toro escalado en z da el mismo brocal a la vista del juego,
    que se dibuja desde arriba y a cuarenta pixeles de alto.
    """
    bpy.ops.mesh.primitive_torus_add(
        major_radius=radius, minor_radius=thick,
        major_segments=sides, minor_segments=4, location=at)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = Vector((1.0, 1.0, height / (2.0 * thick)))
    return obj


def skull(name, size, at, rot=(0, 0, 0)):
    """Una calavera legible a cuarenta pixeles: boveda, mandibula y dos cuencas.

    Las cuencas son cajas metidas hacia dentro, no un booleano: a esta escala se
    leen igual y no hay solver que pueda fallar.
    """
    dome = ball(name + "_dome", size, at, 10, 7)
    dome.scale = Vector((1.0, 1.15, 0.95))

    jaw = box(name + "_jaw", (size * 1.35, size * 1.1, size * 0.55),
              (at[0], at[1] + size * 0.25, at[2] - size * 0.75))

    sockets = []
    for side in (-1, 1):
        sockets.append(box(
            name + "_socket", (size * 0.42, size * 0.30, size * 0.42),
            (at[0] + side * size * 0.42, at[1] + size * 0.80, at[2] + size * 0.10)))

    piece = join([dome, jaw] + sockets, name)
    piece.rotation_euler = rot
    return piece


# --- montaje ---------------------------------------------------------------

def join(parts, name):
    """Suelda una lista de objetos en uno, de pie en el origen del mundo.

    **Se aplica la transformacion, y ese es todo el asunto.** El join de Blender
    le da al resultado la transformacion del *primero* de la lista y reescribe
    los vertices de los demas para que cuadren, asi que el espacio local del
    objeto unido queda donde estuviera esa primera pieza. Aplicarla deja espacio
    local y espacio de mundo siendo lo mismo de aqui en adelante, que es lo que
    hace que una altura medida signifique lo que dice.
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
    """Diferencia booleana, y el cortador desaparece."""
    modifier = target.modifiers.new("cut", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.object = tool

    # Blender 5 renombro los solvers: FAST/EXACT paso a FLOAT/EXACT/MANIFOLD.
    # Se coge el mas barato que ofrezca esta version.
    for solver in ("FLOAT", "FAST", "EXACT"):
        try:
            modifier.solver = solver
            break
        except TypeError:
            continue

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier="cut")

    bpy.data.objects.remove(tool, do_unlink=True)


def paint(parts, name, mat):
    """Une un grupo de piezas y le pone un solo material.

    Se pinta por grupos y no por posicion: las piezas ya saben de que estan
    hechas, y decidirlo por altura fue lo que pinto de oro media calavera del
    trono. El indice de material que queda aqui es el que `palette_uv()` lee
    para elegir cuadro de paleta.
    """
    group = join(parts, name)

    group.data.materials.clear()
    group.data.materials.append(mat)

    for poly in group.data.polygons:
        poly.material_index = 0

    return group


# --- acabado ---------------------------------------------------------------

def atlas_uv(obj, uvs):
    """Apunta cada cara a una coordenada fija del atlas **de vanilla**.

    Es la hermana de `palette_uv()` para las piezas que no pueden traer textura
    propia. Un arma equipada no tiene ranuras de variacion en su JSON: el juego
    resuelve su malla por el id del recurso - `dagger`, `mace`, y desde hoy
    `undead_claws` - y la dibuja con el material del prefab `base_equiped_weapon`,
    que lleva el atlas compartido `base_texture_png`. No hay sitio donde nombrar
    una textura nuestra, asi que el color hay que cogerlo de donde el juego ya
    esta mirando: las UV del dagger caen en (0.9369, 0.0832), que es acero gris.

    `uvs` es una coordenada por ranura de material, en el mismo orden que
    `palette_uv` usa los cuadros.
    """
    mesh = obj.data
    layer = mesh.uv_layers.active or mesh.uv_layers.new(name="atlas")

    for poly in mesh.polygons:
        u, v = uvs[min(poly.material_index, len(uvs) - 1)]
        for loop in poly.loop_indices:
            layer.data[loop].uv = (u, v)


def finish(obj, base_y=0.0, weld=0.0008, uvs=None, tiles=(1, 1)):
    """Suelda, saca las normales fuera, desdobla la paleta y planta la pieza.

    El orden no es negociable: `palette_uv()` necesita los indices de material,
    y `one_submesh()` los borra.

    `tiles` es la huella del edificio en baldosas - el `size` de su JSON, (x, z)
    - y es lo que decide donde cae la malla dentro de ella. Ver `ground()`.
    """
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=weld)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    if uvs is None:
        palette_uv(obj)
    else:
        atlas_uv(obj, uvs)

    one_submesh(obj)

    bpy.ops.object.shade_flat()
    ground(obj, base_y, tiles)
    return obj


def palette_uv(obj):
    """Apunta cada cara al centro del cuadro de su propio material.

    Un modelo cuyos materiales son tres colores planos no necesita espacio de
    textura por cara: necesita tres colores y que cada cara mire al que le toca.
    El centro del cuadro ademas sobrevive a cualquier filtrado y a cualquier
    nivel de mip, que es lo que un desdoblado real no garantiza a esta escala.

    **La ranura no es el cuadro, y ahi estaba el fallo.**
    `bpy.ops.object.join()` no anade las ranuras de los objetos unidos en el
    orden de la lista que se le pasa, sino en el orden en que esos objetos
    estan en la escena, que es el orden en que se crearon sus piezas. Una
    pieza que declara `mats(STONE, WOOD, BLOOD)` pero crea la geometria del
    liquido antes que la del armazon acaba con las ranuras en 0, 2, 1 - y
    `bake_albedo()` pinta los cuadros en el orden declarado, asi que el grupo
    de la ranura 1 lee el color del cuadro 1, que es el del otro grupo.

    En el pozo eso salia como tejado de sangre y agua de madera, y en el gato
    fue dos dias sin ojos. Lo que se arregla aqui es que haya **una sola**
    fuente de verdad: el orden de creacion de los materiales, que es el orden
    en que la pieza los declaro, y es el mismo que `bake_albedo()` usa. La
    posicion en la lista de ranuras no se mira.
    """
    mesh = obj.data
    uvs = mesh.uv_layers.active or mesh.uv_layers.new(name="palette")

    created = list(bpy.data.materials)
    born = {}
    for position, mat in enumerate(mesh.materials):
        born[position] = created.index(mat) if mat in created else position

    # De "cuando nacio cada uno" a "que puesto ocupa": lo que importa no es el
    # numero global, es el orden entre los materiales de esta pieza.
    square = {position: rank for rank, (position, _)
              in enumerate(sorted(born.items(), key=lambda item: item[1]))}

    for poly in mesh.polygons:
        slot = min(square.get(poly.material_index, poly.material_index),
                   PALETTE * PALETTE - 1)

        u = (slot % PALETTE + 0.5) / PALETTE
        v = (slot // PALETTE + 0.5) / PALETTE

        for loop in poly.loop_indices:
            uvs.data[loop].uv = (u, v)


def one_submesh(obj):
    """Junta las ranuras de material en una, ya puestas las UV.

    Una ranura es una submalla, y Unity dibuja `min(subMeshCount,
    materials.Length)`: con un material en el renderer, todo lo que pase de la
    submalla 0 se cae sin avisar. Eso fue el trono de un solo color. El color
    va en las UV, asi que las ranuras no hacen falta para nada.
    """
    mesh = obj.data

    for poly in mesh.polygons:
        poly.material_index = 0

    keep = mesh.materials[0] if len(mesh.materials) else None
    mesh.materials.clear()

    if keep is not None:
        mesh.materials.append(keep)


def ground(obj, base_y=0.0, tiles=(1, 1)):
    """Centro de la huella y base de la malla sobre el origen del edificio.

    Con `base_y` distinto de cero la pieza se deja colgada a esa altura, que es
    lo que hacen las de pared: `banner_wall_large` tiene su base en +0.577 y
    `silver_mirror_wall` en +1.122, y una pieza de pared plantada en el suelo
    aparece a los pies del muro en vez de sobre el.

    **El origen del edificio no es el centro de su huella, y ahi estaba el pozo
    descentrado.** Un edificio de 3x1 no se dibuja centrado en su rectangulo:
    el juego pone la malla sobre la **primera** baldosa y la huella crece desde
    ahi, asi que el centro cae en +(baldosas-1)/2. Se ve en las dos mitades del
    juego a la vez: `wooden_well` mide -0.414..+2.436 en X de Unity, y su
    `boxColliderSettings.centerOffset.x` del JSON vale exactamente 1. Lo mismo
    la mesa de 5 (+2.05), el sarcofago de 3 (+1) y el circulo de 3x3 (+1, +1).
    Una pieza nuestra centrada en el origen aparece por tanto una baldosa
    corrida, que es la foto de `Evidencias/PozoDesalineado.png`.

    Los ejes, medidos y no supuestos: se leyo el bundle ya construido con
    UnityPy y se busco una pieza asimetrica - la manivela del pozo, que se
    modela en Blender en x=+0.98 - y en el juego sale en x=-1.08. Asi que
    **Blender X es -X del juego**, y Blender Y es -Z del juego. De ahi que los
    dos desplazamientos lleven el signo cambiado respecto de lo que dice el
    JSON. Como UnityPy tambien niega la X al escribir un OBJ, los OBJ de
    `ASSESTS/Referencias` estan ya en este mismo espacio: el centro que se
    busca es literalmente el que mide la pieza de vanilla.
    """
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    wide, deep = tiles

    shift = Vector((-(min(xs) + max(xs)) / 2.0 - (wide - 1) / 2.0,
                    -(min(ys) + max(ys)) / 2.0 - (deep - 1) / 2.0,
                    -min(zs) + base_y))

    for vertex in obj.data.vertices:
        vertex.co += shift


def measure(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)), min(zs)


# --- salida ----------------------------------------------------------------

def bake_albedo(name, colours, path, size=64):
    """Escribe la hoja de paleta a la que apuntan las UV.

    Cuatro cuadros planos; los que sobren repiten el primero, que es mejor que
    dejarlos en blanco por si una cara se va de sitio.
    """
    import numpy

    step = size // PALETTE
    flat = numpy.ones((size, size, 4), dtype=numpy.float32)

    filled = list(colours) + [colours[0]] * (PALETTE * PALETTE - len(colours))

    for slot, colour in enumerate(filled):
        col = slot % PALETTE
        row = slot // PALETTE
        flat[row * step:(row + 1) * step, col * step:(col + 1) * step, :3] = colour[:3]

    image = bpy.data.images.new(name, size, size, alpha=True)
    image.pixels = flat.reshape(-1)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()

    return path


def export(obj, folder, name):
    """Escribe el FBX con el cambio de ejes metido en los vertices.

    `axis_up="Y"` por si solo no mueve un vertice: escribe la correccion de
    noventa grados en el *nodo* del objeto exportado, y nada rio abajo ve ese
    nodo - el juego saca un `Mesh` del bundle por nombre y lo pone en su propio
    prefab. Con `bake_space_transform` la rotacion va a los vertices, y con ella
    se cuela la conversion a centimetros: de ahi `global_scale=0.01`.

    El OBJ es solo para mirarlo sin abrir Unity.
    """
    os.makedirs(folder, exist_ok=True)

    fbx = os.path.join(folder, name + ".fbx")
    wave = os.path.join(folder, name + ".obj")

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, apply_unit_scale=True,
        global_scale=0.01, apply_scale_options="FBX_SCALE_NONE",
        object_types={"MESH"}, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, bake_space_transform=True,
        axis_forward="-Z", axis_up="Y")

    bpy.ops.wm.obj_export(filepath=wave, export_selected_objects=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y")

    return fbx, wave


def report(name, obj, reference=None, fbx=None, albedo=None, limit=None,
           preview=None):
    """Las medidas por pantalla, que es lo unico que demuestra la exportacion.

    `limit` es la huella maxima en baldosas (x, y): una pieza mas ancha que su
    propio edificio se sale de la caja de seleccion y de la sombra de contacto.
    """
    size, base = measure(obj)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)

    print("")
    print("=== %s ===" % name)
    print("  size      %.3f x %.3f x %.3f   (la baldosa es 1.000)" % size)
    print("  base at   %+.4f   (0 = pivote en el suelo)" % base)
    print("  triangles %d" % tris)

    if reference:
        print("  vanilla:  %s" % reference)
    if fbx:
        print("  -> %s" % fbx)
    if albedo:
        print("  -> %s" % albedo)
    if preview:
        print("  -> %s   <- abrelo, es lo que se va a ver" % preview)

    if limit and (size[0] > limit[0] or size[1] > limit[1]):
        print("  WARNING: %s se sale de su huella %s" % (name, limit), file=sys.stderr)


# --- mirarlo sin abrir el juego --------------------------------------------
#
# Hasta hoy el unico modo de ver una pieza era cargar partida: el ciclo era
# editar, hornear, Unity, medir, jugar, y el primer vistazo llegaba al final de
# los cinco. Los numeros de `report()` demuestran que la exportacion salio bien
# y no dicen absolutamente nada sobre si la pieza se lee - una silueta
# embarrada, una paleta con los colores cambiados de cuadro y un modelo
# perfecto miden exactamente igual.
#
# **Lo que se renderiza no es el objeto: es lo que el juego dibuja.**
# `one_submesh()` deja una sola ranura de material y el color vive en las UV,
# asi que el objeto acabado es de un color plano y renderizarlo tal cual daria
# una mancha. El render monta el material que el juego monta - el PNG de paleta
# leido a traves de esas mismas UV - y por eso vale para lo que ninguna medida
# vale: si la paleta esta mal ordenada, aqui se ve. El gato estuvo dos dias sin
# ojos porque `[FUR, STONE, DARK, FUR]` mandaba el blanco al dorado, y eso no
# lo habria dejado pasar una imagen.
#
# La rejilla del suelo es una baldosa de verdad, del tamano que el juego llama
# 1.0. `report()` avisa de una huella pasada con un WARNING que hay que leer;
# la rejilla lo hace evidente sin leer nada.

RENDER_AZIMUTH = 0.7854      # 45 grados: la diagonal desde la que se ve el juego
RENDER_ELEVATION = 0.6109    # 35 grados sobre el horizonte


def _preview_material(name, albedo):
    """El material que el juego monta: la paleta leida por las UV de la malla.

    `Closest` y no un filtrado suave porque los cuadros de la paleta son cuatro
    colores planos y las UV apuntan al centro de cada uno: interpolar solo
    puede mezclar cuadros vecinos, que es justo el fallo que este render existe
    para enseñar.
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is None:
        return mat

    image = bpy.data.images.load(albedo, check_existing=True)

    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"

    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.6
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.15

    return mat


def _tile_grid(obj, base_y=0.0):
    """Una rejilla de baldosas de verdad bajo la pieza, para leer la huella.

    El tamano se saca de la propia pieza redondeado hacia arriba y con una
    baldosa de margen por lado, asi que una pieza que se sale de su huella se
    sale visiblemente de un cuadro.
    """
    import math

    size, _ = measure(obj)
    tiles = max(2, int(math.ceil(max(size[0], size[1]))) + 2)

    bpy.ops.mesh.primitive_grid_add(x_subdivisions=tiles, y_subdivisions=tiles,
                                    size=tiles, location=(0, 0, base_y - 0.001))

    grid = bpy.context.active_object
    grid.name = "tile_grid"

    # Un modificador Wireframe y no `display_type = "WIRE"`: lo segundo es solo
    # de ventana y en el render sale un plano gris macizo que tapa la baldosa
    # en vez de dibujarla. El modificador hace alambre de verdad, que es
    # geometria y se renderiza.
    wire = grid.modifiers.new("wire", "WIREFRAME")
    wire.thickness = 0.012
    wire.use_replace = True

    return grid


def _frame(camera, obj, margin=1.18):
    """Encuadra la pieza entera desde donde este puesta la camara.

    Se proyectan los vertices al espacio de la camara y se toma el mayor, en
    vez de fiarse de la caja envolvente: una pieza alta y estrecha vista en
    diagonal ocupa en pantalla algo que ninguna de sus tres medidas dice.
    """
    to_camera = camera.matrix_world.inverted()
    points = [to_camera @ (obj.matrix_world @ v.co) for v in obj.data.vertices]

    if not points:
        return

    half_x = (max(p.x for p in points) - min(p.x for p in points)) / 2.0
    half_y = (max(p.y for p in points) - min(p.y for p in points)) / 2.0

    mid_x = (max(p.x for p in points) + min(p.x for p in points)) / 2.0
    mid_y = (max(p.y for p in points) + min(p.y for p in points)) / 2.0

    # La camara se corre para que el centro de lo proyectado caiga en el centro
    # del cuadro: con el pivote en el suelo, apuntar al origen deja la pieza
    # pegada al borde de abajo.
    right = camera.matrix_world.to_3x3() @ Vector((1, 0, 0))
    up = camera.matrix_world.to_3x3() @ Vector((0, 1, 0))
    camera.location += right * mid_x + up * mid_y

    camera.data.ortho_scale = max(half_x, half_y) * 2.0 * margin


def render(obj, path, albedo=None, base_y=0.0, size=640):
    """Un PNG de la pieza en tres cuartos, con la baldosa dibujada debajo.

    Ortografica y no perspectiva: el juego se dibuja casi sin fuga, y una
    proyeccion sin fuga es ademas la unica en la que dos piezas renderizadas
    por separado se pueden comparar.

    Devuelve la ruta escrita, o `None` si esta maquina no puede renderizar - lo
    que no debe pasar es que un render fallido tire abajo un horneado que ya
    escribio su FBX.
    """
    import math

    scene = bpy.context.scene

    try:
        if albedo:
            obj.data.materials.clear()
            obj.data.materials.append(_preview_material(obj.name + "_preview", albedo))

        grid = _tile_grid(obj, base_y)

        camera_data = bpy.data.cameras.new("preview_cam")
        camera_data.type = "ORTHO"

        camera = bpy.data.objects.new("preview_cam", camera_data)
        scene.collection.objects.link(camera)

        direction = Vector((
            math.cos(RENDER_AZIMUTH) * math.cos(RENDER_ELEVATION),
            -math.sin(RENDER_AZIMUTH) * math.cos(RENDER_ELEVATION),
            math.sin(RENDER_ELEVATION)))

        size_xyz, _ = measure(obj)
        centre = Vector((0.0, 0.0, base_y + size_xyz[2] / 2.0))

        camera.location = centre + direction * 12.0
        camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
        bpy.context.view_layer.update()

        _frame(camera, obj)
        scene.camera = camera

        # Workbench y no EEVEE: no hace falta iluminar nada - los colores son
        # planos y lo que importa es la silueta - y es el unico motor que sale
        # igual en cualquier maquina sin ventana ni GPU decente.
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "TEXTURE"
        shading.show_shadows = True
        shading.show_cavity = True

        scene.render.resolution_x = size
        scene.render.resolution_y = size
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = False
        scene.render.image_settings.file_format = "PNG"

        os.makedirs(os.path.dirname(path), exist_ok=True)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

        bpy.data.objects.remove(grid, do_unlink=True)
        bpy.data.objects.remove(camera, do_unlink=True)

        return path

    except Exception as error:                                   # noqa: BLE001
        print("  (sin render: %s)" % error, file=sys.stderr)
        return None
