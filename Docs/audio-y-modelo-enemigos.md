# Audio del juego + modelo de los enemigos 2 y 3

Rama: `feature/audio-y-modelo-enemigos` (sale de `develop`).

Dos trabajos en una rama porque comparten el mismo destinatario (la escena `Prototype.unity`) y el
mismo criterio de diseño: **nada de cableado manual en el Inspector**, todo resuelto por convención
de nombres o copiado del objeto que ya está bien armado.

---

## 1. Qué se pidió y qué se hizo

| Pedido | Dónde quedó |
|---|---|
| Sonido a los botones del menú | `Assets/Scripts/UI/SonidosUI.cs` (los 24 botones IMGUI del juego) |
| Música de suspenso / terror | `Assets/Scripts/Systems/MusicaAmbiente.cs` + `Audio/Resources/Music/suspenso_loop.wav` |
| Pasos del jugador (`Player`) | `Assets/Scripts/Player/PasosJugador.cs` + 4 variantes de paso |
| Sonido propio por ítem | `ItemPickup`, `MonedaPickup`, `EfectosDeItem`, `ItemData.sonido` + 8 clips |
| Sonido al prender/apagar la linterna | `Assets/Scripts/Systems/FlashlightController.cs` + 2 clips |
| Modelo y animaciones del enemigo 1 en los enemigos 2 y 3 | `Assets/Scripts/Editor/EnemigoModeloDuplicador.cs` |

---

## 2. De dónde salen los `.wav`

**No hay audio de terceros en el repo.** Los 18 clips se **sintetizan por código** con
`Tools/GeneradorAudio/`:

```bash
powershell -ExecutionPolicy Bypass -File Tools\GeneradorAudio\generar.ps1
```

- `GeneradorAudio.cs` es **.NET puro** (no un script de Unity): se puede correr con el Editor
  abierto o cerrado, y no depende de que el proyecto compile.
- El ruido sale de un `System.Random` con **semilla fija por clip**, así que los `.wav` salen
  idénticos byte a byte en cada corrida: regenerarlos de gusto **no ensucia el `git status`**.
- Son placeholders *de producción*: suenan y encajan con el tono del juego, pero están pensados
  para reemplazarse por grabaciones reales **dejando el mismo nombre de archivo**, sin tocar una
  línea de C#.

### Lo que se generó

| Carpeta (`Assets/Audio/Resources/`) | Archivos | Síntesis |
|---|---|---|
| `UI/` | `boton_hover`, `boton_click`, `boton_atras` | Transitorio de ruido con pasabanda + cuerpo que baja de tono. El de "atrás" baja más, para que salir se oiga distinto de entrar. |
| `Music/` | `suspenso_loop` (48 s) | Subgrave en La (55 Hz) con dos osciladores desafinados, pad con **tritono** (220 / 311.25 Hz) entrando y saliendo en ciclos largos, cama de ruido filtrada, latido cada 3,2 s y 7 pings metálicos lejanos. Sin melodía ni ritmo marcado: tiene que poder sonar 20 minutos sin volverse pegadiza. |
| `Player/` | `paso_1..4` | Ruido con pasabanda (la suela) + golpe grave (el peso) + cola de grava. 4 colores distintos. |
| `Flashlight/` | `linterna_on`, `linterna_off` | Dos clicks mecánicos; encender es más agudo que apagar. |
| `Items/` | `pickup_generico`, `pickup_arma`, `pickup_llave_interior`, `pickup_llave_salida`, `pickup_pocion_vida`, `pickup_racion_comida`, `pickup_moneda`, `pickup_item_prueba` | Un timbre por ítem: chapa golpeada para el arma, manojo tintineando para las llaves, vidrio y arpegio para la poción, golpe sordo de bolsa para la ración, ping brillante para la moneda. |

### Loop de la música

El loop cierra solo: **todas** las frecuencias y los LFO son múltiplos de 1/48 Hz, y el latido cae
15 veces exactas en los 48 s. Lo único no periódico (la cama de ruido) se tapa con un crossfade de
2 s de la cola sobre el arranque.

