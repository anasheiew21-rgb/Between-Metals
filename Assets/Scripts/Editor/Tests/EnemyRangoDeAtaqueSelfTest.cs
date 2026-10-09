using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Self-test de la geometria del ataque del enemigo: la distancia a la que ataca, la distancia a la
// que frena el agente y el cono de vision. Mismo patron de menu que los demas autotests del
// proyecto (no hay asmdef de test todavia).
//
// Cubre los tres bugs que hacian que el enemigo "persiga al jugador, lo atraviese por las paredes y
// nunca le pegue":
//
//  1. El rango de ataque se media entre PIVOTES, ignorando que cada cuerpo ocupa lugar. Con la
//     hitbox del enemigo de la escena (0.78 m de radio) y el jugador (0.5 + 0.08 de skin), sus
//     pivotes no se pueden acercar a menos de ~1.36 m, asi que el ataque vivia en un filo de 14 cm
//     y con una hitbox un poco mas gorda el rango quedaba VACIO: el enemigo no podia atacar nunca.
//  2. agent.stoppingDistance era attackRange - 0.3, un numero que no sabia nada de esos cuerpos:
//     le pedia al agente frenar a 1.2 m cuando sus colliders se tocan a 1.36 m, o sea meterse
//     dentro del jugador. El agente nunca daba el destino por alcanzado y empujaba a fondo para
//     siempre; esa presion es la que expulsaba al jugador por el piso y las paredes.
//  3. El cono de vision se media en 3D, asi que la diferencia de altura entre los ojos del enemigo
//     y el pivote del jugador inflaba el angulo al acercarse: a quemarropa el enemigo dejaba de
//     "ver" al jugador que tenia encima y se iba a Investigar en vez de atacar.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyRangoDeAtaqueSelfTest.RunAllAndExit -logFile <log>
public static class EnemyRangoDeAtaqueSelfTest
{
    const string Tag = "[EnemyRangoDeAtaqueSelfTest]";

    // Medidas reales del enemigo y del jugador de Prototype.unity, para que los casos prueben la
    // escena que existe y no un escenario inventado.
    const float RadioHitboxEnemigoEnEscena = 0.7750302f;
    const float AltoHitboxEnemigoEnEscena = 1.0380648f;
    const float RadioJugadorEnEscena = 0.5f;
    const float SkinJugadorEnEscena = 0.08f;

    static int passed;
    static int failed;

