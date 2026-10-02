"""Pull a vanilla mesh out of the game's bundles, as OBJ and as numbers.

Modelling anything for this game starts with the same question, and it has
never had a written answer: how big is the thing I am replacing. Guessing
produced a cat statue two and a half tiles deep, a throne that reads as a
chair, and a fortnight of "no se parece". So the reference comes out of the
game itself - the mesh vanilla uses, its exact bounding box, and where its
pivot sits inside it.

    python tools/models/extract_reference.py --list chair
    python tools/models/extract_reference.py quality_chair foxy_statue
    python tools/models/extract_reference.py --all-sizes > sizes.txt

OBJ files go to ASSESTS/Referencias/<name>.obj in the game's own units, with
the pivot left where the game has it - so a Blender scene that imports one and
models against it is working in the space the result will be drawn in. One
tile is one unit.

Nothing here writes into a mod. It only reads the shipped bundles.
"""

import argparse
import os
import sys

try:
    import UnityPy
except ImportError:  # pragma: no cover - the message is the point
    sys.exit("UnityPy is not installed. pip install UnityPy")


GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Going Medieval"
BUNDLES = os.path.join(GAME, "Going Medieval_Data", "StreamingAssets", "aa", "StandaloneWindows64")

HERE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(HERE, "ASSESTS", "Referencias")


def bundles():
    """Every asset bundle the game ships, biggest first.

    The one that holds the building meshes is defaultlocalgroup_assets_all,
    but naming it would break on the next patch: the hash in the filename
    changes whenever the group is rebuilt.
    """
    if not os.path.isdir(BUNDLES):
        sys.exit("cannot find the game's bundles at " + BUNDLES)

    found = [
        os.path.join(BUNDLES, name)
        for name in os.listdir(BUNDLES)
        if name.endswith(".bundle")
    ]
    return sorted(found, key=os.path.getsize, reverse=True)


def meshes(wanted=None):
    """Walk the bundles and hand back every mesh, or only the named ones.

    Yields (name, mesh). A name can appear more than once - LOD copies share a
    stem - so the caller decides what to do about duplicates.
    """
    for path in bundles():
        env = UnityPy.load(path)
        for obj in env.objects:
            if obj.type.name != "Mesh":
                continue

            data = obj.read()
            name = getattr(data, "m_Name", "")
            if not name:
                continue
            if wanted is not None and name not in wanted:
                continue

            yield name, data


def box(mesh):
    """Centre, size and base offset of a mesh, in game units.

    Read off m_LocalAABB rather than off the vertices: it is what Unity uses
    for Mesh.bounds, which is the number the plugin's own measuring code sees
    at runtime, so the two can be compared without a conversion in between.
    """
    aabb = mesh.m_LocalAABB
    centre = aabb.m_Center
    extent = aabb.m_Extent

    return {
        "centre": (centre.x, centre.y, centre.z),
        "size": (extent.x * 2, extent.y * 2, extent.z * 2),
        "base_y": centre.y - extent.y,
        "min": (centre.x - extent.x, centre.y - extent.y, centre.z - extent.z),
        "max": (centre.x + extent.x, centre.y + extent.y, centre.z + extent.z),
    }


def describe(name, mesh):
    b = box(mesh)
    cx, _, _ = b["centre"]
    sx, sy, sz = b["size"]

    return (
        f"{name:34s} size {sx:6.3f} x {sy:6.3f} x {sz:6.3f}"
        f"   pivot {-b['base_y']:+.3f} above base, {cx:+.3f} off centre in x"
    )


def write_obj(name, mesh, folder):
    """The mesh as an OBJ.

    <b>Through mesh.export(), not through m_Vertices.</b> That field is null on
    every mesh in this game: the positions live packed inside m_VertexData,
    with the stream layout in the channel descriptors, and reading it by hand
    means reimplementing Unity's vertex packing. UnityPy already does it, and
    an hour was lost writing "no vertex data" next to five perfectly good
    meshes before that was noticed.
    """
    text = mesh.export()
    if not text:
        return None

    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".obj")

    with open(path, "w", encoding="utf-8") as handle:
        handle.write(f"# {name} - reference geometry from Going Medieval\n")
        handle.write("# game units: one tile is one unit; pivot as the game has it\n")
        handle.write("# " + describe(name, mesh) + "\n")
        handle.write(text)

    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names", nargs="*", help="mesh names to extract")
    parser.add_argument("--list", metavar="SUBSTRING",
                        help="list matching mesh names instead of extracting")
    parser.add_argument("--all-sizes", action="store_true",
                        help="print the box of every mesh in the game")
    parser.add_argument("--out", default=OUT, help="where the OBJ files go")
    args = parser.parse_args()

    if args.list is not None:
        needle = args.list.lower()
        seen = sorted({name for name, _ in meshes() if needle in name.lower()})
        print("\n".join(seen) or "nothing matches " + args.list)
        return

    if args.all_sizes:
        for name, mesh in sorted(meshes(), key=lambda pair: pair[0]):
            print(describe(name, mesh))
        return

    if not args.names:
        parser.error("name a mesh, or use --list / --all-sizes")

    wanted = set(args.names)
    done = set()

    for name, mesh in meshes(wanted):
        if name in done:
            continue
        done.add(name)

        print(describe(name, mesh))
        path = write_obj(name, mesh, args.out)
        print("   ->", path if path else "no vertex data")

    for missing in sorted(wanted - done):
        print(f"{missing:34s} not found in any bundle")


if __name__ == "__main__":
    main()
