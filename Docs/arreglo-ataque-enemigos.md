# Arreglo del ataque de los enemigos

Síntomas reportados: **los 3 enemigos no pegan, no hacen daño, solo siguen al jugador y lo hacen
atravesar el piso o las paredes por presión.**

Resultado del diagnóstico: los tres síntomas tienen **una causa común** más tres defectos que se
sumaban encima. Nada de esto era aleatorio.

---

## 1. La causa de fondo: el ataque se medía entre pivotes, ignorando que los cuerpos ocupan lugar

`attackRange` (1,5 m en la escena) y `agent.stoppingDistance` (`attackRange - 0.3` = **1,2 m**) se
median de **pivote a pivote**, sin tener en cuenta el radio de ninguno de los dos cuerpos.

Las medidas reales de `Prototype.unity`:

| | radio |
|---|---|
| Hitbox del enemigo (`CapsuleCollider`) | **0,775 m** |
| Jugador (`CharacterController` 0,5 + `skinWidth` 0,08) | **0,580 m** |
| **Sus pivotes no se pueden acercar a menos de** | **1,355 m** |

O sea: al agente se le pedía **frenar a 1,2 m cuando sus colliders se tocan a 1,355 m**. Le
estábamos pidiendo algo físicamente imposible. Consecuencia en cadena:

1. El agente **nunca** daba el destino por alcanzado, así que seguía acelerando contra el jugador
   indefinidamente. Un `Rigidbody` cinemático empujando a un `CharacterController` no se frena: lo
   que cede es el jugador, que Unity "despenetra" apartándolo. Contra una pared o una esquina no
   hay a dónde apartarlo y **sale por el otro lado**. Esa es la presión que lo atravesaba.
2. El ataque quedaba en un **filo de 14 cm** (entre 1,355 y 1,5 m). Cualquier temblor —el jugador
   retrocediendo, la propia despenetración— lo sacaba del rango.
3. Y era pura suerte que el filo existiera: con una hitbox un poco más gorda (radio > 0,92 m) el
   rango de ataque queda **vacío** y el enemigo no puede atacar nunca, por mucho que se acerque.

**Arreglo** (`EnemyAI`): una propiedad `SeparacionDeCuerpos` (la suma de los dos radios) y
`DistanciaDeAtaque = max(attackRange, SeparacionDeCuerpos + 0,2)`. El rango de ataque **no puede
quedar vacío** por gorda que quede la hitbox del modelo, que era justo la fragilidad que lo rompió.
`stoppingDistance` ahora se deriva de ese mismo número, así que el agente frena donde el enemigo
puede pegar en vez de intentar meterse dentro del jugador.

Con las medidas de la escena: `DistanciaDeAtaque` = 1,555 m y `stoppingDistance` = 1,505 m, los dos
por encima de los 1,355 m de contacto. El agente ahora **sí** llega y frena.

`RadioPropio()` soporta las dos formas que hay en la escena: la cápsula del enemigo con modelo y la
**esfera** placeholder de los enemigos extra.

> Detalle que casi se me escapa: `SincronizarFormaDelAgente()` hace un *early return* cuando no hay
> `CapsuleCollider`, y los enemigos 2 y 3 tienen `SphereCollider`. Calcular la distancia de frenado
> ahí dentro habría dejado a esos dos con el `stoppingDistance` 0 por defecto de Unity — o sea el
> bug arreglado en uno de los tres enemigos. Por eso se calcula **fuera** de esa función. Lo cubre
> el caso CP-ENE-17.

## 2. El cono de visión se cerraba justo al acercarse

`PuedeVerAlJugador()` comparaba un vector **3D** contra un `transform.forward` horizontal. Los ojos
del enemigo están a 1,6 m y el pivote del jugador a ~1 m, así que la diferencia de altura **inflaba
el ángulo a medida que se acercaban**: a 1,4 m ya son 24° de los 50 disponibles, y más cerca se pasa
del cono y el enemigo **deja de ver al jugador que tiene encima**.

Eso lo mandaba a `Investigar`, donde camina hacia la última posición conocida —que es justo donde
está el jugador— empujándolo. Es literalmente el "solo me sigue y me empuja" del reporte.

