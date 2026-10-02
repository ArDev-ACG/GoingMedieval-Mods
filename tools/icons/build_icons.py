"""Draws the mod icons and drops them into each mod's Data/Sprites folder.

ModInstance.LoadSprites() reads <Mod>/Data/Sprites/*.png and registers each file
under its own name, so "aldrich_perk_vampire.png" is what an iconPath or a
bubbleIcon field has to say. Sprite keys are global across mods, hence the prefix.

    python tools/icons/build_icons.py            # write into the installed mods
    python tools/icons/build_icons.py <outdir>   # dry run into one folder
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gm_style as S

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
PREFIX = "aldrich_"

# --- palettes ---------------------------------------------------------------
EARTH = ((74, 62, 44), (10, 8, 6))
CRIMSON = ((122, 22, 22), (14, 4, 4))
SICK = ((62, 78, 48), (8, 10, 7))
GRAVE = ((52, 58, 44), (7, 8, 6))    # colder and greyer than SICK, which is the Ghoul's
BONE = (222, 214, 190)
STEEL = (176, 180, 186)
WOOD = (126, 84, 48)
BLOOD = (138, 16, 22)
STONE = (150, 146, 136)


def pts(n, *xy):
    return [(x * n, y * n) for x, y in xy]


# --- perk motifs (drawn on the medallion's radial field) --------------------
def _spade(d, n, cx, cy, s):
    """Shovel: wooden shaft, T-grip, steel blade."""
    d.polygon(pts(n, (cx - 0.035 * s, cy - 0.30 * s), (cx + 0.035 * s, cy - 0.30 * s),
                  (cx + 0.045 * s, cy + 0.14 * s), (cx - 0.045 * s, cy + 0.14 * s)),
              fill=WOOD + (255,))
    d.polygon(pts(n, (cx - 0.035 * s, cy - 0.30 * s), (cx - 0.005 * s, cy - 0.30 * s),
                  (cx + 0.005 * s, cy + 0.14 * s), (cx - 0.035 * s, cy + 0.14 * s)),
              fill=(168, 118, 70, 255))
    d.rounded_rectangle([n * (cx - 0.10 * s), n * (cy - 0.38 * s),
                         n * (cx + 0.10 * s), n * (cy - 0.28 * s)],
                        radius=n * 0.03 * s, fill=WOOD + (255,))
    d.polygon(pts(n, (cx - 0.115 * s, cy + 0.12 * s), (cx + 0.115 * s, cy + 0.12 * s),
                  (cx + 0.095 * s, cy + 0.34 * s), (cx, cy + 0.42 * s),
                  (cx - 0.095 * s, cy + 0.34 * s)), fill=STEEL + (255,))
    d.polygon(pts(n, (cx - 0.115 * s, cy + 0.12 * s), (cx - 0.02 * s, cy + 0.12 * s),
                  (cx - 0.02 * s, cy + 0.40 * s), (cx - 0.095 * s, cy + 0.34 * s)),
              fill=(214, 218, 224, 255))


def _skull(d, n, cx, cy, r, tint=BONE):
    d.ellipse([n * (cx - r), n * (cy - r), n * (cx + r), n * (cy + r * 0.85)], fill=tint + (255,))
    d.polygon(pts(n, (cx - r * 0.55, cy + r * 0.45), (cx + r * 0.55, cy + r * 0.45),
                  (cx + r * 0.40, cy + r * 1.05), (cx - r * 0.40, cy + r * 1.05)),
              fill=tint + (255,))
    for sgn in (-1, 1):
        d.ellipse([n * (cx + sgn * r * 0.44 - r * 0.30), n * (cy - r * 0.30),
                   n * (cx + sgn * r * 0.44 + r * 0.30), n * (cy + r * 0.24)],
                  fill=(18, 12, 10, 255))
    d.polygon(pts(n, (cx, cy + r * 0.20), (cx + r * 0.14, cy + r * 0.52),
                  (cx - r * 0.14, cy + r * 0.52)), fill=(18, 12, 10, 255))


def motif_gravedigger(d, img, n):
    _spade(d, n, 0.60, 0.44, 1.0)
    _skull(d, n, 0.36, 0.60, 0.20)


def motif_vampire(d, img, n):
    """Two fangs over a falling drop - reads as a mouth even at 64px."""
    d.rounded_rectangle([n * 0.20, n * 0.20, n * 0.80, n * 0.40], radius=n * 0.09,
                        fill=(58, 12, 14, 255))
    d.rounded_rectangle([n * 0.22, n * 0.22, n * 0.78, n * 0.34], radius=n * 0.07,
                        fill=(96, 22, 24, 255))
    for cx in (0.36, 0.64):
        d.polygon(pts(n, (cx - 0.075, 0.34), (cx + 0.075, 0.34), (cx, 0.62)),
                  fill=(246, 242, 228, 255))
        d.polygon(pts(n, (cx - 0.075, 0.34), (cx - 0.015, 0.34), (cx - 0.012, 0.56)),
                  fill=(255, 255, 250, 255))
    d.ellipse([n * 0.585, n * 0.66, n * 0.695, n * 0.80], fill=BLOOD + (255,))
    d.polygon(pts(n, (0.64, 0.60), (0.695, 0.74), (0.585, 0.74)), fill=BLOOD + (255,))
    d.ellipse([n * 0.605, n * 0.685, n * 0.635, n * 0.725], fill=(214, 96, 96, 255))


def _capsule(d, n, p0, p1, r, fill):
    """Rounded bar between two normalised points - one finger."""
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0
    ln = max((dx * dx + dy * dy) ** 0.5, 1e-6)
    ox, oy = -dy / ln * r, dx / ln * r
    d.polygon(pts(n, (x0 + ox, y0 + oy), (x1 + ox, y1 + oy),
                  (x1 - ox, y1 - oy), (x0 - ox, y0 - oy)), fill=fill)
    for (cx, cy) in (p0, p1):
        d.ellipse([n * (cx - r), n * (cy - r), n * (cx + r), n * (cy + r)], fill=fill)


def motif_ghoul(d, img, n):
    """Gaunt face: skin pulled tight over the skull, hollow sockets, stained mouth.

    The Vampire medallion already owns the fangs, so the Ghoul gets the other
    half of the story - the one who was fed on. Read at 64px it is a pale wedge
    with two black holes in it, which is all the silhouette a perk icon needs.
    """
    skin = (196, 200, 168)
    lit = (234, 236, 208)
    shade = (108, 114, 86)
    deep = (58, 62, 44)
    hollow = (12, 15, 10)
    stain = (74, 20, 22)

    outer = ((0.29, 0.30), (0.355, 0.165), (0.50, 0.12), (0.645, 0.165),
             (0.71, 0.30), (0.715, 0.45), (0.665, 0.585), (0.60, 0.725),
             (0.50, 0.855), (0.40, 0.725), (0.335, 0.585), (0.285, 0.45))
    d.polygon(pts(n, *outer), fill=shade + (255,))
    inner = ((0.315, 0.31), (0.375, 0.19), (0.50, 0.15), (0.625, 0.19),
             (0.685, 0.31), (0.69, 0.45), (0.645, 0.575), (0.585, 0.705),
             (0.50, 0.815), (0.415, 0.705), (0.355, 0.575), (0.31, 0.45))
    d.polygon(pts(n, *inner), fill=skin + (255,))

    # key light down the left temple and cheekbone
    d.polygon(pts(n, (0.318, 0.32), (0.40, 0.205), (0.435, 0.35),
                  (0.415, 0.60), (0.352, 0.47)), fill=lit + (255,))

    # brow ridge, with the sockets cut in underneath it
    d.polygon(pts(n, (0.325, 0.335), (0.675, 0.335), (0.66, 0.40), (0.34, 0.40)),
              fill=deep + (255,))
    for sgn in (-1, 1):
        cx = 0.50 + sgn * 0.115
        d.ellipse([n * (cx - 0.10), n * 0.365, n * (cx + 0.10), n * 0.515],
                  fill=deep + (255,))
        d.ellipse([n * (cx - 0.082), n * 0.385, n * (cx + 0.082), n * 0.500],
                  fill=hollow + (255,))
        d.ellipse([n * (cx - 0.028), n * 0.455, n * (cx + 0.006), n * 0.492],
                  fill=(74, 80, 58, 255))             # floor of the socket, barely lit

    # sunken cheeks squeezing the face to a wedge
    for sgn in (-1, 1):
        cx = 0.50 + sgn * 0.145
        d.polygon(pts(n, (cx - sgn * 0.02, 0.53), (cx + sgn * 0.055, 0.545),
                      (cx + sgn * 0.02, 0.70), (cx - sgn * 0.045, 0.645)),
                  fill=shade + (255,))

    # nose hollow, then the mouth gash and what is dried under it
    d.polygon(pts(n, (0.50, 0.475), (0.545, 0.615), (0.455, 0.615)),
              fill=deep + (255,))
    d.polygon(pts(n, (0.50, 0.535), (0.527, 0.612), (0.473, 0.612)),
              fill=hollow + (255,))
    d.polygon(pts(n, (0.395, 0.655), (0.605, 0.655), (0.575, 0.715), (0.425, 0.715)),
              fill=hollow + (255,))
    d.polygon(pts(n, (0.452, 0.712), (0.548, 0.712), (0.524, 0.772), (0.476, 0.772)),
              fill=stain + (255,))


COUNT_PURPLE = ((70, 40, 92), (10, 5, 14))


def motif_thrall(d, img, n):
    """Close on a neck under the jaw, with the Count's two punctures running.
    The Vampire owns the fangs and the Ghoul the face, so the Thrall gets the
    wound; the Count's purple field says whose teeth made it."""
    skin, lit, jaw = (212, 194, 168), (238, 226, 204), (120, 100, 84)
    d.polygon(pts(n, (0.10, 0.30), (0.36, 0.40), (0.62, 0.38), (0.90, 0.26),
                  (0.90, 0.92), (0.10, 0.92)), fill=skin + (255,))
    d.polygon(pts(n, (0.10, 0.30), (0.36, 0.40), (0.30, 0.92), (0.10, 0.92)), fill=lit + (255,))
    d.polygon(pts(n, (0.10, 0.30), (0.36, 0.40), (0.62, 0.38), (0.90, 0.26),
                  (0.90, 0.34), (0.62, 0.46), (0.36, 0.48), (0.10, 0.38)), fill=jaw + (255,))
    for cx, cy, drip in ((0.42, 0.58, 0.84), (0.60, 0.56, 0.78)):
        d.ellipse([n * (cx - 0.055), n * (cy - 0.055), n * (cx + 0.055), n * (cy + 0.055)],
                  fill=(58, 6, 10, 255))
        _capsule(d, n, (cx, cy + 0.03), (cx + 0.004, drip), 0.022, BLOOD + (255,))
        d.ellipse([n * (cx - 0.035), n * (drip - 0.015), n * (cx + 0.043), n * (drip + 0.06)],
                  fill=BLOOD + (255,))
        d.ellipse([n * (cx - 0.02), n * (cy - 0.035), n * (cx + 0.0), n * (cy - 0.015)],
                  fill=(150, 40, 46, 255))


