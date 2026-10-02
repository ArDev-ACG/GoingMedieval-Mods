"""Builds our furniture out of vanilla furniture.

The Crimson Circle taught the rule this whole file rests on: **`prefabID`
decides what a building does and the mesh decides what it looks like, and the
two do not have to come from the same building**. So a coffin is a bed's prefab
wearing a sarcophagus, and a throne is a chair with the iron painted out.

What is *not* optional is everything else in the entry. A building blueprint has
around sixty fields - collider offsets, pile meshes, sorting groups, decompose
modifiers, the component ids each prefab silently requires - and one missing one
is a NullReferenceException every frame (`rugComponentID`, 2623 times, the night
of the 2nd). Hand-writing eleven of those is eleven chances to make that mistake.

So each of ours starts as a **copy of the vanilla entry it borrows from** and
only says what changes. Everything not mentioned is whatever Foxy Voxel put
there, which is by definition the set of fields that prefab needs.

    python tools/buildings/build_furniture.py          # write into the mods
    python tools/buildings/build_furniture.py <dir>    # dry run into one folder

Re-running is safe: entries this file owns are replaced, anything else already
in the mod's repository is left where it is.
"""

import copy
import json
import os
import sys

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
VANILLA = (r"C:\Program Files (x86)\Steam\steamapps\common\Going Medieval"
           r"\Going Medieval_Data\StreamingAssets\Constructables"
           r"\BaseBuildingRepository.json")

# What comes back when the building is torn down, as a fraction of what it cost.
# The same numbers the two hand-written entries already use.
RETURN_ON_DESTROY = 0.6
RETURN_ON_DECONSTRUCT = 0.3