**Arreglo**: el cono se mide en el plano horizontal. El raycast sigue siendo 3D (una pared a media
altura tiene que seguir tapando). Casos CP-ENE-18 y CP-ENE-19 (el segundo verifica que el arreglo
no volvió el cono omnidireccional).

## 3. Los clips de ataque no tienen Animation Event

`swiping.fbx` **no tiene ningún** Animation Event (verificado: cero ocurrencias de `m_Events` y de
`OnAttackHit` en su `.meta`). `EnemyAI.OnAttackHit()` nunca se llamaba desde la animación, así que
el daño caía solo por el respaldo de `ActualizarGolpePendiente()`: un temporizador ciego de medio
segundo que no tiene nada que ver con el momento en que la garra toca. El golpe se sentía
desconectado del zarpazo, y encima cada enemigo dejaba un warning en consola pidiendo este paso.

**Arreglo**: `Assets/Scripts/Editor/EventoGolpeSetup.cs`, menú **Between Metals > Enemigos >
"Agregar Animation Event de golpe a los ataques"**. Agrega `OnAttackHit` a `swiping`, `punch`,
`jump_attack` y `jump_attack_alt`, es idempotente, y `BuildEnemySetup` ya lo incluye en su secuencia.

El evento queda al 40 % del clip, que es una **estimación** razonable para un zarpazo, no una
medición: conviene abrir el clip en la ventana de Animation y moverlo al frame donde la garra toca
de verdad. Lo importante es que el evento exista y caiga dentro del swing.

El respaldo de `EnemyAI` se deja como está: es la red que hace que el enemigo no quede inofensivo si
alguien agrega un ataque nuevo sin evento.

## 4. Los enemigos 2 y 3 no tienen Animator, y fallaba en silencio

La escena sigue teniendo **un solo** hijo `Modelo`: los enemigos 2 y 3 son la esfera placeholder,
sin modelo ni `Animator`. `EnemyAnimator` no encontraba Animator y **no hacía nada, sin decir nada**
— el síntoma era "el enemigo me persigue pero nunca lo veo atacar", sin una línea en consola que lo
explicara.

**Arreglo**: `EnemyAnimator` avisa una vez, nombrando el menú que lo resuelve. No desactiva nada: el
daño no depende de la animación, así que un enemigo sin modelo sigue siendo peligroso.

Esto es el paso manual que quedó pendiente del PR #72 y **sigue pendiente** (ver sección 6).

## 5. La hitbox del enemigo está mal medida (pendiente, es dato de escena)

De paso, la cápsula del enemigo en la escena:

- `m_Center: {x: -0.063, y: 0.519, z: 0.015}` — **no está centrada en el eje del agente**. Una
  hitbox descentrada **rota con el enemigo**, así que el solape con el jugador cambia según hacia
  dónde mire. Eso también hace que los golpes del jugador (`PlayerCombat`) sean inconsistentes.
- Radio 0,775 con alto 1,038: como el radio pasa de la mitad del alto, **Unity la trata como una
  esfera** de 0,775. Su base queda en y = −0,256, o sea **26 cm por debajo de los pies**, metida en
  el piso.

`EnemySetupFixer` ya corrige las dos cosas (centra en el eje y recorta el radio a la mitad del
alto): simplemente **nunca se corrió** sobre este enemigo, o lo hizo una versión anterior. Se
arregla con el mismo paso de Editor de la sección 6.

---

## 6. ⚠️ Paso manual pendiente (necesita el Editor)

Unity estaba abierto durante este trabajo (`Temp/UnityLockfile` presente), así que no se pudo correr
en batch. Hay que correr **una vez** en el Editor:

1. Abrir `Assets/Scenes/Prototype.unity`.
2. Menú **Between Metals > Enemigos > Build + Setup completo**, que ahora hace los cuatro pasos:
   - `AnimacionControllerBuilder` — el controller;
   - **`EventoGolpeSetup`** — el Animation Event del golpe (sección 3);
   - **`EnemigoModeloDuplicador`** — el modelo y las animaciones del enemigo 1 en los enemigos 2 y 3
     (sección 4);
   - `EnemySetupFixer` — la hitbox centrada y bien medida (sección 5).