def art_blood_circle(d, img, n):
    """The ring seen from the build menu's angle: black stone, red channel.

    Drawn as flattened ellipses rather than circles because every building icon
    in this game is looked at from the same three-quarter view, and a true
    circle would sit in the tile like a coin dropped on it.
    """
    stone = (46, 43, 41)
    stone_lit = (86, 81, 76)
    channel = (104, 12, 16)
    ember = (176, 32, 30)

    # the slab, then the ring cut into it
    d.ellipse([n * 0.09, n * 0.26, n * 0.91, n * 0.80], fill=stone + (255,))
    d.ellipse([n * 0.09, n * 0.23, n * 0.91, n * 0.77], fill=stone_lit + (255,))
    d.ellipse([n * 0.13, n * 0.26, n * 0.87, n * 0.745], fill=stone + (255,))

    # the channel, filled
    d.ellipse([n * 0.19, n * 0.315, n * 0.81, n * 0.695], fill=channel + (255,))
    d.ellipse([n * 0.245, n * 0.355, n * 0.755, n * 0.655], fill=stone + (255,))

    # four marks on the compass points, and the red pane set at the north one
    for cx, cy, w, h in ((0.50, 0.285, 0.055, 0.030), (0.50, 0.715, 0.045, 0.026),
                         (0.175, 0.505, 0.026, 0.045), (0.825, 0.505, 0.026, 0.045)):
        d.ellipse([n * (cx - w), n * (cy - h), n * (cx + w), n * (cy + h)],
                  fill=channel + (255,))
    d.polygon(pts(n, (0.50, 0.205), (0.545, 0.265), (0.50, 0.325), (0.455, 0.265)),
              fill=ember + (255,))
    d.polygon(pts(n, (0.50, 0.225), (0.527, 0.265), (0.50, 0.305), (0.473, 0.265)),
              fill=(230, 92, 74, 255))

    # a drop leaving the rim, so the channel reads as wet rather than painted
    d.ellipse([n * 0.735, n * 0.735, n * 0.795, n * 0.805], fill=channel + (255,))


def motif_risen(d, img, n):
    """A hand coming up out of the soil.

    The Risen perk was borrowing the Ghoul medallion, and the two things it has
    to be told apart from are a face (Ghoul) and a mouth (Vampire). A hand
    breaking the ground is neither, and it is the oldest shorthand there is for
    what this perk means.

    Everything above the mound is painted in the same dead green-grey the
    walkers are tinted with in play, so the icon and the thing it names are
    recognisably the same creature.
    """
    skin = (131, 143, 118)
    lit = (170, 181, 152)
    edge = (48, 55, 40)          # the gap that keeps five fingers from fusing into a mitten
    nail = (38, 32, 26)
    soil = (72, 59, 41)
    soil_lit = (108, 90, 61)

    # forearm and palm, each laid down fat in the edge colour first
    _capsule(d, n, (0.50, 0.82), (0.50, 0.52), 0.098, edge + (255,))
    _capsule(d, n, (0.50, 0.82), (0.50, 0.52), 0.082, skin + (255,))
    d.ellipse([n * 0.368, n * 0.392, n * 0.632, n * 0.622], fill=edge + (255,))
    d.ellipse([n * 0.385, n * 0.410, n * 0.615, n * 0.605], fill=skin + (255,))
    d.ellipse([n * 0.402, n * 0.428, n * 0.512, n * 0.570], fill=lit + (255,))

    # four fingers splayed wide, thumb across the front, each tipped with a nail
    fingers = ((0.250, 0.255), (0.392, 0.120), (0.575, 0.122), (0.712, 0.268))
    for i, (tx, ty) in enumerate(fingers):
        bx = 0.418 + i * 0.055
        _capsule(d, n, (bx, 0.505), (tx, ty), 0.058, edge + (255,))
    for i, (tx, ty) in enumerate(fingers):
        bx = 0.418 + i * 0.055
        _capsule(d, n, (bx, 0.505), (tx, ty), 0.043, skin + (255,))
        d.ellipse([n * (tx - 0.040), n * (ty - 0.040), n * (tx + 0.040), n * (ty + 0.040)],
                  fill=nail + (255,))
    _capsule(d, n, (0.455, 0.585), (0.700, 0.478), 0.062, edge + (255,))
    _capsule(d, n, (0.455, 0.585), (0.700, 0.478), 0.047, skin + (255,))
    d.ellipse([n * 0.664, n * 0.443, n * 0.740, n * 0.517], fill=nail + (255,))

    # the mound it is pushing through, drawn last so it buries the wrist
    d.polygon(pts(n, (0.00, 1.04), (0.08, 0.84), (0.27, 0.745), (0.50, 0.718),
                  (0.73, 0.750), (0.92, 0.845), (1.00, 1.04)), fill=soil + (255,))
    d.polygon(pts(n, (0.12, 0.875), (0.29, 0.780), (0.50, 0.758), (0.43, 0.812),
                  (0.24, 0.858)), fill=soil_lit + (255,))
    for cx, cy, r in ((0.235, 0.925, 0.048), (0.715, 0.890, 0.040), (0.520, 0.960, 0.032)):
        d.ellipse([n * (cx - r), n * (cy - r), n * (cx + r), n * (cy + r)],
                  fill=soil_lit + (255,))


def emblem_spade(d, w, h):
    d.rounded_rectangle([w * 0.42, h * 0.16, w * 0.58, h * 0.60], radius=w * 0.03, fill=255)
    d.rounded_rectangle([w * 0.30, h * 0.11, w * 0.70, h * 0.21], radius=w * 0.05, fill=255)
    d.polygon([(w * 0.24, h * 0.55), (w * 0.76, h * 0.55), (w * 0.68, h * 0.80),
               (w * 0.50, h * 0.89), (w * 0.32, h * 0.80)], fill=255)
    d.ellipse([w * 0.44, h * 0.13, w * 0.56, h * 0.19], fill=0)     # grip hole


def emblem_crown(d, w, h):
    d.polygon([(w * 0.22, h * 0.62), (w * 0.78, h * 0.62), (w * 0.72, h * 0.28),
               (w * 0.61, h * 0.46), (w * 0.50, h * 0.22), (w * 0.39, h * 0.46),
               (w * 0.28, h * 0.28)], fill=255)
    d.rounded_rectangle([w * 0.20, h * 0.60, w * 0.80, h * 0.74], radius=w * 0.04, fill=255)
    for cx, cy in [(0.28, 0.28), (0.50, 0.22), (0.72, 0.28)]:
        d.ellipse([w * (cx - 0.055), h * (cy - 0.035), w * (cx + 0.055), h * (cy + 0.045)], fill=255)
    d.polygon([(w * 0.50, h * 0.78), (w * 0.575, h * 0.90), (w * 0.425, h * 0.90)], fill=255)
    d.ellipse([w * 0.425, h * 0.855, w * 0.575, h * 0.95], fill=255)   # blood drop below