Detalle que costó encontrar: el pasaaltos que saca el DC tiene que ir **antes** del crossfade. Un
biquad arranca con el estado en cero y deja un transitorio en las primeras muestras; filtrando
después, ese transitorio caía justo sobre la costura (medido: salto de **0,34** entre la última
muestra y la primera, o sea un click en cada vuelta). Filtrando antes, el salto queda en **0,022**,
por debajo del movimiento interno normal de la señal (0,034): la costura es inaudible.

### Ajustes de importación (`.meta`)

Están escritos a mano para que Unity importe cada clip como corresponde en una PC de gama baja:

- **Efectos** (UI, pasos, ítems, linterna): `DecompressOnLoad` + **ADPCM**, 44,1 kHz, mono.
- **Música**: `Streaming` + **Vorbis** calidad 0.5, 22,05 kHz, mono (2 MB, casi nada de RAM).
- **`normalize: 0` en todos**: el balance entre sonidos ya está hecho en el generador (el hover se
  genera mucho más tenue que el click). Si Unity normalizara, lo rompería.
- `3D: 1` en lo que suena en el mundo (ítems, pasos); `3D: 0` en UI y música.

---

## 3. Cómo llega el sonido al juego: `BibliotecaDeSonidos`

Punto único de acceso. Los clips viven en `Assets/Audio/Resources` y se piden **por nombre**
(`Resources.Load`), no por una referencia serializada en el Inspector.

**Por qué por nombre.** Los sonidos los necesitan cosas que se crean en tiempo de ejecución (el
`Menu` se autoinstancia, la música también, los pasos se agregan al jugador desde código) y objetos
que viven en dos escenas distintas. Con referencias serializadas habría que cablear a mano cada
escena y cada prefab, y cualquiera que agregue un ítem o un enemigo se olvidaría de alguna. Con esta
convención, **dejar el `.wav` en la carpeta correcta ES el cableado**. Es el mismo criterio que ya
usaba `AudioPreferences` para cargar `MainMixer` desde `Resources`.

Los scripts que **sí** tienen un campo en el Inspector lo siguen respetando: la biblioteca es el
fallback, no un reemplazo.

Cadena de prioridad al recoger un ítem, de lo más específico a lo más general:

1. `ItemPickup.pickupSound` — lo que diga **esa instancia** en la escena.
2. `ItemData.sonido` — vale para **todas** las copias de ese ítem (campo nuevo).
3. `Items/pickup_<itemId>.wav` — por convención de nombre.
4. `Items/pickup_generico.wav` — siempre hay sonido.

Agregar un ítem nuevo con sonido propio es **dejar un `.wav` más en la carpeta**, sin tocar código.

### Mixer

`AudioPreferences` ganó `MusicGroup` y `RutearAMusica()`, hermanos de los de `sfx` que ya existían.
Todo el audio nuevo se rutea al grupo que le toca, así que los sliders **General / Música /
Efectos** del menú lo afectan de verdad.

De paso: `ItemPickup` y `MonedaPickup` usaban `AudioSource.PlayClipAtPoint`, que **no pasa por el
mixer** — el slider "Efectos" no los afectaba. Ahora pasan por
`BibliotecaDeSonidos.ReproducirEnPunto`, que arma el `AudioSource` ruteado al grupo `sfx`.

---

## 4. Decisiones por sistema

### Botones del menú (`SonidosUI`)

Estas pantallas no usan Canvas: se dibujan por código en `OnGUI` y los "botones" **no existen como
objetos** a los que engancharles un `AudioSource` con un `UnityEvent`. Un wrapper con la misma firma
que `GUILayout.Button` / `GUI.Button` es el único lugar donde meter el sonido una vez y que valga
para los 24 botones del juego:

```csharp
if (SonidosUI.Boton("JUGAR", EstiloUI.BotonPrimario)) Jugar();          // click
if (SonidosUI.BotonAtras(pie, "VOLVER", EstiloUI.BotonPrimario)) ...    // sonido descendente
```