# --- the pieces -------------------------------------------------------------
#
# source   the vanilla entry copied wholesale
# icon     every one of ours now, drawn by tools/icons/build_icons.py under
#          the name aldrich_icon_<id>. It used to be the source's own icon on
#          purpose - a right borrowed icon beats a wrong custom one - but a
#          menu row of vanilla chairs and wells is not a mod anybody can read
# returns  which of the materials come back when it is pulled down; all of
#          them unless said otherwise
# slots    mesh and texture slots to overwrite, by slot name
# tint     shader colours to overwrite, by variable name
SPECS = [
    {
        "mod": "VampireCourt",
        "id": "count_coffin",
        "source": "hay_wood_bed",
        "en": ("Coffin",
               "A bed for someone who does not want the morning to find them. "
               "Stone lid, stone sides, and room enough to lie still in."),
        "es": ("Ataud",
               "Una cama para quien no quiere que la manana lo encuentre. Tapa "
               "de piedra, lados de piedra, y sitio justo para estarse quieto."),
        "materials": {"limestone_block": 30, "wood": 10},
        "set": {"buildTime": 70, "minBuildSkillRequired": 8, "wealthPoints": 70,
                "beautyInput": 12},
        # Malla propia desde hoy. Era el sarcofago de vanilla, que es la
        # misma caja que lleva la cripta: dos muebles distintos con el mismo
        # aspecto en el mismo sitio. El nuestro tiene la tapa abierta.
        "slots": {"baseMesh": "aldrich_count_coffin",
                  "albedo": "aldrich_count_coffin_albedo",
                  "reflection": "base_texture_reflection"},
    },
    {
        "mod": "VampireCourt",
        "id": "court_banner",
        "source": "linen_banner",
        "en": ("Court Banner",
               "The house colours, for a house that keeps different hours."),
        "es": ("Estandarte de la corte",
               "Los colores de la casa, para una casa con otros horarios."),
        "materials": {"linen_cloth": 4, "wood": 10},
        "set": {"wealthPoints": 24, "beautyInput": 22},
        # El tinte se queda: la malla nuestra trae el pano en su paleta, pero
        # las diez variaciones de forma que el jugador puede elegir siguen
        # siendo de vanilla hasta que alguien modele las diez.
        "slots": {"baseMesh": "aldrich_court_banner",
                  "albedo": "aldrich_court_banner_albedo",
                  "reflection": "base_texture_reflection"},
        "tint": {"_ClothTint": "#5E1016"},
    },
    {
        "mod": "VampireCourt",
        "id": "court_banner_wall",
        "source": "linen_banner_wall",
        "en": ("Court Banner (wall)",
               "The same colours, hung where the hall can see them."),
        "es": ("Estandarte de la corte (pared)",
               "Los mismos colores, colgados donde el salon los vea."),
        "materials": {"linen_cloth": 4, "wood": 10},
        "set": {"wealthPoints": 24, "beautyInput": 22},
        "slots": {"baseMesh": "aldrich_court_banner_wall",
                  "albedo": "aldrich_court_banner_wall_albedo",
                  "reflection": "base_texture_reflection"},
        "tint": {"_ClothTint": "#5E1016"},
    },
    {
        "mod": "VampireCourt",
        "id": "count_throne",
        # quality_chair_linen, not iron_chair. `quality_chair` is the *mesh*
        # name, not a building id - the building that wears it is this one -
        # and iron_chair's albedo is `base_texture_png`, the shared swatch
        # atlas, so darkening it darkened every building that shares it and
        # gave the flat black stool that was reported.
        "source": "quality_chair_linen",
        "en": ("The Count's Throne",
               "Iron burnt black. Nobody else sits here, and nobody has to be "
               "told so."),
        "es": ("Trono del Conde",
               "Hierro quemado hasta el negro. Aqui no se sienta nadie mas, y "
               "no hace falta decirlo."),
        "materials": {"iron_ingot": 25, "gold_ingot": 4},
        "set": {"buildTime": 45, "minBuildSkillRequired": 8, "wealthPoints": 90,
                "beautyInput": 18, "iconPath": "aldrich_icon_count_throne",
                # Measured off the mesh by tools/models/build_count_throne.py
                # and kept in step with it by hand: HandleSelectionBoxCollider
                # reads this, not the model, and it runs before the model is
                # built - so a collider left at the borrowed chair's size is a
                # selection box the wrong shape whatever the mesh does.
                "boxColliderSettings": {
                    "centerOffset": {"x": 0, "y": 1.1125, "z": 0},
                    "sizeOffset": {"x": 0.94, "y": 2.225, "z": 0.96}}},
        "slots": {"baseMesh": "aldrich_count_throne",
                  "albedo": "aldrich_count_throne_albedo",
                  "reflection": "base_texture_reflection"},
    },
    {
        "mod": "VampireCourt",
        "id": "crimson_candle",
        "source": "iron_candle",
        "en": ("Crimson Candle",
               "Tallow with something else stirred through it. It burns low "
               "and it burns red."),
        "es": ("Candelabro carmesi",
               "Sebo con algo mas mezclado. Arde bajo y arde rojo."),
        "materials": {"iron_ingot": 5, "wax": 4},
        "set": {"wealthPoints": 26, "beautyInput": 16},
        "slots": {"baseMesh": "aldrich_crimson_candle",
                  "albedo": "aldrich_crimson_candle_albedo",
                  "reflection": "base_texture_reflection"},
        # Iron with wax down it. Copied from `iron_candle` it arrived at full
        # metal, and a fully metallic surface shows its reflection instead of
        # its paint.
        "shader": {"_Metallic": 0.45, "_Smoothness": 0.35},
    },
    {
        "mod": "VampireCourt",
        "id": "blood_brazier",
        "source": "iron_brazier",
        "en": ("Blood Brazier",
               "The coals are banked low and the iron has stopped being iron "
               "coloured."),
        "es": ("Brasero de sangre",
               "Las brasas van bajas y el hierro ha dejado de tener color de "
               "hierro."),
        "materials": {"iron_ingot": 10, "wood": 15},
        "set": {"wealthPoints": 50, "beautyInput": 18},
        "slots": {"baseMesh": "aldrich_blood_brazier",
                  "albedo": "aldrich_blood_brazier_albedo",
                  "reflection": "base_texture_reflection"},
        # Iron, but sooted iron: the sheen has to stay well under the polished
        # chest this was copied from or the coals read as chrome beads.
        "shader": {"_Metallic": 0.55, "_Smoothness": 0.30},
    },
    {
        "mod": "VampireCourt",
        "id": "veiled_mirror",
        "source": "silver_mirror_wall",
        "en": ("Veiled Mirror",
               "Covered, and not out of modesty. Uncovering it settles an "
               "argument nobody in this house wants settled."),
        "es": ("Espejo velado",
               "Tapado, y no por recato. Destaparlo zanja una discusion que "
               "en esta casa nadie quiere zanjar."),
        "materials": {"silver_ingot": 5, "linen_cloth": 2},
        "set": {"wealthPoints": 80, "beautyInput": 24},
        "slots": {"baseMesh": "aldrich_veiled_mirror",
                  "albedo": "aldrich_veiled_mirror_albedo",
                  "reflection": "base_texture_reflection"},
        # The one piece here that really is metal, so it keeps a sheen - but
        # the inherited numbers were 1.7 and 1.15, which are outside the 0..1
        # the shader takes at all. Half of it is cloth in any case.
        "shader": {"_Metallic": 0.70, "_Smoothness": 0.55},
    },
    {
        "mod": "VampireCourt",
        "id": "court_reliquary",
        "source": "relic_shelf",
        "en": ("Reliquary",
               "What is kept in it is nobody's business, and the lock says so."),
        "es": ("Relicario",
               "Lo que guarda no es asunto de nadie, y la cerradura lo dice."),
        "materials": {"wood": 60, "gold_ingot": 10},
        "set": {"wealthPoints": 95},
        "slots": {"baseMesh": "aldrich_court_reliquary",
                  "albedo": "aldrich_court_reliquary_albedo",
                  "reflection": "base_texture_reflection"},
        # A wooden case with gold at the corners. Wood is not metal.
        "shader": {"_Metallic": 0.25, "_Smoothness": 0.25},
    },
    {
        "mod": "VampireCourt",
        "id": "count_crypt",
        "source": "limestone_sarcophagus",
        "en": ("The Count's Crypt",
               "A grave cut for one person in particular, and cut early."),
        "es": ("Cripta del Conde",
               "Una tumba tallada para una persona en concreto, y tallada "
               "pronto."),
        "materials": {"limestone_block": 40, "gold_ingot": 6},
        "set": {"buildTime": 70, "minBuildSkillRequired": 8, "wealthPoints": 70,
                "beautyInput": 42},
        "slots": {"baseMesh": "aldrich_count_crypt",
                  "albedo": "aldrich_count_crypt_albedo",
                  "reflection": "base_texture_reflection"},
    },
    {
        "mod": "VampireCourt",
        "id": "blood_well",
        "source": "wooden_well",
        "en": ("Blood Well",
               "It was a well. The rope still works and nobody pulls it up."),
        "es": ("Pozo de sangre",
               "Fue un pozo. La cuerda sigue funcionando y nadie tira de ella."),
        # Ten flasks go in and it gives them back a mouthful at a time. That
        # is the whole of "agregarle como requisito de construccion 10 botellas
        # de sangre, y que de el se pueda sacar sangre despues": the well is
        # not decoration any more, it is where the court keeps what it has
        # collected. No vanilla building costs anything that is not a build
        # material, so the flasks are the one part of this worth watching in
        # play - the delivery goal reads the resource by id out of the piles
        # and nothing in `ConstructionJobManager` filters by category, but a
        # thing no vanilla entry does is a thing no vanilla entry tests.
        "materials": {"wood": 15, "limestone_block": 10, "blood_draught": 10},
        # And they do not come back out when it is torn down. Ten flasks
        # returned at 60% would make the well a stockpile with a refund, and
        # the point of pouring them in is that they are gone.
        "returns": ["wood", "limestone_block"],
        # `base_production_structure` rather than the well prefab or the
        # decoration one, and the reason is the whole history of this entry.
        # The well prefab gives water, which is the one thing a well of blood
        # must not do, so it was swapped for `base_decoration` - and a prefab
        # is not a bag of optional parts: every vanilla decoration names
        # `basic_decoration`, and without it
        # DecorationComponent.OnBaseBuildingEnterFinishedState throws inside a
        # step the game *retries*, 9792 times, and hangs the save. The same
        # rule applies now in the other direction: a production prefab wants
        # `productionComponentID`, and that entry is in the mod's own
        # ProductionComponentsRepository.json with the two jobs it offers.
        # `wellComponentID`, `waterFlowThroughFloor` and the decoration id all
        # belong to prefabs this is not.
        "set": {"wealthPoints": 45, "beautyInput": 8,
                "prefabID": "base_production_structure",
                "productionComponentID": "blood_well",
                "previewPrefabID": "base_constructable_preview",
                "buildingCategoryUI": "Production",
                "buildingSubCategoryUI": "None",
                "buildingType": 2048,
                "constructableBaseCategory": 2,
                "sortingGroup": "StructuresProduction",
                "buildTime": 60,
                "iconPath": "aldrich_icon_blood_well"},
        "drop": ["wellComponentID", "waterFlowThroughFloor",
                 "decorationComponentID"],
        "slots": {"baseMesh": "aldrich_blood_well",
                  "albedo": "aldrich_blood_well_albedo",
                  "reflection": "base_texture_reflection"},
    },
    {
        "mod": "VampireCourt",
        "id": "vigil_table",
        "source": "stone_table_large",
        "en": ("Table of the Vigil",
               "Long enough for the whole household, and cut with a channel "
               "down the middle."),
        "es": ("Mesa de la vigilia",
               "Larga para toda la casa, y con un canal abierto por el medio."),
        "materials": {"limestone_block": 60},
        "set": {"wealthPoints": 60, "beautyInput": 22},
        "slots": {"baseMesh": "aldrich_vigil_table",
                  "albedo": "aldrich_vigil_table_albedo",
                  "reflection": "base_texture_reflection"},
    },
    {
        "mod": "VampireCourt",
        "id": "impaled_stake",
        "source": "scarecrow",
        "en": ("Impaled Body",
               "Left up where the road can see it. Whoever comes next reads it "
               "on the way in, and the settlement has already stopped looking."),
        "es": ("Empalado",
               "Dejado en alto donde el camino lo vea. Quien venga detras lo "
               "lee al entrar, y en la aldea ya han dejado de mirarlo."),
        "materials": {"wood": 12, "sticks": 10},
        # Beauty stays at zero on purpose. It is not decoration for the people
        # who live here - the whole of its effect is on whoever is walking up
        # to the wall, and that half is StakeDread, not a number in this file.
        "set": {"buildTime": 45, "wealthPoints": 6, "beautyInput": 0,
                "iconPath": "aldrich_icon_impaled_stake"},
        "slots": {"baseMesh": "aldrich_impaled_stake",
                  "albedo": "aldrich_impaled_stake_albedo",
                  "reflection": "base_texture_reflection"},
        # The scarecrow's cloth is tinted at runtime - vanilla puts a purple
        # through it - so the rags come out of a shader parameter, not the
        # texture. Dried blood rather than dye.
        "tint": {"_ClothTint": "#6E2A26"},
    },
    {
        # La pira colectiva, que hasta hoy no era un edificio: `burn_bodies_mass`
        # era una receta colgada del componente de produccion de la pira de
        # vanilla, asi que el jugador veia la hoguera de siempre quemando tres
        # cuerpos. Ahora tiene malla propia - el doble de alta, con los tres
        # amortajados encima - y componente propio, que es donde vive la receta.
        # La receta sigue ademas colgada de la pira de vanilla: quitarla dejaria
        # sin trabajo a quien ya tenga una construida.
        "mod": "Gravedigger",
        "id": "mass_pyre",
        "source": "pyre",
        "en": ("Mass Pyre",
               "Built for three at a time, because there were three at a time. "
               "It burns longer and it burns hotter."),
        "es": ("Pira colectiva",
               "Hecha para tres a la vez, porque eran tres a la vez. Arde mas "
               "tiempo y arde mas fuerte."),
        "materials": {"wood": 40, "sticks": 25},
        "set": {"buildTime": 55, "wealthPoints": 20, "beautyInput": 0,
                "iconPath": "aldrich_icon_mass_pyre",
                "productionComponentID": "aldrich_mass_pyre"},
        "slots": {"baseMesh": "aldrich_mass_pyre",
                  "albedo": "aldrich_mass_pyre_albedo",
                  "reflection": "base_texture_reflection"},
    },
]


