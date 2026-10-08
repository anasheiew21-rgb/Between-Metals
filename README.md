# Between Metals

Between Metals es un videojuego indie de terror psicológico y exploración en primera
persona. El jugador queda atrapado en un laberinto que cambia a sus espaldas y, sin un
sistema de objetivos que lo guíe, su única herramienta es el instinto: leer las pistas
del entorno, administrar la linterna y la estamina, y decidir cuándo enfrentar a lo que
lo persigue y cuándo huir.

Está desarrollado en Unity y optimizado para PCs de bajos recursos.

**Estado actual: prototipo jugable.** El proyecto está en desarrollo activo; las
funcionalidades descritas en este README son las que están efectivamente versionadas en
el repositorio. La escena jugable es `Assets/Scenes/Prototype.unity`. El código y los
comentarios están en español.

## Integrantes y roles

| Integrante | Rol | Cuenta de GitHub | Identidad en el historial de Git |
| --- | --- | --- | --- |
| Ian Gualco | Project Manager | `anasheiew21-rgb` | `kdg`, `anasheiew21-rgb` |
| Viskel Rodriguez | Programador | `xXviskelXx` | `xXviskelXx` |
| Valentin Cerdan | Analista de Marketing / Diseñador / Analista de Negocio | `Enanouwu` | `Enanonashei` |
| Christopher Ibana | Analista Funcional | `seppe67` | `seppe67` |

Algunos integrantes commitearon desde más de una configuración local de Git, por lo que el
nombre que aparece en el historial no siempre coincide con el de su cuenta de GitHub. La
última columna reconcilia ambas.

## Tecnologías

- **Unity 6** — versión exacta del editor: `6000.5.9f1`
- **C#** — scripting del juego (todo compila en `Assembly-CSharp` y `Assembly-CSharp-Editor`)
- **Universal Render Pipeline (URP)** — perfiles `PC_RPAsset` y `Mobile_RPAsset` en `Assets/Settings/`
- **Unity AI Navigation** — NavMesh para la IA de los enemigos
- **Git** — control de versiones
- **GitHub** — repositorio remoto
- **GitHub Issues** — requisitos, historias de usuario, tareas y bugs
- **GitHub Pull Requests** — integración de cada rama de trabajo

## Ejecución

### Requisitos

- Unity **6000.5.9f1** (se recomienda instalarlo desde Unity Hub; otras versiones pueden
  reimportar assets y modificar archivos del proyecto).
- Git para clonar el repositorio.
- Windows. El proyecto se desarrolla y prueba sobre Windows.

### Pasos

1. Clonar el repositorio:

   ```
   git clone https://github.com/anasheiew21-rgb/Between-Metals.git
   ```

2. Abrir la carpeta del proyecto desde Unity Hub con la versión `6000.5.9f1`. La primera
   apertura importa los assets y puede tardar varios minutos.
3. Abrir la escena de inicio **`Assets/Scenes/MenuPrincipal.unity`** (es la primera de
   Build Settings) o, para entrar directamente al laberinto, **`Assets/Scenes/Prototype.unity`**.
4. Entrar en Play Mode.

No hace falta configuración manual adicional: los sistemas de interfaz y de gestión de
partida (HUD, inventario, menú, gestor de barreras, NavMesh en runtime) se auto-instalan
al cargar la escena mediante `RuntimeInitializeOnLoadMethod`, así que no dependen de
objetos colocados a mano en la jerarquía. El audio sigue el mismo criterio: la música y
los sonidos de pasos, ítems, linterna y botones se enganchan solos, y los clips se
resuelven por nombre desde `Assets/Audio/Resources` (ver
[`Docs/audio-y-modelo-enemigos.md`](Docs/audio-y-modelo-enemigos.md)).

### Controles

| Acción | Tecla | Reasignable |
| --- | --- | --- |
| Moverse | `W` `A` `S` `D` | Sí |
| Correr | `Shift` izquierdo | Sí |
| Saltar | `Espacio` | Sí |
| Atacar | Clic izquierdo | Sí |
| Linterna | `F` | Sí |
| Interactuar | `E` | No |
| Inventario | `Tab` | No |
| Usar ítem seleccionado | `R` | No |
| Seleccionar ranura | `1` … `0` | No |
| Menú / cerrar tienda | `Esc` | No |

Las acciones marcadas como reasignables se pueden remapear desde el menú del juego
(`KeyBindings`, en `Assets/Scripts/UI/Menu.cs`). Los controles usan la API legacy
`UnityEngine.Input`.

### Self-tests

El proyecto incluye self-tests que se ejecutan en el Editor, sin entrar en Play Mode,
desde el menú **`Between Metals/Tests/`** (por ejemplo `Senales Ambientales`, `Monedas`,
`Inventario`, `GameManager`, `Barreras Dinamicas (logica)`, `Audio del juego`). Cada uno
imprime en la consola un resultado por caso y un `RESULT: N passed, M failed`.

También se pueden correr sin abrir el Editor, en modo headless:

```
Unity.exe -batchmode -nographics -quit -projectPath <ruta> -executeMethod SenalAmbientalSelfTest.RunAllAndExit
```

El menú `Between Metals/` incluye además utilidades de autor (generación de mapa,
colocación de barreras, de enemigos y del modelo de linterna). Son herramientas de
Editor: modifican la escena y no forman parte del juego en ejecución.

## Estructura de carpetas

