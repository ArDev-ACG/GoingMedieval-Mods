"""Corta las hojas de iconos generadas a mano en los PNG que el juego carga.

Lo que llega a `ASSESTS/Iconos/nuevos/<mod>/` no son iconos: son laminas JPEG
con cinco, seis o doce dibujos sobre un fondo liso. Esto las parte, les quita el
fondo, les pone alfa y las guarda **con el nombre exacto** del icono que
sustituyen - que es lo unico que importa, porque `ModInstance.LoadSprites()`
registra cada fichero bajo su propio nombre y un nombre mal escrito es un sprite
que nunca carga y no da error -. Despues:

    python tools/icons/collect_icons.py --install

Dos formas de cortar, una por lamina:

  * `components`: se inunda el fondo desde los bordes y lo que queda suelto es
    un icono. Vale para las laminas sueltas - pegatinas sobre verde, gris o
    blanco -. Un escudo verde sobre fondo verde no se pierde porque el fondo se
    toma **conectado al borde**, no por color.
  * `grid`: filas por columnas. Es lo que hay que usar cuando la lamina lleva
    marco o rotulos debajo de cada dibujo, porque entonces todo esta pegado a
    todo y la inundacion se lo lleva de una pieza.

El tamano de salida sale del icono al que sustituye (`actuales/`): las burbujas
de pensamiento son 86x96 y los demas 128x128, y eso no se inventa aqui.
"""

import os

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ICONS = os.path.join(HERE, "..", "..", "ASSESTS", "Iconos")
NEW = os.path.join(ICONS, "nuevos")
CURRENT = os.path.join(ICONS, "actuales")

# None salta esa casilla: en las laminas hay dibujos de mas - variantes, un
# hueco vacio, un cuaderno con un movil dentro - y sobra con no usarlos.
SHEETS = [
    dict(
        mod="CarrionAndPlague",
        file="Gemini_Generated_Image_2v00bj2v00bj2v00.jpg",
        mode="components",
        how="olive",
        names=[
            "aldrich_bubble_plague_bite",
            "aldrich_bubble_plague_immune",
            "aldrich_icon_cat_statue",
            "aldrich_icon_plague_hood",
            "aldrich_icon_plague_mask",
        ],
    ),
    dict(
        mod="Gravedigger",
        file="Gemini_Generated_Image_wn5vfzwn5vfzwn5v.jpg",
        mode="components",
        names=[
            "aldrich_bubble_grave_duty",
            "aldrich_bubble_grave_stripped",
            "aldrich_icon_mass_grave",
            "aldrich_icon_mass_pyre",
            "aldrich_perk_gravedigger",
            "aldrich_role_gravedigger",
        ],
    ),
    dict(
        mod="UndeadHorde",
        file="Gemini_Generated_Image_fznvyrfznvyrfznv.jpg",
        mode="components",
        names=[
            "aldrich_icon_undead_claws",
            "aldrich_perk_risen",
        ],
    ),
    dict(
        mod="VampireCourt",
        file="d8c90428-25db-4315-8258-6604051a314f.jpg",
        mode="grid",
        rows=4,
        cols=3,
        names=[
            "aldrich_bubble_bite_mark", "aldrich_bubble_bite_missed", "aldrich_bubble_blood_drained",
            "aldrich_bubble_blood_feast", "aldrich_bubble_count_duty", "aldrich_bubble_count_stripped",
            "aldrich_perk_ghoul", "aldrich_perk_vampire", None,
            "aldrich_role_count", None, None,
        ],
    ),
    dict(
        mod="VampireCourt",
        file="Gemini_Generated_Image_3ckg8o3ckg8o3ckg.jpg",
        mode="grid",
        rows=4,
        cols=3,
        # Marco por fuera y un rotulo bajo cada dibujo: se recorta el marco y se
        # tira el tercio de abajo de cada casilla, que es donde va el texto.
        margin=0.085,
        label=0.30,
        head=0.10,
        names=[
            None, "aldrich_icon_blood_well", "aldrich_icon_blood_altar",
            "aldrich_icon_blood_brazier", "aldrich_icon_blood_draught", None,
            "aldrich_icon_count_throne", "aldrich_icon_court_banner", None,
            None, "aldrich_icon_court_reliquary", None,
        ],
    ),
    dict(
        mod="VampireCourt",
        file="Gemini_Generated_Image_m8wmigm8wmigm8wm.jpg",
        mode="components",
        how="olive",
        names=[
            "aldrich_icon_crimson_candle",
            "aldrich_icon_impaled_stake",
            None,
            "aldrich_icon_vigil_table",
        ],
    ),
]