Hay cuatro sobrecargas, una por cada forma en la que el juego dibuja un botón: `GUILayout` con
texto, y `GUI` (rectángulo calculado a mano) con texto o con `GUIContent` — esta última para las
casillas del inventario, que llevan icono.

- Suena **con el juego congelado**: el menú de pausa corre con `Time.timeScale = 0`, así que todo va
  por `Reproducir2D` y no por nada atado al tiempo de juego.
- **Hover**: en IMGUI no hay ids estables, así que se identifica el botón bajo el cursor por su
  rectángulo y se toca el sonido **una vez al entrar**, no una vez por frame. Solo en `EventType.Repaint`
  (en las otras pasadas el rect todavía no está calculado).
- Un botón gris (`GUI.enabled = false`, como "Comprar" sin oro) **no suena**: no responde al click.
- Abrir y cerrar con **Esc** suena igual que apretar el botón. Si no, el menú se oye a medias y
  parece que el sonido falla.

### Pasos (`PasosJugador`)

La cadencia se mide por **distancia recorrida**, no por tiempo: un paso cada N metros. Así el ritmo
acompaña lo que el cuerpo hace de verdad (caminar, correr, frenar en una esquina) sin replicar la
máquina de estados de `PlayerController`. Con un temporizador fijo los pasos siguen sonando mientras
el jugador se empuja contra una pared sin avanzar — el bug clásico de este sistema.

- Corriendo se lee de `PlayerStats.estaCorriendo`, que `PlayerController` escribe cada frame con si
  **este** frame se está corriendo de verdad (tecla + movimiento + estamina): misma fuente de verdad
  que el movimiento, no se pueden desincronizar.
- Nunca repite el mismo clip dos veces seguidas, y el tono varía ±9 %.
- Golpe extra al aterrizar después de un salto.
- 2D y no 3D: son los pies del propio jugador, siempre a la misma distancia del oído. En 3D sonarían
  corridos hacia abajo (la cámara está en un hijo, a la altura de la cabeza) y con paneo raro al girar.
- **Se agrega desde `PlayerController.Start`**, no a mano en la escena: ese es el único objeto que
  sabe que es "el cuerpo del jugador" (el que tiene el `CharacterController`). Si está puesto en el
  Inspector no se toca y valen sus valores. Mismo patrón de autoinstalación que ya usan
  `Menu.AutoCreate`, `EfectosDeItem.EnsureExists` y `PlayerUI`.

### Música (`MusicaAmbiente`)

Se autoinstancia y vive en `DontDestroyOnLoad`, así que **no se reinicia en cada cambio de escena**.
Un `AudioSource` puesto a mano en `Prototype.unity` y otro en `MenuPrincipal.unity` volverían a
empezar el tema desde cero cada vez que el jugador aprieta "Jugar" o "Reiniciar", que es exactamente
lo que rompe el clima.

- Volumen base 0,45: la música es cama, no protagonista. Lo que el jugador tiene que oír claro son
  los pasos del enemigo.
- Fade de entrada de 3 s con `Time.unscaledDeltaTime`, para que avance igual con el menú abierto.
- `AjustarIntensidad(factor)` queda disponible para bajarla en un momento concreto (un silencio
  antes de un susto) sin cortarla.

### Linterna

Dos clicks distintos (encender más agudo que apagar) para que el jugador sepa en qué estado quedó
**sin mirar nada** — justo lo que hace falta cuando la está prendiendo porque no ve.

### Ítems al usarlos

`EfectosDeItem` es el **único** lugar del proyecto que escucha `Inventory.OnItemUsed`, así que el
sonido de uso va ahí. Suena el mismo clip propio del ítem, más bajo y en 2D (el ítem está en la
mano). Va **antes** del mapeo de curación y sin depender de él: usar una llave o el arma no cura
nada pero igual tiene que sonar.

---

