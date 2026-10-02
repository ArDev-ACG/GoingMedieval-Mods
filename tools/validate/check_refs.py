"""Cross-checks every id our mods reference against what actually exists.

Two silent failures already cost a play session each: an equipment entry that
was dropped for having fields the model does not own, and the same entry
missing its Resources.json half. Neither showed up as anything but a line in
the middle of a 10 MB log. This walks the four mods and reports references that
point at nothing, before the game does.

    python tools/validate/check_refs.py
"""

import base64
import json
import glob
import os
import re
import sys
from collections import defaultdict

MODS = os.path.expanduser(r"~\Documents\Foxy Voxel\Going Medieval\Mods")
GAME = (r"C:\Program Files (x86)\Steam\steamapps\common\Going Medieval"
        r"\Going Medieval_Data\StreamingAssets")
OURS = ("UndeadHorde", "VampireCourt", "Gravedigger", "CarrionAndPlague")


def load(path):
    for enc in ("utf-8-sig", "utf-8"):
        try:
            with open(path, encoding=enc) as handle:
                return json.load(handle)
        except Exception:
            continue
    return None


def ids_from(paths):
    """Every repository id in a set of files."""
    out = set()
    for path in paths:
        data = load(path)
        if not isinstance(data, dict):
            continue
        for entry in data.get("repository", []) or []:
            if isinstance(entry, dict) and entry.get("id"):
                out.add(entry["id"])
    return out


def repo(name):
    """Vanilla + our entries for one repository file name."""
    vanilla = glob.glob(os.path.join(GAME, "**", name), recursive=True)
    mine = [p for mod in OURS
            for p in glob.glob(os.path.join(MODS, mod, "Data", "Models", name))]
    return ids_from(vanilla + mine)


def our_files(name):
    return [p for mod in OURS
            for p in glob.glob(os.path.join(MODS, mod, "Data", "Models", name))]


def sprites():
    out = set()
    for mod in OURS:
        for p in glob.glob(os.path.join(MODS, mod, "Data", "Sprites", "*.png")):
            out.add(os.path.splitext(os.path.basename(p))[0])
    return out


def textures():
    out = set()
    for mod in OURS:
        for p in glob.glob(os.path.join(MODS, mod, "Data", "Textures", "*.png")):
            out.add(os.path.splitext(os.path.basename(p))[0])
    return out


def meshes():
    """Every asset address a mod actually ships, out of its own catalogs.

    A `baseMesh` slot naming a mesh that is in no bundle is the newest way to
    fail silently, and the quietest one yet: `MeshRepository.GetByID` answers
    null, the building is built invisible, and the log says not one word about
    any of it.

    The addresses live in `m_KeyDataString`, not in `m_InternalIds` - that
    second list holds the bundle paths and the project-relative asset paths,
    which is what the first version of this looked in and why it reported the
    throne missing from a bundle that contained it. The key table is a
    base64-encoded binary blob of Addressables' own making, and rather than
    reimplement its serializer for a yes/no membership test, the printable
    runs are pulled straight out. Bundle names and Unity's own guids come with
    them; neither can collide with an `aldrich_*` address, which is the only
    thing this is ever asked about.
    """
    out = set()

    for mod in OURS:
        pattern = os.path.join(MODS, mod, "Data", "AddressableAssets", "*.json")
        for path in glob.glob(pattern):
            data = load(path)
            if not isinstance(data, dict):
                continue

            packed = data.get("m_KeyDataString")
            if not isinstance(packed, str):
                continue

            try:
                raw = base64.b64decode(packed)
            except Exception:
                continue

            for word in re.findall(rb"[A-Za-z0-9_.\-]{4,}", raw):
                out.add(word.decode("ascii", "replace"))

    return out


