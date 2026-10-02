# Modelos: como se abren y como se cambian

Tres cosas distintas que se mezclan siempre, y por eso este fichero:

1. **Las mallas del juego** - las de Foxy Voxel, que nuestros muebles toman
   prestadas. Viven dentro de los bundles del juego y hay que sacarlas.
2. **Las mallas de fuera** - las que descargamos y queremos meter, en
   `ASSESTS/Modelos Descomprimidos/`. Son FBX normales y se abren con Blender,
   pero **ninguna** vale tal cual: hay que medirla y rehacerla.
3. **Las mallas nuestras** - `aldrich_count_throne`, `aldrich_cat_statue`. No se
   editan con el raton: las escribe un script de Python que corre dentro de
   Blender, y ese script es el fuente.

Lo que esta instalado en esta maquina:

| | |
|---|---|
| Blender | `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` |
| Unity | `C:\Program Files\Unity 2022.3.46f1\Editor\Unity.exe` |
| Mods | `C:\Users\<usuario>\Documents\Foxy Voxel\Going Medieval\Mods\` |
| Juego | `C:\Program Files (x86)\Steam\steamapps\common\Going Medieval\` |

---

## 1. Abrir una malla del juego

No hay fichero que abrir: las mallas de Going Medieval estan empaquetadas en los
bundles de Unity del juego, bajo `Going Medieval_Data\StreamingAssets\aa\`. Se
sacan con un comando:

```
python tools/models/extract_reference.py quality_chair foxy_statue
```

Eso escribe `ASSESTS/Referencias/<nombre>.obj` y, mas importante, **imprime las
medidas por pantalla**:

```
foxy_statue          size  0.965 x  1.577 x  1.208   pivot +0.000 above base
cat_stuffed_trophy   size  0.264 x  0.505 x  0.604   pivot +0.251 above base
```

Hay que darle nombres. Para encontrar el nombre,
`python tools/open_assets.py` deja en `ASSESTS/Taller/<mueble>/` la malla
prestada de cada uno de nuestros muebles ya extraida, con el sufijo `_VANILLA`.

El `.obj` se abre en Blender con `File > Import > Wavefront (.obj)`, o con
cualquier visor 3D. **Es para mirar y medir, no para editar**: el juego no carga
ese fichero.

### La medida que importa

Una baldosa es **1.0 unidad**. Nada nuestro debe pasar de unos **0.95** de ancho
ni de fondo, o la vista previa de construccion pisa la casilla de al lado. De
alto no hay limite: una estatua puede ser alta, es lo que la hace estatua.

Y el pivote va en el suelo (`base_y = 0`). Una malla de inventario - un trofeo,
una botella - trae el pivote donde el artista lo necesitaba para que una mano lo
sujete, y eso a escala 1 son dos centimetros que nadie nota y a escala 4 es
media baldosa: la estatua se sale del pedestal y se hunde en el suelo.

---

## 2. Meter una malla de fuera

Lo descargado esta en `ASSESTS/Modelos Descomprimidos/`, una carpeta por
paquete, normalmente con esta forma:

```
<paquete>/source/<algo>.fbx        la malla
<paquete>/textures/*.png           sus texturas
```

Se abre con Blender: `File > Import > FBX (.fbx)`. Si el FBX viene sin texturas
puestas, las de `textures/` se asignan a mano en el editor de materiales: la que
se llama `*_color*` o `*_albedo*` es la que pinta, y las `*_nmap*`, `*_metal*` y
`*_roughness*` no se usan aqui.

**Ninguna de estas mallas vale tal cual, y conviene saberlo antes de perder una
tarde:**

- **La escala nunca coincide.** Un modelo de fuera esta en centimetros, o en
  metros, o en unidades de Max. Lo primero es `N` en Blender y mirar
  `Dimensions`; si no cae cerca de 1.0 por baldosa, hay que escalarlo.
- **El estilo tampoco.** Going Medieval es voxel legible: bloques grandes, pocos
  colores planos, sin suavizado. Un FBX de 80.000 triangulos con normal map sale
  como una mancha gris al lado de una silla del juego. Lo que se hace con una
  malla de fuera es usarla de **referencia de proporciones** y volver a
  construirla con cubos, que es exactamente lo que hacen
  `tools/models/build_count_throne.py` y `build_cat_statue.py`.
- **Los ejes y el origen, lo mismo.** Blender es Z arriba y el juego Y arriba
  (ver la trampa 1 de la seccion 4).

Si aun asi se quiere exportar una malla de fuera directamente, se exporta con
las mismas banderas de la seccion 4 y se deja en `ASSESTS/Modelos/` con nombre
`aldrich_<lo_que_sea>.fbx`. El nombre **es** la direccion con la que el JSON la
pide, asi que el prefijo no es decorativo.

---

## 3. Cambiar una malla nuestra

Las dos que existen hoy:

| Malla | Script | Mod |
|---|---|---|
| `aldrich_count_throne` | `tools/models/build_count_throne.py` | VampireCourt |
| `aldrich_cat_statue` | `tools/models/build_cat_statue.py` | CarrionAndPlague |

**El script es el fuente.** Abrir el `.fbx` en Blender, mover un vertice y
guardar no sirve: la siguiente vez que alguien corra el script se pierde, y el
script se corre cada vez que hay que tocar el bundle. Se edita el `.py`.

Los dos estan escritos igual y se leen de arriba abajo:

- primero un bloque de constantes con **el envelope en unidades de juego**: el
  tamano del voxel (`V = 0.080`), el ancho del plinto, la altura del tambor;
- despues los colores, que son tres o cuatro y nada mas;
- despues una funcion por pieza (`_head`, `_haunches`, `_tail`...) que apila
  cajas alineadas a los ejes sobre una rejilla de voxeles;
- al final el horneado de la paleta y la exportacion.

Para hacerlo mas grande, mas bajo o mas ancho se cambia una constante de arriba.
Para cambiarle la forma se toca la funcion de la pieza.

### Correrlo

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
    --python tools/models/build_cat_statue.py
```

`--background` es sin ventana. Tarda unos segundos y escribe:

- `ASSESTS/Modelos/aldrich_cat_statue.fbx` - lo que Unity empaqueta,
- `ASSESTS/Modelos/aldrich_cat_statue.obj` - lo mismo, para poder mirarlo,
- la textura de paleta, **directamente dentro del mod**.

### La paleta 2x2, que no se pinta

La albedo de una malla nuestra es un PNG de 64x64 con **cuatro cuadros de color
plano**, y cada cara de la malla tiene las UV clavadas en el centro de uno de los
cuatro. No es un desplegado: pintar encima con GIMP no pinta el mueble, solo
cambia los cuatro colores, y mal. Los colores estan en el script de Blender de
esa malla y en ningun otro sitio.

Eso ya costo una ronda: `tools/textures/build_textures.py` tenia entradas para el
trono y para el gato que regeneraban su albedo desde una textura de vanilla -
cuatro cuadros planos sustituidos por una silla y un gato, y el modelo sale
embarrado. **Si una malla es nuestra, su textura la escribe el script de la malla
y nadie mas.**

Y la cuarta casilla existe: el gato tuvo los ojos en la malla dos dias sin que se
vieran porque `bake_albedo` escribia `[FUR, STONE, DARK, FUR]`, o sea que el
blanco de los ojos apuntaba al dorado del pelaje. Ojo dorado sobre cara dorada:
sin cara.

---

## 4. Del FBX al juego

Blender escribe el FBX; **Unity lo mete en un bundle y el juego solo carga
bundles**. Unity aqui no se usa para modelar ni para pintar, y no hace falta
abrir la ventana:

```
"C:/Program Files/Unity 2022.3.46f1/Editor/Unity.exe" -batchmode -quit -nographics ^
    -projectPath tools/unity/AldrichBundles ^
    -executeMethod Aldrich.BuildModBundles.Run -logFile -
```

Escribe, en cada mod que tenga mallas:

```
<Mods>/<mod>/Data/AddressableAssets/catalog.json
<Mods>/<mod>/Data/AddressableAssets/aldrichmeshes<mod>_assets_all_<hash>.bundle
```

Y despues, siempre:

```
python tools/validate/check_refs.py
```

que avisa si un `baseMesh` apunta a algo que no esta en ningun catalogo. Una
malla que falta **no da error de ninguna clase**: `MeshRepository.GetByID`
devuelve null y el edificio se construye invisible, sin excepcion, sin aviso y
sin una linea en el log.

### Las seis trampas de este paso

Todas costaron al menos una partida.

1. **`bake_space_transform=True` es lo que pone la malla de pie.** Blender es Z
   arriba y el juego Y arriba, y `axis_up="Y"` por si solo **no mueve un
   vertice**: escribe la correccion de noventa grados en el *nodo* del objeto
   exportado, y el juego no lee el nodo - saca un `Mesh` del bundle por nombre y
   lo pone en su propio prefab, asi que la malla llega tumbada. Con la bandera
   puesta la rotacion va a los vertices. Y entonces hay que corregir la escala:
   la misma bandera se lleva tambien la conversion a centimetros, asi que
   `global_scale=0.01`, o el trono sale **cien veces** mas grande.

2. **La malla se comprueba dentro del bundle, no en Blender.** Lo unico que
   demuestra que la exportacion salio bien es abrir el `.bundle` publicado y
   medir:

   ```
   python -c "import UnityPy, glob; [print(o.read().m_Name, o.read().m_LocalAABB.m_Extent) for p in glob.glob(r'C:\Users\<usuario>\Documents\Foxy Voxel\Going Medieval\Mods\*\Data\AddressableAssets\*meshes*.bundle') for o in UnityPy.load(p).objects if o.type.name == 'Mesh']"
   ```

   Tiene que ser ancho en x, **alto en y** y con `base_y` en 0. Los tres errores
   del trono - tumbado, cien veces grande y con el pivote a -0.46 - se ven todos
   ahi y ninguno se ve antes.

3. **Un mod, un catalogo, y nada compartido.** `MeshRepository` es global, asi
   que una malla que envia un mod resuelve para un edificio declarado por otro -
   hasta que alguien instala solo el segundo, y entonces el edificio sale
   invisible sin una linea en el log. `BuildModBundles` construye una vez por
   mod, y **el nombre del grupo de Addressables es el nombre interno del
   bundle**: dos bundles con el mismo nombre dentro y Unity carga el primero y
   rechaza el resto (`can't be loaded because another AssetBundle with the same
   files is already loaded`).

4. **Solo puede haber un `.json` en `AddressableAssets`.**
   `AddressableModManager` coge **el primero** que enumere como catalogo, y la
   build de Addressables deja `catalog.json` y `settings.json` juntos. El
   `.hash` no estorba porque no es `.json`. La version de Addressables esta
   clavada en 1.22.3, que es la que lleva el juego: un catalogo no es mas
   portable que el runtime que lo lee.

5. **La ruta de carga de un bundle de mod es plana.** Hay que usar
   `{NSMedieval.Modding.ModdingUtils.LoadPath}` y dejar los bundles **al lado
   del catalogo, sin subcarpeta**. Con la ruta por defecto el catalogo carga, la
   entrada se registra, `TryAddMesh` falla y el edificio sale invisible.

6. **Blender X es -X del juego, y el origen del edificio no es el centro de su
   huella.** Las dos mitades de lo mismo, y las dos se midieron en vez de
   suponerse. La segunda: el juego planta la malla sobre la **primera** baldosa
   del rectangulo, no en su centro, asi que el centro cae en +(baldosas-1)/2 -
   `wooden_well` va de -0.414 a +2.436 en X de Unity y su
   `boxColliderSettings.centerOffset.x` del JSON vale 1, que es el mismo numero
   dicho dos veces. Una pieza nuestra centrada en el origen sale por tanto una
   baldosa corrida, que fue `Evidencias/PozoDesalineado.png`. La primera: para
   saber hacia que lado corregir se leyo el bundle ya construido con UnityPy
   buscando una pieza asimetrica - la manivela del pozo, modelada en x=+0.98 -
   y en el juego sale en x=-1.08. `gm_model.ground()` toma hoy la huella en
   baldosas y hace las dos correcciones; en `PIECES` cada pieza lleva su `size`
   del JSON. Y como UnityPy niega la X al escribir un OBJ, los OBJ de
   `ASSESTS/Referencias` estan ya en ese espacio: el centro que hay que copiar
   es literalmente el que mide la pieza de vanilla.

---

## 5. Que malla lleva puesta cada mueble

Un mueble nuestro es **un prefab de vanilla con una malla encima**, y las dos
mitades se deciden en sitios distintos:

- el **prefab** (`prefabID`) decide lo que el mueble *hace* - una cama, una
  decoracion, un puesto de produccion - y trae sus componentes puestos, cada uno
  pidiendo su id (`decorationComponentID`, `productionComponentID`,
  `rugComponentID`). Uno que falte es una NullReferenceException por frame
  dentro de un paso que el juego **reintenta**: 9792 excepciones y la partida
  colgada.
- la **malla** es un `slot` de tipo `Mesh` llamado `baseMesh` dentro de
  `variationLists`, y su valor es el nombre con el que se publico en el bundle.

Las dos se escriben desde `tools/buildings/build_furniture.py`, que **copia la
entrada de vanilla entera** y solo cambia lo que hace falta - una entrada de
edificio tiene unos sesenta campos, y escribirlos a mano son sesenta ocasiones
de olvidar uno:

```
python tools/buildings/build_furniture.py          # escribe en los mods
python tools/buildings/build_furniture.py <dir>    # ensayo, sin tocar nada
```

Para que un mueble estrene malla propia se le anade al `SPECS` de ese fichero:

```python
"slots": {"baseMesh": "aldrich_lo_que_sea", "albedo": "aldrich_lo_que_sea_albedo"},
```

Y **la caja de seleccion sale del JSON, no del codigo**: `boxColliderSettings` es
lo primero que lee `InitModelSettings`, antes de armar el modelo. Cambiar el
tamano de la malla y no tocar ese bloque deja la caja del tamano anterior, y lo
que cuelga de la caja - el marcador de destruccion y el indicador de recursos -
se coloca sobre `box.center`, o sea en el suelo al lado de la estatua.

---

## 6. El camino corto

Cambiar la forma de una malla nuestra, de principio a fin:

```
1. editar        tools/models/build_<lo_que_sea>.py
2. blender       "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" ^
                     --background --python tools/models/build_<lo_que_sea>.py ^
                     -- <la pieza> --render
2b. mirar        ASSESTS/Vistas/<la pieza>.png antes de seguir
3. unity         "C:/Program Files/Unity 2022.3.46f1/Editor/Unity.exe" -batchmode ^
                     -quit -nographics -projectPath tools/unity/AldrichBundles ^
                     -executeMethod Aldrich.BuildModBundles.Run -logFile -
4. medir         el bundle, con el comando de UnityPy de la trampa 2
5. comprobar     python tools/validate/check_refs.py
6. jugar
```

Si ademas cambio de tamano, entre el 3 y el 5 hay que actualizar
`boxColliderSettings` en `tools/buildings/build_furniture.py` y volver a
correrlo.

---

## 7. Los diecisiete modelos, uno a uno

Todo lo que tiene malla propia hoy sale de **tres scripts**, y ninguno se edita
con el raton:

| Script | Que lleva |
|---|---|
| `tools/models/build_court.py` | quince piezas: la corte, la pira y las garras |
| `tools/models/build_count_throne.py` | solo el trono |
| `tools/models/build_cat_statue.py` | solo la estatua del gato |

Los dos sueltos son anteriores a `gm_model.py` y por eso llevan su propia copia
de las funciones de dibujo. Se corren igual, pero **el trono y el gato no
aceptan argumentos ni `--render`**: el script entero es una sola pieza y su
exportador es propio. Para verlos hay que abrir su `.obj` a mano.

### El bucle, que es el mismo para las diecisiete

```
1. LLEGAR      abrir el .py que dice la ficha de abajo, en la linea que dice
2. EDITAR      cambiar constantes (tamano) o el cuerpo de la funcion (forma)
3. GUARDAR     guardar el .py. Nada mas. El .fbx NO se toca nunca
4. HORNEAR     blender --background --python <ese script> -- <la pieza> --render
5. MIRAR       ASSESTS/Vistas/<pieza>.png, que es lo que se va a ver
6. LEER        la salida por pantalla: size, base at, y ningun WARNING
7. AVISARME    "toque <pieza>" y yo hago Unity + medir + check_refs
```

**El paso 3 es el que se olvida y el que duele.** `ASSESTS/Modelos/*.fbx` es
salida, no fuente: se reescribe entero en el paso 4. Abrir ese fbx en Blender,
mover un vertice y guardar es trabajo que se pierde en el siguiente horneado,
sin aviso.

**El paso 5 es el que no habia.** Hasta hoy el primer vistazo a una pieza
llegaba al final de los cinco pasos, cargando partida. `--render` escribe un
PNG en `ASSESTS/Vistas/` y se puede mirar ahi mismo.

Lo que se renderiza **no es el objeto de Blender: es lo que el juego dibuja**.
La malla acabada tiene un solo material - el color vive en las UV - asi que
renderizarla tal cual daria una mancha de un color. El render monta el mismo
material que monta el juego: el PNG de paleta leido por esas mismas UV. Por eso
sirve para lo que ninguna medida sirve: **una paleta mal ordenada se ve**. Es
como se encontro que el pozo tenia el tejado de sangre y el agua de madera.

La baldosa dibujada debajo es de verdad, del tamano que el juego llama 1.0: una
pieza que se sale de su huella se sale visiblemente de un cuadro.

**El paso 6 sigue sin ser opcional.** `gm.report` imprime el tamano real y lo
compara con la huella; un `WARNING: ... se sale de su huella` significa que la
caja de seleccion del edificio se va a quedar corta, y eso el render no lo
dice.

### La paleta, y por que ya no se puede descuadrar

El orden de los colores era **dos fuentes de verdad que se contradecian**: la
pieza declaraba `mats(STONE, WOOD, BLOOD)` y `bake_albedo()` pintaba los
cuadros en ese orden, pero `bpy.ops.object.join()` no anade las ranuras de
material en el orden de la lista que se le pasa - las anade en el orden en que
esos objetos estan en la escena, o sea en el que se creo su geometria. Una
pieza que crea el liquido antes que el armazon acababa con las ranuras en 0, 2,
1, y cada grupo leyendo el cuadro del otro.

Eso era el pozo con el tejado de sangre y el agua de madera, y era el gato dos
dias sin ojos. `palette_uv()` ya no mira la posicion de la ranura: ordena por
el orden de creacion de los materiales, que es el orden en que la pieza los
declaro y el mismo que usa `bake_albedo()`. Una sola fuente de verdad.

De las quince, dos estaban mal (`blood_well` y `count_coffin`) y trece bien -
por casualidad, porque en esas trece el orden de creacion coincidia con el
declarado. Por eso nunca se noto.

### Como se lee una ficha

- **linea** - donde empieza la funcion de esa pieza dentro del script.
- **colores** - los tres o cuatro nombres que esa pieza saca del bloque de
  colores de la cabecera (`build_court.py`, lineas 49-61). Cambiar `CRIMSON`
  ahi lo cambia en **todas** las piezas que lo usan; para cambiarlo en una
  sola, se le pasa otro nombre en el `return` de esa funcion.
- **huella** - el maximo en baldosas. Pasarse es el `WARNING` del paso 5.
- **vanilla** - la malla contra la que se midio. Se vuelve a sacar con
  `python tools/models/extract_reference.py <nombre>`.
- **albedo** - donde cae la paleta 2x2 de esa pieza. Siempre dentro del mod
  dueno, nunca en `ASSESTS/`.
- **JSON** - quien escribe la entrada del edificio. Dos casos, y confundirlos
  cuesta el cambio entero:
  - *generado* - la entrada la escribe `tools/buildings/build_furniture.py`.
    Editar el JSON del mod a mano **se pierde** la proxima vez que se corra.
    Se edita el `SPECS` del script.
  - *a mano* - la entrada vive solo en el `BaseBuildingRepository.json` del
    mod y se edita ahi.

---

### VampireCourt - las trece de la corte

Todas: `tools/models/build_court.py`, albedo en
`Mods/VampireCourt/Data/Textures/<nombre>_albedo.png`.

Horneado individual, sin esperar a las quince:

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
    --python tools/models/build_court.py -- count_coffin
```

El argumento vale con prefijo o sin el: `count_coffin` y `aldrich_count_coffin`
son lo mismo. Se pueden encadenar varios separados por espacio.

#### 1. `aldrich_count_coffin` - el ataud

| | |
|---|---|
| linea | `build_court.py:74` |
| colores | `STONE, IRON, CRIMSON` |
| huella | 2.95 x 1.00 |
| vanilla | `limestone_sarcophagus` 2.600 x 0.829 x 1.000, pivote en el suelo |
| JSON | generado - `build_furniture.py:55` |

#### 2. `aldrich_count_crypt` - la cripta

| | |
|---|---|
| linea | `build_court.py:119` |
| colores | `STONE, GOLD, BONE` |
| huella | 2.95 x 1.00 |
| vanilla | `limestone_sarcophagus` 2.600 x 0.829 x 1.000 |
| JSON | generado - `build_furniture.py:212` |

#### 3. `aldrich_blood_altar` - el altar

| | |
|---|---|
| linea | `build_court.py:161` |
| colores | `DARKSTONE, IRON, BLOOD` |
| huella | 2.95 x 1.95 (edificio de 3x2) |
| vanilla | `butchering_table` 3.041 x 2.411 x 1.836 |
| JSON | **a mano** - `Mods/VampireCourt/.../BaseBuildingRepository.json:219` |

#### 4. `aldrich_blood_circle` - el circulo ritual

| | |
|---|---|
| linea | `build_court.py:199` (la funcion se llama `blood_ritual_circle`) |
| colores | `DARKSTONE, BLOOD, WAX` |
| huella | 2.99 x 2.99 |
| vanilla | `pagan_ritual_circle` 3.134 x 0.297 x 3.023 |
| JSON | **a mano** - `Mods/VampireCourt/.../BaseBuildingRepository.json:488` |

Es la pieza mas plana que hay: 0.297 de alto en vanilla. Subirla la convierte
en un bordillo; lo que se cambia aqui es el dibujo, no la altura.

#### 5. `aldrich_court_banner` - el estandarte de pie

| | |
|---|---|
| linea | `build_court.py:243` |
| colores | `WOOD, CRIMSON, GOLD` |
| huella | 0.99 x 0.99 |
| vanilla | `banner_single_large` 1.070 x 2.981 x 0.190 |
| JSON | generado - `build_furniture.py:75` |

#### 6. `aldrich_court_banner_wall` - el estandarte de pared

| | |
|---|---|
| linea | `build_court.py:282` |
| colores | `WOOD, CRIMSON, GOLD` |
| huella | 0.99 x 0.99 |
| vanilla | `banner_wall_large` 0.079 x 2.320 x 1.070, **base en +0.577** |
| JSON | generado - `build_furniture.py:93` |

**Su base no es cero.** Va en la tabla `PIECES` (`base_y=0.577`) porque cuelga
del muro; ponerla a cero la deja plantada a los pies de la pared.

#### 7. `aldrich_crimson_candle` - el candelabro

| | |
|---|---|
| linea | `build_court.py:321` |
| colores | `IRON, WAX, BLOOD` |
| huella | 0.99 x 0.99 |
| vanilla | `iron_candle` 0.837 x 2.089 x 0.837 |
| JSON | generado - `build_furniture.py:138` |

#### 8. `aldrich_blood_brazier` - el brasero

| | |
|---|---|
| linea | `build_court.py:365` |
| colores | `IRON, BLOOD, WAX` |
| huella | 0.99 x 0.99 |
| vanilla | `iron_brazear` 0.913 x 0.914 x 0.913 |
| JSON | generado - `build_furniture.py:157` |

#### 9. `aldrich_veiled_mirror` - el espejo velado

| | |
|---|---|
| linea | `build_court.py:410` |
| colores | `GOLD, CRIMSON, SILVER` |
| huella | 0.99 x 0.99 |
| vanilla | `silver_mirror_wall` 0.102 x 1.498 x 0.928, **base en +1.122** |
| JSON | generado - `build_furniture.py:176` |

La otra pieza de pared. Misma trampa que el estandarte de muro.

#### 10. `aldrich_court_reliquary` - el relicario

| | |
|---|---|
| linea | `build_court.py:442` |
| colores | `WOOD, GOLD, BONE` |
| huella | 0.99 x 0.99 |
| vanilla | `relic_shelf` 0.671 x 1.965 x 0.702 |
| JSON | generado - `build_furniture.py:196` |

#### 11. `aldrich_blood_well` - el pozo

| | |
|---|---|
| linea | `build_court.py:482` |
| colores | `STONE, WOOD, BLOOD` |
| huella | 2.95 x 1.60 |
| vanilla | `wooden_well` 2.850 x 2.401 x 1.506 |
| JSON | generado - `build_furniture.py:228` |

#### 12. `aldrich_vigil_table` - la mesa de vigilia

| | |
|---|---|
| linea | `build_court.py:534` |
| colores | `DARKSTONE, GOLD, WAX` |
| huella | 4.95 x 1.95 (edificio de 5x2, la pieza mas larga) |
| vanilla | `table_2x5_stone` 4.849 x 0.824 x 1.804 |
| JSON | generado - `build_furniture.py:280` |

Los candelabros de encima se quedan **por debajo de 1.30 a proposito**: vanilla
mide 0.824 de alto y la caja de seleccion sale del JSON, asi que todo lo que
sobresalga mucho se queda fuera de la caja.

#### 13. `aldrich_impaled_stake` - el empalado

| | |
|---|---|
| linea | `build_court.py:577` |
| colores | `WOOD, FLESH, LINEN, BLOOD` - los cuatro cuadros de la paleta |
| huella | 1.80 x 0.99 |
| vanilla | `scarecrow` 2.235 x 2.942 x 0.704 |
| JSON | generado - `build_furniture.py:295` |

Va **vestido**, y la ropa no se pone encima de la carne: la sustituye. Sayo,
cinto, faldon, mangas y calzon son `LINEN` y ocupan el sitio del torso y de los
brazos de arriba; quedan de carne la cabeza, el cuello, los antebrazos y las
pantorrillas. Dos cajas en el mismo sitio con dos materiales distintos es un
parpadeo de profundidad, no una camisa.

Y los brazos **nacen del hombro y bajan**. Eran una sola caja de 0.62 centrada
en el pecho (z 2.06), asi que asomaban por encima del hombro y acababan a media
espalda - leido desde el juego, un brazo de abajo hacia arriba, que es lo que se
reporto. Ahora son dos tramos encadenados desde `SHOULDER_Z` (2.34) hacia el
suelo, cada uno colgado del final del anterior, y la mano queda por debajo de la
cadera.

#### 14. `aldrich_count_throne` - el trono

Este **no** esta en `build_court.py`.

| | |
|---|---|
| script | `tools/models/build_count_throne.py` - script entero, una sola pieza |
| constantes | lineas 61-74: `WIDTH` 0.94, `DEPTH` 0.96, `HEIGHT` 2.05, `SEAT_Z` 0.46, `PLINTH_Z` 0.13, `ARM_Z` 0.74 |
| colores | `BONE, STONE, GOLD` (lineas 76-78) |
| forma | `build()` en la linea 298 |
| albedo | `Mods/VampireCourt/Data/Textures/aldrich_count_throne_albedo.png` |
| vanilla | `quality_chair` 0.749 x 0.901 x 1.484; `foxy_statue` 1.577 de alto |
| JSON | generado - `build_furniture.py:108` |

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
    --python tools/models/build_count_throne.py
```

**Es el unico con `boxColliderSettings` propio**, en `build_furniture.py:129`.
Si le cambias `WIDTH`, `DEPTH` o `HEIGHT`, ese bloque hay que tocarlo tambien y
volver a correr `build_furniture.py`, o la caja de seleccion se queda del
tamano viejo.

---

### Gravedigger

#### 15. `aldrich_mass_pyre` - la pira colectiva

| | |
|---|---|
| linea | `build_court.py:622` |
| colores | `WOOD, LINEN, EMBER` |
| huella | 2.95 x 1.95 (edificio de 3x2) |
| vanilla | `pyre` 2.520 x 0.900 x 1.863 |
| albedo | `Mods/Gravedigger/Data/Textures/aldrich_mass_pyre_albedo.png` |
| JSON | generado - `build_furniture.py:326` |

Sale del mismo script que la corte pero **su textura va a otro mod**. La tabla
`PIECES` es quien lo decide (`"Gravedigger"`); si se copia una ficha para hacer
una pieza nueva, ese campo es el primero que se olvida, y el sintoma es un
mueble gris para quien instale solo uno de los dos mods.

---

### UndeadHorde

#### 16. `undead_claws` - las garras del alzado

| | |
|---|---|
| linea | `build_court.py:690` |
| colores | `IRON`, y nada mas |
| huella | 0.40 x 0.40 |
| vanilla | `dagger` 0.120 x 0.414 x 0.015 |
| albedo | `Mods/UndeadHorde/Data/Textures/undead_claws_albedo.png` |
| JSON | es un recurso, no un edificio: `Mods/UndeadHorde/Data/Models/Resources.json` |

Tres cosas que no se parecen a ninguna otra pieza:

1. **Sin prefijo `aldrich_`.** El nombre de la malla tiene que ser el que dice
   la ranura `baseMesh` de su ficha, y esa dice `undead_claws`.
2. **La ranura vive en `Resources.json`, no en un edificio.** Y era lo que
   faltaba: un arma equipada **si** tiene `variationLists`, igual que un
   edificio. `EquipmentView.Setup` llama a
   `ChangeMeshAccordingToResourceQuality`, que lee
   `Resource.VariationsById["QualityVariations"]` y de ahi saca `baseMesh`; el
   dagger de vanilla dice ahi `"value": "dagger"`. Nuestra ficha no tenia
   ninguna lista, asi que **nadie aplicaba nada** y el prefab se quedaba con la
   malla que traia - la daga en la mano derecha que se reporto el 17. Escritas
   las dos listas, la malla entra por su nombre y el color por la ranura
   `albedo`, que `MeshVariationHandler.UpdateTextures` resuelve por
   `TextureRepository` - el mismo sitio donde `ModTextures` registra los PNG
   sueltos de cada mod. Asi que hoy si tiene paleta propia y `CLAW_STEEL` ya no
   existe.
3. **Del tamano de un dagger, no de una maza.** El hueco de la mano es el mismo
   para toda arma de una mano: una garra de 0.70 sale del brazo. Y el
   `transformSettingsArray` es el del dagger, que es lo que la tumba 90 grados
   dentro del puno.

Y el nombre no es solo el de la ficha del JSON: **es el nombre del objeto de
Blender**, porque eso es lo que acaba dentro del bundle. Las garras se
publicaban como `steel_group` - eran la unica pieza que devolvia el grupo de
pintura en vez de pasar por `gm.join(..., "<nombre>")` - y una malla que no
aparece con su nombre sale invisible sin una sola linea en el log.
`check_refs.py` no lo ve: comprueba la direccion del catalogo, no el nombre de
la malla de dentro. Lo unico que lo enseño fue medir el bundle (trampa 2).

Y lo de anoche vive aqui: este recurso dice `hasQuality` y no tiene
`materials`, asi que es un proto puro - exactamente el caso que
`QualityBaseGuard` repara. Si alguien le anade `materials` al JSON, el proto
pasa a generar variantes y deja de ser el que acaba en el suelo.

---

### CarrionAndPlague

#### 17. `aldrich_cat_statue` - la estatua del gato

| | |
|---|---|
| script | `tools/models/build_cat_statue.py` - script entero, una sola pieza |
| constantes | lineas 47-62: `V` 0.080 (el voxel; el gato mide quince), `DRUM_R` 0.430, `DRUM_H` 0.320, `BASE_H` 0.075, `PLINTH_W` 0.960, `PLINTH_H` 0.070 |
| colores | `FUR, STONE, DARK, WHITE` (lineas 67-74) |
| forma | `build()` en la linea 174, con una funcion por pieza dentro |
| albedo | `Mods/CarrionAndPlague/Data/Textures/aldrich_cat_statue_albedo.png` |
| vanilla | `foxy_statue` 0.965 x 1.208 x 1.577; `cat_stuffed_trophy` 0.264 x 0.505 x 0.604 con el pivote a +0.251 |
| JSON | **a mano** - `Mods/CarrionAndPlague/.../BaseBuildingRepository.json:114` |

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background ^
    --python tools/models/build_cat_statue.py
```

**Es la unica con cuatro colores**, y por eso la unica donde el orden de la
paleta se puede equivocar en silencio: `bake_albedo` escribia
`[FUR, STONE, DARK, FUR]` y el blanco de los ojos apuntaba al dorado del
pelaje. Ojo dorado sobre cara dorada: dos dias sin ojos y sin un solo error en
ningun sitio. Si tocas los colores, cuenta los cuatro.

---

## 8. Como me lo pasas

Lo tuyo termina en el paso 5: el `.py` guardado y la salida del horneado. Lo de
despues - Unity, medir el bundle, `check_refs` - lo corro yo, y es donde estan
las cinco trampas de la seccion 4.

**Para que lo suba:**

```
"toque aldrich_blood_well, mira a ver"
```

Con eso hago, en este orden:

1. Leer el `.py` y comparar con lo que habia, para saber que cambiaste antes de
   creerme el resultado. (No hay git en esta carpeta, asi que es a ojo sobre el
   fichero; si algun dia lo hay, es un `git diff`.)
2. El horneado otra vez en mi lado, **solo de esa pieza**. Si tu salida y la
   mia no dicen el mismo `size`, una de las dos maquinas tiene otra version del
   script y paramos ahi.
3. Unity, la build de bundles.
4. Las medidas **dentro del bundle**, con el comando de UnityPy de la trampa 2.
   Es lo unico que demuestra que la exportacion salio bien, y donde se ven los
   tres errores clasicos: tumbada, cien veces grande, o el pivote fuera del
   suelo.
5. `python tools/validate/check_refs.py`.
6. Si cambiaste el tamano y la pieza tiene `boxColliderSettings` propio - hoy
   solo el trono - `build_furniture.py` tambien.

**Si no quieres esperar a que yo mire**, los pasos 3 a 5 son los tres comandos
de la seccion 6 y se pueden correr solos. Lo que no conviene es saltarse el 4:
un bundle mal exportado se ve igual de bien en Blender.

**Lo que no hace falta que me mandes:** el `.fbx`, el `.obj` ni el `.png`. Los
tres son salida y se regeneran en un segundo; lo unico irreemplazable es el
`.py`.
