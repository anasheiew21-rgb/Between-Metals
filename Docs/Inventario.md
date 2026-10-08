# Sistema de inventario, recolección de ítems e interfaz

Rama: `feature/inventario`. Cubre RF06 (sistema de inventario) y RF07 (recolección de objetos), a través de HU-05 (interfaz de inventario) y HU-06 (recolección de ítems). Issues: #16 (inventario e interfaz), #19 (pruebas), #21 (escena de prueba aislada) y #20 (parcial: solo el prefab base de recolección, `Assets/Prefabs/Items/ItemPickup_Base.prefab`; la colocación de ítems reales en `Prototype.unity` queda pendiente).

## 1. Qué incluye

- `Assets/Scripts/Inventory/Inventory.cs` — el inventario del jugador: capacidad fija, agregar/usar/quitar ítems.
- `Assets/Scripts/Inventory/ItemData.cs` — definición de un ítem como `ScriptableObject` (id, nombre, ícono, si se consume al usarse, descripción).
- `Assets/Scripts/Inventory/ItemPickup.cs` — objeto recogible en el mundo: detecta al jugador por distancia y línea de visión, y agrega el ítem al `Inventory` que encuentra en la escena.
- `Assets/Scripts/Inventory/InventoryPanelState.cs` — estado del panel (abierto/cerrado, selección, bloqueo por `Menu`/`ShopManager`/`GameOverUI`).
- `Assets/Scripts/Inventory/InventoryUI.cs` — dibuja el panel (`OnGUI`) y se recrea sola después de recargar la escena.
- `Assets/Prefabs/Items/ItemPickup_Base.prefab` — prefab base para crear ítems recogibles en una escena.
- `Assets/Tests/Inventario/InventoryTest.unity` — escena de prueba aislada, generada por `Assets/Scripts/Editor/Tests/InventoryTestSceneBuilder.cs`, que **no toca `Prototype.unity` ni ningún script existente**.
- Autotests de Editor en `Assets/Scripts/Editor/Tests/`: `InventorySelfTest.cs`, `ItemPickupSelfTest.cs`, `InventoryUISelfTest.cs`.

## 2. Cómo agregar un ítem al juego

1. Crear un `ItemData` desde el menú de creación de assets: clic derecho en el Project → **Create → Between Metals → Inventario → Item**. Completar `itemId`, `itemName`, `icon` (opcional), `consumeOnUse` y `description`.
2. Arrastrar `Assets/Prefabs/Items/ItemPickup_Base.prefab` a la escena, en la posición donde debe aparecer el ítem.
3. En el `ItemPickup` de la instancia, asignar el `ItemData` creado en el paso 1.
4. Requisitos para que funcione:
   - el prefab necesita un `Collider` (ya lo trae `ItemPickup_Base`, no quitarlo);
   - el jugador tiene que estar dentro del alcance de recolección (3 m) y con línea de visión libre hacia el ítem;
   - tiene que existir un componente `Inventory` en la escena **desde el arranque** (no se crea en runtime): `ItemPickup` e `InventoryUI` lo ubican con `FindAnyObjectByType<Inventory>()`, no por referencia directa, así que alcanza con que haya exactamente uno activo en la escena.

## 3. Integración pendiente en Prototype.unity