def vanilla_entries():
    with open(VANILLA, encoding="utf-8-sig") as f:
        return {e["id"]: e for e in json.load(f)["repository"]}


def set_slots(entry, slots, tint, shader):
    """Overwrites mesh, texture and shader slots wherever they appear.

    Every variation is touched, not just the first: a banner has ten shapes and
    a player who picks the tattered one still wants it in the house colours.

    `shader` is for the numbers rather than the colours, and it exists because
    of `_Metallic`. A copied entry inherits the material of whatever it was
    copied from, and six of ours came out of chests and candlesticks: the
    crimson circle was carrying `_Metallic: 1`, `_Smoothness: 1`, which is
    polished chrome. A metal surface takes its colour from what it reflects and
    almost none from its albedo, so a stone ring painted deep red rendered as a
    grey disc with red glints - "sale gris con rojo", word for word. Stone is
    not metal, and neither is wax, cloth or wood.
    """
    for varlist in entry.get("variationLists") or []:
        for variation in varlist.get("variations") or []:
            for slot in variation.get("slots") or []:
                kind = slot.get("slotType")

                if kind in ("Mesh", "Texture") and slot.get("slot") in slots:
                    slot["value"] = slots[slot["slot"]]

                if kind == "ShaderParam":
                    param = slot.get("shaderParam") or {}
                    name = param.get("variableName")

                    if name in tint:
                        param["colorValue"] = tint[name]

                    if name in shader:
                        param["floatValue"] = str(shader[name])


