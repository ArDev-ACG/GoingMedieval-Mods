"""Takes the icons out of the mods so they can be looked at one by one, and puts
replacements back in.

The style question - "un icono de edificio de vanilla es un render pintado del
objeto en tres cuartos" - is parked, and what was asked for instead is a folder:
the icons pulled out where they can be opened individually, and somewhere to drop
hand-made ones so they end up in the mods without anybody hunting through four
Data/Sprites folders.

    python tools/icons/collect_icons.py             # out: mods -> ASSESTS/Iconos
    python tools/icons/collect_icons.py --install    # in:  ASSESTS/Iconos/nuevos -> mods

Out puts three things under ASSESTS/Iconos/:

    actuales/<mod>/<nombre>.png     the icon exactly as the game loads it
    actuales/vista/<nombre>.png     the same icon at 4x, nearest-neighbour, on a
                                    chequerboard - a 128 px sprite is unreadable
                                    at 100% on a big screen, and the transparency
                                    is half of what there is to judge
    _hoja_contacto.png              all of them on one labelled sheet

In takes anything in nuevos/ - at the top level or in a per-mod subfolder - and
copies it over the icon of the same name, whichever mod that name belongs to. A
file whose name is not one of ours is reported and left alone, because a typo in
a filename is a sprite that silently never loads: ModInstance.LoadSprites()
registers each file under its own name, so the name *is* the iconPath.
"""

import os
import shutil
import sys

from PIL import Image, ImageDraw

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                   "ASSESTS", "Iconos")
PREFIX = "aldrich_"

ZOOM = 4
CHECKER = 8
LIGHT = (96, 96, 96)
DARK = (72, 72, 72)


def mods():
    """Every mod folder that has sprites, in a fixed order so the sheet is stable."""
    return sorted(d for d in os.listdir(MODS)
                  if os.path.isdir(os.path.join(MODS, d, "Data", "Sprites")))


def sprites():
    """(mod, filename, full path) for every icon of ours, mod by mod."""
    for mod in mods():
        folder = os.path.join(MODS, mod, "Data", "Sprites")
        for name in sorted(os.listdir(folder)):
            if name.startswith(PREFIX) and name.lower().endswith(".png"):
                yield mod, name, os.path.join(folder, name)


