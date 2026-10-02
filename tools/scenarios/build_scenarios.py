"""Writes the two Aldrich scenarios into the scenario mod.

A scenario is a mod of its own, and not by choice: `ModInfo.json` tagged
`Scenario` is what makes `ModManager.CreateInstance` build a
`ScenarioModInstance` instead of the ordinary one, and que instancia lee la
carpeta `Scenarios/` **de dentro de `Data/`** y nada mas - ni `Data/Models` ni
repositorios.

**Y ese `Data/` costo un error en cada arranque.** La primera version escribia
en `AldrichScenarios/Scenarios/`, a la altura de `ModInfo.json`, y el log del
15 a las 22:04 lo dijo dos veces sin que nadie lo leyera: `[ERR] [ModInstance]
Data directory ...\AldrichScenarios\Data not found` y `[ERR]
[ScenarioModInstance] Couldn't find any scenario at ...\Data\Scenarios`.
`UpdateScenarios()` hace `Path.Combine(ModInstance.DataPath, "Scenarios")` y
`DataPath` es siempre `<mod>/Data`, asi que la carpeta de arriba no la mira
nadie: los dos escenarios nunca llegaron a la lista de partida nueva. So the scenarios cannot live inside Vampire Court or Undead
Horde; they need their own folder, and they reach across to those mods only by
naming ids.

**Y un escenario sin `modifiedOnVersion` no llega a la lista.** El 17 los
dos salieron del juego con "tu escenario personalizado ... esta obsoleto y no
se puede cargar", y el motivo no estaba en el fichero sino en quien lo lee:
`ScenarioView.Initialize` mete `GetDefaultScenarios()` sin mirar nada y pasa
`GetUserScenarios()` - donde caen los de un mod - por
`ApplicationVersionUtils.IsValidScenarioVersion(scenario.ModifiedOnVersion)`,
que exige formato `x.y.z` y no menos de `0.8.0`. Los cinco de vanilla no lo
llevan porque son `isDefault` y nadie se lo pregunta. Los nuestros si, asi que
cada escenario sale sellado con la version del juego para la que se escribio.

The shape below is vanilla's `StreamingAssets/Scenario/Scenarios.json`, field
for field. Two things in it are worth knowing before changing a number:

  - **`factionFriendlinessOverrides` keys on the faction *type*, not the
    faction.** That is the whole reason Undead Horde now declares a faction
    type of its own, `the_risen`: while it shared `cannibal` with vanilla's
    cannibals, "hostile to the dead and friendly to everyone else" was not a
    sentence this file could say.

  - **`gameParameters` is a full list or it is nothing.** Vanilla writes all
    forty-two every time, so the neutral list is written out here and the
    handful that move are passed in by name - a typo raises instead of
    silently shipping a scenario with a parameter missing.

    python tools/scenarios/build_scenarios.py
"""

import json
import os

# La version con la que se sella cada escenario. Tiene que ser formato x.y.z y
# no menor que el `ValidScenarioVersion` del juego, que es 0.8.0; se pone la del
# juego para el que se escribieron y asi la linea dice ademas contra que se
# probaron.
VERSION = "1.1.19"

OUT = os.path.expanduser(
    "~/Documents/Foxy Voxel/Going Medieval/Mods/AldrichScenarios/Data/Scenarios/Scenarios.json")

# Every game parameter vanilla writes, in vanilla's order, at its neutral 1.
NEUTRAL = [
    "raidPointsMultiplier", "raidStrengthMultiplier", "enemyRaidStrengthMultiplier",
    "enemyRaidPointsMultiplier", "enemyRaidPointsMax", "enemyRaidPointsMin",
    "woundSeverityMultiplier", "animalSpawnMultiplier", "plantYieldMultiplier",
    "moodTarget", "enemyHPMax", "healthRecoverySpeed", "rottingSpeed",
    "decomposeSpeed", "tradingValue", "huntingRetailateChance", "globalWorkSpeed",
    "useSeeds", "enemiesHaveTrebuchet", "mineYieldMultiplier",
    "fireSpreadSpeedMultiplier", "runawayEvent", "thundertstormEvent",
    "CellarFloodEvent", "hailstormEvent", "blightEvent", "coldsnapEvent",
    "heatwaveEvent", "animalRaidEvent", "animalInfestationEvent", "newWorkerEvent",
    "enemyRaidEvent", "merchantGeneralEvent", "merchantRareEvent",
    "merchantPrisonerEvent", "visitorEvent", "visitorRoleEvent",
    "animalMiscSmallEvent", "animalWildLargeEvent", "animalDomesticSingleEvent",
    "animalBearSingleEvent", "caravanAmbushChanceMultiplier",
]

OBJECTIVES = ["objective_religious", "objective_pagan", "objective_intellectual",
              "objective_military", "objective_trade", "objective_infamy"]


