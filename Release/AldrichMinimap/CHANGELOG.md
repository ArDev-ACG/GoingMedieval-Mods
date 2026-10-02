# Aldrich Minimap

Verificado contra Going Medieval **1.1.19**.

## [No publicado] - 2026-09-25

### 1.0.1 - sin datos de la maquina en la DLL
La 1.0.0 llevaba dentro la ruta del `.pdb` con la que se compilo, y en ella el nombre de usuario de Windows. Ahora se compila sin pdb y con las rutas mapeadas; comprobado que en el zip no queda ni ruta, ni usuario, ni correo. Nada cambia en el juego.

### 1.0.0 - primera publicacion (Nexus)
**Publicado en Nexus Mods el 2026-09-26: https://www.nexusmods.com/goingmedieval/mods/149**
Visto en partida. El minimapa, suelto: suelos por color (hierba, tierra, arena, arcilla, grava, roca, nieve, cultivo, tejado y agua en tres profundidades), criaturas por color, contorno de la camara, clic o arrastre para mover la camara, zoom de 1x a 6x, nombre del suelo bajo el raton y Ctrl+M para ocultarlo. Compila los mismos `Minimap.cs` y `Heartbeat.cs` que el plugin grande (`src/AldrichMinimap/`), y el grande no arranca el suyo si este esta instalado. Probado el 26 instalado como un jugador, con el plugin grande y el resto de mods apagados. El README avisa de que el mapa tarda de 1 a 10 s en verse bien al cargar.

**Ojo al probarlo con los demas mods activos y sin el plugin grande:** la carga se cuelga, pero no es el minimapa. Uno de los mods instalados declara almacenes con un grupo de recursos que no existe, y el hilo del almanaque revienta sin el parche `StorageGroupNullGuard` del plugin grande.