KNOWN = {
    "perk": repo("Perk.json"),
    "effector": repo("Effectors.json"),
    "resource": repo("Resources.json"),
    "equipment": repo("Equipment.json"),
    "production": repo("Production.json"),
    "hitgroup": repo("HitEffectorGroups.json"),
    "building": repo("BaseBuildingRepository.json"),
    "role": repo("Role.json"),
    "npcpreset": repo("NPCPresets.json"),
    "wound": repo("Wounds.json"),
}
OUR_SPRITES = sprites()
OUR_TEXTURES = textures()
OUR_MESHES = meshes()


def our_resources():
    """Nuestras entradas de Resources.json, enteras y no solo el id."""
    out = {}
    for path in our_files("Resources.json"):
        data = load(path)
        for entry in (data or {}).get("repository", []) or []:
            if isinstance(entry, dict) and entry.get("id"):
                out[entry["id"]] = entry
    return out


OUR_RESOURCES = our_resources()

problems = []


# Las seis calidades de `ProductQuality` sin la None, en minusculas y en el
# orden en que el juego las nombra.
QUALITIES = ("flimsy", "sturdy", "good", "fine", "superior", "flawless")


def base_of_equipment(value):
    """
    El id base de una variante de equipo, o el mismo id si no lo es.

    Un arma que un NPC lleva puesta **no** se llama como su proto: se llama
    `<calidad>_<material>_<proto>`, y esas variantes las fabrica el juego al
    arrancar - `ResourceRepository.CacheEquipmentQualityItems`, una por
    material declarado y por calidad -, asi que no estan escritas en ningun
    JSON y buscarlas ahi es buscar algo que nadie escribio. Por eso los
    presets de vanilla dicen `sturdy_iron_dagger` y `Resources.json` solo
    tiene `dagger`.

    Se devuelve tambien el material, porque la mitad del trabajo es comprobar
    que el proto lo declara: pedir un material que el recurso no lista es una
    variante que no existe, y el NPC sale con las manos vacias igual que si el
    recurso entero faltara.
    """
    parts = value.split("_")
    if len(parts) >= 3 and parts[0] in QUALITIES:
        return "_".join(parts[2:]), parts[1]
    return value, None


def check_equipment_resource(resource_id, where):
    """
    Un arma tiene que ser un *proto* para que el juego la registre.

    `EquipmentRepository.InitializeEquipmentItems` busca, por cada equipo, un
    recurso con su mismo id dentro de `ResourceRepository.ProtoItems`, y esa
    lista es exactamente `AllItems.Where(r => r.HasQuality)`. Con
    `hasQuality: false` el recurso nunca entra ahi, el equipo se salta sin mas
    que una linea de Info, y en partida sale
    `[ERR] [NPCManager] Tried to spawn Humanoid with equipment id X not
    defined in equipment.json` mientras el NPC aparece con las manos vacias.
    Los 80 equipos de vanilla cumplen las dos reglas sin una sola excepcion.
    """
    entry = OUR_RESOURCES.get(resource_id)
    if entry is None:
        return   # es de vanilla, y vanilla ya cumple

    if not entry.get("hasQuality"):
        problems.append(
            f"{where}: el recurso '{resource_id}' necesita \"hasQuality\": true "
            f"o EquipmentRepository no registra el equipo")

    proto = entry.get("protoId")
    if proto and proto != resource_id:
        problems.append(
            f"{where}: el recurso '{resource_id}' tiene protoId '{proto}'; "
            f"en vanilla el protoId de un arma es siempre su propio id")


def want(kind, value, where):
    if not value:
        return
    if value not in KNOWN[kind]:
        problems.append(f"{where}: {kind} '{value}' no existe")


