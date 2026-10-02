# Subir Aldrich Minimap a Nexus - paso a paso

En esta carpeta:

| Fichero | Para que |
|---|---|
| `AldrichMinimap_1.0.0.zip` | El archivo que se sube (DLL + README + LICENSE) |
| `DESCRIPCION_NEXUS.txt` | El texto de la pagina, ya en el formato de Nexus (BBCode): se copia y se pega |
| `PASO_A_PASO.md` | Esta guia |

BepInEx 5, el requisito, en Nexus: <https://www.nexusmods.com/site/mods/115>
Y en su sitio oficial, por si Nexus falla: <https://github.com/BepInEx/BepInEx/releases>
(el fichero es `BepInEx_win_x64_5.4.23.x.zip`).

---

## 0. Antes de subir

1. **Capturas.** Haz 2 o 3 en partida con el minimapa a la vista: una normal, una con zoom y una con el nombre del suelo bajo el raton. Nexus pide al menos una imagen para la portada. La del minimapa entero queda bien de portada.
2. **Version del juego.** Tu Steam esta en la rama **experimental**. Ver "Sobre la version experimental" al final.

## 1. Crear la pagina

1. Entra en <https://www.nexusmods.com/goingmedieval> con tu cuenta.
2. Arriba: **Mods > Upload a mod** (o el boton **Upload** / "Add a mod").
3. Juego: **Going Medieval**.

## 2. Pestana "Mod details"

| Campo | Que poner |
|---|---|
| Mod name | `Aldrich Minimap` |
| Version | `1.0.0` |
| Category | `User Interface` (si no existe, `Utilities`) |
| Language | English |
| Brief overview | `A minimap of your map: ground and water by colour, settlers and animals as dots, click to move the camera, zoom and ground names on hover.` |
| Author | `Aldrich` |
| Description | Pega **todo** el contenido de `DESCRIPCION_NEXUS.txt` |
| Tags | User Interface, Map, Minimap, Quality of Life, Utilities, BepInEx (en la pestana del mod, *Tags*; las que Nexus no tenga en su lista, se saltan) |
| Adult content | No |

Pulsa **Save** para seguir.

## 3. Pestana "Media"

Sube las capturas y marca una como **primary image** (portada).

## 4. Pestana "Files"

1. **Add file**.
2. Fichero: `AldrichMinimap_1.0.0.zip` de esta carpeta.
3. File name: `Aldrich Minimap`.
4. Version: `1.0.0`. Category: **Main files**.
5. Description del fichero: `Extract into the Going Medieval folder. Requires BepInEx 5 (x64).`
6. Sube y espera a que termine.

## 5. Pestana "Requirements"

1. **Add Nexus requirement** y busca **BepInEx** (la de *Modding Tools*, `site/mods/115`). Nota: `Required for any plugin to load. Install it first.`
2. Si no te deja enlazarla, en **Add off-site requirement** pon `BepInEx 5 (x64)` con el enlace de GitHub de arriba.

## 6. Pestana "Permissions"

Todo en **No** / "You are not allowed to...":

- Upload to other sites: **No**
- Modification: **No**
- Conversion: **No**
- Asset use: **No**
- Donation points / earning: a tu gusto (es solo si recibes puntos)

En *credits* no hace falta poner a nadie: todo el codigo es tuyo.

## 7. Publicar

1. Revisa la vista previa.
2. **Publish**. El mod queda en revision unos minutos; luego es publico.
3. Guarda el enlace del mod: es tu prueba de autoria y fecha si alguien lo resube.

## 8. Despues

- Si alguien lo resube en otro sitio, "Report" en esa pagina con el enlace al tuyo.
- Para una version nueva: sube el zip nuevo en **Files** como **Main file**, y el viejo a **Old versions**. Sube tambien el numero de version en la pagina.

---

## Sobre la version experimental

Tu Steam esta en la rama **experimental** (build 25501486), y el minimapa esta compilado y probado contra esa version del juego.

- **Que puede pasar:** el minimapa lee piezas internas del juego (el mapa de alturas, el agua, los tipos de suelo, la camara). Si la rama estable las tiene distintas, o una actualizacion las cambia, el minimapa **no aparece** y el log de BepInEx lo dice. No toca las partidas guardadas, asi que quitarlo siempre es seguro.
- **Lo mas probable:** que funcione en las dos ramas. Son piezas basicas del juego que no suelen cambiar, pero no esta comprobado en la estable.
- **Para salir de dudas antes de subir:** Steam > Going Medieval > Propiedades > Betas > **Ninguna**. Deja que actualice, prueba el minimapa en una partida nueva y luego vuelve a **experimental**.
- En la descripcion ya pone "Tested on 1.1.19"; cuando sepas en que ramas funciona, cambialo a
  `Tested on 1.1.19 (experimental and stable)` o solo la que hayas probado.