# --- objects ----------------------------------------------------------------
def art_mass_grave(d, img, n):
    """Open pit with spoil heap and two lashed grave markers."""
    d.polygon(pts(n, (0.5, 0.44), (0.92, 0.66), (0.5, 0.88), (0.08, 0.66)), fill=(74, 56, 38, 255))
    d.polygon(pts(n, (0.5, 0.50), (0.82, 0.665), (0.5, 0.83), (0.18, 0.665)), fill=(30, 21, 14, 255))
    d.polygon(pts(n, (0.5, 0.50), (0.82, 0.665), (0.5, 0.70), (0.18, 0.665)), fill=(12, 8, 6, 255))
    d.polygon(pts(n, (0.5, 0.70), (0.82, 0.665), (0.5, 0.83), (0.18, 0.665)),
              fill=(46, 33, 22, 255))                      # lit far wall of the pit
    d.ellipse([n * 0.62, n * 0.40, n * 0.98, n * 0.60], fill=(94, 72, 48, 255))
    d.ellipse([n * 0.70, n * 0.42, n * 0.90, n * 0.52], fill=(122, 95, 64, 255))
    for cx, top, lean in [(0.26, 0.14, 0.02), (0.44, 0.22, -0.015)]:
        d.polygon(pts(n, (cx - 0.022, top), (cx + 0.022, top),
                      (cx + 0.022 + lean, top + 0.42), (cx - 0.022 + lean, top + 0.42)),
                  fill=WOOD + (255,))
        d.polygon(pts(n, (cx - 0.105, top + 0.09), (cx + 0.105, top + 0.09),
                      (cx + 0.105, top + 0.135), (cx - 0.105, top + 0.135)),
                  fill=(150, 104, 60, 255))
        d.polygon(pts(n, (cx - 0.022, top), (cx - 0.004, top),
                      (cx - 0.004 + lean, top + 0.42), (cx - 0.022 + lean, top + 0.42)),
                  fill=(168, 118, 70, 255))


def art_mass_pyre(d, img, n):
    """Stacked logs under flame - the mass cremation recipe."""
    d.polygon(pts(n, (0.50, 0.05), (0.58, 0.22), (0.66, 0.16), (0.70, 0.34),
                  (0.76, 0.30), (0.72, 0.58), (0.28, 0.58), (0.24, 0.30),
                  (0.30, 0.34), (0.34, 0.16), (0.42, 0.22)), fill=(226, 118, 24, 255))
    d.polygon(pts(n, (0.50, 0.19), (0.57, 0.33), (0.62, 0.28), (0.63, 0.56),
                  (0.37, 0.56), (0.38, 0.28), (0.43, 0.33)), fill=(248, 198, 62, 255))
    d.polygon(pts(n, (0.50, 0.34), (0.56, 0.46), (0.55, 0.55), (0.45, 0.55),
                  (0.44, 0.46)), fill=(255, 244, 196, 255))
    for i, (y, wdt) in enumerate([(0.78, 0.36), (0.70, 0.32), (0.62, 0.27)]):
        d.rounded_rectangle([n * (0.5 - wdt), n * y, n * (0.5 + wdt), n * (y + 0.10)],
                            radius=n * 0.05,
                            fill=(112, 74, 42, 255) if i % 2 else (92, 60, 34, 255))
        d.ellipse([n * (0.5 - wdt), n * y, n * (0.5 - wdt + 0.095), n * (y + 0.10)],
                  fill=(150, 104, 62, 255))
        d.ellipse([n * (0.5 - wdt + 0.022), n * (y + 0.022),
                   n * (0.5 - wdt + 0.073), n * (y + 0.078)], fill=(104, 70, 40, 255))


def art_cat_statue(d, img, n):
    """Carved cat standing on a plinth, drawn to match the in-game mesh.

    The building now uses `cat_stuffed_trophy` - a standing cat on its own slab
    - repainted as limestone, so the icon copies that pose instead of the
    seated cat the old icon showed.
    """
    slab_top = (104, 101, 93)
    slab_l = (64, 62, 57)
    slab_r = (80, 78, 71)
    stone = (176, 172, 160)
    stone_lit = (206, 202, 190)
    stone_dk = (134, 131, 121)

    d.polygon(pts(n, (0.50, 0.615), (0.88, 0.735), (0.50, 0.855), (0.12, 0.735)),
              fill=slab_top + (255,))
    d.polygon(pts(n, (0.12, 0.735), (0.50, 0.855), (0.50, 0.925), (0.12, 0.805)),
              fill=slab_l + (255,))
    d.polygon(pts(n, (0.88, 0.735), (0.50, 0.855), (0.50, 0.925), (0.88, 0.805)),
              fill=slab_r + (255,))

    # tail sweeping out behind the back legs
    d.polygon(pts(n, (0.330, 0.545), (0.185, 0.395), (0.135, 0.435),
                  (0.285, 0.600)), fill=stone_dk + (255,))
    d.ellipse([n * 0.118, n * 0.372, n * 0.202, n * 0.456], fill=stone_dk + (255,))

    for cx, top in ((0.285, 0.545), (0.345, 0.560), (0.585, 0.545), (0.645, 0.560)):
        d.rounded_rectangle([n * (cx - 0.040), n * top, n * (cx + 0.040), n * 0.700],
                            radius=n * 0.028,
                            fill=(stone_dk if cx in (0.345, 0.645) else stone) + (255,))
        d.ellipse([n * (cx - 0.048), n * 0.665, n * (cx + 0.048), n * 0.715],
                  fill=stone + (255,))

    d.rounded_rectangle([n * 0.255, n * 0.400, n * 0.695, n * 0.595],
                        radius=n * 0.085, fill=stone + (255,))
    d.polygon(pts(n, (0.275, 0.415), (0.660, 0.415), (0.640, 0.470), (0.290, 0.470)),
              fill=stone_lit + (255,))          # light along the spine
    d.polygon(pts(n, (0.290, 0.560), (0.660, 0.560), (0.640, 0.600), (0.310, 0.600)),
              fill=stone_dk + (255,))           # belly in shade

    # head, turned to face the camera on top of a short neck
    d.rounded_rectangle([n * 0.610, n * 0.360, n * 0.700, n * 0.470],
                        radius=n * 0.030, fill=stone_dk + (255,))
    d.polygon(pts(n, (0.575, 0.290), (0.615, 0.185), (0.665, 0.300)),
              fill=stone + (255,))              # left ear
    d.polygon(pts(n, (0.755, 0.290), (0.720, 0.185), (0.670, 0.300)),
              fill=stone + (255,))              # right ear
    d.polygon(pts(n, (0.592, 0.280), (0.617, 0.213), (0.641, 0.288)),
              fill=stone_dk + (255,))
    d.polygon(pts(n, (0.742, 0.280), (0.719, 0.213), (0.695, 0.288)),
              fill=stone_dk + (255,))
    d.ellipse([n * 0.578, n * 0.245, n * 0.752, n * 0.415], fill=stone + (255,))
    d.ellipse([n * 0.592, n * 0.258, n * 0.672, n * 0.395], fill=stone_lit + (255,))

    for sgn in (-1, 1):                          # eyes and nose cut into the stone
        cx = 0.665 + sgn * 0.040
        d.ellipse([n * (cx - 0.020), n * 0.300, n * (cx + 0.020), n * 0.335],
                  fill=stone_dk + (255,))
    d.polygon(pts(n, (0.665, 0.345), (0.687, 0.373), (0.643, 0.373)),
              fill=stone_dk + (255,))
    d.line(pts(n, (0.665, 0.373), (0.665, 0.392)), fill=stone_dk + (255,),
           width=max(int(n * 0.008), 1))

def art_blood_draught(d, img, n):
    """Corked flask of blood: the brew recipe and the resource pile share it."""
    d.polygon(pts(n, (0.40, 0.30), (0.60, 0.30), (0.585, 0.46), (0.415, 0.46)),
              fill=(178, 196, 186, 190))
    d.ellipse([n * 0.24, n * 0.40, n * 0.76, n * 0.90], fill=(178, 196, 186, 170))
    d.ellipse([n * 0.27, n * 0.48, n * 0.73, n * 0.87], fill=BLOOD + (255,))
    d.polygon(pts(n, (0.415, 0.42), (0.585, 0.42), (0.60, 0.52), (0.40, 0.52)), fill=BLOOD + (255,))
    d.ellipse([n * 0.29, n * 0.50, n * 0.44, n * 0.66], fill=(190, 44, 44, 140))
    d.rounded_rectangle([n * 0.385, n * 0.17, n * 0.615, n * 0.32], radius=n * 0.03,
                        fill=(148, 106, 62, 255))
    d.rounded_rectangle([n * 0.385, n * 0.17, n * 0.46, n * 0.32], radius=n * 0.03,
                        fill=(180, 134, 84, 255))
    d.line(pts(n, (0.32, 0.52), (0.30, 0.70)), fill=(240, 248, 244, 150), width=int(n * 0.022))


def art_plague_mask(d, img, n):
    """The beaked mask, three-quarters on, so the beak reads as a beak.

    Straight on it is a triangle and nothing else - the shape only says
    "plague doctor" from the side, which is why the whole face is turned.
    """
    leather = (74, 56, 44)
    leather_lit = (104, 80, 62)
    leather_dark = (46, 34, 27)
    glass = (206, 214, 206)

    # the skull of it: a rounded hood shape covering the whole head
    d.ellipse([n * 0.24, n * 0.16, n * 0.78, n * 0.74], fill=leather + (255,))
    d.ellipse([n * 0.30, n * 0.20, n * 0.62, n * 0.54], fill=leather_lit + (255,))

    # the beak, down and forward off the front of the face
    d.polygon(pts(n, (0.30, 0.46), (0.30, 0.62), (0.10, 0.78)),
              fill=leather + (255,))
    d.polygon(pts(n, (0.30, 0.46), (0.20, 0.62), (0.10, 0.78)),
              fill=leather_lit + (255,))
    d.line(pts(n, (0.30, 0.54), (0.10, 0.78)), fill=leather_dark + (255,),
           width=max(1, int(n * 0.012)))

    # the one glass eye the angle shows, and the stitching over the crown
    d.ellipse([n * 0.36, n * 0.36, n * 0.50, n * 0.50], fill=leather_dark + (255,))
    d.ellipse([n * 0.38, n * 0.38, n * 0.48, n * 0.48], fill=glass + (255,))
    d.arc([n * 0.28, n * 0.18, n * 0.74, n * 0.64], 190, 330,
          fill=leather_dark + (255,), width=max(1, int(n * 0.014)))

    # the strap across the back of the head
    d.polygon(pts(n, (0.62, 0.50), (0.80, 0.44), (0.82, 0.54), (0.64, 0.60)),
              fill=leather_dark + (255,))


