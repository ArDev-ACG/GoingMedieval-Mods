"""Lays every one of our assets out on a bench, ready to be edited.

    python tools/open_assets.py            # build the bench and print the index
    python tools/open_assets.py --open     # and open the folder in Explorer

The problem this solves is not that the assets are missing. It is that each one
lives wherever its own pipeline puts it - a mesh in ASSESTS/Modelos, a borrowed
mesh only inside the game's bundles, a texture in the installed mod, an icon in
another folder of the installed mod - so "I want to change the coffin" starts
with twenty minutes of looking for four files.

So this gathers them. For every building we ship it works out:

  - which mesh it wears, and whether that mesh is ours or borrowed;
  - which texture is painted on it, and who owns that texture;
  - which icon the build menu shows;
  - the one command that rebuilds each of those.

Then it copies the editable ones into ASSESTS/Taller/<building>/ and, for the
borrowed meshes, extracts the vanilla original as an .obj next to them, so the
folder can be opened in Blender or in an image editor and worked on directly.

**Nothing here writes into a mod.** The bench is a copy. Editing a file on the
bench changes nothing until the generator that owns it is re-run, and the index
says which generator that is for every single file - because a texture edited by
hand and then overwritten by its generator is a morning gone, and that has
already happened here once.
"""

import argparse
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODS = os.path.join(os.path.expanduser("~"), "Documents", "Foxy Voxel",
                    "Going Medieval", "Mods")
BENCH = os.path.join(HERE, "ASSESTS", "Taller")
MODELS = os.path.join(HERE, "ASSESTS", "Modelos")
REFERENCES = os.path.join(HERE, "ASSESTS", "Referencias")

OUR_MODS = ("VampireCourt", "CarrionAndPlague", "Gravedigger", "UndeadHorde")

# Which command rebuilds what. Printed next to every file on the bench, because
# the only thing worse than not finding an asset is editing the copy that some
# generator is about to overwrite.
REBUILD = {
    "mesh_ours": '"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" '
                 "--background --python tools/models/{script}",
    "mesh_vanilla": "no se toca: es una malla del juego. Para hacerla nuestra, "
                    "copiar tools/models/build_cat_statue.py como punto de partida",
    "texture_palette": "la escribe el mismo script de Blender que la malla "
                       "(paleta 2x2, no editar a mano)",
    "texture_generated": "python tools/textures/build_textures.py",
    "icon": "python tools/icons/build_icons.py",
    "json": "python tools/buildings/build_furniture.py",
    "bundle": '"C:/Program Files/Unity 2022.3.46f1/Editor/Unity.exe" -batchmode -quit '
              "-nographics -projectPath tools/unity/AldrichBundles "
              "-executeMethod Aldrich.BuildModBundles.Run -logFile -",
}

# The Blender script that owns each of our meshes.
MESH_SCRIPTS = {
    "aldrich_count_throne": "build_count_throne.py",
    "aldrich_cat_statue": "build_cat_statue.py",
}


def buildings():
    """Every building we ship, with the mod it belongs to."""
    found = []

    for mod in OUR_MODS:
        path = os.path.join(MODS, mod, "Data", "Models", "BaseBuildingRepository.json")
        if not os.path.exists(path):
            continue

        with open(path, encoding="utf-8-sig") as handle:
            for entry in json.load(handle)["repository"]:
                found.append((mod, entry))

    return found


def slots(entry):
    """The mesh and texture a building's first variation names."""
    mesh = texture = None

    for varlist in entry.get("variationLists") or []:
        for variation in varlist.get("variations") or []:
            for slot in variation.get("slots") or []:
                if slot.get("slotType") not in ("Mesh", "Texture"):
                    continue
                if slot.get("slot") == "baseMesh" and mesh is None:
                    mesh = slot.get("value")
                if slot.get("slot") == "albedo" and texture is None:
                    texture = slot.get("value") or None

    return mesh, texture


def find(mod, *parts):
    path = os.path.join(MODS, mod, *parts)
    return path if os.path.exists(path) else None


def copy(src, into, name=None):
    if not src or not os.path.exists(src):
        return None

    os.makedirs(into, exist_ok=True)
    dst = os.path.join(into, name or os.path.basename(src))
    shutil.copy2(src, dst)
    return dst


def extract(names):
    """Pulls the named vanilla meshes out of the game's bundles as .obj.

    Handed to tools/models/extract_reference.py rather than reimplemented: it
    already knows which bundle to look in and prints the measurements, and the
    measurement is half of why anybody opens a reference in the first place.
    """
    names = sorted({n for n in names if n and not n.startswith("aldrich_")})
    if not names:
        return

    missing = [n for n in names
               if not os.path.exists(os.path.join(REFERENCES, n + ".obj"))]
    if not missing:
        return

    print(f"  extrayendo {len(missing)} malla(s) de vanilla ...")
    subprocess.run(
        [sys.executable, os.path.join(HERE, "tools", "models", "extract_reference.py")]
        + missing,
        cwd=HERE, check=False)


