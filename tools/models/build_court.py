"""Las piezas que le faltaban malla propia: las trece de la corte y la pira.

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
        --python tools/models/build_court.py

    ... --python tools/models/build_court.py -- count_coffin blood_well
        (solo esas dos, para iterar sobre una sin esperar a las trece)

Escribe ASSESTS/Modelos/aldrich_<pieza>.fbx (y .obj, para mirarlo sin Unity) y
la paleta de cada una en Mods/VampireCourt/Data/Textures.

**Por que existen.** Hasta hoy la corte entera era mobiliario de vanilla
repintado: el ataud era un sarcofago, el altar una mesa de despiece, el pozo el
brocal de madera y el empalado un espantapajaros con textura de carne. Eso vale
para probar que el edificio funciona - `prefabID` decide lo que hace y la malla
solo lo que parece - y no vale para publicar, porque un menu de sillas y pozos
de vanilla no es un mod que nadie reconozca.

**Las medidas no se inventan.** Cada pieza se construye contra la malla de
vanilla a la que sustituye, medida con `tools/models/extract_reference.py`, y
la referencia va escrita al lado de cada funcion. El juego se dibuja desde
arriba y en diagonal, asi que lo que decide si una pieza se lee es la silueta:
una caja plana con dos adornos encima se ve como una caja.

**La huella manda.** El `size` del edificio y su `boxColliderSettings` salen
del JSON, no de la malla (`HandleSelectionBoxCollider` corre antes de armar el
modelo), asi que una malla mas ancha que su huella deja la caja de seleccion
mal y se sale de la sombra de contacto. `report(..., limit=...)` avisa.

Blender es Z arriba y el juego Y arriba: aqui se modela en Z y `gm_model.export`
mete la correccion en los vertices.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import gm_model as gm                                           # noqa: E402


# --- los colores de la casa -------------------------------------------------
#
# Cuatro por pieza como mucho, que es lo que cabe en una paleta 2x2, y el
# primero es el que se ve si algo va mal: el cuadro 0 es donde caen las caras
# sin indice de material.

BONE = (0.86, 0.83, 0.74, 1.0)
STONE = (0.34, 0.33, 0.32, 1.0)
DARKSTONE = (0.24, 0.23, 0.23, 1.0)
GOLD = (0.83, 0.66, 0.24, 1.0)
IRON = (0.17, 0.17, 0.19, 1.0)
WOOD = (0.32, 0.22, 0.14, 1.0)
CRIMSON = (0.37, 0.06, 0.09, 1.0)
BLOOD = (0.45, 0.04, 0.05, 1.0)
WAX = (0.80, 0.75, 0.66, 1.0)
SILVER = (0.72, 0.74, 0.78, 1.0)
FLESH = (0.60, 0.48, 0.44, 1.0)
LINEN = (0.74, 0.70, 0.62, 1.0)
EMBER = (0.85, 0.42, 0.12, 1.0)

# La ropa del empalado, una por variante. Van aqui y no en un `_ClothTint`
# del JSON porque el prefab que usa la estaca es `scarecrow`, cuyo shader es
# `FoxyVoxel/Buildings/fv_banner` y no tiene esa propiedad: un ShaderParam a
# un nombre que el shader no declara se traga sin error y sin efecto. La
# paleta si es de cada malla, asi que el color de la tela se hornea aqui.
RAGS_LINEN = (0.74, 0.70, 0.62, 1.0)
RAGS_BROWN = (0.55, 0.46, 0.35, 1.0)
RAGS_SLATE = (0.46, 0.46, 0.51, 1.0)


def mats(*colours):
    """Un material por color, en el orden en que van a la paleta."""
    return [gm.material("slot%d" % i, c, metallic=1.0 if c == GOLD else 0.0)
            for i, c in enumerate(colours)]


# --- 1. el ataud ------------------------------------------------------------
#
# vanilla: limestone_sarcophagus 2.600 x 0.829 x 1.000, pivote en el suelo.

def count_coffin():
    """Un ataud de tapa abierta, con bisagra de hierro y forro rojo.

    La tapa **abierta** es lo unico que lo distingue de la cripta a la vista de
    arriba, que es como se juega: dos cajas cerradas del mismo tamano en el
    mismo sitio son el mismo mueble.
    """
    stone, iron, cloth = mats(STONE, IRON, CRIMSON)

    body, bands, lining = [], [], []

    # El cajon, estrechado a los pies: un ataud es mas ancho por los hombros, y
    # esa forma se lee desde arriba aunque el resto sea una caja.
    body.append(gm.box("shoulders", (1.30, 0.92, 0.46), (-0.55, 0, 0.23)))
    body.append(gm.box("feet", (1.25, 0.70, 0.46), (0.62, 0, 0.23)))
    body.append(gm.box("plinth", (2.58, 0.98, 0.10), (0, 0, 0.05)))

    # Interior: el forro rojo asoma porque la tapa esta abierta. Sin esto el
    # ataud abierto es un cajon de piedra vacio.
    lining.append(gm.box("lining", (2.30, 0.72, 0.06), (0, 0, 0.44)))
    lining.append(gm.box("pillow", (0.34, 0.44, 0.10), (-0.92, 0, 0.50)))

    # Casi vertical y pegada al borde de atras: a sesenta grados la tapa se
    # comia media baldosa por detras (profundidad 1.381 medida, huella 0.99) y
    # la caja de seleccion no la sigue, porque sale del JSON.
    body.append(gm.box("lid", (2.44, 0.86, 0.12), (-0.10, -0.36, 0.90),
                       rot=(-1.45, 0, 0)))

    for x in (-0.95, -0.30, 0.35, 0.95):
        bands.append(gm.box("band_%d" % int(x * 100), (0.07, 0.99, 0.50),
                            (x, 0, 0.26)))

    bands.append(gm.cyl("hinge", 0.05, 2.30, (0, -0.44, 0.50),
                        rot=(0, 1.5708, 0), sides=6))

    return gm.join([gm.paint(body, "stone_group", stone),
                    gm.paint(bands, "iron_group", iron),
                    gm.paint(lining, "cloth_group", cloth)],
                   "aldrich_count_coffin"), (STONE, IRON, CRIMSON)


# --- 2. la cripta -----------------------------------------------------------
#
# vanilla: limestone_sarcophagus 2.600 x 0.829 x 1.000, pivote en el suelo.

def count_crypt():
    """Tumba cerrada con yacente encima, que es lo que la separa del ataud.

    Una yacente entera a esta escala seria una mancha; lo que se lee desde
    arriba son tres cosas - cabeza, manos sobre el pecho y pies - asi que el
    cuerpo es un bulto y el detalle esta solo en esas tres.
    """
    stone, gold, bone = mats(STONE, GOLD, BONE)

    chest, trim, figure = [], [], []

    chest.append(gm.box("base", (2.58, 0.98, 0.14), (0, 0, 0.07)))
    chest.append(gm.box("chest", (2.44, 0.88, 0.52), (0, 0, 0.40)))
    chest.append(gm.box("cap", (2.56, 0.96, 0.08), (0, 0, 0.70)))

    # Arcada ciega en el costado: cinco pilastras marcadas en relieve, que es
    # como se hace una tumba de piedra sin gastar un booleano por hueco.
    for i in range(5):
        trim.append(gm.box("pilaster_%d" % i, (0.08, 0.92, 0.44),
                           (-1.0 + i * 0.5, 0, 0.40)))

    figure.append(gm.box("body", (1.70, 0.44, 0.16), (0.05, 0, 0.82)))
    figure.append(gm.box("legs", (0.70, 0.34, 0.12), (1.00, 0, 0.80)))
    figure.append(gm.ball("head", 0.17, (-0.98, 0, 0.88), 10, 7))
    figure.append(gm.box("hands", (0.22, 0.26, 0.12), (-0.30, 0, 0.92)))
    figure.append(gm.box("feet", (0.16, 0.30, 0.14), (1.24, 0, 0.84)))

    # La espada sobre el pecho, que es lo que dice que el de dentro mandaba.
    trim.append(gm.box("sword", (1.05, 0.07, 0.05), (0.15, 0, 0.94)))
    trim.append(gm.box("guard", (0.06, 0.26, 0.05), (-0.36, 0, 0.94)))

    return gm.join([gm.paint(chest, "stone_group", stone),
                    gm.paint(trim, "gold_group", gold),
                    gm.paint(figure, "bone_group", bone)],
                   "aldrich_count_crypt"), (STONE, GOLD, BONE)


# --- 3. el altar ------------------------------------------------------------
#
# vanilla: butchering_table 3.041 x 2.411 x 1.836, pivote en el suelo.
# La huella del edificio es 3x2, asi que hay sitio en profundidad.

def blood_altar():
    """Piedra con canal de desague y cuenco, que es lo que pedia el ticket.

    El canal va hundido en la losa y **cae hacia un lado**, al cuenco: un canal
    simetrico se lee como una junta de la piedra y no como un desague. La losa
    son tres tiras - dos altas y una hundida - porque eso cuesta tres cajas y
    un booleano cuesta un solver que puede fallar en batch.
    """
    stone, iron, blood = mats(DARKSTONE, IRON, BLOOD)

    slab, fittings, liquid = [], [], []

    slab.append(gm.box("footing", (2.86, 1.66, 0.12), (0, 0, 0.06)))
    slab.append(gm.box("pedestal", (2.30, 1.20, 0.62), (0, 0, 0.42)))
    slab.append(gm.box("slab_front", (2.80, 0.52, 0.16), (0, 0.55, 0.81)))
    slab.append(gm.box("slab_back", (2.80, 0.52, 0.16), (0, -0.55, 0.81)))
    slab.append(gm.box("slab_floor", (2.80, 0.58, 0.09), (0, 0, 0.775)))
    slab.append(gm.box("slab_head", (0.36, 1.62, 0.16), (-1.22, 0, 0.81)))

    liquid.append(gm.box("gutter", (2.30, 0.30, 0.03), (0.16, 0, 0.828)))
    liquid.append(gm.cyl("bowl_fill", 0.20, 0.05, (1.06, 0, 0.90), sides=10))

    fittings.append(gm.ring("bowl", 0.23, 0.05, 0.22, (1.06, 0, 0.88), sides=12))
    for side in (-1, 1):
        fittings.append(gm.box("chain_ring_%d" % side, (0.10, 0.10, 0.05),
                               (-1.05, side * 0.62, 0.90)))
    fittings.append(gm.box("blade_rest", (0.42, 0.10, 0.06), (-0.55, -0.66, 0.92)))

    return gm.join([gm.paint(slab, "stone_group", stone),
                    gm.paint(fittings, "iron_group", iron),
                    gm.paint(liquid, "blood_group", blood)],
                   "aldrich_blood_altar"), (DARKSTONE, IRON, BLOOD)


# --- 4. el circulo ----------------------------------------------------------
#
# vanilla: pagan_ritual_circle 3.134 x 0.297 x 3.023, pivote en el suelo.

def blood_ritual_circle():
    """Un sigilo propio grabado, no el circulo pagano repintado.

    Grabado quiere decir **en relieve por encima del suelo**, no hundido: el
    juego dibuja su suelo por encima de casi todo, y una linea hundida a tres
    centimetros no se ve nunca.
    """
    stone, blood, wax = mats(DARKSTONE, BLOOD, WAX)

    disc, lines, candles = [], [], []

    disc.append(gm.cyl("disc", 1.46, 0.08, (0, 0, 0.04), sides=16))
    disc.append(gm.ring("rim", 1.40, 0.05, 0.10, (0, 0, 0.09), sides=20))
    disc.append(gm.ring("inner", 0.92, 0.04, 0.08, (0, 0, 0.09), sides=16))

    # Estrella de cinco puntas de un solo trazo: es la unica figura que a esta
    # escala sigue leyendose cuando la cruza un colono.
    points = [(math.cos(math.radians(90 + i * 72)) * 0.86,
               math.sin(math.radians(90 + i * 72)) * 0.86) for i in range(5)]
    order = [0, 2, 4, 1, 3, 0]

    for a, b in zip(order, order[1:]):
        x0, y0 = points[a]
        x1, y1 = points[b]
        length = math.hypot(x1 - x0, y1 - y0)
        lines.append(gm.box("ray_%d_%d" % (a, b), (length, 0.07, 0.05),
                            ((x0 + x1) / 2.0, (y0 + y1) / 2.0, 0.09),
                            rot=(0, 0, math.atan2(y1 - y0, x1 - x0))))

    # Cinco velas, una por punta: dan altura a una pieza que si no es un suelo.
    for i, (x, y) in enumerate(points):
        candles.append(gm.cyl("candle_%d" % i, 0.055, 0.22, (x, y, 0.19), sides=6))

    return gm.join([gm.paint(disc, "stone_group", stone),
                    gm.paint(lines, "blood_group", blood),
                    gm.paint(candles, "wax_group", wax)],
                   "aldrich_blood_circle"), (DARKSTONE, BLOOD, WAX)


# --- 5 y 6. los estandartes -------------------------------------------------
#
# vanilla: banner_single_large 1.070 x 2.981 x 0.190, pivote en el suelo.
#          banner_wall_large   0.079 x 2.320 x 1.070, base en +0.577.

def court_banner():
    """De pie: palo, travesano y pano con la heraldica, rasgado por abajo.

    La heraldica va en relieve y no pintada: un escudo pintado necesitaria un
    desdoblado de verdad, y en relieve se ve igual de lejos con el mismo cuadro
    de paleta y sin gastar textura.
    """
    wood, cloth, gold = mats(WOOD, CRIMSON, GOLD)

    frame, sheet, trim = [], [], []

    frame.append(gm.cyl("pole", 0.055, 2.86, (0, 0, 1.43), sides=8))
    frame.append(gm.box("foot", (0.42, 0.42, 0.12), (0, 0, 0.06)))
    frame.append(gm.cyl("crossbar", 0.04, 0.88, (0, 0, 2.62),
                        rot=(0, 1.5708, 0), sides=6))

    sheet.append(gm.box("cloth", (0.80, 0.05, 1.62), (0, 0.06, 1.72)))

    # El borde inferior en tres picos: un pano acabado en recto se lee como una
    # tabla, y este mod va de cosas viejas.
    for i, x in enumerate((-0.28, 0.0, 0.28)):
        sheet.append(gm.box("tail_%d" % i, (0.26, 0.05, 0.30), (x, 0.06, 0.78)))

    sheet.append(gm.box("chev_a", (0.40, 0.03, 0.09), (-0.14, 0.09, 1.86),
                        rot=(0, 0.6, 0)))
    sheet.append(gm.box("chev_b", (0.40, 0.03, 0.09), (0.14, 0.09, 1.86),
                        rot=(0, -0.6, 0)))
    sheet.append(gm.ball("drop", 0.11, (0, 0.09, 1.56), 8, 6))

    trim.append(gm.ball("finial", 0.085, (0, 0, 2.90), 10, 7))
    for side in (-1, 1):
        trim.append(gm.ball("bead_%d" % side, 0.05, (side * 0.43, 0, 2.62), 8, 5))

    return gm.join([gm.paint(frame, "wood_group", wood),
                    gm.paint(sheet, "cloth_group", cloth),
                    gm.paint(trim, "gold_group", gold)],
                   "aldrich_court_banner"), (WOOD, CRIMSON, GOLD)


def court_banner_wall():
    """De pared: el mismo pano, colgado de una barra corta contra el muro.

    Fino en x, que es como cuelga `banner_wall_large`, y con la base a +0.577
    para que quede sobre el muro y no a sus pies.
    """
    wood, cloth, gold = mats(WOOD, CRIMSON, GOLD)

    frame, sheet, trim = [], [], []

    frame.append(gm.cyl("bar", 0.04, 0.86, (0.02, 0, 2.24),
                        rot=(1.5708, 0, 0), sides=6))
    for side in (-1, 1):
        frame.append(gm.box("bracket_%d" % side, (0.09, 0.07, 0.16),
                            (0.0, side * 0.42, 2.24)))

    sheet.append(gm.box("cloth", (0.05, 0.78, 1.52), (0.05, 0, 1.44)))
    for i, y in enumerate((-0.27, 0.0, 0.27)):
        sheet.append(gm.box("tail_%d" % i, (0.05, 0.25, 0.28), (0.05, y, 0.54)))

    # La heraldica gira noventa grados: aqui el pano mira a lo largo de y.
    trim.append(gm.box("chev_a", (0.03, 0.40, 0.09), (0.085, -0.14, 1.52),
                       rot=(0.6, 0, 0)))
    trim.append(gm.box("chev_b", (0.03, 0.40, 0.09), (0.085, 0.14, 1.52),
                       rot=(-0.6, 0, 0)))
    trim.append(gm.ball("drop", 0.11, (0.085, 0, 1.26), 8, 6))
    for side in (-1, 1):
        trim.append(gm.ball("bead_%d" % side, 0.05, (0.02, side * 0.44, 2.24), 8, 5))

    return gm.join([gm.paint(frame, "wood_group", wood),
                    gm.paint(sheet, "cloth_group", cloth),
                    gm.paint(trim, "gold_group", gold)],
                   "aldrich_court_banner_wall"), (WOOD, CRIMSON, GOLD)


# --- 7. el candelabro -------------------------------------------------------
#
# vanilla: iron_candle 0.837 x 2.089 x 0.837, pivote en el suelo.

def crimson_candle():
    """Alto, con varios brazos y cera derramada, que es lo que pedia el ticket.

    Los brazos salen a dos alturas y no a una: seis velas en corona son un aro
    visto desde arriba, y tres arriba y tres abajo tienen silueta desde el lado,
    que es de donde se mira.
    """
    iron, wax, blood = mats(IRON, WAX, BLOOD)

    stand, candles, drips = [], [], []

    stand.append(gm.taper("foot", 0.34, 0.18, 0.14, (0, 0, 0.0), sides=10))
    stand.append(gm.cyl("stem", 0.055, 1.70, (0, 0, 0.95), sides=8))
    stand.append(gm.ball("knop", 0.10, (0, 0, 1.05), 10, 7))

    for level, (height, reach, phase) in enumerate(
            ((1.16, 0.33, 0.0), (1.62, 0.26, math.pi / 3.0))):
        for i in range(3):
            angle = phase + i * (2 * math.pi / 3.0)
            x, y = math.cos(angle) * reach, math.sin(angle) * reach

            stand.append(gm.box("arm_%d_%d" % (level, i), (reach, 0.05, 0.05),
                                (x / 2.0, y / 2.0, height), rot=(0, 0, angle)))
            stand.append(gm.cyl("cup_%d_%d" % (level, i), 0.075, 0.05,
                                (x, y, height + 0.05), sides=8))

            candles.append(gm.cyl("candle_%d_%d" % (level, i), 0.05, 0.26,
                                  (x, y, height + 0.20), sides=6))
            drips.append(gm.ball("drip_%d_%d" % (level, i), 0.045,
                                 (x, y, height + 0.06), 8, 5))

    candles.append(gm.cyl("candle_top", 0.055, 0.30, (0, 0, 1.96), sides=6))
    drips.append(gm.ball("drip_top", 0.05, (0, 0, 1.82), 8, 5))

    return gm.join([gm.paint(stand, "iron_group", iron),
                    gm.paint(candles, "wax_group", wax),
                    gm.paint(drips, "blood_group", blood)],
                   "aldrich_crimson_candle"), (IRON, WAX, BLOOD)


# --- 8. el brasero ----------------------------------------------------------
#
# vanilla: iron_brazear 0.913 x 0.914 x 0.913, pivote en el suelo.

def blood_brazier():
    """Tres patas y un cuenco con relieve, encendido.

    Tres patas y no cuatro porque en diagonal - que es como se ve todo aqui -
    cuatro se tapan de dos en dos y el brasero parece flotar.
    """
    iron, blood, wax = mats(IRON, BLOOD, WAX)

    stand, fire, coals = [], [], []

    for i in range(3):
        angle = math.radians(90 + i * 120)
        x, y = math.cos(angle) * 0.27, math.sin(angle) * 0.27
        stand.append(gm.box("leg_%d" % i, (0.07, 0.07, 0.52), (x, y, 0.26),
                            rot=(math.sin(angle) * 0.18, -math.cos(angle) * 0.18, 0)))
        stand.append(gm.box("claw_%d" % i, (0.13, 0.13, 0.06), (x, y, 0.03)))

    stand.append(gm.taper("bowl", 0.21, 0.40, 0.26, (0, 0, 0.50), sides=12))
    stand.append(gm.ring("lip", 0.40, 0.045, 0.09, (0, 0, 0.78), sides=14))

    # El relieve del cuenco: seis costillas, que es lo que separa este brasero
    # del de vanilla cuando los dos son un cono.
    for i in range(6):
        angle = math.radians(i * 60)
        stand.append(gm.box("rib_%d" % i, (0.05, 0.05, 0.24),
                            (math.cos(angle) * 0.32, math.sin(angle) * 0.32, 0.62),
                            rot=(0, 0, angle)))

    coals.append(gm.cyl("coals", 0.33, 0.06, (0, 0, 0.77), sides=12))
    for i in range(3):
        angle = math.radians(30 + i * 120)
        fire.append(gm.spike("flame_%d" % i, 0.11, 0.34,
                             (math.cos(angle) * 0.13, math.sin(angle) * 0.13, 0.78),
                             sides=6))

    return gm.join([gm.paint(stand, "iron_group", iron),
                    gm.paint(fire, "blood_group", blood),
                    gm.paint(coals, "wax_group", wax)],
                   "aldrich_blood_brazier"), (IRON, BLOOD, WAX)


# --- 9. el espejo velado ----------------------------------------------------
#
# vanilla: silver_mirror_wall 0.102 x 1.498 x 0.928, base en +1.122.

def veiled_mirror():
    """Espejo de pared con el velo echado por encima, medio cubriendolo.

    Medio: un velo entero es una tela colgada y deja de ser un espejo, y un
    espejo sin velo es el de vanilla. La mitad de arriba tapada y el cristal
    asomando por abajo es lo que lo cuenta de un vistazo.
    """
    gold, cloth, silver = mats(GOLD, CRIMSON, SILVER)

    frame, veil, glass = [], [], []

    frame.append(gm.box("frame", (0.07, 0.80, 1.28), (0, 0, 0.64)))
    frame.append(gm.box("crest", (0.07, 0.30, 0.16), (0, 0, 1.34)))
    for side in (-1, 1):
        frame.append(gm.ball("stud_%d" % side, 0.055, (0.03, side * 0.34, 0.10), 8, 5))

    glass.append(gm.box("glass", (0.03, 0.66, 1.10), (0.045, 0, 0.62)))

    veil.append(gm.box("veil", (0.05, 0.92, 0.66), (0.075, 0, 1.06)))
    for i, y in enumerate((-0.30, -0.10, 0.10, 0.30)):
        veil.append(gm.box("fold_%d" % i, (0.05, 0.16, 0.22), (0.075, y, 0.66)))

    return gm.join([gm.paint(frame, "gold_group", gold),
                    gm.paint(veil, "cloth_group", cloth),
                    gm.paint(glass, "silver_group", silver)],
                   "aldrich_veiled_mirror"), (GOLD, CRIMSON, SILVER)


# --- 10. el relicario -------------------------------------------------------
#
# vanilla: relic_shelf 0.671 x 1.965 x 0.702, pivote en el suelo.

def court_reliquary():
    """Vitrina cerrada con reliquias dentro: cristal, no estanteria abierta.

    Lo que hace que se lea como vitrina y no como armario es que los montantes
    son finos y lo de dentro asoma entre ellos - huesos y un craneo, que son
    las dos unicas reliquias que a esta escala se distinguen.
    """
    wood, gold, bone = mats(WOOD, GOLD, BONE)

    case, trim, relics = [], [], []

    case.append(gm.box("plinth", (0.64, 0.66, 0.16), (0, 0, 0.08)))
    case.append(gm.box("back", (0.60, 0.06, 1.42), (0, -0.28, 0.87)))
    for side in (-1, 1):
        case.append(gm.box("side_%d" % side, (0.05, 0.60, 1.42),
                           (side * 0.27, 0, 0.87)))
    case.append(gm.box("shelf_mid", (0.58, 0.60, 0.05), (0, 0, 1.04)))
    case.append(gm.box("roof", (0.66, 0.68, 0.09), (0, 0, 1.62)))
    case.append(gm.spike("finial", 0.09, 0.24, (0, 0, 1.66), sides=6))

    trim.append(gm.box("lock", (0.09, 0.05, 0.12), (0, 0.30, 1.14)))
    trim.append(gm.box("band_low", (0.66, 0.68, 0.04), (0, 0, 0.20)))
    trim.append(gm.box("band_high", (0.66, 0.68, 0.04), (0, 0, 1.54)))

    relics.append(gm.skull("relic_skull", 0.13, (0, 0, 1.28)))
    relics.append(gm.cyl("relic_bone_a", 0.035, 0.40, (-0.10, 0.02, 0.60),
                         rot=(0, 1.5708, 0.3), sides=6))
    relics.append(gm.cyl("relic_bone_b", 0.035, 0.36, (0.08, -0.02, 0.68),
                         rot=(0, 1.5708, -0.4), sides=6))

    return gm.join([gm.paint(case, "wood_group", wood),
                    gm.paint(trim, "gold_group", gold),
                    gm.paint(relics, "bone_group", bone)],
                   "aldrich_court_reliquary"), (WOOD, GOLD, BONE)


# --- 11. el pozo ------------------------------------------------------------
#
# vanilla: wooden_well 2.850 x 2.401 x 1.506, pivote en el suelo.

def blood_well():
    """Brocal de piedra, cubo, y lo de dentro rojo - y esta vez se ve.

    En el pozo prestado el rojo no se veia porque la malla de vanilla no tiene
    superficie interior: se pinto el agua y no habia agua que pintar. Aqui el
    liquido es una **tapa solida** justo por debajo del borde, asi que desde
    arriba, que es de donde se mira, el pozo esta lleno de sangre.
    """
    stone, wood, blood = mats(STONE, WOOD, BLOOD)

    kerb, frame, liquid = [], [], []

    # El brocal es redondo pero el edificio es de 3x1: un circulo de radio uno
    # deja el pozo con dos baldosas y media de fondo (2.400 medidas) sobre una
    # huella de 1.5. Radio 0.62, que es el que cabe.
    kerb.append(gm.cyl("apron", 0.74, 0.10, (0, 0, 0.05), sides=14))
    kerb.append(gm.ring("kerb", 0.62, 0.09, 0.56, (0, 0, 0.34), sides=16))
    for i in range(8):
        angle = math.radians(i * 45)
        kerb.append(gm.box("stone_%d" % i, (0.20, 0.14, 0.14),
                           (math.cos(angle) * 0.62, math.sin(angle) * 0.62, 0.62),
                           rot=(0, 0, angle)))

    liquid.append(gm.cyl("surface", 0.56, 0.06, (0, 0, 0.52), sides=16))
    liquid.append(gm.box("spill", (0.46, 0.26, 0.03), (0.92, 0.16, 0.105)))

    frame.append(gm.box("post_a", (0.14, 0.14, 1.62), (-0.88, 0, 0.90)))
    frame.append(gm.box("post_b", (0.14, 0.14, 1.62), (0.88, 0, 0.90)))
    frame.append(gm.box("lintel", (2.00, 0.16, 0.14), (0, 0, 1.74)))
    frame.append(gm.cyl("winch", 0.10, 1.50, (0, 0, 1.52),
                        rot=(0, 1.5708, 0), sides=8))
    frame.append(gm.box("crank", (0.30, 0.06, 0.06), (0.98, 0, 1.52)))
    frame.append(gm.box("rope", (0.03, 0.03, 0.52), (0.20, 0, 1.24)))
    frame.append(gm.taper("bucket", 0.15, 0.18, 0.26, (0.18, 0, 0.86), sides=10))

    # Dos aguas encima: sin tejadillo los dos postes son dos palos.
    for side in (-1, 1):
        frame.append(gm.box("roof_%d" % side, (2.10, 0.56, 0.07),
                            (0, side * 0.24, 1.92), rot=(side * 0.55, 0, 0)))

    return gm.join([gm.paint(kerb, "stone_group", stone),
                    gm.paint(frame, "wood_group", wood),
                    gm.paint(liquid, "blood_group", blood)],
                   "aldrich_blood_well"), (STONE, WOOD, BLOOD)


# --- 12. la mesa de vigilia -------------------------------------------------
#
# vanilla: table_2x5_stone 4.849 x 0.824 x 1.804, pivote en el suelo.
# El alto de vanilla es 0.824 y la caja de seleccion sale del JSON, asi que los
# candelabros se quedan por debajo de 1.30 a proposito.

def vigil_table():
    """Mesa de banquete con los candelabros integrados en la propia piedra.

    Integrados quiere decir que salen del tablero y no encima de el: un
    candelabro suelto sobre una mesa es otro mueble, y aqui tienen que viajar
    con ella.
    """
    stone, gold, wax = mats(DARKSTONE, GOLD, WAX)

    table, trim, candles = [], [], []

    table.append(gm.box("top", (4.70, 1.70, 0.14), (0, 0, 0.76)))
    table.append(gm.box("apron", (4.40, 1.44, 0.10), (0, 0, 0.66)))

    for x in (-1.90, 0.0, 1.90):
        table.append(gm.box("leg_%d" % int(x * 10), (0.34, 1.20, 0.62), (x, 0, 0.35)))
        table.append(gm.box("shoe_%d" % int(x * 10), (0.46, 1.34, 0.08), (x, 0, 0.04)))

    # Correa de oro por el canto largo: es lo unico que hace que cinco baldosas
    # de piedra oscura no sean un muro tumbado.
    for side in (-1, 1):
        trim.append(gm.box("edge_%d" % side, (4.70, 0.05, 0.05),
                           (0, side * 0.84, 0.83)))
    trim.append(gm.box("inlay", (3.30, 0.30, 0.03), (0, 0, 0.835)))

    for x in (-1.55, 0.0, 1.55):
        table.append(gm.taper("stem_%d" % int(x * 10), 0.16, 0.07, 0.24,
                              (x, 0, 0.83), sides=8))
        trim.append(gm.ring("cup_%d" % int(x * 10), 0.11, 0.035, 0.07,
                            (x, 0, 1.09), sides=10))
        candles.append(gm.cyl("candle_%d" % int(x * 10), 0.055, 0.22,
                              (x, 0, 1.17), sides=6))

    return gm.join([gm.paint(table, "stone_group", stone),
                    gm.paint(trim, "gold_group", gold),
                    gm.paint(candles, "wax_group", wax)],
                   "aldrich_vigil_table"), (DARKSTONE, GOLD, WAX)


# --- 13. el empalado --------------------------------------------------------
#
# vanilla: scarecrow 2.235 x 2.942 x 0.704, pivote en el suelo.

def impaled_stake(variant=0):
    """Un cuerpo vestido en la estaca, no un espantapajaros con textura de carne.

    Lo que separa una cosa de la otra es como cuelga: un espantapajaros esta
    crucificado, con los brazos rectos hacia fuera, y un empalado esta
    **descolgado** - cabeza caida, brazos hacia abajo y el peso en los hombros.

    **Los brazos nacen del hombro y bajan, que es lo que se pidio.** Antes eran
    una sola caja centrada en el pecho, a la altura del torso: como la caja mide
    0.62 y el centro estaba en 2.06, el brazo sobresalia por arriba del hombro y
    terminaba a media espalda - leido desde el juego, un brazo que sale de abajo
    hacia arriba. Ahora son dos tramos encadenados desde `SHOULDER_Z` hacia el
    suelo, cada uno colgado del final del anterior, asi que la mano queda por
    debajo de la cadera y el hombro es el punto del que cuelga todo.

    **Y va vestido.** Cuatro colores en vez de tres - la paleta es de 2x2, asi
    que `LINEN` es el ultimo hueco que quedaba. La ropa no se pone encima de la
    carne sino en su lugar: el sayo *es* el torso visible y la manga *es* el
    tramo de arriba del brazo, porque dos cajas en el mismo sitio con dos
    materiales distintos es un parpadeo de profundidad, no una camisa. Quedan de
    carne la cabeza, el cuello, los antebrazos y las pantorrillas, que es lo que
    se ve de alguien vestido.

    **La sangre del pecho es paleta, no bulto.** Lo que se pidio es una mancha,
    y una mancha no puede ser una caja mas: se lee como un bulto desde arriba y
    rompe la silueta, que es lo unico que se ve a cuarenta pixeles. Va como
    chapa de 3 cm asomada 2.5 cm por delante del sayo y pintada con el cuarto
    color, el mismo `BLOOD` que ya corre por el palo, con dos regueros que
    bajan de ella. Asomada y no a ras: dos caras en el mismo plano parpadean.

    **Tres cabezas y no una.** Dos estacas seguidas eran el mismo hombre dos
    veces. `variant` da rapado, barbado y melenudo, y el pelo va en el grupo de
    la madera a proposito - la paleta es de 2x2 y los cuatro huecos estaban
    dados; el castano del palo es pelo aceptable y un quinto color costaria una
    textura de 3x3 para toda la pieza -. Y cambia la ropa: el hueco de la tela
    se hornea con un color por variante, lino, pardo y pizarra, que es lo que
    acaba de separarlas de lejos. La eleccion no se hace aqui: son tres mallas,
    y `isRandom` de la lista de variaciones la reparte al plantar, como hacen
    las seis estatuas de vanilla.
    """
    rags = (RAGS_LINEN, RAGS_BROWN, RAGS_SLATE)[variant]
    wood, flesh, cloth, blood = mats(WOOD, FLESH, rags, BLOOD)

    stake, body, worn, gore = [], [], [], []

    # El palo muere **dentro** del torso: subido hasta 2.90 pasaba por delante
    # de la cara - el cuerpo esta en -y y el palo en 0 - y lo que se veia de
    # frente era un poste con una bola detras. Lo que asoma por arriba es la
    # punta, y asoma por el hombro.
    stake.append(gm.cyl("post", 0.09, 2.36, (0, 0.03, 1.18), sides=8))
    stake.append(gm.box("cairn", (0.62, 0.62, 0.20), (0, 0, 0.10)))
    for side in (-1, 1):
        stake.append(gm.box("brace_%d" % side, (0.06, 0.06, 0.70),
                            (side * 0.22, 0, 0.42), rot=(0, side * 0.45, 0)))

    # De donde cuelga el brazo: el borde de arriba del torso, no su centro.
    SHOULDER_Z = 2.34
    UPPER = 0.42
    FORE = 0.38

    body.append(gm.box("chest", (0.40, 0.26, 0.20), (0, 0.02, 2.33)))
    body.append(gm.box("neck", (0.13, 0.13, 0.12), (0, 0.04, 2.44)))
    # La cabeza cae hacia un lado distinto en cada variante: es lo primero que
    # se nota al plantar dos juntas, antes que el pelo.
    head_x = (0.0, 0.035, -0.045)[variant]
    body.append(gm.ball("head", 0.155, (head_x, 0.04, 2.52), 10, 7))

    # El pelo va con la madera porque la paleta 2x2 no tiene un quinto hueco.
    if variant == 1:
        # Barbado: barba cerrada y cerquillo corto.
        stake.append(gm.box("beard", (0.15, 0.10, 0.13), (head_x, -0.085, 2.44)))
        stake.append(gm.box("crop", (0.21, 0.21, 0.05), (head_x, 0.04, 2.645)))
    elif variant == 2:
        # Melenudo: pelo largo por detras y por los lados, sin barba.
        stake.append(gm.box("mane", (0.22, 0.10, 0.28), (head_x, 0.155, 2.50)))
        stake.append(gm.box("crop", (0.22, 0.22, 0.05), (head_x, 0.045, 2.645)))
        for side in (-1, 1):
            stake.append(gm.box("lock_%d" % side, (0.05, 0.13, 0.20),
                                (head_x + side * 0.15, 0.08, 2.49)))

    # El sayo, de los hombros a medio muslo, con la falda mas suelta que el
    # cuerpo: es lo que hace que se lea como tela y no como otra caja de carne.
    worn.append(gm.box("tunic", (0.44, 0.30, 0.62), (0, 0.02, 2.02)))
    worn.append(gm.box("skirt", (0.40, 0.28, 0.34), (0, 0.02, 1.56)))
    worn.append(gm.box("belt", (0.45, 0.31, 0.06), (0, 0.02, 1.74)))

    for side in (-1, 1):
        # Tramo de arriba: manga, colgada del hombro y abierta hacia fuera.
        lean = side * 0.20
        top_x = side * 0.21
        mid_x = top_x + math.sin(lean) * (UPPER / 2.0)
        mid_z = SHOULDER_Z - math.cos(lean) * (UPPER / 2.0)
        worn.append(gm.box("sleeve_%d" % side, (0.13, 0.13, UPPER),
                           (mid_x, 0.04, mid_z), rot=(0, lean, 0)))

        # Tramo de abajo: antebrazo y mano, colgados del final de la manga y ya
        # casi a plomo. La mano acaba por debajo de la cadera.
        elbow_x = top_x + math.sin(lean) * UPPER
        elbow_z = SHOULDER_Z - math.cos(lean) * UPPER
        fall = side * 0.06
        rags_x = elbow_x + math.sin(fall) * (FORE / 2.0)
        body.append(gm.box("forearm_%d" % side, (0.10, 0.10, FORE),
                           (rags_x, 0.04, elbow_z - math.cos(fall) * (FORE / 2.0)),
                           rot=(0, fall, 0)))
        body.append(gm.box("hand_%d" % side, (0.11, 0.09, 0.12),
                           (elbow_x + math.sin(fall) * FORE, 0.04,
                            elbow_z - math.cos(fall) * FORE - 0.05)))

        # Pierna: calzon hasta media pantorrilla y el resto de carne.
        worn.append(gm.box("breech_%d" % side, (0.15, 0.15, 0.36),
                           (side * 0.12, 0.04, 1.38), rot=(0, side * 0.12, 0)))
        body.append(gm.box("shin_%d" % side, (0.12, 0.12, 0.42),
                           (side * 0.14, 0.04, 1.00), rot=(0, side * 0.12, 0)))

    # La punta que sale por el hombro dice que esta empalado y no colgado, y la
    # sangre corre por el palo hasta el monton de piedras.
    # La mancha del pecho, la que se pidio: chapa plana asomada por delante del
    # sayo y dos regueros que bajan de ella. El frente es -y, que es por donde
    # ya corre la sangre del palo.
    gore.append(gm.box("chest_stain", (0.26, 0.030, 0.30), (0, -0.140, 2.06)))
    gore.append(gm.box("chest_run_l", (0.055, 0.028, 0.20), (-0.07, -0.140, 1.83)))
    gore.append(gm.box("chest_run_r", (0.045, 0.028, 0.14), (0.08, -0.140, 1.86)))

    # **Por la cabeza, no por el hombro.** Lo que se pidio el 21 es ver la
    # punta salir por la cara, y una punta asomando junto al cuello se leia
    # como un remache. El palo sigue por dentro del cuello y del craneo - un
    # tramo mas fino, que por fuera solo se ve donde el cuerpo no lo tapa - y
    # la punta sale por la coronilla, que es la unica salida que se lee a
    # cuarenta pixeles de lejos. Un reguero baja de la boca por la barbilla,
    # que es lo que dice que le atraveso la cara y no que lleva un sombrero.
    stake.append(gm.cyl("post_neck", 0.055, 0.42, (head_x * 0.5, 0.035, 2.40), sides=6))
    gore.append(gm.spike("crown_tip", 0.055, 0.30, (head_x, 0.035, 2.62), sides=6))
    gore.append(gm.box("mouth_run", (0.045, 0.026, 0.16), (head_x, -0.105, 2.42)))
    gore.append(gm.box("collar_stain", (0.17, 0.028, 0.08), (head_x * 0.5, -0.075, 2.36)))
    gore.append(gm.box("run", (0.07, 0.04, 1.30), (0, -0.06, 1.26)))
    for i, z in enumerate((1.02, 1.44, 1.86)):
        gore.append(gm.ball("drop_%d" % i, 0.05, (0, -0.08, z), 8, 5))

    return gm.join([gm.paint(stake, "wood_group", wood),
                    gm.paint(body, "flesh_group", flesh),
                    gm.paint(worn, "cloth_group", cloth),
                    gm.paint(gore, "blood_group", blood)],
                   "aldrich_impaled_stake"), (WOOD, FLESH, rags, BLOOD)


# --- 14. la pira colectiva (Gravedigger) ------------------------------------
#
# vanilla: pyre 2.520 x 0.900 x 1.863, pivote en el suelo, edificio de 3x2.

def mass_pyre():
    """Una pira para tres cuerpos, que es lo que el trabajo `burn_bodies_mass` pide.

    **Por que tiene edificio propio desde hoy.** La pira colectiva era solo una
    receta colgada del componente de produccion de la pira de vanilla: el
    trabajo existia y lo que el jugador veia era la hoguera de siempre, del
    tamano de siempre, quemando tres cuerpos. Una pila el doble de alta con los
    tres envueltos encima dice de un vistazo lo que hace, y el icono
    `aldrich_icon_mass_pyre` llevaba dibujado desde el dia 9 esperandola.

    Los troncos van cruzados en dos capas - a lo largo abajo, a lo ancho arriba -
    porque una pila de palos paralelos se lee como un suelo de tablas.
    """
    wood, linen, ember = mats(WOOD, LINEN, EMBER)

    logs, shrouds, fire = [], [], []

    for i, y in enumerate((-0.52, -0.17, 0.18, 0.53)):
        logs.append(gm.cyl("log_low_%d" % i, 0.10, 2.40, (0, y, 0.10),
                           rot=(0, 1.5708, 0), sides=8))
    for i, x in enumerate((-0.90, -0.30, 0.30, 0.90)):
        logs.append(gm.cyl("log_high_%d" % i, 0.09, 1.60, (x, 0, 0.29),
                           rot=(1.5708, 0, 0), sides=8))

    # Cuatro estacas en las esquinas: sujetan la pila y le dan silueta por
    # encima de los cuerpos, que si no quedan flotando sobre un monton.
    for sx in (-1, 1):
        for sy in (-1, 1):
            logs.append(gm.box("stake_%d_%d" % (sx, sy), (0.09, 0.09, 0.86),
                               (sx * 1.14, sy * 0.72, 0.43)))

    # Tres cuerpos envueltos, no tres cajas: la atadura de la cabeza y la de
    # los pies es lo unico que a esta escala dice que van amortajados.
    for i, y in enumerate((-0.46, 0.0, 0.46)):
        shrouds.append(gm.box("body_%d" % i, (1.72, 0.30, 0.24), (0, y, 0.52)))
        shrouds.append(gm.ball("head_%d" % i, 0.15, (-0.86, y, 0.54), 8, 6))
        for x in (-0.70, 0.74):
            shrouds.append(gm.cyl("tie_%d_%d" % (i, int(x * 100)), 0.17, 0.05,
                                  (x, y, 0.52), rot=(0, 1.5708, 0), sides=8))

    for i, (x, y, h) in enumerate(((-0.66, -0.30, 0.52), (0.10, 0.34, 0.66),
                                   (0.78, -0.18, 0.44))):
        fire.append(gm.spike("flame_%d" % i, 0.16, h, (x, y, 0.62), sides=6))
    fire.append(gm.cyl("embers", 0.90, 0.05, (0, 0, 0.02), sides=12))

    return gm.join([gm.paint(logs, "wood_group", wood),
                    gm.paint(shrouds, "linen_group", linen),
                    gm.paint(fire, "ember_group", ember)],
                   "aldrich_mass_pyre"), (WOOD, LINEN, EMBER)


# --- 15. las garras del alzado (UndeadHorde) --------------------------------
#
# vanilla: dagger 0.120 x 0.414 x 0.015, y es la referencia buena - `mace` mide
# 0.721 y las garras no son un arma larga.
#
# **Si tiene ranuras, y era lo que faltaba.** Se dio por hecho que un arma
# equipada no tiene donde nombrar un albedo y se le clavaron las UV al atlas de
# vanilla; lo que tiene un arma equipada es exactamente lo mismo que un edificio,
# una `variationLists` en su ficha de `Resources.json`. `EquipmentView.Setup`
# llama a `ChangeMeshAccordingToResourceQuality`, que lee
# `Resource.VariationsById["QualityVariations"]` y de ahi saca la ranura
# `baseMesh`; el dagger de vanilla dice ahi `"value": "dagger"`. Nuestra ficha
# no tenia ninguna lista, asi que nadie aplicaba nada y el prefab se quedaba con
# la malla que traia puesta - **la daga en la mano derecha** que se reporto.
#
# Con la lista escrita, la malla entra por su nombre y el albedo por la ranura
# `albedo` de la lista `MaterialParameters`, que `MeshVariationHandler.
# UpdateTextures` resuelve por `TextureRepository` - el mismo sitio donde
# `ModTextures` registra los PNG sueltos de cada mod. De ahi que las garras
# tengan paleta propia como cualquier otra pieza y que `CLAW_STEEL` ya no
# exista.


def undead_claws():
    """Cuatro unas largas y una tira sobre los nudillos, del tamano de un dagger.

    Del tamano de un dagger y no de una maza: el hueco que el prefab reserva en
    la mano es el mismo para todas las armas de una mano, y una garra de setenta
    centimetros sale del brazo.
    """
    steel, = mats(IRON)

    parts = []

    # La tira de nudillos: es lo que hace que las cuatro unas se lean como una
    # mano y no como cuatro palos sueltos.
    parts.append(gm.box("band", (0.105, 0.045, 0.030), (0, 0, 0.035)))

    for i, x in enumerate((-0.042, -0.014, 0.014, 0.042)):
        # La del medio mas larga, como en una mano, y todas inclinadas hacia
        # fuera: unas paralelas parecen un peine.
        length = (0.20, 0.25, 0.24, 0.18)[i]
        parts.append(gm.spike("nail_%d" % i, 0.013, length, (x, 0, 0.05),
                              rot=(0, x * 2.6, 0), sides=6))
        parts.append(gm.cyl("knuckle_%d" % i, 0.016, 0.030, (x, 0, 0.045),
                            sides=6))

    # El nombre del objeto es el nombre con el que la malla se publica en el
    # bundle, y para un arma equipada ese nombre **tiene** que ser el id del
    # recurso: el juego la busca asi. Devolver el grupo de pintura tal cual la
    # publicaba como `steel_group`, y una malla que no aparece por su nombre no
    # da error de ninguna clase - `MeshRepository.GetByID` devuelve null y el
    # arma sale invisible, sin excepcion y sin una linea en el log.
    return gm.join([gm.paint(parts, "steel_group", steel)], "undead_claws"), (IRON,)


# --- la tabla ---------------------------------------------------------------
#
# nombre -> (funcion, altura de la base, huella maxima, referencia, mod, uv,
#            baldosas)
#
# `uv` es None para todo lo que trae paleta propia, y una coordenada del
# atlas de vanilla para lo que no puede traerla - hoy ya nada: las garras
# dejaron de mirar al atlas cuando el recurso paso a nombrar su propio albedo.
#
# `baldosas` es el `size` del edificio en su JSON, (x, z), y decide donde cae
# la malla dentro de su huella: el juego planta la pieza sobre la **primera**
# baldosa, no en el centro del rectangulo. Ver `gm_model.ground`.
#
# La huella maxima es la del edificio en baldosas menos un pelo, porque la caja
# de seleccion sale del JSON y no de aqui. Las dos piezas de pared llevan su
# base donde la tiene la de vanilla: plantadas en el suelo apareceran a los pies
# del muro.

PIECES = {
    "aldrich_count_coffin": (count_coffin, 0.0, (2.95, 1.00),
                             "limestone_sarcophagus 2.600 x 0.829 x 1.000", "VampireCourt", None, (3, 1)),
    "aldrich_count_crypt": (count_crypt, 0.0, (2.95, 1.00),
                            "limestone_sarcophagus 2.600 x 0.829 x 1.000", "VampireCourt", None, (3, 1)),
    "aldrich_blood_altar": (blood_altar, 0.0, (2.95, 1.95),
                            "butchering_table 3.041 x 2.411 x 1.836", "VampireCourt", None, (3, 2)),
    "aldrich_blood_circle": (blood_ritual_circle, 0.0, (2.99, 2.99),
                             "pagan_ritual_circle 3.134 x 0.297 x 3.023", "VampireCourt", None, (3, 3)),
    "aldrich_court_banner": (court_banner, 0.0, (0.99, 0.99),
                             "banner_single_large 1.070 x 2.981 x 0.190", "VampireCourt", None, (1, 1)),
    "aldrich_court_banner_wall": (court_banner_wall, 0.577, (0.99, 0.99),
                                  "banner_wall_large 0.079 x 2.320 x 1.070, base +0.577", "VampireCourt", None, (1, 1)),
    "aldrich_crimson_candle": (crimson_candle, 0.0, (0.99, 0.99),
                               "iron_candle 0.837 x 2.089 x 0.837", "VampireCourt", None, (1, 1)),
    "aldrich_blood_brazier": (blood_brazier, 0.0, (0.99, 0.99),
                              "iron_brazear 0.913 x 0.914 x 0.913", "VampireCourt", None, (1, 1)),
    "aldrich_veiled_mirror": (veiled_mirror, 1.122, (0.99, 0.99),
                              "silver_mirror_wall 0.102 x 1.498 x 0.928, base +1.122", "VampireCourt", None, (1, 1)),
    "aldrich_court_reliquary": (court_reliquary, 0.0, (0.99, 0.99),
                                "relic_shelf 0.671 x 1.965 x 0.702", "VampireCourt", None, (1, 1)),
    "aldrich_blood_well": (blood_well, 0.0, (2.95, 1.60),
                           "wooden_well 2.850 x 2.401 x 1.506", "VampireCourt", None, (3, 1)),
    "aldrich_vigil_table": (vigil_table, 0.0, (4.95, 1.95),
                            "table_2x5_stone 4.849 x 0.824 x 1.804", "VampireCourt", None, (5, 2)),
    # Tres mallas del mismo empalado: rapado, barbado y melenudo. La lista de
    # variaciones del JSON las reparte con `isRandom`, asi que dos estacas
    # seguidas ya no son el mismo hombre dos veces.
    "aldrich_impaled_stake": (lambda: impaled_stake(0), 0.0, (1.80, 0.99),
                              "scarecrow 2.235 x 2.942 x 0.704", "VampireCourt", None, (1, 1)),
    "aldrich_impaled_stake_02": (lambda: impaled_stake(1), 0.0, (1.80, 0.99),
                                 "scarecrow 2.235 x 2.942 x 0.704", "VampireCourt", None, (1, 1)),
    "aldrich_impaled_stake_03": (lambda: impaled_stake(2), 0.0, (1.80, 0.99),
                                 "scarecrow 2.235 x 2.942 x 0.704", "VampireCourt", None, (1, 1)),
    "aldrich_mass_pyre": (mass_pyre, 0.0, (2.95, 1.95),
                          "pyre 2.520 x 0.900 x 1.863", "Gravedigger", None, (3, 2)),

    # Sin prefijo `aldrich_`: la direccion de la malla **tiene** que ser el id
    # del recurso, que es como el juego la busca para un arma equipada.
    "undead_claws": (undead_claws, 0.0, (0.40, 0.40),
                     "dagger 0.120 x 0.414 x 0.015", "UndeadHorde", None, (1, 1)),
}


def main():
    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    models = os.path.join(here, "ASSESTS", "Modelos")
    mods = os.path.join(os.path.expanduser("~"), "Documents", "Foxy Voxel",
                        "Going Medieval", "Mods")

    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    # `--render` no es una pieza: saca ademas un PNG de cada una en
    # ASSESTS/Vistas, que es la unica forma de ver la pieza sin cargar partida.
    want_render = "--render" in args
    wanted = [a for a in args if not a.startswith("--")]

    names = [n for n in PIECES
             if not wanted or n in wanted
             or n.replace("aldrich_", "") in wanted]

    if not names:
        sys.exit("ninguna pieza coincide con " + " ".join(wanted))

    for name in sorted(names):
        build, base_y, limit, reference, mod, uv, tiles = PIECES[name]

        # Una escena limpia por pieza: lo de la anterior seguiria en el fichero
        # y bastaria un descuido en la seleccion para exportar dos cosas juntas.
        gm.clear()

        piece, colours = build()
        gm.finish(piece, base_y=base_y, uvs=None if uv is None else [uv],
                  tiles=tiles)

        fbx, _ = gm.export(piece, models, name)
        # La textura va al mod que es dueno de la pieza: MeshRepository es
        # global pero los catalogos no, y una malla enviada por un mod con la
        # textura en otro sale gris para quien instale solo uno.
        albedo = None
        if colours is not None:
            albedo = gm.bake_albedo(name + "_albedo", colours,
                                    os.path.join(mods, mod, "Data", "Textures",
                                                 name + "_albedo.png"))

        # El render va detras del horneado de la paleta a proposito: lee ese
        # mismo PNG por las mismas UV, asi que lo que sale es lo que el juego
        # dibuja, paleta mal ordenada incluida.
        preview = None
        if want_render:
            preview = gm.render(piece, os.path.join(here, "ASSESTS", "Vistas",
                                                    name + ".png"),
                                albedo=albedo, base_y=base_y)

        gm.report(name, piece, reference=reference, fbx=fbx, albedo=albedo,
                  limit=limit, preview=preview)


main()
