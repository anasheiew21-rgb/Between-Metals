using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Autotest de editor del combate sincronizado por Animation Event y del audio del enemigo
// (CP-ENE-08..13). No abre ninguna escena ni necesita NavMesh: arma un enemigo y un jugador
// temporales en memoria y le inyecta el objetivo por reflexion, en vez de llamar a Start() (que
// buscaria el PlayerStats real de la escena y crearia un NavMeshAgent que fuera de Play Mode nunca
// se registra). Mismo truco de reflexion que EnemyAISelfTest y GameManagerSelfTest.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyCombatAudioSelfTest.RunAllAndExit -logFile <log>
public static class EnemyCombatAudioSelfTest
{
    const string Tag = "[EnemyCombatAudioSelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/EnemyAI combate y audio")]
    static void RunFromMenu()
    {
        RunAll();
    }

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

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        var temporales = new List<GameObject>();

        try
        {
            Run("CP-ENE-08", "Atacar() no aplica dano: deja el golpe pendiente para el frame de impacto", () =>
            {
                Fixture fx = Fixture.Crear(temporales, new Vector3(0f, 0f, 1f));

                InvokePrivate(fx.ia, "Atacar");

                if (!fx.ia.TieneGolpePendiente) return "no quedo ningun golpe pendiente despues de Atacar()";
                if (fx.stats.CurrentHealth < fx.stats.MaxHealth)
                    return $"Atacar() ya aplico dano por si solo (vida {fx.stats.CurrentHealth})";
                return null;
            });

            Run("CP-ENE-09", "OnAttackHit() con el jugador en rango y de frente aplica el dano una sola vez", () =>
            {
                Fixture fx = Fixture.Crear(temporales, new Vector3(0f, 0f, 1f));
                float vidaInicial = fx.stats.CurrentHealth;

                InvokePrivate(fx.ia, "Atacar");
                fx.ia.OnAttackHit();

                float esperada = vidaInicial - fx.ia.DanoDeAtaque;
                if (!Mathf.Approximately(fx.stats.CurrentHealth, esperada))
                    return $"vida {fx.stats.CurrentHealth} (se esperaba {esperada})";

                // El evento puede llegar repetido (clip con dos eventos, o transicion que reentra):
                // el golpe ya se resolvio, no tiene que volver a pegar.
                fx.ia.OnAttackHit();
                if (!Mathf.Approximately(fx.stats.CurrentHealth, esperada))
                    return $"un segundo OnAttackHit() volvio a pegar (vida {fx.stats.CurrentHealth})";

                if (fx.ia.TieneGolpePendiente) return "el golpe sigue marcado como pendiente despues de resolverse";
                return null;
            });

            Run("CP-ENE-10", "El golpe falla si el jugador se salio del rango o quedo fuera del angulo de ataque", () =>
            {
                Fixture lejos = Fixture.Crear(temporales, new Vector3(0f, 0f, 6f));
                InvokePrivate(lejos.ia, "Atacar");
                lejos.ia.OnAttackHit();
                if (lejos.stats.CurrentHealth < lejos.stats.MaxHealth)
                    return $"pego a 6 m con attackRange 1.5 (vida {lejos.stats.CurrentHealth})";

                Fixture atras = Fixture.Crear(temporales, new Vector3(0f, 0f, -1f));
                InvokePrivate(atras.ia, "Atacar");
                atras.ia.OnAttackHit();
                if (atras.stats.CurrentHealth < atras.stats.MaxHealth)
                    return $"pego a un jugador que estaba exactamente atras (vida {atras.stats.CurrentHealth})";

                return null;
            });

            Run("CP-ENE-11", "Sin Animation Event, el respaldo resuelve el golpe una vez; desactivado, no pega", () =>
            {
                Fixture conRespaldo = Fixture.Crear(temporales, new Vector3(0f, 0f, 1f));
                float vidaInicial = conRespaldo.stats.CurrentHealth;

                InvokePrivate(conRespaldo.ia, "Atacar");
                AgotarEsperaDelGolpe(conRespaldo.ia);

                float esperada = vidaInicial - conRespaldo.ia.DanoDeAtaque;
                if (!Mathf.Approximately(conRespaldo.stats.CurrentHealth, esperada))
                    return $"con respaldo activado, vida {conRespaldo.stats.CurrentHealth} (se esperaba {esperada})";
                if (conRespaldo.ia.TieneGolpePendiente) return "el golpe quedo pendiente despues del respaldo";

                // Si el Animation Event llega tarde, despues de que el respaldo ya cobro, no cobra de nuevo.
                conRespaldo.ia.OnAttackHit();
                if (!Mathf.Approximately(conRespaldo.stats.CurrentHealth, esperada))
                    return $"el evento tardio volvio a pegar (vida {conRespaldo.stats.CurrentHealth})";

                Fixture sinRespaldo = Fixture.Crear(temporales, new Vector3(0f, 0f, 1f));
                SetPrivateField(sinRespaldo.ia, "golpeDeRespaldoSinEvento", false);
                InvokePrivate(sinRespaldo.ia, "Atacar");
                AgotarEsperaDelGolpe(sinRespaldo.ia);

                if (sinRespaldo.stats.CurrentHealth < sinRespaldo.stats.MaxHealth)
                    return $"con el respaldo desactivado igual aplico dano (vida {sinRespaldo.stats.CurrentHealth})";

                return null;
            });

            Run("CP-ENE-12", "ElegirClipAleatorio y SiguienteIntervaloRugido toleran arreglos vacios, huecos y min/max invertidos", () =>
            {
                if (EnemyAI.ElegirClipAleatorio(null) != null) return "con null deberia devolver null";
                if (EnemyAI.ElegirClipAleatorio(new AudioClip[0]) != null) return "con un arreglo vacio deberia devolver null";
                if (EnemyAI.ElegirClipAleatorio(new AudioClip[3]) != null) return "con un arreglo de solo huecos deberia devolver null";

                AudioClip a = AudioClip.Create("CP_ENE_rugido_a", 64, 1, 8000, false);
                AudioClip b = AudioClip.Create("CP_ENE_rugido_b", 64, 1, 8000, false);
                AudioClip[] conHuecos = { null, a, null, b };

                bool saleA = false;
                bool saleB = false;
                for (int i = 0; i < 100; i++)
                {
                    AudioClip elegido = EnemyAI.ElegirClipAleatorio(conHuecos);
                    if (elegido == null) return $"intento {i}: devolvio null habiendo 2 clips validos";
                    if (elegido == a) saleA = true;
                    else if (elegido == b) saleB = true;
                    else return $"intento {i}: devolvio un clip que no estaba en el arreglo";
                }
                if (!saleA || !saleB) return "en 100 intentos no salieron los dos clips: la eleccion no es aleatoria";

                for (int i = 0; i < 50; i++)
                {
                    float normal = EnemyAI.SiguienteIntervaloRugido(4f, 9f);
                    if (normal < 4f || normal > 9f) return $"intervalo {normal} fuera de [4, 9]";

                    float invertido = EnemyAI.SiguienteIntervaloRugido(9f, 4f);
                    if (invertido < 4f || invertido > 9f) return $"con min/max invertidos dio {invertido}, fuera de [4, 9]";

                    float negativo = EnemyAI.SiguienteIntervaloRugido(-5f, 2f);
                    if (negativo < 0f || negativo > 2f) return $"con un minimo negativo dio {negativo}, fuera de [0, 2]";
                }

                return null;
            });

            Run("CP-ENE-13", "PlayerStats.TakeDamage con hurtSound asignado y sin AudioSource aplica el dano sin romperse", () =>
            {
                Fixture fx = Fixture.Crear(temporales, new Vector3(0f, 0f, 1f));
                SetPrivateField(fx.stats, "hurtSound", AudioClip.Create("CP_ENE_dolor", 64, 1, 8000, false));

                float vidaInicial = fx.stats.CurrentHealth;
                fx.stats.TakeDamage(7f);

                if (!Mathf.Approximately(fx.stats.CurrentHealth, vidaInicial - 7f))
                    return $"vida {fx.stats.CurrentHealth} (se esperaba {vidaInicial - 7f})";
                return null;
            });
        }
        finally
        {
            foreach (GameObject temporal in temporales)
            {
                if (temporal != null) UnityEngine.Object.DestroyImmediate(temporal);
            }
        }

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Enemigo + jugador en memoria, con el objetivo ya inyectado (sin pasar por Start()).
    // El enemigo queda en el origen mirando +Z, asi que la posicion del jugador define si el golpe
    // cae dentro del rango y del angulo de ataque.
    class Fixture
    {
        public EnemyAI ia;
        public PlayerStats stats;

        public static Fixture Crear(List<GameObject> temporales, Vector3 posicionJugador)
        {
            GameObject enemigoGO = new GameObject("CP_ENE_Enemigo") { hideFlags = HideFlags.None };
            temporales.Add(enemigoGO);
            EnemyAI ia = enemigoGO.AddComponent<EnemyAI>(); // [RequireComponent] agrega el Rigidbody

            GameObject jugadorGO = new GameObject("CP_ENE_Jugador") { hideFlags = HideFlags.None };
            temporales.Add(jugadorGO);
            jugadorGO.transform.position = posicionJugador;
            PlayerStats stats = jugadorGO.AddComponent<PlayerStats>();

            // Fuera de Play Mode Unity no llama Awake, y sin el PlayerStats arranca con 0 de vida
            // (EstaViva en false): TakeDamage se iria por el guard y el test probaria nada.
            InvokePrivate(stats, "Awake");

            SetPrivateField(ia, "statsJugador", stats);
            SetPrivateField(ia, "jugador", jugadorGO.transform);

            return new Fixture { ia = ia, stats = stats };
        }
    }

    // Lleva la espera del golpe pendiente mas alla de tiempoMaximoEsperaGolpe y corre un "frame" de
    // la vigilancia. No se puede confiar en Time.deltaTime fuera de Play Mode, de ahi el empujon.
    static void AgotarEsperaDelGolpe(EnemyAI ia)
    {
        float maximo = (float)GetPrivateField(ia, "tiempoMaximoEsperaGolpe");
        SetPrivateField(ia, "tiempoEsperandoGolpe", maximo + 1f);
        InvokePrivate(ia, "ActualizarGolpePendiente");
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            error = $"excepcion {e.InnerException.GetType().Name}: {e.InnerException.Message}";
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

    static object GetPrivateField(object obj, string nombre)
    {
        FieldInfo campo = obj.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        return campo?.GetValue(obj);
    }

    static void SetPrivateField(object obj, string nombre, object valor)
    {
        FieldInfo campo = obj.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        campo?.SetValue(obj, valor);
    }

    static object InvokePrivate(object obj, string nombre, params object[] args)
    {
        MethodInfo metodo = obj.GetType().GetMethod(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        return metodo?.Invoke(obj, args);
    }
}
