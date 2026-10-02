# Aldrich mods for Going Medieval

BepInEx 5 mods for [Going Medieval](https://store.steampowered.com/app/1029780/Going_Medieval/).
Each mod is its own DLL and works on its own.

| Mod | What it does | Source |
|---|---|---|
| **Aldrich Minimap** | A minimap of the whole map: ground, water, settlers, enemies and animals. Click to move the camera, zoom 1x-6x, Ctrl+M to hide. [Nexus](https://www.nexusmods.com/goingmedieval/mods/149) | `src/AldrichMinimap` |
| **Aldrich Carrion and Plague** | Unburied dead bring rats, rat bites carry plague, survivors are immune for life. | `src/Mods/CarrionAndPlague` |
| **Aldrich Gravedigger** | Mass grave, mass pyre, the Gravedigger role and perk. | `src/Mods/Gravedigger` |
| **Aldrich Undead Horde** | The Risen: a hostile faction that never surrenders or retreats. | `src/Mods/UndeadHorde` |
| **Aldrich Vampire Court** | Play a vampire: thirst, bites, ghouls, thralls and the Count role. | `src/Mods/VampireCourt` |
| **Aldrich Xenomorph Runner** | Pack-hunting aliens with acid blood that climb walls, and egg-borne face leapers. | `src/Mods/XenomorphRunner` |

## Layout

- `src/GMPlugins/` - code shared by the mods; each `src/Mods/<Mod>/*.csproj` picks the files it compiles.
- `tools/` - Python scripts that build models, textures, icons and buildings; `tools/unity/` builds the asset bundles.
- `ASSESTS/` - original models, icons and textures.
- `Nexus - */` - Nexus page texts and screenshots.

## Building

Needs the .NET SDK and the game with BepInEx 5 installed.

1. Copy `BepInEx.dll`, `BepInEx.Harmony.dll` and `0Harmony.dll` from `<game>/BepInEx/core/` into `src/lib/`.
2. Build:
   ```
   dotnet build src/Mods/Gravedigger -c Release
   ```
   If the game is not in the default Steam folder, add
   `-p:GameManaged="<game>\Going Medieval_Data\Managed"`.

## Licence

All rights reserved - see [LICENSE](LICENSE). Not affiliated with Foxy Voxel.
