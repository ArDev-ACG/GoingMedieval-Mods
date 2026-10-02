# Subir Aldrich - Gravedigger a Nexus

Es el mismo proceso que el minimapa (`Nexus - Aldrich Minimap/PASO_A_PASO.md`); aqui solo lo que cambia.

| Campo | Que poner |
|---|---|
| Mod name | `Aldrich - Gravedigger` |
| Version | `1.0.0` |
| Category | `Gameplay` (si no, `Miscellaneous`) |
| Brief overview | contenido de `SHORT_DESCRIPTION.txt` |
| Description | contenido de `DESCRIPCION_NEXUS.txt` |
| Imagen principal | `Portada.png` (mejor si anades capturas de la fosa y la pira en partida) |
| File | `AldrichGravedigger_1.0.0.zip`, *Main files*, descripcion: `Requires BepInEx 5 (x64). Vortex: install as usual.` |
| Requirements | BepInEx (`site/mods/115`) |
| Tags | Gameplay, Graves, Funeral, Buildings, Roles, Perks, Disease, Immersion, BepInEx (en la pestana del mod, *Tags*; las que Nexus no tenga en su lista, se saltan) |
| Permissions | todo en No, igual que el minimapa |

## La prueba despues de subirlo

Tu sistema ya no tiene Gravedigger (se aparto el 27 a `Release/AldrichGravedigger/retirado-del-sistema/`).

1. Descargalo desde la pagina con **Vortex** e instalalo.
2. Arranca el juego. En `BepInEx\LogOutput.log` tiene que salir:
   - `[data] Gravedigger: 19 fichero(s) copiado(s)` (la primera vez) y
   - `Aldrich - Gravedigger patched ... method(s)` sin ningun `FAILED`.
3. En el menu **Mods** del juego: que salga *Aldrich - Gravedigger*, y activarlo si no lo esta.
4. En partida: construir la fosa comun, meter mas de un cuerpo; construir la pira colectiva y quemar tres.

Si algo falla, el log.