## 5. Modelo y animaciones en los enemigos 2 y 3

### Estado de partida

`EnemigosExtraBuilder` ya colocaba los enemigos `Enemigo_02` y `Enemigo_03` copiando los componentes
de lógica del original (`EnemyAI`, `EnemyHealth`, `EnemyAnimator`, `Rigidbody`) con *Paste Component
Values*, pero **lo visual quedaba como una esfera primitiva** de placeholder. En la escena había un
solo hijo `Modelo` (el del enemigo 1).

### Lo que falta y la herramienta nueva

`Assets/Scripts/Editor/EnemigoModeloDuplicador.cs` — menú
**Between Metals > Enemigos > Duplicar modelo del enemigo 1 en los demás**.

**Por qué copiar del enemigo 1 y no instanciar el FBX de cero** (que es lo que ya hacía
`EnemyModelSetup`): el modelo del enemigo 1 **no es el FBX crudo**. Tiene encima el trabajo de ajuste
que vive en la **escena** y no en el asset:

- el giro en Y que corrige para dónde mira el FBX,
- la altura a la que quedan los pies,
- la escala,
- el material asignado sobre los renderers de la instancia,
- el `Animator` con `Creature.controller` (o sea **todas** las animaciones y sus transiciones),
- `Apply Root Motion` apagado.

Instanciando el FBX otra vez todo eso se perdería y habría que reajustarlo enemigo por enemigo.
Copiando el del enemigo 1, los tres se ven y se animan igual **por construcción**, y cualquier
ajuste futuro sobre el enemigo 1 se propaga volviendo a correr el menú.

Es la pata que faltaba del mismo criterio que ya usaba `EnemigosExtraBuilder` con la lógica.

### Cómo lo hace

1. Busca la **plantilla**: el primer enemigo con un hijo `Modelo` **que tenga `Animator`** (se pide
   el Animator y no sólo el nombre para no tomar como plantilla un enemigo a medio hacer).
2. Para cada enemigo sin modelo: saca el `MeshFilter`/`MeshRenderer` de la esfera (el
   `SphereCollider` **no**, lo reemplaza el fixer al final — así el enemigo nunca se queda sin
   collider en el medio, que lo haría invulnerable).
3. Instancia el modelo **manteniendo el vínculo al prefab** (`PrefabUtility.InstantiatePrefab` sobre
   el asset de origen), así una reimportación del FBX sigue llegando a los tres. Si la plantilla no
   fuera una instancia de prefab, cae a `Instantiate` común.
4. Copia `localPosition` / `localRotation` / `localScale`, el `Animator` con *Paste Component Values*
   y los materiales renderer por renderer.
5. Asegura `EnemyAnimator` en el objeto del enemigo.
6. Delega el resto en **`EnemySetupFixer.CorregirEnemigos()`**, que ya existía y ya corría sobre
   todos los `EnemyAI` de la escena: alineación, hitbox remedida, material de respaldo, `AudioSource`
   3D ruteado al grupo `sfx` y el puente de Animation Events. **No se duplicó esa lógica.**

`BuildEnemySetup.Run()` ahora lo llama **antes** de `EnemyModelSetup`: los dos saben darle un modelo
a un enemigo que no tiene, pero el duplicador lo copia del enemigo 1 ya ajustado y `EnemyModelSetup`
instancia el FBX crudo. Al revés, `EnemyModelSetup` les pondría el FBX a los enemigos 2 y 3 primero y
el duplicador no encontraría nada que hacer. Se le pasa `corregirDespues: false` para no correr el
fixer dos veces en la misma secuencia.

### ⚠️ Paso manual pendiente

El modelo de los enemigos 2 y 3 **es un cambio en la escena**, y la escena no se puede editar sin el
Editor (`Prototype.unity` es YAML de 17 000 líneas con mallas rigueadas; editarlo a mano es
exactamente lo que `CLAUDE.md` dice no hacer). El Editor estaba abierto durante este trabajo, así que
no se pudo correr Unity en batch.