def parameters(**moved):
    """The full list, with the named ones moved off 1."""
    values = {name: 1 for name in NEUTRAL}

    for name, value in moved.items():
        if name not in values:
            raise KeyError("no such game parameter: " + name)
        values[name] = value

    return [{"id": name, "value": values[name]} for name in NEUTRAL]


# The first scenario in one line: everything alive is a trading partner, and
# the only permanent enemy is the one that does not trade because it cannot
# talk.
LONG_NIGHT_FACTIONS = [
    {"factionTypeId": "the_risen", "friendlinessRange": {"min": -100, "max": -100}},
    {"factionTypeId": "bandits", "friendlinessRange": {"min": 40, "max": 75}},
    {"factionTypeId": "cannibal", "friendlinessRange": {"min": 40, "max": 75}},
    {"factionTypeId": "faithful", "friendlinessRange": {"min": 50, "max": 90}},
    {"factionTypeId": "pagan", "friendlinessRange": {"min": 50, "max": 90}},
    {"factionTypeId": "knowledge", "friendlinessRange": {"min": 50, "max": 90}},
    {"factionTypeId": "monarchy", "friendlinessRange": {"min": 50, "max": 90}},
    {"factionTypeId": "anarchy", "friendlinessRange": {"min": 50, "max": 90}},
]

LONG_NIGHT = {
    "id": "aldrich_the_long_night",
    "locKeys": [
        {
            "languageName": "English",
            "name": "The Long Night",
            "info": "Only the dead are hostile. Everyone still alive trades - and there are fewer of them every season.",
            "description": "The plague went through in the spring, and the burying stopped in the summer because there was nobody left who would go near a body. What comes out of the treeline now used to buy your wool. The other settlements still send caravans; they have to, there is nobody else left to trade with. Autumn, dusk, and the wall you have not built yet.",
        },
        {
            "languageName": "Spanish",
            "name": "La noche larga",
            "info": "Solo los muertos son hostiles. Todo el que sigue vivo comercia - y cada estacion quedan menos.",
            "description": "La peste paso en primavera y en verano se dejo de enterrar, porque ya no quedaba quien se acercara a un cuerpo. Lo que sale ahora de la linea de arboles antes te compraba la lana. Los demas asentamientos siguen mandando caravanas; no les queda nadie mas con quien comerciar. Otono, al anochecer, y el muro que aun no has levantado.",
        },
    ],
    "imageId": "scenario_hard",
    "difficulty": "scenario_difficulty_Difficult",
    "isDefault": False,
    "startSeason": 2,
    "startHour": 19,
    "startingEventScheduleId": "default_3",
    "startEventId": "",
    "startMapTypes": ["map_type_wetland", "map_type_valley", "map_type_hill",
                      "map_type_mountain"],
    "factionFriendlinessOverrides": LONG_NIGHT_FACTIONS,
    "startingResources": [
        {"id": "wood", "value": 250},
        {"id": "packaged_meal", "value": 8},
        {"id": "cabbage", "value": 25},
        {"id": "cabbage_seed", "value": 25},
        {"id": "hay", "value": 100},
        {"id": "linen_cloth", "value": 30},
        {"id": "healing_kit_simple", "value": 14},
        {"id": "basic_research_book", "value": 15},
        {"id": "ale", "value": 15},
    ],
    # Spears and shields, and one bow between three. What is coming has no
    # ranged attack and does not stop walking, so reach and a line at the wall
    # are worth more than archery.
    "startingEquipment": [
        {"id": "sturdy_wood_spear", "value": 2},
        {"id": "sturdy_steel_short_sword", "value": 1},
        {"id": "fine_wood_buckler_shield", "value": 2},
        {"id": "good_wood_short_bow", "value": 1},
        {"id": "fine_linen_gambeson_armor", "value": 2},
    ],
    "startingStructurePiles": [],
    "startingAnimals": [
        {"id": "dog", "bodyType": 1, "count": 1, "lifePhaseIndex": 1, "animalType": 2},
        {"id": "goat", "bodyType": 2, "count": 2, "lifePhaseIndex": 1, "animalType": 0},
    ],
    "villagerConstraints": {
        "numberOfVillagers": 3,
        "ageMin": 20, "ageMax": 50,
        "heightMin": 150, "heightMax": 190,
        "weightMin": 50, "weightMax": 105,
        "forceBodyType": 50, "forceReligion": 50,
        "defaultClothes": ["good_linen_winter_clothes", "good_leather_winter_clothes",
                           "good_wool_winter_clothes"],
    },
    # More raids and harder ones, the trade routes wide open, and fewer
    # newcomers - a siege you cannot buy your way through is only a countdown,
    # and one that refills itself for free is not a siege.
    "gameParameters": parameters(
        enemyRaidEvent=3, enemyRaidPointsMultiplier=1.4, enemyRaidStrengthMultiplier=1.3,
        enemyRaidPointsMax=1.5, merchantGeneralEvent=2, merchantRareEvent=1.5,
        merchantPrisonerEvent=1.5, visitorEvent=1.5, animalRaidEvent=0.5,
        newWorkerEvent=0.5, woundSeverityMultiplier=1.2, rottingSpeed=1.25,
        healthRecoverySpeed=0.9, tradingValue=1.15),
    "allowedObjectives": OBJECTIVES,
}