def walk(node, mod, fname, path=""):
    """Follows the reference fields we know how to check."""
    if isinstance(node, list):
        for item in node:
            walk(item, mod, fname, path)
        return
    if not isinstance(node, dict):
        return

    where = f"{mod}/{fname}" + (f" [{node['id']}]" if node.get("id") else "")

    # En HitEffectorGroups, "effectorId" nombra una herida de Wounds.json,
    # no un efector: el nombre del campo miente y costo media hora de falsos
    # positivos la primera vez que se ejecuto esto.
    effector_id_kind = "wound" if "HitEffectorGroups" in fname else "effector"

    for field, kind in (("effector", effector_id_kind),
                        ("effectorId", effector_id_kind),
                        ("perkId", "perk"),
                        ("productionComponentID", None),
                        ("equipmentID", "npcpreset"),
                        ("humanTypeId", None)):
        if kind and field in node and isinstance(node[field], str):
            want(kind, node[field], where)

    for field in ("bannedEffector", "allowedEffectors", "onAssignEffectorIds",
                  "onRetractEffectorIds", "sleepEffectors", "wakeUpEffectors",
                  "dayEffectors", "nightEffectors"):
        for base in (field, field + "#APPEND"):
            for value in node.get(base, []) or []:
                if isinstance(value, str):
                    want("effector", value, where)

    for field in ("hitEffectorGroupIDs", "criticalHitEffectorGroupIDs"):
        for value in node.get(field, []) or []:
            want("hitgroup", value, where)

    for field in ("conflictsWith", "perkIds"):
        for value in node.get(field, []) or []:
            want("perk", value, where)

    for field in ("equipment", "bannedEquipment"):
        for value in node.get(field, []) or []:
            if isinstance(value, str):
                base, material = base_of_equipment(value)
                want("equipment", base, where)
                if base not in KNOWN["resource"]:
                    problems.append(
                        f"{where}: equipo '{value}' no tiene entrada en Resources.json "
                        f"(sin ella EquipmentRepository lo ignora)")
                else:
                    check_equipment_resource(base, where)

                    entry = OUR_RESOURCES.get(base)
                    if entry is not None and material is not None:
                        declared = entry.get("materials") or []
                        if material not in declared:
                            problems.append(
                                f"{where}: '{value}' pide el material '{material}' y "
                                f"'{base}' declara {declared or 'ninguno'}; "
                                f"esa variante no la fabrica nadie")

    # Las claves de coste de un edificio. Un id que no existe no da un aviso:
    # ResourceUtils.GetLocalizedResourceName devuelve null y CacheCursorInfoData
    # peta en cada tick con el fantasma en la mano. Al circulo carmesi le costo
    # una partida pedir "cloth", que no es nada - son linen_cloth y wool_cloth.
    for field in ("materials", "returnOnDestroy", "returnOnDeconstruct"):
        block = node.get(field)
        if isinstance(block, dict):
            for value in block.get("keys", []) or []:
                if isinstance(value, str):
                    want("resource", value, where)

    for field in ("iconPath", "bubbleIcon", "iconBackgroundPath"):
        value = node.get(field)
        if isinstance(value, str) and value.startswith("aldrich_") and value not in OUR_SPRITES:
            problems.append(f"{where}: sprite '{value}' no esta en ningun Data/Sprites")

    # `Repository.Deserialize` termina con `repository.RemoveAll(m => m.HideInGame)`.
    # No es "no lo muestres": la entrada **se borra del repositorio**, y por eso
    # `example_weapon` de vanilla no es un arma sino una plantilla. Nos costo tres
    # cosas a la vez: las garras (`GetByID` devolvia null y el alzado salia con las
    # manos vacias), el perk Alzado (que nunca se podia poner, y sin el los
    # caminantes no se pintaban de gris) y los tres perks del aura del Conde.
    if node.get("hideInGame") is True and node.get("id"):
        problems.append(
            f'{where}: "hideInGame": true borra la entrada del repositorio '
            f"(Repository.Deserialize hace RemoveAll(HideInGame)); quitalo")

    # LocKeyUtils.GetLanguageEntry compara languageName, que es un *string*,
    # contra GlobalSettings.LanguageName ("Spanish", "English", ...). Un numero
    # no coincide nunca y el juego cae en locKeys[0], que en nuestros ficheros
    # es el ingles: por eso salian "Fed from the vein" y "Lord of the crypt" en
    # una partida en castellano. El fichero "Language Enum.txt" del juego lista
    # los numeros, pero solo como referencia; lo que hay que escribir es el
    # nombre.
    if "languageName" in node and not isinstance(node["languageName"], str):
        problems.append(
            f"{where}: languageName {node['languageName']!r} es un numero; "
            f"tiene que ser el nombre del idioma entre comillas")

    if node.get("slotType") == "Texture":
        value = node.get("value")
        if isinstance(value, str) and value.startswith("aldrich_") and value not in OUR_TEXTURES:
            problems.append(f"{where}: textura '{value}' no esta en ningun Data/Textures")

    # Una malla propia que no esta en el bundle no da error de ninguna clase:
    # MeshRepository.GetByID devuelve null y el edificio se construye invisible.
    # Solo se miran las nuestras - las de vanilla viven en los bundles del juego
    # y este script no los abre.
    if node.get("slotType") == "Mesh":
        value = node.get("value")
        if isinstance(value, str) and value.startswith("aldrich_") and value not in OUR_MESHES:
            problems.append(
                f"{where}: malla '{value}' no esta en ningun Data/AddressableAssets "
                f"(reconstruir con tools/unity/AldrichBundles)")

    for value in node.values():
        walk(value, mod, fname, path)


