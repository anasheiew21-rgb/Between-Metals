using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

// Autotest de editor de EnemyAI (HU-08, RF10, RF11, issue #58). Casos CP-ENE-01..07.
// Abre Prototype.unity para probar contra el enemigo y los waypoints reales, pero NUNCA la
// guarda. Como en modo Editor sin Play Unity no llama Start() por si solo, este test lo invoca
// por reflexion (mismo truco que EnemyNavigationSelfTest usa para leer el campo 'waypoints').
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyAISelfTest.RunAllAndExit -logFile <log>
public static class EnemyAISelfTest
{
    const string Tag = "[EnemyAISelfTest]";
    const string EscenaPrototipo = "Assets/Scenes/Prototype.unity";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/EnemyAI")]
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

        EditorSceneManager.OpenScene(EscenaPrototipo, OpenSceneMode.Single);
        NavMeshRuntimeBuilder.EnsureBuilt();

        EnemyAI enemigo = UnityEngine.Object.FindAnyObjectByType<EnemyAI>();
        if (enemigo == null)
        {
            Debug.LogError($"{Tag} no hay ningun EnemyAI en la escena, no se puede probar nada");
            Debug.Log($"{Tag} RESULT: 0 passed, 1 failed");
            return false;
        }

        // Replica la inicializacion real: Start() llama a InicializarNavegacion() y busca al
        // jugador. En modo Editor sin Play, Unity no invoca Start() por si solo.
        InvokePrivateMethod(enemigo, "Start");
        enemigo.InicializarNavegacion();

        var temporales = new List<GameObject>();

        Run("CP-ENE-01", "Tras InicializarNavegacion(), el enemigo tiene un NavMeshAgent sobre el NavMesh y el Rigidbody es cinematico", () =>
        {
            NavMeshAgent agente = enemigo.GetComponent<NavMeshAgent>();
            if (agente == null) return "no se agrego ningun NavMeshAgent";

            // NavMeshAgent.isOnNavMesh (y Warp) dependen del tick interno de simulacion del
            // sistema de navegacion, que solo corre en Play Mode: en Editor/batch sin Play este
            // test abre la escena pero nunca la reproduce (igual que EnemyNavigationSelfTest), asi
            // que se verifica la ubicacion de forma geometrica (misma tecnica que CP-NAV-02) en
            // vez de confiar en isOnNavMesh, que aca siempre da falso aunque la posicion sea correcta.
            if (!agente.isOnNavMesh)
                Debug.LogWarning($"{Tag} CP-ENE-01 isOnNavMesh da falso (esperado fuera de Play Mode); se verifica por posicion");

            if (!NavMesh.SamplePosition(enemigo.transform.position, out NavMeshHit hit, 0.1f, NavMesh.AllAreas))
                return $"el enemigo no quedo sobre el NavMesh (posicion {enemigo.transform.position})";

            Rigidbody rb = enemigo.GetComponent<Rigidbody>();
            if (rb == null) return "no hay Rigidbody en el enemigo";
            if (!rb.isKinematic) return "el Rigidbody no quedo cinematico";
            return null;
        });

        Run("CP-ENE-02", "Con los waypoints de la escena, CantidadWaypointsValidos es 1 y EstaDeambulando es verdadero", () =>
        {
            if (enemigo.CantidadWaypointsValidos != 1)
                return $"CantidadWaypointsValidos = {enemigo.CantidadWaypointsValidos} (se esperaba 1)";
            if (!enemigo.EstaDeambulando) return "EstaDeambulando es falso (se esperaba verdadero)";
            return null;
        });

