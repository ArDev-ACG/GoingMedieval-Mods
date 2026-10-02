# Subir Aldrich - Carrion and Plague a Nexus

Es el mismo proceso que el minimapa (`Nexus - Aldrich Minimap/PASO_A_PASO.md`); aqui solo lo que cambia.

| Campo | Que poner |
|---|---|
| Mod name | `Aldrich - Carrion and Plague` |
| Version | `1.0.0` |
| Category | `Gameplay` (si no, `Miscellaneous`) |
| Brief overview | contenido de `SHORT_DESCRIPTION.txt` |
| Description | contenido de `DESCRIPCION_NEXUS.txt` |
| Media | `Portada.png` como *primary image*. **Faltan capturas en partida**: haz 2 o 3 y subelas tambien |
| File | `AldrichCarrionAndPlague_1.0.0.zip`, *Main files*, descripcion: `Requires BepInEx 5 (x64). Vortex: install as usual.` |
| Requirements | BepInEx (`site/mods/115`) |
| Tags | Gameplay, Disease, Plague, Rats, Animals, Survival, Immersion, Buildings, Clothing, BepInEx (en la pestana del mod, *Tags*; las que Nexus no tenga en su lista, se saltan) |
| Permissions | todo en No, igual que el minimapa |

El zip se rehace con `python tools/package_nexus.py CarrionAndPlague` (despues de `dotnet build src/Mods/CarrionAndPlague -c Release`).

## La prueba despues de subirlo

Tu sistema ya no tiene este mod (apartado el 29 a `Release/AldrichCarrionAndPlague/retirado-del-sistema/`; el `.cfg` con tus valores se queda en `BepInEx/config`).

1. Descargalo desde la pagina con **Vortex** e instalalo.
2. Arranca el juego. En `BepInEx\LogOutput.log` tiene que salir:
   - `[data] CarrionAndPlague: N fichero(s) copiado(s)` (la primera vez) y
   - `Aldrich - Carrion and Plague patched ... method(s)` sin ningun `FAILED`.
3. En el menu **Mods** del juego: que salga *Aldrich - Carrion and Plague*, y activarlo si no lo esta.
4. En partida: dejar cadaveres sin enterrar y esperar ratas; que un mordisco acabe en *Fiebre de la peste* (Salud del colono). Consola: `fireEffector plague_fever` y luego `endEffector plague_fever` -> `[plague] ... is now immune`.
5. Investigar estatuas de piedra y construir la estatua de gato.

Si algo falla, el log. Si funciona, pasa a `SUBIDOS.md`.