**Hay que correrlo una vez en el Editor y guardar la escena:**

1. Abrir `Assets/Scenes/Prototype.unity`.
2. Menú **Between Metals > Enemigos > Duplicar modelo del enemigo 1 en los demás**
   (o **Build + Setup completo**, que ahora lo incluye).
3. Revisar que `Enemigo_02` y `Enemigo_03` tengan su hijo `Modelo` y se animen.
4. **Ctrl+S** y commitear `Prototype.unity`.

Todo lo demás (audio incluido) funciona sin ningún paso manual: entra solo al dar Play.

---

## 6. Verificación hecha

- **Compilación**: `Assembly-CSharp` y `Assembly-CSharp-Editor` compilados con el Roslyn que trae
  Unity (`DotNetSdk/sdk/8.0.318/Roslyn`), con las mismas referencias y `DefineConstants` que usan los
  `.csproj` del proyecto. **0 errores.** Los únicos warnings son preexistentes
  (`EnemyModelSetup.cs:26` API obsoleta, `MapaBuilder.cs:148` código inaccesible); ninguno sale de
  los archivos nuevos.
- **Niveles de los 18 `.wav`**: pico máximo 0,70 (sin clipping), DC ≈ 0 en todos, y arranque/final
  cerca de cero en los efectos (no hay chasquido al disparar un clip).
- **Costura del loop de música**: salto de 0,022 contra un movimiento interno máximo de 0,034.

### Self-test

`Assets/Scripts/Editor/Tests/AudioJuegoSelfTest.cs` — menú
**Between Metals > Tests > Audio del juego Self-Test** (o `-executeMethod AudioJuegoSelfTest.RunAllAndExit`
en batch). Mismo formato que los demás autotests del proyecto: un caso `CP-AUDJ-XX`, una línea
PASS/FAIL por caso y una línea RESULT.

Se prueba lo que **sí** se puede sin dar Play, que resulta ser justo la parte frágil: que los clips
estén donde el código los busca. Un `.wav` mal ubicado o un `itemId` que no coincide con el nombre
de archivo **no rompe nada visible** — el juego sigue andando, solo que en silencio — y es el tipo
de fallo que se descubre tarde.

| Caso | Qué cubre |
|---|---|
| 01 | Los 8 clips declarados como constantes existen (caza un renombre a medias) |
| 02–03 | Una ruta inexistente, `null` o `""` devuelven `null` sin excepción |
| 04–05 | Hay pasos, ninguno es `null` y la numeración es contigua (un `paso_4` tras un hueco quedaría sin usar en silencio) |
| 06–07 | `SonidoDeItem` resuelve el clip propio, ignorando mayúsculas y espacios |
| 08–09 | Cae al genérico con un `itemId` desconocido, vacío o `null`: **siempre** hay sonido |
| 10 | **Cada `ItemData` de `Assets/Items` tiene sonido propio**, no el genérico. Avisa cuando se agrega un ítem y nadie le dejó su `.wav` |
| 11 | El mixer tiene los grupos `sfx` y `music` (sin ellos el audio sale al Master y los sliders no lo afectan: fallo silencioso) |
| 12 | Los `.meta` escritos a mano quedaron aplicados: música `Streaming`, efectos `DecompressOnLoad` |
| 13 | El loop de música sigue midiendo 48 s (el largo no es cosmético: de él depende que el loop cierre) |
| 14 | Ningún `.wav` se importa con `normalize`, que rompería el balance hecho en el generador |

### Lo que NO se verificó

- No se dio **Play**: el Editor estaba abierto y ocupado, así que no se escuchó el audio *en el
  juego* ni se midió el balance real de volúmenes contra los sonidos del enemigo que ya existían.
  Lo más probable que haya que retocar a oído es el volumen de los pasos y el de la música
  (constantes en `PasosJugador` y `MusicaAmbiente`).
- La **cadencia** de `PasosJugador` y el **fade** de `MusicaAmbiente` viven en `Update` y necesitan
  Play, así que quedan fuera del self-test.