3. Revisar que `Enemigo_02` y `Enemigo_03` tengan su hijo `Modelo` y se animen.
4. **Ctrl+S** y commitear `Prototype.unity`.

**Lo de código (secciones 1 y 2) ya funciona sin ningún paso manual.** Es decir: el empuje a través
de paredes y piso está arreglado desde este commit; la animación de ataque de los enemigos 2 y 3
necesita el paso de arriba, porque hoy no tienen Animator.

---

## 7. Verificación

**Compilación**: `Assembly-CSharp` (65 archivos) y `Assembly-CSharp-Editor` (42) con el Roslyn que
trae Unity, mismas referencias y `DefineConstants` que los `.csproj`. **0 errores.**

**Self-test nuevo**: `Assets/Scripts/Editor/Tests/EnemyRangoDeAtaqueSelfTest.cs`, menú
**Between Metals > Tests > Enemigo: rango de ataque**. 8 casos `CP-ENE-14..21`, con las medidas
reales de la escena y no con un escenario inventado:

| Caso | Qué cubre |
|---|---|
| CP-ENE-14 | Con la hitbox de la escena, el jugador a distancia de contacto **sí** está en rango de golpe (el caso que fallaba) |
| CP-ENE-15 | Con una hitbox enorme (3 m), el rango de ataque no queda vacío |
| CP-ENE-16 | `stoppingDistance` queda entre la separación de los cuerpos y la distancia de ataque |
| CP-ENE-17 | Lo mismo para el enemigo **esfera**, que no pasa por `SincronizarFormaDelAgente` |
| CP-ENE-18 | A quemarropa el enemigo no pierde de vista al jugador |
| CP-ENE-19 | …pero a 90° sigue sin verlo (el arreglo no volvió el cono omnidireccional) |
| CP-ENE-20 | Ignorar la colisión no se rompe en una escena sin jugador |
| CP-ENE-21 | Los clips de ataque tienen el Animation Event (avisa si falta el paso de Editor) |

Los `CP-ENE-08..13` que ya existían **siguen valiendo**: su fixture no tiene colliders, así que
`DistanciaDeAtaque` cae en `attackRange` = 1,5 como antes y las expectativas no cambian.

### Lo que NO se verificó

- **No se dio Play.** El Editor estaba ocupado, así que el arreglo está verificado por aritmética
  (las medidas de la escena contra los números que el código calcula ahora) y por los self-tests,
  **no jugando**. Lo que hay que confirmar a mano: que el enemigo ya no empuje, que el zarpazo se
  vea conectar y que quite 10 de vida.
- Los self-tests **tampoco se ejecutaron**, por lo mismo: compilan, pero hay que correrlos desde el
  menú.
- La **causa del "no hace daño"** quedó explicada como consecuencia de la sección 1 (el enemigo
  nunca se estabilizaba en el rango de ataque) y de la 2 (se iba a Investigar al acercarse). Es una
  deducción sólida sobre medidas reales, pero **no la observé fallar en ejecución**. Si después de
  este arreglo y del paso manual el daño sigue sin aplicarse, el siguiente sitio donde mirar es el
  `golpeDeRespaldoSinEvento` / `tiempoMaximoEsperaGolpe` contra el largo real del clip `swiping`.

---

## 8. Archivos

### Nuevos
```
Assets/Scripts/Editor/EventoGolpeSetup.cs                   Animation Event del golpe
Assets/Scripts/Editor/Tests/EnemyRangoDeAtaqueSelfTest.cs   8 casos CP-ENE-14..21
Docs/arreglo-ataque-enemigos.md                             este documento
```

### Modificados
```
Assets/Scripts/Systems/EnemyAI.cs          SeparacionDeCuerpos / DistanciaDeAtaque,
                                           stoppingDistance derivado, cono de vision horizontal,
                                           IgnorarColisionConJugador
Assets/Scripts/Systems/EnemyAnimator.cs    avisa cuando no hay Animator
Assets/Scripts/Editor/BuildEnemySetup.cs   + EventoGolpeSetup en la secuencia
```
