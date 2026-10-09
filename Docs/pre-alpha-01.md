# Pre-alpha-01

Primer build que compila sin errores y bootea el juego de punta a punta: pantalla de
menú (`MenuPrincipal.unity`) y escena jugable (`Prototype.unity`) corriendo en Player
build, no solo en el Editor. A partir de este punto se usa como nombre de referencia
para el estado del proyecto integrado en `main`.

## Qué incluye

Todo lo mergeado en `develop` hasta este punto, entre otras cosas:

- Jugador: movimiento, linterna, estamina, inventario, combate cuerpo a cuerpo.
- Enemigos: IA con NavMesh, hitboxes y animaciones sincronizadas.
- Laberinto: barreras dinámicas, señales ambientales, generación de sectores.
- UI: menú principal, HUD, tienda, pantallas de game over / victoria.
- Audio: música, pasos, ítems, linterna y sonidos de UI (sintetizados por código,
  sin assets de audio de terceros).

## Cómo se verificó

1. Abrir el proyecto en Unity `6000.5.9f1`.
2. `File > Build Settings > Build` (plataforma PC) y correr el ejecutable generado.
3. Confirmar que arranca en `MenuPrincipal.unity` y que desde ahí se puede entrar a
   `Prototype.unity` y jugar sin errores en la consola.

## Versionado

`ProjectSettings/ProjectSettings.asset` mantiene `bundleVersion: 0.1.0`. `pre-alpha-01`
es un nombre de hito de documentación/release, no reemplaza ese número de versión.