def build(spec, vanilla):
    source = vanilla.get(spec["source"])
    if source is None:
        raise SystemExit(f"vanilla entry not found: {spec['source']}")

    entry = copy.deepcopy(source)
    entry["id"] = spec["id"]
    entry["locKeys"] = [
        {"languageName": "English", "name": spec["en"][0], "info": spec["en"][1]},
        {"languageName": "Spanish", "name": spec["es"][0], "info": spec["es"][1]},
    ]

    materials = spec["materials"]
    entry["materials"] = {"keys": list(materials), "values": list(materials.values())}

    # What comes back is the cost by default, and the default is wrong as soon
    # as a building costs something it consumes rather than something it is
    # made of. The well is built out of wood and stone and *filled* with ten
    # flasks of blood; taking it apart should hand back the timber, not the
    # blood.
    returns = spec.get("returns", list(materials))
    entry["returnOnDestroy"] = {
        "keys": returns, "values": [RETURN_ON_DESTROY] * len(returns),
    }
    entry["returnOnDeconstruct"] = {
        "keys": returns, "values": [RETURN_ON_DECONSTRUCT] * len(returns),
    }

    entry.update(spec.get("set") or {})

    # Fields that belong to the borrowed prefab and to nothing else. A copied
    # entry inherits every one of them, and a component id for a component the
    # new prefab does not have is at best ignored and at worst a crash.
    for gone in spec.get("drop") or []:
        entry.pop(gone, None)

    # Its own icon, unless the spec already named one. The build menu is where
    # a player meets the mod, and a chair where the throne should be is the
    # cheapest kind of wrong.
    entry.setdefault("iconPath", "aldrich_icon_" + spec["id"])
    entry["iconPath"] = spec.get("set", {}).get("iconPath", "aldrich_icon_" + spec["id"])

    set_slots(entry, spec.get("slots") or {}, spec.get("tint") or {},
              spec.get("shader") or {})

    return entry


def main():
    dry = sys.argv[1] if len(sys.argv) > 1 else None
    vanilla = vanilla_entries()

    by_mod = {}
    for spec in SPECS:
        by_mod.setdefault(spec["mod"], []).append(build(spec, vanilla))

    for mod, entries in by_mod.items():
        path = (os.path.join(dry, mod + "_BaseBuildingRepository.json") if dry
                else os.path.join(MODS, mod, "Data", "Models",
                                  "BaseBuildingRepository.json"))
        os.makedirs(os.path.dirname(path), exist_ok=True)

        # Anything already there that this file does not own stays, in the
        # order it was in: the two hand-written entries are not ours to move.
        existing = []
        if os.path.exists(path):
            with open(path, encoding="utf-8-sig") as f:
                mine = {e["id"] for e in entries}
                existing = [e for e in json.load(f)["repository"] if e["id"] not in mine]

        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"repository": existing + entries}, f, indent=2, ensure_ascii=False)
            f.write("\n")

        print(f"{mod:18} {len(existing)} kept + {len(entries)} built -> {path}")

    print(f"\n{len(SPECS)} muebles")


if __name__ == "__main__":
    main()
