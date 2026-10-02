"""Repaints vanilla mesh textures and drops them into a mod's Data/Textures.

The buildings' `albedo` slot is an addressable texture name, not a file, so a
mod cannot normally supply one. `ModTextures` (src/GMPlugins) adds the missing
half: it registers every PNG found in <Mod>/Data/Textures with the game's
TextureRepository under its own file name, which is what lets a JSON slot say
"aldrich_cat_statue_albedo" and get this file back.

Only the building that names the texture uses it, so repainting here cannot
touch any vanilla mesh.

    python tools/textures/build_textures.py            # write into the mods
    python tools/textures/build_textures.py <outdir>   # dry run into one folder
"""

import glob
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
BUNDLES = (r"C:\Program Files (x86)\Steam\steamapps\common\Going Medieval"
           r"\Going Medieval_Data\StreamingAssets\aa\StandaloneWindows64")
PREFIX = "aldrich_"

# Limestone: the statue is built out of limestone blocks, so it should end up
# the colour of one.
STONE_DARK = np.array([104, 101, 93], float)
STONE_LIGHT = np.array([216, 212, 199], float)


def vanilla_texture(name, size=None):
    """Pulls one Texture2D out of the game's bundles by exact name.

    Names are case sensitive on purpose: the cat has a 'Cat' (the mesh's UV
    albedo) and a 'cat' (the 128x128 resource icon), and only the first is
    wanted here. Where the icon and the albedo share a name outright - the
    apothecary bench ships both as "apothecary_bench" - `size` picks between
    them; the albedo is the larger of the two.
    """
    for candidate, obj in _index().get(name, []):
        if size is None or candidate == tuple(size):
            return obj.read().image.convert("RGBA")

    raise SystemExit(f"texture not found in the bundles: {name} {size or ''}")


_INDEX = {}


def _index():
    """Every Texture2D in the bundles, by name, read once.

    The bundles take the best part of a minute to walk and there are a dozen
    textures to build, so walking them once per texture is a dozen minutes of
    doing the same work. Only the *metadata* is read here - name and size, off
    the object header - and the pixels are decoded later for the two or three
    that are actually wanted.
    """
    if _INDEX:
        return _INDEX

    import UnityPy

    for bundle in glob.glob(os.path.join(BUNDLES, "*.bundle")):
        try:
            env = UnityPy.load(bundle)
        except Exception:
            continue

        for obj in env.objects:
            if obj.type.name != "Texture2D":
                continue
            try:
                data = obj.read()
            except Exception:
                continue

            name = getattr(data, "m_Name", "")
            if not name:
                continue

            size = (getattr(data, "m_Width", 0), getattr(data, "m_Height", 0))
            _INDEX.setdefault(name, []).append((size, obj))

    # Biggest first, so a name asked for without a size gets the albedo rather
    # than the resource icon that happens to share it.
    for entries in _INDEX.values():
        entries.sort(key=lambda e: e[0][0] * e[0][1], reverse=True)

    return _INDEX