    public static void RunAllAndExit()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Between Metals/Tests/Enemigo: rango de ataque Self-Test")]
    public static void Run()
    {
        RunAll();
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        var temporales = new List<GameObject>();

        try
        {
            Run("CP-ENE-14", "Con la hitbox de la escena, el jugador a distancia de contacto SI esta en rango de golpe", () =>
            {
                // Este es el caso que fallaba: el enemigo pegado al jugador, tan cerca como sus
                // colliders se lo permiten, tenia que poder pegarle.
                Fixture fx = Fixture.DeLaEscena(temporales);
                float contacto = fx.ia.SeparacionDeCuerpos;
                fx.PonerJugadorAlFrente(contacto);

                if (!fx.ia.JugadorEnRangoDeGolpe())
                    return $"a {contacto:0.00} m (cuerpos tocandose) el golpe no conecta; " +
                           $"DistanciaDeAtaque = {fx.ia.DistanciaDeAtaque:0.00}";
                return null;
            });

            Run("CP-ENE-15", "DistanciaDeAtaque nunca queda por debajo de lo que permiten los dos cuerpos", () =>
            {
                // Con una hitbox deliberadamente enorme (3 m de radio), attackRange 1.5 seria
                // inalcanzable: antes el rango de ataque quedaba vacio y el enemigo era inofensivo.
                Fixture fx = Fixture.ConHitbox(temporales, radioEnemigo: 3f, altoEnemigo: 6f);

                if (fx.ia.DistanciaDeAtaque < fx.ia.SeparacionDeCuerpos)
                    return $"DistanciaDeAtaque ({fx.ia.DistanciaDeAtaque:0.00}) es MENOR que la separacion " +
                           $"de los cuerpos ({fx.ia.SeparacionDeCuerpos:0.00}): el rango de ataque esta vacio";

                fx.PonerJugadorAlFrente(fx.ia.SeparacionDeCuerpos);
                if (!fx.ia.JugadorEnRangoDeGolpe())
                    return "con una hitbox gigante el golpe sigue sin conectar a distancia de contacto";
                return null;
            });

            Run("CP-ENE-16", "El agente frena donde el enemigo puede atacar, no dentro del jugador", () =>
            {
                // El bug de la presion: stoppingDistance por debajo de la separacion de los cuerpos
                // es pedirle al agente algo fisicamente imposible, y entonces nunca frena.
                Fixture fx = Fixture.DeLaEscena(temporales);
                fx.ia.InicializarNavegacion();

                NavMeshAgent agente = fx.ia.GetComponent<NavMeshAgent>();
                if (agente == null) return "InicializarNavegacion() no dejo NavMeshAgent";

                if (agente.stoppingDistance < fx.ia.SeparacionDeCuerpos)
                    return $"stoppingDistance ({agente.stoppingDistance:0.00}) es menor que la separacion de " +
                           $"los cuerpos ({fx.ia.SeparacionDeCuerpos:0.00}): el agente va a empujar al jugador " +
                           "para siempre sin dar el destino por alcanzado";

                if (agente.stoppingDistance > fx.ia.DistanciaDeAtaque)
                    return $"stoppingDistance ({agente.stoppingDistance:0.00}) pasa la distancia de ataque " +
                           $"({fx.ia.DistanciaDeAtaque:0.00}): frenaria antes de poder pegar";
                return null;
            });

            Run("CP-ENE-17", "El enemigo esfera (sin CapsuleCollider) tambien frena antes de entrar en el jugador", () =>
            {
                // Los enemigos extra siguen siendo la esfera placeholder de EnemigosExtraBuilder.
                // SincronizarFormaDelAgente se va por un early return cuando no hay CapsuleCollider,
                // asi que la distancia de frenado tiene que calcularse FUERA de ella: si no, estos
                // dos enemigos se quedaban con el stoppingDistance 0 de Unity y entraban hasta el
                // centro del jugador, o sea el bug arreglado en uno de los tres enemigos nada mas.
                Fixture fx = Fixture.Esfera(temporales, radio: 0.5f);
                fx.ia.InicializarNavegacion();

                NavMeshAgent agente = fx.ia.GetComponent<NavMeshAgent>();
                if (agente == null) return "InicializarNavegacion() no dejo NavMeshAgent";

                if (agente.stoppingDistance < fx.ia.SeparacionDeCuerpos)
                    return $"stoppingDistance ({agente.stoppingDistance:0.00}) es menor que la separacion de " +
                           $"los cuerpos ({fx.ia.SeparacionDeCuerpos:0.00})";
                return null;
            });

            Run("CP-ENE-18", "El cono de vision se mide en horizontal: a quemarropa no pierde de vista al jugador", () =>
            {
                // Enemigo mirando a +Z con los ojos a 1.6 m; jugador a 30 cm al frente y con su
                // pivote a 1 m del piso. En 3D el angulo son ~79 grados, fuera del cono de 100/2;
                // en horizontal son 0 y lo ve, que es lo correcto cuando lo tiene encima.
                Fixture fx = Fixture.DeLaEscena(temporales);
                fx.ia.transform.position = Vector3.zero;
                fx.ia.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                fx.jugador.transform.position = new Vector3(0f, 1f, 0.3f);

                bool ve = (bool)Invocar(fx.ia, "PuedeVerAlJugador");
                return ve ? null : "no ve al jugador que tiene pegado adelante (el cono se midio en 3D)";
            });

            Run("CP-ENE-19", "Un jugador realmente de costado sigue quedando fuera del cono", () =>
            {
                // Contracara del caso anterior: el arreglo no puede volver el cono de vision
                // omnidireccional. A 90 grados del frente tiene que seguir sin verlo.
                Fixture fx = Fixture.DeLaEscena(temporales);
                fx.ia.transform.position = Vector3.zero;
                fx.ia.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                fx.jugador.transform.position = new Vector3(5f, 1f, 0f); // 90 grados a la derecha

                bool ve = (bool)Invocar(fx.ia, "PuedeVerAlJugador");
                return ve ? "ve a un jugador que esta a 90 grados, fuera de un cono de 100" : null;
            });

            Run("CP-ENE-20", "Ignorar la colision con el jugador no se rompe si todavia no hay jugador", () =>
            {
                // Un enemigo en una escena sin jugador (un test, una escena de arte) no puede tirar
                // una excepcion en Start por esto.
                var go = new GameObject("CP_ENE_SinJugador");
                temporales.Add(go);
                EnemyAI ia = go.AddComponent<EnemyAI>();

                Invocar(ia, "IgnorarColisionConJugador");
                return null;
            });

            Run("CP-ENE-21", "Los clips de ataque tienen el Animation Event OnAttackHit", () =>
            {
                // No es un bug del codigo sino un paso de Editor que faltaba: sin el evento, EnemyAI
                // aplica el dano por un temporizador ciego de medio segundo en vez de en el frame en
                // que la garra toca. Este caso avisa si falta.
                var faltantes = new List<string>();
                foreach (string clip in EventoGolpeSetup.ClipsConsiderados)
                {
                    if (!EventoGolpeSetup.TieneEventoDeGolpe(clip)) faltantes.Add(clip);
                }

                return faltantes.Count == 0
                    ? null
                    : "sin Animation Event OnAttackHit: " + string.Join(", ", faltantes) +
                      ". Corré Between Metals > Enemigos > \"Agregar Animation Event de golpe a los ataques\"";
            });
        }
        finally
        {
            foreach (GameObject go in temporales)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // ------------------------------------------------------------------ fixture

    class Fixture
    {
        public EnemyAI ia;
        public GameObject jugador;

        /// <summary>Enemigo con la hitbox real de Prototype.unity y un jugador con su CharacterController.</summary>
        public static Fixture DeLaEscena(List<GameObject> temporales)
        {
            return ConHitbox(temporales, RadioHitboxEnemigoEnEscena, AltoHitboxEnemigoEnEscena);
        }

        public static Fixture ConHitbox(List<GameObject> temporales, float radioEnemigo, float altoEnemigo)
        {
            return Armar(temporales, go =>
            {
                CapsuleCollider capsula = go.AddComponent<CapsuleCollider>();
                capsula.radius = radioEnemigo;
                capsula.height = altoEnemigo;
                capsula.direction = 1;
            });
        }

        /// <summary>Enemigo con la esfera placeholder que les deja EnemigosExtraBuilder a los enemigos 2 y 3.</summary>
        public static Fixture Esfera(List<GameObject> temporales, float radio)
        {
            return Armar(temporales, go => go.AddComponent<SphereCollider>().radius = radio);
        }

        static Fixture Armar(List<GameObject> temporales, Action<GameObject> ponerCollider)
        {
            var enemigoGO = new GameObject("CP_ENE_Enemigo");
            temporales.Add(enemigoGO);

            // El collider va ANTES del EnemyAI: RadioPropio() lo busca con GetComponent, y en esta
            // prueba no hay un Start que vuelva a mirar.
            ponerCollider(enemigoGO);

            EnemyAI ia = enemigoGO.AddComponent<EnemyAI>(); // [RequireComponent] agrega el Rigidbody

            var jugadorGO = new GameObject("CP_ENE_Jugador");
            temporales.Add(jugadorGO);
            CharacterController cc = jugadorGO.AddComponent<CharacterController>();
            cc.radius = RadioJugadorEnEscena;
            cc.skinWidth = SkinJugadorEnEscena;
            cc.height = 2f;

            // Fuera de Play Mode Unity no llama Start, asi que las referencias al jugador se ponen
            // a mano (igual que hace EnemyCombatAudioSelfTest.Fixture).
            Asignar(ia, "jugador", jugadorGO.transform);
            Asignar(ia, "controladorJugador", cc);

            return new Fixture { ia = ia, jugador = jugadorGO };
        }

        /// <summary>Pone al jugador a esa distancia, justo al frente del enemigo.</summary>
        public void PonerJugadorAlFrente(float distancia)
        {
            jugador.transform.position = ia.transform.position + ia.transform.forward * distancia;
        }
    }

    // ------------------------------------------------------------------ reflexion

    const BindingFlags Privados = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static void Asignar(object objetivo, string campo, object valor)
    {
        FieldInfo info = objetivo.GetType().GetField(campo, Privados);
        if (info == null) throw new MissingFieldException(objetivo.GetType().Name, campo);
        info.SetValue(objetivo, valor);
    }

    static object Invocar(object objetivo, string metodo)
    {
        MethodInfo info = objetivo.GetType().GetMethod(metodo, Privados);
        if (info == null) throw new MissingMethodException(objetivo.GetType().Name, metodo);

        try
        {
            return info.Invoke(objetivo, null);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw e.InnerException;
        }
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepcion {e.GetType().Name}: {e.Message}";
        }

        if (error == null)
        {
            passed++;
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description} - {error}");
        }
    }
}
