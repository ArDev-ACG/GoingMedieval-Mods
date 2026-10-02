"""Vanilla-matched icon primitives for the Going Medieval mods.

Every measurement here was sampled from the game's own sprites (extracted with
UnityPy from StreamingAssets/aa/StandaloneWindows64/*.bundle), so the families
below sit next to Foxy Voxel art without looking foreign:

  medallion  64x64    perk icons      - metal ring, dark radial field, motif
  shield     390x508  role icons      - grained heraldic field, embossed emblem
  cutout     128x128  item / recipe   - object over thick white keyline + shadow
  ground     128x128  buildings       - object on the build-menu tile grid
  crest      86x96    effector bubble - small badge, gold rim, flat glyph

Art is drawn at SS times the final size and downsampled with LANCZOS, which is
what gives the soft painted edges the vanilla icons have.
"""

from PIL import Image, ImageDraw, ImageFilter
import numpy as np

SS = 6  # supersample factor

# --- colours sampled from vanilla sprites -----------------------------------
RING_LIGHT = (148, 146, 148)    # Callous perk ring
RING_DARK = (8, 4, 8)
SHIELD_BROWN = (145, 83, 54)    # warden_role_icon field
SHIELD_PURPLE = (49, 33, 68)    # sergeant_role_icon field
CREST_GREEN = (104, 184, 107)   # positive_mood_icon
CREST_RED = (244, 101, 101)     # negative_mood_icon
CREST_RIM = (107, 90, 35)