def art_plague_hood(d, img, n):
    """The plague coat: waxed, collar to ankle, belted, nobody inside it.

    It was a hood until the 21st, and a hood is the mask's slot - you could
    wear one or the other. Now it is the body garment, so the icon is the
    coat on its own: high collar, the long skirt split at the front, sleeves
    down past the hands, and the belt that makes it a coat and not a bell.
    """
    wax = (52, 44, 38)
    wax_lit = (82, 70, 58)
    wax_dark = (30, 25, 22)
    belt = (96, 70, 44)
    brass = (178, 150, 92)
    w = max(1, int(n * 0.014))

    # sleeves first, so the body sits over their roots
    d.polygon(pts(n, (0.32, 0.22), (0.20, 0.30), (0.12, 0.66), (0.22, 0.68), (0.30, 0.40)),
              fill=wax + (255,))
    d.polygon(pts(n, (0.68, 0.22), (0.80, 0.30), (0.88, 0.66), (0.78, 0.68), (0.70, 0.40)),
              fill=wax_dark + (255,))

    # the body of the coat, flaring to the hem
    d.polygon(pts(n, (0.32, 0.20), (0.68, 0.20), (0.74, 0.52), (0.84, 0.94), (0.16, 0.94), (0.26, 0.52)),
              fill=wax + (255,))
    d.polygon(pts(n, (0.32, 0.20), (0.50, 0.20), (0.50, 0.94), (0.16, 0.94), (0.26, 0.52)),
              fill=wax_lit + (255,))

    # the front split of the skirt, and two long folds
    d.polygon(pts(n, (0.50, 0.58), (0.56, 0.94), (0.44, 0.94)), fill=wax_dark + (255,))
    for x0, x1 in ((0.38, 0.28), (0.62, 0.72)):
        d.line(pts(n, (x0, 0.58), (x1, 0.92)), fill=wax_dark + (255,), width=w)

    # the high collar, standing up round an empty neck
    d.polygon(pts(n, (0.36, 0.08), (0.64, 0.08), (0.68, 0.22), (0.32, 0.22)), fill=wax_dark + (255,))
    d.polygon(pts(n, (0.40, 0.10), (0.60, 0.10), (0.58, 0.20), (0.42, 0.20)), fill=(16, 14, 13, 255))

    # buttons down the placket, and the belt
    for y in (0.28, 0.36, 0.44):
        d.ellipse([n * 0.485, n * y, n * 0.515, n * (y + 0.03)], fill=brass + (255,))
    d.rectangle([n * 0.26, n * 0.50, n * 0.74, n * 0.555], fill=belt + (255,))
    d.rectangle([n * 0.46, n * 0.495, n * 0.54, n * 0.56], outline=brass + (255,), width=w)


def art_undead_claws(d, img, n):
    """Three yellowed nails on a grey hand: the walkers' only weapon."""
    flesh = (150, 158, 142)
    flesh_lit = (176, 184, 166)
    nail = (206, 198, 160)
    nail_dark = (150, 140, 104)

    # palm, tilted so the icon reads on the diagonal like the other weapons
    d.polygon(pts(n, (0.30, 0.66), (0.52, 0.54), (0.66, 0.72), (0.44, 0.86)),
              fill=flesh + (255,))
    d.polygon(pts(n, (0.30, 0.66), (0.41, 0.60), (0.53, 0.78), (0.44, 0.86)),
              fill=flesh_lit + (255,))

    # three fingers with a claw on the end of each
    for base, tip in (((0.36, 0.60), (0.20, 0.26)),
                      ((0.47, 0.55), (0.44, 0.16)),
                      ((0.58, 0.60), (0.70, 0.24))):
        bx, by = base
        tx, ty = tip
        mx, my = (bx + tx) / 2, (by + ty) / 2
        d.line(pts(n, (bx, by), (mx, my)), fill=flesh + (255,), width=int(n * 0.075))
        d.polygon(pts(n, (mx - 0.045, my + 0.02), (mx + 0.045, my - 0.02),
                      (tx, ty)), fill=nail + (255,))
        d.polygon(pts(n, (mx + 0.045, my - 0.02), (mx + 0.012, my - 0.005),
                      (tx, ty)), fill=nail_dark + (255,))

    # a little blood left on the middle claw
    d.polygon(pts(n, (0.44, 0.16), (0.47, 0.24), (0.42, 0.24)), fill=BLOOD + (255,))


def art_blood_altar(d, img, n):
    """Stone slab with a channel cut down it and a bowl where the channel ends."""
    stone = (172, 168, 156)
    lit = (204, 200, 188)
    dark = (118, 115, 106)
    groove = (78, 74, 68)

    for cx in (0.30, 0.70):                      # legs, back pair first
        d.polygon(pts(n, (cx - 0.045, 0.60), (cx + 0.045, 0.60),
                      (cx + 0.045, 0.84), (cx - 0.045, 0.84)), fill=dark + (255,))
    d.polygon(pts(n, (0.50, 0.34), (0.90, 0.50), (0.50, 0.66), (0.10, 0.50)),
              fill=stone + (255,))               # slab top
    d.polygon(pts(n, (0.10, 0.50), (0.50, 0.66), (0.50, 0.74), (0.10, 0.58)),
              fill=dark + (255,))
    d.polygon(pts(n, (0.90, 0.50), (0.50, 0.66), (0.50, 0.74), (0.90, 0.58)),
              fill=(142, 139, 129, 255))
    d.polygon(pts(n, (0.50, 0.34), (0.72, 0.42), (0.50, 0.50), (0.28, 0.42)),
              fill=lit + (255,))                 # light catching the far half

    d.polygon(pts(n, (0.50, 0.375), (0.585, 0.410), (0.50, 0.645), (0.415, 0.610)),
              fill=groove + (255,))              # the channel down the slab
    d.polygon(pts(n, (0.50, 0.400), (0.553, 0.422), (0.50, 0.622), (0.447, 0.600)),
              fill=BLOOD + (255,))

    d.ellipse([n * 0.385, n * 0.735, n * 0.615, n * 0.855], fill=dark + (255,))
    d.ellipse([n * 0.405, n * 0.750, n * 0.595, n * 0.840], fill=(96, 92, 84, 255))
    d.ellipse([n * 0.420, n * 0.762, n * 0.580, n * 0.828], fill=BLOOD + (255,))
    d.ellipse([n * 0.445, n * 0.775, n * 0.505, n * 0.800], fill=(190, 44, 44, 255))


# --- effector bubble glyphs -------------------------------------------------
def glyph_spade(d, w, h):
    d.rounded_rectangle([w * 0.44, h * 0.14, w * 0.56, h * 0.48], radius=w * 0.03, fill=255)
    d.rounded_rectangle([w * 0.34, h * 0.10, w * 0.66, h * 0.18], radius=w * 0.04, fill=255)
    d.polygon([(w * 0.28, h * 0.44), (w * 0.72, h * 0.44), (w * 0.64, h * 0.64),
               (w * 0.50, h * 0.72), (w * 0.36, h * 0.64)], fill=255)


def glyph_spade_broken(d, w, h):
    d.rounded_rectangle([w * 0.44, h * 0.10, w * 0.56, h * 0.32], radius=w * 0.03, fill=255)
    d.rounded_rectangle([w * 0.34, h * 0.07, w * 0.66, h * 0.15], radius=w * 0.04, fill=255)
    d.polygon([(w * 0.30, h * 0.46), (w * 0.70, h * 0.46), (w * 0.62, h * 0.66),
               (w * 0.50, h * 0.73), (w * 0.38, h * 0.66)], fill=255)
    d.polygon([(w * 0.47, h * 0.32), (w * 0.57, h * 0.32), (w * 0.53, h * 0.42),
               (w * 0.43, h * 0.42)], fill=255)            # snapped, offset shaft


def glyph_crown(d, w, h):
    """Deep valleys and a heavy band, or it reads as a row of hills."""
    d.polygon([(w * 0.22, h * 0.60), (w * 0.78, h * 0.60), (w * 0.74, h * 0.16),
               (w * 0.62, h * 0.44), (w * 0.50, h * 0.12), (w * 0.38, h * 0.44),
               (w * 0.26, h * 0.16)], fill=255)
    d.rounded_rectangle([w * 0.20, h * 0.58, w * 0.80, h * 0.78], radius=w * 0.05, fill=255)
    d.rectangle([w * 0.20, h * 0.645, w * 0.80, h * 0.695], fill=0)   # engraved band line
    for cx, cy in [(0.26, 0.16), (0.50, 0.12), (0.74, 0.16)]:
        d.ellipse([w * (cx - 0.065), h * (cy - 0.05), w * (cx + 0.065), h * (cy + 0.07)], fill=255)


