# Arreglo de los enemigos flotando sobre el suelo

Síntoma: **en partida los enemigos quedan flotando unos centímetros sobre el suelo**, en vez de estar
apoyados y empezar a moverse desde esa posición.

Continuación de [`arreglo-ataque-enemigos.md`](arreglo-ataque-enemigos.md) y, resulta, **el mismo
patrón de fallo**: `EnemyAI` sincroniza con el cuerpo del enemigo algunas propiedades del
`NavMeshAgent` y se olvida de otras, que se quedan con lo que haya quedado serializado.

---

## 1. La causa

`NavMeshAgent` ubica el objeto a **superficie del NavMesh + `baseOffset`**. O sea que `baseOffset`
es *la* propiedad que decide a qué altura queda el enemigo.

**Nadie la tocaba.** `SincronizarFormaDelAgente()` copiaba del collider el `radius` y el `height`
del agente, pero no la altura. Así que quedaba el valor que Unity deja al agregar el componente.
Verificado en `Prototype.unity`:

```
NavMeshAgent de Enemigo_02 y Enemigo_03:
  m_Radius: 0.5
  m_Height: 1
  m_BaseOffset: 0.5      <-- nadie lo escribió nunca
```

Y `grep -rn "baseOffset" Assets/Scripts/` no devolvía **ni una línea**. Es exactamente el mismo
olvido que tenía `stoppingDistance` en el arreglo anterior.

### Por qué un número fijo no servía

Acá está lo que hacía falta mirar antes de "poner un 0 y listo": **el valor correcto no es el mismo
para los dos tipos de enemigo que hay en la escena.**

| | pivote | `baseOffset` correcto |
|---|---|---|
| Enemigos 2 y 3 (esfera placeholder de `EnemigosExtraBuilder`) | el **centro** de la esfera | **0,5** (su radio) — si no, quedan medio enterrados |
| Enemigo 1 (modelo rigueado) | a la altura de los pies | **~0,13** — su hijo `Modelo` quedó en `y = -0,134` |

Con el 0,5 que había:

- los enemigos 2 y 3 estaban **bien** (es su radio);
- el enemigo 1 flotaba `0,5 − 0,134` ≈ **37 cm**.

Un `0` fijo habría enterrado las esferas. Un `0,5` fijo deja flotando al que tiene modelo. Por eso
**se mide** en vez de ponerse a mano.

## 2. El arreglo

`EnemyAI.SincronizarAlturaDelAgente()`: mide cuánto hay desde el pivote del enemigo hasta su punto
visible más bajo (el mínimo de `Renderer.bounds.min.y` de todo lo que cuelga de él) y pone
`baseOffset` en exactamente eso. El punto más bajo del enemigo queda sobre el NavMesh, sea una esfera
o un modelo, con el pivote donde esté.

Decisiones de implementación:

- **Se mide en mundo y relativo a `transform.position`**, no convirtiendo a espacio local: el
  enemigo de la escena está escalado 1,2303 en Y, y una cuenta en local habría que volver a
  multiplicarla por esa escala. Lo cubre el caso CP-ENE-27.
- **Se llama desde `AsegurarAgente` y no desde `SincronizarFormaDelAgente`**, que hace un *early
  return* sin `CapsuleCollider`. Es la misma trampa que ya me había comido con `stoppingDistance`:
  ahí dentro habría arreglado el enemigo 1 y dejado a los enemigos 2 y 3 sin tocar.
- **Antes de `AsegurarSobreNavMesh()`**, que es quien hace el `Warp` inicial. Así el primer
  posicionamiento ya lo deja apoyado y **empieza a caminar desde ahí**, sin un salto en el primer
  frame.
- **Sin `Renderer` no se toca el offset.** Un `0` a lo bruto enterraría a cualquier enemigo cuyo
  pivote no esté en los pies. Caso CP-ENE-26.
- **`ajusteAlturaSobreElPiso`** (nuevo, en el Inspector, por defecto 0): válvula de escape en
  centímetros. Los `bounds` de un `SkinnedMeshRenderer` pueden ser algo más grandes que la malla
  real, y en ese caso el enemigo queda levantado de más. Se corrige a ojo sin tocar código.

## 3. Verificación

**Compilación**: `Assembly-CSharp` (65 archivos) y `Assembly-CSharp-Editor` (43), con el Roslyn de
Unity y las mismas referencias y `DefineConstants` que los `.csproj`. **0 errores.**

**Self-test nuevo**: `Assets/Scripts/Editor/Tests/EnemyAlturaSobreElPisoSelfTest.cs`, menú
**Between Metals > Tests > Enemigo: altura sobre el piso**. 6 casos `CP-ENE-22..27`:

| Caso | Qué cubre |
|---|---|
| CP-ENE-22 | La esfera placeholder se apoya: `baseOffset` = su radio (o sea, **no es una regresión** para los enemigos 2 y 3) |
| CP-ENE-23 | Una esfera más grande se apoya igual — caza un `0.5` hardcodeado |
| CP-ENE-24 | Con el modelo en un hijo corrido 13 cm hacia abajo (el caso real del enemigo 1), el offset lo compensa |
| CP-ENE-25 | `ajusteAlturaSobreElPiso` corre al enemigo esa cantidad |
| CP-ENE-26 | Sin `Renderer`, el offset no se pisa con un valor inventado |
| CP-ENE-27 | La escala del objeto no descuadra la cuenta |

### Lo que NO se verificó

- **No se dio Play** (Unity seguía abierto). El arreglo está verificado por aritmética sobre las
  medidas reales de la escena y por los self-tests, que **tampoco se ejecutaron**: compilan, pero
  hay que correrlos desde el menú.
- La medición usa **`Renderer.bounds`**, que para un `SkinnedMeshRenderer` es la caja autorizada del
  modelo y puede ser unos centímetros más grande que la malla. Es una elección deliberada: medir la
  malla exacta de la pose (`BakeMesh`) sería más preciso en un frame y **peor** en el resto, porque
  el punto más bajo de una criatura caminando cambia a cada paso y el enemigo subiría y bajaría. Si
  al probarlo queda levantado un par de centímetros, eso es lo que corrige
  `ajusteAlturaSobreElPiso`.

## 4. Lo que este arreglo NO toca (sigue pendiente, necesita el Editor)

Dos cosas de **datos de escena** que siguen mal y que `EnemySetupFixer` ya sabe arreglar — lo que
falta es correrlo:

1. El hijo `Modelo` del enemigo 1 está descentrado en horizontal: `localPosition: {x: -0.193,
   y: -0.134, z: 0.12}`. `AlinearModelo()` pone X/Z en 0 y los pies en el origen. Con eso el
   `baseOffset` medido pasaría a ser ~0 y el enemigo quedaría apoyado por construcción, sin depender
   de la compensación.
2. La cápsula del enemigo 1 está descentrada (`center.x = -0,063`) y, como su radio (0,775) pasa de
   la mitad del alto (0,519), **Unity la trata como una esfera** cuya base queda 26 cm **por debajo
   de los pies**: la hitbox sigue metida en el piso aunque el modelo ya se vea apoyado.

Las dos se arreglan con el paso manual que ya venía pendiente:

**Between Metals > Enemigos > Build + Setup completo** → revisar → **Ctrl+S** → commitear
`Prototype.unity`.