def _down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def radial_field(size, inner, outer, reach=1.0):
    """Dark-edged radial gradient: the bed every perk medallion sits on.

    `reach` is where `outer` is finally reached, measured in half-widths. At 1
    the ramp is finished at the middle of each edge and the four corners are
    flat - which is right for a medallion, because the corners are cut off
    anyway, and wrong for a square tile, where it is the one thing that made
    our building icons read as a flat dark card next to vanilla's lit ones.
    See `tile_backdrop`.
    """
    n = size
    y, x = np.mgrid[0:n, 0:n]
    c = (n - 1) / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2) / (c * reach)
    t = np.clip(r, 0, 1)[..., None] ** 1.35
    rgb = np.array(inner)[None, None, :] * (1 - t) + np.array(outer)[None, None, :] * t
    arr = np.dstack([rgb, np.full((n, n), 255)]).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def wood_grain(size, base, seed=7):
    """Vertical streaks plus a top-light falloff: the role shields' plank."""
    w, h = size
    rng = np.random.default_rng(seed)
    cols = max(w // 14, 2)
    streak = rng.normal(0, 1, (h, cols))
    streak = np.repeat(streak, 14, axis=1)
    if streak.shape[1] < w:
        streak = np.pad(streak, ((0, 0), (0, w - streak.shape[1])), mode="edge")
    streak = np.clip(streak[:, :w], -2.2, 2.2) * 7.0
    yy, xx = np.mgrid[0:h, 0:w]
    lift = 34 * (1 - yy / h) - 26 * (yy / h) - 10 * (xx / w)
    rgb = np.clip(np.array(base, float)[None, None, :] + (streak + lift)[..., None], 0, 255)
    arr = np.dstack([rgb, np.full((h, w), 255)]).astype(np.uint8)
    return Image.fromarray(arr, "RGBA").filter(ImageFilter.GaussianBlur(1.2))


def paint_pass(img, grain=9.0, light=0.16, seed=3):
    """Breaks up flat vector fills: fine grain plus a top-left light wash.

    Vanilla perk art is painted, so perfectly even fills read as foreign. This
    leaves alpha untouched and only disturbs the colour channels.
    """
    arr = np.array(img).astype(np.float32)
    h, w = arr.shape[:2]
    rng = np.random.default_rng(seed)
    noise = rng.normal(0, grain, (h, w, 1))
    yy, xx = np.mgrid[0:h, 0:w]
    wash = (1 - (xx / w) * 0.5 - (yy / h) * 0.5) * (255 * light)
    arr[..., :3] = np.clip(arr[..., :3] + noise + wash[..., None], 0, 255)
    out = Image.fromarray(arr.astype(np.uint8), "RGBA")
    return out.filter(ImageFilter.GaussianBlur(max(w // 260, 1)))


def _inner_edge(mask, size, offset, blur):
    """Blurred band just inside a mask, shifted: the emboss light/dark seat."""
    w, h = size
    shrunk = mask.filter(ImageFilter.MinFilter(3))
    band = Image.composite(mask, Image.new("L", (w, h), 0), shrunk.point(lambda v: 255 - v))
    band = band.filter(ImageFilter.GaussianBlur(blur))
    shifted = Image.new("L", (w, h), 0)
    shifted.paste(band, offset)
    return Image.composite(shifted, Image.new("L", (w, h), 0), mask)


def emboss(mask, fill, size, depth=4, gloss=True):
    """Flat shape -> raised enamel emblem: seat shadow, lit rim, gloss sweep."""
    w, h = size
    d = max(int(depth), 2)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    seat = mask.filter(ImageFilter.GaussianBlur(d * 0.9))
    out.paste(Image.new("RGBA", (w, h), (0, 0, 0, 190)), (d, int(d * 1.5)), seat)
    out.paste(Image.new("RGBA", (w, h), tuple(fill) + (255,)), (0, 0), mask)
    hi = tuple(min(255, c + 95) for c in fill)
    lo = tuple(max(0, int(c * 0.45)) for c in fill)
    out.paste(Image.new("RGBA", (w, h), hi + (255,)), (0, 0),
              _inner_edge(mask, size, (-d, -d), d * 0.7))
    out.paste(Image.new("RGBA", (w, h), lo + (255,)), (0, 0),
              _inner_edge(mask, size, (d, d), d * 0.7))
    if gloss:
        g = Image.new("L", (w, h), 0)
        ImageDraw.Draw(g).ellipse([-w * 0.1, -h * 0.55, w * 0.85, h * 0.40], fill=64)
        g = g.filter(ImageFilter.GaussianBlur(w * 0.03))
        out.paste(Image.new("RGBA", (w, h), (255, 255, 255, 255)), (0, 0),
                  Image.composite(g, Image.new("L", (w, h), 0), mask))
    return out


def carve(art, depth=None, darken=0.2, shade=190, light=105):
    """Sinks flat motif art into whatever it sits on.

    A vanilla perk motif is not a sticker laid on the field: the shape reads as
    cut into it. That is two things at once - the fill is a fifth darker than
    the raw vector colour, and the inner edge is shaded the opposite way round
    from `emboss`: dark where the groove wall faces away from the top-left key
    light, catching light on the far side. Alpha is left alone, so the motif
    keeps its silhouette exactly.
    """
    w, h = art.size
    d = max(int(depth or w * 0.012), 2)
    mask = art.split()[3].point(lambda v: 255 if v > 96 else 0)

    arr = np.array(art).astype(np.float32)
    arr[..., :3] *= (1.0 - darken)
    out = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")

    for offset, rgba in (((-d, -d), (0, 0, 0, shade)),
                         ((d, d), (255, 255, 255, light))):
        band = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        band.paste(Image.new("RGBA", (w, h), rgba), (0, 0),
                   _inner_edge(mask, (w, h), offset, d * 0.7))
        out.alpha_composite(band)
    return out


def white_outline(art, thickness, shadow=True):
    """The thick white keyline every loose-object icon in the game wears."""
    w, h = art.size
    grown = art.split()[3]
    for _ in range(int(thickness)):
        grown = grown.filter(ImageFilter.MaxFilter(3))
    grown = grown.point(lambda v: 255 if v > 40 else 0)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if shadow:
        out.paste(Image.new("RGBA", (w, h), (0, 0, 0, 120)),
                  (int(thickness * 0.9), int(thickness * 1.4)),
                  grown.filter(ImageFilter.GaussianBlur(thickness * 0.9)))
    out.paste(Image.new("RGBA", (w, h), (255, 255, 255, 255)), (0, 0), grown)
    out.alpha_composite(art)
    return out


def shield_mask(w, h):
    """Heraldic escutcheon: straight shoulders, bowed flanks, pointed base."""
    m = Image.new("L", (w, h), 0)
    pts = [(w * 0.045, h * 0.02), (w * 0.955, h * 0.02), (w * 0.955, h * 0.50)]
    for i in range(21):                       # right flank sweeping to the point
        t = i / 20
        pts.append((w * (0.955 - 0.455 * t ** 1.7), h * (0.50 + 0.485 * t)))
    for i in range(21):                       # mirrored left flank
        t = 1 - i / 20
        pts.append((w * (0.045 + 0.455 * t ** 1.7), h * (0.50 + 0.485 * t)))
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def crest_mask(w, h):
    """Badge shape of the mood bubbles: rounded top, tapered notched base."""
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([w * 0.06, h * 0.05, w * 0.94, h * 0.62], radius=w * 0.16, fill=255)
    d.polygon([(w * 0.06, h * 0.50), (w * 0.94, h * 0.50),
               (w * 0.74, h * 0.86), (w * 0.5, h * 0.95), (w * 0.26, h * 0.86)], fill=255)
    return m


# El fondo de los iconos de edificio, medido sobre los 102 iconos de 128px del
# propio juego que hay en ASSESTS/Referencias/iconos/, no estimado a ojo.
#
# Lo de antes era un degradado radial con dos familias de lineas tiradas a mano
# (`step = w / 5.5`, tinte verde claro al 18% de alfa). Contra un icono vanilla
# se veia de lejos: la rejilla del juego no esta donde estaba la nuestra, ni
# tiene ese color, ni ese grosor.
#
# Como se midio. El fondo no se puede leer de un icono suelto - todos llevan el
# mueble justo encima del centro - asi que se apilaron los 102 y se saco por
# pixel el valor que mas se repite. Eso da el anillo exterior limpio; con el
# se ajustaron:
#
#   degradado   centro (146,158,68) -> esquina (57,60,0), por tramos de radio
#   rejilla     isometrica 2:1, pendiente +-0.5
#               periodo 61.5 px a 128, fase 16.25 -> los cruces caen en
#               (64,47) y (64,108), que es donde estan en el juego
#   linea       (170,167,128), 2.4 px de ancho a 128, borde suave de 1.3 px
#
# El halo cyan que se ve detras de los muebles en los iconos del juego NO es
# del fondo: lo dibuja cada objeto. Por eso va en `ground`, no aqui.
TILE_STOPS = [(0, (146, 158, 68)), (12, (142, 154, 63)), (24, (140, 150, 57)),
              (32, (135, 146, 49)), (40, (120, 136, 60)), (44, (105, 128, 50)),
              (48, (99, 117, 32)), (52, (99, 107, 10)), (56, (95, 101, 0)),
              (60, (90, 97, 0)), (64, (90, 93, 0)), (68, (84, 89, 0)),
              (72, (82, 85, 0)), (76, (74, 79, 0)), (80, (74, 75, 0)),
              (84, (66, 71, 0)), (88, (63, 66, 0)), (96, (56, 59, 0)),
              (110, (50, 53, 0))]
TILE_LINE = (170, 167, 128)
TILE_PERIOD = 61.5          # px a escala 128
TILE_PHASE = 16.25
TILE_SLOPE = 0.5            # isometrico 2:1
TILE_LINE_W = 2.4
TILE_LINE_SOFT = 1.3
TILE_LINE_A = 0.92


def tile_backdrop(w, h):
    """El fondo olivo con la rejilla del menu de construccion, a cualquier tamano.

    Se dibuja a la resolucion que le pidan - `ground` ya la llama supersampleada -
    escalando la geometria desde los 128 px en que se midio.
    """
    n = max(w, h)
    k = n / 128.0
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float64)
    X = xs + 0.5
    Y = ys + 0.5

    r = np.hypot(X - n / 2.0, Y - n / 2.0) / k
    rs = np.array([s[0] for s in TILE_STOPS], float)
    cs = np.array([s[1] for s in TILE_STOPS], float)
    base = np.stack([np.interp(r, rs, cs[:, i]) for i in range(3)], axis=-1)

    Xi = (X - n / 2.0) / k + 64.0
    Yi = (Y - n / 2.0) / k + 64.0
    lw = TILE_LINE_W * k / 2.0
    soft = TILE_LINE_SOFT * k
    m = np.zeros_like(X)
    for sl in (TILE_SLOPE, -TILE_SLOPE):
        p = Yi - sl * Xi
        d = np.abs(((p - TILE_PHASE) + TILE_PERIOD / 2.0) % TILE_PERIOD
                   - TILE_PERIOD / 2.0) * k * np.cos(np.arctan(sl))
        m = np.maximum(m, np.clip((lw + soft - d) / soft, 0, 1))
    m *= TILE_LINE_A

    rgb = base * (1 - m[..., None]) + np.array(TILE_LINE, float) * m[..., None]
    arr = np.dstack([np.clip(rgb, 0, 255), np.full((n, n), 255)]).astype(np.uint8)
    img = Image.fromarray(arr, "RGBA")
    return img if (w, h) == (n, n) else img.resize((w, h), Image.LANCZOS)


def medallion(motif, field=((90, 30, 26), (12, 6, 6)), out_size=64, zoom=1.35):
    """Perk icon: radial field, motif, vignette, then the grey metal ring.

    Vanilla perk art fills its circle edge to edge, so the motif is drawn on a
    full canvas and then blown up by `zoom` around the centre before landing.
    """
    n = out_size * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    disc = Image.new("L", (n, n), 0)
    ImageDraw.Draw(disc).ellipse([0, 0, n - 1, n - 1], fill=255)
    img.paste(radial_field(n, *field), (0, 0), disc)

    art = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    motif(ImageDraw.Draw(art), art, n)
    if zoom != 1.0:
        big = art.resize((int(n * zoom), int(n * zoom)), Image.LANCZOS)
        art = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        off = (n - big.width) // 2
        art.paste(big, (off, off), big)
    img.alpha_composite(carve(art, depth=n * 0.011))

    img = paint_pass(img)

    # Soft edge darkening only - vanilla vignettes never swallow the art.
    vig = Image.new("L", (n, n), 0)
    ImageDraw.Draw(vig).ellipse([n * 0.06, n * 0.06, n * 0.94, n * 0.94], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(n * 0.07)).point(lambda v: int((255 - v) * 0.62))
    img.paste(Image.new("RGBA", (n, n), (0, 0, 0, 255)), (0, 0),
              Image.composite(vig, Image.new("L", (n, n), 0), disc))

    ring = ImageDraw.Draw(img)
    lw = max(int(n * 0.030), 2)
    ring.ellipse([lw * 0.5, lw * 0.5, n - lw * 0.5, n - lw * 0.5],
                 outline=RING_DARK + (255,), width=int(lw * 1.6))
    ring.ellipse([lw, lw, n - lw, n - lw], outline=RING_LIGHT + (255,), width=lw)
    img.putalpha(Image.composite(img.split()[3], Image.new("L", (n, n), 0), disc))
    return _down(img, out_size, out_size)


def shield(emblem, field=SHIELD_BROWN, emblem_rgb=(185, 178, 164), size=(390, 508)):
    """Role icon: grained plank inside the escutcheon, emblem raised on top."""
    w, h = size[0] * 2, size[1] * 2
    mask = shield_mask(w, h)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    img.paste(wood_grain((w, h), field), (0, 0), mask)
    img.paste(Image.new("RGBA", (w, h), (0, 0, 0, 140)), (0, 0),
              _inner_edge(mask, (w, h), (0, 0), w * 0.012))

    em = Image.new("L", (w, h), 0)
    emblem(ImageDraw.Draw(em), w, h)
    sunk = tuple(int(c * 0.8) for c in emblem_rgb)
    img.alpha_composite(carve(emboss(em, sunk, (w, h), depth=int(w * 0.014)),
                              depth=int(w * 0.010), darken=0.0))
    img.putalpha(Image.composite(img.split()[3], Image.new("L", (w, h), 0), mask))
    return _down(img, *size)


def cutout(draw_fn, out_size=128, thickness=None):
    """Loose object (resource, recipe): art plus the vanilla white keyline."""
    n = out_size * SS
    art = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(art), art, n)
    return _down(white_outline(art, thickness or n * 0.030), out_size, out_size)


# How much light the artwork was missing, and it was a lot. Averaged over the
# middle eighth of the icon, where the object is:
#
#     vanilla  quality_chair (182, 157, 126)   stone_brazier (201, 177, 121)
#     ours     count_throne   (89,  86,  84)   blood_brazier  (88,  56,  49)
#
# Half the brightness. Every one of these is built from the model's own
# palette, and those palettes are a crypt's - which is correct on the model,
# lit by the game's own light, and wrong on a 128px card that has no light of
# its own. So the icon's artwork is lit here, once, rather than by going back
# and brightening fourteen sets of crimsons that are right where they are.
#
# Gain keeps the hue and ambient does not, so the lift is a gain with only a
# little ambient under it, or the whole court comes out pink.
#
# And the gain is worked out per icon rather than set once. A single number
# brightened the throne correctly and blew the coffin to (230, 183, 182) -
# brighter than anything vanilla ships - because the fourteen pieces do not
# start from the same place: a limestone sarcophagus is pale and a black
# brazier is not. What they have to share is where they end up.
#
# The target is vanilla's own: the mean luminance over the middle quarter of
# quality_chair, stone_brazier and limestone_sarcophagus is 166, 160 and 138.
ART_TARGET = 158.0
ART_GAIN_MAX = 2.4
ART_AMBIENT = 10


def light(art):
    """Lifts the artwork to the brightness the game's own icons are drawn at."""
    rgb = np.asarray(art.convert("RGBA"), dtype=np.float32)
    solid = rgb[..., 3] > 128
    if not solid.any():
        return art

    luma = (0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2])[solid].mean()
    if luma <= 1.0:
        return art

    gain = min((ART_TARGET - ART_AMBIENT) / luma, ART_GAIN_MAX)
    gain = max(gain, 1.0)        # never darken: these are already too dark

    rgb[..., :3] = np.clip(rgb[..., :3] * gain + ART_AMBIENT, 0, 255)
    return Image.fromarray(rgb.astype(np.uint8), "RGBA")


# El halo cyan que llevan los iconos de edificio del juego. No esta pintado en
# el fondo - sigue la silueta del mueble, asi que sale de su propio alfa. Los
# valores salen de medirlo en quality_chair, gold_chest e iron_candle.
GLOW_RGB = (150, 236, 236)
GLOW_A = 0.34
GLOW_BLUR = 0.042          # en fracciones del lado
GLOW_GAIN = 2.3


def _glow(img, alpha):
    """Tine el fondo de cyan alrededor de la silueta, por detras del mueble."""
    n = img.size[0]
    g = Image.fromarray((alpha * 255).astype(np.uint8), "L")
    g = g.filter(ImageFilter.GaussianBlur(n * GLOW_BLUR))
    ga = np.clip(np.asarray(g, np.float32) / 255.0 * GLOW_GAIN, 0, 1) * GLOW_A
    arr = np.asarray(img, np.float32)
    arr[..., :3] = (arr[..., :3] * (1 - ga[..., None])
                    + np.array(GLOW_RGB, np.float32) * ga[..., None])
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def ground(draw_fn, out_size=128, art_scale=0.90):
    """Building icon: the object standing on the olive build-menu backdrop.

    The object is shrunk about its footprint so the backdrop stays visible
    around it, the way vanilla building icons frame their subject.
    """
    n = out_size * SS
    img = tile_backdrop(n, n)
    shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([n * 0.22, n * 0.68, n * 0.78, n * 0.86], fill=(0, 0, 0, 110))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(n * 0.03)))

    art = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(art), art, n)
    if art_scale != 1.0:
        m = int(n * art_scale)
        small = art.resize((m, m), Image.LANCZOS)
        art = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        art.paste(small, ((n - m) // 2, int(0.80 * n - 0.80 * m)), small)
    img = _glow(img, np.asarray(art, np.float32)[..., 3] / 255.0)
    img.alpha_composite(light(art))

    # Vanilla building icons have not one partly transparent pixel in them -
    # they are opaque cards. Ours came out with two thousand, from the
    # supersampled edges of the artwork, and a card that is slightly see-through
    # at the edges of everything on it reads as hazy next to one that is not.
    img.putalpha(255)
    return _down(img, out_size, out_size)


def crest(glyph, field=CREST_GREEN, glyph_rgb=(250, 250, 240), size=(86, 96)):
    """Effector bubble badge: gold rim, flat field, one readable glyph."""
    w, h = size[0] * SS, size[1] * SS
    mask = crest_mask(w, h)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    img.paste(Image.new("RGBA", (w, h), tuple(field) + (255,)), (0, 0), mask)

    inner = mask
    for _ in range(max(int(w * 0.012), 1)):
        inner = inner.filter(ImageFilter.MinFilter(3))
    rim = Image.composite(mask, Image.new("L", (w, h), 0), inner.point(lambda v: 255 - v))
    img.paste(Image.new("RGBA", (w, h), CREST_RIM + (255,)), (0, 0), rim)

    # The badge narrows below mid-height, so the glyph is shrunk and lifted to
    # sit inside the shape instead of being clipped by the taper.
    g = Image.new("L", (w, h), 0)
    glyph(ImageDraw.Draw(g), w, h)
    small = g.resize((int(w * 0.74), int(h * 0.74)), Image.LANCZOS)
    g = Image.new("L", (w, h), 0)
    g.paste(small, ((w - small.width) // 2, int(h * 0.06)))

    img.alpha_composite(emboss(g, glyph_rgb, (w, h), depth=int(w * 0.012), gloss=False))
    img.putalpha(Image.composite(img.split()[3], Image.new("L", (w, h), 0), mask))
    return _down(img, *size)