def glyph_crown_broken(d, w, h):
    """Same crown, split down the middle, the right half tipping off."""
    d.polygon([(w * 0.22, h * 0.60), (w * 0.48, h * 0.60), (w * 0.44, h * 0.44),
               (w * 0.36, h * 0.20), (w * 0.26, h * 0.16)], fill=255)
    d.rounded_rectangle([w * 0.20, h * 0.58, w * 0.48, h * 0.78], radius=w * 0.05, fill=255)
    d.ellipse([w * 0.195, h * 0.11, w * 0.325, h * 0.23], fill=255)
    d.polygon([(w * 0.56, h * 0.62), (w * 0.82, h * 0.54), (w * 0.74, h * 0.24),
               (w * 0.66, h * 0.46)], fill=255)
    d.polygon([(w * 0.54, h * 0.60), (w * 0.84, h * 0.52), (w * 0.88, h * 0.70),
               (w * 0.58, h * 0.78)], fill=255)
    d.ellipse([w * 0.685, h * 0.19, w * 0.805, h * 0.30], fill=255)


def glyph_goblet(d, w, h):
    """Wide bowl, thick foot: a cup, not a funnel."""
    d.ellipse([w * 0.26, h * 0.24, w * 0.74, h * 0.38], fill=255)
    d.polygon([(w * 0.26, h * 0.31), (w * 0.74, h * 0.31), (w * 0.60, h * 0.58),
               (w * 0.40, h * 0.58)], fill=255)
    d.rectangle([w * 0.445, h * 0.55, w * 0.555, h * 0.70], fill=255)
    d.rounded_rectangle([w * 0.30, h * 0.68, w * 0.70, h * 0.80], radius=w * 0.04, fill=255)
    d.ellipse([w * 0.44, h * 0.06, w * 0.56, h * 0.20], fill=255)     # drop falling in


def glyph_ward(d, w, h):
    """Small shield with a cross cut out: took the fever and walked away."""
    d.polygon([(w * 0.26, h * 0.16), (w * 0.74, h * 0.16), (w * 0.74, h * 0.48),
               (w * 0.50, h * 0.76), (w * 0.26, h * 0.48)], fill=255)
    d.rectangle([w * 0.455, h * 0.24, w * 0.545, h * 0.62], fill=0)
    d.rectangle([w * 0.33, h * 0.34, w * 0.67, h * 0.42], fill=0)


def glyph_drained(d, w, h):
    """Tipped goblet with the last drop leaving it: someone else drank it."""
    d.polygon([(w * 0.22, h * 0.30), (w * 0.66, h * 0.20), (w * 0.60, h * 0.46),
               (w * 0.34, h * 0.52)], fill=255)
    d.rectangle([w * 0.40, h * 0.48, w * 0.50, h * 0.66], fill=255)
    d.rounded_rectangle([w * 0.28, h * 0.64, w * 0.62, h * 0.76], radius=w * 0.04, fill=255)
    d.ellipse([w * 0.68, h * 0.52, w * 0.80, h * 0.68], fill=255)     # the drop, falling away
    d.ellipse([w * 0.74, h * 0.72, w * 0.82, h * 0.82], fill=255)


def _fangs(d, w, h, cx, cy, s, fill=255):
    """Gum bar and two fangs, sized around a centre.

    Shared by both bite bubbles so that a bite that landed and a bite that
    missed are visibly the same event, and `fill` is a parameter because the
    struck-through version has to cut a gap in itself before it draws.
    """
    d.rounded_rectangle([w * (cx - 0.24 * s), h * (cy - 0.075 * s),
                         w * (cx + 0.24 * s), h * (cy + 0.055 * s)],
                        radius=w * 0.055 * s, fill=fill)
    for sgn in (-1, 1):
        fx = cx + sgn * 0.115 * s
        d.polygon([(w * (fx - 0.082 * s), h * (cy + 0.01 * s)),
                   (w * (fx + 0.082 * s), h * (cy + 0.01 * s)),
                   (w * fx, h * (cy + 0.30 * s))], fill=fill)


def glyph_bite_mark(d, w, h):
    """Fangs with a drop falling from them: something drank here.

    Goes over an animal, which is the one victim the game cannot show this for
    on its own - a goat has no mood, so no effector bubble ever appears over it.
    """
    _fangs(d, w, h, 0.50, 0.27, 1.0)
    d.polygon([(w * 0.50, h * 0.62), (w * 0.575, h * 0.755), (w * 0.425, h * 0.755)], fill=255)
    d.ellipse([w * 0.425, h * 0.695, w * 0.575, h * 0.835], fill=255)


def glyph_bite_missed(d, w, h):
    """The same fangs inside a struck-through ring: the victim got away.

    Every shape here is white on the same field, so anything that touches
    anything else fuses into one blob - which is what the first attempt was.
    Each layer is therefore laid down twice: once fat and cut out, to open a gap
    around it, then again at its real weight. The negative sign reads first, the
    fangs say what was refused.
    """
    d.ellipse([w * 0.07, h * 0.03, w * 0.93, h * 0.80], fill=255)
    d.ellipse([w * 0.155, h * 0.105, w * 0.845, h * 0.725], fill=0)

    # fangs, with a gap burnt around them so they stand off the ring
    _fangs(d, w, h, 0.50, 0.285, 1.32, fill=0)
    _fangs(d, w, h, 0.50, 0.285, 1.22, fill=255)

    bar = [(0.245, 0.170), (0.300, 0.115), (0.760, 0.580), (0.705, 0.635)]
    fat = [(0.205, 0.185), (0.315, 0.075), (0.800, 0.565), (0.690, 0.675)]
    d.polygon([(w * x, h * y) for x, y in fat], fill=0)
    d.polygon([(w * x, h * y) for x, y in bar], fill=255)


def art_impaled_stake(d, img, n):
    """A body on a pole, read from the silhouette in and nothing else.

    At 128 pixels on a build-menu tile there is no room for a face, and there
    should not be one: what has to arrive is the shape - vertical line, weight
    hanging off it, head down - and the one red note where the two meet. Any
    more detail than that and it stops reading as an icon and starts reading as
    a picture of something unpleasant.
    """
    flesh = (128, 134, 112, 255)
    flesh_lit = (152, 156, 134, 255)
    blood = (104, 14, 16, 255)
    edge = (44, 46, 36, 255)

    def limb(points, fill, grow=0.0):
        """One shape, optionally swollen, so it can be drawn as its own edge.

        Everything here is pale against a pale olive tile, and the first two
        attempts came out as one flat robe: without a dark line between them,
        head, chest and arms are the same colour touching. Growing each shape
        about its own centre and laying the dark copy down first is the cheapest
        outline there is, and it keeps the shapes readable at 128 pixels.
        """
        cx = sum(x for x, _ in points) / len(points)
        cy = sum(y for _, y in points) / len(points)
        grown = [(cx + (x - cx) * (1 + grow), cy + (y - cy) * (1 + grow)) for x, y in points]
        d.polygon(pts(n, *grown), fill=fill)

    # The stake first, so the body sits in front of it and the pole still shows
    # above the head and below the feet - which is the whole read. It leans,
    # because nothing driven into the ground in a hurry is plumb.
    d.polygon(pts(n, (0.455, 0.020), (0.530, 0.020), (0.560, 0.980), (0.485, 0.980)),
              fill=(64, 44, 26, 255))
    d.polygon(pts(n, (0.455, 0.020), (0.482, 0.020), (0.512, 0.980), (0.485, 0.980)),
              fill=(112, 80, 46, 255))
    d.polygon(pts(n, (0.455, 0.020), (0.530, 0.020), (0.493, 0.075)),
              fill=(150, 112, 66, 255))      # the sharpened end, still showing

    torso = [(0.398, 0.310), (0.602, 0.310), (0.556, 0.640), (0.444, 0.640)]
    arms = [[(0.376, 0.322), (0.432, 0.332), (0.344, 0.612), (0.292, 0.596)],
            [(0.568, 0.332), (0.624, 0.322), (0.708, 0.596), (0.656, 0.612)]]
    legs = [[(0.422, 0.630), (0.502, 0.630), (0.488, 0.900), (0.414, 0.900)],
            [(0.498, 0.630), (0.578, 0.630), (0.586, 0.900), (0.512, 0.900)]]

    # The dark pass first: every shape swollen a little, so what is drawn over
    # it leaves a line behind.
    for shape in [torso] + arms + legs:
        limb(shape, edge, grow=0.10)
    d.ellipse([n * 0.386, n * 0.138, n * 0.572, n * 0.332], fill=edge)

    # Head, tipped forward and to one side, drawn before the torso so the jaw
    # overlaps the chest: a hanging body has no neck to speak of.
    d.ellipse([n * 0.398, n * 0.150, n * 0.560, n * 0.320], fill=flesh)
    d.ellipse([n * 0.418, n * 0.170, n * 0.500, n * 0.262], fill=flesh_lit)

    limb(torso, flesh)
    d.polygon(pts(n, (0.398, 0.310), (0.470, 0.310), (0.470, 0.640), (0.444, 0.640)),
              fill=flesh_lit)

    for shape in arms + legs:
        limb(shape, flesh)

    # And where the pole goes through - the one red note in the icon, at the
    # join, which is the only place it means anything.
    d.polygon(pts(n, (0.470, 0.262), (0.534, 0.268), (0.552, 0.470), (0.488, 0.462)),
              fill=blood)
    d.polygon(pts(n, (0.488, 0.462), (0.552, 0.470), (0.542, 0.575), (0.500, 0.600)),
              fill=(74, 8, 10, 255))


