# Subir Aldrich - Vampire Court a Nexus

Es el mismo proceso que el minimapa (`Nexus - Aldrich Minimap/PASO_A_PASO.md`); aqui solo lo que cambia.

| Campo | Que poner |
|---|---|
| Mod name | `Aldrich - Vampire Court` |
| Version | `1.0.0` |
| Category | `Gameplay` (si no, `Miscellaneous`) |
| Brief overview | contenido de `SHORT_DESCRIPTION.txt` |
| Description | contenido de `DESCRIPCION_NEXUS.txt` |
| Media | `Portada.png` como *primary image*, y `image1.png`, `image2.png`, `image3.png` |
| File | `AldrichVampireCourt_1.0.0.zip`, *Main files*, descripcion: `Requires BepInEx 5 (x64). Vortex: install as usual.` |
| Requirements | BepInEx (`site/mods/115`) |
| Tags | Gameplay, Vampires, Perks, Roles, Furniture, Buildings, Decoration, Horror, Fantasy, BepInEx (en la pestana del mod, *Tags*; las que Nexus no tenga en su lista, se saltan) |
| Permissions | todo en No, igual que el minimapa |

El zip se rehace con `python tools/package_nexus.py VampireCourt` (despues de `dotnet build src/Mods/VampireCourt -c Release`).

## La prueba despues de subirlo

Tu sistema ya no tiene este mod (apartado el 29 a `Release/AldrichVampireCourt/retirado-del-sistema/`; el `.cfg` con tus valores se queda en `BepInEx/config`).

1. Descargalo desde la pagina con **Vortex** e instalalo.
2. Arranca el juego. En `BepInEx\LogOutput.log` tiene que salir:
   - `[data] VampireCourt: N fichero(s) copiado(s)` (la primera vez) y
   - `Aldrich - Vampire Court patched ... method(s)` sin ningun `FAILED`.
3. En el menu **Mods** del juego: que salga *Aldrich - Vampire Court*, y activarlo si no lo esta.
4. Partida nueva, personalizacion avanzada: elegir el perk *Vampire* en un colono.
5. Comprobar la sed y el boton Morder; investigar y construir un par de muebles de la corte (trono, ataud); dormir en el ataud.

Si algo falla, el log. Si funciona, pasa a `SUBIDOS.md`.