```
Assets/
├── Scenes/              MenuPrincipal.unity — menú de inicio (escena 0 del build)
│                        Prototype.unity — escena jugable del laberinto
├── Scripts/
│   ├── Player/          PlayerController, MouseLook, PlayerInteraction,
│   │                    PlayerStats, PlayerCombat, PasosJugador,
│   │                    BarraRapida, EquipoJugador
│   ├── Systems/         Enemigos (EnemyAI, EnemyHealth, EnemyAnimator…),
│   │                    GameManager, FlashlightController, SenalAmbiental,
│   │                    ActivadorSenalAmbiental, MonedaPickup, NPCMerchant,
│   │                    ComercianteGestos, ItemComercio, PuertaInteractuable,
│   │                    MuroSecreto, BotonSecreto, Baliza, Antorcha,
│   │                    BotinEnemigo, AudioPreferences, BibliotecaDeSonidos,
│   │                    MusicaAmbiente, GraphicsPreferences, ExitTrigger
│   ├── Maze/            Barreras dinámicas y modelo del laberinto
│   │                    (GrafoLaberinto, GestorBarreras, MapaSectores…)
│   ├── Inventory/       Inventory, ItemData, ItemPickup, EfectosDeItem, InventoryUI
│   ├── UI/              Menu + KeyBindings, PlayerUI (HUD), ShopManager,
│   │                    GameOverUI, VictoryUI, PantallaFinal, PromptInteraccion,
│   │                    AvisosUI, NavegacionUI, EstiloUI, IconosUI, FondoMenu,
│   │                    SonidosUI
│   ├── Navigation/      NavMeshRuntimeBuilder
│   └── Editor/          Herramientas de autor y, en Editor/Tests/, los self-tests
├── Prefabs/
│   ├── Environment/     SenalAmbiental.prefab, Cave/CaveRock_01.prefab
│   ├── Items/           Moneda.prefab, ItemPickup_Base.prefab, Arma.prefab,
│   │                    Llave.prefab
│   ├── Player/          (reservada, todavía sin prefabs)
│   └── Enemies/         (reservada, todavía sin prefabs)
├── Items/               ScriptableObjects de ítems (ItemData): PocionDeVida,
│                        RacionDeComida, Arma, Llave_Interior, Llave_Salida,
│                        Item_Prueba
├── Audio/               Audio/Enemy/ (.ogg del enemigo) y Audio/Resources/, que
│                        BibliotecaDeSonidos carga por nombre: MainMixer.mixer y los
│                        .wav de UI, Music, Player, Items, Flashlight y Maze
├── Models/              Modelos propios: entorno (CaveRock_01), brazo y linterna
│                        del jugador, y el enemigo araña (SpiderLowPoly)
├── TripoModels/         Modelos 3D del enemigo generados con Tripo
├── Modelo personajes/   Modelo riggeado del alien y su textura
├── Materials/           Materiales del proyecto
├── Animacion/           Clips y controladores de animación del enemigo
├── Animacion_Comerciante/  Clips de gestos del comerciante
├── TextMesh Pro/        Recursos de TextMeshPro que acompañan a la UI
├── Settings/            Perfiles de URP (PC y Mobile)
├── EnvironmentPack/     Paquete de terceros (corredores sci-fi)
├── hedge_maze_pack/     Paquete de terceros (laberinto)
├── Tests/               Escena y ScriptableObjects auxiliares de prueba
│                        (Tests/Inventario/InventoryTest.unity)
└── _Recovery/           Copias de escena recuperadas por el Editor (no se usan)

Docs/                    Documentación técnica: Inventario.md,
                         audio-y-modelo-enemigos.md, arreglo-ataque-enemigos.md,
                         arreglo-enemigos-flotando.md
Tools/                   Herramientas fuera de Unity (Tools/GeneradorAudio:
                         sintetiza los .wav del juego, .NET puro)
Packages/                Manifiesto de paquetes de Unity
ProjectSettings/         Configuración del proyecto
```

`Library/`, `Logs/`, `UserSettings/`, `Temp/`, los `.csproj` y los `.sln` son generados
por Unity y están ignorados en `.gitignore`.

Los archivos `.meta` de Unity se versionan junto con su asset: se mueven y se renombran
con él, porque son los que guardan los GUID con los que las escenas y los prefabs
referencian a cada recurso.

## Ramas

| Rama | Propósito |
| --- | --- |
| `main` | Versión estable. Recibe el trabajo integrado desde `develop`. |
| `develop` | Rama de desarrollo e integración. Es la base de todo el trabajo nuevo. |
| `feature/*` | Una funcionalidad nueva por rama (por ejemplo `feature/senales-ambientales`, `feature/inventario`, `feature/multiple-enemies`). |
| `fix/*` | Corrección de un bug concreto, normalmente asociado a una issue (por ejemplo `fix/reinicio-ui`). |
| `docs/*` | Cambios de documentación (por ejemplo `docs/update-readme-etapa15`). |

### Flujo de trabajo

1. Cada tarea técnica o funcional se registra como **issue** en GitHub.
2. Se crea una rama `feature/*`, `fix/*` o `docs/*` a partir de `develop`.
3. Se trabaja en esa rama y se publica en el remoto.
4. Se abre un **Pull Request** contra `develop`, se verifica que esté *mergeable* y se
   integra con **merge commit** (sin squash ni rebase) para conservar el historial y la
   autoría de cada commit.
5. Las ramas de trabajo no se borran después del merge: quedan como evidencia de
   trazabilidad.
6. `develop` se integra a `main` mediante un Pull Request de release.

### Convenciones de commits

Se usan mensajes de tipo *Conventional Commits* en las ramas de trabajo:

```
feat(ambiental): add environmental signals and event activators
fix: oscuridad sin reflejo celeste y cielo cubierto por la niebla (#60)
test(ambiental): add self-test for environmental signals
docs: complete project README
```

Las issues se referencian desde el mensaje del commit o del Pull Request para mantener la
trazabilidad **requisito → historia de usuario → issue → rama → commit → Pull Request**.