def glyph_plague_bite(d, w, h):
    """Two punctures and the swelling that comes after them."""
    # the bite
    d.ellipse([w * 0.255, h * 0.170, w * 0.375, h * 0.330], fill=255)
    d.ellipse([w * 0.625, h * 0.170, w * 0.745, h * 0.330], fill=255)

    # the bubo below it, bitten out of its own edge so it cannot be mistaken
    # for the immunity ward, which is a plain closed shape
    d.ellipse([w * 0.250, h * 0.400, w * 0.750, h * 0.880], fill=255)
    d.ellipse([w * 0.560, h * 0.430, w * 0.700, h * 0.570], fill=0)
    d.ellipse([w * 0.330, h * 0.640, w * 0.430, h * 0.740], fill=0)


# --- the court's furniture --------------------------------------------------
#
# All eleven are drawn for `S.ground`, which stands them on the olive build-menu
# tile the way vanilla frames a building. They are read at 128px in a scrolling
# list, so each is built around one silhouette nothing else in the menu has: a
# lid ajar, a spiked back, a veil half off.
CRIMSON_CLOTH = (122, 24, 30)
CRIMSON_LIT = (162, 40, 44)
CRIMSON_DARK = (72, 12, 18)
IRON = (58, 56, 60)
IRON_LIT = (96, 94, 100)
IRON_EDGE = (28, 26, 30)
GOLD = (198, 158, 62)
GOLD_LIT = (238, 206, 116)
STONE_LIT = (196, 192, 180)
STONE_DARK = (108, 105, 98)


def _slab(d, n, cx, cy, w, dep, h, top, left, right):
    """One isometric block: top diamond, then its two visible walls."""
    d.polygon(pts(n, (cx, cy - dep), (cx + w, cy), (cx, cy + dep), (cx - w, cy)),
              fill=top + (255,))
    d.polygon(pts(n, (cx - w, cy), (cx, cy + dep), (cx, cy + dep + h), (cx - w, cy + h)),
              fill=left + (255,))
    d.polygon(pts(n, (cx + w, cy), (cx, cy + dep), (cx, cy + dep + h), (cx + w, cy + h)),
              fill=right + (255,))


def art_count_coffin(d, img, n):
    """Sarcophagus with the lid pushed aside - the one shape a bed cannot have."""
    _slab(d, n, 0.50, 0.545, 0.335, 0.150, 0.185,
          (86, 30, 34), STONE_DARK, (128, 124, 116))
    d.polygon(pts(n, (0.50, 0.425), (0.755, 0.540), (0.50, 0.655), (0.245, 0.540)),
              fill=CRIMSON_DARK + (255,))
    d.polygon(pts(n, (0.50, 0.455), (0.700, 0.545), (0.50, 0.630), (0.300, 0.545)),
              fill=CRIMSON_CLOTH + (255,))

    # The lid, slid off to the near-left and tipped, so the box reads as open.
    _slab(d, n, 0.415, 0.360, 0.300, 0.135, 0.055,
          STONE_LIT, STONE_DARK, (150, 146, 136))
    d.polygon(pts(n, (0.415, 0.250), (0.560, 0.315), (0.415, 0.380), (0.270, 0.315)),
              fill=(214, 210, 198, 255))
    d.ellipse([n * 0.386, n * 0.300, n * 0.446, n * 0.352], fill=CRIMSON_DARK + (255,))
    d.polygon(pts(n, (0.416, 0.276), (0.446, 0.326), (0.386, 0.326)),
              fill=CRIMSON_DARK + (255,))


def art_count_throne(d, img, n):
    """High spiked back, bone arms, gold circlet - the throne's own silhouette."""
    _slab(d, n, 0.50, 0.775, 0.290, 0.110, 0.070, STONE_LIT, STONE_DARK, (150, 146, 136))

    d.polygon(pts(n, (0.355, 0.150), (0.645, 0.150), (0.645, 0.660), (0.355, 0.660)),
              fill=IRON + (255,))
    d.polygon(pts(n, (0.355, 0.150), (0.420, 0.150), (0.420, 0.660), (0.355, 0.660)),
              fill=IRON_LIT + (255,))
    for i in range(5):
        cx = 0.375 + i * 0.0625
        d.polygon(pts(n, (cx - 0.026, 0.155), (cx + 0.026, 0.155), (cx, 0.030)),
                  fill=(IRON_LIT if i % 2 == 0 else IRON) + (255,))
        d.polygon(pts(n, (cx - 0.026, 0.155), (cx - 0.008, 0.155), (cx, 0.030)),
                  fill=(132, 130, 138, 255))

    d.ellipse([n * 0.432, n * 0.212, n * 0.568, n * 0.286], outline=GOLD + (255,),
              width=max(int(n * 0.016), 2))
    for cx in (0.452, 0.500, 0.548):
        d.polygon(pts(n, (cx - 0.020, 0.238), (cx + 0.020, 0.238), (cx, 0.186)),
                  fill=GOLD_LIT + (255,))
    _skull(d, n, 0.500, 0.360, 0.078)

    d.polygon(pts(n, (0.50, 0.600), (0.735, 0.680), (0.50, 0.760), (0.265, 0.680)),
              fill=CRIMSON_CLOTH + (255,))
    d.polygon(pts(n, (0.50, 0.622), (0.685, 0.682), (0.50, 0.742), (0.315, 0.682)),
              fill=CRIMSON_LIT + (255,))
    for sgn in (-1, 1):
        x0 = 0.50 + sgn * 0.245
        d.polygon(pts(n, (x0 - sgn * 0.030, 0.512), (x0, 0.522),
                      (x0, 0.700), (x0 - sgn * 0.030, 0.690)), fill=IRON + (255,))
        _skull(d, n, x0, 0.500, 0.052)


def art_count_crypt(d, img, n):
    """Tomb chest with a recumbent effigy on the lid - not the coffin's shape."""
    # The chest is deliberately the darker stone: the effigy on top is the whole
    # point of the icon and it is pale, so the lid cannot be pale too or the
    # thing reads as a blank block at 128px.
    _slab(d, n, 0.50, 0.610, 0.345, 0.155, 0.200,
          (150, 146, 136), (86, 84, 79), (112, 109, 102))
    for i in range(3):
        x = 0.255 + i * 0.075
        d.polygon(pts(n, (x, 0.700), (x + 0.048, 0.724), (x + 0.048, 0.796), (x, 0.772)),
                  fill=(66, 64, 60, 255))
    # The effigy: a body lying the length of the lid, head to the back-left,
    # feet to the front-right, hands folded on a sword. It is drawn large and
    # across the whole diamond on purpose - small and centred, it disappeared
    # into the stone and the icon read as a plain crate.
    d.polygon(pts(n, (0.359, 0.528), (0.655, 0.619), (0.641, 0.697), (0.346, 0.606)),
              fill=(92, 90, 85, 255))
    d.polygon(pts(n, (0.367, 0.540), (0.646, 0.626), (0.635, 0.687), (0.356, 0.600)),
              fill=(236, 232, 218, 255))
    d.polygon(pts(n, (0.367, 0.540), (0.646, 0.626), (0.641, 0.652), (0.362, 0.566)),
              fill=(250, 246, 234, 255))
    d.ellipse([n * 0.308, n * 0.505, n * 0.404, n * 0.586], fill=(92, 90, 85, 255))
    d.ellipse([n * 0.316, n * 0.513, n * 0.397, n * 0.579], fill=(248, 244, 232, 255))
    d.ellipse([n * 0.332, n * 0.523, n * 0.374, n * 0.555], fill=(214, 210, 198, 255))
    d.polygon(pts(n, (0.610, 0.651), (0.650, 0.663), (0.646, 0.694), (0.605, 0.682)),
              fill=(214, 210, 198, 255))            # the feet, together
    d.polygon(pts(n, (0.419, 0.570), (0.605, 0.627), (0.601, 0.643), (0.415, 0.586)),
              fill=GOLD + (255,))                   # the sword down the body
    d.polygon(pts(n, (0.422, 0.559), (0.446, 0.567), (0.439, 0.600), (0.415, 0.592)),
              fill=GOLD_LIT + (255,))               # its crossguard
    d.ellipse([n * 0.452, n * 0.577, n * 0.506, n * 0.615], fill=(200, 196, 184, 255))


def _banner_cloth(d, n, cx, top, w, h):
    """The house cloth: crimson, tattered hem, one gold drop on it."""
    d.polygon(pts(n, (cx - w, top), (cx + w, top), (cx + w, top + h),
                  (cx + w * 0.55, top + h - 0.045), (cx, top + h + 0.020),
                  (cx - w * 0.55, top + h - 0.045), (cx - w, top + h)),
              fill=CRIMSON_CLOTH + (255,))
    d.polygon(pts(n, (cx - w, top), (cx - w * 0.30, top), (cx - w * 0.30, top + h - 0.012),
                  (cx - w, top + h)), fill=CRIMSON_LIT + (255,))
    d.ellipse([n * (cx - w * 0.40), n * (top + h * 0.42),
               n * (cx + w * 0.40), n * (top + h * 0.80)], fill=GOLD + (255,))
    d.polygon(pts(n, (cx, top + h * 0.24), (cx + w * 0.40, top + h * 0.62),
                  (cx - w * 0.40, top + h * 0.62)), fill=GOLD + (255,))
    d.ellipse([n * (cx - w * 0.22), n * (top + h * 0.52),
               n * (cx - w * 0.02), n * (top + h * 0.68)], fill=GOLD_LIT + (255,))


