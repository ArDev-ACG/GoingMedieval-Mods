"""Draws the five Preview.png the mod list shows, 640x360 each.

The first four shipped Foxy Voxel's placeholder - byte for byte the same file -
which is the one thing a mod page cannot go out with. These are built from the
icons the mods already ship, so the picture and the game agree with each other
and stay in step when an icon is redrawn.

    python tools/icons/build_previews.py            # write into the installed mods
    python tools/icons/build_previews.py <outdir>   # dry run into one folder
"""

import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gm_style as S

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
SIZE = (640, 360)

# Cada mod tiene su color, el mismo que ya usan sus iconos.
FIELDS = {
    "VampireCourt": ((78, 20, 24), (10, 4, 6)),
    "Gravedigger": ((66, 56, 40), (8, 7, 5)),
    "CarrionAndPlague": ((54, 70, 44), (6, 9, 6)),
    "UndeadHorde": ((52, 60, 52), (6, 8, 7)),
    "XenomorphRunner": ((40, 58, 60), (4, 7, 8)),
}

# Cada mod se compone a mano con lo que ese mod trae, y solo con eso: una
# vista previa que ensena el icono de otro mod es una promesa que no cumple.
# Orden de dibujado, de atras hacia delante: (sprite, alto, centro, espejo).
LAYOUTS = {
    "VampireCourt": [
        ("aldrich_perk_vampire", 132, (150, 208), False),
        ("aldrich_perk_ghoul", 132, (492, 208), False),
        ("aldrich_role_count", 226, (320, 172), False),
    ],
    "Gravedigger": [
        ("aldrich_perk_gravedigger", 132, (150, 208), False),
        ("aldrich_icon_mass_grave", 150, (496, 214), False),
        ("aldrich_role_gravedigger", 226, (320, 172), False),
    ],
    "CarrionAndPlague": [
        ("aldrich_bubble_plague_immune", 140, (470, 196), False),
        ("aldrich_icon_cat_statue", 232, (238, 184), False),
    ],
    # Undead Horde solo trae un sprite, asi que la horda son tres veces la
    # misma mano a distinto tamano y una del reves: leidas juntas son varias
    # garras saliendo de la oscuridad, no un icono repetido.
    "UndeadHorde": [
        ("aldrich_icon_undead_claws", 116, (498, 236), True),
        ("aldrich_icon_undead_claws", 146, (150, 216), False),
        ("aldrich_icon_undead_claws", 232, (322, 168), True),
    ],
    "XenomorphRunner": [
        ("aldrich_icon_xeno_egg", 132, (150, 208), False),
        ("aldrich_icon_facehugger", 132, (492, 208), False),
        ("aldrich_icon_xeno_runner", 226, (320, 172), False),
    ],
}


def backdrop(field):
    """Radial field stretched to 16:9, with the grain the icons already carry."""
    square = S.radial_field(max(SIZE), *field)
    w, h = SIZE
    top = (square.height - h * square.width // w) // 2
    return S.paint_pass(square.resize((w, square.height * w // square.width),
                                      Image.LANCZOS).crop((0, top, w, top + h)),
                        grain=6.0, light=0.10)


def sprite(mod, name):
    path = os.path.join(MODS, mod, "Data", "Sprites", name + ".png")
    return Image.open(path).convert("RGBA") if os.path.exists(path) else None


def drop_shadow(img, blur=9, offset=(6, 8), opacity=150):
    shadow = Image.new("RGBA", (img.width + blur * 4, img.height + blur * 4), (0, 0, 0, 0))
    mask = img.split()[3].point(lambda a: min(a, opacity))
    shadow.paste((0, 0, 0, 255), (blur * 2 + offset[0], blur * 2 + offset[1]), mask)
    return shadow.filter(ImageFilter.GaussianBlur(blur))


def place(canvas, img, height, centre):
    if img is None:
        return
    scaled = img.resize((max(int(img.width * height / img.height), 1), height), Image.LANCZOS)
    x = centre[0] - scaled.width // 2
    y = centre[1] - scaled.height // 2
    sh = drop_shadow(scaled)
    canvas.alpha_composite(sh, (x - sh.width // 2 + scaled.width // 2,
                                y - sh.height // 2 + scaled.height // 2))
    canvas.alpha_composite(scaled, (x, y))


def vignette(canvas):
    w, h = canvas.size
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    fade = np.clip(1 - (np.clip(r, 0.55, 1.35) - 0.55) / 0.8 * 0.55, 0, 1)
    arr = np.array(canvas).astype(np.float32)
    arr[..., :3] *= fade[..., None]
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def build(mod):
    canvas = backdrop(FIELDS[mod]).convert("RGBA")

    for name, height, centre, mirror in LAYOUTS[mod]:
        img = sprite(mod, name)
        if img is None:
            print(f"  ! {mod}: falta {name}.png")
            continue
        if mirror:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        place(canvas, img, height, centre)

    return vignette(canvas).convert("RGB")


def main():
    dry = sys.argv[1] if len(sys.argv) > 1 else None
    for mod in LAYOUTS:
        img = build(mod)
        out = os.path.join(dry, mod) if dry else os.path.join(MODS, mod)
        os.makedirs(out, exist_ok=True)
        path = os.path.join(out, "Preview.png")
        img.save(path)
        print(f"{mod:18} Preview.png {img.size[0]}x{img.size[1]}")
    print(f"\n{len(LAYOUTS)} previews -> {dry or MODS}")


if __name__ == "__main__":
    main()