for mod in OURS:
    for path in sorted(glob.glob(os.path.join(MODS, mod, "Data", "Models", "*.json"))):
        data = load(path)
        if data is None:
            problems.append(f"{mod}/{os.path.basename(path)}: JSON ilegible")
            continue
        walk(data, mod, os.path.basename(path))

# Los perks y efectores que el plugin nombra en C# tambien tienen que existir.
CODE_REFS = [
    ("perk", "Vampire"), ("perk", "Ghoul"), ("perk", "Risen"),
    ("effector", "VampireFedOnBlood"), ("effector", "VampireBloodDrained"),
    ("effector", "PlagueImmunityMarker"),
    # Los once perks que RisenTraits pone a cada caminante. Un id mal escrito
    # aqui no es un error: el walker sale con diez rasgos en vez de once y no
    # lo nota nadie.
    ("perk", "Laggardly"), ("perk", "Ruthless"), ("perk", "Callous"),
    ("perk", "Cannibal"), ("perk", "Bloodlust"), ("perk", "Disfigured"),
    ("perk", "Gobbler"), ("perk", "ColdHardy"), ("perk", "Robust"),
    ("perk", "Strapping"),
    # El aura del Conde ya no pasa por perks ocultos: CountAura.cs arranca
    # estos efectores directamente sobre quien esta cerca.
    ("effector", "CountAuraLvl1"), ("effector", "CountAuraLvl2"), ("effector", "CountAuraLvl3"),
    # undead_infection y plague_fever son heridas, y es por eso que el parche
    # que las escucha cuelga de StatsInstance.EndEffector: una herida termina
    # como cualquier otro efector activo.
    ("wound", "undead_infection"), ("wound", "plague_fever"),
    ("effector", "VampireThirstMild"), ("effector", "VampireThirstRising"),
    ("effector", "VampireThirstRavenous"),
    # La quemadura visible del sol y la marca de infeccion son heridas.
    ("wound", "undead_sunburn"), ("wound", "undead_infected"),
    ("building", "cat_statue"), ("building", "blood_altar"),
    ("building", "impaled_stake"), ("building", "count_throne"),
    ("role", "count"),
]
for kind, value in CODE_REFS:
    want(kind, value, "GMPlugins (C#)")

for line in problems:
    print(" -", line)
print(f"\n{len(problems)} referencia(s) rotas")
sys.exit(1 if problems else 0)
