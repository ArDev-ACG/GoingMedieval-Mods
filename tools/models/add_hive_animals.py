"""Mete el huevo y el abrazacaras en el mod del Corredor, clonandolo a el.

    python tools/models/add_hive_animals.py

Los tres ficheros que toca son los tres que hacen falta para que un animal
exista en este juego: `AnimalBase.json` (que es, como se llama y que modelo
lleva), `AttributesLists.json` (lo rapido que anda y lo fuerte que pega, por
fase de vida) y `StatsModelRepository.json` (las barras). Se clonan las
entradas de `xeno_runner` y se les cambia lo justo, que es la unica forma
honesta de escribir un animal aqui: cualquier campo que falte no da error, da
un animal que no sale.

<b>El huevo anda a cero.</b> Se pidio que fuera un **animal** para poder
destruirlo de lejos - a un animal el juego ya sabe dispararle -, y un animal
que se queda quieto no se consigue con una bandera sino con `MovementSpeed` 0
en su lista de atributos. Con el modelo puesto habra que mirar si ademas
conviene quitarle las metas de vagar.

<b>El modelo es el del lobo, de momento.</b> Igual que el Corredor: el lobo es
el que lleva la logica y encima va lo nuestro. `alien-egg.fbx` y
`facehugger-ps1` estan en `Assets globales/Modelo 3d` y entran despues por
`build_xeno_runner.py`, que es la tuberia que ya sabe meter una malla en un
bundle con la etiqueta `Mesh`.
"""

import copy
import io
import json
import os

MOD = os.path.join(os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods"),
                   "XenomorphRunner", "Data", "Models")

EGG = "aldrich_xeno_egg"
HUGGER = "aldrich_facehugger"


def load(name):
    with io.open(os.path.join(MOD, name), encoding="utf-8-sig") as handle:
        return json.load(handle)


def save(name, data):
    path = os.path.join(MOD, name)
    with io.open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, indent=2, ensure_ascii=False)
    print("  ", name)


def put(repository, entry):
    """Sustituye la entrada de ese id, o la anade. Correr esto dos veces no duplica."""
    for index, old in enumerate(repository):
        if old.get("id") == entry["id"]:
            repository[index] = entry
            return
    repository.append(entry)


def animal(source, new_id, name_en, name_es, info_en, info_es, scale, price):
    entry = copy.deepcopy(source)
    entry["id"] = new_id
    entry["category"] = new_id
    entry["statsModelId"] = new_id
    entry["price"] = price
    entry["locKeys"] = [
        {"languageName": "English", "name": name_en, "info": info_en},
        {"languageName": "Spanish", "name": name_es, "info": info_es},
    ]

    # Una sola fase de vida: ni el huevo ni el abrazacaras crecen, y una fase
    # de mas es una fase que hay que rellenar entera para nada.
    phase = copy.deepcopy(source["lifePhases"][-1])
    phase["phaseName"] = new_id + "_mature"
    phase["scaleStart"] = scale
    phase["scaleEnd"] = scale
    phase["attributesList"] = "attributes_" + new_id
    phase["canBreed"] = False
    entry["lifePhases"] = [phase]

    entry["maxCount"] = 0
    entry["canBeTamed"] = False
    entry["canBeTrained"] = False
    entry["canBeInPen"] = False
    entry["minGridSpace"] = 1

    return entry


def attributes(source, new_id, speed, damage, evade):
    entry = copy.deepcopy(source)
    entry["id"] = "attributes_" + new_id

    wanted = {
        "MovementSpeed": speed,
        "UnarmedDamage": damage,
        "EvadeChance": evade,
        "UnarmedBuildingDamage": 0,
        "AnimalTameChance": 0,
        "AnimalTrainChance": 0,
    }

    for attribute in entry["attributes"]:
        if attribute["type"] in wanted:
            attribute["value"] = wanted[attribute["type"]]

    return entry


def stats(source, new_id, health):
    entry = copy.deepcopy(source)
    entry["id"] = new_id

    for stat in entry["stats"]:
        if stat["type"] != "Health":
            continue

        stat["initialValue"] = health
        stat["initialValueRange"] = {"min": health, "max": health}
        stat["attributes"]["max"] = [{"baseValue": health}]

    return entry


