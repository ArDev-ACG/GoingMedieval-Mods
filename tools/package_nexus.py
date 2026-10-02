"""Builds the Nexus zip of each mod, the same shape as Gravedigger's.

    BepInEx/plugins/Aldrich<Mod>/Aldrich.<Mod>.dll
    BepInEx/plugins/Aldrich<Mod>/<Mod>/...      (ModInfo, Preview, Data)
    README.txt, LICENSE.txt

The data goes next to the DLL because Vortex only installs into BepInEx/plugins;
ModDataSync copies it to Documents on the first launch.

    python tools/package_nexus.py                      # the four below
    python tools/package_nexus.py VampireCourt         # just one

Reads the data from Documents\\...\\Mods\\<Mod> and the DLL from
src/Mods/<Mod>/bin/Release, so build first. Leaves out every *.bak* and the
CHANGELOG. Writes Release/Aldrich<Mod>/ and a copy of the zip in the Nexus folder.
"""

import json
import os
import shutil
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODS = os.path.expandvars(r"%USERPROFILE%\Documents\Foxy Voxel\Going Medieval\Mods")

# carpeta del mod -> carpeta de Nexus
PACKAGES = {
    "CarrionAndPlague": "Nexus - Aldrich Carrion",
    "UndeadHorde": "Nexus - Aldrich Risen",
    "VampireCourt": "Nexus - Aldrich Vampire",
    "XenomorphRunner": "Nexus - Aldrich Runners",
}
SKIP_FILES = {"CHANGELOG.md", "__folder_managed_by_vortex"}

LICENSE = """Copyright (c) 2026 Aldrich. All rights reserved.

You may download this mod and use it in your own games.

You may NOT, without written permission from the author:
- re-upload or redistribute this mod, in whole or in part, on any site
  (including Nexus Mods, Steam Workshop, mod.io or others);
- include it in modpacks or collections hosted outside Nexus Mods;
- publish modified versions, translations or ports of it;
- sell it or put it behind any paywall.

The only official download is the author's page on Nexus Mods.
"""


def skipped(rel):
    parts = rel.replace("\\", "/").split("/")
    return any(".bak" in p for p in parts) or parts[-1] in SKIP_FILES


def package(mod, nexus_dir):
    src = os.path.join(MODS, mod)
    info = json.load(open(os.path.join(src, "ModInfo.json"), encoding="utf-8"))
    version = info["modVersion"]
    dll = os.path.join(ROOT, "src", "Mods", mod, "bin", "Release", f"Aldrich.{mod}.dll")
    readme = os.path.join(ROOT, nexus_dir, "README.txt")
    for need in (dll, readme):
        if not os.path.exists(need):
            sys.exit(f"{mod}: falta {need}")

    release = os.path.join(ROOT, "Release", f"Aldrich{mod}")
    stage = os.path.join(release, "stage")
    shutil.rmtree(stage, ignore_errors=True)
    plugin = os.path.join(stage, "BepInEx", "plugins", f"Aldrich{mod}")

    os.makedirs(plugin)
    shutil.copy2(dll, plugin)
    for dirpath, _, files in os.walk(src):
        for f in files:
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, src)
            if skipped(rel):
                continue
            to = os.path.join(plugin, mod, rel)
            os.makedirs(os.path.dirname(to), exist_ok=True)
            shutil.copy2(full, to)
    shutil.copy2(readme, os.path.join(stage, "README.txt"))
    with open(os.path.join(stage, "LICENSE.txt"), "w", encoding="utf-8") as fh:
        fh.write(LICENSE)

    # Un catalogo por mod: AddressableModManager coge el primer .json que vea.
    aa = os.path.join(plugin, mod, "Data", "AddressableAssets")
    if os.path.isdir(aa):
        jsons = [f for f in os.listdir(aa) if f.endswith(".json")]
        assert jsons == ["catalog.json"], f"{mod}: AddressableAssets lleva {jsons}"

    zip_path = os.path.join(release, f"Aldrich{mod}_{version}.zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for dirpath, _, files in os.walk(stage):
            for f in sorted(files):
                full = os.path.join(dirpath, f)
                z.write(full, os.path.relpath(full, stage).replace("\\", "/"))
    shutil.copy2(zip_path, os.path.join(ROOT, nexus_dir))
    shutil.copy2(os.path.join(src, "Preview.png"), os.path.join(ROOT, nexus_dir, "Portada.png"))

    with zipfile.ZipFile(zip_path) as z:
        names = z.namelist()
    print(f"{mod:18} {os.path.basename(zip_path)}  {len(names)} ficheros")


def main():
    wanted = sys.argv[1:] or list(PACKAGES)
    for mod in wanted:
        package(mod, PACKAGES[mod])


if __name__ == "__main__":
    main()
