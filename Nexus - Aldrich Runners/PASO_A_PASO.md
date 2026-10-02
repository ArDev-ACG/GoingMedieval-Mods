# Subir Aldrich - The Runner a Nexus

Es el mismo proceso que el minimapa (`Nexus - Aldrich Minimap/PASO_A_PASO.md`); aqui solo lo que cambia.

| Campo | Que poner |
|---|---|
| Mod name | `Aldrich - The Runner` |
| Version | `1.0.0` |
| Category | `Gameplay` (si no, `Miscellaneous`) |
| Brief overview | contenido de `SHORT_DESCRIPTION.txt` |
| Description | contenido de `DESCRIPCION_NEXUS.txt` |
| Media | `Portada.png` como *primary image*, y `image1.png`, `image2.png`, `image3.png` |
| File | `AldrichXenomorphRunner_1.0.0.zip`, *Main files*, descripcion: `Requires BepInEx 5 (x64). Vortex: install as usual.` |
| Requirements | BepInEx (`site/mods/115`) |
| Tags | Gameplay, Creatures, Aliens, Monsters, Enemies, Combat, Horror, Difficulty, New models, BepInEx (en la pestana del mod, *Tags*; las que Nexus no tenga en su lista, se saltan) |
| Permissions | todo en No, igual que el minimapa; en *credits* los tres modelos CC BY 4.0 (ya van en la descripcion) |

El zip se rehace con `python tools/package_nexus.py XenomorphRunner` (despues de `dotnet build src/Mods/XenomorphRunner -c Release`).

## La prueba despues de subirlo

Tu sistema ya no tiene este mod (apartado el 29 a `Release/AldrichXenomorphRunner/retirado-del-sistema/`; el `.cfg` con tus valores se queda en `BepInEx/config`).

1. Descargalo desde la pagina con **Vortex** e instalalo.
2. Arranca el juego. En `BepInEx\LogOutput.log` tiene que salir:
   - `[data] XenomorphRunner: N fichero(s) copiado(s)` (la primera vez) y
   - `Aldrich - The Runner patched ... method(s)` sin ningun `FAILED`.
3. En el menu **Mods** del juego: que salga *Aldrich - The Runner*, y activarlo si no lo esta.
4. En partida, esperar el evento (o forzarlo desde la consola) y ver Corredores, huevos y aliens arana con su modelo, no de lobo.
5. Que un Corredor suba una pared y que su sangre salga verde.

Si algo falla, el log. Si funciona, pasa a `SUBIDOS.md`.