def size_of(mod, name):
    """El tamano del icono al que sustituye, y 128 si es uno que no teniamos."""
    path = os.path.join(CURRENT, mod, name + ".png")
    if os.path.exists(path):
        return Image.open(path).size
    return (128, 128)


def background(rgb, edge_limit=14.0):
    """
    El fondo es lo llano que se toca con el borde.

    <b>Por color no vale.</b> La primera version comparaba cada pixel con el
    color del borde, y de las seis laminas fallaron tres: el verde de los
    fondos no es un verde sino un degradado con brillo en el centro, asi que a
    medio camino el pixel ya no se parecia al del borde y la mancha de fondo se
    partia - "1 dibujos y 5 nombres" -. Lo que separa un fondo de un dibujo no
    es el color, es que el fondo **no tiene bordes**: se inunda desde el marco
    por donde el gradiente es plano y la linea de contorno de cada dibujo para
    la inundacion. Un degradado suave se cruza; una silueta, no.
    """
    grey = np.asarray(Image.fromarray(rgb).convert("L").filter(
        __import__("PIL.ImageFilter", fromlist=["ImageFilter"]).GaussianBlur(1.2)), float)

    slope = np.hypot(ndimage.sobel(grey, 0), ndimage.sobel(grey, 1))
    flat = slope < edge_limit

    labels, count = ndimage.label(flat)
    if count == 0:
        return np.zeros(grey.shape, bool)

    touching = set(labels[0]) | set(labels[-1]) | set(labels[:, 0]) | set(labels[:, -1])
    touching.discard(0)

    back = np.isin(labels, list(touching))

    # Lo que queda dentro de una silueta es dibujo aunque sea llano.
    return back & ~ndimage.binary_fill_holes(~back)


def cut(rgb, keep, box=None):
    """Un dibujo recortado a su caja, con el fondo en transparente."""
    if box is not None:
        y0, y1, x0, x1 = box
        rgb = rgb[y0:y1, x0:x1]
        keep = keep[y0:y1, x0:x1]

    ys, xs = np.nonzero(keep)
    if len(ys) == 0:
        return None

    top, bottom = ys.min(), ys.max() + 1
    left, right = xs.min(), xs.max() + 1

    piece = np.zeros((bottom - top, right - left, 4), np.uint8)
    piece[..., :3] = rgb[top:bottom, left:right]
    piece[..., 3] = np.where(keep[top:bottom, left:right], 255, 0)

    return Image.fromarray(piece, "RGBA")