def art_court_banner(d, img, n):
    """Free-standing banner: cloth hung off a crossbar on a floor pole."""
    d.polygon(pts(n, (0.478, 0.120), (0.522, 0.120), (0.522, 0.860), (0.478, 0.860)),
              fill=WOOD + (255,))
    d.polygon(pts(n, (0.478, 0.120), (0.494, 0.120), (0.494, 0.860), (0.478, 0.860)),
              fill=(168, 118, 70, 255))
    d.rounded_rectangle([n * 0.255, n * 0.140, n * 0.745, n * 0.185], radius=n * 0.022,
                        fill=(96, 64, 36, 255))
    for cx in (0.290, 0.710):
        d.ellipse([n * (cx - 0.030), n * 0.128, n * (cx + 0.030), n * 0.196],
                  fill=GOLD + (255,))
    _banner_cloth(d, n, 0.500, 0.180, 0.205, 0.520)
    _slab(d, n, 0.500, 0.830, 0.150, 0.060, 0.045, IRON_LIT, IRON_EDGE, IRON)


def art_court_banner_wall(d, img, n):
    """The same cloth, flat against a wall - the pair reads by the wall behind."""
    for i in range(4):
        y = 0.120 + i * 0.150
        d.rectangle([n * 0.145, n * y, n * 0.855, n * (y + 0.135)],
                    fill=(126, 124, 118, 255) if i % 2 else (112, 110, 104, 255))
    d.rectangle([n * 0.145, n * 0.120, n * 0.855, n * 0.720], outline=(84, 82, 78, 255),
                width=max(int(n * 0.010), 2))
    d.rounded_rectangle([n * 0.270, n * 0.200, n * 0.730, n * 0.243], radius=n * 0.020,
                        fill=(96, 64, 36, 255))
    _banner_cloth(d, n, 0.500, 0.238, 0.190, 0.470)


def art_crimson_candle(d, img, n):
    """Tall candelabrum: three red candles alight on an iron stem."""
    _slab(d, n, 0.500, 0.800, 0.180, 0.070, 0.050, IRON_LIT, IRON_EDGE, IRON)
    d.polygon(pts(n, (0.478, 0.330), (0.522, 0.330), (0.522, 0.810), (0.478, 0.810)),
              fill=IRON + (255,))
    d.polygon(pts(n, (0.478, 0.330), (0.494, 0.330), (0.494, 0.810), (0.478, 0.810)),
              fill=IRON_LIT + (255,))
    for sgn in (-1, 1):
        d.polygon(pts(n, (0.500, 0.380), (0.500 + sgn * 0.230, 0.470),
                      (0.500 + sgn * 0.230, 0.512), (0.500, 0.424)), fill=IRON + (255,))
    for cx, top, hgt in [(0.270, 0.400, 0.112), (0.500, 0.230, 0.104), (0.730, 0.400, 0.112)]:
        d.polygon(pts(n, (cx - 0.048, top), (cx + 0.048, top),
                      (cx + 0.048, top + hgt), (cx - 0.048, top + hgt)),
                  fill=CRIMSON_CLOTH + (255,))
        d.polygon(pts(n, (cx - 0.048, top), (cx - 0.016, top),
                      (cx - 0.016, top + hgt), (cx - 0.048, top + hgt)),
                  fill=CRIMSON_LIT + (255,))
        d.ellipse([n * (cx - 0.048), n * (top - 0.016), n * (cx + 0.048), n * (top + 0.016)],
                  fill=(180, 62, 60, 255))
        d.polygon(pts(n, (cx, top - 0.130), (cx + 0.044, top - 0.030),
                      (cx - 0.044, top - 0.030)), fill=(238, 154, 44, 255))
        d.polygon(pts(n, (cx, top - 0.086), (cx + 0.022, top - 0.026),
                      (cx - 0.022, top - 0.026)), fill=(252, 232, 168, 255))


def art_blood_brazier(d, img, n):
    """Three-legged bowl of banked coals, glowing at the bottom of the heap."""
    for lean in (-0.115, 0.0, 0.115):
        d.polygon(pts(n, (0.500 + lean * 0.35 - 0.030, 0.560),
                      (0.500 + lean * 0.35 + 0.030, 0.560),
                      (0.500 + lean + 0.034, 0.850), (0.500 + lean - 0.034, 0.850)),
                  fill=IRON + (255,))
    d.polygon(pts(n, (0.190, 0.400), (0.810, 0.400), (0.700, 0.640), (0.300, 0.640)),
              fill=IRON + (255,))
    d.polygon(pts(n, (0.190, 0.400), (0.330, 0.400), (0.330, 0.632), (0.300, 0.640)),
              fill=IRON_LIT + (255,))
    d.ellipse([n * 0.190, n * 0.352, n * 0.810, n * 0.452], fill=IRON_EDGE + (255,))
    d.ellipse([n * 0.222, n * 0.366, n * 0.778, n * 0.442], fill=(96, 26, 22, 255))
    for cx, cy, r in [(0.400, 0.398, 0.048), (0.520, 0.386, 0.058), (0.630, 0.404, 0.042),
                      (0.462, 0.418, 0.038), (0.578, 0.418, 0.036)]:
        d.ellipse([n * (cx - r), n * (cy - r * 0.62), n * (cx + r), n * (cy + r * 0.62)],
                  fill=(196, 58, 30, 255))
        d.ellipse([n * (cx - r * 0.5), n * (cy - r * 0.34), n * (cx + r * 0.5),
                   n * (cy + r * 0.30)], fill=(248, 176, 64, 255))
    d.polygon(pts(n, (0.500, 0.180), (0.560, 0.320), (0.440, 0.320)), fill=(224, 96, 40, 255))
    d.polygon(pts(n, (0.500, 0.244), (0.534, 0.322), (0.466, 0.322)), fill=(250, 208, 120, 255))


def art_veiled_mirror(d, img, n):
    """Standing glass with the cloth half off - and nothing reflected in it."""
    d.rounded_rectangle([n * 0.230, n * 0.130, n * 0.770, n * 0.830], radius=n * 0.170,
                        fill=(146, 148, 152, 255))
    d.rounded_rectangle([n * 0.262, n * 0.162, n * 0.738, n * 0.798], radius=n * 0.150,
                        fill=(196, 198, 204, 255))
    d.rounded_rectangle([n * 0.300, n * 0.200, n * 0.700, n * 0.760], radius=n * 0.130,
                        fill=(40, 42, 52, 255))
    d.polygon(pts(n, (0.330, 0.700), (0.470, 0.230), (0.530, 0.230), (0.390, 0.700)),
              fill=(58, 62, 76, 255))
    # The veil, dragged back to the left and hanging off the frame. It has to
    # leave most of the glass showing, because the joke of this piece is what is
    # not in the glass, and a covered mirror is just a covered rectangle.
    d.polygon(pts(n, (0.222, 0.120), (0.560, 0.120), (0.512, 0.246),
                  (0.430, 0.300), (0.392, 0.430), (0.336, 0.520), (0.300, 0.620),
                  (0.252, 0.560), (0.226, 0.400)),
              fill=CRIMSON_CLOTH + (255,))
    d.polygon(pts(n, (0.222, 0.120), (0.400, 0.120), (0.336, 0.520),
                  (0.300, 0.620), (0.252, 0.560), (0.226, 0.400)),
              fill=CRIMSON_LIT + (255,))
    d.polygon(pts(n, (0.300, 0.620), (0.336, 0.520), (0.392, 0.430), (0.352, 0.640)),
              fill=CRIMSON_DARK + (255,))
    _slab(d, n, 0.500, 0.840, 0.220, 0.070, 0.048, (86, 58, 34), (52, 34, 20), (68, 45, 26))


def art_court_reliquary(d, img, n):
    """Closed case with glass doors and a crowned skull sitting behind them."""
    _slab(d, n, 0.500, 0.845, 0.330, 0.075, 0.035, (86, 58, 34), (52, 34, 20), (68, 45, 26))
    d.rectangle([n * 0.185, n * 0.120, n * 0.815, n * 0.860], fill=(88, 58, 34, 255))
    d.rectangle([n * 0.185, n * 0.120, n * 0.290, n * 0.860], fill=(120, 82, 48, 255))
    d.polygon(pts(n, (0.155, 0.120), (0.845, 0.120), (0.500, 0.020)), fill=(72, 47, 28, 255))
    d.rectangle([n * 0.250, n * 0.190, n * 0.750, n * 0.790], fill=(26, 24, 28, 255))
    d.rectangle([n * 0.268, n * 0.208, n * 0.750, n * 0.790], fill=(44, 46, 56, 255))
    _skull(d, n, 0.500, 0.430, 0.145)
    d.polygon(pts(n, (0.360, 0.310), (0.640, 0.310), (0.640, 0.262),
                  (0.590, 0.288), (0.560, 0.230), (0.500, 0.276), (0.440, 0.230),
                  (0.410, 0.288), (0.360, 0.262)), fill=GOLD + (255,))
    d.rectangle([n * 0.360, n * 0.306, n * 0.640, n * 0.330], fill=GOLD_LIT + (255,))
    d.rectangle([n * 0.488, n * 0.190, n * 0.512, n * 0.790], fill=(88, 58, 34, 255))
    for y in (0.372, 0.560):
        d.rectangle([n * 0.250, n * y, n * 0.750, n * (y + 0.020)], fill=(88, 58, 34, 255))
    d.polygon(pts(n, (0.300, 0.760), (0.430, 0.220), (0.470, 0.220), (0.340, 0.760)),
              fill=(255, 255, 255, 34))


