# El taller

Copias de todo lo que se puede editar de nuestros muebles, una carpeta
por mueble. **Esto es una copia**: editar aqui no cambia el juego. Al lado
de cada fichero esta el comando que lo vuelve a generar, y ese comando es
tambien el que **machaca** lo que edites a mano, asi que hay que mirarlo
antes de tocar nada.

Regenerar el taller: `python tools/open_assets.py`

## Como se abre cada cosa

| Fichero | Con que se abre | Que hacer |
|---|---|---|
| `*.obj` | Blender (`File > Import > Wavefront .obj`), o cualquier visor 3D | Es para **mirar y medir**. La malla de verdad la escribe el script de Python que dice el indice; se edita ese script y se vuelve a ejecutar. |
| `*.fbx` | Unity o Blender | Lo que Unity mete en el bundle. Sale del `.obj` de al lado; no se edita a mano. |
| `*_VANILLA.obj` | Blender | La malla que el mueble toma prestada del juego, sacada de sus bundles. Es la **referencia de tamano**: se importa en Blender y se modela contra ella. |
| `*_albedo.png` de 64x64 | cualquier editor, pero **no lo hagas** | Es una paleta 2x2: cada cara de la malla apunta al centro de un cuadro. Pintar encima no pinta el mueble, cambia los cuatro colores. Estan en el script de Blender de esa malla. |
| `*_albedo.png` grande | GIMP, Krita, Photoshop | Textura de verdad, sobre el desplegado de la malla prestada. La genera `tools/textures/build_textures.py` a partir de la textura de vanilla. |
| `aldrich_icon_*.png` | GIMP, Krita, Photoshop | El icono del menu de construccion, 128x128. Lo dibuja `tools/icons/build_icons.py` con codigo, no a pincel: se edita la funcion `art_<mueble>` de ese fichero. |

## Y para que sirve Unity

Unity **no** se usa para modelar ni para pintar. Lo unico que hace aqui es
empaquetar los `.fbx` en un bundle que el juego pueda cargar, y eso ya no
necesita abrir la ventana:

```
"C:/Program Files/Unity 2022.3.46f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath tools/unity/AldrichBundles -executeMethod Aldrich.BuildModBundles.Run -logFile -
```

Si quieres abrirlo a mano de todas formas, el proyecto esta en
`tools/unity/AldrichBundles` y las mallas importadas en `Assets/Models`.

## Los muebles

### cat_statue

Mod **CarrionAndPlague**, sobre el prefab `base_decoration`. Carpeta: `ASSESTS/Taller/cat_statue/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `aldrich_cat_statue.obj` | malla NUESTRA | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/models/build_cat_statue.py` |
| `aldrich_cat_statue_albedo.png` | paleta del modelo | `la escribe el mismo script de Blender que la malla (paleta 2x2, no editar a mano)` |
| `aldrich_icon_cat_statue.png` | icono del menu | `python tools/icons/build_icons.py` |

### mass_grave

Mod **Gravedigger**, sobre el prefab `unmarked_grave`. Carpeta: `ASSESTS/Taller/mass_grave/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `unmarked_grave_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_icon_mass_grave.png` | icono del menu | `python tools/icons/build_icons.py` |

### blood_altar

Mod **VampireCourt**, sobre el prefab `base_production_structure`. Carpeta: `ASSESTS/Taller/blood_altar/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `butchering_table_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_blood_altar_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_blood_altar.png` | icono del menu | `python tools/icons/build_icons.py` |

### blood_brazier

Mod **VampireCourt**, sobre el prefab `brazier_element`. Carpeta: `ASSESTS/Taller/blood_brazier/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `iron_brazear_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_blood_brazier_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_blood_brazier.png` | icono del menu | `python tools/icons/build_icons.py` |

### blood_ritual_circle

Mod **VampireCourt**, sobre el prefab `pagan_ritual_circle`. Carpeta: `ASSESTS/Taller/blood_ritual_circle/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `pagan_ritual_circle_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_blood_circle_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_blood_circle.png` | icono del menu | `python tools/icons/build_icons.py` |