def fit(image, size):
    """Dentro de la caja del icono, centrado y sin deformar."""
    w, h = size
    scale = min(w / image.width, h / image.height)
    small = image.resize((max(1, round(image.width * scale)),
                          max(1, round(image.height * scale))), Image.LANCZOS)

    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(small, ((w - small.width) // 2, (h - small.height) // 2), small)
    return out


def olive(rgb, window=(34, 86)):
    """
    El fondo de celosia isometrica, por tono.

    Dos laminas - las que van sobre el tablero verde oliva del juego - traen
    pintadas las lineas de la celosia, y una linea es un borde: la inundacion
    del gradiente se para en ella y devuelve la lamina entera como una sola
    mancha, pasara el umbral que pasara. Con esas dos se va por tono: el oliva
    del tablero ocupa una ventana estrecha de matiz que ningun dibujo pisa - el
    escudo verde de la peste es esmeralda, que cae bastante mas alla -.
    """
    hsv = np.asarray(Image.fromarray(rgb).convert("HSV")).astype(int)
    hue, sat, val = hsv[..., 0], hsv[..., 1], hsv[..., 2]

    back = (hue >= window[0]) & (hue <= window[1]) & (sat > 25) & (val > 25)
    back = ndimage.binary_closing(back, np.ones((7, 7), bool))
    back = ndimage.binary_opening(back, np.ones((5, 5), bool))

    # Y aqui no se rellenan huecos: la mascara es de color, no de contorno, y
    # rellenar los huecos de lo que no es fondo se llevaba la lamina entera.
    return back


def pieces_at(rgb, edge_limit, how=None):
    """Las manchas sueltas con ese umbral, de mayor a menor."""
    keep = ~(olive(rgb) if how == "olive" else background(rgb, edge_limit))
    keep = ndimage.binary_closing(keep, np.ones((5, 5), bool))

    labels, count = ndimage.label(keep, np.ones((3, 3), bool))
    found = []
    for index in range(1, count + 1):
        mask = labels == index
        if mask.sum() < rgb.shape[0] * rgb.shape[1] * 0.004:
            continue
        ys, xs = np.nonzero(mask)
        found.append((ys.min(), ys.max() + 1, xs.min(), xs.max() + 1, mask))

    return found


def components(rgb, names, how=None):
    """
    Cada mancha suelta es un icono, en orden de lectura.

    El umbral no es uno: dos de las seis laminas llevan una celosia isometrica
    pintada en el fondo, y esas lineas son bordes que paran la inundacion y
    devuelven la lamina entera como una sola mancha. Se prueban varios umbrales
    y se queda el primero que da tantas manchas como nombres hay, que es la
    unica comprobacion honesta que se puede hacer aqui.
    """
    found = []
    for limit in (14.0, 20.0, 26.0, 34.0, 44.0, 56.0):
        found = pieces_at(rgb, limit, how)
        if len(found) == len(names) or how == "olive":
            break

    # Orden de lectura: por bandas de fila, y dentro de la banda por x. Una
    # banda es media altura de dibujo, que es lo que separa dos filas de lo que
    # solo esta un poco mas alto que su vecino.
    if found:
        band = max(1, int(np.median([f[1] - f[0] for f in found]) * 0.6))
        found.sort(key=lambda f: (f[0] // band, f[2]))

    if len(found) != len(names):
        print(f"    ojo: {len(found)} dibujos y {len(names)} nombres")

    for piece, name in zip(found, names):
        y0, y1, x0, x1, mask = piece
        yield name, cut(rgb, mask, (y0, y1, x0, x1))


def grid(rgb, names, rows, cols, margin=0.0, label=0.0, head=0.0):
    """Filas por columnas, y de cada casilla se quita el fondo por separado."""
    h, w, _ = rgb.shape
    top, left = int(h * margin), int(w * margin)
    inner_h, inner_w = h - 2 * top, w - 2 * left

    for index, name in enumerate(names):
        if name is None:
            continue

        row, col = divmod(index, cols)
        y0 = top + row * inner_h // rows
        y1 = top + (row + 1) * inner_h // rows
        x0 = left + col * inner_w // cols
        x1 = left + (col + 1) * inner_w // cols

        # `label` tira el rotulo de la casilla y `head` el de la de arriba: el
        # texto de una lamina rotulada no vive dentro de la casilla que nombra,
        # se cuela por el borde de abajo, y por eso salian trozos de "Blood
        # Mana Potion" encima del estandarte.
        tall = y1 - y0
        y1 -= int(tall * label)
        y0 += int(tall * head)

        cell = rgb[y0:y1, x0:x1]
        keep = ~background(cell)
        keep = ndimage.binary_closing(keep, np.ones((5, 5), bool))

        labels, count = ndimage.label(keep, np.ones((3, 3), bool))
        if count > 1:
            # La casilla puede traer una sombra o un trozo de rotulo: se queda
            # la mancha mas grande, que es el dibujo.
            sizes = ndimage.sum(keep, labels, range(1, count + 1))
            keep = labels == (int(np.argmax(sizes)) + 1)

        yield name, cut(cell, keep)


def main():
    for sheet in SHEETS:
        path = os.path.join(NEW, sheet["mod"], sheet["file"])
        print(sheet["mod"], "/", sheet["file"])
        if not os.path.exists(path):
            print("    no esta")
            continue

        rgb = np.asarray(Image.open(path).convert("RGB"))

        if sheet["mode"] == "components":
            pieces = components(rgb, sheet["names"], sheet.get("how"))
        else:
            pieces = grid(rgb, sheet["names"], sheet["rows"], sheet["cols"],
                          sheet.get("margin", 0.0), sheet.get("label", 0.0),
                          sheet.get("head", 0.0))

        for name, image in pieces:
            if name is None or image is None:
                continue
            out = os.path.join(NEW, sheet["mod"], name + ".png")
            fit(image, size_of(sheet["mod"], name)).save(out)
            print("    ", name, Image.open(out).size)


if __name__ == "__main__":
    main()