def bench():
    rows = []
    found = buildings()

    extract([slots(e)[0] for _, e in found])

    for mod, entry in found:
        ident = entry["id"]
        mesh, texture = slots(entry)
        icon = entry.get("iconPath")

        into = os.path.join(BENCH, ident)
        os.makedirs(into, exist_ok=True)

        files = []

        # --- the mesh ------------------------------------------------------
        if mesh and mesh.startswith("aldrich_"):
            script = MESH_SCRIPTS.get(mesh, "?")
            copy(os.path.join(MODELS, mesh + ".obj"), into)
            copy(os.path.join(MODELS, mesh + ".fbx"), into)
            files.append((mesh + ".obj", "malla NUESTRA",
                          REBUILD["mesh_ours"].format(script=script)))
        elif mesh:
            copy(os.path.join(REFERENCES, mesh + ".obj"), into, mesh + "_VANILLA.obj")
            files.append((mesh + "_VANILLA.obj", "malla PRESTADA del juego",
                          REBUILD["mesh_vanilla"]))

        # --- the texture ---------------------------------------------------
        if texture:
            src = find(mod, "Data", "Textures", texture + ".png")
            copy(src, into)

            palette = src is not None and os.path.getsize(src) < 4096
            files.append((texture + ".png",
                          "paleta del modelo" if palette else "textura generada",
                          REBUILD["texture_palette"] if palette
                          else REBUILD["texture_generated"]))

        # --- the icon ------------------------------------------------------
        if icon:
            src = find(mod, "Data", "Sprites", icon + ".png")
            if src:
                copy(src, into)
                files.append((icon + ".png", "icono del menu", REBUILD["icon"]))
            else:
                files.append((icon, "icono DEL JUEGO (prestado)", REBUILD["icon"]))

        rows.append((mod, ident, entry.get("prefabID"), files))

    return rows


def index(rows):
    """Writes the bench's own README, which is the thing actually worth having."""
    lines = [
        "# El taller",
        "",
        "Copias de todo lo que se puede editar de nuestros muebles, una carpeta",
        "por mueble. **Esto es una copia**: editar aqui no cambia el juego. Al lado",
        "de cada fichero esta el comando que lo vuelve a generar, y ese comando es",
        "tambien el que **machaca** lo que edites a mano, asi que hay que mirarlo",
        "antes de tocar nada.",
        "",
        "Regenerar el taller: `python tools/open_assets.py`",
        "",
        "## Como se abre cada cosa",
        "",
        "| Fichero | Con que se abre | Que hacer |",
        "|---|---|---|",
        "| `*.obj` | Blender (`File > Import > Wavefront .obj`), o cualquier visor 3D | "
        "Es para **mirar y medir**. La malla de verdad la escribe el script de Python "
        "que dice el indice; se edita ese script y se vuelve a ejecutar. |",
        "| `*.fbx` | Unity o Blender | Lo que Unity mete en el bundle. Sale del `.obj` "
        "de al lado; no se edita a mano. |",
        "| `*_VANILLA.obj` | Blender | La malla que el mueble toma prestada del juego, "
        "sacada de sus bundles. Es la **referencia de tamano**: se importa en Blender y "
        "se modela contra ella. |",
        "| `*_albedo.png` de 64x64 | cualquier editor, pero **no lo hagas** | Es una "
        "paleta 2x2: cada cara de la malla apunta al centro de un cuadro. Pintar encima "
        "no pinta el mueble, cambia los cuatro colores. Estan en el script de Blender "
        "de esa malla. |",
        "| `*_albedo.png` grande | GIMP, Krita, Photoshop | Textura de verdad, sobre el "
        "desplegado de la malla prestada. La genera `tools/textures/build_textures.py` "
        "a partir de la textura de vanilla. |",
        "| `aldrich_icon_*.png` | GIMP, Krita, Photoshop | El icono del menu de "
        "construccion, 128x128. Lo dibuja `tools/icons/build_icons.py` con codigo, no a "
        "pincel: se edita la funcion `art_<mueble>` de ese fichero. |",
        "",
        "## Y para que sirve Unity",
        "",
        "Unity **no** se usa para modelar ni para pintar. Lo unico que hace aqui es",
        "empaquetar los `.fbx` en un bundle que el juego pueda cargar, y eso ya no",
        "necesita abrir la ventana:",
        "",
        "```",
        REBUILD["bundle"],
        "```",
        "",
        "Si quieres abrirlo a mano de todas formas, el proyecto esta en",
        "`tools/unity/AldrichBundles` y las mallas importadas en `Assets/Models`.",
        "",
        "## Los muebles",
        "",
    ]

    for mod, ident, prefab, files in sorted(rows, key=lambda r: (r[0], r[1])):
        lines.append(f"### {ident}")
        lines.append("")
        lines.append(f"Mod **{mod}**, sobre el prefab `{prefab}`. "
                     f"Carpeta: `ASSESTS/Taller/{ident}/`")
        lines.append("")

        if not files:
            lines.append("Sin ficheros propios todavia.")
            lines.append("")
            continue

        lines.append("| Fichero | Que es | Como se regenera |")
        lines.append("|---|---|---|")
        for name, what, how in files:
            lines.append(f"| `{name}` | {what} | `{how}` |")
        lines.append("")

    lines.append("La entrada JSON de todos ellos - coste, tamano, colisionador,")
    lines.append("nombre y descripcion - sale de `tools/buildings/build_furniture.py`,")
    lines.append(f"y se reescribe con `{REBUILD['json']}`.")
    lines.append("")

    path = os.path.join(BENCH, "README.md")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines))

    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--open", action="store_true",
                        help="abre la carpeta del taller al terminar")
    args = parser.parse_args()

    if os.path.isdir(BENCH):
        shutil.rmtree(BENCH)

    rows = bench()
    readme = index(rows)

    print()
    for mod, ident, _, files in sorted(rows, key=lambda r: (r[0], r[1])):
        print(f"{mod:18} {ident:22} {len(files)} fichero(s)")

    print()
    print(f"{len(rows)} muebles -> {BENCH}")
    print(f"indice -> {readme}")

    if args.open and sys.platform == "win32":
        os.startfile(BENCH)


if __name__ == "__main__":
    main()