### blood_well

Mod **VampireCourt**, sobre el prefab `base_decoration`. Carpeta: `ASSESTS/Taller/blood_well/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `wooden_well_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_blood_well_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_blood_well.png` | icono del menu | `python tools/icons/build_icons.py` |

### count_coffin

Mod **VampireCourt**, sobre el prefab `bed_element`. Carpeta: `ASSESTS/Taller/count_coffin/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `limestone_sarcophagus_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_icon_count_coffin.png` | icono del menu | `python tools/icons/build_icons.py` |

### count_crypt

Mod **VampireCourt**, sobre el prefab `limestone_sarcophagus`. Carpeta: `ASSESTS/Taller/count_crypt/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `limestone_sarcophagus_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_icon_count_crypt.png` | icono del menu | `python tools/icons/build_icons.py` |

### count_throne

Mod **VampireCourt**, sobre el prefab `chair`. Carpeta: `ASSESTS/Taller/count_throne/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `aldrich_count_throne.obj` | malla NUESTRA | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/models/build_count_throne.py` |
| `aldrich_count_throne_albedo.png` | paleta del modelo | `la escribe el mismo script de Blender que la malla (paleta 2x2, no editar a mano)` |
| `aldrich_icon_count_throne.png` | icono del menu | `python tools/icons/build_icons.py` |

### court_banner

Mod **VampireCourt**, sobre el prefab `banner_element`. Carpeta: `ASSESTS/Taller/court_banner/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `banner_single_large_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `base_texture_png.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_court_banner.png` | icono del menu | `python tools/icons/build_icons.py` |

### court_banner_wall

Mod **VampireCourt**, sobre el prefab `banner_element_wall`. Carpeta: `ASSESTS/Taller/court_banner_wall/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `banner_wall_large_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `base_texture_png.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_court_banner_wall.png` | icono del menu | `python tools/icons/build_icons.py` |

### court_reliquary

Mod **VampireCourt**, sobre el prefab `standing_shelf`. Carpeta: `ASSESTS/Taller/court_reliquary/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `relic_shelf_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_court_reliquary_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_court_reliquary.png` | icono del menu | `python tools/icons/build_icons.py` |

### crimson_candle

Mod **VampireCourt**, sobre el prefab `standing_torch`. Carpeta: `ASSESTS/Taller/crimson_candle/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `iron_candle_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_crimson_candle_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_crimson_candle.png` | icono del menu | `python tools/icons/build_icons.py` |

### impaled_stake

Mod **VampireCourt**, sobre el prefab `scarecrow`. Carpeta: `ASSESTS/Taller/impaled_stake/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `scarecrow_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_impaled_stake_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_impaled_stake.png` | icono del menu | `python tools/icons/build_icons.py` |

### veiled_mirror

Mod **VampireCourt**, sobre el prefab `base_wall_decoration`. Carpeta: `ASSESTS/Taller/veiled_mirror/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `silver_mirror_wall_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_veiled_mirror_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_veiled_mirror.png` | icono del menu | `python tools/icons/build_icons.py` |

### vigil_table

Mod **VampireCourt**, sobre el prefab `table_element`. Carpeta: `ASSESTS/Taller/vigil_table/`

| Fichero | Que es | Como se regenera |
|---|---|---|
| `table_2x5_stone_VANILLA.obj` | malla PRESTADA del juego | `no se toca: es una malla del juego. Para hacerla nuestra, copiar tools/models/build_cat_statue.py como punto de partida` |
| `aldrich_vigil_table_albedo.png` | textura generada | `python tools/textures/build_textures.py` |
| `aldrich_icon_vigil_table.png` | icono del menu | `python tools/icons/build_icons.py` |

La entrada JSON de todos ellos - coste, tamano, colisionador,
nombre y descripcion - sale de `tools/buildings/build_furniture.py`,
y se reescribe con `python tools/buildings/build_furniture.py`.