CRIMSON_NIGHT = {
    "id": "aldrich_crimson_night",
    "locKeys": [
        {
            "languageName": "English",
            "name": "Crimson Night",
            "info": "Winter, ten at night. A household that does its living after dark, and a long dark to do it in.",
            "description": "You did not come here to farm. You came because this far north the winter nights run long and nobody asks where the livestock went. Get the coffins in before the thaw.",
        },
        {
            "languageName": "Spanish",
            "name": "Noche carmesi",
            "info": "Invierno, las diez de la noche. Una casa que hace su vida cuando se pone el sol, y una noche larga para hacerla.",
            "description": "No viniste aqui a sembrar. Viniste porque tan al norte las noches de invierno son largas y nadie pregunta a donde fue el ganado. Mete los ataudes antes del deshielo.",
        },
    ],
    "imageId": "scenario_normal",
    "difficulty": "scenario_difficulty_standard",
    "isDefault": False,
    # Winter, at night, in the hills: the two settings that decide how much of
    # the day a vampire gets to keep.
    "startSeason": 3,
    "startHour": 22,
    "startingEventScheduleId": "default_1",
    "startEventId": "",
    "startMapTypes": ["map_type_hill", "map_type_mountain"],
    "factionFriendlinessOverrides": [
        {"factionTypeId": "bandits", "friendlinessRange": {"min": -100, "max": -100}},
        {"factionTypeId": "cannibal", "friendlinessRange": {"min": -100, "max": -100}},
        {"factionTypeId": "the_risen", "friendlinessRange": {"min": -100, "max": -100}},
        # The devout have heard what happens to the livestock.
        {"factionTypeId": "faithful", "friendlinessRange": {"min": -80, "max": 20}},
    ],
    "startingResources": [
        {"id": "wood", "value": 300},
        {"id": "packaged_meal", "value": 10},
        {"id": "linen_cloth", "value": 40},
        {"id": "healing_kit_simple", "value": 8},
        {"id": "basic_research_book", "value": 25},
        {"id": "ale", "value": 30},
        {"id": "hay", "value": 150},
    ],
    "startingEquipment": [
        {"id": "fine_steel_short_sword", "value": 1},
        {"id": "sturdy_wood_spear", "value": 1},
        {"id": "fine_wood_buckler_shield", "value": 1},
        {"id": "fine_linen_gambeson_armor", "value": 1},
    ],
    "startingStructurePiles": [],
    # The livestock is the larder, and the cats are the household.
    "startingAnimals": [
        {"id": "cat", "bodyType": 2, "count": 2, "lifePhaseIndex": 1, "animalType": 2},
        {"id": "goat", "bodyType": 1, "count": 2, "lifePhaseIndex": 1, "animalType": 0},
        {"id": "goat", "bodyType": 2, "count": 2, "lifePhaseIndex": 1, "animalType": 0},
    ],
    "villagerConstraints": {
        "numberOfVillagers": 3,
        "ageMin": 25, "ageMax": 60,
        "heightMin": 155, "heightMax": 195,
        "weightMin": 50, "weightMax": 100,
        "forceBodyType": 50, "forceReligion": 0,
        "defaultClothes": ["fine_linen_winter_clothes", "fine_leather_winter_clothes",
                           "fine_wool_winter_clothes"],
    },
    "gameParameters": parameters(
        enemyRaidEvent=1.2, visitorEvent=1.4, visitorRoleEvent=1.4,
        merchantGeneralEvent=1.3, animalWildLargeEvent=1.3,
        animalDomesticSingleEvent=1.4, plantYieldMultiplier=0.85, coldsnapEvent=1.5),
    "allowedObjectives": OBJECTIVES,
}


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)

    with open(OUT, "w", encoding="utf-8") as handle:
        scenarios = [LONG_NIGHT, CRIMSON_NIGHT]
        for scenario in scenarios:
            scenario["modifiedOnVersion"] = VERSION

        json.dump({"repository": scenarios}, handle,
                  indent=2, ensure_ascii=False)

    print("wrote " + OUT)
    print("  aldrich_the_long_night   autumn, 19:00, only the_risen hostile")
    print("  aldrich_crimson_night    winter, 22:00, hills and mountains only")


main()