def to_limestone(img, grain=3.5, seed=11):
    """Fur -> carved limestone, keeping the original's light and shadow.

    Everything the artist painted (the tabby stripes, the shading down the
    flanks, the modelling around the muzzle) survives as luminance, so the
    carving still reads. What does not survive is colour: the pink nose, ears
    and paw pads are flesh, and flesh on a statue looks like a mistake, so the
    saturated pixels are pushed the other way and come out as the deepest
    chisel shadows instead.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    lum = rgb @ np.array([0.299, 0.587, 0.114])
    sat = (rgb.max(axis=-1) - rgb.min(axis=-1)) / 255.0

    # The cat's two big pale patches own most of the luminance range, so a
    # plain min/max stretch crushes everything else to one flat grey. Split the
    # signal instead: a gentle base tone from the 20-80 band, plus the fine
    # detail the blur throws away, put back at three times the strength. That
    # is what keeps the stripes and the muzzle modelling visible as chisel work.
    blurred = np.array(Image.fromarray(lum.astype(np.uint8), "L")
                       .filter(ImageFilter.GaussianBlur(3.0)), dtype=np.float32)
    detail = (lum - blurred) / 255.0

    lo, hi = np.percentile(lum, 20), np.percentile(lum, 80)
    base = np.clip((lum - lo) / max(hi - lo, 1.0), 0, 1)

    t = 0.30 + 0.52 * base + 3.0 * detail
    t = np.clip(t - sat * 0.42, 0.06, 0.94)   # anything coloured sinks into shadow

    out = STONE_DARK + (STONE_LIGHT - STONE_DARK) * t[..., None]

    rng = np.random.default_rng(seed)
    out += rng.normal(0, grain, out.shape[:2])[..., None]   # tool marks

    return Image.fromarray(
        np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def to_bloodstained_stone(img, seed=23):
    """The apothecary bench, rebuilt in stone and used for something else.

    Same limestone conversion as the statue, then a dark red wash weighted by
    how deep each pixel already sits: the crevices the original texture painted
    are exactly where blood would collect, so staining by inverse luminance puts
    it in the cuts and leaves the flat top clean.
    """
    stone = to_limestone(img, grain=2.5, seed=seed)
    arr = np.array(stone).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    lum = rgb @ np.array([0.299, 0.587, 0.114])
    pooled = np.clip(1.0 - lum / 170.0, 0, 1) ** 2.4

    blood = np.array([104, 16, 18], float)
    out = rgb * (1 - pooled[..., None] * 0.8) + blood[None, None, :] * (pooled[..., None] * 0.8)
    return Image.fromarray(
        np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def to_blood_altar_stone(img, seed=23):
    """The butchering table, which is already a slab things get bled on.

    Three passes, and the order of the first two is the whole trick:

    1. <b>Remember where the red was.</b> The vanilla texture already paints
       splatter across the top of the table - it is a butcher's bench. That is
       exactly the blood an altar wants, so it is measured off the *original*
       (how far red runs ahead of the other two channels) before anything is
       repainted, or the next step would erase it.
    2. <b>Stone, not wood.</b> The same limestone conversion the statue uses,
       then darker and neutral: this belongs in a pagan chapel, not a kitchen.
    3. <b>Put the blood back, and add what ran into the cuts.</b> The splatter
       mask from step 1, unioned with a crevice mask - the difference against a
       blurred copy, which finds joints and shadowed lips rather than "anything
       dark". Staining by plain luminance was the first attempt and it turned
       the whole slab a flat maroon.
    """
    # Hue, not "how much red". Wood is orange - red still leads the other two
    # channels across the whole plank - so measuring redness by channel
    # difference marks the entire table as blood. Hue separates them: the
    # splatter sits under 9 deg and heavily saturated, the wood above it.
    hsv = np.array(img.convert("HSV")).astype(np.float32)
    hue, sat = hsv[..., 0] * 360.0 / 255.0, hsv[..., 1]
    is_red = np.minimum(np.clip((12.0 - hue) / 6.0, 0, 1),
                        np.clip((sat - 88.0) / 50.0, 0, 1))

    # Red hue and saturation alone still catch the frame, which is warm wood of
    # about the same colour. Blood is also *darker* than what it sits on, so a
    # third gate on brightness against the texture's own average separates the
    # stain from the timber it is painted over.
    src_lum = np.array(img.convert("L")).astype(np.float32)
    is_dark = np.clip((src_lum.mean() * 0.92 - src_lum) / 22.0, 0, 1)

    splatter = (is_red * is_dark) ** 0.85

    stone = to_limestone(img, grain=2.5, seed=seed)
    arr = np.array(stone).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    lum = rgb @ np.array([0.299, 0.587, 0.114])
    blurred = np.array(
        Image.fromarray(lum.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.5)),
        dtype=np.float32)
    crevice = np.clip((blurred - lum) / 58.0, 0, 1) ** 1.7

    rgb = rgb * 0.62
    grey = (rgb @ np.array([0.299, 0.587, 0.114]))[..., None]
    rgb = grey * np.array([0.98, 1.00, 1.05])[None, None, :]

    blood = np.array([74, 12, 14], float)
    wash = np.maximum(crevice * 0.68, splatter * 0.92)[..., None]
    out = rgb * (1 - wash) + blood[None, None, :] * wash

    return Image.fromarray(
        np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def to_crimson_circle(img, seed=31):
    """The pagan ring, cut over.

    A household that turns does not quarry a new circle - it takes the one
    already in the chapel and fills the channel. So this starts from the pagan
    shrine's own texture and does two things to it:

    1. <b>Darkens the stone to near-black.</b> Neutral, not blue: the ring has
       to read as old stone in a dim chapel, and a cold tint at this value
       looks like slate roofing.
    2. <b>Fills the carving.</b> The engraved lines are the only place the
       texture sits much darker than its surroundings, so the same crevice mask
       the altar uses - the difference against a blurred copy - finds them
       exactly, and they come back a deep, wet red. Staining by plain luminance
       was tried on the altar and turned the whole slab maroon; no reason to
       make that mistake twice.
    """
    stone = to_limestone(img, grain=2.0, seed=seed)
    arr = np.array(stone).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    lum = rgb @ np.array([0.299, 0.587, 0.114])
    blurred = np.array(
        Image.fromarray(lum.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.0)),
        dtype=np.float32)
    carved = np.clip((blurred - lum) / 40.0, 0, 1) ** 1.35

    # <b>The stone itself is red now, not just the carving.</b> The first
    # version painted a near-black neutral ring and put the blood only in the
    # engraved lines, which is defensible on paper and wrong on screen: eleven
    # per cent of the pixels were reddish and the mean colour was (75, 66, 66),
    # a slate disc with a few pink scratches - "sale gris con rojo", word for
    # word. A circle called carmesi has to read carmesi from across the room, so
    # the tint goes on the body of the stone and the carving stays darker and
    # wetter than it, which is what keeps the relief visible.
    grey = (rgb @ np.array([0.299, 0.587, 0.114]))[..., None] * 0.54
    rgb = grey * np.array([1.62, 0.44, 0.46])[None, None, :]

    blood = np.array([104, 8, 12], float)
    wash = (carved * 0.95)[..., None]
    out = rgb * (1 - wash) + blood[None, None, :] * wash

    return Image.fromarray(
        np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def _crevice(rgb, blur=2.5, scale=58.0, gamma=1.7):
    """Where the texture is darker than its own blurred copy.

    The joints, the shadowed lips, the carved lines - which is exactly where
    anything spilled would collect and where soot would stay. Staining by plain
    luminance instead was the first attempt on the altar and it turned the
    whole slab a flat maroon.
    """
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    blurred = np.array(
        Image.fromarray(lum.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(blur)),
        dtype=np.float32)

    return np.clip((blurred - lum) / scale, 0, 1) ** gamma


def blackened(img, keep=0.40, tint=(1.02, 1.00, 0.99), grain=2.0, seed=17):
    """Metal or wood burnt down to nearly black, with the modelling intact.

    Everything the artist painted survives as luminance and only the colour
    goes, so a chair still reads as a chair with all its rivets - it has just
    stopped being the colour of new iron. `keep` is how much of the original
    brightness stays; below about 0.25 the shape disappears into the shadow it
    is standing in.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    grey = (rgb @ np.array([0.299, 0.587, 0.114]))[..., None] * keep
    out = grey * np.array(tint)[None, None, :]

    rng = np.random.default_rng(seed)
    out += rng.normal(0, grain, out.shape[:2])[..., None]

    return Image.fromarray(np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def stained(img, blood=(88, 12, 16), wash=0.72, blur=2.5, seed=19):
    """Something dark red ran over this and dried in the low places.

    The same crevice mask the altar and the circle use, so the stain lands in
    the cuts and the joins and leaves the flat, wiped surfaces alone. That is
    the difference between a table that has been used for something and a table
    that has been painted red.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    mask = (_crevice(rgb, blur=blur) * wash)[..., None]
    out = rgb * (1 - mask) + np.array(blood, float)[None, None, :] * mask

    rng = np.random.default_rng(seed)
    out += rng.normal(0, 1.5, out.shape[:2])[..., None]

    return Image.fromarray(np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def lacquered(img, red=(124, 20, 24), black=(17, 15, 17), pivot=0.55,
              seed=23):
    """Black in the shadow, blood red in the light - the house colours.

    <b>Why `blackened` plus `stained` was not enough.</b> Together they give a
    grey object with red in its cracks, and a crack is a thin thing: on a
    wiped stone table or a plain iron chest there is almost no crevice for the
    stain to land in, so nine of the ten court textures came out the same dark
    grey. Reported, exactly, as "ponerles colores rojos y negros, rojo y
    negro".

    So the colour is put on the whole surface instead of only in its cuts, and
    the modelling still decides where: luminance below `pivot` runs down into
    black, above it up into red. That keeps every rivet, plank and carving the
    vanilla artist painted - what changes is only which two colours the light
    and the shade are made of.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    lum = (rgb @ np.array([0.299, 0.587, 0.114])) / 255.0

    # <b>The pivot is a percentile, not a brightness.</b> Fixed at 0.42 the
    # first time, and every vanilla texture here is painted brighter than
    # that, so everything landed in the red half and the set came back all
    # red - the opposite failure to the all-grey one. Asking the texture where
    # its own middle is puts `pivot` of it in the black half whatever the
    # original was painted like, which is what "rojo y negro" means: both.
    cut = float(np.percentile(lum, pivot * 100.0))

    # Two ramps meeting at the cut, so the darks do not simply get redder
    # and the whole thing turn to one flat maroon.
    # Curved, not straight. A linear ramp puts half the black half already
    # halfway to red, so the "black" came out maroon and the whole sheet read
    # as one colour. The square keeps the shadow black until close to the cut.
    low = np.clip(lum / max(cut, 1e-3), 0, 1)[..., None] ** 2.4
    high = np.clip((lum - cut) / max(1.0 - cut, 1e-3), 0, 1)[..., None]

    black_a = np.array(black, float)[None, None, :]
    red_a = np.array(red, float)[None, None, :]

    # black -> red across the bottom half, red -> a lit red across the top,
    # rather than red -> white, which reads as plastic.
    out = black_a * (1 - low) + red_a * low
    out = out * (1 - high) + (red_a * 1.45 + 18.0) * high

    rng = np.random.default_rng(seed)
    out += rng.normal(0, 2.0, out.shape[:2])[..., None]

    return Image.fromarray(np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def corpse_flesh(img, seed=29):
    """The scarecrow, with a person on the pole instead of a sack of straw.

    Three things have to happen and only one of them is colour:

    1. **The straw has to stop being straw.** Straw is the most saturated thing
       in the texture and it is warm yellow; skin days old is neither. So
       saturation is crushed first, and hard, or the result is a corn dolly
       with a grey face.
    2. **The flesh goes green-grey**, on the same axis as the Risen: pallid
       within hours, then green as the gut bacteria turn its haemoglobin. A
       shade darker than the walkers, because this one has been out in the
       weather and nobody has taken it down.
    3. **The blood goes where the body meets the wood.** The crevice mask finds
       the seams and the lashings, which on this mesh is where the stake goes
       through, so the darkest red ends up where a stake would actually be.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]

    # The sack is already painted flesh-pink - it is a body made of cloth - so
    # the light and shade on it are worth keeping in full. The first attempt
    # rebuilt the whole texture from luminance and came out a black rag: dead
    # skin is pale, not dark. So brightness is left alone and only the hue is
    # taken somewhere else.
    lum = (rgb @ np.array([0.299, 0.587, 0.114]))[..., None]

    # Green-grey, the same axis as the Risen: pallid within hours, then green
    # as the gut turns its haemoglobin. A little of the original is mixed back
    # so the seams and the weave do not flatten out.
    dead = lum * np.array([0.90, 0.98, 0.84])[None, None, :]
    out = dead * 0.82 + rgb * 0.18
    out *= 0.94

    # And the blood where the body meets the wood: the crevice mask finds the
    # seams and the lashings, which on this mesh is where the stake goes
    # through.
    mask = (_crevice(rgb, blur=2.0, scale=46.0, gamma=1.25) * 0.7)[..., None]
    out = out * (1 - mask) + np.array([86, 16, 18], float)[None, None, :] * mask

    rng = np.random.default_rng(seed)
    out += rng.normal(0, 2.5, out.shape[:2])[..., None]

    return Image.fromarray(np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def waxed_garment(img, base=(34, 29, 25), span=(62, 54, 46), belt=(98, 70, 44), seed=37):
    """A body garment repainted as waxed leather, masks left alone.

    A body texture is not only cloth. Pure magenta is the skin mask and pure
    green the hair mask - the shader swaps them for the settler's own skin
    and hair colour - and the transparent pixels are where the body shows
    through. `blackened` greys all of it, and a settler in the coat would
    come out with grey skin. So those three are kept exactly, and only the
    cloth is waxed: its brightness becomes a dark leather ramp from `base`
    to `base + span`, stitches and folds intact, and the red cords and belt
    of the winter clothes turn to plain leather.
    """
    arr = np.array(img).astype(np.float32)
    rgb, alpha = arr[..., :3], arr[..., 3:]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    skin = (r > 90) & (g < 50) & (b > 70)
    hair = (g > 70) & (r < 50) & (b < 50) & (g > 2.5 * (r + b + 1))
    clear = alpha[..., 0] < 8
    keep = skin | hair | clear

    lum = (rgb @ np.array([0.299, 0.587, 0.114])) / 255.0
    out = np.array(base, float)[None, None, :] + lum[..., None] * np.array(span, float)[None, None, :]

    cord = (r > 150) & (g < 90) & (b < 80) & ~keep
    out[cord] = np.array(belt, float)

    rng = np.random.default_rng(seed)
    out += rng.normal(0, 1.8, out.shape[:2])[..., None]

    out[keep] = rgb[keep]
    return Image.fromarray(np.clip(np.dstack([out, alpha]), 0, 255).astype(np.uint8), "RGBA")


def long_coat(img, seed=41):
    """La gabardina de medico de la peste: un abrigo largo de cuero encerado.

    `waxed_garment` oscurecia la ropa de invierno y se seguia leyendo como
    ropa de invierno: camisa, cinturon y pantalon, tres piezas. Lo que se
    pidio el 25 es que se lea como **una** prenda de cuello a rodilla. En la
    plantilla del cuerpo (512 px) eso es pintar igual cuatro zonas que vanilla
    pinta distinto - torso (arriba), cadera y muslos (centro), faldon (la
    franja de abajo) y mangas (columna izquierda) - y dibujar encima lo que las
    une: la abertura del abrigo por el centro, una botonadura de laton de
    arriba abajo y el bajo mas oscuro. Antebrazo y bota, cuero casi negro:
    guantes y botas. Las mascaras (magenta, verde, transparente) no se tocan.
    """
    out = waxed_garment(img, base=(26, 23, 21), span=(44, 38, 33), belt=(30, 26, 23), seed=seed)
    arr = np.array(out).astype(np.float32)
    src = np.array(img).astype(np.float32)
    r, g, b, a = src[..., 0], src[..., 1], src[..., 2], src[..., 3]
    skin = (r > 90) & (g < 50) & (b > 70)
    hair = (g > 70) & (r < 50) & (b < 50) & (g > 2.5 * (r + b + 1))
    cloth = (a > 8) & ~skin & ~hair
    k = img.size[0] / 512.0
    h, w = cloth.shape
    yy, xx = np.mgrid[0:h, 0:w]

    def paint(mask, colour, blend=1.0):
        m = (mask & cloth)[..., None] * blend
        arr[..., :3] = arr[..., :3] * (1 - m) + np.array(colour, float)[None, None, :] * m

    # Guantes y botas: la columna de la izquierda por debajo del codo.
    paint((xx < 110 * k) & (yy > 188 * k), (17, 15, 14), 0.85)

    # La abertura del abrigo, del cuello al bajo, por el centro del cuerpo.
    centre = 257 * k
    paint((np.abs(xx - centre) < 2.2 * k) & (yy > 20 * k) & (yy < 465 * k) & (xx > 110 * k), (9, 8, 8))

    # El bajo: los ultimos pixeles del faldon, mas oscuros.
    hem = np.zeros_like(cloth)
    for x in range(int(110 * k), int(420 * k)):
        col = np.where(cloth[int(340 * k):, x])[0]
        if len(col):
            bottom = int(340 * k) + col.max()
            hem[max(0, bottom - int(9 * k)):bottom + 1, x] = True
    paint(hem, (12, 11, 10), 0.9)

    # La botonadura: laton, a un lado de la abertura.
    for y in list(range(int(34 * k), int(172 * k), int(20 * k))) + \
            list(range(int(196 * k), int(330 * k), int(24 * k))):
        dot = (xx - (centre + 7 * k)) ** 2 + (yy - y) ** 2 <= (3.2 * k) ** 2
        paint(dot, (150, 116, 52))
        shine = (xx - (centre + 6 * k)) ** 2 + (yy - (y - 1 * k)) ** 2 <= (1.2 * k) ** 2
        paint(shine, (205, 172, 96))

    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


TEXTURES = [
    # **Ninguna pieza de Vampire Court esta ya aqui, y es a proposito.** Las
    # trece tienen malla propia desde hoy - tools/models/build_court.py - y su
    # albedo es la paleta 2x2 a la que apuntan sus UV, escrita al lado de la
    # malla. Lo mismo vale para el trono y la estatua de gato desde antes.
    #
    # Regenerar cualquiera de ellas desde una textura de vanilla la
    # sobreescribiria con una imagen de 256 px de la que las UV leen cuatro
    # pixeles: exactamente lo que le paso al trono a las 23:18, cuando esta
    # lista lo devolvio a una silla embarrada. Una textura pertenece a quien
    # manda en las UV, y aqui solo queda lo que viste una malla de vanilla.

    # --- the plague apparel -------------------------------------------------
    #
    # Both wear a vanilla mesh, so both repaint that mesh's own texture and
    # nothing else. Waxed leather, not black cloth: the point of the thing is
    # that it is sealed, and wax is what reads as sealed at this size.
    # La careta ya no esta aqui: desde el 25 lleva malla propia
    # (tools/models/build_plague_mask.py) y su albedo es la paleta que escribe
    # ese script. Regenerarla desde el mouthpiece le quitaria el color.
    ("CarrionAndPlague", "plague_hood_albedo",
     lambda: blackened(vanilla_texture("cloth_hood_difuse", (128, 128)), keep=0.34,
                       tint=(1.06, 1.00, 0.94))),

    # The coat is a body garment, and a body garment in this game is not a
    # mesh: it is a texture laid over the body, one per body type
    # (Resources.json `equippedTexturePaths`). So these are the winter
    # clothes' own two textures - same UV as the body - waxed dark.
    ("CarrionAndPlague", "plague_coat_male",
     lambda: long_coat(vanilla_texture("male_good_winter_clothes"))),
    ("CarrionAndPlague", "plague_coat_female",
     lambda: long_coat(vanilla_texture("female_good_winter_clothes"))),
]


def main():
    dry = sys.argv[1] if len(sys.argv) > 1 else None
    for mod, name, build in TEXTURES:
        img = build()
        out = dry or os.path.join(MODS, mod, "Data", "Textures")
        os.makedirs(out, exist_ok=True)
        path = os.path.join(out, PREFIX + name + ".png")
        img.save(path)
        print(f"{mod:18} {PREFIX + name:26} {img.size[0]}x{img.size[1]}")
    print(f"\n{len(TEXTURES)} texturas -> {dry or MODS}")


if __name__ == "__main__":
    main()
