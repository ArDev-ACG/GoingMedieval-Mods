# Aldrich.GMPlugins (el plugin)

Verificado contra Going Medieval **1.1.19**.

## [No publicado] - 2026-09-27

### Cambiado - una DLL por mod
Cada mod funciona solo, con BepInEx y nada mas: `src/Mods/<Mod>/` compila el nucleo comun (`Plugin.cs`, `Heartbeat`, `GameAccess`, `Census`, `Shared`, las guardas y la consola) mas los ficheros de ese mod. Los parches comunes los aplica solo la primera DLL que carga (`Shared.Claim`). Las reglas de no-muerto que comparten alzados y vampiros van en `Plugin.Kin.cs`. Fuera: `AnimalSpawnProbe` y `ModMeshMaterialProbe`, y el minimapa, que es el plugin suelto de Nexus. Las DLL se compilan sin pdb ni rutas de esta maquina.

## [No publicado] - 2026-09-25

### Anadido - los FPS en el log, y quitada la caida de FPS
Visto en partida: ya no hay caida de FPS. El juego no escribe FPS en ningun sitio, asi que el plugin escribe una linea `[fps]` por minuto (media, peor frame, tirones y barridos mas caros) y cada tiron de 150 ms o mas dice que barrido nuestro corrio en ese frame. Lo que se quito: el minimapa hacia operaciones de texto por cada una de sus 262.144 columnas al repintar el terreno y lo repintaba con cada obra; el contorno de la camara subia 1 MB a la grafica cuatro veces por segundo con la camara quieta; y los barridos coincidian todos en el mismo frame.