---

## 7. Archivos

### Nuevos

```
Tools/GeneradorAudio/GeneradorAudio.cs          síntesis de los 18 .wav (.NET puro)
Tools/GeneradorAudio/generar.ps1                wrapper
Assets/Audio/Resources/{UI,Music,Player,Items,Flashlight}/   18 .wav + .meta
Assets/Scripts/Systems/BibliotecaDeSonidos.cs   acceso a los clips por nombre
Assets/Scripts/Systems/MusicaAmbiente.cs        loop de suspenso persistente
Assets/Scripts/Player/PasosJugador.cs           pasos por distancia recorrida
Assets/Scripts/UI/SonidosUI.cs                  botones IMGUI con sonido
Assets/Scripts/Editor/EnemigoModeloDuplicador.cs  modelo del enemigo 1 -> 2 y 3
Assets/Scripts/Editor/Tests/AudioJuegoSelfTest.cs 14 casos CP-AUDJ-XX
Docs/audio-y-modelo-enemigos.md                 este documento
```

### Modificados

```
Assets/Scripts/Systems/AudioPreferences.cs      + MusicGroup / RutearAMusica
Assets/Scripts/Systems/FlashlightController.cs  + click de encendido/apagado
Assets/Scripts/Player/PlayerController.cs       + instala PasosJugador
Assets/Scripts/Inventory/ItemData.cs            + campo sonido
Assets/Scripts/Inventory/ItemPickup.cs          cadena de fallbacks + mixer
Assets/Scripts/Inventory/EfectosDeItem.cs       + sonido al usar un ítem
Assets/Scripts/Systems/MonedaPickup.cs          fallback + mixer
Assets/Scripts/UI/Menu.cs                       botones con sonido + Esc
Assets/Scripts/UI/PantallaFinal.cs              botones con sonido (Game Over / Victoria)
Assets/Scripts/UI/ShopManager.cs                botones con sonido
Assets/Scripts/Inventory/InventoryUI.cs         casillas con sonido
Assets/Scripts/Editor/BuildEnemySetup.cs        + paso del duplicador
```

---

## 8. Merge de `develop` (PR #71)

Mientras este trabajo estaba en curso se mergeó a `develop` el **PR #71** (interfaces de la Etapa 11
con la identidad visual de la Etapa 12), que reescribió por completo las cinco pantallas IMGUI que
este trabajo toca y agregó un sistema de estilos (`EstiloUI`, `NavegacionUI`, `IconosUI`,
`PantallaFinal`, `AvisosUI`).

`origin/develop` ya está mergeado en esta rama. La resolución fue **tomar la UI nueva tal cual** en
las cinco pantallas en conflicto y reaplicar el sonido encima, porque el conflicto era de
posición y no de intención: la UI nueva sigue dibujando sus botones con `GUILayout.Button` /
`GUI.Button`, así que el wrapper sigue siendo el lugar correcto. Nada del diseño de la Etapa 12 se
revirtió.

Dos consecuencias:

- Los botones pasaron de 29 a **24** (varias pantallas se unificaron: `GameOverUI` y `VictoryUI`
  ahora delegan en `PantallaFinal`, que es donde quedó el sonido de esas dos).
- `SonidosUI` ganó las sobrecargas de `GUI.Button` **con texto**, que es la forma que usa la UI
  nueva (antes solo cubría `GUILayout` y `GUI.Button` con `GUIContent`).

`FlashlightController` e `ItemPickup`, que las dos ramas tocaron, se auto-mergearon sin conflicto.

## 9. Subir a `develop`

**PR abierto, sin mergear — a la espera de la orden.** El merge se puede hacer desde el PR, o a mano:

```bash
git checkout develop && git merge --no-ff feature/audio-y-modelo-enemigos && git push origin develop
```

Antes de eso conviene hacer el paso manual de la sección 5 y commitear `Prototype.unity` en esta
misma rama, para que `develop` reciba el trabajo completo.