def chequerboard(size):
    """The grey chequer the transparency is judged against."""
    board = Image.new("RGB", size, LIGHT)
    d = ImageDraw.Draw(board)
    for y in range(0, size[1], CHECKER):
        for x in range(0, size[0], CHECKER):
            if (x // CHECKER + y // CHECKER) % 2:
                d.rectangle([x, y, x + CHECKER - 1, y + CHECKER - 1], fill=DARK)
    return board


def zoomed(path):
    """One icon at ZOOM times its size, nearest-neighbour, over the chequer.

    NEAREST and not the default: a mod icon is pixel work at 128 px, and a
    smooth resample turns it into a judgement of the resampler.
    """
    icon = Image.open(path).convert("RGBA")
    big = icon.resize((icon.width * ZOOM, icon.height * ZOOM), Image.NEAREST)

    plate = chequerboard(big.size).convert("RGBA")
    plate.alpha_composite(big)
    return plate


def sheet(rows):
    """All the icons on one labelled sheet, four to a row."""
    if not rows:
        return None

    cols = 4
    cell = 176
    label = 26
    lines = (len(rows) + cols - 1) // cols

    page = Image.new("RGBA", (cols * cell, lines * (cell + label)), (28, 26, 26, 255))
    d = ImageDraw.Draw(page)

    for i, (mod, name, path) in enumerate(rows):
        x = (i % cols) * cell
        y = (i // cols) * (cell + label)

        icon = Image.open(path).convert("RGBA")
        fit = min(cell - 16, max(icon.size) * ZOOM)
        icon = icon.resize((fit, fit), Image.NEAREST)

        plate = chequerboard(icon.size).convert("RGBA")
        plate.alpha_composite(icon)
        page.alpha_composite(plate, (x + (cell - fit) // 2, y + (cell - fit) // 2))

        # Two lines, because one was the mod name running into the next
        # cell's icon name. The name is the thing being judged; the mod is
        # context, and it is already the folder these came out of.
        short = name[len(PREFIX):-len(".png")]
        d.text((x + 6, y + cell + 2), short, fill=(226, 222, 216, 255))
        d.text((x + 6, y + cell + 13), mod, fill=(150, 146, 142, 255))

    return page


def take_out():
    rows = list(sprites())
    if not rows:
        print("no icons found under " + MODS)
        return 1

    view = os.path.join(OUT, "actuales", "vista")
    os.makedirs(view, exist_ok=True)

    for mod, name, path in rows:
        for folder in (os.path.join(OUT, "actuales", mod),
                       os.path.join(OUT, "nuevos", mod)):
            os.makedirs(folder, exist_ok=True)

        shutil.copyfile(path, os.path.join(OUT, "actuales", mod, name))
        zoomed(path).save(os.path.join(view, name))

    page = sheet(rows)
    if page is not None:
        page.save(os.path.join(OUT, "_hoja_contacto.png"))

    readme(rows)

    print(f"{len(rows)} icon(s) -> {os.path.normpath(os.path.join(OUT, 'actuales'))}")
    print("    vista/      the same at 4x on a chequer, for looking at")
    print("    ../nuevos/  drop replacements here, same filename")
    print("    _hoja_contacto.png")
    return 0


def readme(rows):
    """A note in the folder, because a folder of PNGs does not explain itself."""
    byname = {}
    for mod, name, _ in rows:
        byname.setdefault(mod, []).append(name)

    lines = [
        "# Iconos",
        "",
        "`actuales/` es lo que el juego carga hoy, tal cual. `actuales/vista/` es lo",
        "mismo a 4x sobre un damero para poder mirarlo; esa carpeta es solo para ver,",
        "de ahi no se instala nada.",
        "",
        "Para cambiar uno: deja el PNG en `nuevos/` - suelto o dentro de la carpeta de",
        "su mod, da igual - **con el mismo nombre de fichero** y corre:",
        "",
        "    python tools/icons/collect_icons.py --install",
        "",
        "El nombre es lo unico que importa: `ModInstance.LoadSprites()` registra cada",
        "fichero bajo su propio nombre, asi que el nombre *es* el `iconPath` que dice",
        "el JSON. Un nombre mal escrito no da error: el sprite simplemente no aparece.",
        "",
        "Tamano: los iconos de vanilla son de 128 px y opacos. Cualquier tamano carga,",
        "pero el juego lo escala a la casilla de la UI.",
        "",
        "## Lo que hay",
        "",
    ]
    for mod in sorted(byname):
        lines.append(f"**{mod}**")
        lines.append("")
        for name in byname[mod]:
            lines.append(f"- `{name}`")
        lines.append("")

    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "README.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


def put_back():
    """Copies everything in nuevos/ over the icon of the same name."""
    known = {name: os.path.join(MODS, mod, "Data", "Sprites", name)
             for mod, name, _ in sprites()}

    source = os.path.join(OUT, "nuevos")
    if not os.path.isdir(source):
        print("nothing to install: " + os.path.normpath(source) + " does not exist")
        return 1

    done = 0
    unknown = []
    for root, _, files in os.walk(source):
        for name in files:
            if not name.lower().endswith(".png"):
                continue

            target = known.get(name)
            if target is None:
                unknown.append(name)
                continue

            shutil.copyfile(os.path.join(root, name), target)
            print(f"  {name} -> {os.path.relpath(target, MODS)}")
            done += 1

    for name in sorted(set(unknown)):
        print(f"  ! {name} is not the name of any icon of ours - left alone")

    print(f"{done} icon(s) installed")
    return 0 if done or not unknown else 1


if __name__ == "__main__":
    sys.exit(put_back() if "--install" in sys.argv[1:] else take_out())