def art_blood_well(d, img, n):
    """Stone kerb, a roof on two posts, and what the bucket comes up with."""
    d.polygon(pts(n, (0.500, 0.560), (0.860, 0.680), (0.500, 0.800), (0.140, 0.680)),
              fill=STONE_DARK + (255,))
    d.polygon(pts(n, (0.140, 0.680), (0.500, 0.800), (0.500, 0.870), (0.140, 0.750)),
              fill=(92, 89, 84, 255))
    d.polygon(pts(n, (0.860, 0.680), (0.500, 0.800), (0.500, 0.870), (0.860, 0.750)),
              fill=(126, 122, 114, 255))
    d.polygon(pts(n, (0.500, 0.592), (0.760, 0.680), (0.500, 0.768), (0.240, 0.680)),
              fill=(58, 12, 16, 255))
    d.polygon(pts(n, (0.500, 0.618), (0.700, 0.684), (0.500, 0.750), (0.300, 0.684)),
              fill=BLOOD + (255,))
    d.ellipse([n * 0.442, n * 0.672, n * 0.520, n * 0.702], fill=(190, 44, 44, 255))

    for cx in (0.268, 0.732):
        d.polygon(pts(n, (cx - 0.028, 0.290), (cx + 0.028, 0.290),
                      (cx + 0.028, 0.700), (cx - 0.028, 0.700)), fill=WOOD + (255,))
    d.polygon(pts(n, (0.500, 0.090), (0.900, 0.300), (0.500, 0.330), (0.100, 0.300)),
              fill=(96, 64, 36, 255))
    d.polygon(pts(n, (0.500, 0.090), (0.900, 0.300), (0.500, 0.330)),
              fill=(72, 47, 28, 255))
    d.rounded_rectangle([n * 0.240, n * 0.330, n * 0.760, n * 0.368], radius=n * 0.018,
                        fill=(72, 47, 28, 255))
    d.polygon(pts(n, (0.492, 0.368), (0.508, 0.368), (0.508, 0.520), (0.492, 0.520)),
              fill=(214, 210, 198, 255))
    d.polygon(pts(n, (0.432, 0.520), (0.568, 0.520), (0.548, 0.618), (0.452, 0.618)),
              fill=(88, 58, 34, 255))
    d.ellipse([n * 0.432, n * 0.500, n * 0.568, n * 0.542], fill=(58, 12, 16, 255))
    d.ellipse([n * 0.448, n * 0.508, n * 0.552, n * 0.536], fill=BLOOD + (255,))


def art_vigil_table(d, img, n):
    """Long stone table with the channel down it and candles standing on it."""
    for cx, cy in [(0.250, 0.700), (0.750, 0.700)]:
        d.polygon(pts(n, (cx - 0.048, cy), (cx + 0.048, cy),
                      (cx + 0.062, cy + 0.170), (cx - 0.062, cy + 0.170)),
                  fill=STONE_DARK + (255,))
    d.polygon(pts(n, (0.500, 0.480), (0.960, 0.640), (0.500, 0.800), (0.040, 0.640)),
              fill=STONE_LIT + (255,))
    d.polygon(pts(n, (0.040, 0.640), (0.500, 0.800), (0.500, 0.856), (0.040, 0.696)),
              fill=STONE_DARK + (255,))
    d.polygon(pts(n, (0.960, 0.640), (0.500, 0.800), (0.500, 0.856), (0.960, 0.696)),
              fill=(132, 128, 120, 255))
    d.polygon(pts(n, (0.500, 0.510), (0.880, 0.642), (0.500, 0.772), (0.120, 0.642)),
              fill=(178, 174, 164, 255))
    d.polygon(pts(n, (0.130, 0.596), (0.870, 0.596), (0.870, 0.618), (0.130, 0.618)),
              fill=(78, 74, 68, 255))
    d.polygon(pts(n, (0.140, 0.601), (0.860, 0.601), (0.860, 0.613), (0.140, 0.613)),
              fill=BLOOD + (255,))
    for cx, base in [(0.300, 0.556), (0.500, 0.512), (0.700, 0.556)]:
        d.polygon(pts(n, (cx - 0.030, base - 0.150), (cx + 0.030, base - 0.150),
                      (cx + 0.030, base), (cx - 0.030, base)), fill=(226, 220, 202, 255))
        d.polygon(pts(n, (cx - 0.030, base - 0.150), (cx - 0.010, base - 0.150),
                      (cx - 0.010, base), (cx - 0.030, base)), fill=(248, 244, 230, 255))
        d.polygon(pts(n, (cx, base - 0.246), (cx + 0.032, base - 0.166),
                      (cx - 0.032, base - 0.166)), fill=(238, 154, 44, 255))
        d.polygon(pts(n, (cx, base - 0.212), (cx + 0.016, base - 0.164),
                      (cx - 0.016, base - 0.164)), fill=(252, 232, 168, 255))


# --- the icons --------------------------------------------------------------
ICONS = [
    ("Gravedigger", "perk_gravedigger", lambda: S.medallion(motif_gravedigger, EARTH, zoom=1.08)),
    ("Gravedigger", "role_gravedigger", lambda: S.shield(emblem_spade, S.SHIELD_BROWN, (188, 182, 168))),
    ("Gravedigger", "icon_mass_grave", lambda: S.ground(art_mass_grave)),
    ("Gravedigger", "icon_mass_pyre", lambda: S.cutout(art_mass_pyre)),
    ("Gravedigger", "bubble_grave_duty", lambda: S.crest(glyph_spade, S.CREST_GREEN)),
    ("Gravedigger", "bubble_grave_stripped", lambda: S.crest(glyph_spade_broken, S.CREST_RED)),

    ("CarrionAndPlague", "icon_cat_statue", lambda: S.ground(art_cat_statue)),
    ("VampireCourt", "icon_impaled_stake", lambda: S.ground(art_impaled_stake)),
    ("CarrionAndPlague", "icon_plague_mask", lambda: S.cutout(art_plague_mask)),
    ("CarrionAndPlague", "icon_plague_hood", lambda: S.cutout(art_plague_hood)),
    ("CarrionAndPlague", "bubble_plague_immune", lambda: S.crest(glyph_ward, S.CREST_GREEN)),
    ("CarrionAndPlague", "bubble_plague_bite", lambda: S.crest(glyph_plague_bite, S.CREST_RED)),

    ("VampireCourt", "perk_vampire", lambda: S.medallion(motif_vampire, CRIMSON)),
    ("VampireCourt", "perk_ghoul", lambda: S.medallion(motif_ghoul, SICK, zoom=1.15)),
    ("VampireCourt", "perk_thrall", lambda: S.medallion(motif_thrall, COUNT_PURPLE, zoom=1.15)),
    ("VampireCourt", "role_count", lambda: S.shield(emblem_crown, S.SHIELD_PURPLE, (196, 60, 66))),
    ("VampireCourt", "icon_blood_draught", lambda: S.cutout(art_blood_draught)),
    ("VampireCourt", "icon_blood_altar", lambda: S.ground(art_blood_altar)),
    ("VampireCourt", "icon_blood_circle", lambda: S.ground(art_blood_circle)),
    ("VampireCourt", "icon_count_coffin", lambda: S.ground(art_count_coffin)),
    ("VampireCourt", "icon_count_throne", lambda: S.ground(art_count_throne)),
    ("VampireCourt", "icon_count_crypt", lambda: S.ground(art_count_crypt)),
    ("VampireCourt", "icon_court_banner", lambda: S.ground(art_court_banner)),
    ("VampireCourt", "icon_court_banner_wall", lambda: S.ground(art_court_banner_wall)),
    ("VampireCourt", "icon_crimson_candle", lambda: S.ground(art_crimson_candle)),
    ("VampireCourt", "icon_blood_brazier", lambda: S.ground(art_blood_brazier)),
    ("VampireCourt", "icon_veiled_mirror", lambda: S.ground(art_veiled_mirror)),
    ("VampireCourt", "icon_court_reliquary", lambda: S.ground(art_court_reliquary)),
    ("VampireCourt", "icon_blood_well", lambda: S.ground(art_blood_well)),
    ("VampireCourt", "icon_vigil_table", lambda: S.ground(art_vigil_table)),
    ("VampireCourt", "bubble_count_duty", lambda: S.crest(glyph_crown, S.CREST_GREEN)),
    ("VampireCourt", "bubble_count_stripped", lambda: S.crest(glyph_crown_broken, S.CREST_RED)),
    ("VampireCourt", "bubble_blood_feast", lambda: S.crest(glyph_goblet, S.CREST_GREEN)),
    ("VampireCourt", "bubble_blood_drained", lambda: S.crest(glyph_drained, S.CREST_RED)),
    ("VampireCourt", "bubble_bite_mark", lambda: S.crest(glyph_bite_mark, S.CREST_RED)),
    ("VampireCourt", "bubble_bite_missed", lambda: S.crest(glyph_bite_missed, S.CREST_RED)),

    ("UndeadHorde", "icon_undead_claws", lambda: S.cutout(art_undead_claws)),
    ("UndeadHorde", "perk_risen", lambda: S.medallion(motif_risen, GRAVE, zoom=1.05)),
]


def main():
    dry = sys.argv[1] if len(sys.argv) > 1 else None
    for mod, name, build in ICONS:
        img = build()
        out = dry or os.path.join(MODS, mod, "Data", "Sprites")
        os.makedirs(out, exist_ok=True)
        path = os.path.join(out, PREFIX + name + ".png")
        img.save(path)
        print(f"{mod:18} {PREFIX + name:26} {img.size[0]}x{img.size[1]}")
    print(f"\n{len(ICONS)} iconos -> {dry or MODS}")


if __name__ == "__main__":
    main()