Falta:
- Agregar un componente `Inventory` al jugador de `Prototype.unity`. Se recomienda agregarlo en la **raíz "Player"** (el mismo objeto que tiene el tag `Player` y `PlayerInteraction`), no en el hijo "PlayerController" que tiene `PlayerStats`: es donde lo pone `InventoryTestSceneBuilder.BuildPlayer()`, y como la búsqueda es por `FindAnyObjectByType`, no importa la jerarquía exacta, pero mantener el mismo criterio que la escena de prueba evita inconsistencias entre ambas escenas.
- Colocar los ítems reales del juego (instancias de `ItemPickup_Base` con su `ItemData`) en los lugares del laberinto que decida el responsable del mapa (#20). Esto hay que coordinarlo con quien mantiene `Prototype.unity` y `MapaBuilder.cs`, ya que esta rama no los toca.

## 4. Teclas

| Tecla | Acción |
|---|---|
| E | Recoger el ítem apuntado (vía `PlayerInteraction`) |
| Tab | Abrir/cerrar el panel de inventario |
| 1–0 | Seleccionar un ítem por índice |
| Rueda del mouse | Cambiar la selección |
| R | Usar el ítem seleccionado (se consume solo si `consumeOnUse` es `true`) |

Tab, 1–0, la rueda y R están fijas por ahora. Van a ser configurables cuando se implemente el remapeo de teclas de HU-02 (#6).

## 5. Pruebas automáticas

**Desde el menú del Editor** (Unity abierto, sin batch):
- **Between Metals/Tests/Crear escena de prueba de inventario** — regenera `InventoryTest.unity` desde cero (prefab, ítems `TEST_*` y la escena) y corre la validación al final.
- **Between Metals/Tests/Validar escena de prueba de inventario** — abre la escena de prueba **ya existente**, sin reconstruirla ni guardarla, y corre la misma validación.
- Los tres self-test también tienen su propio ítem de menú dentro de **Between Metals/Tests/**.

**En modo batch** (`Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod <Clase>.<Método> -logFile <log>`):

| Método | Casos | Qué valida |
|---|---|---|
| `InventoryTestSceneBuilder.BuildAndExit` | — | Regenera y valida la escena de prueba |
| `InventoryTestSceneBuilder.ValidateAndExit` | — | Abre y valida la escena de prueba sin reconstruirla |
| `InventorySelfTest.RunAllAndExit` | 14 | Lógica de `Inventory` (agregar, usar, capacidad, límites) |
| `ItemPickupSelfTest.RunAllAndExit` | 8 | Detección y recolección de `ItemPickup` |
| `InventoryUISelfTest.RunAllAndExit` | 18 | Panel de inventario: apertura, selección, uso, bloqueo por `Menu`/`ShopManager`/`GameOverUI`, recreación tras recarga de escena |

Cada corrida sale con código 0 si todo pasa, o 1 si falla algo, y deja el detalle en el log indicado.

## 6. Prueba manual

Fecha: 2026-09-26. Responsable: xXviskelXx. Resultado: **13/13 OK**.

| # | Caso | Resultado |
|---|---|---|
| 1 | Moverse y correr | OK |
| 2 | Cartel "Presiona E para recoger [TEST] Llave" | OK |
| 3 | E recoge la Llave, aparece el aviso con la pista de Tab | OK |
| 4 | La Venda y la Pila se recogen, aviso sin la pista | OK |
| 5 | "Inventario lleno" con la Moneda, no se recoge | OK |
| 6 | Tab abre el panel con 3/3 | OK |
| 7 | Las teclas 1, 2 y la rueda seleccionan | OK |
| 8 | R sobre la Llave: no se consume | OK |
| 9 | R sobre la Venda: se consume, contador pasa a 2/3 | OK |
| 10 | La Moneda se puede recoger después de liberar espacio | OK |
| 11 | Esc cierra el panel y bloquea Tab | OK |
| 12 | Sin superposiciones, en 1920x1080 y en 1024x600 | OK |
| 13 | Reiniciar y después Tab: el panel del inventario funciona | OK (ver observación) |

**Observación del caso 13:** en esta rama, sin la corrección de #55 (`fix/reinicio-ui`, todavía no mergeada a `develop`), las barras de estado y el cartel "Presiona E" no reaparecen después de reiniciar. Es un comportamiento esperado y conocido, no un bug de este sistema: el panel de inventario sí se recrea correctamente (esa es la corrección que aporta esta rama), pero la recreación del resto de la interfaz del equipo depende del trabajo de #55.

## 7. Limitaciones conocidas

- El cartel de interacción no se actualiza si el inventario cambia mientras se sigue mirando el mismo ítem.
- El texto "Presiona E" está fijo (no traducible/configurable todavía).
- El componente `Inventory` tiene que existir desde el inicio de la escena; no se crea en runtime si falta.
- Los íconos que vienen de un atlas de sprites se muestran completos (no se recorta el sub-sprite).
- La recreación completa de la interfaz del equipo (barras, cartel) después de reiniciar la escena depende de que se mergee #55 (`fix/reinicio-ui`) a `develop`.