def event(source, new_id, name_en, name_es, text_en, text_es,
          category, low, high, as_raid):
    """
    Un `AnimalGroupEvent` clonado del de los Corredores.

    <b>Como elige el juego que bicho trae.</b> Por `category`, que es un campo
    del animal y no su id - el Corredor los tiene iguales por costumbre -. Asi
    que un evento que traiga huevos es este mismo con la categoria del huevo, y
    nada mas: la lista de bichos del evento no existe.

    <b>Y por que son dos.</b> Se pidieron los huevos "de ambos": uno llega como
    **incursion** - con puntos de incursion, que es lo que hace que crezca con
    la partida - y el otro como **evento** tranquilo, una nidada que aparece y
    ya esta. El segundo no gasta puntos, que si no competiria con las
    incursiones de verdad.
    """
    entry = copy.deepcopy(source)
    entry["id"] = new_id
    entry["category"] = category
    entry["useRaidPoints"] = as_raid
    entry["count"] = {"min": str(low), "max": str(high)}
    entry["locKeys"] = [
        {"languageName": "English", "name": name_en},
        {"languageName": "Spanish", "name": name_es},
    ]

    dialog = entry["dialogs"][0]
    dialog["locKeys"] = [
        {"languageName": "English", "name": name_en, "description": text_en,
         "type": "game_event_type_animalRaid"},
        {"languageName": "Spanish", "name": name_es, "description": text_es,
         "type": "game_event_type_animalRaid"},
    ]

    return entry


def main():
    animals = load("AnimalBase.json")
    lists = load("AttributesLists.json")
    models = load("StatsModelRepository.json")

    runner = next(a for a in animals["repository"] if a["id"] == "xeno_runner")
    runner_attributes = next(a for a in lists["repository"]
                             if a["id"] == "attributes_xeno_runner_mature")
    runner_stats = next(s for s in models["repository"] if s["id"] == "xeno_runner")

    put(animals["repository"], animal(
        runner, EGG,
        "Xeno egg", "Huevo xeno",
        "It does not move and it does not need to. Something inside it is "
        "listening for footsteps, and a bolt from across the yard is the "
        "cheapest answer to it.",
        "No se mueve y no le hace falta. Algo dentro escucha los pasos, y un "
        "virote desde el otro lado del patio es la respuesta mas barata.",
        scale=0.55, price=30))

    put(animals["repository"], animal(
        runner, HUGGER,
        "Facehugger", "Abrazacaras",
        "Eight fingers and a tail. It wants a face, and what it leaves behind "
        "is not a corpse yet.",
        "Ocho dedos y una cola. Quiere una cara, y lo que deja detras todavia "
        "no es un cadaver.",
        scale=0.30, price=40))

    put(lists["repository"], attributes(runner_attributes, EGG,
                                        speed=0, damage=0, evade=0))
    put(lists["repository"], attributes(runner_attributes, HUGGER,
                                        speed=8.5, damage=1, evade=0.55))

    put(models["repository"], stats(runner_stats, EGG, health=30))
    put(models["repository"], stats(runner_stats, HUGGER, health=25))

    events = load("GameEventSettingsRepository.json")
    raid = next(e for e in events["repository"] if e["id"] == "game_event_xeno_runner_raid")

    put(events["repository"], event(
        raid, "game_event_xeno_eggs_raid",
        "A clutch", "Una nidada",
        "Something walked the ridge in the night and left things standing in "
        "the grass. They are not stones, and they are warm.",
        "Algo cruzo la loma de noche y dejo cosas de pie entre la hierba. No "
        "son piedras, y estan calientes.",
        category=EGG, low=2, high=5, as_raid=True))

    put(events["repository"], event(
        raid, "game_event_xeno_eggs_nest",
        "The nest", "El nido",
        "One of the far fields has gone quiet. What is growing there does not "
        "move, and nothing will graze near it.",
        "Uno de los campos de fuera se ha quedado callado. Lo que crece alli "
        "no se mueve, y ningun animal pasta cerca.",
        category=EGG, low=1, high=3, as_raid=False))

    # Un evento escrito y no metido en un grupo no se dispara nunca: el grupo
    # es el que el juego sortea. La incursion de huevos entra en el grupo que
    # ya existe - compite con los Corredores, que es lo suyo - y la nidada
    # tranquila va en uno propio, mas raro y sin invierno.
    groups = load("EventGroups.json")
    raids = next(g for g in groups["repository"] if g["id"] == "animalRaidXeno")
    if "game_event_xeno_eggs_raid" not in raids["events"]:
        raids["events"].append("game_event_xeno_eggs_raid")

    nest = copy.deepcopy(raids)
    nest["id"] = "xenoNest"
    nest["resetCounters"] = ["xenoNest"]
    nest["events"] = ["game_event_xeno_eggs_nest"]
    nest["baseValue"] = "2"
    nest["seasonMultipliers"]["values"] = [1.2, 1.2, 1, 0.2]
    put(groups["repository"], nest)

    print("escrito:")
    save("EventGroups.json", groups)
    save("AnimalBase.json", animals)
    save("AttributesLists.json", lists)
    save("StatsModelRepository.json", models)
    save("GameEventSettingsRepository.json", events)


if __name__ == "__main__":
    main()