        Run("CP-ENE-03", "20 llamadas a ElegirPuntoDeambular devuelven puntos sobre el NavMesh con camino completo", () =>
        {
            for (int i = 0; i < 20; i++)
            {
                if (!enemigo.ElegirPuntoDeambular(out Vector3 punto))
                    return $"intento {i}: no se encontro ningun punto valido dentro del radio de deambular";

                if (!NavMesh.SamplePosition(punto, out NavMeshHit hit, 0.1f, NavMesh.AllAreas))
                    return $"intento {i}: el punto {punto} no quedo sobre el NavMesh";

                NavMeshPath path = new NavMeshPath();
                bool completo = NavMesh.CalculatePath(enemigo.transform.position, punto, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
                if (!completo) return $"intento {i}: camino incompleto del enemigo hacia {punto}";
            }
            return null;
        });

        Run("CP-ENE-04", "La velocidad del agente es patrolSpeed en patrulla y chaseSpeed en persecucion", () =>
        {
            NavMeshAgent agente = enemigo.GetComponent<NavMeshAgent>();
            float patrolSpeed = (float)GetPrivateField(enemigo, "patrolSpeed");
            float chaseSpeed = (float)GetPrivateField(enemigo, "chaseSpeed");

            SetPrivateField(enemigo, "estado", EnemyAI.Estado.Patrulla);
            InvokePrivateMethod(enemigo, "Patrullar");
            if (!Mathf.Approximately(agente.speed, patrolSpeed))
                return $"en patrulla, agent.speed = {agente.speed} (se esperaba {patrolSpeed})";

            InvokePrivateMethod(enemigo, "Perseguir", true);
            if (!Mathf.Approximately(agente.speed, chaseSpeed))
                return $"en persecucion, agent.speed = {agente.speed} (se esperaba {chaseSpeed})";

            return null;
        });

        Run("CP-ENE-05", "Tabla de verdad de DebeInvestigarPorRuido (RF11)", () =>
        {
            const float radio = 12f;
            const float umbral = 6f;

            if (!EnemyAI.DebeInvestigarPorRuido(8f, 7f, radio, umbral, false))
                return "corriendo dentro del radio y sin que lo vea deberia dar verdadero";

            if (EnemyAI.DebeInvestigarPorRuido(8f, 3f, radio, umbral, false))
                return "caminando dentro del radio deberia dar falso";

            if (EnemyAI.DebeInvestigarPorRuido(20f, 7f, radio, umbral, false))
                return "corriendo fuera del radio deberia dar falso";

            if (EnemyAI.DebeInvestigarPorRuido(8f, 7f, radio, umbral, true))
                return "si lo ve deberia dar falso";

            return null;
        });

        Run("CP-ENE-06", "Con 2 waypoints temporales alcanzables, la ruta tiene 2 validos y EstaDeambulando es falso", () =>
        {
            object waypointsOriginales = GetPrivateField(enemigo, "waypoints");

            if (!enemigo.ElegirPuntoDeambular(out Vector3 puntoA)) return "no se pudo elegir el primer punto temporal";
            if (!enemigo.ElegirPuntoDeambular(out Vector3 puntoB)) return "no se pudo elegir el segundo punto temporal";

            GameObject wpA = new GameObject("TempWaypoint_A_EnemyAISelfTest");
            wpA.transform.position = puntoA;
            GameObject wpB = new GameObject("TempWaypoint_B_EnemyAISelfTest");
            wpB.transform.position = puntoB;
            temporales.Add(wpA);
            temporales.Add(wpB);

            SetPrivateField(enemigo, "waypoints", new[] { wpA.transform, wpB.transform });
            enemigo.InicializarNavegacion();

            string error = null;
            if (enemigo.CantidadWaypointsValidos != 2)
                error = $"CantidadWaypointsValidos = {enemigo.CantidadWaypointsValidos} (se esperaba 2)";
            else if (enemigo.EstaDeambulando)
                error = "EstaDeambulando es verdadero (se esperaba falso con 2 waypoints validos)";

            // Se restaura la ruta original de la escena para no afectar corridas posteriores.
            SetPrivateField(enemigo, "waypoints", waypointsOriginales);
            enemigo.InicializarNavegacion();

            return error;
        });

        Run("CP-ENE-07", "InicializarNavegacion() llamado dos veces no agrega un segundo NavMeshAgent", () =>
        {
            enemigo.InicializarNavegacion();
            enemigo.InicializarNavegacion();
            int cantidad = enemigo.GetComponents<NavMeshAgent>().Length;
            return cantidad == 1 ? null : $"hay {cantidad} NavMeshAgent en el enemigo (se esperaba 1)";
        });

        foreach (GameObject temporal in temporales)
        {
            UnityEngine.Object.DestroyImmediate(temporal);
        }

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
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

    static object InvokePrivateMethod(object obj, string nombre, params object[] args)
    {
        MethodInfo metodo = obj.GetType().GetMethod(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        return metodo?.Invoke(obj, args);
    }
}
